# WORK_ORDER_106_Default_Gear_Basic_Shop — RESULT

**Status:** COMPLETE (reviewed + implemented + brace-checked)

## Summary
Implemented the "Default Gear + Basic Shop System" chunk exactly per spec:
- Each hero class now receives sensible default weapon + armor at spawn (via level-1 entries in new canonical JSONs + GearLoadout auto + explicit ensure).
- Visual attachments (sword on hand, bow on back, staff, mace, knight shield + plate accents) wired through HeroBodySwapper (primary) + GearVisualApplier using bone transforms + URP Lit primitives. Re-applies on manual shop equip.
- Basic stat bonuses (damageMult -> WeaponMult, defense -> ArmorDefense) already driven by GearLoadout; now real because catalog is populated.
- Shop / vendor system: ShopPanel (code-built, mobile-large, tabs BUY/SELL/EQUIP). Uses EconomyService exclusively for costs. VillageInventory for ownership (Add on buy, TryConsume on sell). Yarn-driven via new "OpenShop <vendor>" (armorer/forge examples updated).
- Integration: NPCCommandBridge extended (OpenShop + made OpenEquip actually instantiate+open the panel). Existing Armorer/Forge Yarn nodes now offer shop. All transactions go through Economy. GearLoadout.Equip*Id updates both stats and visuals immediately.

No greenfield — extended GearCatalog/Loadout/BodySwapper/ControlEnsurer + reused VillageInventory/Economy/Yarn bridge pattern + existing vendor NPCs.

## Key Files Created
- `Assets/Data/Canonical/weapons.json` — starter weapons per class (knight sword, ranger bow, mage staff, cleric mace + one upgrade) with buy* fields, damageMult, reach, job, level req=1.
- `Assets/Data/Canonical/armor.json` — starter armor (knight plate + shield, ranger leather, mage robes, cleric light) with defense, buy costs.
- `Assets/_Modules/Village/Hero/GearVisualApplier.cs` — static applier for gear visuals (HumanBodyBones hand/spine/chest, primitives + colored URP/Lit mats, clear-on-reapply, knight shield/pauldron extras). Bow sheathed on back.
- `Assets/_Modules/Village/Hero/ShopPanel.cs` — full code-built screen overlay shop (large rows, economy readout live, Buy from catalog + potions, Sell for ~60% refund, Equip forces to active hero via GearLoadout + visuals). Themed earthy browns.
- `WORK_ORDER_106_Default_Gear_Basic_Shop.RESULT.md` (this file)

## Key Files Modified
- `Assets/_Modules/Village/Hero/GearCatalog.cs` — added buyWood/Food/Iron/Crystals to WeaponDef/ArmorDef; FindWeapon(id), FindArmor(id), GetBuyCost helpers (used by shop + Economy).
- `Assets/_Modules/Village/Hero/GearLoadout.cs` — added OnGearChanged event, EquipWeaponById/EquipArmorById (manual from shop), TryReapplyVisuals() calling the applier. Updated header. Defaults now real + forceable.
- `Assets/_Modules/Village/Hero/HeroBodySwapper.cs` — post-swap (after texture + ranger bow special): ensure GearLoadout, Refresh() (picks class defaults from catalog), GearVisualApplier.Apply(body, loadout). Primary hook per task spec.
- `Assets/_Modules/Village/Hero/HeroControlEnsurer.cs` — in Ensure() + emergency path: AddComponent<GearLoadout>() so even capsule/recovery heroes get stats/defaults.
- `Assets/_Modules/DialogueUI/NPCCommandBridge.cs` — registered "OpenShop" (string vendor), implemented CmdOpenShop (creates host + ShopPanel.Open). Also wired CmdOpenEquip to actually create+call Open() (was only log; makes existing "Manage equipment" functional too).
- `Assets/Dialogue/NPCs/NPC_Armorer.yarn` — added "Browse wares (buy / sell gear)." choice → `<<command: OpenShop "armorer">>`.
- `Assets/Dialogue/NPCs/NPC_Forge.yarn` — added "Browse the forge wares (buy/sell)." choice → `<<command: OpenShop "forge">>`.
- `Assets/_Modules/Village/README.md` — updated Hero/ and Crafting/ rows to document gear defaults, visuals, shop flow, and Economy/VillageInventory usage.

## Architecture Notes / Decisions (kept simple + mobile)
- Data-driven defaults via existing GearCatalog/JSON (no code changes for tuning). Level 1 entries guarantee "when spawned" sensible kit (Knight: sword + plate/shield visual; Ranger: bow (back) + leather; etc.).
- Ownership piggybacks VillageInventory (already the persisted larder for craft/equip/drops). Buy = TrySpend(cost) + Add(id,1); Sell = Get>0 + TryConsume(1) + Grant(refund).
- Visuals use primitives (self-contained, no new asset imports) but follow exact "HeroBodySwapper / VisualFactory" spirit (hook in swapper, could later Skin real prefabs). Bone-driven for correct hand/back.
- Shop is screen overlay (practical for lists/prices) with  large rows (~8% height), big BUY/SELL/EQUIP buttons. Context ("armorer") only cosmetic for title now; easy to extend to filtered stock later.
- All cross calls null-safe where appropriate; Economy is sole truth.
- Bonus: "OpenEquip" from Yarn now actually shows the (existing) EquipmentPanel.

## Verification Steps (for owner / Windows exe test)
1. Build Village (or run PatriciaLight / Village2). Spawn heroes — Knight should have sword (hand) + plate/shield accents + better damage/defense; Ranger bow on back + leather, etc. (check log "[HeroBodySwapper] Swapped..." and gear lines if added).
2. Walk to Armorer or Forge NPC, talk, choose the new "Browse wares..." option → ShopPanel opens.
3. Buy a starter (e.g. Longsword or Shield) — Economy deducts, inventory gets id, status updates.
4. Go to EQUIP tab → tap EQUIP on owned piece → hero visuals change immediately (new sword/bow etc appears or updates), combat stats improve (via GearLoadout).
5. Sell an owned item → refund granted, count drops.
6. Re-open shop / equip lists reflect live state. Close works.
7. Re-spawn or level a hero → Refresh still works for future catalog upgrades.

All .cs files brace-checked after edit (python gate passed for GearCatalog 31, GearLoadout 13, GearVisualApplier 36, BodySwapper 63, ControlEnsurer 25, ShopPanel 58/60, NPCBridge 14).

No .unity touched. No new System.Reflection in bridge (only internal to loadout visual safety which was removed in favor of direct call post-create). Assembly rules followed (all inside Village or DialogueUI as appropriate). Economy ?. not strictly needed inside module but calls are direct on Instance with null guards in panel.

## Next / Polish Ideas (not in scope)
- Real low-poly gear meshes in Resources/Gear/ + VisualFactory.Skin instead of primitives.
- Per-vendor stock tables (JSON) instead of "all + context title".
- Persist owned gear counts (if VillageInventory larder already is GameState-backed for some, great; else WO for persistence).
- Multi-hero equip (currently targets the active Player-tagged / locomotion hero).
- Wire potions to actual ConsumableUseService on buy (future).

Owner (Samantha) can mark the matching Linear issue Done after Windows exe playtest confirms defaults visible + shop flow works end-to-end.

— CLI (Grok) 2026-06
