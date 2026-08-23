**Status:** FIXED 2026-08-23 (57b2c4595) — the doorway normal is Root.right, not Root.forward; the previously-shipped fix aimed both rune planes at the arch’s stone side. 24 captures, magenta 0. §4 split to WO-1156. FELT-TEST: walk up to a dungeon portal from several angles. AWAITING OWNER CLOSE.

# WORK ORDER 1062 — The dungeon portal is a flat plane: it reads differently from every angle

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1062 -> 1063 in the SAME edit)
**Assigned:** CLI implements. Nothing is held — the §2 ruling landed the same day.
**Lane:** VFX / World (CLAUDE.md §9 — parallel lane)
**Class:** DEFECT (presentation) + one structural gap in the magenta safety net.
**Evidence:** owner screenshots 2026-08-22 — the **same** Stoneback Tier-2 portal from **three**
angles (NE, NW, SW), reading as three different objects.
**Screen:** `Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs`

---

## 0. One-line truth

**The portal's threshold is a flat, single-plane effect, so it only looks like a portal from the one
direction it faces.** From the front it is a bright swirl; from the side it is a black shard; from
another side it is a flat rune disc. A portal has to read as a portal from wherever the player walks
up to it.

---

## 1. What the three angles show — and why

The spawner attaches three separate VFX layers (`:218-228`):

| Field | Role |
|---|---|
| `GateVfx` | looping magic-circle rune ring at the **arch base** |
| `ThresholdVfx` | the doorway aura |
| `CircleVfx` | held as a plain `GameObject` — *"costs no VFXManager loop slot"* |

And the code records the owner's own pick at `:223-225`:

> *"Owner VFX pick 2026-08-16 (\"Magic circle dark star ... **use this rotated** for the ...\")"*

**That is the whole explanation.** A *magic circle* is authored to be seen face-on, lying flat.
Rotating it upright to stand inside an arch turns it into a **vertical disc with a single viewing
direction**:

| Screenshot | Camera | What the disc does |
|---|---|---|
| NW | roughly face-on | reads correctly — bright swirl, light rays, convincing |
| SW | oblique | the rune mandala is visible but flat, obviously a decal on air |
| **NE** | near edge-on | collapses into **black polygonal shards** — the disc seen almost side-on, its unlit back face rendering dark |

**The effect is not broken. It is being asked to be volumetric when it is planar.**

⚠ Note this is *not* a `VFXManager` loop-budget problem — `CircleVfx` deliberately costs no loop
slot. Do not conflate it with WO-1057.

---

## 2. The fix — OWNER RULING 2026-08-22: two VFX, each facing outwards

> **Owner ruling, verbatim:** *"think we need two vfs each facing outwards."*

**Two instances of the SAME owner-tagged effect, mounted back-to-back in the arch, each facing
outward along the doorway's normal.** Settled — no option list, no art pick needed.

### Why this is the right answer and not merely an acceptable one

- **It matches what a doorway IS.** You approach a portal *through* it, from one side or the other.
  Both approaches now get a face-on read of the effect the owner already chose.
- **It deletes the black-back-face problem by construction.** There is no back face pointed at the
  player any more, because each plane faces away from the other. The dark-slab symptom in the NE
  screenshot cannot recur.
- **It needs NO new VFX key.** It reuses `CircleVfx` as tagged, so nothing is substituted and it can
  ship immediately.
- **It is better than billboarding.** A disc that swivels to follow the camera reads as a sticker
  once the player circles it; two fixed outward planes stay physically anchored in the arch.

### Edge-on from the side is CORRECT — do not "fix" it

Viewed from directly beside the arch, both planes go edge-on and the effect thins to a sheet. **That
is right, not a defect.** From the side the player sees the stone arch — which is real 3D geometry —
with a thin plane of light held inside it. That is what a portal in a doorway should look like, and a
seat that adds a third plane to "fill in" the side view has made it a glowing blob instead of a door.

### THE EXACT SPEC — owner-authored 2026-08-22, do not invent values

> **Owner, verbatim:** *"as you can see from third image it looks good just need one rotated 90 other
> rotated -90"* — and — *"put .25 between them."*

| Parameter | Value |
|---|---|
| Instance A rotation | **+90** |
| Instance B rotation | **-90** |
| Separation along the threshold normal | **0.25** |
| Source asset | the SAME `CircleVfx` key, both instances |

**The third screenshot (SW) is the visual reference for "correct."** The owner has confirmed the
effect itself already looks right at that heading — the work is purely to make both approaches look
like that one. **Do not re-tune the effect, its colour, its scale or its speed.** If the result does
not match the SW screenshot, the rotation or the offset is wrong, not the art.

⚠ **`+90` and `-90` are the owner's numbers and they are the spec.** If the arch's local axes make a
literal `+90/-90` point the planes somewhere other than outward along the doorway, **the intent is
"one facing each way out of the arch"** — implement that and **state in the RESULT which axis you
applied it on**. Do not silently substitute different numbers, and do not implement a literal
rotation that visibly faces the wrong way.

### Remaining implementation notes

- Both instances derive from the **same** prefab/key. No second asset, no divergence.
- **0.25 also solves z-fighting** — the planes are not coplanar, so they cannot shimmer. Still verify
  at distance as well as up close, since the two faces are close together.
- Each plane's material wants to be unlit / not-lit-from-behind, so the outward face reads the same
  at every time of day.
- **Still no `VFXManager` loop slot** — `CircleVfx` is a plain GameObject by design (`:226-228`), and
  two of them is still zero slots.

## 3. ⛔ THE MAGENTA PATCH — a real gap in the safety net, not just a bad material

Screenshot NE carries a **magenta patch** at the top of the arch. Magenta is this project's canonical
"shader failed to resolve" tell, and `MagentaGuard` exists precisely to catch it.

**But read what the guard actually does** (`MagentaGuard.cs:20-22`):

> *"On **every scene load**, scan active Renderers' sharedMaterials..."*

**`DungeonWorldPortalSpawner` builds its portals at RUNTIME, after scene load** — it *"waits (re-scan)
for the OuterWorld"* and constructs `new GameObject($"DungeonWorldPortal_{def.DungeonId}")` at `:570`.

**So the portal is spawned into a scene the guard has already finished scanning, and is never
checked.** That is a structural hole, and it explains how a magenta renderer survives in a project
that has both a global guard *and* (per the guard's own header) a dedicated Portal fixer among its
~8 targeted ones.

**Fix: give `MagentaGuard` a public `Scan(GameObject)` entry and have the portal spawner call it on
each portal it builds.** Any other runtime spawner should do the same — the guard's value is that
the offender *self-identifies in the break-log* instead of costing a guess-and-rebuild cycle, and a
spawner that never calls it gets none of that.

⚠ **The guard also LOGS what it catches.** If it had run, we would already know the material and the
dead shader. **Check the break-log for a MagentaGuard line before assuming it never fired** — a
caught-and-recovered magenta and a never-scanned magenta look identical on screen but have different
fixes.

---

## 4. Also visible, smaller — world placement

In two of the three shots the portal **intersects foliage**: a tree canopy cuts through the arch
(NE and SW), and in SW the camera is clipping through a tree trunk that fills a third of the frame.

The portal is placed without clearing its footprint. **Scope this modestly** — a clearance test at
spawn that rejects or nudges a position overlapping tree colliders, and a minimum camera standoff.
**If either turns out to be non-trivial, split it into its own ticket rather than growing this one.**

---

## 5. What NOT to touch

- **The owner's VFX pick.** `GateVfx` / `ThresholdVfx` / `CircleVfx` keys stay as tagged (§2).
- **`CircleVfx`'s no-loop-slot property.** It is deliberately a plain GameObject; keep it off the
  `VFXManager` pool (`:226-228`).
- **`MagentaGuard`'s scene-load scan.** This WO **adds** an entry point; it does not change what
  already works.
- **`PortalVFXController`'s built-in glow** — the spawner notes the arch reads as a portal with no
  asset at all thanks to it (`:559`). That fallback stays.

---

## 6. Acceptance

1. **Walk a full circle around a discovered portal.** It reads as a portal at every heading — no
   angle where it collapses to shards, a flat disc, or a dark slab.
2. Capture the **same three headings** as the owner's shots (NE / NW / SW) plus a fourth, side by side
   against hers. **The SW shot is the reference** — NE and NW must now read like it.
3. **No magenta** on the portal at any angle, and `MagentaGuard` **runs on runtime-spawned portals**
   — prove it by temporarily breaking a portal material and seeing the guard log + recover it.
4. No black back face anywhere — §2 removes it structurally; confirm from both approaches.
4b. **No z-fighting** between the two planes at the authored 0.25 separation, checked up close AND at distance.
4c. Edge-on from beside the arch reads as a thin sheet of light in a stone doorway — **this is correct and must not be "filled in"**.
5. `VFXManager` loop count is **unchanged** by this work (§1 note).
6. **Greyscale pass** — the portal still reads as a lit doorway with hue removed.
7. `COMPILE_GATE_OK`; brace-check every `.cs`; screenshots opened, not just taken.

## 7. Files

**Read first:** the owner's three 2026-08-22 screenshots ·
`Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs` (`:218-228`, `:559`, `:570`, `:627`) ·
`Assets/_Modules/Core/MagentaGuard.cs` (`:20-22`, and its header on the ~8 targeted fixers)

**Likely edit:** `DungeonWorldPortalSpawner.cs` (plane orientation + the guard call) ·
`MagentaGuard.cs` (a public per-object `Scan` entry).

**Nothing held.** The §2 ruling reuses the already-tagged `CircleVfx`; no new VFX key is needed.
