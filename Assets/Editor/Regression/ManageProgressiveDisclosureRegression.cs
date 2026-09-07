using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeNelle.Core.Jobs;      // ObsidianQueueState - GameState.ObsidianQueue's type (WO-1516 fixture)
using DeNelle.Core.Manage;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.UI;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Source oracle for the phone Manage progressive-disclosure contract.</summary>
    public static class ManageProgressiveDisclosureRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string vm = File.ReadAllText("Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs");
            string panel = File.ReadAllText("Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs");

            // [build-grid-is-unlocked-only] WO-1516 - the MEASURED half, run first so its fixture
            // teardown happens before the source scans below.
            CheckBuildGridIsUnlockedOnly(failures);

            // ⭐ [grid-tile-states-its-state] WO-1563, the RENDERER half. The model half is
            // asserted inside the fixture above; this is the binding that was missing. BuildTile
            // referenced tile.StateText EXACTLY ZERO TIMES while the sibling renderer BuildListRow
            // painted it - the same screen family answering "what can I act on?" two opposite ways.
            // ⚠ A source scan by necessity: ManageWorkspacePanel builds real UGUI objects, which
            // this editor-only suite cannot stand up headless. Stated rather than dressed up.
            // RED RECIPE: delete the LAYER 7 block from ManageWorkspacePanel.BuildTile.
            string workspacePanel = File.ReadAllText("Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs");
            int tileAt = workspacePanel.IndexOf("private void BuildTile(", StringComparison.Ordinal);
            if (tileAt < 0)
                failures.Add("[grid-tile-states-its-state] ManageWorkspacePanel.BuildTile is gone - the grid tile " +
                             "renderer this case pins no longer exists.");
            else
            {
                int tileEnd = workspacePanel.IndexOf("\n        private ", tileAt + 20, StringComparison.Ordinal);
                string body = tileEnd > tileAt ? workspacePanel.Substring(tileAt, tileEnd - tileAt)
                                               : workspacePanel.Substring(tileAt);
                if (body.IndexOf("tile.StateText", StringComparison.Ordinal) < 0)
                    failures.Add("[grid-tile-states-its-state] BuildTile does not reference tile.StateText - the " +
                                 "grid renderer is discarding the state word the model composes, and with the " +
                                 "WO-1516 medallion withheld the tile carries neither glyph nor word. The owner " +
                                 "is red/green colourblind; the word IS the accessible channel (WO-1563).");

                // ⭐ NO ELLIPSIS ON A STATE WORD. MEASURED in
                // Builds/ui-capture/ManageFlow_BUILD_gridtop_2670x1200.png: four tiles read
                // "QUEUE FU...". An ellipsised state word is the same defect as no word -
                // "UPGRADE AVAILABLE" and "UPGRADING" both truncate to "UPGRADI...".
                // RED RECIPE: fit the label with the literal 26f ceiling again, i.e.
                //   ElarionUiKit.FitSingleLine(stateWord, ElarionUiKit.FontHardFloor, 26f);
                if (body.IndexOf("stateFontPx", StringComparison.Ordinal) < 0)
                    failures.Add("[grid-tile-states-its-state] BuildTile fits the state word without the " +
                                 "grid-wide stateFontPx ceiling - each label is fitted on its own again, so the " +
                                 "LONGEST word ellipsises while a short one beside it paints full size. That is " +
                                 "the measured QUEUE FU... defect.");
                if (workspacePanel.IndexOf("ResolveStateWordFont", StringComparison.Ordinal) < 0)
                    failures.Add("[grid-tile-states-its-state] ResolveStateWordFont is gone - nothing derives the " +
                                 "state word's size from the longest word on the grid.");
                // ⛔ DERIVED FROM THE MODEL'S OWN TILES, never from a vocabulary copied into the
                // View. A copied word list is duplicated state and goes stale the first time a
                // composer authors a new badge word.
                if (workspacePanel.IndexOf("GetPreferredValues", StringComparison.Ordinal) < 0)
                    failures.Add("[grid-tile-states-its-state] the state word's size is no longer MEASURED with " +
                                 "TMP GetPreferredValues - a character-advance ratio or any other estimate is a " +
                                 "guess, and CLAUDE.md section 12 forbids shipping one where a measurement exists.");
            }

            if (!vm.Contains("CountPlacedThisTown()") || !vm.Contains("BuildVisibleTabs()"))
                failures.Add("categories are not derived from authoritative current-town placements");
            // ⚠ PIN MOVED 2026-09-06 (WO-1443), WITH THE RULING - four cards became THREE.
            // The owner drew the screen herself: docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png
            // panel 1 is "MANAGE (MAIN) - Simple entry with three core options", BUILD / ARMY /
            // RESEARCH, and CAPTURE_LOOP_GOAL.md 3.0c item 2 states that it supersedes WO-2001's
            // launcher retirement for that screen. Where a text ruling and the mockup disagree, the
            // mockup wins - it is the picture of the thing she wants.
            // ⛔ AND DEFENSE COULD NOT SURVIVE AS A CARD: WO-2001 merged it into BUILD, and
            // ManageScreenPanel.ShowOperational (:1189-1191) maps Defense and Buildings alike onto
            // ManageTabId.Build. A fourth card would open the same destination as the first.
            // WHAT THIS CASE DEFENDS IS UNCHANGED: a STABLE, ORDERED set of launcher cards, so the
            // hub cannot silently re-order or lose one. It now pins the three that exist, and fails
            // if the retired four-card array returns.
            if (!panel.Contains("ManageTab.Buildings, ManageTab.Troops, ManageTab.Research"))
                failures.Add("the stable three-card Manage hub is missing or reordered " +
                             "(mockup panel 1: BUILD / ARMY / RESEARCH)");
            if (panel.Contains("ManageTab.Defense, ManageTab.Buildings, ManageTab.Troops, ManageTab.Research"))
                failures.Add("the retired FOUR-card launcher is back - Defense and Buildings are one " +
                             "destination since WO-2001, so a Defense card opens the Build tab");
            if (!panel.Contains("BarracksUnlock.IsUnlocked") ||
                !panel.Contains("Build a Barracks to unlock") ||
                !panel.Contains("ActivateLauncherCard"))
                failures.Add("locked Troops card is not sourced from BarracksUnlock with explicit feedback");
            int rebuild = panel.IndexOf("_vm.Rebuild();", StringComparison.Ordinal);
            int launcher = panel.IndexOf("RenderLauncherCards();", rebuild, StringComparison.Ordinal);
            if (rebuild < 0 || launcher < rebuild)
                failures.Add("launcher cards are not rendered after the VM populates availability");
            // ⭐ RE-POINTED 2026-09-04 (lead), NOT relaxed - the WO-1159 precedent: a ruling moved,
            // so the pin moves with it and gets STRICTER about the thing the ruling actually meant.
            //
            // THE RULING (F8 2026-08-31) IS "upgrade browsing LEADS; queue administration is OPT-IN".
            // The old check enforced it by banning the string `AddSectionHeader("IN QUEUE - "`
            // ANYWHERE in the panel. That was a fair proxy while the queue had no home at all - but
            // WO-1368 gave the queue verbs a home INSIDE the opt-in drawer, which is precisely what
            // the ruling asks for, and the global ban failed it. A header inside the drawer does not
            // put queue history in the browse catalogue; it labels the opt-in surface.
            //
            // ⛔ So the ban is SCOPED TO RenderList's BODY - the browse catalogue - exactly as its
            // sibling ManageQueueDrawerRegression was re-pointed in the same wave. Banning the string
            // globally would now forbid the fix for a P1 money-path defect (WO-1368: the Finish Now
            // and Ad verbs had NO build site for three days and shipped in the production candidate).
            int upgrade = panel.IndexOf("UPGRADABLE TOWERS", StringComparison.Ordinal);
            int rlStart = panel.IndexOf("private void RenderList(", StringComparison.Ordinal);
            int rlEnd   = rlStart >= 0 ? panel.IndexOf("        private ", rlStart + 24, StringComparison.Ordinal) : -1;
            string renderListBody = (rlStart >= 0 && rlEnd > rlStart) ? panel.Substring(rlStart, rlEnd - rlStart) : "";
            if (rlStart < 0)
                failures.Add("RenderList not found - the browse-leads pin cannot be scoped, so it cannot be trusted");
            // ⭐ RE-POINTED 2026-09-06 (WO-1422 ruling 3.4), SPLIT from the compound OR above it.
            // The queue-isolation half is untouched and keeps its own message; the half that used
            // to require the string "UPGRADABLE TOWERS" moved, because that heading DIES WITH THE
            // PAGED PATH. It was also a LIE: BuildDefenseBrowse admits wall_wood, mine_crystal,
            // healing_caravan and the three storage containers, none of which is a tower (ruling
            // 3.2). What the ruling actually meant - "the Defense tab is a real destination, not an
            // empty list" - is now pinned as the DESTINATION METHOD, which is stronger: a heading
            // string can survive with nothing behind it.
            // RED PROOF: delete RenderDefenseDestination -> first check fires. Restore the
            // "UPGRADABLE TOWERS - affordable first" section header -> second check fires.
            if (!panel.Contains("BuildQueueDrawer(well)") ||
                renderListBody.Contains("AddSectionHeader(\"IN QUEUE - \""))
                failures.Add("upgrade browsing does not lead cleanly with queue history isolated in the opt-in drawer");
            if (!panel.Contains("private void RenderDefenseDestination("))
                failures.Add("[defense-destination] Manage's DEFENSE tab has no RenderDefenseDestination - the tab " +
                             "opens onto nothing, which reads to the player exactly like the feature not existing");
            if (upgrade >= 0)
                failures.Add("[defense-destination] the retired paged heading \"UPGRADABLE TOWERS\" is back in the " +
                             "panel. It went out with the paged list (WO-1422 ruling 3.4) and it never told the truth " +
                             "anyway - the Defense tab also lists walls, a crystal mine, a healing caravan and three " +
                             "storage containers (ruling 3.2)");
            // ⭐ RE-POINTED 2026-09-06 (WO-1422 ruling 3.4): the ASSERTION IS INVERTED.
            // This case used to REQUIRE the pager, and its message argued FOR it - "overflow has no
            // visible count and bidirectional paging affordance". Defense and Research were the last
            // two readers of AddBrowseRow; with both on the WO-1418 workspace, the pager block
            // (Panel:1710-1724), AddBrowseRow (:2940) and BuildBrowseRowContent (:2946-2993) are
            // DELETED. Keeping them would be dead code under a green pin - the exact failure
            // ManageQueueDrawerRegression:103-113 was written to catch for AddQueueRow ("a private
            // method with zero callers is dead code that LOOKS like a shipped feature").
            // RED PROOF: restore any one of the three pager strings to ManageScreenPanel.cs.
            if (panel.Contains("Showing \" + (first + 1)") ||
                panel.Contains("Previous page") || panel.Contains("Next page"))
                failures.Add("[pager-retired] the paged Manage list is back (a \"Showing n-m\" sentence or a " +
                             "Previous/Next page control). All four Manage tabs render the SAME workspace - portrait " +
                             "rail, one selected card, a NOW band, one footer row - and nothing pages (WO-1422 " +
                             "ruling 3.4)");
            if (!panel.Contains("private void BuildResearchCard(") ||
                !panel.Contains("private void BuildDefenseCard("))
                failures.Add("[pager-retired] the pager is gone but the workspace cards that replaced it " +
                             "(BuildDefenseCard / BuildResearchCard) are missing too - the ban above would pass on a " +
                             "panel that simply lost both destinations");
            if (!panel.Contains("Need another town structure?") ||
                !panel.Contains("\"Open build\", OpenTownBuilder") ||
                !panel.Contains("EnterBuildMode(DeNelle.Core.Catalog.BuildType.Town)"))
                failures.Add("absent building categories have no real secondary Town-build route");

            // [research-locked-visible] ADDED 2026-09-04 (WO-1390, owner on the Seeker, build 355905:
            // "under manage research it shows nothing, should it show Tier one and show locked with a
            // link to upgrade the prerequisite"). Device log: `research browse ... 6 with a tier
            // ladder -> 0 perk row(s)`. The cause was ONE line in BuildResearchBrowse -
            // `if (!can) continue;` - which dropped every tier-locked perk and discarded the
            // CanResearch reason as `_`. The Manage rule the Troops tab already follows ("Build a
            // Barracks to unlock" + BuildLockBadge) is now the Research rule too: a locked perk is a
            // LOCKED row whose StateText is the CanResearch reason verbatim and whose CTA is the
            // DOOR to the prerequisite (OpenUpgradePanel -> PanelId.BuildingUpgrade, the one existing
            // start path), never a dead button.
            //
            // This suite is a SOURCE oracle (no VM fixture), so the pin is scoped to the method body.
            //
            // ⭐ RE-POINTED 2026-09-06 (WO-1422 ruling 3.4). The VM half below is UNCHANGED:
            // BuildResearchBrowse and its Rebuild() call site are deliberately KEPT (the proven
            // Troops precedent - BuildTroopsBrowse survived WO-1382 and three suites still drive
            // BrowseRows), so nothing here moved. What moved is the PANEL half: the lock treatment
            // used to live in BuildBrowseRowContent, which WO-1422 DELETES along with the pager and
            // AddBrowseRow. The player-facing treatment now lives on the Research CARD, so the pin
            // follows it to BuildResearchCard - the WO-1159 precedent, and stricter, because the
            // card must also PAINT the reason, not merely know the row is locked.
            //
            // RED PROOF: restore `if (!can) continue;` in place of the locked-row block (the exact
            // pre-WO-1390 text) -> "Research drops locked perks" fires; drop the `Locked = true`
            // assignment -> the row-builder check fires; drop `choice.Locked` / BuildLockBadge( from
            // BuildResearchCard -> the panel check fires.
            int rbStart = vm.IndexOf("private void BuildResearchBrowse()", StringComparison.Ordinal);
            int rbEnd   = rbStart >= 0 ? vm.IndexOf("        private ", rbStart + 32, StringComparison.Ordinal) : -1;
            string researchBody = (rbStart >= 0 && rbEnd > rbStart) ? vm.Substring(rbStart, rbEnd - rbStart) : "";
            if (rbStart < 0)
                failures.Add("[research-locked-visible] BuildResearchBrowse not found - the locked-row pin cannot be scoped");
            if (researchBody.Contains("if (!can) continue;"))
                failures.Add("[research-locked-visible] Research drops locked perks (`if (!can) continue;` is back) - the owner sees an empty tab again");
            if (!researchBody.Contains("CanResearch(bId, pId, out string reason)") ||
                !researchBody.Contains("Locked = true,") ||
                !researchBody.Contains("StateText = gate") ||
                !researchBody.Contains("? \"Locked.\" : reason)") ||
                !researchBody.Contains("OpenUpgradePanel(bId)") ||
                !researchBody.Contains("\"UPGRADE THE HEART\"") ||
                !researchBody.Contains(" locked)."))
                failures.Add("[research-locked-visible] the locked research row is not built from the CanResearch reason with the upgrade-page door and the (M locked) trace");
            int rcStart = panel.IndexOf("private void BuildResearchCard(", StringComparison.Ordinal);
            int rcEnd   = rcStart >= 0 ? panel.IndexOf("\n        private ", rcStart + 31, StringComparison.Ordinal) : -1;
            string researchCardBody = (rcStart >= 0 && rcEnd > rcStart) ? panel.Substring(rcStart, rcEnd - rcStart) : "";
            if (rcStart < 0)
                failures.Add("[research-locked-visible] BuildResearchCard not found - the locked-perk pin cannot be " +
                             "scoped, so it cannot be trusted. The lock treatment moved here from the deleted " +
                             "BuildBrowseRowContent (WO-1422 ruling 3.4); a missing card is a FAIL, not a skip");
            // The parameter NAME is tolerated as `choice` or `selected` (the Buildings precedent
            // uses `choice` in the rail row and `selected` in the card); the FIELD, the BADGE and
            // the painted REASON - the three things the ruling is about - are pinned exactly.
            else if ((!researchCardBody.Contains("choice.Locked") && !researchCardBody.Contains("selected.Locked")) ||
                     !researchCardBody.Contains("BuildLockBadge(") ||
                     !researchCardBody.Contains("LockReason"))
                failures.Add("[research-locked-visible] a locked research perk's card does not dim + seat " +
                             "BuildLockBadge and paint its CanResearch reason, so the owner sees a card she cannot " +
                             "tell apart from an available one - the WO-1390 defect, one surface later");

            reason = failures.Count == 0
                ? "Manage keeps four stable worded cards, derives availability from live placements, renders after VM population, keeps its Build-new routes, pages nowhere, and its BUILD grid shows only unlocked rows."
                : "Manage progressive disclosure regression failed: " + string.Join("; ", failures);
            return failures.Count == 0;
        }

        // =====================================================================
        //  [build-grid-is-unlocked-only] - WO-1516, MEASURED against the live VM
        // ---------------------------------------------------------------------
        //  Owner ruling 2026-09-06 20:07, verbatim:
        //      "manage build scren should only show items that are unlocked and avaliable to them"
        //  and, in the same minute, Logs/device/screens/owner-screen-20260906-200741.png: eight
        //  DEFENSE tiles wearing the IDENTICAL green up-arrow, with no locked/unlocked distinction
        //  visible anywhere on the screen.
        //
        //  THE ONE AUTHORITY is BuildInventoryModel.Tiles - the accessor the BUILD palette's own
        //  visibility rule reduces to (BuildAvailability.Offered). This case measures the composed
        //  grid AGAINST that accessor rather than re-listing what "unlocked" means, so a second
        //  predicate anywhere would show up as a COUNT MISMATCH here.
        //
        //  RED PROOF: put `BuildInventoryModel.ManageTiles(_activeFilter)` back in
        //  ManageScreenVM.InventoryTiles - the count check fires (ManageTiles admits
        //  HiddenPendingUnlock rows) and, on any fixture that has one, the locked-state check fires
        //  too. Delete the StateIconKey line in ProjectAffordanceTile - the badge check fires.
        // =====================================================================
        private static void CheckBuildGridIsUnlockedOnly(List<string> failures)
        {
            var prior = GameStateService.Instance;
            // EnterTab persists the last-used tab; a regression run must never move a developer's
            // editor state (the same courtesy ManageTroopsTrainDoorRegression pays the save slot).
            bool hadTabPref = PlayerPrefs.HasKey(ManageScreenVM.LastTabPrefKey);
            int priorTabPref = PlayerPrefs.GetInt(ManageScreenVM.LastTabPrefKey, 0);
            GameObject host = null;
            GameState fixture = null;
            try
            {
                fixture = ScriptableObject.CreateInstance<GameState>();
                fixture.Onboarded = true;
                fixture.VillageTier = 2;
                // A town with something placed on every BUILD family, so the grid is not empty and
                // the count assertion is not passing on nothing.
                fixture.BaseLayout = new List<PlacedStructureData>
                {
                    new PlacedStructureData("forge", 2, 2, 0, 1),
                    new PlacedStructureData("lumbermill", 4, 2, 0, 1),
                    new PlacedStructureData("barracks", 6, 2, 0, 1),
                    // ⛔ A DEFENCE STRUCTURE, AND IT IS LOAD-BEARING FOR [portrait-key-single-producer].
                    // Measured 2026-09-06 (Builds/reg-wave3f.log): that case FAILED with "the fixture
                    // composed NO defense choices" - the town had no tower, so DefenseChoices was
                    // empty and the portrait-key equality was asserted against nothing.
                    // A TIER-2 tower on purpose: level 1 would exercise only the unsuffixed key and
                    // could not tell ManageArt.BuildingPortraitKey's "-<level>" rule from a producer
                    // that drops the suffix entirely - which is exactly the defect the sibling
                    // ManageDefenseCardRegression case exists to catch.
                    new PlacedStructureData("tower_ground_archer", 8, 2, 0, 2),
                };
                fixture.BuildingTiers["forge"] = 1;
                fixture.BuildingTiers["lumbermill"] = 1;
                fixture.BuildingTiers["barracks"] = 1;
                fixture.ObsidianQueue = ObsidianQueueState.Empty();

                host = new GameObject("GSS (manage-build-grid oracle)");
                var service = host.AddComponent<GameStateService>();
                if (!InstallState(service, fixture))
                {
                    // NOT A SKIP (the UpgradeQueueFullSurfaceRegression ruling): a suite that
                    // green-passes on an unreachable seam asserts nothing, most eagerly on the day
                    // the seam breaks.
                    failures.Add("[build-grid-is-unlocked-only] the GameStateService state seam is not " +
                                 "reflectable, so the BUILD grid could not be composed and WO-1516's ruling " +
                                 "is unmeasured. FAIL, not a skip.");
                    return;
                }

                var model = new ManageScreenVM();
                model.EnterTab(ManageTabId.Build);
                var workspace = model.ComposeWorkspace();
                if (workspace == null || workspace.Tabs == null || workspace.Tabs.Count == 0)
                {
                    failures.Add("[build-grid-is-unlocked-only] ComposeWorkspace produced no tabs, so the grid " +
                                 "could not be read. FAIL, not a skip.");
                    return;
                }
                int index = Mathf.Clamp(workspace.ActiveTabIndex, 0, workspace.Tabs.Count - 1);
                var tab = workspace.Tabs[index];
                if (tab == null || tab.Id != ManageTabId.Build)
                {
                    failures.Add("[build-grid-is-unlocked-only] the active tab after EnterTab(Build) is '" +
                                 (tab != null ? tab.Id.ToString() : "<null>") + "' - the BUILD grid was never " +
                                 "composed, so nothing below asserts anything.");
                    return;
                }

                var authority = BuildInventoryModel.Tiles(model.ActiveFilter);
                int shown = tab.Tiles != null ? tab.Tiles.Count : 0;
                if (shown == 0)
                {
                    failures.Add("[build-grid-is-unlocked-only] the BUILD grid composed ZERO tiles under chip '" +
                                 model.ActiveFilter + "', so the locked-state assertion would pass on an empty " +
                                 "list. FAIL, not a skip.");
                    return;
                }
                if (shown != authority.Count)
                    failures.Add("[build-grid-is-unlocked-only] the BUILD grid shows " + shown + " tile(s) under " +
                                 "chip '" + model.ActiveFilter + "' but BuildInventoryModel.Tiles - the palette's " +
                                 "own unlock authority - offers " + authority.Count + ". Owner ruling 2026-09-06 " +
                                 "20:07: \"manage build scren should only show items that are unlocked and " +
                                 "avaliable to them\". A mismatch means a SECOND membership rule has appeared " +
                                 "beside ManageScreenVM.InventoryTiles.");

                for (int i = 0; i < tab.Tiles.Count; i++)
                {
                    var tile = tab.Tiles[i];
                    if (tile == null) { failures.Add("[build-grid-is-unlocked-only] a null BUILD tile."); continue; }
                    if (tile.VisualState == ManageTileVisualState.Locked)
                        failures.Add("[build-grid-is-unlocked-only] BUILD tile '" + tile.Id + "' renders LOCKED (\"" +
                                     tile.StateText + "\"). The ruling removes the locked state from this screen " +
                                     "entirely - a padlocked tile is precisely what she asked to stop seeing.");

                    // ⭐ THE GREEN UP-ARROW MUST MEAN SOMETHING (WO-1516 acceptance 2).
                    // ManageArt.StatusFor gives every Available-state tile the SAME status-available
                    // medallion, which is why all eight tiles in her frame wore it. It is now
                    // withheld unless the tile can actually be acted on, so a tile that reads SHORT
                    // or HEART GATED must carry no status key at all.
                    bool refused = !string.IsNullOrEmpty(tile.StateText) &&
                                   (tile.StateText.StartsWith("SHORT", StringComparison.Ordinal) ||
                                    string.Equals(tile.StateText, "HEART GATED", StringComparison.Ordinal));
                    if (refused && !string.IsNullOrEmpty(tile.StateIconKey))
                        failures.Add("[build-grid-is-unlocked-only] BUILD tile '" + tile.Id + "' reads \"" +
                                     tile.StateText + "\" and still carries the status medallion '" +
                                     tile.StateIconKey + "'. The badge must state a REAL affordance or be " +
                                     "removed - see ManageScreenVM.ProjectAffordanceTile.");

                    // ⭐ [grid-tile-states-its-state] WO-1563 - THE ACCESSIBILITY CASE.
                    // The model composes StateText on every tile and BuildTile referenced it ZERO
                    // times, so a BUILD/ARMY grid tile carried portrait + name and nothing else.
                    // With WO-1516 correctly WITHHOLDING the meaningless Available medallion, a
                    // tile with neither glyph nor word is MUTE - and the owner is red/green
                    // colourblind, so the glyph was never the reliable channel anyway.
                    // This half asserts the MODEL keeps supplying the word; the renderer half is
                    // pinned by the source scan below (a View cannot be composed headless here).
                    // RED RECIPE: blank BadgeText in ManageScreenVM's tile composers.
                    if (string.IsNullOrEmpty(tile.StateText))
                        failures.Add("[grid-tile-states-its-state] BUILD tile '" + tile.Id + "' carries NO " +
                                     "StateText. It is the only non-colour state channel the grid has left " +
                                     "(WO-1516 withholds the Available medallion), so this tile tells a " +
                                     "colourblind player nothing about what can be acted on.");
                }

                // ⭐ [research-picker-capacity] WO-1564 part 1 - CAPACITY IS DERIVED, NOT AUTHORED.
                // The picker was authored 4 columns x 1 row from a comment reading "four research
                // BUILDINGS in ONE row". FIVE schools exist, so the fifth was orphaned alone on a
                // second row beside three empty cells. A literal 5 would be the same defect one
                // school later, so the geometry is derived from the live school count.
                // RED RECIPE: put `GridColumns = 4, GridRows = 1` back and delete the
                // ApplyPickerCapacity call in ManageScreenVM.ComposeWorkspace.
                CheckResearchPickerCapacityIsDerived(failures);

                // ⭐ [portrait-key-single-producer] - ONE PRODUCER OF THE PORTRAIT KEY.
                CheckPortraitKeysComeFromManageArt(model, tab, failures);
            }
            catch (Exception ex)
            {
                failures.Add("[build-grid-is-unlocked-only] threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                SetGssInstance(prior);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                if (hadTabPref) PlayerPrefs.SetInt(ManageScreenVM.LastTabPrefKey, priorTabPref);
                else PlayerPrefs.DeleteKey(ManageScreenVM.LastTabPrefKey);
            }
        }

        /// <summary>
        /// [portrait-key-single-producer] - every portrait key the VM composes comes from
        /// <see cref="ManageArt.BuildingPortraitKey"/>, the ONE producer, and none from a
        /// display-name slug.
        ///
        /// <para>⛔ THE DEFECT THIS IS SHAPED AROUND, MEASURED not inferred:
        /// <c>Builds/ui-capture/ManageFlow_BUILD_gridtop_2670x1200.png</c> painted Wooden Palisade
        /// and Crystal Mine as blank tan ovals - the placeholder disc <c>ManageArt.LoadSprite</c>
        /// documents at <c>:177-186</c>. <c>cap-manage-wave3.log</c> traced the cause:
        /// <c>BuildDefenseChoices</c> asked for <c>'Portraits/wooden-palisade'</c> and
        /// <c>'Portraits/crystal-mine-2'</c> - display-name slugs against the MIXED ROOT folder,
        /// which exist nowhere - while the art ships id-keyed under
        /// <c>Portraits/Buildings/wall_wood</c> and <c>mine_crystal</c>. A SECOND producer of a key
        /// ManageArt already owned, and it was the stale copy.</para>
        ///
        /// <para>RED RECIPE: put <c>ResolveBuildingPortraitKey(entry, PortraitSlug(...), level)</c>
        /// back in <c>BuildDefenseChoices</c> - both halves below fire.</para>
        /// </summary>
        private static void CheckPortraitKeysComeFromManageArt(ManageScreenVM model, ManageTabVM tab,
            List<string> failures)
        {
            // -- HALF A: the DEFENSE composer, id + tier, asserted EXACTLY --------
            // These rows carry both halves of the key (CatalogEntryId and Level), so the assertion
            // is an equality against the one producer rather than a shape test.
            int measured = 0;
            for (int i = 0; i < model.DefenseChoices.Count; i++)
            {
                var d = model.DefenseChoices[i];
                if (d == null || string.IsNullOrEmpty(d.CatalogEntryId)) continue;
                measured++;
                string expected = ManageArt.BuildingPortraitKey(d.CatalogEntryId, d.Level);
                if (!string.Equals(d.PortraitKey, expected, StringComparison.Ordinal))
                    failures.Add("[portrait-key-single-producer] defense choice '" + d.CatalogEntryId +
                                 "' (L" + d.Level + ") carries PortraitKey '" + d.PortraitKey +
                                 "' but the ONE producer ManageArt.BuildingPortraitKey says '" + expected +
                                 "'. A second key composer has returned - the display-name slug against the " +
                                 "mixed root folder is what painted Wooden Palisade and Crystal Mine as blank " +
                                 "tan ovals. The catalog ID is load-bearing (WO-1567 section 7).");
            }
            if (measured == 0)
                failures.Add("[portrait-key-single-producer] the fixture composed NO defense choices, so the " +
                             "key producer is unmeasured. FAIL, not a skip.");

            // -- HALF B: no BUILD grid tile may address the mixed ROOT folder -----
            // BuildingPortraitKey pins the FOLDER as well as the spelling; a key outside it is a
            // key some other composer made.
            if (tab != null && tab.Tiles != null)
                for (int i = 0; i < tab.Tiles.Count; i++)
                {
                    var t = tab.Tiles[i];
                    if (t == null || string.IsNullOrEmpty(t.PortraitKey)) continue;
                    if (!t.PortraitKey.StartsWith(ManageArt.BuildingPortraitFolder, StringComparison.Ordinal))
                        failures.Add("[portrait-key-single-producer] BUILD tile '" + t.Id + "' addresses '" +
                                     t.PortraitKey + "', outside ManageArt.BuildingPortraitFolder ('" +
                                     ManageArt.BuildingPortraitFolder + "'). The root folder is the MIXED one " +
                                     "whose 20 missing tier keys ManagePortraitCoverageRegression records.");
                }
        }

        /// <summary>
        /// WO-1564 part 1 - the RESEARCH picker seats every live school and leaves fewer empty
        /// cells than it has columns, so no school can be orphaned alone on a ragged row.
        /// <para>Runs inside <see cref="CheckBuildGridIsUnlockedOnly"/>'s fixture, on the same
        /// installed GameStateService - the picker's school list is composed from that town.</para>
        /// </summary>
        private static void CheckResearchPickerCapacityIsDerived(List<string> failures)
        {
            var model = new ManageScreenVM();
            // ⛔ REBUILD BEFORE EnterTab, AND THE ORDER IS LOAD-BEARING (fixed 2026-09-06 after this
            // case went RED with "the active tab after EnterTab(Research) is 'Build'").
            //
            // THE VM IS RIGHT AND THE FIXTURE WAS WRONG. EnterTab (ManageScreenVM.cs:3330) refuses
            // any id not in _availableTabs, and RefreshAvailableTabs (`:3296-3304`) adds RESEARCH
            // only when `VisibleTabs.Contains(ManageTab.Research)`. VisibleTabs is populated by
            // BuildVisibleTabs (`:672-694`), which runs ONLY inside Rebuild() - so on a
            // freshly-constructed VM it is EMPTY, Research is unavailable, and EnterTab correctly
            // warns and stays on BUILD. That refusal is the progressive-disclosure gate working.
            //
            // ⚠ WHY THE SIBLING CASE ABOVE NEEDS NO REBUILD, which is what made this look like a VM
            // defect: RefreshAvailableTabs adds ManageTabId.Build UNCONDITIONALLY (`:3299`), so
            // EnterTab(Build) succeeds on an un-rebuilt VM and every other tab does not.
            // ManageTroopsTrainDoorRegression's fixtures pass for the same reason - they call
            // vm.Rebuild() before reading a tab.
            model.Rebuild();
            model.EnterTab(ManageTabId.Research);
            var workspace = model.ComposeWorkspace();
            if (workspace == null || workspace.Tabs == null || workspace.Tabs.Count == 0)
            {
                failures.Add("[research-picker-capacity] ComposeWorkspace produced no tabs for RESEARCH, so the " +
                             "picker geometry is unmeasured. FAIL, not a skip.");
                return;
            }
            int index = Mathf.Clamp(workspace.ActiveTabIndex, 0, workspace.Tabs.Count - 1);
            var tab = workspace.Tabs[index];
            if (tab == null || tab.Id != ManageTabId.Research)
            {
                // Say WHICH of the two causes it is, so the next reader does not re-diagnose this.
                // Hand-rolled, not Linq: this suite deliberately imports no System.Linq.
                bool offered = false;
                var offeredIds = model.AvailableTabIds;
                if (offeredIds != null)
                    for (int i = 0; i < offeredIds.Count; i++)
                        if (offeredIds[i] == ManageTabId.Research) { offered = true; break; }
                failures.Add("[research-picker-capacity] the active tab after Rebuild + EnterTab(Research) is '" +
                             (tab != null ? tab.Id.ToString() : "<null>") + "'. RESEARCH " +
                             (offered ? "IS" : "is NOT") + " in AvailableTabIds, and VisibleTabs holds [" +
                             VisibleTabWords(model) + "]. " +
                             (offered
                                ? "The tab is offered but the workspace did not activate it - a navigation defect."
                                : "BuildVisibleTabs found no placed building carrying an authored perk " +
                                  "(ManageScreenVM.HasAuthoredPerk), so this FIXTURE's town has no research " +
                                  "school - place one, rather than weakening the gate."));
                return;
            }

            int schools = tab.Tiles != null ? tab.Tiles.Count : 0;
            if (schools <= 0)
            {
                // The seed capacity is deliberately left alone in this state (a derived 0x0 would
                // make the renderer refuse the band instead of saying the list is empty), so there
                // is nothing to assert and saying so is better than a silent green.
                failures.Add("[research-picker-capacity] the RESEARCH picker composed ZERO schools in a town " +
                             "with a forge, a lumbermill and a barracks placed - the capacity assertion would " +
                             "pass on nothing. FAIL, not a skip.");
                return;
            }

            int capacity = tab.GridColumns * tab.GridRows;
            if (capacity < schools)
                failures.Add("[research-picker-capacity] the picker is " + tab.GridColumns + "x" + tab.GridRows +
                             " = " + capacity + " cells for " + schools + " live school(s). A school that does " +
                             "not fit is a school the player cannot reach - this is the authored-literal defect " +
                             "(4x1 against five schools) that WO-1564 exists to end.");
            if (capacity - schools >= tab.GridColumns)
                failures.Add("[research-picker-capacity] the picker is " + tab.GridColumns + "x" + tab.GridRows +
                             " for " + schools + " school(s), leaving " + (capacity - schools) + " empty cell(s) " +
                             "- a whole row or more of dead well. The capacity must follow the count.");
            if (tab.GridColumns > schools)
                failures.Add("[research-picker-capacity] the picker asks for " + tab.GridColumns + " columns for " +
                             schools + " school(s) - more columns than there are tiles cannot be derived from " +
                             "the count and re-introduces an authored literal.");
        }

        /// <summary>The model's visible legacy tabs as a comma-joined word list. Hand-rolled so the
        /// message needs neither System.Linq nor the generic string.Join overload.</summary>
        private static string VisibleTabWords(ManageScreenVM model)
        {
            if (model == null || model.VisibleTabs == null || model.VisibleTabs.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < model.VisibleTabs.Count; i++)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(model.VisibleTabs[i].ToString());
            }
            return sb.ToString();
        }

        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }
    }
}
