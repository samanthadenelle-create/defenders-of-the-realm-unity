// =============================================================================
// ManageMockupConformanceRegression - WO-1567 section 6 item 4.
// -----------------------------------------------------------------------------
// THE OWNER'S MOCKUP IS THE SPEC, AND ACCEPTANCE IS EXACT.
//   docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png
// CAPTURE_LOOP_GOAL.md 3.0c, owner verbatim: "When those screens match, you are
// done ... I gave you the mock up. This is your job." Where a text ruling and the
// mockup disagree, the mockup wins.
//
// ⛔ WHY A SOURCE ORACLE AND NOT A RENDER TEST, STATED RATHER THAN DRESSED UP.
// ManageWorkspacePanel and ManageScreenPanel build real UGUI hierarchies with a
// live canvas, TMP font assets and Resources sprites; an EditMode suite cannot
// stand one up headless. The PICTURE is judged by the capture loop
// (UI_CAPTURE_OK / MANAGE_FLOW_MAP_OK, frames opened and looked at). What THIS
// suite defends is the set of decisions the capture cannot see the reasoning
// behind - so that a later seat cannot silently restore a shape the owner's own
// device already proved wrong. Every case names the frame it was measured from.
//
// Each case carries a RED RECIPE: the exact edit that must make it fail.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace DeNelle.Editor
{
    /// <summary>Source oracle: the Manage screens against the owner's eight-panel mockup.</summary>
    public static class ManageMockupConformanceRegression
    {
        private const string PanelPath = "Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs";
        private const string VmPath = "Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs";
        private const string WorkspacePath = "Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs";
        private const string ProjectionPath = "Assets/_Modules/Core/Manage/ManageVmProjection.cs";
        private const string ContractPath = "Assets/_Modules/Core/Manage/ManageViewContract.cs";
        private const string CapturePath = "Assets/Editor/UICaptureLaunch.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            string panel = ReadOrFail(PanelPath, failures);
            string vm = ReadOrFail(VmPath, failures);
            string workspace = ReadOrFail(WorkspacePath, failures);
            string projection = ReadOrFail(ProjectionPath, failures);
            string contract = ReadOrFail(ContractPath, failures);
            string capture = ReadOrFail(CapturePath, failures);

            if (failures.Count == 0)
            {
                CheckFullScreen(panel, workspace, failures);
                CheckHub(panel, failures);
                CheckTiles(workspace, vm, projection, contract, failures);
                CheckDetail(workspace, vm, failures);
                CheckResearchPicker(vm, failures);
                CheckResearchTree(workspace, vm, projection, contract, failures);
                CheckChrome(panel, vm, failures);
                CheckCaptureFrame(capture, failures);
            }

            reason = failures.Count == 0
                ? "MANAGE_MOCKUP_OK 8 cases (full screen, hub, tiles, detail, research picker, " +
                  "research tree, chrome, capture frame)"
                : string.Join("\n", failures.ToArray());
            return failures.Count == 0;
        }

        private static string ReadOrFail(string path, List<string> failures)
        {
            if (File.Exists(path)) return File.ReadAllText(path);
            failures.Add("[manage-mockup] " + path + " is missing - this suite pins a file that no " +
                         "longer exists, which means the surface moved and nothing re-pointed the oracle.");
            return string.Empty;
        }

        private static bool Has(string body, string token)
        {
            return body.IndexOf(token, StringComparison.Ordinal) >= 0;
        }

        /// <summary>The body of one method, from its signature to the next member at method indent.</summary>
        private static string BodyOf(string file, string signature)
        {
            int at = file.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;
            int end = file.IndexOf("\n        private ", at + signature.Length, StringComparison.Ordinal);
            return end > at ? file.Substring(at, end - at) : file.Substring(at);
        }

        // ── AXIS 1 ON EVERY PANEL - THE SCREEN IS FULL ───────────────────────
        /// <summary>
        /// ⭐ THE PANEL FILLS THE SCREEN. Owner ruling 2026-09-07 01:14, verbatim:
        /// <i>"i expect these images to fill the screen, not 60% of it"</i>.
        /// <para>⛔ THIS IS THE FIRST AXIS OF EVERY OTHER COMPARISON, WHICH IS WHY IT RUNS FIRST.
        /// Every internal proportion on these screens - card size, cell size, well height, detail
        /// art - is a fraction of this rect. Judging any of them against the mockup while the panel
        /// is 64% wide compares two different pictures, and every device frame that night was taken
        /// through that 64% plate with the town visible around it.</para>
        /// <para>THE PIN IS >= 0.95 OF THE SAFE AREA ON BOTH AXES, measured off the inset constant
        /// rather than a literal at the call site - the constant is the one place the number lives.
        /// RED RECIPE: set ManagePanelInsetF to 0.18f, or restore
        /// <c>new Vector2(0.18f, 0.02f), new Vector2(0.82f, 0.98f)</c> at the BuildObsidianPanel call.</para>
        /// </summary>
        private static void CheckFullScreen(string panel, string workspace, List<string> failures)
        {
            if (Has(panel, "new Vector2(0.18f, 0.02f)") || Has(panel, "new Vector2(0.82f, 0.98f)"))
                failures.Add("[panel-fills-the-screen] the Manage modal is back to the retired 0.18-0.82 " +
                             "band - 64% of the canvas, with the town visible around it on every device " +
                             "frame of 2026-09-07. Owner ruling 01:14: \"i expect these images to fill the " +
                             "screen, not 60% of it\".");

            if (!Has(panel, "ManagePanelInsetF"))
            {
                failures.Add("[panel-fills-the-screen] ManagePanelInsetF is gone - the panel's edge inset is " +
                             "typed at its call site again, so nothing states the safe-area intent and " +
                             "nothing can check the 0.95 floor.");
                return;
            }

            // Read the constant's own value, so the floor is measured rather than assumed.
            float inset = ParseConstF(panel, "ManagePanelInsetF");
            if (float.IsNaN(inset))
                failures.Add("[panel-fills-the-screen] ManagePanelInsetF could not be read as a float " +
                             "literal - this case cannot measure the panel and will not pretend it passed.");
            else if (1f - 2f * inset < 0.95f)
                failures.Add("[panel-fills-the-screen] the panel spans " + (1f - 2f * inset).ToString("0.###") +
                             " of the safe area on each axis, under the 0.95 floor (inset " +
                             inset.ToString("0.###") + "). Owner ruling 2026-09-07 01:14.");

            // ⛔ AND THE GRID MUST ABSORB THE EXTRA WIDTH WITHOUT TURNING TILES INTO BARS.
            // This is the other half of the same ruling: the old fix for a wide band was to shrink
            // the MODAL. With the modal full-bleed, the cure lives at the cell.
            // RED RECIPE: delete the MaxTileAspect clamp from the grid builder.
            if (!Has(workspace, "MaxTileAspect"))
                failures.Add("[panel-fills-the-screen] the grid no longer clamps a cell's WIDTH to the " +
                             "mockup's tile aspect. With the panel full-bleed the band grew by roughly half, " +
                             "and the reclaimed width goes straight into the cells - round 4 already " +
                             "measured that outcome at 793x134 (5.9:1) and called them BARS. The surplus " +
                             "belongs in the side margins, which is what the mockup draws.");
            if (!Has(workspace, "TextAnchor.UpperCenter"))
                failures.Add("[panel-fills-the-screen] the grid packs its rows to the LEFT again, so the " +
                             "width the aspect clamp reclaims becomes one ragged black column on the right " +
                             "instead of an even margin either side.");
        }

        /// <summary>The float literal assigned to a named const, or NaN when it cannot be read.</summary>
        private static float ParseConstF(string file, string name)
        {
            int at = file.IndexOf(name + " = ", StringComparison.Ordinal);
            if (at < 0) return float.NaN;
            int start = at + name.Length + 3;
            int end = file.IndexOf(';', start);
            if (end <= start) return float.NaN;
            string raw = file.Substring(start, end - start).Trim().TrimEnd('f', 'F');
            float value;
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value) ? value : float.NaN;
        }

        // ── PANEL 1 - the hub ────────────────────────────────────────────────
        private static void CheckHub(string panel, List<string> failures)
        {
            // The three cards, in the mockup's order. Kept here as well as in
            // ManageProgressiveDisclosureRegression because the two suites defend different things:
            // that one pins that the SET is stable, this one pins the mockup's SHAPE around it.
            if (!Has(panel, "ManageTab.Buildings, ManageTab.Troops, ManageTab.Research"))
                failures.Add("[hub-three-cards] the hub's BUILD / ARMY / RESEARCH card array is gone or " +
                             "reordered. Mockup panel 1 draws exactly three, in that order.");

            // ⛔ THE CARD BAND IS DERIVED FROM PX, NOT TYPED.
            // RED RECIPE: put `grid.anchorMin = new Vector2(0.03f, 0.055f);` back.
            if (!Has(panel, "HubTitleBandPx") || !Has(panel, "HubCloseBandPx"))
                failures.Add("[hub-band-derived] the hub's card band no longer reserves the title and CLOSE " +
                             "bands in PX (HubTitleBandPx / HubCloseBandPx). It was two typed fractions " +
                             "(0.055f..0.695f) that reserved a different number of pixels on every surface " +
                             "height and said nothing about what they were reserving.");
            if (Has(panel, "grid.anchorMax = new Vector2(0.97f, 0.695f)"))
                failures.Add("[hub-band-derived] the typed 0.695f card band is back - the px derivation was " +
                             "reverted, not extended.");

            // ⛔ THE CARD KEEPS THE DRAWN PROPORTIONS.
            // RED RECIPE: `layout.cellSize = new Vector2(width / 3f, height);`
            if (!Has(panel, "HubCardAspect"))
                failures.Add("[hub-card-aspect] the hub cell no longer clamps to HubCardAspect. A third of " +
                             "the band is far wider than the drawn card is tall, so the cards read as wide " +
                             "plaques - which is exactly what the owner's device showed " +
                             "(Logs/device/screens/owner-screen-20260907-004724.png, cards about 2.2:1 " +
                             "against her 0.9:1) and why the descriptions had nowhere to wrap.");

            // ⛔ THE DESCRIPTION WRAPS. IT DOES NOT ELLIPSISE.
            // RED RECIPE: `ElarionUiKit.FitSingleLine(description, 24f, 30f);`
            if (!Has(panel, "ElarionUiKit.FitBlock(description"))
                failures.Add("[hub-description-untruncated] the hub card description is not fitted with " +
                             "FitBlock. On the owner's device all three cards ellipsised - \"Construct and " +
                             "upgrade yo...\", \"Train and manage your tr...\", \"Unlock powerful " +
                             "advance...\" - so the one sentence that says what the card DOES was " +
                             "unreadable on every card (owner-screen-20260907-004724.png).");

            // The framed, EMPTY art well, and the art ask that says why it is empty.
            if (!Has(panel, "BuildHubArtWell"))
                failures.Add("[hub-art-well] the hub cards no longer paint a framed art well. Mockup panel 1 " +
                             "gives each card a portrait illustration; the files do not exist, and a bordered " +
                             "empty well reads as art-pending where a black two thirds reads as broken.");
            if (!Has(panel, "hub-art-ask") || !Has(panel, "ManageArt.HubArtKeys"))
                failures.Add("[hub-art-ask] the hub no longer NAMES the three missing illustrations by " +
                             "Resources key. An art gap that is not named is an art gap nobody closes " +
                             "(CLAUDE.md section 12: never a silent fallback).");

            // ⛔ THE HEART DOOR STAYS ON THE HUB.
            // CAPTURE_LOOP_GOAL.md:130 gates removing the HEART chip on the Heart keeping a door
            // somewhere else. It has none: HeartSurfaceRegression:118-123 pins THIS face.
            // RED RECIPE: delete the BuildHeartFace() call from RenderLauncherCards.
            if (!Has(panel, "BuildHeartFace()"))
                failures.Add("[hub-keeps-the-heart-door] the hub no longer builds the Heart face. The mockup " +
                             "has no HEART chip, but CAPTURE_LOOP_GOAL.md:130 gates its removal on an " +
                             "unconditional Heart door existing ELSEWHERE and none does - " +
                             "HeartSurfaceRegression pins the hub face as the Heart's surface. Removing it " +
                             "ships the WO-1430 defect: a panel with no door.");
        }

        // ── PANEL 2 / 4 - the grid tiles ─────────────────────────────────────
        private static void CheckTiles(string workspace, string vm, string projection,
                                       string contract, List<string> failures)
        {
            string tile = BodyOf(workspace, "private void BuildTile(");
            if (tile == null)
            {
                failures.Add("[tile-square-portrait] ManageWorkspacePanel.BuildTile is gone - the grid tile " +
                             "renderer this case pins no longer exists.");
                return;
            }

            // ⛔ SQUARE, EDGE TO EDGE, NO MEDALLION RING.
            // RED RECIPE: `ElarionUiKit.Portrait(portZone, ManageArt.LoadSprite(tile.PortraitKey), active: false);`
            if (!Has(tile, "SquarePortrait("))
                failures.Add("[tile-square-portrait] BuildTile does not paint the portrait through " +
                             "SquarePortrait. Mockup panels 2 and 4 draw a SQUARE of art filling the tile " +
                             "with the name on a strip below it; the owner's device showed a small circular " +
                             "medallion floating in a black plate on every tile " +
                             "(owner-screen-20260907-004825.png BUILD, -005136.png ARMY).");
            if (Has(tile, "ElarionUiKit.Portrait("))
                failures.Add("[tile-square-portrait] BuildTile is calling ElarionUiKit.Portrait again - the " +
                             "circular disc and gilt ring are the hero/combat frame and the wrong shape for " +
                             "a grid tile. The ring's preserveAspect inset is also what made the art read " +
                             "small twice over.");
            if (!Has(workspace, "AspectRatioFitter.AspectMode.EnvelopeParent"))
                failures.Add("[tile-square-portrait] SquarePortrait no longer envelope-fits its art, so a " +
                             "non-square portrait letterboxes (black bars) or stretches. Both were observed: " +
                             "letterboxing is what made the retired landscape card strips read as broken, " +
                             "stretching is what the research picker was doing to them.");

            // ⛔ LOCKED TILES ARE DIMMED BY LUMINANCE, NEVER BY HUE.
            // RED RECIPE: pass `false` for SquarePortrait's dim argument in BuildTile.
            if (!Has(tile, "ManageTileVisualState.Locked"))
                failures.Add("[tile-locked-dim] BuildTile no longer dims a Locked tile. Mockup panel 4 draws " +
                             "locked troops darker than unlocked ones and the owner's ARMY capture shows all " +
                             "nine at full brightness (owner-screen-20260907-005136.png). It is a LUMINANCE " +
                             "multiply and never a hue change - the owner is red/green colourblind - and it " +
                             "is never the only cue: the word LOCKED and the padlock stay.");

            // ⛔ ONE CLOSED WORD ON A TILE.
            // RED RECIPE: `string tileState = tile.StateText;`
            if (!Has(tile, "tile.StateWord"))
                failures.Add("[tile-closed-state-word] BuildTile paints StateText rather than StateWord. " +
                             "WO-1518's amounts are right on the research row and the detail card and wrong " +
                             "in a grid cell: they measured as \"SHORT 28...\" on Crystal Mine and " +
                             "\"SHORT 72...\" on Healing Caravan (owner-screen-20260907-004825.png), naming " +
                             "neither a resource nor an amount. An ellipsised state word is the same defect " +
                             "class as no state word.");
            if (!Has(contract, "public string StateWord;"))
                failures.Add("[tile-closed-state-word] ManageTileVM.StateWord is gone - the contract no " +
                             "longer carries the tile's short face, so the View has nowhere to read it from " +
                             "except by truncating, which is the derivation canon 9 bans.");
            if (!Has(projection, "StateWord = string.IsNullOrEmpty(item.BadgeWord)"))
                failures.Add("[tile-closed-state-word] ManageVmProjection no longer projects BadgeWord onto " +
                             "StateWord (with BadgeText as the fallback), so a composer that authors a short " +
                             "face is ignored.");
            if (!Has(vm, "item.BadgeWord = ready ? \"READY\" : \"SHORT\""))
                failures.Add("[tile-closed-state-word] ApplyBuildBadge no longer composes the closed word " +
                             "beside the WO-1518 sentence. BOTH faces are the MODEL's; the View chooses " +
                             "between them and never shortens either.");
        }

        // ── PANELS 3 / 5 / 6 - the detail card ───────────────────────────────
        private static void CheckDetail(string workspace, string vm, List<string> failures)
        {
            string sel = BodyOf(workspace, "private void BuildSelection(");
            if (sel == null)
            {
                failures.Add("[detail-layout] ManageWorkspacePanel.BuildSelection is gone - the detail card " +
                             "this case pins no longer exists.");
                return;
            }

            // ⛔ NO DOT-JOINED FACTS. One fix, four measured frames.
            // RED RECIPE: `string descText = Join(sel.Description, sel.AuxiliaryText);`
            if (Has(sel, "Join(sel.Description") || Has(sel, "Join(sel.LevelText"))
                failures.Add("[detail-no-dot-joiner] the detail card is welding two facts together with " +
                             "Join's \"  .  \" again. Measured on the owner's device in one evening: " +
                             "\"Wood production +10%.  .  Wood production +18%.\" (-004903), " +
                             "\"Back-line ranged DPS. Fragile but hits hard.  .  L7 unlocks Thunderbolt\" " +
                             "(-005222), \"Fast flanker...  .  Requires Barracks Tier 4\" (-005311) and " +
                             "\"Level 5  .  UPGRADING\". Each pair is two facts of DIFFERENT kinds sharing " +
                             "one band; the mockup gives each its own line.");

            // The requirement gets a padlock row of its own (mockup panel 6).
            if (!Has(sel, "SelAuxLock"))
                failures.Add("[detail-requirement-row] the auxiliary/requirement line has lost its padlock " +
                             "row. Mockup panel 6 draws a padlock then \"Requires Barracks Tier 4\" on its " +
                             "own row; the owner is red/green colourblind so the padlock is the SHAPE " +
                             "channel for locked (CAPTURE_LOOP_GOAL 3c).");

            // ⛔ THE ART IS BIG AND SQUARE.
            // RED RECIPE: `ElarionUiKit.Portrait(portrait, ManageArt.LoadSprite(sel.PortraitKey), active: false);`
            if (!Has(sel, "SquarePortrait(portrait"))
                failures.Add("[detail-square-art] the detail card is not painting a square portrait. Mockup " +
                             "panels 3, 5 and 6 all draw a large SQUARE block of art in the left third; the " +
                             "owner's three captures all show a circular medallion inside a gilt ring.");

            // ⛔ THE CTA IS THE VERB ONLY.
            // RED RECIPE: `ElarionUiKit.Button(band, Join(face.Label, face.CostText), ...)`
            string row = BodyOf(workspace, "private void BuildActionRow(");
            if (row != null && Has(row, "Join(face.Label"))
                failures.Add("[detail-cta-verb-only] the CTA face is welding the cost onto the label again. " +
                             "Measured: \"UPGRADE  .  STONE 2600  GOL...\" (-004903), which ellipsised " +
                             "mid-word so neither the verb nor the price could be read, and " +
                             "\"TRAIN  .  1M 0S\" (-005222), which put a DURATION on a button as though it " +
                             "were a price. The mockup's buttons read \"UPGRADE\" and \"TRAIN 1 ARCHER\".");
            if (!Has(vm, "\"TRAIN 1 \""))
                failures.Add("[detail-cta-verb-only] the train face no longer names what it trains. Mockup " +
                             "panel 5 reads \"TRAIN 1 ARCHER\" - verb, count and name - on the one button " +
                             "that spends an army slot.");

            // ⛔ THE COST ROW NAMES THE RESOURCE IN WORDS.
            // RED RECIPE: delete the `if (named)` block from BuildCostRow.
            string cost = BodyOf(workspace, "private void BuildCostRow(");
            if (cost == null || !Has(cost, "c.Label"))
                failures.Add("[detail-cost-names-the-resource] BuildCostRow does not draw the cost's Label. " +
                             "The owner's Lumber Mill card read \"2600   970\" - two bare numbers naming no " +
                             "resource at all (owner-screen-20260907-004903.png) - because the row painted " +
                             "only a sprite and an amount, and ManageScreenVM.CostVms was setting IconKey " +
                             "to null so there was no sprite either. The WORD is the accessible channel: a " +
                             "small icon is exactly the kind of meaning the owner cannot separate by colour.");
            if (!Has(vm, "IconKey = CostIconFor(p.ConceptId)"))
                failures.Add("[detail-cost-names-the-resource] ManageScreenVM.CostVms is back to " +
                             "IconKey = null, so no cost row can paint a glyph however the renderer asks.");

            // ⛔ THE PROMOTED VALUE IS EMPHASISED BY WEIGHT.
            // RED RECIPE: drop `bold: promoted` from BuildStatRows' value label.
            string stats = BodyOf(workspace, "private void BuildStatRows(");
            if (stats == null || !Has(stats, "bold: promoted"))
                failures.Add("[detail-next-by-weight] the stats table no longer sets the promoted value BOLD. " +
                             "The mockup prints the new number in green; the owner is red/green colourblind, " +
                             "so WEIGHT plus the ASCII arrow is the channel and gold is only a redundant " +
                             "second one (CAPTURE_LOOP_GOAL 3c: meaning never carried by hue alone).");

            // The clock is its own line, not a cost row and not a button suffix.
            if (!Has(sel, "sel.TimeText"))
                failures.Add("[detail-clock-on-its-own] the detail card no longer paints TimeText. Mockup " +
                             "panels 3 and 5 draw the duration under the costs with a clock glyph. A " +
                             "duration has no bank and no affordability verdict, so it is never a cost row.");
        }

        // ── PANEL 7 - the research picker ────────────────────────────────────
        private static void CheckResearchPicker(string vm, List<string> failures)
        {
            // ⛔ ONE ROW WHILE THEY FIT.
            // RED RECIPE: `int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(schools)));`
            if (!Has(vm, "int columns = Mathf.Clamp(schools, 1, 5)"))
                failures.Add("[research-picker-one-row] ApplyPickerCapacity no longer lays the schools in one " +
                             "row while they fit. ceil(sqrt(4)) gives 2x2, and the owner's device showed four " +
                             "short wide tiles in the top 40% of the well with the rest black " +
                             "(owner-screen-20260907-005358.png) - the very defect ceil(sqrt) was introduced " +
                             "to cure, in a different shape. The mockup draws ONE row of square tiles.");
            if (!Has(vm, "IconId = ManageArt.BuildingPortraitKey(c.BuildingId, 1)"))
                failures.Add("[research-picker-portrait] the research school tiles no longer bind their " +
                             "portrait through ManageArt.BuildingPortraitKey. They read " +
                             "\"HudIcons/BuildingUpgrades/\" + IconName, which resolved the RETIRED 1963x789 " +
                             "landscape card strips - stretched into a tall cell behind an oval mask on the " +
                             "owner's device. One producer for every building key, off the catalog id.");

            // ⛔ WO-1564's OWN RED RECIPE STILL STANDS: a count-derived capacity beats an authored one.
            if (!Has(vm, "ApplyPickerCapacity"))
                failures.Add("[research-picker-one-row] ApplyPickerCapacity is gone - the picker's capacity " +
                             "is authored again, so an authored 4x1 against a 3-school town would once more " +
                             "leave a school orphaned on a ragged row (WO-1564).");
        }

        // ── PANEL 7 - the research TREE (WO-1567 section 6, panel row 8) ─────
        /// <summary>
        /// The tree the owner captured at Logs/device/screens/owner-screen-20260907-010151.png:
        /// four rows against black, no school painting, the requirement glued onto the benefit with
        /// a " . " and truncated, and a baked caption leaking out from under every medallion.
        /// </summary>
        private static void CheckResearchTree(string workspace, string vm, string projection,
                                              string contract, List<string> failures)
        {
            // ⛔ THE SCHOOL'S PAINTING IS ON THE LEFT AND THE ROWS TAKE THE REST.
            // RED RECIPE: delete the `BuildListPainting` call from BuildGrid.
            if (!Has(workspace, "BuildListPainting(band, tab.HeaderArtKey, bandW, bandH)"))
                failures.Add("[research-tree-painting] the list shape no longer carves the left of the well " +
                             "for the school's painting. Mockup panel 7 draws a large picture of the building " +
                             "beside its perk rows; the owner's frame has no picture on the screen at all.");
            if (!Has(contract, "public string HeaderArtKey"))
                failures.Add("[research-tree-painting] ManageTabVM carries no HeaderArtKey, so the MODEL cannot " +
                             "name the painting and the View would have to derive it from an id - the canon-9 " +
                             "derivation that produced the retired slug key producer (WO-1567 s5 i3).");
            if (!Has(vm, "tab.HeaderArtKey = string.IsNullOrEmpty(nav.SchoolId)"))
                failures.Add("[research-tree-painting] the perks screen no longer composes HeaderArtKey off " +
                             "ManageArt.BuildingPortraitKey. One key producer, off the catalog id.");

            // ⛔ THE BENEFIT AND THE REQUIREMENT ARE TWO ROWS, NEVER ONE JOINED LINE.
            // RED RECIPE: restore `item.NextRungLine + " . " + item.LockReason` in ComposeResearchItem.
            if (Has(vm, "item.NextRungLine + \" . \" + item.LockReason"))
                failures.Add("[research-tree-two-rows] ComposeResearchItem joins the benefit and the blocker " +
                             "into one line again. The owner's frame reads \"Wood +8%, offline bucket +8% . " +
                             "Upgrade the building to Tier 3 f...\" - a floating period and the half she needs " +
                             "in order to act ellipsised away. Two facts, two channels.");
            if (!Has(contract, "public string RequirementText"))
                failures.Add("[research-tree-two-rows] ManageTileVM has no RequirementText channel, so the " +
                             "requirement has nowhere to go but back onto the benefit line.");
            if (!Has(projection, "RequirementText = item.Ownership == ManageOwnership.NotUnlocked"))
                failures.Add("[research-tree-two-rows] the projection no longer carries LockReason onto " +
                             "RequirementText for a NotUnlocked item. Reading Ownership keeps this agreeing " +
                             "with the Wave-0 validator (ruling 15 forbids a lock sentence on an owned thing) " +
                             "instead of second-guessing it.");
            if (!Has(workspace, "RowRequirementLock"))
                failures.Add("[research-tree-two-rows] BuildListRow paints no padlock beside the requirement. " +
                             "The owner is red/green colourblind - the glyph is the SHAPE channel for locked, " +
                             "and the mockup draws it.");

            // ⛔ THE INLINE RESEARCH BUTTON CARRIES ITS COST BENEATH IT.
            // RED RECIPE: delete the `rowAction.CostText` label from BuildListRow.
            if (!Has(workspace, "ElarionUiKit.Label(row, rowAction.CostText"))
                failures.Add("[research-tree-inline-cost] the researchable row no longer prints its price under " +
                             "the RESEARCH button. Mockup panel 7 puts a gold RESEARCH face and \"800 / 400\" " +
                             "inside the row so the player can act without opening anything - a button with no " +
                             "price is a blind tap.");
            if (!Has(projection, "if (action.Availability != ManageActionAvailability.Available) return ManageActionVM.Hidden"))
                failures.Add("[research-tree-inline-cost] ProjectRowAction no longer withholds the inline face " +
                             "from a blocked action. A greyed button beside the padlock and the requirement " +
                             "would be a third telling of one fact; the mockup draws a padlock there.");

            // ⛔ THE BAKED CAPTION IS CROPPED OUT OF THE MEDALLION.
            // RED RECIPE: put `ElarionUiKit.Portrait(iconZone, ...)` back unconditionally.
            if (!Has(workspace, "ManageArt.IsCaptionedPerkIcon(tile.PortraitKey)") ||
                !Has(workspace, "CroppedIcon(iconZone"))
                failures.Add("[research-tree-caption-crop] the perk medallion paints the whole card again. " +
                             "MEASURED on Lumber_Mill_T1_Improved_Logging.jpg (786x1177): the framed picture " +
                             "runs y 155..800 from the top and everything under y~840 is the perk's NAME in " +
                             "gold - which the row already typesets two columns to the right. The owner's " +
                             "frame shows that baked caption half-cropped under every medallion.");
        }

        // ── The shared chrome (WO-1491) ──────────────────────────────────────
        private static void CheckChrome(string panel, string vm, List<string> failures)
        {
            // ⛔ THE BACK CONTROL IS AN ARROW SPRITE, NOT THE ASCII LITERAL.
            // RED RECIPE: delete the ApplyBackGlyph call from BuildBackArrow.
            if (!Has(panel, "ApplyBackGlyph(_workspaceBack)"))
                failures.Add("[chrome-back-glyph] the back control keeps its ASCII \"<-\" face. The owner's " +
                             "frame (owner-screen-20260907-010151.png) renders it as \"< -\", two glyphs " +
                             "kerned apart, where the mockup draws a plain arrow. ManageArt.IconBack was " +
                             "delivered with the art wave; the literal stays only as the miss fallback.");
            if (!Has(panel, "ManageArt.LoadSprite(ManageArt.IconBack)"))
                failures.Add("[chrome-back-glyph] the arrow is not loaded through ManageArt.LoadSprite, so a " +
                             "miss would not be announced by key and the button could render blank - a Manage " +
                             "screen with no visible way back is the WO-1443 defect.");

            // ⛔ CLOSE IS THE HUB'S ALONE.
            // RED RECIPE: delete the _chromeClose line from ApplyScreenVisibility.
            if (!Has(panel, "_chromeClose.gameObject.SetActive(_hubShowing)"))
                failures.Add("[chrome-close-on-hub-only] CLOSE is shown on every Manage screen again. The " +
                             "mockup draws it on panel 1 ONLY; panels 2 and 4-8 carry the back arrow, and the " +
                             "owner's walk found CLOSE on five screens that already have a way out. Two exits " +
                             "on one panel teach neither.");

            // ⛔ THE TITLE JOINS WITH A HYPHEN, AS DRAWN.
            // RED RECIPE: put `return "MANAGE / " + TabWordOf(nav.Tab);` back.
            if (!Has(vm, "private const string HeaderJoiner = \" - \""))
                failures.Add("[chrome-title-spelling] HeaderTitle no longer joins on one shared hyphen " +
                             "constant. The mockup heads panel 2 \"MANAGE - BUILD\"; the device build read " +
                             "\"MANAGE / BUILD\". A slash reads as a file path, a hyphen reads as a title.");
            // ⚠ THE TOKEN CARRIES ITS TRAILING `+` ON PURPOSE. A bare "MANAGE / " would also match
            // the prose in this file's siblings' comments, and a comment tripping a source oracle
            // has already cost this screen two rounds (ManageScreenVM's ComposeQueueTabs note
            // records both). With the concatenation operator it can only match CODE.
            if (Has(vm, "\"MANAGE / \" +"))
                failures.Add("[chrome-title-spelling] a \"MANAGE / \" literal is back in ManageScreenVM. Three " +
                             "typed separators is how the next re-spelling lands on two of them.");
        }

        // ── The capture plan ─────────────────────────────────────────────────
        private static void CheckCaptureFrame(string capture, List<string> failures)
        {
            // RED RECIPE: remove the Hub member from ManageFlowFrame.
            if (!Has(capture, "ManageFlowFrame.Hub"))
                failures.Add("[hub-capture-frame] the Manage flow plan no longer shoots the HUB. Mockup " +
                             "panel 1 is the screen Manage OPENS onto and it was the only screen in the flow " +
                             "with no frame. The retirement note in UICaptureLaunch stated this exact " +
                             "reversal condition: the hub was retired for being UNREACHABLE, \"not because a " +
                             "hub is wrong\", and WO-1443 restored ShowLauncher.");
            if (!Has(capture, "InvokePrivate(panel, \"ShowLauncher\")"))
                failures.Add("[hub-capture-frame] the hub frame is not raised through ShowLauncher. Entering " +
                             "a tab assigns a fresh nav entry and RenderWorkspace drops the hub, so an " +
                             "EnterTab path would photograph the GRID under a filename claiming the hub - " +
                             "the silent substitution this capture body refuses to make anywhere else.");
        }
    }
}
