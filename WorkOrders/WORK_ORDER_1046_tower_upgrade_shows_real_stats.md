# WORK ORDER 1046 — "Stronger Archer Tower at Level 3" may be an UNBACKED claim, not just vague copy

**Status:** IMPLEMENTED 2026-08-17 (`19f35ad80`), gate-green + runtime-proven L1->L2 — AWAITING OWNER FELT-VERIFY of the L2->L3 card. See the RESULT file.
**Minted:** 2026-08-17 (UI seat) — provenance stack bumped 1046 → 1047 in the same edit
**Lane:** Tower progression data + upgrade panel copy. ⚠ May be a **balance** defect, not a UI one.
**Provenance:** owner 2026-08-17: *"all it says is stronger tower, can we check what changes, attack
range, splash damage, attack power, better targetting? Dual targetting?"*, with the Archer Tower
Enhancements screenshot (L2 → L3, cost **225 Wood + 100 Iron**).

---

## 1. The surface ask — the panel hides information it could show

The perk line reads *"Stronger Archer Tower at Level 3."* It names no number, so the player cannot
judge whether 225 wood and 100 iron is worth it. That is a real UI gap on its own.

## 2. ★ THE FINDING — the archer tower appears to have NO per-level combat stats at all

Chasing the owner's question turned up something bigger than copy. Measured 2026-08-17:

**The Arcane Spire HAS an authored stat ladder** (`towers.json` → `levels`):

| level | name | range | damage | cooldown |
|---|---|---|---|---|
| 1 | Arcane Spire | 14 | 12 | 1.1s |
| 2 | Runed Spire | 17 | 22 | 0.9s |
| 3 | Warded Spire | 21 | 40 | 0.7s |

That is exactly what the owner is asking to see — range +24%, damage +82%, fire rate +29% per step, and
even a **name change per level**. Rich, specific, motivating.

**The Archer Tower has none of it.** `structures-catalog.json → tower_ground_archer` carries `repo`
with `maxLevel: 3`, an `upgradeCost[]` array, `behaviorId: DefenseTower`, `canHitAir: false` — and **no
range / damage / cooldown ladder**. Searching `DefenseTower.cs` for level-driven scaling finds only:

- `upgradeVisualPath` → `Tower_Wooden_Watchtower` / `_L2` / `_L3` (the **model**)
- `ArcherTowerLevel1_Projectile` / `Level2_Projectile` (the **projectile VFX**)

The only `LevelMultiplier` in the codebase is `StorageCapsCatalog`'s — a storage system, unrelated.

⚠ **So the evidence points to the archer tower's upgrade changing the MODEL and the PROJECTILE ART, and
possibly nothing else.** If that holds, *"Stronger Archer Tower at Level 3"* is not vague — it is
**false**, and the player is paying 225 wood + 100 iron for a reskin.

## 3. ⛔ STEP 1 — CONFIRM IT. Do not fix copy or add stats until measured (§12)

**This is a strong hypothesis from static reading, which §12 says never concludes.** There could be a
generic structure-level HP path, or damage applied somewhere I did not trace.

**Measure:** instrument a tower's **effective** range / damage / cooldown / HP at L1, L2 and L3 and log
all three. One headless run answers it.

- **Stats DO change** → this is a **UI ticket**: surface the real deltas (§4)
- **Stats do NOT change** → this is a **BALANCE ticket**: the upgrade is hollow and the copy is a false
  claim. ⚠ **Escalate to the owner before authoring numbers** — tower balance is a design decision, not
  a fill-in-the-blank

**Record the measured table in the RESULT either way.** It is the fact this whole question rests on.

## 4. The fix, if stats exist (or once authored)

**Show the delta, not an adjective.** The Arcane Spire's ladder is the model to follow — per level:
range, damage, fire rate, and a **name** (*Arcane → Runed → Warded Spire*). A named tier is a cheap,
strong motivator the archer line already lacks.

- List **what changes, with before → after numbers**
- ⚠ Keep it honest: only show what actually changes. A padded list is worse than a short true one
- ASCII-only; legible in greyscale (colourblind law)
- ⚠ Fix the truncated **`UPGRAD...`** label while in here — same defect as WO-1037 §4, still present in
  this screenshot

## 5. The owner's feature list — separate, and a genuine design opportunity

> *"attack range, splash damage, attack power, better targetting? Dual targetting?"*

**Range / power / fire rate** are the axes the Arcane Spire already uses — extending them to the archer
line is authoring, not engineering.

**Splash, better targeting and dual targeting are NEW MECHANICS**, not stat rows:

- ⚠ They need targeting/AI work in `DefenseTower`, not a data edit
- ★ They are also the **WC3 answer to a real gap in the design review**: `docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md`
  lists **"Counters — composition matters; there is no single right army"** as a WC3 pillar we do not
  have. A tower that hits one target hard vs one that splashes weakly **is** a counter axis, and it
  makes tower choice and placement matter — which is what makes the CoC base-layout pillar meaningful
  (WO-1026)
- ⚠ **Do not build them in this ticket.** File separately once §3's measurement says whether the
  existing ladder even works. **`tower-perks.json` already has a `tiers` structure** — the WC3-style
  perk tree (memory `building-upgrades-warcraft3-style`) — and that is the natural home for
  branching capabilities like splash-vs-single-target. **Check what it already holds before designing
  anything new.**

## 6. Acceptance criteria

- [ ] The measured L1/L2/L3 stat table for the archer tower is **in the RESULT** (§3)
- [ ] If stats change: the panel shows **before → after per axis**, no bare adjectives
- [ ] If they do not: escalated to the owner, and the **false "Stronger" claim removed or made true** —
      ⛔ do not ship copy that promises a strengthening that does not happen
- [ ] `UPGRAD...` no longer truncates
- [ ] Greyscale-legible, ASCII-only
- [ ] No balance numbers invented without an owner ruling (§3)

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. Headless: log effective tower stats at each level — **the oracle for this whole ticket**
3. `UI_CAPTURE_OK` — open the PNGs at L1→L2 and L2→L3
4. Owner felt-verifies: *"can I tell what I'm buying?"* + closes (§13)
