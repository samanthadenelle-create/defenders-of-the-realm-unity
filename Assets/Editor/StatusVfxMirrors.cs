// =============================================================================
// StatusVfxMirrors — 2026-08-16 wave: the committer's one-pass mirror for the
// seven new VFX Resources copies (ATB status quintet + fire + the caravan ring).
// -----------------------------------------------------------------------------
// Pattern is TalentPointerVfxMirror verbatim: copy-if-absent, then the ONE
// sanctioned self-containment pass (VfxResourceArtMirror scans the whole
// Assets/Resources/VFX tree), then a per-prefab zero-pack-deps verify. The
// naive-file-copy trap (WO-1100 / Casting_Fire class: a copied prefab still
// references materials inside a gitignored pack) is exactly what the verify
// step exists to catch — no success marker unless every mirror measures clean.
//   Run:    Defenders/VFX/Mirror Status VFX (batchmode:
//           DeNelle.Editor.StatusVfxMirrors.Run)
//   Marker: STATUS_VFX_MIRROR_OK <n> clean / STATUS_VFX_MIRROR_FAIL
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Rule = DeNelle.Editor.Regression.VfxResourceSelfContainmentRegression;

namespace DeNelle.Editor
{
    public static class StatusVfxMirrors
    {
        private const string MarkerOk   = "STATUS_VFX_MIRROR_OK";
        private const string MarkerFail = "STATUS_VFX_MIRROR_FAIL";
        private const string Tag        = "[StatusVfxMirrors] ";

        /// <summary>
        /// source (pack) -> dest (tracked Resources). A null source means the dest is
        /// expected to ALREADY be on disk (hand-staged tracked-pack copies) and only
        /// needs the self-containment verify.
        /// <para>
        /// ⚠ DECLARED IN <see cref="Regression.VfxMirrorPairSet"/>, NOT HERE, for the reason
        /// SurfaceImpactVfxMirrors already records: the gate that checks whether the CATALOG
        /// ROWS point at these mirrors lives in DeNelle.EditorRegression, which this assembly
        /// references ONE WAY. A table declared here would be invisible to that gate and would
        /// need a hand-copied twin - the exact drift CLAUDE.md keeps paying for.
        /// </para></summary>
        public static (string src, string dst)[] Mirrors => Regression.VfxMirrorPairSet.StatusPairs;

        [MenuItem("Defenders/VFX/Mirror Status VFX")]
        public static void Run()
        {
            try
            {
                foreach (var (src, dst) in Mirrors)
                {
                    if (File.Exists(Absolute(dst)))
                    {
                        Debug.Log(Tag + "'" + dst + "' already on disk - adopted.");
                        continue;
                    }
                    if (src == null)
                        throw new Exception("expected hand-staged mirror missing: '" + dst + "'.");
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null)
                        throw new Exception("source would not load (pack absent?): '" + src + "'.");
                    EnsureFolder(Path.GetDirectoryName(dst).Replace('\\', '/'));
                    if (!AssetDatabase.CopyAsset(src, dst))
                        throw new Exception("CopyAsset failed: '" + src + "' -> '" + dst + "'.");
                    AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                    Debug.Log(Tag + "copied '" + src + "' -> '" + dst + "'.");
                }

                AssetDatabase.Refresh();

                // The ONE sanctioned self-containment pass over the whole VFX tree.
                VfxResourceArtMirror.Run();

                var dirty = new List<string>();
                foreach (var (_, dst) in Mirrors)
                {
                    var offenders = Rule.PackDependenciesOf(dst);
                    if (offenders.Count > 0)
                        dirty.Add(dst + " (" + offenders.Count + " pack dep(s): " +
                                  string.Join(", ", offenders.ToArray()) + ")");
                }
                if (dirty.Count > 0)
                    throw new Exception(dirty.Count + " mirror(s) still reach gitignored art: " +
                                        string.Join(" | ", dirty.ToArray()));

                Debug.Log(MarkerOk + " " + Mirrors.Length + " clean - every status/ring mirror " +
                          "resolves with zero gitignored-pack dependencies.");
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "FAILED: " + e.Message);
                Debug.LogError(MarkerFail + " - " + e.Message);
            }
        }

        private static string Absolute(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }
}
