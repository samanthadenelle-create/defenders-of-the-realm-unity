// =============================================================================
// CraftPixPeopleBuilder (owner ruling 2026-08-07: "REPLACE the town's villager
// bodies with the 14 CraftPix medieval people") - SCRIPT-authors the ASSET half of
// the townsfolk body set: importer settings + ONE shared URP/Lit material bound to
// the pack's single atlas + the 14 Resources/NPCs prefabs CastleTownsfolkInjector
// resolves through Resources.Load.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// WHY THIS EXISTS:
//   Every step below touches a .prefab / .mat / .meta. Hand-editing that YAML is
//   banned (CLAUDE.md section 0 + section 3 - mount garble + resave corruption
//   history), so the work goes through AssetDatabase / ModelImporter /
//   TextureImporter / PrefabUtility and UNITY owns the serialization. Nothing here
//   writes a byte of YAML itself. Same precedent as WoodenWatchtowerBuilder.
//
// PROVENANCE (owner-downloaded, NOT re-derivable from any pack):
//   craftpix.net free pack 700077 "Free Medieval 3D People Low Poly Models".
//   Source folder: fbx/people_unity (the UNITY export - the sibling
//   fbx/unral_better_export is the Unreal variant and is deliberately NOT staged).
//   License: craftpix file-licence URL, copied verbatim to
//   Assets/Art/People/CraftPix/License.txt so the licence travels WITH the art.
//
// WHERE THE ART LIVES, AND WHY IT IS NOT UNDER Resources/:
//   Art  ->  Assets/Art/People/CraftPix/   (14 .fbx + people_texture_map.png +
//                                           License.txt + the shared .mat)
//   Load ->  Assets/Resources/NPCs/CraftPixPeople/*.prefab
//
//   Unity FORCE-INCLUDES every asset under a Resources/ folder in the player build
//   whether or not anything references it. Staging 14 raw FBX there would ship the
//   raw model assets as a second, unreferenced copy of the payload. Keeping the art
//   OUTSIDE Resources means the prefab is the single entry point and only what the
//   prefab actually references travels - while Resources/NPCs stays what it already
//   is: the LOADER SURFACE, one prefab per addressable body, exactly the shape
//   CastleTownsfolkInjector's "NPCs/<name>" strings expect.
//
//   GIT: neither path is matched by any rule in .gitignore (checked line by line -
//   the blanket ignores are /Assets/Models/*, /Assets/Art/TripoStructures/ and
//   /Assets/StructureContent/*, none of which touch these two folders), so NO
//   negation is needed and the art is tracked by default. .gitattributes routes
//   *.fbx and *.png through LFS, so the ~3.1 MB of models travels as LFS pointers.
//
// THE RIG - READ FROM THE FILES, NOT ASSUMED:
//   All 14 FBX were probed byte-wise before a line of this was written. Every one
//   carries exactly ONE Skin deformer and 41 Cluster deformers over an "Armature"
//   hierarchy with Unreal-style bone names (Root, Spine_01..03, Neck_01, Head,
//   Upperarm_L/R, Lowerarm_L/R, Hand_L/R, Thigh_L/R, Calf_L/R, Foot_L/R + fingers).
//   So these are SKINNED CHARACTERS, not static props - they import with a
//   SkinnedMeshRenderer, and the material binding below has to walk skinned
//   renderers, not MeshFilters.
//
//   AND: every one of the 14 contains ZERO AnimationStack objects. THE PACK SHIPS
//   NO CLIPS - and under this project's pipeline that is NORMAL, not a blocker.
//
//   AVATAR: animationType = HUMANOID, avatarSetup = CreateFromThisModel. This is CANON
//   (docs/ANIMATION_PIPELINE.md, owner-established): "Every model - heroes, enemies,
//   anything authored later - is Humanoid ... one clip retargets onto every model with
//   no per-character re-authoring." A model is never expected to bring its own clips;
//   it becomes Humanoid and inherits Assets/Action/Shared (Idle / Walk_Forward / Run /
//   turns / Death / Hit_Reaction / Block / Combat_Idle / Victory - 15 clips today).
//
//   An earlier pass of this file chose GENERIC on the reasoning that a Humanoid mapping
//   cannot be verified from file bytes. The caution was right; the conclusion was not.
//   Generic is precisely what BREAKS the shared-rig pipeline: a Generic avatar can only
//   replay clips authored on its OWN bone hierarchy, and CraftPix ships Unreal names
//   (Spine_01 / Upperarm_L / Thigh_L) that match nothing else in the project. Generic
//   townsfolk could therefore never animate at all - and a skinned humanoid with no
//   usable controller renders its BIND POSE, which is the owner's F8 2026-08-02
//   "NPC Stuck in T Pose" (the defect KayKitNpcAnimatorSetup was written to fix).
//   Shipping 14 of those would be a visible DOWNGRADE from the 2 animated peasants
//   they replace - and this pass is explicitly "upgrade quality WITHOUT changing it".
//
//   The verification concern is answered by MEASURING rather than by avoiding: the run
//   reports each model's Avatar.isValid / isHuman after import, so a body whose auto-map
//   failed is NAMED instead of silently shipping a T-pose.
//
// ONE SHARED MATERIAL, ONE SHARED ATLAS:
//   The pack ships a SINGLE 64x64 palette atlas (people_texture_map.png) for all 14
//   bodies, so there is exactly one material for the whole town - 14 bodies, 1 draw
//   material, which is also the cheapest thing to render on the Seeker.
//
//   THE ATLAS IMPORT SETTINGS ARE NOT TASTE (a 64x64 palette map is a special case):
//     * filterMode Point - bilinear would blend ADJACENT PALETTE CELLS together and
//       every body would wear muddy seam colours. Same reason the KayKit palette
//       textures in Resources/NPCs/KayKit import Point (filterMode: 0).
//     * mipmaps OFF - a mip chain averages neighbouring cells, which is the same
//       bleed one level down. The usual argument for mips is distance shimmer, and it
//       does not apply here: a palette atlas is flat colour with high frequency ONLY
//       at the cell edges, which is precisely what the mip would smear.
//     * compression OFF - DXT/ETC block-compress 4x4 pixel blocks, and a block that
//       straddles a palette boundary invents colours that are on no body. At 64x64
//       RGB24 the whole atlas is 12 KB, so there is nothing to save.
//     * maxTextureSize 64 - it is a 64x64 source; anything larger is upsampling.
//     * sRGB ON - it is albedo colour.
//
// MATERIAL BINDING IS ON THE PREFAB'S RENDERERS (not an importer remap):
//   materialImportMode is set to None so the import generates no per-model materials
//   at all, and this builder assigns the ONE shared material onto the prefab's own
//   Renderer.sharedMaterials. Two traps, both handled in PaintRenderer:
//     * Renderer.sharedMaterials RETURNS A COPY - mutating the returned array is a
//       silent no-op; the array must be rebuilt and ASSIGNED BACK.
//     * the array must be ONE MATERIAL PER SUBMESH, sized from the mesh's own
//       subMeshCount. A short array leaves trailing submeshes null, which renders as
//       the untextured slab this builder exists to prevent.
//
// COMPONENT PARITY WITH THE BODIES THESE REPLACE:
//   NPC_Peasant_Mevina/Tob carry NavMeshAgent + AmbientNPC + TownsfolkBubble on the
//   prefab root (read from NPC_Peasant_Mevina.prefab). CastleTownsfolkInjector ADDS a
//   NavMeshAgent and an AmbientNPC when they are missing but NEVER adds a bubble - it
//   only calls GetComponentInChildren<TownsfolkBubble>() and skips SetBubble when the
//   body has none. So a body without a bubble would silently lose its speech panel.
//   These 14 therefore carry the same three components, making them drop-in
//   equivalents rather than near-equivalents.
//
// IDEMPOTENT - safe to run twice:
//   * An existing prefab is REUSED (its GUID, and therefore every reference to it,
//     survives); only drifted material bindings / missing components are re-asserted.
//   * An existing .mat is REUSED (GUID preserved). Structural properties (shader,
//     _BaseMap/_MainTex, keywords) are re-asserted because a wrong one is a bug; the
//     _Metallic / _Smoothness TASTE dials are written only on first creation so an
//     owner retune in the inspector survives a re-run, and are reported as preserved.
//   * Importers are reimported ONLY on an actual change.
//
// TO FORCE A CLEAN REBUILD: delete the .prefab / .mat and re-run.
//
// RUN:
//   Editor menu : Defenders/Art/Build CraftPix Townsfolk Bodies
//   Batchmode   : DeNelle.Editor.CraftPixPeopleBuilder.Build
//   Markers     : CRAFTPIX_PEOPLE_BUILD_OK / CRAFTPIX_PEOPLE_BUILD_FAIL
//                 (distinct to this entry point - a shared marker cannot say which
//                  step passed, which is the 2026-08-02 gate defect. The OK marker is
//                  emitted ONLY when ALL 14 bodies build: CastleTownsfolkInjector's
//                  BodyPool names all 14, and a missing one makes it fall back to a
//                  grey capsule placeholder in the middle of the owner's town.)
//
// DOES NOT TOUCH: NPC_Peasant_Mevina / NPC_Peasant_Tob / NPC_Blacksmith /
// NPC_Merchant (still referenced by seven other injectors - see the RESULT notes),
// any .unity scene, the KayKit or People packs, or structures-catalog.json.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DeNelle.Core.Diagnostics;   // FlowTrace / Guard - CLAUDE.md section 12
using DeNelle.Village;            // AmbientNPC / TownsfolkBubble (DeNelle.Editor references DeNelle.Village)
using UnityEditor;
using UnityEditor.Animations;     // AnimatorController - the shared townsfolk controller
using UnityEngine;
using UnityEngine.AI;             // NavMeshAgent

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor builder for the 14 CraftPix medieval townsfolk bodies: configures the
    /// FBX importers and the shared palette atlas, authors ONE URP/Lit material,
    /// binds it onto each model's skinned renderers and writes the 14
    /// Resources/NPCs/CraftPixPeople prefabs the townsfolk injector loads.
    /// Idempotent; prints CRAFTPIX_PEOPLE_BUILD_OK only when all 14 succeed.
    /// </summary>
    public static class CraftPixPeopleBuilder
    {
        // -- Markers (distinct per entry point) --------------------------------
        private const string MarkerOk   = "CRAFTPIX_PEOPLE_BUILD_OK";
        private const string MarkerFail = "CRAFTPIX_PEOPLE_BUILD_FAIL";
        private const string Tag        = "[CraftPixPeopleBuilder] ";
        private const string Sys        = "CraftPixPeople";   // FlowTrace category

        // -- Paths -------------------------------------------------------------
        /// <summary>Owner-staged source art (models + atlas + licence). NOT under Resources.</summary>
        public const string ArtDir = "Assets/Art/People/CraftPix";

        /// <summary>Where the loadable prefabs live. Resources-relative form is PrefabResourceRoot.</summary>
        public const string PrefabDir = "Assets/Resources/NPCs/CraftPixPeople";

        /// <summary>The Resources.Load prefix CastleTownsfolkInjector.BodyPool uses.</summary>
        public const string PrefabResourceRoot = "NPCs/CraftPixPeople/";

        /// <summary>The pack's single shared palette atlas.</summary>
        public const string AtlasPath = ArtDir + "/people_texture_map.png";

        /// <summary>The ONE material every CraftPix body shares.</summary>
        public const string SharedMaterialPath = ArtDir + "/CraftPixPeople.mat";

        /// <summary>The licence file that must travel with the art.</summary>
        public const string LicensePath = ArtDir + "/License.txt";

        // -- URP shader --------------------------------------------------------
        // Project-wide convention (KayKitMaterials, TripoMaterialFixer, every scene
        // builder): URP/Lit by name, Built-in "Standard" as the last-ditch fallback.
        // A wrong/absent shader ships MAGENTA, so this resolves ONCE up front and a
        // total miss FAILS the run rather than authoring on a null shader.
        private const string UrpLitShaderName   = "Universal Render Pipeline/Lit";
        private const string FallbackShaderName = "Standard";

        // -- Surface finish ----------------------------------------------------
        // Cloth / skin / leather: matte, non-metal. Written only on a freshly created
        // material so an owner retune survives a re-run.
        private const float ClothSmoothness = 0.08f;
        private const float ClothMetallic   = 0.0f;

        // -- Atlas import ------------------------------------------------------
        private const int AtlasMaxSize = 64;   // it IS 64x64; larger is upsampling

        // -- Upright sanity ----------------------------------------------------
        // WHY THIS MATTERS: CastleTownsfolkInjector.NormalizeToHeroHeight scales a body by
        // 1.95 / bounds.size.y. A model that imports LYING DOWN therefore gets its SHORT
        // axis stretched to hero height and spawns as a giant. So orientation must be
        // MEASURED before shipping, never assumed.
        //
        // ⚠ THE FIRST VERSION OF THIS CHECK WAS WRONG, and the measurement is what proved
        // it: aspect >= 1.2 failed 12 of 14 CraftPix bodies whose measured heights were
        // 1.76m..1.96m - i.e. correctly standing humans. The cause is that a SKINNED
        // character imports in BIND POSE with the arms outstretched, so the X extent is
        // ~1.6m and height/width lands near 1.1. A T-posed human genuinely has a squarish
        // bounding box; "well above 2" only describes a posed, arms-down mesh.
        //
        // The real question is not "is it slim?" but "is HEIGHT the dominant axis, and is
        // it a plausible human height?". Both are true for all 14 and both are false for a
        // body lying on its side (height would be the SHORTEST axis, ~0.5m).
        // -- Shared townsfolk animator ------------------------------------------
        // ONE controller for all 14 bodies, built from Assets/Action/Shared - the canon
        // library every Humanoid model in this project retargets (docs/ANIMATION_PIPELINE.md).
        // Its parameter names are NOT a choice: AmbientNPC.UpdateAnimator drives exactly
        // "Speed" (float, damped 0.08) and "IsTalking" (bool), and caches which of the two
        // the controller actually declares so it never drives an absent param (WO-163 -
        // that bug spammed 3,351 errors a run). Thresholds mirror AC_AmbientNPC_Mevina,
        // the shipped peasant controller, so the new bodies behave identically to the two
        // they replace - this pass upgrades what you SEE, never what the game DOES.
        private const string ControllerDir  = "Assets/Resources/NPCs/CraftPixPeople";
        private const string ControllerPath = ControllerDir + "/AC_CraftPixTownsfolk.controller";
        private const string IdleClipFbx    = "Assets/Action/Shared/Shared_Idle.fbx";
        private const string WalkClipFbx    = "Assets/Action/Shared/Shared_Walk_Forward.fbx";

        private const float UprightAspectMin = 1.02f;   // height must simply WIN, T-pose allowed
        private const float PlausibleHeightMin = 1.30f; // a lying-down human reads ~0.5m here
        private const float PlausibleHeightMax = 2.60f;

        /// <summary>One staged model and the prefab it produces.</summary>
        private readonly struct BodySpec
        {
            /// <summary>FBX file stem as CraftPix ships it (note: "citizzens" is THEIR spelling).</summary>
            public readonly string SourceStem;
            /// <summary>Prefab / GameObject name (our spelling, corrected).</summary>
            public readonly string PrefabStem;

            public BodySpec(string sourceStem, string prefabStem)
            {
                SourceStem = sourceStem;
                PrefabStem = prefabStem;
            }

            public string FbxPath        => ArtDir + "/" + SourceStem + ".fbx";
            public string PrefabPath     => PrefabDir + "/" + PrefabStem + ".prefab";
            /// <summary>The Resources-relative path the injector loads (no extension).</summary>
            public string ResourcesPath  => PrefabResourceRoot + PrefabStem;
        }

        // The 14 bodies, in the order BodyPool declares them. ALL must build for the OK
        // marker. Source stems are the CraftPix filenames verbatim (including their
        // "rich_citizzens" typo); prefab stems are ours and are spelled correctly.
        private static readonly BodySpec[] Bodies =
        {
            new BodySpec("city_dwellers_1",   "NPC_CityDweller_1"),
            new BodySpec("city_dwellers_2",   "NPC_CityDweller_2"),
            new BodySpec("peasant_1",         "NPC_Peasant_1"),
            new BodySpec("peasant_2",         "NPC_Peasant_2"),
            new BodySpec("peasant_3",         "NPC_Peasant_3"),
            new BodySpec("peasant_4",         "NPC_Peasant_4"),
            new BodySpec("peasant_5",         "NPC_Peasant_5"),
            new BodySpec("peasant_6",         "NPC_Peasant_6"),
            new BodySpec("rich_citizzens_1",  "NPC_RichCitizen_1"),
            new BodySpec("rich_citizzens_2",  "NPC_RichCitizen_2"),
            new BodySpec("rich_citizzens_3",  "NPC_RichCitizen_3"),
            new BodySpec("rich_citizzens_4",  "NPC_RichCitizen_4"),
            new BodySpec("king",              "NPC_King"),
            new BodySpec("queen",             "NPC_Queen"),
        };

        /// <summary>The Resources paths this builder produces, in declaration order. The
        /// regression suite reads this to prove BodyPool and the built set agree.</summary>
        public static string[] ExpectedResourcePaths()
        {
            var result = new string[Bodies.Length];
            for (int i = 0; i < Bodies.Length; i++) result[i] = Bodies[i].ResourcesPath;
            return result;
        }

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds all 14 townsfolk bodies (importer + shared atlas/material + prefab)
        /// and reports each model's rig, avatar and fit facts. Idempotent. Prints
        /// CRAFTPIX_PEOPLE_BUILD_OK only when EVERY body builds.
        /// </summary>
        [MenuItem("Defenders/Art/Build CraftPix Townsfolk Bodies")]
        public static void Build()
        {
            // CLAUDE.md section 12: enter/exit on the main build entry point, so one run
            // shows the whole path and exactly where it stopped.
            using var _ = FlowTrace.Enter(Sys, "build 14 CraftPix townsfolk bodies");

            var report = new StringBuilder();
            try
            {
                // The art is owner-staged by hand. Pick that up before anything asks the
                // AssetDatabase about it, or the first run after a copy sees nothing.
                AssetDatabase.Refresh();

                AssertStagedArt(report);

                Shader shader = ResolveShader(report);
                EnsureAtlasImport(report);
                Material shared = EnsureSharedMaterial(shader, report);
                EnsureFolder(PrefabDir);

                // BEFORE the bodies: they reference this controller, and a body saved with a
                // null controller renders its BIND POSE (the F8 "NPC Stuck in T Pose").
                BuildSharedController(report);

                // Build EVERY body before judging. A per-body failure is recorded and the
                // run continues, so one run reports all 14 verdicts instead of stopping at
                // the first - the difference between one look and fourteen.
                var results = new List<BodyResult>();
                foreach (var spec in Bodies)
                {
                    results.Add(BuildBody(spec, shared, report));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var failed = results.Where(r => !r.Ok).ToList();
                ReportBodies(results, report);

                if (failed.Count > 0)
                {
                    string why = string.Join(" | ", failed.Select(f => f.Spec.PrefabStem + ": " + f.Error));
                    FlowTrace.Fail(Sys, "build FAILED for " + failed.Count + " of " + Bodies.Length +
                                        " bodies -- " + why);
                    Debug.LogError(Tag + "FAILED (" + failed.Count + "/" + Bodies.Length + "): " + why);
                    Debug.LogError(MarkerFail + " - " + failed.Count + "/" + Bodies.Length +
                                   " bodies failed: " + why + " || progress: " + report);
                    return;
                }

                FlowTrace.Step(Sys, "all " + Bodies.Length + " townsfolk bodies built and verified.");
                Debug.Log(Tag + "DONE. " + report);
                Debug.Log(MarkerOk + " - " + Bodies.Length + " bodies || " + report);
            }
            catch (Exception e)
            {
                // NO success marker on any failure path - a partial set must never read as
                // green, because a missing body spawns a grey capsule in the owner's town.
                FlowTrace.Fail(Sys, "build threw " + e.GetType().Name + ": " + e.Message);
                Debug.LogError(Tag + "FAILED: " + e.Message + "\n" + e.StackTrace);
                Debug.LogError(MarkerFail + " - " + e.Message + " || progress: " + report);
            }
        }

        // =====================================================================
        //  Staged-art assertion
        // =====================================================================

        /// <summary>
        /// Proves the owner-staged art is actually on disk BEFORE anything is authored.
        /// The licence is asserted too: it is not decoration, it is the redistribution
        /// terms for art we are shipping, and it must never drift away from the models.
        /// </summary>
        private static void AssertStagedArt(StringBuilder report)
        {
            var missing = new List<string>();
            foreach (var spec in Bodies)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath) == null) missing.Add(spec.SourceStem + ".fbx");
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath) == null) missing.Add("people_texture_map.png");

            if (missing.Count > 0)
                throw new Exception("staged art missing under '" + ArtDir + "': " + string.Join(", ", missing) +
                                    ". This is owner-downloaded art that exists nowhere else - re-copy it from " +
                                    "the CraftPix pack's fbx/people_unity + texture folders before running.");

            string licenseFull = Path.Combine(Directory.GetCurrentDirectory(),
                                              LicensePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(licenseFull))
            {
                // A warning, not a throw: a missing licence file does not break a single
                // pixel, but shipping third-party art with no licence beside it is a real
                // problem and must never pass silently.
                FlowTrace.Warn(Sys, "License.txt is NOT staged at '" + LicensePath +
                                    "' - the CraftPix file-licence must travel with the art. Re-copy it.");
                report.Append("License.txt MISSING (re-copy it); ");
            }
            else
            {
                report.Append("14 fbx + atlas + License.txt staged; ");
            }

            FlowTrace.Step(Sys, "staged art verified: " + Bodies.Length + " FBX + the shared atlas under " + ArtDir + ".");
        }

        // =====================================================================
        //  Shader resolution (magenta guard)
        // =====================================================================

        /// <summary>
        /// Resolves URP/Lit BEFORE any material is authored. A null shader here is the
        /// magenta ship. Falls back to Built-in Standard with a loud warning (better than
        /// magenta, still wrong for URP) and hard-FAILS when neither resolves.
        /// </summary>
        private static Shader ResolveShader(StringBuilder report)
        {
            var shader = Shader.Find(UrpLitShaderName);
            if (shader != null)
            {
                report.Append("shader='").Append(shader.name).Append("' RESOLVED");
                if (!shader.isSupported)
                {
                    // Resolves by name but cannot compile -> still magenta/white on device.
                    report.Append(" but isSupported=FALSE (magenta/white risk)");
                    FlowTrace.Warn(Sys, "'" + UrpLitShaderName + "' resolved but isSupported=false - " +
                                        "the material is authored against it anyway; verify on device.");
                }
                report.Append("; ");
                FlowTrace.Step(Sys, "shader '" + shader.name + "' resolved (isSupported=" + shader.isSupported + ").");
                return shader;
            }

            var fallback = Shader.Find(FallbackShaderName);
            if (fallback == null)
                throw new Exception("neither '" + UrpLitShaderName + "' nor '" + FallbackShaderName +
                                    "' resolves via Shader.Find - is the Universal RP package present? " +
                                    "Refusing to author a material that would ship MAGENTA.");

            FlowTrace.Warn(Sys, "'" + UrpLitShaderName + "' NOT found - falling back to '" + FallbackShaderName +
                                "'. That is a Built-in-pipeline shader; URP will render it wrong. " +
                                "Fix the URP package before shipping.");
            report.Append("shader=FALLBACK '").Append(fallback.name).Append("' (URP/Lit missing!); ");
            return fallback;
        }

        // =====================================================================
        //  Atlas import
        // =====================================================================

        /// <summary>
        /// Brings the shared 64x64 palette atlas to the settings a palette map needs
        /// (Point / no mips / no compression / clamped / sRGB). See the header for why
        /// each one is a correctness choice rather than taste. Reimports only on a real
        /// change, so a second run is a no-op.
        /// </summary>
        private static void EnsureAtlasImport(StringBuilder report)
        {
            if (AssetImporter.GetAtPath(AtlasPath) is not TextureImporter ti)
                throw new Exception("no TextureImporter at '" + AtlasPath +
                                    "' - the shared atlas cannot be typed, and an untyped atlas " +
                                    "renders the whole town wrong.");

            var changes = new List<string>();

            if (ti.textureType != TextureImporterType.Default)
            { ti.textureType = TextureImporterType.Default; changes.Add("textureType=Default"); }

            if (!ti.sRGBTexture) { ti.sRGBTexture = true; changes.Add("sRGB=on"); }

            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changes.Add("mipmaps=off"); }

            if (ti.filterMode != FilterMode.Point)
            { ti.filterMode = FilterMode.Point; changes.Add("filter=Point"); }

            if (ti.wrapMode != TextureWrapMode.Clamp)
            { ti.wrapMode = TextureWrapMode.Clamp; changes.Add("wrap=Clamp"); }

            if (ti.textureCompression != TextureImporterCompression.Uncompressed)
            { ti.textureCompression = TextureImporterCompression.Uncompressed; changes.Add("compression=off"); }

            if (ti.maxTextureSize != AtlasMaxSize)
            { ti.maxTextureSize = AtlasMaxSize; changes.Add("maxSize=" + AtlasMaxSize); }

            if (ti.alphaSource != TextureImporterAlphaSource.None)
            { ti.alphaSource = TextureImporterAlphaSource.None; changes.Add("alpha=none"); }

            if (changes.Count > 0)
            {
                ti.SaveAndReimport();
                report.Append("atlas importer FIXED [").Append(string.Join(", ", changes)).Append("]; ");
                FlowTrace.Step(Sys, "atlas '" + AtlasPath + "' importer changed: " + string.Join(", ", changes) + ".");
            }
            else
            {
                report.Append("atlas importer already correct; ");
                FlowTrace.Step(Sys, "atlas importer already correct (idempotent no-op).");
            }
        }

        // =====================================================================
        //  Shared material
        // =====================================================================

        /// <summary>
        /// Authors (or reuses) the ONE URP/Lit material every CraftPix body shares.
        /// Structural properties are re-asserted every run because a wrong one is a
        /// rendering bug; the finish dials are written only on first creation.
        /// </summary>
        private static Material EnsureSharedMaterial(Shader shader, StringBuilder report)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null)
                throw new Exception("the shared atlas would not load from '" + AtlasPath +
                                    "' even though the file exists - the import failed. " +
                                    "Every body would render untextured.");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialPath);
            bool fresh = mat == null;
            if (fresh)
            {
                bool made = Guard.Try(Sys, "create shared material", () =>
                {
                    mat = new Material(shader) { name = "CraftPixPeople" };
                    AssetDatabase.CreateAsset(mat, SharedMaterialPath);
                });
                if (!made || mat == null)
                    throw new Exception("could not create the shared material at '" + SharedMaterialPath + "'.");
            }
            else if (mat.shader != shader)
            {
                FlowTrace.Warn(Sys, "'" + SharedMaterialPath + "' was on shader '" +
                                    (mat.shader != null ? mat.shader.name : "<null>") +
                                    "' - healing to '" + shader.name + "'.");
                mat.shader = shader;
            }

            // Set BOTH the URP name and the legacy alias: several runtime fixers in this
            // project read _MainTex first and only then _BaseMap, so a URP-only binding can
            // be invisible to a rebuild that actually ships.
            if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", atlas);
            if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", atlas);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", Color.white);
            mat.mainTexture = atlas;

            // The pack ships no normal / metallic / roughness maps at all, so there is
            // nothing to wire - and a stale keyword from a hand-edit would make URP sample
            // a slot that is now null. Turn them explicitly off.
            if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
            mat.DisableKeyword("_NORMALMAP");
            if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);   // 0 = Opaque

            if (fresh)
            {
                if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", ClothMetallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", ClothSmoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", ClothSmoothness);
                report.Append("shared material CREATED [_BaseMap=people_texture_map.png]; ");
            }
            else
            {
                float metal  = mat.HasProperty("_Metallic")   ? mat.GetFloat("_Metallic")   : ClothMetallic;
                float smooth = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : ClothSmoothness;
                report.Append("shared material REUSED (GUID preserved), finish PRESERVED (metallic=")
                      .Append(metal.ToString("0.##", CultureInfo.InvariantCulture))
                      .Append(", smoothness=").Append(smooth.ToString("0.##", CultureInfo.InvariantCulture))
                      .Append("); ");
            }

            EditorUtility.SetDirty(mat);
            FlowTrace.Step(Sys, "shared material " + (fresh ? "created" : "reused") + " at " + SharedMaterialPath + ".");
            return mat;
        }

        // =====================================================================
        //  Per-body build
        // =====================================================================

        private readonly struct BodyResult
        {
            public readonly BodySpec Spec;
            public readonly bool     Ok;
            public readonly string   Error;
            public readonly int      Renderers;
            public readonly int      Slots;
            public readonly int      Bones;
            public readonly float    RawHeight;
            public readonly float    Aspect;
            public readonly string   Avatar;

            public BodyResult(BodySpec spec, bool ok, string error, int renderers, int slots,
                              int bones, float rawHeight, float aspect, string avatar)
            {
                Spec = spec; Ok = ok; Error = error; Renderers = renderers; Slots = slots;
                Bones = bones; RawHeight = rawHeight; Aspect = aspect; Avatar = avatar;
            }

            public static BodyResult Failed(BodySpec spec, string error)
                => new BodyResult(spec, false, error, 0, 0, 0, 0f, 0f, "n/a");
        }

        private static BodyResult BuildBody(BodySpec spec, Material shared, StringBuilder report)
        {
            report.Append("| ").Append(spec.PrefabStem).Append(": ");

            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
                if (model == null)
                    return Fail(spec, report, "model not found at '" + spec.FbxPath + "'");

                if (AssetImporter.GetAtPath(spec.FbxPath) is not ModelImporter importer)
                    return Fail(spec, report, "'" + spec.FbxPath + "' has no ModelImporter (not recognised as a model?)");

                // -- 1. Importer.
                if (ConfigureImporter(importer, report))
                {
                    importer.SaveAndReimport();
                    report.Append("importer SAVED+REIMPORTED; ");
                }
                else
                {
                    report.Append("importer already correct; ");
                }

                string avatar = DescribeAvatar(spec, report);

                // -- 2. Prefab (GUID-preserving).
                EnsurePrefab(spec, report);

                // -- 3. Bind the shared material + the component parity set.
                var bound = ConfigurePrefab(spec, shared, report);

                // -- 4. Verify through the SAME path the injector uses.
                return VerifyAndMeasure(spec, shared, bound.renderers, bound.slots, bound.bones, avatar, report);
            }
            catch (Exception e)
            {
                return Fail(spec, report, e.GetType().Name + ": " + e.Message);
            }
        }

        private static BodyResult Fail(BodySpec spec, StringBuilder report, string why)
        {
            report.Append("FAILED (").Append(why).Append("); ");
            FlowTrace.Fail(Sys, spec.PrefabStem + ": " + why);
            return BodyResult.Failed(spec, why);
        }

        // =====================================================================
        //  Importer settings
        // =====================================================================

        /// <summary>
        /// Brings the ModelImporter to the state these skinned bodies need. Returns true
        /// when something actually changed (so a second run causes no reimport churn).
        /// </summary>
        private static bool ConfigureImporter(ModelImporter importer, StringBuilder report)
        {
            bool changed = false;

            // NO COLLIDER. A generated MeshCollider on a townsfolk body would block the
            // hero and fight the NavMesh; every NPC body in this project imports without
            // one (the injector's own capsule fallback even forces isTrigger).
            if (importer.addCollider) { importer.addCollider = false; changed = true; report.Append("addCollider->false; "); }

            // HUMANOID. This is CANON, not a preference - docs/ANIMATION_PIPELINE.md:
            // "Every model - heroes, enemies, anything authored later - is Humanoid ... Because
            // all clips carry the mixamorig skeleton, one clip retargets onto every model with no
            // per-character re-authoring."
            //
            // Generic was the wrong call and would have shipped the defect twice over. The pack
            // ships ZERO clips, and under this pipeline a model is never expected to bring its own:
            // it becomes Humanoid and inherits Assets/Action/Shared (Idle, Walk_Forward, Run,
            // turns, Death, Hit_Reaction, ...). GENERIC IS EXACTLY WHAT PREVENTS THAT - a Generic
            // avatar can only replay clips authored on its own bone hierarchy, and CraftPix uses
            // Unreal names (Spine_01 / Upperarm_L / Thigh_L) that match nothing else we own.
            //
            // A skinned humanoid with no usable controller renders its BIND POSE - that is the
            // owner's F8 2026-08-02 "NPC Stuck in T Pose", which KayKitNpcAnimatorSetup exists to
            // fix. Generic here would have reproduced it fourteen times.
            if (importer.animationType != ModelImporterAnimationType.Human)
            { importer.animationType = ModelImporterAnimationType.Human; changed = true; report.Append("animationType->Humanoid; "); }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            { importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; report.Append("avatar->CreateFromThisModel; "); }

            // THE PACK SHIPS NO CLIPS (zero AnimationStack objects in all 14 files, probed
            // byte-wise). Importing animation from a file that has none only produces an
            // empty clip list and slower imports.
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; report.Append("importAnimation->false; "); }

            // Nothing else in these files to import.
            if (importer.importBlendShapes) { importer.importBlendShapes = false; changed = true; report.Append("blendShapes->false; "); }
            if (importer.importCameras)     { importer.importCameras = false;     changed = true; report.Append("cameras->false; "); }
            if (importer.importLights)      { importer.importLights = false;      changed = true; report.Append("lights->false; "); }

            // MATERIALS: none. The import must not generate per-model materials - the ONE
            // shared atlas material is assigned onto the prefab's renderers instead, which
            // needs nothing from the importer and keeps 14 bodies on 1 material.
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; report.Append("materialImportMode->None; "); }

            return changed;
        }

        /// <summary>
        /// Reports what the import actually produced for the rig - skinned or not, bone
        /// count, avatar validity, whether Unity read it as human. MEASURED after the
        /// import, never asserted from the file name: this is the data a follow-up
        /// animation work order needs, and guessing at it is the banned inference-fix.
        /// </summary>
        private static string DescribeAvatar(BodySpec spec, StringBuilder report)
        {
            var avatars = AssetDatabase.LoadAllAssetsAtPath(spec.FbxPath).OfType<Avatar>().ToList();
            if (avatars.Count == 0)
            {
                FlowTrace.Warn(Sys, spec.PrefabStem + ": the import produced NO Avatar - the rig did not " +
                                    "survive. A future animation pass would have nothing to bind to.");
                report.Append("avatar=NONE; ");
                return "none";
            }

            var a = avatars[0];
            string desc = "valid=" + a.isValid + "/human=" + a.isHuman;
            report.Append("avatar ").Append(desc).Append("; ");
            if (!a.isValid)
                FlowTrace.Warn(Sys, spec.PrefabStem + ": the generated Avatar reports isValid=false - " +
                                    "no animation can be bound to this body until that is resolved.");
            return desc;
        }

        // =====================================================================
        //  Prefab authoring
        // =====================================================================

        /// <summary>
        /// Ensures the Resources prefab exists. An existing prefab is left in place so its
        /// GUID - and every reference to it - survives; only a missing one is created.
        /// </summary>
        private static void EnsurePrefab(BodySpec spec, StringBuilder report)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath) != null)
            {
                report.Append("prefab REUSED (GUID preserved); ");
                return;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
            GameObject instance = null;
            bool made = Guard.Try(Sys, "instantiate model for " + spec.PrefabStem, () =>
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            });
            if (!made || instance == null)
                throw new Exception("could not instantiate '" + spec.FbxPath + "' to author its prefab.");

            try
            {
                instance.name = spec.PrefabStem;
                PrefabUtility.SaveAsPrefabAsset(instance, spec.PrefabPath, out bool ok);
                if (!ok) throw new Exception("SaveAsPrefabAsset('" + spec.PrefabPath + "') failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            report.Append("prefab CREATED; ");
            FlowTrace.Step(Sys, "prefab created: " + spec.PrefabPath);
        }

        /// <summary>
        /// Binds the shared material onto every submesh of every renderer and installs the
        /// component parity set (NavMeshAgent + AmbientNPC + TownsfolkBubble). Saves ONLY
        /// when something actually changed, so a re-run writes nothing.
        /// </summary>
        private static (int renderers, int slots, int bones) ConfigurePrefab(
            BodySpec spec, Material shared, StringBuilder report)
        {
            int renderers = 0, slots = 0, bones = 0;
            var added = new List<string>();
            bool changed = false;

            GameObject contents = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
            try
            {
                foreach (var r in contents.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    Mesh mesh = MeshOf(r);
                    if (mesh == null)
                    {
                        // Not a defect on its own (an empty node can carry a renderer), but it
                        // must never be silent - a body that is ALL meshless renderers is the
                        // invisible-NPC bug, and the count below is what proves it is not.
                        FlowTrace.Warn(Sys, spec.PrefabStem + ": renderer '" + r.gameObject.name +
                                            "' has no mesh - skipped (nothing to paint).");
                        continue;
                    }
                    renderers++;
                    if (r is SkinnedMeshRenderer smr && smr.bones != null) bones = Mathf.Max(bones, smr.bones.Length);
                    if (PaintRenderer(r, shared, out int painted)) changed = true;
                    slots += painted;
                }

                if (renderers == 0)
                    throw new Exception("the prefab has no mesh-bearing Renderer to paint. Delete '" +
                                        spec.PrefabPath + "' and re-run for a clean rebuild.");

                // Component parity with NPC_Peasant_Mevina / _Tob (read from that prefab's
                // YAML). The injector adds the agent and the AmbientNPC itself when absent,
                // but NEVER adds a bubble - so without this the body silently loses its
                // speech panel and no error is ever logged.
                if (contents.GetComponent<NavMeshAgent>() == null)
                { contents.AddComponent<NavMeshAgent>(); added.Add("NavMeshAgent"); changed = true; }
                if (contents.GetComponentInChildren<AmbientNPC>(true) == null)
                { contents.AddComponent<AmbientNPC>(); added.Add("AmbientNPC"); changed = true; }
                if (contents.GetComponentInChildren<TownsfolkBubble>(true) == null)
                { contents.AddComponent<TownsfolkBubble>(); added.Add("TownsfolkBubble"); changed = true; }

                // THE CONTROLLER. Without one a skinned humanoid renders its BIND POSE - the
                // owner's F8 2026-08-02 "NPC Stuck in T Pose". The model FBX carries an Animator
                // (Humanoid import), so we only have to point it at the shared townsfolk
                // controller; the avatar is already on it from the import.
                var anim = contents.GetComponentInChildren<Animator>(true);
                if (anim == null)
                {
                    anim = contents.AddComponent<Animator>();
                    added.Add("Animator"); changed = true;
                }
                var sharedController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                if (sharedController == null)
                {
                    FlowTrace.Warn(Sys, spec.PrefabStem + ": shared townsfolk controller missing at '" +
                                        ControllerPath + "' - this body would stand in BIND POSE.");
                }
                else if (anim.runtimeAnimatorController != sharedController)
                {
                    anim.runtimeAnimatorController = sharedController;
                    added.Add("controller"); changed = true;
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, spec.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            if (changed) AssetDatabase.ImportAsset(spec.PrefabPath, ImportAssetOptions.ForceUpdate);

            report.Append(renderers).Append(" renderer(s)/").Append(slots).Append(" slot(s) bound");
            if (bones > 0) report.Append(", ").Append(bones).Append(" bones");
            if (added.Count > 0) report.Append(", added ").Append(string.Join("+", added));
            report.Append(changed ? "; " : " (already correct); ");

            return (renderers, slots, bones);
        }

        /// <summary>
        /// Builds (or refreshes) the ONE shared townsfolk animator controller from the canon
        /// Assets/Action/Shared library. Idempotent: recreated from scratch each run so a stale
        /// state graph can never survive, but written to the SAME path so prefab references hold.
        ///
        /// Two states only - Idle and Walk - because those are the clips that exist. There is NO
        /// talk clip in Shared, so "IsTalking" is DECLARED (AmbientNPC caches parameter presence
        /// and would otherwise never see it) but drives no state: substituting some other clip for
        /// speech would be a creative pick, which is the owner's call, never an implementer's.
        /// </summary>
        private static bool BuildSharedController(StringBuilder report)
        {
            using var _ = FlowTrace.Enter(Sys, "build shared townsfolk controller");

            AnimationClip idle = LoadFirstClip(IdleClipFbx);
            AnimationClip walk = LoadFirstClip(WalkClipFbx);
            if (idle == null || walk == null)
            {
                FlowTrace.Fail(Sys, "shared clips missing - idle='" + (idle != null) + "' walk='" + (walk != null) +
                                    "' from " + IdleClipFbx + " / " + WalkClipFbx +
                                    ". Without them every townsfolk body renders its BIND POSE.");
                report.Append("controller FAILED (shared clips missing); ");
                return false;
            }

            if (!Directory.Exists(ControllerDir)) Directory.CreateDirectory(ControllerDir);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            if (controller == null)
            {
                FlowTrace.Fail(Sys, "CreateAnimatorControllerAtPath returned null for " + ControllerPath);
                report.Append("controller FAILED (create returned null); ");
                return false;
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsTalking", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle");
            sIdle.motion = idle;
            var sWalk = sm.AddState("Walk");
            sWalk.motion = walk;
            sm.defaultState = sIdle;

            // Thresholds copied from AC_AmbientNPC_Mevina so the new bodies move exactly like
            // the two they replace (Idle->Walk on Speed > 0.05 over 0.15s; back on < 0.1 over 0.1s).
            var toWalk = sIdle.AddTransition(sWalk);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            var toIdle = sWalk.AddTransition(sIdle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.10f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.10f, "Speed");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            FlowTrace.Step(Sys, "controller built at " + ControllerPath + " (Idle='" + idle.name +
                                "', Walk='" + walk.name + "', params Speed+IsTalking).");
            report.Append("controller BUILT [Idle=").Append(idle.name).Append(", Walk=").Append(walk.name)
                  .Append(", Speed+IsTalking]; ");
            return true;
        }

        /// <summary>First real AnimationClip inside an FBX (skips Unity's __preview__ clips).</summary>
        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            foreach (var rep in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (rep is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    return clip;
            return null;
        }

        /// <summary>The mesh a renderer draws, or null when it has none.</summary>
        private static Mesh MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        /// <summary>
        /// Assigns <paramref name="mat"/> to EVERY submesh of <paramref name="r"/>. Returns
        /// true when the assignment actually changed something.
        /// <para/>
        /// Two things this gets right that a naive assignment does not:
        ///   * sharedMaterials RETURNS A COPY - mutating the read-back array is a silent
        ///     no-op, so a NEW array is built and assigned back to the property.
        ///   * the array is sized from the MESH's subMeshCount, not from whatever the import
        ///     left behind. A short array leaves trailing submeshes null, which renders as
        ///     the untextured slab this builder exists to prevent.
        /// </summary>
        private static bool PaintRenderer(Renderer r, Material mat, out int submeshes)
        {
            submeshes = Mathf.Max(1, MeshOf(r).subMeshCount);

            var current = r.sharedMaterials;
            bool same = current != null && current.Length == submeshes;
            if (same)
            {
                for (int i = 0; i < submeshes; i++)
                    if (current[i] != mat) { same = false; break; }
            }
            if (same) return false;

            var next = new Material[submeshes];
            for (int i = 0; i < submeshes; i++) next[i] = mat;
            r.sharedMaterials = next;   // assign BACK - the getter returns a copy
            return true;
        }

        // =====================================================================
        //  Verify + measure
        // =====================================================================

        /// <summary>
        /// Proves the prefab resolves through the path the INJECTOR uses
        /// (Resources.Load), that it satisfies the injector's own VerifyRenders contract,
        /// that every slot carries the shared material, and measures the body the way
        /// NormalizeToHeroHeight will - encapsulated Renderer.bounds - so a body that
        /// imports lying down is caught here instead of spawning as a giant.
        /// </summary>
        private static BodyResult VerifyAndMeasure(BodySpec spec, Material shared, int renderers,
                                                   int slots, int bones, string avatar, StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
                return Fail(spec, report, "prefab '" + spec.PrefabPath + "' would not load back after authoring");

            // The injector loads by Resources path, not asset path - prove THAT works.
            var viaResources = Resources.Load<GameObject>(spec.ResourcesPath);
            if (viaResources == null)
                return Fail(spec, report, "Resources.Load(\"" + spec.ResourcesPath + "\") returned null even " +
                                          "though the asset exists - CastleTownsfolkInjector would fall back " +
                                          "to a grey capsule placeholder");

            GameObject probe = null;
            bool made = Guard.Try(Sys, "instantiate " + spec.PrefabStem + " to measure", () =>
            {
                probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            });
            if (!made || probe == null)
                return Fail(spec, report, "could not instantiate the prefab to measure it");

            float height, aspect;
            string slotProblem = null;
            try
            {
                var rends = probe.GetComponentsInChildren<Renderer>(true)
                                 .Where(r => r != null && MeshOf(r) != null).ToList();
                if (rends.Count == 0)
                {
                    UnityEngine.Object.DestroyImmediate(probe);
                    return Fail(spec, report, "no mesh-bearing renderer survived onto the prefab - the injector's " +
                                              "VerifyRenders would drop this body and use a placeholder");
                }

                foreach (var r in rends)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) { slotProblem = "renderer '" + r.gameObject.name + "' submesh " + i + " has a NULL material (renders magenta)"; break; }
                        if (mats[i] != shared) { slotProblem = "renderer '" + r.gameObject.name + "' submesh " + i + " is not on the shared material"; break; }
                    }
                    if (slotProblem != null) break;
                }

                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Count; i++) b.Encapsulate(rends[i].bounds);
                height = b.size.y;
                aspect = b.size.y / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.z));
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }

            if (slotProblem != null) return Fail(spec, report, slotProblem);

            report.Append("h=").Append(height.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append("m aspect=").Append(aspect.ToString("0.00", CultureInfo.InvariantCulture)).Append("; ");

            // Two independent tells, both of which a lying-down body fails and a T-posed
            // standing body passes: height must be the DOMINANT axis, and it must be a
            // plausible human height. A body on its side reads height ~0.5m AND aspect < 1.
            if (aspect < UprightAspectMin || height < PlausibleHeightMin || height > PlausibleHeightMax)
                return Fail(spec, report, "does NOT import upright (height " +
                                          height.ToString("0.00", CultureInfo.InvariantCulture) + "m, aspect " +
                                          aspect.ToString("0.00", CultureInfo.InvariantCulture) +
                                          "). Height must be the dominant axis (>= " +
                                          UprightAspectMin.ToString("0.00", CultureInfo.InvariantCulture) +
                                          ") and a plausible human height (" +
                                          PlausibleHeightMin.ToString("0.0", CultureInfo.InvariantCulture) + ".." +
                                          PlausibleHeightMax.ToString("0.0", CultureInfo.InvariantCulture) +
                                          "m). CastleTownsfolkInjector.NormalizeToHeroHeight scales by " +
                                          "1.95 / bounds.size.y, so a body lying down would have its SHORT " +
                                          "axis stretched to hero height and spawn as a giant. NOTE a T-posed " +
                                          "bind mesh is squarish by nature (arms out ~1.6m) - that is NOT " +
                                          "lying down and must not fail here");

            FlowTrace.Step(Sys, spec.PrefabStem + " OK: " + renderers + " renderer(s), " + slots +
                                " slot(s), " + bones + " bone(s), avatar " + avatar + ", height " +
                                height.ToString("0.00", CultureInfo.InvariantCulture) + "m.");

            return new BodyResult(spec, true, null, renderers, slots, bones, height, aspect, avatar);
        }

        // =====================================================================
        //  Reporting
        // =====================================================================

        private static void ReportBodies(List<BodyResult> results, StringBuilder report)
        {
            var ok = results.Where(r => r.Ok).ToList();
            if (ok.Count == 0) { report.Append("|| NO body built. "); return; }

            float minH = ok.Min(r => r.RawHeight);
            float maxH = ok.Max(r => r.RawHeight);
            int totalSlots = ok.Sum(r => r.Slots);

            report.Append("|| SUMMARY: ").Append(ok.Count).Append('/').Append(results.Count)
                  .Append(" bodies, ").Append(totalSlots).Append(" submesh slot(s) on ONE shared material, ")
                  .Append("raw height ").Append(minH.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append("m..").Append(maxH.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append("m (the injector re-scales every body to 1.95m at spawn); ")
                  .Append("NO animation clips ship with this pack - bodies stand in bind pose until a ")
                  .Append("follow-up work order supplies clips. ");

            FlowTrace.Step(Sys, "summary: " + ok.Count + "/" + results.Count + " bodies built; raw heights " +
                                minH.ToString("0.00", CultureInfo.InvariantCulture) + "m to " +
                                maxH.ToString("0.00", CultureInfo.InvariantCulture) + "m.");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
            string leaf = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
