# WO-1524 RESULT - twelve of thirteen routed; the thirteenth is the one the ticket warned about

**Status:** PARTIALLY IMPLEMENTED IN THE TREE, NOT GATED. Twelve sites converted; one still inline, and it is
not covered by either new oracle.
**Commit:** none for the code - uncommitted in the working tree as of 2026-09-06 21:00, awaiting the
wave-two gate. The ticket itself was minted in `f75c83f66` (2026-09-06 20:29).
**Files (all verified at source today; the ticket's line numbers had moved):**
- `Assets/_Modules/Pets/Pet.cs:574,654` - `MayAttack(EchoSide, dmg)` (was `:556,635`).
- `Village/Buildings/ArcaneTower.cs:408,422` - `IsFriendlyFire(ScanSide, d)` (was `:386,397`).
- `Village/Buildings/DefenseTower.cs:739,755` - `IsFriendlyFire(ScanSide, d)` (was `:717,730`).
- `Village/Buildings/TowerCombat.cs:245,261,302,314` - `MayAttack(ScanSide, ...)` (was `:229,243,283,294`).
- `Village/Hero/HeroAbilities.cs:3116,3146` - `MayAttack(HeroSide, ...)` (was `:3097,3125`).
- `Assets/Editor/Regression/DataRegression.cs:2457-2540` - new CASE K, line-agnostic: half (a) fails on any
  live non-comment `.Faction != / == CombatFaction.*`, half (b) fails if a file stops calling
  `CombatFactionRules` at all, so deleting a guard cannot read as a pass.
- `Assets/_Modules/Core/Combat/CombatFactionRules.cs:69-81` - the corrected census note.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. The two reds (UI-MVVM violation on `BuildPreviewModal.cs:252-253`; hollow-pass at
`NightMarketNoWalletRegression.cs:761`) were fixed at source in `eb161dc98` (20:10), AFTER both logs. Neither
log postdates `eb161dc98` or the working tree, so the wave-two gate is owed; CASE K has not executed.

## The gap, measured not recalled

A non-comment grep for `.Faction != / == CombatFaction.` across `Assets/_Modules/` returns exactly **one**
live hit today: `Village/Enemies/PlayerAttackController.cs:597` -
`if (d == null || !d.IsAlive || d.Faction != CombatFaction.Hostile) continue;`. That is the ability/reticle
lane, the exact asymmetry section 1 calls out. Both oracles miss it: CASE K's `factionCallers` list
(`DataRegression.cs:2485-2491`) holds five files and excludes `PlayerAttackController.cs`, and CASE J matches
only the `damageable.`-qualified form (`DataRegression.cs:2448`), not this `d.`-qualified one.

## Acceptance

- [x] Twelve of thirteen routed; file:line list re-verified at source above (every number had moved).
- [ ] **All thirteen routed - OPEN.** `PlayerAttackController.cs:597` is still inline.
- [ ] The no-inline-faction source case exists - **partially**. CASE K exists and is line-agnostic, but its
      file list excludes the one file that still offends, so it passes today while the defect is live.
      RED proof is documented in source (`DataRegression.cs:2473-2477`); no red run is on file.
- [ ] Behaviour unchanged in a captured town wave and a captured raid - **not captured**.
- [ ] `REGRESSION_OK n/n` on a fresh log - **not run** (see the gates line).

**Still needs a device capture:** a town wave and a raid on a post-fix build proving pets, both towers and
hero abilities engage the same targets as before. Twelve mechanical conversions with no behavioural proof is
the risk this ticket named.
