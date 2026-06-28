# WORK ORDER 553 — Jeweler: Gem + Jewelry Crafting Station

**Status: READY TO IMPLEMENT**
**Lane:** Combat/Economy data + UI (file-disjoint from VillageSceneBuilder — safe parallel)
**Owner routing note:** the WO **number must be slotted into the master backlog**
(`MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`) by the owner —
filesystem max is NOT the numbering authority (CLAUDE.md §2). 553 is a placeholder file name.

---

## OVERVIEW — the Jeweler loop

A runtime **Jeweler's Bench** station in the castle hub (Sable Vey's specialty). The player
walks up, interacts, and opens the Jeweler panel. There they **combine a base ring/amulet +
N gems** to craft a **better piece of jewelry** — a higher-tier ring/amulet with improved
stats (more `hpBonus` / `damageMult` / `defense`).

**Player-felt value:** today rings/amulets are *bought* at the Jeweler shop and never improve.
This adds a **progression sink for gems** (a new drop/economy output) and a **reason to keep
the old ring** — feed your Iron Band + gems into the bench and walk away with a Steadfast Ring.
It is the jewelry parallel to the **Apothecary** (consumable crafting, shipped) and the **gear
Forge** (WO-293 tiered/legendary gear, shipped) — same proven loop, new item family.

This is a **data-driven** feature styled with the **Obsidian/Blink UI SME** — it reuses the
exact crafting + UI scaffolding below and adds NO new architecture.

---

## SME — what already exists (reuse, do not greenfield)

### A. The two shipped crafting loops (mirror these)

**1. Apothecary consumable crafting** (closest UI/station analog):
- Recipes data: `Assets/Resources/Data/Canonical/consumable-recipes.json`
  (+ `Assets/StreamingAssets/Data/Canonical/consumable-recipes.json` — dual-copied, Resources wins).
- Typed catalog: `Assets/_Modules/Village/Items/ConsumableCraftingCatalog.cs`
  (Newtonsoft model + `CanonicalJson.Read` loader; `Find(id)` / `All` / `Reload()`).
- Craft service (atomic consume→grant): `Assets/_Modules/Village/Items/ItemCraftingService.cs`
  (`CanCraft` / `TryCraft` — verify-all-then-consume, grant output into `VillageInventory`).
- ViewModel (pure, no Unity types): `Assets/_Modules/Village/Items/CraftingVM.cs`.
- View (Obsidian skin, code-built uGUI): `Assets/_Modules/Village/Items/CraftingPanelMvvm.cs`
  (registers `PanelId.ConsumableCrafting`; 3-col recipe-card grid).
- Bootstrap (spawns the panel once a hero exists): `Assets/_Modules/Village/Items/CraftingPanelBootstrap.cs`.
- Station (runtime, non-destructive hub injection): `Assets/_Modules/Village/Items/CraftingStationInjector.cs`
  — **the exact pattern the Jeweler station copies** (DDOL singleton, `RuntimeInitializeOnLoadMethod`,
  idempotent, NavMesh-snap, `VisualFactory.Skin` with placeholder-cube fallback, opens its panel
  via `Building` + `BuildingInteractable`). **NB:** it already lists `"Structures/jeweler"` as a
  candidate model.

**2. Gear crafting (WO-293)** — the closest *mechanical* analog because jewelry is **equippable**:
- Recipes data: `Assets/Resources/Data/Canonical/gear-recipes.json` (+ StreamingAssets copy).
- Catalog: `Assets/_Modules/Village/Crafting/GearCraftingRecipeCatalog.cs`.
- Service: `Assets/_Modules/Village/Crafting/GearCraftingService.cs` — **the model to mirror**:
  `Evaluate()` shared by `CanCraft`/`Craft`; spends the **unified wallet** once
  (`EconomyService.TrySpend`), consumes `components` from `VillageInventory`, **grants the output
  equippable id** into `VillageInventory` so the equip layer can equip it; **atomic with rollback**;
  optional `requiresQuestId` gate; raises `OnCrafted`.

### B. Item / gear model (the output items)

- `docs/ITEM_MODEL.md` — Carriable ontology (universal Entry + capability flags). Accessories
  resolve `Carriable|Equippable`.
- **Accessories (rings + amulets) exist** — `Assets/Resources/Data/Canonical/accessories.json`
  (10 entries, WO-543). Typed by `Assets/_Modules/Village/Hero/AccessoryDef.cs`
  (`damageMult` / `defense` / `hpBonus`, all **additive**; `rarity`, `slot` ring|amulet,
  `setId`, `req.level`, `iconPath = ItemIcons/<id>`).
- Loaded by `Assets/_Modules/Village/Hero/GearCatalog.cs`
  (`FindAccessory(id)`, `Accessories`, `AccessoriesForSlot(slot, level)`).
- Equip slots: `Assets/_Modules/Village/Hero/EquipVM.cs` (ring + amulet slots, WO-543);
  loadout/stat application `Assets/_Modules/Village/Hero/GearLoadout.cs`.
- **Real accessory ids to use as recipe inputs/outputs (cited):**
  - Rings: `ring_iron` (common, hp15) → `ring_steadfast` (uncommon, hp35) → `ring_embercoil`
    (rare, dmg+8%) → `ring_heartward` (epic, def+5%/hp30) → `ring_firstlight` (legendary, dmg+12%/hp50).
  - Amulets: `amulet_travelers` (common, hp20) → `amulet_oathward` (uncommon, def+4%) →
    `amulet_lastpressing` (rare, dmg+10%) → `amulet_elarion` (epic, def+7%/hp50) →
    `amulet_heartstone` (legendary, dmg+12%/def+10%, set `aegis`).

### C. **GEMS DO NOT EXIST YET — content to author**

There is **no gem item family** in any catalog. Searched `accessories.json`, `materials.json`,
`weapons.json`, `armor.json`, `loot-tables.json`: "crystals" is an **economy currency**
(`buyCrystals`, `EconomyService`), and `materials.json` has crystal-*category* crafting
ingredients (`ing_ember_crystal`, `ing_aether_shard`, `ing_heartstone_crystal`) but **no gems**.
Design docs describe a *future* Jeweler "rare stone → cut gem" with craft-FAILURE + "+1" perks
(`docs/DEFENSE_DEPTH_ANALYSIS.md`, `docs/RESOURCE_ECONOMY_DESIGN.md`) — **that is V2 scope**, not
this WO.

➡ **Gems are NEW CONTENT to author in this WO** as a material family (mirrors the `ing_*`
materials that the Apothecary added). See DATA MODEL below. Icons are content-to-author
(glyph fallback until sliced, exactly like the Apothecary ingredients).

### D. UI SME (the look to reuse — NOT a new style)

- `Assets/_Modules/Village/Hero/EquipmentPanel.cs` — gold-standard Obsidian look (central 3D hero
  + 2D slot plates). The Jeweler panel borrows its plate/frame vocabulary.
- `Assets/_Modules/Core/UI/RpgUiCatalog.cs` — pack sprites: `PanelWindowDark`, `ButtonFrame`,
  item-plate sprites. `Assets/_Modules/Core/UI/ElarionUiKit.cs` — `BuildModalCanvas`, `Scrim`,
  `PanelFramed`, `Header`, `Label`, `ButtonPack` (Gold/Quiet), `AddImage`, `AddInnerRim`, `Cell`.
- `docs/UI/OBSIDIAN_UI_DESIGN_skilltree_inventory.md` — the Obsidian design language.
- **`CraftingPanelMvvm.cs` is the literal template** — copy its chrome
  (`BuildModalCanvas` 31000 sort, scrim, `PanelFramed` with `RpgUiCatalog.PanelWindowDark`,
  `Header`, `FeatureFlags.BlinkChrome`-aware fill, 3-col card grid, `ButtonPack` craft button
  that dims when not craftable, `Close` button) and re-theme for jewelry.

---

## DATA MODEL — data-driven jeweler recipes

### Recommendation: a NEW file `jeweler-recipes.json` + gems in `materials.json`

- **New recipes file** (do NOT overload `consumable-recipes.json` or `gear-recipes.json` — each
  crafting lane owns its own file, per the §-comments in those catalogs to "never collide"):
  `Assets/Resources/Data/Canonical/jeweler-recipes.json`
  **AND** `Assets/StreamingAssets/Data/Canonical/jeweler-recipes.json` (dual-copy; Resources wins).
- **Gems authored as a material family** in the EXISTING `materials.json` (so they drop, stack in
  `VillageInventory`, and render via `MaterialCatalog` icon/glyph with zero new catalog code).
  Use a `gem_*` id prefix and `category: "gem"`.

### Recipe schema (mirrors gear-recipes.json shape — equippable output + components + cost)

```json
{
  "version": 1,
  "_note": "Jeweler jewelry crafting. base = an accessories.json id consumed from inventory; gems = material ids (materials.json, gem_*) consumed; outputAccessoryId = the upgraded accessories.json id granted into inventory. cost spends the unified wallet (EconomyService.TrySpend). Loaded by JewelerRecipeCatalog via CanonicalJson (Resources first). SEPARATE file from consumable-recipes.json / gear-recipes.json.",
  "recipes": [
    {
      "id": "jewel_ring_steadfast",
      "displayName": "Set the Steadfast Ring",
      "base":   { "id": "ring_iron", "count": 1 },
      "gems":   [ { "id": "gem_garnet", "count": 2 } ],
      "outputAccessoryId": "ring_steadfast",
      "cost":   { "wood": 0, "food": 0, "iron": 30, "crystals": 0 },
      "requiresQuestId": "",
      "saga": "Sable seats two garnets in a plain iron band — and it remembers a purpose."
    }
  ]
}
```

Field meanings:
- `base` — `{id,count}`; an **accessories.json id consumed** from `VillageInventory` (the piece
  you upgrade). Count normally 1.
- `gems` — `[{id,count}]`; **material ids (`gem_*`) consumed** from inventory.
- `outputAccessoryId` — an **accessories.json id granted** into inventory (the better piece).
- `cost` — `{wood,food,iron,crystals}`; spent ONCE via `EconomyService.TrySpend` (may be all-zero).
- `requiresQuestId` — optional gate via `QuestService.IsCompleted` ("" = always available).

### How "better" is expressed — **RECOMMENDATION: tier-up to a higher-rarity output id**

Two options:
1. **Tier-up (RECOMMENDED):** output is a **higher-tier accessory id already in
   `accessories.json`** (e.g. `ring_iron` → `ring_steadfast` → `ring_embercoil`). The stat
   ladder already exists in the catalog; equip applies its static modifiers; no per-instance
   item state is needed. Clean, data-only, ships now.
2. **In-place affix/stat boost (NOT recommended for V1):** mutate the equipped piece's
   `hpBonus`/`damageMult` in place. **There is no per-instance item-state system** —
   accessories are stateless catalog defs and `VillageInventory` stores **ids + counts only**.
   This would require a new instance/affix subsystem (V2; ties to the "+1 perk" / craft-failure
   design in `docs/DEFENSE_DEPTH_ANALYSIS.md`). Out of scope here.

➡ **Implement option 1.** It mirrors `GearCraftingService`'s "grant a stronger output id" exactly.

### Example recipes (4–6) — uses REAL accessory ids; gems are content-to-author placeholders

> Accessory ids are real (`accessories.json`). `gem_*` ids are **NEW content authored by this WO**
> (in `materials.json`); icon art is content-to-author (glyph fallback until sliced).

| id | base (real) | gems (author) | output (real) | cost |
|---|---|---|---|---|
| `jewel_ring_steadfast`   | `ring_iron` ×1        | `gem_garnet` ×2                      | `ring_steadfast`    | iron 30 |
| `jewel_ring_embercoil`   | `ring_steadfast` ×1   | `gem_ruby` ×2, `gem_garnet` ×1       | `ring_embercoil`    | iron 60, crystals 10 |
| `jewel_amulet_oathward`  | `amulet_travelers` ×1 | `gem_sapphire` ×2                    | `amulet_oathward`   | iron 40 |
| `jewel_amulet_lastpress` | `amulet_oathward` ×1  | `gem_sapphire` ×1, `gem_amethyst` ×2 | `amulet_lastpressing` | crystals 20 |
| `jewel_ring_heartward`   | `ring_embercoil` ×1   | `gem_diamond` ×1, `gem_ruby` ×2      | `ring_heartward`    | iron 80, crystals 25 |
| `jewel_amulet_elarion`   | `amulet_lastpressing` ×1 | `gem_diamond` ×2, `gem_amethyst` ×1 | `amulet_elarion`   | iron 60, crystals 40 |

**Gems to author in `materials.json`** (`kind: "material"`, `category: "gem"`, `glyph` fallback,
`iconPath: ItemIcons/gem_*`): `gem_garnet`, `gem_ruby`, `gem_sapphire`, `gem_amethyst`,
`gem_diamond`. (Legendary outputs `ring_firstlight` / `amulet_heartstone` can be added later
behind a quest gate — leave for a follow-up so V1 stays focused.)

**Gem source — OWNER DECISION (2026-06-28):** gems (and regular gear items) drop **ONLY from BOSS
mobs at a LOW drop rate** (trash/garrison mobs drop currency/echoes only). And the **easy win**:
reuse the EXISTING crystal-category ingredients in `materials.json` — `ing_ember_crystal`,
`ing_aether_shard`, `ing_heartstone_crystal` — as the gems (no new gem family to author). So:
gate gem + gear drops in `loot-tables.json` behind a boss/rarity flag with a low roll chance; the
DataRegression "obtainable" check is satisfied by the boss drop entry. See memory
[[loot-drops-boss-only-low-rate]].

---

## SYSTEM REUSE — classes to add/extend (named, with paths)

**New (mirror the named template exactly — do not invent a new shape):**
- `Assets/_Modules/Village/Crafting/JewelerRecipeCatalog.cs` — typed model + loader.
  **Mirror** `ConsumableCraftingCatalog.cs` (Newtonsoft + `CanonicalJson.Read`, `Find`/`All`/`Reload`).
  Model: `JewelerRecipeDef { Id, DisplayName, Base{Id,Count}, Gems[]{Id,Count}, OutputAccessoryId,
  Cost{Wood,Food,Iron,Crystals}, RequiresQuestId, Saga }`.
- `Assets/_Modules/Village/Crafting/JewelerCraftingService.cs` — **mirror `GearCraftingService.cs`**:
  shared `Evaluate()` for `CanCraft`/`WhyCannotCraft`/`Craft`; atomic — verify base + gems coverage
  + wallet `CanAfford`, then `TrySpend` once, `TryConsume` base + each gem, **grant
  `OutputAccessoryId` into `VillageInventory`** (so `EquipVM` can equip it); rollback the spend if a
  consume fails; optional `requiresQuestId` via `QuestService`; raise `OnCrafted`. Lives in
  `DeNelle.Village.Crafting` (needs `EconomyService` + `VillageInventory` — Village→Core legal).
- `Assets/_Modules/Village/Items/JewelerVM.cs` — **mirror `CraftingVM.cs`** (pure, no Unity types):
  project each recipe to a card payload (base have/need + gem checklist have/need + output
  name/icon via `GearCatalog.FindAccessory` + `MaterialCatalog`; `CanCraft`); `Craft(id)` →
  `JewelerCraftingService.Craft`; subscribe `VillageInventory.Changed`.
- `Assets/_Modules/Village/Items/JewelerPanelMvvm.cs` — **mirror `CraftingPanelMvvm.cs`** (Obsidian
  skin; registers a NEW `PanelId.JewelerCrafting`). See UI below.
- `Assets/_Modules/Village/Items/JewelerStationInjector.cs` — **mirror `CraftingStationInjector.cs`**
  (DDOL singleton, idempotent, NavMesh-snap, `VisualFactory.Skin("Structures/jeweler", …)` upright
  correction + placeholder-cube fallback, `Building` + `BuildingInteractable` → opens
  `PanelId.JewelerCrafting`). Reuse the panel bootstrap pattern of `CraftingPanelBootstrap.cs`
  (either extend it to also spawn `JewelerPanelMvvm`, or add a sibling bootstrap).

**Extend (small, surgical):**
- `Assets/_Modules/Core/UI/PanelRouter.cs` — add `JewelerCrafting = 10` to `enum PanelId`
  (after `ConsumableCrafting = 9`).
- `Assets/_Modules/Village/Buildings/Building.cs` — add `BuildingType.JewelersBench` (next free
  enum value after `ApothecaryWorkbench = 8`); `Assets/_Modules/Village/Buildings/BuildingInteractable.cs`
  — map it to label "Jeweler" + route `StructureHookIdFor` to null so it opens the panel directly
  (mirror the apothecary fall-through), and `TryPanelFor` → `PanelId.JewelerCrafting`.
- `Assets/Editor/Regression/DataRegression.cs` — add `CheckJewelerChain` (see Acceptance).
- `Assets/Resources/Data/Canonical/materials.json` (+ StreamingAssets copy) — add the `gem_*`
  family. `Assets/Resources/Data/Canonical/loot-tables.json` (+ copy) — add gem drops (or wire
  gems into the Jeweler shop via `VendorStockContract.cs`).

**Reuse as-is (no change):** `GearCatalog.FindAccessory`, `VillageInventory` (`Get`/`TryConsume`/`Add`/`Changed`),
`EconomyService` (`CanAfford`/`TrySpend`/`Grant`), `MaterialCatalog`, `ElarionUiKit`, `RpgUiCatalog`,
`PanelManager`/`PanelRouter`, `VisualFactory.Skin`.

---

## UI — the Jeweler panel (reuse the Obsidian SME, NOT a new style)

Build `JewelerPanelMvvm` by copying `CraftingPanelMvvm` chrome and re-theming:
- **Canvas/chrome:** `ElarionUiKit.BuildModalCanvas("JewelerPanelMvvmUI", 31000)` + `overrideSorting`;
  `ElarionUiKit.Scrim(... onTapClose)`; dark backdrop; `ElarionUiKit.PanelFramed(..., deep:true,
  packSpriteName: RpgUiCatalog.PanelWindowDark)`; `FeatureFlags.BlinkChrome`-aware solid fill.
- **Header:** `ElarionUiKit.Header(panel, "Jeweler's Bench", …)` + one-line hint
  ("Set gems into a ring or amulet to forge a finer piece.").
- **Card grid (3 columns)** — one card per recipe (reuse the `RebuildCards` layout math):
  - **Output plate (top):** the upgraded jewelry icon (`GearCatalog.FindAccessory(out).iconPath`
    via `Resources.Load<Sprite>`; emoji 💍/📿 fallback) + gilt output name — use the
    `RpgUiCatalog` item-plate vocabulary from `EquipmentPanel`/`CraftingPanelMvvm`
    (`ElarionUiKit.AddImage` cell + `AddInnerRim(AccentSoft)`).
  - **Ingredient checklist:** first the **BASE** piece (icon + "Iron Band  have/need"), then each
    **gem** line (icon + "Ruby  have/need"); green (`ElarionUi.Affordable`) when met, red
    (`ElarionUi.Danger`) when short — exactly like `CraftingPanelMvvm.BuildRecipeCard`. Include a
    small **cost line** (iron/crystals) when `cost` is non-zero.
  - **Craft button:** `ElarionUiKit.ButtonPack(..., recipe.CanCraft ? Gold : Quiet, …)`; label
    "Set Gems" when craftable, "Need Gems" when not; `interactable = recipe.CanCraft`.
  - **Empty state:** "No jewelry recipes available." (mirror the consumable empty label).
- **Close:** `ElarionUiKit.ButtonPack("Close", Quiet, …, packSpriteName: RpgUiCatalog.ButtonFrame)`.

The View NEVER reads game state — all data comes from the bound `JewelerVM` (MVVM seam,
`ui-mvvm-binding-seam` rule). Code-built uGUI only (no UXML — CLAUDE.md §8).

---

## ACCEPTANCE CRITERIA (testable)

1. **Data loads:** `JewelerRecipeCatalog.All` returns the authored recipes (Resources first,
   StreamingAssets fallback); a missing file yields zero recipes (graceful, no throw).
2. **Station + panel:** in a hub scene, a "Jeweler" station is present (idempotent — never two);
   walking up + interacting opens the Jeweler panel (`PanelId.JewelerCrafting`). On a
   pack-missing clone the station still appears (placeholder cube) and still opens the panel.
3. **Craft is atomic + correct (headless DataRegression-style check — `CheckJewelerChain`):**
   for every recipe — (a) `OutputAccessoryId` resolves in `GearCatalog.FindAccessory`;
   (b) `base.id` resolves as an accessory; (c) every `gem.id` resolves in `MaterialCatalog`;
   (d) every gem id is **obtainable** (drops from a loot table OR sold by the Jeweler) so the
   recipe is craftable; (e) a **simulated craft**: seed `VillageInventory` with base + gems +
   wallet, call `JewelerCraftingService.Craft`, assert it returns success, the base + gems are
   **consumed**, the wallet **debited**, and `OutputAccessoryId` is **granted** (count +1);
   and a no-funds craft returns false and consumes nothing (rollback). Log a one-line summary
   ("[jeweler] N recipe(s), M gem(s); K fully craftable base+gems→output").
4. **Equip path:** the granted output id is equippable via the existing `EquipVM` ring/amulet slot
   (no new equip code) — the crafted piece shows its higher stats on equip.
5. **Gate green:** `CompileGate` passes (brace-balanced; no NUL bytes); `DataRegression` passes
   incl. the new `CheckJewelerChain`; both recipe-file copies (Resources + StreamingAssets) in sync.

---

## WHAT NOT TO TOUCH

- **Do NOT hand-edit any `.unity` scene** (CLAUDE.md §3) — the station is runtime-injected.
- **Do NOT modify** `consumable-recipes.json` / `ItemCraftingService` / `CraftingVM` /
  `CraftingPanelMvvm` (the Apothecary lane) or `gear-recipes.json` / `GearCraftingService`
  (the Forge lane) — the Jeweler owns its OWN files (no cross-lane collision).
- **Do NOT** build a per-instance affix/stat-mutation system (option 2) — out of scope (V2).
- **Do NOT** add craft-failure / "+1 perk" / Jeweler-tier mechanics — deferred design
  (`docs/DEFENSE_DEPTH_ANALYSIS.md`); V1 craft is deterministic.
- **Do NOT** touch `VillageSceneBuilder.cs` (serialization bottleneck, §9).

---

## FILES TO EDIT / CREATE

**Create:**
- `Assets/Resources/Data/Canonical/jeweler-recipes.json` (+ `Assets/StreamingAssets/Data/Canonical/jeweler-recipes.json`)
- `Assets/_Modules/Village/Crafting/JewelerRecipeCatalog.cs`
- `Assets/_Modules/Village/Crafting/JewelerCraftingService.cs`
- `Assets/_Modules/Village/Items/JewelerVM.cs`
- `Assets/_Modules/Village/Items/JewelerPanelMvvm.cs`
- `Assets/_Modules/Village/Items/JewelerStationInjector.cs`

**Edit:**
- `Assets/_Modules/Core/UI/PanelRouter.cs` (`PanelId.JewelerCrafting = 10`)
- `Assets/_Modules/Village/Buildings/Building.cs` (`BuildingType.JewelersBench`)
- `Assets/_Modules/Village/Buildings/BuildingInteractable.cs` (label + panel route + null hook)
- `Assets/Resources/Data/Canonical/materials.json` (+ StreamingAssets copy) — `gem_*` family
- `Assets/Resources/Data/Canonical/loot-tables.json` (+ copy) — gem drops *(or wire gems into
  the Jeweler shop via `Assets/_Modules/Village/Hero/VendorStockContract.cs`)*
- `Assets/Editor/Regression/DataRegression.cs` (`CheckJewelerChain`)
- `Assets/_Modules/Village/Items/CraftingPanelBootstrap.cs` *(if extended to also spawn the Jeweler panel)*

---

## OPEN QUESTIONS FOR THE OWNER

1. **Better = tier-up vs affix-boost?** Spec recommends **tier-up to a higher-rarity output id**
   (data-only, ships now). Affix/stat boost needs a new per-instance item-state system (V2). Confirm.
2. **Gems consumed vs socketed?** Spec recommends **consume** (mirrors all existing crafting).
   "Gem sockets" appear in design docs (`DESIGN_VENDOR_STORYLINES…`, "high-end gear sockets") —
   that is a bigger V2 feature. Confirm consume-for-now.
3. **Gem source?** Loot-table drops, Jeweler-shop purchase, or both? (Recipes must be obtainable
   for the regression "craftable" check.)
4. **Craft FAILURE / "+1 perk" / Jeweler tier** (`docs/DEFENSE_DEPTH_ANALYSIS.md`) — confirm
   **deferred to V2** (V1 craft is deterministic).
5. **Gating** — should the Jeweler bench be available from the start, or gated behind a building
   tier / feature flag (e.g. an `ff.jeweler` or the Commerce-quadrant unlock)? The Apothecary
   ships unflagged; the Jeweler could match or be gated.
6. **Gem icon art** — the 5 `gem_*` icons are content-to-author (glyph fallback until sliced).
   Author now, or ship on glyphs first (as the Apothecary did)?
7. **Legendary outputs** (`ring_firstlight` / `amulet_heartstone`, set `aegis`) — include behind a
   quest gate in V1, or hold for a follow-up WO?
```
