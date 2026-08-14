# ⚠ THIS CAPTURE IS NOT PROOF OF WO-1007. IT IS THE EVIDENCE FOR WO-988.

**Do not cite the PNGs in this directory as proof of anything about the dungeon.**

## What this run claimed

```
HEADED_CAPTURE_OK 10 shots -> docs\proof\2026-08-14-wo1007-portal-camera
```

## What actually happened

From `Player.log`, copied alongside this run (single `Initialize engine version` line, so this is one
session — not a stale log):

```
scene='Main_Castle_Overworld'
WORLD CLOCK FROZEN: Time.timeScale=0.00 in scene 'Main_Castle_Overworld'. The hero CANNOT move, turn or ...
```

Three failures, none of which stopped the marker:

1. **Wrong scene.** The harness was invoked for a dungeon and the player booted into the TOWN. The
   `-Scene` parameter is accepted, printed in the launch line, and then never enforced.
2. **Frozen clock.** `Time.timeScale = 0.00`, so every "walk forward / turn left" beat was a no-op
   against a still frame.
3. **Input went to a textbox.** `10_facing_exit.png` shows an open bug-report field containing
   partially-typed text — the synthetic WASD keystrokes were typing into it, not driving the hero.

All ten PNGs are the frozen town.

## What IS still valid from this run

The `Player.log` in this directory is genuine and its `[Flow:DungeonExit]` lines are real. They come
from the **overworld dungeon-entrance portals**, which use the same `DungeonExitInteractable`
component. These are proven by it:

- portal normalize `1m -> 2.7m` (hero 1.8m x 1.5, scale x2.707)
- re-seat delta `(0.00, 0.00, 0.00)` — pivot already at base, correction was a genuine no-op **and the
  trace shows the zero rather than asking a reader to trust it**
- `base sits on seat` on both exits
- `label=REMOVED per owner ruling` on both exits
- `PORTAL swapped in from 'dungeon/exit/portal'` — the Addressable key resolves

**Partially valid:** the camera line reads `headingYawOffset=0.0` with hero yaw 90.0 and rig yaw 90.0,
i.e. delta 0 where it was previously pinned at 90.0. That is proven **AT REST ONLY** — the clock was
stopped, so the delta collapse **under movement is UNPROVEN**.

## Why this directory was kept rather than deleted

It is the captured evidence behind **WO-988** (`headed-dungeon-capture.ps1` reports
`HEADED_CAPTURE_OK` on a wrong-scene, frozen-clock run). Deleting it would remove the proof that the
harness can certify a run that never happened — the same reasoning that keeps a stale code branch
bannered instead of deleted (WO-985).

A capture that cannot fail does not merely omit evidence, it **manufactures** it: these artifacts
landed in `docs/proof/` under a ticket's name, where the next reader would reasonably treat them as
that ticket's proof. Hence this banner.

**Re-run the acceptance capture for WO-1007 only after WO-988 lands.**
