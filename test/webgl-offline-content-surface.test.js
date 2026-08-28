const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const root = path.resolve(__dirname, '..');

test('WebGL hides and runtime-blocks the app offline-download flow', () => {
  const settings = fs.readFileSync(
    path.join(root, 'Assets/_Modules/Settings/SettingsController.cs'), 'utf8');
  const panel = fs.readFileSync(
    path.join(root, 'Assets/_Modules/Core/UI/OfflineOptInPanel.cs'), 'utf8');

  const offlineSection = settings.match(
    /#if !UNITY_WEBGL[\s\S]*?Caption\(body, "Offline", y\);[\s\S]*?OnOfflineClicked\);[\s\S]*?#endif/);
  assert.ok(offlineSection, 'the Settings offline-download entry must be excluded from WebGL');

  const showMethod = panel.match(
    /public static void Show\(\)[\s\S]*?Application\.platform == RuntimePlatform\.WebGLPlayer[\s\S]*?return;/);
  assert.ok(showMethod, 'OfflineOptInPanel.Show must fail closed when called in WebGL');
});
