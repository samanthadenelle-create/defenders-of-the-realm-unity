# WORK ORDER 995 - Booting into a dungeon self-evicts to town: the hero spawns INSIDE the exit trigger

**Status:** FIXED — 2026-08-15 (code; PO 10× boot verify owed)

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*
**Minted:** 2026-08-14 (CLI)
**Silo:** Dungeon routing
**Found by:** WO-988 acceptance runs (11 live launches)

---

## The defect, from a live log

```
DungeonExitInteractable.OnTriggerEnter -> Leave()
  -> [Flow:SceneRouter] LoadSceneWithFade name='Main_Castle_Overworld'
```

Seconds after boot. **The hero spawns inside the dungeon exit's trigger volume**, so booting directly
to a dungeon immediately routes back to town.

## It is NONDETERMINISTIC - which is worse than a hard failure

Of **6 live dungeon launches**, some stayed in the dungeon and some evicted. A hard failure would be
diagnosed once; a coin flip gets re-rolled and blamed on something else each time.

## What it already cost

**This is the true cause of the 2026-08-14 wrong-scene capture.** The headed harness reported
`HEADED_CAPTURE_OK 10 shots` on ten screenshots of the frozen town while claiming to prove a dungeon
fix (see `docs/proof/2026-08-14-wo1007-portal-camera/INVALID_CAPTURE_README.md`). WO-988 fixed the
harness so it now REFUSES that run (exit 5) - but the harness was only ever reporting this bug.

## Consequence for other tickets

⚠ **Any WO-1007 / WO-980 / WO-983 re-capture is a lottery until this is fixed.** A dungeon acceptance
capture can silently land in town; WO-988 now catches it, so the failure mode is a wasted run rather
than false evidence - but the run still cannot be relied on to produce a result.

## Fix directions (pick from evidence, do not guess)

- Spawn the hero **outside** the exit trigger, or
- arm the exit trigger only after a grace period / after the hero has first LEFT the volume, or
- have `Leave()` ignore a trigger entry that occurs within N seconds of scene load.

⚠ **Instrument before choosing** (CLAUDE.md SS12): the trace must show the spawn position, the trigger
volume bounds, and whether the hero began inside it - so "spawned inside" is distinguishable from
"walked in immediately".

## Acceptance

- 10 consecutive `-bootScene` dungeon launches stay in the dungeon. Nondeterministic bugs need
  repetition, not one green run.
- The exit still works normally when the hero actually walks into it.

## Related

- `DungeonExitInteractable.cs` also owns the portal seat/normalize work (WO-1007) - do not disturb it.
- WO-988 gave the harness exit code 5 for scene mismatch; that is the detector, not the fix.