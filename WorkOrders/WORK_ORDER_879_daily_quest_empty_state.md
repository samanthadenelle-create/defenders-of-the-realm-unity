# WORK ORDER 879 — Daily Quests: duplicated empty-state across two mismatched columns

**Status:** READY. **Lane:** HUD/UI — `DailyQuestHud.cs` / `DailyQuestVM`. **WO#:** UI-seat block; **879**.
**Source:** `docs/ui-review/screens-2026-08-04/DailyQuestHud_2340x1080.png`.

## 1. Bad (from the capture)
The empty state renders **TWICE** in two **mismatched** columns: a big black left column says *"No daily quests
today."* and a parchment right column says *"No daily quests today / Fresh quests arrive with the new day."* — same
message, two different chrome languages, ~half the panel dead black. `Close` floats centre-bottom straddling both
columns and the frame edge.

## 2. Fix — one empty-state, from the VM; the View renders it once
- **MVVM law — the View must not DUPLICATE state.** The VM (`DailyQuestVM`, it already has `IsEmpty`) owns the single
  empty-state message; the View renders it in **ONE** place, in one chrome, not once per column. Remove the redundant
  second column (or collapse to a single centered empty panel when `IsEmpty`).
- When there ARE quests, use one consistent quest-row language (not black-vs-parchment).
- `Close` sits in its own footer band (consistent chrome), not floating over the frame. Fixed-pixel bands.

## 3. Acceptance
- [ ] On-device: the "no quests" message shows ONCE in one chrome (not duplicated across two mismatched columns); no
      large dead-black half; Close in consistent footer chrome. `CompileGate` green. Verify on Seeker.

## 4. Do NOT
- Do NOT let the View compute/duplicate the empty-state (VM owns it). No fraction bands. No scene edits.
