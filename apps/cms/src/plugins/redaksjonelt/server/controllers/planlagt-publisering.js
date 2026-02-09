'use strict';

const { errors } = require('@strapi/utils');

const { ApplicationError, ValidationError } = errors;

const LOGG_UID = 'plugin::redaksjonelt.arbeidsflyt-logg';
const PLANLAGT_UID = 'plugin::redaksjonelt.planlagt-publisering';
const VARSLING_UID = 'plugin::redaksjonelt.varsling';

module.exports = ({ strapi }) => ({
  async planlegg(ctx) {
    const { innholdstype, dokumentId, publiserTid } = ctx.request.body;

    if (!innholdstype || !dokumentId || !publiserTid) {
      throw new ValidationError(
        'innholdstype, dokumentId og publiserTid er påkrevd'
      );
    }

    const scheduledDate = new Date(publiserTid);
    if (scheduledDate <= new Date()) {
      throw new ValidationError('publiserTid må være i fremtiden');
    }

    const workflowEntries = await strapi.db.query(LOGG_UID).findMany({
      where: { dokumentId },
      orderBy: { tidspunkt: 'desc' },
      limit: 1,
    });

    const latest = workflowEntries[0];
    if (!latest || latest.handling !== 'godkjent') {
      throw new ApplicationError(
        'Dokumentet må være godkjent før planlagt publisering'
      );
    }

    const adminUser = ctx.state.user;
    const opprettetAv = adminUser?.email || adminUser?.firstname || 'ukjent';

    const entry = await strapi.documents(PLANLAGT_UID).create({
      data: {
        innholdstype,
        dokumentId,
        publiserTid: scheduledDate.toISOString(),
        status: 'venter',
        opprettetAv,
      },
    });

    await strapi.documents(VARSLING_UID).create({
      data: {
        mottaker: opprettetAv,
        type: 'planlagt',
        melding: `Publisering planlagt for ${scheduledDate.toLocaleString('nb-NO')}`,
        innholdstype,
        dokumentId,
        lest: false,
        tidspunkt: new Date().toISOString(),
      },
    });

    return { data: entry };
  },

  async kanseller(ctx) {
    const { id } = ctx.params;

    const existing = await strapi.db.query(PLANLAGT_UID).findOne({
      where: { documentId: id },
    });

    if (!existing) {
      throw new ApplicationError('Planlagt publisering ikke funnet');
    }

    if (existing.status !== 'venter') {
      throw new ApplicationError(
        'Kan kun kansellere publiseringer med status "venter"'
      );
    }

    const updated = await strapi.db.query(PLANLAGT_UID).update({
      where: { documentId: id },
      data: { status: 'kansellert' },
    });

    return { data: updated };
  },

  async kommende(ctx) {
    const entries = await strapi.db.query(PLANLAGT_UID).findMany({
      where: { status: 'venter' },
      orderBy: { publiserTid: 'asc' },
    });

    return { data: entries };
  },
});
