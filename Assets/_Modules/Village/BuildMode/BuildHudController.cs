// =============================================================================
// BuildHudController — the dedicated Build HUD presentation layer (Grok slice 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ONE landscape ElarionUiKit canvas (1920x1080, MatchWidthOrHeight=0.5) that owns
// the Build Mode edit-chrome as a single surface, ending the "seat wars" between
// the old fragmented canvases (BuildPlaceButton + the LeanTouchBuildDriver verb
// bar + BuildSelectionUI + the portrait palette header). BuildModeController — the
// BRAIN — drives it via Show/Hide/SetState/RefreshResources; the HUD calls back
// into the controller for the place intents (rotate/place/cancel/exit).
//
// THREE STATES (Grok, CoC mental model):
//   Browse   — shop open; NO intent bar; tap a placed building to select it.
//   Placing  — the single PLACE intent bar (Rotate L/R . PLACE . Cancel); the shop
//              collapses to the armed-card summary (BuildPaletteUI.Collapse).
//   Selected — the selection verbs (Move/Upgrade/Sell/Cancel) render on the SAME
//              bar family, owned by BuildSelectionUI (kept — the fleet's
//              AssertBuildMoveChain SELECT link asserts that panel renders).
//
// This canvas hosts the wallet row (BuildWalletRow, all pools), the "BUILD MODE"
// label, and the "Done" exit (>=132px). The category tabs + the icon-first card
// carousel stay in BuildPaletteUI's own canvas (its card/art/cost/economy/arm
// logic is reused verbatim, not rebuilt); this controller sequences them by state.
//
// LANDSCAPE only; panels near-black (WO-562 — do NOT lighten); ASCII-only TMP;
// meaning never by colour alone; code-built uGUI on the kit; ZERO UXML. Control
// sizes are NAMED constants set here (this lane must not touch ElarionUiKit's
// MinTouchPx floor — a separate visual lane owns it).
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>The three Build HUD chrome states (Grok CoC model).</summary>
    public enum BuildHudState { Browse, Placing, Selected }

    /// <summary>
    /// The single Build HUD canvas + state machine. Created/owned by
    /// BuildModeController; parents the wallet row, the BUILD MODE label, the Done
    /// exit, and the single place-intent bar.
    /// </summary>
    public sealed class BuildHudController : MonoBehaviour
    {
        // ── NAMED control sizes (this lane sets its own floors; kit lane owns MinTouchPx) ──
        // At the 1920x1080 landscape reference: Exit/close >= 132px shortest side,
        // every other control >= 112px shortest side (owner touch-target ruling).
        private const int SortingOrder = 906;   // above palette dock (900), below selection (910)

        // ── CONSISTENT button rectangle (owner felt-test 2026-07-15: "long thin
        //    rectangles in horizontal mode") ─────────────────────────────────────
        // A wide landscape canvas stretches fraction-of-WIDTH anchors into thin bars
        // (very wide, short). The fix: every HUD button gets a CONSISTENT fixed size
        // via PinSize — height = the kit CTA height (ElarionUiKit.CanonCtaHeight, >=
        // MinTouchPx floor) and a CAPPED width, so it stays a proper tappable box
        // instead of a full-band bar. Standard verb 190w, primary PLACE 240w, the
        // top-right Done 200w. Height sourced from the kit (no new magic floor).
        private const float IntentBtnW = 190f;
        private const float PlaceBtnW  = 240f;
        private const float ExitBtnW   = 200f;

        // Callbacks into the BRAIN (BuildModeController wires these).
        private Action _onRotateLeft;
        private Action _onRotateRight;
        private Action _onPlace;
        private Action _onCancel;
        private Action _onExit;

        private GameObject _canvas;
        private GameObject _intentBar;       // shown only in Placing
        private BuildWalletRow _wallet;
        private BuildHudState _state = BuildHudState.Browse;

        /// <summary>
        /// Create the HUD host (one per session). <paramref name="parent"/> is the
        /// controller transform so the canvas tears down with it.
        /// </summary>
        public static BuildHudController Create(Transform parent,
            Action onRotateLeft, Action onRotateRight, Action onPlace, Action onCancel, Action onExit)
        {
            var host = new GameObject("BuildHudController");
            if (parent != null) host.transform.SetParent(parent, false);
            var hud = host.AddComponent<BuildHudController>();
            hud._onRotateLeft = onRotateLeft;
            hud._onRotateRight = onRotateRight;
            hud._onPlace = onPlace;
            hud._onCancel = onCancel;
            hud._onExit = onExit;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            if (_canvas != null) return;

            // ── ONE landscape canvas (1920x1080, match 0.5) — NOT the kit's portrait
            //    BuildModalCanvas; this HUD is landscape-only (owner ruling). ──────
            _canvas = new GameObject("BuildHudCanvas");
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvas.AddComponent<GraphicRaycaster>();

            BuildTopBar();
            BuildIntentBar();
            FlowTrace.Step("BuildHud",
                "chrome-built: consistent button sizes enforced (h=" +
                ElarionUiKit.CanonCtaHeight + ", capped widths — no thin bars)");

            _canvas.SetActive(false);   // built hidden; Show shows it
        }

        private void BuildTopBar()
        {
            // Near-black top band (WO-562) spanning the top of the landscape canvas.
            var band = ElarionUiKit.AddImage(_canvas.transform, "TopBar",
                new Vector2(0f, 0.90f), new Vector2(1f, 1f), ElarionUiKit.ObsidianFill, rounded: false);
            var bandImg = band.GetComponent<Image>();
            if (bandImg != null) bandImg.raycastTarget = true;   // eat taps on the chrome band

            // Wallet chips (all pools) — left.
            var walletGo = new GameObject("BuildWalletRow");
            walletGo.transform.SetParent(band.transform, false);
            _wallet = walletGo.AddComponent<BuildWalletRow>();
            _wallet.Build(band.transform);

            // "BUILD MODE" label — centre.
            var title = MakeText(band.transform, "BUILD MODE", 26, ElarionUi.Gilt,
                FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.35f, 0.1f), new Vector2(0.65f, 0.9f));
            title.raycastTarget = false;

            // "X Done" exit — top-right. Pinned to the consistent HUD button box
            // (ExitBtnW x CanonCtaHeight) centred on its anchor, so a wide canvas can
            // never stretch it into a thin bar.
            var exit = ElarionUiKit.BuildObsidianButton(_canvas.transform, "X Done",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.905f, 0.86f), new Vector2(0.99f, 0.99f),
                () => _onExit?.Invoke());
            PinSize(exit, ExitBtnW, ElarionUiKit.CanonCtaHeight);
            // Canonical close name (probe/close convention; label stays "X Done").
            exit.gameObject.name = "CloseButton";
        }

        private void BuildIntentBar()
        {
            // Centred, ABOVE the shop dock — shown only in Placing. Each button
            // >=112px shortest side (182x130 / PLACE 230x130 at reference).
            _intentBar = new GameObject("BuildIntentBar", typeof(RectTransform));
            _intentBar.transform.SetParent(_canvas.transform, false);
            var irt = (RectTransform)_intentBar.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;

            // Each verb is pinned to the consistent HUD box (PinSize) after build, so
            // the anchor rect only carries the CENTRE — the four centres below are
            // spaced so the fixed-width boxes sit in a row with even gaps (no overlap,
            // no thin bars). Row is vertically centred at y=0.36.
            var rotL = ElarionUiKit.BuildObsidianButton(_intentBar.transform, "Rotate Left",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.233f, 0.30f), new Vector2(0.323f, 0.42f),
                () => _onRotateLeft?.Invoke());
            PinSize(rotL, IntentBtnW, ElarionUiKit.CanonCtaHeight);

            var rotR = ElarionUiKit.BuildObsidianButton(_intentBar.transform, "Rotate Right",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.371f, 0.30f), new Vector2(0.461f, 0.42f),
                () => _onRotateRight?.Invoke());
            PinSize(rotR, IntentBtnW, ElarionUiKit.CanonCtaHeight);

            var place = ElarionUiKit.BuildObsidianButton(_intentBar.transform, "PLACE",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.524f, 0.30f), new Vector2(0.614f, 0.42f),
                () => _onPlace?.Invoke());
            PinSize(place, PlaceBtnW, ElarionUiKit.CanonCtaHeight);

            var cancel = ElarionUiKit.BuildObsidianButton(_intentBar.transform, "Cancel",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.677f, 0.30f), new Vector2(0.767f, 0.42f),
                () => _onCancel?.Invoke());
            PinSize(cancel, IntentBtnW, ElarionUiKit.CanonCtaHeight);
            cancel.gameObject.name = "BuildHudPlaceCancel";

            _intentBar.SetActive(false);   // Placing state shows it
        }

        // ── Public API the BRAIN drives ────────────────────────────────────────

        public void Show()
        {
            if (_canvas == null) Build();
            if (_canvas != null) _canvas.SetActive(true);
            RefreshResources();
            SetState(_state);
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.SetActive(false);
        }

        /// <summary>Drive the three-state chrome (Grok CoC model).</summary>
        public void SetState(BuildHudState state)
        {
            _state = state;
            // The place-intent bar is exclusive to Placing; Browse/Selected hide it
            // (Selected verbs render on BuildSelectionUI's bar, owned by the brain).
            if (_intentBar != null && _intentBar.activeSelf != (state == BuildHudState.Placing))
                _intentBar.SetActive(state == BuildHudState.Placing);
        }

        /// <summary>Re-read the live wallet (called by the brain on transitions).</summary>
        public void RefreshResources()
        {
            _wallet?.Refresh();
        }

        // ── Consistent-size pin (mirrors ElarionUiKit.PinCanonicalCtaSize) ──────
        /// <summary>
        /// Collapse a kit button's fraction-of-parent anchors to a POINT at the anchor
        /// rect's centre and stamp a fixed <paramref name="w"/> x <paramref name="h"/>
        /// pixel box, so a wide landscape canvas can never stretch it into a thin bar.
        /// Height must be >= ElarionUiKit.MinTouchPx so the kit touch-floor guard no-ops.
        /// Presentation-only: does not restyle or re-wire the button.
        /// </summary>
        private static void PinSize(Button button, float w, float h)
        {
            if (button == null) return;
            var rt = button.transform as RectTransform;
            if (rt == null) return;
            Vector2 centre = (rt.anchorMin + rt.anchorMax) * 0.5f;
            rt.anchorMin = centre;
            rt.anchorMax = centre;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        // ── uGUI helper (BuildPaletteUI/BuildSelectionUI shape) ────────────────
        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
