# WO-1476 RESULT - the riser is the tree-foot Aura_Nature seat, proven from the prefab bytes, and it is withheld

**Status:** FIXED IN SOURCE, OWNER CAPTURE OWED. Uncommitted in the working tree as of 2026-09-06
21:00, awaiting the wave-two gate.
**Commit:** none. All three files are working-tree modifications.
**Files:**
- `Assets/_Modules/Village/Vfx/AmbientAuraPolicy.cs:95-145` - the WO-1476 block. `:124`
  `WithheldTreeFootAuraKey = "atfootprintoftree_Aura"`; `:129`
  `public static readonly bool WithholdTreeFootAura = true` (readonly not const, so neither branch is
  CS0162); `:133` `ShouldWithholdTreeFootAura(key)`; `:137` `TreeFootWithholdReason(site)`.
- `Assets/_Modules/Village/Heart/HeartAuraController.cs:315-320` - the only production call site. When
  withheld it emits a `FlowTrace.Step("Heart", ...)` naming key, reason and foot position instead of
  calling `HeldVfxHook.Play`; the else branch is the original spawn, untouched.
- `Assets/Editor/Regression/VfxLoopFlagRegression.cs:895-910` - the case: no town ambient seat may
  drive particles up +Y. RED proof stated - flip `WithholdTreeFootAura` to false and it fails.

## Which candidate, and how it was decided

The WO named two candidates; a candidate is not a conclusion, so both prefabs were read as YAML.
`TreeofLifeAura_Aura` -> `FireFlies.prefab`: BOTH ParticleSystems carry `VelocityModule enabled: 0`,
`ForceModule enabled: 0`, `gravityModifier` 0, `startSpeed` 0 - nothing there can impart directed
motion, so it CANNOT be the riser, and it is left as the owner tagged it. `atfootprintoftree_Aura` ->
`Aura_Nature.prefab`: its "Energy" sub-emitter carries `VelocityModule enabled: 1` with y
`minMaxState 3`, `minScalar 0.3` / `scalar 0.5` over a 2-4 s `startLifetime`, `inWorldSpace 0`. Every
particle is pushed up local +Y. Its "Trails" sub-emitter is z-only; the other four are disabled.

**The seam is the policy, not the pick.** `VfxManualPicks.json` is the owner's and this row is also
pinned by `NightStoreAuraSelectionRegression`'s PinnedTags table, so the row is left byte-intact and
the SPAWN is withheld - the same shape WO-1002 used for the FireFlies loop in the same file. No
replacement effect was chosen (WO section 3).

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The reds were a UI-MVVM violation on
`BuildPreviewModal.cs:252-253` and a hollow pass at `NightMarketNoWalletRegression.cs:761`, both fixed
at source in `eb161dc98` (20:10), AFTER both logs. Neither log postdates that commit or the current
working tree, so the wave-two gate is owed.

## Acceptance

- [x] The emitter is NAMED - but note the deviation: WO section 2 asks it be identified from a device
      FRAME. No town frame isolating the emitter exists, so it was identified from the two candidates'
      velocity fields. That is a stronger discriminator here, but it is not what was asked.
- [ ] A fresh town capture shows it gone - NOT captured. This is the owner-facing line.
- [x] Scene VFX manifest case added - `VfxLoopFlagRegression.cs:895-910`.
- [ ] `REGRESSION_OK n/n` on a fresh log - not obtained, see the gates line.

Owed: one town device capture on the post-fix build for the owner to confirm the sky is clear.
