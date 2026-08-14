# WORK ORDER 987 — Dungeon exit portal: TOUCH to interact, then a "Continue to exit / Cancel" confirm

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-14 (CLI)
**Silo:** Dungeon exit / UX
**Source:** OWNER RULING, 2026-08-14

---

## The ruling (verbatim)

> *"should be action on interacting with the portal. Touch portal to interact"*
> *"if you want a confirm there that could be smart"*
> *"confirm exiting portal"*
> *"continue or exit"* — clarified to *"continue to exit or cancel"*

## What this changes

**1. Touch is the trigger.** Contact with the portal initiates the interaction. Today the exit is
driven by the proximity prompt path; the ruling makes walking into the portal the action itself.

**2. A two-choice confirm gates the actual exit.**

> ### ⛔ THE TWO FACES ARE `Continue to exit` AND `Cancel`.
> They are **NOT** two ways forward. The owner's earlier phrasing was *"continue or exit"*, which reads
> naturally as "keep playing" vs "leave" — **that is the wrong dialog** and would be an easy mis-build.
> The clarified ruling is *"continue to exit or cancel"*:
> - **`Continue to exit`** → proceed with leaving the dungeon (the destructive/irreversible choice).
> - **`Cancel`** → return the player to the run, **unchanged**. No state consumed, no loot settled,
>   no position moved. The player must be able to cancel and keep playing exactly as before.

## Why the confirm exists

A player currently loses a run by *walking into* the exit. Touch-to-interact makes that easier to do
by accident, not harder — so the confirm is not decoration, it is the thing that makes touch safe.
The confirm must therefore be **impossible to dismiss into an exit**: a stray tap, a back gesture, or
an incidental second contact with the portal must resolve to **Cancel**, never to exit. Default focus
sits on Cancel.

## Design constraints

- ⚠ **Do NOT reuse the raw violet plate.** `MobileInteractButton`'s flat violet fill is being replaced
  with the Obsidian kit under WO-1005 Part 1 (owner confirmed 2026-08-14: *"yes the purple should go
  since the rest went"*, town included). Build this confirm on the Obsidian kit from the start, or it
  ships already-stale. Coordinate: that lane touches
  `Assets/_Modules/Village/Buildings/MobileInteractButton.cs`.
- **The owner is red/green colourblind.** The two choices must be distinguishable by **position, shape
  and text**, never by a red/green pairing. Do not make "Cancel" red and "Continue to exit" green and
  call it done — under a greyscale check they must still read as clearly different actions.
- **Code-built UI only.** UXML does not work in builds (CLAUDE.md §8).
- **Touch targets:** honour `MinTouchPx = 112` (memory `mobile-ui-touch-contrast-standard`). A confirm
  the player mis-taps is worse than no confirm.
- Cross-module service calls via `CoreServices.*` with `?.`.

## Instrumentation (§12 + INSTRUMENTATION_STANDARD §1.4b)

The failure modes here are silent by nature, so each must be separately reportable:

- Portal touched, confirm **shown** → distinct trace line naming the portal and the choice presented.
- Confirm **resolved** → the line must name **which face was taken** (`continue-to-exit` vs `cancel`).
  A line that says "confirm resolved" without naming the choice is a hollow assertion and will be
  rejected.
- Portal touched but confirm **failed to appear** → `FlowTrace.Warn` naming the cause, because the
  visible symptom is "the portal does nothing", which is indistinguishable from "touch not detected"
  without it.
- Cancel taken → an explicit line confirming the run state was **left unchanged**, so a future
  regression that quietly consumes state on cancel is visible in the trace.

Never strip these afterwards (§12, BINDING — flag off, never delete).

## Acceptance criteria

- Walking the hero into the portal raises the confirm. No separate button press required.
- `Cancel` returns to the run with the hero still in the dungeon, run state untouched; the player can
  walk into the portal again and get the same confirm.
- `Continue to exit` performs the existing exit flow unchanged.
- A stray tap / back gesture while the confirm is up resolves to **Cancel**, never to exit.
- The two faces read as different actions in **greyscale**.
- Proof: a headed capture showing the confirm on screen, plus trace lines naming the presented choice
  and the resolved face for **both** paths (take Cancel once and Continue-to-exit once).

> ⚠ **The capture harness cannot currently prove this.** `tools/capture/headed-dungeon-capture.ps1`
> emitted `HEADED_CAPTURE_OK 10 shots` on a run that loaded the TOWN with `Time.timeScale=0.00`
> (2026-08-14). **WO-988 must land first**, or the acceptance capture for this ticket is worthless.

## Files (expected)

- `Assets/_Modules/Dungeons/DungeonExitInteractable.cs` — the touch trigger and confirm hand-off
- The confirm panel itself — Obsidian kit, code-built
- `Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs` — note the type is `ElarionUiKit`
  (`partial class ElarionUiKit`, namespace `DeNelle.Core.UI`). **There is no type named
  `ElarionUiKitObsidian`** — that is the FILE name, and citing it as a type will not compile.

## What NOT to touch

- `DungeonHero.cs` / `DungeonCameraRig.cs` — yaw pairing work is live in another lane (WO-985).
- Any `.unity` scene file.
- The portal's normalize/seat logic in `DungeonExitInteractable` — proven correct 2026-08-14
  (1 m → 2.7 m, zero re-seat delta, base on seat). Leave it alone.
