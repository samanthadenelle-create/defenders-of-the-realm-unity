# WORK ORDER 1361 - Player structures vanish from the save

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Core / save + base layout persistence
**Type:** EXISTING system, silent data loss
**Minted:** 2026-09-03 (CLI). ⚠ RETROACTIVELY, on a defect that has been visible in device logs
since **2026-08-19** and was never ticketed.
**Severity:** ⛔ **P0 - this is player progress disappearing.** It outranks every cosmetic item on
the board.

## THE CAPTURED DATA

From her live session, 2026-09-03, entering build mode:

```
[Flow:BaseLayout] Enter build mode CENSUS: live PlacedStructure(s) in scene=9,
  loader.Loaded=9, persisted BaseLayout=17, scene='False'-enemyOwned.
  live << persisted = structures already gone before this build session
  (F8-39 vanish happened earlier).
```

**Seventeen structures persisted. Nine in the world. Eight gone.**

⭐ **AND IT IS A FORTNIGHT OLD, NOT NEW.** The canon sweep found the same census in the archived
device logs - `grep "Enter build mode CENSUS" logs/device/*.log`:

```
2026-08-19 20:01   0 live / 0 loaded / 8 persisted
2026-08-20 09:04   0 live / 0 loaded / 8 persisted
```

So the loader has been failing to replay persisted structures for at least two weeks, in at least two
distinct shapes (all-missing, and partially-missing). **Nobody has ever worked it.**

## ⛔ WHAT IT IS NOT - do not spend a session re-deriving this

**Destruction cannot explain it.** Per WO-1357 (`e63494ed8`), `Destructible.NotifyBroken` frees the
footprint, calls `BaseLayoutLoader.Forget`, **DROPS the persisted `BaseLayout` record**, burns the
free-build and destroys the object. So a genuine destruction **LOWERS `persisted`**. Here `persisted`
stays high while `live` falls - the opposite signature. Something is failing to REPLAY records that
are still on disk, or replaying them into a state that is then discarded.

## THE INSTRUMENT ALREADY EXISTS - read it before touching code

The census line above is the loader telling you it noticed. ⛔ **Read the captured data first**
(CLAUDE.md §12 and §11B) - the answer to "which eight, and at which step" is very likely already
derivable from `[Flow:BaseLayout]` plus the save file, without a single code change. Split it before
theorising:

- **Never loaded** - the record is on disk and `BaseLayoutLoader` skipped it. Why? An unresolvable
  structure id, a footprint that no longer fits, a catalog row that moved.
- **Loaded then destroyed** - `loader.Loaded=9` says only nine were even loaded on the 09-03 capture,
  which points AWAY from this. But the 08-19/08-20 captures read `0 loaded` with 8 persisted, which is
  a different and even starker failure. **Establish whether these are one defect or two.**
- **Persisted but never real** - records written for structures that never successfully placed, so the
  count was always a lie.

⚠ **`live << persisted` is a symptom with several possible causes and static reading will not choose
between them.** Get a capture that names the eight by id and the step they died at. If the current
trace cannot do that, ADD that before fixing anything - a future occurrence must name itself.

## Relevant, verified today

- Hub structures are **BAKED TWINS** re-skinned by `HubStructureVisualInjector` and do NOT route
  through `StructureFactory`. A census that counts `PlacedStructure` components may legitimately miss
  baked ones - **confirm what the census actually counts before treating a mismatch as loss.**
- `BuildModeController.Place` appends the `BaseLayout` record **before** the build timer starts, so a
  structure interrupted mid-build has a record and no finished object. Worth checking whether the
  missing eight were all in-flight builds.
- Save schema is **v38** - read it off `SaveSchema.CurrentVersion`, never a doc.

## Acceptance

- [ ] A capture names the missing structures by id and the exact step each was lost at. Quote it.
- [ ] Whether the 08-19/08-20 (`0 live / 0 loaded / 8 persisted`) and 09-03 (`9 / 9 / 17`) shapes are
      ONE defect or TWO - answered with evidence.
- [ ] The cause proven from data, not inferred. ⛔ No fix before that line exists.
- [ ] A player who builds N structures and relaunches has N structures. Pinned by an oracle, proven
      RED first.
- [ ] The census keeps its FlowTrace so a recurrence names itself. Never strip it.
- [ ] ⛔ **Owner felt-verifies across a relaunch and closes.** Nothing about this is provable by a
      headless gate alone.
