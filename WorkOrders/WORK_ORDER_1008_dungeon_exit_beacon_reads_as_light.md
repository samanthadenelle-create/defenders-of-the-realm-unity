# WO-1008 — The dungeon EXIT beacon must read as LIGHT, not as a green box

**Status:** SPEC — the re-bake LANDED 2026-08-14 (all 7 dungeons re-composed, `COMPOSE_ALL_OK 7/7`, 13 pads bake `label='Leave'`, every layout emits `exitRoomId`); the code half landed + gated 2026-08-10. REMAINING: the player exit ASSET (`Assets/Resources/Dungeon/Exit/` still absent, so a player build takes the primitive-arch fallback) and the per-layout one-beacon REGRESSION are both handable — but **true-exit PLACEMENT awaits owner authoring** (`exitRoomId` is the `entry` fallback everywhere), so this needs a spec pass first. See the 2026-08-14 note at the bottom. *(Status audit 2026-08-24: the marker itself was MALFORMED — `**Status:` with no closing `**` — so this row could not parse. Marker repaired; body unchanged.)*
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

## 3a. ⚠ A REGRESSION PINS THE OBJECT NAME — do not trip it

`Assets/Editor/Regression/DungeonRoomOwnershipRegression.cs:366-368`:

```csharp
Transform beam = exit.transform.Find("Beacon_Beam");
if (beam == null)
    failures.Add("[exit-beacon] spawned exit has no Beacon_Beam glow pillar");
```

**A suite asserts a child named exactly `Beacon_Beam`.** Replace the Unlit cube without keeping that
name (or updating this oracle in the SAME change) and a correct fix turns the gate red. Prefer keeping
the name — it is the contract "the exit has a visible beacon", which stays true and is worth pinning;
only the *material and mesh* should change.

The exit's other authored children, for reference: `Beacon_Label` (`:245`, the billboarded `EXIT`
TextMesh — mirrored from one side, WO-1005) and `Pillar_L` / `Pillar_R` (`:205-206`, 0.35 x 2.6 x 0.35
emerald). The owner refers to the whole cluster as the **exit points**; there are FIVE of them in
`dg_ember_deep` alone.

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

---

## 2026-08-10 - PARTIAL LANDING of the combined 957 + 1007 + 1008 lane (CLI seat, gated)

**The CODE half landed in full and is gate-green. The BAKE has not been re-run, so none of it is on
screen yet.**

Landed in `Assets/_Modules/Dungeons/DungeonExitInteractable.cs` (+349/-88):
- **WO-1008** - `Beacon_Beam` is now TRANSLUCENT and capped to world y 2.9-4.0 (`:437`), under
  `RoomForgeCanon.WallHeight = 4`, so it can no longer punch through the floor above and read as
  "a green bar rising out of the descent hole". The point light + slow pulse are unchanged and still
  carry the cue from range. Colour is deliberately UNTOUCHED (owner's call, colourblind law): the
  distinction is SHAPE and POSITION.
- **WO-1007** - the true exit builds a KayKit Option-C monument arch (`wall_arched` +
  2x `pillar_decorated`, colliders stripped), resolving Resources first then the editor kit, and on
  failure Warns and falls back to the primitive arch (`:264`, `:293-324`). A lost exit is a softlock,
  so this path never returns nothing.
- **WO-957** - TWO presentations selected by `_isTrueExit` (`:216`, `:255`). TRUE = arch + beam +
  "EXIT". FALSE = a flat translucent `Pad_Marker` disc + a small `Pad_Label`, no light, no beam, no
  "EXIT" (`BuildLeavePad`, `:371`). `DungeonBaker` passes `false` for every per-floor pad and
  passes all four args explicitly, because reflection does NOT apply C# default args. **Owner pin 1
  honoured: the pads STAY.**
- Schema v2, additive: `DungeonComposeLayout.exitRoomId` designates the ONE true exit; unset falls back
  to the entry room (the pre-multi-floor behaviour), so v1 layouts parse and behave identically.

**Completion by the committer:** the lane's session expired having written `BuildLeavePad` against two
helper methods it never extracted - the tree did not compile (`error CS0103` on `ApplyDecorMaterial`
and `BuildWorldLabel`). Both were extracted from the existing inline blocks so the pad and the true
exit now share ONE material path and ONE world-label path - a second copy is how one of them ends up
opaque again. The pinned child names `Beacon_Beam` and `Beacon_Label` are preserved
(`DungeonRoomOwnershipRegression.cs:366` finds the beam by name). One behavioural delta, an
improvement: an unresolved shader now Warns instead of silently keeping the primitive material.

**Owner pin 2 ("the word is Leave") - DATA NOW LANDED:** every shipped content layout authored
`"label": "Extract"`, which overrides the code default, so the pin had not reached the screen. All 13
extract labels across `dg_bonecrypt` / `dg_ember_deep` / `dg_sunken_vault` are now `"Leave"`, in
BOTH dual copies, verified byte-identical and parsing. The two control fixtures (`dg_descent_probe`,
`dg_stair_rig`) were deliberately left alone - they are the quarantined WO-930 control group.

**REMAINING SCOPE - why this WO stays READY:**
1. **The dungeons have NOT been re-baked.** `_isTrueExit` is a `SerializeField` defaulting TRUE and
   the pads are BAKED objects, so every already-baked `Extract_*` still deserialises as a full
   arch+beacon and still says "Extract". **Nothing above is visible until a re-bake** - and per memory
   `dungeon-scene-shared-tree-corruption` that bake belongs in an ISOLATED WORKTREE, not this shared
   tree mid-wave. That is the next mechanical step and it is the owner's call to schedule.
2. **No layout authors `exitRoomId`.** The designation mechanism exists; every layout takes the
   `entry` fallback. Behaviour is correct-by-fallback, but WHERE the one true exit sits is a design
   pick, not something to invent.
3. **The WO-957 per-layout regression was not written** ("for each converted layout, exactly ONE exit
   beacon"). Nothing asserts `_isTrueExit`, `Pad_Marker`, or a beacon count per layout.
4. `Assets/Resources/Dungeon/Exit/` does not exist, so a PLAYER build always takes the primitive-arch
   fallback (with its Warn); the editor/bake path resolves from the gitignored kit. Registered as tracked
   debt in `HudUiRegression.MissingResourceBaseline` rather than hidden.

**Gate:** `Builds/gate-settle4.log` -> `COMPILE_GATE_OK` (zero `error CS`) ·
`Builds/regression-settle3.log` -> `REGRESSION_OK 143/143 suites`.

**Owner felt-verify (after the re-bake):** dark `dg_ember_deep` - the mid-floor pads read as quiet
discs saying "Leave", exactly one arch+beam, and the beam does not stand proud of the floor above.