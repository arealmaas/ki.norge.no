// @ts-check
import { defineConfig } from 'astro/config';
import react from '@astrojs/react';
import cloudflare from '@astrojs/cloudflare';

import sitemap from '@astrojs/sitemap';

// https://astro.build/config
export default defineConfig({
  integrations: [react(), sitemap()],
  site: 'https://ki.norge.no',
  adapter: cloudflare({
    imageService: 'compile',
    sessions: { enabled: false },
  }),
});