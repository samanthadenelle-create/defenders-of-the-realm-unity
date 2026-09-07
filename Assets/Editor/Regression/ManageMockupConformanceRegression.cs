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
using UnityEngine;
using DeNelle.Core.UI;

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
                CheckMeasuredGeometry(panel, workspace, vm, failures);
            }

            reason = failures.Count == 0
                ? "MANAGE_MOCKUP_OK 9 cases (full screen, hub, tiles, detail, research picker, " +
                  "research tree, chrome, capture frame, measured geometry)"
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

        /// <summary>
        /// The value of a named const, or NaN when it cannot be read (every caller treats NaN as a
        /// FAILURE, never as a pass).
        /// <para>⭐ WO-1567 ROUND 26 - IT RESOLVES A CONST AUTHORED AS ANOTHER CONST, one hop, plus
        /// <c>ElarionUiKit.MinTouchPx</c>. Several of these bands are now written
        /// <c>= DrawerTitleOverlayPx;</c> or <c>= ElarionUiKit.MinTouchPx;</c> rather than as a
        /// copied literal, which is the right way round: a band whose only constraint is the touch
        /// floor should FOLLOW the floor, and two bands that must be the same number should be one
        /// number. Without this arm a literal-only parser would report "could not read" on exactly
        /// the authoring style CLAUDE.md's duplicated-state rule asks for.</para>
        /// </summary>
        private static float ParseConstF(string file, string name)
        {
            return ParseConstF(file, name, 0);
        }

        private static float ParseConstF(string file, string name, int depth)
        {
            if (depth > 3) return float.NaN;             // an alias cycle is a NaN, never a hang
            int at = file.IndexOf(name + " = ", StringComparison.Ordinal);
            if (at < 0) return float.NaN;
            int start = at + name.Length + 3;
            int end = file.IndexOf(';', start);
            if (end <= start) return float.NaN;
            string raw = file.Substring(start, end - start).Trim();
            float value;
            if (float.TryParse(raw.TrimEnd('f', 'F'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
                return value;
            if (string.Equals(raw, "ElarionUiKit.MinTouchPx", StringComparison.Ordinal))
                return ElarionUiKit.MinTouchPx;
            if (string.Equals(raw, "ElarionUi.FontFloorMobile", StringComparison.Ordinal))
                return ElarionUi.FontFloorMobile;
            // A bare identifier: one hop to the const it names.
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                bool ok = c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                          (i > 0 && c >= '0' && c <= '9');
                if (!ok) return float.NaN;
            }
            return raw.Length == 0 ? float.NaN : ParseConstF(file, raw, depth + 1);
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

        // =====================================================================
        //  MEASURED GEOMETRY - WO-1567 round 25
        // ---------------------------------------------------------------------
        //  ⛔ EVERY OTHER CASE IN THIS FILE IS A TOKEN TEST: it proves a DECISION is still in the
        //  source. This one does ARITHMETIC on the constants those decisions are made of, and
        //  fails on the NUMBER rather than on the spelling - because round 24 shipped with every
        //  token present and the frames still wrong. `MaxTileHeightPx` was in the file, correct,
        //  documented, and 190 of a 580px band is 33% of the well left black.
        //
        //  ⚠ THE REFERENCE SURFACE IS NAMED AND IT IS MEASURED, NOT INVENTED. Every number below
        //  resolves against ONE reference panel, derived from Builds/cap-manage-wave4.log:
        //    MANAGE_LIST_PAINTING ... in a 1835x580px well          -> the well was 580 x 1835
        //    MANAGE_QUEUE_BANDS drawer=458px                        -> 458 / 0.79 = 580, agreeing
        //  and the panel height that produces a 580px well under the OLD floor is arithmetic:
        //    580 = (WorkspaceHeaderY0 - CloseBandY0 - CloseGapY) * panelPx - CanonCtaHeight
        //        = (0.838 - 0.050 - 0.020) * panelPx - 132   ->   panelPx = 712 / 0.768 = 927
        //  ⛔ THIS IS A YARDSTICK, NOT A CLAIM ABOUT EVERY DEVICE. A ratio measured against one
        //  stated surface is a regression that cannot drift; a ratio measured against "whatever
        //  the device gives" is not measurable in an EditMode suite at all. The RATIOS are what is
        //  pinned. The frames remain the picture's judge (this file's own header says so).
        // =====================================================================

        /// <summary>The reference panel height in kit reference px - see the block above for the
        /// arithmetic that derives it from the captured 580px well.</summary>
        private const float RefPanelPx = 927f;
        /// <summary>The reference well WIDTH, read off `MANAGE_LIST_PAINTING ... 1835x580px`.</summary>
        private const float RefWellWidthPx = 1835f;

        private static void CheckMeasuredGeometry(string panel, string workspace, string vm,
                                                  List<string> failures)
        {
            // ── ROUND 26, from Builds/cap-manage-wave5.log and its frames ────────────────
            // (a) The tree's benefit line wraps. RED RECIPE: FitSingleLine(effect, 18f, 26f).
            if (!Has(workspace, "ElarionUiKit.FitBlock(effect, ElarionUiKit.FontHardFloor, 26f)"))
                failures.Add("[research-tree-benefit-wraps] the tree row's benefit line is fitted as a " +
                             "SINGLE line again. Measured on the round-25 frame: \"Unlocks the Healing " +
                             "Fountain - restores the Hear...\" - ellipsised on the perk whose benefit is " +
                             "the longest, which is the perk a player most needs the sentence for. FitBlock " +
                             "wraps and truncates visibly; FitSingleLine substitutes three dots that look " +
                             "deliberate.");
            if (!Has(workspace, "effectPx < 2f * ElarionUiKit.FontHardFloor"))
                failures.Add("[research-tree-benefit-wraps] the effect band is judged against the ONE-line " +
                             "cull floor while it typesets two. A band that holds one-and-a-bit lines then " +
                             "passes the check and truncates on screen.");

            // (b) A grid that fits is centred - and the VIEWPORT has to be the rows that EXIST.
            // RED RECIPE: `float viewportPx = overflowStrip ? rowsPx : bandH;`
            if (!Has(workspace, "int contentRows = Mathf.Max(1, Mathf.CeilToInt(tiles.Count / (float)columns));") ||
                !Has(workspace, "float viewportPx = overflowStrip ? rowsPx : Mathf.Min(bandH, seatedPx);"))
                failures.Add("[research-picker-one-row] the grid viewport falls back to the whole BAND again, " +
                             "so `bandH - viewportPx` is 0 and the centring branch can never fire. That is " +
                             "why round 25's centring changed nothing: the research picker's one row still " +
                             "sat at the top of a full-height viewport with the well dead beneath it. The " +
                             "viewport must be the rows that EXIST, not the rows that FIT.");

            // (c) The tile art shows the WHOLE building. RED RECIPE: put the portrait zone back to
            // `new Vector2(0f, TilePortY0), new Vector2(1f, 1f)`, or drop the width-fit arm.
            if (!Has(workspace, "var portZone = Zone(cell, \"TilePortrait\", Vector2.zero, Vector2.one);"))
                failures.Add("[tile-art-whole-building] the tile's portrait zone is inset from the cell " +
                             "again. On a square 359px BUILD cell a 0.26..1 zone is 359x266, and a square " +
                             "sprite envelope-fitted into it is cropped by 93px split top and bottom - " +
                             "Archer Tower, Ballista and Cathedral of Magic all lost their ROOFS. The name " +
                             "strip and the state word carry their own dark plates and are painted later, " +
                             "so they ride ON the art exactly as mockup panel 2 draws them.");
            if (!Has(workspace, "AspectRatioFitter.AspectMode.WidthControlsHeight") ||
                !Has(workspace, "bool widthToBottom = zoneWpx > 1f && zoneHpx > 1f && (zoneWpx / aspect) <= zoneHpx + 0.5f;"))
                failures.Add("[tile-art-whole-building] SquarePortrait no longer fits by WIDTH and anchors to " +
                             "the BOTTOM when the whole subject fits. A building sits on the ground, so any " +
                             "surplus belongs ABOVE it - an envelope crop splits the loss around it and takes " +
                             "the roof. ⛔ The fallback to the envelope crop is part of the rule, not a " +
                             "safety net: ARMY's cell is 2.3:1, where a width fit would be 566px tall in a " +
                             "246px mask and would cut the troops' heads.");
            if (!Has(workspace, "SquarePortrait(portZone, tile.PortraitKey,") ||
                !Has(workspace, "tile.VisualState == ManageTileVisualState.Locked, cellW, cellH);"))
                failures.Add("[tile-art-whole-building] the tile no longer hands SquarePortrait its cell's " +
                             "px, so the fit mode is back to a guess about the cell's shape. The rect itself " +
                             "is 0 on the frame the tile is built - it cannot be read back.");

            // (d) Every Builder queue row carries a thumbnail identity.
            // RED RECIPE: `PortraitKey = building != null ? ManageArt.BuildingPortraitKey(buildingId, 1) : ""`.
            if (!Has(vm, "PortraitKey = !string.IsNullOrEmpty(portraitId)") ||
                !Has(vm, "portraitId = catalogId;"))
                failures.Add("[queue-row-thumbnail] the queue row's thumbnail key is guarded on " +
                             "`building != null` again. A TOWER is not in BuildingTierCatalog - it resolves " +
                             "its NAME through CatalogRegistry - so `building` is null for it and every " +
                             "tower and wall row, the most common Builder job there is, asked for no art at " +
                             "all. MEASURED: on every *_queue frame row 1 (\"Archer Tower - Level 2\") has " +
                             "no icon while rows 2 and 4 do, and NOTHING logged, because an empty key never " +
                             "reaches ManageArt for it to announce a miss. The label and the thumbnail are " +
                             "the same lookup and must be resolved by the same branches.");
            if (!Has(vm, "queue-thumb-miss:"))
                failures.Add("[queue-row-thumbnail] a Builder row that resolves no portrait identity is " +
                             "silent again. That silence is why the missing icon survived a whole capture " +
                             "round with a clean log.");

            // ---- the well, after the CLOSE-band reclaim ----------------------------------
            float headerY0 = ParseConstF(panel, "WorkspaceHeaderY0");
            float bodyFloor = ParseConstF(panel, "WorkspaceBodyFloorY");
            float closeY0 = ParseConstF(panel, "CloseBandY0");
            float closeGap = ParseConstF(panel, "CloseGapY");
            if (float.IsNaN(headerY0) || float.IsNaN(bodyFloor) || float.IsNaN(closeY0) || float.IsNaN(closeGap))
            {
                failures.Add("[measured-geometry] the Manage well's band constants could not be read " +
                             "(WorkspaceHeaderY0 / WorkspaceBodyFloorY / CloseBandY0 / CloseGapY). This " +
                             "suite measures RATIOS off them; it will not guess one.");
                return;
            }

            // ⛔ THE RECLAIM ITSELF. RED RECIPE: put `bodyFloor = closeBandTop + CloseGapY` back.
            if (!Has(panel, "float bodyFloor = WorkspaceBodyFloorY;"))
                failures.Add("[well-reclaims-the-close-band] the body well reserves the shared CLOSE band " +
                             "again. CLOSE is rendered on the HUB ALONE (WO-1491, and this file's own " +
                             "[chrome-close-on-hub-only] case pins it), so on BUILD / ARMY / RESEARCH / " +
                             "every detail screen / the queue overlay that reservation is dead space. It " +
                             "cost the well ~150 ref px, which is the 5x2 grid's second row of tiles and " +
                             "three of the queue's five rows.");
            if (!Has(panel, "_hubCloseReservePx"))
                failures.Add("[well-reclaims-the-close-band] the HUB no longer re-takes the reclaimed band " +
                             "inside its own host. The one screen that DOES draw CLOSE must still clear it, " +
                             "and it must do so from the MEASURED reclaim - not from a second typed constant " +
                             "that goes stale the first time CanonCtaHeight moves.");

            float wellPx = (headerY0 - bodyFloor) * RefPanelPx;
            float closeReservePx = Mathf.Max(
                ParseConstF(panel, "HubCloseBandPx"),
                (closeY0 + ElarionUiKit.CanonCtaHeight / RefPanelPx + closeGap - bodyFloor) * RefPanelPx);

            // ---- PANEL 1: the hub cards FILL the well -------------------------------------
            // MEASURED DEFECT: ManageFlow_BUILD_hub_2670x1200.png - three ~245x270 plates centred in
            // an otherwise empty full-bleed well, every description cut mid-word.
            float hubGap = ParseConstF(panel, "HubBandGapPx");
            float heartBandPx = ElarionUiKit.MinTouchPx;
            if (!Has(panel, "private const float HubTitleBandPx = ElarionUiKit.MinTouchPx;"))
                failures.Add("[hub-heart-band-at-the-floor] the hub's top band is not authored at " +
                             "ElarionUiKit.MinTouchPx. It carries the HEART chip, which resolved " +
                             "440.5x75.4 ref px on Builds/cap-manage-wave4.log - 36.6px UNDER the floor - " +
                             "and that one seat produced SEVEN oracle failures: the sub-touch band, three " +
                             "BUTTONS OVERLAP and three BUTTON OVER TEXT, each naming ManageHeartFace " +
                             "against a ManageCard_*.");
            if (!Has(panel, "new Vector2(0.02f, _hubHeartY0), new Vector2(0.24f, 1f), OpenHeartSurface"))
                failures.Add("[hub-heart-band-at-the-floor] the HEART chip is not seated in the hub's " +
                             "header band off the MEASURED _hubHeartY0. Its old band (0.70-0.83) was typed " +
                             "against a card band that had since become derived, so it sat INSIDE all " +
                             "three cards - two tap targets in one place, and only one wins the raycast.");
            if (float.IsNaN(hubGap) || hubGap <= 0f)
                failures.Add("[hub-heart-band-at-the-floor] HubBandGapPx is gone or zero. It is the ONLY " +
                             "thing that keeps the heart band and the card band disjoint by construction; " +
                             "without it they share an edge and the overlap oracle fires again.");

            float hubCardBandPx = wellPx - (closeReservePx + hubGap) - (heartBandPx + hubGap);
            float hubFill = hubCardBandPx / Mathf.Max(1f, wellPx);
            if (hubFill < 0.50f)
                failures.Add("[hub-cards-fill-the-well] the hub's card band is " +
                             hubCardBandPx.ToString("0") + "px of a " + wellPx.ToString("0") +
                             "px well = " + hubFill.ToString("0.##") + " - under the 0.50 floor. Mockup " +
                             "panel 1 draws three TALL cards filling the well between the title and CLOSE; " +
                             "the reservations above and below them have eaten it.");

            // The card's SHAPE, which is what made them read as small plaques. cellW is clamped to
            // HubCardAspect x the band height and the row centres in what is left (BuildLauncher).
            float sideInset = ParseConstF(panel, "HubSideInsetF");
            float aspect = ParseConstExpr(panel, "HubCardAspect");
            if (!float.IsNaN(sideInset) && !float.IsNaN(aspect))
            {
                // 14px padding a side and 24px between the three cards - BuildLauncher's own layout.
                float gridW = (1f - 2f * sideInset) * RefWellWidthPx - 28f - 48f;
                float cellW = Mathf.Min(gridW / 3f, hubCardBandPx * aspect);
                float drawn = cellW / Mathf.Max(1f, hubCardBandPx);
                if (Mathf.Abs(drawn - aspect) > 0.02f)
                    failures.Add("[hub-cards-fill-the-well] the hub card resolves " + cellW.ToString("0") +
                                 "x" + hubCardBandPx.ToString("0") + "px = " + drawn.ToString("0.##") +
                                 ":1 against the mockup's " + aspect.ToString("0.##") +
                                 ":1. The owner measured her device frame at about 2.2:1 and named it - " +
                                 "a card that wide has nowhere to wrap its description.");
            }

            // The description band, in px, against the floor FitBlock cannot go under.
            float descF = ParseConstF(panel, "HubDescBandF");
            if (float.IsNaN(descF))
                failures.Add("[hub-description-untruncated] HubDescBandF is gone - the description's band " +
                             "is back to a fraction typed at its call site, which is how it ended up too " +
                             "short to seat two lines without saying so.");
            else if (hubCardBandPx * descF < 2f * ElarionUi.FontFloorMobile)
                failures.Add("[hub-description-untruncated] the hub card's description band is " +
                             (hubCardBandPx * descF).ToString("0") + "px, under the " +
                             (2f * ElarionUi.FontFloorMobile).ToString("0") + "px two lines at " +
                             "ElarionUi.FontFloorMobile need. FitBlock TRUNCATES rather than going " +
                             "sub-legible, which is 'upgrade your to' on " +
                             "ManageFlow_BUILD_hub_2670x1200.png and three ellipses on the owner's device.");

            // ---- PANELS 2 and 4: the tiles FILL the grid band ------------------------------
            float tileGap = ParseConstF(workspace, "TileGapPx");
            float tileCap = ParseConstF(workspace, "MaxTileHeightPx");
            if (float.IsNaN(tileGap) || float.IsNaN(tileCap))
            {
                failures.Add("[grid-tiles-fill-the-band] TileGapPx / MaxTileHeightPx could not be read.");
                return;
            }
            if (!Has(workspace, "float cellCeiling = asRowShape ? MaxTileHeightPx : Mathf.Max(MaxTileHeightPx, cellW);"))
                failures.Add("[grid-tiles-fill-the-band] the cell's height ceiling is back to the absolute " +
                             "MaxTileHeightPx on a multi-row grid. cellH is ALREADY (bandH - gaps) / rows, " +
                             "so the authored rows fit by construction and a second absolute ceiling can " +
                             "only make them smaller than the band they were divided out of - which is the " +
                             "top-half grid on ManageFlow_BUILD_gridtop_2670x1200.png.");
            if (!Has(workspace, "RowFitEpsilon"))
                failures.Add("[grid-tiles-fill-the-band] the whole-row floor has lost its epsilon. `cell` is " +
                             "derived from `bandH` by the very division this floor inverts, so the ratio is " +
                             "exactly `rows` in real arithmetic and lands on 2.9999997 as readily as on " +
                             "3.0000002 in float - an ARMY grid whose three rows fit would seat two and " +
                             "draw a '+3 MORE' strip over a band that had the room.");

            CheckGridFill(5, 2, tileGap, tileCap, wellPx, failures);
            CheckGridFill(3, 3, tileGap, tileCap, wellPx, failures);

            // The research PICKER is one row of five: it can only be square, so the surplus is
            // SPLIT rather than left as a slab (mockup panel 6 centres it).
            if (!Has(workspace, "bool centreInBand = hidden <= 0 && bandH - viewportPx > 1f;"))
                failures.Add("[research-picker-one-row] a grid that fits its band is top-anchored again, so " +
                             "the research picker's surplus goes back into one dead slab under the tiles - " +
                             "the 'dead well beneath' the owner named on owner-screen-20260907-005358.png. " +
                             "A scrolling grid must still start at its first row.");

            // ---- PANEL 7: the tree rows take the band the painting left ---------------------
            float paintX1 = ParseConstF(workspace, "ListPaintingX1");
            float paintGap = ParseConstF(workspace, "ListPaintingGapF");
            if (!float.IsNaN(paintX1) && !float.IsNaN(paintGap))
            {
                float rowFrac = 1f - paintX1 - paintGap;
                if (rowFrac < 0.55f)
                    failures.Add("[research-tree-rows-take-the-band] the tree's rows get " +
                                 rowFrac.ToString("0.##") + " of the well beside the painting - under the " +
                                 "0.55 floor. Mockup panel 7 gives the picture the smaller share.");
            }
            if (!Has(workspace, "float cellWCap = asRowShape ? float.MaxValue : cellH * MaxTileAspect;"))
                failures.Add("[research-tree-rows-take-the-band] the MaxTileAspect WIDTH clamp is being " +
                             "applied to the LIST shape again. MEASURED on Builds/cap-manage-wave4.log: " +
                             "'grid cell width clamped from 1064px to 316px (2.3:1 against a 137px cell)' - " +
                             "the rows were handed the right band and thrown 70% of it away one line later, " +
                             "which is 'Arcane Bas...', 'RESE...' and 'QUEU...' on " +
                             "ManageFlow_RESEARCH_school_2670x1200.png. columns == 1 is a full-width ROW, " +
                             "not a narrow grid - the same exemption the HEIGHT ceiling already carries.");
            if (!Has(workspace, "float textX0 = iconX1 + 0.02f;"))
                failures.Add("[research-tree-rows-take-the-band] the list row's text column is back to a " +
                             "typed origin while its icon zone is derived. The two collide the first time a " +
                             "row shape widens the icon.");

            // ---- PANELS 3, 5, 6: the detail art is NOT a square zone ------------------------
            if (Has(workspace, "float artFrac = Mathf.Min(0.42f, (cardH * 0.92f) / Mathf.Max(1f, cardW));"))
                failures.Add("[detail-art-crops-the-ring] the detail card's art zone is pinned SQUARE again. " +
                             "The ring is BAKED INTO THE ART (Assets/Resources/RpgUi/troop/troop-footman.png " +
                             "is a 1254x1254 gilt medallion on transparency), and SquarePortrait's " +
                             "EnvelopeParent fit crops NOTHING when a square sprite meets a square zone - so " +
                             "the medallion survives on the detail card (ManageFlow_ARMY_max_2670x1200.png) " +
                             "while the SAME art through the SAME method reads clean in the grid's 2.3:1 " +
                             "tiles (ManageFlow_ARMY_gridtop_2670x1200.png). The zone's aspect is the whole " +
                             "mechanism.");
            if (!Has(workspace, "new Vector2(0.015f, 0.02f), new Vector2(0.015f + artFrac, 0.98f)"))
                failures.Add("[detail-art-crops-the-ring] the detail portrait no longer spans the card's " +
                             "full height. Mockup panels 3, 5 and 6 all draw a block of art down the left " +
                             "side floor to ceiling, and the full-height zone is what makes the crop happen.");

            // ---- PANEL 8: the queue overlay -------------------------------------------------
            CheckQueueBudget(panel, wellPx, failures);
        }

        /// <summary>
        /// One authored grid shape, resolved exactly as <c>BuildGrid</c> resolves it, and judged on
        /// the fraction of the band its rows actually cover.
        /// <para>RED RECIPE: restore the absolute <c>MaxTileHeightPx</c> ceiling - 190px x 2 rows in
        /// a 758px band is 0.51 and this fails at once.</para>
        /// </summary>
        private static void CheckGridFill(int columns, int rows, float tileGap, float tileCap,
                                          float bandH, List<string> failures)
        {
            float bandW = RefWellWidthPx;
            float cellW = (bandW - (columns - 1) * tileGap) / columns;
            float cellH = (bandH - (rows - 1) * tileGap) / rows;
            // MaxTileAspect clamp, then the ceiling - the same order as the renderer.
            float cellWCap = cellH * 2.3f;
            if (cellW > cellWCap) cellW = cellWCap;
            cellH = Mathf.Min(cellH, Mathf.Max(tileCap, cellW));
            float rowsPx = rows * cellH + (rows - 1) * tileGap;
            float fill = rowsPx / Mathf.Max(1f, bandH);
            if (fill < 0.95f)
                failures.Add("[grid-tiles-fill-the-band] a " + columns + "x" + rows + " grid resolves a " +
                             cellW.ToString("0") + "x" + cellH.ToString("0") + "px cell, so its rows cover " +
                             rowsPx.ToString("0") + "px of a " + bandH.ToString("0") + "px band = " +
                             fill.ToString("0.##") + ", under the 0.95 floor. The owner's criterion zero is " +
                             "that the picture FILLS the screen; tiles in the top half of a full-bleed " +
                             "panel fail it however correct every element inside them is.");
        }

        /// <summary>
        /// The queue overlay's row budget, resolved exactly as <c>SetDrawerBands</c> and
        /// <c>SeatQueueListToWholeRows</c> resolve it, and judged against mockup panel 8's five rows.
        /// <para>⛔ NO ROW IS EVER SHRUNK UNDER THE TOUCH FLOOR TO REACH FIVE, and this case does not
        /// let one be: the row height is clamped into [MinTouchPx, RowHeightPx] here exactly as the
        /// renderer clamps it, so the only way to pass is to give the LIST more band.</para>
        /// </summary>
        private static void CheckQueueBudget(string panel, float wellPx, List<string> failures)
        {
            float y0 = ParseConstF(panel, "DrawerOverlayY0");
            float y1 = ParseConstF(panel, "DrawerOverlayY1");
            float titlePx = ParseConstF(panel, "DrawerTitlePx");
            float tabsPx = ParseConstF(panel, "DrawerTabsPx");
            float gapPx = ParseConstF(panel, "DrawerBandGapPx");
            float rowCap = ParseConstF(panel, "RowHeightPx");
            float target = ParseConstF(panel, "QueueRowsVisibleTarget");
            if (float.IsNaN(y0) || float.IsNaN(y1) || float.IsNaN(titlePx) || float.IsNaN(tabsPx) ||
                float.IsNaN(gapPx) || float.IsNaN(rowCap) || float.IsNaN(target))
            {
                failures.Add("[queue-rows-visible] the queue overlay's band constants could not be read.");
                return;
            }

            // ⛔ THE FLAT PLATE IS PART OF THE BUDGET, NOT A STYLE CHOICE. The 9-sliced content-panel
            // frame declares a 96px border, and ResolveDrawerBands correctly bounds the rows inside
            // it - 192px of a 458px overlay, the single biggest item in the arithmetic.
            if (Has(panel, "drawerImage.sprite = Resources.Load<Sprite>(\"UI/ElarionMedieval/frames/content-panel\")"))
                failures.Add("[queue-rows-visible] the queue overlay is painting the 9-sliced content-panel " +
                             "frame again. MEASURED on Builds/cap-manage-wave4.log: 'MANAGE_QUEUE_PLATE " +
                             "sprite=content-panel inset=96px' and 'list=206px' - 192px of a 458px overlay " +
                             "spent on frame art, which is three of the five rows. Mockup panel 8 draws a " +
                             "plain dark rectangle with a THIN gold outline; GoldPerimeter is that outline " +
                             "and it costs the rows nothing.");
            float plateInset = Has(panel, "drawerImage.sprite = null;") ? 0f : 96f;

            float drawerPx = (y1 - y0) * wellPx;
            float listPx = drawerPx - 2f * plateInset - titlePx - tabsPx - 2f * gapPx;

            // MakeScrollZone(spacing: 8f, padding: 10) - BuildQueueDrawer's own call.
            const float Spacing = 8f, Padding = 20f;
            float ideal = (listPx - Padding - (target - 1f) * Spacing) / target;
            float rowPx = Mathf.Clamp(ideal, ElarionUiKit.MinTouchPx, rowCap);
            // The same epsilon the grid's whole-row floor carries, for the same reason: rowPx is
            // DERIVED from listPx by the division this floor inverts, so the ratio sits exactly on
            // an integer and float decides which side of it by one ULP.
            int whole = Mathf.FloorToInt((listPx - Padding + Spacing) / (rowPx + Spacing) + 0.001f);

            if (rowPx < ElarionUiKit.MinTouchPx)
                failures.Add("[queue-rows-visible] the derived queue row is " + rowPx.ToString("0") +
                             "px, under ElarionUiKit.MinTouchPx. Rows are never shrunk under the floor to " +
                             "manufacture a row count - the WELL has to grow.");
            // ⛔ THE FLOOR IS FOUR, NOT FIVE, AND THE DIFFERENCE IS ARITHMETIC RATHER THAN A
            // PREFERENCE — SO IT IS WRITTEN DOWN HERE INSTEAD OF ASSERTED AWAY.
            // Mockup panel 8 draws FIVE rows. Five need
            //   5 x MinTouchPx(112) + 4 x spacing(8) + padding(20) = 612px of list,
            // and the body well is 758px against 256px of the mockup's OWN chrome — title 112
            // (it holds the word QUEUE and a MinTouchPx X), tabs 128, gaps 16 — leaving 502px.
            // ⛔ THE 112 IS NOT NEGOTIABLE AND NEITHER IS THE ROW. Round 25 reached five by drawing
            // the header ABOVE the drawer, and Builds/cap-manage-wave5.log failed it six times:
            // `TEXT OFF PLATE ... overflows its layout.body ZoneBacking by 112 ref px`. The band
            // had to come back inside, and it came out of the list.
            // So this case pins what the screen can HONESTLY seat — four whole rows, none clipped,
            // none under the touch floor — and the runtime WARN in SeatQueueListToWholeRows names
            // the fifth in px on every render. **A well taller than 870px is what buys it back.**
            // ⚠ RAISING THIS BACK TO FIVE WITHOUT GROWING THE WELL CAN ONLY BE DONE BY SHRINKING A
            // ROW UNDER MinTouchPx, WHICH THE CASE ABOVE FAILS. That is deliberate.
            const int SeatableRows = 4;
            if (whole < SeatableRows)
                failures.Add("[queue-rows-visible] the queue overlay seats " + whole + " whole row(s), under " +
                             "the " + SeatableRows + " the body well can honestly hold (mockup panel 8 draws " +
                             (int)target + "). Budget at the reference surface: well " +
                             wellPx.ToString("0") + "px -> drawer " + drawerPx.ToString("0") + "px - plate " +
                             (2f * plateInset).ToString("0") + " - title " + titlePx.ToString("0") +
                             " - tabs " + tabsPx.ToString("0") + " - gaps " + (2f * gapPx).ToString("0") +
                             " = list " + listPx.ToString("0") + "px; " + (int)target + " rows need " +
                             ((int)target * ElarionUiKit.MinTouchPx + ((int)target - 1) * Spacing + Padding)
                                 .ToString("0") + "px. The captured run seated ONE.");

            // ⛔ AND THE IN-ROW CONTROLS CLEAR THE FLOOR AT EVERY ROW HEIGHT THE CLAMP CAN PRODUCE.
            // This is the other forty failures on that log, and it is pure arithmetic:
            // 0.88 x RowHeightPx(132) = 116 (fine) but 0.88 x MinTouchPx(112) = 98.6 (the number on
            // every ObsBtn_SPEED UP / CANCEL / Move up line).
            if (!Has(panel, "private float QueueCtrlY0"))
                failures.Add("[queue-controls-clear-the-touch-floor] the queue row's control band is back to " +
                             "the fixed RowCtrlY0..Y1 fraction. That fraction reasons from RowHeightPx (132), " +
                             "but WO-1488 made the row a MEASUREMENT clamped to [MinTouchPx, RowHeightPx] - " +
                             "so at the floor it resolves 0.88 x 112 = 98.6 ref px, 13.4px under. That one " +
                             "line is FORTY of the forty-four geometry and forty-seven touch failures on " +
                             "Builds/cap-manage-wave4.log.");
            float ctrlSpan = ParseConstF(panel, "RowCtrlY1") - ParseConstF(panel, "RowCtrlY0");
            float[] probes = { ElarionUiKit.MinTouchPx, rowCap, (ElarionUiKit.MinTouchPx + rowCap) * 0.5f };
            for (int i = 0; i < probes.Length; i++)
            {
                float row = probes[i];
                float want = Mathf.Max(ElarionUiKit.MinTouchPx, row * ctrlSpan);
                float resolved = want >= row ? row : want;
                if (resolved < ElarionUiKit.MinTouchPx - 0.01f)
                    failures.Add("[queue-controls-clear-the-touch-floor] a " + row.ToString("0") +
                                 "px queue row resolves a " + resolved.ToString("0.#") +
                                 "px control - under ElarionUiKit.MinTouchPx (" +
                                 ElarionUiKit.MinTouchPx.ToString("0") + "). ClampMinTouch would grow it " +
                                 "SYMMETRICALLY about its centre and spill it into both neighbours; the " +
                                 "band must be authored AT the floor.");
            }
        }

        /// <summary>
        /// <see cref="ParseConstF"/> for a const whose literal is a DIVISION, e.g.
        /// <c>145f / 160f</c>. Returns NaN when it is not one, so a caller can skip rather than
        /// invent a value.
        /// </summary>
        private static float ParseConstExpr(string file, string name)
        {
            int at = file.IndexOf(name + " = ", StringComparison.Ordinal);
            if (at < 0) return float.NaN;
            int start = at + name.Length + 3;
            int end = file.IndexOf(';', start);
            if (end <= start) return float.NaN;
            string raw = file.Substring(start, end - start).Trim();
            int slash = raw.IndexOf('/');
            if (slash < 0) return ParseConstF(file, name);
            float a, b;
            if (!float.TryParse(raw.Substring(0, slash).Trim().TrimEnd('f', 'F'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out a)) return float.NaN;
            if (!float.TryParse(raw.Substring(slash + 1).Trim().TrimEnd('f', 'F'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out b)) return float.NaN;
            return Mathf.Abs(b) < 0.0001f ? float.NaN : a / b;
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
