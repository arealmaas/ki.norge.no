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
  <title>KI Norge</title>
  <meta name="robots" content="noindex" />
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet" />
  <style>
    * { margin: 0; box-sizing: border-box; }
    body { min-height: 100vh; display: flex; align-items: center; justify-content: center; font-family: 'Inter', system-ui, sans-serif; background: #f8fafc; color: #1e293b; }
    .card { text-align: center; padding: 3rem 2rem; max-width: 600px; }
    h1 { font-size: 2rem; font-weight: 600; margin-bottom: 1rem; }
    p { font-size: 1.1rem; line-height: 1.6; color: #475569; }
  </style>
</head>
<body>
  <div class="card">
    <h1>KI Norge</h1>
    <p>Arenaen for innovativ og ansvarlig utvikling og bruk av kunstig intelligens i offentlig sektor er snart klar. Vi jobber med de siste detaljene.</p>
  </div>
</body>
</html>`;

export const onRequest = defineMiddleware(async (context, next) => {
  const { url, cookies } = context;

  // Admin access (status page, coming-soon bypass).
  // Visit /admin-tilgang?key=<ADMIN_SECRET> to set the ki_admin cookie.
  const adminSecret = process.env.ADMIN_SECRET || import.meta.env.ADMIN_SECRET || '';
  if (url.pathname === '/admin-tilgang') {
    const key = url.searchParams.get('key');
    if (key && adminSecret && key === adminSecret) {
      const res = new Response('Tilgang gitt! Du blir videresendt...', {
        status: 302,
        headers: { 'Location': '/status', 'Cache-Control': 'no-store' },
      });
      res.headers.append('Set-Cookie', `ki_admin=1; Path=/; Max-Age=${60 * 60 * 24 * 30}; SameSite=Lax; HttpOnly`);
      return res;
    }
    return new Response('Ugyldig nøkkel', { status: 401 });
  }

  // Status page requires admin cookie
  if (url.pathname === '/status' && !cookies.has('ki_admin')) {
    return new Response('Ikke autorisert. Trenger ki_admin-cookie. Bruk /admin-tilgang?key=<secret>', {
      status: 401,
      headers: { 'Content-Type': 'text/plain; charset=utf-8' },
    });
  }

  // Coming-soon wall for ki.norge.no only.
  // The Azure URL remains open. Admin cookie bypasses.
  // Remove this block entirely when ready to launch.
  const isKiNorgeDomain = url.hostname === 'ki.norge.no';
  const isComingSoon = isKiNorgeDomain || LAUNCH_MODE === 'coming-soon';

  if (isComingSoon) {
    const isApiRoute = url.pathname.startsWith('/api/');
    const hasAdminCookie = cookies.has('ki_admin');

    if (!isApiRoute && !hasAdminCookie) {
      return new Response(COMING_SOON_HTML, {
        status: 200,
        headers: { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' },
      });
    }
  }

  const isPreview =
    url.searchParams.has('preview') || cookies.has('preview');
  const isApiRoute = url.pathname.startsWith('/api/');
  const isAdminRoute = url.pathname === '/status' || url.pathname === '/admin-tilgang';

  const response = await next();

  // Don't cache preview, API, or admin routes
  if (isPreview || isApiRoute || isAdminRoute) {
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
