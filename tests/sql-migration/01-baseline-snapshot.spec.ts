import { test, expect } from '@playwright/test';
import { promises as fs } from 'fs';
import * as path from 'path';

/**
 * Captures the current state of CMS content into baseline-prod.json so we
 * can diff against the post-cutover state. Run ONCE against current SQLite
 * prod, before the cutover.
 *
 * Usage:
 *   TARGET=prod npx playwright test --config=tests/playwright.config.ts \
 *     tests/sql-migration/01-baseline-snapshot.spec.ts
 */

const API_KEY = process.env.UMBRACO_API_KEY || 'ki-norge-delivery-key-2025';
const CMS = process.env.TARGET === 'local'
  ? 'http://localhost:5000'
  : 'https://ki-norge-cms.greentree-c9e56a64.norwayeast.azurecontainerapps.io';

const CONTENT_TYPES = [
  'forside', 'artikkel', 'case', 'sandkasse', 'omOss',
  'veiledningGuide', 'veiledningSteg', 'faq', 'merkelapp', 'ordbokOppslag',
];

async function countByType(request: any, contentType: string): Promise<number> {
  const r = await request.get(
    `${CMS}/umbraco/delivery/api/v2/content?filter=contentType:${contentType}&take=1`,
    { headers: { 'Api-Key': API_KEY } },
  );
  const data = await r.json();
  return data.total ?? 0;
}

test('Snapshot prod baseline', async ({ request }) => {
  const counts: Record<string, number> = {};
  for (const ct of CONTENT_TYPES) {
    counts[ct] = await countByType(request, ct);
  }

  // Sanity: prod should have at least the expected minimums
  expect(counts.artikkel, 'expected at least 5 artikkel').toBeGreaterThanOrEqual(5);
  expect(counts.case, 'expected at least 1 case').toBeGreaterThanOrEqual(1);
  expect(counts.ordbokOppslag, 'expected ~190 ordbok entries').toBeGreaterThan(150);
  expect(counts.forside, 'expected exactly 1 forside').toBe(1);

  const snapshot = {
    capturedAt: new Date().toISOString(),
    cmsHost: CMS,
    counts,
  };

  const out = path.join(import.meta.dirname, 'baseline-prod.json');
  await fs.writeFile(out, JSON.stringify(snapshot, null, 2));
  console.log(`Wrote baseline to ${out}`);
});
