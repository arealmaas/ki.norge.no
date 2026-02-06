export default {
  kind: 'collectionType',
  collectionName: 'arbeidsflyt_loggs',
  info: {
    singularName: 'arbeidsflyt-logg',
    pluralName: 'arbeidsflyt-loggs',
    displayName: 'Arbeidsflyt-logg',
    description: 'Logg over arbeidsflyt-handlinger',
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
    handling: {
      type: 'enumeration',
      enum: [
        'sendt_til_godkjenning',
        'godkjent',
        'avvist',
        'publisert',
        'avpublisert',
      ],
      required: true,
    },
    kommentar: {
      type: 'text',
    },
    utfortAv: {
      type: 'string',
      required: true,
    },
    tidspunkt: {
      type: 'datetime',
      required: true,
    },
  },
};
