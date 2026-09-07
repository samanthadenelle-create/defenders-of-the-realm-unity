# WO-1473 RESULT - the loop release policy is built; it landed in two halves and neither is gated yet

**Status:** FIXED IN SOURCE, GATE AND CAPTURE OWED. The lane landed in TWO halves.
**Commit (half one):** `eb161dc98` (2026-09-06 20:10) - `Assets/Editor/Regression/VfxLoopFlagRegression.cs`
(new, +109, the catalog loop-flag oracle), `Assets/_Modules/Village/Vfx/VFXManager.cs` (+83),
`Assets/Resources/VFX/VFXCatalog.asset` (84 lines), registered at `DataRegression.cs:891` as the
`vfx-loop-flag` suite.
**Half two:** uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
**Files (uncommitted half):**
- `VFXManager.cs:1449` `TickLoopReleasePolicy()` - one policy in the pool, run BEFORE the audit;
  `:337` `OFFSCREEN_RELEASE_GRACE = 6f` in UNSCALED seconds; `:339` `LOOP_VISIBILITY_RADIUS = 3f` for
  the frustum test; `:1570` `EnforceOwnerLoopCap` caps per-owner records at the slot-taking moment,
  which is the counter-leak shape; `:2449` `VfxLoopReleasePolicy` with `:2486 Decide(...)` returning
  Keep / Suspend / Resume. Suspend releases the SLOT without disturbing ownership (particles stopped
  and cleared, resumable byte-identically); the one HARD release stays in `ReclaimDestroyedLoops`.
- `VfxLoopFlagRegression.cs` (+242) - the `Decide` decision table. RED proof in its header: hardwire
  `Decide` to return `Keep` and cases 1, 2, 4 and 6 fail.
- Also in the lane: `ArcaneTower.cs` (+29), `HeartAuraController.cs` (+37), `PortalVFXController.cs`
  (+48), `AmbientAuraPolicy.cs` (+52, that half is WO-1476).

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The reds were a UI-MVVM violation on
`BuildPreviewModal.cs:252-253` and a hollow pass at `NightMarketNoWalletRegression.cs:761`, both fixed
at source in `eb161dc98` (20:10), AFTER both logs. Neither log postdates that commit or the working
tree, so the wave-two gate is owed. That run does carry
`[vfx-loop-flag] vfx-loop-flag OK: 242 catalog`, but that is the CATALOG oracle - the release-policy
cases are in the uncommitted half and have never executed.

## Acceptance

- [ ] Zero `STUCK LOOP` lines in a full town plus raid session - NOT captured. Pre-fix evidence: 20
      occurrences of `STUCK LOOP ArcaneTower_Aura owner='Arcane Spire'#N age=303s ... 14/24`, plus the
      pool pinning at 24/24 in raids with `Damage_Ruin skipped` 31x and `oneshot cap hit` 76x.
- [x] Pool-drain regression with a RED proof stated - the `Decide` decision table above.
- [ ] `REGRESSION_OK n/n` on a fresh log - not obtained, see the gates line.

WO section 3 held: the ceiling stays at 24, and nothing special-cases `ArcaneTower_Aura` - the policy
is generic over any looping effect with a destroyable owner. The `STUCK LOOP` diagnostic is kept and
becomes the oracle.

Owed: a town plus raid device session on the post-fix build. It is also the evidence WO-1459 (frame
floor) and WO-1484 (heap growth) are waiting on.
