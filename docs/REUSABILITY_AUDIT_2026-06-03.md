# Reusability Audit — creation sites (2026-06-03)

Owner principle: "one factory creates hero / companion / KayKit / enemy — don't build each one at a time. That's the central idea of lightweight." Runtime is **~90% already on-pattern**; waste is now in 2 runtime holdouts + the editor builders.

## Verdict
- **Runtime factories (good):** `VisualFactory.Skin` (the skinner), `EnemyFactory.Build`, `StructureFactory.Create`. Almost everything routes through them — all enemy spawners, player placement, save-replay, ghost preview, companion, and (as of `b52b33f`) the **hero**.
- **Runtime holdouts (B):**
  1. **`PetDeployer.SpawnPet/TryLoadPetMesh`** — ~150 dup lines (load/fit/seat/strip/camera+light+particle strip/-90° yaw/Tripo fix) = the exact hero sin. Route through `VisualFactory.Skin` + keep pet-specific extras on top. **Low risk, pure runtime, highest payoff.** MUST preserve `UseLitePetVisuals` (keeps ~208 MB pet FBXs out of the WebGL build — the dominant size lever).
  2. **`PatriciaLightController`** hero spawn (~648) + tower2 spire (~433) hand-load, though the same file uses `VisualFactory.Skin` for enemies (1121). Inconsistent. Low risk.
- **Editor builders (B, ~500 dup lines):** `VillageSceneBuilder.*` is the de-facto editor factory but its helpers (`LoadModel`/`InstantiateModel`/`NormalizeProp`/`SnapFeetToParent`/`StripColliders`/`ApplyTint`/`EnsureCollider`/`HexColor`/`FindType` + 4 reflection setters) are **private** and re-declared verbatim in `FolksGranaryBuilder`, `DungeonSceneBuilder`, `OuterWorldBuilder`, `ExteriorTerrainBuilder`, `DungeonStubBuilder`, `PatriciaLightSceneBuilder`. Editor asmdef can't use the Resources-based runtime factory (CLAUDE.md), so it legitimately needs its OWN parallel factory — but extracted ONCE (`EditorVisualFactory`, internal static), not copied 6×. **Medium risk** (touches the VillageSceneBuilder serialization bottleneck — code-only, no scene resave), single coordinated WO, verify by running each builder once.
- **Leave alone:** procedural primitive geometry (PatriciaLight tower drum/merlons, FolksGranary lanterns, CampVisual campfire, range rings, settlement stubs) — intentional placeholder geometry, not asset-load duplication. Route to a factory only when real models land.
- **No heavy duplicate ASSETS to delete** — the duplication is *code*, not art; factories already point at single canonical Resources paths.

## ⚠️ Correction (asmdef boundary — found when the Pet silo ran)
`VisualFactory` lives in **`DeNelle.Village`**, but **`DeNelle.Pets` deliberately does NOT reference `DeNelle.Village`** (hard, load-bearing boundary; every Pets→Village touch is a reflection bridge — see `MineNodeBridge`, `PetHeroLeash`, headers in `PetDeployer.cs`/`Pet.cs`). So `PetDeployer` **cannot** call `VisualFactory.Skin` directly. The audit's "PetDeployer → VisualFactory" item is blocked until the factory moves.

**The real fix (and the true "one factory for everything" architecture): promote `VisualFactory` to a shared LEAF assembly** — `DeNelle.Core` or a new `DeNelle.Visual` that Village + Pets + others reference. The factory is a generic primitive (load/fit/seat/strip/Tripo-fix), not Village-specific, so it belongs in a leaf everyone can see. This is the enabling step for universal reuse. Medium risk (touches `VisualFactory.cs` + asmdefs + Village callers; compile-wide), so it's a deliberate WO — NOT a mid-firefight change. Alternative (lower risk, uglier): a `VisualFactoryBridge` in Pets that reflection-invokes Village's factory (matches existing Pets pattern).

## Recommended sequence
0. **(enabling) Promote `VisualFactory` to a shared leaf assembly** (Core or new `DeNelle.Visual`). Then Pets — and anything — can reuse it directly. Deliberate WO, not a firefight task.
1. `PetDeployer` → `VisualFactory.Skin` (AFTER step 0; preserve the lite-pet billboard branch).
2. `PatriciaLightController` hero + spire → `VisualFactory.Skin` (low risk; one-file consistency).
3. Extract `EditorVisualFactory` + shared reflection setters, delete the 6 private copies (one WO; touches VillageSceneBuilder — code-only).
