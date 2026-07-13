# Handover — 2026-06-12 overnight autonomous run

Owner resting; lead ran autonomously. Everything below is **committed locally, NOT pushed**
(per CLAUDE.md: push only after owner retest). Review, playtest, then push the ones that pass.

## Builds ready to test
- **Windows:** `Builds/Windows/DefendersOfTheRealm.exe` (fresh, has everything below).
- **WebGL:** `Builds/WebGL/` (382 MB, Brotli). NOT deployed — itch needs a `-NoBrotli`
  rebuild + the payload is near the size ceiling; that deploy is a one-word go when you're back.

## The #1 thing to verify: the castle exit
Every headless score is GREEN (navmesh reaches 8 m outside all 4 gates; exit trigger active +
correctly placed at (−4.37,1.5,−44.6) r9; hero tagged `Player`; OuterWorld warp-landing
(0,0.5,−80) is on navmesh at 0.47 m; OuterWorld enabled in Build Settings). The
`CanStreamedLevelBeLoaded=False` scare was proven a **batchmode artifact** (False for every
scene incl. the open one). So it *should* work in the build.
- **Test it:** Play MainCastle_Hall, walk into the south gate, watch Console for
  `[SceneTransitionTrigger]`. Or enable the **player bot** (`PlayerPrefs "dev.playerbot"=1`) —
  it auto-drives spawn→gate→exit and logs a FAIL (captured by the F8 harness) if it doesn't fire.
- If it still fails, the Console (log vs no-log) tells me trigger-not-firing vs warp-landing,
  and each is a one-line fix.

## Commits this session (newest first)
- `ad281ac` ATB idle-timer reset on player action · DailyQuest feature gate (6 templates unblocked) · HeroLocomotion comment truth
- `de454a6` metas · `dfde9b9` Aegis setId + version + 6 WebGL catalogs mirrored + ??-lint guard
- `58d2555` spatial blueprint extractor + castle-exit behavioral gate + **player bot**
- `330bed6` the `??`-on-UnityObject crash sweep (~30 sites) + 6-silo telemetry bug fixes
- `48c311a` dev-tools settings button ships in builds · `702d808` **P0 HUD partial-build crash** (the cascade behind talk/portrait/comet/BAG/overlap)
- `ed8f569` castle from recipe + exit seam re-placed · `eacec2c` MASTER_CATALOG + binding pre-read

All gated `REGRESSION_OK` (21/21 incl. the new `castle-gate-exitable` behavioral case).

## The big architecture direction (your call to drive)
The **spatial blueprint = player-map contract** (memory `spatial-blueprint-player-map-contract`).
`CastleBlueprint.Extract` proves the method (`docs/CASTLE_BLUEPRINT.md`). Next steps, each with a
confidence gate, when you want them: (2) emit `<scene>-blueprint.json`, (3) `SpatialBlueprint`
Core service, (4) re-point the seam to derive `warpTo` from OuterWorld's declared entry (this is
the exit fix done *right*). I held (4) deliberately — it touches the live exit and shouldn't be
refactored before you confirm the current baseline works.

## Still open / needs you
- Playtest verdict on exit + the HUD partial-build fix (should clear talk/portrait/BAG/overlap).
- itch deploy decision (needs `-NoBrotli`).
- Risky backlog held for your call: magenta enemy/structure materials (do NOT run the broad
  MagentaMaterialFixer — null-fill breaks VFX), GameAudioMixer stub → real mixer.
- Shape the blueprint JSON contract with me before I build steps 2–4.
