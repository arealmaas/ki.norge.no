import { test, expect } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';

/**
 * Editorial workflow integration tests.
 *
 * These tests verify the complete editorial lifecycle:
 *   draft → submit → approve/reject → publish
 * as well as scheduled publishing, notifications, and auth guards.
 *
 * Requires: Strapi running on localhost:1337 with a fresh seeded database.
 */

// Disable parallel execution — tests within describe blocks share state
test.describe.configure({ mode: 'serial' });

const CMS = 'http://localhost:1337';
const CT = 'api::artikkel.artikkel';
const RUN_ID = Date.now().toString(36); // unique per test run

// ── Helpers ─────────────────────────────────────────────────────

let adminToken: string;

async function login(request: APIRequestContext): Promise<string> {
  if (adminToken) return adminToken;

  // Try login first
  const loginRes = await request.post(`${CMS}/admin/login`, {
    data: { email: 'admin@kinorge.no', password: 'Admin1234!' },
  });

  if (loginRes.status() === 200) {
    adminToken = (await loginRes.json()).data.token;
    return adminToken;
  }

  // Admin not yet registered — register
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

/** Create a draft article and return its documentId. */
async function createDraftArticle(
  request: APIRequestContext,
  token: string,
  slug: string,
  tittel: string
): Promise<string> {
  const uniqueSlug = `${slug}-${RUN_ID}`;
  const res = await request.post(
    `${CMS}/content-manager/collection-types/${CT}`,
    {
      headers: { ...auth(token), 'Content-Type': 'application/json' },
      data: {
        tittel,
        slug: uniqueSlug,
        innhold: [
          {
            type: 'paragraph',
            children: [{ type: 'text', text: `Testinnhold for ${tittel}` }],
          },
        ],
        locale: 'nb',
      },
    }
  );
  expect(res.status()).toBe(201);
  const body = await res.json();
  return body.data.documentId;
}

// ── Auth guard tests ────────────────────────────────────────────

test.describe('Auth guards', () => {
  test('workflow endpoints reject unauthenticated requests', async ({
    request,
  }) => {
    const endpoints = [
      { method: 'POST' as const, url: `${CMS}/api/arbeidsflyt/send-til-godkjenning` },
      { method: 'POST' as const, url: `${CMS}/api/arbeidsflyt/godkjenn` },
      { method: 'POST' as const, url: `${CMS}/api/arbeidsflyt/avvis` },
      { method: 'GET' as const, url: `${CMS}/api/arbeidsflyt/mine-oppgaver` },
      { method: 'GET' as const, url: `${CMS}/api/arbeidsflyt/logg/fake-id` },
    ];

    for (const ep of endpoints) {
      const res =
        ep.method === 'GET'
          ? await request.get(ep.url)
          : await request.post(ep.url, { data: {} });
      expect(res.status(), `${ep.method} ${ep.url} should be 403`).toBe(403);
    }
  });

  test('notification endpoints reject unauthenticated requests', async ({
    request,
  }) => {
    const endpoints = [
      { method: 'GET' as const, url: `${CMS}/api/varslinger/mine` },
      { method: 'GET' as const, url: `${CMS}/api/varslinger/uleste` },
    ];

    for (const ep of endpoints) {
      const res = await request.get(ep.url);
      expect(res.status(), `${ep.method} ${ep.url} should be 403`).toBe(403);
    }
  });

  test('workflow endpoints reject invalid bearer token', async ({
    request,
  }) => {
    const headers = auth('not-a-valid-jwt-token');
    const res = await request.get(`${CMS}/api/arbeidsflyt/mine-oppgaver`, {
      headers,
    });
    expect(res.status()).toBe(403);
  });
});

// ── Happy path: submit → approve → publish ──────────────────────

test.describe('Workflow: submit → approve → publish', () => {
  let token: string;
  let docId: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('create a draft article', async ({ request }) => {
    docId = await createDraftArticle(
      request,
      token,
      'test-happy-path',
      'Test: Happy Path Artikkel'
    );
    expect(docId).toBeTruthy();
  });

  test('publish is blocked without approval', async ({ request }) => {
    const res = await request.post(
      `${CMS}/content-manager/collection-types/${CT}/${docId}/actions/publish`,
      { headers: auth(token), data: {} }
    );
    expect(res.status()).toBe(400);
    const body = await res.json();
    expect(body.error.message).toContain('godkjennes');
  });

  test('submit for approval', async ({ request }) => {
    const res = await request.post(
      `${CMS}/api/arbeidsflyt/send-til-godkjenning`,
      {
        headers: auth(token),
        data: { innholdstype: CT, dokumentId: docId },
      }
    );
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.data.handling).toBe('sendt_til_godkjenning');
  });

  test('publish is still blocked (only submitted, not approved)', async ({
    request,
  }) => {
    const res = await request.post(
      `${CMS}/content-manager/collection-types/${CT}/${docId}/actions/publish`,
      { headers: auth(token), data: {} }
    );
    expect(res.status()).toBe(400);
  });

  test('approve the article', async ({ request }) => {
    const res = await request.post(`${CMS}/api/arbeidsflyt/godkjenn`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId, kommentar: 'Ser bra ut!' },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.data.handling).toBe('godkjent');
  });

  test('publish succeeds after approval', async ({ request }) => {
    const res = await request.post(
      `${CMS}/content-manager/collection-types/${CT}/${docId}/actions/publish`,
      { headers: auth(token), data: {} }
    );
    const body = await res.json();
    expect(res.status(), `Publish failed: ${JSON.stringify(body.error || {})}`).toBe(200);
    expect(body.data.publishedAt).toBeTruthy();
  });

  test('workflow log shows complete history', async ({ request }) => {
    const res = await request.get(
      `${CMS}/api/arbeidsflyt/logg/${docId}`,
      { headers: auth(token) }
    );
    expect(res.status()).toBe(200);
    const body = await res.json();
    const handlinger = body.data.map((e: any) => e.handling);
    expect(handlinger).toContain('sendt_til_godkjenning');
    expect(handlinger).toContain('godkjent');
  });
});

// ── Reject path: submit → reject → blocked ─────────────────────

test.describe('Workflow: submit → reject', () => {
  let token: string;
  let docId: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('create a draft article', async ({ request }) => {
    docId = await createDraftArticle(
      request,
      token,
      'test-reject-path',
      'Test: Reject Path Artikkel'
    );
  });

  test('submit for approval', async ({ request }) => {
    const res = await request.post(
      `${CMS}/api/arbeidsflyt/send-til-godkjenning`,
      {
        headers: auth(token),
        data: { innholdstype: CT, dokumentId: docId },
      }
    );
    expect(res.status()).toBe(200);
  });

  test('reject requires a comment', async ({ request }) => {
    const res = await request.post(`${CMS}/api/arbeidsflyt/avvis`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });
    expect(res.status()).toBe(400);
    const body = await res.json();
    expect(body.error.message).toContain('Kommentar');
  });

  test('reject with comment succeeds', async ({ request }) => {
    const res = await request.post(`${CMS}/api/arbeidsflyt/avvis`, {
      headers: auth(token),
      data: {
        innholdstype: CT,
        dokumentId: docId,
        kommentar: 'Mangler kilder, vennligst oppdater',
      },
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.data.handling).toBe('avvist');
    expect(body.data.kommentar).toContain('Mangler kilder');
  });

  test('publish is blocked after rejection', async ({ request }) => {
    const res = await request.post(
      `${CMS}/content-manager/collection-types/${CT}/${docId}/actions/publish`,
      { headers: auth(token), data: {} }
    );
    expect(res.status()).toBe(400);
    const body = await res.json();
    expect(body.error.message).toContain('godkjennes');
  });

  test('can re-submit after rejection', async ({ request }) => {
    const res = await request.post(
      `${CMS}/api/arbeidsflyt/send-til-godkjenning`,
      {
        headers: auth(token),
        data: { innholdstype: CT, dokumentId: docId },
      }
    );
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.data.handling).toBe('sendt_til_godkjenning');
  });

  test('approve after re-submit and publish succeeds', async ({ request }) => {
    // Approve
    const approveRes = await request.post(`${CMS}/api/arbeidsflyt/godkjenn`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });
    expect(approveRes.status()).toBe(200);

    // Publish
    const pubRes = await request.post(
      `${CMS}/content-manager/collection-types/${CT}/${docId}/actions/publish`,
      { headers: auth(token), data: {} }
    );
    expect(pubRes.status()).toBe(200);
  });
});

// ── Validation tests ────────────────────────────────────────────

test.describe('Workflow: validation', () => {
  let token: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('submit requires innholdstype and dokumentId', async ({ request }) => {
    const res = await request.post(
      `${CMS}/api/arbeidsflyt/send-til-godkjenning`,
      {
        headers: auth(token),
        data: {},
      }
    );
    expect(res.status()).toBe(400);
  });

  test('submit rejects unsupported content types', async ({ request }) => {
    const res = await request.post(
      `${CMS}/api/arbeidsflyt/send-til-godkjenning`,
      {
        headers: auth(token),
        data: { innholdstype: 'api::faq.faq', dokumentId: 'fake-id' },
      }
    );
    expect(res.status()).toBe(400);
    const body = await res.json();
    expect(body.error.message).toContain('støtter ikke arbeidsflyt');
  });

  test('submit rejects non-existent document', async ({ request }) => {
    const res = await request.post(
      `${CMS}/api/arbeidsflyt/send-til-godkjenning`,
      {
        headers: auth(token),
        data: { innholdstype: CT, dokumentId: 'does-not-exist-12345' },
      }
    );
    expect(res.status()).toBe(400);
    const body = await res.json();
    expect(body.error.message).toContain('finnes ikke');
  });

  test('approve fails if document is not submitted', async ({ request }) => {
    const docId = await createDraftArticle(
      request,
      token,
      'test-not-submitted',
      'Test: Not Submitted'
    );
    const res = await request.post(`${CMS}/api/arbeidsflyt/godkjenn`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });
    expect(res.status()).toBe(400);
    const body = await res.json();
    expect(body.error.message).toContain('ikke sendt til godkjenning');
  });

  test('reject fails if document is not submitted', async ({ request }) => {
    const docId = await createDraftArticle(
      request,
      token,
      'test-not-submitted-reject',
      'Test: Not Submitted Reject'
    );
    const res = await request.post(`${CMS}/api/arbeidsflyt/avvis`, {
      headers: auth(token),
      data: {
        innholdstype: CT,
        dokumentId: docId,
        kommentar: 'Test rejection',
      },
    });
    expect(res.status()).toBe(400);
    const body = await res.json();
    expect(body.error.message).toContain('ikke sendt til godkjenning');
  });
});

// ── Pending approval list (mine-oppgaver) ───────────────────────

test.describe('Workflow: mine-oppgaver', () => {
  let token: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('mine-oppgaver returns pending items', async ({ request }) => {
    // Create and submit a new article
    const docId = await createDraftArticle(
      request,
      token,
      'test-oppgaver',
      'Test: Oppgaver List'
    );
    await request.post(`${CMS}/api/arbeidsflyt/send-til-godkjenning`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });

    // Check mine-oppgaver
    const res = await request.get(`${CMS}/api/arbeidsflyt/mine-oppgaver`, {
      headers: auth(token),
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    const docIds = body.data.map((e: any) => e.dokumentId);
    expect(docIds).toContain(docId);
  });

  test('approved items are removed from mine-oppgaver', async ({
    request,
  }) => {
    // Create, submit, and approve
    const docId = await createDraftArticle(
      request,
      token,
      'test-oppgaver-approved',
      'Test: Oppgaver Approved'
    );
    await request.post(`${CMS}/api/arbeidsflyt/send-til-godkjenning`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });
    await request.post(`${CMS}/api/arbeidsflyt/godkjenn`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });

    // Check mine-oppgaver — should NOT contain this docId
    const res = await request.get(`${CMS}/api/arbeidsflyt/mine-oppgaver`, {
      headers: auth(token),
    });
    const body = await res.json();
    const docIds = body.data.map((e: any) => e.dokumentId);
    expect(docIds).not.toContain(docId);
  });
});

// ── Notifications ───────────────────────────────────────────────

test.describe('Notifications', () => {
  let token: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('uleste count endpoint works', async ({ request }) => {
    const res = await request.get(`${CMS}/api/varslinger/uleste`, {
      headers: auth(token),
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(typeof body.data.count).toBe('number');
  });

  test('mine varslinger endpoint works', async ({ request }) => {
    const res = await request.get(`${CMS}/api/varslinger/mine`, {
      headers: auth(token),
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body.data)).toBe(true);
  });

  test('workflow actions create notifications', async ({ request }) => {
    // Get initial notification count
    const beforeRes = await request.get(`${CMS}/api/varslinger/mine`, {
      headers: auth(token),
    });
    const beforeCount = (await beforeRes.json()).data.length;

    // Create, submit, and approve a new article
    const docId = await createDraftArticle(
      request,
      token,
      'test-notif',
      'Test: Notification Article'
    );
    await request.post(`${CMS}/api/arbeidsflyt/send-til-godkjenning`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });

    // Check that notifications were created
    // Note: sendt_til_godkjenning notifies OTHER admins, not the submitter.
    // Since there's only one admin, no new notification is created for submit.
    // But approve/reject will create one for the submitter.
    await request.post(`${CMS}/api/arbeidsflyt/godkjenn`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });

    const afterRes = await request.get(`${CMS}/api/varslinger/mine`, {
      headers: auth(token),
    });
    const afterCount = (await afterRes.json()).data.length;

    // Approval should have created a notification for the submitter (us)
    expect(afterCount).toBeGreaterThan(beforeCount);
  });

  test('mark notification as read', async ({ request }) => {
    // Get an unread notification
    const listRes = await request.get(`${CMS}/api/varslinger/mine`, {
      headers: auth(token),
    });
    const notifications = (await listRes.json()).data;
    const unread = notifications.find((n: any) => !n.lest);

    if (!unread) {
      test.skip();
      return;
    }

    const markRes = await request.put(
      `${CMS}/api/varslinger/${unread.documentId}/lest`,
      { headers: auth(token) }
    );
    expect(markRes.status()).toBe(200);
    const body = await markRes.json();
    expect(body.data.lest).toBe(true);
  });
});

// ── Scheduled publishing ────────────────────────────────────────

test.describe('Scheduled publishing', () => {
  let token: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('schedule an approved article for future publish', async ({
    request,
  }) => {
    // Create, submit, approve
    const docId = await createDraftArticle(
      request,
      token,
      'test-scheduled',
      'Test: Scheduled Publish'
    );
    await request.post(`${CMS}/api/arbeidsflyt/send-til-godkjenning`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });
    await request.post(`${CMS}/api/arbeidsflyt/godkjenn`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });

    // Schedule for 1 hour from now
    const future = new Date(Date.now() + 60 * 60 * 1000).toISOString();
    const res = await request.post(
      `${CMS}/api/planlagt-publisering/planlegg`,
      {
        headers: auth(token),
        data: { innholdstype: CT, dokumentId: docId, publiserTid: future },
      }
    );
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.data.status).toBe('venter');
    expect(body.data.dokumentId).toBe(docId);
  });

  test('kommende endpoint lists scheduled publishes', async ({ request }) => {
    const res = await request.get(
      `${CMS}/api/planlagt-publisering/kommende`,
      { headers: auth(token) }
    );
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.data.length).toBeGreaterThan(0);
    // All entries should be in "venter" status
    for (const entry of body.data) {
      expect(entry.status).toBe('venter');
    }
  });

  test('cancel a scheduled publish', async ({ request }) => {
    // Create, submit, approve, and schedule another article
    const docId = await createDraftArticle(
      request,
      token,
      'test-cancel-schedule',
      'Test: Cancel Scheduled'
    );
    await request.post(`${CMS}/api/arbeidsflyt/send-til-godkjenning`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });
    await request.post(`${CMS}/api/arbeidsflyt/godkjenn`, {
      headers: auth(token),
      data: { innholdstype: CT, dokumentId: docId },
    });
    const future = new Date(Date.now() + 60 * 60 * 1000).toISOString();
    const schedRes = await request.post(
      `${CMS}/api/planlagt-publisering/planlegg`,
      {
        headers: auth(token),
        data: { innholdstype: CT, dokumentId: docId, publiserTid: future },
      }
    );
    const schedId = (await schedRes.json()).data.documentId;

    // Cancel
    const cancelRes = await request.put(
      `${CMS}/api/planlagt-publisering/${schedId}/kanseller`,
      { headers: auth(token) }
    );
    expect(cancelRes.status()).toBe(200);
    const body = await cancelRes.json();
    expect(body.data.status).toBe('kansellert');
  });
});

// ── Non-editorial content types bypass workflow ─────────────────

test.describe('Non-editorial types bypass workflow', () => {
  let token: string;

  test.beforeAll(async ({ request }) => {
    token = await login(request);
  });

  test('FAQ can be published without approval', async ({ request }) => {
    // Create a FAQ
    const createRes = await request.post(
      `${CMS}/content-manager/collection-types/api::faq.faq`,
      {
        headers: { ...auth(token), 'Content-Type': 'application/json' },
        data: {
          sporsmal: 'Test FAQ spørsmål?',
          svar: [
            {
              type: 'paragraph',
              children: [{ type: 'text', text: 'Test svar' }],
            },
          ],
          rekkefølge: 99,
          locale: 'nb',
        },
      }
    );
    expect(createRes.status()).toBe(201);
    const faqDocId = (await createRes.json()).data.documentId;

    // Publish directly — no workflow needed
    const pubRes = await request.post(
      `${CMS}/content-manager/collection-types/api::faq.faq/${faqDocId}/actions/publish`,
      { headers: auth(token), data: {} }
    );
    expect(pubRes.status()).toBe(200);
    const body = await pubRes.json();
    expect(body.data.publishedAt).toBeTruthy();
  });
});
