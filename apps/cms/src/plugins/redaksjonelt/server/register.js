'use strict';

const { errors } = require('@strapi/utils');

module.exports = ({ strapi }) => {
  strapi.documents.use(async (ctx, next) => {
    if (ctx.action !== 'publish') return next();

    const contentTypes =
      strapi.plugin('redaksjonelt').config('contentTypes') || [];
    if (!contentTypes.includes(ctx.uid)) return next();

    const workflow = strapi.plugin('redaksjonelt').service('workflow');
    if (workflow.isSeedMode()) return next();

    const dokumentId = ctx.params?.documentId;
    if (!dokumentId) return next();

    try {
      const entries = await strapi.db
        .query('plugin::redaksjonelt.arbeidsflyt-logg')
        .findMany({
          where: { dokumentId },
          orderBy: { tidspunkt: 'desc' },
          limit: 1,
        });

      const latest = entries[0];
      if (latest && latest.handling !== 'godkjent') {
        throw new errors.ApplicationError(
          'Innhold må godkjennes før publisering'
        );
      }
      // No entries = not in workflow = allow publish
    } catch (err) {
      if (err instanceof errors.ApplicationError) throw err;
      strapi.log.warn(
        'Redaksjonelt: workflow check failed, allowing publish',
        err.message
      );
    }

    return next();
  });
};
