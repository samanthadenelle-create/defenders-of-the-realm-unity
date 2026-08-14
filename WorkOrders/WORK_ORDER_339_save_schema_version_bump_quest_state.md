# WO-339 — SaveSchema: add quest state versioning

**Status:** SUPERSEDED

> **SUPERSEDED - determined 2026-08-14 (phantom sweep).** Successor: `QuestProgress` @ save schema v38.
> This WO was still being re-served by the DERIVED board (BOARD.html) because its Status line was
> never flipped when the successor work landed (CLAUDE.md §2). Body preserved below unchanged.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Depends on:** WO-290 (QuestService exists)

**Lane:** 7 (Persistence/Backend) — coordinate with SaveMigrator

---

## Summary

SaveSchema / SaveMigrator needs new fields to persist quest progress state. This WO is the **version-bump pass** for the SaveSchema enum and initial migration stub to unblock all quest data persistence (WO-290, WO-291, WO-294, WO-299, WO-300, WO-304, WO-305 all write quest data).

**Single-writer rule applies:** Only ONE agent bumps SaveSchema version + adds new field stubs at a time (to prevent merge conflicts in GameState.cs).

---

## Files to edit

- `Assets/_Modules/Core/Persistence/SaveSchema.cs`
  - Add `SAVE_VERSION = X → X+1` (ask CLI which version is current)
  - Add new field `public Dictionary<int, QuestRecord> QuestLog { get; set; } = new();`
  - QuestRecord struct: `questId`, `state`, `progress`, `completedAt`
- `Assets/_Modules/Core/Persistence/SaveMigrator.cs`
  - Add migration method for new version (stub that copies old fields + initializes QuestLog)
- No edits to GameState.cs (leave for quest-specific WOs to fill in the properties)

---

## Acceptance criteria

- [ ] SaveSchema version incremented + changelog noted
- [ ] QuestLog field added with proper serialize/deserialize attributes
- [ ] SaveMigrator migration method creates empty dict on load (no crash if field missing in old saves)
- [ ] Brace balance check passes
- [ ] Build succeeds on Windows (CompileGate)
- [ ] No new System.Reflection usage introduced

---

## What NOT to do

- Do NOT add quest verbs or logic
- Do NOT wire GameState.QuestLog to anything
- Do NOT create new quest data types (QuestRecord goes here; content lives in WO-290/291/etc.)
- Do NOT edit Village or HUD assemblies

---

## Notes

This is a **schema anchor** — once it lands, quest-related WOs (291/294/299/300/304/305) can all proceed in parallel and add their data fields without stepping on each other (they'll each touch GameState, never SaveSchema again).
