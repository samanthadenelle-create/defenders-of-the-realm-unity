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
// LICENCE RE-POINT (2026-08-04): Pass 1 no longer sources the Freesound-derived
// Assets/Audio/SFX/Combat masters - their provenance was checked and proved WRONG
// (docs/SME/AUDIO_SME.md sec 4b), so the clips they mirrored were shipping under an
// unknown licence. Pass 1a now sources the SAME loader names from the licensed
// leohpaz pack; Pass 1b holds the two rows that had no honest equivalent
// (DragonRoar, FootstepsWalk) and is the remaining pre-launch licence blocker.
// Full record: Assets/Audio/SFX/Combat/SOURCE_LICENSE.md.
//
// WHAT IT DOES (all via AssetDatabase.CopyAsset - correct .meta/GUID handling):
//   Pass 1a leohpaz clips   Assets/Leohpaz/RPG_Essentials_Free/<cat>/<clip>.wav
//                        -> Assets/_Modules/Audio/Resources/Sfx/<LoaderName>.wav
//           (the string-name path used by GameSfx / EnemyCombatAudio / HeroLocomotion)
//   Pass 1b Combat masters  Assets/Audio/SFX/Combat/<master>.wav -> same dest folder
//           (ONLY the 2 unresolved rows - unknown licence, tracked, not shipped blind)
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
// MACHINE-LOCAL CAVEAT: the leohpaz source pack is an Asset Store import and is
// GITIGNORED (.gitignore:372 "Assets/Leohpaz/"); on a fresh clone it is absent
// (re-import via Package Manager > My Assets). The MIRROR COPIES this tool writes
// into Resources/Sfx SHOULD be committed - then the runtime never depends on the
// source pack being present.
//
// *** THIS MEANS THE 2026-08-04 LICENCE RE-POINT DOES NOT TAKE EFFECT UNTIL THIS
// TOOL IS RUN ON A MACHINE THAT HAS THE PACK, AND THE RESULTING
// Assets/_Modules/Audio/Resources/Sfx/*.wav ARE COMMITTED. *** Editing the table
// alone changes nothing that ships: the committed Resources copies are still the
// old unknown-provenance bytes until the mirror overwrites them.
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

        // =====================================================================
        // LICENCE RE-POINT (2026-08-04) - see Assets/Audio/SFX/Combat/SOURCE_LICENSE.md
        // ---------------------------------------------------------------------
        // The Assets/Audio/SFX/Combat/*.wav masters have UNKNOWN provenance: the
        // three Freesound IDs their licence file logged were checked and every one
        // resolves to an UNRELATED sound (docs/SME/AUDIO_SME.md sec 4b), and the
        // other 14 WAVs never had an ID logged at all. One of the three checked IDs
        // is CC-BY-NC, which would be unusable commercially. Since the runtime
        // copies in Resources/Sfx are byte-identical to those masters, "the masters
        // don't ship" is NOT a defence - the unknown-licence audio ships.
        //
        // So the string-name combat rows are now sourced from the leohpaz
        // "RPG Essentials Sound Effects - FREE" pack (purchased 2026-06-29, Unity
        // Asset Store Extension EULA: commercial use permitted, NO attribution
        // required). THE LOADER KEYS DID NOT MOVE - game code still calls
        // Resources.Load("Sfx/SwordClash") etc, so every dest filename below is
        // byte-identical to what it was; only the SOURCE path changed.
        //
        // TWO ROWS COULD NOT BE HONESTLY RE-POINTED and still read from the
        // unknown-provenance masters (CombatUnresolvedMirror below) - they are
        // tracked as the remaining licence blocker, NOT quietly mis-mapped.
        // =====================================================================

        // -- Pass 1a: leohpaz -> runtime string-name combat clips --------------
        // (leohpazRelativePath, destFileNameUnderRuntimeSfxDir)
        private static readonly (string src, string dest)[] CombatMirror =
        {
            // Melee clash pool (GameSfx.PlaySwordClash picks 1 of 4 at random).
            ("10_Battle_SFX/39_Block_03.wav",            "SwordClash.wav"),
            ("10_Battle_SFX/22_Slash_04.wav",            "SwordClash2.wav"),
            ("10_Battle_SFX/15_Impact_flesh_02.wav",     "SwordClash3.wav"),
            // Weakest of the four (a dull body-fall thud among three sharper hits,
            // kept for pool variety). If it reads wrong on audition, delete THIS ROW
            // *and* Resources/Sfx/SwordClash4.wav - PlaySwordClash shrinks the pool
            // gracefully, but deleting only the row would strand the old clip on disk.
            ("12_Player_Movement_SFX/45_Landing_01.wav", "SwordClash4.wav"),

            ("12_Player_Movement_SFX/56_Attack_03.wav",  "SwordSwing.wav"),
            ("12_Player_Movement_SFX/61_Hit_03.wav",     "HeroHit.wav"),
            ("10_UI_Menu_SFX/070_Equip_10.wav",          "WeaponDraw.wav"),
            ("8_Atk_Magic_SFX/18_Thunder_02.wav",        "SpellCast.wav"),
            ("8_Atk_Magic_SFX/45_Charge_05.wav",         "EnemyCastCharge.wav"),
            ("10_Battle_SFX/69_Enemy_death_01.wav",      "EnemyDeath.wav"),
            // The free pack contains exactly ONE enemy-death vocalisation, so the
            // second death take is the SAME source. Correct sound, no variety: the
            // 50/50 pick in EnemyCombatAudio.PlayDeath now plays one clip either way.
            // Fix = one clip from leohpaz's paid "90 Retro RPG Battle SFX" pack.
            ("10_Battle_SFX/69_Enemy_death_01.wav",      "EnemyDeath2.wav"),
            // No construction/hammer sound exists in the free pack; this is a
            // "got stronger" swell. Tonally magical rather than structural - audition.
            ("8_Buffs_Heals_SFX/16_Atk_buff_04.wav",     "BuildingUpgrade.wav"),
            ("10_UI_Menu_SFX/013_Confirm_03.wav",        "UiClick.wav"),
            // Arrow striking a body; same source as EnemyHit (Pass 2) by design.
            ("10_Battle_SFX/77_flesh_02.wav",            "TowerArrowHit.wav"),
        };

        // -- Pass 1b: rows with NO honest leohpaz equivalent --------------------
        // These deliberately do NOT read from the leohpaz pack, rather than being
        // forced onto a wrong-sounding clip.
        //
        //   FootstepsWalk  - LICENCE BLOCKER, still the unverified Freesound master.
        //                    HeroLocomotion.cs:707 assigns this to a LOOPING
        //                    AudioSource. The master is a 5.83s stereo multi-step
        //                    walk loop; every leohpaz step is a single 0.67s one-shot,
        //                    which looped becomes a metronomic 1.5 Hz single step with
        //                    no L/R variation - wrong on the game's most-continuous
        //                    sound. Fix = a licensed walk LOOP, or change
        //                    HeroLocomotion to a timed one-shot stepper cycling
        //                    03_Step_grass / 08_Step_rock / 12_Step_wood (code change,
        //                    own WO - out of scope for a mapping-table edit).
        //
        //   DragonRoar     - REPLACED CONCURRENTLY by another seat on 2026-08-04:
        //                    dragon_roar.wav was git-rm'd and dragon_roar.mp3 (65 KB,
        //                    valid ID3) dropped in its place, with DragonRoar.mp3
        //                    already written into Resources/Sfx. This row now points at
        //                    the .mp3 so the mirror does not silently log "source
        //                    missing" and leave the runtime copy unmanaged.
        //                    Resources.Load("Sfx/DragonRoar") is extension-agnostic, so
        //                    the loader key is unaffected.
        //                    *** The .mp3's PROVENANCE IS NOT RECORDED ANYWHERE. ***
        //                    Whoever swapped it must log its source + licence in
        //                    Assets/Audio/SFX/Combat/SOURCE_LICENSE.md before ship -
        //                    an unlabelled replacement is the same blocker in new bytes.
        // (sourceFileUnderCombatMasterDir, destFileNameUnderRuntimeSfxDir)
        private static readonly (string src, string dest)[] CombatUnresolvedMirror =
        {
            ("footsteps_walk_loop.wav","FootstepsWalk.wav"),
            ("dragon_roar.mp3",        "DragonRoar.mp3"),
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

            // Pass 1a: the string-name combat clips, now sourced from the licensed
            // leohpaz pack (2026-08-04 licence re-point).
            copied += MirrorSet("combat", LeohpazRoot, CombatMirror, ref skipped, ref missing);
            // Pass 1b: the two rows with no honest leohpaz equivalent - still the
            // unknown-provenance masters. See the CombatUnresolvedMirror banner.
            copied += MirrorSet("combat-unresolved", CombatMasterDir, CombatUnresolvedMirror,
                                ref skipped, ref missing);
            // Pass 2: SfxId synth-overrides + the named leohpaz drops.
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
