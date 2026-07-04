// =============================================================================
// TownHudBridge (WO-339) — feeds the town HUD its Village-side data each ~0.5s.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-339's town HUD (VillageHudController, DeNelle.HUD) exposes a set of plain
// setters that need live Village data:
//     SetWaveProgress(int current, int total)
//     SetTownMetrics(float heartPct01, int towersBuilt, int towersMax, int pop)
//     SetMinimapPoi(string kind, float worldX, float worldZ)
//     ClearMinimapPois()
//     SetLookoutStatus(int status)
// None of those live on IVillageHud, and DeNelle.Village cannot reference
// DeNelle.HUD (CLAUDE.md §5: HUD → Core only). So — exactly like WaveHudBridge /
// PartyHudBridge / ComboHudBridge — we resolve the concrete VillageHudController
// via CoreServices.Hud and invoke the setters by CACHED reflection MethodInfo.
//
// Self-bootstrapping (RuntimeInitializeOnLoadMethod + a DDOL host) — no scene edit
// or rebake needed (PartyHudBridge pattern). BEST-EFFORT + light cadence (every
// ~0.5s, not per-frame): feeds whatever Village data is readily available and
// placeholders the rest. Every read + every invoke is null-/try-guarded so a
// missing system never throws (WebGL halts on an uncaught throw).
//
// WHAT IT FEEDS (and what is a PLACEHOLDER):
//   • Mini-map POIs (rebuilt each tick: ClearMinimapPois then SetMinimapPoi):
//       - the Heart at the origin            kind "heart"
//       - each WaveSpawnPoint (enemy gate)   kind "gate"
//       - each MineNode (resource node)      kind "tree"/"resource" by MineResource
//     Building POIs (forge/market) are best-effort by name match — only emitted if
//     such a GameObject is easily found, else simply omitted (NOT a hard placeholder).
//   • Town metrics:
//       - heartPct01  = HeartController.Hp / 100  (Hp is authored 0..100); 1.0 if no Heart
//       - towersBuilt = live Tower component count
//       - towersMax   = TowersMaxConstant (sensible cap; no catalog max is exposed here)
//       - pop         = live StoryCompanion count (party NPCs). PLACEHOLDER 0 if none.
//   • Wave progress: (currentWaveId, total). Total waves is NOT exposed by
//     WaveManager's public API, so total is passed as 0 → the HUD shows "Wave N"
//     (the WO-339 contract for an unknown total). current = WaveManager.CurrentWaveId.
//   • Lookout status: PLACEHOLDER — derived from whether a wave is live (0 calm /
//     1 alert) since no dedicated lookout system exists yet.
// =============================================================================

using System.Reflection;
using DeNelle.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Pushes Village-side data (wave progress, town metrics, mini-map POIs,
    /// lookout status) into the WO-339 town HUD by cached reflection. Best-effort,
    /// ~0.5s cadence, fully guarded. See file header for exactly what is fed vs
    /// placeholdered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TownHudBridge : MonoBehaviour
    {
        private const float RefreshInterval = 0.5f;
        private const int TowersMaxConstant = 12;   // sensible cap (no catalog max exposed here)
        // WO-403 — the town readouts are only meaningful in the village; gate the feed to
        // that scene so this DDOL bridge does ZERO work out in the overworld (the leak window).
        private const string VillageSceneName = "Village2";

        // WO-361 — passive-XP badge feed. There is NO real passive-XP accrual on towers
        // yet (Tower.cs has no XP concept; the only XP path is wave-clear via WaveXpBridge
        // → HeroProgression.AddXp). So the badge shows the INTENDED idle rate: each built
        // tower is treated as earning this many XP per minute. When real idle accrual is
        // wired, swap this constant for the live per-tower rate (and start crediting it).
        private const int IdleXpPerTowerPerMin = 5;

        // Cached HUD + its setters (resolved once the HUD is registered).
        private object _hud;
        private MethodInfo _setWaveProgress;   // (int current, int total)
        private MethodInfo _setTownMetrics;    // (float heartPct01, int towersBuilt, int towersMax, int pop)
        private MethodInfo _setLookoutStatus;  // (int status)
        private MethodInfo _setPassiveXp;      // (int xpPerMin, int towerCount)  — WO-361
        private bool _resolveWarned;
        // WO-380 / WO-403: the minimap POI setters (_setMinimapPoi / _clearMinimapPois)
        // and their _poiArgs buffer are gone — the minimap is cut from the HUD.

        private readonly object[] _waveArgs      = new object[2];
        private readonly object[] _metricsArgs   = new object[4];
        private readonly object[] _lookoutArgs   = new object[1];
        private readonly object[] _passiveXpArgs = new object[2];

        private float _timer;

        // Cached Village references (re-resolved if they go null across scenes).
        private WaveManager _wave;
        private HeartController _heart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("TownHudBridge");
            go.AddComponent<TownHudBridge>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;

            // WO-403 — only feed the town HUD while the village scene is active. Out in
            // OuterWorld (or a dungeon / ATB scene) the town readouts are hidden anyway,
            // so the bridge does no work there — this is the DDOL bridge's leak window.
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name != VillageSceneName) return;

            try
            {
                if (!ResolveHud()) return;
                PushWaveProgress();
                PushTownMetrics();
                PushPassiveXp();
                // WO-380: the minimap is cut from the HUD (VillageHudController never
                // builds it), so the POI feed — which scanned WaveSpawnPoint + MineNode
                // across the whole scene every tick — is removed. (PushMinimapPois gone.)
                PushLookoutStatus();
            }
            catch (System.Exception e)
            {
                // WebGL halts on an uncaught throw — swallow & warn so the HUD just
                // shows stale values for a tick instead of killing the player.
                Debug.LogWarning("[TownHudBridge] feed tick failed: " + e.Message);
            }
        }

        // ── Feeders (each independently guarded) ─────────────────────────────

        private void PushWaveProgress()
        {
            if (_setWaveProgress == null) return;
            if (_wave == null) _wave = FindAnyObjectByType<WaveManager>();
            int current = _wave != null ? _wave.CurrentWaveId : 0;
            // WaveManager exposes no public total-wave count → pass 0 (HUD shows "Wave N").
            _waveArgs[0] = current;
            _waveArgs[1] = 0;
            _setWaveProgress.Invoke(_hud, _waveArgs);
        }

        private void PushTownMetrics()
        {
            if (_setTownMetrics == null) return;

            if (_heart == null) _heart = FindAnyObjectByType<HeartController>();
            float heartPct01 = _heart != null ? Mathf.Clamp01(_heart.Hp / 100f) : 1f;

            // WO-403 — live tower count + party population read from the O(1) registries
            // (Tower.ActiveCount / StoryCompanion.Active) instead of FindObjectsByType,
            // which scanned the whole scene every 0.5s (a suspect in the OuterWorld leak).
            int towersBuilt = Tower.ActiveCount;
            int pop = StoryCompanion.Active.Count;

            _metricsArgs[0] = heartPct01;
            _metricsArgs[1] = towersBuilt;
            _metricsArgs[2] = TowersMaxConstant;
            _metricsArgs[3] = pop;
            _setTownMetrics.Invoke(_hud, _metricsArgs);
        }

        /// <summary>
        /// WO-361 — feeds the passive-XP badge: towerCount × per-tower idle XP/min.
        /// NOTE: this is a DISPLAY of the intended rate — towers do not actually accrue
        /// passive XP yet (see <see cref="IdleXpPerTowerPerMin"/>). Reuses the same live
        /// Tower count as the metrics strip.
        /// </summary>
        private void PushPassiveXp()
        {
            if (_setPassiveXp == null) return;
            // WO-403 — registry read (no scene scan); same live count as the metrics strip.
            int towerCount = Tower.ActiveCount;
            int xpPerMin = towerCount * IdleXpPerTowerPerMin;
            _passiveXpArgs[0] = xpPerMin;
            _passiveXpArgs[1] = towerCount;
            _setPassiveXp.Invoke(_hud, _passiveXpArgs);
        }

        // WO-380 / WO-403: the minimap is cut from the HUD, so PushMinimapPois /
        // EmitPoi / PoiKindForResource (which scanned WaveSpawnPoint + MineNode across
        // the whole scene every tick) have been removed. The _setMinimapPoi /
        // _clearMinimapPois reflection handles are no longer resolved (see ResolveHud).

        private void PushLookoutStatus()
        {
            if (_setLookoutStatus == null) return;
            // Placeholder: no dedicated lookout system → 1 (alert) while a wave is
            // live, 0 (calm) otherwise.
            if (_wave == null) _wave = FindAnyObjectByType<WaveManager>();
            int status = 0;
            if (_wave != null)
            {
                var phase = _wave.Phase;
                if (phase == WavePhase.Countdown || phase == WavePhase.Active) status = 1;
            }
            _lookoutArgs[0] = status;
            _setLookoutStatus.Invoke(_hud, _lookoutArgs);
        }

        // ── HUD resolution (cache MethodInfo once the HUD registers) ──────────

        private bool ResolveHud()
        {
            var hud = CoreServices.Hud as object;
            if (hud == null) { _hud = null; return false; }

            if (!ReferenceEquals(hud, _hud))
            {
                _hud = hud;
                var t = hud.GetType();
                _setWaveProgress = t.GetMethod("SetWaveProgress",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int), typeof(int) }, null);
                _setTownMetrics = t.GetMethod("SetTownMetrics",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(float), typeof(int), typeof(int), typeof(int) }, null);
                _setLookoutStatus = t.GetMethod("SetLookoutStatus",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int) }, null);
                _setPassiveXp = t.GetMethod("SetPassiveXp",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int), typeof(int) }, null);

                if (!_resolveWarned &&
                    _setWaveProgress == null && _setTownMetrics == null &&
                    _setLookoutStatus == null)
                {
                    _resolveWarned = true;
                    Debug.LogWarning("[TownHudBridge] HUD exposes none of the town setters — " +
                                     "town HUD will not update (WO-339 not present in this HUD).");
                }
            }

            // Any one resolved setter is enough to do useful work.
            return _setWaveProgress != null || _setTownMetrics != null ||
                   _setLookoutStatus != null || _setPassiveXp != null;
        }
    }
}
