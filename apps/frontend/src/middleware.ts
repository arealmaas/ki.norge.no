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

export const onRequest = defineMiddleware(async (context, next) => {
  const { url, cookies } = context;

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
