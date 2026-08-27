# WORK ORDER 1241 - The catalog must stop OFFERING gear the seam will refuse

**Status:** READY TO IMPLEMENT
**Silo:** Catalog / data validation
**Severity:** P2. Nothing breaks, but the player is shown gear they can never use.
**Origin:** Owner ruling 2026-08-26, following the `blink_armor_dragonic` finding.

---

## THE DEFECT

`blink_armor_dragonic` is authored **`job: "any"`, `weight: "heavy"`, `req.level 1`**.

`ClassWeight("mage") == "light"`, so `ArmorFitsClass` is false and `CanEquipArmorNow` **correctly
REFUSES it for a Mage**. The equip seam is doing its job.

**But `job: "any"` means every job-based loot and shop filter still OFFERS it to that Mage.** The
player is shown, and can acquire, armour the game will always refuse to let them wear.

Owner verbatim: *"The equip seam is correctly protecting you. The catalog should stop offering
illegal gear in the first place."*

## THE RULING

1. **Change `blink_armor_dragonic` from `job: "any"` to its explicit eligible classes.**
2. **Add a VALIDATION RULE: wearable armour may not use `job: "any"` unless specifically
   whitelisted.**

The whitelist exists because a genuinely universal item may be legitimate - but it must be a
DELIBERATE, named exception, not a default.

## Required

- Sweep **every** wearable-armour row for `job: "any"` and report each one with a verdict: legitimate
  universal (whitelist it, with the reason) or mis-authored (give it explicit classes).
- The validation rule runs as a **regression over the catalog**, so a future `job: "any"` armour row
  fails the gate rather than reaching a player.
- ⚠ **Weight-vs-class is the real eligibility rule** (`ClassWeight` / `ArmorFitsClass`). If an
  armour's `job` and its `weight` disagree about who can wear it, say so - a row can be legal by job
  and illegal by weight, which is precisely how this one slipped through.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs.
2. A regression that FAILS on today's tree naming `blink_armor_dragonic`. **Prove it RED first**
   (WO-1138) and state how.
3. The RESULT lists every `job: "any"` armour row and its disposition.

## What NOT to touch

- `blink_armor_dragonic`'s authored **weight** (`heavy`). The owner has not ruled on it and the seam
  handles it correctly.
- The equip seam itself. It is right; the catalog is what is wrong.
- Any item's stats or `req.level`.
