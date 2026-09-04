# WORK ORDER 1302 — `DependencyClosureTrace` reports every Synty shader-graph material as a `dep MISS`

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T14:33:28, build 2026.09.04.354315). PRIOR STATUS: FIXED — `DependencyClosureTrace` now asks the SHADER for its texture properties and classifies each by token instead of probing two hardcoded URP/Lit names, so Synty shader-graph albedo slots verify clean while normal/emission/mask-only materials are still reported as a `dep MISS`. See `WORK_ORDER_1302_dependency_closure_trace_synty_shadergraph_false_positive.RESULT.md`.
**Source:** F8 captures seq **4355, 4356, 4357, 4358, 4365, 4366, 4367, 4368, 4371, 4372, 4373, 4374, 4375**
(13 captures across three sessions). Ledger: `docs/qa/F8_TRIAGE_2026-09-02.md` §6.
**Silo:** Core Addressables diagnostics
**Severity:** P2 — **not** a player-visible defect. The tower renders correctly. But it is a standing red
error on `Tower_Wooden_Watchtower`, which is the exact prefab the owner has just ruled the Archer Tower
back onto in her Tripo ladder — so the next seat to open this inbox will read a working asset as broken.

## Owner-facing symptom

None in the game. In the F8 inbox, 13 error captures assert that the wooden watchtower renders as *"an
untextured grey blob"*. It does not. The material is present, correct, and fully textured.

## Captured proving line (§12 evidence — quoted verbatim, full material name)

`logs/f8-inbox/capture-20260902-013500-seq4355.md` and twelve siblings, `scene=Main_Castle_Overworld`:

```
[Flow:StructureAssets]   dep MISS on 'Structures/Tower_Wooden_Watchtower': material 'Castle_Wall_01'
  has NO albedo and NO tint — renders as an untextured grey blob.
```
```
UnityEngine.Debug:LogError (object)
DeNelle.Core.Diagnostics.UnityLogSink:Error (string) (at .../Core/Diagnostics/FlowTrace.cs:461)
DeNelle.Core.Diagnostics.FlowTrace:Fail (string,string) (at .../Core/Diagnostics/FlowTrace.cs:171)
DeNelle.Core.DependencyClosureTrace:Verify (string,string,UnityEngine.Object,bool)
  (at D:/EoA/Assets/_Modules/Core/Addressables/DependencyClosureTrace.cs:119)
DeNelle.Core.StructureAssetLoader:Load<UnityEngine.GameObject> (string)
  (at D:/EoA/Assets/_Modules/Core/Addressables/StructureAssetLoader.cs:150)
```

## Root — proven from the asset on disk, not inferred

The material **is textured**. `Assets/Synty/PolygonFantasyKingdom/Materials/Walls/Castle_Wall_01.mat`:

- `m_Shader` guid `0730dae39bc73f34796280af9875ce14` →
  `Assets/Synty/PolygonGeneric/Shaders/Generic_Basic.shadergraph`
- its albedo slot is named **`_Albedo_Map`**, and it carries guid `24f1ea296c9e695449086de7c2eca5e4` →
  `Assets/Synty/PolygonFantasyKingdom/Textures/Castle/Wall_Brick_01.png`
- it also carries `_Normal_Map`, and its colour properties are `_BaseColor`/`_Color` left at
  `{r:1, g:1, b:1, a:1}` (white) plus a separate `_Emission_Color`

The oracle only knows the two URP/Lit names. `Assets/_Modules/Core/Addressables/DependencyClosureTrace.cs:101-118`:

```csharp
Texture albedo = null;
if (mat.HasProperty("_BaseMap"))                 albedo = mat.GetTexture("_BaseMap");
if (albedo == null && mat.HasProperty("_MainTex")) albedo = mat.GetTexture("_MainTex");
…
Color tint = Color.white;
if (mat.HasProperty("_BaseColor")) tint = mat.GetColor("_BaseColor");
else if (mat.HasProperty("_Color")) tint = mat.GetColor("_Color");
bool tinted = Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b)) < 0.92f;
if (albedo != null || tinted) { ok++; } else { missing++; FlowTrace.Fail(…); }
```

`_Albedo_Map` is never probed, and white fails the tint test — so a fully-textured Synty material lands
in the `missing` branch. **This is occurrence two of the class this very file warns about**, in its own
comment at lines 105-108:

> *"That distinction was learned the hard way today: the first version of this check reported 21 working
> prefabs as broken, and an oracle that cries wolf gets ignored on the day it is right."*

The project is mid-retheme onto Synty art (`WORK_ORDER_1290/1291/1292`), so the false-positive surface
is growing with every prefab swapped over.

## Acceptance criteria

1. `DependencyClosureTrace.Verify` recognises the Synty shader-graph albedo slot (`_Albedo_Map`) in
   addition to `_BaseMap` and `_MainTex`, and stops reporting `Castle_Wall_01` as a miss.
2. **Survey the whole Synty tree before choosing the property list.** Do not fix the one name you were
   handed — grep every `.mat` under `Assets/Synty/` for its texture-property names and cover the set
   (memory `search-by-token-not-by-name`). List the names found in the RESULT.
3. **The oracle still catches a real miss.** Prove it: point it at a material with a genuinely empty
   albedo slot and an untinted colour, and show `dep MISS` still fires. A change that merely silences
   the message is rejected.
4. `Assets/Editor/Regression/StructureNullMaterialSlotRegression.cs` (which documents this exact log
   shape at line 9) is extended with a case that would have caught this: a Synty-shader material with a
   populated `_Albedo_Map` must verify **clean**.
5. A headless load of `Structures/Tower_Wooden_Watchtower` produces **zero** `dep MISS` lines.
6. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs — judge by the marker, never the
   exit code (memory `gates-report-success-without-proving-it`).

## What NOT to touch

- ⛔ **Do not edit `Assets/Synty/PolygonFantasyKingdom/Materials/Walls/Castle_Wall_01.mat` or any other
  Synty material or shader.** The asset is correct. The checker is wrong. Re-authoring the art to
  satisfy a broken oracle is the wrong direction and would corrupt the retheme.
- ⛔ **Do not delete or disable `DependencyClosureTrace.Verify`, and do not downgrade its `FlowTrace.Fail`
  to a `Warn` to get it out of the inbox** (CLAUDE.md §12 — instrumentation is permanent; a demoted
  detector is a silent one).
- ⛔ Do not touch `Assets/_Modules/Core/Addressables/StructureAssetLoader.cs` load ordering, the
  Addressables groups, or anything under `Assets/AddressableAssetsData/`. **A change there re-hashes
  every bundle and would require a fresh `tools\r2-ship.ps1` push (CLAUDE.md §16).** This ticket must
  not touch content.
- ⛔ Do not fold in the address-mapping work of `WORK_ORDER_1291_synty_building_retheme.md` — another
  seat owns that lane.
- ⛔ Do not relax the `< 0.92f` tint threshold; that constant was tuned to stop a previous 21-prefab
  false-positive wave.
