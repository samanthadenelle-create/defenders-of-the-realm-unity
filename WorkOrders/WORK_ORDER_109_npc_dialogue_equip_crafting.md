> ⚠ **NUMBER COLLISION — this document does not own WO-109; `WORK_ORDER_109_rampart_level_wall_towers.md` does.**
> Referred to hereafter as **WO-109-B (NPC dialogue / equip / crafting)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK_ORDER_109 — NPC Dialogue (Yarn Spinner) + Basic Equipment & Crafting Foundation

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: Yarn Spinner absent from tree; in-house Core/Dialogue/DialogueModel.cs; crafting owned by the WO-293 line)

**Context:** Builds on WO-108 (Castle Last Bastion with stationed NPCs via NPCUpgradeStation and districts), WO-106 (Economy as source of truth), previous builder wiring, HeroBodySwapper/VisualFactory for visuals, existing Crafting/VillageInventory/Item foundation, Dialogue/ .yarn setup and DialogueUI command bridges.

**Focus:** End-to-end flow: Approach/talk to stationed NPC → Yarn Spinner dialogue → choices lead to craft/upgrade/equip → consume/produce via Economy → equip on hero with visual change (body swap/attach) + stat effect → feedback.

**Non-negotiables (Claude.md):**
- Navigation reads first (done).
- No .unity hand-edits; builder for placements.
- Brace gate after every .cs (python exact).
- EconomyService for all resource consume/produce (Grant negative or TrySpend for costs; AddResource/Grant for outputs if applicable). No duplicate logic.
- Code-built UIs preferred (UXML doesn't work in builds).
- Reuse: Yarn existing (DialogueRunner, command handlers like IntroCommandBridge, .yarnproject), HeroBodySwapper/VisualFactory for visuals, GearLoadout/EquippedWeapon for gear base, VillageCraftingPanel/CraftingRecipeCatalog/VillageInventory for crafting base, NPCUpgradeStation for upgrade, builder for placing.
- Village → Core only.
- Update module READMEs/indices when adding.
- Mobile: Code UIs, performant.

## Architecture Proposal

**1. Yarn Spinner for NPCs (all stationed: Mill, Armorer, Forge, Lumbermill, Resource Upgrade, Jeweler etc.):**
- Leverage existing: Assets/Dialogue/DefendersDialogue.yarnproject, DialogueRunner (attach at runtime), LineAdvancer for tap/click advance (mobile friendly per Yarn notes), RPG-style presenter if present or default.
- Per-NPC or shared .yarn files in Dialogue/NPCs/ (e.g. NPC_Forge.yarn, NPC_Armorer.yarn). Nodes like:
  ```
  title: Talk
  ---
  Hello, adventurer. I can help you craft or upgrade gear.
  -> Let's craft a weapon.
    <<command: OpenCraft "forge">>
  -> Upgrade your armor.
    <<command: OpenUpgrade "armorer">>
  -> Never mind.
  ===
  ```
- Commands: Extend pattern from IntroCommandBridge (AddCommandHandler via IActionRegistration on runner). New NPCCommandBridge registers "OpenCraft", "OpenUpgrade", "OpenEquip", "LearnRecipe" etc. These open the relevant code-built panels or call station methods, passing context (e.g. "forge" filters recipes).
- Trigger: On NPC station (proximity or interact via BuildingInteractable or new trigger on the placed NPC object), start the DialogueRunner with "Talk" node. Use existing NPCUpgradeStation collider or extend to start runner instead of (or before) direct UI.
- Unlocks: Yarn <<set $unlockedForgeSword = true>> or command "LearnRecipe sword". UI/craft filters by learned (via variables or a known recipes list in a manager).
- Integration: Dialogue can lead directly to upgrade (for buildings) or craft/equip. For end-to-end, one dialogue path: talk → craft weapon → get item → "Now equip it?" → open equip panel.
- Builder wiring: In PlaceNpcStation (Content.cs) or Characters partial, after creating the NPC GO + station, add/find DialogueRunner, set .yarnProject (load from Resources or asset), register any local handlers, set initial node. Reuse existing dialogue input actions.
- Mobile/advancing: Configure LineAdvancer with Pointer press for hurry/advance (as per Yarn notes).

**2. Basic Equipment System (Weapons & Armor for Knight/Ranger/Mage/Cleric):**
- Data structure: Extend Items/ or add in Core (but keep Village for now): Simple ItemDef (or reuse/extend from crafting outputs) with slot (Weapon/Armor), visualRef (prefab path or bone attach name), bonuses (e.g. reachBonus, damageBonus as float). Use the existing "bare-bones item collection" (ItemInventory facade over VillageInventory, or add equippables as special ids).
- Runtime: Extend or new HeroEquipment (on hero GO, alongside GearLoadout which already handles EquippedWeapon for reach in PlayerAttackController).
  - Slots: enum EquipmentSlot { MainHand, Armor }
  - Equip(ItemDef item): Unequip old, store, apply bonuses (e.g. modify PlayerAttackController._attackRange or a stats multiplier; simple additive for demo), trigger visual.
  - Visual: 
    - Weapons: Find bone (e.g. "RightHand" or via HeroBodySwapper body transform after swap), Instantiate visual prefab (from Resources/Items/ or catalog), parent/position/rotate to hand. Use VisualFactory if skinned.
    - Armor: On body swap or post, swap/replace mesh parts or apply via HeroBodySwapper extension / material swap. For demo, scale or tint the body, or attach overlay.
  - HeroBodySwapper integration: After body load, re-apply any equipped visuals (idempotent).
  - Stats: Bonus applied to attack (range/damage in PlayerAttackController) or defense (if HeroHealth has). Persist via GameState or simple.
- UI: New code-built EquipmentPanel (Canvas + buttons like NPCUpgradeStation and prior modals; no UXML). Accessible from NPC dialogue command "OpenEquip" or hero interact. Lists inventory items filterable by slot, "Equip" button calls HeroEquipment. Show current equipped + bonuses.
- Inventory tie: Items added to VillageInventory/ItemInventory as ids (e.g. "basic_sword"). Equip resolves id to def.

**3. Crafting Workshop Foundation (workshops/forge/jeweler functional for equippables):**
- Recipe system: Build on existing CraftingRecipeCatalog (JSON data-driven, RecipeDef with ingredients as counts). Extend recipes.json conceptually (or in code for demo) to have "equipment" outputs: e.g. recipe "forge_sword" with ingredients (iron:5, wood:2), output "basic_sword" (flagged equippable).
- UI: Enhance/use VillageCraftingPanel (existing UI Toolkit; for builds provide code fallback or note). Context from NPC (e.g. "forge" station shows only forge recipes). Button "Craft" : check Economy (map ingredients to Economy resources like Iron/Wood via simple dict or direct TrySpend on ResourceCost equivalent; consume via Grant negative or VillageInventory but prefer Economy per rules), produce item (add to inventory), show toast.
- Unlocks/Learn: Via Yarn (command or variable "learned_sword_recipe"). Crafting UI only shows known + always-available. Recipes learned in dialogue (e.g. "I'll teach you this recipe" sets flag).
- Tie to Economy: All costs via EconomyService.Instance.TrySpend (or AddResource negative for demo). Outputs can grant bonus resources or just the item.
- Workshops: The placed district buildings (Forge etc.) have the NPC that opens the contextual crafting. BuildingInteractable already routes Workshop to Crafting.

**End-to-End Example Flow (Forge NPC):**
- Approach Forge NPC (placed in Artisan district) → trigger starts Yarn "Talk_Forge".
- Dialogue: "I can forge weapons. Want to craft a basic sword?" → choice "Yes" → <<command: OpenCraft "forge">> (or "LearnRecipe sword" first).
- Opens crafting UI filtered to forge recipes. Select "basic_sword" (cost 5 Iron via Economy).
- Craft: Economy.TrySpend (iron), add "basic_sword" to inventory, visual "forged!" toast.
- Dialogue continues or separate "Now equip?" → command OpenEquip.
- Equipment panel: See "basic_sword" in list, Equip → HeroEquipment equips, attaches sword visual to hero hand (post body swap), +2 reach or damage in attack controller.
- Hero now has visible weapon, better combat.

**Files (see list in implementation):**
- New .yarn in Dialogue/NPCs/ for key stations.
- New/Modify command bridge for NPC commands.
- New HeroEquipment.cs, EquipmentPanel.cs (code UI).
- Modify: NPCUpgradeStation, VillageCraftingPanel/Catalog/Inventory (for equipment outputs + Economy consume), builder (attach runners, Place for visuals), HeroBodySwapper (re-apply visuals), PlayerAttackController (use bonuses).
- Update READMEs, yarnproject if needed, indices.

This is minimal viable, data-driven where possible, reuses heavily, mobile (code UI, simple attach), end-to-end testable in village after builder run.

Increment WO. Owner final on exact recipes/visuals/dialogue text. 

Ready to implement after proposal.