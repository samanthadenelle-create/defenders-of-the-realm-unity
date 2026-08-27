# WORK ORDER 1035 — Portal VFX is huge and free-floating: seat it INSIDE the portal mesh, derived from bounds  — **OWNER CLOSED 2026-08-22** (felt-verified by the owner; PO closes, section 13).

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (dungeon review).

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*

> **NUMBER NOTE (2026-08-22):** `grep -rn "WO-1035"` over `Assets/` returns the *BuildHud Done-button* WO-1035
> (`Assets/_Modules/Village/BuildMode/BuildHudController.cs:39,155,301,387,477,583,773,935`), which is a
> **different** ticket sharing this number. Do not read those hits as this ticket's evidence.


> Shipped in **68082bf6b** — *"fix(vfx): WO-1035 - portal effects sit INSIDE the arch (units bug, not art)"*.
> Live at `Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs:866, 968, 1057, 1082` — a MEASURED
> seat and a MEASURED size, not authored constants, which is why it holds across portal variants.
> Pinned by `RangedFacingLockRegression.cs:335`.
>
> ⚠ Worth remembering from the diagnosis: this was a **units bug, not an art bug**. The effect was
> authored correctly and placed wrongly. A seat that "fixes" this class by editing the VFX prefab makes
> it worse and hides the real cause.
>
> The status line sat at READY for a day after the work landed (CLAUDE.md §2 — flip it in the same commit).
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1035 → 1036 in the same edit
**Lane:** World VFX seating. Disjoint from WO-1025 (Heart tree VFX) — different emitter, same class.
**Provenance:** owner 2026-08-16, verbatim: *"portal vfx need inside the interior of the portal.fbx not
huge. Maybe 1/3 of protal.fbx height centered at y/2 x(end-start)/2 to be centered"*, with a build-mode
screenshot showing two oversized portal VFX blobs floating over the terrain.

---

## 1. What the owner is looking at

The capture shows the portal VFX rendering as **large white ring/glyph blobs plus black spheres,
detached from any visible portal geometry** — floating over open grass at a scale comparable to nearby
buildings. They read as loose effects in the world, not as a portal.

## 2. The owner's spec, restated as geometry

Seat the VFX **inside the portal mesh's interior**, derived from the portal's own bounds:

| property | rule |
|---|---|
| **Height** | ≈ **1/3** of the portal mesh's bounds height |
| **Vertical position** | centred at **half** the portal height (`bounds.center.y`, i.e. `y/2` of the mesh extent) |
| **Horizontal position** | centred on the mesh's horizontal midpoint — the owner's `x(end-start)/2`, i.e. `bounds.center.x` |
| **Net effect** | the effect sits **within the archway**, filling roughly the middle third — a portal you look *through*, not a blob you stand beside |

⚠ **Derive every one of these from the portal's actual renderer bounds at runtime — do NOT hardcode
metres.** Portals are placed from the authored `AuthoredPortals` table and NavMesh-seated, and the mesh
can be re-scaled or swapped; a hardcoded offset silently detaches the moment either changes.

**This is the same failure class as WO-1032** (`PetForwardYaw = -90f`, a hand-authored constant that
outlived its mesh) and the canon "PATTERN OF THE NIGHT" (2026-08-06): *a value authored BY HAND instead
of DERIVED from the thing it describes.* Compute from bounds and it cannot drift.

## 3. Where it is built

`Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs:24-25` — the spawner explicitly delegates
the visual:

> *"PORTAL VISUAL + INTERACTION + LOAD — the EXISTING `DungeonPortal` component: it already builds its
> own **`PortalVFXController`** glow…"*

So the seating change belongs with **`PortalVFXController`** / `DungeonPortal`, not in the spawner's
placement table. ⚠ The spawner's authored positions are an **owner ruling** (2026-07-13: *"Portals are
NOT in town — portals are wherever in the world we want"*) — **do not move the portals**; only re-seat
the effect relative to the mesh.

## 4. ⚠ RELATED, ALREADY FIRING — the portal is also MAGENTA

F8 seq **2404–2415** (12 captures, 2026-08-16 06:53):

```
[Flow:MagentaProbe]  FAIL cause=DungeonWorldPortalSpawner.BuildPortal obj='[DungeonWorldPort...
```

A magenta placeholder means a **missing/broken material** on the portal build path — shader not found,
or a material lost in a move. ⚠ **Check whether the "VFX blobs" the owner is seeing are partly this
magenta-fallback geometry rather than the intended effect.** If so, re-seating alone will not fix the
look, and a resize would be applied to the wrong object.

**Diagnose the magenta FIRST**, since it is already instrumented and costs one read (§12). Record the
finding; if it turns out to be a separate defect, mint it rather than silently widening this WO.

## 5. Do NOT

- Do not move the authored portal positions (§3 — owner ruling)
- Do not hardcode the VFX offset or scale in metres (§2)
- Do not swap the VFX key — if the effect itself is wrong, that is an **owner tag**, not a CLI pick
  (memory `vfx-map-owner-tags-no-creative-pick`)
- Do not add a second spawner or pool — reuse `VFXManager`
- Do not delete the `[Flow:MagentaProbe]` trace (§12 — it is what surfaced §4)

## 6. Acceptance criteria

- [ ] The VFX renders **inside the portal archway**, ≈1/3 of mesh height, centred on the mesh in both
      axes per §2
- [ ] Scale and offset are **computed from renderer bounds** — no hardcoded metres
- [ ] A portal at a different scale (or a swapped mesh) still seats correctly — prove it by testing at
      two scales, since that is the whole point of deriving
- [ ] `[Flow:MagentaProbe] FAIL … BuildPortal` no longer fires, or is separately ticketed with its cause
      recorded
- [ ] Portal positions unchanged from `AuthoredPortals`
- [ ] Before/after screenshots from the same camera

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. **Graphics-enabled capture** at a portal — ⚠ the fleet is `-nographics` and shoots blank; and
   headless markers cannot judge scale or seating any more than they could judge orientation
   (canon 2026-08-09: *"this class needs EYES, not markers"*)
3. Owner felt-verifies + closes (§13)
