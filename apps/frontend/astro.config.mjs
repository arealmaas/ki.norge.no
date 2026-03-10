// @ts-check
import { defineConfig } from 'astro/config';
import react from '@astrojs/react';
import node from '@astrojs/node';

import sitemap from '@astrojs/sitemap';

// https://astro.build/config
export default defineConfig({
  output: 'server',
  integrations: [react(), sitemap()],
  site: 'https://ki.norge.no',
  adapter: node({
    mode: 'standalone',
  }),
});