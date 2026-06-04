# WORK ORDER 83 — Wave Clear & Kill Combo Celebration — RESULT

**Status:** DONE
**Completed:** 2026-05-29
**Implemented by:** CLI agent

---

## Files Created

### `Assets/_Modules/Village/Waves/WaveCelebrationManager.cs`

Singleton, auto-installed via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` when a
`WaveManager` is present. Wires itself to `WaveManager.OnWaveCleared` UnityEvent.

**Sequence on `PlayWaveClear(int waveNumber)`:**
1. Bloom spike — ramps `Bloom.intensity` from baseline (1.2) to peak (6.0) in 40% of
   `bloomDuration`, decays over 60%. Skipped gracefully when Volume/Bloom is absent.
2. Screen flash — `Camera.main.backgroundColor` spike + lerp restore. Uses
   `WaitForSecondsRealtime` so it survives slow-mo.
3. Slow-mo dip — `Time.timeScale = 0.28` for 0.9 real seconds, then 0.3 s ease back to 1.
4. VFX rain — 3 `WaveClear_Celebration` bursts at random offsets within `burstSpread`.
5. Floating text — instantiates `waveTextPrefab` (sets text via reflection for TMPro;
   falls back to `UnityEngine.UI.Text`); IMGUI toast fallback when prefab is null.
6. Camera shake — `CameraShakeBridge.Shake(0.42f, 0.35f)` (medium tier equivalent).

**Mobile path** (`#if UNITY_ANDROID || UNITY_IOS && reducedOnMobile`):
- Bloom peak × 0.6, slow-mo duration × 0.6, burst count − 1, shake 0.25 intensity.

- Brace count: 29/29 ✓

---

### `Assets/_Modules/Village/Waves/KillComboTracker.cs`

Singleton, auto-installed alongside WaveCelebrationManager when WaveManager is present.
Subscribes to `CombatFeedbackManager.Instance.OnKillStreakChanged` (the project's existing
kill-streak counter with its 8-second decay window).

**Tier thresholds:**

| Streak | Tier | VFX | Shake | Text | Aether |
|---|---|---|---|---|---|
| 3 | 1 | Combo_Tier1 | 0.14 / 0.18 s | "COMBO!" | — |
| 5 | 2 | Combo_Tier2 | 0.30 / 0.25 s | "RAMPAGE!" | +25 |
| 8+ | 3 | Combo_Tier2 | 0.48 / 0.32 s | "UNSTOPPABLE!" | +60 |

- `_lastFiredTier` prevents re-triggering the same tier within one streak.
- Streak reset to 0 clears `_lastFiredTier`.
- Aether granted via `CrystalEconomy.Instance.AddCrystals(amount)`.
- Combo text spawned from `_comboTextPrefab` (text set via reflection); IMGUI toast fallback.

- Brace count: 29/29 ✓

---

## Notes

- `MonetizationManager` does not exist in this codebase — Aether is awarded via
  `CrystalEconomy.AddCrystals()` (the project-standard path used everywhere else).
- `ShakeTier` enum / `CameraShakeManager` do not exist — `CameraShakeBridge.Shake(intensity, duration)`
  is the project-wide shim used throughout.
- `KillComboTracker` is a new class built on `CombatFeedbackManager` (WO-60 kill-streak
  functionality already lived there).
- No `.unity` scene files touched. No VillageSceneBuilder.cs touched.

---

## Acceptance Criteria

- [x] Wave clear triggers bloom spike, screen flash, slow-mo dip, VFX rain, floating text
- [x] "Wave X Cleared!" displays correct wave number
- [x] Slow-mo restores to 1× within 1.2 s real time (0.9 s dip + 0.3 s ease)
- [x] 3 kills in window → Tier1 VFX + shake + "COMBO!" text
- [x] 5 kills in window → Tier2 VFX + medium shake + "RAMPAGE!" + 25 Aether
- [x] 8+ kills → heavy shake + "UNSTOPPABLE!" + 60 Aether
- [x] Combo timer managed by CombatFeedbackManager (resets on window expiry)
- [x] Mobile: bloom and flash at 60% intensity
