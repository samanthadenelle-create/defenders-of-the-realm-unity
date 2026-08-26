# WORK ORDER 1214 - A dropped shield PERMANENTLY DISARMS the Mage. Drops go to inventory; ineligible gear is held and sellable, never equipped.

**Status:** FIXED 2026-08-26 - `COMPILE_GATE_OK` + `REGRESSION_OK 292/292`; post-fix owner device verification queued
**Silo:** Gear / equip logic + inventory
**Severity:** P0 - unrecoverable loss of the player's only weapon, on a LIVE build that takes real money.
**Origin:** Owner felt-test, Seeker build `2026.08.26.341419`, 2026-08-26.

Owner verbatim, in order - (3) and (4) are the RULINGS and they define the fix:
1. *"playing as mage, but i got a drop in a battle and it auto equips"*
2. *"so now im a mage with no staff instead using a shield"*
3. ***"any drop should just go to inventory"***
4. ***"if cannot equip (shield for mage) then dont allow equip but they can sell"***

---

## PROOF (captured, not inferred)

- Device screenshot `tmp/shield-seat-101829.png` - HUD reads `Thrain Lv 3 - Mana`; the hero holds a
  shield at chest height with **no staff in either hand**.
- `Assets/Resources/Data/Canonical/weapons.json`, read at source this session: **96 weapons total.**
  Filtering `job == "mage"` returns **8, and every one is a staff** (`mage_oak`, `mage_arcane`,
  `mage_void`, `aegis_aetherstaff`, `tripo_staff_a/b/c/d`). **There is ZERO one-handed mage weapon
  in the game.**
- Same file: **19 entries carry `job: "any"`** - `tripo_shield_a` plus `blink_shield1h_01..25`.
- `GearCatalog.JobMatches` (`GearCatalog.cs:591-596`): `"any"` returns **true for every class**.
- `GearCatalog.BestOneHandedWeapon` doc (`GearCatalog.cs:~427`), verbatim: *"the main-hand fall-back
  when a 2H weapon is removed (equipping a shield while a 2H is held) - keeps the armed-hero
  invariant: a 1H can coexist with the off-hand, a 2H cannot."*

### The chain, end to end

1. A shield drops. Its `job` is `"any"`, so the class gate **passes** for a Mage.
2. It auto-equips to the off-hand.
3. Hand-slot enforcement fires: a 2H main-hand cannot coexist with an off-hand, so **the staff is
   force-removed**.
4. The fall-back asks `BestOneHandedWeapon("mage", level)` for a replacement main-hand.
5. The catalog has **no** one-handed mage weapon, so it returns **null**.
6. The Mage is left holding a shield and nothing else, **with no in-game way to recover.**

Every step is working as written. The defect is the design, which the owner has now ruled on.

---

## THE RULINGS

### Ruling 1 - a DROP never auto-equips. It goes to inventory.

*"any drop should just go to inventory"*

This alone kills the bug at the root: if the shield is never equipped, the staff is never displaced,
and steps 2-6 above cannot happen to any class.

⛔ **THIS IS SCOPED TO DROPS. Do not disable auto-equip generally.**
`GearLoadout` auto-equips on class/level refresh, and **auto-upgrade-on-level-up is INTENDED and
STAYS (WO-860)**. The WO-860 comment at `GearCatalog.cs:355-370` explains why in detail - it is
guarding the incident where a levelled knight was silently handed the purchasable
`knight_flameblade` for free, *which is why the owner opened her demo recording holding a flaming
sword*. **The candidate set was the bug there, never the auto-equip verb.** Blurring these two is
how this ticket regresses WO-860.

Find every path that grants gear from a **drop / loot roll / battle reward** and route it to the
inventory store instead of the equip seam. Enumerate them in the RESULT - the arena/outpost loot
rolls are named in `GearCatalog.cs:363-366` as distinct callers, so expect more than one.

### Ruling 2 - ineligible gear is HELD and SELLABLE, never equippable.

*"if cannot equip (shield for mage) then dont allow equip but they can sell"*

- The player **keeps** the item. It is visible in inventory. It is **not** deleted, not refused at
  pickup, not silently dropped.
- Its equip action is **disabled, with a WORDS reason** naming why (e.g. "Mages cannot use shields").
  ⛔ Never a greyed-out button with no explanation, and ⛔ **never colour alone** - the owner is
  red/green colourblind. A disabled control that does not say why is the same silent-failure class
  this whole ticket is about.
- It **can be sold**. ⚠ **Verify a sell path actually reaches inventory-held gear before claiming
  this slice is done** - if the vendor only lists equippable items, selling an ineligible drop is
  impossible and the item becomes dead weight. Report what you find; if the sell path does not
  cover it, that is a finding, not something to quietly skip.

### Ruling 3 (implied, and load-bearing) - enforce eligibility AT THE EQUIP SEAM.

Ruling 2 is only real if the seam can refuse. There is history here, recorded in the code:

> `GearCatalog.MeetsReq` was made **public** by *"F8 seq-642 Fix B"* precisely because the equip seam
> (`GearLoadout.EquipWeaponById` / `EquipOffHandById` / `EquipArmorById`) **physically could not ask
> the same question the auto-best queries ask - so a manual equip enforced NEITHER the class gate nor
> the level gate.** The hole was masked only because the shop/equip UI pre-filters its lists; **every
> non-UI caller (arena grants, outpost drops, story grants, AutoPilot) went straight through it.**

So: enforce **class + level** in the equip seam itself, fail closed, and log the refusal
(`FlowTrace.Warn`) as well as showing it. A UI that merely hides the option is not enforcement.

### Ruling 4 - the armed-hero invariant must FAIL CLOSED (defence in depth)

Even with drops going to inventory, keep the disarm from being reachable at all: if removing a 2H
main-hand would leave the hand empty because no eligible 1H exists, **REFUSE the off-hand equip**
and say so in words. Today it degrades to `null` and ships an unarmed hero. The invariant is already
the stated intent of the fall-back - it simply is not enforced.

---

## Open question for the owner (do NOT decide this yourself)

**Should shields stay `job: "any"`?** Nineteen items carry it. Rulings 1-4 make the game safe either
way, so this is a design preference, not a blocker. ⛔ Do not edit the 19 rows without her word -
`job` is read by the shop/equip filters AND the loot rolls, so narrowing it changes what can drop.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ A regression that **FAILS on today's tree**: grant a `job:"any"` shield to a Mage holding a 2H
   staff via the DROP path, then assert (a) the staff is still equipped, (b) the shield is in
   inventory, (c) the off-hand is empty. Prove it RED first - a test that passes before the fix is
   decoration (WO-1138).
3. A case asserting the equip seam REFUSES a class-ineligible item and that the refusal is logged.
4. A case for the Ranger once its 1H status is established **by reading the catalog** - do not assume
   it is safe because its primary is a melee sweep.
5. A case pinning that level-up auto-upgrade (WO-860) still works - proof this ticket did not
   over-reach into Ruling 1's exclusion.
6. Owner felt-verifies on device and CLOSES.

## What NOT to touch

- ⛔ `PickBestWeapon`'s `owns` parameter and the catalog-wide `null` behaviour. That split is
  deliberate and documented (`GearCatalog.cs:355-370`): loot rolls and the armed-hero oracles MUST
  stay catalog-wide. Do not "tidy" it.
- ⛔ Auto-upgrade-on-level-up (WO-860). See Ruling 1.
- ⛔ The 19 `job: "any"` rows, pending the owner's answer above.
- The shield SEATING defect is **WO-1215**, a different lane. No geometry work here.
## LANDED-WORK AUDIT (2026-08-26)

Implementation landed across `b303c4fbf`'s gear/drop files. Fresh evidence:
`Builds/batch0-compile-2.log:1966` `COMPILE_GATE_OK`;
`Builds/batch0-regression-2.log:83799` `DROPS TO INVENTORY OK` proves both loot paths deposit without
equipping, the Mage shield refusal preserves the staff and sellable item, legal paths still equip,
and level-up auto-upgrade remains wired; `:83814` reports `REGRESSION_OK 291/291`.
**Post-FIXED APK checklist:** owner device felt-verification and close.
