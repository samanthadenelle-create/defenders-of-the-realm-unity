# WORK ORDER 1024 — Structures burn with NO repair surface: the install gate runs once, before the town exists

**Status:** CLOSED 2026-08-24 — owner felt-tested and closed.
`StructureDamageVisuals` raises `HubRepairAffordance.NotifyRepairableAppeared()` the moment it tracks a
structure, so the repair surface follows the town instead of racing it; and the §"remaining defect"
below is closed too — `WaveFeedbackDirector`'s deferral now re-arms on a new `CoreServices.HudRegistered`
event instead of on a wave clearing, so the ENABLED `WallRepairController` installs in a scene that never
fights. Proven in **4/4 headless fleet runs**: `NOT installed ... found YET` -> `installed`, `Title` stays
empty, and `deferred (scene load)` -> `hud-registered re-arm` -> `self-installed WallRepairController`.
⚠ TWO BOXES UNPROVEN — the Manage-screen "Repair all" was never driven, and `[Flow:RepairProbe]` emitted
nothing because no fleet run burned a structure. See the RESULT.
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1024 → 1025 in the same edit
**Lane:** Village / Walls — repair affordance lifecycle. Disjoint from WO-1021 (talent UI) and
WO-1022 (scene GUIDs).
**Provenance:** owner F8 **seq=2398** (also seen at seq=2342), 2026-08-15,
`logs/f8-inbox/capture-20260815-214846-seq2398.md`.
**Severity:** player-facing dead end. A structure is on fire, a wave is live, and there is **no way to
repair anything for the rest of the session**.

---

## 1. The captured line — this is the data, not a theory

```
[Flow:RepairProbe] SURFACES scene='Main_Castle_Overworld'
  WallRepairController=ABSENT  HubRepairAffordance=ABSENT  WaveManager=Active
  -> NO repair surface exists in this scene at all while a structure burns.
     The player has no way to repair anything here.
```
`RepairAvailabilityProbe.cs:209` ← `:136` ← `:118`

Both repair surfaces are absent **while `WaveManager` is Active and a structure is burning**. This is
the failure the probe was written to catch, and it caught it.

## 2. Root cause — proven, and the code PREDICTED it in a comment

`HubRepairAffordance.cs:88-95` — the installer is a one-shot per scene load:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void InstallHook()
{
    SceneManager.sceneLoaded -= OnSceneLoaded;
    SceneManager.sceneLoaded += OnSceneLoaded;
    TrySpawn();
}
private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TrySpawn();
```

`TrySpawn` (`:98-124`) calls `SceneHasRepairables()`; if false it **returns and never retries**.

The bail path's own comment, written before this capture existed, states the defect verbatim
(`:111-116`):

> *"This check runs ONCE per scene load and never retries, so structures rebuilt AFTER load (saved
> placement restore) would leave the player with no repair surface in this scene."*

**That is exactly what happens in `Main_Castle_Overworld`.** The town is **player-built** (canon §8:
strategic placement always on, movable storefronts) — structures are restored from the save **after**
the scene finishes loading. At the instant the gate runs, the town is empty, `SceneHasRepairables()`
returns false, the affordance never installs, and nothing installs a `WallRepairController` either.

**The asymmetry that makes it player-visible** is also already documented at `:107-110`:
`StructureDamageVisuals` installs **UNCONDITIONALLY**, so fire still renders. Fire with no repair option
is precisely that split.

⚠ **This is NOT a coverage bug — do not "fix" it by widening `SceneHasRepairables()`.** That gate was
already widened once (see the `COVERAGE FIX` comment at `:126-140`, extending it to DefenseTower /
ArcaneTower / HarvestSite / ResourceCollector so the installer's reach matches
`WallRepairController.RepairAllCost`). Widening the *predicate* cannot help when the predicate runs at a
time when the answer is legitimately "none yet". **The bug is the TIMING, not the set.**

## 3. What to change

**Make installation event-driven or retrying, not a one-shot at scene load.** Options, in the order I'd
weigh them — CLI picks and records the choice in the RESULT:

**(a) Install on first repairable, not on scene load.** Have structure registration (or
`StructureDamageVisuals`, which already installs unconditionally and therefore already knows a
repairable exists) raise the install. This inverts the dependency so the affordance follows the town
instead of racing it. **Preferred** — it makes the two systems structurally symmetric, which is what
`:107-110` says the defect is.

**(b) Re-arm after save-restore.** Subscribe the installer to whatever signals placement-restore
completion and re-run `TrySpawn` then. Correct, but leaves the ordering dependency in place — a second
async restore path would reintroduce the same race.

**(c) Cheap retry.** Re-poll on a low-frequency tick until repairables appear, then install once.
Robust and trivial, but a poll where an event exists; acceptable only as a stopgap.

**Whatever is chosen, `TrySpawn` must remain idempotent** — it already early-returns when an instance
exists (`:100`), and any retry path must keep that guarantee so a burst of restores cannot install two.

## 4. Do NOT

- **Do not remove or quieten `RepairAvailabilityProbe`.** It is the instrument that caught this.
  CLAUDE.md §12 (owner ruling 2026-08-09): instrumentation is permanent; flag it down, never strip it.
- **Do not delete the `FlowTrace.Warn` on the bail path** (`:111-117`). That warning is what makes the
  next occurrence one read instead of one theory.
- Do not touch `StructureDamageVisuals`' unconditional install — it is the *correct* side of the
  asymmetry; the repair surface should rise to meet it.
- Do not change `WallRepairController.RepairAllCost` pricing or the repairable set.

## 5. Acceptance criteria

- [ ] Load `Main_Castle_Overworld` with a **saved town** (structures restored after scene load) and
      confirm the probe logs `WallRepairController=present+ENABLED` /
      `HubRepairAffordance=present:<state>` — **not** `ABSENT`
- [ ] Load a **blank / newly founded** town, place one structure, and confirm the affordance installs
      **after** placement (this is the case the one-shot gate could never satisfy)
- [ ] Damage a structure and confirm a repair path is reachable in the UI — the Manage screen's
      "Repair all" resolves its controller (it looks it up via `FindFirstObjectByType`)
- [ ] Title / HeroSelect / menu scenes still install **nothing** — the gate's original purpose is
      preserved, no affordance in scenes with genuinely nothing to repair
- [ ] Exactly ONE `HubRepairAffordance` after repeated restores/placements (idempotence held)
- [ ] `[Flow:RepairProbe]` emits `Step`, not `Fail`, across a full wave in a saved town

## 6. Verify

1. Brace-balance on every `.cs` touched
2. `COMPILE_GATE_OK`
3. `REGRESSION_OK <n>/<n> suites`
4. **Headless AutoPilot run against a SAVED town** — the trace line in §1 is the oracle. Grep the run
   for `[Flow:RepairProbe]` and require zero `Fail`. ⚠ A blank-town run will pass vacuously and prove
   nothing; the save-restore ordering **is** the bug.
5. Owner felt-verifies + closes (§13)

## 7. Note for the board

This capture was queued behind ~48 duplicates of the WO-1022 GUID flood on 2026-08-15 and surfaced only
when the queue was drained. It is the second real signal that noise was burying (the other: two
`[Flow:Tutorial] STEP-STUCK` lines, still un-ticketed). **Argument for prioritising WO-1022:** until the
scene throws stop, every genuine capture arrives buried under four duplicates.

---

## ⛔ OWNER RULING 2026-08-19 — the wave gate is CORRECT. The defect is the missing controller.

> Owner, verbatim: **"cannot build/repair during battle"** and **"as far as repairing should be
> available as soon as battle ends"**.

**This CLOSES the open design question in this ticket.** `HubRepairAffordance.cs:207-219` hides the
REPAIR ALL surface unless `wave == null || Phase == Idle || Phase == Countdown` — i.e. it hides during
Active/Breached and returns the moment the wave ends. That is exactly the ruling, so **the gate needs no
change** and its `// BY DESIGN` comment is now owner-confirmed rather than merely asserted.
`EchoRepairService.cs:310` holds the same line for passive mend (`BattleLock.IsInBattle()`), and that is
also correct.

**So the remaining defect is narrower than the ticket title suggests.** It is not "no repair surface in
hub" as a policy problem — it is that the surface can be absent when it SHOULD be present:

- `HubRepairAffordance.cs:180-186` and `EchoRepairService.cs:376-388` both create their
  `WallRepairController` **disabled** (logic-only), so tap-to-repair does not exist in the hub.
- The only path that installs an ENABLED one, `WaveFeedbackDirector.EnsureWallRepairInstalled`
  (`:407`), **defers and returns without retry** when `CoreServices.Hud` is not yet registered
  (`:411-417`). Its doc comment claims *"OnWaveCleared retries, by which time the HUD is live"* — but
  that retry only fires on a wave-cleared event. **In the hub, where no wave may ever run, the retry
  never comes**, and the controller stays absent for the whole session.

That is the WO-1024 capture verbatim: `WallRepairController=ABSENT` while `WaveManager=Active`.

**Acceptance, restated against the ruling:** after a wave ends, every repair surface is present and
usable within one frame of `Phase` returning to Idle — AND the hub has a usable repair surface even in a
session where no wave ever runs. The second half is the one nothing currently guarantees.

**Do NOT "fix" this by relaxing the wave gate.** The owner has now ruled it twice.
