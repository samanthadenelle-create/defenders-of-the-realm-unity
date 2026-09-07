// =============================================================================
// tools/gen-sku-catalog.mjs - WO-1532.
// Copies the canonical pack catalog to where a Vercel function can actually
// read it.
// -----------------------------------------------------------------------------
// ⛔ WHY A COPY EXISTS AT ALL, since a copy is normally the bug in this repo.
//
// `.vercelignore` allowlists exactly `/api`, `/Builds/WebGL`, `vercel.json` and
// `package.json` - and `vercel.json` sets `git.deploymentEnabled:false`, so
// production is a CLI upload of that allowlist. `Assets/` is NEVER uploaded.
// A `require('../../Assets/Resources/Data/Canonical/packs.json')` inside
// api/admin/stats.js therefore throws at MODULE LOAD in production and takes
// down every view on that endpoint, not merely the new one - while working
// perfectly on this machine, which is the worst shape a failure can have.
//
// So the canonical file is copied in, exactly the way canonical game data has
// always reached this backend:
//   api/_lib/tunable-manifest.generated.json  <- tools/gen-tunable-manifest.mjs
//   api/_lib/dungeon-manifest.json            <- pinned by test/dungeon-status.manifest.test.js
//
// ⛔ AND A COPY WITHOUT AN ORACLE IS THE DUPLICATED STATE CLAUDE.md s2/s5/s16
// keeps paying for. test/admin.skus.view.test.js asserts the generated file
// parses EQUAL to the canonical one and goes RED on any drift. Do not edit
// api/_lib/sku-catalog.generated.json by hand - edit packs.json and re-run:
//
//   node tools/gen-sku-catalog.mjs
//
// The copy is VERBATIM. No filtering, no reshaping, no pruning of `_`-prefixed
// authoring notes: the moment this file starts making decisions about the
// catalog, it becomes a second authoring surface, and the two drift.
// =============================================================================

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.join(HERE, '..');

const SOURCE = path.join(REPO, 'Assets', 'Resources', 'Data', 'Canonical', 'packs.json');
const TARGET = path.join(REPO, 'api', '_lib', 'sku-catalog.generated.json');

const raw = fs.readFileSync(SOURCE, 'utf8');

// Parse before writing. A malformed canonical file must fail HERE, loudly, and
// not become a generated file that crashes a serverless function on cold start.
let doc;
try {
    doc = JSON.parse(raw);
} catch (err) {
    console.error('[gen-sku-catalog] canonical packs.json does not parse: ' + err.message);
    process.exit(1);
}
if (!doc || !Array.isArray(doc.packs) || doc.packs.length === 0) {
    console.error('[gen-sku-catalog] canonical packs.json has no packs[] - refusing to write an empty catalog');
    process.exit(1);
}

// LF, no BOM, trailing newline - the repo's canonical-JSON shape (memory:
// canonical-json-edits-binary-only-verify-newlines). Written through Buffer so
// no platform newline translation can touch it.
const out = JSON.stringify(doc, null, 2).replace(/\r\n/g, '\n') + '\n';
fs.writeFileSync(TARGET, Buffer.from(out, 'utf8'));

console.log('[gen-sku-catalog] wrote ' + path.relative(REPO, TARGET) +
            ' (' + doc.packs.length + ' packs, ' + out.length + ' bytes)');
