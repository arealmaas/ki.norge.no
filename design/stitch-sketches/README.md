# Stitch Web Design Sketches

AI-generated design sketches created with Google Stitch (February 2025).
These are visual references for page layouts, not production code. The HTML
files use Tailwind CDN and placeholder images — they are design artifacts,
not implementation starting points.

## How these were made

Two initial designs (homepage + veileder detail) were created in Stitch and
used to establish the visual language: Public Sans font, #136dec primary
blue, light gray backgrounds, Material Symbols icons. These two were then
fed back to Stitch as context along with a detailed prompt describing 13
additional pages. Stitch generated 10 of them.

## Important: consistency issues

Each page was generated independently by Stitch, so **the header, navigation,
and footer vary across pages**. The canonical versions are:

- **Branding:** "KI Norge" (not "KI-portalen")
- **Nav:** Veiledning, Sandkasse, Eksempler, Artikler, Om oss
- **Footer:** Full (3-col with partners) for landing pages, minimal for content pages

When implementing, always use the header/nav/footer from the actual codebase
(`apps/frontend/src/components/shared/`), not from these sketches.

## Page status

### Originals (established visual language)

| Folder | Page | Status |
|---|---|---|
| `original-homepage/` | Homepage (/) | Implemented |
| `original-veileder-detail/` | Veileder detail (/veiledning/[slug]) | Implemented |

### Batch 2 (generated from prompt)

| Folder | Page | Route | Status | Notes |
|---|---|---|---|---|
| `guidance_hub_library/` | Veiledning hub | /veiledning | **To implement** | Best design in the set. Resource library with search, category cards, document table, AI chat CTA. Replaces current basic card grid. |
| `search_results_page/` | Search results | /sok | **To implement** | New page. Filter sidebar, highlighted terms, type tags, pagination. |
| `case_study_detail_page/` | Eksempel detail | /eksempler/[slug] | **To implement** | Better than current. Structured sections (Om prosjektet, Utfordringen, Løsningen), metadata grid, tags. |
| `sandbox_information_page/` | Sandkasse info | /sandkasse | **To implement** | Good concept. Use dark hero only, switch to light theme for body content. Steps timeline is well done. |
| `about_page/` | Om oss | /om-oss | **To implement** | Partner cards, "Hva vi tilbyr" section, newsletter CTA. |
| `faq_page/` | FAQ | /faq | **To implement** | Sidebar categories + accordion. Merge with existing Designsystemet Details components. |
| `article_listing_page/` | Artikler listing | /artikler | **To implement** | Category filter pills to add to current gradient hero design. |
| `case_study_listing_page/` | Eksempler listing | /eksempler | **To implement** | Sector filter pills, sidebar CTA for project submissions. |
| `404_error_page/` | 404 error | (fallback) | **To implement** | Clean watermark design with two CTA buttons. |
| `ai_guidance_chat/` | AI guidance chat | /veiledning/chat | **Parked (Phase 2)** | Full chat interface with conversation history, categories, disclaimers, suggested follow-ups. Requires LLM backend, auth, resource indexing. The veiledning hub's "Start samtale" CTA will link here eventually. |

### Not designed (simple text pages, not needed from Stitch)

- /kontakt — contact page (current coded version is adequate)
- /artikler/[slug] — article detail (reuse veileder detail layout, simpler: no ToC)
- /personvern — privacy policy (text-only legal page)
- /tilgjengelighet — accessibility statement (text-only, links to uutilsynet.no)
- /informasjonskapsler — cookie policy (text-only legal page)

## Implementation priority

1. **Veiledning hub** — biggest UX improvement, flagship page
2. **Search results** — new capability the site currently lacks
3. **Case study detail** — better content structure than current
4. **Sandbox** — currently a CMS wrapper placeholder
5. **About, FAQ, 404** — incremental improvements
6. **Article/case study listings** — merge filter ideas into existing pages
7. **AI chat** — future phase, requires backend work
