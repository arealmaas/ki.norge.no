# SQL Server migration: full plan

Switch CMS from SQLite + Litestream to Azure SQL Database.

**Driver**: SQLite single-writer + Litestream WAL checkpoint contention causes "database table is locked" errors at low concurrency (e.g. two consecutive user creations in the backoffice). `busy_timeout=30s` is a band-aid; the architectural fix is to use Umbraco's other officially supported database.

**Window**: Pre-launch, before Sara enters real production content. Schema + minimal placeholder data only — clean cutover is feasible.

---

## TL;DR

- Provision Azure SQL Database (Standard S0, Norway East, public endpoint with firewall)
- Switch Umbraco connection string + provider
- Remove Litestream from the container
- Clean install (the seeder rebuilds everything)
- Test suite at `tests/sql-migration/` runs before, during, and after the cutover

Total work: ~4-5 hours including thorough testing. Outage during cutover: ~10 min.

---

## Before — preparation work (~1 day before cutover)

These can all be done independently of the migration itself, ideally a day in advance so the cutover is purely "switch config + deploy."

### Communication

- [ ] Tell Sara: brief CMS outage, exact time window, "your placeholder content will be reset to seed defaults — verify with you that this is OK"
- [ ] Tell Marie: CMS will be unavailable for ~10 min during the window
- [ ] Decide cutover time: ideally Tuesday-Thursday morning, not Friday afternoon
- [ ] Pause any external Webhook integrations if they exist (none today, but check)

### Inventory the current state

Capture a snapshot of "what exists today" so we have something to compare against post-cutover. This becomes the spec the migration must match.

- [ ] Export current content tree counts to a baseline file:
  ```bash
  bash scripts/sql-migration/snapshot-baseline.sh > tests/sql-migration/baseline-prod.json
  ```
  Captures: count per content type, count per merkelapp, list of root nodes, total media count, list of users, current Umbraco version.
- [ ] Save current SQLite DB locally as belt-and-braces:
  ```bash
  bash scripts/restore-from-snapshot.sh dump <latest-gen> > /local/backup/Umbraco-$(date +%Y%m%d).sqlite.db
  ```

### Secrets and access

- [ ] Generate a strong SQL admin password (≥24 chars, mixed case + digits + symbols)
- [ ] Decide username convention — proposed `umbracoadmin`
- [ ] Add password to a real password manager (1Password / Bitwarden), not a sticky note
- [ ] Verify Azure PIM is active, will be active for the cutover window

### Cost guardrails

- [ ] Set Azure cost alert at 500 NOK/month for `ki-norge` resource group (catches surprises)
- [ ] Verify Standard S0 tier (~150 NOK/month, 10 DTU, 250 GB max) is in budget
- [ ] Decision: Standard S0 vs Serverless GP_S_Gen5_1?
  - **Standard S0**: predictable cost, no cold starts. Recommended for prod CMS that has constant low-level activity (background jobs, editor pings).
  - **Serverless**: auto-pause after 1h idle = ~50% cheaper for dev/staging, but cold start adds 30-60s to first request. Bad for an admin trying to log in unexpectedly.
  - **Pick Standard S0 for prod.** Serverless if/when we add staging.

### Network architecture decision

- [ ] **MVP path (this migration)**: public endpoint + firewall rule "Allow Azure services and resources to access this server" enabled + SQL auth. Container Apps connects over public internet but stays within Azure backbone. Acceptable for now.
- [ ] **Hardening path (post-launch)**: private endpoint + Container Apps in vnet + managed identity. Separate PR.

### Pre-write all tests

Test suite at `tests/sql-migration/` (see below). All written before the cutover. Each test has a clear name and self-explaining assertions. Run order:

1. Pre-cutover, against current SQLite: tests should pass (validates baseline)
2. Post-cutover, against new SQL Server: tests should still pass (validates migration)

---

## What to expect

### Timeline

| Phase | Time | What's happening |
|---|---|---|
| 0 | T-7d | Communicate to Sara/Marie, set cost alerts |
| 0 | T-1d | Provision Azure SQL, write tests, snapshot baseline |
| 1 | T+0 | Tag current image as `:sqlite-rollback`, build new image with SQL Server config |
| 2 | T+5min | Deploy new image, watch logs, Umbraco creates schema |
| 3 | T+8min | Seeder runs, content created |
| 4 | T+12min | Run test suite against prod |
| 5 | T+20min | Sara verifies login, manual smoke |
| Total | ~25 min | (Sara verification is the slowest step) |

### Observable side effects

- **CMS unavailable for ~10 min** during deploy + first-time schema creation
- **All seed content reverts to defaults** (placeholder ingress, demo articles, etc.) — Sara may need to redo any tweaks
- **Litestream stops replicating** — blob in `kinorgestorage/umbraco-db` becomes stale (we keep it for 30 days as rollback insurance)
- **No Delivery API queries can be served** during the deploy window — the frontend will return cached content where possible, otherwise show stale data via SSR fallback

### Gotchas to watch for

- Umbraco's first-time schema creation against SQL Server takes ~60 seconds. Don't panic if `/api/health/ready` returns 503 for a couple of minutes.
- Container Apps probe initialDelaySeconds is currently 60, may need to bump to 120 for the first deploy if migration takes longer.
- The `_blockListArtikkelDt` data type has a forward reference to `artikkelProsessteg` element type. If creation order is wrong, you'll see "Element type X not found, skipping in block list Y" warnings. This is recoverable: a `RefreshMultiBlockListAllowedModules()` call later in startup fixes it.
- `Default Timeout=30` is the SQLite-specific busy_timeout setting. Don't carry it over to the SQL Server connection string — SQL Server's lock waiting is different.
- The Umbraco unattended install env vars (`UMBRACO__CMS__UNATTENDED__*`) need to point to a writable database — they'll fail silently if connection is broken, leaving you with a half-installed CMS.

### What success looks like

- `/api/health/ready` returns 200 within 5 minutes of deploy
- Backoffice login works for `admin@ki.norge.no / KiNorge2025!`
- Delivery API returns at least 11 artikkel + 4 case + ordbok entries
- All 21 prod smoke tests pass
- The new test suite at `tests/sql-migration/` passes 100%
- Two consecutive user creations succeed (the original failure mode)
- No "database is locked" errors in logs for 24h after cutover

---

## Tests we can write now (pre-cutover)

All tests live under `tests/sql-migration/`. Folder naming makes them obvious: anyone grepping for "sql" or "migration" finds them immediately. Each file is named `NN-what-it-tests.spec.ts` so order is preserved + intent is clear.

### `tests/sql-migration/README.md`

One page explaining: what these tests are for, when to run them, what passing means.

### `tests/sql-migration/01-baseline-snapshot.spec.ts`

Captures the current state of prod into `baseline-prod.json`. **Run once before cutover.** Asserts the file got written.

```ts
test('snapshot prod baseline', async ({ request }) => {
  const counts = await fetchContentCountsByType(request);
  expect(counts.artikkel).toBeGreaterThan(5);
  expect(counts.case).toBeGreaterThan(0);
  expect(counts.ordbokOppslag).toBeGreaterThan(150);
  expect(counts.merkelapp).toBeGreaterThan(0);
  await fs.writeFile('tests/sql-migration/baseline-prod.json', JSON.stringify(counts, null, 2));
});
```

### `tests/sql-migration/02-database-reachable.spec.ts`

Direct connectivity test — bypasses Umbraco. Tries to open a connection to the new SQL Server, runs `SELECT 1`. Catches firewall / DNS / auth issues independently of Umbraco.

```ts
test('SQL Server is reachable from this network', async () => {
  const conn = new sql.Connection({ server, database, user, password });
  await conn.connect();
  const result = await conn.query('SELECT 1 as one');
  expect(result.recordset[0].one).toBe(1);
});
```

### `tests/sql-migration/03-umbraco-schema-present.spec.ts`

Asserts Umbraco's expected tables exist after first start. If unattended install ran cleanly, this passes.

```ts
test.each([
  'umbracoNode',
  'umbracoContent',
  'umbracoContentVersion',
  'umbracoDocument',
  'umbracoPropertyData',
  'cmsContentType',
  'cmsPropertyType',
  'umbracoUser',
])('Umbraco table %s exists', async (table) => {
  const exists = await tableExists(table);
  expect(exists).toBe(true);
});
```

### `tests/sql-migration/04-content-types-present.spec.ts`

Asserts every content type our composer creates is present — proves ContentTypeComposer ran.

```ts
test.each([
  'forside', 'artikkel', 'case', 'sandkasse', 'omOss',
  'veiledningGuide', 'veiledningSteg', 'faq', 'merkelapp',
  'ordbokOppslag',
  // Element types
  'artikkelTekst', 'artikkelTrekkspill', 'artikkelProsessteg',
])('Content type %s is registered', async (alias) => {
  const ct = await getContentType(alias);
  expect(ct, `Content type "${alias}" not found`).not.toBeNull();
});
```

### `tests/sql-migration/05-seeded-content-present.spec.ts`

Asserts the seeder ran and produced expected content. Compares to `baseline-prod.json` if present.

```ts
test('Forside exists at root', async () => {
  const forside = await deliveryApi.getByPath('/');
  expect(forside.contentType).toBe('forside');
});

test('Sandkasse exists under Sider', async () => {
  const sk = await deliveryApi.getByPath('/sandkasse/');
  expect(sk.contentType).toBe('sandkasse');
  expect(sk.properties.tittel).toBeTruthy();
});

test('Article count matches baseline', async () => {
  const baseline = JSON.parse(await fs.readFile('baseline-prod.json'));
  const current = await fetchContentCountsByType();
  expect(current.artikkel).toBe(baseline.artikkel);
});
```

### `tests/sql-migration/06-concurrency-no-lock-error.spec.ts`

**The original failure-mode regression test.** Two parallel user creations should both succeed without "database is locked".

```ts
test('two parallel user creations succeed', async ({ request }) => {
  const [r1, r2] = await Promise.all([
    createUser(request, { email: `test1-${Date.now()}@example.com`, name: 'Test 1' }),
    createUser(request, { email: `test2-${Date.now()}@example.com`, name: 'Test 2' }),
  ]);
  expect(r1.status()).toBe(201);
  expect(r2.status()).toBe(201);
});

test('10 parallel content saves succeed', async ({ request }) => {
  const promises = Array.from({ length: 10 }, (_, i) =>
    createTestContent(request, `Test article ${i}-${Date.now()}`)
  );
  const results = await Promise.all(promises);
  for (const r of results) {
    expect(r.status()).toBe(201);
  }
});
```

### `tests/sql-migration/07-delivery-api-functional.spec.ts`

Standard smoke: every content type returns at least one item via Delivery API.

```ts
test.each(['artikkel', 'case', 'omOss', 'forside', 'faq', 'ordbokOppslag', 'sandkasse'])(
  'Delivery API: contentType %s returns >=1 item',
  async (contentType) => {
    const r = await deliveryApi.list({ contentType, take: 1 });
    expect(r.total).toBeGreaterThan(0);
  }
);
```

### `tests/sql-migration/08-backoffice-login.spec.ts`

Playwright UI test. Logs in as admin, checks the content tree renders, no error toasts.

```ts
test('admin can log in to backoffice', async ({ page }) => {
  await page.goto('/umbraco');
  await page.fill('[name=email]', 'admin@ki.norge.no');
  await page.fill('[name=password]', 'KiNorge2025!');
  await page.click('button[type=submit]');
  await expect(page.locator('text=Content')).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('text=Forside')).toBeVisible();
});
```

### `tests/sql-migration/09-frontend-still-renders.spec.ts`

Frontend smoke — proves the Delivery API contract didn't change in any breaking way.

Reuses the existing `scripts/smoke-test.sh --prod` but runs it via Playwright assertions for explicit pass/fail.

### `tests/sql-migration/10-no-lock-errors-in-logs.spec.ts`

Post-cutover, scans the last 24h of CMS logs for "database is locked" / "table is locked". Expects zero matches.

```ts
test('no SQLite lock errors in CMS logs (last 24h)', async () => {
  const logs = await fetchAzureContainerAppLogs('ki-norge-cms', { hours: 24 });
  const lockErrors = logs.filter(l =>
    l.message.includes('database is locked') ||
    l.message.includes('table is locked')
  );
  expect(lockErrors).toHaveLength(0);
});
```

### Test runner script

`scripts/sql-migration/run-tests.sh`:
```bash
#!/usr/bin/env bash
# Usage: bash scripts/sql-migration/run-tests.sh [--baseline | --post-cutover]
set -euo pipefail

case "${1:-}" in
  --baseline)
    npx playwright test --config=tests/sql-migration/playwright.config.ts \
      tests/sql-migration/01-baseline-snapshot.spec.ts
    ;;
  --post-cutover)
    npx playwright test --config=tests/sql-migration/playwright.config.ts \
      tests/sql-migration/0[2-9]*.spec.ts \
      tests/sql-migration/10-no-lock-errors-in-logs.spec.ts
    ;;
  *)
    echo "Usage: $0 --baseline | --post-cutover"; exit 1;;
esac
```

---

## Migration steps (ordered, with checkpoints)

### Phase 1: Provision (30 min)

```bash
SQL_PW=$(openssl rand -base64 32 | tr -d '=+/' | head -c 32)
echo "$SQL_PW" | pbcopy   # save to password manager NOW

az sql server create \
  --resource-group ki-norge \
  --name kinorgesql \
  --location norwayeast \
  --admin-user umbracoadmin \
  --admin-password "$SQL_PW"

az sql db create \
  --resource-group ki-norge \
  --server kinorgesql \
  --name umbracokinorge \
  --service-objective S0 \
  --backup-storage-redundancy Local \
  --collation SQL_Latin1_General_CP1_CI_AS

az sql server firewall-rule create \
  --resource-group ki-norge \
  --server kinorgesql \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Optional: temporary firewall rule for your dev IP so you can connect from local
az sql server firewall-rule create \
  --resource-group ki-norge \
  --server kinorgesql \
  --name DevIpTemp \
  --start-ip-address $(curl -s ifconfig.me) \
  --end-ip-address $(curl -s ifconfig.me)

az containerapp secret set -g ki-norge -n ki-norge-cms \
  --secrets "sql-admin-password=$SQL_PW"
```

**Checkpoint**: Run `tests/sql-migration/02-database-reachable.spec.ts` from local. Should pass.

### Phase 2: Code changes (45 min)

1. Update `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "umbracoDbDSN": "",
     "umbracoDbDSN_ProviderName": "Microsoft.Data.SqlClient"
   }
   ```

2. Update `appsettings.Development.json`:
   ```json
   "ConnectionStrings": {
     "umbracoDbDSN": "Data Source=|DataDirectory|/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True;Default Timeout=30",
     "umbracoDbDSN_ProviderName": "Microsoft.Data.Sqlite"
   }
   ```
   Local dev STAYS on SQLite (faster iteration, no Azure cost). Only prod switches.

3. Update `Dockerfile`: remove the Litestream binary install + `entrypoint.sh` invocation. Direct `dotnet KiNorge.Cms.dll` becomes the entrypoint.

4. Delete: `apps/cms-umbraco/litestream.yml`, `apps/cms-umbraco/entrypoint.sh`.

5. Update `scripts/deploy-azure.sh`:
   - Replace `ConnectionStrings__umbracoDbDSN` env var:
     ```yaml
     - name: ConnectionStrings__umbracoDbDSN
       value: 'Server=tcp:kinorgesql.database.windows.net,1433;Initial Catalog=umbracokinorge;User ID=umbracoadmin;Password=$(SQL_ADMIN_PASSWORD);Encrypt=True;Connection Timeout=30;'
     - name: ConnectionStrings__umbracoDbDSN_ProviderName
       value: Microsoft.Data.SqlClient
     - name: SQL_ADMIN_PASSWORD
       value: secretref:sql-admin-password
     ```
   - Remove: all `LITESTREAM_*` env vars and the secret reference
   - Bump probe `initialDelaySeconds` from 60 to 120 (first SQL Server schema creation is slow)
   - Set `LAUNCH_MODE` to *unset* for the first cutover deploy (so seeder runs), then re-set to `production` afterward

6. Build, push, tag the previous image as `:sqlite-rollback` automatically (the existing `:prev` tag in deploy-azure.sh covers this).

**Checkpoint**: `dotnet build` passes. Local dev still works (still on SQLite). PR up for review.

### Phase 3: Cutover (15 min)

```bash
# Set LAUNCH_MODE unset for first deploy
sed -i.bak "s/value: 'production'/value: ''/" scripts/deploy-azure.sh   # for LAUNCH_MODE only
bash scripts/deploy-azure.sh
```

Watch logs:
```bash
az containerapp logs show -g ki-norge -n ki-norge-cms --follow --tail 50
```

Look for:
- `Database configuration status: Install completed!` (good — schema created)
- `ContentTypeComposer: ...` (good — composers ran)
- `ContentSeeder: ...` (good — seeder ran)
- `Now listening on: http://0.0.0.0:8080` (good — app started)

Once stable:
```bash
# Re-set LAUNCH_MODE=production to lock seeder
git checkout scripts/deploy-azure.sh
az containerapp update -g ki-norge -n ki-norge-cms \
  --set-env-vars "LAUNCH_MODE=production"
```

**Checkpoint**: Run `bash scripts/sql-migration/run-tests.sh --post-cutover`. All tests pass.

### Phase 4: Cleanup (separate PR, 1 day later)

After 24h with no incidents:

- Delete `umbraco-db` blob container in `kinorgestorage`
- Delete `litestream-azure-account-key` Container App secret
- Remove SQLite-specific code paths if any remain
- Delete `:sqlite-rollback` image tag from ACR (rollback window over)
- Update `CLAUDE.md`: replace SQLite section with SQL Server architecture
- Update memory entries (remove Litestream gotchas, add SQL Server connection notes)
- Remove Litestream from local Dockerfile path if it's still referenced
- Remove the temp firewall rule `DevIpTemp` if added in Phase 1

---

## Rollback plan

If anything fails after deploy:

```bash
# 1. Switch back to Litestream-based image
az containerapp update -g ki-norge -n ki-norge-cms \
  --image kinorgeacr.azurecr.io/ki-norge/cms:sqlite-rollback

# 2. Restore SQLite connection string
az containerapp update -g ki-norge -n ki-norge-cms \
  --set-env-vars \
    "ConnectionStrings__umbracoDbDSN=Data Source=/app/umbraco/Data/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True;Default Timeout=30" \
    "ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite"

# 3. Restore Litestream env var
az containerapp update -g ki-norge -n ki-norge-cms \
  --set-env-vars "LITESTREAM_AZURE_ACCOUNT_KEY=secretref:litestream-azure-account-key"

# 4. Litestream picks up the existing WAL on restart (blob untouched)
```

The blob storage WAL data stays intact for 30 days post-cutover. Rollback in that window is essentially free.

---

## Decisions still open

1. **Standard S0 vs Serverless GP_S_Gen5_1.** Recommend Standard S0 for prod simplicity and no cold starts. Serverless if/when we add a separate staging.
2. **Add a staging SQL DB now or later?** Lean: later. Cost is small but adds complexity to deploy script.
3. **Audit logging.** Azure SQL Auditing to a storage account = 5 NOK/month, defensive. Worth it pre-launch? Probably yes.
4. **Connection pool size.** Default is 100, fine for our scale. Document it.
5. **Backup retention.** Default 7 days for Standard. Bump to 35 days for compliance? Decision needed.
6. **Sara's existing content** — confirm she has no real content yet, or plan for her to re-enter post-cutover.

---

## Communication template

For Sara / Marie:

> **Subject**: KI Norge CMS — kort vedlikehold på [DATE] kl [TIME]
>
> Hei,
>
> Vi flytter CMS-databasen fra dagens SQLite-løsning til Azure SQL Database. Dette gir oss bedre stabilitet og fjerner et kjent problem der lagring kan feile hvis to redaktører jobber samtidig.
>
> **Når**: [DATE] kl [TIME]–[TIME+30min]
> **Hva merker du**: Umbraco-redigering vil være utilgjengelig i ca. 10 minutter midt i vinduet. Selve nettsiden ki.norge.no fortsetter å vise innhold som normalt.
> **Etter**: Eventuelle plassholder-endringer du har gjort i CMS-en vil være tilbakestilt til standard. Reelt innhold er ikke berørt (vi har bekreftet at ingen produksjons-artikler er lagret enda).
>
> Gi beskjed hvis tidspunktet ikke passer, eller hvis du har innhold du ikke vil miste.
>
> Lars

---

## Pre-flight checklist (final go/no-go)

- [ ] All tests in `tests/sql-migration/` are written and run cleanly against current SQLite
- [ ] Baseline snapshot saved to `tests/sql-migration/baseline-prod.json`
- [ ] Sara confirmed nothing in CMS will be lost
- [ ] Sara is reachable for post-cutover login test
- [ ] Azure PIM is active for the cutover window
- [ ] Cost alert is set up
- [ ] SQL admin password is in password manager
- [ ] Rollback procedure read end-to-end at least once
- [ ] PR with code changes is reviewed and approved
- [ ] Litestream blob backup is fresh (last replication < 1h old)
- [ ] You have 2 uninterrupted hours and a coffee

If any unchecked: do not proceed.
