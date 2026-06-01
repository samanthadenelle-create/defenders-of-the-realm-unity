// =============================================================================
// OfflineHarvestService — accrue resources while the player is away (WO-115).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The OFFLINE rung of the core loop (docs/NORTH_STAR.md): "mines + pets keep
// gathering up to a cap → come back richer." On cold load and on app-resume it
// computes how long the player was gone, accrues each active harvest source's
// yield over that window (clamped to a cap), banks it into GameState, and raises
// a one-tap welcome-back summary.
//
// SUPERSEDES the WO-117 WorkerManager PlayerPrefs catch-up STUB:
//   • WorkerManager kept its own "dotr-harvest-last-active" PlayerPrefs ticks and
//     replayed ForceAutoExtract() per node on Start/resume. That is now disabled
//     (WorkerManager.UseOfflineCatchUp = false) so there is ONE offline path.
//   • This service reads the SAME seam WorkerManager exposes
//     (WorkerManager.ActiveAssignments() + MineNode.RatePerSecond) PLUS the
//     WO-159 Settlement faucet (Settlement.HarvestRatePerSecond draining a finite
//     reserve), and banks via the wallet directly — the established award path
//     (Core can't ref Village; we write GameState resource fields).
//   • The accrual CLOCK now lives in the persisted save (GameState.LastHarvestClaimMs,
//     mirroring LastInboxSyncAt) instead of a side-band PlayerPrefs key, so it
//     round-trips with the rest of the save and reconciles with backend sync.
//
// DEDUPE vs BACKEND SAVE-SYNC (no double-grant): accrual only ever runs FORWARD
// from LastHarvestClaimMs, and that timestamp is advanced + persisted ATOMICALLY
// with the grant (advance-even-when-zero). A second resume can't re-accrue the
// same window because the clock already moved past it. GameStateService's backend
// sync ships the FULL snapshot (which will include LastHarvestClaimMs once the save
// owner wires the field through the schema), so server "now" and the banked haul
// stay consistent — the server sees the post-grant wallet + post-grant clock, never
// a stale window to replay. See the save-schema note at the bottom.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Core.World;
using DeNelle.Village.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Computes resources accrued by claimed nodes + settlements (+ harvesting pets,
    /// when WO-111 Phase 4 lands) while the app was backgrounded/closed, grants them
    /// capped, and raises a welcome-back summary. Runs on cold load and on resume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfflineHarvestService : MonoBehaviour
    {
        public static OfflineHarvestService Instance { get; private set; }

        [Header("Cap")]
        [Tooltip("Offline hours credited in one claim. The retention dial: long enough that a " +
                 "twice-a-day check-in feels rewarded, short enough that the mines still want " +
                 "defending. WO-115 suggests 8–12h; default 10h. Owner-tunable in playtest.")]
        [Min(0f)] public float OfflineCapHours = 10f;

        [Header("Resume policy")]
        [Tooltip("Also claim on OnApplicationPause(false) — i.e. when the app returns to the " +
                 "foreground on mobile, not only on a cold load. Off would only accrue on a full " +
                 "relaunch.")]
        public bool ClaimOnResume = true;

        /// <summary>Raised after a claim that banked something — the welcome-back popup listens.</summary>
        public event System.Action<OfflineHarvestResult> Claimed;

        private float OfflineCapSeconds => Mathf.Max(0f, OfflineCapHours) * 3600f;

        private void Awake()
        {
            // Destroy(this) — NOT Destroy(gameObject): may share a host
            // (CLAUDE.md memory: singleton-dedup-destroys-host).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Cold-load claim. Deferred to LateClaim so MineNode/Settlement Awake/Start
            // have run and registered (and the save has loaded in GameStateService.Awake).
            ClaimDeferred();
        }

        private void OnApplicationPause(bool paused)
        {
            // On background we do NOT need to stamp anything — the clock is whatever
            // LastHarvestClaimMs already holds; the next ClaimAccrual() integrates from
            // there. (Backend sync's own OnApplicationPause flush is independent.)
            if (!paused && ClaimOnResume) ClaimDeferred();
        }

        private void ClaimDeferred()
        {
            // One frame of slack so registries are populated, then claim.
            if (isActiveAndEnabled) StartCoroutine(ClaimNextFrame());
        }

        private System.Collections.IEnumerator ClaimNextFrame()
        {
            yield return null;
            var result = ClaimAccrual();
            if (result != null && result.Total > 0)
            {
                Claimed?.Invoke(result);
                TryShowPopup(result);
            }
        }

        // =====================================================================
        //  Accrual — the accrue-on-resume mechanic (WO-115 §1)
        // =====================================================================

        /// <summary>
        /// Integrate every active harvest source over the away-gap, bank the capped
        /// haul into GameState, advance + persist the claim clock, and return what was
        /// banked. Always advances the clock to now — even when nothing accrued — so a
        /// player who claims their first node later doesn't bank a giant retroactive haul.
        /// </summary>
        public OfflineHarvestResult ClaimAccrual()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null) return OfflineHarvestResult.None;

            double nowMs = TimeSource.NowUnixMs();
            double lastClaimMs = state.LastHarvestClaimMs;

            // Fresh save (0) → seed the clock to now, accrue nothing this launch.
            if (lastClaimMs <= 0)
            {
                state.LastHarvestClaimMs = nowMs;
                svc.Save();
                return OfflineHarvestResult.None;
            }

            // Anti-tamper monotonic guard: a clock set BACKWARDS yields a negative
            // delta → clamp to 0 (no accrual, no error). Never re-claim a window.
            double elapsedSec = System.Math.Max(0.0, (nowMs - lastClaimMs) / 1000.0);
            double cappedSec = System.Math.Min(elapsedSec, OfflineCapSeconds);

            var result = new OfflineHarvestResult
            {
                AwaySeconds = elapsedSec,
                WasCapped = elapsedSec > OfflineCapSeconds,
            };

            if (cappedSec > 0.0)
            {
                AccrueWorkerNodes(result, cappedSec);
                AccrueSettlements(result, cappedSec);
                // Pet harvest accrual — null-safe no-op until WO-111 Phase 4 ships
                // pet auto-harvest. Folds into the same result buckets when it lands.
                AccruePets(result, cappedSec);
            }

            if (result.Total > 0) Grant(result, state);

            // ALWAYS advance the clock — even on a zero haul (prevents a giant first
            // claim once a node is finally placed) — and persist atomically with the grant.
            state.LastHarvestClaimMs = nowMs;
            svc.Save();
            return result;
        }

        // ── Source 1: worker-collected mine nodes (WO-117 seam) ───────────────
        // Each on-station worker drives one node at MineNode.RatePerSecond. We
        // integrate that rate over the capped window. (RatePerSecond is 0 for a
        // finite-reserve node, so those are handled only by the settlement path.)
        private void AccrueWorkerNodes(OfflineHarvestResult result, double cappedSec)
        {
            var wm = WorkerManager.Instance;
            if (wm == null) return;

            IReadOnlyList<MineNode> nodes = wm.ActiveAssignments();
            if (nodes == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || node.IsDepleted) continue;
                float rate = node.RatePerSecond;
                if (rate <= 0f) continue;
                int accrued = (int)(rate * cappedSec);
                result.Add(node.Resource, accrued);
            }
        }

        // ── Source 2: WO-159 settlements draining finite reserves ─────────────
        // A settlement auto-harvests its claimed finite-reserve node at
        // HarvestRatePerSecond. Offline we credit rate × window, but never more than
        // the reserve actually held (the ward/claim gate: only an active settlement on
        // a non-empty reserve accrues — a razed/outpost settlement or empty node yields 0).
        private void AccrueSettlements(OfflineHarvestResult result, double cappedSec)
        {
            var all = Settlement.All;
            if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.Phase != SettlementPhase.Active) continue;
                var node = s.ClaimedNode;
                if (node == null || !node.UseFiniteReserve || node.IsReserveEmpty) continue;

                int owed = (int)(s.HarvestRatePerSecond * cappedSec);
                if (owed <= 0) continue;
                // Clamp to what's left in the ground so offline can't over-mine a reserve.
                int banked = Mathf.Min(owed, node.ReserveRemaining);
                result.Add(node.Resource, banked);
            }
        }

        // ── Source 3: harvesting pets (WO-111 Phase 4 — not built yet) ────────
        // Null-safe no-op. When pet auto-harvest lands it exposes a registry of
        // (resource, ratePerSecond) harvesters; integrate them into the same result
        // here. No crash today because there is simply no source to read.
        private void AccruePets(OfflineHarvestResult result, double cappedSec)
        {
            // Intentionally empty until WO-111 Phase 4. Left as an explicit seam so the
            // integration point is obvious and the no-op is deliberate, not forgotten.
        }

        // ── Banking — the established off-chain award path ────────────────────
        // Core can't reference Village, but Village references Core, so writing the
        // GameState resource fields directly is the valid, reflection-free path (the
        // same one MineNode.BankYield uses). Local off-chain currency only — no token mint.
        private void Grant(OfflineHarvestResult result, GameState state)
        {
            if (result.Iron > 0) state.Iron += result.Iron;
            if (result.Wood > 0) state.Wood += result.Wood;
            if (result.Stone > 0) state.Stone += result.Stone;
            if (result.AetherCrystals > 0) state.AetherCrystals += result.AetherCrystals;

            // Nudge the resource-changed listeners (HUD wallet) without coupling to HUD.
            GameStateService.Instance?.ResourcesChanged?.Invoke();

            Debug.Log($"[OfflineHarvest] Banked +{result.Iron} iron, +{result.Wood} wood, " +
                      $"+{result.Stone} stone, +{result.AetherCrystals} crystals over " +
                      $"{Mathf.RoundToInt((float)result.AwaySeconds)}s away" +
                      (result.WasCapped ? " (capped)." : "."));
        }

        // =====================================================================
        //  Welcome-back popup (code-built, hosted on a borrowed PanelSettings)
        // =====================================================================

        private void TryShowPopup(OfflineHarvestResult result)
        {
            // Suppress during an active wave or while the Defend-the-Tower mode is
            // running — the grant already happened (the popup is only a reveal), so a
            // suppressed popup never loses resources; it's just not shown mid-fight.
            if (IsCombatActive())
            {
                Debug.Log("[OfflineHarvest] Combat active — welcome-back reveal suppressed (haul already banked).");
                return;
            }
            WelcomeBackPopup.Show(result);
        }

        private static bool IsCombatActive()
        {
            // Active wave?
            var wm = FindAnyObjectByType<WaveManager>();
            if (wm != null && wm.Phase == WavePhase.Active) return true;

            // Defend-the-Tower mode? (mirrors LevelUpSkillPopup's combat guard)
            if (FindAnyObjectByType<PatriciaLightController>() != null) return true;

            return false;
        }
    }
}
