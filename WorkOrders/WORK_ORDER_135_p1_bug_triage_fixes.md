<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 135 — P1 Bug-Triage Fixes (Gameplay Runtime Audit)

**Status: READY TO IMPLEMENT**
**Priority:** P1 cluster (4 fixes) + P2 cleanup (4, optional same-pass)
**Created:** 2026-05-30
**Source:** `docs/bug-triage.md` (read-only runtime audit; fix diffs drafted there, none applied)
**Lane routing:** UI captured this WO (spec only — has not touched `Assets/` or any `.cs`). **CLI implements all code in the build lane, compile-gates, and verifies.** Single build-lane writer owns source.

> This WO is a spec, not source. The code blocks below are the audit's *suggested* diffs — treat them as pointers to the fix site, not final code. CLI applies and brace-gates.

---

## Scope

Four P1s from the runtime audit. P1 here = silent economy loss, or a counter/collection leak that degrades or breaks the run over time. Three of four are pure code; none require a scene rebake.

**File clustering (land per-file in one pass — both files are bottleneck-ish):**
- **`CrystalMine.cs`** holds P1-1 **and** P1-3 (and a P2) — do all CrystalMine edits together.
- **`WaveManager.cs`** holds P1-4 — coordinate with any other in-flight WaveManager WO (it's a serialization bottleneck per CLAUDE.md §9).
- **`VFXManager.cs`** holds P1-2 (and a related P2).

---

## P1-1 · CrystalMine auto-upgrades on prompt (silent coin spend)

- **File:** `Assets/_Modules/Village/Buildings/CrystalMine.cs:331-352`
- **Symptom:** `OpenUpgradeUI()` (F key) falls into `ShowSimpleUpgradePrompt()` when no UIDocument is wired; that method *immediately* calls `TryUpgrade()`. So one F press both "opens the confirm prompt" and spends 200/400 coins in the same frame — no confirmation. Players lose coins on an accidental walk-by F press.
- **Suggested fix (from triage):** show the prompt, let the *next* F press confirm. Add an `_awaitingSimpleConfirm` bool; in `Update()`, if awaiting + `GetKeyDown(F)` → `TryUpgrade(); _awaitingSimpleConfirm=false; HidePrompt();`. Minimal alternative: delete the `TryUpgrade()` call so the prompt is informational and the existing `_isInRange && F` path drives the upgrade.
- **Acceptance:** Pressing F opens a "Confirm Upgrade" prompt and spends **nothing**; a second F press performs the spend; walking away cancels with no charge.

## P1-2 · VFXManager active-effect counters cross-decrement and drift

- **File:** `Assets/_Modules/Village/Vfx/VFXManager.cs:453-469` (increment at `:745`)
- **Symptom:** `PlayOneshot` bumps `_activeOneshots`; `PlayLoop`/`ProceduralLoopFallback` bump `_activeLoops`. But `ReturnToPool` can't tell which bucket an object came from — it decrements oneshots first, loops only if oneshots are 0. With mixed traffic a returning loop decrements the oneshot counter (or vice-versa); the mismatched counter ratchets up and never recovers. Once it hits `_maxActiveOneshots` (40) / `_maxActiveLoops` (20), **all new VFX of that class are silently skipped** — combat goes quiet mid-run.
- **Suggested fix (from triage):** tag pooled objects with their kind (e.g. a `HashSet<GameObject> _loopObjects` added at loop acquire/create) and decrement the exact counter in `ReturnToPool` via `bool wasLoop = _loopObjects.Remove(go)`.
- **Secondary (P2, same area):** `ProceduralLoopFallback` enqueues a throwaway `[ProceduralLoop_*]` host into `_pools[type]` that later gets `Acquire`d as if a catalog instance — poisons the pool. Prefer `Destroy(go)` for non-catalog hosts.
- **Acceptance:** Sustained mixed oneshot+loop VFX over a long wave never starves; counters return to ~0 when the field clears; no pool poisoning from procedural fallback hosts.

## P1-3 · CrystalMine WaveManager subscription double-fires / targets wrong instance

- **File:** `Assets/_Modules/Village/Buildings/CrystalMine.cs:498-514`
- **Symptom:** `SubscribeToWave()` calls `UnsubscribeFromWave()` (detaches from the *current* `_wave`) then `ResolveWave()` *overwrites* `_wave` with a fresh `Find` result. After a scene reload the reference can change, so a stale manager's listener is never removed; `OnEnable`+upgrade paths can net a duplicated `+1 crystal` per wave or a leaked listener on a destroyed manager.
- **Suggested fix (from triage):** resolve first, then idempotent re-attach — `ResolveWave(); if(_wave==null) return; _wave.OnWaveCleared.RemoveListener(OnWaveCleared); _wave.OnWaveCleared.AddListener(OnWaveCleared);`
- **Acceptance:** Exactly one `OnWaveCleared` fire per cleared wave per mine across scene reloads and post-upgrade; no listeners left on destroyed managers.

## P1-4 · WaveManager stuck-tracking dictionaries leak within a wave

- **File:** `Assets/_Modules/Village/Waves/WaveManager.cs:721-752`, handler at `:1010`
- **Symptom:** `_enemyBestSqr` / `_enemyStuckTime` (keyed by `Enemy`) are cleared per wave and pruned only when an enemy is *culled for being stuck*. An enemy that dies **normally** (the common case) is removed from `_liveEnemies` but its dict entries linger (holding a destroyed `Enemy` key) until the next `EnterCountdown` — steady allocation creep + dead refs kept alive on long/large waves.
- **Suggested fix (from triage):** in `HandleEnemyDied`, also `_enemyBestSqr.Remove(enemy); _enemyStuckTime.Remove(enemy);` alongside the existing `_liveEnemies.Remove(enemy)` and event unhooks.
- **Acceptance:** After a full wave of normal deaths, both tracking dictionaries return to empty (no destroyed-`Enemy` keys retained); no per-wave growth.

---

## P2 cleanup (optional — fold in only if touching the same files)

| P2 | File:line | Fix |
|----|-----------|-----|
| Hero cast-VFX lifetime uses wrong field | `HeroAbilities.cs:419` | `duration + startLifetime.constantMax` (not `startLifetimeMultiplier`) — match `VFXManager.DetectDuration` |
| Enemy.ApplyWaveScaling dead branch | `Enemy.cs:340-347` | both arms identical → collapse to `if(_agent!=null) _agent.speed=_moveSpeed;` |
| CrystalMine deprecated API | `CrystalMine.cs:521` | `FindObjectOfType` → `FindAnyObjectByType<HeroLocomotion>()` (do with the CrystalMine P1s) |
| Tower.Upgrade divergent index guard | `Tower.cs:530-535` | reuse `CurrentUpgrade()` instead of re-indexing inline |

---

## Checked-and-OK (per the audit — do NOT "fix")

- `Enemy.cs` post-truncation is clean: braces balanced, telegraph coroutine / death path / reflection Glimmer bridge intact.
- No live `VFXManager.Instance?.Play(` callers — all use the static `VFXManager.Play(...)`; the one `Instance?.Play` mention is a doc comment in `CastleDoorController.cs`.
- No singleton-dedup-destroys-shared-host instances in scope (`HeroControlEnsurer`, `AttackTimingBonus`, `HeroProgression` all correct).
- `HeartController.SetHp` guard, `WallSegment` clamp, `GameState` (pure data) all sound.

## What NOT to touch

- Do not hand-edit `Village.unity` (CLAUDE.md §3) — none of these fixes need a scene edit or rebake.
- Do not refactor the VFX static `Play` call sites — they're correct.
- Keep all `CrystalMine.cs` edits (P1-1, P1-3, P2) in one coordinated pass; same for `WaveManager.cs`.

## Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passes on every `.cs` file edited (`CrystalMine`, `VFXManager`, `WaveManager`, + any P2 files)
- [ ] No `.unity` scene files hand-edited
- [ ] Null-conditional (`?.`) on cross-module `CoreServices` calls
- [ ] Each P1 acceptance criterion verified in a playtest build
- [ ] `WORK_ORDER_135_p1_bug_triage_fixes.RESULT.md` written when complete
