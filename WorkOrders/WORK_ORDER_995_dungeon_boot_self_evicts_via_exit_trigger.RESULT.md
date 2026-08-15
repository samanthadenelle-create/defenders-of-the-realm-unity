# RESULT — WO-995 Dungeon boot self-evict

**Status:** IMPLEMENTED (code) — 2026-08-15  
**PO felt-verify owed:** 10 consecutive dungeon boots stay in dungeon; walk-in exit still works.

## Change

`DungeonExitInteractable.cs`:
- Boot grace **2.0s** after level load — `Leave()` refused.
- Arm only after hero is **clear of trigger+0.75m for 0.35s** (not a single sample).
- `Leave()` (button + trigger) both go through `CanLeave()` — button no longer bypasses arm.
- FlowTrace when spawn is inside volume and when arm/refuse fires (spawn pos vs exit pos).

## Why

Prior `_armed` on first clear sample was racey with spawn jitter; interact button called `Leave` without checking arm.

## Not done by this RESULT

- Headless 10× boot loop (needs player / batch dungeon load) — owner play or capture with WO-988 harness.
