import { Pencil } from '@strapi/icons';
import pluginId from './pluginId';
import nb from './translations/nb.json';
import en from './translations/en.json';

export default {
  register(app: any) {
    app.addMenuLink({
      to: `plugins/${pluginId}`,
      icon: Pencil,
      intlLabel: {
        id: `${pluginId}.menu.label`,
        defaultMessage: 'Redaksjonelt',
      },
      permissions: [],
      Component: async () => {
        const { default: App } = await import('./pages/App');
        return App;
      },
    });

    app.registerPlugin({
      id: pluginId,
      name: 'Redaksjonelt',
      isReady: true,
    });
  },

  async registerTrads({ locales }: { locales: string[] }) {
    const translations: Record<string, Record<string, string>> = {
      nb,
      en,
    };

    return locales.map((locale) => ({
      data: translations[locale]
        ? prefixPluginTranslations(translations[locale], pluginId)
        : {},
      locale,
    }));
  },
};

function prefixPluginTranslations(
  trad: Record<string, string>,
  pluginId: string
): Record<string, string> {
  return Object.keys(trad).reduce(
    (acc, current) => {
      acc[`${pluginId}.${current}`] = trad[current];
      return acc;
    },
    {} as Record<string, string>
  );
}
