'use strict';

const { errors } = require('@strapi/utils');

const { ValidationError } = errors;

const VARSLING_UID = 'plugin::redaksjonelt.varsling';

module.exports = ({ strapi }) => ({
  async mineVarslinger(ctx) {
    const adminUser = ctx.state.user;
    if (!adminUser?.email) {
      throw new ValidationError('Bruker ikke identifisert');
    }

    const varslinger = await strapi.db.query(VARSLING_UID).findMany({
      where: { mottaker: adminUser.email },
      orderBy: { tidspunkt: 'desc' },
    });

    return { data: varslinger };
  },

  async markerSomLest(ctx) {
    const { id } = ctx.params;

    const updated = await strapi.db.query(VARSLING_UID).update({
      where: { documentId: id },
      data: { lest: true },
    });

    return { data: updated };
  },

  async markerAlleLest(ctx) {
    const adminUser = ctx.state.user;
    if (!adminUser?.email) {
      throw new ValidationError('Bruker ikke identifisert');
    }

    await strapi.db.query(VARSLING_UID).updateMany({
      where: { mottaker: adminUser.email, lest: false },
      data: { lest: true },
    });

    return { data: { ok: true } };
  },

  async uleste(ctx) {
    const adminUser = ctx.state.user;
    if (!adminUser?.email) {
      throw new ValidationError('Bruker ikke identifisert');
    }

    const count = await strapi.db.query(VARSLING_UID).count({
      where: { mottaker: adminUser.email, lest: false },
    });

    return { data: { count } };
  },
});
