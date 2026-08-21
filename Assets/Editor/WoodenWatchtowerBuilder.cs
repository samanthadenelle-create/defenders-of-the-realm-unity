// =============================================================================
// WoodenWatchtowerBuilder (owner 2026-08-06: "New Wooden Tower Level 1", then L2,
// then L3) - SCRIPT-authors the ASSET half of the all-wood archer-tower ladder:
// importer settings + URP/Lit materials + material remaps + the three
// Resources/Structures prefabs the catalog's visualPrefabPath / upgradeVisualPath
// resolve to.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// WHY THIS EXISTS (the whole point):
//   Every step below touches a .prefab / .mat / .meta. Hand-editing that YAML is
//   banned (CLAUDE.md section 0 + section 3 - mount garble + resave corruption
//   history), so the work goes through AssetDatabase / ModelImporter /
//   PrefabUtility and UNITY owns the serialization. Nothing here writes a byte of
//   YAML itself.
//
// THE LADDER IT BUILDS (all three levels of tower_ground_archer):
//   L1 -> Structures/Tower_Wooden_Watchtower      (catalog visualPrefabPath)
//   L2 -> Structures/Tower_Wooden_Watchtower_L2   (repo.upgradeVisualPath[0])
//   L3 -> Structures/Tower_Wooden_Watchtower_L3   (repo.upgradeVisualPath[1])
//   It replaces the Castle_Round / Castle_Square / Medieval_Big polyperfect ladder.
//   heightMul is NOT touched: 1.2 is the owner-ruled cadence ANCHOR the whole
//   structure family is expressed against (see the row's _heightNote).
//
// TWO DIFFERENT ASSET CLASSES, ONE MATERIAL TREATMENT:
//   L1 and L3 are single-mesh Tripo exports with a full PBR set (basecolor +
//   normal + metallic + roughness + a packed "rm"). L2 is a 9-part Tripo export
//   with BASECOLOR ONLY - no normal, no metallic, no roughness on any part.
//
//   THE METALLIC/ROUGHNESS-vs-SMOOTHNESS DECISION (read this before "improving" it):
//   URP/Lit wants a METALLIC-SMOOTHNESS map (metallic in R, SMOOTHNESS in ALPHA,
//   slot _MetallicGlossMap); the asset ships SEPARATE metallic and roughness JPEGs,
//   and JPEG has no alpha. Wiring a roughness map into a smoothness slot INVERTS it
//   and the tower reads as wet plastic, so that was never on the table. The two real
//   options were (a) pack metallic + inverted-roughness into a new RGBA texture at
//   build time, or (b) drive _Metallic / _Smoothness as SCALARS and wire only
//   basecolor + normal.
//
//   WE CHOSE (b), and the deciding evidence is runtime code, not taste:
//   SkinOptions.Structure sets FixTripoMaterials = true (VisualFactory.cs:66), so
//   EVERY placed structure - Create AND ReskinForLevel, both via OptsFor - gets a
//   DeNelle.Core.TripoMaterialFixer. That component rebuilds EVERY material slot as
//   a fresh URP/Lit (TripoMaterialFixer.GetOrCreateSharedMaterial) carrying across
//   ONLY _BaseMap/_MainTex, _BumpMap and emission, then writes _Smoothness and
//   _Metallic as SCALARS from its own fields (0.15f / 0f). It never reads
//   _MetallicGlossMap. A packed metallic-smoothness map would therefore be
//   DISCARDED on every archer tower the player ever places - real build weight for
//   zero pixels. Scalars are what actually ships, so scalars are what we author, and
//   we author the FIXER'S OWN numbers so the editor preview and the in-game look
//   agree instead of drifting.
//
//   The normal map IS worth wiring: the fixer explicitly preserves _BumpMap and
//   re-enables _NORMALMAP, so it survives to the player.
//
//   The metallic / roughness / rm JPEGs are consequently UNREFERENCED. They are
//   owner-staged files under Resources/ (i.e. they ship), so this builder REPORTS
//   their payload rather than deleting owner art on its own authority.
//
// FBX IMPORTER CONVENTION (read from the tree, not invented):
//   Every existing structure FBX here (ArcaneSpire_1/_2, WizardTower_1,
//   GenericContainer) imports with addColliders = 0. SkinOptions.Structure does NOT
//   set StripColliders (only SkinOptions.Enemy does), so a collider generated on a
//   structure prop would SURVIVE onto the placed tower and fight PlacementRules /
//   the navmesh blocker. This builder therefore asserts addCollider = false and
//   never generates one. Scale is left on the file's own units (useFileScale) for
//   the same reason the catalog does not care: StructureFactory fits every model to
//   YHeightVariable * heightMul at runtime, so the authored size is irrelevant -
//   only the model's PROPORTIONS matter, and those are reported below.
//
// ORIENTATION - THE -90 IS BAKED INTO ALL THREE PREFABS, NOT INTO THE CATALOG:
//   The owner measured all three in Offset Forge. This builder READS
//   Assets/OffsetForge/offsets.json as the source of truth and never hardcodes the
//   numbers, so a re-measure flows through on the next run. As of 2026-08-06 all
//   three read rot (-90, 0, 0) - uniform, no per-level special case.
//
//   READ THIS BEFORE MOVING IT INTO THE CATALOG ROW - two independent reasons:
//
//   (a) A TIER MODEL NEVER RECEIVES THE CATALOG ORIENTATION. ReskinForLevel
//       deliberately does not apply entry.orientation, and says so in its own
//       comment: "Tier models rely on their prefab-native orientation" - written
//       after F8-2 2026-07-07, when tower_wall_wizard's Tripo BASE needed Z-90
//       while its polyperfect L2 was already upright. There is ONE orientation
//       block on the row and THREE models behind it, so the row can only ever
//       serve the base visual. L2 and L3 are reachable ONLY through ReskinForLevel,
//       so a catalog-only correction leaves both of them lying on their side.
//
//   (b) THE CATALOG ORIENTATION IS APPLIED **AFTER** THE FIT, NOT BEFORE.
//       VisualFactory.Skin does Fit + SeatOnGround internally before it returns
//       (VisualFactory.cs:159-169); StructureFactory.Create only then applies
//       entry.orientation (StructureFactory.cs:145-169), and the only thing that
//       runs afterwards is ReseatCorrectedBottom, which TRANSLATES in Y and never
//       re-fits. Create's own comment concedes the pose it measured: "SeatOnGround
//       already seated the RAW (un-corrected, often lying-down) bounds". So a model
//       that imports lying down is fit-to-height on its SHORT axis: L2 measures
//       0.519 instead of 1.000, giving scale 4.8/0.519 = 9.25x instead of 4.80x,
//       and the later rotation stands up a 9.25 m tower - 1.93x oversized. The
//       float was fixed; the SIZE never was. Rotating in the PREFAB puts the
//       correction UPSTREAM of Skin's measurement, which is the only place a -90
//       can be applied without corrupting the fit.
//
//   THE PREFAB ROOT CANNOT CARRY IT EITHER: VisualFactory.Skin sets
//   go.transform.localRotation = identity on the instantiated prefab ROOT
//   (VisualFactory.cs:140). A rotation baked there is stomped every single time.
//   So each prefab is a WRAPPER whose name matches the prefab stem (ReskinForLevel
//   matches tiers with child.name.StartsWith(stem)), with the model as a CHILD
//   carrying the rotation. The wrapper root stays identity, as Skin demands.
//
//   Y AND Z ARE DROPPED ON PURPOSE (owner ruling 2026-08-06): X is a DEFECT
//   correction - broken art lying on its side, which must be fixed before the fit
//   measures it. Y/Z is a FACING PREFERENCE, and facing is the player's own choice
//   at placement; baking one would fight the placement yaw and pre-turn every
//   spawn. (After a -90 X the model's original up-axis becomes Z, so Z is the yaw
//   for this family - which is why five of the seven rows the owner zeroed on
//   2026-08-06 carried their facing on Z, not Y.) This builder zeroes BOTH even if
//   a future re-measure records one, and logs that it did.
//
//   *** THE DOUBLE-ROTATION TRAP (tower_arcane_spire, and why this run VERIFIES
//   RATHER THAN TRUSTS) ***
//   Of the 14 manual orientation rows in the catalog, 13 are exactly (-90,0,0) and
//   ONE - tower_arcane_spire / ArcaneSpire_1 - is (0,0,0) with the note "after the
//   Tripo extraction reimport ArcaneSpire_1 stands natively; the earlier
//   (-90,90,90) double-rotated it." So a Tripo model's IMPORT POSE CAN CHANGE
//   ACROSS A REIMPORT, and a correction measured before one can be exactly wrong
//   after it. This builder reimports (it must, to bind the materials).
//   Therefore it does NOT trust the number blindly:
//     * it measures the AS-IMPORTED aspect (height / max(width,depth)) FIRST and
//       REFUSES to bake a rotation onto a model that is already standing - that is
//       precisely the ArcaneSpire double-rotation, caught before it ships;
//     * it measures again AFTER the bake and FAILS the run unless the result is
//       genuinely upright (aspect > UprightAspectMin). A tower on its side reads
//       ~0.5 here and can never pass.
//   Note this builder never calls ExtractTextures / SearchAndRemapMaterials - the
//   colour fix below assigns materials onto the PREFAB's renderers and touches no
//   transform and no importer - so it does not re-run the path that re-posed the
//   spire. The guard exists because a reimport can happen at all, not because we
//   invoke that path.
//
// MATERIAL BINDING IS ON THE PREFAB'S RENDERERS (not an importer remap):
//   The FBXs reference their textures through a Tripo ".fbm" relative path that does
//   not exist in this project, so Unity's auto-import binds a NULL albedo - the
//   classic white-structure symptom (same class as WO-719 arcane spire and the white
//   Knight).
//
//   THE FIRST ATTEMPT WAS ModelImporter.AddRemap (the KayKitMaterials pattern). IT
//   CANNOT WORK HERE, and the builder's own run proved it rather than any theory:
//     WOODEN_WATCHTOWER_BUILD_FAIL - L1: the FBX exposes NO material slot at all
//   AddRemap keys against an importer-exposed sub-material identifier. These models
//   import under materialImportMode=ImportViaMaterialDescription with NO embedded
//   textures to describe, so the import surfaces ZERO extractable material
//   sub-assets - LoadAllAssetRepresentationsAtPath returns none and
//   GetExternalObjectMap is empty. No key exists, so there is nothing to remap onto,
//   and no amount of coaxing the importer changes that. (The FBX itself is NOT
//   materialless - it declares 1 material on L1/L3 and 9 on L2; it is the IMPORT that
//   yields no slots.)
//
//   SO WE BIND WHERE THE RUNTIME ACTUALLY READS: this builder authors the prefab, so
//   it walks the prefab's own Renderers and assigns the authored URP/Lit material(s)
//   to sharedMaterials directly. That needs no importer cooperation whatsoever, and
//   Renderer.sharedMaterials is exactly what TripoMaterialFixer re-reads at placement.
//
//   TWO TRAPS, both handled below:
//     * Renderer.sharedMaterials RETURNS A COPY. Mutating the returned array in place
//       silently does nothing - the array must be rebuilt and ASSIGNED BACK.
//     * The array must be ONE MATERIAL PER SUBMESH. A short array leaves trailing
//       submeshes with a null material, which renders as the untextured/white slab
//       this whole exercise exists to prevent. Length comes from the mesh's
//       subMeshCount, never from whatever the import happened to leave behind.
//
//   L2's nine parts are mapped from the RENDERER side now (the importer's material
//   list does not exist): each part's index is recovered from its own GameObject
//   name. Ambiguity is a hard FAIL - painting the roof texture onto the ladder looks
//   deliberate and is worse than an untextured tower.
//
//   THE IMPORTER IS LEFT ALONE. Nothing here needs it, and every reimport is a chance
//   to re-pose a Tripo model (see the double-rotation trap below), so the fewer the
//   better. The builder only ASSERTS addCollider is false and otherwise reports.
//
// IDEMPOTENT - safe to run twice:
//   * An existing prefab is REUSED and left structurally untouched (its GUID, and
//     therefore the catalog reference, survives); owner-tuned root scale/rotation is
//     PRESERVED and reported, never stomped.
//   * An existing .mat is REUSED (GUID preserved). Structural properties (shader,
//     _BaseMap, _BumpMap, keywords) are re-asserted because a wrong one is a bug;
//     the _Metallic / _Smoothness TASTE dials are written only on first creation and
//     reported as preserved thereafter.
//   * A remap that already points at the right material is left alone, so no
//     needless reimport churn.
//
// TO FORCE A CLEAN REBUILD: delete the .prefab / .mat for that level and re-run.
//
// RUN:
//   Editor menu : Defenders/Catalog/Build Wooden Watchtower Ladder
//   Batchmode   : DeNelle.Editor.WoodenWatchtowerBuilder.Build
//   Markers     : WOODEN_WATCHTOWER_BUILD_OK  /  WOODEN_WATCHTOWER_BUILD_FAIL
//                 (distinct to this entry point - a shared marker cannot say which
//                  step passed, which is the 2026-08-02 gate defect. The OK marker
//                  is emitted ONLY when ALL THREE levels build: a partial ladder is
//                  worse than none, because the player would upgrade into a missing
//                  prefab and StructureFactory.ReskinForLevel would keep the old
//                  visual while the stats stepped.)
//
// DOES NOT TOUCH: structures-catalog.json, CatalogBootstrap, heightMul, any .unity
// scene, the polyperfect pack, or the retired Castle_Round / Castle_Square /
// Medieval_Big prefabs (other builders still consume those - CastleHubBuilder,
// VillageSceneBuilder.Walls, TowerDataSeeder, EnemyStrongholdBuilder).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DeNelle.Core;        // MagentaGuard.IsBrokenShader - the SINGLE authority (see VerifySlots)
using OffsetForge;         // OffsetTable / OffsetTableIO - the owner's measured rotations
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor builder for the all-wood archer-tower ladder: configures the three
    /// Tripo FBX importers, authors their URP/Lit materials, remaps them onto the
    /// models and writes the three Resources/Structures prefabs the catalog loads.
    /// Idempotent; prints WOODEN_WATCHTOWER_BUILD_OK only when all three succeed.
    /// </summary>
    public static class WoodenWatchtowerBuilder
    {
        // -- Markers (distinct per entry point) --------------------------------
        private const string MarkerOk   = "WOODEN_WATCHTOWER_BUILD_OK";
        private const string MarkerFail = "WOODEN_WATCHTOWER_BUILD_FAIL";
        private const string Tag        = "[WoodenWatchtowerBuilder] ";

        // -- Paths -------------------------------------------------------------
        private const string StructuresDir = DeNelle.Core.AssetRoots.StructureContent;

        // -- URP shader --------------------------------------------------------
        // The project-wide convention (KayKitMaterials, MagentaMaterialFixer,
        // TripoMaterialFixer, every scene builder): URP/Lit by name, with the
        // Built-in "Standard" as the last-ditch fallback. A wrong/absent shader
        // ships MAGENTA, so this is resolved ONCE up front and a total miss FAILS
        // the run rather than authoring materials on a null shader.
        private const string UrpLitShaderName  = "Universal Render Pipeline/Lit";
        private const string FallbackShaderName = "Standard";

        // -- Surface finish ----------------------------------------------------
        // NOT taste: these are TripoMaterialFixer's own serialized defaults
        // (_smoothness = 0.15f, _metallic = 0f). That component rebuilds every
        // structure material at runtime and writes exactly these, so authoring the
        // same numbers means the editor preview equals the shipped look. Change
        // these only together with the fixer's fields, or the two will disagree
        // again. For reference, the measured mean of the shipped roughness maps is
        // ~0.76 (=> smoothness ~0.24) on both L1 and L3, i.e. the same wood family.
        private const float WoodSmoothness = 0.15f;
        private const float WoodMetallic   = 0.0f;

        // -- The cadence anchor, for the fit report only (never written) --------
        // Mirrors StructureFactory.YHeightVariable (4 m) * tower heightMul (1.2).
        // This builder REPORTS against it; it does not and must not set it.
        private const float YHeightVariable  = 4f;
        private const float TowerHeightMul   = 1.2f;
        private const float DocumentedAnchorMetres = 2.778f;  // the row's _heightNote claim
        private const float GridCellMetres   = 3f;            // PlacementGrid cell size

        // -- Orientation ------------------------------------------------------
        // The owner's Offset Forge export. SOURCE OF TRUTH for the upright
        // correction; a re-measure flows through on the next run with no code edit.
        private const string OffsetsPath = "Assets/OffsetForge/offsets.json";

        // "Is this model standing up?" = bounds height / max(width, depth).
        // MEASURED, not guessed: these three towers read 1.70-1.92 upright and
        // ~0.52-0.59 lying down, so 1.2 separates the two states with a wide margin
        // and cannot be satisfied by a tower on its side. Used twice - to refuse a
        // rotation on an already-standing model (the ArcaneSpire double-rotation
        // trap) and to prove the baked result actually stands.
        private const float UprightAspectMin = 1.2f;

        // -- How a level's textures are laid out -------------------------------
        private enum TexLayout
        {
            /// <summary>Single mesh, full PBR set. Basecolor + normal are wired; the
            /// metallic / roughness / rm maps are deliberately unreferenced (see header).</summary>
            SinglePbr,

            /// <summary>Tripo multi-part export, BASECOLOR ONLY per part. No normal,
            /// no metallic, no roughness anywhere - scalars carry the finish.</summary>
            TripoPartsBasecolorOnly,
        }

        private readonly struct LevelSpec
        {
            public readonly int    Level;
            public readonly string FbxPath;
            public readonly string PrefabPath;
            public readonly string TexDir;
            public readonly string TexPrefix;    // filename stem before the map suffix
            public readonly string MatStem;      // .mat basename (must match .gitignore negations)
            public readonly TexLayout Layout;

            public LevelSpec(int level, string stem, string texDir, string texPrefix, TexLayout layout)
            {
                Level      = level;
                FbxPath    = StructuresDir + "/" + stem + ".fbx";
                PrefabPath = StructuresDir + "/" + stem + ".prefab";
                TexDir     = StructuresDir + "/" + texDir;
                TexPrefix  = texPrefix;
                MatStem    = stem;
                Layout     = layout;
            }

            /// <summary>The Resources-relative path the catalog uses (no extension).</summary>
            public string ResourcesPath =>
                "Structures/" + Path.GetFileNameWithoutExtension(PrefabPath);
        }

        // The ladder. Ordered L1 -> L3; ALL must build for the OK marker.
        private static readonly LevelSpec[] Levels =
        {
            new LevelSpec(1, "Tower_Wooden_Watchtower",
                          "Tower_Wooden_Watchtower_Tex",    "WoodenWatchtower_",
                          TexLayout.SinglePbr),
            new LevelSpec(2, "Tower_Wooden_Watchtower_L2",
                          "Tower_Wooden_Watchtower_L2_Tex", "WoodenWatchtowerL2_part_",
                          TexLayout.TripoPartsBasecolorOnly),
            new LevelSpec(3, "Tower_Wooden_Watchtower_L3",
                          "Tower_Wooden_Watchtower_L3_Tex", "WoodenWatchtowerL3_",
                          TexLayout.SinglePbr),
        };

        // Tripo names its per-part materials "tripo_part_<N>_material"; Unity may
        // rename them after the bound texture ("..._tripo_part_<N>_basecolor"). BOTH
        // carry the same index, and the FBX's own connection graph binds part_<N>'s
        // material to part_<N>'s basecolor - so the index IS the mapping, read off the
        // model rather than guessed from submesh order.
        private static readonly Regex PartIndexRx =
            new Regex(@"part[_\-\s]?(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds all three wooden-watchtower levels (importer + material(s) + remap
        /// + prefab) and reports each model's fit ratio and footprint. Idempotent.
        /// Prints WOODEN_WATCHTOWER_BUILD_OK only when EVERY level succeeds.
        /// </summary>
        [MenuItem("Defenders/Catalog/Build Wooden Watchtower Ladder")]
        public static void Build()
        {
            var report = new StringBuilder();
            try
            {
                Shader shader = ResolveShader(report);

                // Build every level before judging: a per-level throw aborts the run
                // (no partial-ladder OK marker), but the measurements of the levels
                // that DID build are still logged above the failure for triage.
                var results = new List<LevelResult>();
                foreach (var spec in Levels)
                {
                    results.Add(BuildLevel(spec, shader, report));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                ReportLadder(results, report);

                Debug.Log(Tag + "DONE. " + report);
                Debug.Log(MarkerOk + " - " + report);
            }
            catch (Exception e)
            {
                // NO success marker on any failure path - a partial ladder must never
                // read as green (the player would upgrade into a missing prefab).
                Debug.LogError(Tag + "FAILED: " + e.Message + "\n" + e.StackTrace);
                Debug.LogError(MarkerFail + " - " + e.Message + " || progress: " + report);
            }
        }

        // =====================================================================
        //  Shader resolution (magenta guard)
        // =====================================================================

        /// <summary>
        /// Resolves the URP/Lit shader BEFORE any material is authored. A null shader
        /// here is the magenta ship: every material this builder writes would carry
        /// Hidden/InternalErrorShader. Falls back to Built-in Standard with a loud
        /// warning (better than magenta, still wrong for URP), and hard-FAILS when
        /// neither resolves.
        /// </summary>
        private static Shader ResolveShader(StringBuilder report)
        {
            var shader = Shader.Find(UrpLitShaderName);
            if (shader != null)
            {
                report.Append("shader='").Append(shader.name).Append("' RESOLVED");
                if (!shader.isSupported)
                {
                    // A shader that resolves by name but cannot compile still renders
                    // magenta/white on device - TripoMaterialFixer.VerifyAllRenderersUrp
                    // flags exactly this case at runtime. Say it now, not there.
                    report.Append(" but reports isSupported=FALSE (magenta/white risk)");
                    Debug.LogWarning(Tag + "'" + UrpLitShaderName + "' resolved but isSupported=false - " +
                                     "materials will be authored against it anyway, but verify on device.");
                }
                report.Append("; ");
                Debug.Log(Tag + "shader '" + shader.name + "' resolved (isSupported=" + shader.isSupported + ").");
                return shader;
            }

            var fallback = Shader.Find(FallbackShaderName);
            if (fallback == null)
                throw new Exception("neither '" + UrpLitShaderName + "' nor '" + FallbackShaderName +
                                    "' resolves via Shader.Find - is the Universal RP package present? " +
                                    "Refusing to author materials that would ship MAGENTA.");

            Debug.LogWarning(Tag + "'" + UrpLitShaderName + "' NOT found - falling back to '" +
                             FallbackShaderName + "'. That is a Built-in-pipeline shader; URP will " +
                             "render it wrong. Fix the URP package before shipping.");
            report.Append("shader=FALLBACK '").Append(fallback.name).Append("' (URP/Lit missing!); ");
            return fallback;
        }

        // =====================================================================
        //  Per-level build
        // =====================================================================

        private readonly struct LevelResult
        {
            public readonly LevelSpec Spec;
            public readonly Vector3   RawSize;      // model bounds at import scale
            public readonly float     FitScale;     // uniform scale to reach 4.8 m tall
            public readonly Vector2   FittedXZ;     // footprint after the fit
            public readonly int       Renderers;
            public readonly int       Slots;

            public LevelResult(LevelSpec spec, Vector3 rawSize, float fitScale,
                               Vector2 fittedXZ, int renderers, int slots)
            {
                Spec = spec; RawSize = rawSize; FitScale = fitScale;
                FittedXZ = fittedXZ; Renderers = renderers; Slots = slots;
            }

            public float FittedFootprint => Mathf.Max(FittedXZ.x, FittedXZ.y);
        }

        private static LevelResult BuildLevel(LevelSpec spec, Shader shader, StringBuilder report)
        {
            report.Append("| L").Append(spec.Level).Append(": ");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
            if (model == null)
                throw new Exception("L" + spec.Level + ": model not found at '" + spec.FbxPath +
                                    "'. The owner stages these by hand - re-copy it before running.");

            if (AssetImporter.GetAtPath(spec.FbxPath) is not ModelImporter importer)
                throw new Exception("L" + spec.Level + ": '" + spec.FbxPath +
                                    "' has no ModelImporter (not recognised as a model?).");

            // -- 1. Importer: assert the convention only. Nothing here depends on the
            //       importer producing materials (it does not), and every reimport is a
            //       chance to re-pose a Tripo model - so we touch it as little as possible.
            if (ConfigureImporter(importer, spec, report))
            {
                importer.SaveAndReimport();
                report.Append("importer SAVED+REIMPORTED; ");
            }
            else
            {
                report.Append("importer already correct (no reimport); ");
            }

            // -- 2. Prefab (GUID-preserving) + the owner-measured upright correction.
            //       The prefab is authored BEFORE the materials because the materials are
            //       bound to ITS renderers.
            Vector3 upright = LoadUprightCorrection(spec, report);
            AssertCorrectionIsNeeded(spec, upright, report);
            EnsurePrefab(spec, upright, report);

            // -- 3. Author the material(s) and bind them onto the prefab's renderers.
            BindMaterials(spec, shader, report);

            // -- 4. Verify + measure through the SAME path the runtime uses.
            return VerifyAndMeasure(spec, report);
        }

        // =====================================================================
        //  Importer settings (assert-only)
        // =====================================================================

        /// <summary>
        /// Brings the ModelImporter to the state the remap needs, and ASSERTS the
        /// project's structure-FBX convention. Returns true when something changed.
        /// </summary>
        private static bool ConfigureImporter(ModelImporter importer, LevelSpec spec, StringBuilder report)
        {
            bool changed = false;

            // NO COLLIDER. Every existing structure FBX in this project imports with
            // addColliders = 0, and SkinOptions.Structure does NOT strip colliders
            // (only SkinOptions.Enemy does), so one generated here would ride onto the
            // placed tower and fight PlacementRules / the navmesh blocker.
            if (importer.addCollider)
            {
                importer.addCollider = false;
                report.Append("addCollider TRUE->false (convention: structure props carry none); ");
                changed = true;
            }

            // MATERIAL SETTINGS ARE DELIBERATELY NOT TOUCHED. The first version of this
            // builder set ImportViaMaterialDescription + External to make AddRemap bind,
            // and the run proved that dead end: with no embedded textures to describe,
            // the import surfaces no material sub-assets at all, so no remap key ever
            // exists. Materials are now bound on the PREFAB's renderers instead, which
            // needs nothing from the importer - and leaving it alone means one less
            // reimport that could re-pose the model.
            report.Append("materialImportMode=").Append(importer.materialImportMode)
                  .Append("/location=").Append(importer.materialLocation)
                  .Append(" LEFT AS-IS (binding is on the prefab renderers); ");

            // Scale is DELIBERATELY untouched: useFileScale/globalScale are the
            // project default on every sibling structure FBX, and StructureFactory
            // re-fits the model to YHeightVariable * heightMul at runtime, so the
            // authored size cannot reach the player. Report it, do not override it.
            report.Append("scale kept (useFileScale=").Append(importer.useFileScale)
                  .Append(", globalScale=").Append(importer.globalScale.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append(", animationType=").Append(importer.animationType)
                  .Append(" - matches every sibling structure FBX); ");

            return changed;
        }

        // =====================================================================
        //  Materials + renderer binding
        // =====================================================================

        /// <summary>
        /// Authors this level's URP/Lit material(s) and ASSIGNS them onto the prefab's
        /// own renderers. This is the whole colour fix: no importer involvement, and
        /// Renderer.sharedMaterials is precisely what TripoMaterialFixer re-reads at
        /// placement time.
        /// </summary>
        private static void BindMaterials(LevelSpec spec, Shader shader, StringBuilder report)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
            try
            {
                var renderers = PaintableRenderers(contents, spec, report);

                if (spec.Layout == TexLayout.SinglePbr)
                    BindSinglePbr(spec, shader, renderers, report);
                else
                    BindTripoParts(spec, shader, renderers, report);

                PrefabUtility.SaveAsPrefabAsset(contents, spec.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.ImportAsset(spec.PrefabPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// Every renderer that actually draws geometry. A renderer with no mesh cannot be
        /// painted and is skipped WITH A REPORT (never silently) - it is also not a defect
        /// on its own, so it must not fail the part-index mapping below.
        /// </summary>
        private static List<Renderer> PaintableRenderers(GameObject contents, LevelSpec spec, StringBuilder report)
        {
            var paintable = new List<Renderer>();
            int meshless = 0;

            foreach (var r in contents.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (MeshOf(r) == null) { meshless++; continue; }
                paintable.Add(r);
            }

            if (paintable.Count == 0)
                throw new Exception("L" + spec.Level + ": the prefab has no mesh-bearing Renderer to paint. " +
                                    "Delete '" + spec.PrefabPath + "' and re-run for a clean rebuild.");

            report.Append(paintable.Count).Append(" paintable renderer(s)");
            if (meshless > 0) report.Append(" (+").Append(meshless).Append(" meshless, skipped)");
            report.Append("; ");
            return paintable;
        }

        /// <summary>The mesh a renderer draws, or null when it has none.</summary>
        private static Mesh MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        /// <summary>
        /// Assigns <paramref name="mat"/> to EVERY submesh of <paramref name="r"/>.
        /// <para/>
        /// Two things this gets right that a naive assignment does not:
        ///   * sharedMaterials RETURNS A COPY - mutating the read-back array is a silent
        ///     no-op, so a NEW array is built and assigned back to the property.
        ///   * the array is sized from the MESH's subMeshCount, not from whatever the
        ///     import left behind. A short array leaves trailing submeshes with a null
        ///     material, which renders as the untextured slab this builder exists to stop.
        /// </summary>
        private static int PaintRenderer(Renderer r, Material mat)
        {
            int submeshes = Mathf.Max(1, MeshOf(r).subMeshCount);
            var slots = new Material[submeshes];
            for (int i = 0; i < submeshes; i++) slots[i] = mat;
            r.sharedMaterials = slots;   // assign BACK - the getter returns a copy
            return submeshes;
        }

        /// <summary>
        /// L1 / L3: ONE material for the whole mesh. basecolor -> _BaseMap,
        /// normal -> _BumpMap (with the source texture forced to
        /// TextureImporterType.NormalMap, or it renders as a flat blue albedo).
        /// Metallic / smoothness are SCALARS - see the header for why the shipped
        /// metallic + roughness maps are deliberately not wired.
        /// </summary>
        private static void BindSinglePbr(LevelSpec spec, Shader shader,
                                          List<Renderer> renderers, StringBuilder report)
        {
            string baseColorPath = spec.TexDir + "/" + spec.TexPrefix + "basecolor.JPEG";
            string normalPath    = spec.TexDir + "/" + spec.TexPrefix + "normal.JPEG";

            var baseColor = LoadTextureOrThrow(baseColorPath, spec.Level, "basecolor");
            var normal    = LoadTextureOrThrow(normalPath,    spec.Level, "normal");

            // A normal map imported as a colour texture renders as flat blue paint on
            // the albedo of the lighting - the single most common "why does it look
            // wrong" here. Must be NormalMap, and the reimport must happen BEFORE the
            // texture is bound so the material samples the right encoding.
            EnsureNormalMapImport(normalPath, report);

            string matPath = StructuresDir + "/" + spec.MatStem + ".mat";
            var mat = GetOrCreateMaterial(matPath, shader, out bool freshMat);

            ConfigureWoodMaterial(mat, baseColor, normal, freshMat, report);

            report.Append("material '").Append(Path.GetFileName(matPath)).Append("' ")
                  .Append(freshMat ? "CREATED" : "REUSED (GUID preserved)")
                  .Append(" [_BaseMap=").Append(Path.GetFileName(baseColorPath))
                  .Append(", _BumpMap=").Append(Path.GetFileName(normalPath))
                  .Append("]; ");

            ReportUnreferencedMaps(spec, report);

            int slots = 0;
            foreach (var r in renderers) slots += PaintRenderer(r, mat);
            report.Append("bound to ").Append(renderers.Count).Append(" renderer(s) / ")
                  .Append(slots).Append(" submesh slot(s); ");
        }

        /// <summary>
        /// L2: one basecolor-only material per Tripo part, bound per RENDERER.
        /// <para/>
        /// The part index is recovered from each renderer's own GameObject name
        /// ("tripo_part_3") and matched to "&lt;prefix&gt;&lt;index&gt;_basecolor.JPEG". Tripo's own
        /// export binds part_N's material to part_N's basecolor - verified in the FBX
        /// connection graph - so the index IS the mapping. Submesh/child ORDER is never
        /// used: that is exactly how a roof texture ends up painted on a ladder, and
        /// because it looks deliberate it is worse than an untextured tower.
        /// <para/>
        /// An unparseable name, a duplicate index, or a missing texture each FAIL the run.
        /// </summary>
        private static void BindTripoParts(LevelSpec spec, Shader shader,
                                           List<Renderer> renderers, StringBuilder report)
        {
            var claimed = new Dictionary<int, string>();
            var pairs   = new List<KeyValuePair<Renderer, int>>();

            // -- Resolve every renderer to a part index FIRST. Nothing is painted until
            //    the whole mapping is proven unambiguous.
            foreach (var r in renderers)
            {
                string name = r.gameObject.name ?? string.Empty;
                var m = PartIndexRx.Match(name);
                if (!m.Success)
                    throw new Exception("L" + spec.Level + ": renderer '" + name + "' carries no " +
                                        "'part_<N>' index, so its texture cannot be resolved from the " +
                                        "model's own naming. Refusing to fall back to child or submesh " +
                                        "order - that paints the wrong texture onto a part.");

                int index = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (claimed.TryGetValue(index, out string previous))
                    throw new Exception("L" + spec.Level + ": part index " + index + " is claimed by TWO " +
                                        "renderers ('" + previous + "' and '" + name + "') - the name-based " +
                                        "mapping is ambiguous. Nothing was painted.");
                claimed[index] = name;
                pairs.Add(new KeyValuePair<Renderer, int>(r, index));
            }

            // -- Author + bind.
            int created = 0, reused = 0, slots = 0;
            foreach (var pair in pairs)
            {
                int index = pair.Value;

                string texPath = spec.TexDir + "/" + spec.TexPrefix + index + "_basecolor.JPEG";
                var tex = LoadTextureOrThrow(texPath, spec.Level, "part " + index + " basecolor");

                string matPath = StructuresDir + "/" + spec.MatStem + "_part_" + index + ".mat";
                var mat = GetOrCreateMaterial(matPath, shader, out bool freshMat);
                if (freshMat) created++; else reused++;

                // No normal / metallic / roughness exists for ANY part of this export,
                // so there is nothing to wire and nothing to invert. Scalars only.
                ConfigureWoodMaterial(mat, tex, null, freshMat, null);

                slots += PaintRenderer(pair.Key, mat);
            }

            report.Append(pairs.Count).Append(" part material(s) (")
                  .Append(created).Append(" created, ").Append(reused)
                  .Append(" reused) bound BY NAME index [");
            report.Append(string.Join(", ", claimed.Keys.OrderBy(k => k)
                                                  .Select(k => "part_" + k + "<-" + claimed[k])));
            report.Append("] across ").Append(slots).Append(" submesh slot(s); ")
                  .Append("basecolor only - no normal/metallic/roughness exists for this export; ");

            Debug.Log(Tag + "L" + spec.Level + ": part mapping = " +
                      string.Join(", ", claimed.Keys.OrderBy(k => k)
                                               .Select(k => "part_" + k + " <- renderer '" + claimed[k] + "'")));
        }

        private static Texture2D LoadTextureOrThrow(string path, int level, string what)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                throw new Exception("L" + level + ": " + what + " texture not found at '" + path +
                                    "'. The owner stages these by hand - re-copy the _Tex folder.");
            return tex;
        }

        /// <summary>
        /// Loads (or creates) the .mat at <paramref name="matPath"/>. An existing asset
        /// is REUSED so its GUID - and anything referencing it - survives; only its
        /// shader is healed if it drifted.
        /// </summary>
        private static Material GetOrCreateMaterial(string matPath, Shader shader, out bool fresh)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            fresh = mat == null;
            if (fresh)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
                return mat;
            }
            if (mat.shader != shader)
            {
                Debug.LogWarning(Tag + "'" + matPath + "' was on shader '" +
                                 (mat.shader != null ? mat.shader.name : "<null>") +
                                 "' - healing to '" + shader.name + "'.");
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        /// <summary>
        /// Wires the wood look. STRUCTURAL properties (maps + keywords) are always
        /// re-asserted, because a wrong one is a rendering bug. The _Metallic /
        /// _Smoothness TASTE dials are written only on a freshly created material, so
        /// an owner retune in the inspector survives a re-run and is reported.
        /// </summary>
        private static void ConfigureWoodMaterial(Material mat, Texture2D baseColor, Texture2D normal,
                                                  bool fresh, StringBuilder report)
        {
            // Set both the URP names and the legacy aliases: TripoMaterialFixer reads
            // _MainTex FIRST and only then _BaseMap, so a URP-only binding would be
            // invisible to the runtime rebuild that actually ships.
            if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", baseColor);
            if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", baseColor);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", Color.white);
            mat.mainTexture = baseColor;

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

            // Explicitly OFF: no metallic-gloss map is authored (header), and a stale
            // keyword from a hand-edit would make URP sample a slot that is now null.
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);   // 0 = Opaque

            if (fresh)
            {
                if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", WoodMetallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", WoodSmoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", WoodSmoothness);
            }
            else if (report != null)
            {
                float metal  = mat.HasProperty("_Metallic")   ? mat.GetFloat("_Metallic")   : WoodMetallic;
                float smooth = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : WoodSmoothness;
                report.Append("finish PRESERVED (metallic=")
                      .Append(metal.ToString("0.##", CultureInfo.InvariantCulture))
                      .Append(", smoothness=").Append(smooth.ToString("0.##", CultureInfo.InvariantCulture))
                      .Append(" - not stomped); ");
            }

            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Forces the normal texture's importer to NormalMap. Without it URP samples an
        /// sRGB colour texture as a tangent-space normal and the surface lights wrong
        /// (flat blue sheen). Idempotent - reimports only on an actual change.
        /// </summary>
        private static void EnsureNormalMapImport(string texturePath, StringBuilder report)
        {
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter ti)
                throw new Exception("no TextureImporter at '" + texturePath +
                                    "' - the normal map cannot be typed, and an untyped normal renders wrong.");

            if (ti.textureType == TextureImporterType.NormalMap) return;

            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
            report?.Append("normal '").Append(Path.GetFileName(texturePath))
                   .Append("' importer -> NormalMap; ");
            Debug.Log(Tag + "'" + texturePath + "' textureType -> NormalMap.");
        }

        /// <summary>
        /// States the payload of the PBR maps this builder deliberately does NOT wire
        /// (see the header's metallic/smoothness decision). They sit under Resources/,
        /// so they SHIP - but they are owner-staged art and deleting them is the
        /// owner's call, not this builder's. Reported so the cost is visible.
        /// </summary>
        private static void ReportUnreferencedMaps(LevelSpec spec, StringBuilder report)
        {
            string[] suffixes = { "metallic", "roughness", "rm" };
            long bytes = 0;
            var names = new List<string>();
            foreach (string suffix in suffixes)
            {
                string p = spec.TexDir + "/" + spec.TexPrefix + suffix + ".JPEG";
                string full = Path.Combine(Directory.GetCurrentDirectory(), p);
                if (!File.Exists(full)) continue;
                bytes += new FileInfo(full).Length;
                names.Add(suffix);
            }
            if (names.Count == 0) return;

            report.Append("UNREFERENCED under Resources (ships anyway): ")
                  .Append(string.Join("+", names)).Append(" = ")
                  .Append((bytes / 1024f).ToString("0", CultureInfo.InvariantCulture)).Append(" KB; ");
            Debug.Log(Tag + "L" + spec.Level + ": " + string.Join(", ", names) +
                      " map(s) are NOT wired (TripoMaterialFixer rebuilds every structure material and " +
                      "only carries basecolor/normal/emission, so a metallic-gloss map cannot reach the " +
                      "player) - " + (bytes / 1024f).ToString("0", CultureInfo.InvariantCulture) +
                      " KB of Resources payload for the owner to rule on.");
        }

        // =====================================================================
        //  Prefab + orientation
        // =====================================================================

        /// <summary>
        /// Reads this level's upright correction from the owner's Offset Forge export.
        /// The X term is kept (it stands broken art up); Y and Z are FORCED TO ZERO -
        /// they are facing, and facing belongs to the player's placement yaw, not to
        /// the asset (owner ruling 2026-08-06). Missing entry = hard FAIL: silently
        /// defaulting to identity is how a tower ships on its side.
        /// </summary>
        private static Vector3 LoadUprightCorrection(LevelSpec spec, StringBuilder report)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), OffsetsPath);
            if (!File.Exists(full))
                throw new Exception("L" + spec.Level + ": '" + OffsetsPath + "' not found. That file is " +
                                    "the owner's measured orientation and the ONLY source for it - refusing " +
                                    "to guess a rotation.");

            var table = OffsetTableIO.Load(File.ReadAllText(full));
            string key = Path.GetFileNameWithoutExtension(spec.PrefabPath);
            var entry = table.Find(key);
            if (entry == null)
                throw new Exception("L" + spec.Level + ": no Offset Forge entry with id '" + key +
                                    "' in '" + OffsetsPath + "'. Measure it in the tool before building - " +
                                    "an unmeasured model would be baked at identity and ship lying down.");

            Vector3 raw = entry.rot.ToVector3();
            var corrected = new Vector3(raw.x, 0f, 0f);

            if (Mathf.Abs(raw.y) > 0.01f || Mathf.Abs(raw.z) > 0.01f)
            {
                // Not a silent drop - facing is a real authoring decision and the owner
                // must be able to see that we ignored it deliberately.
                Debug.LogWarning(Tag + "L" + spec.Level + " '" + key + "': Offset Forge records a FACING of " +
                                 "Y=" + raw.y.ToString("0.##", CultureInfo.InvariantCulture) +
                                 " Z=" + raw.z.ToString("0.##", CultureInfo.InvariantCulture) +
                                 " - DROPPED on purpose. Only the X upright correction is baked; which way a " +
                                 "building faces is the player's choice at placement, and baking it would " +
                                 "fight the placement yaw and pre-turn every spawn (owner ruling 2026-08-06).");
                report.Append("facing Y/Z DROPPED (").Append(Fmt(raw)).Append("); ");
            }

            report.Append("upright=").Append(Fmt(corrected)).Append(" from OffsetForge; ");
            return corrected;
        }

        /// <summary>
        /// THE DOUBLE-ROTATION GUARD. Measures the FBX exactly as imported, right now, and
        /// refuses to bake a rotation onto a model that is ALREADY STANDING UP.
        /// <para/>
        /// This is the tower_arcane_spire failure, generalised: that row is the one manual
        /// orientation in the catalog sitting at (0,0,0) rather than (-90,0,0), and its own
        /// note records why - "after the Tripo extraction reimport ArcaneSpire_1 stands
        /// natively; the earlier (-90,90,90) double-rotated it." A Tripo model's import pose
        /// can change across a reimport, so a correction measured beforehand can be exactly
        /// wrong afterwards, and applying it lays a standing tower down. Static node data
        /// cannot predict this (all three of these FBXs carry an identical -90 X node
        /// rotation, yet the measured need differs from what that implies) - only measuring
        /// the imported result settles it. So we measure.
        /// </summary>
        private static void AssertCorrectionIsNeeded(LevelSpec spec, Vector3 upright, StringBuilder report)
        {
            // "Wants a rotation" must mean "wants to be STOOD UP", not "wants any rotation
            // at all". The two are different, and conflating them refuses legitimate work.
            //
            // WHY (2026-08-06): L1 imports STANDING (aspect 1.84) and Offset Forge records
            // 1.9 degrees for it - a cosmetic LEAN correction the owner dialled in while
            // looking at the model. The original predicate treated any non-zero euler as a
            // stand-up request, so it refused 1.9 with "applying it would lay the tower
            // DOWN". A 1.9 degree tilt cannot lay anything down. The guard was right in
            // spirit and wrong in arithmetic.
            //
            // Only a rotation NEAR A QUARTER TURN on X or Z can change which axis is up.
            // Anything under that is a tilt, and a tilt is safe on a model that already
            // stands. Y is excluded entirely - yaw never affects uprightness, and by owner
            // ruling it is the player's at placement anyway.
            const float TopplingDegrees = 45f;   // half a quarter-turn: past this, up changes
            float axisFlip = Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(0f, upright.x)),
                                       Mathf.Abs(Mathf.DeltaAngle(0f, upright.z)));
            bool wantsRotation = axisFlip >= TopplingDegrees;

            // A small tilt still gets BAKED (it is a real correction) - it just does not
            // trip the stand-up guards below. Report it so a stray nudge stays visible
            // rather than being silently applied.
            if (!wantsRotation && upright.sqrMagnitude > 0.0001f)
                report.Append("tilt-only correction ").Append(Fmt(upright))
                      .Append(" (under ").Append(TopplingDegrees.ToString("0", CultureInfo.InvariantCulture))
                      .Append(" deg - cannot change the up axis, baked as authored); ");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (probe == null)
                throw new Exception("L" + spec.Level + ": could not instantiate '" + spec.FbxPath +
                                    "' to measure its as-imported pose.");
            float aspect;
            Vector3 size;
            try
            {
                var rends = probe.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0)
                    throw new Exception("L" + spec.Level + ": '" + spec.FbxPath + "' has no Renderer.");
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                size = b.size;
                aspect = b.size.y / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.z));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }

            bool alreadyStanding = aspect > UprightAspectMin;

            report.Append("as-imported ").Append(Fmt(size)).Append(" aspect ")
                  .Append(aspect.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append(alreadyStanding ? " (STANDING)" : " (lying down)").Append("; ");

            if (alreadyStanding && wantsRotation)
                throw new Exception("L" + spec.Level + ": DOUBLE-ROTATION REFUSED. '" + spec.FbxPath +
                                    "' already imports STANDING UP (bounds " + Fmt(size) + ", aspect " +
                                    aspect.ToString("0.00", CultureInfo.InvariantCulture) +
                                    "), but Offset Forge asks for a rotation of " + Fmt(upright) +
                                    ". Applying it would lay the tower DOWN. This is the tower_arcane_spire " +
                                    "case: a Tripo model's import pose can change across a reimport, so the " +
                                    "measured value is stale. Re-measure it in Offset Forge against the " +
                                    "CURRENT import (it most likely now needs 0,0,0) and re-run.");

            if (!alreadyStanding && !wantsRotation)
                throw new Exception("L" + spec.Level + ": '" + spec.FbxPath + "' imports LYING DOWN (bounds " +
                                    Fmt(size) + ", aspect " + aspect.ToString("0.00", CultureInfo.InvariantCulture) +
                                    ") but Offset Forge records NO correction for it. Baking identity would " +
                                    "ship a tower on its side, and fit-to-height would scale its short axis to " +
                                    "4.8 m and make it giant. Measure it in Offset Forge and re-run.");
        }

        /// <summary>
        /// Ensures the Resources prefab exists and carries the upright correction on its
        /// MODEL CHILD (never the root - VisualFactory.Skin stomps the root's rotation to
        /// identity on every instantiate, so a root-baked rotation is silently lost).
        /// <para/>
        /// The prefab GUID is always preserved: an existing prefab is edited in place via
        /// prefab contents, never recreated. Owner-tuned root SCALE is preserved. The
        /// child ROTATION is re-asserted from the measured data every run, because a wrong
        /// one is not taste - it is a tower on its side - so this is self-healing and
        /// idempotent (a second run reports "already correct" and writes nothing).
        /// </summary>
        private static void EnsurePrefab(LevelSpec spec, Vector3 upright, StringBuilder report)
        {
            string stem = Path.GetFileNameWithoutExtension(spec.PrefabPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);

            // -- Create the wrapper the first time. ------------------------------
            if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath) == null)
            {
                var root = new GameObject(stem);
                try
                {
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    if (child == null)
                        throw new Exception("L" + spec.Level + ": could not instantiate '" + spec.FbxPath + "'.");
                    child.transform.SetParent(root.transform, worldPositionStays: false);
                    child.transform.localPosition = Vector3.zero;
                    child.transform.localScale    = Vector3.one;

                    PrefabUtility.SaveAsPrefabAsset(root, spec.PrefabPath, out bool ok);
                    if (!ok)
                        throw new Exception("L" + spec.Level + ": SaveAsPrefabAsset('" + spec.PrefabPath +
                                            "') failed.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                report.Append("prefab CREATED (wrapper + model child); ");
                Debug.Log(Tag + "prefab created: " + spec.PrefabPath);
            }
            else
            {
                report.Append("prefab REUSED (GUID preserved); ");
            }

            // The RAW import pose of the model child, read fresh from the FBX asset. This is the
            // ORIGIN the idempotency test compares against: an angle test alone cannot tell
            // "correction not yet applied" from "correction applied once", because both sit a
            // fixed angle from the target - which is how L1 ended up with 1.9 baked TWICE.
            Quaternion? importPose = null;
            {
                var srcModel = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
                if (srcModel != null)
                {
                    var srcChild = FindModelChild(srcModel.transform, spec.Level);
                    if (srcChild != null) importPose = srcChild.localRotation;
                }
                if (importPose == null)
                    Debug.LogWarning(Tag + "L" + spec.Level + ": could not read the FBX import pose from '" +
                                     spec.FbxPath + "' - falling back to the angle-delta test, which is " +
                                     "NOT idempotent for a non-zero correction. Verify the result by eye.");
            }

            // -- Assert the pose on the contents (create + reuse take this path). --
            GameObject contents = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
            try
            {
                // The wrapper root MUST stay identity-rotated: Skin overwrites it anyway,
                // so anything here is a lie that silently disappears at runtime.
                if (Quaternion.Angle(contents.transform.localRotation, Quaternion.identity) > 0.01f)
                {
                    Debug.LogWarning(Tag + "L" + spec.Level + ": the prefab ROOT carried a rotation of " +
                                     Fmt(contents.transform.localEulerAngles) + " - clearing it. " +
                                     "VisualFactory.Skin sets the instantiated root to identity, so a " +
                                     "root rotation never survives to runtime; the correction belongs on " +
                                     "the model child.");
                    contents.transform.localRotation = Quaternion.identity;
                }

                Transform child = FindModelChild(contents.transform, spec.Level);

                Vector3 before = child.localEulerAngles;

                // COMPOSE with the import pose - do NOT replace it.
                //
                // WHY (2026-08-06, from a failed run): L1's model child arrives from the
                // FBX already rotated (270, 0, 0) - that baked-in quarter turn is exactly
                // what makes it import STANDING. Assigning the Offset Forge value straight
                // onto localRotation threw that away: (270,0,0) -> (1.9,0,0) laid the tower
                // on its face, measured aspect 0.57, and the post-bake verifier caught it.
                //
                // Offset Forge records a CORRECTION the owner dialled in while looking at
                // the model as it already appears - i.e. an offset applied ON TOP of the
                // import pose, not the final absolute rotation. Composing is what makes
                // both cases work with one rule: L1's 1.9 becomes a small lean on a model
                // that already stands, and L2/L3's -90 stands up a model that does not.
                // IDEMPOTENCY (bug found 2026-08-06). Composing is correct - see
                // the reasoning above - but the old guard was:
                //     bool changed = Quaternion.Angle(child.localRotation, want) > 0.01f;
                // With want = current * upright, that angle IS the correction, so it is ALWAYS
                // greater than 0.01 whenever upright != 0. Every run therefore composed AGAIN.
                // The header's claim that "a second run reports already-correct and writes
                // nothing" only ever held for a ZERO correction.
                //
                // Measured damage: L1 sits at -86.14 = -90 + 1.93 + 1.93 - the 1.9 correction
                // applied TWICE. An angle test cannot tell "not yet applied" from "applied once",
                // because both are a fixed angle away from the target. So compare against the
                // ORIGIN instead: the FBX's own import pose. If the child already differs from
                // the raw import pose by the correction we are about to add, it is already baked.
                var want       = child.localRotation * Quaternion.Euler(upright);
                var alreadyBaked = importPose.HasValue &&
                                   Quaternion.Angle(child.localRotation,
                                                    importPose.Value * Quaternion.Euler(upright)) <= 0.5f;
                bool atImportPose = importPose.HasValue &&
                                    Quaternion.Angle(child.localRotation, importPose.Value) <= 0.5f;

                bool changed;
                // Axis-baked models legitimately carry a ZERO Offset Forge correction. If an
                // older wrapper still has the retired quarter-turn on its child, `want = current`
                // would call that stale pose correct forever. Zero means "use the current import
                // pose", so explicitly rebase it. F8 2026-08-21 caught L3 upside down here.
                if (Mathf.Approximately(upright.sqrMagnitude, 0f) && importPose.HasValue && !atImportPose)
                {
                    changed = true;
                    child.localRotation = importPose.Value;
                    report.Append("zero correction REBASED stale child '").Append(child.name)
                          .Append("' to current import pose (axis-baked model); ");
                }
                else if (alreadyBaked && !Mathf.Approximately(upright.sqrMagnitude, 0f))
                {
                    changed = false;
                    report.Append("upright ALREADY BAKED on child '").Append(child.name)
                          .Append("' (child is import-pose * correction; skipping to stay idempotent); ");
                }
                else if (!atImportPose && importPose.HasValue && !Mathf.Approximately(upright.sqrMagnitude, 0f))
                {
                    // Neither raw nor correctly-baked: someone (an earlier double-run) left it in a
                    // third state. Rebuild from the import pose rather than compounding the drift.
                    changed = true;
                    child.localRotation = importPose.Value * Quaternion.Euler(upright);
                    report.Append("upright REBASED on child '").Append(child.name)
                          .Append("' - was neither raw nor baked (drifted by a previous double-run); ")
                          .Append("restored from import pose ").Append(Fmt(upright)).Append("; ");
                }
                else
                {
                    changed = Quaternion.Angle(child.localRotation, want) > 0.01f;
                    if (changed)
                    {
                        child.localRotation = want;
                        report.Append("upright BAKED on child '").Append(child.name).Append("' ")
                              .Append(Fmt(before)).Append(" -> ").Append(Fmt(upright)).Append("; ");
                    }
                    else
                    {
                        report.Append("upright already correct on child '").Append(child.name)
                              .Append("' (idempotent no-op); ");
                    }
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, spec.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.ImportAsset(spec.PrefabPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// The single renderer-bearing child of the wrapper - the model instance that
        /// carries the correction. Ambiguity is a hard FAIL rather than a guess: picking
        /// the wrong child would rotate part of the tower and leave the rest standing.
        /// </summary>
        private static Transform FindModelChild(Transform root, int level)
        {
            var candidates = new List<Transform>();
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.GetComponentInChildren<Renderer>(true) != null) candidates.Add(c);
            }

            if (candidates.Count == 1) return candidates[0];
            if (candidates.Count == 0)
                throw new Exception("L" + level + ": the prefab has no renderer-bearing child to carry the " +
                                    "upright correction - its structure is not the wrapper+model shape this " +
                                    "builder authors. Delete the prefab and re-run for a clean rebuild.");
            throw new Exception("L" + level + ": the prefab has " + candidates.Count + " renderer-bearing " +
                                "children, so the model child is ambiguous. Refusing to rotate one of them " +
                                "and leave the rest standing. Delete the prefab and re-run.");
        }

        // =====================================================================
        //  Verify + measure
        // =====================================================================

        /// <summary>
        /// Proves the prefab resolves through the path the CATALOG uses, that every
        /// renderer slot carries a real material (an unassigned slot renders magenta;
        /// a null-albedo one renders white), and measures the model the way the runtime
        /// does - encapsulated Renderer.bounds, fit by bounds.size.y - so the reported
        /// footprint is the one PlacementGrid will actually claim.
        /// </summary>
        private static LevelResult VerifyAndMeasure(LevelSpec spec, StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
                throw new Exception("L" + spec.Level + ": prefab '" + spec.PrefabPath +
                                    "' would not load back after authoring.");

            // The catalog loads by Resources path, not by asset path - prove THAT works.
            var viaResources = Resources.Load<GameObject>(spec.ResourcesPath);
            if (viaResources == null)
                throw new Exception("L" + spec.Level + ": Resources.Load(\"" + spec.ResourcesPath +
                                    "\") returned null even though the asset exists. The catalog " +
                                    "resolves visuals through exactly this call, so the tower would " +
                                    "fail to skin and StructureFactory would destroy the root.");

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (probe == null)
                throw new Exception("L" + spec.Level + ": could not instantiate the prefab to measure it.");

            try
            {
                var renderers = probe.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new Exception("L" + spec.Level + ": the prefab has NO Renderer - " +
                                        "StructureFactory.VerifyStructureRenders would reject it as " +
                                        "an invisible structure.");

                int slots = VerifySlots(spec, renderers);

                // Mirror VisualFactory.TryBounds + Fit exactly (world AABB, fit by size.y).
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                // PROVE THE TOWER STANDS UP. The bake happened before this measurement, so
                // a failure here means the correction did not achieve an upright model -
                // e.g. the reimport re-posed the FBX (the tower_arcane_spire class) and the
                // measured -90 is now wrong. Fail loudly rather than ship a tower on its side.
                float aspect = b.size.y / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.z));
                if (aspect <= UprightAspectMin)
                    throw new Exception("L" + spec.Level + ": after baking the upright correction the model " +
                                        "still measures " + Fmt(b.size) + " (height/width aspect " +
                                        aspect.ToString("0.00", CultureInfo.InvariantCulture) + ", needs > " +
                                        UprightAspectMin.ToString("0.0", CultureInfo.InvariantCulture) +
                                        ") - it is NOT standing up. Fit-to-height would then scale the SHORT " +
                                        "axis to 4.8 m and ship a giant tower lying down. Re-measure this " +
                                        "model in Offset Forge: a Tripo import pose can change across a " +
                                        "reimport (see tower_arcane_spire, which needed -90 before its " +
                                        "reimport and 0 after).");

                float target = YHeightVariable * TowerHeightMul;   // 4.8 m - reported, never written
                if (b.size.y < 0.0001f)
                    throw new Exception("L" + spec.Level + ": measured bounds height is ~0 - " +
                                        "VisualFactory.Fit would refuse to scale it and the tower would " +
                                        "appear at its raw import size.");
                float fitScale = target / b.size.y;
                var fittedXZ = new Vector2(b.size.x * fitScale, b.size.z * fitScale);

                report.Append("VERIFIED ").Append(renderers.Length).Append(" renderer(s)/")
                      .Append(slots).Append(" slot(s) textured, upright aspect ")
                      .Append(aspect.ToString("0.00", CultureInfo.InvariantCulture))
                      .Append("; raw=").Append(Fmt(b.size))
                      .Append(" fitx").Append(fitScale.ToString("0.###", CultureInfo.InvariantCulture))
                      .Append(" -> XZ ").Append(fittedXZ.x.ToString("0.###", CultureInfo.InvariantCulture))
                      .Append(" x ").Append(fittedXZ.y.ToString("0.###", CultureInfo.InvariantCulture))
                      .Append(" m ");

                return new LevelResult(spec, b.size, fitScale, fittedXZ, renderers.Length, slots);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        // =====================================================================
        //  Colour-binding proof (the white/magenta tower guard)
        // =====================================================================

        /// <summary>
        /// Proves every renderer slot would still be TEXTURED after the runtime rebuild.
        /// <para/>
        /// This is NOT a generic "has a texture" check. At placement,
        /// SkinOptions.Structure attaches a DeNelle.Core.TripoMaterialFixer, which throws
        /// away the authored material and builds a fresh URP/Lit per slot, carrying across
        /// only what it can re-read from the source. Its albedo lookup is, verbatim:
        ///   if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
        ///   if (tex == null &amp;&amp; src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
        /// So the ONLY thing that matters is whether THAT resolution finds a texture. This
        /// method runs the identical two-step against every slot: if it returns null here,
        /// the fixer will resolve null at runtime, fall through to its neutral stone
        /// MISS-tint, and the owner gets an untextured tower. Anything else is a check that
        /// passes in the editor and lies about the game.
        /// <para/>
        /// Shader brokenness routes through MagentaGuard.IsBrokenShader - the single
        /// authority. It is the only predicate that also tests shader.isSupported, the
        /// on-device case where a shader keeps its name and still renders magenta on the
        /// phone the owner ships APKs to. A local copy here would be the exact drift
        /// ShaderPredicateSingleAuthorityRegression exists to stop.
        /// <para/>
        /// Returns the slot count; THROWS (no success marker) on any bad slot.
        /// </summary>
        private static int VerifySlots(LevelSpec spec, Renderer[] renderers)
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
                        Debug.LogError(Tag + "L" + spec.Level + " renderer '" + r.name + "' slot " + i +
                                       ": material is NULL (MagentaGuard class M2) - the remap did not " +
                                       "bind this slot.");
                        continue;
                    }

                    if (MagentaGuard.IsBrokenShader(m.shader))
                    {
                        bad++;
                        Debug.LogError(Tag + "L" + spec.Level + " renderer '" + r.name + "' slot " + i +
                                       ": material '" + m.name + "' shader='" +
                                       (m.shader != null ? m.shader.name : "<null>") + "' supported=" +
                                       (m.shader != null ? m.shader.isSupported.ToString() : "n/a") +
                                       " - MagentaGuard.IsBrokenShader says this renders MAGENTA under URP.");
                        continue;
                    }

                    // The fixer's own two-step, in its order.
                    Texture tex = null;
                    if (m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
                    if (tex == null && m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");

                    if (tex == null)
                    {
                        bad++;
                        Debug.LogError(Tag + "L" + spec.Level + " renderer '" + r.name + "' slot " + i +
                                       ": material '" + m.name + "' resolves NO base texture through the " +
                                       "exact lookup TripoMaterialFixer uses (_MainTex then _BaseMap). At " +
                                       "runtime the fixer would rebuild this slot with a null albedo and " +
                                       "fall back to its stone MISS-tint - an UNTEXTURED tower.");
                    }
                }
            }

            if (bad > 0)
                throw new Exception("L" + spec.Level + ": " + bad + " of " + slots + " renderer slot(s) would " +
                                    "render magenta or untextured after TripoMaterialFixer's runtime rebuild " +
                                    "(see the errors above). Refusing to emit a success marker for a white tower.");

            Debug.Log(Tag + "L" + spec.Level + ": colour binding VERIFIED - all " + slots + " slot(s) across " +
                      renderers.Length + " renderer(s) resolve a base texture through TripoMaterialFixer's own " +
                      "_MainTex-then-_BaseMap lookup, on a shader MagentaGuard.IsBrokenShader accepts.");
            return slots;
        }

        // =====================================================================
        //  Ladder-level report (the two questions the owner has to rule on)
        // =====================================================================

        /// <summary>
        /// Prints, per level: raw bounds, the fit ratio to 4.8 m, the fitted XZ
        /// footprint against the row's documented 2.778 m / 1x1 anchor, and the cell
        /// claim PlacementGrid would compute. Then states plainly whether the three
        /// levels read as an escalating ladder - because all three are now the same
        /// material family, silhouette and size are the ONLY separation left, and an
        /// upgrade the player cannot see feels broken.
        /// </summary>
        private static void ReportLadder(List<LevelResult> results, StringBuilder report)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Tag + "LADDER MEASUREMENTS (fit target = YHeightVariable " +
                          YHeightVariable.ToString("0.#", CultureInfo.InvariantCulture) +
                          " m x heightMul " + TowerHeightMul.ToString("0.0#", CultureInfo.InvariantCulture) +
                          " = " + (YHeightVariable * TowerHeightMul).ToString("0.0#", CultureInfo.InvariantCulture) +
                          " m tall; heightMul NOT modified by this builder):");

            foreach (var r in results)
            {
                float fp = r.FittedFootprint;
                int cells = Mathf.Max(1, Mathf.CeilToInt(fp / GridCellMetres));
                // PlacementGrid.FootprintCells(m, yaw) inflates by |sin|+|cos| off a
                // cardinal yaw - sqrt(2) at 45 degrees. Report both so a diagonal
                // placement cannot surprise anyone later.
                int cells45 = Mathf.Max(1, Mathf.CeilToInt(fp * Mathf.Sqrt(2f) / GridCellMetres));
                float aspect = r.RawSize.y / Mathf.Max(0.0001f, Mathf.Max(r.RawSize.x, r.RawSize.z));

                sb.AppendLine("  L" + r.Spec.Level + " " + r.Spec.ResourcesPath +
                              ": raw=" + Fmt(r.RawSize) +
                              " aspect(H/W)=" + aspect.ToString("0.00", CultureInfo.InvariantCulture) +
                              " fit=x" + r.FitScale.ToString("0.###", CultureInfo.InvariantCulture) +
                              " -> footprint " + fp.ToString("0.###", CultureInfo.InvariantCulture) +
                              " m (anchor " + DocumentedAnchorMetres.ToString("0.###", CultureInfo.InvariantCulture) +
                              " m, delta " + (fp - DocumentedAnchorMetres).ToString("+0.###;-0.###;0",
                                                                                    CultureInfo.InvariantCulture) +
                              " m) => " + cells + "x" + cells + " cells at the " +
                              GridCellMetres.ToString("0.#", CultureInfo.InvariantCulture) +
                              " m grid (" + cells45 + "x" + cells45 + " at a 45deg yaw)");

                if (fp > GridCellMetres)
                    Debug.LogWarning(Tag + "L" + r.Spec.Level + " fits to " +
                                     fp.ToString("0.###", CultureInfo.InvariantCulture) +
                                     " m across, which EXCEEDS the " +
                                     GridCellMetres.ToString("0.#", CultureInfo.InvariantCulture) +
                                     " m cell - the claim grows past 1x1. An upgrade has NO placement " +
                                     "re-check, so a level that grows the claim can break already-saved towns.");
            }

            // Silhouette separation. The cell claim comes from the BASE model only
            // (MeasureUprightFootprintMetres reads entry.visualPrefabPath), so what
            // matters for "can the player see the upgrade" is how differently the
            // three read AFTER the uniform fit - i.e. their proportions.
            if (results.Count >= 2)
            {
                sb.AppendLine("  SILHOUETTE SEPARATION (all three fit to the same height, so only " +
                              "PROPORTION and shape separate them; StructureTierVisual adds x1.00/x1.12/" +
                              "x1.25 tier scale + a bronze/silver/gold emissive rim on top):");
                for (int i = 1; i < results.Count; i++)
                {
                    var a = results[i - 1];
                    var b = results[i];
                    float fa = a.FittedFootprint, fb = b.FittedFootprint;
                    float pct = Mathf.Abs(fb - fa) / Mathf.Max(0.0001f, fa) * 100f;
                    string verdict = pct < 5f
                        ? "NEAR-IDENTICAL width after fitting - the step reads mostly from SHAPE, not size"
                        : "visibly different width after fitting";
                    sb.AppendLine("    L" + a.Spec.Level + " -> L" + b.Spec.Level + ": " +
                                  fa.ToString("0.###", CultureInfo.InvariantCulture) + " m -> " +
                                  fb.ToString("0.###", CultureInfo.InvariantCulture) + " m (" +
                                  pct.ToString("0.#", CultureInfo.InvariantCulture) + "% change) - " + verdict);
                }
            }

            Debug.Log(sb.ToString());
            report.Append("| ladder measured, see [WoodenWatchtowerBuilder] LADDER MEASUREMENTS; ");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.###", CultureInfo.InvariantCulture) + ", " +
                         v.y.ToString("0.###", CultureInfo.InvariantCulture) + ", " +
                         v.z.ToString("0.###", CultureInfo.InvariantCulture) + ")";
        }
    }
}
