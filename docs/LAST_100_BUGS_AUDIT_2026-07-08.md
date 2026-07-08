# LAST-100-BUGS AUDIT — do our fixes hold, or do they reoccur?

**Date:** 2026-07-08. **Question (owner):** use the coverage system to check the last ~100 logged bugs —
are they all still fixed, or have any reoccurred? **Method:** mine the recent bug records (git `fix`
commits, F8 board, `WorkOrders/*.RESULT.md`, fleet break-log) → dedup to the 107 most-recent distinct
logged bugs → map each to its regression check (a `DataRegression.RunAll` oracle, a fleet `Assert*` probe,
or NONE) → run the full battery (`RunAll` + a fresh 8-seed fleet on the current instrumented tree, exe
built 2026-07-08 18:06) → classify each as **still-fixed / REOCCURRED / unverified**.

---

## THE ANSWER

- **Reoccurrences: ZERO.** No bug marked *fixed* maps to a currently-failing check. Every fixed bug that
  has a regression check — its check **passes**.
- **Coverage of the 107:** 22 covered by an oracle · 36 covered by a fleet probe · **49 have NO check**.
- **The 58 checked bugs:** all confirmed **still-fixed**, except the handful whose *status is still open*
  (F8-39, F8-41, arena untextured, Wood/Iron dual-wallet, pet-slot) — their checks fail because the bug is
  **open, not reoccurred** — plus the 3 known-open pre-existers below.
- **The 49 unchecked bugs:** **unverified** — they have no regression check, so they *could* silently
  reoccur and we would not know. This is the real gap the audit exposes.

**Bottom line:** our fixes are holding. The risk is not regression of what we've fixed — it's the
**~46% of recent bugs with no automated guard at all.**

---

## EVIDENCE

### Oracle battery (`RunAll`, ~30s, this tree)
16/21 PASS. The 5 FAILs are all **open bugs / by-design**, none a reoccurrence: `TowerRespawn` (F8-39
open), `DefenseTargetable` (F8-41 open), `ArenaPrefabAudit` (arena ground open), `VillageEconomy`
(Wood/Iron dual-wallet, COV-021 open), `GlimmerEconomy` (pet-slot, by-design).

### Fleet battery (8 seeds, exe 18:06, EnsureHero overworld coverage LIVE)
Every fixed-bug probe PASS: TutorialArms, HeroHasAlbedo (19/19), VendorContracts (0), VendorTalkRoute (0),
EconomyDeduct, Equip, SaveRoundTrip, TutorialFirstTower, DialogueChain (the P0), OpenEachHUDPanel (12/12),
PopupClose (12/0), OrientModalReleases, CombatInvariants, WaveVendorRules, CompassMarks, ScatterRecords
(gen 18). **Pets-combat-off caused no regression** (CombatInvariants PASS; towers/panels green).

Only 4 fleet tickets, all **known-open pre-existers** (not reoccurrences — never fixed):
- WO-602 home-return unwired (`HOME_RETURN_FAIL`, 8/8)
- WO-453 overworld rep never spawns (`AssertEncounterRealPath`, 8/8)
- CavePortal seam unreachable (bake gap, closest 442.9m > 16m, 5/8) + `AttemptExitCastle` can't path (2/8)

These now surface *because* the EnsureHero coverage-unlock makes the fleet actually drive the overworld
(previously skipped "no hero"). More honest coverage, not new bugs.

---

## THE COVERAGE GAP — 49 bugs with NO check (the real risk)

Clustered, these silently-reoccur-able bugs are:
- **UI-layout / chrome sizing** (~19): partybar glyphs, dock 5%/9-slice, nameplate GUID, portrait mask,
  victory rows, close-band overlap, target-frame hide, canvas-portrait, endstate overlap, dup-UIDocument
  raycast, screenshot IO, font, bug-report button. *No visual-layout/glyph oracle exists.*
- **Object orientation / seating** (~8): wood-yaw, arcane-spire euler, wizard Z-90, gear sheath rot,
  harvest flat, sheathed orient, shield carry rot, hilt float. *No orientation oracle exists.*
- **World geometry / grounding / colliders / z-fight** (~6): castle stairs, arrow-trap tiles, visual-child
  colliders, hero-in-wall, z-fight, buildings-ground.
- **Anim / camera feel** (~6): town locomotion, camera recenter, hero grounding, pet-dead param, cadence
  mult, enemy spawn size. *(Feel — needs the fleet/human, not an oracle.)*
- **DevTools / web / build** (~3): devtools scroll, build-mode watchdog, skr CS0104.
- **Render-artifact / infra / feature-pins** (design pins + not-bugs): wave-posture pin, max-tier-tower
  (feature), repair-costs (feature).

### Highest-value gaps to close next (recurring, player-facing, no guard)
1. **An orientation oracle** — derive-from-bounds+name check on weapon/structure/harvest seating (covers 8
   of the recurring euler/seating bugs; ties to `WEAPON_ARMOR_ORIENT_LOGIC.md`).
2. **A UI-layout / glyph-render oracle** — assert HUD widgets fit their zone + labels render >0 glyphs +
   no overlap (covers ~19 of the layout bugs; several are the same class re-reported).
3. **A structure-albedo oracle** — extend `EnemyRigColor`'s serialized-sheet read to placed structures
   (covers arcane-spire-white and the tower-albedo class).

Closing these three would move ~30 of the 49 unchecked bugs under a guard.

---

## VERDICT ON THE COVERAGE INVESTMENT (the ROI question, answered by this audit)
The system proves our fixes hold (zero reoccurrence across 58 checked bugs, confirmed in ~30s of oracle +
one fleet). Its honest limit is coverage breadth: 49 recent bugs still have no guard. The investment's
payoff is now measurable and ongoing — every future session runs the same battery, and the named
next-oracles convert the gap into guarded coverage. Full findings + ROI scorecard:
`docs/COVERAGE_FINDINGS_LEDGER_2026-07-08.md`. Full 107-row mining map: the `last-100-bugs-audit`
workflow transcript.
</content>
