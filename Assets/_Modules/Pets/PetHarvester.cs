// =============================================================================
// PetHarvester — the CORE pet auto-harvest loop (WO-229).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Pets   Namespace: DeNelle.Pets
//
// "Pet gathers while you defend." A deployed pet, when no enemy needs fighting,
// autonomously walks to the nearest resource node, harvests it on a tick, and the
// yield is banked into the EXISTING economy. When an enemy appears (Defend pets)
// the pet abandons harvesting and fights — combat always wins; harvesting is the
// idle-time faucet.
//
// RECONCILIATION (CLAUDE.md §9 — reuse, do NOT greenfield):
//   • Nodes:    reuses the Village MineNode (WO-142/159) — its TryAutoExtract()
//               already banks one extract into GameState on the node's cooldown,
//               the SAME path Worker.cs and the player's [F] tap use. The pet adds
//               NO new currency and NO new banking path.
//   • Movement: reuses Pet's own movement. Pet.cs already drives a NavMeshAgent via
//               its eased MoveToward kinematics toward HomePost. We steer the pet by
//               re-anchoring HomePost (Pet.SetHomePost) to the node — exactly how
//               PetHeroLeash already drives the pet — so we ride the existing,
//               wall-safe NavMesh locomotion instead of moving the transform raw.
//   • Leash:   PetHeroLeash also writes HomePost every frame. To avoid a tug-of-war
//               we DISABLE the leash while harvesting and RE-ENABLE it when we stop,
//               so the pet's normal "wander near the hero" behaviour is restored
//               intact the moment no node is in range / a foe appears.
//   • Isolation: DeNelle.Pets cannot reference DeNelle.Village, so the node API is
//               reached through MineNodeBridge (reflection) — the same cross-asmdef
//               mechanism PetAttackVfxBridge / PetHeroLeash already use.
//
// COMBAT PRIORITY: a Defend pet that sees a hostile in its hunt range is left to
// Pet.cs's own hunt/attack loop — PetHarvester yields (returns to Idle) whenever
// the pet is in Defend mode AND a hostile is near, so defending is never starved.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Pets
{
    /// <summary>
    /// Drives the autonomous gather loop on a deployed <see cref="Pet"/>: find the
    /// nearest resource node, walk to it (by re-anchoring the pet's HomePost so the
    /// existing NavMesh locomotion carries it), harvest on a tick into the existing
    /// economy via <see cref="MineNodeBridge"/>, and fall back to the pet's normal
    /// leash / defend behaviour when no node is reachable or a foe appears.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Pet))]
    public sealed class PetHarvester : MonoBehaviour
    {
        private enum HarvestState { Idle, MovingToNode, Harvesting }

        [Header("Detection")]
        [Tooltip("How far the pet will look for a harvestable node (world units). " +
                 "Scanned on an interval, not per-frame.")]
        [SerializeField, Min(1f)] private float _detectRadius = 28f;

        [Tooltip("Seconds between node scans while idle (throttle — the scan does a " +
                 "FindObjectsOfType, so it must not run every frame).")]
        [SerializeField, Min(0.1f)] private float _scanInterval = 1.0f;

        [Header("Harvest")]
        [Tooltip("How close the pet must get to the node before it starts harvesting.")]
        [SerializeField, Min(0.5f)] private float _arriveRadius = 2.5f;

        [Tooltip("Seconds between harvest ticks while on station. Each ready tick " +
                 "drives MineNode.TryAutoExtract() (which respects the node's own " +
                 "extract cooldown), so a fast interval simply polls the node.")]
        [SerializeField, Min(0.25f)] private float _harvestInterval = 1.0f;

        [Tooltip("Units the pet can carry before it must 'deposit' and pick a new " +
                 "node. Banking is immediate per extract (MineNode → GameState), so " +
                 "this is a soft work-budget per node before the pet roams on — it " +
                 "keeps one pet from camping a single rich node forever.")]
        [SerializeField, Min(1)] private int _carryCapacity = 50;

        [Tooltip("Give up reaching a node after this long in MovingToNode (e.g. the " +
                 "node is off-NavMesh / unreachable) and rescan, so the pet never " +
                 "gets stuck walking at an unreachable node.")]
        [SerializeField, Min(1f)] private float _moveTimeout = 8f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private Pet _pet;
        private PetHeroLeash _leash;             // disabled while we drive HomePost
        private HarvestState _state = HarvestState.Idle;
        private MineNodeHandle _target;
        private float _nextScan;
        private float _nextHarvest;
        private float _moveDeadline;
        private int _carried;
        private bool _leashWasEnabled;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            _leash = GetComponent<PetHeroLeash>();
        }

        private void OnDisable()
        {
            // Releasing cleanly hands the pet back to its normal behaviour and frees
            // any node claim so a Worker (or another pet) can take it.
            StopHarvesting(restoreLeash: true);
        }

        private void Update()
        {
            if (_pet == null || !_pet.IsAlive) { StopHarvesting(true); return; }

            // Combat ALWAYS wins. A Defend pet with a hostile nearby is handed back
            // to Pet.cs's own hunt/attack loop — never starve defending to gather.
            if (ShouldYieldToCombat())
            {
                if (_state != HarvestState.Idle)
                {
                    FlowTrace.Step("PetHarvest", $"pet '{_pet.PetId}' YIELDING harvest to combat (Defend + hostile in range)");
                    StopHarvesting(restoreLeash: true);
                }
                return;
            }

            switch (_state)
            {
                case HarvestState.Idle:         TickIdle();        break;
                case HarvestState.MovingToNode: TickMovingToNode(); break;
                case HarvestState.Harvesting:   TickHarvesting();  break;
            }
        }

        // =====================================================================
        //  State machine
        // =====================================================================

        // Idle: not gathering — let the pet's normal behaviour run. Periodically
        // scan for a node; when one is found, claim it and begin moving.
        private void TickIdle()
        {
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + _scanInterval;

            if (!MineNodeBridge.Available) return;   // Village/MineNode not present.

            var node = MineNodeBridge.FindNearest(transform.position, _detectRadius);
            if (node == null) return;

            FlowTrace.Step("PetHarvest", $"pet '{_pet.PetId}' found node @ {node.Position} within {_detectRadius}m — moving to harvest");
            BeginMovingTo(node);
        }

        // MovingToNode: steer the pet to the node by anchoring its HomePost there
        // (Pet.cs's MoveToward drives the NavMeshAgent toward HomePost), with the
        // leash suspended so it doesn't fight us for HomePost. Arrive → harvest.
        private void TickMovingToNode()
        {
            if (!NodeStillWorkable()) { StopHarvesting(true); return; }

            // Keep re-anchoring (the node may be static, but re-asserting beats the
            // leash if it ever re-enables, and costs nothing).
            _pet.SetHomePost(_target.Position);

            float distSqr = (_target.Position - transform.position).sqrMagnitude;
            if (distSqr <= _arriveRadius * _arriveRadius)
            {
                FlowTrace.Step("PetHarvest", $"pet '{_pet.PetId}' arrived at node — begin harvesting");
                _state = HarvestState.Harvesting;
                _nextHarvest = Time.time;   // allow an immediate first tick
                return;
            }

            // Unreachable / stuck — give up and rescan from Idle.
            if (Time.time >= _moveDeadline)
            {
                FlowTrace.Warn("PetHarvest", $"pet '{_pet.PetId}' gave up reaching node after {_moveTimeout}s (off-NavMesh/unreachable) — rescan");
                StopHarvesting(restoreLeash: true);
            }
        }

        // Harvesting: on station, pull one extract per tick via the node's EXISTING
        // banking path. Stop when the node depletes/claims-out or the carry budget
        // is hit, then roam to a new node.
        private void TickHarvesting()
        {
            if (!NodeStillWorkable()) { StopHarvesting(true); return; }

            // Hold position at the node while working.
            _pet.SetHomePost(_target.Position);

            if (Time.time < _nextHarvest) return;
            _nextHarvest = Time.time + _harvestInterval;

            int banked = _target.TryAutoExtract();   // MineNode → GameState (one path)
            if (banked > 0)
            {
                _carried += banked;
                FlowTrace.Throttle("PetHarvest", "banked-" + _pet.PetId, 1f, $"pet '{_pet.PetId}' banked +{banked} (carried {_carried}/{_carryCapacity})");
                if (_carried >= _carryCapacity)
                {
                    // Soft budget reached — release this node and look for another so
                    // one pet doesn't camp a single node forever. (Yield is already
                    // banked per extract; nothing to "carry home".)
                    FlowTrace.Step("PetHarvest", $"pet '{_pet.PetId}' hit carry budget {_carryCapacity} — releasing node to roam");
                    _carried = 0;
                    StopHarvesting(restoreLeash: false);   // go idle → rescan next frame
                    _nextScan = 0f;
                }
            }
            // banked == 0 ⇒ node on cooldown; just wait for the next tick.
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private void BeginMovingTo(MineNodeHandle node)
        {
            _target = node;
            _target.SetClaim(true);                  // reuse MineNode's worker-claim seam
            SuspendLeash();
            _carried = 0;
            _moveDeadline = Time.time + _moveTimeout;
            _state = HarvestState.MovingToNode;
        }

        // Returns true while the current target node still exists, isn't depleted,
        // and is still ours to work.
        private bool NodeStillWorkable()
        {
            return _target != null && _target.IsValid && !_target.IsDepleted;
        }

        /// <summary>
        /// Stop the gather loop: release the node claim, clear the target, and
        /// (optionally) restore the leash so the pet resumes its normal
        /// wander-near-hero / return-to-post behaviour. Always returns the pet to
        /// the Idle harvest state.
        /// </summary>
        private void StopHarvesting(bool restoreLeash)
        {
            if (_target != null)
            {
                FlowTrace.Step("PetHarvest", $"pet '{(_pet != null ? _pet.PetId : "<null>")}' stop harvest — released node claim (restoreLeash={restoreLeash})");
                _target.SetClaim(false);
                _target = null;
            }
            _state = HarvestState.Idle;
            if (restoreLeash) RestoreLeash();
        }

        // The pet's normal leash drives HomePost every frame; suspend it so we own
        // HomePost while harvesting, and remember whether it was enabled so we can
        // faithfully restore it (don't force-enable a deliberately-off leash).
        private void SuspendLeash()
        {
            if (_leash == null) _leash = GetComponent<PetHeroLeash>();
            if (_leash != null && _leash.enabled)
            {
                // WO-1014 Half B (instrumentation only): disabling the leash is one of the
                // four ways the FTUE guide-lead can silently do nothing — the anchor is set
                // but this pet no longer consumes it. Named at the moment it happens.
                if (PetHeroLeash.IsLeading)
                    FlowTrace.Warn("Pets",
                        $"PetHarvester SUSPENDING the leash on '{(_pet != null ? _pet.PetId : "<null>")}' WHILE " +
                        $"A GUIDE LEAD IS ACTIVE (anchor {PetHeroLeash.LeadTarget}) — harvesting takes over " +
                        "HomePost, so the tutorial's lead anchor stops reaching this pet until RestoreLeash.");
                _leashWasEnabled = true;
                _leash.enabled = false;
            }
        }

        private void RestoreLeash()
        {
            if (_leash != null && _leashWasEnabled)
            {
                _leash.enabled = true;
                // Re-anchor HomePost to the pet's own spot so it doesn't snap back
                // to the (now stale) node position before the leash's next frame.
                _pet.SetHomePost(transform.position);
            }
            _leashWasEnabled = false;
        }

        // Combat priority: only Defend pets fight, and only when a hostile is near.
        // We can't see Pet's private NearestHostile(), but it scans the same enemy
        // mask; rather than duplicate that, we yield whenever the pet is in Defend
        // mode and there is ANY hostile within the pet's own hunt range — checked
        // cheaply via the shared combat helper on Pet (PetHarvester only needs a
        // yes/no). Pet.HasHostileInRange exposes that.
        private bool ShouldYieldToCombat()
        {
            // PET COMBAT GATE (owner 2026-07-08, ff.petcombat default OFF): with combat gated off the
            // pet never fights (Pet.Update no-ops the hunt/attack), so harvesting must NOT yield to a
            // hostile it can't engage — otherwise the pet would freeze near an enemy instead of gathering.
            if (!DeNelle.Core.FeatureFlags.PetCombat) return false;
            return _pet.Mode == PetMode.Defend && _pet.HasHostileInRange;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 1f, 0.55f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _detectRadius);
            if (_target != null && _target.IsValid)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, _target.Position);
            }
        }
#endif
    }
}
