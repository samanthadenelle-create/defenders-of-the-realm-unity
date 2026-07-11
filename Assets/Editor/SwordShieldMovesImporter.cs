// =============================================================================
// SwordShieldMovesImporter — extracts + retargets the RECOMMENDED subset of the
// ActorCore "Sword and Shield Moves" mocap pack onto the live Knight_Hero
// (Paladin) Humanoid avatar, mirroring the HeroPackageImporter pattern
// (Humanoid + CopyFromOther(Knight_Hero avatar) → standalone .anim extraction
// into Assets/HeroPackages/Knight/Animations/Extracted/ with stable guids).
// -----------------------------------------------------------------------------
// SOURCE (in-project, NOT retargeted until this runs):
//   Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/*.fbx
//   Assets/Action/Knight/Motion/studio-mocap-series-magical-moves/*.fbx (heal pick)
// Unlike HeroPackageImporter this does NOT copy the FBXs — they already live in
// the project; the importer configures each source FBX's ModelImporter in place
// (Humanoid, CopyFromOther hero avatar) and extracts the clip to Extracted/.
//
// SUBSET (Knight_Anim_Inventory.md §2B best-with-import set + owner picks
// 2026-07-11: atk_slashup = W action swing, atk_jump = Heroic Leap,
// atk_shieldswipe01→02 = Block/deflect chain, m-ls-magespellcast-02 = Heal cast):
//   atk_jump, atk_slashup, atk_slashright, atk_slashleft, atk_slashdown,
//   atk_stab, atk_spin, atk_shieldcharge, atk_shieldswipe01, atk_shieldswipe02,
//   m-ls-magespellcast-02 (Magical Moves — first extraction from that pack;
//   subset kept minimal per the caster-pack lane, WO follow-up owns the rest)
//
// NAMING TAXONOMY (established Extracted/ conventions Combat_Weapon_WeaponSkill_*
// for swings, Combat_Spell_* for casts):
//   atk_jump → Combat_Weapon_WeaponSkill_SwordShield_Jump  (etc. — see Subset)
//   m-ls-magespellcast-02 → Combat_Spell_MagicalMoves_SpellCast_02
//
// SELF-VERIFYING (PeopleCharacterImporter verdict precedent): logs one
// OK / WARN / FAIL verdict line per clip + a summary; FAIL never throws — the
// run completes and names every miss.
//
// Idempotent — safe to re-run; existing .anim assets are refreshed in place
// (CopySerialized) so controller references keep their guids.
//
// Run (batchmode, orchestrator-gated):
//   powershell -File ./run-unity-method.ps1 -Method DeNelle.Editor.SwordShieldMovesImporter.Import -LogName sns-subset-import.log
// Or in-editor: Defenders > Heroes > Import Sword And Shield Moves Subset.
// No drag-drop authoring (memory: never-dragdrop-or-manual-playtest).
// =============================================================================
using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class SwordShieldMovesImporter
    {
        private const string SnsDir      = "Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves";
        private const string MagicDir    = "Assets/Action/Knight/Motion/studio-mocap-series-magical-moves";
        private const string PackageRoot = "Assets/HeroPackages/Knight";
        private const string ExtractRoot = PackageRoot + "/Animations/Extracted";
        private const string HeroFbxPath = PackageRoot + "/Knight_Hero.fbx";
        private const string LogPrefix   = "[SwordShieldMovesImporter] ";

        // (source dir, fbx basename, extracted clip name) — the recommended subset.
        // None of these loop (attack/cast one-shots), so loopTime stays false.
        private static readonly (string dir, string fbx, string extracted)[] Subset =
        {
            (SnsDir, "atk_jump",          "Combat_Weapon_WeaponSkill_SwordShield_Jump"),          // owner pick 2026-07-11: Heroic Leap
            (SnsDir, "atk_slashup",       "Combat_Weapon_WeaponSkill_SwordShield_SlashUp"),       // combo finisher (was the W pick; revised to the extracted plain Slash)
            (SnsDir, "atk_slashright",    "Combat_Weapon_WeaponSkill_SwordShield_SlashRight"),
            (SnsDir, "atk_slashleft",     "Combat_Weapon_WeaponSkill_SwordShield_SlashLeft"),
            (SnsDir, "atk_slashdown",     "Combat_Weapon_WeaponSkill_SwordShield_SlashDown"),
            (SnsDir, "atk_stab",          "Combat_Weapon_WeaponSkill_SwordShield_Stab"),
            (SnsDir, "atk_spin",          "Combat_Weapon_WeaponSkill_SwordShield_Spin"),
            (SnsDir, "atk_shieldcharge",  "Combat_Weapon_WeaponSkill_SwordShield_ShieldCharge"),
            (SnsDir, "atk_shieldswipe01", "Combat_Weapon_WeaponSkill_SwordShield_ShieldSwipe01"), // owner pick 2026-07-11: Block beat 1
            (SnsDir, "atk_shieldswipe02", "Combat_Weapon_WeaponSkill_SwordShield_ShieldSwipe02"), // owner pick 2026-07-11: Block beat 2
            // Magical Moves pack (first extraction from that pack — minimal subset):
            (MagicDir, "m-ls-magespellcast-02", "Combat_Spell_MagicalMoves_SpellCast_02"), // owner pick 2026-07-11: Heal cast ("Magic Spell Cast 02", two-hand raise)
            (MagicDir, "m-h-magespellcast-04",  "Combat_Spell_MagicalMoves_SpellCast_04"), // owner pick 2026-07-11: Fireball/ranged-bolt cast ("Magic Spell cast 04", one-hand conjure->release)
        };

        [MenuItem("Defenders/Heroes/Import Sword And Shield Moves Subset")]
        public static void Import()
        {
            var heroAvatar = LoadHeroAvatar();
            if (heroAvatar == null)
            {
                Debug.LogError(LogPrefix + "FAIL — no valid Humanoid avatar in " + HeroFbxPath +
                    "; run DeNelle.Editor.HeroPackageImporter.ImportKnight first. Nothing imported.");
                return;
            }

            EnsureFolder(ExtractRoot);

            int ok = 0, warn = 0, fail = 0;
            foreach (var (dir, fbx, extracted) in Subset)
            {
                string verdict = ImportOne(dir, fbx, extracted, heroAvatar);
                switch (verdict)
                {
                    case "OK":   ok++;   break;
                    case "WARN": warn++; break;
                    default:     fail++; break;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(LogPrefix + "DONE — " + ok + " OK, " + warn + " WARN, " + fail +
                " FAIL of " + Subset.Length + " clip(s) into " + ExtractRoot +
                (fail > 0 ? " — FAILURES ABOVE need attention before the controller bake." : string.Empty));
        }

        // One fbx → extracted .anim. Returns "OK" | "WARN" | "FAIL" (also logged).
        private static string ImportOne(string sourceDir, string fbxBase, string extractedName, Avatar heroAvatar)
        {
            string fbxPath = sourceDir + "/" + fbxBase + ".fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError(LogPrefix + "FAIL " + fbxBase + " — no ModelImporter at " + fbxPath +
                    " (pack file missing?).");
                return "FAIL";
            }

            // Retarget config — mirrors HeroPackageImporter.ConfigureAnimationFbxs:
            // Humanoid + CopyFromOther(Knight_Hero avatar) + importAnimation on.
            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
                importer.sourceAvatar != heroAvatar)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = heroAvatar;
                dirty = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                dirty = true;
            }
            // Attack one-shots never loop.
            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                bool clipDirty = false;
                foreach (var c in clips)
                {
                    if (c.loopTime)
                    {
                        c.loopTime = false;
                        clipDirty = true;
                    }
                }
                if (clipDirty)
                {
                    importer.clipAnimations = clips;
                    dirty = true;
                }
            }
            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log(LogPrefix + "configured " + fbxPath +
                    " (Humanoid, CopyFromOther=" + heroAvatar.name + ", loop=false)");
            }

            // Extract the first non-preview clip sub-asset.
            AnimationClip source = null;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (sub is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    source = clip;
                    break;
                }
            }
            if (source == null)
            {
                Debug.LogError(LogPrefix + "FAIL " + fbxBase +
                    " — no AnimationClip sub-asset after import (retarget failed?).");
                return "FAIL";
            }

            string outPath = ExtractRoot + "/" + extractedName + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (existing != null)
            {
                // Idempotent re-run: refresh in place so controller guids survive.
                EditorUtility.CopySerialized(source, existing);
                existing.name = extractedName;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                var copy = UnityEngine.Object.Instantiate(source);
                copy.name = extractedName;
                AssetDatabase.CreateAsset(copy, outPath);
            }

            // Verdict — humanMotion proves the retarget landed on the Humanoid rig;
            // a zero-length clip is a broken take.
            var extractedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (extractedClip == null)
            {
                Debug.LogError(LogPrefix + "FAIL " + fbxBase + " — extracted asset unreadable at " + outPath);
                return "FAIL";
            }
            if (extractedClip.length <= 0.01f)
            {
                Debug.LogWarning(LogPrefix + "WARN " + fbxBase + " -> " + extractedName +
                    " — clip length " + extractedClip.length.ToString("0.###") + "s (broken take?).");
                return "WARN";
            }
            if (!extractedClip.humanMotion)
            {
                Debug.LogWarning(LogPrefix + "WARN " + fbxBase + " -> " + extractedName +
                    " — clip is NOT humanoid motion (retarget onto " + heroAvatar.name +
                    " may have failed; check the rig mapping).");
                return "WARN";
            }
            Debug.Log(LogPrefix + "OK " + fbxBase + " -> " + outPath +
                " (" + extractedClip.length.ToString("0.##") + "s, humanoid)");
            return "OK";
        }

        // Live hero Humanoid avatar (HeroPackageImporter.ConfigureHeroModel output).
        private static Avatar LoadHeroAvatar()
        {
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(HeroFbxPath))
                if (sub is Avatar av && av.isValid && av.isHuman)
                    return av;
            return null;
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
