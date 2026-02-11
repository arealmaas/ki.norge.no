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

// ── Umbraco RichText JSON → HTML converter ──────────────────────

interface RichTextNode {
  tag: string;
  text?: string;
  attributes?: Record<string, string>;
  elements?: RichTextNode[];
}

function richTextToHtml(node: RichTextNode): string {
  // Text node
  if (node.tag === '#text') {
    return escapeHtml(node.text || '');
  }

  // Root node — just render children
  if (node.tag === '#root') {
    return (node.elements || []).map(richTextToHtml).join('');
  }

  // Comment node
  if (node.tag === '#comment') return '';

  // Self-closing tags
  const selfClosing = ['br', 'hr', 'img', 'input'];
  const children = (node.elements || []).map(richTextToHtml).join('');

  // Heading tags — inject id for TOC anchor links
  if (/^h[1-6]$/.test(node.tag)) {
    const text = nodeToPlainText(node);
    const id = text.toLowerCase().replace(/[^a-zæøå0-9]+/g, '-').replace(/(^-|-$)/g, '');
    const attrs = renderAttributes(node.attributes);
    return `<${node.tag}${attrs} id="${id}">${children}</${node.tag}>`;
  }

  const attrs = renderAttributes(node.attributes);

  if (selfClosing.includes(node.tag)) {
    return `<${node.tag}${attrs} />`;
  }

  return `<${node.tag}${attrs}>${children}</${node.tag}>`;
}

/** Extract plain text from a RichText AST node (used for heading id generation) */
function nodeToPlainText(node: RichTextNode): string {
  if (node.tag === '#text') return node.text || '';
  if (node.tag === '#comment') return '';
  return (node.elements || []).map(nodeToPlainText).join('');
}

function renderAttributes(attrs?: Record<string, string>): string {
  if (!attrs || Object.keys(attrs).length === 0) return '';
  return Object.entries(attrs)
    .map(([key, value]) => ` ${key}="${escapeHtml(value)}"`)
    .join('');
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
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
  };

  // Only send Accept-Language if content has culture variants
  if (options.locale) {
    headers['Accept-Language'] = options.locale;
  }

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
  // Umbraco Delivery API doesn't support filtering by custom properties.
  // Fetch all items of the type and find by slug client-side.
  // This is fine for small content sets (< 100 items).
  const result = await fetchCollection<T>(contentType, {
    ...options,
    take: 100,
  });
  const item = result.data.find((item: any) => item.slug === slug);
  return item || null;
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
        innhold: mapRichText(props.innhold),
      } as T;

    case 'side':
      return {
        ...base,
        tittel: props.tittel as string || item.name,
        slug: props.slug as string || '',
        innhold: mapRichText(props.innhold),
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
        beskrivelse: mapRichText(props.beskrivelse),
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
        innhold: mapRichText(props.innhold),
        kategori: mapKategori(props.kategori),
        lenker: mapLenker(props.lenker),
        rekkefølge: props.rekkefolge as number || 0,
      } as T;

    case 'faq':
      return {
        ...base,
        sporsmal: props.sporsmal as string || item.name,
        svar: mapRichText(props.svar),
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

/**
 * Handle Umbraco rich text which comes as JSON AST: { tag: "#root", elements: [...] }
 * Convert to a single UmbracoBlock with contentType "tekst" containing HTML.
 * Also handles Block List arrays (future use).
 */
function mapRichText(value: unknown): UmbracoBlock[] | undefined {
  if (!value) return undefined;

  // Umbraco RichText JSON: { tag: "#root", elements: [...] }
  if (typeof value === 'object' && !Array.isArray(value) && (value as any).tag === '#root') {
    const html = richTextToHtml(value as RichTextNode);
    if (!html) return undefined;
    return [{ contentType: 'tekst', content: { innhold: html } }];
  }

  // Block List array (if we switch to Block List editor later)
  if (Array.isArray(value)) {
    return value.map((block: any) => ({
      contentType: block.contentType || block.content?.contentType || 'tekst',
      content: block.content || block,
    }));
  }

  // Plain HTML string
  if (typeof value === 'string' && value.trim()) {
    return [{ contentType: 'tekst', content: { innhold: value } }];
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

/**
 * Extract plain text from UmbracoBlock[] (useful for excerpts and SEO).
 * Strips HTML tags from the tekst block's innhold.
 */
export function getPlainText(blocks?: UmbracoBlock[], maxLength?: number): string {
  if (!blocks || blocks.length === 0) return '';
  const html = blocks
    .filter(b => b.contentType === 'tekst')
    .map(b => b.content.innhold as string || '')
    .join(' ');
  // Strip HTML tags
  const text = html.replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim();
  if (maxLength && text.length > maxLength) {
    return text.slice(0, maxLength).replace(/\s+\S*$/, '') + '…';
  }
  return text;
}

/**
 * Extract headings (h2, h3) from UmbracoBlock[] HTML content.
 * Used for building table-of-contents navigation.
 */
export function extractHeadings(blocks?: UmbracoBlock[]): { text: string; id: string; level: number }[] {
  if (!blocks || blocks.length === 0) return [];
  const html = blocks
    .filter(b => b.contentType === 'tekst')
    .map(b => b.content.innhold as string || '')
    .join('');
  const headings: { text: string; id: string; level: number }[] = [];
  const regex = /<h([23])[^>]*>(.*?)<\/h\1>/gi;
  let match;
  while ((match = regex.exec(html)) !== null) {
    const level = parseInt(match[1], 10);
    const text = match[2].replace(/<[^>]+>/g, '').trim();
    const id = text.toLowerCase().replace(/[^a-zæøå0-9]+/g, '-').replace(/(^-|-$)/g, '');
    headings.push({ text, id, level });
  }
  return headings;
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

// English aliases
export const getArticles = getArtikler;
export const getArticle = getArtikkel;
export const getPages = getSider;
export const getPage = getSide;
export const getCases = getEksempler;
export const getCase = getEksempel;
