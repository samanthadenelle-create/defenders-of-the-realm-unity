> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: shipped in 500e5b84;
> `Assets/Art/People/CraftPix/` holds 14 FBX; `CastleTownsfolkInjector.cs:85` pools
> `NPCs/CraftPixPeople/NPC_*`; guarded by `TownsfolkBodyPoolRegression.cs`.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 1003 — Replace town NPCs with the CraftPix medieval people pack

**Status:** DONE (reconciled 2026-08-08; owner felt-verification outstanding) · **Silo:** World/characters/art · **For:** CLAUDE CLI · **Date:** 2026-08-07 · ⚠ **§0's RIGGING DIRECTION WAS WRONG AND WAS CORRECTED 2026-08-20 — see the note directly below.**

> ## ⚠ CORRECTION 2026-08-20 — "link through our existing animator" put civilians in a combat stance
> §0 of this WO directs: *"retarget onto the FULL SHARED humanoid animation set … the shared NPC
> animator, `KayKitNpcIdle.controller`/`ArmIdle`."* Followed literally, that is how **14 purchased
> civilian bodies ended up playing the hero's clips**, on two independent paths:
> - `KayKitNpcIdle` plays `m-standby-idle` from
>   `Assets/Action/Knight/Motion/studio-mocap-series-magical-moves` — **the Knight's COMBAT
>   STANDBY.** Correct for a knight, wrong for a shopkeeper. The three NPC injectors stopped arming
>   CraftPix people with it in `79c1e61b` (suite `NpcIdleControllerRegression` `[npc-idle-controller]`).
> - The **default** path was worse and was missed by that first commit: `AC_CraftPixTownsfolk`'s own
>   Idle and Walk states resolved by GUID to `Assets/Action/Shared/Shared_Idle.fbx` and
>   `Shared_Walk_Forward.fbx` — **the hero's mixamo locomotion**, the same clips Knight/Cleric/Mage/
>   Ranger play. All 14 bodies share that one controller, so every vendor, every wandering villager
>   and both quest NPCs stood combat-ready and walked the hero's walk. Repointed in `9a2d1faae` to
>   the civilian Supercyan `common_people@idle` / `common_people@walk`, via the new editor entry
>   `DeNelle.Editor.CraftPixTownsfolkAnimatorSetup` (Unity's `AnimatorController` API, never a
>   hand-edit of the asset; it refuses a clip that is not imported Humanoid).
>
> **The standing rule this leaves:** *"they inherit the entire existing anim set with zero new
> clips"* is only a virtue if the existing set is the **right** set. A civilian pack needs civilian
> clips; sharing the hero's controller is a cost, not a saving. Related: **PROD-002 §0.2.**
**PO:** Samantha (owner) · **Author:** UI seat · **UI-seat block:** 1000–1099
**Owner:** picked the **CraftPix Free Medieval 3D People Low Poly** pack to replace the KayKit town NPCs with proper dressed people (the "people first, then walls" cohesion pass).

## 0. The asset (verified) + the two gates cleared
- **Source (owner's Downloads):** `C:\Users\Elden\Downloads\craftpix-net-700077-free-medieval-3d-people-low-poly-models\`
  - `fbx/people_unity/*.fbx` — **14 characters:** `peasant_1..6`, `rich_citizzens_1..4`, `city_dwellers_1..2`, `king`, `queen`.
  - `texture/people_texture_map.png` — **ONE shared atlas** for all 14 → one material, one draw call (mobile-ideal).
- **License (owner-confirmed GREEN, 2026-08-07):** CraftPix freebie §2.1 — "use in any number of personal and commercial projects," "modify and include in game projects," "sell and distribute games with our assets," **no attribution required.** Only prohibits reselling the raw art files (N/A). Safe to ship.
- **RIGGING — link through our existing animator (owner direction).** Import each FBX as **Humanoid**; then **retarget onto the FULL SHARED humanoid animation set** (owner: "we have the full animation shared" — the shared NPC animator, `KayKitNpcIdle.controller`/`ArmIdle` + the shared clip library). Humanoid retarget means the CraftPix people inherit the entire existing anim set (idle/walk/talk) with **zero new clips**. **Mixamo is only a fallback IF a FBX carries no skeleton at all** (verify on import); the primary path is our animator.

## 1. What the town uses today (grounded — the audit)
Two spawner families to repoint:
- **Vendor/storefront NPCs (the KayKit bodies):** `CastleVendorNpcInjector.cs` → `KayKitNpcBody.Load(catalogId)` reads `repo.npcModel` (a KayKit slug) from `Assets/Resources/Data/Canonical/structures-catalog.json` → loads `Resources/NPCs/KayKit/<slug>.fbx`. 12 slugs (Cleric, Druid, Engineer, Hoarder, Farmer_A/B, Barbarian, BlackKnight, Tiefling, Mage, Ranger, Paladin). Shared `KayKitNpcIdle.controller`.
- **Wandering townsfolk (CGTrader 4 civilians):** `CastleTownsfolkInjector.cs` + `AmbientNPC.cs` → `Resources/NPCs/NPC_{Peasant_Mevina,Peasant_Tob,Merchant,Blacksmith}.prefab`.

## 2. Do
1. **Stage into the tracked pipeline (survives clone).** Copy `fbx/people_unity/*.fbx` + `texture/people_texture_map.png` into a **git-tracked** stage: `Assets/Resources/NPCs/People/` (mirror the tracked `Resources/NPCs/KayKit/` pattern — do NOT point at a gitignored path). Import each FBX **Humanoid**; extract/build **one URP/Lit material** off the shared atlas (verify no magenta under URP).
2. **Animate.** If rigged, retarget to the shared NPC idle/walk controller (reuse `KayKitNpcIdle.controller`/`ArmIdle`). If static, Mixamo-rig + a simple idle/walk set, then the shared controller.
3. **Repoint the vendors.** Remap the 12 `structures-catalog.json` `npcModel` slugs to CraftPix characters (or swap `KayKitNpcBody` to a new `PeopleNpcBody` resolver pointing at `Resources/NPCs/People/`). **Role mapping:**
   - **Merchants / shopkeepers / notable vendors** → `rich_citizzens_1..4` (well-dressed reads as traders).
   - **Laborer / production vendors** (farm, mine, workshop) → `peasant_1..6`.
   - **Generic / civic vendors** → `city_dwellers_1..2`.
   - Keep each vendor's identity sensible per its building.
4. **Repoint the wandering townsfolk.** `CastleTownsfolkInjector` draws its 5 wanderers from a **pool of the peasants + city dwellers** (the common crowd); retire the 4 CGTrader bodies (or fold them in). Vary the pool so the crowd isn't clones.
5. **King + Queen** → reserve for a **notable placement** (e.g. a castle/throne-area pair, or a special quest/vendor NPC), not the generic wander pool — they're the flavor pieces.

## 3. Acceptance
**Felt (owner closes):**
- [ ] Town is populated by **dressed medieval people** — peasants, rich citizens, city dwellers — not KayKit adventurers (no more paladin/ranger as shopkeepers).
- [ ] The crowd reads varied (multiple looks, not one repeated body); king/queen placed as notable, not wandering the market.
- [ ] Characters are **animated** (idle + wander), sit correctly on the ground, face/scale correct.
- [ ] Style is cohesive with the low-poly world (matches the new terrain; sets up the coming wall upgrade).
**Engineering:**
- [ ] Assets in the **tracked** `Resources/NPCs/People/` stage (survives a clean clone); one shared URP material (one draw call); no magenta.
- [ ] Vendor + townsfolk spawners repointed; the KayKit/CGTrader town bodies retired from the town path (hero body chain untouched — `HeroBodySwapper` stays Blink).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK` — **headless-capture the hub, open the PNG**, confirm the new people + variety before handing to owner.

## 4. Not this WO
- The hero stays on Blink (`HeroBodySwapper`) — this is TOWN NPCs only.
- Castle walls are next (separate WO, after the people land).

## 5. RESULT
`WorkOrders/WORK_ORDER_1003_town_npcs_craftpix_medieval_people.RESULT.md` — the rig-status finding, the role→character mapping used, and a hub screenshot showing the new townsfolk.
