import type { APIRoute } from 'astro';

const PREVIEW_SECRET = import.meta.env.PREVIEW_SECRET || '';
const UMBRACO_URL = import.meta.env.UMBRACO_URL || 'http://localhost:5000';
const UMBRACO_API_KEY = import.meta.env.UMBRACO_API_KEY || '';

// Map content types to their frontend paths
const contentTypeRoutes: Record<string, (slug: string) => string> = {
  artikkel: (slug) => `/artikler/${slug}`,
  side: (slug) => `/${slug}`,
  eksempel: (slug) => `/eksempler/${slug}`,
  veiledning: (slug) => `/veiledning/${slug}`,
  faq: () => '/faq',
};

export const GET: APIRoute = async ({ url, cookies, redirect }) => {
  const secret = url.searchParams.get('secret');
  const type = url.searchParams.get('type');
  const id = url.searchParams.get('id') || url.searchParams.get('documentId');

  // Validate secret
  if (secret !== PREVIEW_SECRET) {
    return new Response('Invalid preview secret', { status: 401 });
  }

  if (!type || !id) {
    return new Response('Missing type or id', { status: 400 });
  }

  // Set preview cookie (expires in 1 hour)
  cookies.set('preview', JSON.stringify({ enabled: true, id }), {
    path: '/',
    maxAge: 60 * 60, // 1 hour
    httpOnly: true,
    sameSite: 'lax',
  });

  try {
    // Fetch the content from Umbraco Delivery API to get the slug
    const headers: HeadersInit = {
      'Accept': 'application/json',
      'Accept-Language': 'nb-NO',
    };

    if (UMBRACO_API_KEY) {
      headers['Api-Key'] = UMBRACO_API_KEY;
    }

    // Fetch by ID with preview mode
    const response = await fetch(
      `${UMBRACO_URL}/umbraco/delivery/api/v2/content/item/${id}?preview=true`,
      { headers }
    );

    if (!response.ok) {
      console.error('Umbraco fetch failed:', response.status, await response.text());
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

    // Redirect to the preview page with preview query param
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
