// =============================================================================
// HeartController — Elarion, the world-tree at the village centre (Week-3 skel).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/heart/ -> HeartController.cs.
//
// "Elarion" / "the Heart" sits at world origin, scaled up ~2x (port spec Part 5
// Week 3). It is both the emotional anchor (the tree suffers + recovers with
// village HP) and the threat indicator (the embedded crystal's violet emissive
// reads serene -> vigilant -> warning -> danger -> critical -> boss).
//
// Week-3 depth: structure + serialized fields + the seven-state table from
// docs/heart-build-spec.md section 3. The crystal-veined emissive shader, the
// per-frame pulse lerp, deriveHeartState(), the leaf-fall particles all land
// later per heart-build-spec.md's 9-step implementation order.
//
// CANON: the tree is named Elarion and is referred to as "the Heart". Those
// strings are NOT typed inline in user-facing copy -- they flow through
// data/canon-strings.json (port spec Part 4). This file only uses them in
// code comments / non-user-facing identifiers.
// =============================================================================

using System;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The Heart's threat state. Verbatim port of the <c>HeartState</c> union
    /// in <c>heart-build-spec.md</c> section 1.
    /// </summary>
    public enum HeartState
    {
        /// <summary>No threat. Default.</summary>
        Serene = 0,
        /// <summary>Wave active, but well-defended.</summary>
        Vigilant = 1,
        /// <summary>At least one lane stressed OR HP 50-75%.</summary>
        Warning = 2,
        /// <summary>Multiple lanes stressed OR HP 25-50%.</summary>
        Danger = 3,
        /// <summary>HP &lt; 25% OR an enemy is within 3u of the Heart.</summary>
        Critical = 4,
        /// <summary>Boss wave active.</summary>
        Boss = 5,
        /// <summary>Wave or all-waves cleared. Brief celebratory state.</summary>
        Victorious = 6,
    }

    /// <summary>
    /// One row of the Heart's state table -- crystal colour, emissive, pulse.
    /// Verbatim port of <c>STATE_VISUALS</c> in <c>heart-build-spec.md</c>
    /// section 3 / section 6.
    /// </summary>
    [Serializable]
    public struct HeartStateVisual
    {
        /// <summary>Crystal colour for this state.</summary>
        public Color Color;
        /// <summary>Base emissive intensity multiplier.</summary>
        public float Emissive;
        /// <summary>Pulse frequency (Hz).</summary>
        public float PulseHz;
        /// <summary>Pulse depth (fraction of the base emissive).</summary>
        public float PulseDepth;
        /// <summary>Glow-halo billboard opacity.</summary>
        public float HaloOpacity;
    }

    /// <summary>
    /// Elarion -- the world-tree at the village centre. Mounts at world origin,
    /// scaled up ~2x. Holds the threat-state machine + visual signature table;
    /// the per-frame pulse / particle work is Week 4+ (heart-build-spec.md
    /// implementation order). Instantiated by <see cref="VillageController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeartController : MonoBehaviour
    {
        [Header("Threat state")]
        [Tooltip("Current threat state. Week 3 = always Serene (no combat in the skeleton).")]
        [SerializeField] private HeartState _state = HeartState.Serene;

        [Tooltip("Heart HP 0-100. Drives the tree's emotional state (leaf colour, bark cracks).")]
        [SerializeField, Range(0f, 100f)] private float _hp = 100f;

        [Header("Scene refs (wired by VillageController)")]
        [Tooltip("The embedded crystal mesh -- the glanceable threat readout.")]
        [SerializeField] private Renderer _crystalRenderer;

        [Tooltip("The tree trunk + canopy renderer -- recolours with HP.")]
        [SerializeField] private Renderer _treeRenderer;

        [Header("Tuning")]
        [Tooltip("Uniform scale applied to the Heart at the village centre (~2x per port spec Week 3).")]
        [SerializeField] private float _heartScale = 2f;

        [Tooltip("Seconds the colour / emissive eases between states (heart-build-spec.md section 3).")]
        [SerializeField] private float _stateEaseSeconds = 0.6f;

        [Tooltip("When true, Awake() leaves the transform exactly as authored in the scene. " +
                 "Set by the village scene builder when Elarion is hosted off-centre + " +
                 "self-scaled per docs/avalon-village-layout-spec.md section 3.3.")]
        [SerializeField] private bool _useAuthoredTransform;

        /// <summary>The Heart's default state colour -- violet (#7C3AED-ish, port spec Week 3).</summary>
        public static readonly Color SereneViolet = new Color(0.486f, 0.227f, 0.929f); // #7C3AED

        // The seven-state visual table -- verbatim from heart-build-spec.md section 3.
        // Colours: serene #a78bfa, vigilant #7dd3fc, warning #facc15, danger #f97316,
        // critical #dc2626, boss #7e22ce, victorious #ffffff.
        private static readonly HeartStateVisual[] StateVisuals =
        {
            new HeartStateVisual { Color = Hex("a78bfa"), Emissive = 1.0f, PulseHz = 0.4f, PulseDepth = 0.10f, HaloOpacity = 0.50f }, // Serene
            new HeartStateVisual { Color = Hex("7dd3fc"), Emissive = 1.1f, PulseHz = 0.6f, PulseDepth = 0.15f, HaloOpacity = 0.55f }, // Vigilant
            new HeartStateVisual { Color = Hex("facc15"), Emissive = 1.2f, PulseHz = 1.0f, PulseDepth = 0.20f, HaloOpacity = 0.65f }, // Warning
            new HeartStateVisual { Color = Hex("f97316"), Emissive = 1.4f, PulseHz = 1.6f, PulseDepth = 0.28f, HaloOpacity = 0.75f }, // Danger
            new HeartStateVisual { Color = Hex("dc2626"), Emissive = 1.7f, PulseHz = 2.4f, PulseDepth = 0.40f, HaloOpacity = 0.90f }, // Critical
            new HeartStateVisual { Color = Hex("7e22ce"), Emissive = 1.5f, PulseHz = 0.5f, PulseDepth = 0.35f, HaloOpacity = 0.85f }, // Boss
            new HeartStateVisual { Color = Hex("ffffff"), Emissive = 2.0f, PulseHz = 0.5f, PulseDepth = 0.00f, HaloOpacity = 1.00f }, // Victorious
        };

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fired whenever <see cref="Hp"/> changes (including after a SetHp call
        /// whose value is clamped to no-op). Subscribers receive the new HP (0-100).
        /// <para>
        /// DEF-54: four systems were forced to poll <c>Hp</c> every frame (TowerAudioController,
        /// TowerRepairVisuals, HeartHudBridge, TowerVoiceController) because no change
        /// notification existed. Subscribe here and unsubscribe in OnDisable/OnDestroy
        /// to avoid stale-delegate leaks.
        /// </para>
        /// </summary>
        public event System.Action<float> OnHealthChanged;

        // ── Properties ───────────────────────────────────────────────────────

        /// <summary>Current threat state of the Heart.</summary>
        public HeartState State => _state;

        /// <summary>Heart HP, 0-100.</summary>
        public float Hp => _hp;

        /// <summary>Uniform scale the Heart stands at (~2x).</summary>
        public float HeartScale => _heartScale;

        /// <summary>The visual signature (colour / emissive / pulse) for the current state.</summary>
        public HeartStateVisual CurrentVisual => GetVisual(_state);

        /// <summary>Looks up the visual signature for any <see cref="HeartState"/>.</summary>
        public static HeartStateVisual GetVisual(HeartState state)
        {
            int i = Mathf.Clamp((int)state, 0, StateVisuals.Length - 1);
            return StateVisuals[i];
        }

        /// <summary>
        /// Sets the threat state. Week 4+ this kicks off the 600ms colour /
        /// emissive ease; Week 3 it just records the value.
        /// </summary>
        public void SetState(HeartState state)
        {
            _state = state;
        }

        /// <summary>
        /// Sets Heart HP (0-100). Clamps the value, fires <see cref="OnHealthChanged"/>
        /// on any change, and auto-derives the threat state from the new HP level so
        /// the crystal colour / pulse track HP without external coordination.
        /// <para>
        /// WaveManager's explicit <see cref="SetState"/> calls (Boss, Victorious,
        /// Breached) still override this derivation — they run AFTER combat events
        /// and are not triggered by HP changes, so there is no race.
        /// </para>
        /// </summary>
        public void SetHp(float hp)
        {
            float next = Mathf.Clamp(hp, 0f, 100f);
            if (Mathf.Approximately(next, _hp)) return;   // no-op guard — avoids spurious events
            _hp = next;
            OnHealthChanged?.Invoke(_hp);
            // Auto-derive a baseline state from HP so the crystal always tracks health.
            // Boss / Victorious / Breached are set explicitly by WaveManager and should
            // NOT be stomped here — only derive when in a "normal" HP-driven state.
            if (_state != HeartState.Boss && _state != HeartState.Victorious)
                _state = DeriveStateFromHp(_hp);
        }

        /// <summary>Maps Heart HP (0-100) to a baseline <see cref="HeartState"/>.</summary>
        private static HeartState DeriveStateFromHp(float hp)
        {
            if (hp < 25f) return HeartState.Critical;
            if (hp < 50f) return HeartState.Danger;
            if (hp < 75f) return HeartState.Warning;
            return HeartState.Vigilant;
        }

        private void Awake()
        {
            // Solid blocker so the hero can't walk through the spire: the scene
            // builder strips the Cathedral FBX colliders and nothing re-adds one,
            // and HeroLocomotion's CapsuleCast only stops on non-trigger colliders.
            // Runtime-attached + idempotent so the curated village scene isn't edited.
            EnsureBlockerCollider();

            // Honor the authored transform when either:
            //  (a) _useAuthoredTransform is true (scene-builder flagged it), OR
            //  (b) the heart is NOT at world origin (the canonical Avalon
            //      layout hosts Elarion at (-6, 0, 1) centre-west of the plaza).
            // Defensive heuristic (b) covers the case where _useAuthoredTransform
            // was set at scene-build time but the SerializedObject write didn't
            // persist into the saved scene (Unity 6 serialisation quirk, same
            // class as the PanelSettings YAML issue, 2026-05-19).
            if (_useAuthoredTransform) return;
            if (transform.position != Vector3.zero) return;
            transform.localScale = Vector3.one * _heartScale;
        }

        // Spire base footprint for the walk-through blocker (world units).
        private const float BlockerRadius = 2.0f;
        private const float BlockerHeight = 8.0f;

        /// <summary>
        /// Adds a solid (non-trigger) collider at the spire base so the hero can't
        /// walk through the Heart. isTrigger MUST be false — HeroLocomotion's
        /// CapsuleCast uses QueryTriggerInteraction.Ignore. Idempotent.
        /// </summary>
        private void EnsureBlockerCollider()
        {
            if (GetComponent<Collider>() != null) return;
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.isTrigger = false;
            col.direction = 1;                                   // Y-axis (upright spire)
            col.radius = BlockerRadius;
            col.height = BlockerHeight;
            col.center = new Vector3(0f, BlockerHeight * 0.5f, 0f); // feet at y=0
        }

        /// <summary>Parses a 6-digit hex string ("rrggbb") into a Color.</summary>
        private static Color Hex(string rrggbb)
        {
            return ColorUtility.TryParseHtmlString("#" + rrggbb, out var c) ? c : Color.magenta;
        }
    }
}
