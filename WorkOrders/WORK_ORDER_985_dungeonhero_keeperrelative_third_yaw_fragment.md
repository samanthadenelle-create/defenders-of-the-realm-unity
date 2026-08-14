# WORK ORDER 985 — `DungeonHero`'s dead `KeeperRelative` branch is a third copy of a yaw offset whose pair was removed

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-14 (CLI)
**Silo:** Dungeon locomotion / camera
**Lane:** Dungeon — conflicts with any other work in `DungeonHero.cs` / `DungeonCameraRig.cs`; one agent at a time

---

## Background — the matched pair, and how it broke

`DungeonHero.FaceHeading` used to apply `Quaternion.LookRotation(heading, Vector3.up) *
Quaternion.Euler(0f, -90f, 0f)`. `DungeonCameraRig._headingYawOffset` defaulted to `90f`.

**These two were a matched pair.** The camera's `+90` existed *solely* to undo the hero's `-90`.
Nothing documented them as a pair at the call sites — but `DungeonCameraRig.cs:53-60` had written the
warning down in advance:

> *"If a dungeon ever ships a hero rig with NO such offset, zero `_headingYawOffset`."*

On 2026-08-14 the owner reported the hero facing left constantly and the `-90` was removed from
`FaceHeading`. **Only one half of the pair moved.** The result: the camera sat 90° to the side.

Proven from the owner's own F8 session (seq 2328) — `rigYaw - heroYaw` held **constant at 90.0
across 39 heartbeats**, min 77.8 / max 100.2:

```
heroYaw   rigYaw    delta     heroPos
90.0      180.0     90.0      -28.00, 0.08, 0.00
123.7     213.7     90.0      -32.84, 0.08, -0.03
```

Both halves are now zeroed (`_headingYawOffset = 0f`; `FaceHeading` returns the bare
`LookRotation`).

## The remaining fragment

`DungeonHero.cs` still carries a `KeeperRelative` branch that applies `ModelYawOffset = 90f` — a
**third copy of the same offset**. It is currently unreachable and is bannered STALE.

**It was deliberately NOT deleted.** Deleting an unreachable branch destroys the evidence of what
the pair used to be, and this ticket exists precisely because that pairing was invisible at the two
call sites that mattered.

## The hazard, stated precisely

If anyone re-enables `KeeperRelative`, it re-introduces the exact 90° bug **against a camera that no
longer compensates** — and the next person debugging it starts from zero, because the two halves
that would have explained it are gone.

## What to decide

This is a small ticket with one real question: **does that branch have a future?**

- **If yes** — wire it with a **zeroed** offset, and add a comment at both the hero and the camera
  naming the pairing explicitly, so the next edit cannot move one half alone.
- **If no** — remove it in **one deliberate edit** that names the pairing in the commit message and
  in `DungeonCameraRig.cs`'s header, so the history retains what the code no longer shows.

Either way the outcome is the same invariant: **there is exactly one place that decides hero model
yaw, and the camera does not compensate for it.**

## Explicit prohibitions

- ⛔ **Do NOT "clean it up" as tidying.** The cleanup and the decision are the same edit.
- ⛔ **Do NOT flip the branch on to see what happens.** That reproduces a known bug in a build the
  owner may pick up; the failure mode is already captured above, so there is nothing to learn.
- ⛔ Do not re-add a compensating offset anywhere. Two offsets that cancel is the defect, not the fix.
- ⛔ Do not strip any `FlowTrace` (CLAUDE.md §12, BINDING — instrumentation is permanent; flag off,
  never delete).

## Acceptance criteria

- Exactly one owner of hero model yaw remains; grep for `Euler(0f, 90f` / `Euler(0f, -90f` /
  `ModelYawOffset` across `Assets/_Modules/Dungeons/` returns only the surviving decision site.
- A headed capture (`tools/capture/headed-dungeon-capture.ps1`) shows `rigYaw - heroYaw` at **~0**,
  and the hero pointing along travel in consecutive frames (`02_forward` → `03_forward_far`).
- Brace balance exact, zero NUL bytes, `COMPILE_GATE_OK`.

## Files

- `Assets/_Modules/Dungeons/DungeonHero.cs` (the `KeeperRelative` branch, ~`:484-489`)
- `Assets/_Modules/Dungeons/DungeonCameraRig.cs` (header comment only — `_headingYawOffset` stays `0f`;
  the "90+90=180" arithmetic at `:515` / `:722` is already marked HISTORICAL, leave it marked)

## What NOT to touch

- `_headingYawOffset`'s value — it is correct at `0f`.
- `DungeonExitInteractable.cs` — active in another lane this session.
- Any `.unity` scene file.
