# WO-1511 RESULT - six of seven sites are direct calls; the seventh cannot take the fix as written

**Status:** PARTIALLY FIXED AT SOURCE, UNGATED. Uncommitted in the working tree as of 2026-09-06 21:00,
awaiting the wave-two gate. One acceptance item is not implementable as specified - see section 3.
**Commit:** none - working tree only.
**Files (all six converted to compiler-checked direct calls):**
- `Assets/_Modules/Village/Arena/BattleArena.cs:3093` - HeroProgression, same assembly.
- `Assets/_Modules/Village/Diagnostics/CastleNavTopologyDiag.cs:153` - SceneTransitionTrigger, same
  assembly. The file now has zero reflection call sites.
- `Assets/_Modules/Village/VisualFactory.cs:664` - TripoMaterialFixer in DeNelle.Core.
- `Assets/_Modules/HUD/HelpMenu.cs:670` - GameStateService and SceneRouter, both DeNelle.Core.
- `Assets/_Modules/HUD/AdminOverlay.cs:67` (the cached Type is gone) and `:537`.
- `Assets/_Modules/Audio/AudioBootstrap.cs:190` - GameStateService in DeNelle.Core.
- New suite `Assets/Editor/Regression/CoreReflectionSourceRegression.cs`, with the WO-1511 note at
  `Assets/Editor/Regression/DataRegression.cs:1524`.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and
committed in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the
current working tree. The wave-two gate is owed.

## 1. The sanctioned seam is preserved

`AdminOverlay.cs:537-551` keeps its `Type.GetType("DeNelle.Village.WaveManager, DeNelle.Village")` and
says so in-code: DeNelle.HUD.asmdef does not reference DeNelle.Village, so that reflection is evidence
of the rule, not a violation. `CoreReflectionSourceRegression.cs:32-36` excludes those sites on purpose
and explains why it structurally cannot flag a sanctioned seam - it reads each file's nearest .asmdef
and fires only where the reference ALREADY exists, rather than keeping a hand-written file allowlist.

## 2. Acceptance

- [x] Six of seven replaced, file:line list above.
- [ ] `HudKitController` audio call goes through `CoreServices.Audio`. NOT DONE, and see section 3.
- [x] A no-new-reflection regression covers these files. `CoreReflectionSourceRegression.cs`, scoped to
      `Assets/_Modules` and reading the .asmdef as the authority.
- [ ] `REGRESSION_OK n/n` on a fresh log. OPEN - see the gates line.

## 3. Where the tree contradicts the ticket

The WO calls `HudKitController.cs:4281` a bypass of the sanctioned seam. Read at source, the site is now
`HudKitController.cs:4519-4533`, `OpenJukebox`, and it does not play audio: it resolves
`DeNelle.Audio.MusicSelectionPanel` and invokes that panel's `Toggle`. Two facts make the specified fix
impossible. `Assets/_Modules/HUD/DeNelle.HUD.asmdef` references only DeNelle.Core, DeNelle.Data and
four UI packages - NOT DeNelle.Audio, so no direct reference exists to fall back on. And
`Assets/_Modules/Core/Audio/IAudioService.cs:6-21` exposes exactly `PlaySfx`, `PlayMusic`, `StopMusic`
and `PlayUiClick` - there is no panel-toggle member, so `CoreServices.Audio` cannot carry this call.
The site is the same class as AdminOverlay's Village reflection. Closing it needs an owner ruling: widen
the Core seam, or accept the reflection and record it as sanctioned.

No device capture is required for this ticket - it is a compile-time architecture change. The proof owed
is the wave-two `REGRESSION_OK` with the new suite registered.
