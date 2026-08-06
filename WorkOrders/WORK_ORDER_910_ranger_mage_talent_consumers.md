# WORK ORDER 910 - Ranger + Mage talent trees have no consumers (owner design ruling needed)

**Status: READY FOR OWNER RULING** (this is a DESIGN call, not an implementation ticket)
**Date:** 2026-08-05
**Silo:** Combat/AI + data (`hero-talents.json`, `TalentConsumerRegistry`) - no scene work
**Raised by:** CLI regression lane, from `TalentStrategyRegression` G3 going red after the
Mage/Ranger unlock removed the audit blind spot.

---

## READ THIS FIRST

**Ranger collapses to ONE usable talent out of 20. Mage collapses to five out of 20.
Both lose their entire tier-4 capstone row.**

Mage and Ranger were unlocked as playable on 2026-08-05. Their talent trees were never
wired: 31 of their 40 nodes are player-reachable talents whose effect has **no implemented
consumer anywhere in the codebase**. A player can spend Wisdom on them and receive nothing.

This is the direct answer to "does picking Ranger work end to end?" - **the class is
selectable and playable, but it has no talent progression.** Same for Mage. Knight is
unaffected.

### Tree shape if the 31 dead nodes were simply hidden

| Tree | tier1 | tier2 | tier3 | tier4 | visible | actually REACHABLE |
|---|---|---|---|---|---|---|
| Ranger | 1/5 | 1/5 | 1/5 | **0/5 STRANDED** | 3/20 | **1** |
| Mage | 4/5 | 2/5 | **0/5 STRANDED** | **0/5 STRANDED** | 6/20 | **5** |

Three whole tiers are stranded. Neither class would have a single capstone to build toward
(capstone = any tier-4 node; `HeroSkillTreeVM.BuildNode`, `!isShared && tier >= 4`).

### Orphaned nodes (visible, but permanently unreachable)

Hiding a node does not rewrite the prerequisite graph, so these three survivors would read
"Requires <hidden node>" forever:

| Orphaned node | Its only prerequisite | Which is |
|---|---|---|
| `ranger.t2n2` Venomcraft | `ranger.t1n2` Hunter's Mark | hidden |
| `ranger.t3n1` Bloodbound Draw | `ranger.t2n1` Windstrider Boots | hidden |
| `mage.t2n5` Blink Mastery | `mage.t1n5` Rune Binding | hidden |

Note `ranger.t3n1` is orphaned *twice over*: its prerequisite is hidden, and its own
downstream `ranger.t4n1` is hidden too.

---

## Scope

- **Ranger: 17 of 20 nodes dead. Mage: 14 of 20 dead. Total 31.**
- **Knight (32 nodes) and the shared pool (9 audited) are FULLY GREEN.** This is isolated
  to the two classes unlocked on 2026-08-05 - it is not a systemic talent-system failure.

## How this stayed invisible

`TalentStrategyRegression.HiddenTrees` held `{ "ranger", "mage" }` from the `ff.knightonly`
era. Its own update rule said to clear it when a class unlocks; that did not happen in the
unlock commit. G3 - the "no dead talent nodes" gate - therefore skipped **both entire trees**
for as long as players could reach them, auditing 41 nodes and reporting green while 40
shipped nodes were never checked at all. `HiddenTrees` is now **empty and must stay empty**.

---

## The 31 nodes

### Class A - unregistered effect key: no consumer exists anywhere (16)

| id | name | tier | effect key | note in data |
|---|---|---|---|---|
| `ranger.t1n1` | Quick Draw | tier1 | `attackSpeed` | (none) |
| `ranger.t2n1` | Windstrider Boots | tier2 | `moveSpeed` | (none) |
| `ranger.t2n3` | Eagle Vision | tier2 | `critChance` | +25% range (range - V2) |
| `ranger.t2n4` | Deep Freeze | tier2 | `modifyAbility:slow` | buffs the ranger_arrow_frost ammoEffect rider (base -35% / 2.5s); NO rider consumer for a slow yet |
| `ranger.t2n5` | Shadow Veil | tier2 | `stealth` | (V2) |
| `ranger.t3n2` | Emberhead | tier3 | `modifyAbility:burn` | buffs the ranger_arrow_fire ammoEffect rider (base 6 dps / 4s) |
| `ranger.t3n3` | Leafcloak | tier3 | `dodge` | (onEvent - V2) |
| `ranger.t3n4` | Beast Companion | tier3 | `summon` | (summon - V2) |
| `ranger.t4n2` | Windstrider Legend | tier4 | `moveSpeed` | +30% dodge (V2) |
| `ranger.t4n3` | Phantom Hunter | tier4 | `stealth` | (proc - V2) |
| `ranger.t4n4` | Nature's Fury | tier4 | `onEvent` | (dot - V2) |
| `mage.t2n1` | Aether Surge | tier2 | `onEvent` | onKill mana (V2) |
| `mage.t2n3` | Arcane Shield | tier2 | `shieldStrength` | no shieldStrength consumer yet (V2) |
| `mage.t3n3` | Aether Form | tier3 | `manaCostReduction` | (none) |
| `mage.t3n4` | Runic Overload | tier3 | `onEvent` | temp buff (V2) |
| `mage.t4n4` | Reality Rift | tier4 | `onEvent` | dot zone (V2) |

Distinct unwired effect types: `attackSpeed`, `moveSpeed`, `critChance`, `stealth`,
`summon`, `dodge`, `onEvent`, `shieldStrength`, `manaCostReduction`,
`modifyAbility:slow`, `modifyAbility:burn`.

### Class B - registered key, but the note declares a stub the consumer does not deliver (15)

| id | name | tier | effect key | note in data |
|---|---|---|---|---|
| `ranger.t1n2` | Hunter's Mark | tier1 | `unlockAbility` | (NEW ability - stub) |
| `ranger.t1n3` | Tumble Step | tier1 | `unlockAbility` | (NEW ability - stub) |
| `ranger.t1n5` | Arrow Storm Prep | tier1 | `unlockAbility` | (NEW ability - stub) |
| `ranger.t3n5` | Precision Strike | tier3 | `unlockAbility` | (NEW ability - stub) |
| `ranger.t4n1` | Storm of Arrows | tier4 | `unlockAbility` | (NEW ability - stub) |
| `ranger.t4n5` | Elarion's Arrow | tier4 | `modifyAbility:` | pierce/chain (V2) |
| `mage.t1n5` | Rune Binding | tier1 | `modifyAbility:` | chain (V2) |
| `mage.t2n4` | Flame Mastery | tier2 | `modifyAbility:` | (V2) |
| `mage.t3n1` | Cataclysm Prep | tier3 | `modifyAbility:` | (V2) |
| `mage.t3n2` | Spell Echo | tier3 | `proc` | duplicate (V2) |
| `mage.t3n5` | Void Rift | tier3 | `unlockAbility` | (NEW ability - stub) |
| `mage.t4n1` | Cataclysm | tier4 | `unlockAbility` | (NEW ability - stub) |
| `mage.t4n2` | Aetherweaver Ascension | tier4 | `damageBonus` | (V2) |
| `mage.t4n3` | Eternal Arcana | tier4 | `damageBonus` | +40% mana regen (V2) |
| `mage.t4n5` | Elarion's Legacy | tier4 | `proc` | duplicate (V2) |

The `unlockAbility` rows are the sharpest: the node advertises a NEW named ability
(`ranger.hunters-mark` etc.) and the equip flow will happily route `abilityId` to a
quick-slot, but the ability itself was never built.

The `modifyAbility:` rows (empty `stat`) resolve to registry key `modifyability:`, which IS
registered - but only for Holy Retribution's taunt-burn rider
(`HeroTalentModifiers.TryGetAbilityDotRider(stat: null)`). These five ranger/mage nodes match
that key by accident of an unset `stat`, not because anything reads them.

---

## Recommendation (for the owner to accept, amend or reject)

Wiring 31 consumers is 31 pieces of combat design and is explicitly NOT something the CLI
lane will invent. What the CLI can say from the data is **which tiers need a spine so the
trees stay traversable**, because a tree with a stranded tier cannot be climbed at all:

- **Ranger needs a spine through t2, t3 and t4.** Only `ranger.t1n4` is currently reachable;
  every ranger path dies at tier 2. Minimum viable: wire one node per tier in a single
  connected column (e.g. `t1n1 attackSpeed` -> `t2n1 moveSpeed` -> `t3n1` (already green) ->
  `t4n1` Storm of Arrows), which also un-orphans `ranger.t3n1`.
- **Mage needs a spine through t3 and t4.** Tier 1 and 2 are partly alive; tiers 3 and 4 are
  completely empty. Minimum viable: one connected column from `t2n2` (already green) upward.
- **Cheapest real wins first:** `attackSpeed`, `moveSpeed` and `critChance` are plain stat
  multipliers of exactly the shape `HeroTalentModifiers` already implements for
  `damageBonus` / `maxHpPct` - three consumers there would revive 5 nodes (`ranger.t1n1`,
  `ranger.t2n1`, `ranger.t2n3`, `ranger.t4n2`, plus the shared `critChance` stub). That is
  the highest nodes-per-unit-of-design-work in the list and needs no new combat concepts.
- **Most expensive:** `summon` (Beast Companion) and the five `unlockAbility` stubs - each is
  a whole new ability with VFX, animation and balance. These are real features, not fixes,
  and should become their own work orders rather than riding this one.

---

## HIDING WAS CONSIDERED AND REJECTED - do not "fix" this by hiding

This section exists so the reasoning survives. The owner's wire-or-hide law allows exactly
two moves per dead node: wire a consumer, or set `"hidden": true`. Hiding was evaluated in
full and rejected for these 31, for two independent reasons:

1. **`hidden` had no runtime reader.** `HeroTalentNodeDef.Hidden`
   (`Assets/_Modules/Village/Talents/HeroTalentCatalog.cs`) shipped on 2026-07-11 with a
   comment claiming "the View skips it". Nothing read it. `HeroSkillTreeVM.Rebuild`
   enumerated every node unconditionally. Setting `"hidden": true` on these 31 would have
   turned the gate green while leaving all 31 nodes **fully clickable in the player's tree**
   - suppression, just spelled in JSON instead of a debt list.
   *This half is now fixed:* the reader was added to `HeroSkillTreeVM.Rebuild` (hero tree +
   shared pool) and the lying comment corrected, so hiding genuinely works from today. The
   field was wired rather than deleted precisely because the law needs its second option to
   exist. **But whether to USE it on these 31 is the owner's ruling, not the gate's.**
2. **Hiding all 31 strands three whole tiers and orphans three more nodes** - see the table
   at the top. Ranger would drop to ONE reachable talent of 20; Mage to five; neither would
   have a capstone. Shipping an unreachable tree is a worse bug than the one being fixed.

If a future pass wants to hide a subset, **check downstream reachability first**: hiding a
node does not re-point the prerequisites of its children, so every child whose only
prerequisite is hidden becomes permanently unreachable.

**Also forbidden:** re-adding `"ranger"` / `"mage"` to `TalentStrategyRegression.HiddenTrees`.
That set is the bug that hid this for weeks. It must stay empty.

---

## What was already done (no owner ruling needed for these)

1. **`TalentStrategyRegression.KnownDeadNodeBaseline`** - the 31 ids recorded ONCE as dated
   tracked debt under this WO, so G3 keeps auditing them and reports them as debt in the
   gate log rather than blocking every other lane. The set is **shrink-only**:
   - a dead node NOT in the baseline still FAILS (new debt cannot be added);
   - a baseline id that no longer reports dead (wired, hidden, renamed or deleted) also
     FAILS, naming the line to delete - so the baseline can never outlive its debt.
   There is no way to green this gate by editing the set; only by wiring a consumer (or the
   owner ruling "hide it") and then pruning the id.
2. **`HeroTalentNodeDef.Hidden` wired** into `HeroSkillTreeVM.Rebuild` (both the hero tree
   and the shared pool loops) + the false comment corrected. No node currently sets
   `hidden`, so this is a no-op on live behaviour today.

## Files this WO will touch when it is implemented

- `Assets/Resources/Data/Canonical/hero-talents.json` **and**
  `Assets/StreamingAssets/Data/Canonical/hero-talents.json` (byte-identical dual copies -
  update both, G1 enforces byte-equality)
- `Assets/_Modules/Village/Talents/HeroTalentModifiers.cs` (new stat accessors)
- the consuming systems per effect type (`HeroAbilities`, `HeroLocomotion`,
  `PlayerAttackController`, ...)
- `Assets/Editor/Regression/TalentStrategyRegression.cs` - register each new consumer with a
  file+member citation in `TalentConsumerRegistry`, and delete the matching id from
  `KnownDeadNodeBaseline` **in the same commit**.

## What NOT to touch

- `TalentStrategyRegression.HiddenTrees` - must stay empty, forever.
- `KnownDeadNodeBaseline` - may only shrink, and only alongside a real wire/hide.
- Knight and shared nodes - fully green, out of scope.
- `Assets/_Modules/Village/Talents/TalentTreePanel.cs` - legacy UI Toolkit panel with no
  live callers; it looks nodes up by the v1 `column` field, which v2 nodes do not set, so it
  cannot surface these nodes. The live panel is `HeroSkillTreePanelMvvm` + `HeroSkillTreeVM`.

## Acceptance criteria (for the eventual implementation pass)

1. Every node wired has a `TalentConsumerRegistry` entry citing the consuming file + member,
   added in the SAME commit as the consumer.
2. Its `effect.note` no longer contains `V2` / `V-later` / `stub` (G3's belt check).
3. Its id is removed from `KnownDeadNodeBaseline` in the same commit.
4. Both `hero-talents.json` copies stay byte-identical (G1).
5. Every tier of every playable tree has at least one REACHABLE node, verified by walking
   prerequisites from the tier-1 roots - not merely one visible node.
6. `DataRegression.RunAll` emits `REGRESSION_OK <n>/<n> suites`.
