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
//   Browse   — shop open; NO verb rail; tap a placed building to select it.
//   Placing  — the LEAN RIGHT-EDGE RAIL (confirm / rotate / cancel); the shop
//              collapses to the armed-card summary (BuildPaletteUI.Collapse).
//   Selected — the selection verbs (Move/Upgrade/Sell/Cancel) render on the SAME
//              bar family, owned by BuildSelectionUI (kept — the fleet's
//              AssertBuildMoveChain SELECT link asserts that panel renders).
//
// WO-1010 §7 OWNER RULINGS D14 / D10 / D17 / D19 (2026-08-08) — THE RIGHT EDGE AND
// THE BOTTOM BAND ARE THE ONLY CHROME:
//   D14 — the three placement verbs live in a LEAN, FIXED right-edge rail (right-thumb
//         territory in landscape). The old "cluster flanks the ghost and flips sides
//         near an edge" follow logic is DELETED: no chrome sits on or beside the piece
//         any more, so the rail needs no per-frame layout at all. The ghost keeps ONLY
//         its name+cost pill, which still follows and still clamps.
//   D10 — the exit is a COMPACT CORNER control labelled "Done" (the "X" glyph is
//         dropped). Small visual, full MinTouch invisible hit pad. It caps the rail's
//         column from the true top-right corner.
//   D17 — the rail speaks ICON language where real SPRITE art exists. Cancel renders
//         RpgUiCatalog element/cross. Confirm and rotate have NO check-mark / circular-
//         arrow sprite anywhere in the pack, so they KEEP their ASCII words: a typed
//         "⟳" would render as tofu on the shipped TMP atlas (the ASCII rule), and this
//         lane does not invent art. The moment those two sprites land, pass them to
//         MakeVerb and the words drop out with no other change.
//   D19 — the resource strip moved OFF the top: it is now ONE THIN bottom-centre
//         obsidian band (fixed pixel height), display-only, seated so the carousel and
//         the placement hint own the band above it. With the strip gone the old
//         full-width top bar had no tenant left and was deleted outright — it was 10%
//         of the field in raycast-blocking chrome for a decorative label.
//
// The category tabs + the icon-first card carousel stay in BuildPaletteUI's own canvas
// (its card/art/cost/economy/arm logic is reused verbatim, not rebuilt); this
// controller sequences them by state. RailReservedWidthPx / ResourceStripReservedPx are
// PUBLIC so the carousel lane can seat clear of this lane's bands without a cross-edit.
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

        // ── WO-1010 P1: SMALL VISUAL INSIDE A MINTOUCH HIT PAD ─────────────────
        // External testers (2026-08-08) could not read the build screen: "buttons
        // everywhere". Every verb here therefore keeps a SMALL visible plate inside an
        // INVISIBLE MinTouch hit box — padding, never visual growth. Growing the visual
        // to 112 would put opaque slabs down the right edge and undo the redesign.
        private const float ChipVisualPx = 52f;
        private const float ChipHitPx    = ElarionUiKit.MinTouchPx;   // 112
        private const float ChipEdgePx   = 3f;    // accent border thickness on each plate
        private const float ChipIconPx   = 30f;   // sprite glyph inside the 52px plate
        private const float GhostPillW   = 620f;  // widened: the first capture wrapped the
        private const float GhostPillH   = 56f;   // name+cost onto 2 lines and overflowed
        private const float GhostPillLiftPx = 96f;   // pill floats ABOVE the ghost anchor
        private const float SafePadPx       = 24f;   // never let the pill touch the screen edge

        // ── D14: THE LEAN RIGHT-EDGE RAIL (fixed — no per-frame layout) ────────
        // Every dimension is FIXED PIXELS at the 1920x1080 reference (never a fraction
        // of screen — a wide canvas stretches fraction anchors into thin bars). Band
        // width = one MinTouch hit column plus a pad each side, so the three 112px hit
        // boxes sit inside the band instead of straddling its trim.
        private const float RailPadPx       = 10f;
        private const float RailGutterPx    = 14f;   // gutter between stacked verbs
        private const float RailEdgeInsetPx = 24f;   // band's own inset from the screen edge
        private const float RailBandW       = ChipHitPx + RailPadPx * 2f;                    // 132
        private const float RailBandH       = ChipHitPx * 3f + RailGutterPx * 2f + RailPadPx * 2f; // 384

        /// <summary>
        /// Horizontal band (from the RIGHT screen edge, in 1920x1080 reference px) that the
        /// D14 rail owns. PUBLIC so the carousel / quick-tab lane can seat clear of it
        /// without reaching into this file — the D7 lesson was that two surfaces drawing in
        /// one band is the defect, not either surface on its own.
        /// </summary>
        public const float RailReservedWidthPx = RailEdgeInsetPx + RailBandW;

        // ── D10: the compact corner Done ───────────────────────────────────────
        // Visual shrinks; the hit area does not. The hit pad is a full MinTouch box in the
        // true top-right corner and the visible plate is centred inside it, so the art sits
        // ~80 reference px in from both edges — well clear of a rounded corner or cutout
        // WITHOUT a live-Screen safe-area read (which would make the headless capture
        // non-deterministic; fixed reference px is the house rule anyway).
        private const float CornerInsetPx  = 24f;
        private const float DoneVisualPx   = 76f;

        // ── D19: the thin bottom-centre resource strip ─────────────────────────
        // BuildWalletRow authors 150x64 chips at 10px spacing and seats its row 24px in
        // from its parent's left edge; the band is sized to that content so the row reads
        // as ONE slim frame rather than a panel with a row parked in it. Display-only, so
        // MinTouch does not apply (WO-1010 D19 states this explicitly).
        private const float StripChipW    = 150f;
        private const float StripChipH    = 64f;
        private const float StripChipGap  = 10f;
        private const float StripSideInset = 24f;   // BuildWalletRow's own left offset
        private const float StripPadPx     = 8f;
        private const float StripBandH     = StripChipH + StripPadPx * 2f;   // 80
        private const float StripBottomPx  = 18f;

        /// <summary>
        /// Vertical band (from the BOTTOM screen edge, in 1920x1080 reference px) that the
        /// D19 resource strip owns. PUBLIC for the same reason as
        /// <see cref="RailReservedWidthPx"/>: the carousel must rest ABOVE this, and in
        /// PLACE the single hint line sits above the strip, never on it.
        /// </summary>
        public const float ResourceStripReservedPx = StripBottomPx + StripBandH;

        // Callbacks into the BRAIN (BuildModeController wires these).
        private Action _onRotateLeft;
        private Action _onRotateRight;
        private Action _onPlace;
        private Action _onCancel;
        private Action _onExit;

        private GameObject _canvas;
        private GameObject _intentBar;       // shown only in Placing (WO-1010 D14: pill + rail)
        private BuildWalletRow _wallet;
        private BuildHudState _state = BuildHudState.Browse;
        private TextMeshProUGUI _placeName;  // name + cost, floated above the ghost

        // ── WO-1010 P1 state ───────────────────────────────────────────────────
        private RectTransform _canvasRect;
        private RectTransform _ghostPill;    // name + cost, above the ghost (the ONE thing that follows)
        private RectTransform _verbRail;     // D14: confirm / rotate / cancel, FIXED at the right edge
        private Button _okChip;
        private TextMeshProUGUI _okChipLabel;
        private Image _okChipRing;
        private GameObject _dpadHost;        // the nudge pad — OFF unless toggled

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

            BuildResourceStrip();   // D19 — thin bottom-centre band
            BuildCornerDone();      // D10 — compact corner exit, caps the rail column
            BuildIntentBar();       // D14 — ghost pill + the fixed right-edge verb rail
            BuildDpadToggle();
            FlowTrace.Step("BuildHud",
                "chrome-built (WO-1010 D10/D14/D19): compact corner Done + fixed right rail (" +
                RailReservedWidthPx + "px reserved) + thin bottom strip (" +
                ResourceStripReservedPx + "px reserved); the old full-width top bar is GONE");

            _canvas.SetActive(false);   // built hidden; Show shows it
        }

        /// <summary>
        /// D19 — the resource strip as ONE THIN bottom-centre band, not a top panel.
        /// The band is sized to BuildWalletRow's actual chip content so it hugs the numbers
        /// instead of being a slab with a row parked in it, and it carries its OWN gold edge:
        /// ObsidianFill is (0.02,0.02,0.025) — effectively black — so a band floating over
        /// live terrain cannot borrow contrast from whatever happens to be behind it.
        /// Display-only (no MinTouch floor), but it DOES eat taps so a mis-aimed read of the
        /// wallet never falls through and drags the ghost.
        /// </summary>
        private void BuildResourceStrip()
        {
            var edge = ElarionUiKit.AddImage(_canvas.transform, "BuildResourceStrip",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), ElarionUi.Gilt, rounded: true);
            var edgeRt = edge.transform as RectTransform;
            if (edgeRt != null)
            {
                edgeRt.anchorMin = edgeRt.anchorMax = new Vector2(0.5f, 0f);
                edgeRt.pivot = new Vector2(0.5f, 0f);
                edgeRt.anchoredPosition = new Vector2(0f, StripBottomPx);
                edgeRt.sizeDelta = new Vector2(StripSideInset * 2f + StripChipW, StripBandH);
            }
            var edgeImg = edge.GetComponent<Image>();
            if (edgeImg != null) edgeImg.raycastTarget = true;

            var fill = ElarionUiKit.AddImage(edge.transform, "StripFill",
                new Vector2(0f, 0f), new Vector2(1f, 1f), ElarionUiKit.ObsidianFill, rounded: true);
            var fillRt = fill.transform as RectTransform;
            if (fillRt != null)
            {
                fillRt.offsetMin = new Vector2(ChipEdgePx, ChipEdgePx);
                fillRt.offsetMax = new Vector2(-ChipEdgePx, -ChipEdgePx);
            }
            var fillImg = fill.GetComponent<Image>();
            if (fillImg != null) fillImg.raycastTarget = false;

            var walletGo = new GameObject("BuildWalletRow");
            walletGo.transform.SetParent(fill.transform, false);
            _wallet = walletGo.AddComponent<BuildWalletRow>();
            _wallet.Build(fill.transform);

            // Size the band to the row it actually built. The pool list is DATA (the wallet
            // DTO), so counting the chips beats hard-coding "five" — a sixth pool would
            // otherwise silently overflow the frame it is supposed to sit inside.
            int chips = 0;
            var row = fill.transform.Find("WalletChips");
            if (row != null)
            {
                for (int i = 0; i < row.childCount; i++)
                    if (row.GetChild(i).name.StartsWith("Chip_", StringComparison.Ordinal)) chips++;
            }
            if (chips > 0 && edgeRt != null)
            {
                float content = chips * StripChipW + (chips - 1) * StripChipGap;
                edgeRt.sizeDelta = new Vector2(StripSideInset * 2f + content, StripBandH);
            }
            FlowTrace.Step("BuildHud",
                "D19 resource strip: thin bottom-centre band, " + chips + " pool(s), h=" +
                StripBandH + "px fixed -- reserves " + ResourceStripReservedPx +
                "px of the bottom band (carousel + hint seat ABOVE it)");
        }

        /// <summary>
        /// D10 — the exit as a COMPACT CORNER control. The owner's markup was verbatim
        /// "Move to Corner Remove the X, Size smaller and more minized": the label loses the
        /// "X" glyph and reads just "Done", the visible plate shrinks to <see cref="DoneVisualPx"/>,
        /// and the 112px MinTouch floor is carried by the SAME invisible hit pad the rail verbs
        /// use — the visual never grows into a slab. Seated in the true top-right corner on the
        /// rail's own column so the right edge reads as ONE stack: Done, then the three verbs.
        ///
        /// KNOWN NEIGHBOUR: ObjectiveBannerUi anchors "Skip Tutorial" to (1,1) at y -92, which
        /// is this corner. The owner ruled the corner anyway and WO-1010 D16 re-targets the
        /// tutorial chrome (and resolves the two-skip nit) in the same pass — flagged, not
        /// silently worked around by parking Done somewhere it was not ruled to be.
        /// </summary>
        private void BuildCornerDone()
        {
            var host = new GameObject("BuildDoneCorner", typeof(RectTransform));
            host.transform.SetParent(_canvas.transform, false);
            var hrt = (RectTransform)host.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(1f, 1f);
            hrt.sizeDelta = new Vector2(ChipHitPx, ChipHitPx);
            // x inset lines the hit box's centre up with the rail band's centre, so the corner
            // control and the rail read as one right-edge column instead of two loose things.
            hrt.anchoredPosition = new Vector2(-(RailEdgeInsetPx + (RailBandW - ChipHitPx) * 0.5f),
                                               -CornerInsetPx);

            var done = MakeVerb(hrt, "DoneCorner", "Done", null, ElarionUi.Gilt,
                Vector2.zero, DoneVisualPx, 18f, () => _onExit?.Invoke(), out _, out _);
            // Canonical close name (probe/close convention; the label is now just "Done").
            done.gameObject.name = "CloseButton";
            FlowTrace.Step("BuildHud",
                "D10 exit: compact corner 'Done' (visual " + DoneVisualPx + "px inside a " +
                ChipHitPx + "px hit pad) -- caps the right-edge rail column");
        }

        // =====================================================================
        //  WO-1010 P1 — the ghost carries its own controls
        // =====================================================================
        /// <summary>
        /// Builds the ghost's name+cost pill and the D14 right-edge verb rail.
        ///
        /// THE SPLIT IS THE RULING: the PILL follows the ghost's projected screen point
        /// (pushed by the brain via TrackGhost) — screen-space, never a world-space billboard,
        /// which would shrink with zoom and fall under the MinTouch floor exactly when the
        /// player is placing a small wall piece. The RAIL does not follow anything. It is a
        /// slim FIXED column hugging the right edge, so the piece being placed is never
        /// covered by its own controls and there is no per-frame layout to get wrong.
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

            // ── D14: THE LEAN RIGHT-EDGE RAIL ─────────────────────────────────────
            // A slim obsidian band with its own gold trim, hugging the right edge and
            // vertically centred — right-thumb territory in landscape, and the same column
            // the compact corner Done sits at the top of. The band is a raycast target so a
            // tap that lands in a gutter is eaten by the chrome instead of falling through
            // and dragging the ghost out from under the player's other hand.
            var railEdge = ElarionUiKit.AddImage(_intentBar.transform, "GhostVerbRail",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), ElarionUi.Gilt, rounded: true);
            _verbRail = railEdge.transform as RectTransform;
            if (_verbRail != null)
            {
                _verbRail.anchorMin = _verbRail.anchorMax = new Vector2(1f, 0.5f);
                _verbRail.pivot = new Vector2(1f, 0.5f);
                _verbRail.anchoredPosition = new Vector2(-RailEdgeInsetPx, 0f);
                _verbRail.sizeDelta = new Vector2(RailBandW, RailBandH);
            }
            var railEdgeImg = railEdge.GetComponent<Image>();
            if (railEdgeImg != null) railEdgeImg.raycastTarget = true;

            var railFill = ElarionUiKit.AddImage(railEdge.transform, "RailFill",
                new Vector2(0f, 0f), new Vector2(1f, 1f), ElarionUiKit.ObsidianFill, rounded: true);
            var railFillRt = railFill.transform as RectTransform;
            if (railFillRt != null)
            {
                railFillRt.offsetMin = new Vector2(ChipEdgePx, ChipEdgePx);
                railFillRt.offsetMax = new Vector2(-ChipEdgePx, -ChipEdgePx);
            }
            var railFillImg = railFill.GetComponent<Image>();
            if (railFillImg != null) railFillImg.raycastTarget = false;

            // ── D17: icon language WHERE REAL SPRITE ART EXISTS, words where it does not.
            // Only the cancel glyph is in the pack (element/cross). There is no check-mark
            // and no circular-arrow sprite anywhere under Resources/RpgUi, and a "⟳" typed
            // into TMP renders as a TOFU BOX on the shipped atlas — so confirm and rotate
            // keep ASCII words rather than shipping a square. RpgUiCatalog's contract is
            // sprite-or-null and every caller keeps its glyph fallback; MakeVerb honours it,
            // so dropping the two missing sprites into the pack is the ONLY change needed
            // to finish D17.
            Sprite cancelIcon = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementCross);
            if (cancelIcon == null)
                FlowTrace.Warn("BuildHud",
                    "D17: element/cross sprite absent -- cancel verb falls back to the ASCII 'X'.");
            FlowTrace.Step("BuildHud",
                "D17 rail icons: confirm=ASCII 'OK' (no check-mark sprite in pack), " +
                "rotate=ASCII 'Rot' (no circular-arrow sprite in pack), cancel=" +
                (cancelIcon != null ? "SPRITE element/cross" : "ASCII 'X'"));

            // Stacked top -> bottom: confirm, rotate, cancel. Cancel sits FURTHEST from
            // confirm so the destructive verb is not the one a slipped thumb finds.
            float step = ChipHitPx + RailGutterPx;
            var railRt = railFill.transform as RectTransform;
            _okChip = MakeVerb(railRt, "OkChip", "OK", null, ElarionUi.Gilt,
                new Vector2(0f, step), ChipVisualPx, 20f,
                () => _onPlace?.Invoke(), out _okChipLabel, out _okChipRing);
            MakeVerb(railRt, "RotChip", "Rot", null, ElarionUi.Parchment,
                Vector2.zero, ChipVisualPx, 20f,
                () => _onRotateRight?.Invoke(), out _, out _);
            MakeVerb(railRt, "CancelChip", "X", cancelIcon, new Color(0.86f, 0.32f, 0.30f),
                new Vector2(0f, -step), ChipVisualPx, 20f,
                () => _onCancel?.Invoke(), out _, out _);

            // Kept as the canonical cancel name so any probe/close convention still resolves
            // it after the word-button retirement.
            var cancelChip = railRt != null ? railRt.Find("CancelChip") : null;
            if (cancelChip != null) cancelChip.gameObject.name = "BuildHudPlaceCancel";

            _intentBar.SetActive(false);   // Placing state shows it
            FlowTrace.Step("BuildHud",
                "WO-1010 D14: the ghost-following chip cluster is RETIRED -> a FIXED lean right-edge " +
                "rail [OK][Rot][X] (" + RailBandW + "x" + RailBandH + "px, " + RailEdgeInsetPx +
                "px inset); the ghost now carries ONLY its name/cost pill, so no chrome sits on the piece");
        }

        /// <summary>
        /// One rail verb (or the corner Done): a MinTouch-sized INVISIBLE hit box with a small
        /// visible plate inside it. The transparent parent Image is the raycast target, so the
        /// tappable area is 112px while the art stays ~52px — the WO's invisible-padding rule.
        /// Growing the visual instead would put slabs down the right edge and undo the point of
        /// the redesign.
        ///
        /// D17: pass a SPRITE in <paramref name="icon"/> and the plate renders the glyph art;
        /// pass null and it renders <paramref name="label"/> as ASCII text. That null path is
        /// not a stub — it is RpgUiCatalog's sprite-or-null contract, and it is what keeps a
        /// missing pack from turning a verb into a tofu box.
        /// </summary>
        private static Button MakeVerb(RectTransform parent, string name, string label,
            Sprite icon, Color accent, Vector2 offset, float visualPx, float fontPx, Action onClick,
            out TextMeshProUGUI labelOut, out Image ringOut)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ChipHitPx, ChipHitPx);
            rt.anchoredPosition = offset;

            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // invisible padding, still raycastable
            hit.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = hit;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // ── The visible plate: an ACCENT-COLOURED EDGE around a near-black fill. ──
            // The first build used a plain ObsidianFill circle, and the capture showed why
            // that fails: ObsidianFill is (0.02,0.02,0.025) — effectively black — so the chip
            // was black-on-black and only the bare label floated over the field. Even inside
            // the rail band the plate keeps its edge: the band itself is the same near-black,
            // so without it the verbs would be black-on-black again. The edge also gives each
            // verb a second, non-textual identity (gold confirm / parchment rotate / red
            // cancel) WITHOUT meaning ever resting on colour alone, because each verb also
            // carries a distinct WORD or a distinct SHAPE.
            var edge = ElarionUiKit.AddImage(go.transform, "ChipEdge",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), accent, rounded: true);
            var edgeRt = edge.transform as RectTransform;
            if (edgeRt != null)
            {
                edgeRt.anchorMin = edgeRt.anchorMax = new Vector2(0.5f, 0.5f);
                edgeRt.pivot = new Vector2(0.5f, 0.5f);
                edgeRt.sizeDelta = new Vector2(visualPx, visualPx);
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
                fillRt.sizeDelta = new Vector2(visualPx - ChipEdgePx * 2f, visualPx - ChipEdgePx * 2f);
            }
            ringOut = fill.GetComponent<Image>();
            if (ringOut != null) ringOut.raycastTarget = false;

            if (icon != null)
            {
                // SPRITE glyph (D17). preserveAspect so a non-square source is never squashed;
                // untinted so the pack art reads as authored — the accent EDGE carries the
                // redundant colour cue and the SHAPE carries the meaning.
                var iconGo = new GameObject("ChipIcon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(fill.transform, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(ChipIconPx, ChipIconPx);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                labelOut = null;
                return btn;
            }

            labelOut = MakeText(fill.transform, label, fontPx, accent, FontStyles.Bold,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            labelOut.raycastTarget = false;
            // A short verb must never ellipsis-cull inside its own plate.
            labelOut.textWrappingMode = TextWrappingModes.NoWrap;
            labelOut.enableAutoSizing = true;
            labelOut.fontSizeMin = fontPx * 0.6f;
            labelOut.fontSizeMax = fontPx;
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
            // ── NO TOGGLE. The pad follows the STATE. (owner ruling 2026-08-09) ────
            // The first pass gave the nudge pad a corner "+" button. That was the wrong trade:
            // it removed a permanent control by adding a permanent control, and it made
            // pixel-nudging a thing the player has to DISCOVER on a screen already accused of
            // having buttons everywhere. The pad is only ever meaningful while something is
            // being positioned, and that is a state the HUD already knows — so it appears on
            // Placing and leaves with it. The player does nothing.
            var padHost = new GameObject("BuildNudgePad", typeof(RectTransform));
            padHost.transform.SetParent(_canvas.transform, false);
            var prt = (RectTransform)padHost.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            // ── SAME MOVE WIDGET AS THE REGULAR HUD (owner, 2026-08-09). ──────────
            // The first pass built this with BuildVirtualDPad — which is the OLDER WO-611
            // widget. The live HUD builds an ANALOG STICK (HudKitController, FeatureFlags.
            // CombatHud611 defaults ON) and keeps the virtual d-pad only as a
            // construction-failure fallback. The result was two different move controls in one
            // game: a stick while walking, a boxy d-pad the moment you entered build mode.
            // Mirroring the HUD's exact construction — stick first, d-pad only if the stick
            // fails to build — so the movement grammar is the same everywhere and a future
            // change to the HUD's widget does not silently leave build mode behind.
            var stick = ElarionUiKit.BuildAnalogStick(padHost.transform, new Vector2(0.12f, 0.26f),
                v => NudgeVector = v);
            if (stick == null || stick.root == null)
            {
                FlowTrace.Warn("BuildHud",
                    "analog stick unavailable — falling back to the WO-611 virtual D-pad for the build nudge.");
                ElarionUiKit.BuildVirtualDPad(padHost.transform, new Vector2(0.12f, 0.26f),
                    v => NudgeVector = v);
            }
            _dpadHost = padHost;
            _dpadHost.SetActive(false);   // Browse has nothing to nudge
        }

        /// <summary>
        /// Show the virtual d-pad exactly while a piece is being positioned. Driven by
        /// <see cref="SetState"/>, never by the player.
        /// </summary>
        /// <summary>
        /// WO-1010 D12 — the BRAIN's per-frame verdict on whether the nudge stick may show:
        /// an item is selected AND the carousel is minimized. Both conditions are automatic;
        /// there is no toggle and the player does nothing. Placement ending (placed OR cancelled)
        /// flips this false, which is what "after placed removes" means in practice.
        /// </summary>
        public void SetNudgePadAllowed(bool allowed)
        {
            SetNudgePadVisible(allowed && _state == BuildHudState.Placing);
        }

        private void SetNudgePadVisible(bool show)
        {
            if (_dpadHost == null || _dpadHost.activeSelf == show) return;
            _dpadHost.SetActive(show);
            if (!show) NudgeVector = Vector2.zero;   // a hidden pad must never keep steering
            FlowTrace.Step("BuildHud", "nudge pad " + (show ? "SHOWN (placing)" : "HIDDEN (not placing)") +
                " — follows state, no player action");
        }

        // NOTE: AllowTwoLineLabel (the "Rotate Right" -> "Rotate Ri..." ellipsis fix) was
        // deleted with WO-1010 D14/D10 — every kit word-button it served is gone. The rail
        // verbs and the corner Done are hand-built plates whose short ASCII labels autosize
        // inside their own plate, so there is no kit FitSingleLine pass left to opt out of.

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

            // The nudge pad only makes sense while something is being positioned, and that is
            // a state the HUD already knows — so it comes and goes on its own. No toggle, no
            // discovery burden, and no permanent control left on screen in Browse.
            // State alone is no longer sufficient — D12 gates on carousel-minimized too, pushed
            // each frame by the brain via SetNudgePadAllowed. Leaving Placing always hides it.
            if (!placing) SetNudgePadVisible(false);

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
        /// Follow the ghost with the PILL (the rail is fixed and needs no pass). LateUpdate so
        /// the anchor pushed this frame is already current. Runs only while Placing, so Browse
        /// costs nothing.
        /// </summary>
        private void LateUpdate()
        {
            LayoutGhostControlsNow();
        }

        /// <summary>
        /// The follow/clamp pass, callable directly. LateUpdate drives it at runtime; the
        /// headless UI capture calls it explicitly because MonoBehaviour ticks do NOT run in
        /// edit mode — without this the capture would photograph the pill parked at the canvas
        /// centre and the screenshot would prove nothing about the clamp rule it is meant to
        /// verify. STILL DIRECTLY CALLABLE by contract, even though D14 shrank the work to the
        /// pill alone: the OK/No verdict below is state, not layout, and the capture needs it.
        /// </summary>
        public void LayoutGhostControlsNow()
        {
            if (_state != BuildHudState.Placing) return;

            // ── D14: THE RAIL IS NOT LAID OUT HERE, AND THAT IS THE POINT. ────────
            // The retired code flanked the ghost with the chip cluster and flipped it to the
            // other side near a screen edge. The owner's ruling ("i want a lean section on
            // right") removes the entire class of problem: fixed chrome cannot land on the
            // piece, cannot walk off-screen, and cannot need a clamp. Only the PILL follows.
            if (_canvasRect != null && _ghostPill != null && _hasGhostAnchor)
            {
                // Screen -> canvas-local. Overlay canvases take a NULL camera here; passing one
                // silently offsets everything.
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRect, _ghostScreenPoint, null, out local))
                {
                    Vector2 half = _canvasRect.rect.size * 0.5f;
                    Vector2 pillHalf = _ghostPill.sizeDelta * 0.5f;

                    // CLAMP AGAINST THE RESERVED BANDS, NOT JUST THE SCREEN. "Fully on-screen"
                    // was never the real requirement — the first edge capture showed two
                    // separately-correct clamps producing one unreadable result. The pill's
                    // limits therefore subtract the rail's column on the right and the resource
                    // strip's band at the bottom, so it can never slide UNDER either of them.
                    float leftLimit   = -half.x + pillHalf.x + SafePadPx;
                    float rightLimit  =  half.x - pillHalf.x - SafePadPx - RailReservedWidthPx;
                    float topLimit    =  half.y - pillHalf.y - SafePadPx;
                    float bottomLimit = -half.y + pillHalf.y + SafePadPx + ResourceStripReservedPx;
                    if (rightLimit < leftLimit) rightLimit = leftLimit;      // absurdly narrow canvas
                    if (topLimit < bottomLimit) topLimit = bottomLimit;

                    // Float ABOVE the ghost; drop below only when there is no room up there,
                    // so the pill labels the piece without covering it.
                    float pillY = local.y + GhostPillLiftPx;
                    if (pillY > topLimit)
                    {
                        float below = local.y - GhostPillLiftPx;
                        pillY = below >= bottomLimit ? below : Mathf.Clamp(pillY, bottomLimit, topLimit);
                    }

                    _ghostPill.anchoredPosition = new Vector2(
                        Mathf.Clamp(local.x, leftLimit, rightLimit),
                        Mathf.Clamp(pillY, bottomLimit, topLimit));
                }
            }

            // ── THE VERDICT: short word on the verb, full reason on the PILL. ─────
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

        // NOTE: the local PinSize helper (the "long thin rectangles in horizontal mode" fix
        // that collapsed a kit button's fraction anchors onto a fixed pixel box) went with the
        // last kit button in this file — WO-1010 D10 replaced the pinned 200x132 "X Done" with
        // the hand-built corner plate above. The pattern itself is unchanged canon and still
        // lives at BuildPaletteUI.PinSize / BuildTabRow; every rect in this file is now
        // authored at fixed pixels directly, which is the same rule one step earlier.

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
