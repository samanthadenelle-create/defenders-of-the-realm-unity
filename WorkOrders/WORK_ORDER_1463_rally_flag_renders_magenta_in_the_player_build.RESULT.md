# WO-1463 RESULT - the rally flag gets real URP materials; only a device capture proves the magenta gone

**Status:** FIXED IN SOURCE, DEVICE PROOF OWED. Uncommitted in the working tree as of 2026-09-06
21:00, awaiting the wave-two gate.
**Commit:** none. Both files are working-tree modifications.
**Files:**
- `Assets/_Modules/Village/Troops/RaidDeployController.cs:622-623` - the standing comment on why every
  primitive here needs an explicit material; `:659-664` pole and `:668-673` banner each call
  `ApplyUrpMaterial(...)` immediately after `CreatePrimitive`, replacing the tint on the built-in
  `Default-Material`; `:676` `MagentaGuard.ProtectPrimitiveArt` registers the deliberate primitive art
  so the magenta sweep does not hide it.
- `Assets/Editor/Regression/RaidSelectionLayoutRegression.cs:721-760` - case S8, a POSITIONAL lint:
  each `CreatePrimitive(` must be followed by a material assignment before the next primitive or the
  enclosing return. A file-wide "helper called at least once" check would pass on one fixed and one
  bare primitive, which is the shape a later edit adds.

## Colour provenance (WO section 3 held)

No hue was invented. The banner takes the kit's canonical gilt `ElarionUi.Gilt` (#eec848,
`Assets/_Modules/Core/UI/ElarionUi.cs:60`), the same gold the obsidian chrome already trims panels
with; the pole keeps its shipped dark-wood literal. The change is HOW the colour is applied.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The reds were a UI-MVVM violation on
`BuildPreviewModal.cs:252-253` and a hollow pass at `NightMarketNoWalletRegression.cs:761`, both fixed
at source in `eb161dc98` (20:10), AFTER both logs. Neither log postdates that commit or the current
working tree, so the wave-two gate is owed.

## Acceptance

- [ ] Flag renders in the kit colour on a device capture - NOT met, and unmeetable from the editor. A
      built-in-material defect only shows in a player build, so this needs a post-fix Seeker
      screenshot. Pre-fix frame: `Logs/device/screens/owner-raid-ui-2026-09-06-143701.png`.
- [x] The `CreatePrimitive` regression exists and goes red on a bare primitive - case S8, RED proof at
      `RaidSelectionLayoutRegression.cs:726-732` (HEAD had two `CreatePrimitive` calls and zero
      `sharedMaterial` or `.material =` occurrences).
- [ ] `REGRESSION_OK n/n` on a fresh log - not obtained, see the gates line.

**Scope deviation:** WO section 2 asks for the lint "anywhere under `_Modules`". The suite is scoped to
the one file the evidence names; the repo-wide sweep is left as a separate, larger ticket.

Owed: a post-fix device capture of the rally flag, plus the wave-two regression run.
