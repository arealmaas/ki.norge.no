/**
 * Verify admin JWT token from Authorization header using Strapi's session manager.
 * Sets ctx.state.user with the admin user if valid.
 */
export default async (policyContext, config, { strapi }) => {
  const { authorization } = policyContext.request.header;
  if (!authorization) {
    return false;
  }

  const parts = authorization.split(/\s+/);
  if (parts[0].toLowerCase() !== 'bearer' || parts.length !== 2) {
    return false;
  }

  const token = parts[1];
  const manager = strapi.sessionManager;
  if (!manager) {
    return false;
  }

  try {
    const result = manager('admin').validateAccessToken(token);
    if (!result.isValid) {
      return false;
    }

    const isActive = await manager('admin').isSessionActive(result.payload.sessionId);
    if (!isActive) {
      return false;
    }

    const rawUserId = result.payload.userId;
    const numericUserId = Number(rawUserId);
    const userId =
      Number.isFinite(numericUserId) && String(numericUserId) === rawUserId
        ? numericUserId
        : rawUserId;

    const user = await strapi.db.query('admin::user').findOne({
      where: { id: userId },
      populate: ['roles'],
    });

    if (!user || !user.isActive) {
      return false;
    }

    policyContext.state.user = user;
    return true;
  } catch {
    return false;
  }
};
