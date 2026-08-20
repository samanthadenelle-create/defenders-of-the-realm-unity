// =============================================================================
// EnemyBodyTextureRegression — the standing guard for the "enemies not having
// coloring" defect (owner report, proven by EnemyProvingHarness.RunBatch).
//
// WHAT WENT WRONG, so the guard is aimed at the real thing:
//   TripoAssetPostprocessor.OnPreprocessModel force-sets materialLocation=External
//   (legacy) + materialName=BasedOnTextureName + materialSearch=RecursiveUp on EVERY
//   FBX under the enemy content root that lacks a ".tripo-extracted" marker. Legacy
//   External mode resolves a model's material by SEARCHING the project for a .mat named
//   after the texture — and all four AccuRig skeleton bodies name their diffuse
//   "Material_Pbr_Diffuse". So every one of them collapsed onto the single project
//   material carrying that name, and 7 enemy ids rendered with ONE body's texture: the
//   Mage's UV layout smeared over four different meshes.
//
//   The .meta externalObjects remaps were INERT under that mode, which is why repointing
//   them only MOVED the collision (observed live: all seven flipped to the Warrior's
//   texture and the Mage broke). The guard therefore asserts the OUTCOME — what each
//   imported model actually binds — not the importer settings that produced it.
//
// WHAT THIS ORACLE ASSERTS — from the REAL imported models, not from the .meta text:
//   (A) NO TWO DISTINCT ENEMY BODIES SHARE A BASE MAP. That is the exact defect.
//   (B) Every base map a body binds lives in THAT BODY'S OWN `<Body>.fbm/` folder —
//       so a body can never be wired to a neighbour's art in the first place.
//   (C) BOTH DIRECTIONS: the detector is fed the known-bad pre-fix wiring as a
//       synthetic fixture and MUST reject it. A guard that cannot fail is not a guard.
//
// SCOPE — deliberately the bodies that ship embedded media (they have a `<Body>.fbm`
//   sibling folder). Bodies with NO base map bound (Troll / Orc / *_NEW Tripo bodies)
//   are coloured at runtime by TripoMaterialFixer and are covered by EnemyBodyColorGuard;
//   they are OUT of scope here and are not a failure. Skeleton_Minion (KayKit) carries a
//   loose project texture rather than embedded media and has no `.fbm`, so it is out of
//   scope too — replacing that body is an owner-owned creative decision.
//
// Assembly: DeNelle.EditorRegression.  No scene, no PlayMode, deterministic.
//
//
// WHY STANDALONE, AND WHEN THAT CHANGES: this oracle landed in an asset-import lane
// that does NOT own Assets/Editor/Regression/DataRegression.cs (another lane holds it
// this session), so it cannot add its own registration line without duelling over that
// file. It is green on today's tree and is proven by its own batch entry. WIRE IT IN
// with the one-liner below the moment DataRegression.cs is free — a standing guard the
// gate does not call is a guard on paper:
//   if (!EnemyBodyTextureRegression.Run(out var enemyBodyTexReason)) failures.Add(enemyBodyTexReason); else log.AppendLine("[enemy-body-texture] " + enemyBodyTexReason);
//
// Entry points:
//   DeNelle.Editor.EnemyBodyTextureRegression.RunAll  ->  ENEMY_BODY_TEXTURE_OK
//                                                     |   ENEMY_BODY_TEXTURE_FAIL (exit 1)
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class EnemyBodyTextureRegression
    {
        private const string EnemyContent = "Assets/EnemyContent";

        /// <summary>
        /// Proves every embedded-media enemy body renders with its OWN diffuse and that
        /// no two bodies share one. Returns true only when both hold AND the detector
        /// demonstrably rejects the known-bad wiring.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- ENEMY BODY TEXTURE (own-diffuse-per-body, read off the imported models) ---");

            // ── gather the real wiring: body -> the base maps its renderers bind ──────
            var wiring = ReadLiveWiring(log);

            if (wiring.Count == 0)
                failures.Add("no embedded-media enemy body was found under " + EnemyContent +
                             " — the oracle inspected nothing, which is not a pass.");

            // ── (A)+(B) run the detector over the live tree ───────────────────────────
            foreach (var v in Detect(wiring))
                failures.Add(v);

            // ── (C) the other direction: the KNOWN-BAD state must FAIL ────────────────
            // This is the exact pre-fix wiring: four AccuRig skeleton bodies all bound to
            // the Mage's diffuse through the single shared Materials/Material_Pbr.mat.
            const string mageDiffuse = EnemyContent + "/Skeleton_Mage.fbm/Material_Pbr_Diffuse.png";
            var knownBad = new Dictionary<string, HashSet<string>>
            {
                { "Skeleton_Warrior", new HashSet<string> { mageDiffuse } },
                { "Skeleton_Rogue",   new HashSet<string> { mageDiffuse } },
                { "Skeleton_Healer",  new HashSet<string> { mageDiffuse } },
                { "Skeleton_Mage",    new HashSet<string> { mageDiffuse } },
            };
            var caught = Detect(knownBad);
            log.AppendLine($"known-bad fixture -> {caught.Count} violation(s) detected");
            if (caught.Count == 0)
                failures.Add("the detector did NOT reject the known-bad pre-fix wiring (four bodies all bound to " +
                             "the Mage's diffuse). A guard that cannot fail proves nothing — the check is hollow.");
            bool sharingCaught = caught.Any(c => c.Contains("share the base map"));
            bool foreignCaught = caught.Any(c => c.Contains("outside its own"));
            if (!sharingCaught)
                failures.Add("the known-bad fixture was not caught by the SHARED-BASE-MAP rule (A) — " +
                             "rule (A) is not actually testing what the defect was.");
            if (!foreignCaught)
                failures.Add("the known-bad fixture was not caught by the OWN-.fbm rule (B) — " +
                             "rule (B) is not actually testing what the defect was.");

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"FAIL ({failures.Count}): ");
                foreach (var f in failures) sb.Append("\n  - ").Append(f);
                sb.Append("\n  ").Append(log.ToString().Replace("\n", "\n  "));
                reason = sb.ToString();
                return false;
            }

            reason = $"OK: {wiring.Count} embedded-media enemy body(ies) each render their own diffuse; " +
                     "no base map is shared across bodies; the known-bad wiring is rejected by both rules.";
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// The rules, as a pure function so the SAME code judges the live tree and the
        /// known-bad fixture. Returns one string per violation; empty means clean.
        /// </summary>
        private static List<string> Detect(Dictionary<string, HashSet<string>> wiring)
        {
            var violations = new List<string>();

            // (B) every bound base map must live in THIS body's own .fbm folder.
            foreach (var kv in wiring)
            {
                string body = kv.Key;
                string ownFolder = $"/{body}.fbm/";
                foreach (var texPath in kv.Value.OrderBy(p => p))
                {
                    if (Norm(texPath).Contains(ownFolder)) continue;
                    violations.Add($"body '{body}' binds base map '{texPath}', which is outside its own " +
                                   $"'{body}.fbm/' folder — the body is wired to art that is not its own.");
                }
            }

            // (A) no base map may be bound by two DISTINCT bodies.
            var owners = new Dictionary<string, List<string>>();
            foreach (var kv in wiring)
                foreach (var texPath in kv.Value)
                {
                    string key = Norm(texPath);
                    if (!owners.TryGetValue(key, out var list)) owners[key] = list = new List<string>();
                    if (!list.Contains(kv.Key)) list.Add(kv.Key);
                }
            foreach (var kv in owners.OrderBy(k => k.Key))
            {
                if (kv.Value.Count < 2) continue;
                violations.Add($"{kv.Value.Count} distinct bodies share the base map '{kv.Key}' " +
                               $"[{string.Join(", ", kv.Value.OrderBy(b => b))}] — every one but the texture's " +
                               "true owner renders another body's UV layout.");
            }

            return violations;
        }

        /// <summary>
        /// Reads what each embedded-media body ACTUALLY renders with, off the imported
        /// model asset — not off the .meta text, which is what a seat would re-derive.
        /// </summary>
        private static Dictionary<string, HashSet<string>> ReadLiveWiring(StringBuilder log)
        {
            var wiring = new Dictionary<string, HashSet<string>>();
            if (!Directory.Exists(EnemyContent)) return wiring;

            foreach (var fbx in Directory.GetFiles(EnemyContent, "*.fbx", SearchOption.TopDirectoryOnly).OrderBy(p => p))
            {
                string body = Path.GetFileNameWithoutExtension(fbx);

                // In scope only when the body ships embedded media (a `<Body>.fbm` sibling).
                if (!Directory.Exists($"{EnemyContent}/{body}.fbm")) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyContent}/{body}.fbx");
                if (go == null) { log.AppendLine($"{body}: model asset would not load — skipped."); continue; }

                var maps = new HashSet<string>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
                        if (t == null) continue; // runtime-tinted body — out of scope (see header)
                        string p = AssetDatabase.GetAssetPath(t);
                        if (!string.IsNullOrEmpty(p)) maps.Add(p);
                    }
                }

                if (maps.Count == 0) { log.AppendLine($"{body}: binds no base map (runtime-tinted) — out of scope."); continue; }
                wiring[body] = maps;
                log.AppendLine($"{body}: binds {maps.Count} base map(s) -> {string.Join(", ", maps.OrderBy(p => p))}");
            }

            return wiring;
        }

        private static string Norm(string p) => (p ?? string.Empty).Replace('\\', '/');

        // ─────────────────────────────────────────────────────────────────────────────
        /// <summary>Batch entry. Emits the marker; exits 1 on failure.</summary>
        public static void RunAll()
        {
            bool ok = Run(out var reason);
            if (!ok)
            {
                Debug.LogError("ENEMY_BODY_TEXTURE_FAIL: " + reason);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("ENEMY_BODY_TEXTURE_OK - " + reason);
        }
    }
}
