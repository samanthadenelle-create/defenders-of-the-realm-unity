// =============================================================================
// RaidHudThumbBandRegression [raid-thumb-band] — WO-1436, owner ruling 2026-09-06
// -----------------------------------------------------------------------------
// ⭐ THE INVARIANT: THE HERO'S ABILITY ROW OWNS THE THUMB POSITION AT THE BOTTOM
// OF THE SCREEN, AND NOTHING IS EVER DRAWN INTO THAT BAND.
//
// The raid deploy bar (FOOTMAN / ARCHER / Rally ON / RETREAT) stacks ABOVE it.
// Owner's reasoning, and the reason this is the right way round: CASTING IS
// CONSTANT, DEPLOYING IS OCCASIONAL — the constant action belongs under the thumb.
//
// WHAT WENT WRONG, MEASURED AT SOURCE (WO-1436 §7.3 — not inferred, not theorised)
// -------------------------------------------------------------------------------
//   surface                              Y band (viewport)   canvas sortingOrder
//   kit `actionBar` (holds combatDock)   0.015 – 0.150        4 000
//   raid deploy bar panel                0.010 – 0.160       30 000
// The same band, with the deploy bar 26 000 layers above it. The owner's device
// screenshot (mid-raid, 1:58 remaining, Razed 79%) shows the ability faces
// CAST · BLOCK · Arcane Bolt · Mend · EMPTY · ITEM rendering *underneath* the
// deploy strip. They existed, the log truthfully said they had been pushed, and
// the player still could not fight — which from the chair is indistinguishable
// from not having them. Then WO-1436's posture fix made a raid declare combat for
// its WHOLE duration (correct, and it stays), so combatDock went from
// intermittently buried to PERMANENTLY buried.
//
// ⛔ WHY NO EXISTING ORACLE COULD CATCH IT, AND WHAT THIS ONE DOES DIFFERENTLY
// ---------------------------------------------------------------------------
// Every HUD oracle in this folder asks "does surface X lay itself out correctly?"
// — and both surfaces did, perfectly, in isolation. Nobody asked whether the two
// of them, authored in two assemblies that CANNOT SEE EACH OTHER, could occupy the
// same pixels. This oracle asks exactly that, and asks it from the AUTHORED
// ANCHORS ON BOTH SIDES rather than from figures copied into this file.
//
// ⚠ WHY FRACTIONS FROM TWO DIFFERENT CANVASES ARE COMPARABLE. HudAreasHost builds
// a ScreenSpaceOverlay canvas (sortingOrder 4000); RaidDeployController builds a
// separate ScreenSpaceOverlay canvas (30000). On a ScreenSpaceOverlay canvas an
// anchor fraction is a fraction OF THE SCREEN whatever CanvasScaler the canvas
// carries — the scaler changes the size of a reference unit, never where
// anchorMin.y = 0.16 lands. So the band arithmetic below is valid across the two,
// and sortingOrder stops mattering the moment the bands are exclusive. ⛔ A future
// overlap must be fixed by separating the BANDS, never by re-ordering the canvases.
//
// ═══ RED PROOF — THIS ORACLE FAILS AGAINST THE BUILD AS IT SHIPPED ═══════════
// Run against the pre-WO-1436-layout tree, all four cases go red:
//   [1] deploy bar 0.010–0.160 vs ability row 0.015–0.150 →
//       HudLayoutBands.Intersects(...) == TRUE → FAIL "the raid deploy bar is
//       drawn INTO the ability row's band".
//   [2] DeployBarBand.yMin (0.010) < BottomOverlayFloorY (0.160) → FAIL.
//   [3] RaidDeployController.cs contained the literal
//       `new Vector2(0.98f, 0.16f)` — a hardcoded bottom-Y — → FAIL.
//   [4] HudAreasHost.cs's ActionBar mount contained the literal `0.150f` instead
//       of reading HudLayoutBands.ThumbActionRowMaxY → FAIL.
// Cases 3 and 4 are the ones that matter for the NEXT seat: they are not about
// today's numbers, they are about the numbers never being re-typed into two
// files again (CLAUDE.md §2/§5/§8/§16 — the duplicated-state failure, four times
// documented, every instance correct on the day it was written).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.HUD.Kit;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// Asserts the raid deploy bar and the hero ability row occupy EXCLUSIVE screen bands,
    /// measured from the authored anchors on both sides, and that neither side re-authors the
    /// shared band as a local literal.
    /// </summary>
    public static class RaidHudThumbBandRegression
    {
        public static bool Run(out string reason)
        {
            try
            {
                var failures = new List<string>();

                // ── CASE 1 + 2: the numeric exclusion, from the authored anchors ──────────
                // The ability row's x edges come from HudAreasHost (their source text is pinned
                // by HudDockLayoutRegression, so they are read, never restated); its y edges come
                // from HudLayoutBands, the ONE copy. The deploy bar's band comes from
                // RaidDeployController's own public accessor, which BuildHud itself consumes —
                // so if the runtime seat and this assertion ever disagree, they cannot.
                Rect abilityRow = HudLayoutBands.ThumbActionRowBand(
                    HudAreasHost.ActionBarMinX, HudAreasHost.ActionBarMaxX);
                Rect deployBar = RaidDeployController.DeployBarBand;
                Rect deployStatus = RaidDeployController.DeployStatusBand;

                if (HudLayoutBands.Intersects(abilityRow, deployBar))
                    failures.Add("[buried-abilities] the raid deploy bar (y " +
                                 F(deployBar.yMin) + ".." + F(deployBar.yMax) +
                                 ") is drawn INTO the hero ability row's band (y " +
                                 F(abilityRow.yMin) + ".." + F(abilityRow.yMax) +
                                 "). The faces render and cannot be tapped — the WO-1436 defect. " +
                                 "Fix by moving the BAR, never by re-ordering the canvases.");

                if (HudLayoutBands.Intersects(abilityRow, deployStatus))
                    failures.Add("[buried-abilities] the raid deploy STATUS line (y " +
                                 F(deployStatus.yMin) + ".." + F(deployStatus.yMax) +
                                 ") is drawn INTO the hero ability row's band (y " +
                                 F(abilityRow.yMin) + ".." + F(abilityRow.yMax) + ").");

                if (deployBar.yMin < HudLayoutBands.BottomOverlayFloorY - HudLayoutBands.Epsilon)
                    failures.Add("[thumb-floor] the deploy bar bottoms out at y " +
                                 F(deployBar.yMin) + ", BELOW the reserved floor " +
                                 F(HudLayoutBands.BottomOverlayFloorY) +
                                 " (HudLayoutBands.BottomOverlayFloorY). The thumb band is the " +
                                 "ability row's, by owner ruling 2026-09-06.");

                if (HudLayoutBands.Intersects(deployBar, deployStatus))
                    failures.Add("[deploy-self] the deploy bar and its own status line overlap (bar y " +
                                 F(deployBar.yMin) + ".." + F(deployBar.yMax) + ", status y " +
                                 F(deployStatus.yMin) + ".." + F(deployStatus.yMax) + ").");

                // Stacking ORDER, stated as an assertion rather than assumed from the numbers:
                // the deploy bar must be ABOVE the ability row, not merely disjoint from it.
                if (deployBar.yMin < abilityRow.yMax)
                    failures.Add("[stack-order] the deploy bar starts at y " + F(deployBar.yMin) +
                                 ", which is not above the ability row's top edge " +
                                 F(abilityRow.yMax) + ". The owner's ruling is a STACK " +
                                 "(abilities under the thumb, deploy bar on top), not a swap.");

                // ── CASE 3 + 4: neither side may re-author the shared band as a literal ───
                // This is the assertion that outlives today's numbers. The whole defect exists
                // because DeNelle.Village cannot reference DeNelle.HUD (CLAUDE.md §5), so the
                // only way the two agreed was a human retyping a number — and this repo has four
                // separate documented cases of exactly that going stale.
                string raid = Read("Assets/_Modules/Village/Troops/RaidDeployController.cs");
                string host = Read("Assets/_Modules/HUD/Kit/HudAreasHost.cs");

                Require(raid, "HudLayoutBands.StackAboveThumbBand", failures,
                    "[shared-seam] RaidDeployController no longer derives its band from " +
                    "HudLayoutBands — a Village-local Y literal is exactly how the bar came to " +
                    "sit on the ability row");
                Forbid(raid, "new Vector2(0.98f, 0.16f)", failures,
                    "[shared-seam] RaidDeployController has a hardcoded bar top-Y again " +
                    "(`new Vector2(0.98f, 0.16f)`) — the literal that shipped the defect");
                Forbid(raid, "new Vector2(0.02f, 0.01f)", failures,
                    "[shared-seam] RaidDeployController has a hardcoded bar bottom-Y again " +
                    "(`new Vector2(0.02f, 0.01f)`) — it reached below the reserved thumb band");
                Forbid(raid, "new Vector2(0.02f, 0.165f)", failures,
                    "[shared-seam] the deploy status line is hardcoded again " +
                    "(`new Vector2(0.02f, 0.165f)`) instead of stacking off the shared gap");

                Require(host, "HudLayoutBands.ThumbActionRowMinY", failures,
                    "[shared-seam] HudAreasHost no longer reads the ability row's BOTTOM edge " +
                    "from HudLayoutBands — the Village side can no longer follow it");
                Require(host, "HudLayoutBands.ThumbActionRowMaxY", failures,
                    "[shared-seam] HudAreasHost no longer reads the ability row's TOP edge from " +
                    "HudLayoutBands — the Village side can no longer follow it");
                Forbid(host, "new Vector2(ActionBarMaxX, 0.150f)", failures,
                    "[shared-seam] the ActionBar mount authors its own top-Y literal (0.150f) " +
                    "again — DeNelle.Village cannot see it, so the deploy bar cannot follow it");

                // The Core seam must stay in DeNelle.Core, which is the ONLY assembly both sides
                // may reference. If it ever migrates into DeNelle.HUD the invariant is unbuildable.
                string bandsPath = "Assets/_Modules/Core/UI/HudLayoutBands.cs";
                if (!File.Exists(FullPath(bandsPath)))
                    failures.Add("[shared-seam] " + bandsPath + " is gone — the reserved thumb " +
                                 "band has no lawful home outside DeNelle.Core (CLAUDE.md §5)");

                if (failures.Count > 0)
                {
                    reason = "raid-thumb-band: " + failures.Count + " failure(s): " +
                             string.Join(" | ", failures);
                    return false;
                }

                reason = "raid-thumb-band: the hero ability row owns y " +
                         F(abilityRow.yMin) + ".." + F(abilityRow.yMax) +
                         " (the thumb band); the raid deploy bar stacks above it at y " +
                         F(deployBar.yMin) + ".." + F(deployBar.yMax) + " with its status line at y " +
                         F(deployStatus.yMin) + ".." + F(deployStatus.yMax) +
                         "; both sides derive from HudLayoutBands (DeNelle.Core.UI), neither " +
                         "re-authors the band as a local literal";
                return true;
            }
            catch (Exception ex)
            {
                reason = "raid-thumb-band: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("RAID_THUMB_BAND_OK - " + reason);
            else Debug.LogError("RAID_THUMB_BAND_FAIL - " + reason);
        }

        private static string F(float v) { return v.ToString("F3"); }

        private static string FullPath(string relative)
        {
            return Path.Combine(Application.dataPath, "..", relative);
        }

        private static string Read(string relative)
        {
            return File.ReadAllText(FullPath(relative));
        }

        private static void Require(string source, string token, List<string> failures, string message)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0) failures.Add(message);
        }

        private static void Forbid(string source, string token, List<string> failures, string message)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0) failures.Add(message);
        }
    }
}
