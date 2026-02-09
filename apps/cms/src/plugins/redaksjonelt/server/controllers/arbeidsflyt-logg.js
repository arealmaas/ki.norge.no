'use strict';

const { errors } = require('@strapi/utils');

const { ApplicationError, ValidationError } = errors;

const LOGG_UID = 'plugin::redaksjonelt.arbeidsflyt-logg';
const PLANLAGT_UID = 'plugin::redaksjonelt.planlagt-publisering';
const VARSLING_UID = 'plugin::redaksjonelt.varsling';

function getContentTypes(strapi) {
  return strapi.plugin('redaksjonelt').config('contentTypes') || [];
}

async function getLatestWorkflowEntry(strapi, dokumentId) {
  const entries = await strapi.db.query(LOGG_UID).findMany({
    where: { dokumentId },
    orderBy: { tidspunkt: 'desc' },
    limit: 1,
  });
  return entries[0] || null;
}

async function createWorkflowNotification(strapi, opts) {
  const messages = {
    sendt_til_godkjenning: `${opts.utfortAv} har sendt innhold til godkjenning (${opts.innholdstype})`,
    godkjent: `${opts.utfortAv} har godkjent innholdet ditt (${opts.innholdstype})`,
    avvist: `${opts.utfortAv} har avvist innholdet ditt: ${opts.kommentar || ''}`,
    publisert: `Innhold har blitt publisert (${opts.innholdstype})`,
  };

  const melding = messages[opts.type] || `Arbeidsflyt-hendelse: ${opts.type}`;

  if (opts.type === 'sendt_til_godkjenning') {
    const adminUsers = await strapi.db
      .query('admin::user')
      .findMany({ where: { isActive: true } });

    for (const user of adminUsers) {
      if (user.email === opts.utfortAv) continue;
      await strapi.documents(VARSLING_UID).create({
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
    await strapi.documents(VARSLING_UID).create({
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

module.exports = ({ strapi }) => ({
  async sendTilGodkjenning(ctx) {
    const { innholdstype, dokumentId, kommentar } = ctx.request.body;

    if (!innholdstype || !dokumentId) {
      throw new ValidationError('innholdstype og dokumentId er påkrevd');
    }

    const contentTypes = getContentTypes(strapi);
    if (!contentTypes.includes(innholdstype)) {
      throw new ValidationError(
        `Innholdstype ${innholdstype} støtter ikke arbeidsflyt`
      );
    }

    const locale = ctx.request.body.locale || 'nb';
    const document = await strapi.documents(innholdstype).findOne({
      documentId: dokumentId,
      locale,
    });

    if (!document) {
      throw new ApplicationError('Dokumentet finnes ikke');
    }

    const adminUser = ctx.state.user;
    const utfortAv = adminUser?.email || adminUser?.firstname || 'ukjent';

    const entry = await strapi.documents(LOGG_UID).create({
      data: {
        innholdstype,
        dokumentId,
        handling: 'sendt_til_godkjenning',
        kommentar: kommentar || null,
        utfortAv,
        tidspunkt: new Date().toISOString(),
      },
    });

    await createWorkflowNotification(strapi, {
      type: 'sendt_til_godkjenning',
      innholdstype,
      dokumentId,
      utfortAv,
    });

    return { data: entry };
  },

  async godkjenn(ctx) {
    const { innholdstype, dokumentId, kommentar } = ctx.request.body;

    if (!innholdstype || !dokumentId) {
      throw new ValidationError('innholdstype og dokumentId er påkrevd');
    }

    const latestEntry = await getLatestWorkflowEntry(strapi, dokumentId);
    if (!latestEntry || latestEntry.handling !== 'sendt_til_godkjenning') {
      throw new ApplicationError('Dokumentet er ikke sendt til godkjenning');
    }

    const adminUser = ctx.state.user;
    const utfortAv = adminUser?.email || adminUser?.firstname || 'ukjent';

    const entry = await strapi.documents(LOGG_UID).create({
      data: {
        innholdstype,
        dokumentId,
        handling: 'godkjent',
        kommentar: kommentar || null,
        utfortAv,
        tidspunkt: new Date().toISOString(),
      },
    });

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
    const { innholdstype, dokumentId, kommentar } = ctx.request.body;

    if (!innholdstype || !dokumentId) {
      throw new ValidationError('innholdstype og dokumentId er påkrevd');
    }

    if (!kommentar) {
      throw new ValidationError('Kommentar er påkrevd ved avvisning');
    }

    const latestEntry = await getLatestWorkflowEntry(strapi, dokumentId);
    if (!latestEntry || latestEntry.handling !== 'sendt_til_godkjenning') {
      throw new ApplicationError('Dokumentet er ikke sendt til godkjenning');
    }

    const adminUser = ctx.state.user;
    const utfortAv = adminUser?.email || adminUser?.firstname || 'ukjent';

    const entry = await strapi.documents(LOGG_UID).create({
      data: {
        innholdstype,
        dokumentId,
        handling: 'avvist',
        kommentar,
        utfortAv,
        tidspunkt: new Date().toISOString(),
      },
    });

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
    const allLogs = await strapi.db.query(LOGG_UID).findMany({
      orderBy: { tidspunkt: 'desc' },
    });

    const latestByDoc = new Map();
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

    const logs = await strapi.db.query(LOGG_UID).findMany({
      where: { dokumentId },
      orderBy: { tidspunkt: 'desc' },
    });

    return { data: logs };
  },

  async oversikt(ctx) {
    const allLogs = await strapi.db.query(LOGG_UID).findMany({
      orderBy: { tidspunkt: 'desc' },
    });

    const latestByDoc = new Map();
    for (const log of allLogs) {
      if (!latestByDoc.has(log.dokumentId)) {
        latestByDoc.set(log.dokumentId, log);
      }
    }

    const enrichEntry = async (log) => {
      let tittel = '(ukjent)';
      try {
        const doc = await strapi.documents(log.innholdstype).findFirst({
          filters: { documentId: log.dokumentId },
          status: 'draft',
        });
        if (doc && doc.tittel) {
          tittel = doc.tittel;
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

    const tilGodkjenning = [];
    const godkjent = [];
    const avvist = [];

    for (const log of latestByDoc.values()) {
      if (log.handling === 'sendt_til_godkjenning') {
        tilGodkjenning.push(log);
      } else if (log.handling === 'godkjent') {
        godkjent.push(log);
      } else if (log.handling === 'avvist') {
        avvist.push(log);
      }
    }

    const planlagte = await strapi.db.query(PLANLAGT_UID).findMany({
      where: { status: 'venter' },
      orderBy: { publiserTid: 'asc' },
    });

    const [tilGodkjenningEnriched, godkjentEnriched, avvistEnriched] =
      await Promise.all([
        Promise.all(tilGodkjenning.map(enrichEntry)),
        Promise.all(godkjent.map(enrichEntry)),
        Promise.all(avvist.map(enrichEntry)),
      ]);

    const planlagtEnriched = await Promise.all(
      planlagte.map(async (p) => {
        let tittel = '(ukjent)';
        try {
          const doc = await strapi.documents(p.innholdstype).findFirst({
            filters: { documentId: p.dokumentId },
            status: 'draft',
          });
          if (doc && doc.tittel) {
            tittel = doc.tittel;
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

    const adminUser = ctx.state.user;
    const userEmail = adminUser?.email || '';
    let uleste = 0;
    if (userEmail) {
      uleste = await strapi.db.query(VARSLING_UID).count({
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
});
