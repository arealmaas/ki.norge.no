import { test, expect } from '@playwright/test';

/**
 * Post-cutover smoke: every content type returns at least one item via Delivery API.
 * This is the same check as scripts/smoke-test.sh, kept here for completeness in the
 * SQL-migration test suite.
 */

const API_KEY = process.env.UMBRACO_API_KEY || 'ki-norge-delivery-key-2025';
const CMS = process.env.TARGET === 'local'
  ? 'http://localhost:5000'
  : 'https://ki-norge-cms.greentree-c9e56a64.norwayeast.azurecontainerapps.io';

const REQUIRED_TYPES = [
  'artikkel',
  'case',
  'omOss',
  'forside',
  'faq',
  'ordbokOppslag',
  'sandkasse',
  'merkelapp',
];

for (const contentType of REQUIRED_TYPES) {
  test(`Delivery API: ${contentType} returns >=1 item`, async ({ request }) => {
    const r = await request.get(
      `${CMS}/umbraco/delivery/api/v2/content?filter=contentType:${contentType}&take=1`,
      { headers: { 'Api-Key': API_KEY } },
    );
    expect(r.status()).toBe(200);
    const data = await r.json();
    expect(data.total, `${contentType} should have at least 1 item`).toBeGreaterThanOrEqual(1);
  });
}

test('Sort by updateDate works (regression test for publishedAt:desc bug)', async ({ request }) => {
  const r = await request.get(
    `${CMS}/umbraco/delivery/api/v2/content?filter=contentType:case&take=1&sort=updateDate:desc`,
    { headers: { 'Api-Key': API_KEY } },
  );
  expect(r.status()).toBe(200);
});
