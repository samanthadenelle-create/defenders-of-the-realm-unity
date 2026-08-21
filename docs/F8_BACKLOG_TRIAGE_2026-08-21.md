# F8 backlog triage — 2026-08-21 (frozen ledger)

Inbox drained: seq 2552 -> 3575 (1024 captures) acked with `f8-ack.ps1 -All`
after read-only triage. Owner directive: "ok clear inbox".

## Covered by the 08-21 fix wave (see docs/WORK_LOG_2026-08-21.md)

| Owner flag | Fix commit |
|---|---|
| `NO MOVEMENT BASIS` (2552, 2553) | `b5e49e618` camera tagged MainCamera |
| "no motion on this enemy" / "some slide some run" (3544, 3552) | `5e9512cb5` late-bind animator refresh |
| "Archer Tower 3 is uspide down" (3551) | `7dcb83b75` L3 double-rotation removed |
| "catapults in raids are on their side" (3566) | `8f46c9647` siege excluded from fallen-building rotation |
| dungeon door "goes no where" (3565) | `56ff0fd69` + `33edd77f0` |
| dungeon "Feels really broken" / oil (3346, 3547) | `b77fb339c` + `341599672` |
| Ember Deep stuck (2876) | `520efe031` HeroStartPoint_PlayerSpawn landing contract |

## NOT covered — carried forward (verified at source, not fixed)

1. **Title "tons of dings" (3537)** — `SfxClipLibrary.asset` does not exist; all six
   named clips absent from `Resources/Sfx/`: `TowerFire`, `WaveStart`, `Sfx_ComboSmall`,
   `Sfx_ComboBig`, `Sfx_WaveClear`, `Strike`. Every hit falls through to `ProceduralSfx`
   synth — that is the dinging. (audio.md FLAG-3.)
2. **`BATTLE_QUIESCENCE_FAIL` x3 (3345 arena win, 3545 arena win, 3546 retreat)** —
   "1 invariant NOT restored after the battle". No Quiescence commit in the 08-21 wave.
3. **EditMode unit tests stale + red** — `Builds/test-results-EditMode.xml` total=967
   failed=11, stamped 08-21 09:25 (~5 h before HEAD). `REGRESSION_OK` and `TESTS_OK`
   are different gates; the unit tests were never re-run after the wave.
4. ~~**StandaloneWindows64 CDN verify gap**~~ — **RETRACTED 2026-08-21, this was WRONG.**
   I wrote that `ServerData/StandaloneWindows64/`'s 53 bundles were live content no gate
   had ever verified. Verifying directly disproved it: `python tools/r2_sync.py
   --verify-catalog ServerData/StandaloneWindows64` REFUSES outright —
   *"Library/com.unity.addressables/aa/StandaloneWindows64/settings.json not found - no
   built Addressables state"*. There is no current Windows content build; those files are
   leftovers from an older one, not unverified live content. It is **not** the WO-1124
   shape. The real rule is per-platform: whichever platform ships needs its own content
   build and its own push, and `r2-ship.ps1` verifying the explicit target it just built
   is correct, not a gap. Kept visible rather than deleted — a retraction that vanishes
   teaches nothing.

## Owner design asks captured in the backlog (hers to rule, not defects)

- seq 3283 HeroSelect: "this screen has no value can we remove it, since only in the
  solana dapp store all authentication should be on wallet, this can be hidden till i
  go to google play store"
- seq 3538 HeroSelect: "image is very stretched"

## Un-triaged signals seen in the backlog (noted, not worked)

- 3553-3563: 11x `A scripted object (probably TMPro.ShaderUtilities?) has a different
  serialization layout when loading` (URP AdditionalLightsShadowCasterPass once)
- 3567 / 3572 / 3574: `[Flow:TreeOfLifeFix]` WebGL spawn-guard — no Tree-of-Life at
  `Resources/Structures/tree_of_life`
- 3568 / 3573 / 3575: `[Flow:Hero] EMERGENCY pill spawned in scene 'Village2' — carried
  hero not found`
- 3571: `[Flow:BiomeRoads] TryMeasureWorldBounds found NO active Terrain`
- 2833: `[Flow:Equip] RT PROBE: the preview render texture is a UNIFORM clear colour`

## Standing guard added the same day

`.githooks/pre-push` (owner directive: *"make sure anything pushed always is in sync with
R2"*) refuses `git push` whenever anything under `ServerData/` is newer than
`Builds/r2-parity.log`, or that log carries no `R2_PARITY_OK`. Wired by
`git config core.hooksPath .githooks` — LOCAL config, so **set it once per clone**.
No override flag by design. See CLAUDE.md section 16.
