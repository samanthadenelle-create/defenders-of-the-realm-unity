const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '..');
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8');

test('local-only clan chat is gated at both player entry points', () => {
  const gate = read('Assets/_Modules/Core/Services/ClanFeatureGate.cs');
  const dock = read('Assets/_Modules/HUD/Kit/HudKitController.cs');
  const bootstrap = read('Assets/_Modules/HUD/ClanChatPanelBootstrap.cs');
  assert.match(gate, /PlayerFacingEnabled = false/);
  assert.match(dock, /if \(DeNelle\.Core\.Services\.ClanFeatureGate\.PlayerFacingEnabled\)[\s\S]*"Chat"/);
  assert.match(bootstrap, /if \(!ClanFeatureGate\.PlayerFacingEnabled\) return;/);
});

