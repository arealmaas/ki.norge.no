import { errors } from '@strapi/utils';

// Flag to bypass workflow check during seeding
let seeding = false;

export default {
  /**
   * An asynchronous register function that runs before
   * your application is initialized.
   *
   * Adds document service middleware to enforce editorial workflow.
   */
  register({ strapi }) {
    strapi.documents.use(async (ctx, next) => {
      if (ctx.action === 'publish' && !seeding) {
        const editorialTypes = [
          'api::artikkel.artikkel',
          'api::eksempel.eksempel',
          'api::veiledning.veiledning',
          'api::side.side',
        ];

        if (editorialTypes.includes(ctx.uid)) {
          const dokumentId = ctx.params?.documentId;
          if (dokumentId) {
            // Check if latest workflow entry is "godkjent"
            const entries = await strapi.db
              .query('api::arbeidsflyt-logg.arbeidsflyt-logg')
              .findMany({
                where: { dokumentId },
                orderBy: { tidspunkt: 'desc' },
                limit: 1,
              });

            const latest = entries[0];
            if (!latest || latest.handling !== 'godkjent') {
              throw new errors.ApplicationError(
                'Innhold må godkjennes før publisering'
              );
            }
          }
        }
      }
      return next();
    });
  },

  /**
   * An asynchronous bootstrap function that runs before
   * your application gets started.
   */
  async bootstrap({ strapi }) {
    // Configure public permissions for API access
    const publicRole = await strapi.db
      .query("plugin::users-permissions.role")
      .findOne({ where: { type: "public" } });

    if (!publicRole) {
      strapi.log.warn("Public role not found, skipping permission setup");
      return;
    }

    // Content types that should have public read access
    const contentTypes = [
      "api::artikkel.artikkel",
      "api::side.side",
      "api::eksempel.eksempel",
      "api::veiledning.veiledning",
      "api::faq.faq",
      "api::merkelapp.merkelapp",
    ];

    // Actions to enable for public access
    const actions = ["find", "findOne"];

    for (const contentType of contentTypes) {
      for (const action of actions) {
        const existingPermission = await strapi.db
          .query("plugin::users-permissions.permission")
          .findOne({
            where: {
              role: publicRole.id,
              action: `${contentType}.${action}`,
            },
          });

        if (!existingPermission) {
          await strapi.db.query("plugin::users-permissions.permission").create({
            data: {
              role: publicRole.id,
              action: `${contentType}.${action}`,
            },
          });
          strapi.log.info(`Granted public ${action} permission for ${contentType}`);
        }
      }
    }

    strapi.log.info("Public API permissions configured");

    // Ensure Norwegian locale exists and is default
    await ensureLocales(strapi);

    // Seed test content (only if no content exists)
    seeding = true;
    await seedTestContent(strapi);
    seeding = false;
  },
};

async function ensureLocales(strapi) {
  const localeService = strapi.plugin("i18n").service("locales");
  const existing = await localeService.find();
  const codes = existing.map((l) => l.code);

  // Create Norwegian locale if missing
  if (!codes.includes("nb")) {
    await localeService.create({ code: "nb", name: "Norsk bokmål (nb)" });
    strapi.log.info("Created locale: nb");
  }

  // Set Norwegian as default
  const currentDefault = await localeService.getDefaultLocale();
  if (currentDefault !== "nb") {
    await localeService.setDefaultLocale({ code: "nb" });
    strapi.log.info("Set nb as default locale");
  }

  // Ensure English locale exists too
  if (!codes.includes("en")) {
    await localeService.create({ code: "en", name: "English (en)" });
    strapi.log.info("Created locale: en");
  }
}

async function seedTestContent(strapi) {
  // Check if we already have content (published or draft)
  const existingArtikler = await strapi.documents("api::artikkel.artikkel").findMany({
    status: "published",
    locale: "nb",
  });
  if (existingArtikler.length > 0) {
    strapi.log.info("Published content already exists, skipping seed");
    return;
  }

  strapi.log.info("Seeding test content...");

  const now = new Date();
  const seedUser = "seed@kinorge.no";

  // Helper: create a workflow log entry
  async function logWorkflow(dokumentId: string, innholdstype: string, handling: string, kommentar?: string, minutesAgo = 0) {
    const tidspunkt = new Date(now.getTime() - minutesAgo * 60000).toISOString();
    await strapi.documents("api::arbeidsflyt-logg.arbeidsflyt-logg").create({
      data: { innholdstype, dokumentId, handling, utfortAv: seedUser, tidspunkt, ...(kommentar ? { kommentar } : {}) },
    });
  }

  // ── Tags ──────────────────────────────────────────────────────
  const tagKI = await strapi.documents("api::merkelapp.merkelapp").create({
    data: { navn: "Kunstig intelligens", slug: "kunstig-intelligens", beskrivelse: "Innhold relatert til kunstig intelligens" },
    locale: "nb",
  });

  const tagOffentlig = await strapi.documents("api::merkelapp.merkelapp").create({
    data: { navn: "Offentlig sektor", slug: "offentlig-sektor", beskrivelse: "Innhold for offentlig sektor" },
    locale: "nb",
  });

  const tagVeiledning = await strapi.documents("api::merkelapp.merkelapp").create({
    data: { navn: "Veiledning", slug: "veiledning", beskrivelse: "Veiledninger og retningslinjer" },
    locale: "nb",
  });

  // ── Helper: simple paragraph block ────────────────────────────
  const p = (text: string) => ({ type: "paragraph", children: [{ type: "text", text }] });
  const h2 = (text: string) => ({ type: "heading", level: 2, children: [{ type: "text", text }] });
  const CT = "api::artikkel.artikkel";

  // ══════════════════════════════════════════════════════════════
  //  ARTICLES — one per workflow stage
  // ══════════════════════════════════════════════════════════════

  // 1. PUBLISHED (full workflow: submit → approve → publish)
  const art1 = await strapi.documents(CT).create({
    data: {
      tittel: "KI-strategien for offentlig sektor lansert",
      slug: "ki-strategien-lansert",
      innhold: [
        p("Regjeringen har lansert en ny nasjonal strategi for kunstig intelligens i offentlig sektor. Strategien skal sikre ansvarlig og effektiv bruk av KI-teknologi i offentlige tjenester."),
        h2("Hovedmål"),
        p("Strategien har tre hovedmål: styrke digital kompetanse, fremme innovasjon, og sikre etisk bruk av KI."),
        { type: "list", format: "unordered", children: [
          { type: "list-item", children: [{ type: "text", text: "Øke kompetansen på KI i offentlig sektor" }] },
          { type: "list-item", children: [{ type: "text", text: "Etablere felles rammeverk for ansvarlig KI" }] },
          { type: "list-item", children: [{ type: "text", text: "Støtte innovasjon og eksperimentering" }] },
        ]},
      ],
    },
    locale: "nb",
    status: "published",
  });
  await logWorkflow(art1.documentId, CT, "sendt_til_godkjenning", undefined, 60);
  await logWorkflow(art1.documentId, CT, "godkjent", undefined, 30);
  await logWorkflow(art1.documentId, CT, "publisert", undefined, 25);

  // 2. PUBLISHED (full workflow)
  const art2 = await strapi.documents(CT).create({
    data: {
      tittel: "Nye retningslinjer for KI i saksbehandling",
      slug: "retningslinjer-ki-saksbehandling",
      innhold: [
        p("Digitaliseringsdirektoratet har utarbeidet nye retningslinjer for bruk av kunstig intelligens i saksbehandling. Retningslinjene gir veiledning om hvordan KI kan brukes på en måte som ivaretar borgernes rettssikkerhet."),
        p("Retningslinjene dekker blant annet krav til transparens, kvalitetssikring av data, og menneskelig tilsyn ved automatiserte beslutninger."),
      ],
    },
    locale: "nb",
    status: "published",
  });
  await logWorkflow(art2.documentId, CT, "sendt_til_godkjenning", undefined, 120);
  await logWorkflow(art2.documentId, CT, "godkjent", undefined, 90);
  await logWorkflow(art2.documentId, CT, "publisert", undefined, 85);

  // 3. PUBLISHED (full workflow)
  const art3 = await strapi.documents(CT).create({
    data: {
      tittel: "Slik kommer du i gang med KI i din virksomhet",
      slug: "kom-i-gang-med-ki",
      innhold: [
        p("Mange offentlige virksomheter ønsker å ta i bruk kunstig intelligens, men vet ikke hvor de skal begynne. Her er noen råd for å komme i gang."),
        h2("Start med et konkret problem"),
        p("Identifiser et konkret problem eller en oppgave som kan løses eller forbedres med KI. Unngå å lete etter problemer som passer til en løsning."),
      ],
    },
    locale: "nb",
    status: "published",
  });
  await logWorkflow(art3.documentId, CT, "sendt_til_godkjenning", undefined, 180);
  await logWorkflow(art3.documentId, CT, "godkjent", undefined, 150);
  await logWorkflow(art3.documentId, CT, "publisert", undefined, 145);

  // 4. DRAFT — no workflow action taken yet (fresh draft)
  await strapi.documents(CT).create({
    data: {
      tittel: "Chatbots i offentlige tjenester",
      slug: "chatbots-offentlige-tjenester",
      innhold: [
        p("Flere offentlige virksomheter eksperimenterer med chatbots for å forbedre innbyggertjenester. Denne artikkelen ser på erfaringer og beste praksis."),
        h2("Hva er en chatbot?"),
        p("En chatbot er et dataprogram som simulerer menneskelig samtale, vanligvis gjennom tekst. Moderne chatbots bruker KI og naturlig språkprosessering for å forstå og svare på spørsmål."),
      ],
    },
    locale: "nb",
  });
  // No workflow log — this is a pure draft

  // 5. SUBMITTED for approval (sendt_til_godkjenning)
  const art5 = await strapi.documents(CT).create({
    data: {
      tittel: "KI og personvern: En praktisk guide",
      slug: "ki-og-personvern",
      innhold: [
        p("Bruk av kunstig intelligens reiser viktige spørsmål om personvern og datasikkerhet. Denne guiden gir praktiske råd for å ivareta personvernet ved bruk av KI."),
        h2("GDPR og KI"),
        p("Personvernforordningen stiller krav til automatisert beslutningstaking og profilering som er direkte relevante for KI-systemer."),
      ],
    },
    locale: "nb",
  });
  await logWorkflow(art5.documentId, CT, "sendt_til_godkjenning", undefined, 10);

  // 6. APPROVED but not yet published (godkjent)
  const art6 = await strapi.documents(CT).create({
    data: {
      tittel: "Automatisering av dokumentbehandling med KI",
      slug: "automatisering-dokumentbehandling",
      innhold: [
        p("Dokumentbehandling er en av de mest lovende bruksområdene for KI i offentlig sektor. Automatisert klassifisering, uttrekk og routing kan spare tusenvis av arbeidstimer."),
        h2("Teknologier"),
        p("OCR, NLP og maskinlæring er nøkkelteknologiene bak automatisert dokumentbehandling."),
      ],
    },
    locale: "nb",
  });
  await logWorkflow(art6.documentId, CT, "sendt_til_godkjenning", undefined, 45);
  await logWorkflow(art6.documentId, CT, "godkjent", "Ser bra ut, klar for publisering.", 15);

  // 7. REJECTED (avvist) — needs revision
  const art7 = await strapi.documents(CT).create({
    data: {
      tittel: "Erfaringer fra KI-piloter i kommunene",
      slug: "ki-piloter-kommunene",
      innhold: [
        p("Kommunene har vært i front med å teste KI-løsninger. Her deler vi erfaringer fra flere pilotprosjekter."),
        h2("Eksempler"),
        p("Bergen, Trondheim og Stavanger har alle kjørt KI-piloter innen helse, utdanning og administrasjon."),
      ],
    },
    locale: "nb",
  });
  await logWorkflow(art7.documentId, CT, "sendt_til_godkjenning", undefined, 50);
  await logWorkflow(art7.documentId, CT, "avvist", "Mangler kilder og konkrete tall fra pilotene. Vennligst oppdater med resultater.", 20);

  // 8. SCHEDULED for future publish (approved + planlagt-publisering)
  const art8 = await strapi.documents(CT).create({
    data: {
      tittel: "EU AI Act: Hva betyr det for norsk offentlig sektor?",
      slug: "eu-ai-act-norge",
      innhold: [
        p("EUs forordning om kunstig intelligens (AI Act) trer snart i kraft og vil påvirke hvordan norske offentlige virksomheter utvikler og bruker KI-systemer."),
        h2("Risikobasert tilnærming"),
        p("AI Act klassifiserer KI-systemer etter risikonivå, med strengest krav til høyrisiko-systemer som brukes i offentlig forvaltning."),
      ],
    },
    locale: "nb",
  });
  await logWorkflow(art8.documentId, CT, "sendt_til_godkjenning", undefined, 40);
  await logWorkflow(art8.documentId, CT, "godkjent", "Publiseres planlagt neste uke.", 10);
  // Schedule for 7 days from now
  const publishDate = new Date(now.getTime() + 7 * 24 * 60 * 60000);
  await strapi.documents("api::planlagt-publisering.planlagt-publisering").create({
    data: {
      innholdstype: CT,
      dokumentId: art8.documentId,
      publiserTid: publishDate.toISOString(),
      status: "venter",
      opprettetAv: seedUser,
    },
  });

  // ── Notifications for the workflow actions ─────────────────────
  await strapi.documents("api::varsling.varsling").create({
    data: {
      mottaker: seedUser,
      type: "sendt_til_godkjenning",
      melding: "«KI og personvern: En praktisk guide» er sendt til godkjenning.",
      innholdstype: CT,
      dokumentId: art5.documentId,
      lest: false,
      tidspunkt: new Date(now.getTime() - 10 * 60000).toISOString(),
    },
  });
  await strapi.documents("api::varsling.varsling").create({
    data: {
      mottaker: seedUser,
      type: "godkjent",
      melding: "«Automatisering av dokumentbehandling med KI» er godkjent.",
      innholdstype: CT,
      dokumentId: art6.documentId,
      lest: false,
      tidspunkt: new Date(now.getTime() - 15 * 60000).toISOString(),
    },
  });
  await strapi.documents("api::varsling.varsling").create({
    data: {
      mottaker: seedUser,
      type: "avvist",
      melding: "«Erfaringer fra KI-piloter i kommunene» ble avvist: Mangler kilder og konkrete tall fra pilotene.",
      innholdstype: CT,
      dokumentId: art7.documentId,
      lest: true,
      tidspunkt: new Date(now.getTime() - 20 * 60000).toISOString(),
    },
  });
  await strapi.documents("api::varsling.varsling").create({
    data: {
      mottaker: seedUser,
      type: "planlagt",
      melding: "«EU AI Act: Hva betyr det for norsk offentlig sektor?» er planlagt publisert om 7 dager.",
      innholdstype: CT,
      dokumentId: art8.documentId,
      lest: false,
      tidspunkt: new Date(now.getTime() - 10 * 60000).toISOString(),
    },
  });

  // ── FAQs ──────────────────────────────────────────────────────
  await strapi.documents("api::faq.faq").create({
    data: {
      sporsmal: "Hva er kunstig intelligens (KI)?",
      svar: [p("Kunstig intelligens (KI) er datamaskinsystemer som kan utføre oppgaver som normalt krever menneskelig intelligens. Dette inkluderer oppgaver som å gjenkjenne mønstre, forstå språk, ta beslutninger og lære av erfaring.")],
      rekkefølge: 1,
      kategori: { connect: [{ documentId: tagKI.documentId }] },
    },
    locale: "nb",
    status: "published",
  });

  await strapi.documents("api::faq.faq").create({
    data: {
      sporsmal: "Hvordan sikrer vi ansvarlig bruk av KI?",
      svar: [p("Ansvarlig bruk av KI innebærer å følge etiske prinsipper som transparens, rettferdighet, og personvern. Det er viktig å ha menneskelig tilsyn, dokumentere beslutninger, og regelmessig evaluere systemenes ytelse og konsekvenser.")],
      rekkefølge: 2,
      kategori: { connect: [{ documentId: tagKI.documentId }] },
    },
    locale: "nb",
    status: "published",
  });

  await strapi.documents("api::faq.faq").create({
    data: {
      sporsmal: "Hvilke lovkrav gjelder for bruk av KI i offentlig sektor?",
      svar: [p("Offentlige virksomheter må overholde personvernforordningen (GDPR), forvaltningsloven, og arkivloven ved bruk av KI. EU's AI Act vil også stille krav til KI-systemer basert på risikonivå.")],
      rekkefølge: 3,
      kategori: { connect: [{ documentId: tagOffentlig.documentId }] },
    },
    locale: "nb",
    status: "published",
  });

  // ── Examples ──────────────────────────────────────────────────
  await strapi.documents("api::eksempel.eksempel").create({
    data: {
      tittel: "NAV - Chatbot for brukerveiledning",
      slug: "nav-chatbot",
      organisasjon: "NAV",
      beskrivelse: [
        p("NAV har utviklet en chatbot som hjelper brukere med å finne informasjon og navigere på nav.no. Chatboten bruker naturlig språkprosessering for å forstå brukerens spørsmål og gi relevante svar."),
        h2("Resultater"),
        p("Chatboten har redusert antall henvendelser til kundesenteret med 15% og forbedret brukertilfredshet betydelig."),
      ],
      status: "i_drift",
      resultater: "Redusert henvendelser med 15%, forbedret brukertilfredshet",
      merkelapper: { connect: [{ documentId: tagKI.documentId }, { documentId: tagOffentlig.documentId }] },
    },
    locale: "nb",
    status: "published",
  });

  await strapi.documents("api::eksempel.eksempel").create({
    data: {
      tittel: "Skatteetaten - Automatisk dokumentklassifisering",
      slug: "skatteetaten-dokumentklassifisering",
      organisasjon: "Skatteetaten",
      beskrivelse: [p("Skatteetaten bruker maskinlæring til å automatisk klassifisere innkommende dokumenter. Systemet analyserer dokumentenes innhold og ruter dem til riktig saksbehandler.")],
      status: "pilot",
      resultater: "Pilotfase - foreløpige resultater viser 40% reduksjon i manuelt sorteringsarbeid",
      merkelapper: { connect: [{ documentId: tagKI.documentId }] },
    },
    locale: "nb",
    status: "published",
  });

  // ── Veiledning ────────────────────────────────────────────────
  await strapi.documents("api::veiledning.veiledning").create({
    data: {
      tittel: "Kom i gang med KI",
      slug: "kom-i-gang",
      innhold: [
        p("Denne veiledningen hjelper deg med å komme i gang med kunstig intelligens i din virksomhet. Vi dekker alt fra grunnleggende forståelse til praktisk implementering."),
        h2("Forstå behovene"),
        p("Start med å kartlegge hvilke oppgaver i virksomheten som kan ha nytte av KI. Se etter repetitive oppgaver, beslutninger basert på store datamengder, eller prosesser som krever mønstergjenkjenning."),
      ],
      rekkefølge: 1,
      kategori: { connect: [{ documentId: tagVeiledning.documentId }] },
    },
    locale: "nb",
    status: "published",
  });

  // ── Side (page) ───────────────────────────────────────────────
  await strapi.documents("api::side.side").create({
    data: {
      tittel: "Om KI Norge",
      slug: "om-oss",
      template: "standard",
      innhold: [
        p("KI Norge er en nasjonal satsing for å fremme ansvarlig bruk av kunstig intelligens i offentlig sektor."),
        h2("Vår visjon"),
        p("Vi ønsker at Norge skal være ledende i Europa på ansvarlig og innovativ bruk av KI i offentlige tjenester."),
      ],
      seoTittel: "Om KI Norge - Kunstig intelligens i offentlig sektor",
      seoBeskrivelse: "Les om KI Norge og vår satsing på ansvarlig bruk av kunstig intelligens i offentlig sektor.",
    },
    locale: "nb",
    status: "published",
  });

  strapi.log.info("Test content seeded successfully");
  strapi.log.info("Workflow seed summary:");
  strapi.log.info("  - 3 articles: PUBLISHED (full workflow history)");
  strapi.log.info("  - 1 article:  DRAFT (no workflow action)");
  strapi.log.info("  - 1 article:  SUBMITTED for approval");
  strapi.log.info("  - 1 article:  APPROVED (ready to publish)");
  strapi.log.info("  - 1 article:  REJECTED (needs revision)");
  strapi.log.info("  - 1 article:  SCHEDULED (approved, publish in 7 days)");
  strapi.log.info("  - 4 notifications, 1 scheduled publish entry");
}
