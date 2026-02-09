'use strict';

const adminAuth = {
  auth: false,
  policies: ['plugin::redaksjonelt.is-admin'],
};

module.exports = [
  {
    method: 'POST',
    path: '/send-til-godkjenning',
    handler: 'arbeidsflyt-logg.sendTilGodkjenning',
    config: adminAuth,
  },
  {
    method: 'POST',
    path: '/godkjenn',
    handler: 'arbeidsflyt-logg.godkjenn',
    config: adminAuth,
  },
  {
    method: 'POST',
    path: '/avvis',
    handler: 'arbeidsflyt-logg.avvis',
    config: adminAuth,
  },
  {
    method: 'GET',
    path: '/mine-oppgaver',
    handler: 'arbeidsflyt-logg.mineOppgaver',
    config: adminAuth,
  },
  {
    method: 'GET',
    path: '/logg/:dokumentId',
    handler: 'arbeidsflyt-logg.logg',
    config: adminAuth,
  },
  {
    method: 'GET',
    path: '/oversikt',
    handler: 'arbeidsflyt-logg.oversikt',
    config: adminAuth,
  },
];
