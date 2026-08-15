// =============================================================================
// HeroFacingAudit - WO-965 (owner F8 seq 2309, "Mage faces northwest when
// running north").
//
// WHY THIS EXISTS. RangerBodyBuilder.ReportFacing has always MEASURED the
// Ranger FBX's true forward from its humanoid shoulder axis and printed the yaw
// needed to face world +Z. There was no equivalent for any OTHER hero - so the
// -90 that HeroBodySwapper applies to EVERY non-Knight body
// (HeroBodySwapper.cs:263) has never been checked against the Mage's actual
// mesh. "The Mage's rig probably matches the Ranger's" is exactly the kind of
// assumption CLAUDE.md 12 forbids acting on: this turns it into a NUMBER, with
// no playtest and no owner capture required.
//
// It REUSES RangerBodyBuilder.MeasureForwardYawNeeded - the identical maths the
// builder reports with. A second copy of that measurement would drift and the
// audit would then be measuring a different thing than the builder.
//
// REPORTS ONLY. Nothing is rotated, no importer is touched, no yaw is changed.
// The applied-yaw constants below are MIRRORED FOR COMPARISON ONLY; the
// authority is HeroBodySwapper.cs:263 and this file must never become a second
// place that "sets" them.
//
// Batchmode:
//   -executeMethod DeNelle.Editor.HeroFacingAudit.MeasureAll
// Menu: Defenders/Art/Measure Hero Facing (all heroes)
// Markers: HERO_FACING_AUDIT_OK <measured>/<total>  |  HERO_FACING_AUDIT_FAIL
// =============================================================================

using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Measures each hero FBX's true forward axis and compares it to the
    /// forward-yaw HeroBodySwapper applies to that class. Reports only (WO-965).</summary>
    public static class HeroFacingAudit
    {
        private const string Tag        = "[HeroFacingAudit] ";
        private const string MarkerOk   = "HERO_FACING_AUDIT_OK";
        private const string MarkerFail = "HERO_FACING_AUDIT_FAIL";

        // Agreement band, same threshold the Ranger builder warns at.
        private const float WarnDegrees = 15f;

        // MIRROR of HeroBodySwapper BuildLegacyResourcesBody for comparison only.
        // WO-966: non-Knight skins at 0 then AlignBodyFacingToRoot (shoulder); Knight stays +15.
        private const float SwapperYawKnight   = 15f;
        private const float SwapperYawNonKnight = 0f;

        private const string HeroDir = "Assets/Resources/Heroes";

        private struct Target
        {
            public string Path;
            public string Label;
            public float AppliedYaw;
            public string AppliedNote;
        }

        private static readonly Target[] Targets =
        {
            new Target { Path = HeroDir + "/Mage.fbx",     Label = "Mage",
                         AppliedYaw = SwapperYawNonKnight, AppliedNote = "non-Knight 0 + AlignBodyFacingToRoot (WO-966)" },
            new Target { Path = HeroDir + "/Ranger.fbx",   Label = "Ranger",
                         AppliedYaw = SwapperYawNonKnight, AppliedNote = "non-Knight 0 + AlignBodyFacingToRoot (WO-966)" },
            new Target { Path = HeroDir + "/KnightV3.fbx", Label = "KnightV3",
                         AppliedYaw = SwapperYawKnight,    AppliedNote = "Knight +15 (Offset Forge locked)" },
        };

        [MenuItem("Defenders/Art/Measure Hero Facing (all heroes)")]
        public static void MeasureAll()
        {
            var report = new StringBuilder();
            int measured = 0;
            int disagreements = 0;

            for (int i = 0; i < Targets.Length; i++)
            {
                var t = Targets[i];
                try
                {
                    if (MeasureOne(t, report, out bool agrees))
                    {
                        measured++;
                        if (!agrees) disagreements++;
                    }
                }
                catch (Exception ex)
                {
                    report.Append(t.Label).Append(": MEASURE THREW (")
                          .Append(ex.GetType().Name).Append(": ").Append(ex.Message).Append("); ");
                    Debug.LogWarning(Tag + t.Label + " could not be measured: " +
                                     ex.GetType().Name + ": " + ex.Message);
                }
            }

            Debug.Log(Tag + "SUMMARY: " + report.ToString());

            if (measured == Targets.Length)
                Debug.Log(MarkerOk + " " + measured + "/" + Targets.Length +
                          " models measured, " + disagreements + " disagree with the applied yaw " +
                          "(reported only - nothing was rotated; the fix, if any, lives in " +
                          "HeroBodySwapper.cs:263 and is an owner call).");
            else
                Debug.LogError(MarkerFail + " only " + measured + "/" + Targets.Length +
                               " hero models could be measured - see the warnings above.");
        }

        /// <summary>Instantiates one FBX, measures its forward via the SHARED
        /// RangerBodyBuilder measurement, and prints measured-vs-applied. Returns true
        /// when the model was measured at all; <paramref name="agrees"/> is whether the
        /// swapper's applied yaw is inside the warn band.</summary>
        private static bool MeasureOne(Target t, StringBuilder report, out bool agrees)
        {
            agrees = true;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(t.Path);
            if (model == null)
            {
                report.Append(t.Label).Append(": MISSING at ").Append(t.Path).Append("; ");
                Debug.LogWarning(Tag + t.Label + " not found at " + t.Path +
                                 " - the hero FBXs are large art assets and may not be synced on " +
                                 "this machine; nothing measured for this class.");
                return false;
            }

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (probe == null)
            {
                report.Append(t.Label).Append(": would not instantiate; ");
                Debug.LogWarning(Tag + t.Label + " would not instantiate from " + t.Path + ".");
                return false;
            }

            try
            {
                var renderers = probe.GetComponentsInChildren<Renderer>(true);
                Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(probe.transform.position, Vector3.one);
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                float yawNeeded = RangerBodyBuilder.MeasureForwardYawNeeded(
                    probe, b, out Vector3 forward, out string source);

                // The number that matters: how far the yaw the swapper ACTUALLY applies sits
                // from the yaw this mesh needs to face world +Z. HeroLocomotion drives the
                // ROOT's +Z, so a non-zero delta here is a body that points off the direction
                // of travel by exactly this many degrees.
                float delta = Mathf.DeltaAngle(t.AppliedYaw, yawNeeded);
                agrees = Mathf.Abs(delta) <= WarnDegrees;

                report.Append(t.Label).Append(": needs ").Append(Num(yawNeeded))
                      .Append(" deg, swapper applies ").Append(Num(t.AppliedYaw))
                      .Append(" deg, DELTA ").Append(Num(delta)).Append(" deg ")
                      .Append(agrees ? "(AGREES)" : "(DISAGREES)").Append("; ");

                string line = Tag + t.Label + " (" + t.Path + "): measured forward " + Fmt(forward) +
                              " via " + source + " -> yaw needed to face world +Z = " + Num(yawNeeded) +
                              " deg. HeroBodySwapper applies " + Num(t.AppliedYaw) + " deg (" +
                              t.AppliedNote + "). DELTA (needed - applied) = " + Num(delta) + " deg" +
                              (renderers.Length == 0 ? " [NO RENDERERS - bounds fallback is meaningless here]" : "") + ".";

                if (agrees) Debug.Log(line + " Within " + Num(WarnDegrees) + " deg - agrees.");
                else Debug.LogWarning(line + " This EXCEEDS the " + Num(WarnDegrees) +
                                      " deg band: the visible body would read " + Num(delta) +
                                      " deg off the direction of travel. REPORTED ONLY - nothing was " +
                                      "rotated. The single place to change it is HeroBodySwapper.cs:263; " +
                                      "baking a rotation into the asset instead would compose with that " +
                                      "yaw and double-rotate the hero.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static string Num(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Fmt(Vector3 v) =>
            "(" + Num(v.x) + ", " + Num(v.y) + ", " + Num(v.z) + ")";
    }
}
