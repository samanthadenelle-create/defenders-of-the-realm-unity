# RESULT — WO-682: web errors caught quietly (FSB decode root + pre-warm + oracle)

**Status: IMPLEMENTED + HEADLESS-VERIFIED (data gates). Owner felt-pass on the new web preview
pending (PO closes).** Commit `965309a6` (audio lane).

## Root cause — PROVEN (pipeline rule 0)

Pre-fix proving lines (WebTrace → db → runtime-log echo, 2026-07-12):
```
[Main_Castle_Overworld] error: Loading FSB failed for audio clip "SwordSwing".   (22:48:27 + 23:05:04 UTC)
[Flow:Perf] LOW fps=6 ms=167.9 …   /   [Flow:Perf] LOW fps=0 ms=4000.0 …        (same batches)
```
`SwordSwing.wav.meta` carried a WebGL `platformSettingOverrides` block (loadType 0 /
sampleRateSetting 2 / quality 0.45) that fails FSB decode in the web player. It was loaded
per-swing (`GameSfx.PlaySwordSwing` ← `PlayerAttackController.cs:394`) → per-swing error spam +
main-thread decode stalls. The owner-seen "giant JSON failure screen" is the Development-player
error overlay: the ship WebGL path was verified already `BuildOptions.None` (`WebGLBuild.cs:124`;
catalog risk P3 #25 is STALE for WebGL) — the overlay build was a `-DevBuild` artifact; tonight's
deploy is a clean ship build.

## What shipped

- **13 Sfx clip metas swept** to the default import shape (SwordSwing + 12 siblings found by the
  new oracle: LookoutHorn, BuildingUpgrade, DragonRoar, EnemyCastCharge, EnemyDeath,
  FootstepsWalk, HeroHit, SpellCast, SwordClash, TowerArrowHit, UiClick, WeaponDraw). GUIDs
  untouched.
- **`AudioService`**: every clip load/resolve Guard-wrapped; `PrewarmCombatSfx()` (owner ask:
  "pre warm those files on battle load") decodes the ~20-clip combat set under the Battle/Arena
  music cue (the masked warp-in — `BattleArena.BeginEncounter` / `ArenaMode.TryStartRaid` route
  here via the Core seam); failed clips quarantined via `MarkSfxClipDead` — ONE Warn, then
  silent skip. Boot-gesture prewarm deliberately skipped (would move the stall onto first tap).
- **New headless oracle** `SfxWebglAudioRegression` → `SFX_WEBGL_OK`, wired into
  `DataRegression.RunAll`: every Sfx clip must import and none may carry a WebGL override.

## Verification (post-fix)

- `COMPILE_GATE_OK` on the combined tree (2026-07-12 evening).
- `DataRegression.RunAll`: **first run FAILED with the 12-sibling finding (the oracle catching
  the class live), re-run after sweep = `SFX_WEBGL_OK`**, total failures back to the 3 known
  pre-existers (arena ground / B2 dual-wallet / pet-slot flag_17) — zero new.
- Owner felt-checks on the new preview: combat audio plays, no decode error line in the web
  session (verify via WebTrace: no `Loading FSB failed` at error level), no failure overlay.
