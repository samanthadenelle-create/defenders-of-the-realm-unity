const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');

test('WebGL currency skin splits Pi Browser from Solana web hosts', () => {
  const source = fs.readFileSync(
    path.join(root, 'Assets/_Modules/Core/Platform/CurrencySkinResolver.cs'),
    'utf8'
  );

  assert.match(source, /bool piBrowser = WebGLPiPlatform\.IsPiBrowserEnvironment;/);
  assert.match(source, /requested = piBrowser \? "pi" : "skr";/);
});

