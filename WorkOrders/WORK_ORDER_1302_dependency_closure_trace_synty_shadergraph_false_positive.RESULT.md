# WORK ORDER 1302 — RESULT

**Status:** FIXED (edit-only; NOT gated, NOT committed — the lead gates and commits)
**Date:** 2026-09-02

## What was actually wrong

`DependencyClosureTrace.Verify` asked two hardcoded questions — `_BaseMap` and `_MainTex` — and treated
"neither of those two names holds a texture" as "this material has no albedo". Those are the URP/Lit and
built-in-pipeline names. Synty's shader graphs call the same slot `_Albedo_Map` (or `_Base_Map` /
`_Base_Texture`), and leave `_BaseColor`/`_Color` at white, so a fully textured, correct, deliberate
Synty material fell straight through to the `missing` branch and self-reported as an "untextured grey
blob" — 13 F8 error captures on one working watchtower, on a surface that grows with every prefab the
retheme swaps over.

The asset was right. The oracle was asking the wrong question.

## The fix — a classifier, not an allowlist

`Assets/_Modules/Core/Addressables/DependencyClosureTrace.cs`

The detector now **asks the shader what its albedo slot is called** (`Shader.GetPropertyCount` /
`GetPropertyName` / `GetPropertyType == ShaderPropertyType.Texture`) and classifies each texture property
**by token**:

- reject first, on `detail, normal, bump, mask, emission, emissive, occlusion, metallic, specular,
  gloss, smoothness, rough, height, parallax, lightmap, shadow, noise, displacement, opacity, overlay,
  curvature, flow, matcap`
- then accept on `albedo, basemap, basetexture, basecolor, basecolour, maintex, maintexture, diffuse,
  colormap, colourmap, triplanartexture`
- names are normalised (lower-cased, separators stripped) so `_Albedo_Map` and `_AlbedoMap` compare equal

`_BaseMap` / `_MainTex` are kept as an explicit fast path so the common case costs no reflection.

**Deliberately NOT a list of known materials, shaders or packs.** A hand-maintained exception list is one
fact written twice; it rots the day the next pack lands, which is the exact failure this file's own
comment warns about. A token classifier generalises: it accepts an albedo slot it has never seen, and it
still rejects a normal/emission/mask map, so a material whose only populated texture is a normal map is
still correctly a grey blob.

The `dep MISS` line now also names the shader and every albedo-classified slot with `set` / `EMPTY`, so a
genuine miss reads in one line and a future false positive is self-evident ("the slot it lives in is not
listed").

The tint test, the `< 0.92f` threshold, `FlowTrace.Fail` severity, the Addressables/Resources fallback
branch and the deps `n/n` counter are all untouched.

## Acceptance criterion 2 — the survey (property names found across `Assets/Synty/**`, 160 `.mat`)

Enumerated every `m_TexEnvs` key in the tree (by token, not by guessed name). 38 distinct names:

**Classified ALBEDO (8):** `_Albedo_Map` (94), `_MainTex` (68), `_BaseMap` (59), `_Base_Map` (58),
`_Base_Texture` (2), `_Triplanar_Texture_Top` / `_Triplanar_Texture_Side` / `_Triplanar_Texture_Bottom` (2 each)

**Classified NOT-albedo (30):** `_Normal_Map` (137), `_Emission_Map` (79), `_BumpMap`,
`_DetailAlbedoMap`, `_DetailMask`, `_DetailNormalMap`, `_EmissionMap`, `_MetallicGlossMap`,
`_OcclusionMap`, `_ParallaxMap`, `_SpecGlossMap` (62 each), `_Hair_Mask`, `_Skin_Mask` (29 each),
`_Metallic_Smoothness_Map` (12), `_AO_Texture`, `_Emission_Texture`, `_Metallic_Smoothness_Texture`,
`_Normal_Texture`, `_Overlay_Texture`, `_Triplanar_Emission_Texture`, `_Triplanar_Normal_Texture_{Top,
Side,Bottom}`, `_Shore_Wave_Foam_Noise_Texture`, `_Water_Noise_Texture`, `_Water_Normal_Texture`,
`_Spherical_Map`, `unity_Lightmaps`, `unity_LightmapsInd`, `unity_ShadowMasks`

Note `_DetailAlbedoMap` is correctly REJECTED (the `detail` reject token runs before the `albedo` accept
token) — a detail map is not the base colour and must not launder a material with an empty base slot.

## Both directions proved

**Healthy case stops failing.** The classifier was run over the full 38-name corpus above and produced
exactly the partition listed: `Castle_Wall_01`'s `_Albedo_Map` is now found and populated, so it takes the
`ok++` branch instead of `missing++`.

**Mutation — a genuine miss is still caught.** Clone the real `Castle_Wall_01` material and clear every
albedo-classified slot on it; `DependencyClosureTrace.HasAlbedo` must return `false`. This mutation is
**permanent, not a one-off**: it is wired into the regression suite as a negative control, so nobody has
to re-mutate by hand to trust the green. Second half of the mutation: the 16 not-albedo names above are
asserted to stay unclassified, so a material whose only texture is a normal/emission/mask map still
reports `dep MISS`.

## Acceptance criterion 4 — regression extended

`Assets/Editor/Regression/StructureNullMaterialSlotRegression.cs` gains `CheckAlbedoOracle`, called from
`RunCore` and counted in the pass line (`albedoOracleChecks=N`):

1. 16 not-albedo slot names must NOT classify as albedo (a real grey blob must not walk through).
2. 8 albedo slot names (including `_Albedo_Map`, `_Base_Map`, `_Base_Texture`, `_Triplanar_Texture_Top`)
   MUST classify as albedo (the WO-1302 false positive).
3. The real on-disk `Assets/Synty/PolygonFantasyKingdom/Materials/Walls/Castle_Wall_01.mat` must read as
   textured — asserted against the asset, not a synthetic stand-in.
4. NEGATIVE CONTROL: a clone of that material with every albedo slot emptied must read as NOT textured.
   If it reads clean, the detector was silenced rather than fixed and the suite goes red.

Hollow-pass guarded: if the Synty material is absent (gitignored pack), 3 + 4 record a
`RegressionOutcome.PartialSkip` and say so, rather than passing quietly.

Three public members were added to `DependencyClosureTrace` purely so the suite can assert both
directions: `IsAlbedoSlot(string)`, `HasAlbedo(Material)`, `DescribeAlbedo(Material)`.

## Not run here (edit-only lane)

- `COMPILE_GATE_OK`, `REGRESSION_OK <n>/<n> suites`, and the headless
  `Structures/Tower_Wooden_Watchtower` load (acceptance 5, 6) — the lead gates. Judge by marker on a
  fresh log, never the exit code.

## Deliberately NOT touched

- Any `.mat`, `.shadergraph` or texture under `Assets/Synty/**` — the art is correct and deliberate.
- `FlowTrace.Fail` severity (not demoted to `Warn`), the `Verify` call sites, and the `< 0.92f` tint
  threshold.
- `StructureAssetLoader.cs` load ordering, the Addressables groups, anything under
  `Assets/AddressableAssetsData/` — a change there re-hashes every bundle and would demand a fresh
  `tools\r2-ship.ps1` push (CLAUDE.md §16). No content was touched, so no R2 push is implied.
- The WO-1291 address-mapping lane.

## Brace / NUL check (CLAUDE.md §1)

```
Assets/_Modules/Core/Addressables/DependencyClosureTrace.cs        BALANCED clean
Assets/Editor/Regression/StructureNullMaterialSlotRegression.cs    BALANCED clean
```
