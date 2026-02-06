import { factories } from '@strapi/strapi';
import { errors } from '@strapi/utils';

const { ApplicationError, ValidationError } = errors;

const EDITORIAL_TYPES = [
  'api::artikkel.artikkel',
  'api::eksempel.eksempel',
  'api::veiledning.veiledning',
  'api::side.side',
];

export default factories.createCoreController(
  'api::arbeidsflyt-logg.arbeidsflyt-logg',
  ({ strapi }) => ({
    async sendTilGodkjenning(ctx) {
      const { innholdstype, dokumentId, kommentar } = ctx.request.body as {
        innholdstype: string;
        dokumentId: string;
        kommentar?: string;
      };

      if (!innholdstype || !dokumentId) {
        throw new ValidationError('innholdstype og dokumentId er påkrevd');
      }

      if (!EDITORIAL_TYPES.includes(innholdstype)) {
        throw new ValidationError(
          `Innholdstype ${innholdstype} støtter ikke arbeidsflyt`
        );
      }

      // Verify the document exists (check with locale, default to nb)
      const locale = (ctx.request.body as any).locale || 'nb';
      const document = await strapi.documents(innholdstype as any).findOne({
        documentId: dokumentId,
        locale,
      });

      if (!document) {
        throw new ApplicationError('Dokumentet finnes ikke');
      }

      const adminUser = ctx.state.user;
      const utfortAv = adminUser?.email || adminUser?.firstname || 'ukjent';

      const entry = await strapi.documents('api::arbeidsflyt-logg.arbeidsflyt-logg').create({
        data: {
          innholdstype,
          dokumentId,
          handling: 'sendt_til_godkjenning',
          kommentar: kommentar || null,
          utfortAv,
          tidspunkt: new Date().toISOString(),
        },
      });

      // Create notifications for approvers
      await createWorkflowNotification(strapi, {
        type: 'sendt_til_godkjenning',
        innholdstype,
        dokumentId,
        utfortAv,
      });

      return { data: entry };
    },

    async godkjenn(ctx) {
      const { innholdstype, dokumentId, kommentar } = ctx.request.body as {
        innholdstype: string;
        dokumentId: string;
        kommentar?: string;
      };

      if (!innholdstype || !dokumentId) {
        throw new ValidationError('innholdstype og dokumentId er påkrevd');
      }

      // Verify latest status is "sendt_til_godkjenning"
      const latestEntry = await getLatestWorkflowEntry(strapi, dokumentId);
      if (!latestEntry || latestEntry.handling !== 'sendt_til_godkjenning') {
        throw new ApplicationError(
          'Dokumentet er ikke sendt til godkjenning'
        );
      }

      const adminUser = ctx.state.user;
      const utfortAv = adminUser?.email || adminUser?.firstname || 'ukjent';

      const entry = await strapi.documents('api::arbeidsflyt-logg.arbeidsflyt-logg').create({
        data: {
          innholdstype,
          dokumentId,
          handling: 'godkjent',
          kommentar: kommentar || null,
          utfortAv,
          tidspunkt: new Date().toISOString(),
        },
      });

      // Create notification for submitter
      await createWorkflowNotification(strapi, {
        type: 'godkjent',
        innholdstype,
        dokumentId,
        utfortAv,
        mottaker: latestEntry.utfortAv,
      });

      return { data: entry };
    },

    async avvis(ctx) {
      const { innholdstype, dokumentId, kommentar } = ctx.request.body as {
        innholdstype: string;
        dokumentId: string;
        kommentar: string;
      };

      if (!innholdstype || !dokumentId) {
        throw new ValidationError('innholdstype og dokumentId er påkrevd');
      }

      if (!kommentar) {
        throw new ValidationError('Kommentar er påkrevd ved avvisning');
      }

      // Verify latest status is "sendt_til_godkjenning"
      const latestEntry = await getLatestWorkflowEntry(strapi, dokumentId);
      if (!latestEntry || latestEntry.handling !== 'sendt_til_godkjenning') {
        throw new ApplicationError(
          'Dokumentet er ikke sendt til godkjenning'
        );
      }

      const adminUser = ctx.state.user;
      const utfortAv = adminUser?.email || adminUser?.firstname || 'ukjent';

      const entry = await strapi.documents('api::arbeidsflyt-logg.arbeidsflyt-logg').create({
        data: {
          innholdstype,
          dokumentId,
          handling: 'avvist',
          kommentar,
          utfortAv,
          tidspunkt: new Date().toISOString(),
        },
      });

      // Create notification for submitter
      await createWorkflowNotification(strapi, {
        type: 'avvist',
        innholdstype,
        dokumentId,
        utfortAv,
        kommentar,
        mottaker: latestEntry.utfortAv,
      });

      return { data: entry };
    },

    async mineOppgaver(ctx) {
      // Find all documents with latest status "sendt_til_godkjenning"
      const allLogs = await strapi.db
        .query('api::arbeidsflyt-logg.arbeidsflyt-logg')
        .findMany({
          orderBy: { tidspunkt: 'desc' },
        });

      // Group by dokumentId and find those where latest entry is "sendt_til_godkjenning"
      const latestByDoc = new Map<string, any>();
      for (const log of allLogs) {
        if (!latestByDoc.has(log.dokumentId)) {
          latestByDoc.set(log.dokumentId, log);
        }
      }

      const pendingApproval = Array.from(latestByDoc.values()).filter(
        (log) => log.handling === 'sendt_til_godkjenning'
      );

      return { data: pendingApproval };
    },

    async logg(ctx) {
      const { dokumentId } = ctx.params;

      if (!dokumentId) {
        throw new ValidationError('dokumentId er påkrevd');
      }

      const logs = await strapi.db
        .query('api::arbeidsflyt-logg.arbeidsflyt-logg')
        .findMany({
          where: { dokumentId },
          orderBy: { tidspunkt: 'desc' },
        });

      return { data: logs };
    },

    async oversikt(ctx) {
      // 1. Get all workflow log entries, ordered by most recent first
      const allLogs = await strapi.db
        .query('api::arbeidsflyt-logg.arbeidsflyt-logg')
        .findMany({
          orderBy: { tidspunkt: 'desc' },
        });

      // 2. Get latest workflow entry per document
      const latestByDoc = new Map<string, any>();
      for (const log of allLogs) {
        if (!latestByDoc.has(log.dokumentId)) {
          latestByDoc.set(log.dokumentId, log);
        }
      }

      // 3. For each, fetch the actual document title
      const enrichEntry = async (log: any) => {
        let tittel = '(ukjent)';
        try {
          const doc = await strapi.documents(log.innholdstype as any).findFirst({
            filters: { documentId: log.dokumentId } as any,
            status: 'draft',
          });
          if (doc && (doc as any).tittel) {
            tittel = (doc as any).tittel;
          }
        } catch {
          // Document may have been deleted
        }
        return {
          dokumentId: log.dokumentId,
          innholdstype: log.innholdstype,
          tittel,
          utfortAv: log.utfortAv,
          tidspunkt: log.tidspunkt,
        };
      };

      // 4. Group into categories
      const tilGodkjenning: any[] = [];
      const godkjent: any[] = [];
      const avvist: any[] = [];

      for (const log of latestByDoc.values()) {
        if (log.handling === 'sendt_til_godkjenning') {
          tilGodkjenning.push(log);
        } else if (log.handling === 'godkjent') {
          godkjent.push(log);
        } else if (log.handling === 'avvist') {
          avvist.push(log);
        }
      }

      // 5. Get upcoming scheduled publishes
      const planlagte = await strapi.db
        .query('api::planlagt-publisering.planlagt-publisering')
        .findMany({
          where: { status: 'venter' },
          orderBy: { publiserTid: 'asc' },
        });

      // 6. Enrich all entries with titles
      const [tilGodkjenningEnriched, godkjentEnriched, avvistEnriched] =
        await Promise.all([
          Promise.all(tilGodkjenning.map(enrichEntry)),
          Promise.all(godkjent.map(enrichEntry)),
          Promise.all(avvist.map(enrichEntry)),
        ]);

      const planlagtEnriched = await Promise.all(
        planlagte.map(async (p: any) => {
          let tittel = '(ukjent)';
          try {
            const doc = await strapi.documents(p.innholdstype as any).findFirst({
              filters: { documentId: p.dokumentId } as any,
              status: 'draft',
            });
            if (doc && (doc as any).tittel) {
              tittel = (doc as any).tittel;
            }
          } catch {
            // Document may have been deleted
          }
          return {
            dokumentId: p.dokumentId,
            innholdstype: p.innholdstype,
            tittel,
            publiserTid: p.publiserTid,
            opprettetAv: p.opprettetAv,
          };
        })
      );

      // 7. Get notification count for current user
      const adminUser = ctx.state.user;
      const userEmail = adminUser?.email || '';
      let uleste = 0;
      if (userEmail) {
        uleste = await strapi.db
          .query('api::varsling.varsling')
          .count({
            where: { mottaker: userEmail, lest: false },
          });
      }

      return {
        data: {
          oversikt: {
            til_godkjenning: tilGodkjenningEnriched,
            godkjent: godkjentEnriched,
            avvist: avvistEnriched,
            planlagt: planlagtEnriched,
          },
          antall: {
            til_godkjenning: tilGodkjenningEnriched.length,
            godkjent: godkjentEnriched.length,
            avvist: avvistEnriched.length,
            planlagt: planlagtEnriched.length,
          },
          varslinger: {
            uleste,
          },
        },
      };
    },
  })
);

async function getLatestWorkflowEntry(strapi: any, dokumentId: string) {
  const entries = await strapi.db
    .query('api::arbeidsflyt-logg.arbeidsflyt-logg')
    .findMany({
      where: { dokumentId },
      orderBy: { tidspunkt: 'desc' },
      limit: 1,
    });
  return entries[0] || null;
}

async function createWorkflowNotification(
  strapi: any,
  opts: {
    type: string;
    innholdstype: string;
    dokumentId: string;
    utfortAv: string;
    kommentar?: string;
    mottaker?: string;
  }
) {
  const messages: Record<string, string> = {
    sendt_til_godkjenning: `${opts.utfortAv} har sendt innhold til godkjenning (${opts.innholdstype})`,
    godkjent: `${opts.utfortAv} har godkjent innholdet ditt (${opts.innholdstype})`,
    avvist: `${opts.utfortAv} har avvist innholdet ditt: ${opts.kommentar || ''}`,
    publisert: `Innhold har blitt publisert (${opts.innholdstype})`,
  };

  const melding = messages[opts.type] || `Arbeidsflyt-hendelse: ${opts.type}`;

  // For "sendt_til_godkjenning", notify all admin users (approvers)
  // For other types, notify the specific recipient
  if (opts.type === 'sendt_til_godkjenning') {
    const adminUsers = await strapi.db
      .query('admin::user')
      .findMany({ where: { isActive: true } });

    for (const user of adminUsers) {
      if (user.email === opts.utfortAv) continue; // Don't notify yourself
      await strapi.documents('api::varsling.varsling').create({
        data: {
          mottaker: user.email,
          type: opts.type,
          melding,
          innholdstype: opts.innholdstype,
          dokumentId: opts.dokumentId,
          lest: false,
          tidspunkt: new Date().toISOString(),
        },
      });
    }
  } else if (opts.mottaker) {
    await strapi.documents('api::varsling.varsling').create({
      data: {
        mottaker: opts.mottaker,
        type: opts.type,
        melding,
        innholdstype: opts.innholdstype,
        dokumentId: opts.dokumentId,
        lest: false,
        tidspunkt: new Date().toISOString(),
      },
    });
  }
}
