// =============================================================================
// KayKitNpcImporter -- stages the per-structure KayKit character bodies into
// Resources so the runtime NPC injectors can Resources.Load them "so they load
// neatly" (owner directive).
// -----------------------------------------------------------------------------
// WHY THIS EXISTS: the KayKit packs under Assets/Models/KayKit are GITIGNORED
// (asset-pipeline rule: big art travels by zip, not git). Nothing at runtime may
// reference them directly -- a fresh clone would break. So, exactly like
// PeopleCharacterImporter (heroes/orcs) and BlinkOrcImporter (Blink orcs), this
// tool copies the chosen FBX + texture into Assets/Resources/NPCs/KayKit/
// (AssetDatabase.CopyAsset = fresh GUID, no .meta duplication) and the COPIES
// are committed. The structure -> model mapping will live in
// structures-catalog.json repo.npcModel, and the injectors will
// Resources.Load("NPCs/KayKit/<slug>").
//
// NOTE: this tool does NOT touch structures-catalog.json, RepoProps, or any
// injector -- a separate lane owns those. Runtime scale is also not handled
// here: the drillmaster/vendor injectors' NormalizeToHeroHeight handles KayKit
// native scale at runtime.
//
// Each staged FBX copy is flipped to a Humanoid rig with a model-generated
// avatar (PeopleCharacterImporter.ImportHumanoid pattern, verdict-logged) so
// the shared Action/Mixamo library can retarget onto it.
//
// Idempotent: rows whose staged FBX + texture already match the source byte
// sizes are skipped. Pack absent (fresh clone/CI) => LogWarning + skip, never
// throw; the committed Resources mirror keeps working.
//
// Run headless:  -executeMethod DeNelle.Editor.KayKitNpcImporter.StageAll
// Marker:        KAYKIT_STAGE_OK <ok>/<total>   (KAYKIT_STAGE_PARTIAL if any row missing)
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class KayKitNpcImporter
    {
        private const string StageDir = "Assets/Resources/NPCs/KayKit";

        private const string AdvDir = "Assets/Models/KayKit/KayKit Adventurers 2.0/Characters/fbx";
        private const string Mm4Dir = "Assets/Models/KayKit/KayKit Mystery Monthly Series 4";
        private const string Mm5Dir = "Assets/Models/KayKit/KayKit Mystery Monthly Series 5";
        private const string Mm6Dir = "Assets/Models/KayKit/KayKit Mystery Monthly Series 6";

        private struct NpcRow
        {
            public string StructureId;   // structures-catalog.json id this body will front
            public string Slug;          // Resources.Load("NPCs/KayKit/<Slug>")
            public string FbxPath;       // source FBX under the gitignored pack
            public string TexturePath;   // source texture under the gitignored pack
            public bool   Available;     // false = source known-absent; log + skip, never throw
        }

        // All 12 source paths resolved against the on-disk packs 2026-08-01.
        // Adventurers 2.0 textures live BESIDE the fbx in Characters/fbx (verified);
        // Mystery Monthly textures use the assets/fbx copy (fbx-matched palette),
        // except Farmer_B whose _B texture only ships in the pack's textures/ folder.
        private static readonly NpcRow[] Rows =
        {
            new NpcRow { StructureId = "barracks",             Slug = "Paladin_with_Helmet", Available = true,
                         FbxPath     = Mm4Dir + "/10 - April 2024 - Paladin/characters/fbx/Paladin_with_Helmet.fbx",
                         TexturePath = Mm4Dir + "/10 - April 2024 - Paladin/assets/fbx/paladin_texture_A.png" },
            new NpcRow { StructureId = "workshop",             Slug = "Engineer", Available = true,
                         FbxPath     = AdvDir + "/Engineer.fbx",
                         TexturePath = AdvDir + "/engineer_texture.png" },
            new NpcRow { StructureId = "forge",                Slug = "Barbarian", Available = true,
                         FbxPath     = AdvDir + "/Barbarian.fbx",
                         TexturePath = AdvDir + "/barbarian_texture.png" },
            new NpcRow { StructureId = "armorer",              Slug = "BlackKnight", Available = true,
                         FbxPath     = Mm5Dir + "/3 - September 2024 - Black Knight/characters/BlackKnight.fbx",
                         TexturePath = Mm5Dir + "/3 - September 2024 - Black Knight/assets/fbx/blackknight_texture.png" },
            new NpcRow { StructureId = "jeweler",              Slug = "Tiefling", Available = true,
                         FbxPath     = Mm5Dir + "/12 - June 2025 - Tiefling/characters/Tiefling.fbx",
                         TexturePath = Mm5Dir + "/12 - June 2025 - Tiefling/assets/fbx/tiefling_texture.png" },
            new NpcRow { StructureId = "market",               Slug = "Hoarder", Available = true,
                         FbxPath     = Mm6Dir + "/8 - February 2026 - Hoarder/characters/Hoarder.fbx",
                         TexturePath = Mm6Dir + "/8 - February 2026 - Hoarder/assets/fbx/hoarder_texture.png" },
            new NpcRow { StructureId = "arcane-tower",         Slug = "Mage", Available = true,
                         FbxPath     = AdvDir + "/Mage.fbx",
                         TexturePath = AdvDir + "/mage_texture.png" },
            new NpcRow { StructureId = "pet-house",            Slug = "Druid", Available = true,
                         FbxPath     = AdvDir + "/Druid.fbx",
                         TexturePath = AdvDir + "/druid_texture.png" },
            new NpcRow { StructureId = "collector_farm",       Slug = "Farmer_A", Available = true,
                         FbxPath     = Mm6Dir + "/12 - June 2026 - Farmers/characters/Farmer_A.fbx",
                         TexturePath = Mm6Dir + "/12 - June 2026 - Farmers/assets/fbx/farmer_texture_A.png" },
            new NpcRow { StructureId = "mill",                 Slug = "Farmer_B", Available = true,
                         FbxPath     = Mm6Dir + "/12 - June 2026 - Farmers/characters/Farmer_B.fbx",
                         TexturePath = Mm6Dir + "/12 - June 2026 - Farmers/textures/farmer_texture_B.png" },
            new NpcRow { StructureId = "collector_lumbermill", Slug = "Ranger", Available = true,
                         FbxPath     = AdvDir + "/Ranger.fbx",
                         TexturePath = AdvDir + "/ranger_texture.png" },
            new NpcRow { StructureId = "fountain_healing",     Slug = "Cleric", Available = true,
                         FbxPath     = Mm6Dir + "/3 - September 2025 - Cleric/characters/Cleric.fbx",
                         TexturePath = Mm6Dir + "/3 - September 2025 - Cleric/assets/fbx/cleric_texture.png" },
        };

        [MenuItem("Defenders/Art/Stage KayKit NPC Bodies")]
        public static void StageAllMenu() => StageAll();

        /// <summary>Batchmode entry: -executeMethod DeNelle.Editor.KayKitNpcImporter.StageAll</summary>
        public static void StageAll()
        {
            var report = new List<string>();
            report.Add("=== KayKitNpcImporter (structure NPC bodies -> " + StageDir + ") ===");

            EnsureFolder(StageDir);

            int ok = 0;       // staged this run OR already staged identical
            int missing = 0;  // source absent / row unavailable / copy failed
            foreach (var row in Rows)
            {
                if (StageRow(row, report)) ok++;
                else missing++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string marker = missing == 0
                ? $"KAYKIT_STAGE_OK {ok}/{Rows.Length}"
                : $"KAYKIT_STAGE_PARTIAL {ok}/{Rows.Length} ({missing} missing)";
            Debug.Log("[KayKitNpcImporter] DONE -- " + marker + "\n" + string.Join("\n", report));
        }

        /// <summary>Stage one row: copy FBX + texture into StageDir, flip the FBX
        /// copy to Humanoid (PeopleCharacterImporter.ImportHumanoid pattern).
        /// Returns true when the row is usable (freshly staged or already staged).</summary>
        private static bool StageRow(NpcRow row, List<string> report)
        {
            string label = row.StructureId + " -> " + row.Slug;

            if (!row.Available)
            {
                Debug.LogWarning("[KayKitNpcImporter] " + label + ": marked unavailable -- skipped");
                report.Add("  " + label + ": UNAVAILABLE (source pack lacks this model) -- skipped");
                return false;
            }

            // Skip-if-source-missing (pack gitignored -- absent on fresh clone/CI).
            if (AssetImporter.GetAtPath(row.FbxPath) == null)
            {
                Debug.LogWarning("[KayKitNpcImporter] " + label + ": MISSING SRC FBX " + row.FbxPath +
                                 " (KayKit pack gitignored -- re-import it); staged copy, if committed, keeps working.");
                report.Add("  " + label + ": MISSING src fbx -- skipped");
                return false;
            }
            if (AssetImporter.GetAtPath(row.TexturePath) == null)
            {
                Debug.LogWarning("[KayKitNpcImporter] " + label + ": MISSING SRC TEXTURE " + row.TexturePath + " -- skipped");
                report.Add("  " + label + ": MISSING src texture -- skipped");
                return false;
            }

            string dstFbx = StageDir + "/" + row.Slug + ".fbx";
            string dstTex = StageDir + "/" + Path.GetFileName(row.TexturePath);

            // Skip-if-already-staged-and-identical (byte-size compare, both files).
            if (SameSizeOnDisk(row.FbxPath, dstFbx) && SameSizeOnDisk(row.TexturePath, dstTex))
            {
                report.Add("  " + label + ": skipped (already staged, sizes match) -> " + dstFbx);
                return true;
            }

            // Texture copy (plain CopyAsset; defaults are fine for a KayKit palette png).
            AssetDatabase.DeleteAsset(dstTex);
            if (!AssetDatabase.CopyAsset(row.TexturePath, dstTex))
            {
                Debug.LogWarning("[KayKitNpcImporter] " + label + ": texture COPY FAILED " + row.TexturePath + " -> " + dstTex);
                report.Add("  " + label + ": texture COPY FAILED -- skipped");
                return false;
            }

            // FBX copy (fresh GUID) + flip the COPY to Humanoid; source untouched.
            AssetDatabase.DeleteAsset(dstFbx);
            if (!AssetDatabase.CopyAsset(row.FbxPath, dstFbx))
            {
                Debug.LogWarning("[KayKitNpcImporter] " + label + ": fbx COPY FAILED " + row.FbxPath + " -> " + dstFbx);
                report.Add("  " + label + ": fbx COPY FAILED -- skipped");
                return false;
            }

            var imp = AssetImporter.GetAtPath(dstFbx) as ModelImporter;
            if (imp == null)
            {
                Debug.LogWarning("[KayKitNpcImporter] " + label + ": NO IMPORTER at " + dstFbx);
                report.Add("  " + label + ": NO IMPORTER on staged fbx");
                return false;
            }
            imp.animationType   = ModelImporterAnimationType.Human;
            imp.avatarSetup     = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;   // body mesh carries no clips; Action library retargets on
            imp.SaveAndReimport();

            // Avatar verdict (prove the retarget, PeopleCharacterImporter pattern).
            var go   = AssetDatabase.LoadAssetAtPath<GameObject>(dstFbx);
            var anim = go != null ? go.GetComponentInChildren<Animator>() : null;
            var av   = anim != null ? anim.avatar : null;
            string verdict;
            if (av != null && av.isValid && av.isHuman) verdict = "OK Humanoid avatar (retarget ready)";
            else if (av != null && av.isValid)          verdict = "WARN avatar valid but GENERIC (not human)";
            else                                        verdict = "FAIL no valid avatar -- rig did NOT map";
            report.Add("  " + label + ": staged, " + verdict + " -> " + dstFbx + " (+ " + Path.GetFileName(dstTex) + ")");
            return true;
        }

        /// <summary>True when both asset paths exist on disk with the same byte size.</summary>
        private static bool SameSizeOnDisk(string srcAssetPath, string dstAssetPath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            var src = new FileInfo(Path.Combine(root, srcAssetPath));
            var dst = new FileInfo(Path.Combine(root, dstAssetPath));
            return src.Exists && dst.Exists && src.Length == dst.Length;
        }

        // BlinkOrcImporter.EnsureFolder idiom -- recursive AssetDatabase.CreateFolder.
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
