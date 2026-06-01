// =============================================================================
// MineNode — a harvestable resource node in the outer world (WO-142 + the owner's
// "add mine nodes" step). Player walks up, presses [F], extracts; the node banks
// the yield into GameState and goes on cooldown / depletes.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Lean + self-contained: it does NOT depend on the (still-unbuilt) WO-141
// ResourceNode SO pipeline — it banks directly into GameState (Iron/Wood/Stone/
// AetherCrystals) the same way the rest of the economy does, so it works today.
// When WO-141's full HarvestNodeData lands, MineNode can be folded into it.
//
// Region-aware: the node reads ZoneManager.DangerTierAt(position) so a designer
// can scale yield by region danger (deadlier region = richer node) — the
// danger=reward spine shared with raids (WO-143) and crystal grades (WO-144).
// =============================================================================
using UnityEngine;
using DeNelle.Core.World;

namespace DeNelle.Village
{
    /// <summary>What a mine node yields. Maps 1:1 to a GameState wallet field.</summary>
    public enum MineResource { Iron, Wood, Stone, AetherCrystal }

    [DisallowMultipleComponent]
    public sealed class MineNode : MonoBehaviour
    {
        [Header("Yield")]
        [Tooltip("Which resource this node banks into GameState.")]
        public MineResource Resource = MineResource.Iron;

        [Tooltip("Base amount granted per extract (before the region danger bonus).")]
        [Min(1)] public int YieldPerExtract = 5;

        [Tooltip("Seconds before the node can be extracted again.")]
        [Min(0f)] public float ExtractCooldown = 8f;

        [Tooltip("Total extracts before the node depletes. 0 = infinite.")]
        [Min(0)] public int TotalExtracts = 6;

        [Tooltip("Seconds to respawn after depletion. 0 = never respawns.")]
        [Min(0f)] public float RespawnSeconds = 60f;

        [Header("Interaction")]
        [Tooltip("How close the player must be to press [F].")]
        [Min(0.5f)] public float InteractRadius = 2.5f;

        private float _cooldown;
        private int   _extractsLeft;
        private float _respawnTimer;
        private bool  _depleted;
        private Transform _player;
        private bool  _claimedByWorker;

        private void Awake()
        {
            _extractsLeft = TotalExtracts;
            var p = GameObject.FindWithTag("Player");
            _player = p != null ? p.transform : null;
        }

        // =====================================================================
        // WO-117 — auto-collect seam. A dispatched Worker drives the SAME extract
        // path the player's [F] uses (one banking source of truth — no parallel
        // economy). The worker claims the node, then ticks TryAutoExtract() on the
        // node's own cooldown; extraction reuses Extract() so yield, region-danger
        // bonus, cooldown and depletion all behave identically to a manual tap.
        // =====================================================================

        /// <summary>True once a worker has claimed this node for auto-collect.
        /// A second worker should not be dispatched to a claimed node.</summary>
        public bool IsClaimedByWorker => _claimedByWorker;

        /// <summary>True when the node has run out of extracts (waiting to respawn,
        /// or permanently spent if RespawnSeconds == 0). No further yield until it
        /// respawns.</summary>
        public bool IsDepleted => _depleted;

        /// <summary>Seconds remaining before the next extract is allowed. 0 = ready.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldown);

        /// <summary>Extracts remaining before depletion (TotalExtracts==0 ⇒ infinite,
        /// reported as int.MaxValue). Read-only progress for the fill indicator.</summary>
        public int ExtractsRemaining => TotalExtracts <= 0 ? int.MaxValue : Mathf.Max(0, _extractsLeft);

        /// <summary>0..1 fill toward depletion for the world UI. Infinite nodes report 1
        /// (always "full of resource"). Depleted reports 0.</summary>
        public float ExtractFraction
        {
            get
            {
                if (_depleted) return 0f;
                if (TotalExtracts <= 0) return 1f;
                return Mathf.Clamp01((float)_extractsLeft / TotalExtracts);
            }
        }

        /// <summary>The node's effective yield-per-extract right now, including the
        /// region danger bonus. Read-only — used by offline catch-up and UI.</summary>
        public int EffectiveYield
        {
            get
            {
                int tier = Mathf.Max(0, ZoneManager.DangerTierAt(transform.position));
                return Mathf.RoundToInt(YieldPerExtract * (1f + 0.25f * tier));
            }
        }

        /// <summary>Average resource banked per second when a worker is on station
        /// (one extract every ExtractCooldown). The read-only rate the offline-accrual
        /// seam (WorkerManager.ActiveAssignments) integrates while the player is away.</summary>
        public float RatePerSecond =>
            (_depleted || ExtractCooldown <= 0f) ? 0f : EffectiveYield / ExtractCooldown;

        /// <summary>Claim (or release) the node for a worker. Idempotent.</summary>
        public void SetWorkerClaim(bool claimed) => _claimedByWorker = claimed;

        /// <summary>Worker-driven collect. Banks one extract IF the node is ready
        /// (not depleted, cooldown elapsed); returns the amount banked (0 if not ready).
        /// Reuses the exact same Extract() path as the manual [F] tap, so there is no
        /// second banking code path to keep in sync.</summary>
        public int TryAutoExtract()
        {
            if (_depleted || _cooldown > 0f) return 0;
            int before = EffectiveYield;
            Extract();
            return before;
        }

        /// <summary>Offline catch-up collect — banks one extract ignoring the LIVE
        /// cooldown (the elapsed offline time already "paid" it), but still respects
        /// depletion so an offline node runs dry exactly as a live one would. Returns
        /// the amount banked (0 if depleted). Used only by WorkerManager's offline
        /// integration; live collection uses TryAutoExtract().</summary>
        public int ForceAutoExtract()
        {
            if (_depleted) return 0;
            int before = EffectiveYield;
            Extract();          // advances depletion + sets the next live cooldown
            return before;
        }

        private void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;

            if (_depleted)
            {
                if (RespawnSeconds <= 0f) return;
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f) { _depleted = false; _extractsLeft = TotalExtracts; }
                return;
            }

            if (_player == null)
            {
                var p = GameObject.FindWithTag("Player");
                _player = p != null ? p.transform : null;
                if (_player == null) return;
            }

            bool inRange = (_player.position - transform.position).sqrMagnitude
                           <= InteractRadius * InteractRadius;
            if (inRange && _cooldown <= 0f &&
                (UnityEngine.Input.GetKeyDown(KeyCode.F) ||
                 (Keyboard_FPressed())))
            {
                Extract();
            }
        }

        // New Input System safe-check without a hard dependency: fall back to legacy
        // Input above; this returns false if the new system isn't the active path.
        private static bool Keyboard_FPressed() => false;

        private void Extract()
        {
            int tier = Mathf.Max(0, ZoneManager.DangerTierAt(transform.position));
            // Region danger bonus: +25% yield per danger tier (Goldfields ×1.25 …
            // Ashwood ×2.0). Danger = reward.
            int amount = Mathf.RoundToInt(YieldPerExtract * (1f + 0.25f * tier));

            BankYield(amount);
            _cooldown = ExtractCooldown;

            if (TotalExtracts > 0)
            {
                _extractsLeft--;
                if (_extractsLeft <= 0)
                {
                    _depleted = true;
                    _respawnTimer = RespawnSeconds;
                }
            }

            Debug.Log($"[MineNode] +{amount} {Resource} (tier {tier}) — {_extractsLeft} left" +
                      (_depleted ? ", depleted." : "."));
        }

        // Core can't reference Village; we reach GameState by reflection-free direct
        // call IF the type is accessible. GameState lives in DeNelle.Core.State and
        // DeNelle.Village references DeNelle.Core, so the direct path is valid.
        private void BankYield(int amount)
        {
            var state = DeNelle.Core.State.GameStateService.Instance != null
                ? DeNelle.Core.State.GameStateService.Instance.State : null;
            if (state == null)
            {
                Debug.LogWarning("[MineNode] GameStateService.Instance.State null — yield dropped.");
                return;
            }
            switch (Resource)
            {
                case MineResource.Iron:          state.Iron += amount;          break;
                case MineResource.Wood:          state.Wood += amount;          break;
                case MineResource.Stone:         state.Stone += amount;         break;
                case MineResource.AetherCrystal: state.AetherCrystals += amount; break;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 0.95f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, InteractRadius);
        }
#endif
    }
}
