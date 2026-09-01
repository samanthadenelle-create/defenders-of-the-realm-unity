// =============================================================================
// EchoRosterView -- the "pet box": an informative Echo roster grid. DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Opened by the HUD "Pets" button (EchoUnlockFeedback.BuildPetBoxButton) via the
// static EchoRoster.Open(). A code-built Obsidian modal (ElarionUiKit -- NO UXML,
// PIPELINE_STATE S8) showing all 6 canonical spirits as portrait cards.
//
// MVVM (Silo F): the View reads NO service. Every card's identity / owned-locked
// state / portrait / per-echo lane-level-bonus readout, the header ETA + progress,
// the shared-perk line, and the first-run / empty framing all come from
// EchoRosterVM. OWNED cards tap through to the per-echo lane picker via the VM's
// OpenCard command (WO-738 reachability). Rebuilt fresh each open (VM re-created)
// so owned/locked + the ETA are always current. Colorblind-safe (portrait + TEXT
// status). Guard-wrapped card build (one bad card logs + skips). ASCII-only.
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Static opener for the Echo roster ("pet box"). Lazily creates the
    /// singleton view host; safe to call from any Village code (or the HUD Pets button).</summary>
    public static class EchoRoster
    {
        private static EchoRosterView s_view;

        /// <summary>Open (or refresh) the Echo roster grid.</summary>
        public static void Open()
        {
            if (s_view == null)
            {
                var go = new GameObject("EchoRoster");
                Object.DontDestroyOnLoad(go);
                s_view = go.AddComponent<EchoRosterView>();
            }
            s_view.OpenPanel();
        }
    }

    /// <summary>The Echo roster grid view. Rebuilt each open so owned/locked state +
    /// the next-echo ETA are always current. Binds <see cref="EchoRosterVM"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class EchoRosterView : MonoBehaviour
    {
        // -- FIXED REF-PIXEL LAYOUT CONSTANTS (the WO-841 / WO-852 pattern) ----
        // CANON_GROUND_TRUTH 2026-08-02 Sec.4: a text band is sized in FIXED reference
        // pixels >= the font's line box, NEVER as a fraction of a parent. A fraction
        // band shrinks with the pane, under-heights the TMP line box, and TMP then
        // culls or ellipsizes the glyphs with no error -- which is what this screen
        // shipped (the ETA band resolved to ~30 px for a FontBody line box of 64.5,
        // and the first-run card row to ~102 px against a 112 px touch floor).
        // Every value derives from a KIT constant so a kit change moves the roster
        // with it. `public const` so an oracle can pin them without reflection tricks.

        /// <summary>One TMP line box at the kit's auto-size floor (ElarionUiKit.FontFloor).</summary>
        public const float FloorLinePx = ElarionUiKit.FontFloor * 1.25f + 2f;   // 39.5
        /// <summary>The header ETA line -- one FontBody line box.</summary>
        public const float EtaBandPx = ElarionUi.FontBody * 1.25f + 2f;         // 64.5
        /// <summary>The section-head line in the empty hint -- one FontHead line box.</summary>
        public const float HeadBandPx = ElarionUi.FontHead * 1.25f + 2f;        // 82
        /// <summary>The next-Echo progress bar. Not text, so it has no line box; this is the
        /// height the old 0.045 fraction band resolved to at the capture aspects.</summary>
        public const float BarBandPx = 18f;
        /// <summary>The shared-perk readout -- one floor line box (it seats in the frame's
        /// ~44 px sub-header meta band, which cannot hold a FontLabel line box of 52).</summary>
        public const float PerkBandPx = FloorLinePx;                            // 39.5
        /// <summary>First-run banner: two floor line boxes (title + copy) plus its own pad.</summary>
        public const float BannerPx = 4f + FloorLinePx + 6f + FloorLinePx + 4f; // 93
        /// <summary>Empty hint: head + a three-line copy block + the cadence footnote.</summary>
        public const float EmptyHintPx = 6f + HeadBandPx + 8f + 3f * FloorLinePx
                                       + 8f + FloorLinePx + 6f;                 // 268
        /// <summary>A roster card's name / status lines -- one floor line box each.</summary>
        public const float CardTextPx = FloorLinePx;                            // 39.5
        /// <summary>Pixels of a card reserved for its two text lines plus their pads.</summary>
        public const float CardTextStackPx = 4f + CardTextPx + 4f + CardTextPx + 6f;  // 93
        /// <summary>Every card is a tap target, so a row is never shorter than the kit floor.</summary>
        public const float CardRowMinPx = 170f;
        /// <summary>Inset that keeps the two text bands inside the visible card artwork.</summary>
        public const float CardVisualInsetPx = 26f;
        /// <summary>Gap between stacked fixed bands.</summary>
        public const float BandGapPx = 8f;
        /// <summary>Gap between the two card rows.</summary>
        public const float RowGapPx = 8f;
        /// <summary>Inset from the body well's top / bottom edge.</summary>
        public const float BodyPadPx = 6f;

        // Panel rect. WO-852 raised the sibling Echo CARD to 0.05-0.95 because the body
        // well is the binding constraint once bands are honest pixels; the roster has the
        // same problem (six touch-floor cards in two rows) and takes the same rect, which
        // also makes the two Echo screens open at one size.
        private const float PanelYMin = 0.05f;
        private const float PanelYMax = 0.95f;

        private GameObject _modal;
        private bool _open;
        private EchoRosterVM _vm;
        private PanelHandle _panelHandle;   // HUD-1: modal arbiter registration (one Echo modal at a time)

        private static readonly Color OwnedGlass  = new Color(0.09f, 0.10f, 0.13f, 0.95f);
        private static readonly Color LockedGlass = new Color(0.05f, 0.05f, 0.06f, 0.95f);
        private static readonly Color LifeGreen   = new Color(0.40f, 0.78f, 0.45f, 1f);

        /// <summary>Open + (re)build the grid to the current workforce state.</summary>
        public void OpenPanel()
        {
            using var _t = FlowTrace.Enter("Echo", "RosterOpen");
            EnsureEventSystem();

            // Rebuild fresh each open (cheap; keeps owned/locked + ETA current).
            if (_modal != null) { Destroy(_modal); _modal = null; }
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _vm = EchoRosterVM.CreateDefault(Close);

            bool ok = Guard.Try("Echo", "build echo roster", Build);
            if (!ok || _modal == null)
            {
                FlowTrace.Fail("Echo", "RosterOpen: roster failed to build -- not shown.");
                return;
            }
            _open = true;
            _modal.SetActive(true);

            // HUD-1: register with the single-modal arbiter and announce the open. Opening the
            // roster CLOSES any other Echo modal (card/picker, harvest, unlock dialogue) that was
            // up -- no more stacked modals. Battle-lock (WO-437): a rejected open self-closes.
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("EchoRoster", Close, () => _open);
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                FlowTrace.Warn("Echo", "RosterOpen rejected by PanelManager (battle-lock) -- not shown.");
                return;
            }
            FlowTrace.Step("Echo", $"Echo roster OPEN (owned {_vm.Owned}/{_vm.MaxEchoes}).");
        }

        private void Close()
        {
            _open = false;
            if (_modal != null) _modal.SetActive(false);
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            FlowTrace.Step("Echo", "Echo roster CLOSED.");
        }

        // -- fixed-pixel band pins (the WO-841 / WO-852 pattern, EchoCardView) --
        // Re-hang a control on its parent's TOP or BOTTOM edge with a FIXED reference-pixel
        // band. X anchors / offsets are preserved; only the vertical seat changes, so a band
        // never scales with the pane and never under-heights its line box again.

        private static void PinBandFromTop(RectTransform rt, float topPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, -(topPx + heightPx));
            rt.offsetMax = new Vector2(rt.offsetMax.x, -topPx);
        }

        private static void PinBandFromBottom(RectTransform rt, float bottomPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 0f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottomPx);
            rt.offsetMax = new Vector2(rt.offsetMax.x, bottomPx + heightPx);
        }

        /// <summary>Resolved height of a frame drop-zone in REFERENCE pixels. The zone is anchored
        /// as a fraction of the panel, and the panel as a fraction of the post-scale canvas, so the
        /// product is the height the zone will really have -- readable on the build frame, unlike
        /// rect.height (which returns raw screen pixels before the CanvasScaler applies).</summary>
        private static float ZoneHeightPx(RectTransform zone, float panelPx)
        {
            if (zone == null) return 0f;
            return (zone.anchorMax.y - zone.anchorMin.y) * panelPx;
        }

        // -- build --------------------------------------------------------------
        private void Build()
        {
            // Owner F8 2026-07-24: EVERY overlap on the pet screen came from parenting into
            // chrome.content (full panel 0..1) so labels painted ON the frame title + Close.
            // Kit law: drop chrome-less content into layout.body (header/title/close reserved).
            var built = ElarionUiKit.BuildObsidianModal(
                "EchoRoster", "ECHOES OF ELARION",
                new Vector2(0.10f, PanelYMin), new Vector2(0.90f, PanelYMax),
                onClose: Close, sortingOrder: 31000,
                frameName: RpgUiCatalog.FrameCore);
            _modal = built.canvas;

            MedievalUiSkin.ApplyShell(built.chrome);
            var closeImage = built.chrome != null && built.chrome.close != null
                ? built.chrome.close.targetGraphic as Image : null;
            if (closeImage != null) closeImage.type = Image.Type.Simple;

            // Chrome title stays the product name ONLY (never "Echoes 1/6 - ..." which collided).
            if (built.chrome.title != null)
            {
                built.chrome.title.text = "ECHOES OF ELARION";
                ElarionUiKit.FitSingleLine(built.chrome.title);
            }

            var layout = built.chrome.layout;

            // BODY well is the only safe parent (above Close band, below header plate).
            Transform body = layout != null && layout.body != null
                ? (Transform)layout.body
                : built.chrome.content.transform;

            // Reference-pixel budget for this build. Everything below is seated against it,
            // so the same stack resolves at 1920x1080, 2340x1080 and any phone aspect.
            float canvasH = ElarionUiKit.PostScaleCanvasHeight(built.canvas.transform);
            float panelPx = (PanelYMax - PanelYMin) * canvasH;
            float bodyPx = layout != null && layout.body != null
                ? ZoneHeightPx(layout.body, panelPx)
                : panelPx;
            float subHeaderPx = layout != null ? ZoneHeightPx(layout.subHeader, panelPx) : 0f;
            float footerPx = layout != null ? ZoneHeightPx(layout.footer, panelPx) : 0f;

            int owned = _vm.Owned;
            int perEcho = _vm.PerEcho;
            bool firstRun = _vm.FirstRun;
            int wavesToNext = _vm.WavesToNext;

            // -- Strict top-down FIXED-PIXEL stack inside BODY --
            //   ETA (Echoes N/M - next spirit)  EtaBandPx     single line
            //   next-Echo progress bar          BarBandPx
            //   [first-run banner]              BannerPx      only when it cannot go in the footer
            //   3x2 card grid                   the rest, rows >= CardRowMinPx
            // The shared-perk line moves to the frame's SUB-HEADER meta band and the first-run
            // banner to the frame's FOOTER strip whenever those zones can seat them. That is not
            // decoration: at 2340x1080 the body well is ~418 px, and ETA + bar + perk + a
            // two-line banner + two touch-floor card rows need ~530 px. WO-852 solved the same
            // squeeze on the Echo card by moving its name to the header plate and its status to
            // the footer; this is the same move on the same frame.
            float cursor = BodyPadPx;

            var eta = ElarionUiKit.Label(body, _vm.RosterEtaText, 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontBody, TextAlignmentOptions.Center, 0.03f, 0.97f, bold: true);
            ElarionUiKit.FitSingleLine(eta);
            PinBandFromTop(eta.rectTransform, cursor, EtaBandPx);
            cursor += EtaBandPx + BandGapPx;

            var bar = ElarionUiKit.Bar(body, ElarionUiKit.BarKind.Castle,
                new Vector2(0.08f, 0f), new Vector2(0.92f, 1f), withValue: false);
            if (bar.fill != null) { bar.fill.color = LifeGreen; bar.fill.fillAmount = _vm.NextEchoProgress; }
            PinBandFromTop(bar.track, cursor, BarBandPx);
            cursor += BarBandPx + BandGapPx;

            if (_vm.HarvestPerkLine != null)
            {
                bool perkInSubHeader = layout != null && layout.subHeader != null && subHeaderPx >= PerkBandPx;
                Transform perkHost = perkInSubHeader ? (Transform)layout.subHeader : body;
                var perk = ElarionUiKit.Label(perkHost, _vm.HarvestPerkLine, 0f, 1f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontFloorMobile,
                    TextAlignmentOptions.Center, 0.03f, 0.97f, bold: false);
                ElarionUiKit.FitSingleLine(perk);
                if (perkInSubHeader)
                {
                    PinBandFromBottom(perk.rectTransform, 2f, PerkBandPx);
                }
                else
                {
                    PinBandFromTop(perk.rectTransform, cursor, PerkBandPx);
                    cursor += PerkBandPx + BandGapPx;
                }
            }

            if (_vm.Empty)
            {
                FlowTrace.Step("Echo", "Roster EMPTY (owned 0) -- showing centered awaken hint (no bare locked grid).");
                BuildEmptyHint(body, cursor, wavesToNext, perEcho);
                return;
            }

            if (firstRun)
            {
                bool bannerInFooter = layout != null && layout.footer != null && footerPx >= BannerPx;
                FlowTrace.Step("Echo", $"Roster FIRST-RUN (owned {owned}) -- awaken banner in "
                    + (bannerInFooter ? "the frame footer strip" : "the body well") + ".");
                BuildFirstRunHint(bannerInFooter ? (Transform)layout.footer : body,
                    bannerInFooter, cursor, _vm.StarterName, wavesToNext);
                if (!bannerInFooter) cursor += BannerPx + BandGapPx;
            }

            // 3x2 grid -- the rest of the body well, split into two FIXED-PIXEL rows. Every card
            // is a tap target, so a row is clamped up to the kit touch floor rather than being
            // allowed to divide down into an untappable sliver (the WO-852 failure, which the
            // fraction grid re-created here: at 2340x1080 the first-run cell was ~102 px).
            float gridTopPx = cursor;
            float gridPx = Mathf.Max(0f, bodyPx - gridTopPx - BodyPadPx);
            float rowPx = (gridPx - RowGapPx) * 0.5f;
            if (rowPx < CardRowMinPx)
            {
                FlowTrace.Warn("Echo", $"Roster grid well is {gridPx:F0} ref px (body {bodyPx:F0}, "
                    + $"stack above {gridTopPx:F0}) -- a shared row of {rowPx:F0} px is below the kit "
                    + $"touch floor {CardRowMinPx:F0}; clamping the rows UP, the last row may run "
                    + "into the body pad. Shed a band from the body instead of shrinking the cards.");
                rowPx = CardRowMinPx;
            }
            FlowTrace.Step("Echo", $"Roster grid: body {bodyPx:F0}px, stack {gridTopPx:F0}px, "
                + $"well {gridPx:F0}px, row {rowPx:F0}px (floor {CardRowMinPx:F0}).");

            const int cols = 3;
            float padX = 0.02f, gapX = 0.02f;
            float cellW = (1f - 2f * padX - (cols - 1) * gapX) / cols;

            Guard.TryEach("Echo", "build roster card", _vm.Cards, card =>
            {
                int index = card.Order - 1;
                int col = index % cols;
                int row = index / cols;
                float x0 = padX + col * (cellW + gapX);
                float x1 = x0 + cellW;
                float topPx = gridTopPx + row * (rowPx + RowGapPx);
                BuildCard(body, card, x0, x1, topPx, rowPx);
            });
        }

        // -- friendly empty / first-run hints -----------------------------------

        /// <summary>First-run banner: TWO non-overlapping FIXED-PIXEL lines (owner F8: gold title
        /// was painted through the parchment body). Seats in the frame's footer strip when that
        /// band can hold it, else as a fixed band at <paramref name="topPx"/> in the body well.
        /// Both lines are authored at the kit's mobile floor because the whole banner is 93 ref px
        /// -- a FontBody line box (64.5) plus a FontLabel one (52) does not fit any host it has.</summary>
        private void BuildFirstRunHint(Transform host, bool inFooter, float topPx,
                                       string starterName, int wavesToNext)
        {
            var panel = ElarionUiKit.Panel(host,
                new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), deep: false, innerRim: true);
            var prt = (RectTransform)panel.transform;
            if (inFooter) PinBandFromBottom(prt, 4f, BannerPx);
            else PinBandFromTop(prt, topPx, BannerPx);
            var t = panel.transform;

            // Title line, forced single line.
            var title = ElarionUiKit.Label(t, starterName + " has answered your call.", 0f, 1f,
                ElarionUi.Gilt, (int)ElarionUi.FontFloorMobile,
                TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            ElarionUiKit.FitSingleLine(title);
            PinBandFromTop(title.rectTransform, 4f, FloorLinePx);

            // Copy line -- its own band, so it can never cross the title.
            string bodyTxt = "It gathers for you now. Clear " + wavesToNext
                           + " more wave" + (wavesToNext == 1 ? "" : "s")
                           + " for your next Echo.";
            var b = ElarionUiKit.Label(t, bodyTxt, 0f, 1f,
                ElarionUi.Parchment, (int)ElarionUi.FontFloorMobile,
                TextAlignmentOptions.Center, 0.04f, 0.96f, bold: false);
            ElarionUiKit.FitSingleLine(b);
            PinBandFromTop(b.rectTransform, 4f + FloorLinePx + 6f, FloorLinePx);
        }

        /// <summary>True-empty hero hint (owned == 0): one centered card seated as a FIXED-PIXEL
        /// band at <paramref name="topPx"/> in the body well.</summary>
        private void BuildEmptyHint(Transform body, float topPx, int wavesToNext, int perEcho)
        {
            var panel = ElarionUiKit.Panel(body,
                new Vector2(0.10f, 0f), new Vector2(0.90f, 1f), deep: true, innerRim: true);
            PinBandFromTop((RectTransform)panel.transform, topPx, EmptyHintPx);
            var t = panel.transform;

            float cursor = 6f;

            var head = ElarionUiKit.Label(t, "The Tree sleeps.", 0f, 1f,
                ElarionUi.Gilt, ElarionUi.FontHead,
                TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            ElarionUiKit.FitSingleLine(head);
            PinBandFromTop(head.rectTransform, cursor, HeadBandPx);
            cursor += HeadBandPx + BandGapPx;

            // Wrapped copy in a THREE floor-line box: the sentence needs two lines at the widest
            // capture aspect and three at the narrowest, so the band is sized for the worst case
            // and never truncates. Authored at the mobile floor -- at FontBody it would need five
            // lines and the band would have to eat the card grid's pixels.
            string bodyTxt = "Defend Elarion's waves and the Heart will awaken a spirit. "
                           + "Clear " + wavesToNext + " wave" + (wavesToNext == 1 ? "" : "s")
                           + " to call your first Echo.";
            var b = ElarionUiKit.Label(t, bodyTxt, 0f, 1f,
                ElarionUi.Parchment, (int)ElarionUi.FontFloorMobile,
                TextAlignmentOptions.Center, 0.06f, 0.94f, bold: false);
            b.textWrappingMode = TextWrappingModes.Normal;
            PinBandFromTop(b.rectTransform, cursor, 3f * FloorLinePx);
            cursor += 3f * FloorLinePx + BandGapPx;

            var faint = ElarionUiKit.Label(t,
                "Six spirits wait -- one awakens every " + perEcho + " waves.", 0f, 1f,
                ElarionUi.ParchmentDim, (int)ElarionUi.FontFloorMobile,
                TextAlignmentOptions.Center, 0.05f, 0.94f, bold: false);
            ElarionUiKit.FitSingleLine(faint);
            PinBandFromTop(faint.rectTransform, cursor, FloorLinePx);

            FlowTrace.Step("Echo", $"Empty-hint built (call first Echo in {wavesToNext} waves; cadence {perEcho}).");
        }

        /// <summary>One roster card. X is a fraction of the body well (the columns are ~460 ref px
        /// wide, far above the touch floor); Y is a FIXED-PIXEL band so the row height and the two
        /// text lines inside it are never divided down by the pane.</summary>
        private void BuildCard(Transform body, EchoRosterCardVM card,
                               float x0, float x1, float topPx, float rowPx)
        {
            bool owned = card.Owned;

            var cardGo = new GameObject($"EchoCard_{card.Order}", typeof(Image));
            cardGo.transform.SetParent(body, false);
            var crt = cardGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(x0, 1f); crt.anchorMax = new Vector2(x1, 1f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            PinBandFromTop(crt, topPx, rowPx);
            var cbg = cardGo.GetComponent<Image>();
            var cardFrame = Resources.Load<Sprite>("UI/ElarionMedieval/frames/content-panel");
            if (cardFrame != null)
            {
                cbg.sprite = cardFrame;
                cbg.type = Image.Type.Simple;
            }
            cbg.color = owned ? Color.white : new Color(.38f, .38f, .40f, .88f);
            if (owned)
            {
                cbg.raycastTarget = true;
                var tapBtn = cardGo.AddComponent<UnityEngine.UI.Button>();
                tapBtn.targetGraphic = cbg;
                ElarionUiKit.StyleButtonColors(tapBtn);
                int tapIndex = card.Index;
                tapBtn.onClick.AddListener(() =>
                {
                    FlowTrace.Step("Echo", $"Roster card tapped -> open picker for echo {tapIndex}.");
                    if (_vm != null) _vm.OpenCard(tapIndex);
                });
            }
            else
            {
                cbg.raycastTarget = false;
            }
            var cardT = cardGo.transform;

            // Card-internal FIXED-PIXEL bands, stacked up from the card's own bottom edge so the
            // two text lines keep their whole line box whatever the row resolves to:
            //   status    bottom 4    .. 43.5   single line (lane only -- not long Element lore)
            //   name      bottom 47.5 .. 87     single line
            //   portrait  everything above the text stack (image, so it may take what is left)
            // Authored at the kit's mobile floor: the bands are one FLOOR line box, which a
            // FontLabel(40) line box of 52 could not seat -- the old 0.04-0.20 / 0.22-0.40
            // fractions resolved to ~18 / ~20 px and only escaped blanking because the kit
            // fit-guard relaxed them toward FontHardFloor, i.e. they shipped sub-legible.
            var sprite = card.Portrait;
            if (sprite != null)
            {
                var pg = new GameObject("PortraitMedallion", typeof(Image));
                pg.transform.SetParent(cardT, false);
                var prt = pg.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.30f, 0f);
                prt.anchorMax = new Vector2(0.70f, 1f);
                prt.offsetMin = new Vector2(0f, CardTextStackPx + CardVisualInsetPx);
                prt.offsetMax = new Vector2(0f, -4f);
                var pimg = pg.GetComponent<Image>();
                pimg.sprite = Resources.Load<Sprite>(
                    "UI/ElarionMedieval/frames/circular-bezel-four-point");
                pimg.preserveAspect = true;
                pimg.raycastTarget = false;
                pimg.color = owned ? Color.white : new Color(0.12f, 0.12f, 0.14f, 0.95f);
                var portrait = ElarionUiKit.AddImage(pg.transform, "Portrait",
                    new Vector2(0.23f, 0.23f), new Vector2(0.77f, 0.77f), Color.white, rounded: false);
                var portraitImage = portrait.GetComponent<Image>();
                portraitImage.sprite = sprite;
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;
                portraitImage.color = owned ? Color.white : new Color(0.12f, 0.12f, 0.14f, 0.95f);
            }
            else
            {
                // Glyph stand-in for a missing portrait: its own floor line box directly above
                // the text stack, never a share of whatever the portrait region resolved to.
                var fb = ElarionUiKit.Label(cardT, card.PortraitFallback, 0f, 1f,
                    ElarionUi.ParchmentDim, (int)ElarionUi.FontFloorMobile,
                    TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                ElarionUiKit.FitSingleLine(fb);
                PinBandFromBottom(fb.rectTransform, CardTextStackPx + CardVisualInsetPx, FloorLinePx);
            }

            var nameLabel = ElarionUiKit.Label(cardT, card.DisplayName, 0f, 1f,
                owned ? ElarionUi.Gilt : ElarionUi.ParchmentDim, (int)ElarionUi.FontFloorMobile,
                TextAlignmentOptions.Center, 0.03f, 0.97f, bold: true);
            ElarionUiKit.FitSingleLine(nameLabel);
            PinBandFromBottom(nameLabel.rectTransform,
                CardVisualInsetPx + 4f + CardTextPx + 4f, CardTextPx);

            var statusLabel = ElarionUiKit.Label(cardT, card.StatusText, 0f, 1f,
                owned ? LifeGreen : ElarionUi.ParchmentDim, (int)ElarionUi.FontFloorMobile,
                TextAlignmentOptions.Center, 0.03f, 0.97f, bold: false);
            ElarionUiKit.FitSingleLine(statusLabel);
            PinBandFromBottom(statusLabel.rectTransform, CardVisualInsetPx, CardTextPx);
        }

        private void OnDestroy()
        {
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            if (_modal != null) Destroy(_modal);
        }

        // -- helpers ------------------------------------------------------------
        private static void EnsureEventSystem()
        {
            // EventSystem.current is a plain static (NOT a scene query) -- no banned FindAnyObjectByType.
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }
}
