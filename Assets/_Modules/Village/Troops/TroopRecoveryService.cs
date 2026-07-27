// =============================================================================
// TroopRecoveryService — the wounded-troop RECOVERY advance hook (WO-781).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ArmyStorage.TickRecovery had ZERO callers, so wounded troops (set on raid
// retreat/defeat via ArmyStorage.ReconcileAfterRaid) never healed and the army
// silently degraded toward unwinnable. This thin, self-bootstrapping singleton is
// the missing caller — the army-recovery twin of OfflineHarvestService (offline
// accrual) and BuildTimerService (queue sweep): it drives the persisted recovery
// clock off the SAME wall-clock the Obsidian work queue reads (TimeSource.NowUnixMs
// — reused, NOT forked) so healing and job-resolve stay consistent offline.
// (WO-781: renumbered from the interim WO-779 label; same work.)
//
// The recovery MATH is the pure DeNelle.Core.State.ArmyStorage.AdvanceRecovery
// (headlessly unit-testable with a simulated nowMs); this MonoBehaviour only feeds
// it the clock on the three cadences a CoC-style timer needs:
//   • Start()           — cold-load offline catch-up (credit the away-gap once the
//                         save + Army have loaded in GameStateService.Awake).
//   • OnApplicationPause(false) — mobile resume: credit the backgrounded gap.
//   • Update() ~1/sec   — live cadence while the app is open (mirrors BuildTimerService).
//
// ASSEMBLY NOTE: lives Village-side (not Core) BECAUSE TimeSource is a DeNelle.Village
// type and Core may not reference Village. ArmyStorage / GameState.Army are Core, and
// Village references Core, so reading state.Army.AdvanceRecovery(...) here is the valid,
// reflection-free direction (identical to how OfflineHarvestService banks into GameState).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Ticks the persisted wounded-troop recovery clock (WO-781): cold-load offline
    /// catch-up, mobile-resume catch-up, and a ~1/sec live cadence, all routing through
    /// <see cref="ArmyStorage.AdvanceRecovery"/> off <see cref="TimeSource.NowUnixMs"/>.
    /// Self-bootstrapping DontDestroyOnLoad singleton (mirrors OfflineHarvestService).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TroopRecoveryService : MonoBehaviour
    {
        public static TroopRecoveryService Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("TroopRecoveryService");
            DontDestroyOnLoad(go);
            go.AddComponent<TroopRecoveryService>();
        }

        private void Awake()
        {
            // Destroy(this) NOT Destroy(gameObject) — may share a host (CLAUDE.md memory).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            // One frame's slack so GameStateService.Awake has loaded the save + Army, then
            // credit any recovery earned while the app was closed (offline catch-up).
            StartCoroutine(AdvanceNextFrame());
        }

        private System.Collections.IEnumerator AdvanceNextFrame()
        {
            yield return null;
            Advance();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) Advance();   // resume → credit the backgrounded gap
        }

        // ~1/sec live cadence so a troop's countdown finishes while the app is open,
        // without waiting for the next load (cheap: a handful of troops, checked once/sec).
        private float _nextTick;
        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            Advance();
        }

        // The single advance seam — offline sweep, resume AND the live tick all route here
        // so the recovery math lands identically. Reuses the queue's clock (TimeSource).
        private static void Advance()
        {
            var svc = GameStateService.Instance;
            var army = svc != null && svc.State != null ? svc.State.Army : null;
            if (army == null) return;   // null/empty-army safe (AdvanceRecovery also no-ops)

            int recovered = army.AdvanceRecovery(TimeSource.NowUnixMs());
            if (recovered > 0)
            {
                // Persist the healed roster + fresh anchor, and nudge the combat/roster
                // listeners so the deployable count refreshes. (A zero-heal tick keeps the
                // anchor in memory; it rides along on the next Save — no per-second save spam.)
                FlowTrace.Step("Army", $"TroopRecoveryService: {recovered} troop(s) healed this tick.");
                svc.Save();
                svc.CombatChanged?.Invoke();
            }
            else
            {
                // Throttle: surface that the live cadence is running while anyone is still
                // recovering (proves the advance hook is live without spamming every second).
                int stillWounded = 0;
                if (army.Owned != null)
                {
                    for (int i = 0; i < army.Owned.Count; i++)
                    {
                        var t = army.Owned[i];
                        if (t != null && t.Wounded) stillWounded++;
                    }
                }
                if (stillWounded > 0)
                    FlowTrace.Throttle("Army", "recovery-tick", 5f,
                        $"TroopRecoveryService: recovery advanced; {stillWounded} still wounded.");
            }
        }
    }
}
