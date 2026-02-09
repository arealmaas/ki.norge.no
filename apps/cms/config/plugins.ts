export default ({ env }) => ({
  i18n: {
    enabled: true,
    config: {
      defaultLocale: 'nb',
      locales: ['nb', 'en'],
    },
  },
  redaksjonelt: {
    enabled: true,
    resolve: './src/plugins/redaksjonelt',
    config: {
      contentTypes: [
        'api::artikkel.artikkel',
        'api::eksempel.eksempel',
        'api::veiledning.veiledning',
        'api::side.side',
      ],
    },
  },
});
