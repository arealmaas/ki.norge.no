# SQL Server migration plan

Switch CMS from SQLite + Litestream to Azure SQL Database. Driver: SQLite single-writer model causes lock contention in Umbraco backoffice + Litestream WAL checkpoints, hit at low load. Window: pre-launch, before Sara enters real content.

## Decision summary

- **Target**: Azure SQL Database, Standard S0 tier (~150 NOK/month, 10 DTU, 250 GB max)
- **Region**: Norway East (same as Container Apps env, no cross-region latency)
- **Auth**: SQL authentication initially, migrate to managed identity in a follow-up PR
- **Network**: Public endpoint with "Allow Azure services" firewall (simple, secure enough for MVP). Private endpoint is a future hardening step.
- **Migration strategy**: Clean install. All current content is seeder-generated placeholder; the seeder recreates everything. Sara hasn't entered real content yet.

## Scope (~3 hours of work, deployable in one go)

### Phase 1: Provision Azure SQL (30 min)

```bash
# Server
az sql server create \
  --resource-group ki-norge \
  --name kinorgesql \
  --location norwayeast \
  --admin-user umbracoadmin \
  --admin-password <generated-strong-pw>

# Database
az sql db create \
  --resource-group ki-norge \
  --server kinorgesql \
  --name umbracokinorge \
  --service-objective S0 \
  --backup-storage-redundancy Local

# Firewall: allow Azure-hosted services (Container Apps egress)
az sql server firewall-rule create \
  --resource-group ki-norge \
  --server kinorgesql \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Add admin password as Container App secret
az containerapp secret set -g ki-norge -n ki-norge-cms \
  --secrets "sql-admin-password=<pw>"
```

Cost: S0 ~15 USD/month, idle: same (no auto-pause on Standard tier; consider Serverless GP_S_Gen5_1 if idle hours matter).

### Phase 2: Code changes (30 min)

1. **`apps/cms-umbraco/KiNorge.Cms.csproj`**: ensure `Microsoft.Data.SqlClient` package is included (Umbraco brings it in transitively, verify).

2. **`apps/cms-umbraco/appsettings.json`**: update connection string template to use SqlClient provider:
   ```json
   "ConnectionStrings": {
     "umbracoDbDSN": "",
     "umbracoDbDSN_ProviderName": "Microsoft.Data.SqlClient"
   }
   ```

3. **`apps/cms-umbraco/Dockerfile`**: remove Litestream binary install + entrypoint script.

4. **Delete**: `apps/cms-umbraco/litestream.yml`, `apps/cms-umbraco/entrypoint.sh`.

5. **`scripts/deploy-azure.sh`**:
   - Replace `ConnectionStrings__umbracoDbDSN` with SQL Server connection string referencing the secret:
     ```
     Server=tcp:kinorgesql.database.windows.net,1433;Initial Catalog=umbracokinorge;Persist Security Info=False;User ID=umbracoadmin;Password=$(SQL_ADMIN_PASSWORD);MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
     ```
   - Set `ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.SqlClient`
   - Add env var `SQL_ADMIN_PASSWORD=secretref:sql-admin-password`
   - Remove `LITESTREAM_AZURE_ACCOUNT_KEY` env var
   - Remove `litestream-azure-account-key` secret reference (keep secret in case of rollback need)

### Phase 3: Cutover deploy (45 min)

1. Tag current SQLite-based image as `:sqlite-rollback` for emergency revert
2. Backup current Litestream state (already in blob, but pull a fresh snapshot locally too)
3. Deploy new image with SQL Server connection string + LAUNCH_MODE unset (seeder runs)
4. Watch container logs:
   - Umbraco unattended install creates schema in Azure SQL
   - ContentTypeComposer creates content types
   - ContentSeeder fills demo content
5. Verify with smoke test
6. Re-set `LAUNCH_MODE=production` to lock seeder going forward
7. Verify backoffice login works end-to-end

### Phase 4: Cleanup (later, separate PR)

- Remove `umbraco-db` blob container in `kinorgestorage`
- Remove `litestream-azure-account-key` Container App secret
- Remove Litestream restore logic from `entrypoint.sh` (already deleted in Phase 2)
- Update `CLAUDE.md`: replace SQLite/Litestream architecture section with SQL Server
- Update memory entries (remove SQLite gotchas, add SQL Server config)
- Remove SQLite-specific dotnet packages from csproj (Microsoft.Data.Sqlite — Umbraco brings both, but no harm leaving)

### Phase 5: Hardening (post-launch)

- Switch from SQL auth to managed identity (eliminates the password rotation problem)
- Move SQL Server behind a private endpoint, add Container Apps to a vnet
- Set explicit point-in-time restore retention (default is 7 days on Standard)
- Set up failover group if multi-region resilience matters (probably not)

## Risk assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Schema migration fails on first SQL Server start | Low | High (no CMS) | Tag previous image as `:sqlite-rollback`. Roll back via `az containerapp update --image kinorgeacr.azurecr.io/ki-norge/cms:sqlite-rollback`. |
| Connection string typo | Medium | High (CMS won't start) | Test locally first against Azure SQL via VPN/firewall rule for dev IP. |
| Cost surprise | Low | Low | S0 is fixed ~150 NOK/month. Set Azure cost alert at 500 NOK/month for the resource group. |
| Lose any real content already entered | Low (none yet) | High | Verify with Sara that nothing is in prod yet beyond placeholder. |
| SQL auth password leak | Low | Medium | Stored as Container App secret (encrypted at rest). Rotate on managed identity migration. |

## Pre-flight checklist

Before kicking off the cutover, confirm:

- [ ] Sara knows there will be a brief CMS outage (~5 min)
- [ ] Sara has nothing she'll lose (verify content tree state)
- [ ] Azure PIM is active for Lars
- [ ] Cost alert is set up (or at least we've checked monthly forecast)
- [ ] The new SQL Server can be reached from Container Apps environment

## Smoke checks after cutover

```bash
# Health
curl https://ki-norge-cms.greentree-c9e56a64.norwayeast.azurecontainerapps.io/api/health/ready

# Delivery API content
curl "https://.../umbraco/delivery/api/v2/content?filter=contentType:artikkel&take=5" -H "Api-Key: ..."

# Backoffice login
# Manual: log in as admin@ki.norge.no / KiNorge2025!

# Two consecutive user creations (the original failure mode)
# Manual: create two users back-to-back, both should succeed without "table is locked"

# Existing prod smoke suite
bash scripts/smoke-test.sh --prod
```

## Rollback plan

If anything fails after deploy:

```bash
# Switch back to Litestream-based image
az containerapp update -g ki-norge -n ki-norge-cms \
  --image kinorgeacr.azurecr.io/ki-norge/cms:sqlite-rollback

# Restore connection string
az containerapp update -g ki-norge -n ki-norge-cms \
  --set-env-vars "ConnectionStrings__umbracoDbDSN=Data Source=/app/umbraco/Data/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True;Default Timeout=30" \
                  "ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite"

# Litestream will pick up the existing WAL on restart
```

The blob storage WAL data stays untouched until Phase 4, so rollback in the first week is essentially free.

## Open questions

1. Standard S0 vs Serverless GP_S_Gen5_1 — Serverless auto-pauses after 1h idle, saves money in dev but adds 30-60s wake-up cold start when first request hits. For a CMS with constant Litestream/background writes (now gone) and editor activity in business hours, **S0 is simpler**. Worth the 5 USD/month difference?
2. Should we provision a separate `umbracokinorge-staging` SQL DB for a future staging environment? Probably yes, so deploy script can target either.
3. Do we want to set up Azure SQL auditing immediately, or defer to hardening phase?

## When to do this

Recommend: this week, before next Figma push lands real content. Specifically before Sara is given an editor account and starts entering finished veiledning content.

Realistic earliest: any morning where you have 2 hours uninterrupted + Sara is free for a quick login test afterward.
