'use strict';

const adminAuth = {
  auth: false,
  policies: ['plugin::redaksjonelt.is-admin'],
};

module.exports = [
  {
    method: 'POST',
    path: '/planlegg',
    handler: 'planlagt-publisering.planlegg',
    config: adminAuth,
  },
  {
    method: 'PUT',
    path: '/planlagt/:id/kanseller',
    handler: 'planlagt-publisering.kanseller',
    config: adminAuth,
  },
  {
    method: 'GET',
    path: '/kommende',
    handler: 'planlagt-publisering.kommende',
    config: adminAuth,
  },
];
