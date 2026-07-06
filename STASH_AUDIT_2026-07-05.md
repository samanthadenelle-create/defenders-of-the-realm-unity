# STASH AUDIT — 2026-07-05 — `stash@{0}` (WIP 2026-06-30 un-stack + overnight features)

> Read-only audit. Nothing applied/popped/dropped. Compares the 52 stashed files against
> current HEAD (`1afde341`) on `wip/village2-and-f8-tickets`.
> Method: `git diff stash@{0} HEAD -- <path>` (empty = identical/landed) + HEAD-signature
> greps + scene/flag existence checks.

## TL;DR
- The **feature half** of the stash (potions/mana-draught, talents, instrumentation, data files)
  **ALREADY LANDED** in HEAD — and HEAD moved *further* (WO-598 vendor prices, +12 feature flags,
  AutoPilot rewrites). Re-applying the stash would **REVERT** that newer work.
- The **un-stack half** (WO-453) is **OBSOLETE**: the world moved to the **WO-608 single-scene
  merge** (`ff.mergedworld` default-ON in HEAD; `OuterWorld.unity` **DELETED**; `Main_Castle_Overworld.unity`
  is the live scene; FeatureFlags comment: *"the seam infrastructure is now DEAD CODE"*). The
  cluster also **won't compile** — it references `DeNelle.Core.World.WorldGeometry.OuterWorldOffset`,
  a symbol that is in **neither the stash nor HEAD** (WorldGeometry.cs was never captured here).
- **STILL-RELEVANT / pending: ZERO files.**
- **RECOMMENDATION: (a) DROP the stash entirely.** WO-453's *intent* (7.1km encounter-return strand)
  stays an open owner decision, but must be **re-authored against the merged world** — this stash is
  a dead vehicle for it.

## Evidence anchors
- `ProjectSettings/EditorBuildSettings.asset`: stash lists `OuterWorld.unity`; HEAD **removed it and
  added `Main_Castle_Overworld.unity`**. `git cat-file -e HEAD:Assets/Scenes/OuterWorld.unity` → **GONE**.
- `FeatureFlags.cs`: stash 33 flags, HEAD 45 (strict superset; +`MergedWorld`,`WorldFeel`,`TutorialV2`,
  `CombatFeel`,`MocapLocomotion`,`HeroPackage`,… — **none stash-only**). `MergedWorld => defaultOn:true`.
- `consumables.json`/`materials.json`: HEAD has WO-598 `"price"` fields; stash **lacks** them (stash older).
- `HeroAbilities.RestoreManaOverTime`/`RestoreManaToFull`, `ConsumableUseService.RestoreManaOverTime`,
  `Tower` FlowTrace, `BattleArena` = present in HEAD (LANDED); HEAD line counts all exceed the stash.
- Un-stack `.cs` (ZoneManager/RuntimeRegionGate/OuterWorldBoundaryInjector/MineNodeVisual/RaidOutpost/
  ExteriorTerrainBuilder/OuterWorldCavePortalBuilder) all reference `WorldGeometry.OuterWorldOffset`
  → **CS0103 if re-applied** (missing type).
- `SESSION_CANON_LOADER.md`: stash = 06-30 "un-stack is current work"; HEAD = 07-03 FEEL ARC header
  that itself records "Seam un-stack PARKED in stash@{0}". Re-applying reverts canon.

## Per-file table

| # | File | Status | Reason |
|---|------|--------|--------|
| 1 | AddressableAssetsData/link.xml | ALREADY-LANDED | Byte-identical to HEAD (0-diff) |
| 2 | AddressableAssetsData/link.xml.meta | ALREADY-LANDED | Identical (0-diff) |
| 3 | Resources/Arena/Dwarven_Ground.mat | ALREADY-LANDED | Identical (0-diff) |
| 4 | Resources/Arena/Materials/ArenaGround.mat | ALREADY-LANDED | Identical (0-diff) |
| 5 | Resources/.../consumable-recipes.json | ALREADY-LANDED | Identical (0-diff) |
| 6 | Resources/.../cosmetics.json | ALREADY-LANDED | Identical (0-diff) |
| 7 | Resources/.../loot-tables.json | ALREADY-LANDED | Identical (0-diff) |
| 8 | Resources/.../packs.json | ALREADY-LANDED | Identical (0-diff) |
| 9 | StreamingAssets/.../consumable-recipes.json | ALREADY-LANDED | Identical (0-diff) |
| 10 | StreamingAssets/.../cosmetics.json | ALREADY-LANDED | Identical (0-diff) |
| 11 | StreamingAssets/.../loot-tables.json | ALREADY-LANDED | Identical (0-diff) |
| 12 | StreamingAssets/.../packs.json | ALREADY-LANDED | Identical (0-diff) |
| 13 | Village/DeNelle.Village.asmdef | ALREADY-LANDED | Identical (0-diff) |
| 14 | Village/Talents/HeroSkillTreeVM.cs | ALREADY-LANDED | Identical (0-diff) — the +50 landed |
| 15 | run-autopilot-fleet.ps1 | ALREADY-LANDED | Identical (0-diff) |
| 16 | Village/Hero/HeroAbilities.cs | ALREADY-LANDED (superseded) | RestoreManaOverTime/ToFull in HEAD; HEAD ahead — re-apply reverts |
| 17 | Village/Items/ConsumableUseService.cs | ALREADY-LANDED (superseded) | Mana-draught wiring in HEAD; HEAD ahead |
| 18 | Village/Items/ConsumableCatalog.cs | ALREADY-LANDED (superseded) | HEAD added WO-598 `Price` field; stash older |
| 19 | Resources/.../consumables.json | STALE | HEAD has WO-598 `price`; stash lacks it — re-apply reverts pricing |
| 20 | Resources/.../materials.json | STALE | HEAD has WO-598 `price`; stash lacks it |
| 21 | StreamingAssets/.../consumables.json | STALE | Same — stash pre-WO-598 |
| 22 | StreamingAssets/.../materials.json | STALE | Same — stash pre-WO-598 |
| 23 | Village/Buildings/Tower.cs | ALREADY-LANDED (superseded) | FlowTrace in HEAD; HEAD ahead (1281 vs 1272) |
| 24 | Village/Arena/BattleArena.cs | ALREADY-LANDED (superseded) | In HEAD; HEAD ahead (1878 vs 1758) |
| 25 | Core/FeatureFlags.cs | ALREADY-LANDED (superseded) | HEAD 45 flags ⊃ stash 33; re-apply drops 12 flags |
| 26 | Core/CoreServices.cs | ALREADY-LANDED (superseded) | HEAD carries equivalent; re-apply reverts newer |
| 27 | DevTools/AutoPilotDriver.cs | ALREADY-LANDED (superseded) | HEAD 3929 vs stash 2270 lines — massively ahead |
| 28 | DevTools/AutoPilotProbes.cs | ALREADY-LANDED (superseded) | HEAD 959 vs stash 888 — ahead |
| 29 | Resources/RpgUi/button/button_confirm.png.meta | ALREADY-LANDED (superseded) | Meta re-import; HEAD current |
| 30 | SESSION_CANON_LOADER.md | STALE | Stash = 06-30 header; HEAD = 07-03 FEEL ARC — re-apply reverts canon |
| 31 | Editor/CastleHubBuilder.cs | STALE | Un-stack/seam builder; seam is DEAD CODE post-WO-608 merge |
| 32 | Editor/ExteriorTerrainBuilder.cs | STALE | References `WorldGeometry.OuterWorldOffset` (missing) → won't compile |
| 33 | Editor/OuterWorldBuilder.cs | STALE | Builds now-deleted OuterWorld; WorldGeometry dep |
| 34 | Editor/OuterWorldCavePortalBuilder.cs | STALE | WorldGeometry offset dep; targets merged-away world |
| 35 | Editor/OuterWorldNavBake.cs | STALE | Bakes deleted OuterWorld scene |
| 36 | Editor/WorldBakeOrchestrator.cs | STALE | Orchestrates the obsolete two-scene bake |
| 37 | Generated/Terrain/ExteriorTerrainData.asset | STALE | Generated binary — never stash-apply; must rebake; 06-30 bake weeks stale |
| 38–42 | Generated/Terrain/Exterior_{Dead,Grass,Mud,Snow,Stone}.terrainlayer | STALE | Terrain layer tweaks for the obsolete bake |
| 43 | Scenes/MainCastle_Hall.unity | STALE | 9682-line 06-30 bake; superseded by 07-02→05 rebakes + WO-608 merge — re-apply nukes owner-felt world feel |
| 44 | Scenes/MainCastle_Hall/NavMesh-NavMeshSurface.asset | STALE | Generated binary — rebake, never stash-apply |
| 45 | Scenes/OuterWorld.unity | STALE | **Scene DELETED at HEAD** (merged into Main_Castle_Overworld) |
| 46 | Scenes/OuterWorld/NavMesh-OuterWorld.asset | STALE | Navmesh for a deleted scene |
| 47 | Core/World/ZoneManager.cs | STALE | Frame-aware `ResolveLocal` + WorldGeometry dep; merged world removed the frame split → won't compile |
| 48 | Village/World/RuntimeRegionGate.cs | STALE | `_landingWorld = ToWorld+OuterWorldOffset`; seam DEAD CODE; WorldGeometry dep |
| 49 | Village/World/OuterWorldBoundaryInjector.cs | STALE | WorldGeometry offset dep; obsolete |
| 50 | Village/World/MineNodeVisual.cs | STALE | Offset-aware node placement for the un-stacked world |
| 51 | Village/World/Camps/RaidOutpostSystem.cs | STALE | WorldGeometry offset dep |
| 52 | ProjectSettings/EditorBuildSettings.asset | STALE ⚠ RISK | Re-adds DELETED OuterWorld.unity + removes Main_Castle_Overworld.unity → **build abort** |

## Bucket summary
- **ALREADY-LANDED: 29** (15 byte-identical + 14 landed-but-HEAD-moved-further)
- **STILL-RELEVANT / PENDING: 0**
- **STALE / OBSOLETE: 23** (18 un-stack cluster + 4 pre-WO-598 data files + 1 canon loader)

*(Files 19–22 and 30 land in the STALE bucket because re-applying actively reverts newer HEAD work,
not merely a no-op.)*

## Recommendation — (a) DROP THE STASH
Nothing in `stash@{0}` is uniquely worth salvaging:
- The feature work is in HEAD and HEAD is ahead — re-applying regresses vendor pricing, flags, AutoPilot.
- The un-stack cluster is obsoleted by the **WO-608 merged world** (`ff.mergedworld` ON, OuterWorld.unity
  deleted) **and** structurally broken (missing `WorldGeometry.cs` → CS0103).

**Easy vs. right:** *Easy* = leave it parked "just in case." That's the wrong call — the stash now
references a **deleted scene** and a **missing type**, rots the tree's mental model, and any accidental
`git stash pop` corrupts EditorBuildSettings + reverts the merged world. *Right* = **drop it** and, if the
owner un-parks **WO-453** (the still-open 7.1km encounter-return strand — publisher critique #1), spec it
**fresh against `Main_Castle_Overworld.unity`**. Dropping the stash does **not** close WO-453; it retires a
stale vehicle.

Suggested (owner-gated) command, for the record — **do not run without owner OK**:
`git stash drop stash@{0}`

## Risk flags
1. **EditorBuildSettings.asset (file 52) — HIGH.** Re-applying re-adds the deleted `OuterWorld.unity` and
   drops `Main_Castle_Overworld.unity` → every build aborts (memory `deleting-scene-requires-editorbuildsettings-cleanup`; that exact class of bug cost 3h on WO-608). The compile gate would NOT catch it.
2. **Compile break.** The un-stack `.cs` files reference `WorldGeometry.OuterWorldOffset` — absent from
   stash and HEAD. A partial re-apply = red compile.
3. **Data silent-revert.** consumables/materials.json stash versions predate WO-598 `price` — re-apply
   silently removes vendor shelf prices with no compile error.
4. **Generated binaries (scenes/navmesh/terrain).** Never stash-apply; they are re-baked artifacts and the
   06-30 bakes predate the owner-felt-approved 07-02→05 world-feel work. Re-applying = visual regression.
5. **§0 mount-garble.** N/A to this read-only audit, but if WO-453 is ever re-authored, those `.cs` edits
   must be written by CLI on the Windows path (Write/Edit), never via the Linux mount.
