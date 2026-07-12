// =============================================================================
// MotionCastings — the ONE interpreter over motion-castings.json (WO-670 slice 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Canon: docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md
//
// The Action Keyword Registry is the One Model applied to motion: TARGETS (enemy
// family | hero class) are entries, KEYWORDS (ActionKeywords / the json
// `vocabulary` block) are the capability slots, and every consumer — the three
// controller builders in V1 — is a READER that asks Resolve(target, keyword,
// builderDefault) and never hardcodes per-type.
//
// Resolution per (target, keyword) — §1.4 of the canon doc:
//   1. Exact target row.
//   2. Walk `inherits` upward (single parent, MAX DEPTH 3, cycle-guarded; a
//      cycle / over-depth is a LogWarning + chain truncation).
//   3. Registry miss -> the calling builder's hardcoded pick (the terminal
//      default — what makes an EMPTY registry byte-identical to today).
//   4. Every fall-through self-reports (Debug.LogWarning); a hit logs the
//      WO-670 acceptance line: [MotionCaster] '<t>.<kw>' -> '<clip>' (manual).
//      A miss is never a silent T-pose.
//
// Clip reference: `clip` asset path is PRIMARY (owner-readable, hand-diffable);
// `guid` is the repair reference — if the path 404s the loader tries
// AssetDatabase.GUIDToAssetPath(guid), warns, and self-heals the in-memory path
// (the file itself is only rewritten by WriteRow — no silent writes on read).
//
// WriteRow is the guarded save surface the Motion Caster tool (later lane) will
// call: `manual:true` rows are CANON and are NEVER overwritten by default
// (Offset Forge law, WO-490 / canon §8) — an owner re-pick must pass
// allowManualOverwrite:true explicitly (the tool's confirm dialog).
//
// NOTE (canon §6 test #2): the empty-registry controller-hash parity check
// (bake each builder with the file absent vs {} and hash the serialized
// .controller) requires an actual bake and belongs in the DataRegression
// harness (Assets/Editor/Regression) — a follow-up regression, not an EditMode
// test here.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>One (target, keyword) registry row — canon doc §1 field table
    /// + the WO-671 action-bundle fields (§9a adopted: vfxDelay/attachBone/
    /// playOneShot). All bundle fields optional — absent = today's behavior.</summary>
    [Serializable]
    public class CastingRow
    {
        public string clip;       // PRIMARY: asset path (FBX or extracted .anim)
        public string guid;       // secondary/repair reference (survives file moves)
        public string vfxKey;     // optional — VFXManager.PlayKey namespace
        public string sfxId;      // optional — SfxId namespace
        public float  vfxDelay;   // optional — seconds after anim start to fire the VFX (default 0)
        public string attachBone; // optional — humanoid bone/attach name ("hand.r", "weapon", "spine")
        public bool   playOneShot;// optional — one-shot overlay, base state undisturbed (default false)
        public bool   manual;     // true = owner pick = CANON, never overwritten
        public string pickedUtc;  // provenance timestamp
        public string source;     // "motion-caster" | "migrated-weaponskill" | "auto"
    }

    /// <summary>
    /// The ONE interpreter over Assets/StreamingAssets/Data/Canonical/
    /// motion-castings.json. Builders call <see cref="Resolve"/> around their
    /// hardcoded clip picks; the Motion Caster tool (WO-670, later lane) calls
    /// <see cref="WriteRow"/>. Empty/absent registry ⇒ every Resolve returns
    /// its builderDefault ⇒ byte-identical bake outputs.
    /// </summary>
    public static class MotionCastings
    {
        public const string DefaultRegistryPath =
            "Assets/StreamingAssets/Data/Canonical/motion-castings.json";

        /// <summary>The canonical Resources mirror — the DataRegression core-datahub
        /// oracle enforces the dual-copy rule on every canonical StreamingAssets file
        /// (canon §1, amended 2026-07-11), so WriteRow keeps BOTH byte-identical.</summary>
        public const string ResourcesRegistryPath =
            "Assets/Resources/Data/Canonical/motion-castings.json";

        private const string LogHit  = "[MotionCaster] ";   // hit line (WO-670 acceptance)
        private const string LogMiss = "[MotionCasting] ";  // miss/fall-through self-report
        private const int MaxInheritDepth = 3;              // hops beyond the target itself

        private static string s_registryPath = DefaultRegistryPath;
        private static bool s_loaded;
        private static List<string> s_vocabulary = new List<string>();
        private static Dictionary<string, List<string>> s_vocabularyByCategory =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private static Dictionary<string, Dictionary<string, CastingRow>> s_targets =
            new Dictionary<string, Dictionary<string, CastingRow>>(StringComparer.Ordinal);
        private static Dictionary<string, string> s_inherits =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Registry file path — DefaultRegistryPath in production; tests point it
        /// at a fixture (and MUST restore it in teardown). Setting it invalidates
        /// the cache.
        /// </summary>
        public static string RegistryPath
        {
            get => s_registryPath;
            set
            {
                string next = string.IsNullOrEmpty(value) ? DefaultRegistryPath : value;
                if (next == s_registryPath) return;
                s_registryPath = next;
                s_loaded = false;
            }
        }

        /// <summary>Drops the cache — next access re-reads the file.</summary>
        public static void Reload() => s_loaded = false;

        /// <summary>
        /// The closed keyword vocabulary, flattened across categories, as loaded
        /// from the registry file (falls back to ActionKeywords.All when the file
        /// is absent — one source, two views).
        /// </summary>
        public static IReadOnlyList<string> Vocabulary
        {
            get
            {
                EnsureLoaded();
                return s_vocabulary.Count > 0
                    ? (IReadOnlyList<string>)s_vocabulary
                    : DeNelle.Core.Combat.ActionKeywords.All;
            }
        }

        /// <summary>The vocabulary category names as declared in the json (locomotion/
        /// attack/cast/reaction/death/signature) — the Motion Caster's chip source.</summary>
        public static IReadOnlyCollection<string> Categories
        {
            get { EnsureLoaded(); return s_vocabularyByCategory.Keys; }
        }

        /// <summary>Vocabulary keywords of one json category (locomotion/attack/cast/
        /// reaction/death/signature); empty list for an unknown category.</summary>
        public static IReadOnlyList<string> CategoryKeywords(string category)
        {
            EnsureLoaded();
            return s_vocabularyByCategory.TryGetValue(category ?? string.Empty, out var list)
                ? (IReadOnlyList<string>)list
                : Array.Empty<string>();
        }

        /// <summary>All target ids declared in the registry (families + classes + roots).</summary>
        public static IReadOnlyCollection<string> Targets
        {
            get { EnsureLoaded(); return s_targets.Keys; }
        }

        /// <summary>
        /// EXACT-target row lookup — no inheritance walk (Resolve walks). Returns
        /// false when the target or keyword has no row of its own.
        /// </summary>
        public static bool TryGetRow(string target, string keyword, out CastingRow row)
        {
            EnsureLoaded();
            row = null;
            return !string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(keyword)
                && s_targets.TryGetValue(target, out var rows)
                && rows.TryGetValue(keyword, out row);
        }

        /// <summary>
        /// Resolves (target, keyword) through the inheritance chain (§1.4):
        /// exact row → inherits walk (≤3 hops, cycle-guarded) → builderDefault.
        /// A hit logs "[MotionCaster] '&lt;t&gt;.&lt;kw&gt;' -&gt; '&lt;clip&gt;' (manual)";
        /// every miss self-reports via LogWarning — never a silent T-pose.
        /// </summary>
        public static AnimationClip Resolve(string target, string keyword,
                                            AnimationClip builderDefault)
        {
            EnsureLoaded();

            var chain = new List<string>();
            string current = target;
            while (!string.IsNullOrEmpty(current))
            {
                if (chain.Contains(current))
                {
                    Debug.LogWarning(LogMiss + $"inherits CYCLE at '{current}' resolving " +
                        $"'{target}.{keyword}' (chain: {string.Join(" -> ", chain)}) — chain truncated.");
                    break;
                }
                if (chain.Count > MaxInheritDepth)
                {
                    Debug.LogWarning(LogMiss + $"inherits chain for '{target}' exceeds max depth " +
                        $"{MaxInheritDepth} resolving '{target}.{keyword}' " +
                        $"(chain: {string.Join(" -> ", chain)}) — chain truncated.");
                    break;
                }
                chain.Add(current);

                if (s_targets.TryGetValue(current, out var rows) &&
                    rows.TryGetValue(keyword, out var row))
                {
                    var clip = LoadRowClip(row, target, keyword);
                    if (clip != null)
                    {
                        Debug.Log(LogHit + $"'{target}.{keyword}' -> '{row.clip}' " +
                            $"({(row.manual ? "manual" : string.IsNullOrEmpty(row.source) ? "auto" : row.source)})");
                        return clip;
                    }
                    // Row present but its clip failed to load (LoadRowClip warned) —
                    // keep walking the chain rather than dead-ending.
                }

                s_inherits.TryGetValue(current, out string parent);
                current = parent;
            }

            Debug.LogWarning(LogMiss + $"miss '{target}.{keyword}' " +
                $"(chain: {(chain.Count > 0 ? string.Join(" -> ", chain) : target)} -> registry exhausted) " +
                $"-> builder default '{(builderDefault != null ? builderDefault.name : "<null>")}'.");
            return builderDefault;
        }

        /// <summary>
        /// Guarded save surface for the Motion Caster tool (canon §8). Refuses
        /// (returns false, file untouched) when: the keyword is outside the closed
        /// vocabulary, the row is null/clipless, or the existing row is
        /// <c>manual:true</c> and <paramref name="allowManualOverwrite"/> is false
        /// (manual = CANON — Offset Forge law; the tool passes true only on an
        /// explicit owner confirm). A successful production save writes BOTH
        /// canonical copies byte-identically (StreamingAssets + Resources mirror,
        /// dual-copy rule §1).
        /// </summary>
        public static bool WriteRow(string target, string keyword, CastingRow row,
                                    bool allowManualOverwrite = false)
        {
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(keyword) ||
                row == null || string.IsNullOrEmpty(row.clip))
            {
                Debug.LogError(LogMiss + "WriteRow rejected — target/keyword/row.clip required " +
                    $"(target='{target}', keyword='{keyword}').");
                return false;
            }

            // Bundle-field sanity (WO-671 §1): a negative delay is meaningless —
            // reject loudly rather than clamp silently (§5 never-silent).
            if (row.vfxDelay < 0f)
            {
                Debug.LogError(LogMiss + $"WriteRow rejected — vfxDelay {row.vfxDelay} is negative " +
                    $"('{target}.{keyword}'); seconds after animation start, must be >= 0.");
                return false;
            }

            // Closed vocabulary — an unknown keyword is a save ERROR (§2).
            bool known = false;
            foreach (string kw in Vocabulary)
                if (string.Equals(kw, keyword, StringComparison.Ordinal)) { known = true; break; }
            if (!known)
            {
                Debug.LogError(LogMiss + $"WriteRow rejected — keyword '{keyword}' is not in the " +
                    "closed vocabulary (new keyword = version bump + vocabulary row + reader, §2).");
                return false;
            }

            // Manual-row preservation gate — checked BEFORE any file write so a
            // refused save leaves the file byte-identical.
            if (TryGetRow(target, keyword, out var existing) && existing.manual && !allowManualOverwrite)
            {
                Debug.LogWarning(LogMiss + $"WriteRow refused — '{target}.{keyword}' is CANON " +
                    "(manual:true, owner pick). Pass allowManualOverwrite:true only on an " +
                    "explicit owner confirm.");
                return false;
            }

            try
            {
                JObject root = File.Exists(s_registryPath)
                    ? JObject.Parse(File.ReadAllText(s_registryPath))
                    : new JObject { ["version"] = 1, ["targets"] = new JObject() };

                if (!(root["targets"] is JObject targets))
                {
                    targets = new JObject();
                    root["targets"] = targets;
                }
                if (!(targets[target] is JObject targetObj))
                {
                    targetObj = new JObject();
                    targets[target] = targetObj;
                }

                targetObj[keyword] = new JObject
                {
                    ["clip"]        = row.clip,
                    ["guid"]        = row.guid ?? string.Empty,
                    ["vfxKey"]      = row.vfxKey ?? string.Empty,
                    ["sfxId"]       = row.sfxId ?? string.Empty,
                    ["vfxDelay"]    = row.vfxDelay,
                    ["attachBone"]  = row.attachBone ?? string.Empty,
                    ["playOneShot"] = row.playOneShot,
                    ["manual"]      = row.manual,
                    ["pickedUtc"]   = row.pickedUtc ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["source"]      = string.IsNullOrEmpty(row.source) ? "motion-caster" : row.source,
                };

                string json = root.ToString(Formatting.Indented) + "\n";
                File.WriteAllText(s_registryPath, json);

                // Dual-copy rule (canon §1, amended 2026-07-11): the canonical
                // registry ships a byte-identical Resources mirror. Only mirrors
                // the PRODUCTION path — test fixtures (RegistryPath redirected)
                // must never clobber the real Resources copy.
                if (s_registryPath == DefaultRegistryPath)
                {
                    string mirrorDir = Path.GetDirectoryName(ResourcesRegistryPath);
                    if (!string.IsNullOrEmpty(mirrorDir) && !Directory.Exists(mirrorDir))
                        Directory.CreateDirectory(mirrorDir);
                    File.WriteAllText(ResourcesRegistryPath, json);
                }

                Reload();
                Debug.Log(LogHit + $"'{target}.{keyword}' -> '{row.clip}' " +
                    $"({(row.manual ? "manual" : string.IsNullOrEmpty(row.source) ? "auto" : row.source)}) saved.");
                return true;
            }
            catch (Exception ex)
            {
                // Never silent (§5): a failed save names itself.
                Debug.LogError(LogMiss + $"WriteRow FAILED for '{target}.{keyword}': {ex.Message}");
                return false;
            }
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;

            s_vocabulary = new List<string>();
            s_vocabularyByCategory = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            s_targets = new Dictionary<string, Dictionary<string, CastingRow>>(StringComparer.Ordinal);
            s_inherits = new Dictionary<string, string>(StringComparer.Ordinal);

            if (!File.Exists(s_registryPath))
            {
                // Absent registry is a legal V1 state (empty ⇒ builder defaults) —
                // report once per load, not per resolve.
                Debug.LogWarning(LogMiss + $"registry not found at '{s_registryPath}' — " +
                    "every Resolve returns its builder default.");
                return;
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(s_registryPath));
            }
            catch (Exception ex)
            {
                Debug.LogError(LogMiss + $"registry parse FAILED at '{s_registryPath}': {ex.Message} " +
                    "— treating as empty (builder defaults).");
                return;
            }

            // Vocabulary — categorized block, flattened for membership checks.
            if (root["vocabulary"] is JObject vocab)
            {
                foreach (var cat in vocab.Properties())
                {
                    var list = new List<string>();
                    if (cat.Value is JArray arr)
                        foreach (var kw in arr)
                            if (kw.Type == JTokenType.String)
                            {
                                list.Add((string)kw);
                                s_vocabulary.Add((string)kw);
                            }
                    s_vocabularyByCategory[cat.Name] = list;
                }
            }

            // Targets — rows keyed by keyword; "_comment"/"inherits" are metadata.
            if (root["targets"] is JObject targets)
            {
                foreach (var targetProp in targets.Properties())
                {
                    if (!(targetProp.Value is JObject targetObj)) continue;
                    var rows = new Dictionary<string, CastingRow>(StringComparer.Ordinal);
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
                            rows[rowProp.Name] = rowObj.ToObject<CastingRow>();
                        }
                        catch (Exception ex)
                        {
                            // Never silent — one bad row is skipped, named.
                            Debug.LogWarning(LogMiss + $"bad row '{targetProp.Name}.{rowProp.Name}' " +
                                $"skipped: {ex.Message}");
                        }
                    }
                    s_targets[targetProp.Name] = rows;
                }
            }

            ValidateInheritsChains();
        }

        /// <summary>Load-time chain validation (§1.4): a cycle or over-depth chain
        /// is a LogWarning; Resolve truncates the walk at the same limits.</summary>
        private static void ValidateInheritsChains()
        {
            foreach (string start in s_targets.Keys)
            {
                var seen = new List<string> { start };
                string current = start;
                while (s_inherits.TryGetValue(current, out string parent) && !string.IsNullOrEmpty(parent))
                {
                    if (seen.Contains(parent))
                    {
                        Debug.LogWarning(LogMiss + $"inherits CYCLE: {string.Join(" -> ", seen)} -> {parent} " +
                            "— resolves truncate at the repeat.");
                        break;
                    }
                    seen.Add(parent);
                    if (seen.Count - 1 > MaxInheritDepth)
                    {
                        Debug.LogWarning(LogMiss + $"inherits chain from '{start}' exceeds max depth " +
                            $"{MaxInheritDepth}: {string.Join(" -> ", seen)} — resolves truncate there.");
                        break;
                    }
                    current = parent;
                }
            }
        }

        /// <summary>
        /// Loads the row's clip — path PRIMARY; on a 404 tries the guid repair
        /// (GUIDToAssetPath), warns, and self-heals the in-memory path (the file is
        /// only rewritten by WriteRow). FBX paths pick the first non-__preview
        /// AnimationClip sub-asset (the builders' convention).
        /// </summary>
        private static AnimationClip LoadRowClip(CastingRow row, string target, string keyword)
        {
            var clip = LoadClipAtPath(row.clip);
            if (clip != null) return clip;

            if (!string.IsNullOrEmpty(row.guid))
            {
                string repaired = AssetDatabase.GUIDToAssetPath(row.guid);
                if (!string.IsNullOrEmpty(repaired))
                {
                    clip = LoadClipAtPath(repaired);
                    if (clip != null)
                    {
                        Debug.LogWarning(LogMiss + $"'{target}.{keyword}' path 404 at '{row.clip}' — " +
                            $"guid repaired to '{repaired}' (in-memory; re-save in Motion Caster to persist).");
                        row.clip = repaired; // self-heal the cached row
                        return clip;
                    }
                }
            }

            Debug.LogWarning(LogMiss + $"'{target}.{keyword}' clip NOT LOADABLE " +
                $"(path='{row.clip}', guid='{row.guid}') — falling through the chain.");
            return null;
        }

        private static AnimationClip LoadClipAtPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (direct != null && IsRealMotionClip(direct))
                return direct;
            // FBX container: pick the BEST clip sub-asset, not the first. ActorCore FBXs
            // ship a '0_T-Pose' take (0.04s) BEFORE the motion take — F8 2026-07-11
            // 'walking forward' proof: 'clips=[0_T-Pose(w=1.00,len=0.04)]' — the hero
            // walked on the T-pose frame. Skip preview/T-pose takes; prefer the longest
            // remaining clip (the motion take).
            AnimationClip best = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is AnimationClip c && IsRealMotionClip(c))
                    if (best == null || c.length > best.length) best = c;
            return best;
        }

        /// <summary>Rejects preview/T-pose/bind-pose takes and sub-0.1s placeholder frames.</summary>
        private static bool IsRealMotionClip(AnimationClip c)
        {
            if (c == null) return false;
            if (c.name.StartsWith("__preview", StringComparison.Ordinal)) return false;
            string n = c.name.ToLowerInvariant();
            if (n.Contains("t-pose") || n.Contains("tpose") || n.Contains("bind")) return false;
            return c.length >= 0.1f;
        }
    }
}
