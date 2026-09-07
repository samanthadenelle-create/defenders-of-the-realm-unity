// =============================================================================
// TextureImportBudgetRegression [texture-import-budget] — WO-1485.
// Assembly: DeNelle.EditorRegression.
// Markers: TEXTURE_IMPORT_BUDGET_OK / TEXTURE_IMPORT_BUDGET_FAIL.
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.TextureImportBudgetRegression.RunAll
// Registered in DataRegression.RunAll as the "texture-import-budget suite".
// -----------------------------------------------------------------------------
// WHY THIS SUITE EXISTS — AND WHY IT DOES **NOT** ASSERT "HAS AN ANDROID OVERRIDE".
//
// WO-1485 was minted against `Builds/apk-build.log`: textures were 82% of user
// assets, and the ticket's stated cause was that the ElarionMedieval UI atlas
// "has no Android override at all (`card-frame-empty.png.meta`: overridden: 0,
// crunchedCompression: 0, maxTextureSize: 4096)".
//
// ⛔ THAT DIAGNOSIS WAS WRONG, and a suite written to it would have PASSED on the
// broken tree. Measured at source 2026-09-07: every one of the 73 ElarionMedieval
// textures ALREADY carried `buildTarget: Android / overridden: 1 / maxTextureSize:
// 2048 / crunchedCompression: 1`. The values the ticket quoted are the
// DefaultTexturePlatform block, which sits directly above the Android one in the
// same `platformSettings` list. The override was never missing.
//
// THE REAL CAUSE, measured from the build report rather than inferred:
//
//   Android format Automatic + crunched compression falls back to UNCOMPRESSED
//   RGBA32 whenever the POST-CLAMP dimensions are not both multiples of 4.
//
// The evidence is a clean split across 977 shipped textures in this repo, with no
// exceptions in either direction (bytes-per-pixel from the build report's own
// "Used Assets ... sorted by uncompressed size" table, divided by post-clamp px):
//
//   format Automatic + crunch + dims %4==0      ->  n=37   median 0.195 B/px
//   format Automatic + crunch + dims %4!=0      ->  n=34   median 4.001 B/px  <-- RGBA32
//   format 50 (ASTC) + dims %4==0               ->  n=555  median 0.461 B/px
//   format 50 (ASTC) + dims %4!=0               ->  n=120  median 0.458 B/px  <-- unaffected
//
// The last row is the load-bearing one: an EXPLICIT ASTC format does not care about
// the multiple-of-4 rule, so naming the format is the fix and the mod-4 arithmetic
// never has to be reasoned about again. `card-frame-empty.png` is 1774x887 — 1774
// is not a multiple of 4 — and shipped at 6.0 MB, exactly 4 bytes per pixel.
//
// The clamp is part of the trap and is easy to miss: `button-normal-empty.png` is
// 2172x724. BOTH source dimensions are multiples of 4, but maxTextureSize 2048
// scales it to 2048x683, and 683 is odd. It shipped at 4.048 B/px. Rule 1 therefore
// measures the dimensions AFTER the clamp, never the source ones.
//
// 34 files / 177.2 MB — 23.9% of the entire 740 MB texture budget — were in that
// state. WO-1485 set an explicit `textureFormat: 48` (ASTC, the crisper 4x4 tier,
// measured 1.3 B/px on this repo's existing mipmapped users) on all 34.
//
// -----------------------------------------------------------------------------
// RULES
//
//   1 [rgba32-fallback]  HARD FAIL. A .png under Assets/ whose Android settings are
//        overridden with format Automatic, crunched, and whose POST-CLAMP dimensions
//        are not both multiples of 4. This is the invariant that actually broke, and
//        it is the only one that fails outright: measured 30 offenders above the
//        floor before WO-1485, 0 after.
//
//   2 [android-override]  LEDGER. A texture at or above SizeFloorBytes with NO
//        Android override must be either inside a vendor/staging root or named in
//        AndroidOverrideLedger. FROZEN 2026-09-07 at 91 entries.
//
//   3 [duplicate-content] LEDGER. Two textures at or above HashFloorBytes sharing a
//        content hash must be either tolerated (vendor / staging / .fbm) or named in
//        DuplicateLedger. FROZEN 2026-09-07 at 29 groups / 10.4 MB redundant.
//
//   4 [self-test]  The Rule 1 predicate is a pure function, and this group feeds it
//        the measured cases above — including the known-BAD ones — and requires the
//        right verdict. Project law: "A gate that does not fail the known-bad state
//        is not a gate" (AndroidContentTargetRegression.cs:17-20). Rules 1-3 scan a
//        tree that is currently clean, so without group 4 a suite that had silently
//        stopped classifying anything would still report OK.
//
// ⚠ WHY RULES 2 AND 3 ARE LEDGERS AND NOT HARD FAILS — say the number out loud:
// 2114 textures currently have no Android override and 623 duplicate groups exist.
// Hard-failing either would make DataRegression permanently red for every lane in
// the repo, which is not a gate, it is a broken build. And a COUNT ceiling could not
// be used instead: `Assets/Mirza Beig`, `Assets/Spells Pack`, `Assets/UnityTechnologies`,
// `Assets/Synty`, `Assets/MeshBaker`, `Assets/polyperfect` and `Assets/Tech hud elements`
// are GITIGNORED (CLAUDE.md §4 — re-imported per clone), so any count over them reads
// differently on every machine and the ratchet would be noise. The FILE SET is the
// honest ratchet, exactly as AssetRootsRegression.ArtTokenLedger reasons about the
// same problem. Both ledgers may SHRINK, never grow — every removal is permanent
// progress. A ledger entry naming a file that is not present is tolerated silently
// (the gitignored packs are absent on a fresh clone) and only counted in the reason.
//
// ⚠ RULE 1 IS PNG-ONLY, AND THAT IS DELIBERATE. It needs source dimensions, and the
// only way to get them without loading the asset is to read the file header. Loading
// thousands of textures through AssetDatabase would be slow, and `Texture2D.width`
// reports dimensions for whatever build target batchmode last left ACTIVE — a
// target-dependent reading is worse than no reading in a suite that is specifically
// about the Android platform block. All 34 known offenders are .png. The post-clamp
// rounding here mirrors Unity's observed behaviour rather than its source, which was
// not read: the formula reproduced the 34-file offender set exactly against the build
// report, which is the evidence for it.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
// AssetRoots owns every relocatable art root. Both ledgers below name files under
// StructureContent / EnemyContent, and re-typing either root here is exactly the
// silent-relocation failure [asset-roots] rule 1 exists to catch — it flagged 18 of
// them in this file on Builds/reg-wave10.log before this using was added.
using DeNelle.Core;

namespace DeNelle.Editor.Regression
{
    public static class TextureImportBudgetRegression
    {
        public const string MarkerOk = "TEXTURE_IMPORT_BUDGET_OK";
        public const string MarkerFail = "TEXTURE_IMPORT_BUDGET_FAIL";

        /// <summary>Rule 2 floor: a texture smaller than this on disk is not worth an override row.</summary>
        private const long SizeFloorBytes = 262144;

        /// <summary>Rule 3 floor: below this a duplicate is not worth the hash cost.</summary>
        private const long HashFloorBytes = 65536;

        /// <summary>Rule 1 floor, in POST-CLAMP pixels (256x256). An RGBA32 fallback below this costs &lt;256 KB.</summary>
        private const long PixelFloor = 65536;

        /// <summary>TextureImporterFormat.Automatic. Spelled as the enum below; the value is noted for the .meta reader.</summary>
        private const int FormatAutomatic = -1;

        private static readonly string[] TextureExtensions =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".bmp"
        };

        /// <summary>
        /// Third-party pack and intake roots. Their import settings are the vendor's, most are
        /// GITIGNORED (CLAUDE.md §4), and an intake tree holds a staging copy on purpose — so
        /// neither rule 2 nor rule 3 treats them as debt this repo owns.
        /// </summary>
        private static readonly string[] ToleratedRoots =
        {
            "Assets/Blink/",
            "Assets/Mirza Beig/",
            "Assets/Spells Pack/",
            "Assets/UnityTechnologies/",
            "Assets/Synty/",
            "Assets/MeshBaker/",
            "Assets/Supercyan/",
            "Assets/Tech hud elements/",
            "Assets/3DForge/",
            "Assets/Dragon/",
            "Assets/polyperfect/",
            "Assets/Animals/",
            "Assets/Lana Studio/",
            "Assets/Hovl Studio/",
            "Assets/TextMesh Pro/",
            "Assets/Models/KayKit/",
            // Intake / staging trees — a copy here is the workflow, not duplication debt.
            "Assets/Art/Incoming_Tripo/",
            "Assets/Art/Retired/",
        };

        /// <summary>
        /// Rule 2 ledger, FROZEN 2026-09-07 at 91 entries. Project-relative, forward slashes.
        /// A texture at or above SizeFloorBytes with no Android override that is NOT here and NOT
        /// under a tolerated root FAILS. This list may shrink, never grow.
        /// <para>Assets/Branding/AppIcon.png is a permanent member, not debt: the launcher icon is
        /// consumed by the Android packager, not sampled by a shader, and compressing it would be
        /// wrong rather than merely lossy.</para>
        /// </summary>
        private static readonly string[] AndroidOverrideLedger =
        {
            "Assets/Art/Enemies/ATB/goblin_family.jpg",
            "Assets/Art/Enemies/ATB/orc_mixed_family.jpg",
            "Assets/Art/Enemies/ATB/orc_warband.jpg",
            "Assets/Art/Enemies/ATB/troll_family.jpg",
            "Assets/Art/Hero Select/HeroSelect.jpg",
            "Assets/Art/Heroes/ATB/elara_healer_states.jpg",
            "Assets/Art/Heroes/ATB/grom_knight_states.jpg",
            "Assets/Art/Heroes/ATB/roster_animation_states_gray.jpg",
            "Assets/Art/Title/Title_H.jpg",
            "Assets/Art/Title/Title_L.jpg",
            "Assets/Art/Towers/VikingWatchTower/textures/tower_decorations_BaseColor.png",
            "Assets/Art/Towers/VikingWatchTower/textures/tower_decorations_Normal.png",
            "Assets/Art/Towers/VikingWatchTower/textures/tower_tower_BaseColor.png",
            "Assets/Art/Towers/VikingWatchTower/textures/tower_tower_Normal.png",
            "Assets/Art/UI/HudIcons/hud_widgets_sheet.jpg",
            "Assets/Art/UI/ItemIcons/0D5St.jpg",
            "Assets/Art/UI/ItemIcons/ConsumablesCrafting/bRUz5.jpg",
            "Assets/Art/UI/ItemIcons/ConsumablesCrafting/CtQcX.jpg",
            "Assets/Art/UI/ItemIcons/ConsumablesCrafting/jdRCa.jpg",
            "Assets/Art/UI/ItemIcons/ConsumablesCrafting/P94Fw.jpg",
            "Assets/Art/UI/ItemIcons/inEJH.jpg",
            "Assets/Art/UI/ItemIcons/Ud37F.jpg",
            "Assets/Art/UI/ItemIcons/VxBVb.jpg",
            "Assets/Art/UI/ItemIcons/WRdWM.jpg",
            "Assets/Art/UI/Raids/Raids_banner.jpg",
            "Assets/Art/VFX/Projectiles/projectiles_arrows_magic.jpg",
            "Assets/Art/VFX/Projectiles/projectiles_spell_vfx_lifecycle.jpg",
            "Assets/Branding/AppIcon.png",
            // The ONE genuinely enemy-art row in either ledger, so it asks EnemyArtPaths for both the
            // .fbm sidecar folder and the embedded diffuse stem rather than re-typing them. The other
            // seven token hits in this file are structure/prop trees that merely share the "_basecolor"
            // vocabulary — see the [art-ledger] note on the class.
            EnemyArtPaths.EmbeddedFolder("Skeleton_Healer") + "/" + EnemyArtPaths.EmbeddedDiffuseStem + ".png",
            "Assets/HeroPackages/Knight/Knight_Hero.fbm/Paladin_diffuse.png",
            "Assets/HeroPackages/Knight/Knight_Hero.fbm/Paladin_normal.png",
            "Assets/HeroPackages/Knight/Knight_Hero.fbm/Paladin_specular.png",
            "Assets/HeroPackages/Knight/Textures/Paladin_diffuse.png",
            "Assets/HeroPackages/Knight/Textures/Paladin_normal.png",
            "Assets/HeroPackages/Knight/Textures/Paladin_specular.png",
            "Assets/HeroPackages/Paladin/Paladin_Hero.fbm/Paladin_diffuse.png",
            "Assets/HeroPackages/Paladin/Paladin_Hero.fbm/Paladin_normal.png",
            "Assets/HeroPackages/Paladin/Paladin_Hero.fbm/Paladin_specular.png",
            "Assets/Models/CastleGate/castle+ballast+Tower.fbm/castle+ballast+Tower_basecolor.jpg",
            "Assets/Models/Cathedral/Cathedral.fbm/fantasycathedral3dmodel_basecolor.PNG",
            "Assets/Models/Cathedral/Textures/fantasycathedral3dmodel_basecolor.PNG",
            "Assets/Models/KayKit Adventurers 2.0/contents.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/alternative_textures.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/barbarian.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/barbarian_Large.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/druid.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/engineer.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/knight.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/mage.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/ranger.png",
            "Assets/Models/KayKit Adventurers 2.0/Samples/rogue.png",
            "Assets/Models/People/0_FighterClass_High_High_1024_LOD0.fbm/remesh_12_combined_Bake_Diffuse.png",
            "Assets/Models/People/0_FighterClass_High_High_1024_LOD0.fbm/remesh_12_combined_Bake_Metallic.png",
            "Assets/Models/People/0_FighterClass_High_High_1024_LOD0.fbm/remesh_12_combined_Bake_Normal.png",
            "Assets/Models/People/Blacksmith/Textures/T_Anvil_Base_color.png",
            "Assets/Models/People/Blacksmith/Textures/T_Anvil_Normal_OpenGL.png",
            "Assets/Models/People/Blacksmith/Textures/T_Blacksmith_Base_color.png",
            "Assets/Models/People/Blacksmith/Textures/T_Blacksmith_Normal_OpenGL.png",
            "Assets/Models/People/Blacksmith/Textures/T_Hammer_Base_color.png",
            "Assets/Models/People/Blacksmith/Textures/T_Hammer_Normal_OpenGL.png",
            "Assets/Models/People/Merchant/Textures/T_Merchant_Base_color.png",
            "Assets/Models/People/Merchant/Textures/T_Merchant_Normal_OpenGL.png",
            "Assets/Models/People/Peasant Tob/Textures/T_Peasant_Tob_Base_color.png",
            "Assets/Models/People/Peasant Tob/Textures/T_Peasant_Tob_Normal_OpenGL.png",
            "Assets/Models/People/Peasant/Textures/T_Peasant_Mevina_Base_color.png",
            "Assets/Models/People/Peasant/Textures/T_Peasant_Mevina_Normal_OpenGL.png",
            "Assets/Models/Pet/_archive_raw/sprite.fbm/Color_759cb347-988d-4424-9a9a-a345660243c6.png",
            "Assets/Models/Pet/_archive_raw/sprite.fbm/NormalGL_759cb347-988d-4424-9a9a-a345660243c6.png",
            "Assets/Models/Pet/_archive_raw/sprite.fbm/tripo_image_61f79f65-bbcd-4843-a5a9-915a063faea4_0_Metallic.png",
            "Assets/Models/Pet/_archive_raw/sprite.fbm/tripo_image_61f79f65-bbcd-4843-a5a9-915a063faea4_0_Roughness.png",
            "Assets/Models/Pet/0_Fox_Normal_Normal_512_LOD0.fbm/Coyote_Mesh_Bake_Diffuse.png",
            "Assets/Models/Pet/0_Fox_Normal_Normal_512_LOD0.fbm/Coyote_Mesh_Bake_Normal.png",
            "Assets/Resources/HudIcons/BuildingUpgrades/Upgrade.png",
            "Assets/Resources/HudIcons/Upgrade.png",
            "Assets/Resources/PetPortraits/pet-aether-sprite.png",
            "Assets/Resources/Portraits/Buildings/heart.png",
            "Assets/Resources/VFX/_Shared/Textures/FireFlyAlbedo.tif",
            "Assets/Resources/VFX/_Shared/Textures/FireFlyEmission.tif",
            "Assets/Resources/VFX/_Shared/Textures/SphereNormal.tif",
            AssetRoots.StructureContent + "/armorer.fbm/Armourer_rm.JPEG",
            AssetRoots.StructureContent + "/Ballista_L3.fbm/Ballista_L3_rm.JPEG",
            AssetRoots.StructureContent + "/Forge.fbm/WeaponSmith_rm.JPEG",
            AssetRoots.StructureContent + "/HealingCaravan_Textures/medieval_wagon_3d_model_rm.jpg",
            AssetRoots.StructureContent + "/jeweler.fbm/Jeweler_rm.JPEG",
            AssetRoots.StructureContent + "/RealmStore.fbm/RealmStore_rm.JPEG",
            AssetRoots.StructureContent + "/ShopAndCrafting.fbm/ShopAndCrafting_rm.JPEG",
            AssetRoots.StructureContent + "/Tower_Wooden_Watchtower_L2_Tex/WoodenWatchtowerL2_part_0_basecolor.JPEG",
            AssetRoots.StructureContent + "/Tower_Wooden_Watchtower_L2_Tex/WoodenWatchtowerL2_part_1_basecolor.JPEG",
            AssetRoots.StructureContent + "/Tower_Wooden_Watchtower_L3_Tex/WoodenWatchtowerL3_rm.JPEG",
            AssetRoots.StructureContent + "/Tower_Wooden_Watchtower_Tex/WoodenWatchtower_rm.JPEG",
            AssetRoots.StructureContent + "/Wagon_Tex/medieval_wagon_3d_model_rm.JPEG",
        };

        /// <summary>
        /// Rule 3 ledger, FROZEN 2026-09-07 at 29 groups (10.4 MB redundant source bytes). Each
        /// entry is the group's members, project-relative, sorted ordinally and joined with '|'.
        /// A duplicate group not listed here and not tolerated FAILS. May shrink, never grow.
        /// <para>Two shapes dominate and both are real work for the lead, not false positives: an
        /// authoring copy under Assets/Art next to the live Resources copy, and a hash-named intake
        /// file kept alongside its renamed twin (`Bh1tD.jpg` / `dungeon_backdrop.jpg`). Removing
        /// either is a GUID retarget of the referring material or loader, never a bare delete.</para>
        /// <para>⚠ THE KEY IS SORTED WITH <c>StringComparer.Ordinal</c>, AND THAT BIT ME ONCE —
        /// recorded here because the next seat to regenerate this list will hit the identical trap.
        /// The frozen entries were generated with PowerShell's <c>Sort-Object</c>, which is
        /// CASE-INSENSITIVE by default, while <see cref="CheckDuplicateLedger"/> sorts each group
        /// with <c>StringComparer.Ordinal</c>, where every uppercase letter sorts BEFORE every
        /// lowercase one. The two collations agree on most of these paths and disagree on exactly
        /// two, whose members differ in the case of their first character:
        /// <c>KTj1N.jpg</c>/<c>castle_backdrop.jpg</c> and <c>LugGn.jpg</c>/<c>cavern_backdrop.jpg</c>.
        /// Both groups existed at HEAD and both were in this list — with their halves in the wrong
        /// ORDER, so the key never matched and [duplicate-content] reported them as NEW on
        /// Builds/reg-wave10.log. They are corrected in place below, NOT appended: nothing new was
        /// found, and treating a collation bug as fresh debt would have quietly inflated the frozen
        /// count from 29 to 31 and hidden the real cause. To regenerate, sort with a case-SENSITIVE
        /// ordinal comparer (`Sort-Object -CaseSensitive`, or do it in C#).</para>
        /// </summary>
        private static readonly string[] DuplicateLedger =
        {
            "Assets/_Modules/Onboarding/Art/heart-wing.jpg|Assets/_Modules/Onboarding/Resources/heart-wing.jpg",
            "Assets/Art/Title/Title_L.jpg|Assets/Resources/Title/Title_L.jpg",
            "Assets/Art/Tree_Of_Life/enchantedtree3dmodel_basecolor.JPEG" + "|" + AssetRoots.StructureContent + "/TreeofLife_basecolor.JPEG",
            "Assets/Art/UI/HudIcons/hud_widgets_sheet.jpg|Assets/Art/UI/ItemIcons/ConsumablesCrafting/jdRCa.jpg|Assets/Resources/ItemIcons/jdRCa.jpg",
            "Assets/Art/UI/ItemIcons/0D5St.jpg|Assets/Resources/ItemIcons/0D5St.jpg",
            "Assets/Art/UI/ItemIcons/ConsumablesCrafting/bRUz5.jpg|Assets/Resources/ItemIcons/bRUz5.jpg",
            "Assets/Art/UI/ItemIcons/ConsumablesCrafting/CtQcX.jpg|Assets/Resources/ItemIcons/CtQcX.jpg",
            "Assets/Art/UI/ItemIcons/inEJH.jpg|Assets/Resources/ItemIcons/inEJH.jpg",
            "Assets/Art/UI/ItemIcons/Ud37F.jpg|Assets/Resources/ItemIcons/Ud37F.jpg",
            "Assets/Art/UI/ItemIcons/VxBVb.jpg|Assets/Resources/ItemIcons/VxBVb.jpg",
            "Assets/Art/UI/ItemIcons/WRdWM.jpg|Assets/Resources/ItemIcons/WRdWM.jpg",
            "Assets/Art/UI/Raids/Raids_banner.jpg|Assets/Resources/Raids/Raids_banner.jpg",
            "Assets/Art/VFX/Projectiles/projectiles_arrows_magic.jpg|Assets/Resources/ProjectileIcons/projectiles_arrows_magic.jpg",
            "Assets/Art/VFX/Projectiles/projectiles_spell_vfx_lifecycle.jpg|Assets/Resources/ProjectileIcons/projectiles_spell_vfx_lifecycle.jpg",
            "Assets/Resources/Arena/Backdrops/Bh1tD.jpg|Assets/Resources/Arena/Backdrops/dungeon_backdrop.jpg",
            "Assets/Resources/Arena/Backdrops/c1S70.jpg|Assets/Resources/Arena/Backdrops/forest_backdrop.jpg",
            // Ordinal order: 'K' (0x4B) and 'L' (0x4C) sort BEFORE 'c' (0x63). See the collation
            // note above — these two read the other way round until 2026-09-07.
            "Assets/Resources/Arena/Backdrops/KTj1N.jpg|Assets/Resources/Arena/Backdrops/castle_backdrop.jpg",
            "Assets/Resources/Arena/Backdrops/LugGn.jpg|Assets/Resources/Arena/Backdrops/cavern_backdrop.jpg",
            "Assets/Resources/Arena/Backdrops/MxSKY.jpg|Assets/Resources/Arena/Backdrops/volcanic_backdrop.jpg",
            "Assets/Resources/Arena/Backdrops/PNBkH.jpg|Assets/Resources/Arena/Backdrops/ruins_backdrop.jpg",
            "Assets/Resources/HudIcons/BuildingUpgrades/Upgrade.png|Assets/Resources/HudIcons/Upgrade.png",
            "Assets/Resources/HudIcons/hud_food.png|Assets/Resources/RpgUi/currency/currency_food.png",
            "Assets/Resources/HudIcons/hud_raid.jpg|Assets/Resources/Portraits/barracks.jpg",
            "Assets/Resources/HudIcons/hud_wood.png|Assets/Resources/RpgUi/currency/currency_wood.png",
            "Assets/Resources/RpgUi/crown/crown_perfect.png|Assets/Resources/RpgUi/crown/tier3.png",
            AssetRoots.StructureContent + "/HealingCaravan_Textures/medieval_wagon_3d_model_basecolor.jpg" + "|" + AssetRoots.StructureContent + "/Wagon_Tex/medieval_wagon_3d_model_basecolor.JPEG",
            AssetRoots.StructureContent + "/HealingCaravan_Textures/medieval_wagon_3d_model_metallic.jpg" + "|" + AssetRoots.StructureContent + "/Wagon_Tex/medieval_wagon_3d_model_metallic.JPEG",
            AssetRoots.StructureContent + "/HealingCaravan_Textures/medieval_wagon_3d_model_rm.jpg" + "|" + AssetRoots.StructureContent + "/Wagon_Tex/medieval_wagon_3d_model_rm.JPEG",
            AssetRoots.StructureContent + "/HealingCaravan_Textures/medieval_wagon_3d_model_roughness.jpg" + "|" + AssetRoots.StructureContent + "/Wagon_Tex/medieval_wagon_3d_model_roughness.JPEG",
        };

        // ── the Rule 1 predicate, kept pure so group 4 can prove it ──────────────

        public enum Verdict
        {
            /// <summary>Below the pixel floor, or not a .png — not classified.</summary>
            Skipped,
            /// <summary>No Android override at all. Rule 2's business, not Rule 1's.</summary>
            NoOverride,
            /// <summary>Will compress on Android.</summary>
            Ok,
            /// <summary>Automatic + crunch + post-clamp dims not both %4 — falls back to RGBA32.</summary>
            Rgba32Fallback,
        }

        /// <summary>
        /// The whole of Rule 1, as a pure function of the six values that decide it. Group 4 feeds
        /// this the measured cases; nothing else in the suite decides a Rule 1 verdict.
        /// </summary>
        public static Verdict Classify(bool overridden, int androidFormat, bool crunched,
                                       int sourceWidth, int sourceHeight, int maxTextureSize)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0) return Verdict.Skipped;

            ClampedSize(sourceWidth, sourceHeight, maxTextureSize, out int w, out int h);
            if ((long)w * h < PixelFloor) return Verdict.Skipped;

            if (!overridden) return Verdict.NoOverride;
            if (androidFormat != FormatAutomatic) return Verdict.Ok;
            if (!crunched) return Verdict.Ok;

            return (w % 4 == 0 && h % 4 == 0) ? Verdict.Ok : Verdict.Rgba32Fallback;
        }

        /// <summary>
        /// The same verdict for a real asset on disk, so a SECOND suite never has to re-implement the
        /// multiple-of-4 rule. <see cref="CostFormatSourceRegression"/> calls this: that oracle owns
        /// the ElarionMedieval atlas's import policy, this one owns the compression rule, and a copy
        /// of the rule living in both is exactly the duplicated state CLAUDE.md §5/§8 keeps warning
        /// about. Returns <see cref="Verdict.Skipped"/> for anything whose size cannot be read here
        /// (non-png), which callers must treat as "not judged", never as "fine".
        /// </summary>
        public static Verdict ClassifyAsset(string assetPath, TextureImporterPlatformSettings android)
        {
            if (android == null) return Verdict.Skipped;
            if (!TryReadPngSize(assetPath, out int w, out int h)) return Verdict.Skipped;
            return Classify(android.overridden, (int)android.format, android.crunchedCompression,
                            w, h, android.maxTextureSize);
        }

        /// <summary>
        /// Unity clamps the LONGEST edge to maxTextureSize and scales the other to match. Both edges
        /// matter to Rule 1, because it is the SHORTER one that usually lands on an odd number.
        /// </summary>
        public static void ClampedSize(int srcW, int srcH, int maxSize, out int w, out int h)
        {
            w = srcW; h = srcH;
            if (maxSize <= 0) return;
            int longest = Math.Max(srcW, srcH);
            if (longest <= maxSize) return;
            double scale = (double)maxSize / longest;
            w = (int)Math.Round(srcW * scale, MidpointRounding.AwayFromZero);
            h = (int)Math.Round(srcH * scale, MidpointRounding.AwayFromZero);
            if (w < 1) w = 1;
            if (h < 1) h = 1;
        }

        // ── entry points ────────────────────────────────────────────────────────

        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (!ok) Debug.LogError(MarkerFail + ": " + reason);
            else Debug.Log(MarkerOk + " - " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TEXTURE IMPORT BUDGET (WO-1485) ---");

            List<string> paths = CollectTexturePaths();
            log.AppendLine($"  scanned {paths.Count} texture assets under Assets/ (Packages excluded)");

            int rule1Checked = CheckRgba32Fallback(paths, failures, log);
            int rule2Debt = CheckAndroidOverrideLedger(paths, failures, log);
            int rule3Groups = CheckDuplicateLedger(paths, failures, log);
            CheckSelfTest(failures, log);

            if (failures.Count > 0)
            {
                reason = $"{failures.Count} failure(s). " + string.Join(" | ", failures);
                Debug.Log(log.ToString());
                return false;
            }

            reason = $"rule1 judged {rule1Checked} overridden png; rule2 ledger holds {rule2Debt} " +
                     $"no-override texture(s); rule3 ledger holds {rule3Groups} duplicate group(s); " +
                     "self-test passed.";
            Debug.Log(log.ToString());
            return true;
        }

        // ── rule 1 ──────────────────────────────────────────────────────────────

        private static int CheckRgba32Fallback(List<string> paths, List<string> failures, StringBuilder log)
        {
            int classified = 0;
            var offenders = new List<string>();

            foreach (string path in paths)
            {
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                if (!TryReadPngSize(path, out int srcW, out int srcH)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                if (android == null) continue;

                Verdict v = Classify(android.overridden, (int)android.format, android.crunchedCompression,
                                     srcW, srcH, android.maxTextureSize);
                // Count only what Rule 1 actually JUDGED. A NoOverride png is rule 2's business, and
                // folding ~2000 of them into this number would make the reason line read as far more
                // coverage than the rule has.
                if (v == Verdict.Skipped || v == Verdict.NoOverride) continue;
                classified++;
                if (v != Verdict.Rgba32Fallback) continue;

                ClampedSize(srcW, srcH, android.maxTextureSize, out int w, out int h);
                offenders.Add($"{path} (source {srcW}x{srcH}, max {android.maxTextureSize} -> {w}x{h}; " +
                              $"{(w % 4 != 0 ? "width" : "height")} is not a multiple of 4)");
            }

            if (offenders.Count > 0)
            {
                failures.Add($"[rgba32-fallback] {offenders.Count} texture(s) ship UNCOMPRESSED RGBA32 on " +
                             "Android: format Automatic + crunched compression falls back to raw whenever the " +
                             "post-clamp dimensions are not both multiples of 4. Name an explicit ASTC format " +
                             "in the Android platform block (textureFormat: 48 keeps UI crisp; 50 is ~3x " +
                             "smaller again) and clear crunchedCompression. Offenders: " +
                             string.Join("; ", offenders));
            }
            else
            {
                log.AppendLine($"  [rgba32-fallback] OK - {classified} png classified, none falling back to RGBA32");
            }
            return classified;
        }

        // ── rule 2 ──────────────────────────────────────────────────────────────

        private static int CheckAndroidOverrideLedger(List<string> paths, List<string> failures, StringBuilder log)
        {
            var ledger = new HashSet<string>(AndroidOverrideLedger, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var offenders = new List<string>();
            int tolerated = 0;

            foreach (string path in paths)
            {
                long size = FileLength(path);
                if (size < SizeFloorBytes) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                if (android != null && android.overridden) continue;

                if (IsToleratedRoot(path)) { tolerated++; continue; }
                if (ledger.Contains(path)) { seen.Add(path); continue; }

                offenders.Add($"{path} ({size / 1024} KB)");
            }

            if (offenders.Count > 0)
            {
                failures.Add($"[android-override] {offenders.Count} texture(s) at or above {SizeFloorBytes / 1024} KB " +
                             "carry NO Android platform override and are not on the frozen ledger. Add an Android " +
                             "override (see WO-1485) — the ledger is a shrinking record of existing debt, never a " +
                             "place to park a new entry. Offenders: " + string.Join("; ", offenders));
            }
            else
            {
                int absent = ledger.Count - seen.Count;
                log.AppendLine($"  [android-override] OK - {seen.Count}/{ledger.Count} ledger entries present " +
                               $"({absent} absent, expected: the vendor packs are gitignored), " +
                               $"{tolerated} under tolerated roots");
            }
            return seen.Count;
        }

        // ── rule 3 ──────────────────────────────────────────────────────────────

        private static int CheckDuplicateLedger(List<string> paths, List<string> failures, StringBuilder log)
        {
            // Size-prefilter FIRST. Hashing every texture would read ~15 GB per run; grouping by
            // length collapses that to the few files that could possibly be identical.
            var bySize = new Dictionary<long, List<string>>();
            foreach (string path in paths)
            {
                long size = FileLength(path);
                if (size < HashFloorBytes) continue;
                if (!bySize.TryGetValue(size, out List<string> bucket))
                {
                    bucket = new List<string>();
                    bySize[size] = bucket;
                }
                bucket.Add(path);
            }

            var ledger = new HashSet<string>(DuplicateLedger, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var offenders = new List<string>();
            int groups = 0;
            long hashedBytes = 0;

            foreach (KeyValuePair<long, List<string>> pair in bySize)
            {
                if (pair.Value.Count < 2) continue;

                var byHash = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (string path in pair.Value)
                {
                    string hash = ContentHash(path);
                    if (hash == null) continue;
                    hashedBytes += pair.Key;
                    if (!byHash.TryGetValue(hash, out List<string> g))
                    {
                        g = new List<string>();
                        byHash[hash] = g;
                    }
                    g.Add(path);
                }

                foreach (KeyValuePair<string, List<string>> group in byHash)
                {
                    if (group.Value.Count < 2) continue;
                    groups++;

                    bool anyTolerated = false;
                    foreach (string p in group.Value)
                    {
                        if (IsToleratedRoot(p) || p.IndexOf(".fbm/", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            anyTolerated = true;
                            break;
                        }
                    }
                    if (anyTolerated) continue;

                    group.Value.Sort(StringComparer.Ordinal);
                    string key = string.Join("|", group.Value.ToArray());
                    if (ledger.Contains(key)) { seen.Add(key); continue; }

                    offenders.Add($"{pair.Key / 1024} KB x{group.Value.Count}: {key}");
                }
            }

            if (offenders.Count > 0)
            {
                failures.Add($"[duplicate-content] {offenders.Count} NEW duplicate texture group(s) — byte-identical " +
                             "files at two paths, each shipping its own copy. Retarget the referring material or " +
                             "loader at the surviving GUID and delete the other; never delete first, the reference " +
                             "goes with it. Offenders: " + string.Join("; ", offenders));
            }
            else
            {
                log.AppendLine($"  [duplicate-content] OK - {groups} duplicate group(s) found, " +
                               $"{seen.Count}/{ledger.Count} ledger entries matched, " +
                               $"{hashedBytes / (1024 * 1024)} MB hashed");
            }
            return seen.Count;
        }

        // ── rule 4 — the self-test ──────────────────────────────────────────────

        private static void CheckSelfTest(List<string> failures, StringBuilder log)
        {
            // Every case is a MEASURED file from the WO-1485 audit, with the verdict the build
            // report's bytes-per-pixel proved. If Classify ever stops agreeing with these, the
            // scan above is measuring something other than what this suite claims to measure.
            var cases = new[]
            {
                // overridden, format, crunch, w, h, max, expected, why
                new object[] { true,  -1, true,  1774,  887, 2048, Verdict.Rgba32Fallback,
                    "card-frame-empty.png — 1774 is not a multiple of 4; shipped at 6.0 MB = 4.0 B/px" },
                new object[] { true,  -1, true,  2172,  724, 2048, Verdict.Rgba32Fallback,
                    "button-normal-empty.png — both source edges ARE multiples of 4, but the 2048 clamp " +
                    "makes it 2048x683; shipped at 5.4 MB = 4.048 B/px" },
                new object[] { true,  48, false, 1774,  887, 2048, Verdict.Ok,
                    "the WO-1485 fix — an explicit ASTC format is not subject to the multiple-of-4 rule" },
                new object[] { true,  -1, true,  2048,  768, 2048, Verdict.Ok,
                    "tab-unselected.png — non-power-of-two but both edges %4, so crunch works: 0.131 B/px" },
                new object[] { true,  -1, true,   512,  512, 2048, Verdict.Ok,
                    "Manage/status-available.png — power of two, crunched to 0.17 B/px" },
                new object[] { true,  -1, true,   653,  301, 2048, Verdict.Rgba32Fallback,
                    "cards/bag.png in its PRE-FIX state — 653x301 = 196,553 px, above the 65,536 px floor, " +
                    "and 653 is odd; shipped at 0.75 MB = 4.006 B/px" },
                new object[] { true,  -1, true,   200,  200, 2048, Verdict.Skipped,
                    "40,000 px is below the floor — an RGBA32 fallback there costs 160 KB and is not worth a fail" },
                new object[] { false, -1, true,  4096, 4096, 4096, Verdict.NoOverride,
                    "the Mirza Beig spritesheets — rule 2's business, never rule 1's" },
                new object[] { true,  50, false, 1024,   93, 2048, Verdict.Ok,
                    "Spell 5.png — ASTC on a 93-tall texture, measured 0.438 B/px" },
            };

            int passed = 0;
            foreach (object[] c in cases)
            {
                Verdict got = Classify((bool)c[0], (int)c[1], (bool)c[2], (int)c[3], (int)c[4], (int)c[5]);
                var want = (Verdict)c[6];
                if (got == want) { passed++; continue; }
                failures.Add($"[self-test] Classify(overridden={c[0]}, format={c[1]}, crunch={c[2]}, " +
                             $"{c[3]}x{c[4]}, max={c[5]}) returned {got}, expected {want}. {c[7]}");
            }

            if (passed == cases.Length)
                log.AppendLine($"  [self-test] OK - {passed}/{cases.Length} measured cases classified correctly");
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private static List<string> CollectTexturePaths()
        {
            var result = new List<string>();
            string[] all = AssetDatabase.GetAllAssetPaths();
            foreach (string path in all)
            {
                if (path == null) continue;
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;  // Packages/ is read-only
                string ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext)) continue;
                for (int i = 0; i < TextureExtensions.Length; i++)
                {
                    if (string.Equals(ext, TextureExtensions[i], StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(path);
                        break;
                    }
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static bool IsToleratedRoot(string path)
        {
            for (int i = 0; i < ToleratedRoots.Length; i++)
            {
                if (path.StartsWith(ToleratedRoots[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static long FileLength(string assetPath)
        {
            try
            {
                var info = new FileInfo(assetPath);
                return info.Exists ? info.Length : 0;
            }
            catch { return 0; }
        }

        private static string ContentHash(string assetPath)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (FileStream fs = File.OpenRead(assetPath))
                {
                    byte[] hash = md5.ComputeHash(fs);
                    var sb = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Reads width/height straight out of the PNG IHDR chunk. Deliberately NOT via Texture2D:
        /// the loaded asset's width reflects whatever build target is active, and this rule is
        /// specifically about the Android block.
        /// </summary>
        private static bool TryReadPngSize(string assetPath, out int width, out int height)
        {
            width = 0; height = 0;
            try
            {
                using (FileStream fs = File.OpenRead(assetPath))
                {
                    var head = new byte[24];
                    if (fs.Read(head, 0, head.Length) < head.Length) return false;
                    // PNG signature: 89 50 4E 47 0D 0A 1A 0A
                    if (head[0] != 0x89 || head[1] != 0x50 || head[2] != 0x4E || head[3] != 0x47) return false;
                    width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
                    height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
                    return width > 0 && height > 0;
                }
            }
            catch { return false; }
        }
    }
}
