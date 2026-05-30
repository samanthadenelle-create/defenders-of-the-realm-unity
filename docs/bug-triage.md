# Gameplay Runtime Bug Triage

Read-only audit of the gameplay runtime modules (Enemies, Hero, Buildings, Core/State, Vfx).
Findings are ready-to-apply fix diffs for the build lane — **none applied here**.

Scope note: `Enemy.cs` (recently truncated to fix corruption) is **coherent** — braces
balanced, all members intact, no dangling fragments. No `VFXManager.Instance?.Play(` call
sites exist in live code (only a doc comment in `CastleDoorController.cs`); all `Play` calls
use the static `VFXManager.Play(...)` correctly.

## Severity summary

| Sev | File:line | Description |
|-----|-----------|-------------|
| P1 | CrystalMine.cs:347-351 | `ShowSimpleUpgradePrompt` auto-calls `TryUpgrade()` — single F press silently spends coins, no confirm |
| P1 | VFXManager.cs:453-469 / 745 | Active-FX counters cross-decrement (oneshot vs loop) → counter drift permanently blocks new VFX |
| P1 | CrystalMine.cs:498-503 | `SubscribeToWave` re-resolves a *different* WaveManager but unsubscribes from the old `_wave` only if same ref — duplicate `OnWaveCleared` fire / wrong-instance leak |
| P1 | WaveManager.cs:734-748 | Stuck-cull dictionaries (`_enemyBestSqr`/`_enemyStuckTime`) never pruned on normal death → grow unbounded within a wave |
| P2 | HeroAbilities.cs:419 | Cast VFX lifetime uses `startLifetimeMultiplier` (a scalar), not the actual max lifetime — prefab particles can be destroyed mid-play |
| P2 | Enemy.cs:340-352 | `ApplyWaveScaling` speed branch is a dead if/else (both arms identical) — minor, harmless |
| P2 | CrystalMine.cs:521 | `ResolveHero` uses deprecated `FindObjectOfType` (perf; consistency) |
| P2 | Tower.cs:530 | Re-indexes `_data.upgrades` after `Upgrade()` without the same null guard `CurrentUpgrade()` uses — safe today, fragile |

---

## P1 — CrystalMine auto-upgrades on prompt (silent coin spend)

**File:** `Assets/_Modules/Village/Buildings/CrystalMine.cs:331-352`

`OpenUpgradeUI()` (triggered by the F key) falls into `ShowSimpleUpgradePrompt()` when no
UIDocument is wired. That method *immediately* calls `TryUpgrade()` — so pressing F once both
"opens the confirm prompt" **and** spends the player's coins in the same frame, with no
confirmation step. The comment even says "another F press confirms", but the code never waits
for it.

Why it matters: players lose 200/400 coins on an accidental walk-by F press. Real economy bug.

```csharp
// BEFORE (line 341-351)
int cost = _currentLevel == 1 ? _costL1toL2 : _costL2toL3;
_promptGo = BuildBubble($"〔 F 〕 Upgrade — {cost} Coins", ...);
// In simple mode: another F press confirms the upgrade.
_uiOpen = false; // allow F to reach Update again for confirm
TryUpgrade();
ShowPrompt(); // refresh prompt text
```

```csharp
// AFTER — show the prompt, let the NEXT F press confirm (don't spend now)
int cost = _currentLevel == 1 ? _costL1toL2 : _costL2toL3;
_promptGo = BuildBubble($"〔 F 〕 Confirm Upgrade — {cost} Coins", ...);
// In simple mode: leave the UI "open" so the next F press in Update confirms.
_awaitingSimpleConfirm = true;   // new bool field
```

…and in `Update()` add: if `_awaitingSimpleConfirm` and `Input.GetKeyDown(F)` → `TryUpgrade();
_awaitingSimpleConfirm = false; HidePrompt();`. Minimal alternative if you want to keep it
one-shot: just delete the `TryUpgrade()` call so the prompt is informational only and the
existing `_isInRange && F` path in `Update` drives the actual upgrade.

---

## P1 — VFXManager active-effect counters cross-decrement and drift

**File:** `Assets/_Modules/Village/Vfx/VFXManager.cs:453-469` (and increment at `:745`)

`PlayOneshot` increments `_activeOneshots`; `PlayLoop`/`ProceduralLoopFallback` increment
`_activeLoops`. But `ReturnToPool` can't tell which bucket a returning object came from, so it
decrements `_activeOneshots` first and only touches `_activeLoops` if oneshots are already 0:

```csharp
// BEFORE (line 466-468)
if (_activeOneshots > 0) _activeOneshots--;
else if (_activeLoops > 0) _activeLoops--;
```

With mixed oneshot+loop traffic, a returning **loop** decrements the **oneshot** counter (or
vice-versa). The mismatched counter ratchets upward and never recovers, so once it reaches
`_maxActiveOneshots` (40) or `_maxActiveLoops` (20) **all new VFX of that class are silently
skipped** (`if (_activeOneshots >= _maxActiveOneshots) return;`). Combat goes quiet mid-run.

Fix: track the kind explicitly so the return decrements the right counter.

```csharp
// AFTER — tag pooled objects with their kind, decrement that exact counter.
// (Store kind in a Dictionary<GameObject,bool> isLoop set at Acquire/loop-create,
//  OR add a tiny marker MonoBehaviour. Minimal version using a HashSet:)

private readonly HashSet<GameObject> _loopObjects = new HashSet<GameObject>();
// in PlayLoop after go acquired:           _loopObjects.Add(go);
// in ProceduralLoopFallback before return: _loopObjects.Add(host);

public void ReturnToPool(GameObject go, VFXType type)
{
    if (go == null) return;
    bool wasLoop = _loopObjects.Remove(go);
    ...
    if (wasLoop) { if (_activeLoops > 0) _activeLoops--; }
    else         { if (_activeOneshots > 0) _activeOneshots--; }
}
```

Secondary (P2, same area): `ProceduralLoopFallback` builds a throwaway `[ProceduralLoop_*]`
GameObject that gets enqueued into `_pools[type]` on return and later `Acquire`d as if it were
a catalog instance — slowly poisons the pool with empty hosts. Prefer `Destroy(go)` for hosts
that did not originate from a catalog prefab.

---

## P1 — CrystalMine WaveManager subscription can double-fire / target wrong instance

**File:** `Assets/_Modules/Village/Buildings/CrystalMine.cs:498-514`

```csharp
private void SubscribeToWave()
{
    UnsubscribeFromWave();      // removes listener from the CURRENT _wave
    ResolveWave();              // then OVERWRITES _wave with a fresh Find result
    if (_wave != null) _wave.OnWaveCleared.AddListener(OnWaveCleared);
}
```

`UnsubscribeFromWave()` runs against the old `_wave`, but `ResolveWave()` immediately reassigns
`_wave` (potentially a different WaveManager after a scene reload). If `TryUpgrade()` calls
`SubscribeToWave()` again (line 194) while `_wave` already points at the live manager, the
unsubscribe+resubscribe nets out fine — but `OnEnable` → `SubscribeToWave` and the upgrade path
both run, and `ResolveWave` between them means a stale manager's listener is never removed if the
reference changed. Net effect: a duplicated `+1 crystal` per wave, or a leaked listener on a
destroyed manager.

```csharp
// AFTER — unsubscribe AFTER re-resolving so we always detach from the manager
// we are actually about to (re)attach to, and guard against double-add.
private void SubscribeToWave()
{
    ResolveWave();
    if (_wave == null) return;
    _wave.OnWaveCleared.RemoveListener(OnWaveCleared); // idempotent
    _wave.OnWaveCleared.AddListener(OnWaveCleared);
}
```

---

## P1 — WaveManager stuck-tracking dictionaries leak within a wave

**File:** `Assets/_Modules/Village/Waves/WaveManager.cs:721-752`, handler at `:1010`

`_enemyBestSqr` / `_enemyStuckTime` are keyed by `Enemy`. They are cleared per wave in
`EnterCountdown` and pruned only when an enemy is *culled for being stuck* (`:746-747`). An enemy
that dies **normally** (the common case) is removed from `_liveEnemies` via `HandleEnemyDied` but
its dictionary entries are never removed — they linger (holding a destroyed `Enemy` key) until the
next `EnterCountdown`. On a long/large wave this is a steady allocation creep and keeps dead
references alive.

```csharp
// AFTER — in HandleEnemyDied (WaveManager.cs:1010), also drop tracking entries:
private void HandleEnemyDied(Enemy enemy)
{
    if (enemy != null)
    {
        enemy.Died -= HandleEnemyDied;
        enemy.ReachedHeart -= HandleEnemyReachedHeart;
        _enemyBestSqr.Remove(enemy);
        _enemyStuckTime.Remove(enemy);
    }
    _liveEnemies.Remove(enemy);
}
```

---

## P2 — HeroAbilities cast-VFX lifetime uses the wrong field

**File:** `Assets/_Modules/Village/Hero/HeroAbilities.cs:419`

```csharp
// BEFORE
float life = ps.main.duration + ps.main.startLifetimeMultiplier;
Destroy(ps.gameObject, life + 0.5f);
```

`startLifetimeMultiplier` is the *multiplier* on a min/max curve, not the lifetime in seconds.
For a curve authored as constant 2.0s with multiplier 1, `life` becomes `duration + 1` instead of
`duration + 2`, so a longer prefab effect can be destroyed before its particles finish.

```csharp
// AFTER
float life = ps.main.duration + ps.main.startLifetime.constantMax;
Destroy(ps.gameObject, life + 0.5f);
```

(`VFXManager.DetectDuration` already does this correctly with `startLifetime.constantMax` — match it.)

---

## P2 — Enemy.ApplyWaveScaling dead branch

**File:** `Assets/_Modules/Village/Enemies/Enemy.cs:340-347`

```csharp
// BEFORE — both arms set _agent.speed = _moveSpeed identically
if (_agent != null && _agent.isOnNavMesh)
    _agent.speed = _moveSpeed;
else if (_agent != null)
    _agent.speed = _moveSpeed;
```

Harmless but clearly a leftover. Collapse:

```csharp
// AFTER
if (_agent != null) _agent.speed = _moveSpeed;
```

---

## P2 — CrystalMine deprecated FindObjectOfType

**File:** `Assets/_Modules/Village/Buildings/CrystalMine.cs:521`

`FindObjectOfType<HeroLocomotion>()` is the obsolete API; the rest of the codebase uses
`FindAnyObjectByType` / `FindObjectsByType`. Swap for consistency and to avoid the Unity 6
deprecation warning:

```csharp
// AFTER
var loco = FindAnyObjectByType<HeroLocomotion>();
```

---

## P2 — Tower.Upgrade re-indexes upgrades without the shared guard

**File:** `Assets/_Modules/Village/Buildings/Tower.cs:530-535`

`Upgrade()` re-checks `_currentLevel - 1` bounds inline instead of reusing `CurrentUpgrade()`.
Safe today (the bounds check is correct), but two divergent index-guards on the same array invite
drift. Prefer:

```csharp
// AFTER
var u = CurrentUpgrade();
if (u != null && u.ability != SpecialAbility.None)
    ActivateSpecialAbility(u.ability);
```

---

## Things checked and found OK

- `Enemy.cs` truncation is clean; the telegraph coroutine, death path, and reflection Glimmer
  bridge are all intact and brace-balanced.
- No live `VFXManager.Instance?.Play(` callers (the flagged antipattern) — all use static `Play`.
- The `if (Instance != null && Instance != this) Destroy(gameObject)` dedup pattern appears widely,
  but every hit is on a **dedicated singleton GameObject** (managers, bootstraps), not a shared
  host. `HeroControlEnsurer` (on its own bootstrapped GO) and `AttackTimingBonus` (uses
  `Destroy(this)`) are both correct. `HeroProgression` already special-cases the shared-hero case.
  **No singleton-dedup-destroys-host instances found in scope.**
- `HeartController.SetHp` no-op guard, `WallSegment` clamp, and `GameState` (pure data) are sound.

_Capped at the highest-value findings; the list is complete for the audited scope, not truncated._
