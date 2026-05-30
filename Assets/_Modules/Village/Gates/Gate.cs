// =============================================================================
// Gate — one cardinal force-field gate MonoBehaviour (Week 4).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/walls/Gate.tsx -> Gate.cs.
//
// One MonoBehaviour per cardinal gate (N / E / S / W). VillageController
// instantiates one per WallLayout.Gates entry and calls Configure().
//
// WEEK 4 — this file gains the gameplay layer over the Week-3 skeleton:
//   - the gate TAKES DAMAGE (TakeDamage / Repair),
//   - the force field visibly COLLAPSES below 25% HP — the ForceFieldGate
//     shader's _Collapse property eases 0 -> 1 as HP falls through the
//     threshold band, tearing the violet sheet apart,
//   - the blocker collider TOGGLES OFF on collapse so the Hollow Ones can
//     pour through the opening (port spec Part 5 Week 4).
//
// Geometry comes from docs/four-cardinal-gates-spec.md: on the SQUARE wall a
// gate sits centred in one side, the wall sections meet the pillars flush, and
// gate HP rides on the buildingDamage map as gate-0 .. gate-3.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>Cardinal direction of a gate -- parallel to <c>GATE_DIRECTIONS</c> in gate.ts.</summary>
    public enum GateDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>
    /// A single cardinal force-field gate centred in one side of the square
    /// wall. Holds its stable damage id (<c>gate-0</c> .. <c>gate-3</c>),
    /// direction, force-field HP, the shader-driven collapse state, and the
    /// blocker collider that toggles on collapse. Instantiated by
    /// <see cref="VillageController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate : MonoBehaviour, IDamageableStructure
    {
        [Header("Identity")]
        [Tooltip("Stable damage id -- gate-0 (N) .. gate-3 (W). From WallLayout.Gates.")]
        [SerializeField] private string _gateId;

        [Tooltip("Cardinal direction -- drives HUD copy / repair-modal headlines.")]
        [SerializeField] private GateDirection _direction = GateDirection.North;

        [Tooltip("Cardinal heading (radians) from village centre toward this gate's side.")]
        [SerializeField] private float _angle;

        [Header("Force field")]
        [Tooltip("Gate HP, 0-100. The force field collapses below 25%, letting Hollow Ones through.")]
        [SerializeField, Range(0f, 100f)] private float _hp = 100f;

        [Tooltip("Max gate HP, 0-100.")]
        [SerializeField, Range(0f, 100f)] private float _maxHp = 100f;

        [Tooltip("Collider that blocks enemies while the force field is up. Toggled off on collapse.")]
        [SerializeField] private BoxCollider _forceFieldCollider;

        [Tooltip("Renderer of the force-field sheet — its material runs ForceFieldGate.shader.")]
        [SerializeField] private Renderer _forceFieldRenderer;

        [Tooltip("Half-width (world units) of the gate opening. Matches WallLayout.GateHalfWidth.")]
        [SerializeField] private float _halfWidth = WallLayout.GateHalfWidth;

        [Header("Collapse tuning")]
        [Tooltip("Seconds the shader _Collapse property eases toward its target after an HP change.")]
        [SerializeField] private float _collapseEaseSeconds = 0.35f;

        /// <summary>HP fraction below which the force field collapses (Week 4 gameplay).</summary>
        public const float CollapseThreshold = 0.25f;

        /// <summary>Shader property id for the ForceFieldGate.shader collapse driver.</summary>
        private static readonly int CollapseId = Shader.PropertyToID("_Collapse");

        /// <summary>Fired when this gate's HP changes — HUD repair prompts subscribe.</summary>
        public event Action<Gate> HpChanged;

        /// <summary>Fired once when the force field collapses (HP crosses below 25%).</summary>
        public event Action<Gate> ForceFieldCollapsed;

        /// <summary>Fired once when a collapsed force field is restored above 25%.</summary>
        public event Action<Gate> ForceFieldRestored;

        // --- runtime ---
        private MaterialPropertyBlock _mpb;
        private float _collapseDisplayed;   // shader value, eased toward _collapseTarget
        private float _collapseTarget;      // 0 = full strength, 1 = collapsed
        private bool _wasForceFieldUp = true;
        // WO-08: set while the hero is inside a GateProximityOpener radius. An
        // ADDITIONAL reason to drop the field (the hero walks through), layered
        // on top of the existing HP-derived collapse — enemies never set it.
        private bool _isOpenForHero;

        /// <summary>Stable damage id -- <c>gate-0</c> .. <c>gate-3</c>.</summary>
        public string GateId => _gateId;

        /// <summary>Cardinal direction of this gate.</summary>
        public GateDirection Direction => _direction;

        /// <summary>Cardinal heading (radians) toward this gate's square side.</summary>
        public float Angle => _angle;

        /// <summary>Gate HP, 0-100.</summary>
        public float Hp => _hp;

        /// <summary>Max gate HP, 0-100.</summary>
        public float MaxHp => _maxHp;

        /// <summary>Gate HP as a 0..1 fraction.</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

        /// <summary>True while the force field still blocks enemies (HP above the collapse threshold).</summary>
        public bool IsForceFieldUp => HpFraction > CollapseThreshold;

        /// <summary>
        /// <see cref="IDamageableStructure"/> — true while the gate still has HP
        /// for an enemy to attack. Once HP hits zero the field is fully torn and
        /// enemies stop attacking and path through the opening. (The force field
        /// stops *blocking* earlier, at the 25% collapse threshold — see
        /// <see cref="IsForceFieldUp"/>.)
        /// </summary>
        public bool IsAlive => _hp > 0f;

        /// <summary>
        /// Wires this gate from a <see cref="GateGap"/> layout record. Called by
        /// <see cref="VillageController"/> right after instantiation.
        /// </summary>
        /// <param name="gap">The <see cref="WallLayout"/> gate-gap record this gate fills.</param>
        public void Configure(GateGap gap)
        {
            _gateId = gap.Id;
            _direction = (GateDirection)Mathf.Clamp(gap.Index, 0, 3);
            _angle = gap.Angle;
            _halfWidth = WallLayout.GateHalfWidth;
            RebuildCollider();
            RefreshCollapseTarget(snap: true);
            ApplyForceFieldState();
        }

        /// <summary>
        /// Sets the gate's HP directly (e.g. restoring from a save). Drives the
        /// collapse visuals and the blocker toggle.
        /// </summary>
        public void SetHp(float hp, float maxHp = -1f)
        {
            if (maxHp > 0f) _maxHp = Mathf.Clamp(maxHp, 1f, 100f);
            _hp = Mathf.Clamp(hp, 0f, _maxHp);
            RefreshCollapseTarget(snap: true);
            ApplyForceFieldState();
            HpChanged?.Invoke(this);
        }

        /// <summary>
        /// Applies <paramref name="amount"/> damage to the gate. Below 25% HP
        /// the force field collapses — the blocker drops and the shader tears
        /// the violet sheet apart so enemies can pour through.
        /// </summary>
        /// <param name="amount">Damage on the 0–100 HP scale.</param>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f || _hp <= 0f) return;
            _hp = Mathf.Max(0f, _hp - amount);
            RefreshCollapseTarget(snap: false);
            ApplyForceFieldState();
            HpChanged?.Invoke(this);
        }

        /// <summary>
        /// Repairs the gate by <paramref name="amount"/> HP (the village repair
        /// flow / a Workshop crew). Restores the force field if HP climbs back
        /// above the collapse threshold.
        /// </summary>
        /// <param name="amount">HP to restore on the 0–100 scale.</param>
        public void Repair(float amount)
        {
            if (amount <= 0f) return;
            _hp = Mathf.Min(_maxHp, _hp + amount);
            RefreshCollapseTarget(snap: false);
            ApplyForceFieldState();
            HpChanged?.Invoke(this);
        }

        /// <summary>
        /// <see cref="IDamageableStructure"/> contact-attack entry point — a
        /// Hollow One in melee contact with the force field routes its hit here.
        /// A thin adapter onto the existing <see cref="TakeDamage"/>, which drives
        /// the shader collapse + blocker toggle. Once the field collapses below
        /// 25% the blocker drops and enemies pour through (port spec Week 4).
        /// </summary>
        public void ApplyContactDamage(float amount) => TakeDamage(amount);

        /// <summary>True while a hero is inside this gate's proximity radius.</summary>
        public bool IsOpenForHero => _isOpenForHero;

        /// <summary>
        /// WO-08 — opens the force field for an approaching hero: eases the
        /// collapse to fully passable and drops the blocker so the hero walks
        /// through. Layered ON TOP of the HP-derived state, so the existing
        /// damage→collapse mechanic is untouched. Idempotent; called by
        /// <see cref="GateProximityOpener"/> on hero approach. Only the hero
        /// ever triggers this — enemies do not.
        /// </summary>
        public void RequestOpen()
        {
            if (_isOpenForHero) return;
            _isOpenForHero = true;
            RefreshCollapseTarget(snap: false);
            ApplyForceFieldState();
        }

        /// <summary>
        /// WO-08 — closes the force field again once the hero leaves the
        /// proximity radius: the collapse target reverts to whatever HP dictates
        /// and the blocker re-enables IF HP also wants the field up (a gate the
        /// enemies already battered below 25% stays open). Idempotent.
        /// </summary>
        public void RequestClose()
        {
            if (!_isOpenForHero) return;
            _isOpenForHero = false;
            RefreshCollapseTarget(snap: false);
            ApplyForceFieldState();
        }

        private void Awake()
        {
            if (_forceFieldCollider == null) _forceFieldCollider = GetComponent<BoxCollider>();
            if (_forceFieldRenderer == null) _forceFieldRenderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            ApplyFieldColor();          // BUG-016
            RefreshCollapseTarget(snap: true);
            ApplyForceFieldState();
        }

        // BUG-016: the force-field stand-in rendered WHITE — a fallback material
        // reads _BaseColor/_Color, which the ForceFieldGate.shader's violet
        // (_FieldColor) never sets. Push the canon violet to all three via the
        // shared MPB so the field is violet on the real shader AND any fallback
        // material; SetColor on a property the material's shader lacks is a no-op.
        private void ApplyFieldColor()
        {
            if (_forceFieldRenderer == null) return;
            var violet = new Color(0.486f, 0.227f, 0.929f, 1f); // ForceFieldGate.shader default
            _forceFieldRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_FieldColor", violet);
            _mpb.SetColor("_BaseColor", violet);
            _mpb.SetColor("_Color", violet);
            _forceFieldRenderer.SetPropertyBlock(_mpb);
        }

        private void Update()
        {
            // Ease the shader's _Collapse value toward its HP-derived target so
            // the field tears/heals smoothly rather than snapping.
            if (!Mathf.Approximately(_collapseDisplayed, _collapseTarget))
            {
                float step = _collapseEaseSeconds > 0f
                    ? Time.deltaTime / _collapseEaseSeconds
                    : 1f;
                _collapseDisplayed = Mathf.MoveTowards(_collapseDisplayed, _collapseTarget, step);
                PushCollapseToShader();
            }
        }

        /// <summary>
        /// Recomputes the shader collapse target from current HP. Below 25% HP
        /// the field eases toward fully collapsed (1); above it, toward 0.
        /// Between 25% and 0% the collapse ramps so the field looks like it is
        /// failing progressively, not popping off in one frame.
        /// </summary>
        private void RefreshCollapseTarget(bool snap)
        {
            float frac = HpFraction;
            if (_isOpenForHero)
            {
                // WO-08: hero in proximity — fully passable regardless of HP.
                _collapseTarget = 1f;
            }
            else if (frac > CollapseThreshold)
            {
                _collapseTarget = 0f;
            }
            else
            {
                // frac in [0, 0.25] -> collapse in [1, 0]; 0 HP = fully torn.
                _collapseTarget = 1f - Mathf.Clamp01(frac / CollapseThreshold);
            }

            if (snap)
            {
                _collapseDisplayed = _collapseTarget;
                PushCollapseToShader();
            }
        }

        /// <summary>
        /// Toggles the blocker collider with the force-field state and fires the
        /// collapse / restore events on the rising / falling edge.
        /// </summary>
        private void ApplyForceFieldState()
        {
            // WO-08: the blocker is up only when the HP-derived field is up AND no
            // hero is in proximity. Combined state, so the collapse/restore events
            // fire on the rising/falling edge of "blocking enemies right now".
            bool up = !_isOpenForHero && IsForceFieldUp;
            if (_forceFieldCollider != null)
                _forceFieldCollider.enabled = up; // blocker drops on collapse

            if (up != _wasForceFieldUp)
            {
                if (up) ForceFieldRestored?.Invoke(this);
                else ForceFieldCollapsed?.Invoke(this);
                _wasForceFieldUp = up;
            }
        }

        /// <summary>Writes the eased collapse value into the force-field material.</summary>
        private void PushCollapseToShader()
        {
            if (_forceFieldRenderer == null) return;
            _mpb ??= new MaterialPropertyBlock();
            _forceFieldRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(CollapseId, _collapseDisplayed);
            _forceFieldRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>Sizes the force-field collider to span the gate opening.</summary>
        private void RebuildCollider()
        {
            if (_forceFieldCollider == null) _forceFieldCollider = GetComponent<BoxCollider>();
            if (_forceFieldCollider == null) _forceFieldCollider = gameObject.AddComponent<BoxCollider>();
            // Span runs along local X (matches the gate's WallLayout rotation rule).
            _forceFieldCollider.size = new Vector3(_halfWidth * 2f, 4f, WallLayout.WallThickness);
            _forceFieldCollider.center = new Vector3(0f, 2f, 0f);
            // A solid blocker while the field is up; ApplyForceFieldState()
            // toggles .enabled off when the field collapses.
            _forceFieldCollider.isTrigger = false;
        }
    }
}
