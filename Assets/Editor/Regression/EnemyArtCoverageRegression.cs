// =============================================================================
// EnemyArtCoverageRegression [enemy-art-coverage]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor
// Markers: ENEMY_ART_COVERAGE_OK / ENEMY_ART_COVERAGE_FAIL
// Standalone: run-unity-method DeNelle.Editor.EnemyArtCoverageRegression.RunAll
//
// regression-registry: REGISTERED (WO-1496, 2026-09-06) — the [enemy-art-coverage]
// row is in DataRegression.RunAll, between the fences.
//
// ⚠ THE STANDALONE HOLD IS WITHDRAWN, AND ITS OWN CONDITION IS WHY. The header
// token said: "WHEN THE ORC ART LANDS: delete this token and add the row." Measured
// 2026-09-06 against the tree: Orc_Berserker.mat + EnemyContent/textures/Orc_Berserker/
// and Materials/orcnecromancer_basecolor.mat exist on disk, and Orc_Shaman is no
// longer referenced by enemies.json at all. The named blocker was the orc import and
// the orc import has landed. What remained - enemies.json:400 "modelKey": "OgreMage",
// a key whose mesh was deleted in 0cec81a78 - was a DIFFERENT defect, sanctioned as art-pending in
// a local HashSet this suite could not see, which is exactly why the two suites
// disagreed about that row. WO-1536 (2026-09-07) CLOSED IT AT THE DATA AUTHORITY: the
// row now names Orc_Shaman (the body the ogre has always actually worn, bound via
// Orc_Shaman.fbx externalObjects -> Orc_Shaman.mat -> OrcTex/Orc_Mage_basecolor.jpg),
// and the art-pending HashSet was DELETED rather than emptied, so there is no longer
// any exemption this suite cannot see. THE 2026-08-20 WARNING BELOW STILL
// BINDS: if this suite reds, land the art or change what the row references. Never
// relax the file. A suite edited until it passes is not an oracle.
//
// THE INVARIANT: every model referenced by enemies.json must have a RESOLVABLE
// BASECOLOR — a real albedo image this project could put on that mesh.
//
// WHY THIS SUITE EXISTS — IT IS THE ORACLE WHOSE ABSENCE LET THE BUG SIT.
// On 2026-08-20 two enemy bodies were found rendering as pure-white silhouettes:
// Necromancer_NEW and Skeleton_Golem_NEW. Both are WO-954 replacements — bodies the
// owner specifically asked for after rejecting the KayKit originals — so the two
// wrong ones were the two she had chosen. They had been that way for days.
//
// NOTHING IN THE PROJECT COULD HAVE NOTICED, and that is the point:
//   • the compile gate sees a texture binding as data, not code;
//   • EnemyFactory.VerifyVisualRenders proves a body has a MESH, never a COLOUR;
//   • EnemyTintRegression pins the guard's CLASSIFIER — given a slot, is it painted —
//     but never asks whether art EXISTS for a model in the first place;
//   • the runtime trace only fires once an enemy actually spawns, on a device, in
//     front of the owner. She was the detector. That is the failure mode the whole
//     ticket pipeline exists to end.
// This suite asks the question at ASSET level, where it is cheap and where a missing
// file is a hard, nameable fact rather than a felt impression.
//
// ── WHAT COUNTS AS "RESOLVABLE", AND IN WHICH ORDER ─────────────────────────────
// Deliberately the same precedence the game itself uses, so a pass here means the
// same thing it means on screen:
//   1. BOUND — a material actually on the FBX carries a base map. This is the only
//      tier that is real in edit mode, in a build, and before Start() runs, and the
//      only tier guaranteed to match the mesh's own UVs.
//   2. OWN .fbm — the model's own extracted embedded art
//      (<Model>.fbm/*_Diffuse|*basecolor). Ships with the mesh, so UVs cannot
//      mismatch; needs binding to become tier 1.
//   3. ATLAS — TripoTex/OrcTex/<name>_basecolor, retried with a trailing "_NEW"
//      stripped, because "_NEW" DISAMBIGUATES A MESH FILE, NOT A CHARACTER
//      (EnemyFactory.ResolveBasecolor applies the identical rule at runtime).
//   4. PACK — a loose image beside the FBX under the enemy content root, which is
//      how the KayKit bodies are textured (skeleton_texture_A.png). Without this
//      tier the suite would fail four working skeletons and get weakened to shut up.
// Tiers 2–4 pass, but only tier 1 is proof of a look; tiers 2–4 mean "the art
// exists and something must still bind it", and the reason line says so per model.
//
// ⚠ THIS SUITE IS EXPECTED TO FAIL TODAY, AND MUST NOT BE WEAKENED TO GO GREEN.
// At the time of writing four models referenced by enemies.json had NO art in the
// project under any tier: OgreMage (whose mesh had been deleted too), Orc_Berserker,
// Orc_Necromancer and Orc_Shaman. All four are resolved as of WO-1536 (2026-09-07):
// the orc art landed, and the OgreMage reference is gone from the data. Another lane
// is importing orc replacements. The
// correct resolutions are to land the art or to change what those rows reference —
// never to relax this file. A suite edited until it passes is not an oracle.
//
// ⚠ AND NOTE THE ORC NAMING TRAP, because it is why "the orc art is already in the
// project" is a tempting and WRONG conclusion: EnemyContent/OrcTex does hold three
// per-body orc atlases — but they are Orc_Mage, Orc_Tank and Orc_Warrior, and
// enemies.json references Orc_Berserker, Orc_Shaman and Orc_Necromancer. Different
// bodies, adjacent names. Surveying by the common token ("basecolor") finds the
// folder; only matching it against the DATA shows the trio does not line up.
//
// CASES
//   1 [data-readable]     enemies.json is present and yields at least one modelKey.
//                         A suite that silently examines zero models always passes.
//   2 [every-model-has-art] Each referenced model resolves a basecolor at some tier.
//                         Failures NAME the model and say which tiers were tried.
//   3 [new-suffix-rule]   The "_NEW" strip is still the rule at runtime. If
//                         EnemyFactory.ResolveBasecolor loses it, this suite's tier-3
//                         probe and the game's would silently disagree, and a body
//                         that passes here would render untextured in play.
//   4 [binding-and-sentinel] WO-1509, 2026-09-06; RULE NARROWED WO-1536, 2026-09-07.
//                         Mostly FILE-LEVEL facts, so they are true in a FRESH CLONE and in a
//                         headless run before any import has happened. The one exception is
//                         (a2) below, which reuses this suite's own basecolor resolver:
//                           (a1) every *.fbx directly under the enemy content root THAT
//                               DECLARES ONE OR MORE MATERIAL ENTRIES IN ITS .fbx.meta
//                               externalObjects TABLE has its sibling ".tripo-extracted"
//                               sentinel. WITHOUT ONE,
//                               TripoAssetPostprocessor.OnPreprocessModel force-sets
//                               materialLocation=External + materialName=BasedOnTextureName
//                               on EVERY import, which makes the importer IGNORE the
//                               externalObjects remap table and resolve materials BY
//                               TEXTURE NAME instead (see the "*.tripo-extracted -- UN-IGNORED"
//                               block in .gitignore). That is not a
//                               theory: on 2026-09-06 the device logged
//                               "NO ALBEDO on 'Orc_Berserker(Clone)' ... material=
//                               'tripo_mat_f84a1f82_Pbr (URP)'" — a search-by-name hit —
//                               while Orc_Berserker.fbx.meta's remap table pointed at
//                               Orc_Berserker.mat the whole time. The sentinel IS the state.
//                           (a2) an FBX whose externalObjects table is ABSENT or EMPTY is NOT
//                               asked for a sentinel, because there is nothing for one to
//                               protect: TripoAssetPostprocessor.cs's early-return exists to
//                               preserve an AUTHORED remap, and a sentinel over an empty table
//                               would pin the importer onto the remap path and guarantee a
//                               white body -- the WO-1509 defect, manufactured by its own gate.
//                               Such an FBX is instead required to RESOLVE A BASECOLOR, via
//                               this suite's own ResolveArt (tier 1 = the importer's binding,
//                               then own .fbm / atlas / pack). WO-1536, 2026-09-07: before this
//                               narrowing the case redded seven legacy FBX on the missing
//                               sentinel ALONE while the same run logged "bindings ok", and the
//                               only fix it suggested -- add the sentinels -- would have broken
//                               four bodies that bind correctly by name today.
//                           (a3) an FBX whose .fbx.meta cannot be read is named and FAILS.
//                               Unproven remap state is never a pass.
//                           (b) every enemy-family .mat under that root — a .mat whose stem
//                               is a modelKey, or <modelKey>_Body — carries a NON-ZERO guid
//                               on its _BaseMap. "m_Texture: {fileID: 0}" is the exact byte
//                               pattern behind every NO ALBEDO line this ticket collected.
//                         ⚠ (b) IS WEAKER THAN CASE 2 TIER 1 AND MUST NOT BE MISTAKEN FOR IT.
//                         It proves a guid is present, never that the texture belongs to that
//                         body: Orc_Berserker.mat passes (b) while binding the WARRIOR
//                         basecolor, and Orc_Necromancer/Orc_Shaman pass on a STOPGAP Mage
//                         binding (WO-1509). Only tier 1 under Unity reads the renderer.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Core;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Asset-level oracle: every enemy model in the data owns a resolvable albedo.</summary>
    public static class EnemyArtCoverageRegression
    {
        private const string DataPath    = "Assets/Resources/Data/Canonical/enemies.json";
        private const string FactorySrc  = "Assets/_Modules/Village/Enemies/EnemyFactory.cs";

        /// <summary>WO-1129: the root is DECLARED ONCE in AssetRoots, never re-typed.
        /// This used to be a literal "Assets/EnemyContent" right here — the exact
        /// find-and-replace-across-sixteen-files disease AssetRoots exists to end.</summary>
        private static string ContentRoot => AssetRoots.EnemyContent;

        /// <summary>WO-1129: the atlas folders, their precedence and the "_NEW" alias now come
        /// from <see cref="EnemyArtPaths"/> — THE SAME ARRAY EnemyFactory.TryBasecolor probes.
        /// <para>They used to be an independent copy here, and the fact that the two agreed was
        /// asserted only by a comment in each file. That is how "the oracle passes but the enemy
        /// renders untextured" becomes possible. Now a pass here means the same thing it means
        /// on screen BY CONSTRUCTION, and Case 3 below is the belt to this braces.</para></summary>
        private static string[] AtlasFolders => EnemyArtPaths.AtlasFolders;

        /// <summary>Deliberately WIDER than EnemyArtPaths.ImageExtensions: this suite STATS files
        /// (including tier-4 pack images and authoring .psd), whereas the runtime loads through
        /// Resources and never sees an extension at all. Not an art-path literal.</summary>
        private static readonly string[] ImageExts    = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };

        /// <summary>WO-1509: the opt-out marker TripoAssetPostprocessor honours. Mirrors that
        /// file's own private MarkerSuffix — the two are in different assemblies, so this is a
        /// second copy by necessity; Case 4's failure text names the source file so a rename
        /// there surfaces here as a whole-root red rather than a silent pass.</summary>
        private const string MarkerSuffix = ".tripo-extracted";

        /// <summary>Batchmode entry: writes the OK/FAIL marker, exits 1 on failure.</summary>
        public static void RunAll()
        {
            bool ok;
            string reason;
            try
            {
                ok = Run(out reason);
            }
            catch (Exception ex)
            {
                ok = false;
                reason = "threw " + ex.GetType().Name + ": " + ex.Message;
            }

            Debug.Log((ok ? "ENEMY_ART_COVERAGE_OK " : "ENEMY_ART_COVERAGE_FAIL ") + reason);
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>
        /// DataRegression-shaped contract. True when every case passes; <paramref name="reason"/>
        /// always carries a human-readable summary. Never throws — RunAll folds an unexpected
        /// exception into a failure.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();

            // ── 1 [data-readable] ────────────────────────────────────────────
            List<string> models = ReadModelKeys(out string dataError);
            if (dataError != null)
            {
                reason = "[data-readable] " + dataError;
                return false;
            }
            if (models.Count == 0)
            {
                reason = "[data-readable] '" + DataPath + "' yielded NO modelKey values — this suite would " +
                         "examine zero models and pass vacuously, which is worse than no suite at all";
                return false;
            }
            log.Append("[data-readable] ").Append(models.Count).Append(" model(s) referenced; ");

            // ── 2 [every-model-has-art] ──────────────────────────────────────
            var bare = new List<string>();
            int bound = 0, own = 0, atlas = 0, pack = 0;
            foreach (string model in models)
            {
                string tier = ResolveArt(model, out string where);
                switch (tier)
                {
                    case "BOUND": bound++; break;
                    case "OWN":   own++;   break;
                    case "ATLAS": atlas++; break;
                    case "PACK":  pack++;  break;
                    default:
                        bare.Add(model + " (" + where + ")");
                        break;
                }
            }
            if (bare.Count > 0)
                // Sub-lines are INDENTED on purpose: the suite denominator counts lines that
                // begin with '[' at column 0, so an unindented continuation would inflate it.
                failures.Add("[every-model-has-art] " + bare.Count + " model(s) referenced by enemies.json have NO " +
                             "basecolor anywhere in the project — no bound material map, no own .fbm diffuse, no " +
                             "TripoTex/OrcTex atlas (with or without a '_NEW' strip) and no loose pack image. These " +
                             "bodies can only ever render as a flat family tint: " + string.Join("; ", bare) +
                             ". Land the art or change what those rows reference — do NOT relax this suite");
            else
                log.Append("[every-model-has-art] ok; ");
            log.Append("tiers: bound=").Append(bound).Append(" own-fbm=").Append(own)
               .Append(" atlas=").Append(atlas).Append(" pack=").Append(pack).Append("; ");

            // ── 3 [new-suffix-rule] ──────────────────────────────────────────
            // WO-1129 UPGRADED THIS CASE FROM A SOURCE LINT TO A BEHAVIOURAL ASSERT.
            // It used to grep EnemyFactory.cs for the literal "_NEW", which proved only that a
            // STRING was present in a file. The rule now has ONE home (EnemyArtPaths.NameAliases)
            // and both the runtime probe and this suite call it — so the honest question is no
            // longer "does the source mention it" but "does it actually produce the alias", and
            // that can be asked directly. The delegation lint below is what keeps the runtime
            // from quietly growing a second, divergent copy again.
            const string SuffixedProbe = "Necromancer_NEW";
            const string SuffixedBase  = "Necromancer";
            var aliases = EnemyArtPaths.NameAliases(SuffixedProbe);
            if (aliases == null || aliases.Count < 2 || aliases[0] != SuffixedProbe || aliases[1] != SuffixedBase)
            {
                failures.Add("[new-suffix-rule] EnemyArtPaths.NameAliases(\"" + SuffixedProbe + "\") did not yield \"" +
                             SuffixedBase + "\". The suffix disambiguates a MESH FILE, not a character: " +
                             "Skeleton_Golem_NEW and Necromancer_NEW carry it while their authored atlases ship " +
                             "under the legacy base name. Drop the strip and both bodies — the two replacements " +
                             "the owner specifically asked for — silently fall back to a solid tint again");
            }
            else if (!File.Exists(FactorySrc))
            {
                failures.Add("[new-suffix-rule] '" + FactorySrc + "' does not exist, so this suite cannot prove the " +
                             "runtime resolver still routes through EnemyArtPaths — and a probe that disagrees " +
                             "with the game passes models the game cannot texture");
            }
            else
            {
                string src = File.ReadAllText(FactorySrc);
                if (src.IndexOf("EnemyArtPaths.ResourceCandidates", StringComparison.Ordinal) < 0)
                    failures.Add("[new-suffix-rule] EnemyFactory no longer probes through " +
                                 "EnemyArtPaths.ResourceCandidates. The runtime has grown a SECOND copy of the " +
                                 "atlas-folder order and the '_NEW' alias, so this suite is once again asserting " +
                                 "its own behaviour rather than the game's (WO-1129 §3.1)");
                else
                    log.Append("[new-suffix-rule] ok (behavioural + delegation); ");
            }

            // ── 4 [binding-and-sentinel] ─────────────────────────────────────
            // WO-1509, 2026-09-06. RULE NARROWED by WO-1536, 2026-09-07: the sentinel is
            // demanded only of an FBX that HAS a remap table to protect. See CASES header.
            // The sentinel sweep, the .fbx.meta read and the .mat sweep are pure File/Directory
            // work, so they answer the same in a fresh clone, in batchmode and before the first
            // import. The ONE AssetDatabase touch is the remap-less branch, which delegates to
            // ResolveArt -- whose tier 1 IS the importer's own binding, and which is the only
            // thing that can answer "does this body resolve a basecolor" for an FBX that has no
            // remap table in the first place.
            var noSentinel = new List<string>();   // remap PRESENT, sentinel MISSING
            var noMeta     = new List<string>();   // .fbx.meta missing or unreadable
            var noArt      = new List<string>();   // remap-less AND no basecolor at any tier
            var unbound    = new List<string>();
            if (!Directory.Exists(ContentRoot))
            {
                failures.Add("[binding-and-sentinel] the enemy content root '" + ContentRoot +
                             "' does not exist, so neither the sentinel sweep nor the _BaseMap sweep " +
                             "examined anything — a vacuous pass is worse than no case");
            }
            else
            {
                foreach (string fbx in Directory.GetFiles(ContentRoot, "*.fbx"))
                {
                    string fbxName = Path.GetFileName(fbx);
                    bool metaOk;
                    int remaps = CountMaterialRemaps(fbx + ".meta", out metaOk);
                    if (!metaOk)
                    {
                        // Unreadable meta == UNPROVEN remap state. Never a silent pass:
                        // defaulting to "remap-less" here is how the whole case gets skipped
                        // by deleting one file.
                        noMeta.Add(fbxName);
                        continue;
                    }

                    if (remaps > 0)
                    {
                        // A remap table EXISTS, so the postprocessor's force-set has something
                        // real to destroy. This is the only shape the sentinel protects.
                        if (!File.Exists(fbx + MarkerSuffix))
                            noSentinel.Add(fbxName + " (" + remaps + " material remap(s))");
                        continue;
                    }

                    // Remap-less: there is nothing for a sentinel to protect, so demanding one
                    // would be worse than useless -- adding it flips this FBX onto the remap
                    // path with an EMPTY table, which is the WO-1509 white-body defect itself.
                    // Ask the question that actually matters instead: does a basecolor resolve?
                    string where;
                    if (ResolveArt(Path.GetFileNameWithoutExtension(fbx), out where) == null)
                        noArt.Add(fbxName + " (" + where + ")");
                }

                if (noMeta.Count > 0)
                {
                    noMeta.Sort(StringComparer.Ordinal);
                    failures.Add("[binding-and-sentinel] " + noMeta.Count + " FBX under '" + ContentRoot +
                                 "' have a MISSING or UNREADABLE '.fbx.meta', so this case cannot tell " +
                                 "whether they carry an externalObjects material remap. Unproven is not " +
                                 "a pass: " + string.Join(", ", noMeta));
                }

                if (noSentinel.Count > 0)
                {
                    noSentinel.Sort(StringComparer.Ordinal);
                    failures.Add("[binding-and-sentinel] " + noSentinel.Count + " FBX under '" + ContentRoot +
                                 "' DECLARE an externalObjects material remap but have NO '" + MarkerSuffix +
                                 "' sentinel, so TripoAssetPostprocessor.OnPreprocessModel force-sets " +
                                 "materialLocation=External + materialName=BasedOnTextureName on every " +
                                 "import of each -- which IGNORES that remap table and binds materials BY " +
                                 "TEXTURE NAME instead (see the '*.tripo-extracted -- UN-IGNORED' block in " +
                                 ".gitignore; WO-1509 device capture). Add the sentinel beside the FBX and " +
                                 "TRACK it -- ignoring it makes the fix local-only and the defect returns " +
                                 "silently on a fresh clone: " + string.Join(", ", noSentinel));
                }

                if (noArt.Count > 0)
                {
                    noArt.Sort(StringComparer.Ordinal);
                    failures.Add("[binding-and-sentinel] " + noArt.Count + " FBX under '" + ContentRoot +
                                 "' declare NO externalObjects material remap AND resolve no basecolor at " +
                                 "any tier. The sentinel is IRRELEVANT to these -- do not reach for one, " +
                                 "and do NOT add one: with an empty remap table the sentinel would pin the " +
                                 "importer onto the remap path and guarantee a white body (WO-1509). Land " +
                                 "the art, bind it, or retire the FBX: " + string.Join("; ", noArt));
                }

                if (noMeta.Count == 0 && noSentinel.Count == 0 && noArt.Count == 0)
                {
                    log.Append("[binding-and-sentinel] sentinels ok (remap-bearing FBX carry one; ")
                       .Append("remap-less FBX resolve a basecolor); ");
                }

                // Enemy-family materials only: a .mat whose stem is a modelKey or <modelKey>_Body.
                // Scoped deliberately — the root also holds Tripo scratch materials and shared
                // pack materials, and widening the net is how an oracle gets weakened to shut up.
                var family = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string m in models) { family.Add(m); family.Add(m + "_Body"); }

                foreach (string mat in Directory.GetFiles(ContentRoot, "*.mat", SearchOption.AllDirectories))
                {
                    string stem = Path.GetFileNameWithoutExtension(mat);
                    if (!family.Contains(stem)) continue;
                    if (HasBoundBaseMap(mat)) continue;
                    unbound.Add(stem + " (" + mat.Replace('\\', '/') + ")");
                }
                if (unbound.Count > 0)
                {
                    unbound.Sort(StringComparer.Ordinal);
                    failures.Add("[binding-and-sentinel] " + unbound.Count + " enemy-family material(s) carry " +
                                 "_BaseMap m_Texture {fileID: 0} — a NULL albedo, the exact byte pattern behind " +
                                 "every 'NO ALBEDO on <body>' line the device logs. Bind a real texture guid or " +
                                 "land the art; do NOT drop the material from the family set to go green: " +
                                 string.Join("; ", unbound));
                }
                else
                {
                    log.Append("[binding-and-sentinel] ").Append("bindings ok; ");
                }
            }

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures) + " || context: " +
                         log.ToString().TrimEnd(' ', ';');
                return false;
            }

            reason = "4/4 cases pass — " + log.ToString().TrimEnd(' ', ';');
            return true;
        }

        // ---------------------------------------------------------------------
        // Resolution
        // ---------------------------------------------------------------------

        /// <summary>
        /// Returns the tier at which <paramref name="model"/>'s albedo resolves
        /// ("BOUND" / "OWN" / "ATLAS" / "PACK"), or null when nothing does.
        /// <paramref name="where"/> always describes what was found or tried.
        /// </summary>
        public static string ResolveArt(string model, out string where)
        {
            where = "no model name";
            if (string.IsNullOrEmpty(model)) return null;

            string fbx = EnemyArtPaths.FbxPath(model);
            bool hasMesh = File.Exists(fbx);

            // ── tier 1: a material ON the FBX already carries a base map ──────
            // Read from the imported prefab's RENDERERS, not only from Material
            // sub-assets of the FBX. Once a material is remapped through
            // externalObjects it is a standalone .mat and stops being a sub-asset —
            // so a sub-asset-only probe scores a correctly-bound body as unbound,
            // which is exactly backwards.
            if (hasMesh)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (root != null)
                {
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var mat in r.sharedMaterials)
                        {
                            if (mat == null) continue;
                            Texture t = null;
                            if (mat.HasProperty("_MainTex")) t = mat.GetTexture("_MainTex");
                            if (t == null && mat.HasProperty("_BaseMap")) t = mat.GetTexture("_BaseMap");
                            if (t == null) continue;
                            where = "bound on '" + mat.name + "' -> " + AssetDatabase.GetAssetPath(t);
                            return "BOUND";
                        }
                    }
                }
            }

            // ── tier 2: the model's own extracted embedded art ────────────────
            string fbm = EnemyArtPaths.EmbeddedFolder(model);
            string ownHit = FirstAlbedoIn(fbm);
            if (ownHit != null)
            {
                where = "own embedded art at " + ownHit;
                return "OWN";
            }

            // ── tier 3: the authored atlas, with the "_NEW" retry ─────────────
            foreach (string name in NameCandidates(model))
            {
                foreach (string folder in AtlasFolders)
                {
                    foreach (string ext in ImageExts)
                    {
                        string p = ContentRoot + "/" + folder + "/" + name +
                                   EnemyArtPaths.BaseColorSuffix + ext;
                        if (!File.Exists(p)) continue;
                        where = "atlas at " + p + (name == model ? "" : " (matched after stripping '_NEW')");
                        return "ATLAS";
                    }
                }
            }

            // ── tier 4: a loose pack image beside the FBX ─────────────────────
            // How the KayKit bodies are textured (skeleton_texture_A.png). Matched by
            // the model's own leading token so a pack image is never credited to an
            // unrelated body: "Skeleton_Minion" accepts "skeleton_*", not "orc_*".
            string token = LeadingToken(model);
            if (!string.IsNullOrEmpty(token) && Directory.Exists(ContentRoot))
            {
                foreach (string f in Directory.GetFiles(ContentRoot))
                {
                    if (!IsImage(f)) continue;
                    string stem = Path.GetFileNameWithoutExtension(f);
                    if (stem.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    {
                        where = "pack image at " + f.Replace('\\', '/');
                        return "PACK";
                    }
                }
            }

            where = hasMesh
                ? "mesh present at " + fbx + " but NO albedo at any tier"
                : "NO MESH at " + fbx + " and no albedo at any tier";
            return null;
        }

        /// <summary>The model name, then the same name with a trailing "_NEW" removed.
        /// <para>WO-1129: DELEGATED to <see cref="EnemyArtPaths.NameAliases"/>, which is the ONE
        /// home of the "_NEW" rule. It was previously reimplemented here, and Case 3 of this very
        /// suite exists to catch the runtime copy drifting — a rule that needs a gate to keep two
        /// copies honest is a rule that should have had one copy.</para></summary>
        private static IEnumerable<string> NameCandidates(string model)
        {
            return EnemyArtPaths.NameAliases(model);
        }

        /// <summary>
        /// WO-1536, 2026-09-07: how many MATERIAL entries an FBX's importer meta declares in its
        /// externalObjects remap table. Read as TEXT so the answer is the same in a fresh clone.
        /// <para>This is the discriminator Case 4 turns on. A remap table is state an author
        /// created and the postprocessor can destroy, so it needs the sentinel. NO remap table is
        /// not a lesser version of that -- it is a different shape entirely, with nothing to
        /// protect, and adding a sentinel to it is actively harmful: the sentinel pins the
        /// importer onto the remap path with an EMPTY table, which is the white-body defect
        /// WO-1509 was opened for. So the sentinel is demanded here and ONLY here.</para>
        /// <para>Two serialised shapes exist and both are handled: the inline-empty
        /// "externalObjects: {}" and the list form, whose entries carry a
        /// "type: UnityEngine:Material" line. NOTE THE COLON -- Unity writes
        /// "UnityEngine:Material", not "UnityEngine.Material"; matching the dotted spelling finds
        /// nothing, reads every FBX as remap-less, and silently deletes this case.</para>
        /// <para>The scan is BOUNDED to the externalObjects block -- from its own line to the next
        /// line indented two spaces or less (in practice "  materials:") -- so a Material type
        /// mentioned anywhere else in the meta cannot be miscounted as a remap.</para>
        /// </summary>
        private static int CountMaterialRemaps(string metaPath, out bool readable)
        {
            readable = false;
            string[] lines;
            try { lines = File.ReadAllLines(metaPath); }
            catch { return 0; }
            readable = true;

            const string Head = "externalObjects:";
            const string MatType = "type: UnityEngine:Material";

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith(Head, StringComparison.Ordinal)) continue;

                // "externalObjects: {}" -- declared and EMPTY. Nothing to protect.
                if (trimmed.Length > Head.Length &&
                    trimmed.Substring(Head.Length).Trim() == "{}") return 0;

                int headIndent = Indent(lines[i]);
                int count = 0;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    string body = lines[j].Trim();
                    if (body.Length == 0) continue;
                    // WARNING: A YAML SEQUENCE ITEM SITS AT THE SAME INDENT AS ITS KEY. Unity writes
                    // "  externalObjects:" and then "  - first:" -- both at two spaces. A plain
                    // "indent <= headIndent ends the block" test therefore terminates on the
                    // FIRST entry and reports every remap-bearing FBX as remap-less, which is
                    // this case deleting itself. The block ends at the next line that is at or
                    // left of the key AND is not one of its entries (in practice "  materials:").
                    if (Indent(lines[j]) <= headIndent &&
                        !body.StartsWith("- ", StringComparison.Ordinal)) break;
                    if (body == MatType) count++;
                }
                return count;
            }
            return 0;   // no externalObjects key at all == no remap table
        }

        /// <summary>Leading-space count of a YAML line.</summary>
        private static int Indent(string line)
        {
            int n = 0;
            while (n < line.Length && line[n] == ' ') n++;
            return n;
        }

        /// <summary>
        /// WO-1509: true when a .mat's _BaseMap entry names a NON-ZERO texture guid.
        /// <para>Read as TEXT, not through AssetDatabase, so the answer is the same in a fresh
        /// clone and before any import. Unity serialises the slot as two lines —
        /// "- _BaseMap:" then an indented "m_Texture: {fileID: ...}" — and an unbound slot is
        /// the literal "{fileID: 0}" with no guid at all, which is precisely what
        /// Orc_Necromancer.mat and Orc_Shaman.mat carried when this case was written.</para>
        /// </summary>
        private static bool HasBoundBaseMap(string matPath)
        {
            string[] lines;
            try { lines = File.ReadAllLines(matPath); }
            catch { return false; }   // unreadable == unproven == not bound

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != "- _BaseMap:") continue;
                // The binding is on the next non-blank line; scan a couple in case the
                // serialiser ever interleaves a comment.
                for (int j = i + 1; j < lines.Length && j <= i + 3; j++)
                {
                    string s = lines[j].Trim();
                    if (s.Length == 0) continue;
                    if (!s.StartsWith("m_Texture:", StringComparison.Ordinal)) break;
                    int g = s.IndexOf("guid:", StringComparison.Ordinal);
                    if (g < 0) return false;                      // "{fileID: 0}" — no guid
                    // Take the hex run directly rather than splitting on a delimiter set:
                    // a char literal for the closing brace would unbalance CLAUDE.md §1's
                    // brace count on an otherwise valid file, and this reads no worse.
                    string rest = s.Substring(g + 5).TrimStart();
                    int n = 0;
                    while (n < rest.Length && Uri.IsHexDigit(rest[n])) n++;
                    string guid = rest.Substring(0, n);
                    return guid.Length == 32 && guid.Trim('0').Length > 0;
                }
                return false;
            }
            return false;   // no _BaseMap slot at all
        }

        /// <summary>First diffuse/basecolor image in a folder, or null.</summary>
        private static string FirstAlbedoIn(string dir)
        {
            if (!Directory.Exists(dir)) return null;
            foreach (string f in Directory.GetFiles(dir))
            {
                if (!IsImage(f)) continue;
                // WO-1129: the two-token colour-map test ("*basecolor*" OR "*diffuse*") is the
                // one the 2026-08-20 sweep proved DISCOVERS rather than confirms. It lives in
                // EnemyArtPaths so a future third convention is added in one place.
                string stem = Path.GetFileNameWithoutExtension(f);
                if (EnemyArtPaths.IsColorMapStem(stem))
                    return f.Replace('\\', '/');
            }
            return null;
        }

        private static bool IsImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            foreach (string e in ImageExts) if (e == ext) return true;
            return false;
        }

        /// <summary>"Skeleton_Minion" -> "skeleton". Used to scope the loose-pack probe.</summary>
        private static string LeadingToken(string model)
        {
            int u = model.IndexOf('_');
            return (u > 0 ? model.Substring(0, u) : model).ToLowerInvariant();
        }

        // ---------------------------------------------------------------------
        // Data
        // ---------------------------------------------------------------------

        /// <summary>
        /// Every distinct "modelKey" in enemies.json. Scanned rather than deserialised on
        /// purpose: DeNelle.EditorRegression does not reference Newtonsoft, and adding a
        /// JSON dependency to an oracle in order to read one string field would put more
        /// machinery between the suite and the fact than the fact is worth.
        /// </summary>
        private static List<string> ReadModelKeys(out string error)
        {
            error = null;
            var found = new List<string>();
            if (!File.Exists(DataPath))
            {
                error = "'" + DataPath + "' does not exist — the enemy roster is the input to this suite";
                return found;
            }

            string json = File.ReadAllText(DataPath);
            const string Key = "\"modelKey\"";
            int i = 0;
            while (true)
            {
                i = json.IndexOf(Key, i, StringComparison.Ordinal);
                if (i < 0) break;
                i += Key.Length;
                int colon = json.IndexOf(':', i);
                if (colon < 0) break;
                int q1 = json.IndexOf('"', colon);
                if (q1 < 0) break;
                int q2 = json.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                string val = json.Substring(q1 + 1, q2 - q1 - 1);
                // ONLY plain identifiers. enemies.json's "_schemaNotes" block DESCRIBES the
                // modelKey field in prose, and the scan happily matched that paragraph and
                // credited it as a 15th model — which then "failed" as a body with no art.
                // A scan over a document that talks about its own fields has to tell a value
                // from a description of one, and an identifier shape is that line.
                if (IsIdentifier(val) && !found.Contains(val)) found.Add(val);
                i = q2 + 1;
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        /// <summary>Letters, digits and underscores only — the shape of a model key.</summary>
        private static bool IsIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length > 64) return false;
            foreach (char c in s)
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            return true;
        }
    }
}
