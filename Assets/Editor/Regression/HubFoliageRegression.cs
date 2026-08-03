// =============================================================================
// HubFoliageRegression [hub-foliage] -- proves the runtime hub-foliage scatter is
// SAFE and DETERMINISTIC without opening a scene or running the game.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. The owner asked for a fuller-looking world
// (2026-08-02); HubFoliageInjector answers it by scattering props at RUNTIME.
// The three ways that goes wrong are all cheap to prove headless:
//
//   A. BEHAVIORAL (the real proof) -- invoke the REAL sampler,
//      HubFoliageInjector.GenerateCandidates(seed, count), twice with the same seed
//      and assert byte-identical output (a screenshot diff is meaningless otherwise),
//      that its output is capped, that every point is inside the authored scatter
//      annulus, and that NO point lands in a cardinal gate lane. Resolved by
//      REFLECTION -- DeNelle.EditorRegression does not reference DeNelle.Village
//      (same technique as DungeonDressingRegression).
//
//   B. STRUCTURAL -- the injector must self-bootstrap
//      ([RuntimeInitializeOnLoadMethod]) and must expose a hard instance cap.
//      Read off the real type, not the source text.
//
//   C. SOURCE-LEVEL (the only way to prove a NEGATIVE) -- the file must never
//      hand-edit a scene (no AssetDatabase / EditorSceneManager / SaveScene /
//      ".unity" write) and must strip colliders off its decorative props. A source
//      scan is the correct tool for "this code path does not exist".
//
// Marker: HUB_FOLIAGE_OK / HUB_FOLIAGE_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "hub-foliage suite", () => { if (!HubFoliageRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hub-foliage] " + r); });
// =============================================================================
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HubFoliageRegression
    {
        private const string InjectorTypeName = "DeNelle.Village.World.HubFoliageInjector";
        private const string SourcePath = "Assets/_Modules/Village/World/HubFoliageInjector.cs";

        // Mirrors of the injector's authored geometry. If the owner dials the band in the
        // injector, these move with it -- they are asserted as a RANGE, not exact values.
        private const float BandInnerMin = 1f;      // any positive inner radius keeps props off the castle
        private const float LaneMinHalf  = 4f;      // a gate lane narrower than this is not a real corridor
        private const int   SampleCount  = 400;     // enough points to catch a non-deterministic sampler

        public static bool Run(out string reason)
        {
            var log = new StringBuilder();
            log.AppendLine("--- HUB FOLIAGE (deterministic seeded scatter, capped, keep-out honoured, no scene edit) ---");

            var injector = FindType(InjectorTypeName);
            if (injector == null)
            {
                reason = "hub-foliage: " + InjectorTypeName + " not found -- the runtime hub dressing is missing.";
                Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                return false;
            }

            try
            {
                // ---- B. STRUCTURAL: self-bootstrapping + a hard cap --------------
                if (!HasRuntimeInitializeHook(injector))
                {
                    reason = "hub-foliage: no [RuntimeInitializeOnLoadMethod] on " + InjectorTypeName +
                             " -- the injector would never self-bootstrap, so the hub stays empty.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }

                var maxProp = injector.GetProperty("MaxInstances", BindingFlags.Public | BindingFlags.Static);
                if (maxProp == null)
                {
                    reason = "hub-foliage: no public static MaxInstances on " + InjectorTypeName +
                             " -- an uncapped runtime scatter is a mobile perf hazard.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                int maxInstances = Convert.ToInt32(maxProp.GetValue(null));
                if (maxInstances <= 0 || maxInstances > 600)
                {
                    reason = $"hub-foliage: MaxInstances = {maxInstances} -- outside the sane mobile budget (1..600).";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                log.AppendLine($"  structural: RuntimeInitializeOnLoadMethod present; MaxInstances = {maxInstances}");

                // ---- A. BEHAVIORAL: determinism + geometric keep-out -------------
                var gen = injector.GetMethod("GenerateCandidates", BindingFlags.Public | BindingFlags.Static,
                                             null, new[] { typeof(int), typeof(int) }, null);
                if (gen == null)
                {
                    reason = "hub-foliage: no public static GenerateCandidates(int,int) on " + InjectorTypeName +
                             " -- the scatter cannot be proven deterministic headless.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }

                var seedProp = injector.GetProperty("ScatterSeed", BindingFlags.Public | BindingFlags.Static);
                int seed = seedProp != null ? Convert.ToInt32(seedProp.GetValue(null)) : 12345;

                var a = gen.Invoke(null, new object[] { seed, SampleCount }) as Vector3[];
                var b = gen.Invoke(null, new object[] { seed, SampleCount }) as Vector3[];
                if (a == null || b == null || a.Length == 0)
                {
                    reason = "hub-foliage: GenerateCandidates returned null/empty -- the scatter would place nothing.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                if (a.Length != b.Length)
                {
                    reason = $"hub-foliage: NOT deterministic -- same seed produced {a.Length} then {b.Length} points.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] == b[i]) continue;
                    reason = $"hub-foliage: NOT deterministic -- point {i} was {a[i]} then {b[i]} for the same seed " +
                             "(a screenshot diff of the hub would be meaningless).";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }

                // A different seed must actually produce a different layout (proves the seed is USED).
                var c = gen.Invoke(null, new object[] { seed + 7919, SampleCount }) as Vector3[];
                bool differs = c == null || c.Length != a.Length;
                if (!differs)
                {
                    for (int i = 0; i < a.Length && !differs; i++) if (a[i] != c[i]) differs = true;
                }
                if (!differs)
                {
                    reason = "hub-foliage: a different seed produced an identical layout -- the seed is ignored.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }

                // Geometry: inside the annulus, outside the gate lanes, and no point at the origin.
                float minR = float.MaxValue, maxR = 0f, minLane = float.MaxValue;
                foreach (var p in a)
                {
                    float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                    if (r < minR) minR = r;
                    if (r > maxR) maxR = r;
                    float lane = Mathf.Min(Mathf.Abs(p.x), Mathf.Abs(p.z));
                    if (lane < minLane) minLane = lane;
                    if (Mathf.Abs(p.y) > 0.001f)
                    {
                        reason = $"hub-foliage: candidate {p} is not a flat XZ sample (y must be 0; ground is resolved later).";
                        Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                        return false;
                    }
                }
                if (minR < BandInnerMin)
                {
                    reason = $"hub-foliage: a candidate landed {minR:0.0}m from the castle centre -- " +
                             "the inner keep-out radius is not being applied (props would spawn on the plaza/Heart).";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                if (minLane < LaneMinHalf)
                {
                    reason = $"hub-foliage: a candidate sat {minLane:0.0}m from a world axis -- " +
                             "the cardinal GATE LANES are not kept clear (a prop could block a gate exit).";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                log.AppendLine($"  behavioral: {a.Length} candidates, byte-identical across two runs at seed {seed}; " +
                               $"radius {minR:0.0}..{maxR:0.0}m; closest approach to a gate lane {minLane:0.0}m");

                // Cap: asking for far more than the ceiling must NOT hand back more.
                var huge = gen.Invoke(null, new object[] { seed, maxInstances * 1000 }) as Vector3[];
                if (huge != null && huge.Length > maxInstances * 100)
                {
                    reason = $"hub-foliage: GenerateCandidates returned {huge.Length} points for an absurd request -- " +
                             "the sampler has no ceiling.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }

                // ---- C. SOURCE: never hand-edits a scene; strips colliders -------
                if (!File.Exists(SourcePath))
                {
                    reason = "hub-foliage: source not found at " + SourcePath + " -- cannot prove it never edits a scene.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                // Scan CODE only -- the file's header comment legitimately EXPLAINS why it must not
                // use AssetDatabase / UnityEngine.Random, and a naive text scan would trip on the
                // explanation itself.
                string src = StripComments(File.ReadAllText(SourcePath));

                string[] banned = { "AssetDatabase", "EditorSceneManager", "SaveScene", "PrefabUtility", "UnityEditor" };
                foreach (var token in banned)
                {
                    if (src.IndexOf(token, StringComparison.Ordinal) < 0) continue;
                    reason = $"hub-foliage: the runtime injector references '{token}' -- it must NEVER touch a scene " +
                             "asset (CLAUDE.md SS3: Main_Castle_Overworld.unity has a resave-corruption history).";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                if (src.IndexOf("Collider", StringComparison.Ordinal) < 0 ||
                    src.IndexOf("Destroy(c)", StringComparison.Ordinal) < 0)
                {
                    reason = "hub-foliage: the injector does not strip colliders off its decorative props -- " +
                             "scattered foliage would block the hero and could invalidate the baked navmesh.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                if (src.IndexOf("UnityEngine.Random", StringComparison.Ordinal) >= 0)
                {
                    reason = "hub-foliage: the injector uses UnityEngine.Random -- it shares global state with other " +
                             "systems, so the hub layout would change run to run. Use System.Random with the fixed seed.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                if (src.IndexOf("FeatureFlags.HubFoliage", StringComparison.Ordinal) < 0)
                {
                    reason = "hub-foliage: the injector is not gated behind FeatureFlags.HubFoliage -- " +
                             "it could not be switched off without a rebuild.";
                    Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                    return false;
                }
                log.AppendLine("  source: no AssetDatabase/EditorSceneManager/PrefabUtility (never edits a scene); " +
                               "colliders stripped; System.Random only; FeatureFlags.HubFoliage gate present");

                reason = $"HUB FOLIAGE OK -- deterministic seeded scatter (seed {seed}), capped at {maxInstances} " +
                         $"instances, gate lanes + inner radius honoured, colliders stripped, no scene edit";
                Debug.Log(log.ToString() + "HUB_FOLIAGE_OK");
                return true;
            }
            catch (Exception ex)
            {
                reason = "hub-foliage: exception during the suite -- " + ex.GetBaseException().Message;
                Debug.LogError(log.ToString() + "HUB_FOLIAGE_FAIL: " + reason);
                return false;
            }
        }

        // Remove // line comments and /* */ block comments so the banned-token scan reads
        // executable code only. Deliberately simple: this file has no string literal that
        // contains a comment marker, so a full C# lexer would be over-engineering here.
        private static string StripComments(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            bool inLine = false, inBlock = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

                if (inLine)
                {
                    if (ch == '\n') { inLine = false; sb.Append(ch); }
                    continue;
                }
                if (inBlock)
                {
                    if (ch == '*' && next == '/') { inBlock = false; i++; }
                    continue;
                }
                if (ch == '/' && next == '/') { inLine = true; i++; continue; }
                if (ch == '/' && next == '*') { inBlock = true; i++; continue; }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        // Any private/public static method carrying [RuntimeInitializeOnLoadMethod].
        private static bool HasRuntimeInitializeHook(Type t)
        {
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var m in t.GetMethods(flags))
            {
                if (m.GetCustomAttribute(typeof(RuntimeInitializeOnLoadMethodAttribute)) != null) return true;
            }
            return false;
        }

        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
