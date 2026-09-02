# WORK ORDER 1308 — Retreat leaves the battle-lock held; the enemy sits in fight

**Status:** READY TO IMPLEMENT
**Silo:** Combat / Quiescence
**Minted:** 2026-09-02 (CLI) from an owner flag during a live felt-test.
**Severity:** P1 — player-facing. Combat input stays suppressed and the HUD cannot return to town.

## Owner report, verbatim

> **"somehow the wolf is still here and sitting in fight"** (F8 seq 4665, `Main_Castle_Overworld`)

## The captured evidence — section 12, do NOT re-theorise past this

seq 4663:
```
[Flow:Quiescence] BATTLE_QUIESCENCE_FAIL (retreat) - 1 invariant(s) NOT restored after the battle:
  - battle-lock: still HELD after the battle ended. Combat input stays suppressed and the HUD cannot
    return to its town context. HOLDER(S): PursuitBattleProbe.Probe, WaveManager.<OnEnable>b__106_0
    (of 3 registered: PursuitBattleProbe.Probe, BattleArena.<Awake>b__84_0, WaveManager.<OnEnable>b__106_0).
```

seq 4664 — **the self-heal ran and one holder survived it**:
```
[Flow:Quiescence] battle-lock STILL HELD after the self-heal (retreat): [WaveManager.<OnEnable>b__106_0]
  (was [PursuitBattleProbe.Probe, WaveManager.<OnEnable>b__106_0]). A holder that survives a full
  session release is either a LIVE chase re-pulsing every aggro tick, or an owner whose probe is
  latched true with no battle behind it. Read the holder name: it is the owner to fix.
```

`PursuitBattleProbe.Probe` released correctly. **`WaveManager.<OnEnable>b__106_0` did not.**

## The seam, read at source

`Assets/_Modules/Village/Waves/WaveManager.cs:658-660`
```csharp
_waveBattleProbe = () => isActiveAndEnabled && Instance == this && _phase == WavePhase.Active;
BattleLock.RegisterProbe(_waveBattleProbe);
```
Registered on `OnEnable`, unregistered only on `OnDestroy` (`:664`, `:734`). **The lock is held for
exactly as long as `_phase == WavePhase.Active`.**

Context: the wolf came from `OverworldEncounterSpawner.SpawnOverworldFamilyPack`
(`OverworldEncounterSpawner.cs:831`, seen in the sibling captures of the same session), i.e. an
OVERWORLD ENCOUNTER, not a village siege.

## ⚠ The distinction this ticket turns on — do not "fix" the probe

**The probe is very likely CORRECT AS WRITTEN.** A live village siege genuinely IS combat, and
retreating from a wolf must NOT cancel a siege. If a wave really is running, the lock SHOULD hold.

So the question is the second branch the trace names: **was `_phase == Active` with no live wave
behind it?** Establish that before changing anything. Candidate shapes, all to be VERIFIED not assumed:
- an overworld encounter drives `_phase` to `Active` and a retreat has no path that clears it;
- a wave ended without the phase transitioning (an early-out, an exception inside the end path, a
  coroutine dropped by host deactivation);
- two WaveManagers exist across scenes and the `Instance == this` claim moved, stranding the phase on
  the loser (note `ClaimInstanceIfCanonical` re-asserts on enable — a real possibility worth ruling in
  or out).

⛔ **Do NOT make the probe return false during a genuine siege.** That would trade a stuck lock for a
combat state the game does not know it is in — strictly worse, and invisible.

## Method

INSTRUMENT FIRST (CLAUDE.md sec.12). The trace already names the holder; what is missing is WHY
`_phase` is `Active`. Log the phase, the wave index, the live-enemy count and the last transition
that set the phase, at the moment the quiescence gate fails. Then fix what the data names.

## Acceptance criteria

1. A retreat from an overworld encounter releases the battle-lock, with input and the town HUD restored.
2. A retreat during a GENUINE active wave still holds the lock — prove both directions.
3. `BATTLE_QUIESCENCE_FAIL (retreat)` no longer fires in a normal retreat, evidenced by a captured run,
   not by absence of a report.
4. The self-heal path still runs and still reports honestly; it is not silenced to make the log clean.

## What NOT to touch

- ⛔ `Assets/_Modules/Core/UI/WorldHold.cs`, `WaveCelebrationManager.cs`, `CombatFeedbackManager.cs`,
  `HeroHitReaction.cs` — a concurrent lane owns the `timeScale` half of quiescence (owner flag 4656,
  the 0.28 leak). Different invariant, different agent. Do not touch the clock here.
- ⛔ Do not weaken or re-threshold `BattleQuiescenceGate`'s reporting. It named the holder by name and
  is the only reason this is diagnosable at all.
- ⛔ Do not add a second lock, probe registry or recovery ladder. `BattleLock` + `BattleSessionEnd` exist.
- ⛔ Do not unregister the wave probe as a workaround — that hides a siege from the lock.

---

# RCA ADDENDUM — 2026-09-02 (read-only agent). The probe is CORRECT; the phase is genuinely latched.

## The structural fact this ticket turns on

`_phase` is `private` with a read-only accessor (`WaveManager.cs:398`, `:501`) — **there is no
external writer.** Nine assignment sites, and **`StartWave` (`:1532`) is the ONLY writer of `Active`.**

**The load-bearing asymmetry:** once Active, the only routine exit is
`TickActiveWave:2666 -> CompleteWave:2900 -> EnterCountdown:2936`. `TickActiveWave` is called from
exactly ONE place — the `switch (_phase)` at `Update:881-889` — which sits **behind two early
returns** (`:791` FTUE, `:852` TownSuspension). **If `Update` cannot reach line 886, `Active` is
permanent.** Nothing else can lower the phase.

## Q2 — can an overworld encounter drive the phase? Directly NO. By CO-RESIDENCY yes.

`OverworldEncounterSpawner.cs` has **zero** references to `WaveManager`. It cannot call `StartWave`.

But the hub runs the full village wave loop anyway:
- `HubScenes.cs:25` — `Main_Castle_Overworld` IS in the canonical hub list
- `WaveManager.cs:707-713` — `IsHomeHubScene()` true for it
- `WaveManager.cs:697-698` — `autoArm = _autoStart || (FeatureFlags.WaveAutoStart && IsHomeHubScene())`
- `FeatureFlags.cs:269` — **`WaveAutoStart` defaults ON**
- `Main_Castle_Overworld.unity` **contains a WaveManager** (script guid `10f1241a6ca92b84388c8f9447ba8bbd`; so do `MainCastle_Hall.unity` and `Village2.unity`)
- `SiegeScheduler.cs:195` gates on `HubScenes.IsHub(...)` and calls `ForceBeginNextWave` (`:254`)

**The encounter scene is also running an auto-armed village wave loop with no player-visible siege
framing.** The encounter does not set the phase; it happens in a scene that already has one.

## Q3 — can a wave end without the phase transitioning? RULED IN, four shapes

**(a) Held-reinforcement wedge — the dropped-async shape, PERSISTENT. Most likely root.**
`TickActiveWave:2658-2664` returns *before* the clear test whenever `_heldSmartReinforcements > 0`.
That counter is owned entirely by the fire-and-forget `DrainSmartReinforcements(...).Forget()`
(`:1965`, `:2021`). Every bail zeroes it except the deliberate hand-off at `:2102` — but because it is
`.Forget()`, **any exception between `:2153` (`_smartSpawner.SpawnWave`) and `:2181` leaves the
counter non-zero forever** and the wave can never clear. This is the same dropped-async shape as
tonight's timeScale leak (commit `c558bc53f`), which is why the ticket flagged it as live.

**(b) `OnDisable` empties the roster but never lowers the phase.** `:732-771` clears `_liveEnemies`
(`:741`) and `_heldSmartReinforcements` (`:744`) and unregisters the probe (`:734`) — **`_phase` is
untouched.** `OnEnable:655-661` re-registers against a stale `Active` with an empty field.

**(c) TownSuspension freezes Active, and the window overlaps the gate.** `Update:852-879` returns
before the switch whenever `TownSuspension.SuspendedFor(this)`; under the default `SuspendAndResume`
(`TownSuspension.cs:110`) `_phase` is **deliberately left Active** (`:871-877`). Timing:
`BattleArena.cs:2699` Resume -> `TownSuspension.cs:103` starts a **3.5 s return grace** during which
`Held` is still true (`:145`), while `BattleQuiescenceGate.cs:122` judges **0.75 s** after resolve
(retreat passes `null` for the reward screen, `BattleArena.cs:2755-2757`, so settle starts at once).
**The gate evaluates ~2.75 s inside a window where the loop is structurally forbidden from leaving
Active.** Caveat: `SuspendedFor` exempts objects in the ACTIVE scene (`TownSuspension.cs:175`), so
this bites only a WaveManager outside it or with an invalid/DDOL scene handle (`:172`).

**(d) The stuck-enemy failsafe is heart-gated.** The cull loop is inside `if (_heart != null)`
(`:2576-2603`). With no `HeartController` in scene, an unreachable wave enemy is never culled.

## Q4 — two WaveManagers: mechanically possible, UNDETERMINED here

`:613-630` states it outright: WaveManager is not DDOL, instances are baked into multiple scenes, and
**"we never destroy the loser (both managers still run their own scene's loop)"**. Three scene files
carry the script. `ClaimInstanceIfCanonical` runs only from `Awake:654` / `OnEnable:657` — it is
**not** hooked to `activeSceneChanged`.

⚠ **The direction this ticket guessed is the harmless one.** A phase stranded on the LOSER is inert,
because the probe's `Instance == this` clause (`:659`) neutralises it. **The dangerous direction is the
reverse:** `OnDestroy:667` nulls `Instance`, after which the next `OnEnable` of any survivor claims it
via the `Instance == null` branch (`:646`) — **including one sitting stale-Active per (b)**.
`ClaimInstanceIfCanonical` re-asserting on enable does not rule this out; it is the mechanism.

## Verdict and the discriminator

Most likely: **a wave latched `Active` with nothing left to clear it**, in the hub's own auto-armed
loop, with the only exit unreachable when the gate fires. Ranked by DURATION, which is what
distinguishes them — only a persistent latch matches *"the wolf is STILL here"* and survives the
self-heal:
1. `_heldSmartReinforcements > 0` wedging `:2658`, or a never-cullable enemy under the heart-gated
   failsafe `:2576`.
2. The TownSuspension early return `:852` across the 3.5 s grace — fits the timing exactly but would
   self-clear shortly after.

Both are inert to `BattleSessionEnd.Release`, which is exactly why the self-heal
(`BattleQuiescenceGate.cs:330-353`) could not shift it while `PursuitBattleProbe` released cleanly.

## What to instrument — one probe, no new architecture

Register a **`QuiescenceProbe` from the Village side** via `BattleQuiescenceGate.Register`
(`BattleQuiescenceGate.cs:139`) named `wave-phase`, so it prints inside the same
`BATTLE_QUIESCENCE_FAIL` block (`:196-208`) **with no Core->Village reference**. It must report:
`_phase`, `_currentWaveId`, `_awaitingPlayerStart`, `_countdownRemaining`; `_liveEnemies.Count` after
a null-prune plus the apex boss; **`_heldSmartReinforcements`** (the single discriminator between
root 1 and 2); the last transition `from -> to` with site, `Time.unscaledTime`, `Time.frameCount`;
the frame of the last `Update` that actually **reached the switch at `:886`** (proves whether `:852`
is eating the tick); `TownSuspension.IsSuspended/.Reason/.ReturnGraceRemaining/.Held/SuspendedFor(this)`;
and `gameObject.scene.name` vs the active scene, `Instance == this`, and the live WaveManager count.

**Reading it:** `held > 0` with `live == 0` => root 1(a). `live > 0` with a null `_heart` => root 1(d).
`held == 0`, `live == 0`, `SuspendedFor == true` with grace ~2.7 s and a stale last-Update frame =>
root 2. A scene mismatch or >1 WaveManager => Q4 is in play too.

⚠ Cleanest capture is a `SetPhase(WavePhase, string site)` helper replacing all nine assignments so
the last transition is recorded. That is a DIAGNOSTIC suggestion, not the sanctioned fix.
