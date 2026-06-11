// =============================================================================
// WaveHudBridge — wires WaveManager events to IVillageHud via CoreServices.
// -----------------------------------------------------------------------------
// WO-41 refactor: reflection removed. Previously held a plain UnityEngine.Object
// _hud field and resolved VillageHudController.SetWave(int, float) by reflection
// at runtime. Now uses CoreServices.Hud (IVillageHud) directly — no asmdef
// reference to DeNelle.HUD needed because IVillageHud lives in DeNelle.Core.
//
// The _hud serialised field is REMOVED. VillageSceneBuilder no longer needs to
// assign it (Step 6 removes that SetObjectField call). The _wave field is kept
// so the scene builder can still wire the WaveManager reference.
// =============================================================================

using System.Reflection;
using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Forwards WaveManager countdown / wave-start events to
    /// <see cref="DeNelle.Core.HUD.IVillageHud"/> via <see cref="CoreServices.Hud"/>.
    /// Also pushes a live "X / Y enemies" readout (live count + the wave's peak
    /// live count as the total) so the player always knows how close the wave is
    /// to being cleared — that setter is NOT on IVillageHud, so it is resolved on
    /// the concrete VillageHudController by reflection (same seam as the other
    /// HUD bridges: DeNelle.Village cannot reference DeNelle.HUD).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveHudBridge : MonoBehaviour
    {
        [Tooltip("The scene's WaveManager. Bound by the village scene builder.")]
        [SerializeField] private WaveManager _wave;

        // ── Enemy-count readout (reflection, not on IVillageHud) ──────────────
        private object _hud;                 // VillageHudController held as object (via CoreServices.Hud)
        private MethodInfo _setEnemyCount;   // SetEnemyCount(int live, int total)
        private readonly object[] _countArgs = new object[2];
        private bool _hudResolveGaveUp;
        private int _hudResolveAttempts;
        private const int MaxHudResolveAttempts = 600;
        // The wave's peak simultaneous live count — used as the "total" denominator
        // so the readout reads as progress toward clearing the wave. Reset each wave.
        private int _waveLivePeak;
        private bool _waveActive;

        // Subscribe in OnEnable, not Start — WaveManager.BeginLoop is async; if
        // we wait for Start the first OnCountdownTick can fire before our
        // listener attaches, leaving the HUD stuck at "Wave 1 / 0".
        private void OnEnable()
        {
            if (_wave == null)
            {
                Debug.LogWarning("[WaveHudBridge] No WaveManager assigned — wave HUD will not update.");
                return;
            }

            _wave.OnCountdownTick.AddListener(OnCountdown);
            _wave.OnWaveStarted.AddListener(OnWaveStart);
            _wave.OnWaveCleared.AddListener(OnWaveCleared);
            // Push an initial value so the timer doesn't stick at the UXML default.
            Forward(_wave.CurrentWaveId, 0f);
            ResolveHud();
        }

        private void OnDisable()
        {
            if (_wave == null) return;
            _wave.OnCountdownTick.RemoveListener(OnCountdown);
            _wave.OnWaveStarted.RemoveListener(OnWaveStart);
            _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
        }

        private void OnCountdown(float secondsRemaining)
        {
            if (_wave == null) return;
            // During countdown the HUD shows next-wave number + countdown timer.
            Forward(_wave.CurrentWaveId + 1, secondsRemaining);
        }

        private void OnWaveStart(int waveId)
        {
            // Wave is live now — clear the countdown timer.
            Forward(waveId, 0f);
            _waveActive = true;
            _waveLivePeak = 0;
        }

        private void OnWaveCleared(int waveId)
        {
            // Wave done — hide the enemy-count line until the next wave spawns.
            _waveActive = false;
            _waveLivePeak = 0;
            PushEnemyCount(0, 0);
        }

        // Push the live "X / Y enemies" readout each frame while a wave is active.
        // total = the wave's peak simultaneous live count (so the readout reads as
        // progress toward clearing). Cheap: one cached-MethodInfo invoke/frame.
        //
        // WO-403 — the count is only pushed while a wave is ACTIVE (≤ a few seconds of
        // pushes per wave), and HUD resolution no longer scans the scene (see ResolveHud).
        // The previous per-frame FindObjectsByType<MonoBehaviour> ran forever whenever
        // _hud went "fake-null" after a HUD rebuild — a leading OuterWorld-leak suspect.
        private void Update()
        {
            if (!_waveActive || _wave == null) return;
            if (_hud == null || _setEnemyCount == null) ResolveHud();

            int live = _wave.LiveEnemies != null ? _wave.LiveEnemies.Count : 0;
            if (live > _waveLivePeak) _waveLivePeak = live;
            PushEnemyCount(live, _waveLivePeak);
        }

        private void PushEnemyCount(int live, int total)
        {
            if (_setEnemyCount == null || _hud == null) return;
            _countArgs[0] = live;
            _countArgs[1] = total;
            _setEnemyCount.Invoke(_hud, _countArgs);
        }

        // Resolve the concrete VillageHudController + its SetEnemyCount(int,int) by
        // reflection (the setter is not on IVillageHud).
        //
        // WO-403 — the HUD is resolved via CoreServices.Hud (an O(1) registry lookup of
        // the registered IVillageHud, which IS the VillageHudController), NOT a
        // FindObjectsByType<MonoBehaviour> whole-scene scan. The old scan ran every frame
        // (a leading OuterWorld-leak suspect); this lookup is free and never scans. The
        // attempt cap remains a safety net for the brief window before the HUD registers.
        private void ResolveHud()
        {
            if (_hudResolveGaveUp) return;
            if (_hud != null && _setEnemyCount != null) return;

            var hud = CoreServices.Hud as object;
            if (hud == null)
            {
                if (++_hudResolveAttempts > MaxHudResolveAttempts)
                {
                    _hudResolveGaveUp = true;
                    Debug.LogWarning("[WaveHudBridge] VillageHudController not registered — enemy-count readout disabled.");
                }
                return;
            }

            _hud = hud;
            if (_setEnemyCount == null)
            {
                _setEnemyCount = _hud.GetType().GetMethod("SetEnemyCount",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int), typeof(int) }, null);
            }
        }

        private void Forward(int waveNumber, float countdown)
        {
            CoreServices.Hud?.SetWave(waveNumber);
            CoreServices.Hud?.SetCountdown(countdown);
        }
    }
}
