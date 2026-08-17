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
// WO-1147 -- THIS SERVICE NO LONGER OWNS THE CLOCK. It is now one CONSUMER of
// OfflineClaimCoordinator (see that file's header): the coordinator reads
// GameState.LastHarvestClaimMs once, computes ONE elapsed window, fans it out to
// every consumer (node harvest here, the Echo silo, Echo repair), and advances +
// persists the clock exactly once. Before that, this service wrote the clock from
// its own Start+1-frame coroutine while EchoService read it in the SAME frame
// (coin-flip) and EchoRepairService read it one frame LATER (always zero -- offline
// repair never accrued once). Do NOT re-add a write to LastHarvestClaimMs here.
// The 10h away-cap below stays OURS: the coordinator publishes the raw window and
// each consumer clamps with its own documented cap.
//
// DEDUPE vs BACKEND SAVE-SYNC (no double-grant): accrual only ever runs FORWARD
// from LastHarvestClaimMs, and that timestamp is advanced + persisted ATOMICALLY
// with the grant (advance-even-when-zero) by the coordinator. A second resume can't
// re-accrue the same window because the clock already moved past it. GameStateService's backend
// sync ships the FULL snapshot (which will include LastHarvestClaimMs once the save
// owner wires the field through the schema), so server "now" and the banked haul
// stay consistent — the server sees the post-grant wallet + post-grant clock, never
// a stale window to replay. See the save-schema note at the bottom.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Economy;   // WO-857 Phase F — the town bank cap (this path writes the wallet directly)
using DeNelle.Core.State;
using DeNelle.Core.World;
using DeNelle.Village.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Computes resources accrued by worker-claimed nodes + WO-159 settlements +
    /// WO-229 harvesting pets (each derived from the shared MineNode claim seam) while
    /// the app was backgrounded/closed, grants them capped, and raises a welcome-back
    /// summary. Runs on cold load and on resume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfflineHarvestService : MonoBehaviour, IOfflineClaimConsumer
    {
        public static OfflineHarvestService Instance { get; private set; }

        /// <summary>Trace name for this consumer's share of the shared offline window.</summary>
        public string OfflineConsumerName => "harvest-nodes";

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

        // Worker-owned nodes captured during AccrueWorkerNodes, so AccruePets can
        // exclude them and credit ONLY the pet-claimed nodes (disjoint sets → no
        // double-grant). Reused (cleared) each claim; never holds across frames.
        private readonly HashSet<MineNode> _workerOwnedThisClaim = new HashSet<MineNode>();

        // The result produced by the most recent ApplyOfflineWindow, so the public
        // ClaimAccrual() verb can still return "what this consumer banked".
        private OfflineHarvestResult _lastResult;

        private void Awake()
        {
            // Destroy(this) — NOT Destroy(gameObject): may share a host
            // (CLAUDE.md memory: singleton-dedup-destroys-host).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            OfflineClaimCoordinator.Register(this);
        }

        private void OnDestroy()
        {
            OfflineClaimCoordinator.Unregister(this);
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Cold-load claim. Deferred TWO frames so MineNode/Settlement Awake/Start have
            // run and registered, the save has loaded (GameStateService.Awake), AND every
            // other offline consumer (EchoService / EchoRepairService, installed by their
            // own AfterSceneLoad bootstrap) has registered — the fan-out must reach all of
            // them on the SAME claim. Two frames is the slowest consumer's old deferral
            // (EchoRepairService needed scene structures present), now shared.
            ClaimDeferred("cold-load");
        }

        private void OnApplicationPause(bool paused)
        {
            // Tell the ONE authority where the background edge was, so a RESUME claim counts
            // only the truly-away stretch. Every consumer of a resume claim has an online
            // loop that already covered the foreground stretch (the silo tick, the repair
            // tick, settlement/pet node extraction), so counting from the persisted clock —
            // which is what this service used to do — paid that stretch TWICE. We do not
            // stamp the persisted clock at pause: a hard kill while backgrounded must still
            // leave a claimable window for the next cold load.
            OfflineClaimCoordinator.NotePaused(paused);
            if (!paused && ClaimOnResume) ClaimDeferred("resume");
        }

        private void ClaimDeferred(string reason)
        {
            if (isActiveAndEnabled) StartCoroutine(ClaimAfterTwoFrames(reason));
        }

        private System.Collections.IEnumerator ClaimAfterTwoFrames(string reason)
        {
            yield return null;
            yield return null;
            // ONE claim for the whole game: the coordinator fans the window out to every
            // consumer and advances the clock once. Our own share lands in
            // ApplyOfflineWindow (which raises Claimed + the welcome-back popup).
            OfflineClaimCoordinator.Claim(reason);
        }

        // =====================================================================
        //  Accrual — the accrue-on-resume mechanic (WO-115 §1)
        // =====================================================================

        /// <summary>
        /// Runs a FULL offline claim through the one authority
        /// (<see cref="OfflineClaimCoordinator"/>) and returns THIS service's share of it.
        /// The coordinator reads the clock once, fans the same window out to every
        /// consumer, and advances + persists the clock exactly once (even on a zero haul,
        /// so a player who claims their first node later banks no retroactive haul).
        /// Kept as a public verb because oracles + legacy callers drive the claim by name.
        /// </summary>
        public OfflineHarvestResult ClaimAccrual()
        {
            FlowTrace.Step("Offline", "ClaimAccrual");
            // Idempotent: editmode/headless AddComponent never runs Awake, so register here too.
            OfflineClaimCoordinator.Register(this);
            _lastResult = OfflineHarvestResult.None;
            OfflineClaimCoordinator.Claim("OfflineHarvestService.ClaimAccrual");
            return _lastResult ?? OfflineHarvestResult.None;
        }

        /// <summary>
        /// THIS consumer's share of the shared window: integrate every active harvest
        /// source over the window CLAMPED TO OUR OWN 10h away-cap, bank the haul, and
        /// raise the welcome-back reveal. Never touches the clock — the coordinator owns it.
        /// </summary>
        public void ApplyOfflineWindow(OfflineClaimWindow window)
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                FlowTrace.Warn("Offline", "no GameStateService — node accrual skipped (None)");
                _lastResult = OfflineHarvestResult.None;
                return;
            }

            double elapsedSec = window.ElapsedSeconds;
            double cappedSec = window.CappedSeconds(OfflineCapHours);
            bool wasCapped = window.ExceedsCap(OfflineCapHours);
            if (wasCapped) FlowTrace.Warn("Offline", $"away {elapsedSec:0}s exceeds cap {OfflineCapSeconds:0}s — capped");

            var result = new OfflineHarvestResult
            {
                AwaySeconds = elapsedSec,
                WasCapped = wasCapped,
            };

            if (cappedSec > 0.0)
            {
                // ORDER MATTERS for double-grant safety. AccrueWorkerNodes snapshots the
                // worker-owned node set FIRST; AccruePets then credits the pet-owned nodes,
                // which it derives as "claimed but NOT worker-owned" — so the two source
                // sets are disjoint by construction and a node is never counted twice.
                AccrueWorkerNodes(result, cappedSec);
                AccrueSettlements(result, cappedSec);
                AccruePets(result, cappedSec);
                FlowTrace.Step("Offline", $"accrued over {cappedSec:0}s: worker-owned={_workerOwnedThisClaim.Count} node(s), total={result.Total}");
            }

            if (result.Total > 0) Grant(result, state);
            else FlowTrace.Step("Offline", "zero haul — clock still advances (prevents retroactive first-claim)");

            FlowTrace.Step("Offline",
                $"claim #{window.Sequence}: 'harvest-nodes' share = {cappedSec:0}s of the {elapsedSec:0}s window " +
                $"(cap {OfflineCapHours:0.##}h) -> total {result.Total}.");

            _lastResult = result;
            if (result.Total > 0)
            {
                Claimed?.Invoke(result);
                TryShowPopup(result);
            }
        }

        // ── Source 1: worker-collected mine nodes (WO-117 seam) ───────────────
        // Each on-station worker drives one node at MineNode.RatePerSecond. We
        // integrate that rate over the capped window. (RatePerSecond is 0 for a
        // finite-reserve node, so those are handled only by the settlement path.)
        private void AccrueWorkerNodes(OfflineHarvestResult result, double cappedSec)
        {
            _workerOwnedThisClaim.Clear();

            var wm = WorkerManager.Instance;
            if (wm == null) return;

            IReadOnlyList<MineNode> nodes = wm.ActiveAssignments();
            if (nodes == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null) continue;
                // Record worker ownership BEFORE the depletion/rate gates so AccruePets
                // never re-credits a node a worker owns, even one that's spent this frame.
                _workerOwnedThisClaim.Add(node);
                if (node.IsDepleted) continue;
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

        // ── Source 3: harvesting pets (WO-229 PetHarvester) ───────────────────
        // A deployed harvesting pet works a MineNode through the SAME seam a Worker
        // does: it claims the node (MineNode.SetWorkerClaim → IsClaimedByWorker) and
        // drives TryAutoExtract() on the node's cooldown. So a pet-worked node banks at
        // exactly the node's RatePerSecond, just like a worker-worked one.
        //
        // We can't enumerate PetHarvester from here (it lives in DeNelle.Pets, which
        // Village must not reference — CLAUDE.md §5/§9; pets reach Village only via the
        // reflection MineNodeBridge). Instead we read the shared MineNode claim seam:
        // a node that is CLAIMED but is NOT one of WorkerManager's active worker
        // assignments is, by elimination, being worked by a pet (the only other thing
        // that calls SetWorkerClaim). That set is disjoint from AccrueWorkerNodes's set
        // by construction (we exclude _workerOwnedThisClaim), so no node is double-counted.
        //
        // Note: claims are runtime-only (not persisted), so on a COLD load a pet hasn't
        // re-acquired a node yet within this deferred frame → pets contribute 0 that
        // launch (the clock still advances; nothing is lost, just not retro-credited).
        // On RESUME (the common mobile case) claims are live, so the away-gap is credited.
        private void AccruePets(OfflineHarvestResult result, double cappedSec)
        {
#if UNITY_2023_1_OR_NEWER
            var nodes = Object.FindObjectsByType<MineNode>();
#else
            var nodes = Object.FindObjectsByType<MineNode>();
#endif
            if (nodes == null) return;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || node.IsDepleted) continue;
                if (!node.IsClaimedByWorker) continue;             // unclaimed → no harvester
                if (_workerOwnedThisClaim.Contains(node)) continue; // a Worker owns it → already credited
                float rate = node.RatePerSecond;                   // finite-reserve nodes report 0 (settlement path)
                if (rate <= 0f) continue;
                int accrued = (int)(rate * cappedSec);
                result.Add(node.Resource, accrued);
            }
        }

        // ── Banking — the established off-chain award path ────────────────────
        // Core can't reference Village, but Village references Core, so writing the
        // GameState resource fields directly is the valid, reflection-free path (the
        // same one MineNode.BankYield uses). Local off-chain currency only — no token mint.
        //
        // WO-857 / WO-901 Phase F — this path writes the wallet DIRECTLY (it cannot call
        // EconomyService.Grant from here for the reason above), so it must apply the town bank
        // cap itself through the SAME one reader. It is the only real income source in the game
        // that bypasses the EconomyService choke, and it is the one most likely to overflow:
        // an away pool banks hours of production in a single frame. Clamp-and-warn (owner ruling
        // 2026-08-04) — TownBankCapacity.ClampGrant raises the [Flow:Bank] warn and the on-screen
        // toast for us. Crystals are UNCAPPED by design and pass through untouched.
        private void Grant(OfflineHarvestResult result, GameState state)
        {
            int iron = TownBankCapacity.ClampGrant(BankResource.Iron, state.Iron, result.Iron, "OfflineHarvest", out _);
            int wood = TownBankCapacity.ClampGrant(BankResource.Wood, state.Wood, result.Wood, "OfflineHarvest", out _);
            int food = TownBankCapacity.ClampGrant(BankResource.Food, state.Resources.Food, result.Food, "OfflineHarvest", out _);

            if (iron > 0) state.Iron += iron;
            if (wood > 0) state.Wood += wood;
            if (food > 0)
            {
                // Food lives on the wallet struct (Resources.Food) — DEF-121.
                var bal = state.Resources;
                bal.Food += food;
                state.Resources = bal;
            }
            if (result.AetherCrystals > 0)
            {
                // Crystals unified onto Resources.Crystals (the single wallet).
                var cbal = state.Resources;
                cbal.Crystals += result.AetherCrystals;
                state.Resources = cbal;
            }

            // Nudge the resource-changed listeners (HUD wallet) without coupling to HUD.
            GameStateService.Instance?.ResourcesChanged?.Invoke();

            // Report what was actually BANKED (post-bank-cap), and name the accrual separately when
            // the two differ — a log that shows the pre-clamp number is how a silent loss hides.
            bool bankTruncated = iron != result.Iron || wood != result.Wood || food != result.Food;
            Debug.Log($"[OfflineHarvest] Banked +{iron} iron, +{wood} wood, " +
                      $"+{food} food, +{result.AetherCrystals} crystals over " +
                      $"{Mathf.RoundToInt((float)result.AwaySeconds)}s away" +
                      (result.WasCapped ? " (away-cap)." : ".") +
                      (bankTruncated
                          ? $" BANK FULL - accrued {result.Iron} iron / {result.Wood} wood / {result.Food} food; the surplus was LOST."
                          : ""));
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

            // (Defend-the-Tower mode removed — only an active wave counts as combat now.)
            return false;
        }
    }
}
