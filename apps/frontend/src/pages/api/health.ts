import type { APIRoute } from 'astro';

/**
 * Liveness probe — process is alive, no upstream check.
 * Used by Container Apps to detect if the app needs to be restarted.
 * Always returns 200 unless the Astro runtime itself is dead (in which case
 * the request never reaches here).
 */
export const GET: APIRoute = async () => {
  return new Response(
    JSON.stringify({ status: 'ok', ts: new Date().toISOString() }),
    {
      status: 200,
      headers: {
        'Content-Type': 'application/json',
        'Cache-Control': 'no-store',
      },
    },
  );
};
