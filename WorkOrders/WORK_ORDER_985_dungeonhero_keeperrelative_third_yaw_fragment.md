# WORK ORDER 985 — `DungeonHero`'s dead `KeeperRelative` branch is a third copy of a yaw offset whose pair was removed

**Status:** IMPLEMENTED — 2026-08-15 (`ModelYawOffset` 90→0; coupled to FaceHeading; branch still dead)
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

> ## ⚠ PRECISION CORRECTION (SME diagnosis, 2026-08-14, same day this WO was minted)
> **The `KeeperRelative` fragment is NOT a rotation writer. It is an input-basis READER.**
> At `DungeonHero.cs:489` it converts the root rotation into an *assumed visual forward* in order to
> read the stick against it (`:490-491`). It shares the **premise** of the removed pair, not the
> **mechanism** — so zeroing it is **not symmetric** with zeroing a writer, and it must not be treated
> as "the third copy of the same line".
> If the branch is ever kept, `0f` is correct **only because `FaceHeading` (`:726`) no longer offsets**
> — i.e. this fragment is coupled to `DungeonHero.cs:726`, **NOT** to `DungeonCameraRig._headingYawOffset`.
> Wire that reasoning into the edit or the next reader re-derives the wrong pairing.

> ## ⛔ THIS TICKET IS COUPLED TO WO-966. `HeroBodySwapper.cs:263` IS SHARED SURFACE.
> The dungeon Keeper uses the **same `HeroBody`**, swapped by the **same `HeroBodySwapper`**
> (`DungeonHero.cs:126-127, 188, 215, 301` re-resolves its animator across that swap;
> `DungeonCameraRig.cs:57-58` exists to survive it). So a WO-966 change to `:263` for the Mage
> **also changes the dungeon Mage's visible facing**, against a camera that now compensates for nothing.
> **`:263` has no lane. It is ONE edit, gated once, verified in BOTH scenes before commit.**

> ## ⚠ THE AT-REST CAPTURE DOES NOT CLEAR THIS TICKET.
> The 2026-08-14 capture showing `rigYaw - heroYaw = 0` ran at `Time.timeScale = 0.00` in the TOWN
> scene. **A constant offset and a correct rig are indistinguishable at zero velocity** — `FaceHeading`
> early-returns below `sqrMagnitude < 0.0025f` (`DungeonHero.cs:707`) and `bodyErr` early-returns below
> `velMag 0.2` (`HeroGaitForensics.cs:160`). Both instruments are silent exactly where the bug lives.
> **Correct-under-movement is UNPROVEN.** Requires WO-988 (the capture harness certifies frozen-clock,
> wrong-scene runs) before an acceptance capture means anything.

> ## ⚠ INSTRUMENTATION GAP — the dungeon facing instrument reads a HARD ZERO regardless of truth.
> `HeroGaitForensics` computes `velMag` from `_loco.Velocity` (`:132-134`) and gates `bodyErr` on
> `velMag > 0.2f` (`:160`). In a dungeon, `HeroLocomotion.cs:972-975` **forces `Velocity = Vector3.zero`
> every frame** because `DungeonHero`'s CharacterController owns the transform. So `velMag` is 0,
> `heading` freezes at `_lastHeading`, and **`bodyErr` reports 0 whether or not the body is wrong.**
> This is a §1.4b hollow field in the very instrument that would prove this ticket.
> **Feed it the measured root speed** (the same source `HeroLocomotion.cs:27-31` already uses for the
> animator) **BEFORE trusting any dungeon facing number.**

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
