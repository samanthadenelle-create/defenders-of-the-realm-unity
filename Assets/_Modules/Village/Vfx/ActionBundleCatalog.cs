// =============================================================================
// ActionBundleCatalog — runtime row reader for motion-castings.json (WO-671 §3).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
// Canon: docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md (§1 row shape, §4 join)
//
// The RUNTIME half of the Action Keyword Registry: loads the SAME
// motion-castings.json the editor-side MotionCastings interpreter reads
// (Assets/Editor/MotionCastings.cs), but through the WebGL-safe CanonicalJson
// seam (Resources copy WINS, StreamingAssets fallback — the GearCatalog
// pattern), and builds a target -> keyword -> row lookup with the SAME
// inherits fallback semantics: single parent, MAX DEPTH 3, cycle-guarded,
// every fall-through hop self-reports via FlowTrace.Warn — never silent.
//
// WHAT THE RUNTIME READS FROM A ROW (arch §3 — NO runtime clip swap in V1):
//   clip / guid    — carried for logging/provenance ONLY. Runtime NEVER loads or
//                    CrossFades the clip (Phase-2 substrate decision, arch §3);
//                    animation routes through the baked controllers via
//                    ActorAnimator (see ActionBundlePlayer).
//   vfxKey         — VFXManager.PlayKey key (HovlVfxCatalog namespace, pooled).
//   sfxId          — audio key (Resources/Sfx/<sfxId> clip, GameSfx convention —
//                    SfxId enum lives in DeNelle.Audio, unreachable from Village,
//                    so the AudioClip seam via CoreServices.Audio is the call).
//   vfxDelay       — seconds after animation start to fire the VFX (WO-671 §1).
//   attachBone     — humanoid bone / attach name ("hand.r", "spine", "weapon").
//   playOneShot    — effects-only overlay; the base animator state is untouched.
//   manual/source  — provenance for the consume log line.
//
// Absent/empty registry is a LEGAL state: every resolve misses loudly and the
// player simply fires nothing extra — today's behavior, byte-identical.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// One runtime (target, keyword) action-bundle row — the WO-671 extension of
    /// the canon §1 row shape. <see cref="clip"/> is provenance only at runtime
    /// (no clip swap in V1 — arch §3); the effect fields drive the bundle.
    /// </summary>
    [Serializable]
    public sealed class ActionBundleRow
    {
        public string clip;              // asset path — LOGGED, never loaded at runtime (V1)
        public string guid;              // repair reference (editor-side concern; carried)
        public string vfxKey;            // VFXManager.PlayKey / HovlVfxCatalog key
        public string sfxId;             // Resources/Sfx/<sfxId> clip name (GameSfx convention)
        public float  vfxDelay;          // seconds after anim start to fire the VFX (default 0)
        public string attachBone;        // humanoid bone / attach name ("hand.r", "spine")
        public bool   playOneShot;       // effects-only overlay — base animator state untouched
        public bool   manual;            // owner pick = CANON (provenance)
        public string pickedUtc;         // provenance timestamp
        public string source;            // "motion-caster" | "migrated-weaponskill" | "auto"
    }

    /// <summary>
    /// Static runtime loader + resolver over motion-castings.json (Resources copy
    /// wins via <see cref="DeNelle.Core.CanonicalJson"/>). Mirrors the editor
    /// interpreter's inheritance walk exactly: exact row → inherits chain
    /// (≤3 hops, cycle-guarded) → miss. Every miss hop warns via FlowTrace.
    /// </summary>
    public static class ActionBundleCatalog
    {
        /// <summary>StreamingAssets-relative path (CanonicalJson resolves the Resources mirror first).</summary>
        public const string RegistryPath = "Data/Canonical/motion-castings.json";

        private const string System = "Action";
        private const int MaxInheritDepth = 3;   // hops beyond the target itself (canon §1.4)

        private static bool s_loaded;
        private static Dictionary<string, Dictionary<string, ActionBundleRow>> s_targets =
            new Dictionary<string, Dictionary<string, ActionBundleRow>>(StringComparer.Ordinal);
        private static Dictionary<string, string> s_inherits =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Drops the cache — the next resolve re-reads the file.</summary>
        public static void Reload() => s_loaded = false;

        /// <summary>All target ids declared in the registry (families + classes + roots).</summary>
        public static IReadOnlyCollection<string> Targets
        {
            get { EnsureLoaded(); return s_targets.Keys; }
        }

        /// <summary>
        /// EXACT-target row lookup — no inheritance walk (<see cref="TryResolve"/> walks).
        /// </summary>
        public static bool TryGetRow(string target, string keyword, out ActionBundleRow row)
        {
            EnsureLoaded();
            row = null;
            return !string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(keyword)
                && s_targets.TryGetValue(target, out var rows)
                && rows.TryGetValue(keyword, out row);
        }

        /// <summary>
        /// Resolves (target, keyword) through the inherits chain — the same
        /// semantics as the editor MotionCastings.Resolve (canon §1.4): exact row →
        /// walk <c>inherits</c> upward (≤3 hops, cycle-guarded) → miss (false).
        /// <paramref name="resolvedFrom"/> names the target whose row answered
        /// (may be an ancestor). Every fall-through hop FlowTrace.Warns; a final
        /// miss warns with the whole chain — never silent.
        /// </summary>
        public static bool TryResolve(string target, string keyword,
                                      out ActionBundleRow row, out string resolvedFrom)
        {
            EnsureLoaded();
            row = null;
            resolvedFrom = null;
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(keyword))
            {
                FlowTrace.Warn(System,
                    $"TryResolve rejected — target/keyword required (target='{target}', keyword='{keyword}').");
                return false;
            }

            var chain = new List<string>();
            string current = target;
            while (!string.IsNullOrEmpty(current))
            {
                if (chain.Contains(current))
                {
                    FlowTrace.Warn(System, $"inherits CYCLE at '{current}' resolving " +
                        $"'{target}.{keyword}' (chain: {string.Join(" -> ", chain)}) — chain truncated.");
                    break;
                }
                if (chain.Count > MaxInheritDepth)
                {
                    FlowTrace.Warn(System, $"inherits chain for '{target}' exceeds max depth " +
                        $"{MaxInheritDepth} resolving '{target}.{keyword}' " +
                        $"(chain: {string.Join(" -> ", chain)}) — chain truncated.");
                    break;
                }
                chain.Add(current);

                if (s_targets.TryGetValue(current, out var rows) &&
                    rows.TryGetValue(keyword, out row))
                {
                    resolvedFrom = current;
                    return true;
                }

                // Miss on this hop — self-report before walking up (never silent, canon §1.4).
                s_inherits.TryGetValue(current, out string parent);
                if (!string.IsNullOrEmpty(parent))
                    FlowTrace.Warn(System,
                        $"miss '{current}.{keyword}' -> falling through to '{parent}'.");
                current = parent;
            }

            FlowTrace.Warn(System, $"miss '{target}.{keyword}' " +
                $"(chain: {(chain.Count > 0 ? string.Join(" -> ", chain) : target)} -> registry exhausted) " +
                "— no bundle plays.");
            row = null;
            return false;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;

            s_targets = new Dictionary<string, Dictionary<string, ActionBundleRow>>(StringComparer.Ordinal);
            s_inherits = new Dictionary<string, string>(StringComparer.Ordinal);

            string json = DeNelle.Core.CanonicalJson.Read(RegistryPath);
            if (string.IsNullOrEmpty(json))
            {
                // Absent registry is a legal V1 state — CanonicalJson already warned;
                // name the consequence once per load, not per resolve.
                FlowTrace.Warn(System,
                    $"registry '{RegistryPath}' not found — every PlayAction resolve will miss (no bundles).");
                return;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                // Never silent (§12): a bad file names itself and degrades to empty.
                FlowTrace.Fail(System,
                    $"registry parse FAILED for '{RegistryPath}': {ex.Message} — treating as empty.");
                return;
            }

            // Targets — rows keyed by keyword; "_comment"/"inherits" are metadata
            // (the exact editor-side MotionCastings.EnsureLoaded shape).
            if (root["targets"] is JObject targets)
            {
                foreach (var targetProp in targets.Properties())
                {
                    if (!(targetProp.Value is JObject targetObj)) continue;
                    var rows = new Dictionary<string, ActionBundleRow>(StringComparer.Ordinal);
                    foreach (var rowProp in targetObj.Properties())
                    {
                        if (rowProp.Name == "_comment") continue;
                        if (rowProp.Name == "inherits")
                        {
                            s_inherits[targetProp.Name] = (string)rowProp.Value;
                            continue;
                        }
                        if (!(rowProp.Value is JObject rowObj)) continue;
                        try
                        {
                            rows[rowProp.Name] = rowObj.ToObject<ActionBundleRow>();
                        }
                        catch (Exception ex)
                        {
                            // One bad row is skipped, named — never silently dropped.
                            FlowTrace.Warn(System,
                                $"bad row '{targetProp.Name}.{rowProp.Name}' skipped: {ex.Message}");
                        }
                    }
                    s_targets[targetProp.Name] = rows;
                }
            }

            FlowTrace.Step(System,
                $"registry loaded: {s_targets.Count} target(s), {s_inherits.Count} inherits link(s).");
        }
    }
}
