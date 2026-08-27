# WORK ORDER 1235 - Three mana potions + a crafting scroll: the tutorial entrance to crafting

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
**Silo:** Onboarding / Crafting / Economy
**Type:** NEW FEATURE (per CLAUDE.md §13 this is a spec, not an RCA).
**Origin:** Owner, 2026-08-26: ***"can we start with 3 mana potions and let the users get a crafting
scroll like the defensive plans"*** -> ***"kind of the tuorial enterance to crafting"***.

---

## The loop this is buying

**have -> use -> run out -> find the recipe -> craft more.** The potions are not a gift, they are the
SETUP: the player must feel the resource run out before a recipe means anything. That is the whole
design and every implementation decision should serve it.

## What ALREADY EXISTS — extend it, do NOT greenfield

| Piece | Where | State |
|---|---|---|
| Founding-consumable kit | `StartingBudget.FoundingHealPotions = 3` (`Core/State/NestedTypes.cs:116`), granted at `GameStateService.cs:1103` via `HudCommands.HpPotionId` | **works today** |
| The mana potion itself | `consumables.json` -> `cons_mana_draught` (kind potion, effect mana, magnitudePct 30, duration 10, useCooldown 10, usableInFight true, price 12) | **authored** |
| The recipe | `consumable-recipes.json` -> `craft-survival-mana-potion`: `ManaCrystalShard` x2 + `ArcaneDust` x1 -> `cons_mana_draught` | **authored** |
| Ingredient supply | both drop from **four** `loot-tables.json` tables (5, 8, 16, 18) | **obtainable** |
| The drop-and-collect pattern | `CastleDefensePlansService` (WO-1013) | **proven, copy it** |

## ⛔ THE ONE THING THAT DOES NOT EXIST — and it is the real work

**There is NO recipe-unlock concept in the codebase.** A sweep for `KnownRecipes` / `UnlockRecipe` /
`recipeUnlock` / `IsRecipeKnown` returns **nothing**. Recipes today are simply all present.

So the scroll needs a genuinely new piece of state: *which recipes does this player know?* Design it
deliberately, because everything later hangs off it:
- It is **save state** -> it needs a schema bump. Read the current version off
  `SaveSchema.CurrentVersion` (`Core/State/SaveSchema.cs:41`), **never off a doc**, and write the
  migration so an existing save does not lose access to what it could already craft.
- ⚠ **Existing players must not be retro-locked.** If recipes are open today, a naive "known set"
  starting empty silently REMOVES crafting from every live player. Migrate them to knowing whatever
  they can craft now, and say in the RESULT exactly how.
- Prefer the smallest thing that works: a set of unlocked recipe ids on the save, defaulting to the
  current behaviour for pre-migration saves.

## Part A — the founding kit (small, mirrors an existing constant)

Add `StartingBudget.FoundingManaPotions = 3` beside `FoundingHealPotions`, granted through the SAME
dictionary at `GameStateService.cs:1094-1103`. ⛔ Do not invent a second grant path.
⚠ `HudCommands.HpPotionId` is the health id — find or add the mana equivalent; do not reuse it.

## Part B — the scroll drop (copy the proven pattern)

Model on `CastleDefensePlansService`: self-bootstrapping (`RuntimeInitializeOnLoadMethod`, no scene
authoring, no `VillageSceneBuilder` re-save), a cheap ~1 s scan, and **every acceptance shape falling
out of ONE pure rule** decided from persisted state — spawn IFF <gate> AND not collected AND no prop
already standing. Deterministically re-spawned on every scene entry until collected; nothing about
the prop itself is saved. `ShouldSpawnDrop` is pure so the regression can pin the truth table.

⚠ **Read that file's header before writing a line** — including its own warning that the required
count is a CONSTANT and must never be restated in prose, "that is how this header went stale once
already".

## ⭐ OWNER RULINGS NEEDED — flag these, do NOT decide them

1. **What gates the scroll?** The defense plans use `WavesCompleted >= RequiredWavesSurvived`. But
   this is a CRAFTING tutorial, and the loop above says the teaching moment is **running out**.
   Gating on "has consumed N mana potions" or "mana potions == 0" would land the scroll exactly when
   the player feels the need. **Recommend the consumption gate; the owner decides.**
2. **Does the scroll unlock ONE recipe or open crafting generally?** "The tutorial entrance to
   crafting" suggests it teaches the SYSTEM, not just one draught.
3. **Where is it crafted?** Confirm the surface (a bench, a building, the Bag) exists and is reachable
   at that point in the FTUE. A recipe the player cannot act on teaches nothing.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. A regression pinning: a NEW save starts with exactly 3 mana draughts AND 3 heal draughts; the
   scroll's spawn rule as a pure truth table; and **an existing pre-migration save keeps every recipe
   it could already craft.** Prove each RED first (WO-1138).
3. ⭐ **A device or editor screenshot** of the scroll prop in world and of the crafting surface after
   the unlock. `UI_CAPTURE_OK` alone is not acceptance.
4. ASCII-only TMP strings; no meaning by colour alone (the owner is red/green colourblind).
5. Owner felt-verifies the whole loop — start, use, run out, collect, craft — and CLOSES.

## What NOT to touch

- ⛔ `FoundingHealPotions = 3`, `StrategicGold = 200`, or any other founding constant. Owner-ruled today.
- ⛔ `cons_mana_draught`'s balance (magnitudePct 30 / duration 10 / cooldown 10) or the recipe's
  ingredient counts. If the tutorial needs different numbers, that is an owner ruling — raise it.
- ⛔ `CastleDefensePlansService` itself. Copy the pattern; do not fold a second drop into it.
- ⛔ The loot tables. The ingredients already drop from four of them.

---

## OWNER RULING 2026-08-26 - THE RECIPE MOVES TO MATCH THE ART

The owner supplied the scroll art (`Assets/Resources/ItemIcons/scroll_mana_potion.jpg`, installed).
It depicts a THREE-ingredient recipe: **Moonleaf Herb + Crystal Dust + Pure Water**. The authored
`craft-survival-mana-potion` is a different, two-ingredient recipe: `ManaCrystalShard` x2 +
`ArcaneDust` x1. For a TEACHING item that mismatch would actively mis-teach.

**RULED: re-author the recipe to match the art.**

```
craft-survival-mana-potion  ->  cons_mana_draught
    ing_moonbloom      (Moonbloom Herb)   x1
    ArcaneDust         (Arcane Dust)      x1
    ing_spring_water   (Spring Water)     x1
```

**Why this is the better FTUE anyway, independent of the art:** all three already exist in
`materials.json` and drop from the live loot tables, and the owner's own treasure chest on 2026-08-26
contained **Moonbloom Herb** and **Spring Water** - so a new player has two of the three in hand
before they ever read the scroll. The old recipe wanted two Mana Crystal Shards, which is a
deeper-progression material and a worse first lesson.

⚠ **The names still differ by a word** (Moonleaf/Moonbloom, Pure/Spring Water, Crystal Dust/Arcane
Dust). The owner accepted the art as a near-enough likeness rather than renaming live materials -
**renaming was explicitly considered and NOT chosen**, because those display names appear across
loot, chests, the treasure panel and every other recipe. ⛔ **Do NOT rename the materials or their
ids.** If the crafting UI shows ingredient names, it shows the CATALOG names; the scroll is the
flavour rendering of the same recipe.

⛔ Do NOT change the ingredient COUNTS beyond the 1/1/1 above without a further ruling, and do not
touch `cons_mana_draught`'s own balance.

---

## OWNER RULING 2026-08-26 (FINAL) - all three open questions answered

### 1. TRIGGER: consumption-based, and gated on reachability
The scroll becomes discoverable when **Mana Potions fall to 0 or 1 for the FIRST time**.
Owner verbatim: *"That creates the exact 'I need this -> I discover the solution' teaching moment."*

### 2. SCOPE: opens the door, hands over one key
The scroll **unlocks Crafting as a VISIBLE SYSTEM**, but grants **only the Mana Potion recipe**.
Owner verbatim: *"Introduces the mechanic without vomiting the whole crafting catalog onto a new
player."* Do NOT unlock the recipe book.

### 3. LOCATION: no station, no scroll
**Guarantee the crafting station/surface exists and is reachable BEFORE the scroll can trigger.**
Owner verbatim: *"Never teach a verb the player cannot immediately perform."*
This is a HARD PRECONDITION on the drop, not a follow-up polish item. No station means the scroll
does not drop yet.

### The shipped sequence, in the owner's own words
> start with a few Mana Potions -> use them naturally -> reach 0/1 -> recipe scroll becomes
> discoverable -> player obtains it -> Crafting becomes visible -> Mana Potion recipe unlocks ->
> UI directs them to the ALREADY-ACCESSIBLE crafting station -> first potion is crafted.

The player learns crafting **at the exact moment crafting solves a problem they personally
experienced**. Every implementation choice serves that ordering.