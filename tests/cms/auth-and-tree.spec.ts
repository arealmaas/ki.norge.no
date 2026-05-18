import { test, expect, Page } from '@playwright/test';

const ADMIN_EMAIL = process.env.CMS_USER || 'admin@ki.norge.no';
const ADMIN_PASS = process.env.CMS_PASS || 'KiNorge2025!';

/**
 * Logs into Umbraco backoffice. Reusable across tests.
 */
async function login(page: Page) {
  await page.goto('/umbraco');
  await page.waitForLoadState('domcontentloaded');

  // Umbraco 17 uses Lit components; login form is shadow DOM in some places.
  // Try to find the email input — fall back across selectors.
  const emailInput = page.locator('input[name="email"], input[type="email"]').first();
  await emailInput.waitFor({ state: 'visible', timeout: 30_000 });
  await emailInput.fill(ADMIN_EMAIL);

  const passwordInput = page.locator('input[name="password"], input[type="password"]').first();
  await passwordInput.fill(ADMIN_PASS);

  await page.getByRole('button', { name: /log in|logg inn|sign in/i }).first().click();

  // Wait for backoffice to load — look for the Content section nav
  await page.waitForURL(/\/umbraco\/section\/.+/, { timeout: 30_000 });
}

test('Admin can log in', async ({ page }) => {
  await login(page);
  // Section bar should be visible
  await expect(page.locator('text=/Content|Innhold/i').first()).toBeVisible();
});

test('Content tree shows expected structure', async ({ page }) => {
  await login(page);
  await page.goto('/umbraco/section/content');
  await page.waitForLoadState('networkidle');

  // Forside should be at the top
  const tree = page.locator('umb-tree, [role="tree"]').first();
  // Items we expect (post-cleanup): Forside, Artikler, Caser, Veiledning, Sider, Ofte stilte spørsmål, Merkelapper, KI-ordbok
  for (const name of ['Forside', 'Artikler', 'Caser', 'Veiledning', 'Sider', 'Merkelapper']) {
    await expect(page.locator(`text=${name}`).first()).toBeVisible({ timeout: 10_000 });
  }

  // Eksempler should NOT be in the tree (migrated to Caser, container deleted)
  await expect(page.locator('text=Eksempler')).toHaveCount(0);
  // Ikoner should NOT be in the tree
  await expect(page.locator('text=Tilgjengelige ikoner')).toHaveCount(0);
});

test('Diagnostics endpoint reports valid state', async ({ request }) => {
  const res = await request.get('/api/diagnostics');
  expect(res.ok()).toBeTruthy();
  const data = await res.json();
  expect(data.artikkelFields.hasIngress).toBe(true);
  expect(data.artikkelFields.hasBilde).toBe(true);
  expect(data.richTextDataTypes.length).toBeGreaterThanOrEqual(2);
  // Verify both standard and restricted RichText exist
  const names = data.richTextDataTypes.map((d: any) => d.name);
  expect(names).toContain('Richtext editor');
  expect(names).toContain('Richtext editor (begrenset)');
});
