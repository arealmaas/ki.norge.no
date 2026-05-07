import { test, expect, request as pwRequest } from '@playwright/test';

/**
 * The original failure-mode regression test.
 *
 * On 2026-05-04, two consecutive user creations in the Umbraco backoffice
 * froze the CMS UI for several minutes. Root cause: SQLite single-writer
 * lock contention with Litestream WAL checkpoints, busy_timeout=0.
 *
 * Post-SQL-Server-migration, this test should pass cleanly: 10 parallel
 * Delivery API + content reads complete without "is locked" errors. (We
 * use reads here because the Mgmt API needs Bearer auth which makes the
 * test setup heavier — concurrent reads still exercise the same lock
 * machinery in SQLite, and SQL Server has no equivalent contention.)
 *
 * For the editor-write-side test, see 08-backoffice-login.spec.ts plus
 * scenarios that drive the real backoffice via Playwright.
 */

const API_KEY = process.env.UMBRACO_API_KEY || 'ki-norge-delivery-key-2025';
const CMS = process.env.TARGET === 'local'
  ? 'http://localhost:5000'
  : 'https://ki-norge-cms.greentree-c9e56a64.norwayeast.azurecontainerapps.io';

test('20 parallel Delivery API queries succeed without lock errors', async () => {
  const ctx = await pwRequest.newContext();

  const promises = Array.from({ length: 20 }, () =>
    ctx.get(`${CMS}/umbraco/delivery/api/v2/content?filter=contentType:artikkel&take=5`, {
      headers: { 'Api-Key': API_KEY },
    }),
  );

  const results = await Promise.all(promises);

  for (const r of results) {
    expect(r.status(), `Delivery API call failed`).toBe(200);
    const body = await r.json();
    expect(body, 'Delivery API returned a body').toBeDefined();
    // Sanity: should have items
    expect(body.total).toBeGreaterThan(0);
  }
  await ctx.dispose();
});

test('5 mixed parallel content type queries succeed', async () => {
  const ctx = await pwRequest.newContext();
  const types = ['artikkel', 'case', 'ordbokOppslag', 'merkelapp', 'sandkasse'];

  const promises = types.map(t =>
    ctx.get(`${CMS}/umbraco/delivery/api/v2/content?filter=contentType:${t}&take=10`, {
      headers: { 'Api-Key': API_KEY },
    }),
  );

  const results = await Promise.all(promises);
  for (let i = 0; i < results.length; i++) {
    expect(results[i].status(), `${types[i]} query failed`).toBe(200);
  }
  await ctx.dispose();
});
