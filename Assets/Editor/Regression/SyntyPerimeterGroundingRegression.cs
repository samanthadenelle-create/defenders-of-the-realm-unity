#if UNITY_EDITOR
using System;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>Prevents the merged-world perimeter from returning to the retired +3m island seat.</summary>
    public static class SyntyPerimeterGroundingRegression
    {
        private const string Builder = "Assets/Editor/WallTools/SyntyCastlePerimeterBuilder.cs";

        public static bool Run(out string reason)
        {
            try
            {
                string src = File.ReadAllText(Builder);
                Require(src, "private const float MergedWorldGroundY = 0f;");
                Require(src, "MergedWorldGroundY - MeasureMinY(wall)");
                Require(src, "MergedWorldGroundY - MeasureMinY(gate)");
                Require(src, "MergedWorldGroundY + MeasureMaxY(tower)");
                Require(src, "Quaternion.Euler(180f, 0f, 0f)");
                Require(src, "Vector3.up * moduleSeatY");
                Require(src, "Vector3.up * gateSeatY");
                Require(src, "ApplyOpenGatePose(gateInstance)");
                Require(src, "SM_Bld_Castle_Wall_Gate_Door_L_01");
                Require(src, "SM_Bld_Castle_Wall_Gate_Door_R_01");
                Require(src, "SM_Bld_Castle_Wall_Gate_Portcullis_01");
                Require(src, "DisableColliders(gate.transform)");
                Require(src, "AddGateFlankColliders(side.transform, gateInstance, s)");
                Require(src, "Wall_DoorJamb_L");
                Require(src, "Wall_DoorJamb_R");
                Require(src, "GATE_CLEARANCE_OK 4/4 gates");
                Require(src, "GetComponentsInChildren<Collider>(true)");
                Require(src, "clearWidth >= 3.95f");
                Require(src, "MergedWorldGroundY - 0.25f");
                string carve = File.ReadAllText("Assets/_Modules/Village/World/CastleWallNavObstacleInstaller.cs");
                Require(carve, "if (!col.enabled || col.isTrigger) continue;");
                string traversal = File.ReadAllText("Assets/_Modules/Village/World/GateTraversalInjector.cs");
                // NOTE: do NOT re-add a Require() on a doc-comment phrase here. A comment
                // assertion breaks on a rename while a real behaviour change slips past it.
                // The two IndexOf guards below are the actual re-introduction guard.
                Require(traversal, "zero NavMeshLinks and zero hero warps authored");
                if (traversal.IndexOf("AddComponent<NavMeshLink>", StringComparison.Ordinal) >= 0 ||
                    traversal.IndexOf("class GateWarp", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("merged-world gates reintroduced a runtime nav link or hero warp");
                if (src.IndexOf("CastleHubBuilder.CastleFootprintLiftY", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("merged perimeter reads the retired +3m island lift and can float above y=0 again");

                reason = "SYNTY_PERIMETER_GROUNDING_OK: walls/gates ground, source corner towers are corrected, and all four openings are continuous with no gate nav link/warp.";
                return true;
            }
            catch (Exception ex)
            {
                reason = "SYNTY_PERIMETER_GROUNDING_FAIL: " + ex.Message;
                return false;
            }
        }

        private static void Require(string source, string token)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("missing grounding contract: " + token);
        }
    }
}
#endif
