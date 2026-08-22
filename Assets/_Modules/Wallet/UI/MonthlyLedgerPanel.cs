// =============================================================================
// MonthlyLedgerPanel - the Monthly Ledger screen (WO section U2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet
//
// CODE-BUILT uGUI. Not UXML - UXML renders EMPTY in player builds.
//
// =============================================================================
//  IT IS A CALENDAR, BECAUSE THE THING BEING DESCRIBED IS A CALENDAR
// -----------------------------------------------------------------------------
// A 10 x 3 grid, all THIRTY days drawn at once, BEFORE the player has paid
// anything. That is the WO's section 3.2 promise - "full 30-day table shown
// pre-purchase, no hidden mystery day" - made STRUCTURAL rather than promised:
// you cannot hide a day in a layout that draws every one of them. There is no
// code path in this file that omits a cell.
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
    /// <summary>The monthly ledger screen: header, 10x3 day grid, side panel, claim footer.</summary>
    [DisallowMultipleComponent]
    public sealed class MonthlyLedgerPanel : MonoBehaviour
    {
        // Zones as fractions of the WO's 2670 x 1200 landscape canvas.
        // Header 120 top / Footer 150 bottom / Grid 1700 wide / Side panel 840 wide.
        private const float GridWidthFraction = 1700f / 2670f;
        private static readonly Vector2 HeaderMin = new Vector2(0.015f, 1f - 120f / 1200f);
        private static readonly Vector2 HeaderMax = new Vector2(0.985f, 0.99f);
        private static readonly Vector2 GridMin   = new Vector2(0.015f, 150f / 1200f);
        private static readonly Vector2 GridMax   = new Vector2(GridWidthFraction, 1f - 120f / 1200f);
        private static readonly Vector2 SideMin   = new Vector2(GridWidthFraction + 0.01f, 150f / 1200f);
        private static readonly Vector2 SideMax   = new Vector2(0.985f, 1f - 120f / 1200f);
        private static readonly Vector2 FooterMin = new Vector2(0.015f, 0.02f);
        private static readonly Vector2 FooterMax = new Vector2(0.985f, 150f / 1200f);

        private const int GridColumns = 10;
        private const int GridRows = 3;

        /// <summary>Cell 158 x 158 from the wireframe, floored against the kit's touch minimum.</summary>
        private static readonly float CellSize = Mathf.Max(158f, ElarionUiKit.MinTouchPx);

        private GameObject _canvas;
        private Transform  _body, _gridHost, _sideHost;
        private TextMeshProUGUI _titleLine, _claimsLine, _todayLine, _todayReward, _exclusiveLine;
        private Button _claimCta;
        private TextMeshProUGUI _claimCtaLabel;

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

            _canvas = ElarionUiKit.BuildModalCanvas("MonthlyLedgerUI", 621);
            if (_canvas == null)
            {
                FlowTrace.Fail("MonthlyCard", "MonthlyLedgerPanel: modal canvas failed to build - screen not shown.");
                return;
            }

            ElarionUiKit.Scrim(_canvas.transform, Close);
            var panel = ElarionUiKit.Panel(_canvas.transform, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f),
                                           deep: true);
            _body = panel.transform;
            Plate(_body, NightMarketPalette.Ground);

            BuildHeader();
            _gridHost = Zone(_body, "Grid", GridMin, GridMax);
            _sideHost = Zone(_body, "Side", SideMin, SideMax);
            Plate(_sideHost, NightMarketPalette.GroundRaised);
            BuildSidePanel();
            BuildFooter();

            _canvas.SetActive(false);
            FlowTrace.Step("MonthlyCard", "MonthlyLedgerPanel built: header, 10x3 grid, side panel, claim footer.");
        }

        private void BuildHeader()
        {
            var host = Zone(_body, "Header", HeaderMin, HeaderMax);

            _titleLine = Text(host, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerTitle), 20,
                              ElarionUi.Parchment, FontStyles.Bold, TextAlignmentOptions.Left,
                              new Vector2(0f, 0f), new Vector2(0.55f, 1f));

            // ⛔ A COUNT OF CLAIMS, NEVER A DATE OR A CLOCK. See the file header.
            _claimsLine = Text(host, string.Empty, 15, NightMarketPalette.Patronage, FontStyles.Bold,
                               TextAlignmentOptions.Right, new Vector2(0.55f, 0f), new Vector2(0.93f, 1f));

            ElarionUiKit.ObsidianCloseButton(host, Close);
        }

        private void BuildSidePanel()
        {
            _todayLine = Text(_sideHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerTodayReward), 13,
                              ElarionUi.Parchment, FontStyles.Bold, TextAlignmentOptions.Left,
                              new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f));

            _todayReward = Text(_sideHost, string.Empty, 12, NightMarketPalette.Free, FontStyles.Normal,
                                TextAlignmentOptions.Left,
                                new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.85f));

            // The month-exclusive keepsake slot. Unauthored today, and the copy SAYS SO rather than
            // drawing an empty frame - a blank box on a paid card reads as broken or as a tease.
            _exclusiveLine = Text(_sideHost, string.Empty, 11, ElarionUi.ParchmentDim, FontStyles.Italic,
                                  TextAlignmentOptions.Left,
                                  new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.66f));

            // The full-value promise. It is the reason the pool model exists and it is stated on the
            // screen, not just in the data file.
            Text(_sideHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerPoolPromise), 11,
                 ElarionUi.Parchment, FontStyles.Normal, TextAlignmentOptions.Left,
                 new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.44f));

            Text(_sideHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerNoTimer), 10,
                 NightMarketPalette.Free, FontStyles.Bold, TextAlignmentOptions.Left,
                 new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.25f));

            Text(_sideHost, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerBonusOnly), 10,
                 ElarionUi.ParchmentDim, FontStyles.Normal, TextAlignmentOptions.Left,
                 new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.15f));
        }

        private void BuildFooter()
        {
            var host = Zone(_body, "Footer", FooterMin, FooterMax);
            Plate(host, new Color(0f, 0f, 0f, 0.30f));

            Text(host, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerNoCard), 11, ElarionUi.ParchmentDim,
                 FontStyles.Normal, TextAlignmentOptions.Left,
                 new Vector2(0.02f, 0.10f), new Vector2(0.62f, 0.90f));

            _claimCta = ElarionUiKit.Button(host, StoreStrings.Get(StoreStrings.KeyMonthlyLedgerClaimCta),
                                            ElarionUiKit.ButtonKind.Gold,
                                            new Vector2(0.66f, 0.08f), new Vector2(0.98f, 0.92f),
                                            OnClaimTapped);
            _claimCtaLabel = _claimCta != null ? _claimCta.GetComponentInChildren<TextMeshProUGUI>() : null;
        }

        // =====================================================================
        //  Render
        // =====================================================================

        /// <summary>Rebuilds the 30-day grid and refreshes every live line.</summary>
        public void Render()
        {
            if (_gridHost == null) return;
            using var _ = FlowTrace.Enter("MonthlyCard", "MonthlyLedgerPanel.Render");

            // ⚠ DETACH BEFORE Destroy. Destroy is DEFERRED to end-of-frame, so a doomed child still
            // counts in childCount until then. Render can run twice in one frame (the claim CTA
            // renders, and the Changed event it raised renders again) — without the detach the
            // second pass would draw a second full grid over one that has not been reaped, and the
            // player would briefly see sixty days.
            for (int i = _gridHost.childCount - 1; i >= 0; i--)
            {
                var child = _gridHost.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

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
                _titleLine.text = StoreStrings.Get(StoreStrings.KeyMonthlyLedgerTitle) + " - " + (card.Name ?? "");

            if (_claimsLine != null)
                _claimsLine.text = StoreStrings.Format(StoreStrings.KeyMonthlyLedgerClaimsLeft,
                                                       MonthlyCardService.ClaimsRemaining(card.Sku));

            BuildGrid(card);
            RefreshSide(card);
            RefreshCta(card);
        }

        /// <summary>
        /// Draws ALL <c>durationDays</c> cells, unconditionally.
        /// <para>The loop bound is the card's own durationDays and there is no state test inside it
        /// that can skip a cell. That is the "no hidden day" promise expressed as control flow.</para>
        /// </summary>
        private void BuildGrid(MonthlyCard card)
        {
            int total = Mathf.Max(0, card.DurationDays);
            float cellW = 1f / GridColumns;
            float cellH = 1f / GridRows;

            for (int i = 0; i < total; i++)
            {
                int day = i + 1;
                int col = i % GridColumns;
                int row = i / GridColumns;
                if (row >= GridRows) break;   // a longer table would need a taller grid, not a hidden day

                var min = new Vector2(col * cellW + 0.004f, 1f - (row + 1) * cellH + 0.010f);
                var max = new Vector2((col + 1) * cellW - 0.004f, 1f - row * cellH - 0.010f);
                BuildDayCell(card, day, min, max);
            }

            if (total > GridColumns * GridRows)
                FlowTrace.Fail("MonthlyCard", "card '" + card.Sku + "' authors " + total + " days but the grid is " +
                                              GridColumns + "x" + GridRows + " - days beyond " +
                                              (GridColumns * GridRows) + " would be INVISIBLE. Widen the grid; " +
                                              "never let a day go undrawn.");
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
            plate.color = state == MonthlyDayState.Claimed
                ? new Color(NightMarketPalette.Free.r, NightMarketPalette.Free.g, NightMarketPalette.Free.b, 0.10f)
                : NightMarketPalette.GroundRaised;

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
            }

            Text(cell.transform, day.ToString(), 12, ElarionUi.Parchment, FontStyles.Bold,
                 TextAlignmentOptions.Center, new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.92f));

            // The reward, always drawn - pre-purchase included. That is the point of the screen.
            string body = drip != null && drip.Grant != null ? drip.Grant.Describe() : "-";
            Text(cell.transform, body, 8, ElarionUi.ParchmentDim, FontStyles.Normal,
                 TextAlignmentOptions.Center, new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.64f));

            // The WORD - what survives a greyscale read.
            Text(cell.transform, DayStateWord(state), 8, DayStateColor(state), FontStyles.Bold,
                 TextAlignmentOptions.Center, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.24f));

            // THE ONE ANIMATION on this screen.
            if (state == MonthlyDayState.Today)
            {
                var pulse = cell.AddComponent<PulseToday>();
                pulse.Bind(plate, NightMarketPalette.GroundRaised, NightMarketPalette.Patronage);
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
            else               _claimCtaLabel.text = StoreStrings.Get(StoreStrings.KeyMonthlyLedgerNotForSale);
        }

        private void OnClaimTapped()
        {
            var card = ActiveCard;
            if (card == null) return;
            bool ok = MonthlyCardService.Claim(card.Sku);
            FlowTrace.Step("MonthlyCard", "claim CTA tapped for '" + card.Sku + "': " + (ok ? "granted" : "refused"));
            Render();
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
