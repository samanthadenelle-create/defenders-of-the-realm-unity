# WORK ORDER 1227 - Device F8 captures never reach the inbox. The owner has been the detector all day.

**Status:** FIXED 2026-08-26 — the additive device-to-existing-inbox bridge landed in `89977006a` and is proven end to end on Seeker `SM02G4061955851`: device flag entry `2026-08-26T17:35:16.9235460Z` published through the existing queue as `capture-device-20260826-132348-seq3608.md` with real screenshot candidates, and the same queue subsequently carried seq 3609 and 3610; `ACK.json` records the one-at-a-time high-water acknowledgement through seq 3609 while seq 3610 remains independently pending. `device-state.json` holds only the per-device read watermark (`lineOffset: 741`, bounded seen keys and last UTC), and repeated polling after the last publish advanced/retained that watermark without duplicating queue entries. Three fresh no-device passes on 2026-08-26 each returned the required silent-no-op result and exit 0. `logs/f8-inbox/DEVICE_BACKFILL_2026-08-26.md` exists and readably digests all 736 historical entries without flooding the live queue. Source inspection confirms `Publish-F8Capture` from the existing `f8-inbox-lib.ps1`, the existing `QUEUE.jsonl`, and existing `ACK.json` remain the sole inbox/queue/ack authorities. `89977006a` is an ancestor of APK source `bcef3be7`; the fresh tree also passed `COMPILE_GATE_OK` and `REGRESSION_OK 292/292`. Awaiting owner felt-test of a new FLAG flowing to the seat to close.
**Silo:** Tooling / F8 pipeline
**Severity:** P0 for the WAY WE WORK. Not a game defect — a hole in the machinery CLAUDE.md §14
exists to guarantee.
**Origin:** Owner used the on-screen FLAG on Seeker build `2026.08.26.342290`, then asked
***"did it reach you?"***. It had not.

---

## PROOF

`f8-check-inbox.ps1` reported `NO_CAPTURE ack=3607 ping=3607` — unchanged all day.

Meanwhile, on the device (`adb shell ls`,
`/sdcard/Android/data/com.denellestudios.echoesofelarion/files/`):

```
break-log.jsonl        450 KB   12:28
break_01_error.png     2.2 MB   12:28     <- her flag
break_00_error.png     3.5 MB   12:17
break_02_error.png     3.0 MB   10:14     ... eight PNGs from today alone
```

Pulled and parsed, that log holds **729 entries**:
`error 588 · exception 25 · possible_softlock 6 · flagged 7 · scene_loaded 81 · session_start 21`
— with entries dating back to **2026-07-20**. **None of it has ever reached a seat.**

## THE GAP

`BreakCaptureHarness` writes correctly on device. `f8-watch-daemon.ps1` watches the DESKTOP path
(`%LOCALAPPDATA%Low\DeNelle\…`). **Nothing moves a capture off the phone.** So on desktop the §14
chain is whole, and on device it is severed at the first link — which is the only platform the owner
actually plays on.

⚠ `-Tester` (WO-1226-era work today) gave her the FLAG button. **The button was necessary and not
sufficient** — it writes to a place the listener has never looked. Do not mistake the button landing
for the chain working; that was today's mistake.

## Required

A **device → inbox bridge**, arriving in the same queue the desktop path already feeds, so a flag on
the Seeker surfaces exactly like a flag on the exe.

- Pull `break-log.jsonl` + the `break_*.png` set over `adb` and publish each new capture through the
  EXISTING queue lib — `.claude/skills/run-defenders/f8-inbox-lib.ps1` (`Publish-F8Capture`,
  `Get-F8Pending`, ack state). ⛔ **Do NOT build a second inbox.** WO-965 already made this an
  append-only QUEUE (`logs/f8-inbox/QUEUE.jsonl`) because a single-slot inbox silently buried the
  owner's seq 2307 and 2308 on 2026-08-10.
- **Track a device-side watermark** so a pull is incremental and idempotent. The log is append-only
  and 450 KB already; re-publishing 729 entries on every poll would bury the queue as thoroughly as
  the current silence does.
- ⚠ **Filter the same way the desktop daemon does:** `session_start` and `scene_loaded` are startup
  noise. Fire on `flagged` / `error` / `exception` / `possible_softlock` only.
- ⚠ **`adb` is NOT on PATH.** It lives under the Unity Hub Android SDK
  (`…/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe`). Resolve it, do not assume a bare
  `adb`.
- ⚠ **Git Bash mangles device paths.** `/sdcard/...` becomes `C:/Program Files/Git/sdcard/...`. Use
  `//sdcard/...` or set `MSYS_NO_PATHCONV=1`. This cost a failed pull today.
- **Handle no-device gracefully.** The phone is usually unplugged. A missing device is a silent
  no-op, never an error that trains the seat to ignore the daemon.

## ⭐ Backfill the 729 that are already there

Those entries are real, unread evidence spanning five weeks — 6 softlocks and 25 exceptions among
them. **Do not silently import all 729 into the live queue** (that buries today's). Produce a
one-shot triage DIGEST at `logs/f8-inbox/DEVICE_BACKFILL_2026-08-26.md`: every `flagged`,
`possible_softlock` and `exception`, plus deduped `error` messages with counts and first/last seen,
newest first. The owner and the lead read it once and decide what becomes a ticket.

## Acceptance

1. A flag pressed on the device surfaces via `f8-check-inbox.ps1` as `NEW_CAPTURE` with a real
   `capture=` path, and `f8-ack.ps1` acks exactly ONE. Demonstrate it end to end.
2. Polling twice with no new device activity publishes nothing (watermark proven idempotent).
3. With no device attached: silent, exit 0, no error spam.
4. The backfill digest exists and is readable.
5. ⛔ No second inbox, no second ack state, no second queue file.

## What NOT to touch

- ⛔ `f8-inbox-lib.ps1`'s queue semantics or the ack watermark (WO-965).
- ⛔ The desktop watch path. This is additive — the exe path keeps working unchanged.
- ⛔ `BreakCaptureHarness` on the game side. It is writing correctly; the defect is transport.
