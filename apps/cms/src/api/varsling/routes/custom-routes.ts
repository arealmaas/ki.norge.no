const adminAuth = {
  auth: false,
  policies: ['global::is-admin'],
};

export default {
  routes: [
    {
      method: 'GET',
      path: '/varslinger/mine',
      handler: 'varsling.mineVarslinger',
      config: adminAuth,
    },
    {
      method: 'PUT',
      path: '/varslinger/:id/lest',
      handler: 'varsling.markerSomLest',
      config: adminAuth,
    },
    {
      method: 'PUT',
      path: '/varslinger/alle-lest',
      handler: 'varsling.markerAlleLest',
      config: adminAuth,
    },
    {
      method: 'GET',
      path: '/varslinger/uleste',
      handler: 'varsling.uleste',
      config: adminAuth,
    },
  ],
};
