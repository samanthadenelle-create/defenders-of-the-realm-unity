> ⚠ **SUPERSEDED — replaced by the Tripo self-rigged roster pivot** (Knight/Ranger/Wizard + orc/skeleton/troll families). Kept for history. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Create Your Hero — the character pillar (presets → creator)

> Owner (2026-05-30): *"go from these 3 heroes to create-your-hero."* The fixed roster (Knight / Mage /
> Ranger) becomes the **first three presets** of a modular character creator — the same catalog⊥repo
> engine as the village builder, applied to heroes. Player-built village **and** player-built hero, one
> engine. Pairs with `CHARACTER_ARCHITECTURE.md`, `CATALOG_SYSTEM.md`, `BRAND_BIBLE.md`.

## The reframe
A hero is **not a model** — it's a **composition of catalog parts**:

| Layer | Source | Half | Drives |
|---|---|---|---|
| Body / race / face | modular part catalog | look (catalog) | appearance |
| Armor / cosmetic | catalog entry (ownership-gated) | look (catalog) | appearance only — **no power** (catalog⊥repo) |
| **Weapon** | `Equipment` def | **behavior (repo)** | **class → role → ActionSet → HUD layout** |

**The weapon defines the hero.** Sword → melee bruiser (Tank/DPS), swing/parry moveset, melee HUD.
Staff → caster (DPS), cast anims, spell-slot HUD. Bow → ranged, draw/fire, aim HUD. The **`Role` axis
(Healer/Ranged/DPS/Tank)** that already drives enemy AI + tower priority **also drives the player HUD
layout** (role-driven HUD — see ENGINE_MASTER_PLAN dynamic HUD binding).

## Starters = presets, not the ceiling
- 🛡️ **Knight** — modular body + plate + **sword** → melee, Tank/DPS HUD
- 🔮 **Mage** — modular body + robes + **staff** → caster, spell-slot HUD
- 🏹 **Ranger** — modular body + leather + **bow** → ranged, aim/draw HUD

These are the first three saved `CharacterDef`s. The creator exposes the part catalog so players compose
their own; presets are the on-ramp.

## Art direction (decided 2026-05-30): Elden Ring lean — mature, dark-fantasy, class-rich
Owner: *"lean in on a mix of Elden Ring — classes and heroes like that theme"* + *"put budget aside,
everything is based on this one design."* This **retires the Synty/KayKit cartoon-stylized route** —
too vibrant for the grounded dark-fantasy mood. Target = **stylized-realistic adult heroes**, weighty
combat, deep class variety.

## Best solution (budget aside): Reallusion Character Creator 4 (CC4) backbone
**CC4 is a true creation pipeline, not a model pack** — morph sliders (body/face), SkinGen, swappable
outfits/armor/weapons = literally "create your hero" and exactly the "maps to all" the owner wants
(parts = catalog, CC4 = the creator). Why it wins once budget is off the table + theme is Elden Ring:
- **Mature realistic-stylized** look (the Elden Ring mood; Synty is the opposite).
- **Unity-native:** AccuRIG auto-rig + official Unity Auto-Setup → **Humanoid** (Mixamo/mocap retarget on).
- **Endless dark-fantasy wardrobe** (Reallusion marketplace + Daz interop): plate, robes, hoods, greatswords
  as modular parts.
- **Creator tech alt (free, lighter):** UMA — slider creator, but base art is dated; CC4 wins budget-aside.

**Motion (weighty Elden-Ring combat):** **Kevin Iglesias RPG/Greatsword** + **MoCap Online
Swordsman/Warrior** for swings/rolls/parries; **Mixamo** free baseline. Humanoid rig is the hard
requirement — lets every source combine + kills the legacy slide bug ([[hero-animation-pipeline]]).

## ⚠️ Mobile fidelity gate (money aside ≠ perf budget aside)
Elden Ring is console/PC AAA; we are mobile-first. Lean into its **theme + class depth + weighty
combat FEEL, NOT literal poly/shader fidelity.** Target **stylized-realistic at mobile cost** (LODs,
atlased materials, ~15–40k tris/hero). **Gate: import ONE CC4 hero → run `Perf Budget (Standard
Phone)` BEFORE committing the roster.** Green = pipeline proven; red = dial fidelity. The creator +
class design is unchanged either way. See [[catalog-thesis-validated-live]] (PerfBudget tooling).

## Buyer's checklist (avoid the known import pains)
Humanoid/Mecanim rig (not Generic) · combat anims included OR backfill via Mixamo · **URP materials**
(dodge the Tripo Phong / pink-material trap, [[tripo-fbx-material-fixer]]) · cohesive multi-class set ·
commercial license · mobile tri-counts.

## How it reuses the engine (nothing new invented)
- Character parts = `CatalogType` (add `CharacterPart`/`Weapon`/`Armor` tabs) → same `CatalogRegistry`.
- Compose = a `CharacterDef` (list of part entries + a weapon entry), built by `CharacterFactory`.
- Cosmetic armor swaps `visual`, never `repo` — the structural cosmetic-only guarantee, monetization-safe.
- Save/load a hero = serialize its def-list (same data⊥instantiation as the realm = a def-list).

## Status / next
- Pack: owner purchasing a paid modular pack (Synty modular recommended). On import → wire hero #1 onto
  the `Character` substrate, prove a real swing through `Equipment → ActionSet`, then the other two
  presets, then expose the part catalog as the creator. Foundation dependency: engine skeleton
  (WO-106/119) + catalog model (WO-137, Part A written/brace-clean). See [[catalog-thesis-validated-live]].
