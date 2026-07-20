// =============================================================================
// SfxResourceMirror (BLIND-02-A01) - one-click mirror of authored/licensed SFX
// into the runtime Resources/Sfx folder under the names the loaders expect.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorAudio   Namespace: DeNelle.Editor.Audio   EDITOR-ONLY
//
// WHY (BLIND-02-A01): the game has real, licensed audio on disk - the ffmpeg-
// processed Freesound combat set (Assets/Audio/SFX/Combat/*.wav) and the imported
// leohpaz "RPG Essentials Sound Effects - FREE" pack (Assets/Leohpaz/...). But the
// RUNTIME only ever loads Resources.Load<AudioClip>("Sfx/<Name>"): a clip that is
// not mirrored into a Resources/Sfx folder under the exact loader name is orphaned,
// and the game plays the procedural synth placeholder (ProceduralSfx / GameSfx
// Generate*). The combat masters were mirrored once by hand (2026-07-02); this tool
// makes that mirror REPEATABLE and extends it to the leohpaz pack so the SfxId synth
// path (ProceduralSfx checks Resources/Sfx/Sfx_<Id> first) upgrades to authored audio.
//
// WHAT IT DOES (all via AssetDatabase.CopyAsset - correct .meta/GUID handling):
//   Pass 1  Combat masters  Assets/Audio/SFX/Combat/<master>.wav
//                        -> Assets/_Modules/Audio/Resources/Sfx/<LoaderName>.wav
//           (the string-name path used by GameSfx / EnemyCombatAudio / HeroLocomotion)
//   Pass 2  leohpaz clips   Assets/Leohpaz/RPG_Essentials_Free/<cat>/<clip>.wav
//                        -> Assets/_Modules/Audio/Resources/Sfx/Sfx_<SfxId>.wav
//           (the ProceduralSfx override path - upgrades the SfxId synth placeholders)
//           plus a couple string-name drops (Heal, EnemyHit) the loaders want.
//
// SAFE + IDEMPOTENT: a source that is not on THIS machine (e.g. leohpaz not yet
// imported) is logged and skipped, never an error. A dest that already exists is
// overwritten so re-running keeps the mirror in sync with updated masters. After
// each copy the AudioImporter is NORMALIZED to carry NO divergent per-platform
// overrides (WO-682 SFX_WEBGL_OK) so the WebGL SFX oracle stays green.
//
// MACHINE-LOCAL CAVEAT: the leohpaz source pack is an Asset Store import; on a fresh
// clone it may be absent (re-import via Package Manager > My Assets). The MIRROR
// COPIES this tool writes into Resources/Sfx SHOULD be committed - then the runtime
// never depends on the source pack being present (same pattern as the committed
// combat mirror + its Assets/Audio/SFX/Combat masters).
//
// RUN (headless, via run-unity-method.ps1):
//   .\run-unity-method.ps1 DeNelle.Editor.Audio.SfxResourceMirror.Mirror
// or in-editor: Defenders > Audio > Mirror SFX to Resources.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Audio
{
    /// <summary>
    /// Mirrors authored/licensed SFX masters into the runtime Resources/Sfx folder
    /// under the loader names, so real audio wins over the synth placeholders
    /// (BLIND-02-A01). Editor-only, idempotent, source-missing-safe.
    /// </summary>
    public static class SfxResourceMirror
    {
        // Where the runtime loads from: Resources.Load<AudioClip>("Sfx/<Name>")
        // resolves to Assets/_Modules/Audio/Resources/Sfx/<Name>.wav.
        private const string RuntimeSfxDir = "Assets/_Modules/Audio/Resources/Sfx";

        private const string CombatMasterDir = "Assets/Audio/SFX/Combat";
        private const string LeohpazRoot = "Assets/Leohpaz/RPG_Essentials_Free";

        // -- Pass 1: combat masters -> runtime string-name clips --------------
        // (sourceFileUnderCombatMasterDir, destFileNameUnderRuntimeSfxDir)
        private static readonly (string src, string dest)[] CombatMirror =
        {
            ("sword_clash_1.wav",      "SwordClash.wav"),
            ("sword_clash_2.wav",      "SwordClash2.wav"),
            ("sword_clash_3.wav",      "SwordClash3.wav"),
            ("sword_clash_4.wav",      "SwordClash4.wav"),
            ("melee_swing.wav",        "SwordSwing.wav"),
            ("sword_draw.wav",         "WeaponDraw.wav"),
            ("cast_spell.wav",         "SpellCast.wav"),
            ("enemy_cast_chant.wav",   "EnemyCastCharge.wav"),
            ("enemy_death.wav",        "EnemyDeath.wav"),
            ("enemy_death_2.wav",      "EnemyDeath2.wav"),
            ("footsteps_walk_loop.wav","FootstepsWalk.wav"),
            ("dragon_roar.wav",        "DragonRoar.wav"),
            ("building_construct.wav", "BuildingUpgrade.wav"),
            ("ui_select.wav",          "UiClick.wav"),
            ("projectile_whoosh_1.wav","TowerArrowHit.wav"),
        };

        // -- Pass 2: leohpaz clips -> SfxId synth-override + named drops -------
        // Targets Sfx_<SfxId> (ProceduralSfx.ResourceName) so PlaySfxAtPosition
        // plays authored audio instead of the synth tone; plus "Heal"/"EnemyHit"
        // for the string-name paths (Motion Caster castHeal row; EnemyCombatAudio).
        // (leohpazRelativePath, destFileNameUnderRuntimeSfxDir)
        private static readonly (string src, string dest)[] LeohpazMirror =
        {
            // SfxId synth-override upgrades (ProceduralSfx checks Sfx_<Id> first).
            ("8_Atk_Magic_SFX/04_Fire_explosion_04_medium.wav", "Sfx_FireExplosion.wav"),
            ("8_Atk_Magic_SFX/13_Ice_explosion_01.wav",         "Sfx_ArcaneExplosion.wav"),
            ("8_Atk_Magic_SFX/30_Earth_02.wav",                 "Sfx_Shockwave.wav"),
            ("8_Buffs_Heals_SFX/02_Heal_02.wav",                "Sfx_Heal.wav"),
            ("8_Atk_Magic_SFX/45_Charge_05.wav",                "Sfx_WizardCast.wav"),
            ("8_Atk_Magic_SFX/25_Wind_01.wav",                  "Sfx_FlameArrowLaunch.wav"),
            ("10_Battle_SFX/22_Slash_04.wav",                   "Sfx_TowerShot.wav"),
            ("10_Battle_SFX/69_Enemy_death_01.wav",             "Sfx_EnemyDeath.wav"),
            ("8_Buffs_Heals_SFX/48_Speed_up_02.wav",            "Sfx_LevelUp.wav"),
            ("10_Battle_SFX/03_Claw_03.wav",                    "Sfx_PetAttack.wav"),
            ("8_Buffs_Heals_SFX/17_Def_buff_01.wav",            "Sfx_WardLit.wav"),
            ("8_Buffs_Heals_SFX/21_Debuff_01.wav",              "Sfx_WardDim.wav"),
            // String-name drops the loaders want (see AUDIO_SME sec 2e / sec 3).
            ("8_Buffs_Heals_SFX/02_Heal_02.wav",                "Heal.wav"),
            ("10_Battle_SFX/15_Impact_flesh_02.wav",            "EnemyHit.wav"),
        };

        [MenuItem("Defenders/Audio/Mirror SFX to Resources")]
        public static void Mirror()
        {
            EnsureFolder(RuntimeSfxDir);

            int copied = 0, skipped = 0, missing = 0;

            copied += MirrorSet("combat", CombatMasterDir, CombatMirror, ref skipped, ref missing);
            copied += MirrorSet("leohpaz", LeohpazRoot, LeohpazMirror, ref skipped, ref missing);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SfxResourceMirror] BLIND-02-A01 done: {copied} clips mirrored into " +
                      $"{RuntimeSfxDir} ({skipped} unchanged, {missing} sources not on this machine - " +
                      "leohpaz likely not imported; re-import via Package Manager > My Assets). " +
                      "Authored clips now WIN over the synth placeholders; commit the copies so a " +
                      "fresh clone does not depend on the source packs.");
        }

        private static int MirrorSet(string label, string srcRoot, (string src, string dest)[] set,
                                     ref int skipped, ref int missing)
        {
            int copied = 0;
            foreach (var (src, dest) in set)
            {
                string srcPath = srcRoot + "/" + src;
                string destPath = RuntimeSfxDir + "/" + dest;

                // Source not on this machine (e.g. leohpaz not imported) - skip, never error.
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(srcPath) == null)
                {
                    missing++;
                    Debug.Log($"[SfxResourceMirror] {label}: source missing, skipped - {srcPath}");
                    continue;
                }

                // Overwrite an existing dest so a re-run re-syncs updated masters.
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(destPath) != null)
                    AssetDatabase.DeleteAsset(destPath);

                if (!AssetDatabase.CopyAsset(srcPath, destPath))
                {
                    skipped++;
                    Debug.LogWarning($"[SfxResourceMirror] {label}: CopyAsset failed {srcPath} -> {destPath}");
                    continue;
                }

                NormalizeImporter(destPath);
                copied++;
            }
            return copied;
        }

        /// <summary>
        /// Strips any per-platform import overrides off the mirrored clip so the WebGL
        /// SFX metas stay override-free (WO-682 SFX_WEBGL_OK). Leaves the default
        /// import settings (2D one-shot, compressed) intact.
        /// </summary>
        private static void NormalizeImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null) return;

            foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone", "iOS" })
                importer.ClearSampleSettingOverride(platform);

            importer.forceToMono = false;
            importer.SaveAndReimport();
        }

        // -- Folder helper (mirrors SfxClipLibraryBuilder.EnsureFolder) -------
        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
