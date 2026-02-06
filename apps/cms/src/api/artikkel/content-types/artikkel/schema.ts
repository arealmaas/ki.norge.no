export default {
  kind: 'collectionType',
  collectionName: 'artikkels',
  info: {
    singularName: 'artikkel',
    pluralName: 'artikkels',
    displayName: 'Artikkel',
    description: 'Nyheter og oppdateringer',
  },
  options: {
    draftAndPublish: true,
  },
  pluginOptions: {
    i18n: {
      localized: true,
    },
  },
  attributes: {
    tittel: {
      type: 'string',
      required: true,
    },
    slug: {
      type: 'uid',
      targetField: 'tittel',
      required: true,
    },
    innhold: {
      type: 'blocks',
    },
  },
};
