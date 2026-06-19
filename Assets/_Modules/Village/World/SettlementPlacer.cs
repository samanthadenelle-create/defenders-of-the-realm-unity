// =============================================================================
// SettlementPlacer — the claim VERB for node settlements (WO-159 Phase 2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The player claims a finite-reserve MineNode (WO-159) by building a Settlement on
// it. This is the build-mode placement SEAM: ClaimAt() is the one method the real
// build-mode UI (TowerPlacementSystem-style ghost, or a context menu) calls; it
// enforces the rules and creates the Settlement. A lean click-to-claim demo verb
// (mirroring WorkerManager.ClickToDispatch) makes the loop playable today without a
// dedicated UI — disable it when the proper build-mode flow drives ClaimAt().
//
// RULES (WO-159 acceptance):
//   • Only finite-reserve nodes with reserve remaining are claimable.
//   • A site that is RAZED and still inside its 3-game-day lockout REJECTS placement
//     (returns a reason string so the UI can show "Razed — clears in N days").
//   • A node already claimed by a standing settlement is not double-claimed.
//
// Reuses Settlement (the harvest+defend behaviour) + GameClock (the day read) +
// GameState.Settlements (the persisted records) — no parallel systems. Pack-agnostic
// (the Settlement renders a simple code-built marker; swap to a catalog visual via
// StructureFactory behaviorId="Settlement" later).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.World;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Places node-claiming <see cref="Settlement"/>s (WO-159). Singleton; exposes
    /// <see cref="ClaimAt"/> (the build-mode seam) + an optional click-to-claim demo.
    /// </summary>
    public sealed class SettlementPlacer : MonoBehaviour
    {
        public static SettlementPlacer Instance { get; private set; }

        [Header("Demo claim verb")]
        [Tooltip("If true, a left-click on a finite-reserve MineNode builds a settlement there " +
                 "(the playable Phase-2 verb). Disable when a proper build-mode UI calls ClaimAt().")]
        public bool ClickToClaim = true;

        [Header("Settlement defaults")]
        [Tooltip("Starting max HP of a freshly-built settlement (the base garrison the player " +
                 "can reinforce with walls/towers/defenders later).")]
        [Min(1f)] public float DefaultMaxHp = 100f;

        [Tooltip("Reserve units a settlement auto-harvests per second.")]
        [Min(0f)] public float DefaultHarvestRate = 2f;

        private Transform _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("SettlementPlacer").AddComponent<SettlementPlacer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (ClickToClaim) HandleClickClaim();
        }

        private void HandleClickClaim()
        {
            if (!UnityEngine.Input.GetMouseButtonDown(0)) return;
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 500f))
            {
                var node = hit.collider.GetComponentInParent<MineNode>();
                if (node != null && node.UseFiniteReserve)
                {
                    var result = ClaimAt(node);
                    if (!result.Ok) FlowTrace.Step("Settlement", $"Click-claim rejected: {result.Reason}");
                }
            }
        }

        /// <summary>
        /// Try to build a settlement claiming <paramref name="node"/>. Returns whether it
        /// succeeded + a reason on failure (the UI surfaces the reason, e.g. the lockout
        /// countdown). The build-mode seam — call this from the real placement UI.
        /// </summary>
        public ClaimResult ClaimAt(MineNode node)
        {
            using var _ = FlowTrace.Enter("Settlement", $"ClaimAt node='{(node != null ? node.name : "<null>")}'");
            if (node == null)
            {
                FlowTrace.Warn("Settlement", "ClaimAt: null node — claim rejected.");
                return ClaimResult.Fail("no node");
            }
            if (!node.UseFiniteReserve) return ClaimResult.Fail("node is not a claimable reserve");
            if (node.IsReserveEmpty) return ClaimResult.Fail("node is mined out");

            string siteId = $"site-{node.name}";

            // Razed-site lockout — reject while the game-day clock hasn't passed RazedUntilDay.
            var existing = FindRecord(siteId);
            if (existing != null && existing.Phase == SettlementPhase.Razed)
            {
                int today = GameClock.CurrentDay();
                if (existing.IsLockedAt(today))
                {
                    int daysLeft = Mathf.Max(0, existing.RazedUntilDay - today);
                    return ClaimResult.Fail($"Razed — clears in {daysLeft} day(s)");
                }
                // Lockout passed — clear the razed record so a fresh claim can bind.
                RemoveRecord(siteId);
            }

            // Don't double-claim a node already held by a standing settlement.
            foreach (var s in Settlement.All)
                if (s != null && s.ClaimedNode == node && s.IsAlive)
                    return ClaimResult.Fail("node already claimed");

            if (_root == null) _root = new GameObject("[Settlements]").transform;

            var go = new GameObject($"Settlement ({node.name})");
            go.transform.SetParent(_root, false);
            go.transform.position = node.transform.position;

            // A simple code-built marker so the settlement is visible without an art prefab
            // (pack-agnostic, CLAUDE.md §4). Swap for a catalog visual later.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Outpost";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(2f, 1.2f, 2f);
            var col = body.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;   // raids' contact probe needs a solid to hit

            var settlement = go.AddComponent<Settlement>();
            settlement.ClaimedNode = node;
            settlement.MaxHp = DefaultMaxHp;
            settlement.HarvestRatePerSecond = DefaultHarvestRate;

            node.SetWorkerClaim(true);   // mark the node claimed (blocks legacy worker dispatch)

            // V (TGVRU-V): prove the committed outpost has a live renderer (a primitive cube can
            // come back with no enabled Renderer) so an invisible settlement self-reports instead
            // of leaving the player with a claimed-but-unseen site. Non-fatal — the claim stands.
            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer == null || !bodyRenderer.enabled)
                FlowTrace.Warn("Settlement",
                    $"ClaimAt: settlement body for '{node.name}' has no enabled renderer — outpost may be INVISIBLE.");

            FlowTrace.Step("Settlement", $"ClaimAt: claimed '{node.name}' — settlement built at {go.transform.position}, auto-harvesting.");
            return ClaimResult.Pass(settlement);
        }

        private static SettlementState FindRecord(string siteId)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state?.Settlements == null) return null;
            for (int i = 0; i < state.Settlements.Count; i++)
                if (state.Settlements[i] != null && state.Settlements[i].SiteId == siteId)
                    return state.Settlements[i];
            return null;
        }

        private static void RemoveRecord(string siteId)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state?.Settlements == null) return;
            state.Settlements.RemoveAll(s => s != null && s.SiteId == siteId);
        }

        /// <summary>Result of a claim attempt — Ok + the settlement, or a UI reason on failure.</summary>
        public struct ClaimResult
        {
            public bool Ok;
            public string Reason;
            public Settlement Settlement;

            public static ClaimResult Pass(Settlement s) => new ClaimResult { Ok = true, Settlement = s };
            public static ClaimResult Fail(string reason) => new ClaimResult { Ok = false, Reason = reason };
        }
    }
}
