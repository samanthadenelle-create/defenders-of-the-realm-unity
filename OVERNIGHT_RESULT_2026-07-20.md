# OVERNIGHT RESULT — 2026-07-20 (autonomous fix -> regress -> verify loop)

Execution of `OVERNIGHT_ORDERS_2026-07-19.md`. Owner was away; CLI ran the whole loop solo.
**Frozen point-in-time record (do not rewrite).**

## TL;DR
- **13 of 14 P1 regression suites GREEN** (were 5 baseline reds + 9 fresh fail-by-design at start).
- **12 commits, pushed** to `wip/village2-and-f8-tickets` (origin now `d9653b05`). **Prod untouched.**
- **Founding Echo card visually verified by headless screenshot** (both text states) — two real bugs
  (text truncation + button overlap) were caught BEFORE the build and fixed. This is the owner's
  "never be the first to see a broken panel" rule, now enforced by a working tool.
- **3 builds launched** (Seeker APK -> Windows -> WebGL, serialized by Unity's lock); status in
  `Builds\night-build-status.txt`. WebGL DEPLOY is pending owner (no `vercel` CLI on this box).
- **1 red left, deferred by owner decision #3:** `dungeon-dressing` (content — needs a prop-seeding
  pass in the RoomForge composer). Flagged, not faked green.

## P1s resolved (each with its proving green marker)
| P1 | Fix | Regression marker |
|---|---|---|
| Wave difficulty didn't scale | `EnsureScalingCurve()` code-default curve (HP 1.0->2.5, dmg 1.0->2.0 by wave 20) | WAVE_SCALING_OK |
| Enemies paid no kill rewards | per-enemy xp/coin in enemies.json + Enemy reward sink | ENEMY_REWARDS_OK |
| Walls didn't mitigate Heart dmg | runtime WallDefense reads walls.json heartDamageMultiplier | WALL_MITIGATION_OK |
| Building upgrade authority split | TryUpgrade advances GameState.BuildingTiers (legacy prefs untouched) | UPGRADE_AUTHORITY_OK |
| Pack cosmetics unequippable | route SKUs through GlimmerCurrencyService.MarkCosmeticOwned | PACK_GRANT_OK |
| SFX could resolve null/silent | synth fallbacks + SfxResourceMirror clips into Resources | SFX_RESOLVE_OK |
| Dungeon had no exit | runtime DungeonExitInteractable bootstrap (no re-bake) | DUNGEON_EXIT_OK |
| Founding choice unreachable | HeroSelect bypass now routes PresentOrContinue | FOUNDING_REACH_OK |
| Tutorial taught a non-existent defense | honest copy + real highlight target | FTUE_HONESTY_OK |
| Echo card said "Leveled Up to 1" | awaken header + 6 named souls essence copy | ECHO_CARD_COPY_OK |
| URP shaders could strip in build | IPreprocessBuild pin incl URP Terrain/Lit | SHADER_PIN_OK |
| Modals invisible to back/battle-lock | registered 15 top-band modals w/ PanelManager | MODAL_REGISTRATION_OK |
| Crystal yield hard-coded in C# | data-driven via buildings.json crystalsPerWave (SSOT) | CRYSTAL_PRODUCTION_OK |

## Iterations (caught by the loop, not shipped blind)
- **pack-grant** first attempt used GrantAchievement (no-op for non-catalog pack SKUs) -> re-fixed to
  MarkCosmeticOwned writing the `_ownedSet` that Owns() reads. Green on 2nd pass.
- **modal-registration** first pass wired 7 modals; oracle then surfaced 8 more -> registered all 15
  (battle-allowed vs plain distinction; fixed a Pause<->Settings unfreeze cascade). Green on 2nd pass.
- **Echo founding card** the rect-math "fix" passed compile but the HEADLESS SCREENSHOT showed
  (a) the founding teach line "wood, iron, or grain -- and it is done" TRUNCATED, and (b) three buttons
  overlapping + a redundant Close/Dismiss. Redesigned: auto-size (no truncate) + 3-across button row +
  dropped the duplicate dismiss. Re-rendered both states -> clean. **Screenshots:** `Builds\ui-capture\`.

## New durable tooling
- **Headless UI screenshot capture** — `DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless`
  (edit-mode synchronous render; the old Play-mode path can't work under `-batchmode -quit`). Shoots the
  founding Echo card at 1920x1080 + 2340x1080, both text states, writes `Builds\ui-capture\*.png` +
  `UI_CAPTURE_OK <n>`. Add more edit-mode-safe panels to its pattern over time.
- **14 SME P1 regression suites** wired into `DataRegression.RunAll` (the known-dictionary registry).

## Current DataRegression verdict
`REGRESSION_FAIL: 1` — only `dungeon-dressing` (FAIL-BY-DESIGN, deferred content). All 13 others green.

## What the owner must decide / provide (morning)
1. **Felt-verify** the founding Echo card in a build (screenshots attached but feel is yours).
2. **WebGL deploy:** install `vercel` CLI (`npm i -g vercel`) so the web build can go to a preview URL.
   The chain builds the WebGL player; it does NOT deploy.
3. **Ad App Key + Notion /mcp** still pending you (monetization wiring + live board sync).
4. **dungeon-dressing** + **WO-752 Part B (post-tutorial pet handoff)** + **dual-economy balance** are the
   next content follows — say go and I'll queue them (they need design calls, not just wiring).

## Builds
Launched detached ~06:28 (APK first). Poll `Builds\night-build-status.txt`:
`APK_OK/WINDOWS_OK/WEBGL_OK` per stage, `CHAIN_DONE` at end. A watcher will report each stage.
