// =============================================================================
// InventoryUIBuilder — UI construction for the Bag ("The Armory Rail", WO-1133).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT THIS SCREEN IS FOR (WO-1133 D0, the one sentence everything justifies itself
// against): it tells you what you are carrying, what you are wearing, whether a thing
// is better than the thing it would replace - and where else you can go from here.
//
// THE SHAPE (D2/D3): a LEFT RAIL of sections replaces the top tab strip, a STAGE holds
// the selected section, and a PANE is always present for detail/compare. In landscape
// that is not a style preference: a vertical entry owns the full rail width so a
// selected-state mark can never eat its own label (the captured defect 2), each entry
// can carry its COUNT so the player sees what is worth opening BEFORE opening it, and
// the column fills the exact left band that was dead black (defect 1).
//
// WHAT WAS DELETED HERE AND MUST NOT COME BACK (D8 - roughly half this ticket is
// removal):
//   * The tab ROW (the old BuildTabs + RebuildTabsRow host). The rail is the navigation.
//   * The left hero CARD with its EMPTY preview box and its gold "VIEW GEAR" ribbon
//     (InventoryPaperDoll). That ribbon opened EquipmentPanel via PanelRouter - so the
//     owner was looking at a broken preview box sitting directly on top of a button that
//     opened the real one. The door was the defect, not the room: the route survives as
//     rail entry one, the box and the ribbon are gone.
//   * The full-width gold hint bar ("Tap an item to inspect it.") - the pane says
//     something useful instead of narrating the interface.
//
// ⛔ NO NEW 3D VIEWPORT IS INTRODUCED (D1). The only renderer is HeroPreviewViewer, the
// rig already proven at five other call sites - and it is mounted ONLY through the
// evidence gate below. See TryMountHeroPreview.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Hero;
using DeNelle.Village.Items;

namespace DeNelle.Village
{
    public sealed partial class HeroInventoryController : MonoBehaviour
    {
        // =====================================================================
        // D3 GEOMETRY — the authored numbers, in ONE place, so nothing is invented
        // at a call site and the regression has something to measure against.
        // ---------------------------------------------------------------------
        // The design states its zones as device px on a 2670x1200 canvas: rail 374,
        // stage 1496, pane 800 (14% / 56% / 30%). Those RATIOS are what survive a
        // resolution change, so they are what is authored here - applied across the
        // panel's interior x band. The three bands abut exactly and sum to the whole
        // interior; InventoryArmoryRailRegression asserts that against the design's
        // own ratios rather than recomputing them from these same constants.
        // =====================================================================

        /// <summary>Panel interior, left edge (fraction of the framed panel).</summary>
        private const float ZoneX0 = 0.035f;
        /// <summary>Panel interior, right edge.</summary>
        private const float ZoneX1 = 0.965f;
        /// <summary>Rail | Stage seam. ZoneX0 + 0.930 * (374/2670).</summary>
        private const float RailX1 = 0.035f;
        /// <summary>Stage | Pane seam. RailX1 + 0.930 * (1496/2670).</summary>
        private const float StageX1 = 0.620f;

        /// <summary>Header band (hero identity + vitals) - D3's full-width x 120 strip.</summary>
        private const float HeaderY0 = 0.885f, HeaderY1 = 0.985f;

        /// <summary>
        /// The STAGE and PANE floor. The kit seats the ONE shared Close as a fixed
        /// CanonCtaHeight box growing up from the default bottom-CENTRE band, so any
        /// full-width content below this line would be painted over. The rail escapes it
        /// horizontally (it is far left of the centred Close), which is why RailY0 is
        /// lower - exactly the dodge the previous layout's left column already used.
        /// </summary>
        private const float BodyY0 = 0.300f, BodyY1 = 0.750f;

        /// <summary>The rail column's band. Lower than the body because it clears the Close in X.</summary>
        private const float RailY0 = 0.760f, RailY1 = 0.875f;

        /// <summary>Purse strip - above the reserved Close band, below the stage.</summary>
        private const float PurseY0 = 0.222f, PurseY1 = 0.288f;

        /// <summary>Grid columns across the stage (D3).</summary>
        private const int GridColumns = 6;

        /// <summary>
        /// Rail entries, in order. Seven today; the count is a constant so the touch-floor
        /// arithmetic below and the regression both read the SAME number.
        /// </summary>
        private const int RailEntryCount = 8;

        /// <summary>Rail entry height, in canvas REFERENCE px, authored AT the kit touch floor.</summary>
        private const float RailEntryHeightPx = ElarionUiKit.MinTouchPx;

        /// <summary>Gap between rail entries, reference px.</summary>
        private const float RailEntryGapPx = 8f;

        // --- ROOT + CHROME ---------------------------------------------------
        private void BuildRoot()
        {
            // The kit's ONE standard modal canvas (1080x1920 reference / 0.5 match / 31000
            // band), same as every other Obsidian modal. overrideSorting is applied after,
            // preserving the prior behaviour.
            _ui = ElarionUiKit.BuildModalCanvas("HeroInventoryUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;

            ElarionUiKit.Scrim(_ui.transform, Close);

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header + the ONE
            // standard Close. D4 asked for the Close to move to a top-right button_exit; that is
            // NOT done here and the reason is recorded rather than silently ignored - the shared
            // Close is a kit-wide invariant ("every Close is the SAME pixel size on every screen",
            // owner F8 x3) that ~19 panels share, and re-seating it for one screen is a kit change,
            // not a Bag change. The layout instead RESPECTS the band: BodyY0/PurseY0 sit above it
            // and the rail clears it horizontally.
            var panelChrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "INVENTORY",
                new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f),
                Close, headerX0: 0.05f, headerX1: 0.80f,
                frameName: RpgUiCatalog.FrameInventory);
            var panel = panelChrome.content;

            // The frame's own medallion socket keeps the active hero's portrait. This is the
            // FRAME's socket, not the deleted hero card - leaving it empty would punch a hole in
            // the frame art. The slug comes from the persisted class through the two maps that
            // already exist (PlayableHeroes.JobKey -> PortraitSlug); deliberately not a third copy.
            var medallion = panelChrome.layout != null ? panelChrome.layout.medallion : null;
            if (medallion != null)
            {
                string portraitSlug = ActiveHeroPortraitSlug();
                // WO-1234: folder from the ONE constant (DeNelle.Core.HeroPortraitPaths).
                var portraitTex = Resources.Load<Texture2D>(DeNelle.Core.HeroPortraitPaths.ResourceKey(portraitSlug));
                if (portraitTex != null)
                {
                    var raw = new GameObject("HeroPortrait", typeof(RawImage));
                    var rrt = raw.GetComponent<RectTransform>();
                    rrt.SetParent(medallion, false);
                    rrt.anchorMin = Vector2.zero;
                    rrt.anchorMax = Vector2.one;
                    rrt.offsetMin = Vector2.zero;
                    rrt.offsetMax = Vector2.zero;
                    var rawImg = raw.GetComponent<RawImage>();
                    rawImg.texture = portraitTex;
                    rawImg.color = Color.white;   // preserveAspect=false: fill the oval
                    rawImg.raycastTarget = false;
                }
                else
                {
                    var nameLbl = ElarionUiKit.Label(medallion, portraitSlug.ToUpperInvariant(), 0f, 1f,
                        ElarionUi.Gilt, 40, TextAlignmentOptions.Center, 0f, 1f, bold: true);
                    nameLbl.raycastTarget = false;
                }
            }

            // ── HEADER: hero identity + vitals, one luminance-separated row (D3/D5). The old
            //    saturated green/blue/magenta bar stack on a left card is gone with the card.
            _headerRoot = AddImage(panel.transform, "HeaderBand",
                                   new Vector2(ZoneX0, HeaderY0), new Vector2(ZoneX1, HeaderY1),
                                   new Color(0f, 0f, 0f, 0f));
            NoRaycast(_headerRoot);

            // ── RAIL: the navigation. A carved niche column (D4: Well / panel_grid).
            _railRoot = AddImage(panel.transform, "InventoryTabs",
                                 new Vector2(ZoneX0, RailY0), new Vector2(ZoneX1, RailY1),
                                 new Color(0f, 0f, 0f, 0f));
            NoRaycast(_railRoot);

            // ── STAGE: the selected section.
            _stageRoot = AddImage(panel.transform, "Stage",
                                  new Vector2(ZoneX0, BodyY0), new Vector2(StageX1, BodyY1),
                                  new Color(0f, 0f, 0f, 0f));
            NoRaycast(_stageRoot);

            // ── PANE: detail / compare, ALWAYS present (D3). Never the old thin strip that
            //    could only say "Tap an item to inspect it."
            _paneRoot = AddImage(panel.transform, "Pane",
                                 new Vector2(StageX1, BodyY0), new Vector2(ZoneX1, BodyY1),
                                 new Color(0f, 0f, 0f, 0f));
            NoRaycast(_paneRoot);

            // ── PURSE STRIP: wallet + the next-step hint (D3).
            _purseRoot = AddImage(panel.transform, "PurseStrip",
                                  new Vector2(ZoneX0, PurseY0), new Vector2(ZoneX1, PurseY1),
                                  new Color(0f, 0f, 0f, 0f));
            NoRaycast(_purseRoot);

            // §12: the zone arithmetic, stated as numbers on every build. A capture that shows a
            // collapsed or overlapping band names the offending edge here instead of leaving the
            // next reader to measure a screenshot with a ruler.
            FlowTrace.Step("Inventory", string.Format(
                "Bag layout: rail x[{0:F3}..{1:F3}] stage x[{1:F3}..{2:F3}] pane x[{2:F3}..{3:F3}] " +
                "| header y[{4:F3}..{5:F3}] body y[{6:F3}..{7:F3}] rail y[{8:F3}..{9:F3}] purse y[{10:F3}..{11:F3}]",
                ZoneX0, RailX1, StageX1, ZoneX1,
                HeaderY0, HeaderY1, BodyY0, BodyY1, RailY0, RailY1, PurseY0, PurseY1));

            BuildPurseStrip(_purseRoot.transform);

            // The ONE shared open ease (kit PanelOpenCloseFx). Close stays instant.
            if (panelChrome.root != null)
                ElarionUiKit.AttachPanelOpenFx(_ui, panelChrome.root.GetComponent<RectTransform>());

            // Modal arbiter registration (shared _panelHandle with Open()/Close(); idempotent).
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("Inventory",
                    () => { if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle); Close(); },
                    () => IsOpen);
            PanelManager.NotifyOpened(_panelHandle);
        }

        // =====================================================================
        //  THE RAIL (D2) — sections down the left, each with its count.
        // =====================================================================
        //
        // ⚠ THE TOUCH FLOOR IS WHY THIS SCROLLS, AND THAT IS NOT A STYLE CHOICE.
        // D3 authors seven entries at 374x132 device px and asserts every target is above
        // MinTouchPx. At the design's own 2670x1200 the CanvasScaler (1080x1920, match 0.5)
        // resolves to ~965 reference px of canvas height, so the whole panel is ~907 ref px
        // and 132 device px is ~106 ref px - BELOW the 112 floor. Seven entries at the real
        // floor need 7*112 + 6*8 = 832 ref px, which is more height than the panel has once
        // the header exists at all. The numbers simply do not close.
        //
        // The two ways out are: let the entries sit sub-floor (D3 itself forbids relying on
        // ClampMinTouch - a sub-floor element inflates and stacks into its neighbour, the
        // 2026-07-16 grey-plate defect class), or author AT the floor and let the column
        // scroll. This takes the second. Every entry is exactly RailEntryHeightPx, so the
        // clamp is the no-op D3 asked for, and roughly five of the seven are visible at once
        // with the selected entry scrolled into view.
        private void BuildRail(Transform host)
        {
            if (host == null) return;
            BuildTopTabs(host);
        }

        // Kept temporarily as a non-runtime reference while the WO-1133 regression is
        // rewritten; BuildRail no longer calls this legacy presentation.
        private void BuildLegacyRail(Transform host)
        {

            // Caption: the column says what it is (D9 invRailHeader).
            var caption = ElarionUiKit.Label(host, InventoryStrings.Get(InventoryStrings.KeyRailHeader),
                0.945f, 1f, InkMicro, ElarionUi.FontMicro,
                TextAlignmentOptions.Center, 0.06f, 0.94f, spacing: 2f);
            caption.raycastTarget = false;
            ElarionUiKit.FitSingleLine(caption, 0f, ElarionUi.FontMicro);

            // Scroll host for the entries (see the touch-floor note above).
            var viewport = AddImage(host, "RailViewport",
                                    new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.935f),
                                    new Color(0f, 0f, 0f, 0f));
            viewport.AddComponent<RectMask2D>();
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var content = new GameObject("RailContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;
            scroll.content = crt;
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RailEntryGapPx;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The entries. Counts come from vm.Tabs (.Label and .Count BOTH already exist on
            // InventoryTab) - the rail is a projection, never a second source of truth.
            var counts = new Dictionary<InventoryTabKind, int>();
            if (_vm != null && _vm.Tabs != null)
                foreach (var t in _vm.Tabs) counts[t.Kind] = t.Count;

            bool mapOn = DeNelle.Core.FeatureFlags.MapTab;

            BuildRailEntry(content.transform, RailGear,     InventoryStrings.KeyRailGear,     -1, false);
            AddRailSeparator(content.transform);
            BuildRailEntry(content.transform, RailWeapons,  InventoryStrings.KeyRailWeapons,
                           CountOf(counts, InventoryTabKind.Weapons), false);
            BuildRailEntry(content.transform, RailOffHand, InventoryStrings.KeySlotOffHand,
                           CountOf(counts, InventoryTabKind.OffHand), false);
            BuildRailEntry(content.transform, RailArmor,    InventoryStrings.KeyRailArmor,
                           CountOf(counts, InventoryTabKind.Armor), false);
            BuildRailEntry(content.transform, RailTrinkets, InventoryStrings.KeyRailTrinkets,
                           CountOf(counts, InventoryTabKind.Outfits), false);
            BuildRailEntry(content.transform, RailPotions,  InventoryStrings.KeyRailPotions,
                           CountOf(counts, InventoryTabKind.Consumables), false);
            BuildRailEntry(content.transform, RailSkills,   InventoryStrings.KeyRailSkills,   -1, false);
            AddRailSeparator(content.transform);
            BuildRailEntry(content.transform, RailMap,      InventoryStrings.KeyRailMap,      -1, !mapOn);

            if (!mapOn)
            {
                // §12: the DORMANCY must be readable in a capture, or the next person hunts a dead
                // entry instead of finding the flag that dimmed it. D8 says render it dimmed and
                // inert rather than hiding it - "inert" is read here as "does not open the map",
                // not "cannot be selected", because the section still has an authored sentence
                // (invEmptyMapLocked) that exists precisely so the lock is never a surprise.
                FlowTrace.Step("UI",
                    "Bag rail: Map entry DORMANT (FeatureFlags.MapTab OFF - realm travel is a WO-827 " +
                    "stub). It is drawn dimmed with the 'soon' badge and selects to its locked copy; " +
                    "it does not route.");
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(crt);
        }

        private void BuildTopTabs(Transform host)
        {
            var counts = new Dictionary<InventoryTabKind, int>();
            if (_vm != null && _vm.Tabs != null)
                foreach (var t in _vm.Tabs) counts[t.Kind] = t.Count;
            string WithCount(string label, InventoryTabKind kind)
            {
                int n = CountOf(counts, kind);
                return n > 0 ? label + " " + n : label;
            }
            // ⚠ THE TAB ORDER IS THE RAIL ORDINAL ORDER, AND THAT IS LOAD-BEARING.
            // BuildTabRow hands its callback the tab INDEX, which is forwarded verbatim to
            // SelectRail — so labels[i] must be the section RailXxx == i names. Appending in
            // ordinal order (Gear 0 .. Potions 5, Skills 6, Map 7) is what keeps that true.
            // Insert nothing in the middle without renumbering the RailXxx constants.
            //
            // ⛔ SKILLS IS NOT OPTIONAL HERE — IT IS THE ONLY PLAYER-REACHABLE SKILL-TREE DOOR.
            // The other two openers of PanelId.HeroSkillTree are an ArcaneTower building
            // (BuildingInteractable.cs:491 — needs one PLACED, so a fresh save has none) and a
            // Yarn "OpenTalents" command (DialogueCommandSink.cs:106 — no live script calls it).
            // When this row was first authored as a scrolling rail it carried a Skills entry;
            // the 2026-08-30 rewrite to a tab row dropped it, and the hero talent tree became
            // unreachable in a normal town session while every talent-layer suite still passed.
            // HeroSkillTreeDoorRegression pins the label back to SelectRail -> OpenSkillTree.
            bool mapOn = DeNelle.Core.FeatureFlags.MapTab;
            var labelList = new System.Collections.Generic.List<string>
            {
                "Gear",
                WithCount("Weapons", InventoryTabKind.Weapons),
                WithCount("Off Hand", InventoryTabKind.OffHand),
                WithCount("Armor", InventoryTabKind.Armor),
                WithCount("Trinkets", InventoryTabKind.Outfits),
                WithCount("Potions", InventoryTabKind.Consumables),
                InventoryStrings.Get(InventoryStrings.KeyRailSkills)
            };
            // Map stays DORMANT behind FeatureFlags.MapTab (WO-827 realm travel is a stub, §7).
            // It is appended only when the flag is on so that flipping the flag restores a real
            // tab instead of leaving a second silently-orphaned door behind it.
            if (mapOn) labelList.Add(InventoryStrings.Get(InventoryStrings.KeyRailMap));

            ElarionUiKit.BuildTabRow(host, labelList.ToArray(), index => SelectRail(index),
                initial: Mathf.Clamp(_railIndex, RailGear, RailPotions));
            FlowTrace.Step("Inventory", "Bag tabs: " + labelList.Count +
                " visible, non-scrolling (Skills routes out to the hero skill tree; Map " +
                (mapOn ? "on" : "OFF - not drawn") + "); selected=" + _railIndex);
        }

        private static int CountOf(Dictionary<InventoryTabKind, int> counts, InventoryTabKind kind)
        {
            int n;
            return counts != null && counts.TryGetValue(kind, out n) ? n : 0;
        }

        /// <summary>One of D3's two rail separators — a thin rule that groups Gear / sections / Map.</summary>
        private void AddRailSeparator(Transform content)
        {
            var go = AddImage(content, "RailSeparator", Vector2.zero, Vector2.one,
                              new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.30f),
                              rounded: false);
            NoRaycast(go);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 3f;
            le.minHeight = 3f;
        }

        /// <summary>
        /// One rail entry: a kit button carrying its LABEL, its COUNT and (when it holds the
        /// worn item) the word "worn". D5: selection is a border plus a 3 px mark plus the pane
        /// changing - never a hue. Dormant entries carry the WORD "soon", not a grey tint alone.
        /// </summary>
        private void BuildRailEntry(Transform content, int railIndex, string labelKey,
                                    int count, bool dormant)
        {
            bool selected = _railIndex == railIndex;

            // The kit builds the button AND its label (never an empty label — a 0-glyph label
            // trips the TextFitGuard, owner F8 2026-07-10). The label is then RE-ANCHORED to the
            // entry's upper band so the count has its own row underneath, the same technique
            // BuildTabRow uses when it seats an icon beside a tab's label.
            var btn = ElarionUiKit.ButtonPack(content, InventoryStrings.Get(labelKey),
                ElarionUiKit.ButtonKind.Quiet,
                Vector2.zero, Vector2.one, () => SelectRail(railIndex));
            if (btn == null) return;
            btn.gameObject.name = "RailEntry_" + railIndex;

            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = RailEntryHeightPx;
            le.minHeight = RailEntryHeightPx;
            // Authored AT the floor, so this is the no-op D3 asked for. It stays because a future
            // resolution change must inflate the entry rather than ship a sub-floor tap target.
            ElarionUiKit.ClampMinTouch(btn);

            var root = btn.gameObject;

            // The label owns the full rail width, which is the whole point of a rail: no
            // selected-state mark can overlap it (the captured defect 2).
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                var lrt = lbl.rectTransform;
                lrt.anchorMin = new Vector2(0.16f, 0.42f);
                lrt.anchorMax = new Vector2(0.98f, 0.92f);
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                lbl.alignment = TextAlignmentOptions.MidlineLeft;
                lbl.color = selected ? GiltInk : (dormant ? InkDim : Ink);
                ElarionUiKit.FitSingleLine(lbl, 0f, ElarionUi.FontLabel);
            }

            // The COUNT is the reason to go, shown ahead of going (D2). A section with nothing
            // in it shows nothing rather than a "0" that reads as a broken counter.
            if (count > 0)
            {
                var cnt = ElarionUiKit.Label(root.transform, count.ToString(),
                    0.08f, 0.44f, InkMicro, ElarionUi.FontMicro,
                    TextAlignmentOptions.MidlineLeft, 0.16f, 0.98f);
                cnt.raycastTarget = false;
                ElarionUiKit.FitSingleLine(cnt, 0f, ElarionUi.FontMicro);
            }

            if (dormant)
            {
                var soon = ElarionUiKit.Label(root.transform,
                    InventoryStrings.Get(InventoryStrings.KeyRailMapSoon),
                    0.08f, 0.44f, InkMicro, ElarionUi.FontMicro,
                    TextAlignmentOptions.MidlineLeft, 0.16f, 0.98f);
                soon.raycastTarget = false;
                ElarionUiKit.FitSingleLine(soon, 0f, ElarionUi.FontMicro);
            }

            if (selected)
            {
                // SHAPE + POSITION, never colour alone (D5): a gold inner rim on the plate plus a
                // 3 px mark on the rail's inner edge. Both survive a greyscale pass.
                AddInnerRim(root, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.95f));
                var mark = AddImage(root.transform, "SelectedMark",
                                    new Vector2(0f, 0.10f), new Vector2(0.022f, 0.90f),
                                    ElarionUi.Gilt, rounded: false);
                NoRaycast(mark);
            }
        }

        // ── PURSE STRIP (D3) ─────────────────────────────────────────────────
        // The wallet, plus the screen's next-step hint. The hint REUSES the emptiest section's
        // own invEmpty* sentence (D9) so one string sits in two placements and the wording can
        // never drift between them.
        private void BuildPurseStrip(Transform host)
        {
            int coins    = _vm != null ? _vm.Coins : 0;
            int crystals = _vm != null ? _vm.Crystals : 0;

            // Left: the plain-words next step. This is what replaces the full-width gold hint bar
            // that shouted "Tap an item to inspect it." louder than the two items it described.
            _purseHint = ElarionUiKit.Label(host, "", 0.05f, 0.95f, InkDim, ElarionUi.FontMicro,
                TextAlignmentOptions.MidlineLeft, 0.01f, 0.56f);
            _purseHint.raycastTarget = false;
            ElarionUiKit.FitSingleLine(_purseHint, 0f, ElarionUi.FontMicro);

            // Right: the standard kit chip row (CurrencyChip owns ALL currency presentation —
            // CompactNumber, icon-first identity, no flash), never hand-rolled wells.
            var chipHost = AddImage(host, "WalletChips",
                                    new Vector2(0.58f, 0.05f), new Vector2(0.995f, 0.95f),
                                    new Color(0f, 0f, 0f, 0f));
            NoRaycast(chipHost);
            var softHost = AddImage(chipHost.transform, "SoftCurrency",
                                    new Vector2(0f, 0f), new Vector2(0.64f, 1f),
                                    new Color(0f, 0f, 0f, 0f));
            NoRaycast(softHost);
            var chips = ElarionUiKit.BuildWalletRow(softHost.transform,
                new[] { ElarionUiKit.CurrencyKind.Gold, ElarionUiKit.CurrencyKind.Crystal });
            if (chips != null && chips.Length > 0 && chips[0] != null) chips[0].SetAmount(coins, animate: false);
            if (chips != null && chips.Length > 1 && chips[1] != null) chips[1].SetAmount(crystals, animate: false);

            BuildGenericWalletChip(chipHost.transform);
        }

        /// <summary>Repaint the purse hint from the current section (called on every Render).</summary>
        private void RefreshPurseHint()
        {
            if (_purseHint == null) return;
            _purseHint.text = NextStepLine();
            ElarionUiKit.FitSingleLine(_purseHint, 0f, ElarionUi.FontMicro);
        }

        /// <summary>
        /// The pane footer / purse hint sentence — the screen always naming the next step in
        /// plain words (D2). Sourced from canon, never typed here.
        /// </summary>
        private string NextStepLine()
        {
            switch (_railIndex)
            {
                case RailGear:  return InventoryStrings.Get(InventoryStrings.KeyNextRailHint);
                case RailSkills: return InventoryStrings.Get(InventoryStrings.KeyEmptySkills);
                case RailMap:   return InventoryStrings.Get(InventoryStrings.KeyEmptyMapLocked);
            }
            // A content section: if it is empty, say what fills it; otherwise teach the rail.
            int count = SectionCount(RailTab(_railIndex));
            if (count <= 0) return InventoryStrings.EmptyLineFor(RailTab(_railIndex));
            bool compare = (_railIndex == RailWeapons || _railIndex == RailArmor)
                           && _vm != null && _vm.SelectedId != null
                           && SectionCount(RailTab(_railIndex)) > 1;
            return compare
                ? InventoryStrings.Get(InventoryStrings.KeyNextCompareHint)
                : InventoryStrings.Get(InventoryStrings.KeyNextCountHint);
        }

        /// <summary>Owned count in a section, read off vm.Tabs (the VM stays the source of truth).</summary>
        private int SectionCount(InventoryTabKind kind)
        {
            if (_vm == null || _vm.Tabs == null) return 0;
            foreach (var t in _vm.Tabs) if (t.Kind == kind) return t.Count;
            return 0;
        }

        // The premium/wallet chip — GENERIC under the V1 "wallet" skin (owner ruling appended to
        // WO-713). Built on the SAME kit CurrencyChip as the soft currencies (no hand-rolled well),
        // then re-iconed to the wallet/bag art: icon + plain amount, zero symbol glyphs.
        private void BuildGenericWalletChip(Transform host)
        {
            var skin = DeNelle.Core.Platform.CurrencySkinResolver.Active;
            string tagText = (string.IsNullOrEmpty(skin.CurrencyName) ? "Wallet" : skin.CurrencyName)
                             .ToUpperInvariant();
            var chip = ElarionUiKit.CurrencyChip(host, ElarionUiKit.CurrencyKind.Gold,
                new Vector2(0.66f, 0f), new Vector2(1f, 1f), primary: false, tag: tagText);
            if (chip == null || chip.root == null) return;
            chip.root.name = "WalletChip";

            var bag = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconInventory);
            if (bag != null && chip.icon != null)
            {
                chip.icon.sprite = bag;
                chip.icon.gameObject.SetActive(true);
                if (chip.tag != null) chip.tag.text = tagText;
            }
            else
            {
                // No wallet art: never let the GOLD kind icon mislabel the wallet — drop the icon
                // and make sure a text identifier carries the chip's identity instead.
                if (chip.icon != null) chip.icon.gameObject.SetActive(false);
                if (chip.tag != null) chip.tag.text = tagText;
                else
                {
                    var t = ElarionUiKit.Label(chip.root.transform, tagText, 0f, 1f,
                        ElarionUi.Parchment, ElarionUi.FontMicro,
                        TMPro.TextAlignmentOptions.MidlineLeft, 0.06f, 0.58f);
                    t.raycastTarget = false;
                    ElarionUiKit.FitSingleLine(t, 0f, ElarionUi.FontMicro);
                }
            }

            // V1 ships zero crypto and no local premium-balance model exists — the honest 0.
            chip.SetAmount(0, animate: false);
        }

        // ── WO-1015 E1: the in-panel "Orient" word-button is GONE from this screen ──────────
        // The seating editor keeps its ONE sanctioned entry point, AdminOverlay. Do not re-add
        // a per-screen launcher.

        // The "Map" section (WO-911): open the Realm Map parchment overworld. Routes through
        // PanelRouter so the inventory needs NO reference to RealmMapPanel. Reached only when
        // FeatureFlags.MapTab is ON — the dormant entry never calls this.
        private void OpenRealmMap()
        {
            if (DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.RealmMap))
                return;
            Close();
            FlowTrace.Warn("UI", "Map section: PanelId.RealmMap has no registered opener — nothing to open.");
        }

        // The "Skills" section: open the code-built MVVM skill tree (HeroSkillTreePanelMvvm).
        // Routes through PanelRouter so the inventory needs NO reference to the Talents panel type.
        private void OpenSkillTree()
        {
            if (DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.HeroSkillTree))
                return;
            Close();
            FlowTrace.Warn("UI", "Skills section: HeroSkillTree panel not registered (no hero?) — nothing to open.");
        }

        // ── THE PROMOTED GEAR ROUTE (D1) ─────────────────────────────────────
        // The full Character / Gear panel (EquipmentPanel — 1,452 lines of bound MVVM with
        // labelled per-slot drawers) already exists and this is the SAME PanelRouter call the
        // deleted "VIEW GEAR" ribbon made. It is now reached from the Gear section's own action
        // instead of from a ribbon painted across a broken preview box.
        //
        // ⚠ WHAT THAT PANEL SHOWS TODAY, on captured evidence, not inference: F8 seq 3585 caught
        // its RT PROBE reporting "the preview render texture is a UNIFORM clear colour - the
        // preview box is blank at the SOURCE". Its slot plates and its drawers work; its 3D hero
        // does not draw. So this route is deliberately NOT the Gear section's only content - the
        // worn-slot column in the stage answers "what am I wearing" from pure model data whether
        // or not the render is fixed.
        private void OpenGearPreview()
        {
            if (DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.EquipmentPanel))
                return;
            var panel = FindAnyObjectByType<DeNelle.Village.Hero.EquipmentPanel>();
            if (panel == null)
                panel = new GameObject("EquipmentPanelHost").AddComponent<DeNelle.Village.Hero.EquipmentPanel>();
            panel.Open();   // NotifyOpened closes this inventory (it is the registered open panel)
        }

        // ── Live 3D dressed-hero preview in the Gear section's niche ─────────────────
        // REUSE (no new system, D1): the SAME HeroPreviewViewer the Character / Gear screen
        // drives. The viewer is PERSISTED across rebuilds (the RT is reused and RefreshGear
        // mirrors equip changes) and disposed on Close (DisposeHeroPreview) so there is no
        // RenderTexture leak.
        private DeNelle.Village.Hero.HeroPreviewViewer _heroPreview;
        private RawImage _heroPreviewImage;

        /// <summary>
        /// Mount the live hero into <paramref name="parent"/> — but ONLY on evidence that it
        /// actually drew.
        ///
        /// ⛔ THIS GATE IS THE POINT OF THE TICKET. The preview camera clears to a colour
        /// byte-identical to the plate behind it, so "the rig drew a hero" and "the rig drew
        /// nothing" are the same pixels: a flat navy rectangle that reads as broken. That
        /// rectangle is what the owner photographed, and mounting unconditionally is how it
        /// shipped. HeroPreviewViewer.DrewContent runs the probe readback and answers the
        /// question, so a rig that drew nothing falls through to the 2D portrait instead of
        /// presenting an empty box. An honest portrait is strictly better than a plate the
        /// player has to guess about.
        ///
        /// Returns true only when a VERIFIED-NON-BLANK preview is showing.
        /// </summary>
        private bool TryMountHeroPreview(Transform parent)
        {
            if (parent == null) return false;
            try
            {
                var body = ResolvePreviewBody();
                if (body == null)
                {
                    FlowTrace.Step("Inventory", "Gear niche: no hero body — using 2D portrait fallback.");
                    return false;
                }

                string weaponId  = _loadout != null && _loadout.EquippedWeapon  != null ? _loadout.EquippedWeapon.id  : null;
                string offHandId = _loadout != null && _loadout.EquippedOffHand != null ? _loadout.EquippedOffHand.id : null;
                int    armorTier = _loadout != null ? GearLoadout.ArmorVisualTier(_loadout.EquippedArmor) : 0;

                if (_heroPreview == null)
                {
                    _heroPreview = new DeNelle.Village.Hero.HeroPreviewViewer();
                    if (!_heroPreview.Begin(body, textureSize: 512, weaponId: weaponId,
                                            offHandId: offHandId, armorTier: armorTier))
                    {
                        DisposeHeroPreview();
                        return false;
                    }
                    _heroPreview.SetRotation(18f);   // same 3/4 hero angle as the Gear screen
                }
                else
                {
                    // Persisted across rebuilds: mirror the latest equipped look, reusing the
                    // existing RenderTexture — no re-Begin, no RT churn.
                    _heroPreview.RefreshGear(weaponId, offHandId, armorTier);
                }

                if (!_heroPreview.IsValid || _heroPreview.Texture == null)
                {
                    DisposeHeroPreview();
                    return false;
                }

                // §12 — PROBE THIS CALL SITE. WO-1133 D1 required BOTH preview paths to be
                // probed, because until now only EquipmentPanel called ProbeRenderedContent
                // (tagging its lines "Equip"), so a blank capture could not be attributed to a
                // path. This one tags "Inventory". Two tags, two answers, no more guessing which
                // surface a probe line came from.
                string drewDetail;
                if (!_heroPreview.DrewContent(out drewDetail, "Inventory"))
                {
                    FlowTrace.Warn("Inventory",
                        "Gear niche: the preview rig drew NOTHING (" + drewDetail + ") — mounting the " +
                        "2D portrait instead. A flat plate here is the exact defect WO-1133 was raised " +
                        "for; the render itself is a separate CLI ticket (see the RT PROBE lines).");
                    return false;
                }

                var imgGo = new GameObject("HeroPreviewRawImage", typeof(RectTransform), typeof(RawImage));
                imgGo.transform.SetParent(parent, false);
                var rt = imgGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.05f, 0.05f);
                rt.anchorMax = new Vector2(0.95f, 0.95f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _heroPreviewImage = imgGo.GetComponent<RawImage>();
                _heroPreviewImage.raycastTarget = false;
                _heroPreviewImage.color = Color.white;
                _heroPreviewImage.texture = _heroPreview.Texture;
                return true;
            }
            catch (System.Exception ex)
            {
                // No silent failure (§12): log once, drop to the 2D portrait, never crash the modal.
                FlowTrace.Warn("Inventory",
                    $"TryMountHeroPreview threw ({ex.GetType().Name}: {ex.Message}) — using 2D portrait fallback.");
                DisposeHeroPreview();
                return false;
            }
        }

        // The actor body to clone for the preview — mirrors EquipmentPanel.ResolveBody: the live
        // hero's "HeroBody" child (the visual rig) or the tagged root itself, falling back to the
        // resolved loadout's GameObject. Null when no hero is present (caller skips the preview).
        private GameObject ResolvePreviewBody()
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) hero = SafeFindByTag("HeroTarget");
            if (hero != null)
            {
                var t = hero.transform.Find("HeroBody");
                if (t != null)
                {
                    var body = t.gameObject;
                    FlowTrace.Step("Preview",
                        $"ResolvePreviewBody: hero='{hero.name}' -> 'HeroBody' child '{body.name}' (children={body.transform.childCount})");
                    return body;
                }
                FlowTrace.Step("Preview",
                    $"ResolvePreviewBody: hero='{hero.name}' had NO 'HeroBody' child -> returning hero root '{hero.name}' (children={hero.transform.childCount})");
                return hero;
            }
            var fallback = _loadout != null ? _loadout.gameObject : null;
            FlowTrace.Step("Preview",
                $"ResolvePreviewBody: no 'Player'/'HeroTarget' hero found -> loadout fallback '{(fallback != null ? fallback.name : "NULL")}'" +
                (fallback != null ? $" (children={fallback.transform.childCount})" : ""));
            return fallback;
        }

        // Free the preview rig + its RenderTexture. Called on Close / OnDestroy (and on any build
        // failure) so the off-screen clone + RT never leak. Safe to call repeatedly.
        private void DisposeHeroPreview()
        {
            _heroPreview?.Dispose();
            _heroPreview = null;
            _heroPreviewImage = null;
        }

        // (Shared UI primitives Add*/Dress*/AddCircle*/Rarity*/glyphs/Has/Cap/Hero* live once in the
        // main partial file. High-level builder chrome (root/rail/purse) lives here.)
    }
}
