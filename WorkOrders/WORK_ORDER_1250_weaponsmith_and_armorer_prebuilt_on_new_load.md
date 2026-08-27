# WORK ORDER 1250 - Weaponsmith and Armorer show as ALREADY BUILT on a new load

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated).
**Silo:** Village / Buildings + save state
**Severity:** P1. A new player is handed two buildings they never built, which breaks the founding
sequence and the economy that the first hour is balanced around.
**Origin:** Owner, on device, 2026-08-27: *"weaponsmith and armorer show as buil on new load"*.
Tester APK built 11:47, commit `fffa4ea9c`.

---

## What was reported

On a **new load**, the Weaponsmith and the Armorer appear already built.

⚠ **"New load" needs one clarification before deep work, and it changes the diagnosis completely:**
- a **brand new save** (nothing should be built), or
- **reloading an existing save** (in which case the question is whether those two were ever built).

If the trace makes it obvious, say which from the data and proceed. If it does not, ask - do not pick
one and build on it.

## Where to look first, and why it is probably not "the buildings are wrong"

⭐ **`everBuiltStructureIds` (save schema v36) exists precisely to answer "has this ever been built".**
It was added for the blank-town baked-standdown problem - the town is BAKED with structures present,
and the save decides which of them the player actually owns. So a structure appearing pre-built on a
new save is far more likely to be *the standdown not standing it down* than the building itself
being spawned.

That makes the likely suspects, in order:
1. The baked hub scene contains these two, and the standdown that should hide them is not covering
   them (missing id, wrong id, or they were added to the bake after the standdown list).
2. A founding/seed path grants them.
3. `everBuiltStructureIds` is being populated at boot rather than on build.

⛔ **Do not theorise between these - INSTRUMENT (CLAUDE.md section 12).** Put `FlowTrace.Step` on the
standdown decision for each structure id and read which branch these two take. A static read of the
builder will locate candidates and will not conclude the cause.

⚠ **`adb logcat -d` after the fact will NOT contain the boot window** - the 256 KiB ring plus the
`[Flow:Offset]` firehose evicts it. Start the capture before launching.

## Also reported in the same breath - already handled, do NOT re-open here

The owner also saw *"some type of new pillar"*. That was the **WO-1073 Founders Monument stand-in**,
whose Addressables address does not exist yet, falling back to grey primitive cubes. Its feature flag
was defaulted **OFF** the same day (`FeatureFlags.FoundersMonument`). **It is unrelated to this
ticket** and must not be conflated with the pre-built buildings.

## Required

1. The branch that leaves these two standing, named from a captured trace line.
2. The fix at that seam.
3. Confirmation of which structures SHOULD exist on a brand new save, checked against canon rather
   than against what the scene happens to contain.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. A regression pinning the founding state: a brand new save owns exactly the intended set and no
   more. Prove RED first (WO-1138) by adding a structure to the baked set and watching it fail -
   that is the shape of the bug being fixed.
3. ⛔ The guard must not be a hollow pass: if it cannot resolve the baked set, it must assert or emit
   `RegressionOutcome.Skip`/`PartialSkip`, never return quietly green.
4. Owner felt-verifies with a fresh save on device.

## What NOT to touch

- ⛔ Do not hand-edit any `.unity` scene (CLAUDE.md section 3). If the bake is wrong, write what the
  builder must do and say so.
- ⛔ Do not change `SaveSchema.CurrentVersion` or migrate saves to work around this. If the save is
  wrong, fix what writes it.
- ⛔ `RepoProps.MaxStructureLevel` and the storage/capacity ladders are unrelated.
