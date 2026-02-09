const UMBRACO_URL = import.meta.env.UMBRACO_URL || 'http://localhost:5000';
const API_KEY = import.meta.env.UMBRACO_API_KEY;

// Preview mode options
export interface FetchOptions {
  preview?: boolean;
  locale?: string;
}

// Umbraco Content Delivery API response format
interface UmbracoResponse<T> {
  total: number;
  items: UmbracoItem[];
}

interface UmbracoItem {
  id: string;
  name: string;
  contentType: string;
  createDate: string;
  updateDate: string;
  route: { path: string; startItem: { id: string; path: string } };
  properties: Record<string, unknown>;
  cultures: Record<string, { path: string; startItem: { id: string; path: string } }>;
}

interface UmbracoSingleItem extends UmbracoItem {}

// Block List item from Umbraco Delivery API
export interface UmbracoBlock {
  contentType: string;
  content: Record<string, unknown>;
}

// Re-export compatible block types for BlocksRenderer
// Umbraco blocks come as { contentType, content: { propertyAlias: value } }
// The "tekst" element type has an "innhold" property with HTML string
export interface BlockNode {
  type: 'paragraph' | 'heading' | 'list' | 'quote' | 'code' | 'image' | 'link';
  children?: BlockChild[];
  level?: number;
  format?: 'ordered' | 'unordered';
  url?: string;
  image?: {
    url: string;
    alternativeText?: string;
  };
}

export interface BlockChild {
  type: 'text' | 'link';
  text?: string;
  bold?: boolean;
  italic?: boolean;
  underline?: boolean;
  strikethrough?: boolean;
  code?: boolean;
  url?: string;
  children?: BlockChild[];
}

// Content types matching Umbraco document type schemas
export interface Artikkel {
  id: string;
  documentId: string;
  tittel: string;
  slug: string;
  innhold?: UmbracoBlock[];
  createdAt: string;
  updatedAt: string;
  publishedAt: string;
}

export interface Side {
  id: string;
  documentId: string;
  tittel: string;
  slug: string;
  innhold?: UmbracoBlock[];
  template?: 'standard' | 'bred' | 'landingsside';
  seoTittel?: string;
  seoBeskrivelse?: string;
  createdAt: string;
  updatedAt: string;
  publishedAt: string;
  locale: string;
}

export interface Eksempel {
  id: string;
  documentId: string;
  tittel: string;
  slug: string;
  organisasjon?: string;
  beskrivelse?: UmbracoBlock[];
  verktoy?: string[];
  resultater?: string;
  status?: 'i_utvikling' | 'pilot' | 'i_drift' | 'avsluttet';
  bilde?: UmbracoMedia;
  merkelapper?: Merkelapp[];
  createdAt: string;
  updatedAt: string;
  publishedAt: string;
  locale: string;
}

export interface Veiledning {
  id: string;
  documentId: string;
  tittel: string;
  slug: string;
  innhold?: UmbracoBlock[];
  kategori?: Merkelapp;
  lenker?: { tekst: string; url: string; ekstern?: boolean }[];
  rekkefølge?: number;
  createdAt: string;
  updatedAt: string;
  publishedAt: string;
  locale: string;
}

export interface FAQ {
  id: string;
  documentId: string;
  sporsmal: string;
  svar?: UmbracoBlock[];
  kategori?: Merkelapp;
  rekkefølge?: number;
  createdAt: string;
  updatedAt: string;
  publishedAt: string;
  locale: string;
}

export interface Merkelapp {
  id: string;
  documentId: string;
  navn: string;
  slug: string;
  beskrivelse?: string;
  locale: string;
}

export interface UmbracoMedia {
  id: string;
  url: string;
  alternativeText?: string;
  width?: number;
  height?: number;
  focalPoint?: { left: number; top: number };
}

// Strapi-compatible response wrapper so page code can use .data and .meta
interface CompatResponse<T> {
  data: T[];
  meta: {
    pagination: {
      page: number;
      pageSize: number;
      pageCount: number;
      total: number;
    };
  };
}

// ── Generic fetch for Umbraco Content Delivery API v2 ───────────

const API_BASE = `${UMBRACO_URL}/umbraco/delivery/api/v2/content`;

async function fetchCollection<T>(
  contentType: string,
  options: FetchOptions & {
    filter?: string;
    sort?: string;
    skip?: number;
    take?: number;
  } = {}
): Promise<CompatResponse<T>> {
  const headers: HeadersInit = {
    'Accept': 'application/json',
    'Accept-Language': options.locale || 'nb-NO',
  };

  if (options.preview && API_KEY) {
    headers['Api-Key'] = API_KEY;
  }

  const params = new URLSearchParams();
  params.set('filter', `contentType:${contentType}`);
  if (options.filter) {
    params.append('filter', options.filter);
  }
  if (options.sort) {
    params.set('sort', options.sort);
  }
  if (options.take) {
    params.set('take', String(options.take));
  }
  if (options.skip) {
    params.set('skip', String(options.skip));
  }
  if (options.preview) {
    params.set('preview', 'true');
  }

  const url = `${API_BASE}?${params.toString()}`;

  try {
    const res = await fetch(url, { headers });

    if (!res.ok) {
      throw new Error(`Umbraco API error: ${res.status} ${res.statusText}`);
    }

    const data: UmbracoResponse<T> = await res.json();

    return {
      data: data.items.map((item) => mapItem<T>(item, contentType)),
      meta: {
        pagination: {
          page: 1,
          pageSize: options.take || data.total,
          pageCount: 1,
          total: data.total,
        },
      },
    };
  } catch (error) {
    console.error(`Failed to fetch from Umbraco: ${contentType}`, error);
    throw error;
  }
}

async function fetchBySlug<T>(
  contentType: string,
  slug: string,
  options: FetchOptions = {}
): Promise<T | null> {
  const headers: HeadersInit = {
    'Accept': 'application/json',
    'Accept-Language': options.locale || 'nb-NO',
  };

  if (options.preview && API_KEY) {
    headers['Api-Key'] = API_KEY;
  }

  const params = new URLSearchParams();
  params.set('filter', `contentType:${contentType}`);
  // Umbraco Delivery API filter by property value
  params.append('filter', `slug:${slug}`);
  params.set('take', '1');
  if (options.preview) {
    params.set('preview', 'true');
  }

  const url = `${API_BASE}?${params.toString()}`;

  try {
    const res = await fetch(url, { headers });

    if (!res.ok) {
      throw new Error(`Umbraco API error: ${res.status} ${res.statusText}`);
    }

    const data: UmbracoResponse<T> = await res.json();

    if (data.items.length === 0) return null;

    return mapItem<T>(data.items[0], contentType);
  } catch (error) {
    console.error(`Failed to fetch from Umbraco: ${contentType}/${slug}`, error);
    throw error;
  }
}

// ── Map Umbraco item to our content type interfaces ─────────────

function mapItem<T>(item: UmbracoItem, contentType: string): T {
  const props = item.properties;

  const base = {
    id: item.id,
    documentId: item.id,
    createdAt: item.createDate,
    updatedAt: item.updateDate,
    publishedAt: item.updateDate,
    locale: Object.keys(item.cultures || {})[0] || 'nb-NO',
  };

  switch (contentType) {
    case 'artikkel':
      return {
        ...base,
        tittel: props.tittel as string || item.name,
        slug: props.slug as string || '',
        innhold: mapBlockList(props.innhold),
      } as T;

    case 'side':
      return {
        ...base,
        tittel: props.tittel as string || item.name,
        slug: props.slug as string || '',
        innhold: mapBlockList(props.innhold),
        template: props.template as string || 'standard',
        seoTittel: props.seoTittel as string || '',
        seoBeskrivelse: props.seoBeskrivelse as string || '',
      } as T;

    case 'eksempel':
      return {
        ...base,
        tittel: props.tittel as string || item.name,
        slug: props.slug as string || '',
        organisasjon: props.organisasjon as string || '',
        beskrivelse: mapBlockList(props.beskrivelse),
        verktoy: parseJsonArray(props.verktoy as string),
        resultater: props.resultater as string || '',
        status: props.status as string || undefined,
        bilde: mapMedia(props.bilde),
        merkelapper: mapMerkelapper(props.merkelapper),
      } as T;

    case 'veiledning':
      return {
        ...base,
        tittel: props.tittel as string || item.name,
        slug: props.slug as string || '',
        innhold: mapBlockList(props.innhold),
        kategori: mapKategori(props.kategori),
        lenker: mapLenker(props.lenker),
        rekkefølge: props.rekkefolge as number || 0,
      } as T;

    case 'faq':
      return {
        ...base,
        sporsmal: props.sporsmal as string || item.name,
        svar: mapBlockList(props.svar),
        kategori: mapKategori(props.kategori),
        rekkefølge: props.rekkefolge as number || 0,
      } as T;

    case 'merkelapp':
      return {
        ...base,
        navn: props.navn as string || item.name,
        slug: props.slug as string || '',
        beskrivelse: props.beskrivelse as string || '',
      } as T;

    default:
      return { ...base, ...props } as T;
  }
}

// ── Mapping helpers ─────────────────────────────────────────────

function mapBlockList(value: unknown): UmbracoBlock[] | undefined {
  if (!value) return undefined;
  if (Array.isArray(value)) {
    return value.map((block: any) => ({
      contentType: block.contentType || block.content?.contentType || 'tekst',
      content: block.content || block,
    }));
  }
  return undefined;
}

function mapMedia(value: unknown): UmbracoMedia | undefined {
  if (!value) return undefined;
  if (Array.isArray(value) && value.length > 0) {
    const media = value[0];
    return {
      id: media.id || '',
      url: media.url || media.mediaUrl || '',
      alternativeText: media.altText || media.name || '',
      width: media.width,
      height: media.height,
      focalPoint: media.focalPoint,
    };
  }
  if (typeof value === 'object' && value !== null) {
    const media = value as any;
    return {
      id: media.id || '',
      url: media.url || media.mediaUrl || '',
      alternativeText: media.altText || media.name || '',
      width: media.width,
      height: media.height,
    };
  }
  return undefined;
}

function mapMerkelapper(value: unknown): Merkelapp[] {
  if (!value || !Array.isArray(value)) return [];
  return value.map((item: any) => ({
    id: item.id || '',
    documentId: item.id || '',
    navn: item.properties?.navn || item.name || '',
    slug: item.properties?.slug || '',
    beskrivelse: item.properties?.beskrivelse || '',
    locale: 'nb-NO',
  }));
}

function mapKategori(value: unknown): Merkelapp | undefined {
  if (!value) return undefined;
  const item = value as any;
  return {
    id: item.id || '',
    documentId: item.id || '',
    navn: item.properties?.navn || item.name || '',
    slug: item.properties?.slug || '',
    beskrivelse: item.properties?.beskrivelse || '',
    locale: 'nb-NO',
  };
}

function mapLenker(value: unknown): { tekst: string; url: string; ekstern?: boolean }[] {
  if (!value) return [];
  // Block list of lenke items
  if (Array.isArray(value)) {
    return value.map((block: any) => {
      const content = block.content || block;
      return {
        tekst: content.tekst || '',
        url: content.url || '',
        ekstern: content.ekstern || false,
      };
    });
  }
  // JSON string fallback
  if (typeof value === 'string') {
    try {
      return JSON.parse(value);
    } catch {
      return [];
    }
  }
  return [];
}

function parseJsonArray(value: string | undefined): string[] {
  if (!value) return [];
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

// Helper to get full media URL
export function getMediaUrl(media?: UmbracoMedia): string | undefined {
  if (!media?.url) return undefined;
  if (media.url.startsWith('http')) return media.url;
  return `${UMBRACO_URL}${media.url}`;
}

// ── Artikkel API functions ──────────────────────────────────────

export async function getArtikler(limit?: number, options: FetchOptions = {}) {
  return fetchCollection<Artikkel>('artikkel', {
    ...options,
    sort: 'updateDate:desc',
    take: limit,
  });
}

export async function getArtikkel(slug: string, options: FetchOptions = {}) {
  return fetchBySlug<Artikkel>('artikkel', slug, options);
}

// ── Side (Page) API functions ───────────────────────────────────

export async function getSider(options: FetchOptions = {}) {
  return fetchCollection<Side>('side', options);
}

export async function getSide(slug: string, options: FetchOptions = {}) {
  return fetchBySlug<Side>('side', slug, options);
}

// ── Eksempel (Case) API functions ───────────────────────────────

export async function getEksempler(options: FetchOptions = {}) {
  return fetchCollection<Eksempel>('eksempel', {
    ...options,
    sort: 'createDate:desc',
  });
}

export async function getEksempel(slug: string, options: FetchOptions = {}) {
  return fetchBySlug<Eksempel>('eksempel', slug, options);
}

// ── Veiledning (Guidance) API functions ─────────────────────────

export async function getVeiledninger(options: FetchOptions = {}) {
  return fetchCollection<Veiledning>('veiledning', {
    ...options,
    sort: 'sortOrder:asc',
  });
}

export async function getVeiledning(slug: string, options: FetchOptions = {}) {
  return fetchBySlug<Veiledning>('veiledning', slug, options);
}

// ── FAQ API functions ───────────────────────────────────────────

export async function getFAQs(options: FetchOptions = {}) {
  return fetchCollection<FAQ>('faq', {
    ...options,
    sort: 'sortOrder:asc',
  });
}

export async function getFAQsByKategori(kategoriSlug: string, options: FetchOptions = {}) {
  return fetchCollection<FAQ>('faq', {
    ...options,
    sort: 'sortOrder:asc',
  });
}

// ── Merkelapp (Tag) API functions ───────────────────────────────

export async function getMerkelapper(options: FetchOptions = {}) {
  return fetchCollection<Merkelapp>('merkelapp', options);
}

export async function getMerkelapp(slug: string, options: FetchOptions = {}) {
  return fetchBySlug<Merkelapp>('merkelapp', slug, options);
}

// Legacy compatibility — map old names to new ones
export const getArticles = getArtikler;
export const getArticle = getArtikkel;
export const getPages = getSider;
export const getPage = getSide;
export const getCases = getEksempler;
export const getCase = getEksempel;

// Re-export StrapiMedia as UmbracoMedia for backward compat
export type StrapiMedia = UmbracoMedia;
