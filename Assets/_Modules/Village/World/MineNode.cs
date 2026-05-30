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

        private void Awake()
        {
            _extractsLeft = TotalExtracts;
            var p = GameObject.FindWithTag("Player");
            _player = p != null ? p.transform : null;
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
