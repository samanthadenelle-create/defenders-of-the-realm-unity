# Hero Mesh Decimation — repeatable process

**Status:** **ADOPTED (2026-09-03).** `Assets/HeroContent/Ranger.fbx` and `Mage.fbx` now ship at
**~50,000 triangles** — see §10. Knight is deliberately untouched (§9).
**Owner ruling this serves:** *"50k looks good"*, given after comparing the densities in
`Builds/decimate-poc/COMPARE.html`. (Superseding the earlier *"for right now to push to live let's
leave those alone, but let's definitely decimate them and get that process going."*)

This doc is the process. It is written so the **next** character needs no rediscovery.

> ## ⛔ REPLACING A SHIPPING ASSET NEEDS TWO ARGUMENTS THE PoC DID NOT
> A ratio-1.0 round-trip through Blender is **not** byte-faithful to Unity's cached rig. Two
> defaults silently break the Humanoid avatar, and **both fail on the round-trip CONTROL too** —
> which is how you tell rig damage apart from decimation damage. Always pass:
>
> | argument | without it Unity logs | why |
> |---|---|---|
> | `root=<NodeName>` | `Rig Error: Avatar creation failed: Transform 'Armature' not found in HumanDescription.` | Blender's importer **discards the source root node's name** and always calls the armature `Armature`. The `.fbx.meta` `humanDescription.skeleton` lists all 103 transforms **by name**, so the rename orphans the whole rig. Read the value off the FIRST `- name:` under `skeleton:` — `Ranger(Clone)`, `Mage(Clone)`. |
> | `bone_orient=exact` (now the DEFAULT) | `Rig Error: Avatar Rig Configuration mis-match. Bone length in configuration does not match position in animation file:` with per-bone errors up to **435 mm** | `automatic_bone_orientation=True` re-aims every bone at its child. Harmless for a look-at-it PoC, wrong for a replacement: the cached rest pose no longer matches. |
>
> **The oracle is Unity, not Blender.** A Blender→Blender round-trip measured **0.000 mm** of bone
> displacement under *both* settings, so Blender cannot see this class of damage at all — it is
> self-consistent while diverging from the source. The proving control is the reverse one:
> **the pristine originals import with ZERO rig errors**, so *any* `Rig Error` on a re-imported
> FBX is round-trip damage you introduced. Grep the gate log for it; the marker alone will not
> catch it, because `COMPILE_GATE_OK` is emitted regardless.

---

## 0. Why — the measurement that justifies it

| | Ranger | Mage | KnightV3 |
|---|---:|---:|---:|
| source bytes | 13,946,976 | 8,565,536 | 8,561,776 |
| **triangles** | **314,892** | **169,110** | **9,808** |
| vertices | 157,273 | 84,532 | 5,406 |
| geometry as % of FBX | 70.7% | 61.3% | 4.3% |

KnightV3 ships at **9,808 triangles** and is the most-played hero. Ranger is **32x** the Knight's
density. Blend shapes, bone count and vertex channels are already ruled out — it is raw triangle
count. (Confirmed independently in Blender: Ranger = 1 mesh, 314,892 tris, 101 bones, 1 UV set,
0 vertex colours, and its 2 "blend shapes" move **zero** vertices.)

---

## 1. Tool

**Blender 5.2 LTS, already installed** at
`C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`.
Free, headless-capable, nothing to install, **no new tool required and no cost**.

Blender's **Decimate → Collapse** is the right operator for a skinned character: it is
quadric-error-metric edge collapse, and it **interpolates vertex groups (skin weights)** rather than
dropping them. Unity has no built-in decimator, and Unity's LOD import cannot reduce the source asset.

## 2. Scripts (tracked, reusable)

| file | does |
|---|---|
| `tools/mesh/decimate_skinned_fbx.py` | imports an FBX, decimates to each requested triangle target, exports one FBX per density, writes `decimate-report.csv` |
| `tools/mesh/render_fbx_views.py` | renders identical-camera greyscale evidence views of one FBX |

## 3. Steps

```bash
BL="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
NAME=Ranger                       # character to process
POC=D:/eoa/Builds/decimate-poc    # scratch, gitignored

# (1) COPY the shipping asset. NEVER point the tool at Assets/.
#     Heroes live in Assets/HeroContent/ (migrated out of Assets/Resources/Heroes 2026-09-03,
#     which now holds only the controllers + the deliberately-local Knight set).
mkdir -p $POC/source $POC/out $POC/renders
cp "Assets/HeroContent/$NAME.fbx" "$POC/source/${NAME}_COPY.fbx"

# (2) Decimate. The first output is always a ratio-1.0 ROUND-TRIP CONTROL.
#     root= is MANDATORY if the output will replace the shipping asset -- see the
#     banner at the top of this doc. Harmless to pass always, so pass it always.
"$BL" -b --factory-startup --python tools/mesh/decimate_skinned_fbx.py -- \
      "$POC/source/${NAME}_COPY.fbx" "$POC/out" 50000 25000 10000 "root=${NAME}(Clone)"

# (3) Evidence renders. framing.json is written once then REUSED, which is what
#     guarantees every density is framed identically.
for tag in orig-roundtrip 50k 25k 10k; do
  "$BL" -b --factory-startup --python tools/mesh/render_fbx_views.py -- \
        "$POC/out/${NAME}_COPY_${tag}.fbx" "$POC/renders" "$tag" "$POC/renders/framing.json"
done
#     A 5th arg forces the up-axis guess: auto (default) | yes | no. Pass "yes" for a
#     character carrying a long horizontal prop -- the guess reads the SMALLEST bounds
#     axis as depth, and the Mage's staff makes that the wrong one, so he renders lying
#     down. The close-ups aim at the top-slab / mid-slab vertex centroid, NOT the bounds
#     centre, for the same reason: that staff drags the bounds centre ~0.5 m off his head
#     and the head shot came back empty.

# (4) Open $POC/COMPARE.html to judge the densities side by side.
```

Runtime: about 3 minutes end to end for Ranger. Unity does not need to be closed — this process
never launches Unity and never touches the Unity project lock.

### Why the round-trip control exists
Ratio 1.0 re-exports the mesh **unchanged** through Blender. Its size (12.05 MB, vs the 13.30 MB
source) is the exporter's own format difference. **Always compare a density against the round-trip
number, not against the source** — otherwise you credit decimation with ~1.25 MB it did not save.

### Two traps baked into the script
- **Decimate cannot be applied while shape keys exist.** The script verifies every shape key is
  inert (moves no vertex) and only then removes it. A character with a **live** blend shape
  **aborts the run** rather than silently losing it.
- **Decimate must be moved to modifier index 0**, ahead of the Armature modifier. Applied out of
  order it bakes the skinning into the mesh. The script reorders it explicitly.

## 4. What to verify after — every time

1. **`decimate-report.csv`**: `bones`, `vgroups`, `uv_sets` must be **unchanged** at every density.
   For Ranger: 101 / 60 / 1 at all four. A drop here means skinning or UVs were lost — reject it.
2. **`[DECIM] DECIMATE_OK`** printed on a fresh log. Judge the marker, never the exit code.
3. **Framing identical**: every render logs its bounds; they must match across densities.
4. **Look at the renders.** `COMPARE.html`, head close-up first — that is where loss concentrates.
5. **In motion** — see §7. Not optional, and not provable from these stills.

**If the output is REPLACING a shipping asset, four more, all mandatory:**

6. **`.fbx.meta` byte-identical before and after** (`sha256sum`) — it carries the GUID every prefab,
   controller and Addressables entry resolves through, plus this asset's `meshCompression`. Replace
   the FBX bytes *beside* it; never regenerate it. `git status` must show the `.fbx` modified and
   **not** the `.meta`.
7. **Zero `Rig Error` in the gate log** — `grep -c -i "Rig Error" Builds/compile-gate.log` must be
   `0`. `COMPILE_GATE_OK` is emitted even when the avatar failed to build, so the marker does not
   cover this. See the banner at the top of this doc for the two arguments that cause it.
8. **Originals backed up outside `Assets/`, gitignored, with hashes** — `Backups/` is gitignored, so
   `Backups/mesh-decimation-<date>/` with a `SHA256.txt`. One `cp` reverts.
9. **The bundles RE-HASH.** Addressables bundle names are content-hashed, so a decimated hero needs
   its own `tools\r2-ship.ps1` push before any build reaches a device — a previous push cannot cover
   it (CLAUDE.md §16).

## 5. Measured result — Ranger

| density | ratio | tris | verts | FBX bytes | gzip | vs round-trip |
|---|---:|---:|---:|---:|---:|---:|
| original (round-trip) | 1.0 | 314,892 | 157,273 | 12,631,836 | 11,813,180 | — |
| 50k | 0.15878 | 50,000 | 24,822 | 2,381,596 | 2,153,701 | −81.2% |
| 25k | 0.07939 | 24,999 | 12,321 | 1,337,100 | 1,140,485 | −89.4% |
| 10k | 0.03176 | 9,999 | 4,820 | 692,828 | 514,677 | −94.5% |

Bones 101, vertex groups 60, UV sets 1 — **identical at every density.** The rig survives.
The 10k projection (~94%, under 1 MB) is now a **measurement**: 94.5%, 0.66 MB.

## 6. What the cut costs, visually

Renders use one flat grey material and luminance-only studio light, so every difference is **shape**,
never colour. Cavity shading is on, which makes lost topology read as visible faceting.
**No texture is applied — these show the geometry-only worst case.**

- **50k** — silhouette indistinguishable at full-body. Face fully readable: eyes, nose, mouth and ear
  points all intact. Losses: fine hair strands flatten, the brow-band edge softens, cheek and jaw
  planes pick up mild faceting. Cloak tatters keep their shape but their edges sharpen slightly.
- **25k** — silhouette still holds. Face **shape** survives but the surface goes clearly polygonal:
  hair becomes a smooth dome with no strands, the mouth flattens toward a crease, eye sockets
  shallow out. The belt buckle becomes a visible octagon. Cloak tatter tips become spikes.
- **10k** — silhouette still reads as this character at full-body distance, but the **face collapses**:
  the mouth is essentially gone, the eyes are shallow dents, the hair is a featureless dome. Hands
  become blobs with stray spikes. Torso armour detail flattens into facets. Cloak edges break into
  shards.

**Where loss lands, in order:** the **face** first (hair, mouth, eye sockets), then **hands and
finger detail**, then **cloth-fold and tatter edges**, then **small hard props** (belt buckle, quiver
arrows). The **overall silhouette is the last thing to go** and survives even 10k.

**Why Knight parity is not the right target for Ranger.** KnightV3 gets away with 9,808 triangles
because it is fully armoured — plate, helm, hard flat panels, and no exposed sculpted face. Ranger is
an unhelmeted character whose value is in a sculpted face, layered cloth and a tattered cloak. Same
art style, different geometric demand. "Knight parity" is a floor proof, not a spec.

**A texture caveat that matters here:** Ranger's basecolor is only **512×512 / 100 KB** for the whole
character, so the face gets roughly a 40-pixel patch. The texture **cannot** paint back a face that
geometry stopped describing. Ranger's quality currently lives in geometry, which is exactly why it is
314k tris.

## 7. The animation risk — stated plainly

Decimating a **skinned, rigged** mesh is not like a static prop. It can look perfect in T-pose and
distort under motion. Collapse merges vertices and **averages their bone weights**, so a vertex can
end up weighted to bones its neighbours are not — and that shows up only when those bones move.
Every image in §6 is a T-pose still, so none of them can clear this risk.

**What survived, measured:** 101 bones and all 60 vertex groups at every density; the Armature
binding intact; UVs intact; bone names unchanged.

**What lowers the risk a great deal for these heroes:** `Ranger.fbx` contains **no animation**. It
holds a single-frame T-Pose take only, and Unity's importer is set `importAnimation: 0` with
`clipAnimations: []` and `animationType: 3` (Humanoid). Animation comes from the `.controller` and
separate clip assets. So decimation touches **mesh + rig**, never a clip library, and the Humanoid
avatar re-maps by bone name.

**What must still be checked in motion, per character, before anything ships:**
1. **Face / jaw / brow** under any expression or head-turn animation — highest weight density, first
   to break. At 10k there is no longer enough geometry here to deform smoothly.
2. **Finger and wrist joints** — collapse merges across knuckles. Watch for fingers fusing or
   collapsing when the hand grips a weapon.
3. **Shoulders and hips at full rotation** — the classic candy-wrapper pinch. Check the arm raised
   overhead and the leg at full stride.
4. **Cloak / tatter edges during locomotion** — thin geometry loses vertices fastest; watch for
   flicker, self-intersection, or tatters visually detaching.
5. **Weapon attach points** — confirm the bow and quiver still line up in the attack animation.
6. **Re-check that the Humanoid avatar builds** with no bone-mapping errors after re-import.

**Verdict on the rig:** it survives structurally at all three densities. The **deformation quality**
at 10k is the open question, and it cannot be answered from stills.

## 8. What decimation does NOT fix — the honest wire number

The shipped payload is ~157 MB. Decimation is a **mesh-only** win. It does nothing for textures,
audio, code, or the Addressables catalog.

The wire discount, however, **runs the other way for mesh** than it did for the 29.2 MB → 7.7 MB
case, and this is measured, not assumed:

| | uncompressed | gzip | compressor squeeze |
|---|---:|---:|---:|
| Ranger round-trip | 12,631,836 | 11,813,180 | **only 1.07x** |
| Ranger 10k | 692,828 | 514,677 | 1.35x |
| yesterday's payload, for contrast | 29.2 MB | 7.7 MB | 3.79x |

Dense quantized mesh data compresses **poorly** — 1.07x. That cuts both ways: the compressor cannot
shrink it, so **removing it is the only way to shrink it, and the saving lands on the wire nearly
1:1.** Ranger at 25k saves **≈10.7 MB compressed**; at 10k, **≈11.3 MB compressed**. The fact that
yesterday's 29.2 MB squeezed to 7.7 MB tells us that payload was **not** mesh-dominated — it was
dominated by data that compresses well.

**Caveats — do not over-claim:**
- These are **FBX** bytes. Unity does not ship the FBX; it ships its own serialized mesh inside an
  Addressables bundle. The direction and rough magnitude carry over (Unity mesh data is also dense
  and quantized), but **the definitive number needs a content build plus a bundle-size read**, which
  this investigation deliberately did not do.
- Ranger + Mage together are the realistic scope. Best case across both, roughly **14–18 MB** off a
  157 MB payload — real and worth having, about **10%**, but it is **not** the headline fix. Textures
  and the rest of the payload are where the other 90% lives.
- `Ranger.fbx.meta` has `isReadable: 1`, which keeps a CPU copy of the mesh at runtime. That is a
  **memory** cost, not a download cost, and it scales down with triangle count too.

## 9. Rules for using this process

- ⛔ **Never point the tool at a shipping asset.** Always copy to `Builds/decimate-poc/source/` first.
  The shipping `.fbx.meta` files are owned by other lanes.
- ⛔ Adopting a decimated mesh into the game is a **separate, owner-approved** change. This process
  produces evidence, not a shipped asset. (Ranger + Mage at 50k **were** so approved — §10.)
- ⛔ **Knight is off-limits.** `Knight.fbx`, `Knight.fbm/` and `KnightV3.fbx` stay LOCAL and
  undecimated: `TroopFactory` resolves troop bodies through a path with no Addressables arm in a
  player build, so touching them turns `troop-shieldguard` and `troop-echo-legionnaire` into
  capsules. KnightV3 is 9,808 tris anyway — there is nothing to win.
- Keep intermediates in `Builds/decimate-poc/` (PoC) or `Builds/decimate-ship/` (an adoption pass) —
  both gitignored. Only the two scripts and this doc are tracked.
- This process must not edit any character's `.fbx.meta`. Re-import settings are a different
  decision.
- Known limitation: Workbench **wireframe** shading is viewport-only and renders blank offscreen.
  Topology loss is read from faceting in the solid+cavity render instead. Do not re-add a wireframe
  pass expecting it to work.

---

## 10. Adoption record — Ranger + Mage at 50k (2026-09-03)

Owner ruling **"50k looks good"**. The shipping FBX bytes in `Assets/HeroContent/` were replaced;
both `.fbx.meta` files are untouched, so both GUIDs, both `meshCompression: 1` settings and the
`Hero_Ranger` / `Hero_Mage` Addressables Remote groups are intact.

| | tris before | tris after | FBX bytes before | FBX bytes after | saved |
|---|---:|---:|---:|---:|---:|
| Ranger | 314,892 | 50,000 | 12,631,756 *(round-trip)* | 2,381,532 | −81.1% |
| Mage | 169,110 | 49,999 | 7,106,844 *(round-trip)* | 2,397,900 | −66.3% |

Mage's ratio cut is smaller because he started at half Ranger's density — **do not assume Ranger's
81% transfers to the next character.** Against the *source* bytes (13,946,976 / 8,565,536) the two
together drop **~17.7 MB** of FBX, but the honest wire number is §8's: mesh compresses at only
**1.07x**, so the saving lands on the wire nearly 1:1 — roughly **14–18 MB off a ~157 MB payload,
about 10%**. Real, and **not** the fix for the 65% stall. Textures at 98.9 MB remain the largest
block.

Rig integrity, measured at both densities: **Ranger 101 bones / 60 vertex groups / 1 UV set**,
**Mage 101 / 61 / 1** — identical to their originals. Unity re-imports both with **zero rig errors**.

- Originals: `Backups/mesh-decimation-2026-09-03/` (gitignored) + `SHA256.txt`.
- Working files and fresh renders: `Builds/decimate-ship/`, incl. its own `COMPARE.html`.
- Gates on the installed assets: `COMPILE_GATE_OK`, `REGRESSION_OK 354/354 suites`, with
  `[hero-remote-content]` and `[addressable-troop-visual]` both green.
- Two scripts gained arguments this pass: `root=` and `bone_orient=` on the decimator (see the
  banner), and a `standup` override plus centroid-aimed close-ups on the renderer (see §3).

**Still open, and only the owner can close it:** every render is a T-pose still, and Collapse
averages bone weights across merged vertices, so a mesh can be perfect standing and distort in
motion. `Ranger.fbx` carries no animation (clips live in the `.controller`) and the avatar rebuilds
clean, so the rig survives *structurally* — deformation quality does not follow from that. Check on
a device: **face/jaw on a head-turn, fingers and wrist gripping a weapon, shoulder and hip at full
rotation, cloak tatters in locomotion, weapon attach alignment.**
