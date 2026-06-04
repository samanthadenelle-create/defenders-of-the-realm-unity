# WORK ORDER 180 — Production Building Roster: Lumbermill, Forge, Armourer, Arcane Library

**Status: READY TO IMPLEMENT (phased)**
**Priority:** Medium-High — the crafting/upgrade economy needs its buildings; each function gets a home.
**Date:** 2026-05-31
**Lane:** Architect (building placement, `VillageSceneBuilder` single-writer) + gameplay (upgrade panels).
**Source:** owner — *"need structures for Lumbermill, and upgrades available: Forge→weapons, Armourer→armor,
Arcane Library→upgrading mage skill trees."*
**Builds on:** DEFENSE_DEPTH_ANALYSIS (the Warcraft crafting spine), WO-151 (BuildingUpgrade/VillageLevel),
WO-159 (catalog buildings), TALENT_TREE_V2 (mage skill upgrades).

---

## The roster — each crafting/upgrade function = a dedicated building

| Building | Structure | Refines / Produces | UPGRADES | Skill gate |
|---|---|---|---|---|
| **Lumbermill** | sawmill/woodshop | Wood → **Planks** (refined) | wall tiers + wood structures | Woodworking tier |
| **Forge** | blacksmith (exists as relabeled Workshop, WO-150) | Iron → **Ingots** | **WEAPONS** (+damage; the Forge spine) | Blacksmith tier |
| **Armourer** | armour-works (NEW) | Ingots/hides → **armor plate** | **ARMOR** (−damage taken / defense) | Armouring tier |
| **Arcane Library** | scholarly tower/study (NEW) | Aether → **Cores** | **MAGE SKILL TREES** (talent ranks — TALENT_V2) | Arcane tier |

Each building is the **home of its upgrade function** — you visit it to do that crafting/upgrade, and the
building's **tier gates** what you can make (the Warcraft "upgrade the Blacksmith to unlock the next weapon
tier" loop). This splits the generic "Workshop crafting" into **purpose-built stations.**

## What each does (the upgrade verbs)
- **Lumbermill** — refine Wood→Planks; Planks feed wall tiers (WO-114) + wood buildings. The materials backbone.
- **Forge** — refine Iron→Ingots; **upgrade weapons** with Ingots (+weapon/ability damage). The existing
  Forge (WO-150/151) — this gives it the weapon-upgrade station. (Ties the +1 craft / legendary reforge.)
- **Armourer** — refine into armor plate; **upgrade armor** (−damage taken, defense, the party's protection).
  The counterpart to the Forge: Forge = offense, Armourer = defense. Each its own upgrade tree.
- **Arcane Library** — refine Aether→Cores; **upgrade the mage/hero skill trees** — i.e. the **Arcane
  Library is where you spend Wisdom / rank up talents** (TALENT_TREE_V2). Makes talent progression a
  *place you go* (a study/library), not just a menu — and gates higher talent ranks behind Library tier.

## Implementation (phased; reconcile, don't reinvent)
- **Buildings = catalog entries** placed by the builder (WO-159 `StructureFactory`/catalog). Lumbermill +
  Armourer + Arcane Library are **new** roster entries; Forge exists (WO-150). Use stylized polyperfect
  `_M` meshes (match the art language — confirm names in the catalog; e.g. a sawmill, an armour-works, a
  scholarly tower).
- **Each gets a `BuildingUpgrade`** (WO-151) — its tier is the gate for what it can craft/upgrade.
- **Each gets an interaction → its upgrade panel** (code-built, no UXML): Forge→weapon-upgrade UI,
  Armourer→armor-upgrade UI, Arcane Library→**the talent/skill-tree panel** (route the existing
  `HeroTalentPanel`/`TalentTreePanel` through the Library interaction), Lumbermill→refine/production UI.
- **Refine recipes** (raw→refined) per DEFENSE_DEPTH_ANALYSIS — each building hosts its refine step,
  tier-gated throughput.
- **Layout** — group them as the "production cluster"/"smithing quarter" (WO-151/152 districts): Forge +
  Armourer + Arcane Library as a crafting quarter, Lumbermill + Farm as the resource cluster.
- Single-writer on `VillageSceneBuilder` for placement (Agent 1); the upgrade panels are gameplay-lane code.

## Constraints
- Reuse catalog/`StructureFactory` (WO-148/159), `BuildingUpgrade`/`VillageLevel` (WO-151), the existing
  talent panel (route Arcane Library → it), the economy/refine spine (DEFENSE_DEPTH). **No new crafting,
  catalog, or talent engine** — these are buildings + interactions wired to existing systems.
- Stylized `_M` meshes; missing prefab → `Debug.LogWarning` + stub. No UXML. Brace-gate. Editor-closed rebake for placement.
- Note: WO-150 said "Lumbermill = LATER" — **this is that later WO.** Adds it now.

## Acceptance criteria
1. **Lumbermill, Forge, Armourer, Arcane Library** exist as placed buildings (stylized, matching the village), each in the crafting/production district.
2. **Forge → weapon upgrades**, **Armourer → armor upgrades**, **Arcane Library → mage/hero talent upgrades** (routes the talent panel), **Lumbermill → wood refine/production** — each via its building's interaction.
3. Each building's **tier gates** what it can craft/upgrade (Warcraft spine); uses `BuildingUpgrade`/`VillageLevel`.
4. Refine recipes (raw→refined) hosted per building; built on the existing economy/talent/catalog systems (no new engines).
5. Code-built panels (no UXML); single-writer placement + rebake; brace balance.

## Open questions for owner
- **Armourer skill name** — "Armouring"/"Smithing"/"Leatherworking" tier? (Recommend Armouring, parallel to Blacksmith.)
- **Arcane Library** — does it gate *higher talent ranks* behind Library tier (upgrade the Library to unlock tier-3 talents), or just house the panel? (Recommend gate — gives the Library a reason to upgrade; ties TALENT_V2 ranks to it.)
- **Do all 4 cluster together, or spread one-per-district?** (Recommend smithing quarter = Forge+Armourer+Library; Lumbermill with Farm.)

## Done checklist (CLAUDE.md §10)
- [ ] 4 buildings placed (Lumbermill/Forge/Armourer/Arcane Library), stylized, in the crafting district
- [ ] Forge→weapons, Armourer→armor, Arcane Library→mage talents, Lumbermill→wood refine — each wired
- [ ] Per-building tier gates crafting (BuildingUpgrade/VillageLevel); refine recipes hosted
- [ ] Built on catalog/talent/economy systems (no new engines); code-built panels; no UXML
- [ ] Single-writer placement + rebake; brace balance
- [ ] `WORK_ORDER_180_production_building_roster.RESULT.md` when complete
