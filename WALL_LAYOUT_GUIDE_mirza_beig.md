# Wall Layout Guide — "Mirza Beig" folder (moat + castle build)

> Research date: 2026-05-30. Read-only research. No `.cs` or `.unity` files were edited.

## TL;DR — Important finding first

**The `Assets/Mirza Beig` folder does NOT contain any wall, castle, gate, tower,
moat, bridge, or other modular architecture meshes.** It is the **Mirza Beig
"Ultimate VFX"** particle / shader / VFX pack. There is nothing in it to lay out a
curtain wall with — no `.fbx`/`.obj` building meshes, no wall-segment prefabs.

If you came here to build a moat + castle, the geometry must come from a different
pack (in this project, that is the **Polyperfect Low Poly Ultimate Pack** — see the
"Where the actual walls live" section). The Mirza Beig pack is still useful for the
build: it supplies the *effects* that dress a castle (torch fire, smoke, dust,
water mist, magic glows). That role is covered at the end of this guide.

---

## What is actually in `Assets/Mirza Beig`

Verified folder tree (top level + relevant sub-folders):

```
Assets/Mirza Beig/
├─ _DOCS/                         (pack documentation lives here)
├─ Editor Extensions/
│  └─ Utilities/
│     ├─ Multi-Asset Renamer/
│     ├─ Particle Playback/
│     └─ Particle Scaler/
├─ Particle Systems/
│  ├─ Ultimate VFX/
│  │  ├─ Demos/
│  │  ├─ Expansions/
│  │  ├─ Materials/
│  │  ├─ Prefabs/        ← all prefabs here are PARTICLE EFFECTS, not meshes
│  │  ├─ Scenes/
│  │  └─ Textures/
│  └─ _Common/  (Materials, Post-Processing Profiles, Prefabs, Scripts, Shaders, Textures)
├─ Scripting/
│  └─ Effects/  (Particle Affectors, Particle Flocking, Particle Force Fields,
│                Particle Lights, Particle Plexus)
└─ Shaders/
   ├─ Image Effects/  (Resources, Tests)
   ├─ Particles/      (Add Soft, Alpha Cutoff, Animation Blend, Distance Fade,
   │                   Distortion, Intersection Highlight, Mask, No Fog)
   └─ Standard/        (Standard-Terrain Rain.shader, Standard-Terrain Rain 2.shader)
```

This is unmistakably a **VFX pack**: particle prefabs, particle shaders, image-effect
(post-processing) shaders, and editor utilities for scaling/playing particles. There
are zero architectural building meshes.

### Wall / castle / moat inventory found in this folder

| Category | Found? | Notes |
|---|---|---|
| Wall segments | **None** | No straight/curved wall meshes. |
| Wall corners | **None** | — |
| Gates / gatehouses | **None** | — |
| Towers | **None** | — |
| Battlements / crenellations | **None** | — |
| Pillars / foundations | **None** | — |
| Bridges | **None** | — |
| Moat / water planes / embankments | **None** | Only a `Standard-Terrain Rain` shader (weather, not water geometry). |

Verified counts (from a full recursive scan):
- **`.prefab` files: 564** — every one is a particle system.
- **`.fbx` / `.obj` mesh files: 0** — there is no building geometry of any kind.
- The only prefabs whose names contain "wall"/"gate" are VFX:
  `pf_vfx-ult_demo_psys_loop_warpGate.prefab`, `..._loop_voidgate.prefab`,
  `..._loop_stargate.prefab` (sci-fi/magic portals), and a `Gravity Clock UVFX`
  demo under `Demos/Wallpapers/`. None are architecture.

**Real documentation DOES exist** in `Assets/Mirza Beig/_DOCS/` (plain-text READMEs):
`README - Ultimate VFX.txt`, `README - Action VFX.txt`, `README - Storm VFX.txt`,
`README - Particle Force Fields.txt`, `README - Particle Plexus.txt`,
`README - Advanced Particle Scaler.txt`. The Ultimate VFX README confirms:
- Pack: **Ultimate VFX v3.2.0+** by **Mirza Beig**
- Online docs: **http://www.mirzabeig.com/products/ultimate-vfx/**
- Tools menu: **Window > Mirza Beig**
- Some demo prefabs ship **disabled** — enable the GameObject after placing it.
None of the docs mention walls, castles, or any modular geometry — confirming this
is purely a VFX pack.

---

## Where the actual walls live (use this for the build)

Per the project's `CLAUDE.md` and `docs/polyperfect-asset-catalog.md`, the modular
building geometry in this project is the **Polyperfect Low Poly Ultimate Pack**:

- Pack root: `Assets/polyperfect/Low Poly Ultimate Pack/`
- Always use the `_M` quality tier: `_M/Prefabs_M/<Category>_M/`
- The pack is **gitignored** — on a fresh clone, re-import then run
  `Defenders/Art/Fix Polyperfect URP Materials`.
- **Check `docs/polyperfect-asset-catalog.md` before referencing any prefab name** —
  that catalog is the source of truth for exact Polyperfect wall/tower/gate names.
  (This guide deliberately does not invent Polyperfect prefab names.)

So the practical workflow is:
1. Build the **curtain wall, towers, gatehouse, and bridge** from Polyperfect `_M` prefabs.
2. Build the **moat** from a terrain depression or a water plane (project's water solution).
3. **Decorate with Mirza Beig VFX** (torches, braziers, smoke, water mist, dust).

---

## Modular layout approach for a rectangular curtain wall + moat

Because the Mirza Beig pack provides no wall dimensions, the numbers below are an
**assumption you must confirm** against the actual Polyperfect wall prefab once you
pick it. The method is what matters; plug in the real segment length when known.

### Assumed module (CONFIRM against your wall prefab)
- **Wall segment length:** assume **4 m** along its long axis (common for low-poly
  modular walls; verify in the prefab's mesh bounds / by snapping two copies).
- **Wall height:** assume **~4–5 m** to the walkway, plus crenellations on top.
- **Snap grid:** set Unity grid snap (Edit > Grid and Snap) to the segment length
  (e.g. **4 m** move snap) and **90°** rotation snap. With a clean modular kit,
  pieces then tile seamlessly with zero manual nudging.
- **Pivot/origin convention:** low-poly modular walls usually pivot at one end on
  the ground (origin at floor level, at the start of the segment). If a piece
  pivots at its centre instead, offset placements by half the segment length.
  Confirm by dropping one piece at world origin and reading its transform.

### Recommended build — closed rectangular keep

Pick a footprint in whole segment-multiples so corners land cleanly. Example with a
4 m segment and a **40 m × 32 m** outer wall (10 segments × 8 segments):

1. **Lay one side first, along +X.**
   - Place wall segments end-to-end at `x = 0, 4, 8, … 36` (z = 0). Snap (4 m) keeps
     them flush. Y = 0 (ground).
2. **Turn the corners with corner pieces (or rotated wall ends).**
   - At each of the 4 corners, place a corner tower (see step 4) and rotate the next
     run by **90°**. Run the four sides: +X edge, then +Z edge, then −X edge, then −Z edge.
   - If the kit has dedicated corner wall pieces, use them; otherwise butt two wall
     ends at the corner and hide the seam behind a corner tower.
3. **Keep everything on the grid.** Every segment start should be a multiple of the
   segment length. This guarantees the loop closes with no gap or overlap on the
   final piece.
4. **Corner towers — one at each of the 4 corners.**
   - Center the tower on the corner intersection point. Towers are usually wider than
     the wall, so they naturally cover the corner seam. Keep rotation aligned to the
     grid (0/90/180/270°).
5. **Gatehouse — replace ONE wall segment on the front (−Z or +X) side.**
   - Remove the wall segment where the entrance goes and drop a gatehouse/gate prefab
     in its place, snapped to the same grid cell. Face the gate **outward** (rotate so
     the opening points away from the courtyard).
   - If the gatehouse is wider than one segment, reserve 2 segment cells for it and
     shift the side's segment count accordingly so the loop still closes.
6. **Battlements/crenellations** sit on top of the walkway. If they are separate
   prefabs, place them at the wall's top Y, snapped along the same X/Z grid as the
   wall below. If the wall prefab already includes crenellations, skip this.

### The moat

7. **Carve the moat OUTSIDE the wall footprint.**
   - Offset the moat ring ~2–4 m beyond the outer wall face on all four sides, so
     there's a berm/walkway between wall base and water.
   - Build the moat as either (a) a sculpted **terrain trench** filled with a water
     plane, or (b) a flat **water plane** sunk below ground with embankment geometry.
     (No moat/water mesh exists in the Mirza Beig pack — use the project's water/terrain.)
   - Keep the water plane's top a little below the ground plane (e.g. Y = −0.5 to −1)
     so banks read as raised.
8. **Bridge at the gate.**
   - Center a bridge across the moat directly in front of the gatehouse, aligned to
     the gate's outward axis. Length = moat width + a little overlap onto each bank.
   - Snap the bridge's inner end flush to the gatehouse threshold; rest the outer end
     on the far bank. If using a drawbridge, pivot it at the gatehouse-side edge.

### Placement order (summary)
1. Set grid/rotation snap → 2. Lay one wall side → 3. Corners + 90° turns to close the
loop → 4. Corner towers → 5. Swap in the gatehouse → 6. Crenellations on top →
7. Carve moat outside the footprint → 8. Bridge across the moat at the gate.

---

## Dressing the build with Mirza Beig VFX (the part this pack is good for)

The pack ships **564 particle prefabs**. Per `README - Ultimate VFX.txt`, the only
expansion families present are exactly **five**: `XP - ACTION`, `XP - CONSTR. KIT`,
`XP - SHOCKWAVES`, `XP - STORM`, `XP - TITLES` (confirmed on disk — there is **no**
Water/Magic/Arcane expansion). Base prefabs are under `Ultimate VFX/Prefabs/`.
Useful picks for a castle scene — **every name below was verified to exist on disk**:

| Use on the castle | Prefab (exact, verified file name) |
|---|---|
| Torch / brazier fire | `pf_vfx-ult_demo_psys_loop_fire.prefab` |
| Campfire (courtyard) | `pf_vfx-ult_demo_psys_loop_campFire.prefab` |
| Dancing flame variant | `pf_vfx-ult_demo_psys_loop_fireDance.prefab` |
| Realistic fire (additive) | `pf_vfx-ult_demo_psys_loop_realisticFireAdd.prefab` |
| Fire embers (oneshot) | `pf_vfx-ult_xp-ckit_psys_oneshot_fireEmbers.prefab` |
| Sparks rising from fire | `pf_vfx-ult_demo_psys_loop_sparks.prefab` |
| Spark lights (glowing) | `pf_vfx-ult_demo_psys_loop_sparkLights.prefab` |
| Fireflies / ambient motes | `pf_vfx-ult_demo_psys_loop_fireflies.prefab` |
| Chimney / battle smoke | `pf_vfx-ult_demo_psys_oneshot_smoke3.prefab` |
| Looping action smoke | `pf_vfx-ult_xp-action_psys_loop_smoke.prefab` |
| Realistic smoke wisp | `pf_vfx-ult_demo_psys_loop_realisticSmokeWisp.prefab` |
| Moat mist / low fog | `pf_vfx-ult_demo_psys_loop_mist.prefab` |
| Dark / still water for moat | `pf_vfx-ult_demo_psys_loop_blackwater.prefab` |
| Smoky waterfall into moat | `pf_vfx-ult_demo_psys_loop_realisticSmokeyWaterfall.prefab` |
| Dust haze (banks/courtyard) | `pf_vfx-ult_demo_psys_loop_dusty.prefab` |
| Falling dust motes | `pf_vfx-ult_demo_psys_loop_dustDrop.prefab` |
| Magic glow at the gate | `pf_vfx-ult_demo_psys_loop_warpGate.prefab` |
| Reliquary portal glow | `pf_vfx-ult_demo_psys_loop_voidgate.prefab` |

> Note: there is no dedicated water-splash/ripple or rune-circle prefab in this pack.
> For literal water surface effects you'll lean on `mist`, `blackwater`, and the
> smoky-waterfall prefab, or use a separate water asset. The "gate" prefabs are
> sci-fi/magic *portals*, not stone gatehouses — use them only as a glow/FX accent.

Base/demo prefabs live under `Assets/Mirza Beig/Particle Systems/Ultimate VFX/Prefabs/`
(`Loop/` and `Oneshot/`); expansion prefabs under
`.../Ultimate VFX/Expansions/<family>/Prefabs/Loop/` (or `/Oneshot/`). Supporting
runtime behaviours are in `Assets/Mirza Beig/Scripting/Effects/` (Affectors, Flocking,
Force Fields, Lights, Plexus). **Tip from the README:** some demo prefabs ship
**disabled** — enable the GameObject after dropping it in. Tools menu: **Window > Mirza Beig**.

- **Image-effect (post-processing) shaders** (`Shaders/Image Effects/`) → atmosphere/bloom.
- **Advanced Particle Scaler / Particle Playback** (`Editor Extensions/Utilities/`) →
  resize an effect to fit a torch sconce and preview it without entering Play mode.

### Mirza Beig URP / setup gotchas
- **Render pipeline:** Ultimate VFX ships **Built-in (Standard) particle shaders**.
  This project is **URP**. Built-in particle materials show up **magenta** under URP
  and must be re-mapped to URP particle shaders (Edit > Rendering > Materials >
  Convert, or hand-swap to `Universal Render Pipeline/Particles/*`). Budget time for
  this before relying on any VFX prefab visually.
- **Image-effect shaders** in `Shaders/Image Effects/` are **legacy
  `OnRenderImage`/`MirzaPostProcessing.cs`** style — they will **not** drive URP
  post-processing. Use URP Volume overrides instead; treat these as reference only.
- **Particle Scaler:** if you parent/scale a VFX prefab by hand, particle sizes do
  **not** scale with the transform — use the included **Particle Scaler** utility so
  emission/size/velocity scale together.
- **Check `_DOCS/`** for the bundled PDF/readme and the original Asset Store link
  (Mirza Beig — Ultimate VFX) for the authoritative effect list and per-effect notes.

---

## Bottom line
- **For walls/towers/gates/bridges:** do not use Mirza Beig — it has none. Use the
  **Polyperfect `_M`** prefabs and the names in `docs/polyperfect-asset-catalog.md`.
- **For the moat water and terrain trench:** use the project's water/terrain tooling.
- **For atmosphere on the finished castle:** Mirza Beig Ultimate VFX is exactly the
  right pack (fire, smoke, mist, glow) — after converting its materials to URP.
