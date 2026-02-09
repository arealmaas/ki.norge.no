import { test, expect } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';

/**
 * Plugin-disabled integration tests.
 *
 * These tests verify that when the redaksjonelt plugin is disabled:
 * - Content can be published without approval
 * - Workflow API routes return 404
 * - Content is unaffected
 *
 * IMPORTANT: These tests require Strapi to be running with the
 * redaksjonelt plugin disabled in config/plugins.ts:
 *   redaksjonelt: { enabled: false, ... }
 *
 * Run separately from the main workflow tests.
 */

test.describe.configure({ mode: 'serial' });

const CMS = 'http://localhost:1337';
const CT = 'api::artikkel.artikkel';
const RUN_ID = Date.now().toString(36);

let adminToken: string;

async function login(request: APIRequestContext): Promise<string> {
  if (adminToken) return adminToken;

  const loginRes = await request.post(`${CMS}/admin/login`, {
    data: { email: 'admin@kinorge.no', password: 'Admin1234!' },
  });

  if (loginRes.status() === 200) {
    adminToken = (await loginRes.json()).data.token;
    return adminToken;
  }

  const reg = await request.post(`${CMS}/admin/register-admin`, {
    data: {
      firstname: 'Test',
      lastname: 'Admin',
      email: 'admin@kinorge.no',
      password: 'Admin1234!',
    },
  });
  expect(reg.status(), 'Admin registration or login must succeed').toBe(200);
  adminToken = (await reg.json()).data.token;
  return adminToken;
}

function auth(token: string) {
  return { Authorization: `Bearer ${token}` };
}

test.describe('Plugin disabled: publish without approval', () => {
  let token: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('can publish article without approval when plugin is disabled', async ({
    request,
  }) => {
    const uniqueSlug = `test-no-plugin-${RUN_ID}`;
    const createRes = await request.post(
      `${CMS}/content-manager/collection-types/${CT}`,
      {
        headers: { ...auth(token), 'Content-Type': 'application/json' },
        data: {
          tittel: 'Test: Plugin Disabled Publish',
          slug: uniqueSlug,
          innhold: [
            {
              type: 'paragraph',
              children: [{ type: 'text', text: 'Test content without plugin' }],
            },
          ],
          locale: 'nb',
        },
      }
    );
    expect(createRes.status()).toBe(201);
    const docId = (await createRes.json()).data.documentId;

    // Publish directly — should succeed without workflow
    const pubRes = await request.post(
      `${CMS}/content-manager/collection-types/${CT}/${docId}/actions/publish`,
      { headers: auth(token), data: {} }
    );
    expect(pubRes.status()).toBe(200);
    const body = await pubRes.json();
    expect(body.data.publishedAt).toBeTruthy();
  });
});

test.describe('Plugin disabled: workflow routes return 404', () => {
  test('workflow endpoints return 404', async ({ request }) => {
    const endpoints = [
      `${CMS}/api/redaksjonelt/send-til-godkjenning`,
      `${CMS}/api/redaksjonelt/godkjenn`,
      `${CMS}/api/redaksjonelt/avvis`,
      `${CMS}/api/redaksjonelt/mine-oppgaver`,
      `${CMS}/api/redaksjonelt/oversikt`,
    ];

    for (const url of endpoints) {
      const res = await request.get(url);
      expect(res.status(), `${url} should be 404`).toBe(404);
    }
  });

  test('notification endpoints return 404', async ({ request }) => {
    const endpoints = [
      `${CMS}/api/redaksjonelt/varslinger/mine`,
      `${CMS}/api/redaksjonelt/varslinger/uleste`,
    ];

    for (const url of endpoints) {
      const res = await request.get(url);
      expect(res.status(), `${url} should be 404`).toBe(404);
    }
  });

  test('scheduling endpoints return 404', async ({ request }) => {
    const endpoints = [
      `${CMS}/api/redaksjonelt/planlegg`,
      `${CMS}/api/redaksjonelt/kommende`,
    ];

    for (const url of endpoints) {
      const res = await request.get(url);
      expect(res.status(), `${url} should be 404`).toBe(404);
    }
  });
});

test.describe('Plugin disabled: content unaffected', () => {
  let token: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('existing content types still work', async ({ request }) => {
    // Verify we can still list articles
    const res = await request.get(`${CMS}/api/artikler?locale=nb`);
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body.data)).toBe(true);
  });
});
