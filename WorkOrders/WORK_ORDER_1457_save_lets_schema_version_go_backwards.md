# WO-1457: /api/game/save lets schema_version go BACKWARDS and defaults a missing version to 10

**Status:** FIXED locally 2026-09-06 (321b753c4) - all four acceptance criteria met; vercel --prod push still owed, HELD behind WO-1446 (see RESULT).
**Silo:** `api/game/save.js`.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1457 -> 1458 in the same edit).

## 1. EVIDENCE

```
api/game/save.js:332   incoming schema_version defaults to 10 when absent
api/game/save.js:339   schema_version = EXCLUDED.schema_version      // no GREATEST()
```

The live client schema is v38 (`SaveSchema.CurrentVersion`). An old build, a replayed request, or a payload
that simply omits the field therefore stamps the row back to 10 while writing its state. The next load then
runs the wrong migration chain against data that is not shaped for it.

## 2. FIX SHAPE

- `schema_version = GREATEST(game_saves.schema_version, EXCLUDED.schema_version)` on the upsert.
- Refuse an explicit downgrade with a distinct error code rather than silently accepting the write, so a
  stale client is visible instead of quietly corrupting.
- Drop the default-to-10: an absent version is a malformed payload, not a v10 payload.

## 3. WHAT NOT TO DO
- Do not fix this by having the client always send the version. The server must not accept a downgrade
  regardless of client behaviour.

## 4. ACCEPTANCE
- [ ] `GREATEST()` on the upsert; explicit downgrade refused with a named code.
- [ ] `node --test` cases: downgrade refused, equal version accepted, upgrade accepted, absent version refused.
- [ ] `node --test` green across `test/`.
