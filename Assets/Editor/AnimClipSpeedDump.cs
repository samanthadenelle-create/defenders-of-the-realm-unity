using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// FOOT-SKATE MEASURE — authored-stride side (owner 2026-07-04, gates the KnightMocap builder).
    ///
    /// For the KnightMocap + orc locomotion clips, logs each clip's AUTHORED root speed:
    ///   clip.apparentSpeed        — Unity's estimate of the m/s the clip's root moves (the
    ///                               "how fast the stride reads" number),
    ///   clip.averageSpeed         — the mean root-velocity vector; .magnitude = authored m/s,
    ///   clip.length               — clip duration in seconds.
    /// Note: the Action clips are imported IN PLACE (ActionClipImporter bakes horizontal root
    /// translation into the pose — lockRootPositionXZ=true), so apparentSpeed/averageSpeed report
    /// the AUTHORED stride the runtime blend tree is tuned against; the RUNTIME side (actual travel
    /// m/s) is the `[Flow:HeroLoco]` / `[Flow:EnemyLoco]` `vel=` field. The gap between them = foot-skate.
    ///
    /// One clean marker line per clip so a headless run can grep it:
    ///   [AnimClipSpeedDump] CLIP_SPEED group=... file='...' clip='...' apparentSpeed=.. avgSpeed=.. length=..s
    /// plus a CLIP_SPEED_BEGIN / CLIP_SPEED_END pair bracketing the run.
    ///
    /// Run headless:
    ///   -executeMethod DeNelle.Editor.AnimClipSpeedDump.Dump
    /// or menu: Defenders/Animation/Dump Locomotion Clip Speeds (foot-skate).
    /// </summary>
    public static class AnimClipSpeedDump
    {
        // KnightMocap locomotion clips (studio-mocap sword-and-shield set) — the LIVE hero path.
        private const string KnightDir = "Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/";
        private static readonly string[] KnightLocoFbx =
        {
            KnightDir + "idle_ready.fbx",
            KnightDir + "walkforward01.fbx",
            KnightDir + "runforward_218667.fbx",
        };

        // Orc locomotion clips — the shared OrcHumanoid loco tree (see BuildOrcHumanoidController:
        // IdleFbx / WalkFbx / RunFbx). These are the authored strides the orc blend bands tune to.
        private static readonly string[] OrcLocoFbx =
        {
            "Assets/Action/Orc Idle.fbx",
            "Assets/Action/standing walk forward.fbx",
            "Assets/Action/standing run forward.fbx",
        };

        [MenuItem("Defenders/Animation/Dump Locomotion Clip Speeds (foot-skate)")]
        public static void Dump()
        {
            Debug.Log("[AnimClipSpeedDump] CLIP_SPEED_BEGIN — authored stride m/s for KnightMocap + orc locomotion clips.");
            int n = 0;
            n += DumpGroup("knight", KnightLocoFbx);
            n += DumpGroup("orc", OrcLocoFbx);
            Debug.Log($"[AnimClipSpeedDump] CLIP_SPEED_END — dumped {n} clip(s).");
        }

        /// <summary>Dump every AnimationClip sub-asset found in each FBX path for a group.</summary>
        private static int DumpGroup(string group, string[] fbxPaths)
        {
            int count = 0;
            foreach (var path in fbxPaths)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets == null || assets.Length == 0)
                {
                    Debug.LogWarning($"[AnimClipSpeedDump] CLIP_SPEED_MISSING group={group} file='{path}' (asset not found / not imported)");
                    continue;
                }

                bool foundClip = false;
                foreach (var a in assets)
                {
                    if (a is AnimationClip c && !c.name.StartsWith("__preview"))
                    {
                        foundClip = true;
                        count++;
                        Debug.Log(
                            $"[AnimClipSpeedDump] CLIP_SPEED group={group} file='{path}' clip='{c.name}' " +
                            $"apparentSpeed={c.apparentSpeed:F3} avgSpeed={c.averageSpeed.magnitude:F3} " +
                            $"avgVec=({c.averageSpeed.x:F3},{c.averageSpeed.y:F3},{c.averageSpeed.z:F3}) " +
                            $"length={c.length:F3}s");
                    }
                }
                if (!foundClip)
                    Debug.LogWarning($"[AnimClipSpeedDump] CLIP_SPEED_NOCLIP group={group} file='{path}' (no AnimationClip sub-asset)");
            }
            return count;
        }
    }
}
