# Session Summary — 2026-06-16 (overnight + live)

All work gated headless (`COMPILE_GATE_OK`) and committed by explicit path (LFS-safe). No `git add -A`.

## Overnight batch (owner: "can you and the bots work that overnight? … go")

1. **Yarn "no node" siblings** — `8ed0bce9`. OpenUpgrade/OpenCraft/OpenEquip/OpenArena/OpenRumorBoard
   were synchronous Actions → Yarn auto-Continue()'d into an ended node → recurring
   `DialogueException: No node has been selected`. Converted all 5 to async `IEnumerator`
   (open → `yield` → `DialogueService.Stop()`), registrations recast to `Func<…,IEnumerator>`.
   Same fix as the earlier OpenShop. **Dialogue flow should no longer wedge on these commands.**

2. **Camp pivot: invade → own → build + CoC square walls** — `50b7b743`.
   - `OutpostFoundationGenerator.GenerateSquareWalledBaseRecipe(w,d,tier,rings)` — a CoC-style
     square base with N concentric **wood-wall rings** (default 2 = "two rows"). Outer ring =
     corner towers + one front **gate**; inner rings = plain walls with the gate column left
     open → a straight **entry corridor** into an interior **courtyard**.
   - `ClaimableCamp`: now a **7×7** footprint + double ring → a **3×3 buildable courtyard**.
     On BUILD it flips to a **player-owned base** immediately (`MarkOwned()` — sets Secured,
     grants the territory/economy benefit, raises `OnDefended` for existing UI). The broken
     **DEFEND counterattack is retired** (silent auto-resolve when no attackers spawned → felt
     broken). `StartDefense`/`HandleDefended` kept dead for a later re-enable.
   - Single-ring `GenerateFootprintRecipe` preserved for EnemyOutpost / Arena callers.
   - **Asset note:** `Resources/Structures/arcane tower.fbx` is ~52 KB (very lightweight) —
     candidate corner-tower swap vs `tower_ground_archer`.

3. **"Second companion ends up being ME"** — `b1fd1730`. Root cause: the canon companion roster
   is FIXED (Sylas=Ranger, Elara=Cleric, Grom=Knight), so a player who picks Ranger/Cleric/Knight
   has one roster entry that **collides with their own class** → the injector spawned a body that
   cloned the player, appearing as the party grew across loads. Fix: `Spawn()` drops the player's
   own hero class from the desired spawn set. Roster / party-frame count untouched; only the
   duplicate **body** is suppressed.

## Live feature (owner, same session): per-member equip + class/weight restrictions

Owner asks: "select which character gets gear", "play as Grom, assign Ranger's bow to him",
"armor lightweight/heavy distinction." Decisions: armor **LIGHT = Ranger+Mage / HEAVY = Knight+
Cleric**; **full scope** (selector + restriction + persisted per-member stats + weapon shown on
the companion body, re-applied each respawn).

- **Backend** — `d32a0de7`:
  - `ArmorDef.weight` + `GearCatalog.ClassWeight / ArmorFitsClass / WeaponFitsClass`; `BestArmor`
    respects the weight gate. `armor.json` (×3) tagged: cloth/aegis = any, leather = light,
    chain/plate = heavy.
  - `GearLoadout`: per-class **PlayerPrefs persistence** (`dotr-equip-weapon/armor-<class>`,
    case-normalized) so a manual equip sticks across loads for the hero AND each companion;
    `BindOwnerClass` for companion loadouts; pushes `WeaponMult` onto a sibling `StoryCompanion`.
  - `StoryCompanion._gearWeaponMult` applied at all 3 damage sites.
  - Companion bodies get a `GearLoadout` + `EquipmentController` bound to their class on spawn →
    each auto-equips its best (or persisted) gear and shows the weapon mesh.
- **Panel UI** — (commit pending gate): `EquipmentPanel` gains a **target picker** (hero +
  each live companion) above the filter tabs; selecting a member repoints the loadout, rebuilds
  the medallion (its crest/name), **filters the list to what that member's class can use**
  (weapons by job, armor by weight), and routes equip to that member's loadout. Summary line
  names the active member. Legacy `HeroEquipment` demo-def path guarded to the hero only.

## Lightweight art drops wired (owner made them via the new low-poly workflow)

- **Harvest models** — `782a7856`. `Resources/Harvest/{wood,iron,food,crystals}.fbx` (~33–156 KB)
  now render on BOTH node paths: `HarvestSite.BuildVisual` (real model on the claimed platform,
  primitive post/top kept as fallback) and `MineNodeVisual.PropPath` (repointed from the
  never-shipped `Props/Nodes/*` to `Harvest/*`). Both via `VisualFactory` (FitLargest +
  FixTripoMaterials) so they size right + render in URP; procedural silhouette stays as fallback.
- **PetHouse2** — `782a7856`. Echo Hollow (pet-house) catalog visual repointed
  `Stables_Medieval → Structures/PetHouse2` (~80 KB vs the **7.5 MB unused** `PetHouse.fbx`).
  `StructureFactory` skins via `SkinOptions.Structure`, so it auto-fits the footprint + URP-fixes
  materials. ⚠️ **Two caveats:** (1) orientation may need an eyeball on playtest (Tripo FBXs can
  import facing +X); (2) the town's Echo Hollow may be a **scene-baked** Stables instance
  (`CityManifest.json`) — the catalog repoint covers build-mode / runtime-catalog placement, a
  baked instance would need a scene rebuild to change.
- **Team UI minimized** — (commit pending gate). Town party-frame stack narrowed from the wide
  595px (portrait) / 510px (landscape) to a compact **248px** LEFT strip (`VillageHudController`
  ApplyOrientation), per "minimize the team to the left side in town — takes up too much room."

## Blink → UI (owner: "huge win", "might clear 1/3 of blockers")
- **`ac8dacc4`** — native-prop equip pipeline. `EquipmentController` gains a NATIVE path:
  `SeatNative` trusts a grip-at-origin authored prefab (scale-to-length only — no re-centre / no
  hilt-inference / no hand-axis rotation) vs the bounds-normalize path for raw Tripo/KayKit FBX.
  `knight_starter → Blink Sword1h_01` (`sword_A.prefab`), marked `Native(...)`. Old KayKit
  `sword_A.fbx` removed (backed up locally). **To add more Blink weapons: drop the `.prefab` +
  wrap the IdMap entry in `Native(...)`.** Grip/orientation wants an in-Play eyeball (editor-gated
  per WO-466); the mechanism + first sword are landed + gated.

## Overnight batch 2 (owner: "few housekeeping overnight")
1. **Load JSON → DB for weapons/armor + pull from DB** — **SPECCED as `WORK_ORDER_430`** (not
   blind-built). Reality: backend is a **Vercel REST API in a separate repo**, not a direct DB; the
   table/endpoint/seed live there. WebGL-feasible via REST + local-JSON fallback. Reverses the demo's
   local-JSON call → needs an owner decision (recommend: seed the DB now, keep local JSON
   authoritative for the demo until the endpoint is proven fast/cached).
2. **Mapping to nodes for direct harvesting** — already present: `MineNode` = walk-up + [F] extract →
   banks to the `MineResource` wallet; tonight's `MineNodeVisual` repoint gave them the lightweight
   `Harvest/<type>` models. The resource→node→model→wallet mapping is complete.
3. **Base with harvest nodes after camp clear** + 4. **static locations / OuterWorld / seam** —
   **`e2a6ac5e`**. `ClaimableCamp.SpawnHarvestNodes()` plants one direct-harvest `MineNode` per
   resource at static courtyard offsets on `MarkOwned()`. Camps already spawn at static
   `CampSystem.CampAnchors` in OuterWorld; nodes parent to that anchor (deterministic) and re-plant
   on every owned (re)load (idempotent + restore-path) → effectively persistent / seamed. Nodes are
   infinite + renewable (permanent base resource). ⚠️ Courtyard node placement wants an in-Play
   eyeball (offsets are math-derived; may overlap the OutpostHub).

### Follow-ups / notes
- **DEAD WEIGHT (recommend delete):** `Resources/Structures/PetHouse.fbx` is **7.5 MB** and now
  fully unused (nothing loads `Structures/PetHouse`; Echo Hollow uses PetHouse2). Removing it is a
  ~7.5 MB WebGL win — held for owner OK (it's in git history, recoverable). `PetHome_basecolor.JPEG`
  (~187 KB) is likely its orphaned texture — check before deleting.
- **Armor data balance (owner to tune):** light classes currently have cloth→leather→aegis only
  (thin mid-tier). Add light mid-tiers in `armor.json` (content, no recompile) if the gap matters.
- **Weapon meshes:** companion weapons use the same `Resources/Heroes/Props/Weapons/` path as the
  hero; where a KayKit mesh isn't copied yet, a tinted primitive stands in (same as the hero).
- **Overnight list: all items now addressed** (Yarn siblings, camp pivot, companion clone, team UI).
