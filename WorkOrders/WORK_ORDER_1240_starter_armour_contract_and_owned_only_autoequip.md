# WORK ORDER 1240 - The starter-armour contract: auto-equip may only choose what the player OWNS

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated).
**Silo:** Gear / catalog / progression
**Severity:** P1. The owner called it *"both a progression bug and an economy hole."*
**Origin:** Owner ruling 2026-08-26, from the residual hole the WO-1214 lane surfaced.

---

## THE DEFECT

`GearLoadout.cs:342` - `EquippedArmor = GearCatalog.BestArmor(job, level)`.

Unlike the main hand, **armour is not ownership-gated.** On every `Refresh` the hero auto-wears the
best armour their class qualifies for - **including armour they do not own**: dropped-but-unbanked
gear and, worse, **shop/catalog entries they have never bought.**

The in-file comment names why nobody closed it: **there is no authored starter armour row in
`StarterLoadout`**, so ownership-gating resolves to null on a fresh save and drops the hero to
`ArmorDefense 0`. That is why this has survived as a seam patch instead of a fix.

## THE OWNER RULING - fix it structurally, not with another seam patch

> Never auto-equip unowned catalog/shop armor. Auto-equip may inspect owned inventory only.
> Add authored starter armor to every hero/class.

### 1. A STARTER EQUIPMENT CONTRACT
**Every hero begins OWNING one authored starter armour item.** The owner named the shape:

| Class | Starter armour |
|---|---|
| Knight | basic mail / plate |
| Ranger | basic leather |
| Black Mage | basic robes |
| Cleric | basic vestments |

⚠ The exact ids, stats and art are authoring work - propose them, do not invent balance. What is
RULED is that **the row must exist for every class**, so the ownership gate has something to resolve to.

### 2. ONE LAW FOR AUTO-EQUIP
> **Auto-equip can choose only from items the player OWNS.**

No shop preview, catalog entry, locked gear, or unowned item may EVER participate. State in the
RESULT which collection you treat as ownership and why it is the authority.

### 3. This closes the ArmorDefense 0 problem by construction
With a starter row owned from character creation, gating auto-equip to owned items can no longer
strand a fresh hero at zero. **Do not ship the gate without the starter rows** - that is the exact
trap the previous seat correctly refused to walk into.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. Regression proving: a NEW save of EVERY class owns and wears its starter armour with
   `ArmorDefense > 0`; auto-equip NEVER selects an unowned item (drive it with an unowned better
   item present and assert it is not worn); and a legitimately owned upgrade IS still auto-worn.
   **Prove each RED first (WO-1138)** and state how.
   ⚠ The good-path case is not optional - a gate that refuses everything would otherwise pass.
3. The RESULT states the ownership authority and lists the four starter rows.

## What NOT to touch

- The main-hand path. It is already ownership-gated and correct.
- Any authored stat, weight or `req.level` on existing gear.
- `StarterLoadout`'s other contents beyond adding the armour rows.
