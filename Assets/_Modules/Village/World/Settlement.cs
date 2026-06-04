// =============================================================================
// Settlement — a player-built node-claiming outpost (WO-159).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE LOOP (owner-confirmed): Claim → Build → Harvest → Defend → Deplete.
//   • CLAIM   — placed at/adjacent to a MineNode (finite-reserve, WO-159) via
//               build-mode (StructureFactory). The settlement IS the claim.
//   • HARVEST — passively drains the node's ReserveRemaining into the player's
//               wallet at HarvestRatePerSecond (no manual tapping). Reuses
//               MineNode.DrainReserve → BankYield, so there is ONE banking path.
//   • DEFEND  — implements IDamageableStructure: wandering tribes (WO-160) raid it.
//               HP drops under attack; at 0 the settlement is DESTROYED — the claim
//               is lost, the node reverts to unclaimed, the site is razed + locked
//               for 3 game days (owner-locked).
//   • DEPLETE — when the node is mined empty the node despawns but the settlement
//               REMAINS as a plain held outpost (SettlementPhase.Outpost).
//
// RECONCILIATION (no parallel systems, CLAUDE.md §9):
//   • Harvest reuses MineNode's finite-reserve seam (DrainReserve) — not a second
//     economy. Wallet writes go through MineNode.BankYield → GameState.
//   • Defence reuses the Core IDamageableStructure contract (same as Heart/Tower/
//     Gate), so EnemyBrain.TryAttack / Enemy contact damage hit it with zero new code.
//   • State persists as a SettlementState record in GameState.Settlements (the save
//     owner wires the schema round-trip — see GameState/WorldContent notes).
//   • Game-day lockout reads GameClock.CurrentDay() (the single day read), NOT a
//     parallel time system.
//
// Placement: created at runtime by SettlementPlacer (the build-mode seam) or by a
// future StructureFactory behaviorId="Settlement" case. This component is the
// behaviour; it is pack-agnostic (no required art) and degrades gracefully.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.World;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// A player-built node settlement (WO-159). Auto-harvests an adjacent
    /// finite-reserve <see cref="MineNode"/> and defends it; destroyed if a tribe
    /// raid overruns its HP. Implements <see cref="IDamageableStructure"/> so the
    /// existing enemy contact-damage path raids it with no new wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Settlement : MonoBehaviour, IDamageableStructure
    {
        /// <summary>Live settlements in the scene — the raid-target registry TribeManager scans.</summary>
        private static readonly List<Settlement> _all = new List<Settlement>();
        public static IReadOnlyList<Settlement> All => _all;

        [Header("Claim")]
        [Tooltip("The finite-reserve node this settlement claims and drains. May be set by " +
                 "the placer; if null the settlement finds the nearest MineNode in ClaimRadius at Start.")]
        public MineNode ClaimedNode;

        [Tooltip("Search radius for an unclaimed node at Start when ClaimedNode is unset.")]
        [Min(1f)] public float ClaimRadius = 12f;

        [Header("Harvest")]
        [Tooltip("Reserve units drained into the wallet per second while standing + the node " +
                 "has reserve. The auto-harvest faucet (no manual tapping).")]
        [Min(0f)] public float HarvestRatePerSecond = 2f;

        [Header("Defence")]
        [Tooltip("Settlement max HP — the garrison + structures the player invested. A raid " +
                 "that out-damages this destroys the settlement (claim lost, 3-day lockout).")]
        [Min(1f)] public float MaxHp = 100f;

        [Tooltip("Seconds with no incoming raid damage before HP slowly self-repairs (a held, " +
                 "supported settlement recovers between raids). 0 = never auto-repair.")]
        [Min(0f)] public float RepairDelay = 20f;

        [Tooltip("HP per second restored once RepairDelay has elapsed with no damage.")]
        [Min(0f)] public float RepairRate = 3f;

        // ── Runtime state ────────────────────────────────────────────────────
        private float _hp;
        private SettlementPhase _phase = SettlementPhase.Active;
        private float _harvestAccumulator;   // fractional reserve carried between ticks
        private float _sinceDamage;
        private SettlementState _record;      // GameState-persisted record (mirror)

        /// <summary>Region this settlement sits in.</summary>
        public RegionId Region => ZoneManager.GetZone(transform.position);

        /// <summary>Stable site id — derived from the node id / position (the SettlementState key).</summary>
        public string SiteId { get; private set; }

        /// <summary>Lifecycle phase.</summary>
        public SettlementPhase Phase => _phase;

        /// <summary>Current HP fraction (drives a defence bar / the "under attack" tell).</summary>
        public float HpFraction => MaxHp > 0f ? Mathf.Clamp01(_hp / MaxHp) : 0f;

        /// <summary>Fired when a raid first starts dealing damage (the WO-159 "under attack" warning seam).</summary>
        public event System.Action<Settlement> UnderAttack;
        /// <summary>Fired when the settlement is destroyed (claim lost / razed).</summary>
        public event System.Action<Settlement> Destroyed;

        // ── IDamageableStructure ─────────────────────────────────────────────
        /// <summary>Raids can hit the settlement only while it stands and is harvesting/held.</summary>
        public bool IsAlive => _phase != SettlementPhase.Razed && _hp > 0f;

        public void ApplyContactDamage(float amount)
        {
            if (amount <= 0f || _phase == SettlementPhase.Razed) return;

            bool wasFull = _sinceDamage > RepairDelay || Mathf.Approximately(_hp, MaxHp);
            _sinceDamage = 0f;

            float before = _hp;
            _hp = Mathf.Max(0f, _hp - amount);
            if (_record != null) _record.Hp = _hp;

            if (wasFull && before >= _hp) UnderAttack?.Invoke(this);   // first hit of a raid → warn

            if (_hp <= 0f) Raze();
        }

        private void Awake()
        {
            _hp = MaxHp;
        }

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() { _all.Remove(this); }

        private void Start()
        {
            if (ClaimedNode == null) ClaimedNode = FindNearestClaimableNode();
            SiteId = ResolveSiteId();
            BindOrCreateRecord();
        }

        private void Update()
        {
            _sinceDamage += Time.deltaTime;

            if (_phase == SettlementPhase.Active)
            {
                TickHarvest();
                TickRepair();
                CheckNodeDepleted();
            }
            else if (_phase == SettlementPhase.Outpost)
            {
                TickRepair();   // a held outpost still self-repairs between raids
            }
        }

        // ── Harvest ───────────────────────────────────────────────────────────

        private void TickHarvest()
        {
            if (ClaimedNode == null) { CheckNodeDepleted(); return; }
            if (!ClaimedNode.UseFiniteReserve || ClaimedNode.IsReserveEmpty) return;

            _harvestAccumulator += HarvestRatePerSecond * Time.deltaTime;
            int whole = Mathf.FloorToInt(_harvestAccumulator);
            if (whole <= 0) return;

            int banked = ClaimedNode.DrainReserve(whole);   // banks to wallet + despawns node when empty
            _harvestAccumulator -= banked;                  // only consume what was actually banked
            if (banked <= 0) _harvestAccumulator = 0f;      // node empty/gone — stop carrying a debt
        }

        // The node despawns itself when empty (MineNode.DrainReserve). Detect that
        // (ClaimedNode now null OR reserve empty) and flip to a held outpost.
        private void CheckNodeDepleted()
        {
            bool nodeGone = ClaimedNode == null || ClaimedNode.IsReserveEmpty;
            if (nodeGone && _phase == SettlementPhase.Active)
            {
                _phase = SettlementPhase.Outpost;
                if (_record != null) _record.Phase = SettlementPhase.Outpost;
                Debug.Log($"[Settlement {SiteId}] node mined empty — settlement REMAINS as an outpost.");
            }
        }

        // ── Defence / repair / raze ──────────────────────────────────────────

        private void TickRepair()
        {
            if (RepairRate <= 0f || _hp >= MaxHp) return;
            if (_sinceDamage < RepairDelay) return;
            _hp = Mathf.Min(MaxHp, _hp + RepairRate * Time.deltaTime);
            if (_record != null) _record.Hp = _hp;
        }

        private void Raze()
        {
            if (_phase == SettlementPhase.Razed) return;
            _phase = SettlementPhase.Razed;

            int razedUntil = GameClock.CurrentDay() + LockoutDays;
            if (_record != null)
            {
                _record.Phase = SettlementPhase.Razed;
                _record.Hp = 0f;
                _record.RazedUntilDay = razedUntil;
            }
            // Release the claim so the node (if reserve remains) becomes re-claimable
            // once the lockout passes.
            if (ClaimedNode != null) ClaimedNode.SetWorkerClaim(false);

            Debug.Log($"[Settlement {SiteId}] OVERRUN — destroyed. Site razed; locked until game-day {razedUntil} " +
                      $"({LockoutDays}-day lockout). Claim lost.");
            Destroyed?.Invoke(this);
            Destroy(gameObject);
        }

        /// <summary>Owner-locked: a razed site is blocked for this many game-days.</summary>
        public const int LockoutDays = 3;

        // ── Record binding (GameState.Settlements) ───────────────────────────

        private void BindOrCreateRecord()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return;
            if (state.Settlements == null) state.Settlements = new List<SettlementState>();

            // Re-bind an existing record for this site (e.g. after a reload) so HP /
            // phase / lockout survive; otherwise create one.
            for (int i = 0; i < state.Settlements.Count; i++)
            {
                if (state.Settlements[i] != null && state.Settlements[i].SiteId == SiteId)
                {
                    _record = state.Settlements[i];
                    _hp = Mathf.Clamp(_record.Hp <= 0f ? MaxHp : _record.Hp, 1f, MaxHp);
                    _phase = _record.Phase == SettlementPhase.Razed ? SettlementPhase.Active : _record.Phase;
                    return;
                }
            }

            var p = transform.position;
            _record = new SettlementState(SiteId, Region,
                new WorldPoint(p.x, p.y, p.z), MaxHp);
            state.Settlements.Add(_record);
        }

        private string ResolveSiteId()
        {
            if (ClaimedNode != null) return $"site-{ClaimedNode.name}";
            var p = transform.position;
            return $"site-{Mathf.RoundToInt(p.x)}-{Mathf.RoundToInt(p.z)}";
        }

        private MineNode FindNearestClaimableNode()
        {
#if UNITY_2023_1_OR_NEWER
            var nodes = Object.FindObjectsByType<MineNode>(FindObjectsSortMode.None);
#else
            var nodes = Object.FindObjectsOfType<MineNode>();
#endif
            MineNode best = null;
            float bestSqr = ClaimRadius * ClaimRadius;
            foreach (var n in nodes)
            {
                if (n == null || !n.UseFiniteReserve || n.IsReserveEmpty) continue;
                float d = (n.transform.position - transform.position).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = n; }
            }
            return best;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.85f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, ClaimRadius);
        }
#endif
    }
}
