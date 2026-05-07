import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test.describe('Accessibility tests (WCAG 2.1 AA)', () => {
  test('homepage has no critical accessibility violations', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
      .analyze();

    // Filter out violations to focus on serious issues
    const criticalViolations = results.violations.filter(
      (v) => v.impact === 'critical' || v.impact === 'serious'
    );

    expect(criticalViolations).toEqual([]);
  });

  test('articles listing page has no critical accessibility violations', async ({ page }) => {
    await page.goto('/artikler');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
      .analyze();

    const criticalViolations = results.violations.filter(
      (v) => v.impact === 'critical' || v.impact === 'serious'
    );

    expect(criticalViolations).toEqual([]);
  });

  test('FAQ page has no critical accessibility violations', async ({ page }) => {
    await page.goto('/faq');
    await page.waitForLoadState('load');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
      .analyze();

    const criticalViolations = results.violations.filter(
      (v) => v.impact === 'critical' || v.impact === 'serious'
    );

    expect(criticalViolations).toEqual([]);
  });

  test('contact page has no critical accessibility violations', async ({ page }) => {
    await page.goto('/kontakt');
    await page.waitForLoadState('networkidle');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
      .analyze();

    const criticalViolations = results.violations.filter(
      (v) => v.impact === 'critical' || v.impact === 'serious'
    );

    expect(criticalViolations).toEqual([]);
  });
});

test.describe('Dark mode accessibility', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('theme', 'dark');
    });
  });

  test('homepage in dark mode has no critical accessibility violations', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Verify dark mode is active
    const html = page.locator('html');
    await expect(html).toHaveClass(/dark/);

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
      .analyze();

    const criticalViolations = results.violations.filter(
      (v) => v.impact === 'critical' || v.impact === 'serious'
    );

    expect(criticalViolations).toEqual([]);
  });
});

test.describe('Landmark and structure', () => {
  test('all pages have valid landmarks', async ({ page }) => {
    const pages = ['/', '/artikler', '/faq', '/kontakt', '/om-oss'];

    for (const url of pages) {
      await page.goto(url);
      await page.waitForLoadState('load');

      // Should have exactly one main landmark (app main, not dev tools)
      const main = page.locator('main#main-content');
      await expect(main).toHaveCount(1);

      // Should have a header (use class to exclude dev toolbar)
      const header = page.locator('header.header');
      await expect(header).toHaveCount(1);

      // Should have a footer (use class to exclude dev toolbar)
      const footer = page.locator('footer.footer');
      await expect(footer).toHaveCount(1);

      // Should have navigation (use class to exclude dev toolbar)
      const nav = page.locator('nav.nav-desktop, nav.nav-mobile, header.header nav');
      const navCount = await nav.count();
      expect(navCount).toBeGreaterThanOrEqual(1);
    }
  });

  test('all images have alt text', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const images = page.locator('img');
    const count = await images.count();

    for (let i = 0; i < count; i++) {
      const img = images.nth(i);
      const alt = await img.getAttribute('alt');
      // Alt can be empty string for decorative images, but must exist
      expect(alt).not.toBeNull();
    }
  });
});

test.describe('Keyboard navigation', () => {
  test('can navigate through main interactive elements with keyboard', async ({ page }) => {
    await page.goto('/');

    // Start tabbing through the page
    const focusableElements: string[] = [];

    // Tab through first 20 focusable elements
    for (let i = 0; i < 20; i++) {
      await page.keyboard.press('Tab');
      const focused = await page.evaluate(() => {
        const el = document.activeElement;
        return el ? el.tagName.toLowerCase() : null;
      });
      if (focused) {
        focusableElements.push(focused);
      }
    }

    // Should have navigated through multiple elements
    expect(focusableElements.length).toBeGreaterThan(0);

    // Should include links and buttons
    const hasLinks = focusableElements.includes('a');
    const hasButtons = focusableElements.includes('button');
    expect(hasLinks || hasButtons).toBe(true);
  });

  test('focus indicators are visible', async ({ page }) => {
    await page.goto('/');

    // Tab to first interactive element
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    // Get the focused element
    const focusedElement = page.locator(':focus');

    // Check that focus is visible (has outline or other visible indicator)
    const outline = await focusedElement.evaluate((el) => {
      const style = window.getComputedStyle(el);
      return {
        outline: style.outline,
        outlineWidth: style.outlineWidth,
        boxShadow: style.boxShadow,
      };
    });

    // Element should have some visible focus indicator
    const hasVisibleFocus =
      outline.outlineWidth !== '0px' ||
      outline.outline !== 'none' ||
      outline.boxShadow !== 'none';

    expect(hasVisibleFocus).toBe(true);
  });
});

test.describe('Skip link', () => {
  test('skip link is present and functional', async ({ page }) => {
    await page.goto('/');

    // Skip link should exist
    const skipLink = page.locator('a[href="#main-content"]');
    await expect(skipLink).toHaveCount(1);

    // Skip link should become visible on focus
    await page.keyboard.press('Tab');
    const skipLinkFocused = page.locator('.skip-link:focus');
    await expect(skipLinkFocused).toBeVisible();

    // Activating skip link should navigate to main content
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL('/#main-content');
  });
});

test.describe('Form accessibility', () => {
  test('form labels are properly associated', async ({ page }) => {
    await page.goto('/eksempler/send-inn');
    await page.waitForLoadState('load');

    const inputs = page.locator('form input:not([type="hidden"]):not([type="checkbox"]), form select, form textarea');
    const count = await inputs.count();

    // This page must have form inputs — fail if it doesn't
    expect(count).toBeGreaterThan(0);

    for (let i = 0; i < count; i++) {
      const input = inputs.nth(i);
      const id = await input.getAttribute('id');
      const ariaLabel = await input.getAttribute('aria-label');
      const ariaLabelledBy = await input.getAttribute('aria-labelledby');

      // Each input should have either an associated label, aria-label, or aria-labelledby
      if (id) {
        const label = page.locator(`label[for="${id}"]`);
        const hasLabel = (await label.count()) > 0;
        expect(hasLabel || ariaLabel || ariaLabelledBy).toBeTruthy();
      } else {
        expect(ariaLabel || ariaLabelledBy).toBeTruthy();
      }
    }
  });
});
