'use strict';

// =============================================================================
// THE SITE / GAME SPLIT, PINNED (owner ruling 2026-09-07: "i want the site that
// has our dapp link and about the game").
// -----------------------------------------------------------------------------
// echoes-of-elarion.vercel.app serves the MARKETING SITE (site/), and
// defenders-webgl.vercel.app serves the WEBGL GAME. That split has been undone
// once already, by accident: on 2026-09-02 a deploy of the repo root went to the
// echoes-of-elarion project id, replaced the landing site with the Unity shell,
// and took /privacy and /terms down with it. Every gate in the repo went green.
// A Solana dApp Store reviewer found it on 2026-09-03 and rejected the app.
//
// tools/web-ship.ps1 now encodes the split in its surface registry, and its
// Phase 2 asserts the served page still carries the dApp Store deep link - a
// string the Unity shell cannot contain. This file guards the SOURCE side of
// that: the registry entries and the page invariants they assert about. It is a
// text check over a PowerShell file because node cannot execute one, which is
// the same unavoidable duplication api/_lib/dungeon-manifest.json documents -
// so it is scoped to the registry block and guarded, never a loose grep.
//
// The behavioural proof is the dry run, which needs PowerShell and is not run
// from here:
//   powershell -NoProfile -File tools\web-ship.ps1 -DryRun
//
// Run: node --test test/
// =============================================================================

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.join(__dirname, '..');
const shipPath = path.join(repoRoot, 'tools', 'web-ship.ps1');
const indexPath = path.join(repoRoot, 'site', 'index.html');

const DAPP_LINK = 'solanadappstore://details?id=com.denellestudios.echoesofelarion';
const WEBGL_HOST = 'https://defenders-webgl.vercel.app/';

const ship = fs.readFileSync(shipPath, 'utf8');
const indexHtml = fs.readFileSync(indexPath, 'utf8');

/**
 * The `[pscustomobject]@{ ... }` block for one surface, sliced out of the
 * $Surfaces literal by its Name. Scoping every assertion to one surface's own
 * block is what stops a value belonging to a DIFFERENT surface - or to a
 * comment - from satisfying it.
 */
function surfaceBlock(name) {
  const start = ship.indexOf('$Surfaces = @(');
  assert.notEqual(start, -1, 'tools/web-ship.ps1 no longer declares $Surfaces');
  const nameAt = ship.indexOf(`Name            = '${name}'`, start);
  assert.notEqual(nameAt, -1, `$Surfaces has no entry named ${name}`);
  const blockStart = ship.lastIndexOf('[pscustomobject]@{', nameAt);
  let blockEnd = ship.indexOf('[pscustomobject]@{', nameAt);
  if (blockEnd === -1) blockEnd = ship.indexOf('\n)', nameAt);
  return ship.slice(blockStart, blockEnd);
}

function field(block, key) {
  const m = block.match(new RegExp(`^\\s*${key}\\s*=\\s*(.+)$`, 'm'));
  return m ? m[1].trim() : null;
}

test('the marketing site surface deploys site/ and is a site payload', () => {
  const b = surfaceBlock('echoes-of-elarion');
  assert.equal(field(b, 'Payload'), "'site'");
  // DeployRoot is the half that stops the 2026-09-02 accident repeating:
  // `vercel deploy` uploads the current directory, so the repo root would ship
  // the game shell to this project id all over again.
  assert.equal(field(b, 'DeployRoot'), "'site'");
  assert.equal(field(b, 'Role'), "'production'");
  assert.match(field(b, 'ProjectId'), /^'prj_[A-Za-z0-9]+'$/, 'the site surface needs a real project id');
  // ONE alias. It used to carry the WebGL domain too, back when both domains
  // served the same payload; leaving it would alias the game's domain onto the
  // marketing site.
  assert.equal(field(b, 'Aliases'), "@('echoes-of-elarion.vercel.app')");
});

test('the site surface asserts the dApp Store deep link is served', () => {
  const b = surfaceBlock('echoes-of-elarion');
  const expect = field(b, 'Expect');
  assert.ok(
    expect.includes(DAPP_LINK),
    'Expect must name the dApp Store deep link - it is the one string the Unity WebGL shell cannot contain, and therefore the only reliable detector that the wrong payload was deployed'
  );
  assert.ok(expect.includes('Echoes of Elarion'), 'Expect must name the brand');
});

test('the WebGL surface owns the game domain and is a webgl payload', () => {
  const b = surfaceBlock('defenders-webgl');
  assert.equal(field(b, 'Payload'), "'webgl'");
  assert.equal(field(b, 'Role'), "'production'");
  assert.equal(field(b, 'Aliases'), "@('defenders-webgl.vercel.app')");
});

test('an unknown project id blocks a deploy loudly instead of passing silently', () => {
  // defenders-webgl's project id is UNPROVEN from the repo: no `vercel project
  // ls` output exists under Builds/. It is left empty on purpose, and the file
  // must emit WEB_DEPLOY_BLOCKED rather than skip it quietly. If the owner
  // pastes the id in, this case still passes - it pins the mechanism, not the
  // emptiness.
  assert.ok(
    ship.includes('WEB_DEPLOY_BLOCKED'),
    'web-ship.ps1 must announce a surface it could not ship'
  );
  assert.ok(
    /WEB_SHIP_PUSH_OK deployed=\{0\} blocked=\{1\}/.test(ship),
    'the blocked list must ride on the WEB_SHIP_PUSH_OK marker line, not in a separate message a reader can miss'
  );
});

test('site/index.html keeps both doors and stays ASCII', () => {
  assert.ok(indexHtml.includes(DAPP_LINK), 'the dApp Store deep link is the primary call to action');
  assert.ok(indexHtml.includes(WEBGL_HOST), 'the browser-build link must point at the WebGL surface');
  assert.ok(
    indexHtml.includes('/qr-dappstore.png'),
    'the dApp Store QR is the only door for a visitor who is not on a Seeker'
  );
  const bytes = fs.readFileSync(indexPath);
  const offending = [];
  for (let i = 0; i < bytes.length; i += 1) {
    if (bytes[i] > 0x7f) offending.push(i);
  }
  assert.deepEqual(offending, [], 'site/index.html carries non-ASCII bytes at these offsets');
});
