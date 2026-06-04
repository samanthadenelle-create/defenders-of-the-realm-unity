// =============================================================================
// MineNode — a harvestable resource node in the outer world (WO-142 + the owner's
// "add mine nodes" step). Player walks up, presses [F], extracts; the node banks
// the yield into GameState and goes on cooldown / depletes.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Lean + self-contained: it does NOT depend on the (still-unbuilt) WO-141
// ResourceNode SO pipeline — it banks directly into GameState (Iron/Wood/Food/
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
    /// <summary>What a mine node yields. Maps 1:1 to a GameState wallet field.
    /// DEF-121 / WO-230: the four harvestables are Wood / Food / Iron / Crystals.
    /// Food replaces the retired "Stone" harvest axis and banks into
    /// GameState.Resources.Food (the existing wallet field) — Stone is no longer a
    /// player-facing harvestable (Magic is a building-upgrade tech axis, not a node).</summary>
    public enum MineResource { Iron, Wood, Food, AetherCrystal }

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

        [Tooltip("Seconds to respawn after depletion. 0 = never respawns. " +
                 "Ignored when UseFiniteReserve is on (a finite reserve never respawns — " +
                 "the node despawns when mined empty, per WO-159).")]
        [Min(0f)] public float RespawnSeconds = 60f;

        // ── WO-159 — finite-reserve reframe (additive, opt-in) ────────────────
        [Header("Finite reserve (WO-159)")]
        [Tooltip("When ON the node is a persistent FINITE RESERVE (territory-control model): " +
                 "it holds ReserveTotal units, persists in the world until mined empty, then " +
                 "DESPAWNS (no cooldown-respawn). A player-built Settlement auto-drains it. " +
                 "When OFF the node keeps the legacy extract-count + cooldown-respawn model " +
                 "so existing scenes / the worker demo are unchanged.")]
        public bool UseFiniteReserve = false;

        [Tooltip("Total reserve units the node holds (e.g. 500 iron). Drained by a settlement " +
                 "over time; when it hits 0 the node despawns. Region/danger scales this " +
                 "(deadlier region = richer reserve) via ReserveTotalScaled.")]
        [Min(1)] public int ReserveTotal = 500;

        [Tooltip("If true the node GameObject is destroyed when the reserve empties (the " +
                 "node 'vanishes' per WO-159 — the settlement remains). If false it stays as " +
                 "a spent marker (useful in the editor).")]
        public bool DespawnOnEmpty = true;

        private int  _reserveRemaining;
        private bool _reserveInitialised;

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
            InitReserve();
            var p = GameObject.FindWithTag("Player");
            _player = p != null ? p.transform : null;
        }

        // =====================================================================
        // WO-159 — finite reserve seam. The node is a depleting reserve worked by
        // a Settlement (StructureFactory-placed via build-mode), NOT a per-tap
        // clicker. The reserve is region/danger-scaled (deadlier region = richer)
        // sharing the same danger⇄reward dial as the per-extract yield bonus.
        // Drain reuses BankYield() so there is ONE banking path into GameState.
        // =====================================================================

        private void InitReserve()
        {
            if (_reserveInitialised) return;
            _reserveInitialised = true;
            _reserveRemaining = ReserveTotalScaled;
        }

        /// <summary>Region/danger-scaled total reserve (deadlier region = richer node):
        /// <c>ReserveTotal × (1 + 0.25 × dangerTier)</c> — mirrors the per-extract bonus dial.</summary>
        public int ReserveTotalScaled
        {
            get
            {
                int tier = Mathf.Max(0, ZoneManager.DangerTierAt(transform.position));
                return Mathf.RoundToInt(ReserveTotal * (1f + 0.25f * tier));
            }
        }

        /// <summary>Reserve units still in the ground (finite-reserve model). 0 = mined out.</summary>
        public int ReserveRemaining
        {
            get { InitReserve(); return Mathf.Max(0, _reserveRemaining); }
        }

        /// <summary>0..1 fill of the finite reserve (for the world UI / settlement readout).</summary>
        public float ReserveFraction
        {
            get
            {
                InitReserve();
                int total = ReserveTotalScaled;
                return total <= 0 ? 0f : Mathf.Clamp01((float)_reserveRemaining / total);
            }
        }

        /// <summary>True once a finite-reserve node has been mined empty (WO-159).</summary>
        public bool IsReserveEmpty => UseFiniteReserve && ReserveRemaining <= 0;

        /// <summary>
        /// WO-159 settlement drain: pull up to <paramref name="amount"/> units from the
        /// reserve into the player's wallet (the auto-harvest faucet — no manual tap).
        /// Returns the amount actually banked (clamped to what's left). When the reserve
        /// empties the node despawns (DespawnOnEmpty) — the settlement remains. Only does
        /// anything when UseFiniteReserve is on; otherwise returns 0 so the legacy worker
        /// path stays the sole banking route. Reuses BankYield (one GameState path).
        /// </summary>
        public int DrainReserve(int amount)
        {
            if (!UseFiniteReserve || amount <= 0) return 0;
            InitReserve();
            if (_reserveRemaining <= 0) return 0;

            int banked = Mathf.Min(amount, _reserveRemaining);
            _reserveRemaining -= banked;
            BankYield(banked);

            if (_reserveRemaining <= 0)
            {
                _depleted = true;   // also satisfies IsDepleted for any worker watcher
                Debug.Log($"[MineNode] Reserve mined empty ({Resource}) — node despawns; settlement remains.");
                if (DespawnOnEmpty) Destroy(gameObject);
            }
            return banked;
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
            (UseFiniteReserve || _depleted || ExtractCooldown <= 0f) ? 0f : EffectiveYield / ExtractCooldown;

        /// <summary>Claim (or release) the node for a worker. Idempotent.</summary>
        public void SetWorkerClaim(bool claimed) => _claimedByWorker = claimed;

        /// <summary>Worker-driven collect. Banks one extract IF the node is ready
        /// (not depleted, cooldown elapsed); returns the amount banked (0 if not ready).
        /// Reuses the exact same Extract() path as the manual [F] tap, so there is no
        /// second banking code path to keep in sync.</summary>
        public int TryAutoExtract()
        {
            // Finite-reserve nodes (WO-159) are drained by a Settlement via DrainReserve,
            // not the legacy worker/cooldown path — keep ONE banking route per node.
            if (UseFiniteReserve) return 0;
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
            if (UseFiniteReserve) return 0;   // settlement-drained; see DrainReserve.
            if (_depleted) return 0;
            int before = EffectiveYield;
            Extract();          // advances depletion + sets the next live cooldown
            return before;
        }

        private void Update()
        {
            // WO-159 finite-reserve nodes are worked by a Settlement (DrainReserve),
            // NOT the per-tap [F] verb or the worker cooldown loop — so the legacy
            // Update interaction is skipped entirely for them. The node just sits as a
            // persistent reserve until a settlement drains it empty.
            if (UseFiniteReserve) return;

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

            // DEF-203: register the shared on-screen Interact button while in range and
            // off cooldown so touch/mobile (no keyboard) can extract too. Desktop F kept.
            if (inRange && _cooldown <= 0f)
                MobileInteractButton.Request(this, "Mine " + Resource, Extract);
            else
                MobileInteractButton.Release(this);

            if (inRange && _cooldown <= 0f &&
                (UnityEngine.Input.GetKeyDown(KeyCode.F) ||
                 (Keyboard_FPressed())))
            {
                Extract();
            }
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
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
                case MineResource.Food:
                {
                    // Food banks into the wallet (Resources.Food) — a struct, so
                    // read-whole / mutate / write-whole back.
                    var bal = state.Resources;
                    bal.Food += amount;
                    state.Resources = bal;
                    break;
                }
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
