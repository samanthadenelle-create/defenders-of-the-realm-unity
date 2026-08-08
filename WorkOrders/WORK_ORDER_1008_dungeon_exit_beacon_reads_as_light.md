# WO-1008 — The dungeon EXIT beacon must read as LIGHT, not as a green box

**Status: READY TO IMPLEMENT**
**Date:** 2026-08-08 · **Priority:** Medium-High (it is the most visually wrong thing in a dungeon)
**Block:** UI seat (1000-1099) · **Lane:** Dungeons / VFX / UI cohesion
**Owner ruling 2026-08-08:** felt-test, verbatim — *"big green bar doesnt make sense"*

> ⚠ **Minted as 1007, RENUMBERED to 1008 the same minute.** Two seats minted 1007 concurrently
> (2026-08-08 09:24 and 09:26) on the same subject. Resolved by CLAUDE.md §2 —
> **first-on-disk-and-referenced wins** — so `WORK_ORDER_1007_dungeon_exit_real_asset.md` keeps 1007
> and this took the next number. Recorded rather than silently corrected, because a renumber that
> leaves no trace is how a stale cross-reference is born.

**⚠ READ WITH WO-1007 — they are two halves of one object, not duplicates.**
`WO-1007` replaces the exit **archway** (the primitive emerald-cube arch in
`DungeonExitInteractable.BuildVisual`) with a real KayKit prop, and explicitly states it is
*"keeping the walk-in trigger + beacon"*. **This WO is that beacon.** Ship them together or 1007
lands a handsome stone doorway with a flat green box still stuck through it.

**Siblings:** WO-1005 (dungeon UI cohesion) owns the mirrored `EXIT` label and the flat-purple Descend
prompt — same family, same pass. WO-924 kills the *debug* volumes; this one replaces the beacon that
is supposed to stay.

---

## 1. The intent is RIGHT. The execution is a primitive.

A vertical shaft of light marking the way out is exactly correct for this game — it is what both
halves of the north star do (Warcraft's beams of light over objectives, Clash's glow markers). **Do not
delete the beacon.** In a dark, enclosed dungeon the player needs a landmark that says *your way out is
here* from across a room.

The problem is that it is not light. It is a **cube**.

`Assets/_Modules/Dungeons/DungeonExitInteractable.cs`:

| Line | What |
|---|---|
| `:233` | `AddDecor("Beacon_Beam", pos (0, 6.2, 0), scale (0.28, 6.4, 0.28), glow, false)` |
| `:203` | `glow = new Color(0.55f, 0.95f, 0.55f, 0.72f)` — pale green |
| `:271-294` | `AddDecor` builds a `PrimitiveType.Cube`, strips its collider (`:276`) |
| `:284` | assigns **`Shader.Find("Universal Render Pipeline/Unlit")`** |
| `:205-206` | siblings `Pillar_L` / `Pillar_R`, 0.35 x 2.6 x 0.35 emerald |
| `:249` | billboarded `TextMesh "EXIT"` |

An **Unlit** material ignores every light in the scene. That is why it survived unnoticed while dungeons
were bright and why it screams now: WO-919/WO-1004 dropped ambient to `#0a0a10` with linear fog, so a
full-brightness flat green box is the only thing in frame not obeying the lighting model. It reads as
debug geometry because, materially, it is.

---

## 2. What to build instead

A beacon that **responds to the dark** rather than ignoring it. Direction, not prescription:

- **Additive / soft-particle beam** rather than an opaque cube — a shaft with falloff, softer at the
  top, so it looks emitted rather than placed. The VFX pipeline for this already exists; prefer a
  catalogued effect over a hand-built primitive (`docs/vfx/VFX_PREFAB_HANDBOOK.md`).
- **A real point light at the base**, so the beacon actually spills onto the floor around it. That is
  what sells it as a light source and it is the single cheapest win here.
- **Gentle motion** — a slow pulse or drift. A static bar reads as an object; a moving one reads as an
  effect. Keep it slow; this is a landmark, not a hazard tell.
- **Scale it down.** 6.4 m of beam in a 4 m-walled room is why it punches through the ceiling.

⚠ **Colour is the owner's pick, not the implementer's.** Green currently collides with the
legendary-gate green that WO-924 is removing, and the owner is red/green colourblind — so the beacon
must not depend on hue to be identifiable. Distinguish it by **shape, motion and position**, and treat
any colour in this WO as a placeholder pending her ruling.

---

## 3. ⚠ SEPARATE DEFECT FOUND IN THE SAME PLACE — NOT this WO's job, do not silently fix

While diagnosing the green bar, the exit beacon was found to be **spawning inside a STAIR room**, not at
an exit. `Assets/StreamingAssets/Data/Canonical/dungeon-layouts/dg_bonecrypt.json`:

```
bc-extract-l1   roomId "stair_up_1"   offset [1.6, 0.0, 0.0]   label "Extract"
```

`stair_up_1` sits at cell (10, **-6**, 10). `DungeonBaker.cs:1684-1713` seats the extract by `RoomSeat`
(`:1585-1593`), so the beam centres at local y 6.2 with half-height 3.2 and spans world y **-3.0 to
+3.4** — through the lower room's ceiling shaft, through the upper floor plane at y = 0, and 3.4 m proud
of the floor above. Its XZ (11.6, 10) falls **inside** the `stair_dn_0` floor cut.

**That is what the owner photographed**: a green bar rising out of the descent hole. It is an exit marker
from the floor below, showing through a hole it was never meant to be near.

This is a **layout/authoring** defect (which room an extract is seated in), not a visual one. It belongs
to the dungeon program lane, not the UI seat. Fixing the beacon's look here will make it *prettier in
the wrong place* — file the seating fix separately and say so in this WO's RESULT.

---

## 4. Acceptance

- [ ] The beacon reads as emitted light in a dark dungeon: it is affected by / contributes to lighting,
      and is not an Unlit primitive.
- [ ] It casts some spill on the floor at its base.
- [ ] It does not exceed room height (`RoomForgeCanon.WallHeight = 4`) or punch through a ceiling.
- [ ] It is identifiable **without relying on hue** (owner is red/green colourblind, CLAUDE.md).
- [ ] It no longer collides visually with the WO-924 debug volumes (which are being removed).
- [ ] Captured before/after screenshots in a **dark** dungeon — this defect only exists in the dark, so a
      bright capture proves nothing. Use `Defenders > Dungeon > Walk Test` on a real dungeon, NOT the
      inspection-lit rigs.
- [ ] Owner felt-verifies and closes.
