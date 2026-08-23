// UI-001: independent source oracle for the landscape Night Market and its persistent HUD door.
// Presentation only. It deliberately does not inspect or alter PurchaseGate/payment transport.
//
// =============================================================================
//  ⚠ RE-POINTED 2026-08-23 — READ THIS BEFORE "FIXING" A RED FROM THIS FILE.
// -----------------------------------------------------------------------------
//  This suite was written against the PRE-§R Night Market and then never
//  REGISTERED, so it had NEVER RUN. When it was registered it went red with 21
//  failures — and 20 of them were this ORACLE being stale, not the screen being
//  broken: the §R rebuild (2026-08-22) replaced a fraction-anchored two-column
//  layout with an authored REFERENCE-PIXEL three-band / three-column budget, and
//  replaced four inline MakeText calls per card with the one StorePackCard
//  template. The old assertions looked for `SpotlightMin`, `CardHeightPx`,
//  `pack.Name, 24` — identifiers the rebuild legitimately retired. An oracle that
//  names a vanished identifier does not detect a defect; it reports its own
//  staleness in the defect's voice, which is worse than silence.
//
//  ⛔ SO THE RULE FOR THIS FILE: assert the INTENT, read the CURRENT names, and
//  keep the THRESHOLDS independent. Every bound below is an acceptance bound
//  owned by this oracle — shrinking the implementation toward it still goes red.
//  What is NOT duplicated here are the kit's canon numbers (MinTouchPx,
//  CanonCtaWidth/Height, FontFloorMobile): those are PARSED OUT OF ElarionUiKit /
//  ElarionUi at run time, because ~25 files derive from them and a 26th copy in a
//  test is exactly the duplicated-state drift CLAUDE.md §2/§5/§16 keep recording.
//
//  ⛔ AND THE TEXT SCAN IS COMMENT/STRING-BLIND ON PURPOSE. The old
//  "more than one Realm Store routing authority" red was a FALSE POSITIVE: it
//  counted raw occurrences of `PanelRouter.Open(PanelId.RealmStore)` in
//  HudKitController, and three matched — one real call site, one explanatory
//  COMMENT, and one FlowTrace.Fail STRING that quotes the call it is reporting on.
//  Counting authority by substring punishes a file for documenting itself. The
//  count now runs over Code(), which strips comments and string literals first.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using DeNelle.Wallet;   // WO-1162: the composition + card template are the live authority now

namespace DeNelle.Editor.Regression
{
    public static class NightMarketUiRegression
    {
        private const string StoreRel  = "/_Modules/Wallet/PackStore.cs";
        private const string CardRel   = "/_Modules/Wallet/StorePackCard.cs";
        private const string FooterRel = "/_Modules/Wallet/StoreLegalFooter.cs";
        private const string HudRel    = "/_Modules/HUD/Kit/HudKitController.cs";
        private const string CompositionRel = "/_Modules/Wallet/NightMarketComposition.cs";
        private const string KitRel    = "/_Modules/Core/UI/ElarionUiKit.cs";
        private const string UiRel     = "/_Modules/Core/UI/ElarionUi.cs";

        // ── Independent acceptance bounds. Owned here, not imported. ──────────
        private const float MinPanelWidthShare   = 0.80f;  // the store is the screen (owner ruling 1)
        private const float MinBodyHeightShare   = 0.65f;  // vertical must not be given away to chrome
        private const float MinShelfWidthShare   = 0.40f;  // the shelf is the widest of the three columns
        private const float MinStandardCardPx    = 240f;   // readability floor for a priced card
        private const int   RequiredCardsPerRow  = 2;      // device-verified readability ruling

        [MenuItem("Tools/Regression/UI/Night Market Landscape")]
        public static void RunMenu()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log(reason); else Debug.LogError(reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("--- NIGHT MARKET UI-001 ---\n");

            string store  = Read(Application.dataPath + StoreRel,  failures);
            string card   = Read(Application.dataPath + CardRel,   failures);
            string footer = Read(Application.dataPath + FooterRel, failures);
            string hud    = Read(Application.dataPath + HudRel,    failures);
            string kit    = Read(Application.dataPath + KitRel,    failures);
            string ui     = Read(Application.dataPath + UiRel,     failures);

            // The kit's canon numbers, parsed — never re-declared (see header).
            float minTouch  = Scalar(kit, "MinTouchPx",       failures);
            float ctaWidth  = Scalar(kit, "CanonCtaWidth",    failures);
            float ctaHeight = Scalar(kit, "CanonCtaHeight",   failures);
            float fontFloor = Scalar(ui,  "FontFloorMobile",  failures);

            CheckLandscapeBudget(store, ctaHeight, failures);
            CheckCard(card, minTouch, fontFloor, failures);
            CheckFreeBand(store, minTouch, failures);
            CheckCommerceCta(store, minTouch, ctaWidth, failures);
            CheckOneTitleAndOneLegalOwner(store, footer, ctaWidth, failures);
            CheckHudDoor(hud, failures);

            if (failures.Count > 0)
            {
                reason = "NIGHT_MARKET_UI_FAIL\n - " + string.Join("\n - ", failures);
                return false;
            }

            log.Append("NIGHT_MARKET_UI_OK — landscape body, one visible title, readable cards, " +
                       "one legal owner, persistent single-authority HUD door");
            reason = log.ToString();
            return true;
        }

        // =====================================================================
        //  §R2 — three bands vertically, three columns horizontally, in ref px.
        // =====================================================================
        private static void CheckLandscapeBudget(string store, float ctaHeight, List<string> failures)
        {
            var panelMin = Vector(store, "PanelMin", failures);
            var panelMax = Vector(store, "PanelMax", failures);
            if (panelMax.x - panelMin.x < MinPanelWidthShare)
                failures.Add($"Night Market panel uses only {(panelMax.x - panelMin.x):P0} of landscape width " +
                             $"(minimum {MinPanelWidthShare:P0}).");

            // WO-1162: CardsPerRow is owned by NightMarketComposition (the shelf minimum is stated
            // in terms of it), so it is read off the live type rather than parsed as a literal.
            float cardsPerRow = NightMarketComposition.CardsPerRow;
            if (Math.Abs(cardsPerRow - RequiredCardsPerRow) > 0.01f)
                failures.Add($"priced shelf is {cardsPerRow:0}-up; the device-verified readability " +
                             $"ruling is {RequiredCardsPerRow}-up.");

            float usableW = Scalar(store, "UsableWidthPx",   failures);
            float usableH = Scalar(store, "UsableHeightPx",  failures);
            float topBar  = Scalar(store, "TopBarPx",        failures);
            // ⚠ RE-POINTED 2026-08-23 (WO-1162). The three column widths are no longer literals in
            // PackStore: NightMarketComposition DERIVES each rail's minimum from the narrowest
            // content it must hold and RESOLVES a composition for the surface. So this suite asks
            // the live type for the plan it would resolve at the reference surface instead of
            // parsing two numbers that no longer exist — the acceptance bounds below are unchanged.
            float pad  = Scalar(store, "EdgePadPx", failures);
            float gap  = DeNelle.Wallet.NightMarketComposition.ColumnGapPx;

            var refPlan = DeNelle.Wallet.NightMarketComposition.Resolve(
                usableW - 2f * pad, usableH - topBar - ctaHeight);
            float spot     = refPlan.SpotlightWidthPx;
            float commerce = refPlan.CommerceWidthPx;

            if (refPlan.Mode == DeNelle.Wallet.NightMarketMode.StackedTwoColumn)
                failures.Add("the REFERENCE landscape surface (2120x978) no longer resolves to three " +
                             "columns — the derived minimums have grown past the surface the store " +
                             "ships on. Composition: " +
                             DeNelle.Wallet.NightMarketComposition.Describe(refPlan));
            if (refPlan.Deficit)
                failures.Add("the reference landscape surface resolves to a DEFICIT composition (" +
                             refPlan.DeficitPx.ToString("0") + "px short) — cards would overrun the shelf mask.");

            // The formula itself must stay a derivation, not drift back into a literal.
            Require(Read(Application.dataPath + CompositionRel, failures),
                "SpotlightMinPx + ShelfMinForTwoCardsPx + CommerceMinPx + 2f * ColumnGapPx",
                "the three-column breakpoint is no longer derived from the content minimums — a " +
                "hardcoded breakpoint cannot know what it is protecting.", failures);

            // The ONE bottom band IS the canon CTA height — that identity is what makes a second
            // band structurally impossible (§6 / P1-6). Assert the identity, do not restate 132.
            Require(store, "BottomBandPx = ElarionUiKit.CanonCtaHeight",
                "the single bottom band no longer IS the canon CTA height — a second band can " +
                "reappear underneath it (P1-6).", failures);
            Require(store, "BodyPx = UsableHeightPx - TopBarPx - BottomBandPx",
                "body height is no longer derived from the budget; a hardcoded body height can " +
                "silently over- or under-run the 978-unit landscape canvas.", failures);

            float body = usableH - topBar - ctaHeight;
            if (usableH > 0f && body / usableH < MinBodyHeightShare)
                failures.Add($"store body is only {(body / usableH):P0} of the landscape canvas " +
                             $"(minimum {MinBodyHeightShare:P0}) — too much vertical space is chrome.");

            // Three columns, side by side, inside the padded width. Overlap here is the P0-3 class.
            float rails = spot + commerce + (2f * gap) + (2f * pad);
            float shelf = usableW - rails;
            if (shelf <= 0f)
                failures.Add("spotlight + commerce rails consume the whole body width — the shelf " +
                             "has no room and the columns overlap.");
            else
            {
                if (usableW > 0f && shelf / usableW < MinShelfWidthShare)
                    failures.Add($"shelf owns only {(shelf / usableW):P0} of the body width " +
                                 $"(minimum {MinShelfWidthShare:P0}).");
                if (shelf < spot || shelf < commerce)
                    failures.Add("shelf is narrower than one of its side rails — the merchandise is " +
                                 "no longer the widest column.");
            }
        }

        // =====================================================================
        //  §R3 — the ONE card template.
        // =====================================================================
        private static void CheckCard(string card, float minTouch, float fontFloor, List<string> failures)
        {
            // ⚠ RE-POINTED 2026-08-23 (WO-1162 FIX 2). The card heights are DERIVED from the block
            // budget they must carry now, so they are properties, not parseable literals. Read them
            // off the live type; the acceptance bounds below are unchanged.
            float standard = StorePackCard.StandardHeightPx;
            float compact  = StorePackCard.CompactHeightPx;
            float minWidth = StorePackCard.MinCardWidthPx;

            // The budget must SUM, or the stack overruns the price lane again (the 268..330 overlap).
            float requiredStandard = StorePackCard.StandardArtPx
                                   + StorePackCard.NameBlockPx(StorePackCardVariant.Standard)
                                   + StorePackCard.ContentsBlockPx + StorePackCard.PriceBlockPx;
            if (standard < requiredStandard)
                failures.Add($"standard card ({standard:0}px) is shorter than the blocks it must carry " +
                             $"({requiredStandard:0}px of art + name + contents + price, before any gap) — " +
                             "the text stack will reach into the bottom-pinned price lane.");

            if (standard < MinStandardCardPx)
                failures.Add($"standard card height {standard:0}px is below the {MinStandardCardPx:0}px " +
                             "readability floor.");
            // A card carries an art well AND a text stack; each must clear the touch/readability
            // floor on its own, so the shortest variant is bounded at two floors, not one.
            if (compact < minTouch * 2f)
                failures.Add($"compact card height {compact:0}px is under two touch floors " +
                             $"({minTouch * 2f:0}px) — art well and text stack cannot both clear it.");
            if (minWidth < minTouch * 2f)
                failures.Add($"minimum card width {minWidth:0}px is under two touch floors " +
                             $"({minTouch * 2f:0}px).");

            // Every authored type size on the card clears the project's own mobile floor. Generic on
            // purpose: adding a new Font* constant under the floor fails without editing this suite.
            foreach (Match m in Regex.Matches(card, @"private const int (Font\w+)\s*=\s*(\d+);"))
            {
                if (float.TryParse(m.Groups[2].Value, out float px) && px < fontFloor)
                    failures.Add($"card type size {m.Groups[1].Value}={px:0} is below the mobile " +
                                 $"readability floor ({fontFloor:0}).");
            }
            float fontName  = Scalar(card, "FontName",  failures);
            float fontPrice = Scalar(card, "FontPrice", failures);
            if (fontPrice < fontName)
                failures.Add("the price is set smaller than the pack name — price is the one string " +
                             "on this screen that must never be the hardest to read.");

            // Structure, so the name/state lanes CANNOT overlap: the pill lives in the art well and
            // the text stack starts BELOW it. (The old suite compared two anchored rects that the
            // rebuild retired; this asserts the property those rects were a proxy for.)
            Require(card, "float y = artH + TextGapPx",
                "the card's text stack no longer starts below the art well — name and the state/badge " +
                "pill can occupy the same lane.", failures);
            Require(card, "float priceLaneTop = cardH - (BottomPadPx + PriceBlockPx)",
                "the card's text stack no longer reserves the bottom-pinned price lane before it " +
                "spends its budget — that is the 268..330 contents-over-price overlap (WO-1162 FIX 2).",
                failures);
            Require(card, "budget >= CaptionBlockPx",
                "the OPTIONAL value caption is drawn without checking the remaining budget — it was " +
                "landing 62px BELOW the card's own bottom edge.", failures);
            Require(card, "BuildPill(card, Ascii(pill), cardH, artH)",
                "the state/badge pill is no longer seated against the art well.", failures);
            Require(card, "BottomAnchoredText(card, model.PriceMajor",
                "the price is no longer bottom-pinned — a two-line name can push it out of the card " +
                "(P0-1/P0-2 showed '20 SKR' for a 120 SKR pack).", failures);
            Require(card, "FitSingleLine(handle.PriceLabel",
                "the price has no single-line fit guard and can clip its leading digit.", failures);
            Require(card, "StateWord",
                "the card no longer renders commerce state as a WORD — hue alone cannot carry state " +
                "(the owner is red/green colourblind).", failures);
        }

        // =====================================================================
        //  FREE TONIGHT — one tab rail, every tab over the touch floor.
        //  (Re-pointed 2026-08-23: the two two-up "free door" card rows were
        //  replaced by a single row of three utility TABS. The assertion is the
        //  same one it always was — nothing here may be authored under the floor,
        //  because a sub-floor control is GROWN over its neighbour by the clamp.)
        // =====================================================================
        private static void CheckFreeBand(string store, float minTouch, List<string> failures)
        {
            string freeBand = Slice(store, "private void BuildFreeBand", "private void BuildUtilityTab", failures);

            if (Regex.Matches(freeBand, @"BuildCardRow\(").Count != 1)
                failures.Add("FREE TONIGHT is not composed as ONE tab rail.");
            if (Regex.Matches(freeBand, @"BuildUtilityTab\(").Count != 3)
                failures.Add("FREE TONIGHT does not carry its three doors (redeem + season track + monthly ledger).");
            // Nothing is asked for before something is given: the redeem door is first on the rail.
            int redeemAt = freeBand.IndexOf("OpenRedeemPanel", StringComparison.Ordinal);
            int seasonAt = freeBand.IndexOf("PanelId.BattlePass", StringComparison.Ordinal);
            if (redeemAt < 0)
                failures.Add("the promo redeem door has no entry point in the FREE band.");
            else if (seasonAt >= 0 && redeemAt > seasonAt)
                failures.Add("the redeem door is not first on the FREE rail.");

            string tab = Slice(store, "private void BuildUtilityTab", "private void BuildFreeDoor", failures);
            float rowH  = Scalar(store, "FreeTabRowPx",      failures);
            float minW  = Scalar(store, "FreeTabMinWidthPx", failures);
            float padY  = RowVerticalPadding(store);
            float usable = rowH - padY;

            var face = AnchorsAfter(tab, "ObsidianButtonColor.Gray,", failures);
            float facePx = (face.max.y - face.min.y) * usable;
            if (facePx < minTouch)
                failures.Add($"FREE-band utility tab derives to {facePx:0}px tall, below the {minTouch:0}px " +
                             "touch floor — the clamp will grow it over the tab beside it.");
            float faceWidthPx = (face.max.x - face.min.x) * minW;
            if (faceWidthPx < minTouch)
                failures.Add($"FREE-band utility tab derives to {faceWidthPx:0}px wide, below the " +
                             $"{minTouch:0}px touch floor.");

            Require(tab, "FitSingleLine(text,",
                "the FREE-band tab label has no explicit readable single-line guard — 'Redeem a Code' " +
                "truncated to 'Redee...' on the owner's device (UI-001 defect #4).", failures);
        }

        // =====================================================================
        //  The ONE Buy control, in the commerce column.
        // =====================================================================
        private static void CheckCommerceCta(string store, float minTouch, float ctaWidth, List<string> failures)
        {
            // ⚠ RE-POINTED 2026-08-23 (WO-1162). The CTA is no longer a fraction pair typed into
            // BuildCommerce; it is authored in PIXELS against the resolved plan's CTA sub-host. So
            // the assertion is now the stronger one it was always a proxy for: the Buy control
            // clears the touch floor in EVERY composition, not just the one the literals were
            // measured in. The old fraction-of-a-fixed-440 form could not see the stacked case.
            Require(store, "NightMarketComposition.CtaBottomPadPx / ctaHostPx",
                "the Buy control's height is no longer authored in pixels against its real host — a " +
                "fraction of a host that shrinks lands the button under the touch floor, where the " +
                "clamp GROWS it over its neighbour.", failures);

            foreach (var probe in CompositionProbes())
            {
                var plan = DeNelle.Wallet.NightMarketComposition.Resolve(probe.w, probe.h);
                float ctaPx = DeNelle.Wallet.NightMarketComposition.CtaButtonPx;
                if (ctaPx < minTouch)
                    failures.Add($"[{probe.name}] commerce CTA is {ctaPx:0}px tall (floor {minTouch:0}px).");
                if (plan.CtaHostPx < DeNelle.Wallet.NightMarketComposition.CtaHostMinPx)
                    failures.Add($"[{probe.name}] CTA sub-host resolved to {plan.CtaHostPx:0}px, under the " +
                                 $"{DeNelle.Wallet.NightMarketComposition.CtaHostMinPx:0}px the button + " +
                                 "its padding needs - the button cannot be seated at canon size.");

                float ctaWidthPx = plan.CommerceWidthPx - 2f * DeNelle.Wallet.NightMarketComposition.CommerceGutterPx;
                if (ctaWidthPx < ctaWidth)
                    failures.Add($"[{probe.name}] commerce rail leaves {ctaWidthPx:0}px for a canon " +
                                 $"{ctaWidth:0}px-wide Buy control.");

                if (plan.CardWidthPx < StorePackCard.MinCardWidthPx)
                    failures.Add($"[{probe.name}] shelf card resolves to {plan.CardWidthPx:0}px, under the " +
                                 $"{StorePackCard.MinCardWidthPx:0}px readable minimum — the row will overrun " +
                                 "its mask and clip a price.");
            }
        }

        /// <summary>
        /// The body boxes this suite resolves a composition for. Landscape only — the store is a
        /// landscape screen — and deliberately including a 4:3 tablet, which is the aspect that
        /// actually crosses the two-column breakpoint.
        /// </summary>
        private static (string name, float w, float h)[] CompositionProbes()
        {
            // Reference box = sqrt(1080*1920/aspect) tall by aspect*that wide (CanvasScaler 1080x1920,
            // MatchWidthOrHeight 0.5). Body = that minus 2*EdgePad(18) and the 100/132 bands.
            (string name, float aspect)[] surfaces =
            {
                ("2340x1080 (phone)", 2340f / 1080f),
                ("2670x1200 (Seeker)", 2670f / 1200f),
                ("1920x1080",          1920f / 1080f),
                ("1600x1200 (4:3)",    4f / 3f),
            };
            var probes = new (string, float, float)[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                float h = Mathf.Sqrt(1080f * 1920f / surfaces[i].aspect);
                float w = surfaces[i].aspect * h;
                probes[i] = (surfaces[i].name, w - 36f, h - 100f - 132f);
            }
            return probes;
        }

        // =====================================================================
        //  One visible title; one owner of the legal band.
        // =====================================================================
        private static void CheckOneTitleAndOneLegalOwner(string store, string footer, float ctaWidth,
                                                          List<string> failures)
        {
            if (Regex.Matches(store, "KeyWordmark").Count != 1) // modal title only; no body duplicate
                failures.Add("wordmark occurrence count drifted; confirm the visible title is rendered exactly once.");

            Require(store, "StoreLegalFooter.Build(",
                "shared legal/footer component is absent — the store authors its claims inline again.", failures);
            Require(footer, "public static class StoreLegalFooter",
                "StoreLegalFooter is not a shared component.", failures);

            // ⛔ ONE OWNER. The keep-out the canon Close carves out of the band lives with the copy
            // it protects; a second copy in PackStore is how the band and the button came to hold
            // two versions of one measurement.
            if (Code(store).Contains("CloseKeepOutPx"))
                failures.Add("PackStore declares a second Close keep-out — StoreLegalFooter owns it.");
            Require(footer, "ElarionUiKit.CanonCtaWidth",
                "the legal band's Close keep-out is not derived from the canon button width.", failures);
            if (ctaWidth <= 0f)
                failures.Add("could not read CanonCtaWidth from the kit — the keep-out cannot be verified.");

            Require(footer, "FontLegalPx",
                "the legal band has no named type size; legal copy can drift under the readability floor.",
                failures);
            Require(footer, "ElarionUiKit.FitBlock(t,",
                "the legal band's copy has no fit guard — a claim long enough to wrap OVERFLOWS the " +
                "132-unit band upward, over the shelf card above it (seen in the 2026-08-23 capture).",
                failures);
        }

        // =====================================================================
        //  The persistent HUD door — exactly ONE routing authority.
        // =====================================================================
        private static void CheckHudDoor(string hud, List<string> failures)
        {
            Require(hud, "RealmStoreHudButton", "dedicated HUD Store face is absent", failures);
            Require(hud, "sizeDelta = new Vector2(dockTabPx, dockTabPx)",
                "HUD Store face is not pinned to the touch floor", failures);
            Require(hud, "SafeAreaInset.EdgeMarginPx",
                "HUD Store face is not seated inside the common safe-area margin", failures);
            Require(hud, "PanelRouter.Open(PanelId.RealmStore)",
                "HUD Store does not route to the existing Realm Store door", failures);

            // Count CALL SITES, not substrings — comments and log strings are not authorities.
            int authorities = Regex.Matches(Code(hud), @"PanelRouter\.Open\(PanelId\.RealmStore\)").Count;
            if (authorities != 1)
                failures.Add($"HUD declares {authorities} Realm Store routing authorities; there must be exactly one.");

            if (hud.Contains("AddDockTab(_slideDock.panel, 5, \"Realm Store\""))
                failures.Add("Realm Store still occupies a drawer row in addition to the persistent face.");
            Require(hud, "Register(\"chatDock\"",
                "Store/Menu column is not posture-owned with the dock", failures);
        }

        // =====================================================================
        //  Source helpers.
        // =====================================================================
        private static string Read(string path, List<string> failures)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            failures.Add("missing source: " + path);
            return string.Empty;
        }

        /// <summary>
        /// The file with `//` comments, `/* */` blocks and "string literals" blanked out, so a text
        /// scan measures CODE. Written character-wise rather than by regex because a regex that
        /// tries to do all three at once gets the nesting wrong on exactly the lines that matter.
        /// </summary>
        private static string Code(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                char n = i + 1 < source.Length ? source[i + 1] : '\0';

                if (c == '/' && n == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && n == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                    i = Math.Min(source.Length, i + 2);
                    continue;
                }
                if (c == '"')
                {
                    // Verbatim strings (@"...") escape only by doubling the quote.
                    bool verbatim = i > 0 && source[i - 1] == '@';
                    i++;
                    while (i < source.Length)
                    {
                        if (!verbatim && source[i] == '\\') { i += 2; continue; }
                        if (source[i] == '"')
                        {
                            if (verbatim && i + 1 < source.Length && source[i + 1] == '"') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    continue;
                }
                if (c == '\'')
                {
                    i++;
                    while (i < source.Length && source[i] != '\'')
                        i += source[i] == '\\' ? 2 : 1;
                    i++;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static Vector2 Vector(string source, string name, List<string> failures)
        {
            var m = Regex.Match(source, name + @"\s*=\s*new Vector2\(([-0-9.]+)f,\s*([-0-9.]+)f\)");
            if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float y)) return new Vector2(x, y);
            failures.Add("could not parse independent layout value " + name);
            return Vector2.zero;
        }

        private static float Scalar(string source, string name, List<string> failures)
        {
            var m = Regex.Match(source, @"\b" + name + @"\s*=\s*([-0-9.]+)f?");
            if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value)) return value;
            failures.Add("could not parse independent layout scalar " + name);
            return 0f;
        }

        /// <summary>Top+bottom padding of a shelf row, parsed from BuildCardRow's RectOffset.</summary>
        private static float RowVerticalPadding(string store)
        {
            var m = Regex.Match(store, @"padding = new RectOffset\(\d+,\s*\d+,\s*(\d+),\s*(\d+)\)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int top) &&
                int.TryParse(m.Groups[2].Value, out int bottom)) return top + bottom;
            return 0f;   // absent padding cannot make the bound laxer than the raw card height
        }

        private static string Slice(string source, string start, string end, List<string> failures)
        {
            int a = source.IndexOf(start, StringComparison.Ordinal);
            int b = a >= 0 ? source.IndexOf(end, a + start.Length, StringComparison.Ordinal) : -1;
            if (a >= 0 && b > a) return source.Substring(a, b - a);
            failures.Add("could not isolate source block " + start);
            return string.Empty;
        }

        private static (Vector2 min, Vector2 max) AnchorsAfter(string source, string marker, List<string> failures)
        {
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            if (start >= 0)
            {
                var matches = Regex.Matches(source.Substring(start, Math.Min(800, source.Length - start)),
                    @"new Vector2\(([-0-9.]+)f,\s*([-0-9.]+)f\)");
                if (matches.Count >= 2)
                {
                    float Parse(Group g) => float.Parse(g.Value, System.Globalization.CultureInfo.InvariantCulture);
                    return (new Vector2(Parse(matches[0].Groups[1]), Parse(matches[0].Groups[2])),
                            new Vector2(Parse(matches[1].Groups[1]), Parse(matches[1].Groups[2])));
                }
            }
            failures.Add("could not derive anchored rect after " + marker);
            return (Vector2.zero, Vector2.zero);
        }

        private static void Require(string source, string marker, string failure, List<string> failures)
        {
            if (source.IndexOf(marker, StringComparison.Ordinal) < 0) failures.Add(failure);
        }
    }
}
