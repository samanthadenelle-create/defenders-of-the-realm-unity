// =============================================================================
// DungeonHudController — the dungeon HUD's lantern oil meter, on the Obsidian kit.
// -----------------------------------------------------------------------------
// WO-1005 (dungeon UI cohesion): this View was the last UXML/UIDocument surface a
// dungeon player could actually see. Two problems in one object:
//   1. COHESION — the UXML oil panel was its own one-off styling (DungeonHud.uss),
//      not the obsidian+gold ElarionUiKit chrome every other panel/toast/button
//      wears. The WO's ruling: every player-facing dungeon overlay uses the kit.
//   2. UXML IN BUILDS DOES NOT WORK (CLAUDE.md sec.8, learned the hard way) — the
//      UIDocument came up EMPTY in a player build, so the oil meter the owner
//      acceptance-listed ("make the duration legible") was blank exactly where it
//      mattered.
// The View is now CODE-BUILT uGUI on the kit: an obsidian card (near-black fill +
// soft gold rim, the ToastCard/ObsidianFill chrome), the kit's ObsidianBar as the
// oil bar, kit Labels for the caption + burn-time copy, and the shared ToastCard
// (Danger tone) as the low-oil pill. Nothing here is tappable, so MinTouchPx does
// not apply; the whole overlay is raycast-transparent (never swallows gameplay).
//
// COLOURBLIND LAW: the low/critical state is carried by the PILL'S WORDS and the
// burn-time copy, never by hue alone — the fill tint (amber/red) is a secondary
// reinforcement and only applied when the kit built a tintable fill.
//
// MVVM unchanged: DungeonHudVM still owns ALL band logic/copy; this View binds and
// paints, reading NO game state. The DungeonController SetLantern PUSH seam is
// preserved verbatim.
//
// LEGACY SEAM: the cottage scene's UIDocument may still carry DungeonHud.uxml
// (shared with the crafting panel's sub-tree). We hide ONLY the "dungeon-hud-root"
// sub-tree so the crafting panel keeps its document; the serialized _document field
// is kept so existing scene data binds without a rebake.
//
// Instrumented per CLAUDE.md sec.12 — [Flow:DungeonHud] on build, on the legacy
// hide, and on every band transition.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
// Both UI and UIElements are imported (the kit is uGUI; the legacy hide seam is
// UIElements) — alias the collisions to the uGUI side the kit is built on.
using Image = UnityEngine.UI.Image;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Drives the dungeon HUD — a glanceable lantern oil meter built from the
    /// Obsidian ElarionUiKit chrome (WO-1005 cohesion; code-built uGUI, no UXML).
    /// A passive UI view: it binds a <see cref="DungeonHudVM"/> and paints; it
    /// never mutates the lantern and never blocks raycasts.
    /// </summary>
    public sealed class DungeonHudController : MonoBehaviour
    {
        private const string Sys = "DungeonHud";

        // Below DungeonToastView (720) — toasts read over the passive meter.
        private const int SortingOrder = 600;

        [Header("Legacy UXML host (kept so existing scene data binds; the HUD sub-tree " +
                "inside it is hidden — the crafting panel may share this document)")]
        [SerializeField] private UIDocument _document;

        [Header("Lantern source")]
        [Tooltip("The Keeper's lantern — the oil meter is fed from its public API " +
                 "each frame. Pushed in by DungeonController on load; optional here.")]
        [SerializeField] private Lantern _lantern;

        [Header("Empty-oil threshold")]
        [Tooltip("At or below this oil fraction the meter reads CRITICAL (red band). " +
                 "A second band inside the lantern's own low-oil fraction so the " +
                 "player gets a graded warning.")]
        [SerializeField, Range(0f, 1f)] private float _criticalOilFraction = 0.1f;

        // ── The ViewModel (owns ALL oil-meter state/band logic + copy) ───────
        private DungeonHudVM _vm;

        // ── Code-built kit UI ────────────────────────────────────────────────
        private GameObject _canvasGo;
        private ElarionUiKit.BarHandle _oilBar;
        private TMPro.TextMeshProUGUI _timeLabel;
        private GameObject _lowPill;

        // Fill tint band (secondary reinforcement — the pill's WORDS are the carrier).
        private Color _fillNormal;
        private bool _fillTintable;

        // Change-only repaint caches (no per-frame string/state churn).
        private string _lastTimeText;
        private bool _lastLow;
        private bool _lastCritical;
        private bool _legacyHidden;

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
            Guard.Try(Sys, "build kit oil HUD", BuildKitHud);
            HideLegacyUxmlHud();
        }

        private void OnDisable()
        {
            if (_canvasGo != null)
            {
                Destroy(_canvasGo);
                _canvasGo = null;
                _oilBar = null;
                _timeLabel = null;
                _lowPill = null;
            }
            // Force a full repaint on the next enable.
            _lastTimeText = null;
            _lastLow = false;
            _lastCritical = false;
        }

        /// <summary>
        /// Binds the lantern the oil meter is fed from. Called by the
        /// <see cref="DungeonController"/> on dungeon load — the controller
        /// already holds the Lantern reference. PUSH seam preserved.
        /// </summary>
        public void SetLantern(Lantern lantern)
        {
            _lantern = lantern;
            if (_vm == null) _vm = new DungeonHudVM(_criticalOilFraction);
            _vm.SetLantern(lantern != null ? new LanternReadoutAdapter(lantern) : null);
        }

        // =====================================================================
        //  Build — Obsidian kit chrome, code-built uGUI (no UXML)
        // =====================================================================

        private void BuildKitHud()
        {
            if (_canvasGo != null) return;   // idempotent across enable cycles

            _canvasGo = new GameObject("DungeonHud_Kit");
            _canvasGo.transform.SetParent(transform, false);

            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Passive display: never swallow gameplay / interact input. No
            // GraphicRaycaster on purpose — nothing here is tappable.
            var group = _canvasGo.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // ── The oil card: obsidian near-black rounded fill + soft gold rim
            //    (the ToastCard/ObsidianFill chrome), seated top-left. ──────────
            var card = ElarionUiKit.AddImage(_canvasGo.transform, "OilCard",
                Vector2.zero, Vector2.zero, ElarionUiKit.ObsidianFill, rounded: true);
            ElarionUiKit.AddInnerRim(card, ElarionUiKit.ObsidianTrim);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(24f, -24f);
            crt.sizeDelta = new Vector2(460f, 180f);
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null) cardImg.raycastTarget = false;

            // Caption — gold, small caps feel via spacing (kit Label primitive).
            ElarionUiKit.Label(card.transform, "LANTERN", 0.66f, 0.97f,
                ElarionUi.Gold, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft,
                x0: 0.06f, x1: 0.94f, spacing: 6f, bold: true);

            // THE bar (kit section 1.1) — Energy kind: the gold-amber fill reads as
            // lamp oil and matches the HUD bar family art.
            _oilBar = ElarionUiKit.BuildObsidianBar(card.transform,
                ElarionUiKit.ObsidianBarKind.Energy,
                new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.64f),
                withValue: false, framed: true);
            _fillNormal = _oilBar != null && _oilBar.fill != null ? _oilBar.fill.color : Color.white;
            // A coloured pack fill stays white on purpose (kit rule) — only a
            // tintable (non-white) fill takes the amber/red band reinforcement.
            _fillTintable = _fillNormal != Color.white;

            // Burn-time copy ("Light: 1m 12s") — parchment body text.
            _timeLabel = ElarionUiKit.Label(card.transform, DungeonHudVM.FormatBurnTime(float.PositiveInfinity),
                0.04f, 0.34f, ElarionUi.Parchment, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.MidlineLeft, x0: 0.06f, x1: 0.94f);

            // ── Low-oil pill: the ONE shared obsidian toast chrome, Danger tone —
            //    the WORDS carry the state (colourblind law), the accent reinforces. ──
            var pill = ElarionUiKit.ToastCard(_canvasGo.transform, ElarionUiKit.ToastTone.Danger,
                                              accentLeft: true, align: TextAnchor.MiddleLeft);
            _lowPill = pill.card;
            var prt = (RectTransform)pill.card.transform;
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(24f, -216f);
            prt.sizeDelta = new Vector2(460f, 64f);
            if (pill.label != null) pill.label.text = "LOW OIL - find an oil stone";
            _lowPill.SetActive(false);

            FlowTrace.Step(Sys,
                "kit oil HUD built (WO-1005): obsidian card 460x180 top-left @ (24,-24), " +
                $"ObsidianBar kind=Energy fillTintable={_fillTintable}, low pill=ToastCard(Danger) " +
                $"sortingOrder={SortingOrder} raycast=OFF (code-built uGUI, no UXML)");
        }

        /// <summary>
        /// Hide ONLY the legacy UXML HUD sub-tree ("dungeon-hud-root") so a scene
        /// still carrying DungeonHud.uxml never double-draws the meter — while the
        /// crafting panel, which may share this UIDocument, keeps its own sub-tree.
        /// </summary>
        private void HideLegacyUxmlHud()
        {
            if (_legacyHidden) return;
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                // Expected on a code-built seat with no UIDocument: nothing to hide.
                FlowTrace.Step(Sys, "no legacy UIDocument root - kit HUD is the only oil meter");
                _legacyHidden = true;
                return;
            }
            var legacy = root.Q<VisualElement>("dungeon-hud-root");
            if (legacy != null)
            {
                legacy.style.display = DisplayStyle.None;
                FlowTrace.Step(Sys, "legacy UXML 'dungeon-hud-root' sub-tree HIDDEN " +
                    "(kit HUD replaces it; crafting sub-tree untouched)");
            }
            _legacyHidden = true;
        }

        // =====================================================================
        //  Per-frame — paint from the VM only (no game-state read)
        // =====================================================================

        private void Update()
        {
            if (_vm == null || _canvasGo == null) return;

            if (_oilBar != null)
                _oilBar.SetImmediate(_vm.BarFraction, 1f);   // per-frame sweep: no easing

            // Band transitions (change-only: tint reinforcement + trace).
            bool critical = _vm.IsCritical;
            bool low = _vm.ShowLowWarning;
            if (critical != _lastCritical || low != _lastLow)
            {
                _lastCritical = critical;
                _lastLow = low;
                if (_fillTintable && _oilBar != null && _oilBar.fill != null)
                {
                    _oilBar.fill.color = critical ? ElarionUi.Danger
                        : _vm.IsWarning ? new Color(1f, 0.65f, 0.18f, 1f)   // amber low band
                        : _fillNormal;
                }
                if (_lowPill != null) _lowPill.SetActive(low);
                FlowTrace.Step(Sys,
                    $"oil band -> {(critical ? "CRITICAL" : low ? "LOW" : "ok")} " +
                    $"(fraction={_vm.BarFraction:F2}, pill={(low ? "shown" : "hidden")})");
            }

            // Burn-time copy (change-only — the VM string only shifts once a second).
            string time = _vm.TimeLabel;
            if (_timeLabel != null && !ReferenceEquals(time, _lastTimeText) && time != _lastTimeText)
            {
                _lastTimeText = time;
                _timeLabel.text = time;
                _timeLabel.color = critical ? ElarionUi.Danger : ElarionUi.Parchment;
            }
        }
    }
}
