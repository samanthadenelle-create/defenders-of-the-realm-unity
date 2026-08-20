# WO-1024 RESULT — the repair surface now follows the town instead of racing it

**Implemented:** 2026-08-20, CLI seat (overnight autonomy).
**Option chosen from §3: (a) — install on first repairable.** Recorded here as the ticket requires.
**Also fixed:** the narrower defect the owner's 2026-08-19 ruling exposed — the deferral that never
retried in a scene without waves.

---

## 1. What was wrong, in two parts

**Part A — the affordance never installed in a restored town.** `HubRepairAffordance.TrySpawn` ran
once per scene load and bailed for good when `SceneHasRepairables()` answered false. The town is
player-built and restored from the save **after** the scene finishes loading, so at the instant the
gate ran the answer was legitimately "none yet". The predicate was not wrong — it was asked too
early. Meanwhile `StructureDamageVisuals` installs unconditionally, so fire still rendered. Fire with
no repair option is exactly that asymmetry.

**Part B — the ENABLED controller never installed in the hub.** The only path that creates an enabled
`WallRepairController` is `WaveFeedbackDirector.EnsureWallRepairInstalled`, which deferred when
`CoreServices.Hud` had not registered yet. Its doc comment claimed *"OnWaveCleared retries, by which
time the HUD is live"* — but that retry only fires on a **wave-cleared** event. In the hub, where no
wave may ever run, **the retry never came.** The two other paths (`HubRepairAffordance.EnsureRepair`,
`EchoRepairService`) deliberately create a **disabled**, logic-only controller, so tap-to-repair did
not exist at all.

## 2. What changed

| file | change |
|---|---|
| `Walls/HubRepairAffordance.cs` | new `internal static NotifyRepairableAppeared()`; `s_installedThisScene` guard so the call is a bool test on the hot path; cleared on scene load; bail-path `Warn` reworded (it is no longer terminal) |
| `Vfx/StructureDamageVisuals.cs` | raises `NotifyRepairableAppeared()` at the moment it tracks a structure — the one place that provably knows a repairable exists |
| `Core/CoreServices.cs` | new `HudRegistered` event, Guard-wrapped so a throwing subscriber cannot break HUD registration |
| `Waves/WaveFeedbackDirector.cs` | the deferral re-arms on `CoreServices.HudRegistered` instead of on a wave clearing; handler unsubscribes first (static event, survives scene loads) |

`StructureDamageVisuals` was chosen as the caller precisely because §3(a) asked for structural
symmetry: it already installs unconditionally and already knows a repairable exists. The repair
surface now **follows** the town.

## 3. Proof — captured, from a 4-run headless AutoPilot fleet

Identical in **4/4 runs** (`autopilot-runs/<n>/`, fresh logs, stale slate wiped by the fleet script):

```
[Flow:Repair]     hub repair affordance NOT installed (scene='Main_Castle_Overworld') ... found YET
[Flow:Repair]     hub repair affordance installed (scene='Main_Castle_Overworld')      <- the fix
[Flow:Repair]     hub repair affordance NOT installed (scene='Title')                  <- and stays out

[Flow:WaveClear]  wall-repair self-install deferred (scene load): CoreServices.Hud not registered yet
                  - RE-ARMED on CoreServices.HudRegistered (WO-1024)
[Flow:WaveClear]  self-installed WallRepairController (scene='Main_Castle_Overworld', hud-registered re-arm)
```

The first pair is the whole ticket: the hub says "nothing to repair yet" at load, then installs once
the town exists. The second pair is Part B: the enabled controller now arrives in a scene where no
wave ever ran.

### Acceptance criteria (§5)

- [x] Restored town → affordance present, not ABSENT — **4/4 runs**
- [x] Structure appears after load → affordance installs then — **that is the second line above**
- [ ] **Manage screen "Repair all" reachable in the UI** — NOT PROVEN. The enabled controller now
      exists (the trace proves it), and Manage resolves it by `FindFirstObjectByType`, so the
      mechanism is in place — but no run drove that screen. **Needs owner felt-verify.**
- [x] Title installs nothing — `NOT installed (scene='Title')`, and no later install line, 4/4
- [x] Exactly ONE affordance after repeated restores — one install line per run, 4/4
- [ ] `[Flow:RepairProbe]` across a full wave — **NOT PROVEN.** The probe emitted nothing in these
      runs: it reports when a structure is burning, and no fleet run burned one. The probe is intact
      and untouched (§4 honoured); it simply had nothing to say.

### Gates

`COMPILE_GATE_OK` · DataRegression **209/213, 4 failure(s) = the known-red baseline exactly**, nothing
new · `FLEET_PLAYERLOG_OK 4/4`.

## 4. §4 "Do NOT" — all honoured

`RepairAvailabilityProbe` untouched. The bail-path `FlowTrace.Warn` is **kept** and reworded, not
deleted — it now states the new contract, so the next occurrence is still one read.
`StructureDamageVisuals`' unconditional install is untouched; the repair surface rose to meet it, as
the ticket asked. `RepairAllCost` pricing and the repairable set are untouched.

## 5. One thing the owner should know

The 08-19 ruling is confirmed as already-correct in code and needed **no change**:
`HubRepairAffordance` hides REPAIR ALL unless `wave == null || Phase == Idle || Phase == Countdown`,
and `EchoRepairService` holds the same line via `BattleLock.IsInBattle()`. That is exactly *"cannot
build/repair during battle"* and *"available as soon as battle ends"*. The `// BY DESIGN` comment
there is now owner-confirmed rather than merely asserted.

**Status → IMPLEMENTED, awaiting owner felt-verify (PO closes, §13).** The two unproven boxes above
are the felt-test: damage a structure in the hub, end the wave, and confirm a repair button is
actually reachable.
