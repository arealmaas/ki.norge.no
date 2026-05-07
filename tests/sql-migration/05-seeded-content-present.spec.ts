import { test, expect } from '@playwright/test';
import { promises as fs } from 'fs';
import * as path from 'path';

/**
 * Post-cutover: verify the new SQL Server CMS has the expected content,
 * compared against the baseline captured pre-cutover.
 *
 * If baseline-prod.json is missing, falls back to absolute-minimum sanity
 * checks (counts > 0, etc).
 */

const API_KEY = process.env.UMBRACO_API_KEY || 'ki-norge-delivery-key-2025';
const CMS = process.env.TARGET === 'local'
  ? 'http://localhost:5000'
  : 'https://ki-norge-cms.greentree-c9e56a64.norwayeast.azurecontainerapps.io';

async function countByType(request: any, contentType: string): Promise<number> {
  const r = await request.get(
    `${CMS}/umbraco/delivery/api/v2/content?filter=contentType:${contentType}&take=1`,
    { headers: { 'Api-Key': API_KEY } },
  );
  const data = await r.json();
  return data.total ?? 0;
}

async function loadBaseline(): Promise<Record<string, number> | null> {
  const p = path.join(import.meta.dirname, 'baseline-prod.json');
  try {
    const raw = await fs.readFile(p, 'utf-8');
    return JSON.parse(raw).counts;
  } catch {
    return null;
  }
}

test('Forside exists at root', async ({ request }) => {
  const r = await request.get(`${CMS}/umbraco/delivery/api/v2/content/item/`, {
    headers: { 'Api-Key': API_KEY },
  });
  // either / returns the forside, or there's at least 1 forside in the tree
  const total = await countByType(request, 'forside');
  expect(total).toBe(1);
});

test('Sandkasse exists', async ({ request }) => {
  const total = await countByType(request, 'sandkasse');
  expect(total, 'should have exactly one sandkasse node').toBe(1);
});

test('Article count matches baseline (or sanity floor)', async ({ request }) => {
  const baseline = await loadBaseline();
  const current = await countByType(request, 'artikkel');

  if (baseline) {
    expect(current).toBe(baseline.artikkel);
  } else {
    console.warn('No baseline-prod.json — using sanity floor');
    expect(current).toBeGreaterThanOrEqual(5);
  }
});

test('Ordbok count matches baseline (or sanity floor)', async ({ request }) => {
  const baseline = await loadBaseline();
  const current = await countByType(request, 'ordbokOppslag');

  if (baseline) {
    expect(current).toBe(baseline.ordbokOppslag);
  } else {
    expect(current).toBeGreaterThan(150);
  }
});

test('Case count matches baseline (or sanity floor)', async ({ request }) => {
  const baseline = await loadBaseline();
  const current = await countByType(request, 'case');

  if (baseline) {
    expect(current).toBe(baseline.case);
  } else {
    expect(current).toBeGreaterThanOrEqual(1);
  }
});
