# SQL Server migration tests

Test suite for the SQLite → Azure SQL Database migration. See full plan in
`docs/sql-server-migration-plan.md`.

## When to run

- **Before cutover** (against current SQLite prod): run `01-baseline-snapshot` to capture what
  exists today. Saves to `baseline-prod.json`.
- **After cutover** (against new SQL Server prod): run all other tests. They verify the
  migration succeeded and that the original failure mode (concurrent writes) is fixed.

## Quick reference

```bash
# Capture baseline (do this once before cutover, against SQLite)
npx playwright test --config=tests/sql-migration/playwright.config.ts \
  tests/sql-migration/01-baseline-snapshot.spec.ts

# Verify migration succeeded (after cutover, against SQL Server)
npx playwright test --config=tests/sql-migration/playwright.config.ts \
  tests/sql-migration/0[2-9]*.spec.ts \
  tests/sql-migration/10-no-lock-errors-in-logs.spec.ts
```

## Files

| File | Runs against | What it proves |
|---|---|---|
| `01-baseline-snapshot` | Pre-cutover SQLite | Captures content counts to compare after |
| `02-database-reachable` | Post-cutover SQL Server | Connectivity + auth (bypasses Umbraco) |
| `03-umbraco-schema-present` | Post-cutover | Core Umbraco tables created |
| `04-content-types-present` | Post-cutover | Composer ran, every type registered |
| `05-seeded-content-present` | Post-cutover | Seeder ran (or content was migrated) |
| `06-concurrency-no-lock-error` | Post-cutover | **The original failure-mode regression test** |
| `07-delivery-api-functional` | Post-cutover | Each content type returns ≥1 item |
| `08-backoffice-login` | Post-cutover | Admin can log in via UI |
| `09-frontend-still-renders` | Post-cutover | Public pages render |
| `10-no-lock-errors-in-logs` | Post-cutover | No "is locked" in 24h of logs |

## Notes

- All tests use the existing `tests/playwright.config.ts` setup and Delivery API key
- `02-database-reachable` requires `mssql` Node package + connection string in `SQL_CONNECTION_STRING` env var
- `08-backoffice-login` needs `CMS_USER` / `CMS_PASS` env vars (default: unattended-install admin)
- `10-no-lock-errors-in-logs` requires Azure CLI logged in with PIM-active access

## Data hygiene

Tests that create content (`06-concurrency`) use a unique timestamp prefix and clean up
after themselves. Adding `--reporter=list` shows progress; failures dump screenshots
to `test-results/`.
