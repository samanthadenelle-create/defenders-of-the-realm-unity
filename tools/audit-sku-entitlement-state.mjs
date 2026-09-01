#!/usr/bin/env node
// Read-only WO-1275 constraint audit. Never prints credentials or row data.
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@neondatabase/serverless';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
let url = process.env.DATABASE_URL || '';
if (!url) {
  const match = readFileSync(join(root, '.env.local'), 'utf8').match(/^\s*DATABASE_URL\s*=\s*(.*)$/m);
  if (match) {
    url = match[1].trim();
    if ((url.startsWith('"') && url.endsWith('"')) ||
        (url.startsWith("'") && url.endsWith("'"))) url = url.slice(1, -1);
  }
}
if (!url) process.exit(16);
const client = new Client(url);
await client.connect();
try {
  const result = await client.query(`
    SELECT conname, pg_get_constraintdef(oid) AS definition
      FROM pg_constraint
     WHERE conrelid = 'public.sku_entitlements'::regclass
       AND contype = 'c'
     ORDER BY conname`);
  for (const row of result.rows) console.log(`${row.conname}: ${row.definition}`);
} finally { await client.end(); }
