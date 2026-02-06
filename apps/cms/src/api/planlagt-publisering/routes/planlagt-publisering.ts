const adminAuth = {
  auth: false,
  policies: ['global::is-admin'],
};

export default {
  routes: [
    {
      method: 'POST',
      path: '/planlagt-publisering/planlegg',
      handler: 'planlagt-publisering.planlegg',
      config: adminAuth,
    },
    {
      method: 'PUT',
      path: '/planlagt-publisering/:id/kanseller',
      handler: 'planlagt-publisering.kanseller',
      config: adminAuth,
    },
    {
      method: 'GET',
      path: '/planlagt-publisering/kommende',
      handler: 'planlagt-publisering.kommende',
      config: adminAuth,
    },
  ],
};
