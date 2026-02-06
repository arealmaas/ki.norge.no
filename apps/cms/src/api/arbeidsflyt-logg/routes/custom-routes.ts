const adminAuth = {
  auth: false,
  policies: ['global::is-admin'],
};

export default {
  routes: [
    {
      method: 'POST',
      path: '/arbeidsflyt/send-til-godkjenning',
      handler: 'arbeidsflyt-logg.sendTilGodkjenning',
      config: adminAuth,
    },
    {
      method: 'POST',
      path: '/arbeidsflyt/godkjenn',
      handler: 'arbeidsflyt-logg.godkjenn',
      config: adminAuth,
    },
    {
      method: 'POST',
      path: '/arbeidsflyt/avvis',
      handler: 'arbeidsflyt-logg.avvis',
      config: adminAuth,
    },
    {
      method: 'GET',
      path: '/arbeidsflyt/mine-oppgaver',
      handler: 'arbeidsflyt-logg.mineOppgaver',
      config: adminAuth,
    },
    {
      method: 'GET',
      path: '/arbeidsflyt/logg/:dokumentId',
      handler: 'arbeidsflyt-logg.logg',
      config: adminAuth,
    },
    {
      method: 'GET',
      path: '/arbeidsflyt/oversikt',
      handler: 'arbeidsflyt-logg.oversikt',
      config: adminAuth,
    },
  ],
};
