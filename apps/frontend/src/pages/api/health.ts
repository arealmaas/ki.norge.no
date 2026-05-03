import type { APIRoute } from 'astro';

const startTime = Date.now();

const buildSha =
  import.meta.env.BUILD_SHA ||
  import.meta.env.GITHUB_SHA ||
  'unknown';

const buildDate =
  import.meta.env.BUILD_DATE ||
  'unknown';

export const GET: APIRoute = async () => {
  const uptimeSec = Math.floor((Date.now() - startTime) / 1000);
  return new Response(
    JSON.stringify({
      status: 'ok',
      ts: new Date().toISOString(),
      uptimeSec,
      build: { sha: buildSha, date: buildDate },
    }),
    {
      status: 200,
      headers: {
        'Content-Type': 'application/json',
        'Cache-Control': 'no-store',
      },
    },
  );
};
