// =============================================================================
// NightMarketRuntimeLayoutRegression [night-market-runtime-layout] (WO-1162 §1
// FIX 3) - the Night Market's layout is proved on REAL RectTransforms, after a
// real layout pass, with real TMP text, at four real surfaces.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression
// Markers:  NIGHT_MARKET_RUNTIME_LAYOUT_OK / NIGHT_MARKET_RUNTIME_LAYOUT_FAIL
//
// ==========================  WHY THIS EXISTS  ================================
// NightMarketUiRegression is a SOURCE oracle. It parses constants and asserts
// source laws, which proves the NUMBERS are present and sane. It cannot prove
// that Unity's own resolution of those numbers - after HorizontalLayoutGroup
// negotiation, LayoutElement minimums, safe-area insets, TMP auto-sizing,
// wrapping and truncation - produces rects that do not overlap. Every one of the
// defects WO-1162 was raised for was legal in source and wrong on the device:
//
//   * the standard card's contents block resolved to 268..360 while its
//     bottom-pinned price row resolved to 268..330 - the SAME lane - and the
//     optional value caption resolved to 366..406 on a card that ends at 344.
//     Three literals, each legal, summing to a card that overdraws itself.
//   * the three body columns were two literals measured on ONE surface, so a
//     narrower aspect quietly pushed the shelf under the width two cards need
//     and the row overran its mask, clipping a price column.
//
// So this suite MEASURES. It builds a canvas, calls the SAME
// NightMarketComposition.Compose and StorePackCard.Build the player gets, forces
// the layout, and reads GetWorldCorners. If it ever re-derived a rect from the
// constants the layout used, it could not fail - the hollow-pass shape WO-1138
// named, and the most expensive defect class in this repo.
//
// ==========================  THE WO-1138 TAXONOMY  ===========================
// FIXTURE ABSENT      -> FAIL, naming the path. packs.json, the card template and
//                        the composition are PRODUCT. Their absence is a defect,
//                        not a harness limitation, and it is reported red with the
//                        path in the message.
// CAPABILITY ABSENT   -> a VISIBLE stand-down that can never read as a pass.
//                        Two are possible here and both are declared through
//                        RegressionOutcome:
//                          - no canvas can be created at all  -> whole-suite Skip
//                            ([SKIPPED], counted out of the green column)
//                          - no TMP font resolvable, so glyph advances cannot be
//                            measured -> PartialSkip on the MEASUREMENT case only;
//                            every geometric case still runs and still gates.
//                          - a live TMP_Text that yields NO textInfo after
//                            ForceMeshUpdate (the same missing font, one label at
//                            a time) -> PartialSkip NAMING that label. Corrected
//                            2026-08-23: that branch used to `return` having
//                            asserted nothing, so a run in which not one glyph
//                            could be counted read exactly like a run in which
//                            every string fit. An oracle that hollow-passes while
//                            policing others for it is the worse of the two.
// CONTENT ABSENT      -> assert THROUGH the fallback. An empty OPTIONAL block is
//                        the card correctly drawing a caption absent rather than
//                        inventing one; an empty REQUIRED string (price, state
//                        word, name) is RED, because on the device an empty label
//                        and a culled one are the same pixel.
// There is deliberately NO branch that returns green having asserted nothing.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Wallet;

namespace DeNelle.Editor.Regression
{
    public static class NightMarketRuntimeLayoutRegression
    {
        // ── Fixtures this suite REQUIRES (absent => red, with the path) ───────
        private const string PacksRes  = "Assets/Resources/Data/Canonical/packs.json";
        private const string CardSrc   = "Assets/_Modules/Wallet/StorePackCard.cs";
        private const string CompSrc   = "Assets/_Modules/Wallet/NightMarketComposition.cs";
        private const string StoreSrc  = "Assets/_Modules/Wallet/PackStore.cs";

        /// <summary>Sub-pixel slack. Two rects touching edge-to-edge are adjacent, not overlapping.</summary>
        private const float Eps = 0.75f;

        /// <summary>The three-band vertical budget PackStore composes on. Pinned in source by
        /// NightMarketUiRegression ("BodyPx = UsableHeightPx - TopBarPx - BottomBandPx"), so the two
        /// suites cannot drift apart silently.</summary>
        private const float TopBarPx  = 100f;
        private const float EdgePadPx = 18f;

        private struct Surface
        {
            public string Name;
            public int W, H;
            /// <summary>Safe-area inset in SCREEN px: left, right, bottom, top.</summary>
            public Vector4 SafeInset;
        }

        private static readonly Surface[] Surfaces =
        {
            new Surface { Name = "2340x1080 phone",        W = 2340, H = 1080, SafeInset = Vector4.zero },
            new Surface { Name = "2670x1200 Seeker",       W = 2670, H = 1200, SafeInset = Vector4.zero },
            new Surface { Name = "1920x1080",              W = 1920, H = 1080, SafeInset = Vector4.zero },
            // The aspect that actually crosses the two-column breakpoint. A landscape tablet.
            new Surface { Name = "1600x1200 4:3 tablet",   W = 1600, H = 1200, SafeInset = Vector4.zero },
            // Notch left + gesture bar bottom, the shape a real cutout device hands us.
            new Surface { Name = "2340x1080 notched",      W = 2340, H = 1080,
                          SafeInset = new Vector4(132f, 44f, 48f, 0f) },
        };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("NIGHT_MARKET_RUNTIME_LAYOUT_OK - " + reason);
            else Debug.LogError("NIGHT_MARKET_RUNTIME_LAYOUT_FAIL: " + reason);
        }

        [MenuItem("Tools/Regression/UI/Night Market Runtime Layout")]
        private static void RunMenu() => RunAll();

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();

            // ── FIXTURES. Absent product is RED, and the message names the path. ──
            foreach (string path in new[] { PacksRes, CardSrc, CompSrc, StoreSrc })
                if (!File.Exists(path))
                    failures.Add("[fixture] MISSING " + path + " - the Night Market cannot be measured " +
                                 "because the thing it measures is not on disk.");

            IReadOnlyList<PackDef> packs = null;
            try { packs = PackCatalog.Packs; }
            catch (Exception ex) { failures.Add("[fixture] PackCatalog.Packs THREW " + ex.GetType().Name + ": " + ex.Message); }
            if (failures.Count == 0 && (packs == null || packs.Count == 0))
                failures.Add("[fixture] the pack catalogue is EMPTY (" + PacksRes + ") - a store with no " +
                             "rows cannot prove its shelf holds a row.");

            if (failures.Count > 0)
            {
                reason = "night-market-runtime-layout FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
                return false;
            }

            // ── CAPABILITY. No canvas => a DECLARED stand-down, never a pass. ──
            GameObject probe = null;
            try
            {
                probe = NewCanvas("nm-oracle-capability-probe", 100f, 100f);
            }
            catch (Exception ex)
            {
                notes.Add("canvas creation threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }
            if (notes.Count > 0)
            {
                return RegressionOutcome.Skip(out reason, "NIGHT MARKET RUNTIME LAYOUT",
                    "no UI canvas can be instantiated in this environment (" + notes[0] +
                    ") - no rect can be measured");
            }

            // ── THE MEASURED CASES ────────────────────────────────────────────
            try
            {
                Case(failures, "glyph-minimums", () => CaseGlyphMinimums(failures, notes, log));
                foreach (var s in Surfaces)
                {
                    var surface = s;
                    Case(failures, "surface:" + surface.Name, () => CaseSurface(surface, packs, failures, notes, log));
                }
                Case(failures, "deficit-unreachable", () => CaseDeficitUnreachable(failures, log));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : string.Empty;
            if (failures.Count == 0)
            {
                reason = "NIGHT MARKET RUNTIME LAYOUT OK - " + Surfaces.Length + " surfaces composed on a live " +
                         "canvas and MEASURED: body columns disjoint, every rect inside the safe area, every " +
                         "control at or over MinTouchPx(" + ElarionUiKit.MinTouchPx.ToString("0") + "), no card " +
                         "text block overlapping the bottom-pinned price lane, no label truncated at the font " +
                         "floor, and two cards per shelf row never under " +
                         StorePackCard.MinCardWidthPx.ToString("0") + "px" + noteStr + "\n" + log;
                return true;
            }

            reason = "night-market-runtime-layout FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the composition's derived minimums, MEASURED against the
        //  real font rather than re-derived from the same arithmetic.
        // =====================================================================
        private static void CaseGlyphMinimums(List<string> failures, List<string> notes, StringBuilder log)
        {
            float floor = ElarionUi.FontFloorMobile;

            float figure = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, "999,999", floor, out string d1);
            float label  = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, "crystals", floor, out string d2);

            if (figure < 0f || label < 0f)
            {
                // CAPABILITY ABSENT, DECLARED. MeasureLineWidthPx returns -1 (never 0) precisely so
                // this cannot be mistaken for "it fits". The geometric cases below still run.
                notes.Add(RegressionOutcome.PartialSkip("[glyph-minimums] real-font measurement",
                    "no TMP font resolvable (" + (figure < 0f ? d1 : d2) + ") - the spotlight minimum " +
                    "is NOT proved against real glyph advances this run"));
                return;
            }

            // The spotlight minimum is the width at which the ledger row's three sub-bands each hold
            // their longest content. Measure the content; assert the derived minimum covers it.
            float spotMin = NightMarketComposition.SpotlightMinPx;
            float inner   = spotMin * NightMarketComposition.LedgerInsetFrac;

            float figureBand = inner * NightMarketComposition.LedgerNumberFrac;
            float labelBand  = inner * NightMarketComposition.LedgerLabelFrac;
            float barBand    = inner * NightMarketComposition.LedgerBarFrac;

            if (figureBand < figure)
                failures.Add($"[glyph-minimums] SpotlightMinPx({spotMin:0}) gives the ledger FIGURE band " +
                             $"{figureBand:0}px, but '999,999' MEASURES {figure:0}px at the {floor:0}px font " +
                             "floor - the exact quantity would clip. Raise the minimum; do not shrink the type.");
            if (labelBand < label)
                failures.Add($"[glyph-minimums] SpotlightMinPx({spotMin:0}) gives the ledger LABEL band " +
                             $"{labelBand:0}px, but 'crystals' MEASURES {label:0}px at the {floor:0}px font floor.");
            if (barBand < NightMarketComposition.LedgerBarMinPx)
                failures.Add($"[glyph-minimums] SpotlightMinPx({spotMin:0}) gives the comparison bar " +
                             $"{barBand:0}px, under its {NightMarketComposition.LedgerBarMinPx:0}px floor.");

            // And the CTA face: the longest Buy label the store can print must fit the canon button.
            float buy = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, "Buy - 123456 SKR", floor, out _);
            float face = ElarionUiKit.CanonCtaWidth * 0.92f;   // BuildObsidianButton insets its label
            if (buy > 0f && buy > face)
                failures.Add($"[glyph-minimums] the widest Buy face 'Buy - 123456 SKR' MEASURES {buy:0}px at the " +
                             $"font floor but the canon button gives it {face:0}px - the price on the BUTTON clips.");

            log.AppendLine($"  [glyph-minimums] spotlight-min {spotMin:0}px vs measured figure {figure:0} / " +
                           $"label {label:0} / buy-face {buy:0}px at floor {floor:0}px.");
        }

        // =====================================================================
        //  CASE 2 (x5) - compose one surface for real and measure everything.
        // =====================================================================
        private static void CaseSurface(Surface s, IReadOnlyList<PackDef> packs,
                                        List<string> failures, List<string> notes, StringBuilder log)
        {
            string tag = "[" + s.Name + "]";
            GameObject root = null;
            try
            {
                // The reference box this surface resolves to under the kit's CanvasScaler
                // (1080x1920, MatchWidthOrHeight 0.5). Derived, not tabulated.
                float scale = Mathf.Pow(2f, Mathf.Lerp(
                    Mathf.Log(s.W / 1080f, 2f), Mathf.Log(s.H / 1920f, 2f), 0.5f));
                float refW = s.W / scale;
                float refH = s.H / scale;

                root = NewCanvas("nm-oracle-" + s.W + "x" + s.H, refW, refH);
                var rootRt = (RectTransform)root.transform;

                // ── The safe-area host, inset in REFERENCE px exactly as PackStore.ApplySafeArea
                //    computes it (fraction of the SURFACE, applied to the REFERENCE box).
                float insetL = s.SafeInset.x / s.W * refW;
                float insetR = s.SafeInset.y / s.W * refW;
                float insetB = s.SafeInset.z / s.H * refH;
                float insetT = s.SafeInset.w / s.H * refH;

                var screen = Region(rootRt, "NightMarket", Vector2.zero, Vector2.one,
                    new Vector2(insetL, insetB), new Vector2(-insetR, -insetT));

                float bottomBandPx = ElarionUiKit.CanonCtaHeight;
                var body = Region(screen, "Body", Vector2.zero, Vector2.one,
                    new Vector2(EdgePadPx, bottomBandPx), new Vector2(-EdgePadPx, -TopBarPx));

                float bodyW = refW - insetL - insetR - 2f * EdgePadPx;
                float bodyH = refH - insetB - insetT - TopBarPx - bottomBandPx;

                // ⛔ THE SAME TWO CALLS THE PLAYER GETS. Not a re-implementation.
                var plan = NightMarketComposition.Resolve(bodyW, bodyH);
                var cols = NightMarketComposition.Compose(body, plan);

                if (plan.Deficit)
                    failures.Add(tag + " composition resolved to a DEFICIT (" + plan.DeficitPx.ToString("0") +
                                 "px short of the two-column minimum) - the shelf row will overrun its mask " +
                                 "and clip a price. " + NightMarketComposition.Describe(plan));

                // ── Populate the market column with a REAL two-up row of REAL cards ──
                var row = BuildCardRowLikeTheStore(cols.Market);
                var models = WorstCaseModels(packs);
                var handles = new List<StorePackCardHandle>();
                for (int i = 0; i < NightMarketComposition.CardsPerRow; i++)
                    handles.Add(StorePackCard.Build(row, models[i % models.Count],
                                                    StorePackCardVariant.Standard, null));

                // ── And the commerce column's ONE Buy control, seated the way
                //    PackStore seats it: pixels, expressed against the real host.
                var ctaHost = Region(cols.Commerce, "CommerceCta", Vector2.zero, new Vector2(1f, 0f),
                    Vector2.zero, new Vector2(0f, plan.CtaHostPx));
                float gutterFrac = Mathf.Clamp(NightMarketComposition.CommerceGutterPx /
                                               Mathf.Max(1f, plan.CommerceWidthPx), 0.02f, 0.20f);
                float y0 = NightMarketComposition.CtaBottomPadPx / Mathf.Max(1f, plan.CtaHostPx);
                float y1 = (NightMarketComposition.CtaBottomPadPx + NightMarketComposition.CtaButtonPx)
                           / Mathf.Max(1f, plan.CtaHostPx);
                var buy = ElarionUiKit.BuildObsidianButton(ctaHost, "Buy - 123456 SKR",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(gutterFrac, y0), new Vector2(1f - gutterFrac, Mathf.Min(1f, y1)), null);

                // ── The one status surface, carrying the worst copy it can carry ──
                var statusBand = Region(cols.Commerce, "CommerceStatus",
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(NightMarketComposition.CommerceGutterPx, -plan.StatusBandPx),
                    new Vector2(-NightMarketComposition.CommerceGutterPx, 0f));
                // The worst SHORT-FORM status the store prints: a large shortfall figure plus the
                // refusal. The band is budgeted for it in every composition, so it is held to
                // rendering WHOLE - a purchase message that loses its second line is a player told
                // half of why nothing happened.
                var status = NewLabel(statusBand,
                    "You are 123,456 Crystals short. Price unavailable.", ElarionUi.FontFloorMobile);

                Settle(rootRt);

                // ── MEASUREMENTS ──────────────────────────────────────────────
                Rect screenRect = WorldRect(screen);

                var columnRects = new List<(string, Rect)>
                {
                    ("spotlight", WorldRect(cols.Spotlight)),
                    ("market",    WorldRect(cols.Market)),
                    ("commerce",  WorldRect(cols.Commerce)),
                };

                // 1. SIBLING OVERLAP - the three body columns are disjoint boxes.
                for (int a = 0; a < columnRects.Count; a++)
                    for (int b = a + 1; b < columnRects.Count; b++)
                        if (Overlaps(columnRects[a].Item2, columnRects[b].Item2))
                            failures.Add(tag + " body columns OVERLAP: " + columnRects[a].Item1 + " " +
                                         Fmt(columnRects[a].Item2) + " intersects " + columnRects[b].Item1 +
                                         " " + Fmt(columnRects[b].Item2) + ". " +
                                         NightMarketComposition.Describe(plan));

                // 2. SAFE-AREA INTRUSION - nothing composed reaches outside the inset host.
                foreach (var c in columnRects)
                    if (!Contains(screenRect, c.Item2))
                        failures.Add(tag + " the " + c.Item1 + " column " + Fmt(c.Item2) +
                                     " reaches OUTSIDE the safe-area host " + Fmt(screenRect) +
                                     " - it would draw under a cutout or the gesture bar.");

                // 3. TOUCH FLOORS - every interactive rect, measured after layout.
                foreach (var btn in root.GetComponentsInChildren<Button>(true))
                {
                    var rt = btn.transform as RectTransform;
                    if (rt == null) continue;
                    Rect r = WorldRect(rt);
                    if (r.width + Eps < ElarionUiKit.MinTouchPx || r.height + Eps < ElarionUiKit.MinTouchPx)
                        failures.Add(tag + " control '" + btn.name + "' resolved to " + Fmt(r) +
                                     ", under the " + ElarionUiKit.MinTouchPx.ToString("0") +
                                     "px touch floor - the clamp would GROW it over its neighbour.");
                }

                // 4. THE CARDS. Two-up, disjoint, each over its readable minimum, and no text block
                //    in the bottom-pinned price lane.
                var cardRects = new List<Rect>();
                for (int i = 0; i < handles.Count; i++)
                {
                    var h = handles[i];
                    if (h == null || h.Root == null)
                    {
                        failures.Add(tag + " the card template returned no root for row slot " + i +
                                     " - a shelf row is missing a card entirely.");
                        continue;
                    }
                    var cardRt = (RectTransform)h.Root.transform;
                    Rect cr = WorldRect(cardRt);
                    cardRects.Add(cr);

                    if (cr.width + Eps < StorePackCard.MinCardWidthPx)
                        failures.Add(tag + " shelf card " + i + " resolved to " + cr.width.ToString("0") +
                                     "px wide, under the " + StorePackCard.MinCardWidthPx.ToString("0") +
                                     "px readable minimum. Change the COMPOSITION, never the card. " +
                                     NightMarketComposition.Describe(plan));

                    if (!Contains(WorldRect(cols.Market), cr))
                        failures.Add(tag + " shelf card " + i + " " + Fmt(cr) + " is not inside the market " +
                                     "column " + Fmt(WorldRect(cols.Market)) + " - the row has overrun its mask.");

                    // 4a. Every label the card built is INSIDE the card. This is the assertion the
                    //     value caption drawn 62px below the card's own bottom edge failed.
                    foreach (var t in h.Root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        Rect lr = WorldRect(t.rectTransform);
                        if (!Contains(cr, lr, 2f))
                            failures.Add(tag + " card " + i + " label '" + Short(t.text) + "' " + Fmt(lr) +
                                         " is drawn OUTSIDE its own card " + Fmt(cr) + ".");
                    }

                    // 4b. NOTHING overlaps the price lane. The price is the one string on this screen
                    //     that must never be occluded (P0-1/P0-2: "20 SKR" for a 120 SKR pack).
                    if (h.PriceLabel != null)
                    {
                        Rect priceRect = WorldRect(h.PriceLabel.rectTransform);
                        foreach (var t in h.Root.GetComponentsInChildren<TextMeshProUGUI>(true))
                        {
                            if (t == h.PriceLabel) continue;
                            if (t.transform.parent != h.Root.transform) continue;   // card-level blocks only
                            Rect lr = WorldRect(t.rectTransform);
                            if (Overlaps(priceRect, lr))
                                failures.Add(tag + " card " + i + ": '" + Short(t.text) + "' " + Fmt(lr) +
                                             " OVERLAPS the bottom-pinned price lane " + Fmt(priceRect) +
                                             " - this is the 268..330 contents-over-price defect.");
                        }
                    }
                    else
                    {
                        failures.Add(tag + " card " + i + " has NO price label - the required half of a " +
                                     "store card is missing.");
                    }

                    // 4c. TEXT CLIPPING. Measured off the generated mesh, not inferred.
                    // ⛔ THE BAR IS DELIBERATELY NOT UNIFORM, AND THAT IS A RULING, NOT A SOFTENING.
                    // The REQUIRED strings - the price and the state word - may never lose a glyph:
                    // "20 SKR" for a 120 SKR pack is the defect this whole screen was rebuilt for.
                    // The CONTENTS SUMMARY is a summary; it already ends "+N more" and ellipsising
                    // it is honest. So contents is held to "renders at all", the rest to "renders
                    // WHOLE". Anyone tempted to relax the price rule to match: don't - move the
                    // layout instead.
                    if (h.PriceLabel != null) CheckNotTruncated(h.PriceLabel, tag + " card " + i + " price", failures, notes, true);
                    if (h.StateLabel != null) CheckNotTruncated(h.StateLabel, tag + " card " + i + " state", failures, notes, true);
                    foreach (var t in h.Root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (t == h.PriceLabel || t == h.StateLabel) continue;
                        bool cardLevel = t.transform.parent == h.Root.transform;
                        // Card-level blocks other than the name are summaries; the name is required.
                        bool required = cardLevel && string.Equals(t.text, models[i % models.Count].Name, StringComparison.Ordinal);
                        CheckNotTruncated(t, tag + " card " + i, failures, notes, required);
                    }
                }

                for (int a = 0; a < cardRects.Count; a++)
                    for (int b = a + 1; b < cardRects.Count; b++)
                        if (Overlaps(cardRects[a], cardRects[b]))
                            failures.Add(tag + " two shelf cards OVERLAP: " + Fmt(cardRects[a]) +
                                         " intersects " + Fmt(cardRects[b]) + ".");

                // 5. The commerce column's own contents stay in their lanes.
                if (buy != null)
                {
                    Rect buyRect = WorldRect((RectTransform)buy.transform);
                    Rect statusRect = status != null ? WorldRect(status.rectTransform) : new Rect();
                    if (status != null && Overlaps(buyRect, statusRect))
                        failures.Add(tag + " the status surface " + Fmt(statusRect) + " OVERLAPS the Buy " +
                                     "control " + Fmt(buyRect) + " - a purchase message drawn through the " +
                                     "button that acts on it.");
                    if (!Contains(WorldRect(cols.Commerce), buyRect, 1f))
                        failures.Add(tag + " the Buy control " + Fmt(buyRect) + " is not inside the commerce " +
                                     "rail " + Fmt(WorldRect(cols.Commerce)) + ".");
                    var buyLabel = buy.GetComponentInChildren<TMP_Text>(true);
                    if (buyLabel != null) CheckNotTruncated(buyLabel, tag + " buy-face", failures, notes);
                }
                else
                {
                    failures.Add(tag + " the kit built NO Buy control - the one commerce CTA is absent.");
                }
                if (status != null) CheckNotTruncated(status, tag + " status", failures, notes);

                log.AppendLine("  " + tag + " ref " + refW.ToString("0") + "x" + refH.ToString("0") + " -> " +
                               NightMarketComposition.Describe(plan));
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // =====================================================================
        //  CASE 3 - the deficit branch is unreachable in landscape.
        // ---------------------------------------------------------------------
        //  The composition CLAMPS rather than shrinking a card when it runs out
        //  of screen, and declares it. That branch is honest, but it must not be
        //  reachable on any surface the game ships to - so this walks the whole
        //  landscape aspect range instead of trusting the five sampled surfaces.
        // =====================================================================
        private static void CaseDeficitUnreachable(List<string> failures, StringBuilder log)
        {
            string worst = null;
            float worstAspect = 0f;
            for (float aspect = 4f / 3f; aspect <= 21f / 9f + 0.001f; aspect += 0.01f)
            {
                float refH = Mathf.Sqrt(1080f * 1920f / aspect);
                float refW = aspect * refH;
                var plan = NightMarketComposition.Resolve(
                    refW - 2f * EdgePadPx, refH - TopBarPx - ElarionUiKit.CanonCtaHeight);
                if (plan.Deficit && worst == null)
                {
                    worst = NightMarketComposition.Describe(plan);
                    worstAspect = aspect;
                }
                if (plan.CardWidthPx + Eps < StorePackCard.MinCardWidthPx)
                    failures.Add($"[deficit-unreachable] at aspect {aspect:0.00}:1 a shelf card resolves to " +
                                 $"{plan.CardWidthPx:0}px, under the {StorePackCard.MinCardWidthPx:0}px minimum. " +
                                 NightMarketComposition.Describe(plan));
            }
            if (worst != null)
                failures.Add($"[deficit-unreachable] the composition hits its DEFICIT clamp at aspect " +
                             $"{worstAspect:0.00}:1, inside the shipped landscape range. " + worst);

            log.AppendLine("  [deficit-unreachable] 4:3 .. 21:9 walked; three-column breakpoint " +
                           NightMarketComposition.ThreeColumnMinBodyPx.ToString("0") + "px, wide at " +
                           NightMarketComposition.ThreeColumnWideBodyPx.ToString("0") + "px.");
        }

        // =====================================================================
        //  Measurement helpers. Everything here reads the LIVE transform.
        // =====================================================================

        /// <summary>
        /// A WorldSpace canvas sized in REFERENCE units. WorldSpace on purpose: a ScreenSpaceOverlay
        /// canvas takes its rect from the editor's own (640x480 in batchmode) window, so it could
        /// not be driven to the four surfaces this suite has to cover.
        /// </summary>
        private static GameObject NewCanvas(string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.hideFlags = HideFlags.HideAndDontSave;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.position = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static RectTransform Region(Transform parent, string name, Vector2 aMin, Vector2 aMax,
                                            Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        /// <summary>
        /// The shelf row, with the SAME layout settings PackStore.BuildCardRow authors. The four
        /// numbers are also what NightMarketComposition.ShelfChromePx is built from, so a change
        /// there and no change here shows up as a measured card-width failure rather than silence.
        /// </summary>
        private static Transform BuildCardRowLikeTheStore(Transform market)
        {
            var go = new GameObject("row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(market, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);

            // The scroll column's own padding, plus the gutter the composition reserves on the
            // right so a scroll indicator can never sit on a card's price column. Together these
            // are ShelfChromePx minus the row's own padding and spacing.
            float insetL = NightMarketComposition.ShelfPadPerSidePx;
            float insetR = NightMarketComposition.ShelfPadPerSidePx + NightMarketComposition.ScrollGutterPx;
            rt.sizeDelta = new Vector2(-(insetL + insetR), StorePackCard.StandardHeightPx);
            rt.anchoredPosition = new Vector2((insetL - insetR) * 0.5f, -NightMarketComposition.ShelfPadPerSidePx);

            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = NightMarketComposition.RowSpacingPx;
            h.padding = new RectOffset((int)NightMarketComposition.RowPadPerSidePx,
                                       (int)NightMarketComposition.RowPadPerSidePx, 3, 3);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return go.transform;
        }

        /// <summary>
        /// The worst content a card can be handed, from WO-1162 FIX 2's own list: the longest REAL
        /// pack name in the catalogue, a two-line name beside a badge, a large integer price, the
        /// "$49.99" minor reference, "Price unavailable", owned state, and a long content summary.
        /// </summary>
        private static List<StorePackCardModel> WorstCaseModels(IReadOnlyList<PackDef> packs)
        {
            string longest = string.Empty;
            for (int i = 0; i < packs.Count; i++)
            {
                var p = packs[i];
                if (p != null && !string.IsNullOrEmpty(p.Name) && p.Name.Length > longest.Length)
                    longest = p.Name;
            }
            if (string.IsNullOrEmpty(longest)) longest = "Founders Vow";

            return new List<StorePackCardModel>
            {
                new StorePackCardModel
                {
                    Sku = "oracle-longest-name", Name = longest,
                    Contents = "2,400 Wood, 2,400 Iron, 1,200 Food, 600 Crystals, 3 Builder Slots +4 more",
                    ValueCaption = "1,922 goods per $",
                    PriceMajor = "123456 SKR", PriceMinor = "$49.99",
                    Badge = "BEST VALUE", StateWord = string.Empty,
                    Band = StoreBand.Patronage, OrbTint = string.Empty, Selected = true,
                },
                new StorePackCardModel
                {
                    Sku = "oracle-owned-unavailable",
                    Name = "The Quartermasters Standing Order",       // forces a two-line name
                    Contents = "Price unavailable",
                    ValueCaption = string.Empty,
                    PriceMajor = "Price unavailable", PriceMinor = string.Empty,
                    Badge = "FOUNDERS", StateWord = "Owned",
                    Band = StoreBand.Basket, OrbTint = string.Empty, Selected = false,
                },
            };
        }

        private static TextMeshProUGUI NewLabel(Transform parent, string text, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);
            t.text = text;
            t.fontSize = size;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>Force Unity to actually resolve the tree, twice - nested layout groups settle on
        /// the second pass, and a first-pass measurement is how a layout oracle reports a rect the
        /// player never sees.</summary>
        private static void Settle(RectTransform root)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                t.ForceMeshUpdate();
        }

        private static readonly Vector3[] _corners = new Vector3[4];

        /// <summary>The rect a RectTransform ACTUALLY resolved to, in canvas units.</summary>
        private static Rect WorldRect(RectTransform rt)
        {
            if (rt == null) return new Rect();
            rt.GetWorldCorners(_corners);
            float x0 = Mathf.Min(_corners[0].x, _corners[2].x);
            float x1 = Mathf.Max(_corners[0].x, _corners[2].x);
            float y0 = Mathf.Min(_corners[0].y, _corners[2].y);
            float y1 = Mathf.Max(_corners[0].y, _corners[2].y);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            if (a.width <= Eps || a.height <= Eps || b.width <= Eps || b.height <= Eps) return false;
            return a.xMin + Eps < b.xMax && b.xMin + Eps < a.xMax &&
                   a.yMin + Eps < b.yMax && b.yMin + Eps < a.yMax;
        }

        private static bool Contains(Rect outer, Rect inner, float slack = 0.5f)
        {
            if (inner.width <= 0f || inner.height <= 0f) return true;   // nothing drawn cannot intrude
            return inner.xMin >= outer.xMin - slack && inner.xMax <= outer.xMax + slack &&
                   inner.yMin >= outer.yMin - slack && inner.yMax <= outer.yMax + slack;
        }

        /// <summary>
        /// Truncation, read off the GENERATED MESH. TMP drops or ellipsises characters it cannot
        /// place; comparing the visible glyph count with the printable characters in the source
        /// string catches that without depending on any one TMP version's overflow API.
        /// </summary>
        private static void CheckNotTruncated(TMP_Text t, string where, List<string> failures,
                                              List<string> notes, bool requireEveryGlyph = true)
        {
            // ⛔ NO BRANCH OUT OF HERE IS SILENT (WO-1138 taxonomy; the meta-ratchet caught the
            // 'info == null' one below returning green having asserted nothing - the most expensive
            // defect class in this repo, and worse in an oracle that polices others for it).
            if (t == null)
            {
                // FIXTURE ABSENT. The caller asked us to measure a label that does not exist; for a
                // REQUIRED string that is the defect itself, and the whole call is meaningless
                // either way, so it is never a quiet return.
                failures.Add(where + ": there is no label object to measure - the string was never built.");
                return;
            }

            int printable = 0;
            string src = t.text ?? string.Empty;
            for (int i = 0; i < src.Length; i++)
                if (!char.IsWhiteSpace(src[i])) printable++;
            if (printable == 0)
            {
                // CONTENT ABSENT. An OPTIONAL block that is drawn absent is the card behaving
                // correctly (the value caption is dropped, not invented). A REQUIRED one - the
                // price, the state word, the pack name - being empty is a red defect: an empty
                // required string is indistinguishable ON THE DEVICE from a culled one.
                if (requireEveryGlyph)
                    failures.Add(where + ": the label is EMPTY - a REQUIRED store string was built " +
                                 "with no text at all, which reads on the device exactly like a culled one.");
                return;
            }

            t.ForceMeshUpdate();
            var info = t.textInfo;
            if (info == null)
            {
                // CAPABILITY ABSENT, DECLARED. A live TMP_Text builds a textInfo on ForceMeshUpdate
                // unless no font could be resolved at all - the SAME capability CaseGlyphMinimums
                // stands down on, so it is declared the SAME way: a PartialSkip that NAMES the label
                // it could not measure. A run where nothing could be measured must never be able to
                // read as a run where everything fit.
                notes.Add(RegressionOutcome.PartialSkip(where + " truncation check",
                    "TMP produced NO textInfo for '" + Short(src) + "' after ForceMeshUpdate (no font " +
                    "resolvable) - this label's glyph count is NOT proved this run"));
                return;
            }

            int visible = 0;
            int count = Mathf.Min(info.characterCount, info.characterInfo != null ? info.characterInfo.Length : 0);
            for (int i = 0; i < count; i++)
                if (info.characterInfo[i].isVisible) visible++;

            if (visible == 0)
            {
                failures.Add(where + ": '" + Short(t.text) + "' renders ZERO glyphs in a " +
                             t.rectTransform.rect.width.ToString("0") + "x" +
                             t.rectTransform.rect.height.ToString("0") + " rect at font " +
                             t.fontSize.ToString("0") + " - the label is culled whole.");
                return;
            }
            if (requireEveryGlyph && visible < printable)
                failures.Add(where + ": '" + Short(t.text) + "' is TRUNCATED - " + visible + " of " +
                             printable + " glyphs drawn in a " + t.rectTransform.rect.width.ToString("0") +
                             "x" + t.rectTransform.rect.height.ToString("0") + " rect at font " +
                             t.fontSize.ToString("0") + " (floor " + ElarionUi.FontFloorMobile.ToString("0") + ").");
        }

        private static string Fmt(Rect r) =>
            "(" + r.xMin.ToString("0") + "," + r.yMin.ToString("0") + " " +
            r.width.ToString("0") + "x" + r.height.ToString("0") + ")";

        private static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Replace("\n", " ");
            return s.Length <= 42 ? s : s.Substring(0, 42) + "...";
        }
    }
}
