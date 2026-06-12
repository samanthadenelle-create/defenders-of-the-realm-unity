# Ticket Log — 2026-06-12 (F8 telemetry + owner asks → fix → commit)

The closed-loop record so a fix is never re-derived (see memory `fixes-get-lost-and-rederived`).
Source tickets = the in-game F8 BreakCaptureHarness flags + owner directives this session.
All commits on `feat/tower-core-loop`, pushed to origin.

| # | Ticket / ask | Root cause | Fix | Commit |
|---|---|---|---|---|
| 1 | HUD partial / talk broken / no comet | `SetTalkAvailable` `??` on a UnityObject (fake-null) → `MissingComponentException` aborted `Build()` → partial HUD | TryGetComponent | `702d808` |
| 2 | Dev tools button missing in builds | HelpMenu entry `#if DEVELOPMENT_BUILD` while AdminOverlay ships | un-gate the button | `48c311a` |
| 3 | (class) ~30 `??`-on-UnityObject sites | C# `??`/`?.` return Unity fake-null | sweep → TryGetComponent / explicit `==` | `330bed6` |
| 4 | "can't exit castle / map" (×3) | gate arch mesh voxelizes solid across the opening on bake (WO-168 class) | exclude gate meshes from bake + ±65 walkable floor | `2a2d1c8` |
| 5 | store "no stock" | ShopPanel rows used normalized anchors → went negative past ~12 items → off-content | VerticalLayoutGroup + ContentSizeFitter | `2a2d1c8` |
| 6 | dev "Add Resources never did anything" | `GiveCrystals` wrote GameState directly, not `EconomyService.GrantSpendable` → HUD bar (reads EconomyService) never populated | route via GrantSpendable + PingHud | `17d69d0` |
| 7 | "No Command command:" (recruit + ALL vendors/quests) | `.yarn` used dead `<<command: Verb>>` prefix → Yarn read name as `command:` | strip prefix (13 files, 11 verbs) | `7e6d80f`, `f3245f0` |
| 8 | wave timer/Start button dead | MainCastle_Hall had NO WaveManager | add WaveManager + 4 WaveSpawnPoints (12m outside gates) | `2a2d1c8` |
| 9 | combat-idle in town | stance gated on WaveManager-presence; the castle now HAS one | gate on `WaveManager.Phase == Countdown/Active` | `1f6aad0` |
| 10 | enemy damages hero, HP bar doesn't move | `HeroAbilitiesHudBridge` only attached in Village2, never the castle | RuntimeInitialize bootstrap attaches it everywhere | `1f6aad0` |
| 11 | companion intro placement/trigger | introducer at south gate, Talk-only | move to (0,0,20) 20m N + auto-fire on approach | `2a2d1c8` |
| 12 | Aegis set unreachable / WebGL catalogs null | weapons missing setId; 6 catalogs StreamingAssets-only | add setId + mirror 6 catalogs to Resources | `dfde9b9` |
| 13 | DailyQuest templates filtered out | FeatureShipped false for shipped features | flip harvesting/tower-build/cosmetic/talents true | `ad281ac` |
| 14 | "style combat/store/inventory like town HUD" | each UI rolled private styling | route all through ElarionUiKit; Start-Wave top-left | `8af4180` |
| 15 | Vercel deploy fails every push | dashboard Git integration on a backend with no DATABASE_URL | `vercel.json` deploymentEnabled=false (+ owner dashboard disconnect) | `78d9a47` |

## Open (root-caused, not yet implemented)
- **Enemy variety** — `WaveManager._enemyPrefab` single-prefab override defeats `EnemyFactory.Build(def)` + the family system → one enemy type. Fix: don't override / use varied defs.
- **Weaponskill→animation** — `weaponskill-animations.json` built (`78d9a47`); wiring (`WeaponSkillAnimCatalog` + `HeroAbilities.TryCast` + ATB) is a `.cs` pass. Ranger needs fire/volley clips.
- **DPS no healing** — Ranger/Mage have a heal in the E slot → `abilities.json` re-author.
- **Magic persistence + consumable value** — `docs/DESIGN_magic_persistence_and_consumables.md`; needs persisted mana field + un-stub. 2 owner questions open.
- **Spellbook FX, enemy weaponskills, pooling pass** — pending (docs-first per owner).
- **Pixelated animations** — deferred art/texture pass.
