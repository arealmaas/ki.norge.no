import type { APIRoute } from 'astro';
export const prerender = false;

// Map content types to their frontend paths
const contentTypeRoutes: Record<string, (slug: string) => string> = {
  artikkel: (slug) => `/artikler/${slug}`,
  side: (slug) => `/${slug}`,
  eksempel: (slug) => `/eksempler/${slug}`,
  veiledning: (slug) => `/veiledning/${slug}`,
  faq: () => '/faq',
};

export const GET: APIRoute = async ({ url, cookies, redirect }) => {
  const PREVIEW_SECRET = import.meta.env.PREVIEW_SECRET || '';
  const UMBRACO_URL = import.meta.env.UMBRACO_URL || 'http://localhost:5000';
  const UMBRACO_API_KEY = import.meta.env.UMBRACO_API_KEY || '';

  // Exit preview mode
  if (url.searchParams.has('exit')) {
    cookies.delete('preview', { path: '/' });
    const returnTo = url.searchParams.get('returnTo') || '/';
    return redirect(returnTo, 307);
  }

  const secret = url.searchParams.get('secret');
  const type = url.searchParams.get('type');
  const id = url.searchParams.get('id') || url.searchParams.get('documentId');

  // Validate secret
  if (!PREVIEW_SECRET || secret !== PREVIEW_SECRET) {
    return new Response('Invalid preview secret', { status: 401 });
  }

  if (!type || !id) {
    return new Response('Missing type or id', { status: 400 });
  }

  if (!UMBRACO_API_KEY) {
    return new Response('UMBRACO_API_KEY not configured — required for preview', { status: 500 });
  }

  // Set preview cookie (expires in 1 hour)
  cookies.set('preview', JSON.stringify({ enabled: true, id }), {
    path: '/',
    maxAge: 60 * 60,
    httpOnly: true,
    sameSite: 'lax',
  });

  try {
    // Fetch the content from Umbraco Delivery API to get the slug
    const response = await fetch(
      `${UMBRACO_URL}/umbraco/delivery/api/v2/content/item/${id}?preview=true`,
      {
        headers: {
          'Accept': 'application/json',
          'Api-Key': UMBRACO_API_KEY,
        },
      }
    );

    if (!response.ok) {
      console.error('Umbraco preview fetch failed:', response.status, await response.text());
      return new Response('Failed to fetch content from Umbraco', { status: 500 });
    }

    const content = await response.json();

    if (!content) {
      return new Response('Content not found', { status: 404 });
    }

    // Get the slug from properties and build redirect URL
    const slug = content.properties?.slug || content.route?.path?.split('/').pop() || id;
    const routeBuilder = contentTypeRoutes[type];

    if (!routeBuilder) {
      return new Response(`No route configured for type: ${type}`, { status: 400 });
    }

    const targetPath = routeBuilder(slug);
    return redirect(`${targetPath}?preview=true`, 307);
  } catch (error) {
    console.error('Preview error:', error);
    return new Response('Preview error', { status: 500 });
  }
};

// Also handle POST for webhook-style requests
export const POST: APIRoute = async (context) => {
  return GET(context);
};
