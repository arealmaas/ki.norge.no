export default ({ env }) => ({
  host: env('HOST', '0.0.0.0'),
  port: env.int('PORT', 1337),
  app: {
    keys: env.array('APP_KEYS'),
  },
  cron: {
    enabled: true,
    tasks: {
      // Every 5 minutes: auto-publish scheduled content
      '*/5 * * * *': async ({ strapi }) => {
        const now = new Date().toISOString();

        const dueEntries = await strapi.db
          .query('api::planlagt-publisering.planlagt-publisering')
          .findMany({
            where: {
              status: 'venter',
              publiserTid: { $lte: now },
            },
          });

        for (const entry of dueEntries) {
          try {
            // Publish the document
            await strapi
              .documents(entry.innholdstype as any)
              .publish({
                documentId: entry.dokumentId,
              });

            // Update scheduled entry status
            await strapi.db
              .query('api::planlagt-publisering.planlagt-publisering')
              .update({
                where: { id: entry.id },
                data: { status: 'publisert' },
              });

            // Create workflow log entry
            await strapi
              .documents('api::arbeidsflyt-logg.arbeidsflyt-logg')
              .create({
                data: {
                  innholdstype: entry.innholdstype,
                  dokumentId: entry.dokumentId,
                  handling: 'publisert',
                  kommentar: 'Automatisk publisert via planlagt publisering',
                  utfortAv: 'system',
                  tidspunkt: new Date().toISOString(),
                },
              });

            // Notify the scheduler
            await strapi.documents('api::varsling.varsling').create({
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
    },
  },
});
