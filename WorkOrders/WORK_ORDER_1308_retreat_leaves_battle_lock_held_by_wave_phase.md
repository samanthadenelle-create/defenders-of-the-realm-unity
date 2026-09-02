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
