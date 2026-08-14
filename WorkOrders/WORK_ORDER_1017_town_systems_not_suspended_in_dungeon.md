# WORK ORDER 1017 — Town systems keep running inside dungeon scenes (suspension gate never fires)

**Status:** DONE — shipped `bb3293a3` *fix(world): WO-1017* (the `SuspendAndResume` gate now fires in
dungeon scenes). ⚠ Caveat: the `TownActivityProbe` invariant is the proof surface — a future dungeon
capture printing `suspended=False` in a non-hub scene reopens this.
**Minted:** 2026-08-10 (UI seat) — provenance stack bumped 1017 → 1018 in the same edit
**Lane:** Village/world systems + scene gating.
**Provenance:** F8 capture **seq=2314**, 2026-08-10 18:35, scene `Dungeon_HealersCottage`. Kind=**error**
(the harness raised it, not the owner). ⚠ This capture was auto-acked by `f8-ack.ps1` while a newer seq
was pending, so it is filed here explicitly to prevent loss.
Capture file: `logs/f8-inbox/capture-20260810-183545.md`.

---

## 1. RCA — the error is self-describing (§12: the data already names the dead gate)

`TownActivityProbe` fails its own invariant, in the dungeon:
```
[Flow:TownProbe] scene='Dungeon_HealersCottage' suspended=False grace=2.7s
  policy=SuspendAndResume reason='none'
  :: Enemy x1 in the ACTIVE scene (these MUST keep running)
  -> town systems are alive while the player is NOT in a hub scene,
     and the suspension is NOT engaged. The scene-driven gate did not fire for this scene.
```
Stack: `TownActivityProbe.Update` → `Poll` (`Assets/_Modules/Village/World/TownActivityProbe.cs:147`)
→ `FlowTrace.Fail`.

**Read plainly:** the probe's policy is `SuspendAndResume` and it correctly detects it is off-hub, but
`suspended=False` and `reason='none'` — i.e. **nothing ever asked it to suspend**. The scene-driven gate
that should fire on entering a non-hub scene does not fire for dungeon scenes. Town systems therefore
keep ticking while the player is in a dungeon (and at least one Enemy is live in the active scene).

Why it matters beyond noise: off-hub town activity burns frame budget in dungeons, can drive spawning /
timers / AI that the dungeon does not want, and it is a **silent-until-instrumented** class of bug — the
only reason we know is that a previous session wired this FlowTrace.Fail. (Good instrumentation paid off;
per §12 it must stay.)

## 2. What to do

1. **Instrument, then fix (§12).** Trace the gate's inputs: which scene-load event feeds it, what set of
   scene names/kinds it treats as "hub", and why `Dungeon_HealersCottage` misses. Read the capture before
   editing.
2. **Prime hypothesis to TEST (not assume):** the gate matches an allow/deny list of scene names (hub =
   `Main_Castle_Overworld`, etc.) and dungeon scenes — especially the **composed/baked `DungeonCompose`
   scenes**, which are generated and thus not in any hand-written list — fall through the match and are
   treated as neutral, so no suspend is requested.
3. **Fix the classification, not the symptom.** The gate should decide from a scene's KIND (hub vs
   dungeon vs raid vs overworld) resolved from a single authority, so a NEW scene is classified correctly
   the day it is baked — never from a hand-maintained name list that goes stale (the same duplicated-state
   failure canon warns about repeatedly).
4. Keep the invariant loud: `TownActivityProbe`'s `FlowTrace.Fail` stays exactly as it is, so a future
   regression re-announces itself.

## 3. Acceptance criteria

- [ ] Entering any dungeon scene engages suspension: `[Flow:TownProbe] suspended=True` with a stated
      `reason`, and NO `FlowTrace.Fail` in the capture.
- [ ] Leaving the dungeon back to the hub RESUMES town systems (policy is SuspendAndResume — prove both
      halves; a suspend that never resumes is a worse bug).
- [ ] Classification is derived from scene kind, not a hand-written name list; a newly baked
      `DungeonCompose` scene is correctly classified with no doc/list edit.
- [ ] Raid + overworld scenes classified and behaving correctly (sweep, don't spot-fix).
- [ ] The "Enemy x1 in the ACTIVE scene" carve-out still holds — active-scene enemies MUST keep running.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`; add a regression that asserts suspension state
      for one scene of each kind.

## 4. What NOT to touch

- The probe's error text/instrumentation (it is the detector — never strip, §12).
- Dungeon locomotion/camera (WO-1016), dungeon composition/bake.
- Wave/spawn balance — this WO changes WHEN town systems run, not what they do.
