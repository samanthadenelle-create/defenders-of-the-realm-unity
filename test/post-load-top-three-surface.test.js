const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');

test('post-load top three uses only the live board and fails quietly', () => {
  const service = read('Assets/_Modules/Core/Services/LeaderboardService.cs');
  const view = read('Assets/_Modules/Onboarding/PostLoadTopThree.cs');
  assert.match(service, /api\/leaderboard\/get/);
  assert.match(service, /new RemoteLeaderboardSource\(this\)/);
  assert.match(service, /onResult\?\.Invoke\(Array\.Empty<LeaderboardEntry>\(\)\)/);
  assert.doesNotMatch(view, /LocalStubLeaderboardSource|Aldric|Mira/);
  assert.match(view, /BestWave, 3/);
  assert.match(view, /rows == null \|\| rows\.Count == 0/);
  assert.match(view, /!PanelManager\.AnyOpen/);
  assert.match(view, /!BattleLock\.IsInBattle/);
});
