# WO-1503 RESULT - the P0 was a misreading trace; the melee lane is on the one authority

**Status:** IMPLEMENTED IN THE TREE, NOT YET GATED. The reported defect did not exist; severity P0 -> P2.
**Commit:** none for the code - uncommitted in the working tree as of 2026-09-06 21:00, awaiting the
wave-two gate. Only the rescoped ticket text is committed (`f75c83f66`, 2026-09-06 20:29).
**Files:**
- `Assets/_Modules/Village/Enemies/PlayerAttackController.cs:719` -
  `if (!CombatFactionRules.MayAttack(HeroFaction, damageable)) continue;` replaces the inline
  `damageable.Faction != CombatFaction.Hostile` copy. `:85-97` adds the cached `HeroFaction` property
  (resolves `IDamageableStructure` from the rig, falls back to `Friendly`), mirroring `Enemy.SelfFaction`.
- `PlayerAttackController.cs:770-772` - the trace now prints target name, type, root, target faction and
  attacker faction. It previously printed only `col.transform.root.name`.
- `Assets/_Modules/Core/Combat/CombatFactionRules.cs:69-81` - the header's "the REMAINING copy" (singular)
  claim corrected with a counted census, flagged as duplicated state.
- `Assets/Editor/Regression/DataRegression.cs:2394-2456` - cases H, I and J (rule refuses own-side, still
  admits Hostile, and the melee caller is pinned to the authority with no inline copy beside it).

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. The two reds (UI-MVVM violation on `BuildPreviewModal.cs:252-253`; hollow-pass at
`NightMarketNoWalletRegression.cs:761`) were fixed at source in `eb161dc98` (20:10), AFTER both logs. Neither
log postdates `eb161dc98` or the working tree, so the wave-two gate is owed; H/I/J have not executed.

## What landed

`CastleHubRoot` carries a `Transform` and nothing else, so it holds no faction and cannot take a hit, while
every hub enemy's `transform.root.name` is `CastleHubRoot` because `WaveManager`'s enemy root is its child.
The eleven logged hits were correct kills printed under the hierarchy root's name. The trace fix is the real
deliverable; the `MayAttack` conversion is the correct end state regardless.

## Acceptance

- [x] Premise disproven at source with the component list - recorded in the ticket, section 1B.
- [x] The melee guard goes through `CombatFactionRules.MayAttack` - `PlayerAttackController.cs:719`.
- [x] The trace names target, type and attacker faction - `PlayerAttackController.cs:770-772`.
- [x] Structure sweep cases H / I / J exist - `DataRegression.cs:2394-2456`.
- [ ] `REGRESSION_OK n/n` on a fresh log - **not run** (see the gates line).

**Carried forward, NOT closed here:** the ability/reticle lane in the same file still compares faction inline
at `PlayerAttackController.cs:597` (`d.Faction != CombatFaction.Hostile`). Case J's regex matches only the
`damageable.`-qualified form, so that line is unpinned. See the WO-1524 RESULT.

**Still needs a device capture:** one post-fix town wave showing the rewritten `hero MELEE hit` line naming a
real enemy rather than `CastleHubRoot`, proving the trace that cost this P0 is readable on the device.
