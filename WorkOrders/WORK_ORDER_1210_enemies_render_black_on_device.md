# WORK ORDER 1210 - Every enemy renders as a flat black silhouette on device

**Status:** CLOSED 2026-08-25 - **NOT A DEFECT. OWNER RULED THE LOOK INTENDED AND PINNED IT.** Verbatim: *"yes thats the intended look for hollow, pin it"* (and earlier, *"i dont care so much about the black enemies, until you said something i assumed that was expected"*). The flat black Hollow silhouette is ART, not a rendering fault. ⛔ The pin lives in CODE, at the exact place a future seat would "fix" it - `EnemyFactory.FamilyFallbackTint` - not only in this file, because a ruling recorded where nobody reads it is indistinguishable from no ruling. ⚠ **THE CLI WAS WRONG TO CALL THIS A SHIP-BLOCKER.** It raised the block on its own reading of the evidence and the owner overruled it: what the game should LOOK like is hers, never the CLI's. The evidence below stays as history and as the mechanism hunt (see the caveat), not as a defect report.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1210 -> 1211 in the same edit)
**Silo:** Village / enemies + rendering
**Reported:** the owner, 2026-08-25, felt-testing build `2026.08.25.341262`: *"combat is wrong still"*.

---

## ⚠ WHAT STAYS OPEN AFTER THE CLOSE - the mechanism, not the look

The look is ruled and pinned. **We still do not know WHY the bodies render black**, and that matters
for one reason only: if a future lighting, ambient or probe change makes the Hollow bodies suddenly
LIT, that is not an improvement - it is this ruling being broken by accident, and nobody will
recognise it as a regression because it will look like a fix.

`EnemyRenderDiagnostic` (committed, in the build) dumps ambient mode/intensity, probe and lightmap
counts, the directional light's culling mask against the enemy layer, and per-renderer/per-material
state **with the hero as the control group in the same frame**. One wave reads it.

⛔ Read that dump BEFORE touching ambient or probes in the merged world. Do not re-open this ticket as
a defect.

## The symptom, with a picture

`tmp/felt2/combat-191119.png` - a wave fight in bright daylight outside the castle. Every Hollow enemy
renders as a **solid black cut-out**, no shading, no texture, hard silhouette edges. In the SAME frame
the hero is fully lit and textured, the terrain and walls are lit, and the lock-on plate at the top of
the screen shows that enemy's portrait **fully textured and green-skinned**.

⭐ **Same creature, two renderers, only one of them painting.** That is the whole shape of the bug.

## What the captured data has ALREADY RULED OUT - do not re-hunt these

**1. It is NOT missing content / a missed R2 push (CLAUDE.md sec.16).** The models resolve and pool
normally:

```
[Flow:EnemyPool] Return key='model:Hollow_Walker': body returned to pool (now 4 dormant)
[Flow:Enemy] SnapBodyToGround(Enemy (hollow-walker)) ground=0.00 footGap=0.00 -> pivotY=0.00
```

The build was installed behind `R2_PUSH_OK` + `R2_PARITY_OK 43 object(s) verified` against
`catalog_2026.08.25.341262`, the same stamp the device reports. Capsules would be the missing-bundle
signature; these are the real meshes.

**2. It is NOT an unpainted or textureless body.** `EnemyBodyColorGuard`'s audit runs on device and
reports every slot healthy, per family:

```
[Flow:EnemyColor] colour audit (FINAL) 'Skeleton_Warrior' (id 'hollow-warrior', rig SkeletonHumanoid):
                  textured=6 painted=0 emissive=0 unpainted=0 repaired=0 - every slot carries a skin
[Flow:EnemyColor] colour audit (FINAL) 'Orc_Warrior' (id 'orc-warrior', rig OrcHumanoid): textured=1 ...
```

`unpainted=0 repaired=0` across skeleton AND orc families. The guard reads the FINAL material state
after `TripoMaterialFixer`, so the albedo is present. ⛔ Do not "fix" this by widening the colour
guard - it is already reporting the truth.

**3. It is not one family.** Hollow and Orc both audit clean and the owner saw black bodies in a Hollow
wave; confirm the family scope on the next capture rather than assuming.

## Therefore: the remaining candidates are the LIGHTING/SHADER path, and the next move is a capture

A textured mesh that renders black is receiving no light or resolving no lit shader variant. Plausible
and mutually exclusive enough to separate in ONE instrumented run:

1. **Ambient/probe state on dynamic renderers** - enemies are pooled, spawned at runtime, and never
   lightmapped. If `RenderSettings.ambientMode` resolves to Baked with no baked probes in the merged
   world, dynamic bodies get black ambient while lightmapped static geometry stays lit. **This fits the
   screenshot exactly** (lit terrain and walls, black dynamic bodies) - which is why it must be
   MEASURED, not assumed. The hero is a different spawn path and may carry different probe settings.
2. **A stripped shader variant on Android.** URP strips variants aggressively; a keyword combination
   used only by the enemy material path can survive in the editor and vanish in the player. Editor
   never shows it; device always does.
3. **`SkinnedMeshRenderer` probe/anchor configuration** on the pooled bodies specifically
   (`lightProbeUsage`, `reflectionProbeUsage`, `probeAnchor`) differing from the hero's.

## What to build - instrument FIRST (CLAUDE.md sec.12)

⛔ **No edit until a captured line names the cause.** Two static theories were proposed for the capsule
incident on 2026-08-20 and BOTH were wrong at the cost of an hour; the device log settled it in one
line. Add a one-shot enemy-render diagnostic that emits, for the first spawned enemy of each family:

- `RenderSettings.ambientMode`, `ambientLight`, `ambientIntensity`;
- whether a live directional light exists and its intensity/culling mask vs the enemy's layer;
- per renderer: `lightProbeUsage`, `reflectionProbeUsage`, `probeAnchor != null`, `isPartOfStaticBatch`;
- per material: `shader.name`, `renderQueue`, and whether the resolved shader `isSupported`;
- the same five readings for the HERO's renderer in the same frame - **the hero is the control group,
  and the difference between the two is the answer.**

Run it on device, read it, THEN fix exactly what it names.

## Acceptance criteria

- The proving line is quoted in the RESULT - `[Flow:...]` naming the differing property.
- Enemies render lit and textured on the Seeker, judged by a device screenshot that is OPENED, not by
  a green marker: no headless gate can see this (the 2026-08-18 orientation lesson applies verbatim).
- A registered oracle for whatever class the cause belongs to - if it is a stripped variant, an oracle
  that the enemy shaders are in the always-included set; if it is probe configuration, an oracle on the
  spawn path's renderer settings.
- The diagnostic STAYS in the code, flagged off if noisy (sec.12 forbids stripping instrumentation).

## What NOT to touch

- ⛔ The R2 push / Addressables grouping. Parity is proven for this build; re-grouping rehashes every
  bundle and forces a full re-download for installed players.
- ⛔ `EnemyBodyColorGuard`'s thresholds or `ChromaFloor`. It is reporting correctly; silencing or
  widening it would only destroy the evidence that the art is fine.
- ⛔ Per-material hand-tinting to "brighten" enemies. That paints over a lighting fault and would ship
  a second colour authority - the exact double-owner failure this repo keeps paying for.
