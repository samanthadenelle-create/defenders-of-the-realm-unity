// =============================================================================
// EnemyArtCoverageRegression [enemy-art-coverage]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor
// Markers: ENEMY_ART_COVERAGE_OK / ENEMY_ART_COVERAGE_FAIL
// Standalone: run-unity-method DeNelle.Editor.EnemyArtCoverageRegression.RunAll
//
// regression-registry: standalone
//
// ⚠ THAT TOKEN IS A DELIBERATE, TEMPORARY HOLD — AND THE COMMITTER'S DECISION TO
// UNDO. The contract below is DataRegression-shaped and this suite is MEANT to be
// registered; DataRegression.cs is lane-fenced, so it is not self-registered here.
// It is marked standalone rather than left unregistered for one reason: as of
// 2026-08-20 this suite FAILS BY DESIGN on four models whose art does not exist
// (see the warning below), and registering it today would red the batch on a defect
// that belongs to the in-flight orc import, not to the registry. Registering it
// while it fails would also make the very first instinct "relax the suite".
// WHEN THE ORC ART LANDS: delete this token and add the [enemy-art-coverage] row to
// DataRegression.RunAll. Leaving it standalone forever is the failure mode this
// header exists to prevent — an unregistered oracle is a file that never runs.
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
// At the time of writing four models referenced by enemies.json have NO art in the
// project under any tier: OgreMage (which has no mesh either), Orc_Berserker,
// Orc_Necromancer and Orc_Shaman. Another lane is importing orc replacements. The
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
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Asset-level oracle: every enemy model in the data owns a resolvable albedo.</summary>
    public static class EnemyArtCoverageRegression
    {
        private const string DataPath    = "Assets/Resources/Data/Canonical/enemies.json";
        private const string ContentRoot = "Assets/EnemyContent";
        private const string FactorySrc  = "Assets/_Modules/Village/Enemies/EnemyFactory.cs";

        private static readonly string[] AtlasFolders = { "TripoTex", "OrcTex" };
        private static readonly string[] ImageExts    = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };

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
            if (!File.Exists(FactorySrc))
            {
                failures.Add("[new-suffix-rule] '" + FactorySrc + "' does not exist, so this suite cannot prove the " +
                             "runtime resolver still strips a trailing '_NEW' — and a probe that disagrees with the " +
                             "game passes models the game cannot texture");
            }
            else
            {
                string src = File.ReadAllText(FactorySrc);
                if (src.IndexOf("\"_NEW\"", StringComparison.Ordinal) < 0)
                    failures.Add("[new-suffix-rule] EnemyFactory.ResolveBasecolor no longer mentions the '_NEW' " +
                                 "suffix. The suffix disambiguates a MESH FILE, not a character: Skeleton_Golem_NEW " +
                                 "and Necromancer_NEW carry it while their authored atlases ship under the legacy " +
                                 "base name. Drop the strip and both bodies silently fall back to a solid tint again");
                else
                    log.Append("[new-suffix-rule] ok; ");
            }

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures) + " || context: " +
                         log.ToString().TrimEnd(' ', ';');
                return false;
            }

            reason = "3/3 cases pass — " + log.ToString().TrimEnd(' ', ';');
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

            string fbx = ContentRoot + "/" + model + ".fbx";
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
            string fbm = ContentRoot + "/" + model + ".fbm";
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
                        string p = ContentRoot + "/" + folder + "/" + name + "_basecolor" + ext;
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

        /// <summary>The model name, then the same name with a trailing "_NEW" removed.</summary>
        private static IEnumerable<string> NameCandidates(string model)
        {
            yield return model;
            if (model.EndsWith("_NEW", StringComparison.Ordinal))
                yield return model.Substring(0, model.Length - 4);
        }

        /// <summary>First diffuse/basecolor image in a folder, or null.</summary>
        private static string FirstAlbedoIn(string dir)
        {
            if (!Directory.Exists(dir)) return null;
            foreach (string f in Directory.GetFiles(dir))
            {
                if (!IsImage(f)) continue;
                string stem = Path.GetFileNameWithoutExtension(f);
                if (stem.IndexOf("Diffuse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    stem.IndexOf("basecolor", StringComparison.OrdinalIgnoreCase) >= 0)
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
