# WORK ORDER 1041 — Dungeon-exclusive gem drops: the stone loop is ALREADY BUILT and has no source

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE 2026-08-16 (`eff761fcc`, shipped with WO-1042) — RESULT filed; pending PO felt-verify
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1041 → 1042 in the same edit
**Lane:** Dungeon reward economy. Small. ⚠ Was scoped pillar-sized until §2 was measured.
**Provenance:** owner 2026-08-16: *"a stone or a weapon or a ring"* · *"rings exist"* · *"**but magic
stones**"* · *"**something we can do to justify a dungeon**"* · *"**thats what the jeweler is all
about**"* · *"only way to get a stone is no deaths?"* · *"**or weighted so anyone can but better
ranking the better the odds**"*.
**Related:** **WO-1028** (creeping loop — this is its §3 payout) · **WO-1040 §3b** (graded runs supply
the odds).

---

## 1. The design principle

*"Something we can do to justify a dungeon."* In WC3, creeping is worth the risk because creeps drop
what you **cannot get anywhere else**. Today the dungeon pays out its own consumables (WO-1040 §3), so
it justifies nothing outside itself.

## 2. ★ THE ENTIRE LOOP IS ALREADY BUILT. It is missing ONE thing: a source.

The owner's *"thats what the jeweler is all about"* is exactly right, and the tree bears it out.
Measured 2026-08-16:

| piece | state |
|---|---|
| **Gems ("magic stones")** | ✅ **EXIST** — `ing_ember_crystal`, `ing_aether_shard`, `ing_heartstone_crystal` |
| **The Jeweler bench** | ✅ **BUILT** — `PanelId.JewelerCrafting = 10` → `JewelerPanelMvvm`; `BuildingKind.JewelersBench = 9`; **43 files** reference it (WO-553) |
| **Recipes** | ✅ **`jeweler-recipes.json` — 6 recipes**, each `base accessory + gems → higher-tier accessory` |
| **The upgrade chain** | ✅ `ring_iron` → `ring_steadfast` → `ring_embercoil` → `ring_heartward`; amulets likewise (`amulet_travelers` → `amulet_oathward`) |
| **Rings / amulets** | ✅ **SHIPPED** — 5 + 5 in `accessories.json`, live slots, `EquipAccessoryById`, `ArmorVfxMap` slot-aware (WO-543) |
| **A DUNGEON SOURCE for gems** | ❌ **MISSING — this ticket** |

⚠ **I previously scoped this as a pillar-scale new item system. That was wrong.** Item class, consumption
path, upgrade chain, crafting UI and save state are **all shipped**. **Do not build a socketing system,
a stone catalog, a stat pipeline or a new screen.** Everything downstream of the drop exists.

**The complete loop, once gems drop underground:**

> descend → earn gems → Jeweler → upgrade your ring → **stronger in town, waves and raids**

That is exclusive, persistent, cross-pillar, and repeatable without inflation — every property §1 asks
for, and it is **a drop table away.**

## 3. ⛔ THE RULING — weighted odds, not a flawless-run gate

> Owner: *"only way to get a stone is no deaths?"* → *"**or weighted so anyone can but better ranking
> the better the odds**"*

**Take the second. The owner talked herself onto the right answer and it is worth stating why:**

A **no-deaths gate** means the median player *never once* sees the reward that justifies the dungeon.
The pillar would then justify itself only for experts — and the players most in need of a power boost
(the ones dying) are precisely the ones locked out, which inverts the difficulty curve.

**Weighted odds keep everyone in the loop while making mastery pay.** It is also the standard both
reference games use: CoC pays loot on a 1-star and pays *more* on a 3-star; WC3 gives XP for a sloppy
creep and better drops from harder camps.

**Required shape:**

- ⛔ **Every completed run has a non-zero gem chance.** A clean run raises the odds and/or the gem
  tier — it never becomes the only door. (This is also WO-1040 §3b trap 3: a completed run must always
  pay something.)
- **Grade → odds** comes from **WO-1040 §3b**'s run rating (enemies killed / potions used / deaths /
  time). ⚠ **One rubric, owned there.** Do not invent a second rating here.
- **Deeper = better** — the torch/oil/darkness risk system is the player's own difficulty dial; let it
  raise gem tier, so elected risk is rewarded (WC3 creeping in one line).
- ⚠ **Tune against the recipes, not vibes.** `jeweler-recipes.json` states exact gem counts
  (`ing_ember_crystal ×2`, `×2 + ing_aether_shard ×1`, …). **Read those, and size drop rates so a
  reasonable number of runs completes a real upgrade.** A rate that needs 40 runs for one ring reads
  as no reward at all.

## 4. Do NOT

- ⛔ **Do not build a socketing system, a new stone catalog, or a new screen** — §2; all shipped
- Do not touch `accessories.json`, `jeweler-recipes.json` semantics, or the Jeweler panel (WO-543 /
  WO-553 are done)
- Do not invent a second run-rating rubric (§3 — WO-1040 §3b owns it)
- Do not re-open dungeon generation / stairs / navmesh (WO-1028 §4 — closed and expensive;
  `dg_stair_rig` / `dg_descent_probe` are quarantined fixtures)
- ⚠ **Do not let gems become purchasable.** WO-1037 just introduced single-resource impulse packs; if
  gems ever enter that catalog, the dungeon stops being justified and this ticket's thesis is void

## 5. Acceptance criteria

- [ ] Completing a dungeon can drop `ing_ember_crystal` / `ing_aether_shard` / `ing_heartstone_crystal`
- [ ] **Every completed run has a non-zero chance**; grade and depth raise odds and/or tier (§3)
- [ ] Odds derive from **WO-1040 §3b**'s grade — one rubric
- [ ] Drop rates sized against real `jeweler-recipes.json` gem counts, and the math recorded in the RESULT
- [ ] The full loop works end to end: **descend → gem → Jeweler → upgraded ring → measurably stronger
      hero in town**
- [ ] A regression pins **dungeon-exclusivity** — no other grant path can produce these gems ⚠ this
      invariant is the pillar's justification and will erode silently without an oracle
- [ ] Zero changes to the Jeweler, accessories, or recipe semantics

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. Headless: descend → clear → gem granted → craft at the Jeweler → equip → hero stat changed in town.
   ⚠ **Test the whole chain, not the drop** — the drop is the only new link, but the chain is the product
3. Repeat runs at different grades; confirm the odds actually move
4. Owner felt-verifies: *"is this worth going down for?"* + closes (§13)
