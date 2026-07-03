# Moat Water Design — the Seam Water Around the Raised Castle

**Date:** 2026-07-03 · **Lane:** creative/technical-art DESIGN (no code in this order)
**Status:** DECISION-READY — owner picks §1 direction + answers §5, then this becomes a WO.
**Companion canon:** `docs/CASTLE_MOAT_DESIGN_NOTE.md` (WO-509 frame: boundary + four gates + chokepoints).
**Quality bar:** the ten-year-old test — the terrain already earned "feels like there is something real now"; the water must meet it.

---

## 0. What exists today (SME baseline — read from code, not comments)

All facts below are from `Assets/_Modules/Village/World/CastleMoatBuilder.cs` (HEAD, 2026-07-03) unless noted.

| Fact | Source |
|---|---|
| Moat band is a **square annulus r=44..58** (14 m across): inner edge = `RampInnerRadius` 44 (plinth face), outer = `RampOuterRadius − 2` = 58, leaving a 2 m dry shore before the r=60 ramp landings | `MoatInnerRadius/MoatWidth/MoatCentreRadius`, CastleMoatBuilder.cs:70-72 |
| Water surface = **4 overlapping Unity `Plane` primitives** (one per side, corners overlap "cheap, no corner mesh") at `waterY = measured ground + 0.05` (raycast probe at r≈59.5, fallback y=0) | `BuildWaterRing` :274-304, probe :226-227 |
| Water material = **runtime URP/Lit, transparent**, `_BaseColor (0.10, 0.42, 0.45, 0.62)` teal, `_Smoothness 0.25`, ZWrite off, alpha blend | `WaterColor` :90, `BuildLitMaterial` :812-841 |
| Motion = **`MoatWaterShimmer` (DEF-195)**: generates a 32×32 procedural sine-wavelet normal map once, assigns to `_BumpMap`, scrolls `_BumpMap_ST` offset per frame (~0.015-0.022 u/s), `_BumpScale 0.35`, tiling 2. It deliberately never touches `_BaseColor/_Smoothness` (owner's de-glossed-teal constraint, 2026-06-01) | `MoatWaterShimmer.cs` whole file; attach at CastleMoatBuilder.cs:858-869 |
| The WO-590 **dip-fill** (broad 58..72 sheet) is now **dead code in practice**: it only builds when a measured ≥0.5 m dip exists, and `ExteriorTerrainBuilder` holds `CastleDepressionDepth = 0f` (flush terrain, 2026-06-30) → the probe skips it every run | dip probe :236-242, `DipRequiredDepth` :119 |
| **FishSchool**: 10 fish (cap 12, "owner runs on a Pi"), spawned over the SOUTH band, swim at `waterY − 0.3`, wander-in-box + bob, one shared GPU-instanced URP/Lit material, primitive-ellipsoid fallback if `Resources/Env/Fish` absent | `FishSchool.cs`; `SpawnFishSchool` :353-373 |
| Castle courtyard sits on a plinth at `castle.liftY` (PlayerPrefs, default **3**); N/W/E gates get stone-tinted **funnel ramps** (gate-width 9 m → ×1.75 flare) descending r=44→60, landing sunk 0.25 m into measured terrain | :389, :146-164, :415-466 |
| **South bridge** = the felt-tested crossing: stone prefab (`Resources/Bridges/Bridge_Medieval_Stone`, OffsetForge id `bridge_south`), pitched about its outer end so castle-end top = liftY; **analytic walk-plane box collider** (stone walkway at local y=2.6) + rail colliders; all slots repainted with shared stone URP/Lit (0.55,0.55,0.57) | `TryPlaceBridgePrefab` :619-781 |
| Water quads have **colliders stripped** — water is visual only; the impassability is navmesh/boundary, not physics | `StripCollider` :299, :341 |

**Known visual debts in the current pass** (why it doesn't hit the bar yet):

1. **Corner double-blend.** The 4 transparent ring planes overlap at the corners; two coplanar alpha quads double-blend there — the code itself names this failure mode for the fill (":106-108 overlapping coplanar transparent quads double-blend / z-fight") but the *ring* still ships with overlapping corners. Four darker corner patches, visible from the ramps.
2. **No edge language.** Water meets the plinth stone and the terrain as a razor alpha edge — no foam, no darkening, no "wet" band. Edges are where water is *read*; ours are silent.
3. **Uniform sheet.** One flat tint across 14 m: no shallow→deep gradient, so the band reads as tinted glass laid on grass rather than a channel with depth.
4. **Motion is normal-only.** The shimmer is good value (proven, near-free) but there is no color/foam motion, so at the player's grazing camera angle (third-person, hero at y≈3 descending to y=0) the specular wobble is the *only* life.

---

## 1. CREATIVE DIRECTION — three treatments

The player's dominant view: third-person camera behind the hero, seeing the moat **at a grazing angle** while descending the south bridge or a ramp, water band running left-right across the frame with the plinth wall rising behind it. Grazing angle means: **edges, foam, and color banding read strongly; looking-down transparency/depth reads weakly.** Any treatment must earn its keep at that angle. The moat's narrative job: *the castle is a safe island; the water is why you cross HERE, at the bridge* — the seam made diegetic.

### Treatment A — "Storybook Bands" (stylized painterly low-poly water)

**Look in words.** Opaque-ish stylized water in 2–3 hard-stepped color bands: a bright shallow teal hugging both shores, a deeper blue-green mid-channel, and a crisp near-white **foam line lapping the plinth face** that slowly pulses in and out like a breath. Scrolling wavelet normals stay (subtle sparkle), but the *color steps* do the talking. Think hand-painted, confident, flat-shaded — matching the low-poly KayKit/polyperfect world.

**At the player's angle.** The banding is parallel to the shore, so at a grazing angle the player sees clean ribbons of color converging on the bridge — extremely legible, zero shimmer-noise dependence. The foam line against the plinth is the money shot from the ramps: it visually *welds* water to castle.

**How it sells the seam.** Stepped color says "this has depth, you'd sink"; the pulsing foam against the plinth says "the island is *in* the water," not next to it. Reads as deliberate art direction, not a missing shader.

**Reference tier.** Wind Waker / Link's Awakening (2019) moats; Ni no Kuni overworld water; Polytopia at the minimal end. All ship on weaker hardware than our floor.

### Treatment B — "Living Current" (animated-normal PBR-lite, the current approach evolved)

**Look in words.** Keep the translucent teal + scrolling normals, and add: a **fresnel** term (denser color at grazing angles, so the sheet stops reading as glass exactly where our camera lives), a second counter-scrolling normal layer (the shimmer already half-does this with `_BaseMap_ST` :113-115), and a shoreline foam band driven by mesh vertex data. Fish stay visible through the surface — the transparency is the point.

**At the player's angle.** Fresnel is *made* for grazing cameras: the water deepens/saturates toward the horizon-side and stays see-through at the player's feet, which reads as real water physics even to a ten-year-old. Motion is continuous rather than banded.

**How it sells the seam.** "I can see fish under there but I clearly can't walk on it." Transparency + fresnel + foam = a believable barrier that stays friendly (calm castle-side water, per the posture canon: castle = FRIENDLY space).

**Reference tier.** RuneScape (modern), Albion Online, Fortnite's early water — the mobile-shippable end of "realistic-ish."

### Treatment C — "Flat Poster" (gradient + geometric foam ring, minimum spend)

**Look in words.** Fully **opaque** vertex-colored ring: smooth gradient dark-center → light-shore baked into mesh vertex colors under plain URP/Lit, plus a separate thin white foam *strip mesh* against the plinth whose alpha/scale pulses on a sine. No transparency anywhere → no blend cost, no sorting, no double-blend, no fish visibility problem to solve (fish get skipped or swim as surface dimples).

**At the player's angle.** Clean and graphic; motion limited to shimmer + foam pulse. Reads "board-game" — handsome but the least *alive* of the three.

**How it sells the seam.** Adequately — an obviously-solid colored band still reads as "not ground" — but it loses the fish (a living-water touch already built and owner-endorsed in WO-590) or renders them floating on an opaque sheet.

**Reference tier.** Monument Valley, Polytopia, most low-poly asset-pack water.

### ★ Recommendation: **A + the best of B** — "Storybook Bands with a fresnel heart"

One hand-written URP shader that is ~90 % Treatment A (stepped shore-parallel bands + pulsing plinth foam, mostly-opaque) with Treatment B's **fresnel darkening** and the **existing scrolling normal** folded in, and **semi-transparency only in the shallow band** so the fish still read.

Why this one:

- **It wins at the actual camera.** A's bands + foam are grazing-angle-first; B alone leans on transparency the camera rarely rewards; C sacrifices the fish and the life.
- **It matches the world.** The terrain the owner loves is stylized low-poly; painterly stepped water is the same visual language. Full PBR-lite water (B) would be the most *realistic* thing in frame — which reads as a mismatch, not quality.
- **It's the cheapest per pixel of the "alive" options.** Mostly-opaque → drastically less blended overdraw than today's fully-transparent 62 %-alpha sheet; one texture sample (the existing 32×32 ripple normal) + arithmetic; no depth-texture, no reflection probe, no grab-pass — all WebGL-safe.
- **It keeps every proven piece**: MoatWaterShimmer's scroll (drives the same `_BumpMap_ST`), FishSchool (visible through the shallow band), the analytic south bridge, the derived band geometry. Evolution, not greenfield — per "~70 % built, do NOT greenfield" culture.

---

## 2. TECHNICAL PLAN (for the recommendation)

### 2.1 Mesh — purpose-built square annulus (the planes retire)

The 4 overlapping `Plane` primitives **do not survive**: the corner double-blend (§0 debt 1) is unfixable while transparent quads overlap, and Planes carry no vertex data for the shader. Replace with **one procedural square-annulus ring mesh** built in `CastleMoatBuilder` — the codebase already builds bespoke meshes right there (`BuildTaperedDeckMesh` :527-563 is the pattern to mirror).

- Geometry: 4 mitred trapezoid sides forming a closed square ring, inner half-extent 44, outer 58, y = the existing derived `waterY`. Each side subdivided **across the channel only** (5 vertex rows: inner edge / inner-shallow / mid / outer-shallow / outer edge) → ~160 verts, ~240 tris, **one mesh, one draw call**.
- Vertex data (this is the whole trick — the shader needs no depth texture):
  - `uv0.x` = **shore distance** 0→1→0 across the channel (0 at both shores, 1 mid-channel). Drives band stepping, depth tint, transparency falloff.
  - `uv0.y` = distance **along** the ring (for foam scroll variation so the foam isn't a uniform pulse).
  - `color.r` = **plinth-side mask** (1 at the inner edge fading over ~2 m) — the lapping foam lives only against the castle.
- No collider (unchanged — water never blocks, :299).

### 2.2 Shader — one small hand-written URP shader, `MoatWaterStylized`

Hand-written ShaderLab+HLSL (unlit-with-lighting-tint style), **not** Shader Graph — no graph asset to maintain, trivially WebGL-safe, and the effect is ~40 lines of fragment code. Ships as `Assets/Shaders/MoatWaterStylized.shader` **plus a committed material in `Resources/`** so it's guaranteed into the build (runtime `Shader.Find` on a shader nothing references gets stripped; today's code survives only because URP/Lit is always included — a new shader must be referenced by a Resources material).

Fragment sketch (all `half` precision, one texture sample):

```
n      = UnpackNormal(tex2D(_BumpMap, uv * _Tiling + _ScrollOffset));      // reuse shimmer's map+scroll
shore  = uv0.x;                                                             // 0 shore .. 1 mid
wob    = n.x * _BandWobble;                                                 // normals wiggle the band edges
col    = shore+wob > _DeepStart  ? _DeepColor
       : shore+wob > _ShallowEnd ? _MidColor : _ShallowColor;               // 3 stepped bands
fres   = pow(1 - saturate(dot(viewDir, up)), _FresnelPow);
col    = lerp(col, _DeepColor, fres * _FresnelAmt);                         // grazing-angle deepening
foam   = plinthMask * step(frac(shore*_FoamFreq - _Time*_FoamSpeed + uv0.y*0.3), _FoamWidth)
       + sinePulse(_Time)*plinthMask;                                       // lapping line at the plinth
col    = lerp(col, _FoamColor, saturate(foam));
alpha  = lerp(_ShallowAlpha /*≈0.55, fish visible*/, 1.0 /*opaque*/, smoothstep(0, 0.4, shore));
spec   = cheap Blinn-ish glint from n (keep low — the de-glossed-teal constraint holds)
```

Render state: `Transparent` queue, alpha blend, **ZWrite off**, cull back — but with alpha ≈1 over most of the band the blend cost is near-opaque in practice.

**MoatWaterShimmer keeps its job unmodified**: it scrolls `_BumpMap_ST` on whatever shared material the first child renderer holds (:67-73) — the new material exposes the same `_BumpMap/_BumpMap_ST/_BumpScale` names, so DEF-195 drives the new shader for free. (It skips generating a normal if one is already assigned, :85 — the committed material can carry a nicer 64×64 baked ripple, or stay empty and take the procedural one.)

### 2.3 Edge treatment

- **Plinth (inner) edge — foam, in-shader** (§2.2): a 30–60 cm animated lap line + slow sine pulse, masked by `color.r`. This is the single highest-value pixel in the whole feature: it welds castle to water from every ramp.
- **Terrain (outer) edge — wet-shore strip, geometry**: a separate thin **opaque** ring strip (same annulus builder, r=58..58.7, y = terrain + 0.02) in a dark wet-earth tint (terrain albedo × ~0.6, URP/Lit, zero new shader). Hides the razor alpha edge where water meets grass and reads "the ground here is wet." One extra draw call.
- **Corners** — solved structurally by the mitred annulus (no overlap exists anymore).

### 2.4 Bridge + ramp interaction

- The south stone bridge's arches stand *in* the water; the analytic walk-plane collider (:726-762) and the OffsetForge seat are untouched — the water surface (waterY ≈ 0.05) passes under the deck exactly as now.
- **Reflections: skipped**, deliberately. Planar reflection = second camera pass (Pi-fatal); SSR needs depth+opaque texture (WebGL risk, and broken under transparents). The **fresnel deepening + normal glint fake the "wet mirror" read** at grazing angles — the accepted trick at this tier (Wind Waker has no true reflections either).
- Optional slice-3 flourish: a slightly stronger foam ring around each bridge-arch footing — reuse the plinth-mask channel painted high on the verts nearest the bridge span (the builder knows `gateLateral` + the span; it can paint `color.r` on the nearest ring verts at build time). Cheap, sells "the water flows around the piers."

### 2.5 FishSchool integration

- Fish live at `waterY − 0.3` under the **shallow** semi-transparent band near the south shore — visible, which is the point of WO-590. Ensure fish render **before** the water (fish = Geometry queue, water = Transparent queue → already correct by default; note it in the WO acceptance so a queue tweak never silently hides them).
- Slice-3 touch: tint the shared `MoatFish` material toward `_MidColor` (they read "underwater" instead of grey), and have each fish leave an occasional expanding **ripple ring** (a 6-segment torus-billboard, spawn ≤1/sec school-wide, pooled ×3). Strictly optional; feel-only.

### 2.6 Mood / posture tint hook (cheap drama)

New tiny component `MoatWaterMood` on the moat root, mirroring the proven `WorldMusicDirector` pattern (0.5 s poll, react on *transition* only, WorldMusicDirector.cs:61-93):

- Poll `DeNelle.Core.HudModel.PostureSignals.PursuitActive` (the pulse-based, self-decaying pursuit fact the HUD posture arc already uses — `PostureSignals.cs:69-79`). Village → Core reference is legal (§5 cross-assembly rule).
- Calm → the owner's palette. Hostile/pursuit → lerp `_ShallowColor/_MidColor/_DeepColor` toward a steel-grey-green "the realm holds its breath" set + raise `_FoamSpeed` ~1.5×, over ~2 s. Reverting on decay.
- Pure material-property lerp — zero extra draw cost. Flag-gated (`ff.moatmood`, default ON) so it can be felt-tested independently.

### 2.7 Data-driven tunables (owner-tunable, not constants)

Everything tasteful moves to a recipe, per the owner's data-structures preference: **`Assets/Resources/Data/moat-water.json`**, loaded by `CastleMoatBuilder` with the current constants as fallback (exact pattern of `ReadSouthGatePos` + `castle-south-recipe.json`, :787-804):

```json
{
  "shallowColor": [0.16, 0.55, 0.55, 0.55], "midColor": [0.10, 0.42, 0.45, 1.0],
  "deepColor":   [0.05, 0.28, 0.34, 1.0],  "foamColor": [0.85, 0.93, 0.92, 1.0],
  "bandSteps": { "shallowEnd": 0.25, "deepStart": 0.6 },
  "scrollSpeed": [0.015, 0.022], "foamSpeed": 0.35, "foamWidth": 0.5, "bumpStrength": 0.35,
  "fresnel": { "power": 3.0, "amount": 0.5 },
  "fishCount": 10,
  "mood": { "hostileTintShift": [-0.05, -0.10, -0.08], "foamSpeedMul": 1.5, "lerpSeconds": 2.0 }
}
```

The band *geometry* (44/58) stays derived from `RampInnerRadius`/`RampOuterRadius` in code — it is structural (coupled to the plinth and landings), not taste.

---

## 3. PERFORMANCE BUDGET (per treatment; Pi Browser is the floor)

| Cost | Today (baseline) | A (bands) | B (PBR-lite) | C (flat) | ★ Recommended |
|---|---|---|---|---|---|
| Draw calls (water) | 4 transparent planes (+0-4 dead fill) | 1 | 1-2 (2 normal layers can still be 1 pass) | 2 opaque | **2** (ring + wet-shore strip) |
| Blended-overdraw area | full 5 700 m² band @ α0.62 | shallow band only (~25 %) | full band | none | **~25 % of band** |
| Texture memory | 4 KB (32×32 RGBA runtime normal) | 4-16 KB | 8-32 KB (2 normals) | 0-4 KB | **≤16 KB** |
| Fragment cost | Lit + 1 normal sample | 1 sample + arithmetic | 2 samples + fresnel | flat Lit | **1 sample + arithmetic** |
| CPU/frame | 1 material-ST write (shimmer) | same | same | same + foam pulse | **same + 0.5 s mood poll** |
| Fish | 10 instanced ellipsoids, no physics | keep | keep | cut/awkward | **keep (cap 12 holds)** |

**WebGL-specific risks + mitigations**

- **Precision:** mediump halves on mobile GPUs — time-driven UV offsets must stay wrapped (shimmer already does `_offset %= 1`, :103-104; the shader's foam phase uses `frac()`); never feed raw `_Time.y` into a `sin` after minutes of play.
- **No MSAA assumption:** hard band *edges* are wobbled by the normal (`_BandWobble`) so stepped-color boundaries alias less than geometry edges would; foam is a soft-edged `step`/`smoothstep` mix.
- **No depth texture:** the shore gradient comes from vertex data, so we never enable `_CameraDepthTexture` (an extra full-res pass on WebGL/tile GPUs) — this is the recommendation's key structural saving over "proper" depth-fade water.
- **Shader inclusion:** new shader MUST be referenced by a committed `Resources/` material or it strips from the build (`Shader.Find` alone is not inclusion). Acceptance test: material renders in a **player build**, not just editor — the bridge MeshCollider lesson (:713-717) generalizes: editor masks asset-pipeline failures.
- **Sorting:** one transparent water mesh + instanced opaque fish = no intra-water sorting problem (the double-blend class of bug dies with the planes).

---

## 4. IMPLEMENTATION SLICES

### Slice 1 — "The water stops being glass" (one cycle, no new shader, shippable alone)

Smallest change that visibly upgrades feel, entirely inside existing files:

1. **`CastleMoatBuilder.BuildWaterRing`** → build the **mitred annulus mesh** (new private `BuildMoatAnnulusMesh`, modeled on `BuildTaperedDeckMesh`) instead of 4 planes. Kills the corner double-blend; adds the vertex channels (inert under URP/Lit today, ready for slice 2).
2. **Two-tone now, via geometry:** build the annulus as two concentric sub-rings sharing the mesh (shore band α≈0.5 lighter teal / mid band α≈0.85 deeper teal, two submeshes, two shared URP/Lit materials). Instant depth read, +1 draw call.
3. **Wet-shore strip** at the outer edge (opaque dark ring, §2.3). +1 draw call.
4. **Delete the dead dip-fill path** (`BuildWaterFill`, `FillInnerRadius/FillOuterRadius/DipRequiredDepth`, the probe at :236-242) — flush terrain made it unreachable; carrying it violates the catalog-current rule.
5. Keep shimmer + fish untouched (both auto-follow: shimmer resolves the first child renderer's material, fish spawn from band constants).

Files: `Assets/_Modules/Village/World/CastleMoatBuilder.cs` only. Risk: near-zero (visual-only, colliders unchanged, idempotency + flag gate unchanged).

### Slice 2 — the full treatment (the recommendation proper)

1. **`Assets/Shaders/MoatWaterStylized.shader`** + committed material `Assets/Resources/Materials/MoatWaterStylized.mat` (build-inclusion guarantee).
2. `CastleMoatBuilder` loads that material (fallback: slice-1 URP/Lit path if load fails — Guard + FlowTrace.Warn, the `TryPlaceBridgePrefab` degradation pattern); collapses the two sub-rings back to one mesh (bands move into the shader); paints the plinth foam mask into `color.r`.
3. **`Assets/Resources/Data/moat-water.json`** recipe (§2.7) + a `ReadMoatWaterRecipe` mirroring `ReadSouthGatePos`.
4. FlowTrace: `Step("CastleMoat", "stylized water: shader=<ok|fallback>, recipe=<ok|defaults>, verts=N")` — §12 instrumentation from the first line.
5. Verify in a **player build** + headless screenshot compare (the run-defenders screenshot lane), not editor-only.

Files: CastleMoatBuilder.cs, new shader, new material, new json. MoatWaterShimmer.cs unchanged (property-name compatible).

### Slice 3 — life + drama (feel polish, each item independently droppable)

1. **`MoatWaterMood`** (new file, `Assets/_Modules/Village/World/`) — posture tint per §2.6, flag `ff.moatmood`.
2. **Fish under-water tint** + optional ripple rings (`FishSchool.cs`, additive).
3. **Bridge-pier foam boost** (paint `color.r` near the south span in the builder).
4. Foam-line audio one-shot candidates (lap SFX loop, very quiet) — route via `CoreServices.Audio`, owner call.

---

## 5. OPEN CALLS FOR THE OWNER (taste only — everything above works with any answer)

1. **Palette.** Keep the established de-glossed teal family (recipe defaults above are teal-derived), or shift toward a deeper storybook blue now that the water is bands-not-glass? *(One json edit either way — pick after seeing slice 1.)*
2. **Water clarity.** Should fish be visible through the shallow band (semi-transparent shore, recommended) or is fully-opaque painterly water (fish as surface dimples/shadows) more your storybook? This decides `_ShallowAlpha`.
3. **Motion intensity.** Foam pulse: slow breathing (~8 s cycle, stately) vs. lively lapping (~3 s)? And band-edge wobble: subtle or visibly wavy? *(Two recipe numbers: `foamSpeed`, `bumpStrength`.)*
4. **Fish density.** 10 feels sparse over a 4-sided ring; the school currently only lives on the south band (:359). Options: keep south-only 10 (frames the main approach), or 4 small schools of 5-6 (one per side, cap raised to ~24 — still trivially cheap, but it's your Pi). *(Recipe `fishCount` + a `schoolsPerSide` if you want the spread.)*
5. **Hostile mood tint** — yes/no, and how theatrical? Subtle steel-shift (recommended default) vs. clearly-darkened "the moat knows" drama. *(Recipe `mood` block; flag ff.moatmood lets you feel-test it in isolation.)*
6. **Moat width taste check.** The band is now 14 m (44..58) — derived, and it fixed "bridges crossing dry grass" (F8 flag_14). But CASTLE_MOAT_DESIGN_NOTE.md records your earlier "~3 units wide, NOT a wide flood" directive. Confirm the 14 m channel is the current canon (this doc assumes yes — the raised-plinth redesign superseded the thin-ring frame) so the design note can be banner-updated per §15.

---

*No code was changed in this order. Implementation = a WO per slice; slice 1 is a single-file change ready to spec.*

---
**OWNER RULING (2026-07-03):** moat width = the live ~14m band — correct by design (the
water must cover the seam from castle plinth to OuterWorld terrain). The older ~3-unit
note is superseded/bannered. Width question CLOSED; remaining open calls: color/mood,
motion intensity, fish density.

**Owner verification method (07-03):** the SOUTH BRIDGE is the ground truth for moat
width — it spans castle plinth → OuterWorld terrain, so its measured span bounds the
seam gap the water must cover. Measured: bridge ≈ 22.2m end-to-end (10.85 local ×
2.049 scale), ends bedded on land both sides → open water ≈ the 14m band. STANDING
ORACLE for any moat/water change: water band must remain fully covered by the bridge
span (band ⊂ bridge extent) or the crossing visually breaks.
