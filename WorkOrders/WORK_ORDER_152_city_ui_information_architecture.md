> ⚠ **NUMBER COLLISION — this document does not own WO-152; `WORK_ORDER_152_full_city_redesign_component_catalog.md` does.**
> Referred to hereafter as **WO-152-B (city UI information architecture)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> the two files were added in the **same commit**, so first-on-disk is a tie; ownership decided on **cross-references** (the winner is the file the rest of the corpus cites).
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 152 — City UI Information Architecture (which surfaces group into which buildings)

**Status:** READY TO IMPLEMENT (IA decision locked; panel wiring phased)
**Date:** 2026-05-30
**Priority:** Medium-High — this is the *navigation spine* of the whole town. Every progression/commerce panel the project already built (talents, crafting, cosmetics, store, pet tree) is currently reached through ad-hoc per-building key presses with no consistent mental model. Locking the IA now stops the next 6 WOs from each inventing their own "where does this live" answer.
**Lane:** **design / UX** (this WO) → wiring is HUD + Village code (CLI). NOT the architect/`VillageSceneBuilder` lane — roster *placement* is SPEC'd for a later coordinated CLI builder pass (CLAUDE.md §9).
**Depends on:**
- **WO-150** — the live baked 5-building roster (Store/Forge/Pet Home/Tower/Farm) + the `BuildingInteractable` [F]-dispatch this WO re-maps.
- **WO-151** — adds Lumbermill/Ironworks/Armory + `VillageLevel` gate; this WO places those buildings in the IA and decides where weapon/armor upgrade panels live (answer: AT the crafting buildings, not the character hall — justified below).
**North Star:** `docs/NORTH_STAR.md` — CoC×Warcraft base-builder. A *legible town* (commerce district vs. character hall vs. production cluster vs. defense) is the CoC/Warcraft convention that makes a base "readable at a glance"; this WO is that legibility pass.

---

## Owner question (verbatim — the decision being made)

> "just wondering if all these displays should reside in a singular building — inventory gear items spells that stuff"
> "if we grouped them it would make more sense and leave the store or auction house separate"
> Surface list named: "store, pet store, cosmetic store, then upgradeables weapon armor inventory and exp skill tree and maybe from whichever magic skill tree they can choose where they want to go"

Owner lean: **character-stuff grouped** (inventory / gear / spells / skill-tree) · **commerce separate**.

---

## 1. RECONCILE FIRST — what panels ALREADY EXIST vs. are MISSING (verified by inspection)

I read each file before deciding. **Do NOT greenfield anything in the "EXISTS" column — wire it up.**

| Surface owner named | State | Where (verified) |
|---|---|---|
| **Main store** | **EXISTS** | `Assets/_Modules/Wallet/PackStore.cs` + `PackCatalog.cs`; opened by `MarketplaceInteractor.cs` ([F] proximity). The "Realm Store" — packs/premium. |
| **Cosmetic store** | **EXISTS** | `Assets/_Modules/HUD/CosmeticShopPanel.cs` (+`Bootstrap`), toggled with **C**; spends **Glimmer** (`Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs`); catalog `Cosmetics/CosmeticCatalog.cs`. Categories already Hero/Pet/Village. |
| **Pet store** | **MISSING as a store.** Pet Home today opens the **pet skill tree**, not a shop. | `Assets/_Modules/HUD/PetSkillTreePanel.cs` (+`Bootstrap`), `Pets/PetSkillTreeCatalog.cs`. No pet-purchase/roster-buy panel exists. |
| **EXP / skill tree (hero talents)** | **EXISTS** | `Assets/_Modules/Village/Talents/TalentTreePanel.cs` (Wisdom-spend, 3-tier×2-col) + `Assets/_Modules/HUD/HeroTalentPanel.cs`(+`Bootstrap`); EXP/level via `Village/Hero/HeroProgression.cs`, gear-screen `HUD/PlayerProgressPanel.cs`. |
| **"magic skill tree — choose which path"** | **PARTIAL — the data spine exists, the *branch-choice UX* does NOT.** | `Assets/_Modules/Core/Progression/SkillSystem.cs` holds **three craft/skill tracks: Blacksmith / Woodworking / Arcane** (+GatheringSpeed), `SpendPoint(SkillType)`, `AvailablePoints`. This IS the "choose which path" substrate — but no panel lets the player *pick a path*; points are spent via a `LevelUpSkillPopup`. The hero *talent* tree (`TalentTreePanel`) is class-fixed, separate from this. |
| **Spells** | **EXISTS (kit), no management panel.** | `Assets/_Modules/Village/Hero/HeroAbilities.cs` = Q/W/E/R kit from `AbilityCatalog.cs`; HUD bar `HeroAbilitiesHudBridge.cs`. No spell-book / spell-swap / loadout panel. |
| **Weapon upgrade** | **SPEC'd (WO-151)** | Forge → `BuildingEffects.WeaponDamageMultiplier`. Upgrade panel is WO-151's "code-built, mirror TowerUpgradeButton". |
| **Armor upgrade** | **SPEC'd (WO-151)** | Armory → `BuildingEffects.DamageTakenMultiplier`. |
| **Inventory** | **EXISTS but is a CRAFTING LARDER, not an equipment bag.** | `Assets/_Modules/Village/Crafting/VillageInventory.cs` (ingredient/output counts, persisted) shown inside `Village/Crafting/VillageCraftingPanel.cs`. |
| **Gear / equipment** | **MISSING.** No equip-slot system anywhere. `PlayerProgressPanel` is XP-only (its header *says* "gear-screen" but renders Level/XP). | — (grep for `Equip`/`EquipmentSlot`/`Loadout` → none) |
| **[F] building dispatch** | **EXISTS** | `Village/Buildings/BuildingInteractable.cs` maps `BuildingType`→panel by reflective key-press (PetHouse→P, Workshop→K, ArcaneTower→T). This is the dispatch table we re-map. |

**Net:** 5 of the named surfaces are BUILT (main store, cosmetic store, hero talent tree, EXP, spells-kit + crafting "inventory"). 4 are MISSING/partial: **pet store, true equip/gear, spell-management panel, and the magic-path *choice* UX**. The IA must place all of them without duplicating the built ones.

> **UXML constraint (project memory *uxml-uidocuments-dont-render-in-builds*; PIPELINE_STATE.md §8):** every panel here is **code-built UI Toolkit** (the pattern `TalentTreePanel`/`VillageCraftingPanel`/`TowerUpgradeButton` already use). Any NEW panel below MUST be code-built — no `.uxml` source. State this in each child WO.

---

## 2. THE IA DECISION (the answer to the owner's question)

**Cognitive grouping by *verb*, the CoC/Warcraft town convention** — a player should know what a district is *for* before they walk in:

| District (verb) | Mental model | Buildings |
|---|---|---|
| **COMMERCE** — "spend currency, get a thing" | shops / market square | Store · Cosmetic Store · Pet Store (+ optional Auction House) |
| **CHARACTER** — "manage *me* (the hero)" | a Great Hall / Sanctum | **Hero Hall** (new) — inventory, gear, spells, EXP/talent tree, magic-path choice |
| **PRODUCTION** — "raise materials" | resource cluster | Farm · Lumbermill · Ironworks · Crystal Mine |
| **SMITHING / UPGRADE** — "spend materials → combat power" | forge quarter | Forge (weapon+) · Armory (armor−dmg) |
| **DEFENSE** | the wall line | Arcane Tower (+ wall tiers WO-114) |

### 2a. Owner's core question — group character-stuff? **YES. Decision: a single "Hero Hall" building hosts inventory + gear + spells + EXP/talent-tree + magic-path choice.**

**Justification:**
- These five surfaces all answer *"who is my hero right now"* — they're consulted together (check XP → spend a talent point → re-slot a spell → check gear). Scattering them across 5 buildings forces a walking tour for one mental task. CoC's "Hero altar" and Warcraft's character sheet both co-locate exactly this set.
- It collapses today's confusing split where talents open via the **Tower** ([T]) and EXP lives on a HUD gear-icon — neither is where a player looks for "my character." The Hero Hall becomes the **one** answer.
- It's *one new building* + a tabbed panel, reusing 4 already-built panels as tabs (talent tree, EXP view, spells kit display, the crafting-inventory as the "bag" tab). Only **gear** and the **magic-path choice tab** are net-new code.

### 2b. Commerce — **STAYS SEPARATE from character, and the three stores stay as THREE standalone buildings.** (Matches owner lean.)

- **Store** (PackStore) and **Cosmetic Store** (CosmeticShopPanel) are already distinct, use *different currencies* (premium/packs vs. Glimmer), and have different art/intent — merging them would muddy both. Keep standalone.
- **Pet Store** = **new, standalone** (the one missing commerce surface). It is *not* the Pet Home: Pet Home = pet **skill tree** (manage owned pets); Pet Store = **acquire** pets/pet-items. Two verbs, two buildings — consistent with the rule.
- **Auction House:** owner floated it as the "separate commerce" example. **Recommendation: DEFER as a roster *slot*, do not build now** — it needs a live backend marketplace (memory *backend-never-connected*: backend was never deployed; an AH without server-side trade is a stub). Reserve the plot in the commerce district; ship when backend lands. Flagged, not built.

### 2c. Upgradeables — **weapon upgrade lives AT the Forge, armor upgrade AT the Armory (NOT in the Hero Hall).** (Decision; resolves the WO-151 cross-question.)

**Justification:** weapon/armor upgrades in this game are **village-building upgrades** (WO-151: Forge tier → `WeaponDamageMultiplier`, Armory tier → `DamageTakenMultiplier`), *spent in materials, gated by `VillageLevel`* — they are a *base-building* action, not a *character-sheet* action. They belong in the SMITHING district where the materials are forged, exactly as WO-151 specs. The Hero Hall is where you *view* your resulting combat power (read-only stat readout), the Forge/Armory is where you *buy the upgrade*. This keeps the build-up loop (CoC-style: upgrade the building) distinct from the character loop (spend talent points). **Do not move WO-151's upgrade panels into the Hero Hall.**

### 2d. The "magic skill tree — choose which path" — **lives in the Hero Hall as a "Path / Skills" tab, driven by `SkillSystem`.** (Decision.)

- The substrate is **`SkillSystem`** (`Core/Progression/SkillSystem.cs`): three tracks **Blacksmith / Woodworking / Arcane** + `AvailablePoints` + `SpendPoint(SkillType)`. The owner's "choose which path they want to go" = letting the player *direct their points into a track* (lean Arcane vs. Blacksmith vs. Woodworking) instead of the current popup that spends them inline.
- Build a **code-built "Skill Paths" tab** in the Hero Hall that renders the three `SkillSystem` tracks as branch columns, shows `AvailablePoints`, and calls `SpendPoint(type)` to invest down a chosen path. This is the missing *choice UX* — the data/spend API already exists, do NOT fork it.
- **Keep it distinct from the hero *talent* tree** (`TalentTreePanel`, Wisdom-spend, class-fixed): talents = per-class combat nodes; skill-paths = the cross-cutting craft/arcane specialization that also gates tower placement (`HasRequiredSkill`). Both are tabs in the Hero Hall, clearly labeled ("Talents" vs "Skill Paths"), so the hall is the single home for *all* hero progression without conflating two systems.
> If the owner intends "magic path" to mean a *mage subclass / spell-school* choice rather than the craft-skill tracks, that's a larger new system — flag and confirm before building. Default interpretation = the existing `SkillSystem` Arcane/Blacksmith/Woodworking tracks, since that's what exists and what "skill tree path" maps to in-code.

---

## 3. FINAL BUILDING ROSTER → PANEL MAPPING (the deliverable table)

Reconciles current 5 (WO-150) + WO-151's 3 + this WO's Hero Hall + Pet Store (+ deferred AH).

| # | Building | District | [F] / hotkey opens | Panel state | Notes |
|---|---|---|---|---|---|
| 1 | **Store** (Market) | Commerce | [F] → PackStore | **EXISTS** (`PackStore`/`MarketplaceInteractor`) | + WO-151 "buy materials" stall. Packs/premium. |
| 2 | **Cosmetic Store** | Commerce | [F] → CosmeticShopPanel (also **C**) | **EXISTS** (`CosmeticShopPanel`, Glimmer) | Today only C-key; ADD an [F]-building entry. |
| 3 | **Pet Store** | Commerce | [F] → PetStorePanel | **NEW (build)** | Acquire pets/pet-items. Distinct from Pet Home. Currency = owner's call (Crystals/Glimmer). |
| 4 | **Hero Hall** | Character | [F] → HeroHallPanel (tabbed) | **NEW shell, reuses 4 existing panels as tabs** | Tabs: **Inventory/Bag** (VillageInventory) · **Gear** (NEW) · **Spells** (HeroAbilities/AbilityCatalog readout + slotting) · **EXP & Talents** (HeroProgression + TalentTreePanel) · **Skill Paths** (SkillSystem branch-choice, NEW tab). |
| 5 | **Pet Home** | Character-adjacent | [F] → PetSkillTreePanel | **EXISTS** | Manage OWNED pets (skill tree). Stays standalone (it's the pet character-sheet; pairs conceptually with Hero Hall but keeps its own building). |
| 6 | **Forge** | Smithing | [F] → BuildingUpgrade panel (weapon) | **SPEC'd WO-151** | +weapon dmg/tier. Upgrade lives HERE, not Hero Hall (§2c). Also hosts `VillageCraftingPanel` crafting (existing). |
| 7 | **Armory** | Smithing | [F] → BuildingUpgrade panel (armor) | **NEW building (WO-151)** | −damage taken/tier. |
| 8 | **Farm** | Production | [F] → BuildingUpgrade panel (food yield) | **EXISTS / WO-151** | |
| 9 | **Lumbermill** | Production | [F] → BuildingUpgrade panel (wood yield) | **NEW (WO-151)** | |
| 10 | **Ironworks** | Production | [F] → BuildingUpgrade panel (iron yield) | **NEW (WO-151)** | |
| 11 | **Crystal Mine** | Production | [F] → crystal harvest | **EXISTS** (`CrystalMine.cs`) | |
| 12 | **Arcane Tower** | Defense | [F] → Tower upgrade chain | **EXISTS** (TowerData/TowerUpgrade) | Talents NO LONGER open here (move [T] dispatch to Hero Hall, §4). |
| — | *(Auction House)* | Commerce | — | **DEFERRED** (reserve plot) | Needs deployed backend (memory *backend-never-connected*). Do NOT build now. |

> **Grouped-vs-standalone, per the surfaces the owner named:**
> - Inventory → **GROUPED** (Hero Hall tab) · Gear → **GROUPED** (Hero Hall tab, new) · Spells → **GROUPED** (Hero Hall tab) · EXP/skill-tree → **GROUPED** (Hero Hall tab) · magic-path choice → **GROUPED** (Hero Hall "Skill Paths" tab).
> - Store → **STANDALONE** · Cosmetic Store → **STANDALONE** · Pet Store → **STANDALONE (new)**.
> - Weapon-upgrade → **STANDALONE at Forge** · Armor-upgrade → **STANDALONE at Armory** (building-upgrade, not character — §2c).

---

## 4. The [F]-dispatch re-map (the one behavior change to an existing file)

`BuildingInteractable.cs` currently routes `ArcaneTower → [T] talent tree` and `Workshop → [K] crafting`. The IA needs:

- **Add a `BuildingType.HeroHall`** (or reuse a generic id) → opens `HeroHallPanel`.
- **Move talent-tree dispatch OFF the Tower** → onto the Hero Hall. Tower [F] should open the **tower upgrade** panel (its actual purpose), not talents.
- **Add Pet Store** and **Cosmetic Store** dispatch entries.
- The existing reflective `SimulateKeyPress`/`Toggle()` bridge pattern is fine to extend (it avoids asmdef refs) — but prefer adding the new panels' type names to that switch rather than inventing a new mechanism. **Note (tech debt, flag only):** `BuildingInteractable` uses `System.Reflection` to toggle panels; CLAUDE.md §10 discourages *new* reflection in bridge scripts. Extending an existing reflective switch is acceptable; do NOT add a *new* reflection bridge for the Hero Hall — if a cleaner seam is wanted, route through `CoreServices`/an interface (designer flags this for CLI's judgment, does not mandate a refactor in this WO).

> `BuildingType` is in `Building.cs` (`DeNelle.Village`) — adding `HeroHall`/`PetStore` enum members is a small, safe edit (append to the enum to preserve serialized order; CrystalMine=0…Farm=4 must keep their values). This is CODE, owned by CLI.

---

## 5. PHASING

**Phase A — wire-up only (no new panels; highest value, lowest risk):**
- Re-map `BuildingInteractable` dispatch (§4): Tower→tower-upgrade, add Cosmetic Store [F] entry (panel already exists), keep Pet Home→pet tree.
- This alone makes the *existing* built panels reachable through a consistent town model.

**Phase B — Hero Hall shell (reuse existing panels as tabs):**
- New `HeroHallPanel` code-built tabbed shell. Tabs that are **pure reuse**: Bag (`VillageInventory`), EXP/Talents (`HeroProgression`+`TalentTreePanel`), Spells (read-only `AbilityCatalog`/`HeroAbilities` display).
- New building entry in roster SPEC (§6) for CLI.

**Phase C — net-new panels:**
- **Skill Paths tab** (drive `SkillSystem.SpendPoint` as a branch-choice UI) — small.
- **Gear tab + equipment system** — *largest* net-new (needs an equip-slot model + persistence; there is none today). **Recommend its own WO** (WO-153) — do not inline a full gear system here; the Hero Hall ships with a "Gear — coming soon" tab placeholder if WO-153 hasn't landed.
- **Pet Store panel** — new commerce panel (mirror CosmeticShopPanel's card-list shape).

**Phase D (deferred):** Auction House — gated on backend deploy.

---

## 6. Roster placement SPEC for CLI (VillageSceneBuilder is FROZEN — do NOT edit here)

Same rule as WO-151 §6: this WO **does not touch `VillageSceneBuilder.cs`** and **fires no bake**. It SPECS for a later coordinated CLI builder pass:
- Add **Hero Hall** building (Character district, central/near the Heart plaza — it's the "great hall") with a `BuildingInteractable` → HeroHallPanel.
- Add **Pet Store** (Commerce district, beside Store + Cosmetic Store — the market square).
- Cosmetic Store: ensure it has an [F] building entry (today it's C-key only).
- District layout intent: **commerce cluster** (Store/Cosmetic/Pet Store) · **smithing quarter** (Forge/Armory, WO-151) · **production cluster** (Farm/Lumbermill/Ironworks/Crystal Mine, WO-151) · **Hero Hall** central · **Tower** on the wall line. CLI decides exact plots.

---

## Files to Create / Edit

| File | Action | Note |
|---|---|---|
| `Assets/_Modules/HUD/HeroHallPanel.cs` (+`HeroHallPanelBootstrap.cs`) | **Create** | Code-built tabbed shell (no UXML). Tabs reuse existing panels. |
| `Assets/_Modules/HUD/HeroSkillPathsTab` (or within HeroHallPanel) | **Create** | Branch-choice over `SkillSystem` tracks; calls `SpendPoint`. |
| `Assets/_Modules/HUD/PetStorePanel.cs` (+`Bootstrap`) | **Create** | Mirror `CosmeticShopPanel` card-list; acquire pets/items. |
| `Assets/_Modules/Village/Buildings/Building.cs` | **Edit** | Append `HeroHall`, `PetStore` to `BuildingType` enum (preserve 0–4 values). |
| `Assets/_Modules/Village/Buildings/BuildingInteractable.cs` | **Edit** | Re-map dispatch (§4): Tower→tower-upgrade; add HeroHall/PetStore/CosmeticStore entries. |
| `Assets/Editor/VillageSceneBuilder.cs` | **DO NOT EDIT — SPEC ONLY (§6)** | Roster additions are a future coordinated CLI pass. |
| **Gear/equipment system** | **OUT OF SCOPE → WO-153** | Hero Hall ships a placeholder "Gear" tab until then. |

---

## What NOT to touch / NOT to duplicate

- **Do NOT greenfield the stores** — `PackStore`/`PackCatalog` (main) and `CosmeticShopPanel`/`GlimmerCurrencyService` (cosmetic) EXIST (memory *monetization-stack-already-built*). Wire, don't rebuild.
- **Do NOT duplicate the talent tree, skill system, crafting inventory, or ability kit** — `TalentTreePanel`, `SkillSystem`, `VillageInventory`, `HeroAbilities`/`AbilityCatalog` all exist; the Hero Hall *hosts* them as tabs.
- **Do NOT move WO-151's weapon/armor upgrade panels into the Hero Hall** — they stay at Forge/Armory (§2c). Hero Hall only *displays* the resulting stats.
- **Do NOT build the Auction House** — deferred on backend (memory *backend-never-connected*).
- **Do NOT build a full gear/equipment system in this WO** — split to WO-153 (none exists today; it's a real system, not a wire-up).
- **Do NOT edit `VillageSceneBuilder.cs` or fire any bake/batchmode** (CLAUDE.md §3/§9; frozen single-writer).
- **Do NOT hand-edit `Village.unity`** (CLAUDE.md §3).
- **Do NOT build any panel in UXML** — code-built UI Toolkit only (PIPELINE_STATE.md §8; memory *uxml-uidocuments-dont-render-in-builds*).
- **Do NOT add a NEW `System.Reflection` bridge** — extending `BuildingInteractable`'s existing reflective switch is OK; a new reflection path is not (CLAUDE.md §10).
- **Asmdef rule (CLAUDE.md §5):** HUD → Core only (never references Village directly — Hero Hall reaches Village systems via the existing reflective bridge / `CoreServices`, exactly as `CosmeticShopPanel`/`PlayerProgressPanel` already do). Village → Core only. `?.` on all cross-module service calls.

---

## Acceptance Criteria

- [ ] IA decision table (§3) is the single source of truth for building→panel mapping; matches the roster of WO-150 + WO-151 + this WO.
- [ ] Character surfaces (inventory, gear, spells, EXP/talents, magic-path) are GROUPED into one **Hero Hall** building.
- [ ] Commerce surfaces are SEPARATE standalone buildings: Store, Cosmetic Store, Pet Store (new); Auction House DEFERRED (plot reserved, not built).
- [ ] Weapon-upgrade stays at Forge, armor-upgrade at Armory (NOT in Hero Hall) — WO-151 panels untouched.
- [ ] Magic-path choice lives in the Hero Hall "Skill Paths" tab, driving the EXISTING `SkillSystem.SpendPoint` (no forked skill system).
- [ ] `BuildingInteractable` dispatch re-mapped: Tower→tower-upgrade (NOT talents); Hero Hall→HeroHallPanel; Cosmetic Store gets an [F] entry; Pet Home still→pet tree.
- [ ] All new panels (HeroHallPanel, PetStorePanel, Skill-Paths tab) are code-built UI Toolkit — no `.uxml`.
- [ ] `BuildingType` enum extended without breaking serialized values (0–4 preserved).
- [ ] Gear/equipment correctly scoped OUT to WO-153 with a placeholder tab — not half-built here.
- [ ] Roster placement SPEC'd for CLI (§6); `VillageSceneBuilder.cs` NOT edited; no bake fired.
- [ ] No store/talent/skill/inventory/ability system duplicated; HUD→Core only; `?.` on cross-module calls; no new reflection bridge.

---

## Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passed on every `.cs` edited (HeroHallPanel + bootstrap, PetStorePanel + bootstrap, Building.cs, BuildingInteractable.cs).
- [ ] No `.unity` scene file hand-edited; `VillageSceneBuilder.cs` NOT touched; no bake/batchmode fired.
- [ ] No NEW `System.Reflection` bridge introduced (existing switch extension only).
- [ ] HUD → Core only; Village → Core only; cross-module via existing reflective bridge / `CoreServices`; `?.` everywhere.
- [ ] No new currency; commerce reuses PackStore/Glimmer; Skill Paths reuses `SkillSystem`.
- [ ] Acceptance criteria reviewed line by line.
- [ ] Owner confirms the "magic path" interpretation (SkillSystem tracks vs. spell-school subclass) before Skill-Paths tab is built (§2d flag).
- [ ] Gear system split to WO-153 (do not inline).
- [ ] `WORK_ORDER_152_city_ui_information_architecture.RESULT.md` written by CLI when complete.

---

🤖 Spec'd by the design/UX lane (UI). Reconciled by inspection against: `PackStore.cs`/`PackCatalog.cs` + `MarketplaceInteractor.cs` (main store — exists), `CosmeticShopPanel.cs`/`GlimmerCurrencyService.cs` (cosmetic store — exists), `PetSkillTreePanel.cs`/`PetSkillTreeCatalog.cs` (Pet Home = skill tree, NOT a store), `TalentTreePanel.cs`/`HeroTalentPanel.cs`/`HeroProgression.cs`/`PlayerProgressPanel.cs` (talents + EXP — exist), `SkillSystem.cs` (Blacksmith/Woodworking/Arcane path tracks + `SpendPoint`/`AvailablePoints` — the magic-path substrate, choice-UX missing), `HeroAbilities.cs`/`AbilityCatalog.cs` (spell kit — exists, no management panel), `VillageInventory.cs`/`VillageCraftingPanel.cs` (inventory = crafting larder, not equip bag), `Building.cs` (`BuildingType` enum) + `BuildingInteractable.cs` ([F] dispatch table), and WO-150/WO-151, CLAUDE.md §3/§5/§9/§10, PIPELINE_STATE.md §8, memory *monetization-stack-already-built* / *backend-never-connected* / *uxml-uidocuments-dont-render-in-builds*. Markdown work order only — no `.cs` touched, no bake fired, `VillageSceneBuilder` not edited.
