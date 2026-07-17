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
//
// WO-99 NOTE — HeartHealth / Defend the Tower (DTT) wiring:
//   A separate HeartHealth.cs is NOT needed for the Defend the Tower scene.
//   PatriciaLightController instantiates a HeartController at TowerPos and
//   drives tower HP directly via HeartController.SetHp() + the OnHealthChanged
//   event (see PatriciaLightController.ApplyTowerDamage / UpdateTowerHpUi).
//   HeartController already implements IDamageableStructure (IsAlive + ApplyContactDamage)
//   which enemies use for contact attacks. The tower HP bar in DTT is a code-
//   built UI-Toolkit slider bound to OnHealthChanged — no second MonoBehaviour
//   required. If a dedicated HeartHealth component is needed in a future scene
//   that cannot host a full HeartController (e.g. a lightweight arena), create
//   HeartHealth.cs in this folder as a thin wrapper: expose Hp, SetHp, and the
//   OnHealthChanged event, and have it implement IDamageableStructure.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Combat;

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
    public sealed class HeartController : MonoBehaviour, IDamageableStructure
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

        /// <summary>
        /// Fired exactly once, the moment the Heart's HP first reaches 0 — the
        /// village lose condition (WO-125 Bug 3). A subscriber (WaveManager) halts
        /// the wave loop and surfaces the defeat outcome. Guarded by
        /// <see cref="_destroyed"/> so repeat 0-HP <see cref="SetHp"/> calls (e.g. a
        /// dragon striking an already-dead Heart) cannot re-fire it.
        /// </summary>
        public event System.Action OnHeartDestroyed;

        /// <summary>True once <see cref="OnHeartDestroyed"/> has fired — fire-once guard.</summary>
        private bool _destroyed;

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
        /// Heals the Heart by <paramref name="amount"/> HP (0-100 clamp). Delegates to
        /// <see cref="SetHp"/> so the crystal state, OnHealthChanged event and the
        /// no-op guard are all handled in one place.
        /// <para>
        /// WO-98: SalveAbility routes here when the active scene is a Defend-the-Tower
        /// mode so the E-slot heal repairs the tower instead of the hero.
        /// </para>
        /// </summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            SetHp(_hp + amount);
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

            // WO-125 Bug 3: the Heart fell — raise the village lose condition once.
            // The dragon (and any other source) drains HP through SetHp, but nothing
            // reacted to it hitting 0; a subscriber now halts the loop + shows defeat.
            if (_hp <= 0f && !_destroyed)
            {
                _destroyed = true;
                OnHeartDestroyed?.Invoke();
            }
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
            //  (b) the heart is NOT at world origin (the canonical Elarion
            //      layout hosts Elarion at (-6, 0, 1) centre-west of the plaza).
            // Defensive heuristic (b) covers the case where _useAuthoredTransform
            // was set at scene-build time but the SerializedObject write didn't
            // persist into the saved scene (Unity 6 serialisation quirk, same
            // class as the PanelSettings YAML issue, 2026-05-19).
            if (_useAuthoredTransform) return;
            if (transform.position != Vector3.zero) return;
            transform.localScale = Vector3.one * _heartScale;
        }

        // Central reliquary base footprint for the walk-through blocker (LOCAL units;
        // world footprint = radius * lossyScale). Owner F8 2026-07-17 (combat arena):
        // "something preventing me from moving through the middle of the arena." RCA: the
        // arena hero is a NavMeshAgent (HeroLocomotion), so it is stopped ONLY by the
        // NavMesh being carved, never by a raw physics collider. In the arena the NavMesh
        // is RUNTIME-baked (ArenaNavMeshBaker, useGeometry=PhysicsColliders) AFTER this
        // collider is added in Awake -- and the Heart is scaled ~2x, so the old radius 2.0
        // became a ~4m-radius (8m-diameter) INVISIBLE capsule that CARVED a wide no-walk
        // hole dead-centre (far wider than the visible brazier base). Right-sized to ~the
        // visible reliquary base (1.0 local -> ~2m world radius at 2x) so the centre bakes
        // walkable up to the brazier edge, while you still cannot walk through the fire.
        // VILLAGE IS UNAFFECTED: this collider is added at RUNTIME (Awake), so it is NOT
        // part of the pre-baked village NavMesh -- shrinking it changes only the runtime-
        // baked arena carve. Owner felt-tunable (drop to ~0 for fully-passable decor).
        private const float BlockerRadius = 1.0f;   // was 2.0f -- halves the central arena carve (F8 2026-07-17)
        private const float BlockerHeight = 8.0f;

        /// <summary>
        /// Adds a solid (non-trigger) collider at the reliquary base. In the arena this
        /// collider is what the runtime NavMesh bake carves, so its world footprint == the
        /// central no-walk hole the NavMeshAgent hero must clear. Idempotent.
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

        private void Start()
        {
            // S12 PROOF (owner F8 2026-07-17 arena-centre blocker): report the Heart blocker's
            // FINAL world footprint (after the ~2x scale is applied in Awake) so a headless/felt
            // run PROVES the central no-walk carve is right-sized. The arena NavMesh is runtime-
            // baked over this collider, so its world radius == the central hole the hero clears.
            // Near-zero cost when FlowTrace is disabled; ASCII-only.
            var col = GetComponent<Collider>();
            if (col != null)
            {
                Bounds b = col.bounds; // world-space AABB (post-scale)
                DeNelle.Core.Diagnostics.FlowTrace.Step("Structure",
                    "Heart blocker FINAL world footprint: boundsSize=" + b.size.ToString("0.0") +
                    " centre=" + b.center.ToString("0.0") + " lossyScale=" + transform.lossyScale.ToString("0.00") +
                    " (central carve into the runtime-baked arena NavMesh; radius right-sized 2.0 -> " +
                    BlockerRadius.ToString("0.0") + " so the hero can cross the middle).");
            }
        }

        // ── IDamageableStructure ─────────────────────────────────────────────
        bool IDamageableStructure.IsAlive => _hp > 0f;
        void IDamageableStructure.ApplyContactDamage(float amount) => SetHp(_hp - amount);

        /// <summary>Parses a 6-digit hex string ("rrggbb") into a Color.</summary>
        private static Color Hex(string rrggbb)
        {
            return ColorUtility.TryParseHtmlString("#" + rrggbb, out var c) ? c : Color.magenta;
        }
    }
}
