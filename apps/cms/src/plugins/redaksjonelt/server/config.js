'use strict';

module.exports = {
  default: {
    contentTypes: [],
    scheduling: { enabled: true, cron: '*/5 * * * *' },
    notifications: { enabled: true },
  },
  validator(config) {
    if (!Array.isArray(config.contentTypes)) {
      throw new Error('redaksjonelt: contentTypes must be an array');
    }
  },
};
