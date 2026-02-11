import { test, expect } from '@playwright/test';

test.describe('Navigation', () => {
  test('all main nav links navigate correctly', async ({ page }) => {
    await page.goto('/');

    // Test Veiledning link
    await page.click('nav a[href="/veiledning"]');
    await expect(page).toHaveURL('/veiledning');

    // Test Sandkasse link
    await page.goto('/');
    await page.click('nav a[href="/sandkasse"]');
    await expect(page).toHaveURL('/sandkasse');

    // Test Eksempler link
    await page.goto('/');
    await page.click('nav a[href="/eksempler"]');
    await expect(page).toHaveURL('/eksempler');

    // Test Artikler link
    await page.goto('/');
    await page.click('nav a[href="/artikler"]');
    await expect(page).toHaveURL('/artikler');

    // Test Om oss link
    await page.goto('/');
    await page.click('nav a[href="/om-oss"]');
    await expect(page).toHaveURL('/om-oss');
  });

  test('logo navigates to homepage', async ({ page }) => {
    await page.goto('/artikler');
    await page.click('header a[href="/"]');
    await expect(page).toHaveURL('/');
  });
});

test.describe('Skip link accessibility', () => {
  test('skip link focuses main content when activated', async ({ page }) => {
    await page.goto('/');

    // Tab to the skip link
    await page.keyboard.press('Tab');

    // The skip link should be visible when focused
    const skipLink = page.locator('.skip-link');
    await expect(skipLink).toBeFocused();

    // Activate the skip link
    await page.keyboard.press('Enter');

    // Main content should have focus or be the target
    await expect(page).toHaveURL('/#main-content');
  });
});

test.describe('Mobile menu', () => {
  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
  });

  test('mobile menu toggle opens and closes', async ({ page }) => {
    await page.goto('/');

    const menuToggle = page.locator('[data-mobile-menu-toggle]');
    const mobileNav = page.locator('#mobile-nav');

    // Menu should be hidden initially
    await expect(mobileNav).toBeHidden();

    // Click toggle to open
    await menuToggle.click();
    await expect(mobileNav).toBeVisible();
    await expect(menuToggle).toHaveAttribute('aria-expanded', 'true');

    // Click toggle to close
    await menuToggle.click();
    await expect(mobileNav).toBeHidden();
    await expect(menuToggle).toHaveAttribute('aria-expanded', 'false');
  });

  test('mobile menu closes on Escape key', async ({ page }) => {
    await page.goto('/');

    const menuToggle = page.locator('[data-mobile-menu-toggle]');
    const mobileNav = page.locator('#mobile-nav');

    // Open menu
    await menuToggle.click();
    await expect(mobileNav).toBeVisible();

    // Press Escape
    await page.keyboard.press('Escape');

    // Menu should close
    await expect(mobileNav).toBeHidden();
  });
});

test.describe('Dark mode', () => {
  test('dark mode toggle switches theme', async ({ page }) => {
    await page.goto('/');

    const html = page.locator('html');
    const toggle = page.locator('[data-theme-toggle]');

    // Should start in light mode
    await expect(html).not.toHaveClass(/dark/);

    // Click toggle
    await toggle.click();

    // Should switch to dark mode
    await expect(html).toHaveClass(/dark/);
    await expect(html).toHaveAttribute('data-ds-color-mode', 'dark');

    // Click again
    await toggle.click();

    // Should switch back to light mode
    await expect(html).not.toHaveClass(/dark/);
    await expect(html).toHaveAttribute('data-ds-color-mode', 'light');
  });

  test('dark mode persists on navigation', async ({ page }) => {
    await page.goto('/');

    const html = page.locator('html');
    const toggle = page.locator('[data-theme-toggle]');

    // Switch to dark mode
    await toggle.click();
    await expect(html).toHaveClass(/dark/);

    // Navigate to another page
    await page.goto('/artikler');

    // Dark mode should persist
    await expect(html).toHaveClass(/dark/);
  });

  test('dark mode persists on refresh', async ({ page }) => {
    await page.goto('/');

    const html = page.locator('html');
    const toggle = page.locator('[data-theme-toggle]');

    // Switch to dark mode
    await toggle.click();
    await expect(html).toHaveClass(/dark/);

    // Refresh the page
    await page.reload();

    // Dark mode should persist
    await expect(html).toHaveClass(/dark/);
  });
});

test.describe('Card interactions', () => {
  test('article cards are hoverable and clickable', async ({ page }) => {
    await page.goto('/artikler');

    // Articles must render cards — fail if none found
    const cards = page.locator('.article-card');
    const cardCount = await cards.count();
    expect(cardCount).toBeGreaterThan(0);

    const firstCard = cards.first();
    await expect(firstCard).toBeVisible();

    // Hover should work
    await firstCard.hover();

    // Card should contain a link that navigates to an article
    const link = firstCard.locator('a').first();
    const href = await link.getAttribute('href');
    expect(href).toBeTruthy();

    await link.click();
    await expect(page).toHaveURL(new RegExp(href!.replace(/\//g, '\\/')));
  });
});

test.describe('Responsive breakpoints', () => {
  test('layout at 375px (mobile)', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/');

    // Mobile menu button should be visible
    const mobileToggle = page.locator('[data-mobile-menu-toggle]');
    await expect(mobileToggle).toBeVisible();

    // Desktop nav should be hidden
    const desktopNav = page.locator('.nav-desktop');
    await expect(desktopNav).toBeHidden();
  });

  test('layout at 768px (tablet)', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/');

    const mobileToggle = page.locator('[data-mobile-menu-toggle]');
    const desktopNav = page.locator('.nav-desktop');

    const mobileVisible = await mobileToggle.isVisible();
    const desktopVisible = await desktopNav.isVisible();

    // Exactly one navigation mode should be active — not both, not neither
    expect(mobileVisible || desktopVisible).toBe(true);
    expect(mobileVisible && desktopVisible).toBe(false);
  });

  test('layout at 1280px (desktop)', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.goto('/');

    // Desktop nav should be visible
    const desktopNav = page.locator('.nav-desktop');
    await expect(desktopNav).toBeVisible();

    // Mobile menu button should be hidden
    const mobileToggle = page.locator('[data-mobile-menu-toggle]');
    await expect(mobileToggle).toBeHidden();
  });
});
