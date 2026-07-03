// =============================================================================
// HeroPackageImporter — imports the owner's dedicated hero animation package
// (Desktop\Animations\Knight) into the project as a self-contained hero package
// under Assets/HeroPackages/Knight/, then EXTRACTS every AnimationClip out of
// the Mixamo FBXs into standalone, controller-referenceable .anim assets.
// -----------------------------------------------------------------------------
// WHY: the raw package ships FBX-embedded clips only (no .meta files, so any
// guid references inside its .controller are dangling). To wire a dedicated
// Knight controller — and later ship the hero as an Addressables package
// (WO-545) — the clips must exist as standalone project assets with stable
// guids, imported HUMANOID against the Knight_Hero avatar so they retarget
// onto the Tripo/Mixamo hero rig.
//
// Steps (all idempotent — safe to re-run after the owner drops new FBXs in):
//   1. Copy the Desktop package into Assets/HeroPackages/Knight/ (skips
//      byte-identical files) and refresh.
//   2. Import config: Knight_Hero.fbx = Humanoid + CreateFromThisModel avatar
//      (the shipped Avatar/Knight_Avatar.asset is a GENERIC avatar — empty
//      m_Human block — so it is NOT used as the humanoid source); every FBX
//      under Animations/ = Humanoid + CopyFromOther(Knight_Hero avatar),
//      loopTime on for locomotion/idle clips by name heuristic.
//   3. Extract: each AnimationClip sub-asset (skip __preview__) is duplicated
//      to Assets/HeroPackages/Knight/Animations/Extracted/<CleanName>.anim.
//      Names are derived from the FBX's path under Animations/ (many files are
//      all called "Death.fbx" and most Mixamo takes are named "mixamo.com" —
//      the file path is the only unique, human-meaningful key).
//
// Run (batchmode, orchestrator-gated):
//   powershell -File ./run-unity-method.ps1 -Method DeNelle.Editor.HeroPackageImporter.ImportKnight -LogName hero-import.log
// Or in-editor: Defenders > Heroes > Import Knight Hero Package.
// No drag-drop authoring (memory: never-dragdrop-or-manual-playtest).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HeroPackageImporter
    {
        private const string SourceDir    = @"C:\Users\Kayden-Laptop\Desktop\Animations\Knight";
        private const string PackageRoot  = "Assets/HeroPackages/Knight";
        private const string AnimRoot     = PackageRoot + "/Animations";
        private const string ExtractRoot  = AnimRoot + "/Extracted";
        private const string HeroFbxPath  = PackageRoot + "/Knight_Hero.fbx";
        private const string LogPrefix    = "[HeroPackageImporter] ";

        // Name-heuristic: clips whose source path contains any of these loop.
        private static readonly string[] LoopHints =
            { "idle", "walk", "run", "sprint", "loop", "locomotion", "aim" };

        [MenuItem("Defenders/Heroes/Import Knight Hero Package")]
        public static void ImportKnight()
        {
            if (!Directory.Exists(SourceDir))
            {
                Debug.LogError(LogPrefix + "source folder not found: " + SourceDir);
                return;
            }

            int copied = CopyPackage();
            AssetDatabase.Refresh();

            var heroAvatar = ConfigureHeroModel();
            int fbxCount = ConfigureAnimationFbxs(heroAvatar);
            int clipCount = ExtractClips();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(LogPrefix + "DONE — copied " + copied + " file(s), configured " +
                fbxCount + " animation FBX(s), extracted " + clipCount +
                " clip(s) into " + ExtractRoot);
        }

        // ---------------------------------------------------------------------
        // Step 1 — copy Desktop package into the project (skip byte-identical).
        // ---------------------------------------------------------------------
        private static int CopyPackage()
        {
            int copied = 0;
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (string src in Directory.GetFiles(SourceDir, "*", SearchOption.AllDirectories))
            {
                if (src.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string rel = src.Substring(SourceDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                string dst = Path.Combine(projectRoot,
                    PackageRoot.Replace('/', Path.DirectorySeparatorChar),
                    rel.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(dst) && FilesIdentical(src, dst)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, true);
                copied++;
                Debug.Log(LogPrefix + "copied " + rel);
            }
            return copied;
        }

        private static bool FilesIdentical(string a, string b)
        {
            var ia = new FileInfo(a);
            var ib = new FileInfo(b);
            if (ia.Length != ib.Length) return false;
            byte[] ba = File.ReadAllBytes(a);
            byte[] bb = File.ReadAllBytes(b);
            for (int i = 0; i < ba.Length; i++)
                if (ba[i] != bb[i]) return false;
            return true;
        }

        // ---------------------------------------------------------------------
        // Step 2a — hero model: Humanoid, avatar created from this model.
        // Returns the generated Avatar (humanoid source for every anim FBX).
        // ---------------------------------------------------------------------
        private static Avatar ConfigureHeroModel()
        {
            var importer = AssetImporter.GetAtPath(HeroFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError(LogPrefix + "no ModelImporter at " + HeroFbxPath +
                    " — did the copy/refresh succeed?");
                return null;
            }

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                dirty = true;
            }
            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log(LogPrefix + "reimported hero model as Humanoid (CreateFromThisModel): " + HeroFbxPath);
            }

            Avatar avatar = null;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(HeroFbxPath))
            {
                avatar = sub as Avatar;
                if (avatar != null) break;
            }
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                Debug.LogError(LogPrefix + "hero avatar missing/invalid/non-humanoid after reimport — " +
                    "humanoid bone auto-map may have failed; open the FBX rig tab and check the mapping. " +
                    "Anim FBXs will fall back to their own humanoid import.");
            else
                Debug.Log(LogPrefix + "hero humanoid avatar ready: " + avatar.name);
            return avatar;
        }

        // ---------------------------------------------------------------------
        // Step 2b — every FBX under Animations/: Humanoid, CopyFromOther(hero
        // avatar), loop heuristic, animation import on.
        // ---------------------------------------------------------------------
        private static int ConfigureAnimationFbxs(Avatar heroAvatar)
        {
            int count = 0;
            foreach (string assetPath in AllAnimationFbxPaths())
            {
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning(LogPrefix + "no ModelImporter for " + assetPath + " — skipped.");
                    continue;
                }

                bool dirty = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    dirty = true;
                }
                if (heroAvatar != null)
                {
                    if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                        importer.sourceAvatar != heroAvatar)
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        importer.sourceAvatar = heroAvatar;
                        dirty = true;
                    }
                }
                if (!importer.importAnimation)
                {
                    importer.importAnimation = true;
                    dirty = true;
                }

                // Loop flag by name heuristic (file path is more reliable than
                // the take name — Mixamo takes are all "mixamo.com").
                bool shouldLoop = HasLoopHint(assetPath);
                var clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    bool clipDirty = false;
                    foreach (var c in clips)
                    {
                        if (c.loopTime != shouldLoop)
                        {
                            c.loopTime = shouldLoop;
                            clipDirty = true;
                        }
                    }
                    // Only take over clipAnimations when the loop flag actually
                    // needs to differ from the default — keeps untouched FBXs
                    // on their default import.
                    if (clipDirty)
                    {
                        importer.clipAnimations = clips;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    Debug.Log(LogPrefix + "configured " + assetPath +
                        " (Humanoid, CopyFromOther, loop=" + shouldLoop + ")");
                }
                count++;
            }
            return count;
        }

        private static bool HasLoopHint(string path)
        {
            string lower = path.ToLowerInvariant();
            foreach (string hint in LoopHints)
                if (lower.Contains(hint)) return true;
            return false;
        }

        // ---------------------------------------------------------------------
        // Step 3 — extract each AnimationClip sub-asset to a standalone .anim.
        // ---------------------------------------------------------------------
        private static int ExtractClips()
        {
            EnsureFolder(ExtractRoot);

            int extracted = 0;
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string assetPath in AllAnimationFbxPaths())
            {
                string baseName = CleanNameFor(assetPath);
                int clipIndexInFbx = 0;
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    var clip = sub as AnimationClip;
                    if (clip == null) continue;
                    if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;

                    string name = clipIndexInFbx == 0 ? baseName : baseName + "_" + clipIndexInFbx;
                    clipIndexInFbx++;
                    // Guard collisions across folders (many Death.fbx variants).
                    string unique = name;
                    int n = 2;
                    while (!usedNames.Add(unique)) unique = name + "_" + n++;

                    string outPath = ExtractRoot + "/" + unique + ".anim";
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
                    if (existing != null)
                    {
                        // Idempotent re-run: refresh the existing asset in place
                        // so controller references to it keep their guid.
                        EditorUtility.CopySerialized(clip, existing);
                        existing.name = unique;
                        EditorUtility.SetDirty(existing);
                    }
                    else
                    {
                        var copy = UnityEngine.Object.Instantiate(clip);
                        copy.name = unique;
                        AssetDatabase.CreateAsset(copy, outPath);
                    }
                    extracted++;
                    Debug.Log(LogPrefix + "extracted clip '" + clip.name + "' from " +
                        assetPath + " -> " + outPath);
                }
            }
            return extracted;
        }

        // Unique, readable name from the path under Animations/, e.g.
        // "Passive/Reaction/Hit Reaction.fbx" -> "Passive_Reaction_Hit_Reaction".
        private static string CleanNameFor(string assetPath)
        {
            string rel = assetPath.StartsWith(AnimRoot + "/")
                ? assetPath.Substring(AnimRoot.Length + 1)
                : Path.GetFileName(assetPath);
            rel = rel.Substring(0, rel.Length - Path.GetExtension(rel).Length);
            var sb = new System.Text.StringBuilder(rel.Length);
            foreach (char ch in rel)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            string clean = sb.ToString();
            while (clean.Contains("__")) clean = clean.Replace("__", "_");
            return clean.Trim('_');
        }

        private static IEnumerable<string> AllAnimationFbxPaths()
        {
            if (!AssetDatabase.IsValidFolder(AnimRoot)) yield break;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    yield return path;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
        }
    }
}
