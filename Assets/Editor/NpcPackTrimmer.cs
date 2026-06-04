// =============================================================================
// NpcPackTrimmer (WO-93 / Option 1) - drops the dead weight from the People pack
// so it can be committed lean: the duplicate "CGTrader Tob" folder (NpcPackSetup
// already ignores it; canonical "Peasant Tob" is authoritative) and every
// animation FBX the controllers don't use. Everything removed is first copied to
// <repo>/Backups/People_Trim/ (outside Assets, gitignored) so it is fully
// recoverable - and the full set also lives in the original CGTrader purchase.
//
// KEEP set = exactly the clips NpcPackBuild's controllers reference. The 99 AS
// FBX (~171 MB) each embed the mesh; keeping only the used ones drops them to
// ~30-40 MB. Restore any clip by copying it back from Backups/ and re-running
// NpcPackSetup + NpcPackBuild.
//
// Run: Defenders -> NPC Pack - Trim (drop duplicate + unused anim FBX).
// =============================================================================

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class NpcPackTrimmer
    {
        private const string Root = "Assets/Models/People";
        private const string DupeFolder = "Assets/Models/People/CGTrader Tob";

        // Suffixes of the clips the controllers actually drive (NpcPackBuild):
        // Idle_1 + Walk (locomotion via Speed), Talking/Talking2 + Forging.
        private static readonly string[] Keep =
            { "_Idle_1", "_Walk", "_Talking", "_Talking2", "_Forging" };

        [MenuItem("Defenders/NPC Pack - Trim (drop duplicate + unused anim FBX)")]
        public static void Trim()
        {
            string backup = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backups", "People_Trim"));
            Directory.CreateDirectory(backup);

            int removedFbx = 0;
            bool dupeRemoved = false;

            // 1) The duplicate Tob folder.
            if (AssetDatabase.IsValidFolder(DupeFolder))
            {
                CopyDir(Path.GetFullPath(DupeFolder), Path.Combine(backup, "CGTrader_Tob"));
                dupeRemoved = AssetDatabase.DeleteAsset(DupeFolder);
            }

            // 2) Unused animation FBX (keep only controller-referenced clips).
            var asPaths = AssetDatabase.FindAssets("t:Model", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p).StartsWith("AS_", StringComparison.OrdinalIgnoreCase))
                .Distinct().ToList();

            foreach (var p in asPaths)
            {
                var bn = Path.GetFileNameWithoutExtension(p);
                if (Keep.Any(k => bn.EndsWith(k, StringComparison.OrdinalIgnoreCase))) continue;
                File.Copy(Path.GetFullPath(p), Path.Combine(backup, Path.GetFileName(p)), overwrite: true);
                if (AssetDatabase.DeleteAsset(p)) removedFbx++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"TRIM_DONE dupeRemoved={dupeRemoved} removedFbx={removedFbx} backup={backup}");
        }

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }
    }
}
