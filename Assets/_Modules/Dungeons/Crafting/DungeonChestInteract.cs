// =============================================================================
// DungeonChestInteract — the WO-749 treasure-chest interact + reward grant.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// Before WO-749 a DungeonChest was placed as a VISUAL only (DungeonSceneBuilder
// "Chest_{id}") with no behaviour, and DungeonRuntimeState.OpenChest granted
// NOTHING — DungeonChest.rewardKey was read by no C#. This component closes that
// gap: the Keeper walking into the chest opens it ONCE per run (deduped by
// DungeonRuntimeState.OpenChest) and resolves rewardKey -> a loot roll banked in
// the persistent village larder via DungeonLootGrant.
//
// Idiom: proximity auto-open, the SAME pattern as IngredientPickup / Checkpoint —
// an XZ-plane distance check against the hero, firing once on first cross. Wired
// at runtime by DungeonController.HydrateChests (no scene bake). ASCII only.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using DeNelle.Core.Diagnostics;   // FlowTrace (§12)

namespace DeNelle.Dungeons
{
    /// <summary>
    /// A treasure-chest interactable — opens once on hero proximity and resolves
    /// its authored <see cref="DungeonChest.rewardKey"/> into a larder loot grant
    /// (WO-749). Deduped by <see cref="DungeonRuntimeState.OpenChest"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonChestInteract : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("World-unit radius within which the Keeper auto-opens this chest.")]
        [SerializeField] private float _openRadius = 2.4f;

        [Header("Events")]
        [Tooltip("Raised with the rewardKey the moment the chest is first opened.")]
        public UnityEvent<string> Opened = new UnityEvent<string>();

        private DungeonChest _def;
        private DungeonRuntimeState _state;
        private Transform _hero;
        private bool _opened;

        /// <summary>The chest's stable id, or empty before configuration.</summary>
        public string ChestId => _def?.id ?? string.Empty;

        /// <summary>True once this chest has been opened (this run or a prior resume).</summary>
        public bool IsOpened => _opened;

        /// <summary>
        /// Wires the interactable to its authored chest def, the run state (for the
        /// open-once dedupe) and the hero it watches. A chest already opened this run
        /// (an ATB-resume reload) starts settled so it never re-grants.
        /// </summary>
        public void Configure(DungeonChest def, DungeonRuntimeState state, Transform hero)
        {
            _def = def;
            _state = state;
            _hero = hero;
            _opened = _def != null && _state != null && _state.HasOpenedChest(_def.id);
        }

        private void Update()
        {
            if (_opened || _def == null || _hero == null || _state == null) return;

            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = _hero.position; b.y = 0f;
            if ((a - b).sqrMagnitude > _openRadius * _openRadius) return;

            TryOpen();
        }

        private void TryOpen()
        {
            // OpenChest returns true only on the FIRST open this run (deduped).
            bool firstOpen = _state.OpenChest(_def.id);
            _opened = true;
            if (!firstOpen)
            {
                FlowTrace.Step("DungeonLoot",
                    $"chest '{_def.id}' already opened this run — no re-grant.");
                return;
            }

            FlowTrace.Step("DungeonLoot",
                $"chest '{_def.id}' opened (rewardKey='{_def.rewardKey}') — resolving loot to larder.");
            DungeonLootGrant.GrantChest(_def.rewardKey);
            Opened.Invoke(_def.rewardKey ?? string.Empty);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.70f, 0.25f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _openRadius);
        }
    }
}
