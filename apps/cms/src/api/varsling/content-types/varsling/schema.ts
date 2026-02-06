export default {
  kind: 'collectionType',
  collectionName: 'varslinger',
  info: {
    singularName: 'varsling',
    pluralName: 'varslinger',
    displayName: 'Varsling',
    description: 'Varsler for arbeidsflyt-hendelser',
  },
  options: {
    draftAndPublish: false,
  },
  attributes: {
    mottaker: {
      type: 'string',
      required: true,
    },
    type: {
      type: 'enumeration',
      enum: [
        'sendt_til_godkjenning',
        'godkjent',
        'avvist',
        'publisert',
        'planlagt',
      ],
      required: true,
    },
    melding: {
      type: 'text',
      required: true,
    },
    innholdstype: {
      type: 'string',
    },
    dokumentId: {
      type: 'string',
    },
    lest: {
      type: 'boolean',
      default: false,
    },
    tidspunkt: {
      type: 'datetime',
      required: true,
    },
  },
};
