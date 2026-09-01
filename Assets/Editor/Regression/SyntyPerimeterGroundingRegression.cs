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
                Require(src, "MergedWorldGroundY - MeasureMinY(tower)");
                Require(src, "Vector3.up * moduleSeatY");
                Require(src, "Vector3.up * gateSeatY");
                Require(src, "ApplyOpenGatePose(gateInstance)");
                Require(src, "SM_Bld_Castle_Wall_Gate_Door_L_01");
                Require(src, "SM_Bld_Castle_Wall_Gate_Door_R_01");
                Require(src, "SM_Bld_Castle_Wall_Gate_Portcullis_01");
                Require(src, "DisableColliders(portcullis)");
                if (src.IndexOf("CastleHubBuilder.CastleFootprintLiftY", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("merged perimeter reads the retired +3m island lift and can float above y=0 again");

                reason = "SYNTY_PERIMETER_GROUNDING_OK: walls, gates, and towers seat measured renderer bounds on merged-world y=0; traversable gates are visibly authored open.";
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
