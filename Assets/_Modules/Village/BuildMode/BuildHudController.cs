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
        private TextMeshProUGUI _placeName;  // "Placing: <name>" — folded into the intent cluster

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

            // "X Done" exit — the ONE exit affordance (the palette's duplicate "Done" is
            // removed). Seated in the top band RIGHT-OF-CENTRE, NOT the screen corner:
            // the FTUE Skip Tutorial + HUD Menu/gear both hug the top-right SCREEN corner
            // (ObjectiveBannerUi anchors Skip Tutorial to (1,1) at y -92), so a corner Done
            // collided with them. Centre ~0.735 keeps Done inside the HUD's own dark band,
            // large + tappable, and clear of that right-edge column on every screen ratio.
            // Pinned to the consistent HUD box (ExitBtnW x CanonCtaHeight) so a wide canvas
            // can never stretch it into a thin bar.
            var exit = ElarionUiKit.BuildObsidianButton(_canvas.transform, "X Done",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.685f, 0.86f), new Vector2(0.785f, 0.99f),
                () => _onExit?.Invoke());
            PinSize(exit, ExitBtnW, ElarionUiKit.CanonCtaHeight);
            FlowTrace.Step("BuildHud",
                "single exit 'X Done' seated right-of-centre in the top band (clear of the " +
                "right-edge Skip Tutorial / Menu column)");
            // Canonical close name (probe/close convention; label stays "X Done").
            exit.gameObject.name = "CloseButton";
        }

        private void BuildIntentBar()
        {
            // The ONE control cluster shown while Placing, hugging the BOTTOM edge (CoC).
            // Because BuildPaletteUI.Collapse now hides EVERY dock background, the whole
            // map/ghost ABOVE this row is visible — no black wall. The bar's own rect
            // carries NO Image (fully transparent) and the pill below is non-raycast, so
            // world taps used to set the drop location pass straight through; only the four
            // buttons eat a tap.
            _intentBar = new GameObject("BuildIntentBar", typeof(RectTransform));
            _intentBar.transform.SetParent(_canvas.transform, false);
            var irt = (RectTransform)_intentBar.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;

            // ── Slim "Placing: <name>" + one-line hint pill, folded into this cluster
            //    (owner: "at most a THIN label, or fold that into the intent bar"). Narrow +
            //    centred + rounded so it reads as a pill, NEVER a wall. Sits just ABOVE the
            //    verb row (both in the bottom third) so the drop zone (screen centre) stays
            //    clear. NON-raycast on the pill AND both text lines so it can never eat the
            //    world tap. The name line is filled by SetPlacingLabel() on Arm/move. ──────
            var hintBack = ElarionUiKit.AddImage(_intentBar.transform, "PlaceHintBack",
                new Vector2(0.30f, 0.185f), new Vector2(0.70f, 0.305f),
                ElarionUiKit.ObsidianFill, rounded: true);
            var hintBackImg = hintBack.GetComponent<Image>();
            if (hintBackImg != null) hintBackImg.raycastTarget = false;
            _placeName = MakeText(hintBack.transform, "Placing: structure",
                22, ElarionUi.Gilt, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.50f), new Vector2(0.96f, 0.98f));
            _placeName.raycastTarget = false;
            var placeHint = MakeText(hintBack.transform, "Tap to set location, then rotate.",
                18, ElarionUi.Parchment, FontStyles.Normal, TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.50f));
            placeHint.raycastTarget = false;

            // ── ONE verb row hugging the bottom edge. Each button is PinSize'd to a FIXED
            //    box centred on its anchor, so a wide LANDSCAPE canvas never stretches it
            //    into a thin bar AND a narrow PORTRAIT canvas scales it proportionally
            //    (fraction anchors + fixed PinSize = the SAME relative row in both
            //    orientations, never truncated/crammed). Centres spread 0.18/0.38/0.60/0.82
            //    so the four boxes seat with even gaps. Row centred at y=0.095 (bottom). ────
            var rotL = ElarionUiKit.BuildObsidianButton(_intentBar.transform, "Rotate Left",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.14f, 0.035f), new Vector2(0.22f, 0.155f),
                () => _onRotateLeft?.Invoke());
            PinSize(rotL, IntentBtnW, ElarionUiKit.CanonCtaHeight);
            AllowTwoLineLabel(rotL);   // never truncate "Rotate Left" -> wrap to 2 lines

            var rotR = ElarionUiKit.BuildObsidianButton(_intentBar.transform, "Rotate Right",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.34f, 0.035f), new Vector2(0.42f, 0.155f),
                () => _onRotateRight?.Invoke());
            PinSize(rotR, IntentBtnW, ElarionUiKit.CanonCtaHeight);
            AllowTwoLineLabel(rotR);   // never truncate "Rotate Right" -> wrap to 2 lines

            var place = ElarionUiKit.BuildObsidianButton(_intentBar.transform, "PLACE",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.55f, 0.035f), new Vector2(0.65f, 0.155f),
                () => _onPlace?.Invoke());
            PinSize(place, PlaceBtnW, ElarionUiKit.CanonCtaHeight);

            var cancel = ElarionUiKit.BuildObsidianButton(_intentBar.transform, "Cancel",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.78f, 0.035f), new Vector2(0.86f, 0.155f),
                () => _onCancel?.Invoke());
            PinSize(cancel, IntentBtnW, ElarionUiKit.CanonCtaHeight);
            cancel.gameObject.name = "BuildHudPlaceCancel";

            _intentBar.SetActive(false);   // Placing state shows it
            FlowTrace.Step("BuildHud",
                "intent bar rebuilt: bottom-edge [Rotate Left][Rotate Right][PLACE][Cancel] row " +
                "+ slim Placing pill above; map/ghost stays visible (dock fully collapsed)");
        }

        // ── Rotate labels never truncate (owner felt-test 2026-07-16) ───────────
        /// <summary>
        /// Opt a kit button's TMP label OUT of the kit's single-line ellipsis fit
        /// (ElarionUiKit.FitSingleLine leaves it NoWrap + Ellipsis, which clips
        /// "Rotate Right" to "Rotate Ri..."). We flip it to normal WRAP + Overflow so it
        /// wraps to two lines and NEVER culls a glyph — the button is pinned tall
        /// (CanonCtaHeight = 132) so two lines seat comfortably. Autosizing off keeps the
        /// size deterministic (no shrink-to-nothing). Presentation-only.
        /// </summary>
        private static void AllowTwoLineLabel(Button button)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;   // wrap, do not clip
            label.overflowMode = TextOverflowModes.Overflow;     // never ellipsis-cull a line
            label.alignment = TextAlignmentOptions.Center;
            FlowTrace.Step("BuildHud",
                "rotate label '" + (label.text ?? "") + "' set to wrap (2-line, no truncate)");
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

        /// <summary>
        /// Fold the "Placing: &lt;name&gt;" label into the intent cluster (owner redesign:
        /// "at most a thin label, or fold that into the intent bar"). Called by the brain on
        /// Arm / begin-move so the collapsed shop needs no black summary panel of its own.
        /// ASCII only; presentation-only; safe before the bar is built (no-op).
        /// </summary>
        public void SetPlacingLabel(string displayName)
        {
            if (_placeName == null) return;
            string n = string.IsNullOrEmpty(displayName) ? "structure" : displayName;
            _placeName.text = "Placing: " + n;
            FlowTrace.Step("BuildHud", "placing label folded into intent bar: " + n);
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
