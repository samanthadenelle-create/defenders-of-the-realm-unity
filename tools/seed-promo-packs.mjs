#!/usr/bin/env node
// One-shot SQL generator for WO-1258. It never connects or writes production.
import fs from 'node:fs';
import path from 'node:path';

const source = path.resolve('Assets/Resources/Data/Canonical/packs.json');
const doc = JSON.parse(fs.readFileSync(source, 'utf8'));
if (!Array.isArray(doc.packs)) throw new Error('packs.json has no packs array');

const literal = (value) => `'${String(value).replaceAll("'", "''")}'`;
process.stdout.write('BEGIN;\n');
for (const pack of doc.packs) {
  if (!pack?.sku || !pack?.name || !pack?.contents) throw new Error('pack missing sku/name/contents');
  const contents = JSON.stringify(pack.contents);
  process.stdout.write(
    `INSERT INTO packs (sku,name,contents,active,store_visible) VALUES (` +
    `${literal(pack.sku)},${literal(pack.name)},${literal(contents)}::jsonb,TRUE,` +
    `${pack.storeVisible === true ? 'TRUE' : 'FALSE'}) ON CONFLICT (sku) DO NOTHING;\n`
  );
}
process.stdout.write('COMMIT;\n');
