// =============================================================================
// MonthlyLedgerPanel - the Monthly Ledger screen (WO section U2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet
//
// CODE-BUILT uGUI. Not UXML - UXML renders EMPTY in player builds.
//
// =============================================================================
//  IT IS A WEEK-TABBED CALENDAR, BECAUSE THIRTY CARDS CANNOT READ ON SEEKER
// -----------------------------------------------------------------------------
// Five tabs expose every authored day before purchase, seven days at a time (the
// fifth tab is short). This preserves the full-disclosure promise while spending
// landscape width on legible reward copy instead of shrinking thirty cards.
//
// =============================================================================
//  ⛔ NO COUNTDOWN. ANYWHERE. THIS IS THE LOAD-BEARING RULE OF THE SCREEN.
// -----------------------------------------------------------------------------
// The card runs on the POOL model: durationDays counts CLAIMS, not calendar days,
// and a missed day is never lost. Nothing expires - so a ticking clock would be a
// lie that manufactures urgency over a deadline that does not exist, which is
// exactly the pressure section 3.2 promises not to apply.
//
// The header therefore says "12 claims left" and NEVER "expires in 4d 06h". There
// is no Time-formatting call in this file, no expiry field read, and no
// canon-strings key in the monthlyLedger* block that could express one. If someone
// asks for a timer here, the answer is that there is nothing to time.
//
// =============================================================================
//  THE OTHER THREE RULES
// -----------------------------------------------------------------------------
// * EVERY CELL STATE CARRIES A WORD (CLAIMED / TODAY / YOURS / UPCOMING). The
//   owner is red/green colourblind; strip every hue and the grid still reads.
//   YOURS is the state that PROVES the pool model - a day the player walked past
//   is still theirs, and the grid says so in a word.
// * ONE ANIMATED ELEMENT: the TODAY cell, and nothing else. Motion means
//   "claimable now" and means nothing else.
// * NEVER AUTO-POPS (section 8, discovery rule C5). The player opens it.
//
// ASCII-only strings, all from canon-strings.json via StoreStrings.
// =============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>The monthly ledger screen: header, five week tabs, side panel, claim footer.</summary>
    [DisallowMultipleComponent]
    public sealed class MonthlyLedgerPanel : MonoBehaviour
    {
        // Zones as fractions of the WO's 2670 x 1200 landscape canvas.
        // Header 120 top / Footer 150 bottom / Grid 1700 wide / Side panel 840 wide.
        private const float GridWidthFraction = 1700f / 2670f;
        private static readonly Vector2 HeaderMin = new Vector2(0.015f, 1f - 120f / 1200f);
        private static readonly Vector2 HeaderMax = new Vector2(0.985f, 0.99f);
        // Author these bands above the shared 112px touch floor after CanvasScaler + panel inset.
        // The former 120px tabs and 150px footer resolved to 90-101px on supported landscape
        // ratios, so the runtime rescue clamp spilled controls into neighbouring content.
        private static readonly Vector2 TabsMin   = new Vector2(0.015f, 1f - 315f / 1200f);
        private static readonly Vector2 TabsMax   = new Vector2(GridWidthFraction, 1f - 125f / 1200f);
        private static readonly Vector2 GridMin   = new Vector2(0.015f, 240f / 1200f);
        private static readonly Vector2 GridMax   = new Vector2(GridWidthFraction, 1f - 120f / 1200f);
        private static readonly Vector2 SideMin   = new Vector2(GridWidthFraction + 0.01f, 240f / 1200f);
        // Reserve the upper-right Close footprint instead of allowing Today's claim copy beneath it.
        private static readonly Vector2 SideMax   = new Vector2(0.985f, 1f - 225f / 1200f);
        private static readonly Vector2 FooterMin = new Vector2(0.015f, 0.02f);
        private static readonly Vector2 FooterMax = new Vector2(0.985f, 240f / 1200f);

        private const int DaysPerWeek = 7;
        private const int WeekCount = 5;

        /// <summary>Cell 158 x 158 from the wireframe, floored against the kit's touch minimum.</summary>
        private static readonly float CellSize = Mathf.Max(158f, ElarionUiKit.MinTouchPx);

        private GameObject _canvas;
        private Transform  _body, _tabsHost, _gridHost, _sideHost;
        private TextMeshProUGUI _titleLine, _claimsLine, _todayLine, _todayReward, _exclusiveLine;
        private Button _claimCta;
        private TextMeshProUGUI _claimCtaLabel;
        private int _selectedWeek = -1;

        /// <summary>Which card the screen is showing. Defaults to the first authored card.</summary>
        private string _sku;

        /// <summary>Points the screen at a specific card SKU. Call before enabling.</summary>
        public void Show(string sku) { _sku = sku; }

        // ── The modal-arbiter handle (DEF-212) ────────────────────────────────
        // The one discipline carried over from the retired BattleMonthlyPanels wrapper. Without a
        // handle the arbiter never learns this screen is up: PanelRouter's post-open verify would
        // Fail-log a correctly rendered ledger as the WO-465 invisible-scrim class, and a second
        // modal could sit on top of it. The name is a DIAGNOSTIC label (FlowTrace / DeathTrace) and
        // is never drawn - every player-facing sentence here comes from canon-strings.
        private PanelHandle _panelHandle;

        private void Awake()
        {
            _panelHandle = PanelManager.Register(
                "Monthly Ledger",
                () => { if (this != null) gameObject.SetActive(false); },
                () => this != null && _canvas != null && _canvas.activeInHierarchy);
        }

        private void OnEnable()
        {
            EnsureBuilt();
            MonthlyCardService.Changed += Render;
            if (_canvas != null) _canvas.SetActive(true);
            Render();

            // ⛔ CLOSE ON FALSE. NotifyOpened REFUSES the open during a battle (WO-437). A refusal
            // that left this canvas up would be an un-arbitrated modal over live combat.
            if (_panelHandle != null && !PanelManager.NotifyOpened(_panelHandle))
            {
                FlowTrace.Warn("MonthlyCard", "MonthlyLedgerPanel: arbiter refused the open (battle-lock) - closing.");
                Close();
            }
        }

        private void OnDisable()
        {
            MonthlyCardService.Changed -= Render;
            if (_canvas != null) _canvas.SetActive(false);
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas);
        }

        private void Close() => gameObject.SetActive(false);

        private MonthlyCard ActiveCard
        {
            get
            {
                var card = BattleMonthlyCatalog.FindCard(_sku);
                if (card != null) return card;
                var all = BattleMonthlyCatalog.Cards;
                return all != null && all.Count > 0 ? all[0] : null;
            }
        }

        // =====================================================================
        //  Build
        // =====================================================================

        private void EnsureBuilt()
        {
            if (_canvas != null) return;
            using var _ = FlowTrace.Enter("MonthlyCard", "MonthlyLedgerPanel.EnsureBuilt");

            // Modal band: the HUD rail owns sorting order 4000. The old 621 canvas let
            // its Echoes chip draw THROUGH this screen even though the modal arbiter
            // correctly considered the ledger open.
            _canvas = ElarionUiKit.BuildModalCanvas("MonthlyLedgerUI", 31000);
            if (_canvas == null)
            {
                FlowTrace.Fail("MonthlyCard", "MonthlyLedgerPanel: modal canvas failed to build - screen not shown.");
                return;
            }

            ElarionUiKit.Scrim(_canvas.transform, Close);
            // Build on the shared shell itself. Re-skinning the old rounded Panel left its
            // mustard procedural backing visible through the authored frame margins.
            var chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform, string.Empty,
                new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f), onClose: null,
                withBackdrop: false, frameName: RpgUiCatalog.FrameCore);
            MedievalUiSkin.ApplyShell(chrome);
            var body = new GameObject("LedgerContent", typeof(RectTransform), typeof(Image));
            body.transform.SetParent(chrome.content.transform, false);
            var bodyRt = (RectTransform)body.transform;
            bodyRt.anchorMin = new Vector2(0.045f, 0.075f);
            bodyRt.anchorMax = new Vector2(0.955f, 0.925f);
            bodyRt.offsetMin = Vector2.zero; bodyRt.offsetMax = Vector2.zero;
            var bodyImage = body.GetComponent<Image>();
            bodyImage.color = new Color(0.018f, 0.015f, 0.020f, 0.94f);
            bodyImage.raycastTarget = false;
            _body = body.transform;

            BuildHeader();
            _tabsHost = Zone(_body, "WeekTabs", TabsMin, TabsMax);
            _gridHost = Zone(_body, "WeekGrid", GridMin, new Vector2(GridMax.x, TabsMin.y - 0.008f));
            _sideHost = Zone(_body, "Side", SideMin, SideMax);
            Plate(_sideHost, new Color(0.025f, 0.022f, 0.032f, 0.98f));
            BuildSidePanel();
            BuildFooter();

            _canvas.SetActive(false);
            FlowTrace.Step("MonthlyCard", "MonthlyLedgerPanel built: header, 10x3 grid, side panel, claim footer.");
        }

        private void BuildHeader()
        {
            var host = Zone(_body, "Header", HeaderMin, HeaderMax);

            _titleLine = Text(host, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerTitle), 42,
                              ElarionUi.Gold, FontStyles.Bold, TextAlignmentOptions.Left,
                              new Vector2(0f, 0f), new Vector2(0.55f, 1f));
            ElarionUiKit.FitSingleLine(_titleLine, 28f, 42f);

            // ⛔ A COUNT OF CLAIMS, NEVER A DATE OR A CLOCK. See the file header.
            _claimsLine = Text(host, string.Empty, 30, NightMarketPalette.Patronage, FontStyles.Bold,
                               TextAlignmentOptions.Right, new Vector2(0.55f, 0f), new Vector2(0.76f, 1f));

            // The shared Close is a fixed 360x132 control. Parenting it to this
            // shallow header band made it overhang the top edge. Seat it against the
            // full panel body so the complete control remains inside the frame.
            var close = ElarionUiKit.ObsidianCloseButton(_body, Close,
                new Vector4(0.79f, 0.84f, 0.97f, 0.96f));
            MedievalUiSkin.ApplyClose(close);
        }

        private void BuildSidePanel()
        {
            _todayLine = Text(_sideHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerTodayReward), 28,
                              ElarionUi.Parchment, FontStyles.Bold, TextAlignmentOptions.Left,
                              new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f));

            _todayReward = Text(_sideHost, string.Empty, 24, NightMarketPalette.Free, FontStyles.Normal,
                                TextAlignmentOptions.Left,
                                new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.85f));

            // The month-exclusive keepsake slot. Unauthored today, and the copy SAYS SO rather than
            // drawing an empty frame - a blank box on a paid card reads as broken or as a tease.
            _exclusiveLine = Text(_sideHost, string.Empty, 20, ElarionUi.ParchmentDim, FontStyles.Italic,
                                  TextAlignmentOptions.Left,
                                  new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.66f));

            // The full-value promise. It is the reason the pool model exists and it is stated on the
            // screen, not just in the data file.
            Text(_sideHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerPoolPromise), 20,
                 ElarionUi.Parchment, FontStyles.Normal, TextAlignmentOptions.Left,
                 new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.44f));

            Text(_sideHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerNoTimer), 20,
                 NightMarketPalette.Free, FontStyles.Bold, TextAlignmentOptions.Left,
                 new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.25f));

            Text(_sideHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerBonusOnly), 18,
                 ElarionUi.ParchmentDim, FontStyles.Normal, TextAlignmentOptions.Left,
                 new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.15f));
        }

        private void BuildFooter()
        {
            var host = Zone(_body, "Footer", FooterMin, FooterMax);
            Plate(host, new Color(0f, 0f, 0f, 0.30f));

            Text(host, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerNoCard), 20, ElarionUi.ParchmentDim,
                 FontStyles.Normal, TextAlignmentOptions.Left,
                 new Vector2(0.02f, 0.10f), new Vector2(0.62f, 0.90f));

            _claimCta = ElarionUiKit.Button(host, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerClaimCta),
                                            ElarionUiKit.ButtonKind.Gold,
                                            new Vector2(0.66f, 0.08f), new Vector2(0.98f, 0.92f),
                                            OnClaimTapped);
            MedievalUiSkin.ApplyButton(_claimCta, primary: true);
            _claimCtaLabel = _claimCta != null ? _claimCta.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (_claimCtaLabel != null)
                ElarionUiKit.FitSingleLine(_claimCtaLabel, 18f, 38f);
        }

        // =====================================================================
        //  Render
        // =====================================================================

        /// <summary>Rebuilds the selected seven-day week and refreshes every live line.</summary>
        public void Render()
        {
            if (_gridHost == null) return;
            using var _ = FlowTrace.Enter("MonthlyCard", "MonthlyLedgerPanel.Render");

            // ⚠ DETACH BEFORE Destroy. Destroy is DEFERRED to end-of-frame, so a doomed child still
            // counts in childCount until then. Render can run twice in one frame (the claim CTA
            // renders, and the Changed event it raised renders again) — without the detach the
            // second pass would draw a second full grid over one that has not been reaped, and the
            // player would briefly see sixty days.
            ClearChildren(_gridHost);
            ClearChildren(_tabsHost);

            var card = ActiveCard;
            if (card == null)
            {
                Text(_gridHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerEmpty), 13, ElarionUi.ParchmentDim,
                     FontStyles.Italic, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
                if (_claimsLine != null) _claimsLine.text = string.Empty;
                FlowTrace.Warn("MonthlyCard", "Render: no card authored - empty state shown (NOT a blank panel).");
                return;
            }

            if (_titleLine != null)
                _titleLine.text = (StoreStrings.Get(StoreStrings.KeyMonthlyLedgerTitle) + " - " +
                                   (card.Name ?? "")).ToUpperInvariant();

            if (_claimsLine != null)
                _claimsLine.text = StoreStrings.Format(StoreStrings.KeyMonthlyLedgerClaimsLeft,
                                                       MonthlyCardService.ClaimsRemaining(card.Sku));

            int total = Mathf.Max(0, card.DurationDays);
            int nextDay = Mathf.Clamp(MonthlyCardService.NextDay(card.Sku), 1, Mathf.Max(1, total));
            if (_selectedWeek < 0 || _selectedWeek >= WeekCount)
                _selectedWeek = Mathf.Clamp((nextDay - 1) / DaysPerWeek, 0, WeekCount - 1);
            BuildWeekTabs(card, total);
            BuildGrid(card, total);
            RefreshSide(card);
            RefreshCta(card);
        }

        /// <summary>
        /// Draws every day in the selected week. The five persistent tabs expose the
        /// complete authored duration before purchase without a thirty-card wall.
        /// </summary>
        private void BuildWeekTabs(MonthlyCard card, int total)
        {
            if (_tabsHost == null) return;
            float tabW = 1f / WeekCount;
            int claimDay = Mathf.Clamp(MonthlyCardService.NextDay(card.Sku), 1, Mathf.Max(1, total));
            int claimWeek = Mathf.Clamp((claimDay - 1) / DaysPerWeek, 0, WeekCount - 1);

            for (int week = 0; week < WeekCount; week++)
            {
                int captured = week;
                int first = week * DaysPerWeek + 1;
                int last = Mathf.Min(total, first + DaysPerWeek - 1);
                string label = StoreStrings.Format(StoreStrings.KeyMonthlyLedgerWeekTab,
                    week + 1, first, last);
                if (week == claimWeek)
                    label = "WEEK " + (week + 1) + "\nREADY";
                else
                    label = "WEEK " + (week + 1) + "\nDAYS " + first + "-" + last;

                var button = ElarionUiKit.Button(_tabsHost, label, ElarionUiKit.ButtonKind.Quiet,
                    new Vector2(week * tabW + 0.004f, 0f),
                    new Vector2((week + 1) * tabW - 0.004f, 1f),
                    () => { _selectedWeek = captured; Render(); });
                if (button == null) continue;
                MedievalUiSkin.ApplyButton(button, primary: week == _selectedWeek);
                var tabLabel = button.GetComponentInChildren<TextMeshProUGUI>();
                if (tabLabel != null)
                {
                    // Five tabs must remain fully contained at the narrow 16:9 gate.
                    // Fixed two-line typography is more stable here than TMP auto-size,
                    // which previously selected 32px and pushed SELECTED below the frame.
                    tabLabel.enableAutoSizing = false;
                    tabLabel.fontSize = 20f;
                    tabLabel.textWrappingMode = TextWrappingModes.NoWrap;
                    tabLabel.overflowMode = TextOverflowModes.Truncate;
                    tabLabel.margin = new Vector4(10f, 5f, 10f, 7f);
                    tabLabel.lineSpacing = -8f;
                }
                var le = button.gameObject.AddComponent<LayoutElement>();
                le.minHeight = ElarionUiKit.MinTouchPx;
                le.minWidth = ElarionUiKit.MinTouchPx;

                // Selected identity survives greyscale: a physical underline plus
                // the word SELECTED, not a tint-only tab state.
                if (week == _selectedWeek)
                {
                    var text = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                        text.text = "WEEK " + (week + 1) + "\nSELECTED";
                    var underline = new GameObject("SelectedUnderline", typeof(RectTransform), typeof(Image));
                    underline.transform.SetParent(button.transform, false);
                    var urt = underline.GetComponent<RectTransform>();
                    urt.anchorMin = new Vector2(0.08f, 0f); urt.anchorMax = new Vector2(0.92f, 0.055f);
                    urt.offsetMin = Vector2.zero; urt.offsetMax = Vector2.zero;
                    underline.GetComponent<Image>().color = ElarionUi.Gold;
                }
            }
        }

        private void BuildGrid(MonthlyCard card, int total)
        {
            int firstDay = _selectedWeek * DaysPerWeek + 1;
            int lastDay = Mathf.Min(total, firstDay + DaysPerWeek - 1);
            int shown = Mathf.Max(0, lastDay - firstDay + 1);
            if (shown == 0) return;
            float cellW = 1f / DaysPerWeek;

            for (int day = firstDay; day <= lastDay; day++)
            {
                int col = day - firstDay;

                var min = new Vector2(col * cellW + 0.004f, 0.02f);
                var max = new Vector2((col + 1) * cellW - 0.004f, 0.98f);
                BuildDayCell(card, day, min, max);
            }
        }

        private void BuildDayCell(MonthlyCard card, int day, Vector2 min, Vector2 max)
        {
            var state = MonthlyCardService.DayState(card.Sku, day);
            var drip = card.Day(day);

            var cell = new GameObject("Day" + day, typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(_gridHost, false);
            var rt = cell.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // The kit floor, enforced rather than assumed: the grid is fraction-anchored, so this
            // sets a hard minimum on the resolved cell instead of trusting the wireframe arithmetic.
            var le = cell.AddComponent<LayoutElement>();
            le.minWidth = CellSize;
            le.minHeight = CellSize;

            var plate = cell.GetComponent<Image>();
            plate.raycastTarget = false;
            // Ledger days are portrait/square tiles. The former landscape card frame only
            // occupied the middle band when nine-sliced into these cells, leaving labels
            // visually detached from their container.
            var cardFrame = Resources.Load<Sprite>("UI/ElarionMedieval/frames/square-icon-frame");
            if (cardFrame != null)
            {
                plate.sprite = cardFrame;
                plate.type = Image.Type.Sliced;
                plate.color = state == MonthlyDayState.Claimed
                    ? new Color(0.62f, 0.72f, 0.62f, 0.72f)
                    : Color.white;
            }
            else
            {
                plate.color = state == MonthlyDayState.Claimed
                    ? new Color(0.055f, 0.08f, 0.065f, 0.98f)
                    : new Color(0.025f, 0.022f, 0.032f, 0.98f);
            }

            // A highlight day gets a gold rail at its head - a MARK, so "this one is bigger" is not
            // carried by a colour.
            if (drip != null && drip.Highlight)
            {
                var mark = new GameObject("mark", typeof(RectTransform), typeof(Image));
                mark.transform.SetParent(cell.transform, false);
                var mrt = mark.GetComponent<RectTransform>();
                mrt.anchorMin = new Vector2(0f, 0.94f); mrt.anchorMax = new Vector2(1f, 1f);
                mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;
                var mimg = mark.GetComponent<Image>();
                mimg.color = NightMarketPalette.Patronage;
                mimg.raycastTarget = false;
                Text(cell.transform, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerMilestone), 13,
                     ElarionUi.Parchment, FontStyles.Bold, TextAlignmentOptions.Center,
                     new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.94f));
            }

            var dayText = Text(cell.transform, StoreStrings.Format(StoreStrings.KeyMonthlyLedgerDay, day), 21,
                ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.82f));
            ElarionUiKit.FitSingleLine(dayText, 16f, 21f);

            // The reward, always drawn - pre-purchase included. That is the point of the screen.
            string body = drip != null && drip.Grant != null ? drip.Grant.Describe() : "-";
            var rewardText = Text(cell.transform, body, 15, ElarionUi.Parchment, FontStyles.Normal,
                 TextAlignmentOptions.Center, new Vector2(0.10f, 0.27f), new Vector2(0.90f, 0.64f));
            rewardText.enableAutoSizing = true;
            rewardText.fontSizeMin = 11f;
            rewardText.fontSizeMax = 15f;
            rewardText.textWrappingMode = TextWrappingModes.Normal;
            rewardText.overflowMode = TextOverflowModes.Truncate;

            // The WORD - what survives a greyscale read.
            var stateText = Text(cell.transform, DayStateWord(state), 17, DayStateColor(state), FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.23f));
            ElarionUiKit.FitSingleLine(stateText, 13f, 17f);

            // THE ONE ANIMATION on this screen.
            if (state == MonthlyDayState.Today)
            {
                var pulse = cell.AddComponent<PulseToday>();
                pulse.Bind(plate, plate.color, ElarionUi.Gold);
            }
        }

        private static string DayStateWord(MonthlyDayState state)
        {
            switch (state)
            {
                case MonthlyDayState.Claimed:   return StoreStrings.Get(StoreStrings.KeyMonthlyLedgerStateClaimed);
                case MonthlyDayState.Today:     return StoreStrings.Get(StoreStrings.KeyMonthlyLedgerStateToday);
                case MonthlyDayState.Available: return StoreStrings.Get(StoreStrings.KeyMonthlyLedgerStateAvailable);
                default:                        return StoreStrings.Get(StoreStrings.KeyMonthlyLedgerStateUpcoming);
            }
        }

        private static Color DayStateColor(MonthlyDayState state)
        {
            switch (state)
            {
                case MonthlyDayState.Claimed:   return NightMarketPalette.Free;
                case MonthlyDayState.Today:     return NightMarketPalette.Patronage;
                case MonthlyDayState.Available: return ElarionUi.Parchment;
                default:                        return ElarionUi.ParchmentDim;
            }
        }

        private void RefreshSide(MonthlyCard card)
        {
            if (_todayReward != null)
            {
                var drip = card.Day(MonthlyCardService.NextDay(card.Sku));
                _todayReward.text = drip != null && drip.Grant != null ? drip.Grant.Describe() : "-";
            }

            if (_exclusiveLine != null)
                _exclusiveLine.text = string.IsNullOrEmpty(card.ExclusiveCosmetic)
                    ? StoreStrings.Get(StoreStrings.KeyMonthlyLedgerExclusiveNone)
                    : card.ExclusiveCosmetic;

            if (_todayLine != null)
                _todayLine.text = StoreStrings.Get(StoreStrings.KeyMonthlyLedgerTodayReward);
        }

        private void RefreshCta(MonthlyCard card)
        {
            bool canClaim = MonthlyCardService.CanClaimToday(card.Sku);
            bool owned = MonthlyCardService.IsActive(card.Sku);

            if (_claimCta != null) _claimCta.interactable = canClaim;
            if (_claimCtaLabel == null) return;

            if (canClaim)      _claimCtaLabel.text = StoreStrings.Get(StoreStrings.KeyMonthlyLedgerClaimCta);
            else if (owned)    _claimCtaLabel.text = StoreStrings.Get(StoreStrings.KeyMonthlyLedgerClaimedToday);
            else               _claimCtaLabel.text = StoreStrings.Get(StoreStrings.KeyMonthlyLedgerNotForSaleCta);
        }

        private void OnClaimTapped()
        {
            var card = ActiveCard;
            if (card == null) return;
            bool ok = MonthlyCardService.Claim(card.Sku);
            FlowTrace.Step("MonthlyCard", "claim CTA tapped for '" + card.Sku + "': " + (ok ? "granted" : "refused"));
            Render();
        }

        private static void ClearChildren(Transform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var child = host.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        /// <summary>The single animated element: the TODAY cell breathes, and nothing else moves.</summary>
        private sealed class PulseToday : MonoBehaviour
        {
            private Image _plate;
            private Color _rest, _lit;

            public void Bind(Image plate, Color rest, Color lit)
            {
                _plate = plate;
                _rest = rest;
                _lit = new Color(lit.r, lit.g, lit.b, 0.30f);
            }

            private void Update()
            {
                if (_plate == null) { enabled = false; return; }
                float t = (Mathf.Sin(Time.unscaledTime * 2.2f) + 1f) * 0.5f;
                _plate.color = Color.Lerp(_rest, _lit, t);
            }
        }

        // =====================================================================
        //  uGUI helpers (same shapes as PackStore / SeasonTrackPanel)
        // =====================================================================

        private static Transform Zone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static Image Plate(Transform parent, Color color)
        {
            if (parent == null) return null;
            var go = new GameObject("plate", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.transform.SetAsFirstSibling();
            return img;
        }

        private static TextMeshProUGUI Text(Transform parent, string text, float size, Color color,
                                            FontStyles style, TextAlignmentOptions align,
                                            Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text ?? string.Empty;
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
