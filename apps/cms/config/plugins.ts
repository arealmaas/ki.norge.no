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
  },
});
