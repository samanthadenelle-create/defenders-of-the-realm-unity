// =============================================================================
// LoreStone — a tap-to-read lore interactable (Week 6).
// -----------------------------------------------------------------------------
// Port spec Part 3 row:
//   src/modules/dungeons/lore-stones/ -> _Modules/Dungeons/LoreStone.cs + LoreStoneModal.uxml
// Port spec Part 5 Week 6: "tap-to-read interactable. Modal (UI Toolkit) shows
// the lore text. Each lore-stone ID maps to a fragment in data/lore-fragments.json."
//
// C# port of src/modules/dungeons/3d/interactables/LoreStone3D.tsx. A reading
// stand the Keeper taps to read a fragment of Alduin's journal. The Healer's
// Cottage has four — journal entries 1-4, one per main beat. Reading all four
// advances "The Healer's Garden" questline (storyline §4.1 beat 3).
//
// ── Canon ──
// The lore prose is CANON — verbatim from docs/dungeon-3d-healers-cottage-design.md
// and docs/dungeons-storyline.md, carried in the layout JSON (which mirrors
// data/lore-fragments.json). This MonoBehaviour NEVER authors or paraphrases
// lore copy; it only displays the strings handed to it via Configure().
//
// The UI Toolkit reading modal (LoreStoneModal.uxml) is a sibling deliverable;
// this MonoBehaviour raises a typed request event the HUD module's modal
// controller subscribes to — keeping the dungeon module free of a HUD import.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// The payload of a lore-read request — handed to the UI Toolkit reading
    /// modal. Carries the canon prose verbatim; the modal only renders it.
    /// </summary>
    [Serializable]
    public sealed class LoreReadRequest
    {
        /// <summary>The lore-stone id (e.g. <c>journal-1</c>) — keys lore-fragments.json.</summary>
        public string LoreStoneId;
        /// <summary>The fragment heading, e.g. "Alduin's journal — entry 1".</summary>
        public string Title;
        /// <summary>The fragment body — one paragraph per array entry. Canon prose.</summary>
        public string[] Body;
    }

    /// <summary>
    /// A tap-to-read lore stone. Becomes interactable when the Keeper is within
    /// <see cref="_interactRadius"/>; on tap it raises <see cref="ReadRequested"/>
    /// for the HUD's reading modal and records the read on the run state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoreStone : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("World-unit distance within which the Keeper can tap to read.")]
        [SerializeField] private float _interactRadius = 2.5f;

        [Tooltip("Optional prompt object shown while the Keeper is in range " +
                 "(a world-space 'Read' pill). Toggled on/off by proximity.")]
        [SerializeField] private GameObject _interactPrompt;

        [Header("Events")]
        [Tooltip("Raised when the Keeper taps an in-range lore stone — the HUD's " +
                 "reading modal subscribes and shows the fragment.")]
        public UnityEvent<LoreReadRequest> ReadRequested = new UnityEvent<LoreReadRequest>();

        // ── Runtime data (handed in via Configure) ───────────────────────────

        private DungeonLoreStone _def;
        private DungeonRuntimeState _runtimeState;
        private Transform _hero;
        private int _totalLoreCount;

        /// <summary>True while the Keeper is within reach of this stone.</summary>
        public bool InRange { get; private set; }

        /// <summary>True once this stone's fragment has been read this run.</summary>
        public bool HasBeenRead =>
            _runtimeState != null && _def != null && _runtimeState.HasReadLore(_def.id);

        /// <summary>The lore-stone id — e.g. <c>journal-1</c>.</summary>
        public string LoreStoneId => _def?.id ?? string.Empty;

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Wires the lore stone to its authored definition + the run state.
        /// Positions the transform at the layout's coordinates. Called by the
        /// dungeon scene builder against the layout JSON.
        /// </summary>
        public void Configure(
            DungeonLoreStone def,
            DungeonRuntimeState runtimeState,
            Transform hero,
            int totalLoreCount)
        {
            _def = def;
            _runtimeState = runtimeState;
            _hero = hero;
            _totalLoreCount = totalLoreCount;

            if (_def != null)
                transform.position = _def.position.ToWorld();

            if (_interactPrompt != null) _interactPrompt.SetActive(false);
        }

        // ── Per-frame proximity ──────────────────────────────────────────────

        private void Update()
        {
            if (_def == null || _hero == null) return;

            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = _hero.position; b.y = 0f;
            bool nowInRange = (a - b).sqrMagnitude <= _interactRadius * _interactRadius;

            if (nowInRange != InRange)
            {
                InRange = nowInRange;
                // The prompt only shows for an unread stone in range — a read
                // stone gives no further prompt (it can still be re-tapped).
                if (_interactPrompt != null)
                    _interactPrompt.SetActive(InRange && !HasBeenRead);
            }
        }

        // ── Interaction ──────────────────────────────────────────────────────

        /// <summary>
        /// Reads the stone — raises <see cref="ReadRequested"/> with the canon
        /// fragment and records the read on the run state. The input layer
        /// (tap / interact key) calls this only when <see cref="InRange"/>.
        /// First read of all four stones fires the questline-beat event on the
        /// run state (advances "The Healer's Garden").
        /// </summary>
        public void Read()
        {
            if (_def == null || !InRange) return;

            ReadRequested.Invoke(new LoreReadRequest
            {
                LoreStoneId = _def.id,
                Title = _def.title,
                Body = _def.body,
            });

            // Record the read — drives the questline-beat completion check.
            _runtimeState?.ReadLoreStone(_def.id, _totalLoreCount);

            // A read stone no longer prompts.
            if (_interactPrompt != null) _interactPrompt.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
