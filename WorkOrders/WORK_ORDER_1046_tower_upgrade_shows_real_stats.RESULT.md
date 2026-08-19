# WO-1046 RESULT — the tower upgrade card shows real stats

**Written:** 2026-08-19 (CLI seat) — reconstructed from source + captured logs after an independent
verification pass, because the implementing commit never wrote one.
**Verdict:** IMPLEMENTED and RUNTIME-PROVEN for L1→L2. **Owner felt-verify still outstanding** for the
L2→L3 card, which is the case the ticket was filed from.

---

## 1. What was wrong

The Archer Tower upgrade card read only *"Stronger Archer Tower at Level 3"* for 225 wood + 100 iron —
no number anywhere, so the player could not tell an upgrade from a reskin.

## 2. The §2 balance hypothesis was WRONG, and that matters

§2 suspected a **balance** defect: a static read of `DefenseTower.cs` found `level` used only for the
model and the projectile art, which reads as "the upgrade buys a reskin." §3 therefore ordered a
measurement before any fix — and the measurement disproved the hypothesis.

**The scaling is real; it just lives somewhere else.** It is applied by the placer, not the tower:
`BuildModeController.cs:2592` holds the ladder `s_towerTierMul = { 1f, 1f, 1.25f, 1.55f }`, applied to
the catalog base at `:2560` / `:2577`. `DefenseTower` never needed to know.

This is the §12 rule paying for itself: the fix that "obviously" followed from the code-read would have
re-authored a balance ladder that was already correct.

## 3. What shipped

- `BuildModeController.cs:2614-2615` — `internal static float TowerStatMultiplier(int tier)` exposes the
  pre-existing private ladder. **The ladder is the single source of truth; nothing re-derives it.**
- `BuildingUpgradeVM.cs:1445-1477` — `AddTowerStatDeltas`, called unconditionally from `BuildPlaced`
  (`:1418`). Reads `entry.repo.range` / `entry.repo.damage` through `Guard.Try`; when either is positive
  it emits the deltas, otherwise it keeps the old generic line.
- The generic-line fallback is **deliberate, not a miss**: granaries, collectors and walls have different
  ladders and correctly log *"no range/damage base — generic bonus line kept."*

Landed in commit **`19f35ad80`** (2026-08-17 10:27:43). No feature flag; unconditional at HEAD.

> ⚠ **Commit-trail hazard, recorded so the next seat does not lose an hour:** `19f35ad80` is *titled*
> WO-1036 (the tutorial watchdog) but carries the WO-1046 code, while `c1e9636f2` — whose message
> narrates WO-1046 at length — contains **none** of it (its eight files are all WO-1045).
> `git log --grep=1046` therefore points at the wrong diff.

## 4. THE MEASURED TABLE (§6 criterion 1)

`tower_ground_archer` authors `range: 14`, `damage: 6`
(`Assets/Resources/Data/Canonical/structures-catalog.json:48-49`). Against the ladder:

| level | multiplier | range | damage |
|---|---|---|---|
| L1 | 1.00 | 14.0 | 6.0 |
| L2 | 1.25 | 17.5 | 7.5 |
| L3 | 1.55 | 21.7 | 9.3 |

**Fire rate is deliberately unscaled** — only range and damage ride the ladder.

## 5. THE PROVING LINE

`Builds/data-regression.log:55052` (2026-08-17 15:37, after the commit):

```
[Flow:Upgrade] upgrade card 'tower_ground_archer' L1->L2: range 14.0->17.5, damage 6.0->7.5
               (mul 1.00->1.25, the placer's own ladder).
```

Originates from a real `BuildingUpgradeVM` construction inside `PlacedUpgradePageTruthRegression:192`.
Still present in the newest runs — `Builds/dr-night4.log` (2026-08-18 21:38), `dr-night3.log`,
`dr-night2.log`, `dr-prod003-shield.log`, `dr-axis.log`. **No later capture shows a regression.**

## 6. WHAT IS NOT PROVEN — stated rather than hidden

1. **Only L1→L2 is captured.** Every proving line is L1→L2; the owner filed this from an **L2→L3** card.
   The arithmetic for L3 is above, but it has not been observed.
2. **No UI capture.** `Builds/UICaps/building_upgrade.png` is dated 2026-07-30 — three weeks BEFORE the
   change — so `UI_CAPTURE_OK` has never run against this panel. A green marker elsewhere proves nothing
   about how this card reads.
3. **No assertion guards the card copy.** Grepping `Assets/Editor` for `AddTowerStatDeltas` /
   `NextBonuses` returns nothing. The only live related assertion is pre-existing
   (`BuildEconomyRegression.cs:331-340`, reflects `s_towerTierMul` and fails if it is missing or not
   strictly increasing) — it pins the LADDER, not the CARD. A regression to the vague line would be
   caught by nobody.
4. **The `UPGRAD...` truncation (§6 item 4) is unaddressed here.** It is the same defect still listed
   open in `WORK_ORDER_1037_*.md:187,203` and belongs there, not on this ticket.

## 7. THE ONE-LINE GAP WORTH CLOSING

An assertion that `BuildingUpgradeVM.NextBonuses` for `tower_ground_archer` **contains** `"Range "` and
`"Damage "` and **does not contain** `"Stronger "`. It can fail, which is the whole point — today the
behaviour is only witnessed by an unasserted log line.

## 8. STATUS

Implemented, gate-green, runtime-proven for L1→L2. **The PO closes, not the CLI** (§13) — this is handed
back for felt-verify of the L2→L3 card.
