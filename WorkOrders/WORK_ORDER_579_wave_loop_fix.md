# WORK ORDER 579 — Village Wave-Loop Felt-Bug Cluster (MainCastle_Hall)

**Status:** IMPLEMENTED (edit-only; not gated/committed — orchestrator batch-gates + commits)
**Date:** 2026-06-28
**Source:** Owner F8 felt-test cluster (5 gaps) + coordinator-relayed felt detail
**Branch base:** `wip/village2-and-f8-tickets` @ `d455bd42` (ff-merged to tip first, per contention note)
**Silo:** Combat/Waves + HUD-context (file-disjoint from the just-committed 9-zone/flash/talent work)

---

## RCA — data-grounded (file:line)

### 1. START WAVE doesn't flip to the Battle HUD
`BattleHudVisibilityManager.EvaluateMode()` is the visible HUD-mode driver (fades the Town group,
spawns `BattleHud9Zone`). At **BattleHudVisibilityManager.cs:310-316** a prior fix (#7) explicitly
**removed** `IsWaveActive()` as a Battle trigger — comment: *"a hub wave must NOT pull up the legacy
wave-defense Battle HUD … it is no longer a Battle trigger."* So a live wave kept the Town HUD. The
owner has **reversed** that decision: a live wave should show the Battle (9-zone) HUD.

### 2. Top-left next-wave COUNTDOWN timer shows no value
The timer exists: `VillageHudController` builds the top-left gear-clock + `_townTimerText`
(**VillageHudController.cs:2059-2083**) and `PollWaveTimer()` (**:791-852**) writes
`_townTimerSeconds` from `WaveManager.CountdownRemaining` while `Phase == Countdown`. The display
block (**:922-945**) shows `MM:SS` during Countdown and `"IN WAVE"` during Active. The logic is
correct — it reads blank because **the loop never enters the Countdown phase** (see #3): nothing
arms it, so `_townTimerSeconds` stays `-1` → empty text, then jumps straight to `"IN WAVE"` when a
wave is forced active.

### 3. No auto-trigger (Start Wave was the only kickoff)
`WaveManager._autoStart` defaults **false** (**WaveManager.cs:170**) and `Start()` only kicks the
loop when `_autoStart && !IsFirstRun()` (**:482**). The baked MainCastle_Hall WaveManager has it
serialized OFF, and the **only** other kickoff is the HUD "Defend/Start Wave" button
(`StartWaveHudBridge → ForceBeginNextWave`). So the prepare-phase countdown never runs on its own
and waves never auto-attack.

### 4. Wave "~3" auto-launches the deprecated ATBBattle scene
The **only** ATB launch from the village loop is `TriggerBreach() → EnterAtbBattle() →
SceneRouter.GoBattle()` (**WaveManager.cs:1846 / 1907**), fired from the breach-ring detection in
`TickActiveWave()` (**:1559-1578**) and from `HandleEnemyReachedHeart()` (**:1955-1963**). There is
**no** hardcoded `wave==3` route — verified: `waves.json` wave 3 = trolls/ogre/orcs (brute band),
wave 4 = the apex dragon; no ATB/boss flag on wave 3. The "always wave 3" the owner sees is the
**symptom**: by wave 3 the escalating slow brutes (tanks placed front-centre, marching the tree) get
one body across the Heart ring → breach → ATB. Mechanism = breach-to-**Heart** proximity (it does
NOT depend on the hero's position — owner hugging the tree is irrelevant). Per canon
(`atb-flat-vs-overworld`), ATB is the deprecated flat side-path; the village wave must resolve **in
the hub**.

### 5. Resets to wave 1 / no persisted towers
`BeginLoop()` always called `EnterCountdown(_startWave)` with `_startWave = 1`
(**WaveManager.cs:611, 173**); `_currentWaveId` lives only in the (non-DontDestroyOnLoad) instance.
The ATB detour (#4) **swaps scenes** — on return the hub rebuilds fresh, so placed towers and the
wave counter reset to 1. Removing the scene swap (#4) keeps the hub loaded so towers + wave never
reset on that path; a wave-resume seed covers a genuine hub reload.

---

## FIXES (edit-only; flag-gated, revertable)

**`Assets/_Modules/Core/FeatureFlags.cs`** — two flags:
- `WaveAutoStart` (`ff.waveautostart`, **default ON**) — home-hub auto-armed countdown + auto-start.
- `WaveBreachToAtb` (`ff.wavebreachtoatb`, **default OFF**) — legacy breach→ATB handoff (deprecated).

**`Assets/_Modules/Village/Waves/WaveManager.cs`**
- **#3 auto-trigger:** `Start()` now auto-arms via `GuardedKickoff` when `WaveAutoStart` AND the
  scene is the **home hub** (`IsHomeHubScene()` = `HubScenes.IsHub && !IsEnemyOwnedScene` →
  MainCastle_Hall/CastleHub, **excludes** the Village2 enemy stronghold). FTUE (`IsFirstRun`) still
  blocks it. → countdown ticks (fixes **#2**) and the wave auto-starts at zero.
- **#3 override:** unchanged — `ForceBeginNextWave()` from Countdown zeroes the timer + `StartWave`
  (Start Wave button = manual EARLY override).
- **#4 in-hub resolution:** breach-ring detection (`TickActiveWave`) and `HandleEnemyReachedHeart`
  are gated behind `WaveBreachToAtb` (off) → no `SceneRouter.GoBattle` / ATBBattle load. Enemies that
  reach the Heart simply **contact-attack it** (`Enemy.ExecuteContactAttack` → `HeartController :
  IDamageableStructure`); Heart at 0 = the existing `Defeated` path. `EnterAtbBattle` is **kept**
  (ATB system intact for dungeons/sandbox), just no longer called by the wave loop.
- **#5 persistence:** `s_resumeWaveId` static (survives a scene reload within a play session; reset
  at each play start via `[RuntimeInitializeOnLoadMethod]` so a new game / save reset re-seeds).
  `ResolveStartWave()` seeds once from `GameState.BestWave + 1` (cross-session) and is the new
  `BeginLoop` start arg. `CompleteWave()` advances `s_resumeWaveId` and calls `RecordRun(cleared)`
  (persists BestWave + Save).

**`Assets/_Modules/HUD/BattleHudVisibilityManager.cs`** (#1) — `EvaluateMode()` now returns
`Battle` when `IsWaveFighting()` (new helper: wave **Active** phase / wave-started event, **not**
Countdown). Reverses #7 per owner. Countdown stays Town (timer visible); the live wave → 9-zone HUD.

**`Assets/_Modules/Village/HUD/HudContextEvaluator.cs`** (#1 consistency) — `IsWaveActive()`
narrowed from `Countdown||Active` to **Active-only** so arming a countdown no longer flips the Core
HUD model to Battle and hides the Town timer.

---

## CONFIRMATIONS
- **Start Wave → Battle HUD:** `ForceBeginNextWave` (Countdown→Active) → `IsWaveFighting()` true →
  `EvaluateMode` = Battle → Town fades, `BattleHud9Zone` spawns. ✓
- **Countdown restored:** auto-arm puts the loop in Countdown → `PollWaveTimer` surfaces
  `CountdownRemaining` → top-left clock shows ticking `MM:SS`. ✓
- **Auto-trigger + override:** `TickCountdown` auto-`StartWave` at 0; Start Wave = early override. ✓
- **No ATB launch:** breach→ATB gated OFF by default; enemies resolve in-hub against the Heart. ✓
- **Wave advances/persists:** `CompleteWave → EnterCountdown(cleared+1)` in-session;
  `s_resumeWaveId` + `BestWave` seed resume across a reload — no scene swap to reset it. ✓

## BRACE / NUL CHECK (all PASS)
- FeatureFlags.cs 14/14 · WaveManager.cs 273/273 · BattleHudVisibilityManager.cs 54/54 ·
  HudContextEvaluator.cs 14/14 · no NUL bytes.

## OWNER-DECISION FLAGS
1. **Wave cadence (seconds):** unchanged from `waves.json` — wave 1 = 45 s, later = 300 s
   (× DifficultyTuning). Auto-attack now uses these as the real inter-wave timer. Tune if too long
   for the felt loop.
2. **Resume policy:** resumes at `max(BestWave+1, dev _startWave)`; a **Heart defeat** does NOT
   currently reset to wave 1 (continues at the hardest reached). Confirm whether a lost run should
   restart at 1.
3. **Max waves:** schedule is finite (4 authored; smart-composition generates beyond). Phase hits
   `Complete` past the schedule end — confirm desired end-of-run behavior (loop? scale forever?).
4. **Tower persistence across legitimate town↔battle transitions:** OUT OF SCOPE here — removing the
   ATB scene-swap keeps towers loaded for the wave loop. If towers must survive a real scene reload
   (e.g. hub→OuterWorld→hub when the hub unloads), that needs a separate GameState tower
   save/rebuild WO. Flag for PO.

## FILES MODIFIED (for reconcile, by explicit path)
- `Assets/_Modules/Core/FeatureFlags.cs`
- `Assets/_Modules/Village/Waves/WaveManager.cs`
- `Assets/_Modules/HUD/BattleHudVisibilityManager.cs`
- `Assets/_Modules/Village/HUD/HudContextEvaluator.cs`
- `WorkOrders/WORK_ORDER_579_wave_loop_fix.md` (this doc)
