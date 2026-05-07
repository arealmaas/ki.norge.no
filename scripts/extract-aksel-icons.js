#!/usr/bin/env node
/**
 * Extracts SVG path data from @navikt/aksel-icons into a TypeScript map.
 * Run from repo root: node scripts/extract-aksel-icons.js
 */
const fs = require('fs');
const path = require('path');

const dir = path.join(__dirname, '../apps/frontend/node_modules/@navikt/aksel-icons/src');
const outFile = path.join(__dirname, '../apps/frontend/src/lib/aksel-icons.ts');

const files = fs.readdirSync(dir).filter(f => f.endsWith('.tsx') && f !== 'index.ts');
const icons = {};
let count = 0;

for (const file of files) {
  const name = file.replace('.tsx', '');
  if (name.endsWith('Fill')) continue;

  const content = fs.readFileSync(path.join(dir, file), 'utf-8');
  const match = content.match(/<svg[^>]*>([\s\S]*?)<\/svg>/);
  if (!match) continue;

  let inner = match[1]
    .replace(/{title \? <title id={titleId}>{title}<\/title> : null}/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/fillRule=/g, 'fill-rule=')
    .replace(/clipRule=/g, 'clip-rule=')
    .replace(/strokeWidth=/g, 'stroke-width=')
    .replace(/strokeLinecap=/g, 'stroke-linecap=')
    .replace(/strokeLinejoin=/g, 'stroke-linejoin=')
    .replace(/strokeMiterlimit=/g, 'stroke-miterlimit=')
    .replace(/className=/g, 'class=');

  if (inner.length > 0) {
    icons[name] = inner;
    count++;
  }
}

const output = `// Auto-generated from @navikt/aksel-icons — do not edit manually\n` +
  `// Run: node scripts/extract-aksel-icons.js to regenerate\n\n` +
  `export const akselIcons: Record<string, string> = ${JSON.stringify(icons, null, 2)};\n`;

fs.writeFileSync(outFile, output);
console.log(`Extracted ${count} icons to ${outFile}`);
