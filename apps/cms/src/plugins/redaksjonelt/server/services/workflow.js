'use strict';

module.exports = ({ strapi }) => {
  let seedMode = false;
  return {
    setSeedMode(val) {
      seedMode = val;
    },
    isSeedMode() {
      return seedMode;
    },
  };
};
