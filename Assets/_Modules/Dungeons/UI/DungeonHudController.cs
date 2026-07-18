// =============================================================================
// DungeonHudController — drives the dungeon HUD overlay (Workstream C).
// -----------------------------------------------------------------------------
// The controller behind DungeonHud.uxml. The dungeon scene had no HUD before
// this; the village HUD (DeNelle.HUD) is a different scene's overlay owned by
// another agent. This HUD lives inside DeNelle.Dungeons and ships ONE element
// for the v2 build: the lantern oil / duration readout (owner acceptance
// checklist — "torch / lantern duration mechanic: make the duration legible").
//
// MODULE ISOLATION: the HUD is a PASSIVE display. MVVM (Silo G): the View reads
// NO game state — it binds a DungeonHudVM that owns the lantern projection (bar
// fraction, low/critical band, low-oil pill, burn-time copy). The Lantern is fed
// to the VM through the narrow ILanternReadout seam. Lantern lives in the SAME
// module (DeNelle.Dungeons), so a direct reference is allowed — no cross-module
// dependency; only the read-of-state moved into the VM.
//
// The Lantern reference is pushed in by the DungeonController on dungeon load
// (SetLantern) — the controller already owns the Lantern reference, so the HUD
// does not need to find it. SetLantern now just routes the ref into the VM (PUSH
// seam preserved). Until a lantern is bound the oil panel reads "--".
//
// All UI is UI Toolkit (UXML/USS) — project mandate.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Drives the dungeon HUD (DungeonHud.uxml) — a glanceable lantern oil meter
    /// fed each frame from the <see cref="Lantern"/>'s public API, with a clear
    /// low-oil warning state. A passive UI view; it never mutates the lantern.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DungeonHudController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument hosting DungeonHud.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Lantern source")]
        [Tooltip("The Keeper's lantern — the oil meter is fed from its public API " +
                 "each frame. Pushed in by DungeonController on load; optional here.")]
        [SerializeField] private Lantern _lantern;

        [Header("Empty-oil threshold")]
        [Tooltip("At or below this oil fraction the bar switches from the low-oil " +
                 "amber to the critical red. A second band inside the lantern's " +
                 "own low-oil fraction so the player gets a graded warning.")]
        [SerializeField, Range(0f, 1f)] private float _criticalOilFraction = 0.1f;

        // ── UXML element-name contract with DungeonHud.uxml ──────────────────
        private const string RootName = "dungeon-hud-root";
        private const string OilCaptionName = "oil-caption";
        private const string OilBarFillName = "oil-bar-fill";
        private const string OilTimeLabelName = "oil-time-label";
        private const string OilLowWarningName = "oil-low-warning";

        // ── USS class names styled by DungeonHud.uss ─────────────────────────
        private const string OilFillWarningClass = "oil-bar-fill--warning";
        private const string OilFillEmptyClass = "oil-bar-fill--empty";
        private const string OilLowWarningShownClass = "oil-low-warning--shown";

        // ── The ViewModel (owns ALL oil-meter state/band logic + copy) ───────
        private DungeonHudVM _vm;

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private VisualElement _oilBarFill;
        private Label _oilTimeLabel;
        private Label _oilLowWarning;
        private bool _bound;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            // The VM owns the band logic + copy; seed it with the critical threshold
            // (serialized config) and, if a lantern was inspector-assigned, that ref.
            _vm = new DungeonHudVM(_criticalOilFraction);
            if (_lantern != null) _vm.SetLantern(new LanternReadoutAdapter(_lantern));
        }

        private void OnEnable()
        {
            BindElements();
        }

        // =====================================================================
        //  Binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning(
                    "[DungeonHudController] No UIDocument root — dungeon HUD will not display.");
                return;
            }

            // The dungeon HUD may share its UIDocument with the crafting panel.
            // Resolve the HUD sub-tree by its root name so both can co-exist.
            VisualElement hud = _root.Q<VisualElement>(RootName) ?? _root;

            _oilBarFill = hud.Q<VisualElement>(OilBarFillName);
            _oilTimeLabel = hud.Q<Label>(OilTimeLabelName);
            _oilLowWarning = hud.Q<Label>(OilLowWarningName);
            _bound = true;

            // Paint an initial idle state so the panel is not blank pre-run.
            RenderOil();
        }

        /// <summary>
        /// Binds the lantern the oil meter is fed from. Called by the
        /// <see cref="DungeonController"/> on dungeon load — the controller
        /// already holds the Lantern reference.
        /// </summary>
        public void SetLantern(Lantern lantern)
        {
            _lantern = lantern;
            // PUSH seam preserved: route the pushed ref into the VM (wrapped in the
            // read-only seam) rather than reading it from the View each frame.
            if (_vm == null) _vm = new DungeonHudVM(_criticalOilFraction);
            _vm.SetLantern(lantern != null ? new LanternReadoutAdapter(lantern) : null);
        }

        // =====================================================================
        //  Per-frame — poll the lantern's public API
        // =====================================================================

        private void Update()
        {
            if (!_bound) BindElements();
            RenderOil();
        }

        /// <summary>
        /// Repaints the oil meter from the VM ONLY (no game-state read). The bar
        /// fill is the VM's oil fraction; the bar tints amber in the low band and
        /// red when critically low; the time label shows the VM's burn-time copy;
        /// the warning pill shows while the VM reads low oil.
        /// </summary>
        private void RenderOil()
        {
            if (_vm == null) return;

            SetBarFraction(_vm.BarFraction);
            SetBarState(_vm.IsWarning, _vm.IsCritical);
            ShowLowWarning(_vm.ShowLowWarning);

            if (_oilTimeLabel != null)
                _oilTimeLabel.text = _vm.TimeLabel;
        }

        // =====================================================================
        //  Render helpers
        // =====================================================================

        /// <summary>Sets the oil bar fill width as a 0..1 fraction of its track.</summary>
        private void SetBarFraction(float fraction)
        {
            if (_oilBarFill == null) return;
            _oilBarFill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        }

        /// <summary>Applies the amber (low) / red (critical) oil-bar tint.</summary>
        private void SetBarState(bool warning, bool critical)
        {
            if (_oilBarFill == null) return;
            _oilBarFill.EnableInClassList(OilFillWarningClass, warning);
            _oilBarFill.EnableInClassList(OilFillEmptyClass, critical);
        }

        /// <summary>Shows / hides the low-oil warning pill.</summary>
        private void ShowLowWarning(bool show)
        {
            if (_oilLowWarning == null) return;
            _oilLowWarning.EnableInClassList(OilLowWarningShownClass, show);
        }
    }
}
