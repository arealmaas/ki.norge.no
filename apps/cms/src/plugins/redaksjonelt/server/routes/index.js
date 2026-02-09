'use strict';

module.exports = {
  'content-api': {
    type: 'content-api',
    routes: [
      ...require('./arbeidsflyt-logg'),
      ...require('./planlagt-publisering'),
      ...require('./varsling'),
    ],
  },
};
