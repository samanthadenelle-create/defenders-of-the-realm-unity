# WORK ORDER 988 — `headed-dungeon-capture.ps1` reports `HEADED_CAPTURE_OK` on a wrong-scene, frozen-clock run

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-14 (CLI)
**Silo:** Tooling / verification harness
**Sibling:** WO-984 (the Unity method wrapper judges by log text, not markers) — same defect class

---

## What happened

A capture run tagged `wo1007-portal-camera` was launched to prove three dungeon fixes. It printed:

```
HEADED_CAPTURE_OK 10 shots -> docs\proof\2026-08-14-wo1007-portal-camera
```

All ten shots are of the **town**, with the game clock **stopped**. From the `Player.log` copied
alongside that same run:

```
scene='Main_Castle_Overworld'
WORLD CLOCK FROZEN: Time.timeScale=0.00 in scene 'Main_Castle_Overworld'. The hero CANNOT move, turn or ...
```

One `Initialize engine version` line, so this is a single run — not a stale log from an earlier session.

Three independent things were wrong, and **none of them stopped the marker**:

1. **`-Scene` is accepted and then never enforced.** The parameter defaults to
   `Dungeon_HealersCottage`, the launch line prints `launching 'Dungeon_HealersCottage'`, and the
   player booted into `Main_Castle_Overworld` anyway. The harness never checks what actually loaded.
2. **The world clock was frozen** (`Time.timeScale = 0.00`), so the hero could not move, turn, or be
   driven. Every "walk forward / turn left" beat was a no-op against a still frame.
3. **The synthetic keystrokes landed in a text field.** `10_facing_exit.png` shows an open bug-report
   input containing partially-typed text. The WASD beats were typing into a textbox, not driving the
   hero — so even with a live clock and the right scene, the drive would have done nothing.

## Why this is worse than no capture

The harness's own closing line already says the right thing:

> *"Now OPEN them. A green marker proves a frame rendered, never that it looks right."*

That is true and insufficient. The marker did not merely fail to prove the *look* — it certified a run
that **loaded the wrong scene and never moved**. A capture that cannot fail does not just omit
evidence, it **manufactures** it: the artifacts land in `docs/proof/` under a ticket's name, where the
next reader reasonably treats them as that ticket's proof.

This is the same class as WO-984 and as the 44 rows in
`docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md` (`INSTRUMENTATION_STANDARD` §1.4b — *"a trace field that
cannot report failure is a bug, not a nicety"*). Three instances of the class surfaced on 2026-08-14
alone: the gate wrapper, this harness, and a WO-983 acceptance grep written against an em-dashed
string that could not match.

## The fix — assert the preconditions, fail closed

After load and before driving, read the live `Player.log` and **refuse** (non-zero exit, **no marker**,
and do not write shots) unless all of these hold:

| Precondition | Why |
|---|---|
| Active scene **equals** `-Scene` | The defect above. Accepting a parameter and ignoring it is the bug. |
| `Time.timeScale > 0` | A frozen clock makes every drive beat a no-op. |
| **No modal / text-input has focus** | Keystrokes must reach the game, not a textbox. |
| Hero **position changed** between `01_idle` and `03_forward_far` | The only end-to-end proof the drive actually drove. Everything else is a precondition; this is the outcome. |

The last row is the important one — it is the difference between "the harness believes it sent input"
and "the game moved". Report the measured start and end positions in the marker line so a reader can
see the movement rather than trust it.

On failure, name **which** precondition failed. A generic "capture failed" reproduces the problem one
level up.

## Acceptance criteria

Each row must be demonstrated by an actual run, with the exit code pasted into the RESULT. Do not
argue these from reading the script — that is the error this ticket documents (CLAUDE.md §12: static
reading LOCATES, it never CONCLUDES).

| Case | Required |
|---|---|
| Requested scene did not load | non-zero, no marker, names the scene mismatch |
| `Time.timeScale == 0` | non-zero, no marker, names the frozen clock |
| A text field / modal has focus | non-zero, no marker, names the focus owner |
| Hero position unchanged across the drive | non-zero, no marker, prints both positions |
| Healthy run | `HEADED_CAPTURE_OK <n> shots`, plus the start/end hero positions |

## Files

- `tools/capture/headed-dungeon-capture.ps1`

## What NOT to touch, and one constraint that is not negotiable

- **Keep `PrintWindow(PW_RENDERFULLCONTENT)` as the primary path**, and keep `Assert-Frontmost`
  throwing rather than warning. ⚠ On 2026-08-14 an earlier version of this harness used
  `CopyFromScreen` while `SetForegroundWindow` failed **silently**, and photographed the owner's live
  trading terminal — open positions and balances — into `docs/proof/`. Deleted immediately; verified
  never committed. **A capture tool whose failure mode is "wrong window" instead of "no file" is a
  privacy leak.** The same principle drives this whole ticket: the failure mode must be *no artifact*,
  never *a plausible wrong artifact*.
- **ASCII-only.** Windows PowerShell 5.1 reads BOM-less files as ANSI; em-dashes and smart quotes
  corrupt and break the parse. This already bit this exact file once.
- Do not touch any gameplay `.cs`, any `.unity` scene, or any catalog data.
