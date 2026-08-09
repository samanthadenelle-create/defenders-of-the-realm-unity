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

        // ── WO-1010 P1: CHIPS ON THE GHOST (CoC grammar) ───────────────────────
        // External testers (2026-08-08) could not read the build screen: "buttons
        // everywhere". The four word-buttons sat at the bottom edge while the thing they
        // acted on was under the player's finger in the middle of the field, so the
        // controls and their subject were never in the same place. The chips move TO the
        // ghost. Visual circle is small; the HIT AREA is a full MinTouch box around it —
        // invisible padding, NOT visual growth (the chip must not become a slab).
        private const float ChipVisualPx = 52f;
        private const float ChipHitPx    = ElarionUiKit.MinTouchPx;   // 112
        private const float ChipGapPx    = 12f;
        private const float ChipEdgePx   = 3f;    // accent border thickness on each chip
        private const float GhostPillW   = 620f;  // widened: the first capture wrapped the
        private const float GhostPillH   = 56f;   // name+cost onto 2 lines and overflowed
        private const float GhostPillLiftPx = 96f;   // pill floats ABOVE the ghost anchor
        private const float ChipDropPx      = 78f;   // chips sit BELOW/beside the anchor
        private const float SafePadPx       = 24f;   // never let a chip touch the screen edge
        private const float DpadToggleW     = 96f;

        // Callbacks into the BRAIN (BuildModeController wires these).
        private Action _onRotateLeft;
        private Action _onRotateRight;
        private Action _onPlace;
        private Action _onCancel;
        private Action _onExit;

        private GameObject _canvas;
        private GameObject _intentBar;       // shown only in Placing (WO-1010: now the CHIP cluster)
        private BuildWalletRow _wallet;
        private BuildHudState _state = BuildHudState.Browse;
        private TextMeshProUGUI _placeName;  // name + cost, floated above the ghost

        // ── WO-1010 P1 state ───────────────────────────────────────────────────
        private RectTransform _canvasRect;
        private RectTransform _ghostPill;    // name + cost, above the ghost
        private RectTransform _chipCluster;  // OK / Rot / X, beside the ghost
        private Button _okChip;
        private TextMeshProUGUI _okChipLabel;
        private Image _okChipRing;
        private GameObject _dpadHost;        // the nudge pad — OFF unless toggled
        private TextMeshProUGUI _dpadToggleLabel;
        private bool _dpadShown;

        private bool _hasGhostAnchor;
        private Vector2 _ghostScreenPoint;
        private bool _ghostValid = true;
        private string _ghostBlockReason = string.Empty;
        /// <summary>Name + cost as authored, kept separate so the blocked reason can be
        /// appended and removed without the base line being lost to string surgery.</summary>
        private string _pillBase = "structure";

        /// <summary>
        /// Live direction from the build-owned nudge pad (zero when released or hidden).
        /// The BRAIN polls this and folds it into its existing pending-drop nudge, so the
        /// pad needs NO cross-assembly reach into the shared HUD's pad.
        /// </summary>
        public Vector2 NudgeVector { get; private set; }

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
            _canvasRect = _canvas.transform as RectTransform;

            BuildTopBar();
            BuildIntentBar();
            BuildDpadToggle();
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

        // =====================================================================
        //  WO-1010 P1 — the ghost carries its own controls
        // =====================================================================
        /// <summary>
        /// Builds the name+cost pill and the OK / Rot / X chip cluster. Both are SCREEN-SPACE
        /// UI that follows the ghost's projected point (pushed by the brain via TrackGhost) —
        /// deliberately NOT world-space billboards, which shrink with zoom and would fall
        /// under the MinTouch floor exactly when the player is placing a small wall piece.
        /// </summary>
        private void BuildIntentBar()
        {
            _intentBar = new GameObject("BuildGhostControls", typeof(RectTransform));
            _intentBar.transform.SetParent(_canvas.transform, false);
            var irt = (RectTransform)_intentBar.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;

            // ── Name + cost pill, floating above the ghost. One line, one place — this
            //    absorbs the old "Placing: <name>" hint pill AND the cost readout the
            //    player previously had to find back on the card. Non-raycast so it can
            //    never eat a world tap meant for the ground.
            // Gold edge around a near-black fill, for the same reason as the chips: the pill
            // floats over live terrain and cannot rely on the ground behind it for contrast.
            var pillEdge = ElarionUiKit.AddImage(_intentBar.transform, "GhostPill",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), ElarionUi.Gilt, rounded: true);
            _ghostPill = pillEdge.transform as RectTransform;
            if (_ghostPill != null)
            {
                _ghostPill.anchorMin = _ghostPill.anchorMax = new Vector2(0.5f, 0.5f);
                _ghostPill.pivot = new Vector2(0.5f, 0.5f);
                _ghostPill.sizeDelta = new Vector2(GhostPillW, GhostPillH);
            }
            var pillEdgeImg = pillEdge.GetComponent<Image>();
            if (pillEdgeImg != null) pillEdgeImg.raycastTarget = false;

            var pillFill = ElarionUiKit.AddImage(pillEdge.transform, "GhostPillFill",
                new Vector2(0f, 0f), new Vector2(1f, 1f), ElarionUiKit.ObsidianFill, rounded: true);
            var pillFillRt = pillFill.transform as RectTransform;
            if (pillFillRt != null)
            {
                pillFillRt.offsetMin = new Vector2(ChipEdgePx, ChipEdgePx);
                pillFillRt.offsetMax = new Vector2(-ChipEdgePx, -ChipEdgePx);
            }
            var pillFillImg = pillFill.GetComponent<Image>();
            if (pillFillImg != null) pillFillImg.raycastTarget = false;

            _placeName = MakeText(pillFill.transform, "structure", 22, ElarionUi.Gilt,
                FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.98f));
            _placeName.raycastTarget = false;
            // ONE LINE. The first capture wrapped "Arcane Spire - 88 wood, 88 iron, 187
            // crystals" onto two lines and pushed it out of the pill. A long name shrinks to
            // fit rather than escaping its own background.
            _placeName.textWrappingMode = TextWrappingModes.NoWrap;
            _placeName.enableAutoSizing = true;
            _placeName.fontSizeMin = 14f;
            _placeName.fontSizeMax = 22f;

            // ── The chip cluster. Three round chips in a row, pivoted CENTRE so the
            //    clamp math below can keep the whole cluster on-screen as one unit.
            var cluster = new GameObject("GhostChips", typeof(RectTransform));
            cluster.transform.SetParent(_intentBar.transform, false);
            _chipCluster = (RectTransform)cluster.transform;
            _chipCluster.anchorMin = _chipCluster.anchorMax = new Vector2(0.5f, 0.5f);
            _chipCluster.pivot = new Vector2(0.5f, 0.5f);
            _chipCluster.sizeDelta = new Vector2(ChipHitPx * 3f + ChipGapPx * 2f, ChipHitPx);

            float step = ChipHitPx + ChipGapPx;
            _okChip = MakeChip(_chipCluster, "OkChip", "OK", ElarionUi.Gilt, -step,
                () => _onPlace?.Invoke(), out _okChipLabel, out _okChipRing);
            MakeChip(_chipCluster, "RotChip", "Rot", ElarionUi.Parchment, 0f,
                () => _onRotateRight?.Invoke(), out _, out _);
            MakeChip(_chipCluster, "CancelChip", "X", new Color(0.86f, 0.32f, 0.30f), step,
                () => _onCancel?.Invoke(), out _, out _);

            // Kept as the canonical cancel name so any probe/close convention still resolves
            // it after the word-button retirement.
            var cancelChip = _chipCluster.Find("CancelChip");
            if (cancelChip != null) cancelChip.gameObject.name = "BuildHudPlaceCancel";

            _intentBar.SetActive(false);   // Placing state shows it
            FlowTrace.Step("BuildHud",
                "WO-1010 P1: intent bar RETIRED -> chips on the ghost [OK][Rot][X] + name/cost pill; " +
                "controls now sit where the player is looking, not at the bottom edge");
        }

        /// <summary>
        /// One chip: a MinTouch-sized INVISIBLE hit box with a small visible circle inside it.
        /// The transparent parent Image is the raycast target, so the tappable area is ~112px
        /// while the art stays ~52px — the WO's invisible-padding rule. Growing the visual
        /// instead would put three slabs over the field and undo the point of the redesign.
        /// </summary>
        private static Button MakeChip(RectTransform parent, string name, string label,
            Color accent, float xOffset, Action onClick,
            out TextMeshProUGUI labelOut, out Image ringOut)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ChipHitPx, ChipHitPx);
            rt.anchoredPosition = new Vector2(xOffset, 0f);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // invisible padding, still raycastable
            hit.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = hit;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // ── The visible chip: an ACCENT-COLOURED EDGE around a near-black fill. ──
            // The first build used a plain ObsidianFill circle, and the capture showed why
            // that fails: ObsidianFill is (0.02,0.02,0.025) — effectively black — so the chip
            // was black-on-black and only the bare label floated over the field. A chip that
            // follows the ghost sits over ARBITRARY terrain (pale sand, dark water, grass), so
            // it cannot borrow contrast from whatever happens to be behind it; it has to carry
            // its own edge. The edge also gives each chip a second, non-textual identity
            // (gold confirm / grey rotate / red cancel) WITHOUT meaning ever resting on colour
            // alone, because the label already says which is which.
            var edge = ElarionUiKit.AddImage(go.transform, "ChipEdge",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), accent, rounded: true);
            var edgeRt = edge.transform as RectTransform;
            if (edgeRt != null)
            {
                edgeRt.anchorMin = edgeRt.anchorMax = new Vector2(0.5f, 0.5f);
                edgeRt.pivot = new Vector2(0.5f, 0.5f);
                edgeRt.sizeDelta = new Vector2(ChipVisualPx, ChipVisualPx);
            }
            var edgeImg = edge.GetComponent<Image>();
            if (edgeImg != null) edgeImg.raycastTarget = false;

            var fill = ElarionUiKit.AddImage(edge.transform, "ChipFill",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), ElarionUiKit.ObsidianFill, rounded: true);
            var fillRt = fill.transform as RectTransform;
            if (fillRt != null)
            {
                fillRt.anchorMin = fillRt.anchorMax = new Vector2(0.5f, 0.5f);
                fillRt.pivot = new Vector2(0.5f, 0.5f);
                fillRt.sizeDelta = new Vector2(ChipVisualPx - ChipEdgePx * 2f, ChipVisualPx - ChipEdgePx * 2f);
            }
            ringOut = fill.GetComponent<Image>();
            if (ringOut != null) ringOut.raycastTarget = false;

            labelOut = MakeText(fill.transform, label, 20, accent, FontStyles.Bold,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            labelOut.raycastTarget = false;
            return btn;
        }

        /// <summary>
        /// The nudge pad's corner toggle. The pad is OFF by default (it was permanently
        /// on-screen before, and testers counted it among the "buttons everywhere"), but it
        /// stays reachable because pixel-precise nudging is what makes a long wall run
        /// placeable at all. Built on the Core kit's own d-pad seam, so no new reflection
        /// bridge into the HUD assembly is introduced.
        /// </summary>
        private void BuildDpadToggle()
        {
            var toggle = ElarionUiKit.BuildObsidianButton(_canvas.transform, "+",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.02f, 0.04f), new Vector2(0.09f, 0.16f),
                ToggleDpad);
            PinSize(toggle, DpadToggleW, ElarionUiKit.CanonCtaHeight);
            toggle.gameObject.name = "BuildNudgePadToggle";
            _dpadToggleLabel = toggle.GetComponentInChildren<TMP_Text>(true) as TextMeshProUGUI;

            var padHost = new GameObject("BuildNudgePad", typeof(RectTransform));
            padHost.transform.SetParent(_canvas.transform, false);
            var prt = (RectTransform)padHost.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            ElarionUiKit.BuildVirtualDPad(padHost.transform, new Vector2(0.12f, 0.26f),
                v => NudgeVector = v);
            _dpadHost = padHost;
            _dpadHost.SetActive(false);
            _dpadShown = false;
        }

        private void ToggleDpad()
        {
            _dpadShown = !_dpadShown;
            if (_dpadHost != null) _dpadHost.SetActive(_dpadShown);
            if (!_dpadShown) NudgeVector = Vector2.zero;   // a hidden pad must not keep steering
            if (_dpadToggleLabel != null) _dpadToggleLabel.text = _dpadShown ? "-" : "+";
            FlowTrace.Step("BuildHud", "nudge pad toggled " + (_dpadShown ? "ON" : "OFF") +
                " (off by default — WO-1010 retires the always-on d-pad)");
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
            // The ghost controls are exclusive to Placing; Browse/Selected hide them
            // (Selected verbs render on BuildSelectionUI's bar, owned by the brain).
            bool placing = state == BuildHudState.Placing;
            if (_intentBar != null && _intentBar.activeSelf != placing)
                _intentBar.SetActive(placing);

            // The nudge pad only makes sense while something is being positioned. Leaving it
            // up in Browse would put back a permanent on-screen control, which is the thing
            // WO-1010 set out to remove.
            if (!placing && _dpadShown) ToggleDpad();

            // Leaving a stale anchor behind would park the chips wherever the last ghost
            // died until the next push; drop it with the state.
            if (!placing) _hasGhostAnchor = false;
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
            _pillBase = n;
            _placeName.text = n;
            FlowTrace.Step("BuildHud", "ghost pill label: " + n);
        }

        /// <summary>
        /// WO-1010: name AND cost on the ghost's own pill. The cost used to live only on the
        /// card the player already dismissed, so during placement — the exact moment they are
        /// deciding whether to commit — the price was off-screen. ASCII only.
        /// </summary>
        public void SetPlacingLabel(string displayName, string costLine)
        {
            if (_placeName == null) return;
            string n = string.IsNullOrEmpty(displayName) ? "structure" : displayName;
            _pillBase = string.IsNullOrEmpty(costLine) ? n : n + " - " + costLine;
            _placeName.text = _pillBase;
            FlowTrace.Step("BuildHud", "ghost pill: " + _pillBase);
        }

        /// <summary>
        /// Push the ghost's PROJECTED SCREEN POINT plus its validity. The BRAIN projects
        /// (it owns the camera) — presentation never touches a world object or a camera,
        /// which is the layering rule this HUD exists to keep.
        ///
        /// <paramref name="blockedReason"/> is shown on the OK chip when placement is
        /// invalid, so the refusal is READABLE TEXT rather than a colour the player has to
        /// interpret — validity here is shape + word, never tint alone.
        /// </summary>
        public void TrackGhost(Vector2 screenPoint, bool valid, string blockedReason)
        {
            _ghostScreenPoint = screenPoint;
            _hasGhostAnchor = true;
            if (valid != _ghostValid)
            {
                _ghostValid = valid;
                FlowTrace.Step("BuildHud", "ghost validity -> " + (valid ? "OK" : "BLOCKED"));
            }
            _ghostBlockReason = blockedReason ?? string.Empty;
        }

        /// <summary>
        /// Follow the ghost. LateUpdate so the anchor pushed this frame is already current.
        /// Runs only while Placing, so Browse costs nothing.
        /// </summary>
        private void LateUpdate()
        {
            LayoutGhostControlsNow();
        }

        /// <summary>
        /// The follow/clamp pass, callable directly. LateUpdate drives it at runtime; the
        /// headless UI capture calls it explicitly because MonoBehaviour ticks do NOT run in
        /// edit mode — without this the capture would photograph the chips parked at the
        /// canvas centre and the screenshot would prove nothing about the edge-clamp rule it
        /// is meant to verify.
        /// </summary>
        public void LayoutGhostControlsNow()
        {
            if (_state != BuildHudState.Placing || !_hasGhostAnchor) return;
            if (_canvasRect == null || _chipCluster == null) return;

            // Screen -> canvas-local. Overlay canvases take a NULL camera here; passing one
            // silently offsets everything.
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, _ghostScreenPoint, null, out local))
                return;

            Vector2 half = _canvasRect.rect.size * 0.5f;

            // CLAMP AS A UNIT. A chip that walks off-screen when the ghost nears an edge is
            // an unplaceable building — the acceptance criteria call this out because it is
            // the failure the old bottom-edge bar could not have.
            Vector2 chipHalf = _chipCluster.sizeDelta * 0.5f;
            Vector2 chipPos = new Vector2(local.x, local.y - ChipDropPx);
            chipPos.x = Mathf.Clamp(chipPos.x, -half.x + chipHalf.x + SafePadPx, half.x - chipHalf.x - SafePadPx);
            chipPos.y = Mathf.Clamp(chipPos.y, -half.y + chipHalf.y + SafePadPx, half.y - chipHalf.y - SafePadPx);
            _chipCluster.anchoredPosition = chipPos;

            if (_ghostPill != null)
            {
                // ── THE PILL IS PLACED RELATIVE TO THE *CLAMPED* CHIPS, NOT THE GHOST. ──
                // Clamping the two independently is what the first edge capture caught: in a
                // corner they each satisfied "fully on-screen" and then landed ON TOP OF EACH
                // OTHER, with the chips covering the cost text. Two separately-correct clamps
                // can still produce one unreadable result, so the pair is positioned as a unit.
                Vector2 pillHalf = _ghostPill.sizeDelta * 0.5f;
                float gap = 14f;
                float topLimit    = half.y - pillHalf.y - SafePadPx;
                float bottomLimit = -half.y + pillHalf.y + SafePadPx;

                // Prefer above the chips; if there is no room up there, sit below them.
                float pillY = chipPos.y + chipHalf.y + pillHalf.y + gap;
                if (pillY > topLimit)
                {
                    float below = chipPos.y - chipHalf.y - pillHalf.y - gap;
                    if (below >= bottomLimit) pillY = below;
                    else pillY = Mathf.Clamp(pillY, bottomLimit, topLimit);
                }

                Vector2 pillPos = new Vector2(local.x, pillY);
                pillPos.x = Mathf.Clamp(pillPos.x, -half.x + pillHalf.x + SafePadPx, half.x - pillHalf.x - SafePadPx);
                _ghostPill.anchoredPosition = pillPos;
            }

            // ── THE VERDICT: short word on the chip, full reason on the PILL. ──────
            // The first capture put the whole reason ON the chip and "Not enough Wood" wrapped
            // to four lines and spilled outside a 52px circle — unreadable, and it covered the
            // other chips. A sentence needs the WIDE surface; the chip only ever has room for
            // a verb. So the chip says OK / No, and the pill — which is already 620px and
            // right above the ghost — carries the why. Both are words, so the refusal is still
            // never communicated by colour alone.
            if (_okChipLabel != null)
            {
                string want = _ghostValid ? "OK" : "No";
                if (_okChipLabel.text != want) _okChipLabel.text = want;
                _okChipLabel.color = _ghostValid ? ElarionUi.Gilt : new Color(0.86f, 0.32f, 0.30f);
            }
            if (_okChipRing != null) { /* fill stays obsidian; the EDGE carries chip identity */ }
            if (_okChip != null) _okChip.interactable = _ghostValid;

            if (_placeName != null)
            {
                string want = (!_ghostValid && !string.IsNullOrEmpty(_ghostBlockReason))
                    ? _pillBase + " - " + _ghostBlockReason
                    : _pillBase;
                if (_placeName.text != want) _placeName.text = want;
                _placeName.color = _ghostValid ? ElarionUi.Gilt : new Color(0.93f, 0.55f, 0.45f);
            }
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
