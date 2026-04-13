import { defineMiddleware } from 'astro:middleware';

/**
 * Edge caching middleware.
 *
 * Normal requests get Cache-Control headers so Cloudflare CDN caches them
 * at the edge. After the first visit, subsequent requests are served from
 * cache (~20ms) without invoking the Worker or hitting Umbraco.
 *
 * Preview requests (cookie or query param) bypass the cache entirely so
 * editors always see fresh draft content.
 *
 * Cache invalidation: Umbraco publishes trigger a Cloudflare cache purge
 * via webhook, so the next visitor gets a fresh render.
 */

const CACHE_MAX_AGE = 60 * 60; // 1 hour edge cache
const STALE_WHILE_REVALIDATE = 60 * 60 * 24; // serve stale for up to 24h while revalidating

const LAUNCH_MODE = process.env.LAUNCH_MODE || import.meta.env.LAUNCH_MODE || '';

const COMING_SOON_HTML = `<!doctype html>
<html lang="nb">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>KI Norge — Kommer snart</title>
  <meta name="robots" content="noindex" />
  <style>
    * { margin: 0; box-sizing: border-box; }
    body { min-height: 100vh; display: flex; align-items: center; justify-content: center; font-family: system-ui, sans-serif; background: #f8fafc; color: #1e293b; }
    .card { text-align: center; padding: 3rem 2rem; max-width: 520px; }
    h1 { font-size: 2rem; font-weight: 600; margin-bottom: 1rem; }
    p { font-size: 1.1rem; line-height: 1.6; color: #475569; }
    .tag { display: inline-block; margin-top: 1.5rem; padding: 0.35rem 1rem; background: #e5f2f7; color: #1a2a6d; border-radius: 99px; font-size: 0.85rem; font-weight: 500; }
  </style>
</head>
<body>
  <div class="card">
    <h1>KI Norge</h1>
    <p>Portalen for kunstig intelligens i offentlig sektor er snart klar. Vi jobber med de siste detaljene.</p>
    <span class="tag">Lansering juni 2026</span>
  </div>
</body>
</html>`;

export const onRequest = defineMiddleware(async (context, next) => {
  const { url, cookies } = context;

  // Coming-soon mode: show placeholder for all non-API routes.
  // Set LAUNCH_MODE=coming-soon as env var to activate.
  if (LAUNCH_MODE === 'coming-soon') {
    const isApiRoute = url.pathname.startsWith('/api/');
    if (!isApiRoute) {
      return new Response(COMING_SOON_HTML, {
        status: 200,
        headers: { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' },
      });
    }
  }

  const isPreview =
    url.searchParams.has('preview') || cookies.has('preview');
  const isApiRoute = url.pathname.startsWith('/api/');

  const response = await next();

  // Don't cache preview requests or API routes
  if (isPreview || isApiRoute) {
    response.headers.set('Cache-Control', 'private, no-store');
    return response;
  }

  // Cache everything else at the edge
  response.headers.set(
    'Cache-Control',
    `public, s-maxage=${CACHE_MAX_AGE}, stale-while-revalidate=${STALE_WHILE_REVALIDATE}`,
  );

  return response;
});
