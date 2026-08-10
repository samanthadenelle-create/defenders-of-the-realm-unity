// =============================================================================
// EchoHollowRouteRegression [echo-hollow-route] -- WO-951: proves the Echo Hollow
// (pet-house) interact route opens the EXISTING Echo roster popup.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (direct DeNelle.Village reference -- no
// reflection). Owner ruling 2026-08-10, verbatim: "so then when they go to the
// store they open the echos pop up on right? Simple and easy."
//
// WHAT THIS PINS (all headless, all pure):
//   1. CastleNpcInteractable.ResolveRoute("pet-house") == "echo-roster" -- the
//      SHARED routing chokepoint (the same method the keeper NPC's Interact()
//      branches on, and the AutoPilot AssertVendorTalkRoute oracle reads) decides
//      the Hollow opens the roster, case-insensitively, before the upgrade
//      short-circuit or the legacy Yarn grant menu can steal it.
//   2. The opener seam exists: EchoRoster.Open (the existing roster popup's
//      public static opener) still compiles as the route's target. OpenPanel
//      self-traces [Flow:Echo] RosterOpen and registers with PanelManager.
//   3. Neighbor routes are NOT stolen: barracks (talk-function) and market
//      (shoppable) still resolve "talk-dialogue".
//   4. The keeper NPC still anchors to the Hollow: RoleForBuildingId("pet-house")
//      == "EchoHollow", so a seated keeper carries the id the route keys on.
//   5. Source-lint on the building-TAP surface (Interact() is instance+scene-bound,
//      so the branch itself is asserted at source): BuildingInteractable.cs must
//      consult CastleNpcInteractable.IsEchoHollowId and call EchoRoster.Open().
//
// Marker: ECHO_HOLLOW_ROUTE_OK / ECHO_HOLLOW_ROUTE_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "echo-hollow-route suite", () => { if (!EchoHollowRouteRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-hollow-route] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class EchoHollowRouteRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- ECHO HOLLOW ROUTE (WO-951: pet-house interact -> Echo roster popup) ---");

            // 1) The shared routing chokepoint decides "echo-roster" for the Hollow.
            try
            {
                string route = CastleNpcInteractable.ResolveRoute(CastleNpcInteractable.EchoHollowId);
                log.AppendLine($"  ResolveRoute('{CastleNpcInteractable.EchoHollowId}') = '{route}'");
                if (route != CastleNpcInteractable.EchoRosterRoute)
                    failures.Add($"[echo-hollow-route] ResolveRoute('pet-house')='{route}' (expected '{CastleNpcInteractable.EchoRosterRoute}') -- the Hollow's tap/Talk no longer opens the Echo roster (WO-951 ruling)");

                string routeCased = CastleNpcInteractable.ResolveRoute("Pet-House");
                if (routeCased != CastleNpcInteractable.EchoRosterRoute)
                    failures.Add($"[echo-hollow-route] ResolveRoute('Pet-House')='{routeCased}' -- the Hollow route must be case-insensitive (building ids fall back to GameObject names)");

                if (CastleNpcInteractable.IsEchoHollowId(null) || CastleNpcInteractable.IsEchoHollowId(""))
                    failures.Add("[echo-hollow-route] IsEchoHollowId(null/empty) returned true -- a hookless building would open the roster");
            }
            catch (Exception ex)
            {
                failures.Add($"[echo-hollow-route] ResolveRoute threw: {ex.GetType().Name}: {ex.Message}");
            }

            // 2) The opener seam the route calls -- compile-time bound, assert non-null.
            Action opener = EchoRoster.Open;
            if (opener == null)
                failures.Add("[echo-hollow-route] EchoRoster.Open opener seam is null");
            else
                log.AppendLine("  EchoRoster.Open seam present (compile-time bound).");

            // 3) Neighbor routes untouched: talk-function + shoppable stay "talk-dialogue".
            try
            {
                string barracks = CastleNpcInteractable.ResolveRoute("barracks");
                if (barracks != "talk-dialogue")
                    failures.Add($"[echo-hollow-route] ResolveRoute('barracks')='{barracks}' (expected 'talk-dialogue') -- the WO-951 branch disturbed the talk-function route");
                string market = CastleNpcInteractable.ResolveRoute("market");
                if (market != "talk-dialogue")
                    failures.Add($"[echo-hollow-route] ResolveRoute('market')='{market}' (expected 'talk-dialogue') -- the WO-951 branch disturbed the shoppable route");
                log.AppendLine($"  neighbors: barracks='{barracks}', market='{market}' (both expect 'talk-dialogue').");
            }
            catch (Exception ex)
            {
                failures.Add($"[echo-hollow-route] neighbor ResolveRoute threw: {ex.GetType().Name}: {ex.Message}");
            }

            // 4) The keeper NPC still anchors to the Hollow's building id.
            try
            {
                string role = CastleVendorNpcInjector.RoleForBuildingId(CastleNpcInteractable.EchoHollowId);
                log.AppendLine($"  RoleForBuildingId('pet-house') = '{role}'");
                if (!string.Equals(role, "EchoHollow", StringComparison.OrdinalIgnoreCase))
                    failures.Add($"[echo-hollow-route] RoleForBuildingId('pet-house')='{role}' (expected 'EchoHollow') -- no keeper NPC would seat at the Hollow, so the Talk surface disappears");
            }
            catch (Exception ex)
            {
                failures.Add($"[echo-hollow-route] RoleForBuildingId threw: {ex.GetType().Name}: {ex.Message}");
            }

            // 5) Source-lint the building-TAP surface (Interact() is private + scene-bound;
            //    the branch is proven at source, the DECISION is proven above via the shared
            //    chokepoint both surfaces consult).
            try
            {
                string path = Path.Combine(Application.dataPath, "_Modules/Village/Buildings/BuildingInteractable.cs");
                if (!File.Exists(path))
                    failures.Add($"[echo-hollow-route] source-lint: BuildingInteractable.cs not found at '{path}'");
                else
                {
                    string src = File.ReadAllText(path);
                    if (!src.Contains("IsEchoHollowId(hookId)"))
                        failures.Add("[echo-hollow-route] source-lint: BuildingInteractable.Interact no longer consults CastleNpcInteractable.IsEchoHollowId(hookId) -- the building TAP would fall back to the legacy Yarn grant menu");
                    if (!src.Contains("EchoRoster.Open()"))
                        failures.Add("[echo-hollow-route] source-lint: BuildingInteractable no longer calls EchoRoster.Open() -- the WO-951 tap route is gone");
                    else
                        log.AppendLine("  source-lint: tap surface consults IsEchoHollowId + calls EchoRoster.Open().");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"[echo-hollow-route] source-lint threw: {ex.GetType().Name}: {ex.Message}");
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                string ok = "ECHO_HOLLOW_ROUTE_OK -- pet-house interact routes to the Echo roster (5 checks)";
                Debug.Log(log.ToString() + ok);
                return ok;
            }
            string fail = "ECHO_HOLLOW_ROUTE_FAIL -- " + string.Join(" | ", failures);
            Debug.LogError(log.ToString() + fail);
            return fail;
        }
    }
}
