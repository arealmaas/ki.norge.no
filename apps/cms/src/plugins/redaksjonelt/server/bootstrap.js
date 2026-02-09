'use strict';

const LOGG_UID = 'plugin::redaksjonelt.arbeidsflyt-logg';
const PLANLAGT_UID = 'plugin::redaksjonelt.planlagt-publisering';
const VARSLING_UID = 'plugin::redaksjonelt.varsling';

module.exports = async ({ strapi }) => {
  const config = strapi.plugin('redaksjonelt').config;
  const scheduling = config('scheduling') || {};

  if (scheduling.enabled !== false) {
    strapi.cron.add({
      'redaksjonelt-publish-scheduled': {
        task: async ({ strapi }) => {
          const now = new Date().toISOString();

          const dueEntries = await strapi.db.query(PLANLAGT_UID).findMany({
            where: {
              status: 'venter',
              publiserTid: { $lte: now },
            },
          });

          for (const entry of dueEntries) {
            try {
              await strapi.documents(entry.innholdstype).publish({
                documentId: entry.dokumentId,
              });

              await strapi.db.query(PLANLAGT_UID).update({
                where: { id: entry.id },
                data: { status: 'publisert' },
              });

              await strapi.documents(LOGG_UID).create({
                data: {
                  innholdstype: entry.innholdstype,
                  dokumentId: entry.dokumentId,
                  handling: 'publisert',
                  kommentar: 'Automatisk publisert via planlagt publisering',
                  utfortAv: 'system',
                  tidspunkt: new Date().toISOString(),
                },
              });

              await strapi.documents(VARSLING_UID).create({
                data: {
                  mottaker: entry.opprettetAv,
                  type: 'publisert',
                  melding: `Planlagt innhold er nå publisert (${entry.innholdstype})`,
                  innholdstype: entry.innholdstype,
                  dokumentId: entry.dokumentId,
                  lest: false,
                  tidspunkt: new Date().toISOString(),
                },
              });

              strapi.log.info(
                `Auto-published ${entry.innholdstype} ${entry.dokumentId}`
              );
            } catch (err) {
              strapi.log.error(
                `Failed to auto-publish ${entry.innholdstype} ${entry.dokumentId}:`,
                err
              );
            }
          }
        },
        options: scheduling.cron || '*/5 * * * *',
      },
    });
  }
};
