// =============================================================================
// PortalCircleVfxMirror - ships the owner-picked portal-face MAGIC CIRCLE.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// OWNER VFX PICK (2026-08-16, mapped VERBATIM):
//   "Assets\Hovl Studio\Magic circles\Prefabs\Magic circle dark star.prefab"
//   - "use this rotated for the portals"
//   -> the DUNGEON WORLD PORTAL face circle (DungeonWorldPortalSpawner.
//      AttachPortalCircle stands it vertical in the arch opening).
//
// The Hovl pack is GITIGNORED (.gitignore:218), so the prefab must be mirrored
// into the tracked tree to ship - and a naive CopyAsset is NOT enough (the
// WO-1100 / Casting_Fire class of defect: CopyAsset duplicates the PREFAB FILE
// ONLY; its materials/textures keep pointing into the pack and render magenta on
// any machine without the pack). Same two-step shape as TalentPointerVfxMirror:
//
//   STEP 1  CopyAsset the prefab into Assets/Resources/VFX/Portal/
//           PortalCircleDarkStar.prefab (idempotent - an existing mirror is
//           ADOPTED, never re-copied, which preserves its GUID across runs).
//   STEP 2  Run the sanctioned self-containment pass, VfxResourceArtMirror.Run()
//           - it scans EVERYTHING under Assets/Resources/VFX/, so the new mirror
//           is in scope automatically: materials/textures/shaders are mirrored
//           into _Shared/ and re-pointed. (Byte-scan 2026-08-16: this prefab
//           carries NO MonoBehaviours - nothing for the strip pass to lose -
//           and no all-null renderer slots, so [vfx-null-slot] stays green.)
//   VERIFY  Re-measure THIS prefab with the single-home rule
//           (VfxResourceSelfContainmentRegression.PackDependenciesOf) and emit
//           the OK marker ONLY at zero remaining pack dependencies.
//
// RUN:
//   Editor menu : Defenders/VFX/Mirror Portal Circle VFX
//   Batchmode   : DeNelle.Editor.PortalCircleVfxMirror.Run
//   Markers     : PORTAL_CIRCLE_VFX_OK / PORTAL_CIRCLE_VFX_FAIL
//                 (distinct per entry point - 2026-08-02 gate law; the inner
//                  VfxResourceArtMirror pass emits its own VFX_ART_MIRROR_OK/FAIL)
//
// Runtime loads the mirror as Resources "VFX/Portal/PortalCircleDarkStar"
// (DungeonWorldPortalSpawner.CirclePrefabResourcePath) - keep the two in lockstep.
// =============================================================================

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Rule = DeNelle.Editor.Regression.VfxResourceSelfContainmentRegression;

namespace DeNelle.Editor
{
    /// <summary>
    /// Copies the owner-picked Hovl "Magic circle dark star" prefab into the tracked
    /// Resources/VFX tree and self-contains its art via VfxResourceArtMirror. Idempotent.
    /// Prints PORTAL_CIRCLE_VFX_OK only when the mirrored prefab measures ZERO
    /// remaining gitignored-pack dependencies.
    /// </summary>
    public static class PortalCircleVfxMirror
    {
        private const string MarkerOk   = "PORTAL_CIRCLE_VFX_OK";
        private const string MarkerFail = "PORTAL_CIRCLE_VFX_FAIL";
        private const string Tag        = "[PortalCircleVfxMirror] ";

        /// <summary>The owner's verbatim pick (2026-08-16, "use this rotated for the portals").</summary>
        private const string SrcPath =
            "Assets/Hovl Studio/Magic circles/Prefabs/Magic circle dark star.prefab";

        /// <summary>The tracked mirror. Runtime loads it as Resources
        /// "VFX/Portal/PortalCircleDarkStar" (DungeonWorldPortalSpawner) - keep in lockstep.</summary>
        private const string DstPath = "Assets/Resources/VFX/Portal/PortalCircleDarkStar.prefab";

        [MenuItem("Defenders/VFX/Mirror Portal Circle VFX")]
        public static void Run()
        {
            try
            {
                // -- STEP 1: copy the prefab (adopt an existing mirror, GUID preserved) --
                bool existed = File.Exists(AbsoluteOf(DstPath));
                if (existed)
                {
                    Debug.Log(Tag + "mirror already on disk at '" + DstPath +
                              "' - ADOPTED (GUID preserved), no re-copy.");
                }
                else
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(SrcPath) == null)
                        throw new Exception("source prefab would not load: '" + SrcPath +
                            "' - the Hovl Studio pack is not imported on this machine, so the " +
                            "circle cannot be mirrored here. Import the pack and re-run; nothing was faked.");

                    EnsureFolder("Assets/Resources/VFX/Portal");
                    if (!AssetDatabase.CopyAsset(SrcPath, DstPath))
                        throw new Exception("AssetDatabase.CopyAsset('" + SrcPath + "' -> '" +
                                            DstPath + "') returned false.");
                    AssetDatabase.ImportAsset(DstPath, ImportAssetOptions.ForceUpdate);
                    Debug.Log(Tag + "copied '" + SrcPath + "' -> '" + DstPath + "'.");
                }

                int before = Rule.PackDependenciesOf(DstPath).Count;
                Debug.Log(Tag + "BEFORE self-containment: " + before +
                          " reference(s) into gitignored art roots.");

                // -- STEP 2: the sanctioned self-containment pass over the whole VFX tree.
                // The mirror scans everything under Assets/Resources/VFX/, so the new
                // prefab is in scope; it emits its own VFX_ART_MIRROR_OK/FAIL markers.
                VfxResourceArtMirror.Run();

                // -- VERIFY this prefab specifically; no success marker otherwise --------
                var offenders = Rule.PackDependenciesOf(DstPath);
                if (offenders.Count > 0)
                    throw new Exception("'" + DstPath + "' STILL reaches " + offenders.Count +
                        " gitignored asset(s) after the art-mirror pass: " +
                        string.Join(", ", offenders.ToArray()) +
                        " - see the VfxResourceArtMirror output above for the failing step.");

                Debug.Log(MarkerOk + " - '" + DstPath + "' mirrored (packDeps " + before +
                          " -> 0); runtime Resources path 'VFX/Portal/PortalCircleDarkStar' now " +
                          "resolves on a fresh clone.");
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "FAILED: " + e.Message);
                Debug.LogError(MarkerFail + " - " + e.Message);
            }
        }

        private static string AbsoluteOf(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parts = dir.Split('/');
            string cur = parts[0];                       // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
