'use strict';

module.exports = {
  kind: 'collectionType',
  collectionName: 'redaksjonelt_planlagt_publisering',
  info: {
    singularName: 'planlagt-publisering',
    pluralName: 'planlagt-publiserings',
    displayName: 'Planlagt publisering',
    description: 'Planlagte publiseringer',
  },
  options: {
    draftAndPublish: false,
  },
  attributes: {
    innholdstype: {
      type: 'string',
      required: true,
    },
    dokumentId: {
      type: 'string',
      required: true,
    },
    publiserTid: {
      type: 'datetime',
      required: true,
    },
    status: {
      type: 'enumeration',
      enum: ['venter', 'publisert', 'kansellert'],
      default: 'venter',
      required: true,
    },
    opprettetAv: {
      type: 'string',
      required: true,
    },
  },
};
