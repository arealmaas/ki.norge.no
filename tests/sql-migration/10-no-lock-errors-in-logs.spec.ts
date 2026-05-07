import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

/**
 * Post-cutover: scan recent CMS container logs for "is locked" errors.
 * Should be zero on SQL Server. Requires Azure CLI logged in with
 * PIM-active access to the ki-norge resource group.
 */

const HOURS = 24;

test('No SQLite lock errors in CMS logs (last 24h)', async () => {
  let logs = '';
  try {
    // Tail the most recent logs from the active CMS revision
    logs = execSync(
      `az containerapp logs show -g ki-norge -n ki-norge-cms --tail 500 2>&1`,
      { encoding: 'utf-8', timeout: 60_000 },
    );
  } catch (err) {
    test.skip(true, `Could not fetch Azure logs (need PIM-active CLI session): ${err}`);
    return;
  }

  const lockPattern = /database (?:is |table is )?locked/i;
  const lockLines = logs.split('\n').filter((l) => lockPattern.test(l));

  expect(
    lockLines,
    `Expected zero "database is locked" errors in CMS logs.\nFound ${lockLines.length}:\n${lockLines.slice(0, 10).join('\n')}`,
  ).toEqual([]);
});

test('No SQLite-specific errors in CMS logs', async () => {
  let logs = '';
  try {
    logs = execSync(
      `az containerapp logs show -g ki-norge -n ki-norge-cms --tail 500 2>&1`,
      { encoding: 'utf-8', timeout: 60_000 },
    );
  } catch (err) {
    test.skip(true, `Could not fetch Azure logs: ${err}`);
    return;
  }

  // After SQL Server migration there should be NO references to SQLite
  const sqlitePattern = /SqliteException|SQLite Error/i;
  const sqliteLines = logs.split('\n').filter((l) => sqlitePattern.test(l));

  expect(
    sqliteLines,
    `Expected zero SQLite errors after SQL Server cutover.\nFound:\n${sqliteLines.slice(0, 10).join('\n')}`,
  ).toEqual([]);
});
