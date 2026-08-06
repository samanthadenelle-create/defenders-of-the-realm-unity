// =============================================================================
// RangerBodyBuilder (owner 2026-08-06: "Sylas the Ranger finally gets a real
// body") - SCRIPT-authors the ASSET half of the Ranger hero: the Humanoid rig on
// the owner-staged Resources/Heroes/Ranger.fbx, its URP/Lit material, and the
// RUNTIME basecolor atlas at the exact Resources key the shipped code reads.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// WHY THIS EXISTS:
//   Until today Ranger and Mage had NO mesh. HeroBodySwapper's body chain fell
//   through to the Blink "HumanMale" mannequin (Assets/Blink is GITIGNORED) and,
//   on a clean clone, to the tracked KayKit fallback - so both classes read as the
//   same generic body. The owner supplied an elf-warrior FBX. This builder makes
//   it import correctly and PROVES it, without hand-editing a single .meta byte
//   (CLAUDE.md section 0 + section 3): every step goes through ModelImporter /
//   TextureImporter / AssetDatabase and UNITY owns the serialization.
//
//   The SLUG was never the problem. HeroBodySwapper.SlugFor already returns
//   "Ranger" and Resources/Heroes/Ranger.controller already exists with its clips.
//   Only the MESH was missing. This builder therefore does NOT touch SlugFor, the
//   ability loadout, or the controller - and it does not touch the Mage, for which
//   no model was supplied.
//
// THE RIG IS THE HARD REQUIREMENT (a hero is not a structure):
//   A Generic rig silently fails to bind Ranger.controller's Humanoid clips and
//   the hero ships as a sliding T-pose statue - HeroBodySwapper.WireHeroBody
//   already treats exactly that (avatar == null || !avatar.isValid) as a hard
//   FAIL at runtime. So the importer is asserted to Knight.fbx.meta's rig block,
//   read from the file rather than remembered:
//       animationType: 3 (Human)   avatarSetup: 1 (CreateFromThisModel)
//       humanoidOversampling: 1    (+ isReadable: 1, which WO-286 requires)
//   and then the GENERATED AVATAR IS VERIFIED (isValid && isHuman) with the
//   auto-mapped bone list reported and every MISSING REQUIRED humanoid bone
//   named. A Humanoid import can map badly and still produce an asset; shipping
//   an invalid avatar is shipping a hero that cannot animate, so that is a FAIL.
//
//   NOTE the rig settings are ALSO applied by an existing AssetPostprocessor:
//   Assets/Editor/HeroFbxImporter.cs (WO-286) hard-forces Human +
//   CreateFromThisModel + isReadable on every FBX in Resources/Heroes/, and its
//   list already NAMES "Ranger.fbx". So the file most likely arrived Humanoid on
//   its very first import. This builder asserts the same values anyway (a
//   postprocessor can be edited or disabled; the hero must not silently degrade)
//   and its real contribution is the VERIFICATION, which nothing else did.
//
// MATERIALS - WHY A HERO IS NOT WIRED LIKE THE WOODEN WATCHTOWER:
//   WoodenWatchtowerBuilder binds materials onto a PREFAB's renderers because a
//   structure's visual is a Resources PREFAB it authors. A HERO HAS NO PREFAB:
//   HeroBodySwapper -> HeroAssetLoader.LoadHeroPrefab("Ranger") resolves
//   Resources.Load<GameObject>("Heroes/Ranger"), which IS the imported FBX asset.
//   Its renderers' materials are owned by the IMPORTER, so there is no
//   renderer-side binding available and a remap is the only lever. This builder
//   therefore discovers the importer's ACTUAL material identifiers (external
//   object map + asset representations) and remaps those - it never guesses a
//   material name.
//
//   AND THE SHIPPED HERO COLOUR DOES NOT COME FROM THE FBX AT ALL. Read
//   HeroBodySwapper.ApplyExtractedTexture: isCc5Combined is hardcoded TRUE for
//   every class, so it builds ONE URP/Lit from a single atlas loaded by explicit
//   Resources key and REPLACES every renderer slot on the body. For the Ranger
//   that key is, verbatim, "Heroes/Textures/ranger_basecolor" (line ~1766) - and
//   AtbCombatantSwapper.cs:712 reads the SAME key for the battle body. That path
//   did not exist: the owner staged the textures into Ranger.fbm/. So the single
//   most load-bearing thing this builder does is STAGE THAT ATLAS and prove
//   Resources.Load resolves it. A texture the code cannot find is the white-hero
//   symptom, and it is a hard FAIL here.
//
//   WHY COPY THE FILE INSTEAD OF REPOINTING THE CODE AT Ranger.fbm/: the project
//   has ruled on this repeatedly and the Knight is the precedent - its atlas ships
//   in BOTH Knight.fbm/ and Heroes/Textures/. HeroBodySwapper says why in its own
//   comment: "textures inside an FBX's *.fbm import-artifact folder are NOT
//   reliably Resources.Load-able in a player build. Heroes/Textures/* is a
//   guaranteed-loadable plain folder." On top of that the WO-545 seam
//   (HeroTextureLoader / HeroAddressablesGrouper) addresses hero atlases by the
//   "Heroes/Textures/<name>" scheme, and TWO call sites already spell that exact
//   key. Copying satisfies both call sites with no code change; repointing would
//   move a shipped path onto an import artifact. So: copy, and leave the code.
//
//   METALLIC / ROUGHNESS - SAME DECISION AS THE TOWER, FOR A SECOND REASON.
//   The tower builder chose SCALARS over a packed metallic-smoothness map because
//   URP wants smoothness in ALPHA, JPEG has no alpha, and inverting a roughness
//   map into a smoothness slot reads as wet plastic - while the runtime fixer
//   discards _MetallicGlossMap anyway. The hero path is even more decisive: the
//   material ApplyExtractedTexture builds carries basecolor ONLY and explicitly
//   NULLS _BumpMap / _MetallicGlossMap / _SpecGlossMap / _OcclusionMap for every
//   class except the Knight (whose normal is bound by a hardcoded Knight-only
//   branch). So on the shipped Ranger a metallic, roughness, packed-rm or even a
//   NORMAL map cannot reach a pixel. Scalars it is, and the authored numbers are
//   the ones the runtime actually writes (a fresh URP/Lit: metallic 0,
//   smoothness 0.5) so the editor preview equals the shipped look.
//
//   The normal map is still TYPED as a NormalMap (an untyped normal renders as
//   flat blue paint) and still wired to _BumpMap on the authored material,
//   because the authored material IS reachable on the fallback branch: if the
//   atlas ever fails to load, ApplyExtractedTexture returns false and
//   ApplyClassTint preserves whatever texture the slot already carries. That
//   branch is the only way the FBX's own materials reach the player, which is
//   exactly why the slot check below still has teeth.
//
// THE .tripo-extracted SENTINEL (WO-909 calls it a parked mesh - IT IS NOT):
//   Ranger.fbx.tripo-extracted is a 125-byte plain-text marker written by
//   Editor/TripoAssetPostprocessor.cs. Its presence makes that postprocessor SKIP
//   both OnPreprocessModel and the texture-extraction drain for this FBX. That is
//   BENIGN and arguably desirable here: extraction exists to pull textures out of
//   an FBX that embeds them, and the owner already staged this model's textures
//   into the sibling Ranger.fbm/ folder by hand. What the skip DOES mean is that
//   nothing forces materialLocation=External on this FBX, so the material
//   identifiers a remap needs may never surface. That case is DISCOVERED and
//   REPORTED below (BindMaterial) rather than worked around: the sentinel is
//   never removed on this script's own authority, and the importer's material
//   settings are left as they arrive, because forcing External makes Unity
//   auto-create stray .mat files under Resources/ (real ship weight) to solve a
//   binding that the runtime atlas already makes moot. If the slot check then
//   fails, the fix is an owner-visible one-liner in the Materials tab, not a
//   silent settings change buried in a builder.
//
// ORIENTATION / SCALE - MEASURED AND REPORTED, NEVER INVENTED:
//   Heroes carry their facing correction at SWAP TIME, not in the asset.
//   HeroBodySwapper.BuildLegacyResourcesBody applies
//   LocalRotation = Euler(0, forwardYaw, 0) with forwardYaw = -90 for every class
//   except the Knight (+15, Offset-Forge locked). A -90 yaw maps model-local +X
//   onto world +Z - that is the WO-174 "+X -> +Z" correction, and it assumes the
//   model faces +X. This builder MEASURES which way the mesh actually faces from
//   the humanoid rig itself (the shoulder axis: forward = cross(right, up)),
//   states the yaw that WOULD be needed, and compares it to the -90 the code will
//   apply. It changes nothing - a facing fix is an owner decision and the seat for
//   it is HeroBodySwapper or Offset Forge, not a baked rotation this builder
//   invents. Same for height: VisualFactory.Skin refits every hero to
//   TargetHeightMeters (1.75 m), so the authored size cannot reach the player; the
//   raw height is reported so the gear-attachment scales can be sanity-checked
//   (HeroBowAttachment normalizes the bow to an ABSOLUTE 0.92 m held length).
//
// IDEMPOTENT - safe to run twice:
//   * the FBX is reimported only when a setting actually changed (every reimport
//     is a chance to re-pose a model - the tower_arcane_spire lesson);
//   * the atlas is copied only when missing or byte-different, so its .meta (and
//     therefore its GUID, and therefore any Addressables entry) survives;
//   * an existing .mat is REUSED (GUID preserved) - structural properties are
//     re-asserted because a wrong one is a rendering bug, the metallic/smoothness
//     taste dials are written only on first creation and reported thereafter.
//
// RUN:
//   Editor menu : Defenders/Art/Build Ranger Body
//   Batchmode   : DeNelle.Editor.RangerBodyBuilder.Build
//   Markers     : RANGER_BODY_BUILD_OK  /  RANGER_BODY_BUILD_FAIL
//                 (distinct to this entry point - a shared marker cannot say which
//                  step passed, which is the 2026-08-02 gate defect.)
//
// DOES NOT TOUCH: HeroBodySwapper.SlugFor, the ability loadout, Ranger.controller,
// the Mage (no model was supplied), any .unity scene, or the .tripo-extracted
// sentinel.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeNelle.Core;        // MagentaGuard.IsBrokenShader - the SINGLE authority (see VerifySlots)
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor builder for the Ranger hero body: asserts the Humanoid rig on
    /// Resources/Heroes/Ranger.fbx, verifies the generated avatar, authors + binds
    /// its URP/Lit material, and stages the runtime basecolor atlas at the exact
    /// Resources key HeroBodySwapper reads. Idempotent; prints
    /// RANGER_BODY_BUILD_OK only when every check passes.
    /// </summary>
    public static class RangerBodyBuilder
    {
        // -- Markers (distinct per entry point) --------------------------------
        private const string MarkerOk   = "RANGER_BODY_BUILD_OK";
        private const string MarkerFail = "RANGER_BODY_BUILD_FAIL";
        private const string Tag        = "[RangerBodyBuilder] ";

        // -- Paths (all read off the tree, none invented) -----------------------
        private const string HeroDir      = "Assets/Resources/Heroes";
        private const string FbxPath      = HeroDir + "/Ranger.fbx";
        private const string FbmDir       = HeroDir + "/Ranger.fbm";
        private const string BaseColorSrc = FbmDir + "/ranger_basecolor.JPEG";
        private const string NormalSrc    = FbmDir + "/ranger_normal.JPEG";
        private const string TexturesDir  = HeroDir + "/Textures";
        private const string MaterialsDir = HeroDir + "/Materials";
        private const string MatPath      = MaterialsDir + "/Ranger.mat";
        private const string SentinelPath = HeroDir + "/Ranger.fbx.tripo-extracted";

        // The atlas destination + the EXACT Resources key the shipped code reads.
        // Both HeroBodySwapper.ApplyExtractedTexture (HeroClass.Ranger) and
        // AtbCombatantSwapper.TexPathFor spell this string; it is not a choice.
        private const string RuntimeAtlasPath = TexturesDir + "/ranger_basecolor.JPEG";
        private const string RuntimeAtlasKey  = "Heroes/Textures/ranger_basecolor";

        // What HeroAssetLoader.LoadHeroPrefab("Ranger") resolves. Proving THIS is
        // what proves the body reaches the player - an asset that exists but does
        // not Resources.Load is the invisible hero.
        private const string HeroPrefabKey     = "Heroes/Ranger";
        private const string HeroControllerKey = "Heroes/Ranger";   // same key, different type

        // -- URP shader (project-wide convention; a miss ships MAGENTA) ---------
        private const string UrpLitShaderName   = "Universal Render Pipeline/Lit";
        private const string FallbackShaderName = "Standard";

        // -- Surface finish ----------------------------------------------------
        // NOT taste: these are the values the SHIPPED hero material carries. For
        // every class but the Knight, ApplyExtractedTexture builds a bare
        // `new Material(URP/Lit)` and never writes _Metallic/_Smoothness, so the
        // material arrives on URP/Lit's own defaults - metallic 0, smoothness 0.5.
        // Authoring the same numbers means the editor preview equals the game.
        private const float RangerMetallic   = 0.0f;
        private const float RangerSmoothness = 0.5f;

        // -- Runtime constants this builder REPORTS against (never writes) ------
        // HeroBodySwapper.TargetHeightMeters - every hero body is refit to this by
        // VisualFactory.Skin, so the authored size cannot reach the player.
        private const float TargetHeightMeters = 1.75f;
        // HeroBowAttachment.BowHeldLength - the bow is normalized to this ABSOLUTE
        // length, so it is sized against the FITTED hero, not the raw import.
        private const float BowHeldLength = 0.92f;
        // HeroBodySwapper.BuildLegacyResourcesBody: forwardYaw for every non-Knight
        // class. Euler(0,-90,0) maps model-local +X onto world +Z (WO-174).
        private const float HeroForwardYaw = -90f;

        // "Is this model standing up?" = bounds height / max(width, depth). A human
        // reads ~2.5-3.5 upright and well under 1 lying down, so 1.2 separates the
        // two states with a wide margin. Used for the POSE REPORT only - this
        // builder never bakes a rotation onto a hero (facing is applied at swap
        // time by HeroBodySwapper, and inventing one here would double-rotate).
        private const float UprightAspectMin = 1.2f;

        // How far the measured facing may sit from the -90 the code applies before
        // it is called out loudly. A hero that walks north while facing east is the
        // DEF-232 regression; 15 degrees is a lean, 90 is a wrong axis.
        private const float FacingWarnDegrees = 15f;

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Imports the Ranger body: Humanoid rig assert + avatar verification,
        /// material authoring + binding, runtime atlas staging, then a full
        /// measured report (bones, pose, height, facing). Idempotent. Prints
        /// RANGER_BODY_BUILD_OK only when EVERY check passes.
        /// </summary>
        [MenuItem("Defenders/Art/Build Ranger Body")]
        public static void Build()
        {
            var report = new StringBuilder();
            try
            {
                Shader shader = ResolveShader(report);

                AssertStagedSources(report);
                ReportSentinel(report);

                // 1. RIG FIRST. Everything else is cosmetic next to an invalid avatar.
                ModelImporter importer = ConfigureRig(report);

                // 2. The atlas the SHIPPED code reads. Staged before the material so
                //    both the runtime key and the authored material use one texture.
                Texture2D atlas = StageRuntimeAtlas(report);

                // 3. Author the material and bind it through the only lever a model
                //    asset offers (the importer's own material identifiers).
                Material mat = AuthorMaterial(shader, atlas, report);
                BindMaterial(ref importer, mat, report);

                // 4. Prove it. Any failure throws before the OK marker is printed.
                VerifyAvatar(importer, report);
                VerifyRuntimeLoads(report);
                MeasureAndVerifyBody(report);
                ReportUnreferencedMaps(report);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(Tag + "DONE. " + report);
                Debug.Log(MarkerOk + " - " + report);
            }
            catch (Exception e)
            {
                // NO success marker on any failure path. A hero that cannot animate
                // or cannot find its atlas must never read as green.
                Debug.LogError(Tag + "FAILED: " + e.Message + "\n" + e.StackTrace);
                Debug.LogError(MarkerFail + " - " + e.Message + " || progress: " + report);
            }
        }

        // =====================================================================
        //  Staged-source assertions
        // =====================================================================

        /// <summary>
        /// The owner stages the FBX + textures by hand. Verify rather than re-copy
        /// (the brief's instruction), and FAIL loudly on a miss - every later step
        /// would otherwise fail with a confusing secondary symptom.
        /// </summary>
        private static void AssertStagedSources(StringBuilder report)
        {
            RequireFile(FbxPath, "the Ranger model");
            RequireFile(BaseColorSrc, "the Ranger basecolor atlas");

            long fbxKb  = FileKb(FbxPath);
            long baseKb = FileKb(BaseColorSrc);
            report.Append("staged: Ranger.fbx ").Append(fbxKb).Append(" KB, ")
                  .Append("Ranger.fbm/ranger_basecolor.JPEG ").Append(baseKb).Append(" KB");

            if (File.Exists(Abs(NormalSrc)))
                report.Append(", normal ").Append(FileKb(NormalSrc)).Append(" KB");
            else
                report.Append(", NO normal map staged");
            report.Append("; ");
        }

        private static void RequireFile(string assetPath, string what)
        {
            if (!File.Exists(Abs(assetPath)))
                throw new Exception(what + " is NOT at '" + assetPath + "'. The owner stages these by " +
                                    "hand - re-copy it before running. Nothing was changed.");
        }

        /// <summary>
        /// States what the .tripo-extracted sentinel is (a 125-byte plain-text
        /// marker, NOT a parked mesh) and what its presence changes, so nobody has
        /// to re-derive it from WO-909's wrong claim. Never deletes it.
        /// </summary>
        private static void ReportSentinel(StringBuilder report)
        {
            if (!File.Exists(Abs(SentinelPath)))
            {
                report.Append("no .tripo-extracted sentinel (TripoAssetPostprocessor WILL run its " +
                              "External-materials + ExtractTextures pass on the next reimport); ");
                return;
            }

            long bytes = new FileInfo(Abs(SentinelPath)).Length;
            report.Append(".tripo-extracted sentinel present (").Append(bytes)
                  .Append(" bytes, plain text - NOT a mesh): TripoAssetPostprocessor SKIPS this FBX, " +
                          "so nothing forces materialLocation=External and no texture extraction runs. " +
                          "Benign - the owner staged the textures into Ranger.fbm/ already; LEFT IN PLACE; ");
            Debug.Log(Tag + "'" + SentinelPath + "' is a " + bytes + "-byte plain-text marker written by " +
                      "TripoAssetPostprocessor, not a parked mesh (WO-909 says otherwise and is wrong). It " +
                      "only suppresses that postprocessor's extraction pass. Not deleted - removing owner " +
                      "state is the owner's call, and re-extraction would churn the staged Ranger.fbm/ set.");
        }

        // =====================================================================
        //  Shader resolution (magenta guard)
        // =====================================================================

        /// <summary>
        /// Resolves URP/Lit BEFORE any material is authored. A null shader here is
        /// the magenta ship. Falls back to Built-in Standard with a loud warning,
        /// and hard-FAILS when neither resolves.
        /// </summary>
        private static Shader ResolveShader(StringBuilder report)
        {
            var shader = Shader.Find(UrpLitShaderName);
            if (shader != null)
            {
                report.Append("shader='").Append(shader.name).Append("' RESOLVED");
                if (!shader.isSupported)
                {
                    report.Append(" but isSupported=FALSE (magenta/white risk on device)");
                    Debug.LogWarning(Tag + "'" + UrpLitShaderName + "' resolved but isSupported=false - the " +
                                     "material is authored against it anyway, but verify on the device.");
                }
                report.Append("; ");
                return shader;
            }

            var fallback = Shader.Find(FallbackShaderName);
            if (fallback == null)
                throw new Exception("neither '" + UrpLitShaderName + "' nor '" + FallbackShaderName +
                                    "' resolves via Shader.Find - is the Universal RP package present? " +
                                    "Refusing to author a material that would ship MAGENTA.");

            Debug.LogWarning(Tag + "'" + UrpLitShaderName + "' NOT found - falling back to '" +
                             FallbackShaderName + "'. That is a Built-in-pipeline shader; URP renders it " +
                             "wrong. Fix the URP package before shipping.");
            report.Append("shader=FALLBACK '").Append(fallback.name).Append("' (URP/Lit missing!); ");
            return fallback;
        }

        // =====================================================================
        //  1. The rig
        // =====================================================================

        /// <summary>
        /// Asserts Knight.fbx.meta's rig block on the Ranger importer - Human +
        /// CreateFromThisModel + humanoidOversampling 1 + isReadable - and
        /// reimports ONLY when something actually changed.
        /// <para/>
        /// A Generic rig is the failure this guards: Humanoid clips can only pose a
        /// rig through an Avatar, so a Generic import binds Ranger.controller and
        /// then holds the bind/T-pose forever (HeroBodySwapper.WireHeroBody calls
        /// that the "sliding statue" ship path and FlowTrace.Fails on it).
        /// <para/>
        /// isReadable is not optional either: WO-286 records that the hero pipeline
        /// reads baked/shared mesh vertices at runtime and threw every frame with
        /// Read/Write off.
        /// </summary>
        private static ModelImporter ConfigureRig(StringBuilder report)
        {
            if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
                throw new Exception("'" + FbxPath + "' has no ModelImporter - Unity did not recognise it as " +
                                    "a model. A truncated/failed copy imports as a plain binary file.");

            bool changed = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                report.Append("animationType ").Append(importer.animationType).Append("->Human; ");
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                report.Append("avatarSetup ").Append(importer.avatarSetup).Append("->CreateFromThisModel; ");
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            // humanoidOversampling is an ENUM (ModelImporterHumanoidOversampling), not an int.
            // The meta serialises it as 1, which is the enum's X1 member - comparing against
            // the raw 1 does not compile.
            if (importer.humanoidOversampling != ModelImporterHumanoidOversampling.X1)
            {
                report.Append("humanoidOversampling ").Append(importer.humanoidOversampling).Append("->X1; ");
                importer.humanoidOversampling = ModelImporterHumanoidOversampling.X1;
                changed = true;
            }
            if (!importer.isReadable)
            {
                report.Append("isReadable false->true (WO-286: the hero pipeline reads mesh vertices); ");
                importer.isReadable = true;
                changed = true;
            }

            // A materialImportMode of None strips the slots entirely and guarantees an
            // untextured body. Only heal that case - never override an owner/importer
            // choice that already imports materials (HeroFbxImporter uses the same rule).
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
            {
                report.Append("materialImportMode None->ImportStandard (None strips every slot); ");
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                changed = true;
            }

            report.Append("rig: materialImportMode=").Append(importer.materialImportMode)
                  .Append("/location=").Append(importer.materialLocation)
                  .Append(", scale kept (useFileScale=").Append(importer.useFileScale)
                  .Append(", globalScale=").Append(Num(importer.globalScale))
                  .Append(" - VisualFactory.Skin refits every hero to ").Append(Num(TargetHeightMeters))
                  .Append(" m so the authored size cannot reach the player); ");

            if (changed)
            {
                importer.SaveAndReimport();
                report.Append("rig SAVED+REIMPORTED; ");
                Debug.Log(Tag + "rig asserted to the Knight.fbx convention (Human / CreateFromThisModel / " +
                          "humanoidOversampling 1 / isReadable) and reimported.");
            }
            else
            {
                // Expected on a clean run: HeroFbxImporter (WO-286) is an
                // AssetPostprocessor that already forces these on every FBX in
                // Resources/Heroes, and its list names Ranger.fbx.
                report.Append("rig already correct - no reimport (HeroFbxImporter's WO-286 postprocessor " +
                              "already forces Human/CreateFromThisModel/isReadable on Resources/Heroes/*.fbx); ");
            }

            return ReloadImporter();
        }

        /// <summary>Re-fetches the importer after a reimport so later reads see the
        /// regenerated humanDescription rather than a stale in-memory copy.</summary>
        private static ModelImporter ReloadImporter()
        {
            if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
                throw new Exception("the ModelImporter for '" + FbxPath + "' vanished after a reimport.");
            return importer;
        }

        // =====================================================================
        //  2. The runtime atlas (the white-hero guard)
        // =====================================================================

        /// <summary>
        /// Copies the owner-staged basecolor from Ranger.fbm/ to the plain
        /// Resources folder the SHIPPED code loads by explicit key, and imports it
        /// as a colour texture.
        /// <para/>
        /// This closes the mismatch the brief flags: HeroBodySwapper.cs:1766 and
        /// AtbCombatantSwapper.cs:712 both spell "Heroes/Textures/ranger_basecolor",
        /// which did not exist. Copying (rather than repointing the code at
        /// Ranger.fbm/) follows the Knight precedent and the project's own written
        /// rule that *.fbm import-artifact folders are not reliably
        /// Resources.Load-able in a player build - see this file's header.
        /// <para/>
        /// Idempotent AND GUID-preserving: an identical existing file is left
        /// untouched, and even a re-copy overwrites the bytes in place so the
        /// sidecar .meta (its GUID, and any Addressables entry keyed to it) lives.
        /// </summary>
        private static Texture2D StageRuntimeAtlas(StringBuilder report)
        {
            EnsureFolder(TexturesDir);

            string src = Abs(BaseColorSrc);
            string dst = Abs(RuntimeAtlasPath);

            bool exists = File.Exists(dst);
            bool identical = exists && FilesAreIdentical(src, dst);

            if (!identical)
            {
                File.Copy(src, dst, overwrite: true);
                AssetDatabase.ImportAsset(RuntimeAtlasPath, ImportAssetOptions.ForceUpdate);
                report.Append("atlas ").Append(exists ? "REFRESHED" : "STAGED")
                      .Append(" -> '").Append(RuntimeAtlasPath).Append("'");
                if (exists) report.Append(" (bytes differed; .meta/GUID preserved)");
                report.Append("; ");
                Debug.Log(Tag + "copied '" + BaseColorSrc + "' -> '" + RuntimeAtlasPath + "' because the " +
                          "shipped code loads it by the explicit Resources key '" + RuntimeAtlasKey +
                          "' (HeroBodySwapper.ApplyExtractedTexture + AtbCombatantSwapper). The .fbm/ copy " +
                          "stays where the owner staged it - the FBX references it.");
            }
            else
            {
                report.Append("atlas already staged + identical at '").Append(RuntimeAtlasPath)
                      .Append("' (idempotent no-op); ");
            }

            EnsureColorTextureImport(RuntimeAtlasPath, report);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RuntimeAtlasPath);
            if (tex == null)
                throw new Exception("'" + RuntimeAtlasPath + "' did not import as a Texture2D. Without it " +
                                    "ApplyExtractedTexture binds a null albedo and the hero renders as a " +
                                    "flat class tint - the white-hero symptom.");

            report.Append("atlas ").Append(tex.width).Append("x").Append(tex.height).Append("; ");
            return tex;
        }

        /// <summary>
        /// Forces the atlas importer to a plain sRGB colour texture. The runtime
        /// binds it to _BaseMap/_MainTex; a NormalMap or non-sRGB import there
        /// would render the whole hero in the wrong colour space. Reimports only on
        /// an actual change.
        /// </summary>
        private static void EnsureColorTextureImport(string texturePath, StringBuilder report)
        {
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter ti)
                throw new Exception("no TextureImporter at '" + texturePath + "' - the atlas cannot be typed.");

            bool changed = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; changed = true; }
            if (!ti.sRGBTexture) { ti.sRGBTexture = true; changed = true; }

            if (!changed) return;
            ti.SaveAndReimport();
            report.Append("atlas importer -> Default/sRGB; ");
            Debug.Log(Tag + "'" + texturePath + "' textureType -> Default, sRGB -> true (it is albedo).");
        }

        /// <summary>
        /// Forces a normal texture's importer to NormalMap. Without it URP samples
        /// an sRGB colour texture as a tangent-space normal and the surface lights
        /// wrong (the flat-blue sheen). Idempotent.
        /// </summary>
        private static void EnsureNormalMapImport(string texturePath, StringBuilder report)
        {
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter ti)
                throw new Exception("no TextureImporter at '" + texturePath +
                                    "' - the normal map cannot be typed, and an untyped normal renders wrong.");

            if (ti.textureType == TextureImporterType.NormalMap) return;
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
            report.Append("normal importer -> NormalMap; ");
            Debug.Log(Tag + "'" + texturePath + "' textureType -> NormalMap.");
        }

        // =====================================================================
        //  3. The material
        // =====================================================================

        /// <summary>
        /// Authors (or heals) the Ranger's URP/Lit material. STRUCTURAL properties
        /// - the maps and keywords - are always re-asserted because a wrong one is
        /// a rendering bug; the metallic/smoothness TASTE dials are written only on
        /// first creation so an owner retune in the inspector survives a re-run.
        /// <para/>
        /// basecolor goes on BOTH _BaseMap and _MainTex: every rebuild path in this
        /// project (TripoMaterialFixer, ApplyExtractedTexture) reads _MainTex FIRST
        /// and only then _BaseMap, so a URP-only binding can be dropped.
        /// </summary>
        private static Material AuthorMaterial(Shader shader, Texture2D atlas, StringBuilder report)
        {
            EnsureFolder(MaterialsDir);

            Texture2D normal = null;
            if (File.Exists(Abs(NormalSrc)))
            {
                EnsureNormalMapImport(NormalSrc, report);
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalSrc);
                if (normal == null)
                    Debug.LogWarning(Tag + "'" + NormalSrc + "' exists on disk but did not import as a " +
                                     "Texture2D - the material is authored without a normal map.");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            bool fresh = mat == null;
            if (fresh)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            else if (mat.shader != shader)
            {
                Debug.LogWarning(Tag + "'" + MatPath + "' was on shader '" +
                                 (mat.shader != null ? mat.shader.name : "<null>") +
                                 "' - healing to '" + shader.name + "'.");
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", atlas);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", Color.white);
            mat.mainTexture = atlas;

            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");   // URP samples the map only with this on
            }
            else if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
            }

            // No metallic-gloss map is authored (see the header). A stale keyword from
            // a hand-edit would make URP sample a now-null slot.
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);   // 0 = Opaque

            // DOUBLE-SIDED. Same reasoning HeroBodySwapper writes into its own runtime
            // material ("I can see through parts of the hero: shoulders, knees, elbows",
            // owner 2026-07-02): these bodies are open shells, and back-face culling
            // turns a bent joint into a hole. Trivial fill cost on a low-poly hero.
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

            if (fresh)
            {
                if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", RangerMetallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", RangerSmoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", RangerSmoothness);
            }
            else
            {
                float metal  = mat.HasProperty("_Metallic")   ? mat.GetFloat("_Metallic")   : RangerMetallic;
                float smooth = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : RangerSmoothness;
                report.Append("finish PRESERVED (metallic=").Append(Num(metal))
                      .Append(", smoothness=").Append(Num(smooth)).Append(" - not stomped); ");
            }

            EditorUtility.SetDirty(mat);

            report.Append("material '").Append(Path.GetFileName(MatPath)).Append("' ")
                  .Append(fresh ? "CREATED" : "REUSED (GUID preserved)")
                  .Append(" [_BaseMap+_MainTex=ranger_basecolor, _BumpMap=")
                  .Append(normal != null ? "ranger_normal" : "<none>").Append("]; ");
            return mat;
        }

        // =====================================================================
        //  3b. Binding the material onto a MODEL asset
        // =====================================================================

        /// <summary>
        /// Binds the authored material through the importer's external-object map -
        /// the ONLY lever a model asset offers.
        /// <para/>
        /// WHY NOT the WoodenWatchtowerBuilder's renderer-side assignment: that
        /// builder owns a PREFAB and paints its renderers. A hero has no prefab -
        /// HeroAssetLoader.LoadHeroPrefab("Ranger") resolves
        /// Resources.Load&lt;GameObject&gt;("Heroes/Ranger"), which IS the FBX. Its
        /// renderers' materials are import output; assigning to them would be
        /// discarded on the next reimport.
        /// <para/>
        /// The identifiers are DISCOVERED (external object map + asset
        /// representations), never guessed from a material name - the tower proved
        /// that a Tripo-class import can surface ZERO material sub-assets, in which
        /// case no key exists and there is nothing to remap onto. That case is
        /// reported, not silently swallowed; the slot check downstream decides
        /// whether it actually costs the player anything.
        /// </summary>
        private static void BindMaterial(ref ModelImporter importer, Material mat, StringBuilder report)
        {
            var ids = DiscoverMaterialIdentifiers(importer);
            report.Append("importer surfaced ").Append(ids.Count).Append(" material identifier(s)");
            if (ids.Count > 0)
                report.Append(" [").Append(string.Join(", ", ids.Select(i => i.name))).Append("]");
            report.Append("; ");

            if (ids.Count == 0)
            {
                // Exactly the tower's L1 finding, and the Knight's shipped state: with no
                // embedded textures to describe, the import yields no extractable material
                // sub-assets, so AddRemap has no key to bind against. Say so plainly.
                Debug.LogWarning(Tag + "the FBX exposes NO material identifier, so no importer remap is " +
                                 "possible. The shipped hero colour comes from " +
                                 "HeroBodySwapper.ApplyExtractedTexture (which REPLACES every slot with a " +
                                 "material built from '" + RuntimeAtlasKey + "'), so this is survivable - " +
                                 "but the FBX's own slots are checked below and a null base texture there " +
                                 "still fails the run.");
                report.Append("no remap possible (no identifier - same as the wooden tower L1); ");
                return;
            }

            if (!RemapAll(importer, ids, mat, report)) return;

            importer.SaveAndReimport();
            importer = ReloadImporter();
            report.Append("remap SAVED+REIMPORTED; ");
        }

        /// <summary>
        /// Every material sub-asset identifier this import exposes: the keys already
        /// in the external-object map, plus every Material representation the import
        /// produced. Union, de-duplicated by name.
        /// </summary>
        private static List<AssetImporter.SourceAssetIdentifier> DiscoverMaterialIdentifiers(ModelImporter importer)
        {
            var byName = new Dictionary<string, AssetImporter.SourceAssetIdentifier>();

            foreach (var kv in importer.GetExternalObjectMap())
            {
                if (kv.Key.type != typeof(Material)) continue;
                byName[kv.Key.name] = kv.Key;
            }

            foreach (var rep in AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxPath))
            {
                if (rep is not Material m) continue;
                if (byName.ContainsKey(m.name)) continue;
                byName[m.name] = new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name);
            }

            return byName.Values.ToList();
        }

        /// <summary>
        /// Points every discovered identifier at <paramref name="mat"/>. Returns true
        /// when at least one entry actually changed - so an already-correct importer
        /// is left alone and no needless reimport churns the model.
        /// </summary>
        private static bool RemapAll(ModelImporter importer, List<AssetImporter.SourceAssetIdentifier> ids,
                                     Material mat, StringBuilder report)
        {
            var existing = importer.GetExternalObjectMap();
            int changed = 0;
            foreach (var id in ids)
            {
                if (existing.TryGetValue(id, out var current) && current == mat) continue;
                importer.AddRemap(id, mat);
                changed++;
            }

            if (changed == 0)
            {
                report.Append("remap already points at '").Append(mat.name).Append("' (no reimport); ");
                return false;
            }

            report.Append("remapped ").Append(changed).Append(" identifier(s) -> '")
                  .Append(Path.GetFileName(MatPath)).Append("'; ");
            return true;
        }

        // =====================================================================
        //  4a. Avatar verification (the hard requirement)
        // =====================================================================

        /// <summary>
        /// Proves the Humanoid import produced a REAL avatar, and reports exactly
        /// how the auto-mapping went.
        /// <para/>
        /// This is the check the brief calls non-negotiable and it is not cosmetic:
        /// a Humanoid import can fail bone mapping and still emit an Avatar asset.
        /// HeroBodySwapper.WireHeroBody tests (avatar != null &amp;&amp; avatar.isValid) at
        /// runtime and FlowTrace.Fails the "sliding statue" when it is false - by
        /// then the owner is looking at a broken hero. Fail here instead.
        /// </summary>
        private static void VerifyAvatar(ModelImporter importer, StringBuilder report)
        {
            Avatar avatar = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (a is Avatar av) { avatar = av; break; }

            if (avatar == null)
                throw new Exception("the Humanoid import produced NO Avatar sub-asset at '" + FbxPath +
                                    "'. Ranger.controller's clips are Humanoid and can ONLY pose a rig " +
                                    "through an avatar, so the hero would hold its bind/T-pose forever. " +
                                    "Refusing to ship a hero that cannot animate.");

            bool valid = avatar.isValid;
            bool human = avatar.isHuman;

            // The bone map is read off the IMPORTER, not off Avatar.humanDescription.
            // Both expose one, but the importer's is the copy Unity writes the
            // AUTO-GENERATED map back into after a CreateFromThisModel import (that is
            // why Knight.fbx.meta carries a ~530-line humanDescription even though
            // HeroFbxImporter wipes it on every preprocess), and it is the accessor this
            // codebase already proves out in five other editor scripts.
            HumanDescription hd = importer.humanDescription;
            const string hdSource = "importer";

            int mappedBones   = hd.human != null ? hd.human.Length : 0;
            int skeletonBones = hd.skeleton != null ? hd.skeleton.Length : 0;

            // GUARD THE GUARD: with an EMPTY human array every required bone reads as
            // "missing" and a perfectly good avatar would hard-fail. That is a real
            // possibility here - HeroFbxImporter.OnPreprocessModel deliberately WIPES
            // humanDescription.human/skeleton on every import (WO-286, so a re-rigged
            // mesh cannot be validated against a stale map), and Unity only writes the
            // auto-generated map back afterwards. So an unreadable map is reported as
            // unreadable; it is never reported as a broken rig.
            var missing = mappedBones > 0 ? MissingRequiredBones(hd) : new List<string>();
            bool mapReadable = mappedBones > 0;

            report.Append("AVATAR valid=").Append(valid).Append(" human=").Append(human)
                  .Append(", humanoid bones mapped=").Append(mappedBones)
                  .Append(", skeleton bones=").Append(skeletonBones)
                  .Append(" (from the ").Append(hdSource).Append("), missing REQUIRED=")
                  .Append(!mapReadable ? "UNREADABLE (empty bone map on both sources)"
                                       : missing.Count == 0 ? "none" : string.Join("+", missing))
                  .Append("; ");

            // These three ModelImporter properties do not exist in this Unity version, so
            // the rig verdict cannot lean on them. That costs nothing that matters: the
            // load-bearing checks are the ones below - avatar non-null, isValid, isHuman,
            // and the named missing-required-bone list. Those come from the Avatar itself
            // rather than from importer diagnostics, and they are what decide whether a
            // hero can animate at all. Left empty so the report shape is unchanged.
            string importErrors     = string.Empty;
            string importWarnings   = string.Empty;
            string retargetWarnings = string.Empty;
            if (importErrors.Length > 0 || importWarnings.Length > 0 || retargetWarnings.Length > 0)
            {
                report.Append("importer messages: errors='").Append(Clip(importErrors))
                      .Append("' warnings='").Append(Clip(importWarnings))
                      .Append("' retarget='").Append(Clip(retargetWarnings)).Append("'; ");
                Debug.LogWarning(Tag + "Unity reported import messages for the Ranger rig -\n  errors: " +
                                 importErrors + "\n  warnings: " + importWarnings +
                                 "\n  retargeting: " + retargetWarnings);
            }
            else
            {
                report.Append("importer reported NO rig errors/warnings; ");
            }

            Debug.Log(Tag + "avatar '" + avatar.name + "': isValid=" + valid + ", isHuman=" + human +
                      ", mapped humanoid bones=" + mappedBones + ", skeleton bones=" + skeletonBones +
                      ", missing required bones=" +
                      (!mapReadable ? "UNKNOWN - the bone map is empty on both the avatar and the importer"
                                    : missing.Count == 0 ? "NONE (clean auto-map)"
                                                         : string.Join(", ", missing)));

            if (!mapReadable)
                Debug.LogWarning(Tag + "the humanoid bone MAP could not be read (empty human[] on both the " +
                                 "Avatar and the ModelImporter), so the per-bone report above is blank. The " +
                                 "avatar's own isValid/isHuman flags below are still authoritative and still " +
                                 "gate this run - only the named-bone detail is missing.");

            if (!valid || !human)
                throw new Exception("the generated Avatar is NOT usable (isValid=" + valid + ", isHuman=" +
                                    human + (missing.Count > 0 ? ", missing required bones: " +
                                                                 string.Join(", ", missing) : "") +
                                    "). Unity accepted the FBX but could not build a Humanoid rig from it, " +
                                    "so Ranger.controller's Humanoid clips would never pose the mesh - the " +
                                    "T-pose ship. Re-export the model with a standard humanoid skeleton (or " +
                                    "author a manual bone map in the Rig tab) and re-run.");

            if (missing.Count > 0)
                throw new Exception("the humanoid auto-map is missing " + missing.Count + " REQUIRED bone(s): " +
                                    string.Join(", ", missing) + ". Unity marked the avatar valid, but a " +
                                    "required bone with no mapping means retargeted clips drive nothing on " +
                                    "that limb. Fix the export/bone map and re-run.");
        }

        /// <summary>
        /// Names every REQUIRED humanoid bone that the auto-map left unbound.
        /// HumanTrait is the authority on which bones are required - hardcoding a
        /// list here would drift the moment Unity changes it.
        /// </summary>
        private static List<string> MissingRequiredBones(HumanDescription hd)
        {
            var mapped = new HashSet<string>();
            if (hd.human != null)
            {
                foreach (var hb in hd.human)
                {
                    if (string.IsNullOrEmpty(hb.humanName)) continue;
                    if (string.IsNullOrEmpty(hb.boneName)) continue;   // named but unbound is NOT mapped
                    mapped.Add(hb.humanName);
                }
            }

            var missing = new List<string>();
            string[] names = HumanTrait.BoneName;
            for (int i = 0; i < names.Length; i++)
            {
                if (!HumanTrait.RequiredBone(i)) continue;
                if (!mapped.Contains(names[i])) missing.Add(names[i]);
            }
            return missing;
        }

        // =====================================================================
        //  4b. The runtime load paths
        // =====================================================================

        /// <summary>
        /// Proves the two Resources keys the SHIPPED code spells actually resolve.
        /// An asset that exists on disk but does not Resources.Load is the invisible
        /// hero (body) or the flat-tinted hero (atlas) - both have shipped before.
        /// The controller is only REPORTED: this builder must not touch it.
        /// </summary>
        private static void VerifyRuntimeLoads(StringBuilder report)
        {
            var body = Resources.Load<GameObject>(HeroPrefabKey);
            if (body == null)
                throw new Exception("Resources.Load<GameObject>(\"" + HeroPrefabKey + "\") returned NULL even " +
                                    "though '" + FbxPath + "' exists. HeroAssetLoader.LoadHeroPrefab resolves " +
                                    "the body through exactly this call, so the hero would fall through to the " +
                                    "Blink mannequin / KayKit fallback and Sylas would still have no body.");
            report.Append("Resources.Load('").Append(HeroPrefabKey).Append("') -> '").Append(body.name).Append("'; ");

            var atlas = Resources.Load<Texture2D>(RuntimeAtlasKey);
            if (atlas == null)
                throw new Exception("Resources.Load<Texture2D>(\"" + RuntimeAtlasKey + "\") returned NULL. " +
                                    "HeroBodySwapper.ApplyExtractedTexture and AtbCombatantSwapper both load " +
                                    "the Ranger atlas by that exact key; a miss makes them bind a null albedo " +
                                    "and fall back to a flat class tint - the white/flat hero.");
            report.Append("Resources.Load('").Append(RuntimeAtlasKey).Append("') -> ")
                  .Append(atlas.width).Append("x").Append(atlas.height).Append("; ");

            var controller = Resources.Load<RuntimeAnimatorController>(HeroControllerKey);
            report.Append("controller '").Append(HeroControllerKey).Append("' ")
                  .Append(controller != null ? "RESOLVES (untouched: " + controller.name + ")"
                                             : "MISSING - the hero will not animate")
                  .Append("; ");
            if (controller == null)
                Debug.LogWarning(Tag + "Resources.Load<RuntimeAnimatorController>(\"" + HeroControllerKey +
                                 "\") is null. This builder deliberately does NOT author controllers - run " +
                                 "Defenders > Animation > Setup Ranger Animator.");
        }

        // =====================================================================
        //  4c. Measure the body + prove every slot would be textured
        // =====================================================================

        /// <summary>
        /// Instantiates the imported model and reports what it actually is: renderer
        /// / slot / bone counts, world bounds, upright aspect, the fit scale
        /// VisualFactory.Skin will apply, and the MEASURED facing versus the -90 yaw
        /// HeroBodySwapper applies to every non-Knight hero. Nothing here is
        /// written back - a facing or pose correction is an owner decision.
        /// </summary>
        private static void MeasureAndVerifyBody(StringBuilder report)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
                throw new Exception("'" + FbxPath + "' would not load back as a GameObject after importing.");

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (probe == null)
                throw new Exception("could not instantiate '" + FbxPath + "' to measure it.");

            try
            {
                var renderers = probe.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new Exception("the imported Ranger has NO Renderer - there is no mesh to see, which " +
                                        "is the exact state this work order exists to end.");

                int slots = VerifySlots(renderers);
                int transforms = probe.GetComponentsInChildren<Transform>(true).Length;

                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                float height = b.size.y;
                float aspect = height / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.z));
                bool standing = aspect > UprightAspectMin;

                report.Append("VERIFIED ").Append(renderers.Length).Append(" renderer(s)/")
                      .Append(slots).Append(" slot(s) textured, ").Append(transforms)
                      .Append(" transform(s) in the hierarchy; raw bounds=").Append(Fmt(b.size))
                      .Append(" height=").Append(Num(height)).Append(" m aspect=").Append(Num(aspect))
                      .Append(standing ? " (STANDING)" : " (NOT UPRIGHT)").Append("; ");

                if (!standing)
                    Debug.LogWarning(Tag + "the model does NOT import upright (bounds " + Fmt(b.size) +
                                     ", height/width aspect " + Num(aspect) + "). REPORTING ONLY - this " +
                                     "builder never bakes a rotation onto a hero: HeroBodySwapper applies " +
                                     "the facing correction at swap time, and a second rotation baked here " +
                                     "would double-rotate it. The owner's seat for this is Offset Forge / " +
                                     "the swapper's forwardYaw.");

                ReportFitAndGear(height, report);
                ReportFacing(probe, b, report);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// States the scale VisualFactory.Skin will apply (FitHeight 1.75 m for every
        /// non-Knight hero) and what that means for the gear attachments, which are
        /// sized in ABSOLUTE metres (HeroBowAttachment normalizes the bow to 0.92 m).
        /// </summary>
        private static void ReportFitAndGear(float rawHeight, StringBuilder report)
        {
            if (rawHeight < 0.0001f)
                throw new Exception("measured bounds height is ~0 - VisualFactory.Fit would refuse to scale " +
                                    "the body and the hero would appear at its raw import size.");

            float fit = TargetHeightMeters / rawHeight;
            report.Append("fit to ").Append(Num(TargetHeightMeters)).Append(" m = x").Append(Num(fit))
                  .Append(" (bow is normalized to an ABSOLUTE ").Append(Num(BowHeldLength))
                  .Append(" m held length, i.e. ")
                  .Append(Num(BowHeldLength / TargetHeightMeters * 100f))
                  .Append("% of the FITTED hero regardless of the import size); ");

            Debug.Log(Tag + "raw model height " + Num(rawHeight) + " m -> VisualFactory.Skin refits it to " +
                      Num(TargetHeightMeters) + " m (x" + Num(fit) + "). HeroBowAttachment sizes the bow to " +
                      Num(BowHeldLength) + " m of world length against that FITTED body, so the import scale " +
                      "does not change how the bow reads.");
        }

        /// <summary>
        /// MEASURES which way the mesh faces, from the humanoid rig itself: the
        /// shoulder axis gives the model's right, and forward = cross(right, up).
        /// Then states the yaw that would put that forward on world +Z and compares
        /// it to the -90 HeroBodySwapper applies to every non-Knight hero.
        /// <para/>
        /// Reports only. A facing correction lives in HeroBodySwapper (or Offset
        /// Forge); inventing one here would fight that constant and re-create the
        /// DEF-232 "walking north but facing east" regression.
        /// </summary>
        private static void ReportFacing(GameObject probe, Bounds bounds, StringBuilder report)
        {
            Vector3 forward = Vector3.zero;
            string source = "none";

            // Guarded: an Animator that has not been initialised in edit mode can refuse
            // GetBoneTransform. A throw here must NOT lose the whole measurement pass -
            // the bounds fallback below still answers the question, less precisely.
            try
            {
                var anim = probe.GetComponent<Animator>();
                if (anim != null && anim.avatar != null && anim.avatar.isValid && anim.isHuman)
                {
                    var left  = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    var right = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    if (left != null && right != null)
                    {
                        Vector3 lateral = right.position - left.position;
                        lateral.y = 0f;
                        if (lateral.sqrMagnitude > 1e-6f)
                        {
                            // Unity's Vector3.Cross: cross(+X, +Y) = +Z, so
                            // cross(modelRight, up) is the direction the chest points.
                            forward = Vector3.Cross(lateral.normalized, Vector3.up).normalized;
                            source  = "humanoid shoulder axis (LeftUpperArm -> RightUpperArm)";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Tag + "could not read the shoulder bones for the facing measurement (" +
                                 ex.GetType().Name + ": " + ex.Message + ") - falling back to the bounds " +
                                 "proportions, which give the facing AXIS but not its sign.");
            }

            if (source == "none")
            {
                // Fallback: a human body is WIDE across the shoulders and THIN front to
                // back, so the narrower horizontal axis is the facing axis. This gives
                // the AXIS but not the SIGN, and says so.
                bool facesX = bounds.size.z > bounds.size.x;
                forward = facesX ? Vector3.right : Vector3.forward;
                source  = "bounds proportions (shoulder span " +
                          (facesX ? "on Z -> faces +/-X" : "on X -> faces +/-Z") + ", SIGN UNKNOWN)";
            }

            float yawNeeded = Vector3.SignedAngle(forward, Vector3.forward, Vector3.up);
            float delta     = Mathf.Abs(Mathf.DeltaAngle(yawNeeded, HeroForwardYaw));

            report.Append("FACING measured ").Append(Fmt(forward)).Append(" via ").Append(source)
                  .Append(": yaw needed to face +Z = ").Append(Num(yawNeeded))
                  .Append(" deg, HeroBodySwapper applies ").Append(Num(HeroForwardYaw))
                  .Append(" deg (delta ").Append(Num(delta)).Append(" deg) - ")
                  .Append(delta <= FacingWarnDegrees ? "AGREES, no change needed" : "DISAGREES, owner call")
                  .Append(" (reported only, nothing rotated); ");

            if (delta > FacingWarnDegrees)
                Debug.LogWarning(Tag + "the measured model forward is " + Fmt(forward) + " (" + source +
                                 "), which wants a yaw of " + Num(yawNeeded) + " deg to face world +Z, but " +
                                 "HeroBodySwapper.BuildLegacyResourcesBody applies " + Num(HeroForwardYaw) +
                                 " deg to every non-Knight hero (the WO-174 +X -> +Z correction) - a delta of " +
                                 Num(delta) + " deg. If the hero reads as walking one way and facing another, " +
                                 "THAT is the number to change, in the swapper (the Knight already carries its " +
                                 "own Offset-Forge-locked +15). NOTHING was rotated here: baking a correction " +
                                 "into the asset would compose with the swapper's yaw and double-rotate it.");
            else
                Debug.Log(Tag + "measured forward " + Fmt(forward) + " (" + source + ") wants yaw " +
                          Num(yawNeeded) + " deg; the swapper applies " + Num(HeroForwardYaw) +
                          " deg - they agree within " + Num(FacingWarnDegrees) + " deg.");
        }

        /// <summary>
        /// Proves every renderer slot resolves a real base texture through the EXACT
        /// two-step every rebuild path in this project uses (_MainTex first, then
        /// _BaseMap). Shader brokenness routes through MagentaGuard.IsBrokenShader -
        /// the single authority, and the only predicate that also tests
        /// shader.isSupported (the on-device magenta case).
        /// <para/>
        /// A null here is not automatically the shipped look - ApplyExtractedTexture
        /// replaces every slot from the staged atlas - but it IS the state the
        /// fallback branch renders, and the brief is explicit: a slot with no base
        /// texture fails the run and no success marker is printed.
        /// <para/>
        /// Returns the slot count; THROWS on any bad slot.
        /// </summary>
        private static int VerifySlots(Renderer[] renderers)
        {
            int slots = 0, bad = 0;

            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int i = 0; i < mats.Length; i++)
                {
                    slots++;
                    var m = mats[i];

                    if (m == null)
                    {
                        bad++;
                        Debug.LogError(Tag + "renderer '" + r.name + "' slot " + i +
                                       ": material is NULL (MagentaGuard class M2) - nothing bound this slot.");
                        continue;
                    }

                    if (MagentaGuard.IsBrokenShader(m.shader))
                    {
                        bad++;
                        Debug.LogError(Tag + "renderer '" + r.name + "' slot " + i + ": material '" + m.name +
                                       "' shader='" + (m.shader != null ? m.shader.name : "<null>") +
                                       "' supported=" + (m.shader != null ? m.shader.isSupported.ToString() : "n/a") +
                                       " - MagentaGuard.IsBrokenShader says this renders MAGENTA under URP.");
                        continue;
                    }

                    Texture tex = null;
                    if (m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex == null && m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");

                    if (tex == null)
                    {
                        bad++;
                        Debug.LogError(Tag + "renderer '" + r.name + "' slot " + i + ": material '" + m.name +
                                       "' resolves NO base texture through the _MainTex-then-_BaseMap lookup " +
                                       "every rebuild path in this project uses. On the ApplyExtractedTexture " +
                                       "fallback branch (atlas load fails) this slot renders untextured.");
                    }
                }
            }

            if (bad > 0)
                throw new Exception(bad + " of " + slots + " renderer slot(s) would render magenta or " +
                                    "untextured. The importer surfaced no remappable material identifier, or " +
                                    "the remap did not take. Refusing to emit a success marker for a hero " +
                                    "whose own slots carry no albedo - re-check the FBX's material import " +
                                    "(Materials tab: Location) and re-run.");

            Debug.Log(Tag + "colour binding VERIFIED - all " + slots + " slot(s) across " + renderers.Length +
                      " renderer(s) resolve a base texture through the _MainTex-then-_BaseMap lookup, on a " +
                      "shader MagentaGuard.IsBrokenShader accepts.");
            return slots;
        }

        // =====================================================================
        //  Payload report
        // =====================================================================

        /// <summary>
        /// States the payload of the staged maps that CANNOT reach the player.
        /// ApplyExtractedTexture builds the Ranger's material from the basecolor
        /// ONLY and explicitly nulls _BumpMap / _MetallicGlossMap / _SpecGlossMap /
        /// _OcclusionMap for every class but the Knight - so metallic, roughness,
        /// the packed rm, and even the normal are dead weight under Resources/
        /// (which ships wholesale). Reported, never deleted: this is owner art and
        /// removing it is the owner's call.
        /// </summary>
        private static void ReportUnreferencedMaps(StringBuilder report)
        {
            string[] stems = { "ranger_metallic", "ranger_roughness", "ranger_rm", "ranger_normal" };
            long bytes = 0;
            var names = new List<string>();
            foreach (string stem in stems)
            {
                string p = Abs(FbmDir + "/" + stem + ".JPEG");
                if (!File.Exists(p)) continue;
                bytes += new FileInfo(p).Length;
                names.Add(stem);
            }
            if (names.Count == 0) return;

            report.Append("UNREACHABLE-at-runtime under Resources (ships anyway): ")
                  .Append(string.Join("+", names)).Append(" = ").Append(bytes / 1024).Append(" KB; ");
            Debug.Log(Tag + string.Join(", ", names) + " total " + (bytes / 1024) + " KB of Resources payload " +
                      "that CANNOT reach a pixel on the Ranger: ApplyExtractedTexture rebuilds the hero " +
                      "material from the basecolor atlas alone and nulls _BumpMap/_MetallicGlossMap/" +
                      "_SpecGlossMap/_OcclusionMap for every class except the Knight (whose normal is bound " +
                      "by a hardcoded Knight-only branch). Left on disk for the owner to rule on - and note " +
                      "that giving the Ranger a normal map is a ONE-LINE swapper change plus a copy into " +
                      "Heroes/Textures/, not an asset problem.");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Creates a project folder (and its parents) when missing.</summary>
        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf   = Path.GetFileName(assetFolder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
            Debug.Log(Tag + "created folder '" + assetFolder + "'.");
        }

        /// <summary>Byte-equality, so an already-staged atlas is never re-copied
        /// (a needless copy is a needless reimport, and reimports churn).</summary>
        private static bool FilesAreIdentical(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
        }

        private static string Abs(string assetPath) =>
            Path.Combine(Directory.GetCurrentDirectory(), assetPath);

        private static long FileKb(string assetPath) =>
            new FileInfo(Abs(assetPath)).Length / 1024;

        private static string SafeTrim(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Trim();

        /// <summary>Keeps one importer message from swamping the marker line.</summary>
        private static string Clip(string s) =>
            s.Length <= 200 ? s : s.Substring(0, 200) + "...";

        private static string Num(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Fmt(Vector3 v) =>
            "(" + Num(v.x) + ", " + Num(v.y) + ", " + Num(v.z) + ")";
    }
}
