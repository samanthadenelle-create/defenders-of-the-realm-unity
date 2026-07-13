# STRUCTURE TRANSFORM CENSUS — Ballista / `tower_wall_wizard` / `Structures/WizardTower_1.fbx` — 2026-07-08

> Owner directive: "i want to see everything that happens to it from selecting fbx to placement in game."
> Read-only, code-verified, file:line cited (agent census, 2026-07-08). Modeled on
> `docs/WEAPON_TRANSFORM_CENSUS_2026-07-07.md`. Companion runtime instrument: the `[Flow:Xform]`
> value-trace in `VisualFactory.Skin` prints the actual euler/pos/scale after every stage below.
> Concrete asset at census time: catalog id `tower_wall_wizard` (displayName "Ballista"),
> `visualPrefabPath: "Structures/WizardTower_1"`, `repo.visualHeight = 5.0`,
> `orientation{ manual:true, euler:[-90,0,0] }`, `placement.footprint = 2.0`,
> `upgradeVisualPath:["Structures/Tower_Medieval_Big"]`.

## HEADLINE FINDINGS

**1. The final on-screen pose is a THREE-TRANSFORM stack, not one.** Root (world yaw) → visual child
(fit-scale + seat, then orientation rotate + reseat) → and at tier ≥ 2 a SECOND scale multiply on the
ROOT via `StructureTierVisual`. The catalog euler is applied to the *visual child's* `localRotation`;
the placement yaw is applied to the *root*. Different transforms — they compose rather than fight.

**2. Fit/scale and seat are computed on the UN-CORRECTED (lying-down) mesh, THEN the mesh is rotated
upright, THEN re-seated.** `VisualFactory.Fit` (height=5) and `SeatOnGround` run at
`VisualFactory.cs:142-152` BEFORE `StructureFactory` applies `orientation.Euler` at
`StructureFactory.cs:122`. `Fit` measures `bounds.size.y` of the raw pre-correction mesh
(`VisualFactory.cs:245`), so the "5 m tall" target is measured on the wrong axis for a model that only
stands up after the pitch. `ReseatCorrectedBottom` (`StructureFactory.cs:137/408`) fixes the vertical
float afterward but does NOT re-fit — the height error is baked in. **Primary order-dependent risk.**

**3. The ghost and the placed object apply scale DIFFERENTLY.** `GhostPreview` applies only the uniform
`_orientation.scale` (`GhostPreview.cs:96-97`); `StructureFactory`/loader apply the per-axis
`EffectiveScale` (`StructureFactory.cs:128-129`). Identity for the Ballista today — but any Orient-tool
`scaleAxis` dial makes the ghost lie about the placed size.

**4. Import contributes IDENTITY pose.** `WizardTower_1.fbx.meta`: `bakeAxisConversion:0`,
`globalScale:1`, `useFileScale:1`, `useFileUnits:1`, no rotation bake. All orientation comes from the
catalog. `TripoAssetPostprocessor` matches `Resources/Structures/` but touches MATERIALS only.

**5. Fresh placement is ALWAYS level 1** (`Place` hardcodes `level=1`) — `ReskinForLevel` skipped,
`StructureTierVisual.Apply(1)` is a ×1.0 no-op. Tier scaling and the L2 model swap enter only on
upgrade/reload at level ≥ 2.

**6. Persistence stores NO transform** — only `itemId/cell/yawSteps/level/yawOffset/worldY/wallMounted`
(`PlacedStructureData.cs:37-88`). Every mesh-space transform is RE-DERIVED from the catalog on load.

## STAGE 1 — FBX IMPORT (`WizardTower_1.fbx.meta`)
- `:36` `globalScale: 1`; `:50` `useFileUnits: 1`; `:71` `useFileScale: 1` — identity multipliers.
- `:53` `bakeAxisConversion: 0` — native FBX axes kept; uprighting is downstream (catalog euler).
- `TripoAssetPostprocessor.cs:66` matches `Resources/Structures/`; `:88-105 OnPreprocessModel` sets
  material import fields ONLY; `:116-195` texture extraction only. **No transform effect.**

## STAGE 2 — CATALOG (`structures-catalog.json` entry)
- `repo.visualHeight: 5.0` → `StructureFactory.Create:83-88` (FitHeight), `OptsFor:233-238`,
  `GhostPreview.SetEntry:72-77`, `MeasureUprightFootprintMetres:467-468`.
- `placement.footprint: 2.0` → fit fallback (`Create:91-94`), grid cells (`BaseLayoutLoader.Spawn:257`).
  Footprint = collider/nav size, not the visible mesh.
- `orientation{manual,euler,offset,scale,scaleAxis}` → gates at `StructureFactory.Create:115`,
  `GhostPreview:91`, `MeasureUprightFootprintMetres:478`; euler applied `StructureFactory.cs:122` /
  `GhostPreview.cs:94` / `StructureFactory.cs:480`; offset `+=` at `:123`/`:95`/`:481`;
  `EffectiveScale` per-axis at `:128-129`/`:484-485` vs GHOST uniform-only at `GhostPreview.cs:96-97`.
- `upgradeVisualPath` → `VisualPathForLevel` (`StructureFactory.cs:165-173`) → `ReskinForLevel`.

## STAGE 3 — PALETTE/ARM + GHOST
- Arm: `BuildModeController.Arm:940-951` — `_armedYawSteps=0`, `_armedYawOffset=0`, `SetEntry`.
- Ghost: `GhostPreview.SetEntry:49-120` — Skin (fit+seat), then manual-orientation apply
  (`:94` euler compose, `:95` offset, `:96-97` UNIFORM scale, `:103` ReseatCorrectedBottom).
  Orientation on the skinned CHILD; yaw on the ghost HOST (`MoveTo:158-164`, `Euler(0, yawSteps*90, 0)`).
- Player yaw: `UpdatePlaceLoop:444-445` rotate input → `_armedYawSteps=(+1)&3`.
- `TowerPlacementRotateMenu` non-dev path is DORMANT for normal placement (`BuildModeController.cs:470-478`);
  `_armedYawOffset` stays 0. Yaw-only if ever re-enabled (see risk R6).

## STAGE 4 — PLACEMENT (`StructureFactory.Create`) — exact mutation order
1. Root world pose: `BaseLayoutLoader.Spawn:236` `rot = Euler(0, yawSteps*90 + yawOffset, 0)`;
   `Create:71` `SetPositionAndRotation`.
2. `OptsFor` → `FitHeight = 5` (visualHeight wins over footprint-largest).
3. `VisualFactory.Skin` (`VisualFactory.cs:100-190`) on the visual CHILD:
   `:122` localPosition=0 → `:127` localRotation = opts.LocalRotation ?? identity (structures: identity;
   SeatFlat NOT run for structures) → `:145` `Fit(go, 5, largest:false)` measuring RAW `bounds.size.y`
   (`:245-247`) → `:150-151` `SeatOnGround` on RAW bounds. (The `[Flow:Xform]` trace prints values at
   each of these stages.)
4. Manual-orientation block (`Create:115-139`): `:122` euler pre-multiplied onto localRotation →
   `:123` offset += → `:128-129` EffectiveScale (skipped when identity) → `:137` `ReseatCorrectedBottom`
   (`:408-424`) drops the now-upright bounds.min.y to root.y.
5. `VerifyStructureRenders` (read-only) → `AttachBehavior` (stats only).
6. Loader extras: `:257` footprint via `MeasureUprightFootprintMetres` (off-screen probe re-applies
   orientation `:480-485`, destroyed `:496-497`); `:259 AddFootprintBlocker` sets BoxCollider +
   NavMeshObstacle size/center on the ROOT (`:324-340`).

## STAGE 5 — RUNTIME EXTRAS
- **StructureOrientationLocalStore overlay:** Orient-tool Confirm writes the live entry AND upserts
  `persistentDataPath/structure-orientations.json` (`TowerPlacementRotateMenu:891-903`, store `:61-81`);
  `CatalogBootstrap.cs:72` → `ApplyAll` (`:87-120`) REPLACES `entry.orientation` (manual=true) at
  startup, BEFORE any Create/ghost read — LOCAL WINS, one FlowTrace line announces it.
- **ReskinForLevel** (`StructureFactory.cs:183-227`): L≥2 swaps the visual (re-runs Skin fit+seat on
  the new model) and deliberately does NOT re-apply the base euler (`:215-222`) — tier models rely on
  prefab-native orientation.
- **StructureTierVisual** (`:61-74`, `:94`): ROOT `localScale = _baseScale * s_tierScale[tier]`
  (×1.0/×1.12/×1.25) — multiplies on top of the child fit-scale; L1 no-op.
- **HubStructureVisualInjector:** HUB-ONLY (gates on `HubScenes.IsHub` `:139/:144`; named baked objects
  only, `:60-79/:121-132`). Cannot touch build-mode structures.

## STAGE 6 — SAVE / REPLAY
- Saved: `itemId, cellX, cellZ, yawSteps, level, yawOffset, worldY, wallMounted`
  (`PlacedStructureData.cs:37-88`; written `BuildModeController.Place:881-916`).
- Replay `BaseLayoutLoader.Spawn:217-315`: `:230` cell→world, `:235` worldY, `:236` yaw →
  `:242` `StructureFactory.Create` RE-RUNS all of Stage 4 from the CURRENT catalog; `:291-293`
  ReskinForLevel (L≥2) + StructureTierVisual; `:299-300` tier stats (no transform).
  Only yaw + cell + seat-Y come from the save — the pose is always the live catalog's.

## CONFLICT / ORDER SUMMARY

```
root (world): position = grid cell (+worldY), rotation = Euler(0, yawSteps*90 + yawOffset, 0)
  └─ visual child (local):
        localScale    = import(1) × Fit(target/RAW-height)   [pre-uprighting measure ⚠]
                        × EffectiveScale (when authored)
        localRotation = Euler(orientation.euler) × identity
        position      = SeatOnGround(raw) then ReseatCorrectedBottom(upright)
  (root.localScale ×= s_tierScale[tier] at tier≥2)
```

- **R1 — Fit before upright (the big one):** height fit measured on the RAW mesh axis; a model that
  only stands after a pitch gets fitted on the wrong axis. Reseat fixes position, never scale.
  Meanwhile the collider footprint measures POST-orientation — mesh scale and footprint derive from
  different-orientation measurements.
- **R2 — Two scale owners on two transforms:** child fit-scale (+EffectiveScale) vs ROOT tier-scale.
- **R3 — Ghost vs placed scale divergence:** ghost uniform-only vs placed per-axis EffectiveScale.
- **R4 — Local overlay silently overrides shipped euler at startup** (announced by one FlowTrace line).
- **R5 — Tier models have no orientation authoring seam** (base euler deliberately not re-applied).
- **R6 — Latent yawOffset double-rotation** if `TowerPlacementRotateMenu`'s non-dev confirm path is
  ever re-enabled (`_armedYawOffset = yawSteps*90` composing with `yawSteps*90`). Disarmed today
  (`BuildModeController.cs:476-477`).
