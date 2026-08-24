// =============================================================================
// TowerPerkFallbackGenerator - WO-1170 site #1 (owner ruling 2026-08-24:
// "We need to not have anything pulled other than from json").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor
//
// WHAT THIS REPLACES.
// TowerPerkTable.BuiltInFallback() was a HAND-WRITTEN four-row table of tier
// name / damageMult / damageAdd / rangeAdd / fireRateMult / signatureAbility,
// carrying a doc comment that called itself "identical to the shipped JSON".
// That sentence was an ASSERTION WITH NOTHING ENFORCING IT. The moment anyone
// tuned tower-perks.json the two disagreed, and because the table is the
// JSON-load-FAILURE path, a parse failure would then silently revert EVERY
// tower in the game to the old balance - during the incident that caused the
// parse failure, when nobody can tell which is which.
//
// The irony is on the record in WO-1170: that comment was written to fix
// upgrades SILENTLY DOING NOTHING. The cure for one silent failure was a
// second, quieter one.
//
// WHAT IT GENERATES, AND WHY THIS SHAPE.
// It emits Assets/_Modules/Village/Buildings/Generated/TowerPerkFallbackData.g.cs
// - the canonical tower-perks.json as an ASCII-escaped string constant, plus
// its SHA-256, byte length, tier count and schema version. TowerPerkTable.Reload
// then parses that constant through the SAME ParseRows method the file path
// uses, so both paths are one method and cannot diverge.
//
// This is the WO-1137 pattern, copied deliberately and not re-invented (WO-1170
// s5: only two sanctioned outcomes, and a hand-maintained table with a
// "keep the two in sync" comment is NOT one of them). Like WO-1137 it emits a
// STRING, not field-by-field object initializers: initializers would have to
// track every Row schema change forever, and a field added to Row tomorrow
// would silently stop being mirrored. A string constant is schema-agnostic - it
// is the file, byte for byte.
//
// HOW IT IS JUDGED (CLAUDE.md s8): the MARKER on a FRESH log, never the exit code.
//   TOWER_PERK_FALLBACK_GEN_OK   / TOWER_PERK_FALLBACK_GEN_FAIL
//
// FRESHNESS is gated by TowerPerkRegression's "[tower-fallback-parity]" block,
// which fails RED the moment tower-perks.json is edited without regenerating.
//
// Batchmode:
//   powershell -NoProfile -File .\run-unity-method.ps1 `
//       -Method DeNelle.Editor.TowerPerkFallbackGenerator.Generate `
//       -LogName tower-perk-fallback-gen.log `
//       -ExpectMarker TOWER_PERK_FALLBACK_GEN_OK
// =============================================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class TowerPerkFallbackGenerator
    {
        public const string MarkerOk   = "TOWER_PERK_FALLBACK_GEN_OK";
        public const string MarkerFail = "TOWER_PERK_FALLBACK_GEN_FAIL";

        /// <summary>The copy CanonicalJson.Read resolves FIRST, so it is the copy we generate from.</summary>
        public const string ResourcesCopy = "Assets/Resources/Data/Canonical/tower-perks.json";

        /// <summary>The authoring copy. Must stay BYTE-IDENTICAL to the Resources copy.</summary>
        public const string StreamingAssetsCopy = "Assets/StreamingAssets/Data/Canonical/tower-perks.json";

        /// <summary>Generated output. Lives under Assets/_Modules/Village so DeNelle.Village can see it.</summary>
        public const string OutputPath = "Assets/_Modules/Village/Buildings/Generated/TowerPerkFallbackData.g.cs";

        /// <summary>Regeneration command quoted verbatim in every freshness-gate failure message.</summary>
        public const string RegenCommand =
            "powershell -NoProfile -File .\\run-unity-method.ps1 " +
            "-Method DeNelle.Editor.TowerPerkFallbackGenerator.Generate " +
            "-LogName tower-perk-fallback-gen.log -ExpectMarker TOWER_PERK_FALLBACK_GEN_OK";

        /// <summary>Source chars per emitted string literal. Keeps every literal well under any
        /// metadata/expression size cliff and keeps the generated file diff-readable.</summary>
        private const int ChunkChars = 2048;

        [MenuItem("Defenders/Catalog/Regenerate Tower Perk Fallback (WO-1170)")]
        public static void GenerateMenu() => Generate();

        public static void Generate()
        {
            try
            {
                string root = Directory.GetCurrentDirectory();
                string resAbs    = Path.Combine(root, ResourcesCopy.Replace('/', Path.DirectorySeparatorChar));
                string streamAbs = Path.Combine(root, StreamingAssetsCopy.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(resAbs))
                {
                    Fail($"the Resources copy is MISSING at {ResourcesCopy} - nothing to embed.");
                    return;
                }
                if (!File.Exists(streamAbs))
                {
                    Fail($"the StreamingAssets copy is MISSING at {StreamingAssetsCopy}. Both canonical " +
                         "copies must exist and be byte-identical.");
                    return;
                }

                byte[] resBytes    = File.ReadAllBytes(resAbs);
                byte[] streamBytes = File.ReadAllBytes(streamAbs);

                string resHash    = Sha256(resBytes);
                string streamHash = Sha256(streamBytes);

                // The dual-copy invariant, checked HERE rather than trusted: generating from a
                // Resources copy that has drifted from the authoring copy would bake the drift in.
                if (resHash != streamHash)
                {
                    Fail($"the two canonical copies are NOT byte-identical - " +
                         $"{ResourcesCopy} sha256={resHash} ({resBytes.Length} bytes) vs " +
                         $"{StreamingAssetsCopy} sha256={streamHash} ({streamBytes.Length} bytes). " +
                         "Reconcile them before generating; embedding a drifted copy would make the " +
                         "failure path fight the game on balance numbers the authoring copy does not have.");
                    return;
                }

                // Decode as UTF-8 WITHOUT a BOM so the round-trip (embed -> Encoding.UTF8.GetBytes)
                // is byte-exact against the file we hashed.
                string json = new UTF8Encoding(false).GetString(resBytes);
                byte[] roundTrip = new UTF8Encoding(false).GetBytes(json);
                if (roundTrip.Length != resBytes.Length || Sha256(roundTrip) != resHash)
                {
                    Fail("tower-perks.json does not round-trip through UTF-8 byte-exactly (a BOM or an " +
                         "invalid byte sequence?). Refusing to embed a copy that is not the file.");
                    return;
                }

                int tierCount;
                int version;
                try
                {
                    var parsed = JObject.Parse(json);
                    var tiers = parsed["tiers"] as JArray;
                    if (tiers == null)
                    {
                        Fail($"{ResourcesCopy} has no 'tiers' array - refusing to embed a perk table with " +
                             "no rows, because the fallback would then make every tower upgrade a NO-OP, " +
                             "which is the exact defect WO-432 closed.");
                        return;
                    }
                    tierCount = tiers.Count;
                    version   = parsed["version"] != null ? (int)parsed["version"] : 0;
                }
                catch (Exception pex)
                {
                    Fail($"{ResourcesCopy} did not parse: {pex.GetType().Name}: {pex.Message}. Refusing to " +
                         "embed an unparseable perk table - the fallback would be dead on arrival.");
                    return;
                }

                if (tierCount < 3)
                {
                    Fail($"{ResourcesCopy} parsed to {tierCount} tier row(s) - expected at least 3 " +
                         "(the placed Level 1/2/3 upgrades). Refusing to embed a perk table that cannot " +
                         "upgrade a tower.");
                    return;
                }

                string source = BuildSource(json, resHash, tierCount, version, resBytes.Length);

                string outAbs = Path.Combine(root, OutputPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(outAbs));

                // LF-normalised UTF-8 without BOM. Deterministic bytes for the same input, so a
                // no-op regeneration produces a no-op diff.
                File.WriteAllBytes(outAbs, new UTF8Encoding(false).GetBytes(source));

                AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

                Debug.Log($"{MarkerOk} wrote {OutputPath} from {ResourcesCopy} " +
                          $"(tiers={tierCount} version={version} bytes={resBytes.Length} sha256={resHash}); " +
                          $"both canonical copies verified byte-identical.");
            }
            catch (Exception ex)
            {
                Fail($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void Fail(string why)
        {
            Debug.LogError($"{MarkerFail} {why}");
        }

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ---------------------------------------------------------------------
        //  Emission
        // ---------------------------------------------------------------------
        private static string BuildSource(string json, string sha, int tierCount, int version, int byteLen)
        {
            var sb = new StringBuilder(json.Length * 3);

            sb.Append("// <auto-generated>\n");
            sb.Append("// =============================================================================\n");
            sb.Append("//  !!  DO NOT EDIT THIS FILE  !!   IT IS GENERATED.  ANY HAND EDIT IS LOST.\n");
            sb.Append("// -----------------------------------------------------------------------------\n");
            sb.Append("//  Generator : DeNelle.Editor.TowerPerkFallbackGenerator.Generate\n");
            sb.Append("//              (menu: Defenders > Catalog > Regenerate Tower Perk Fallback (WO-1170))\n");
            sb.Append("//  Source    : " + ResourcesCopy + "\n");
            sb.Append("//  Regenerate:\n");
            sb.Append("//    " + RegenCommand + "\n");
            sb.Append("//\n");
            sb.Append("//  WO-1170 site #1 (owner ruling 2026-08-24). TowerPerkTable's JSON-load-FAILURE\n");
            sb.Append("//  path used to be a hand-written four-row mirror of tower-perks.json whose own\n");
            sb.Append("//  comment claimed it was \"identical to the shipped JSON\" - an assertion with\n");
            sb.Append("//  nothing enforcing it, on COMBAT BALANCE. It is now THIS: the perk table\n");
            sb.Append("//  itself, embedded byte-for-byte and parsed through the same code path as the\n");
            sb.Append("//  file. Drift is not gated, it is impossible.\n");
            sb.Append("//\n");
            sb.Append("//  If a value here looks wrong, EDIT " + ResourcesCopy + "\n");
            sb.Append("//  (and its StreamingAssets twin) and re-run the generator. Editing this file\n");
            sb.Append("//  instead makes the two disagree, and TowerPerkRegression's\n");
            sb.Append("//  [tower-fallback-parity] freshness gate will go RED on the SHA mismatch.\n");
            sb.Append("// =============================================================================\n");
            sb.Append("\n");
            sb.Append("namespace DeNelle.Village\n");
            sb.Append("{\n");
            sb.Append("    /// <summary>\n");
            sb.Append("    /// GENERATED. The canonical tower perk table, compiled into the assembly so tower\n");
            sb.Append("    /// upgrades keep granting their designed stats even if the file cannot be READ at\n");
            sb.Append("    /// runtime. Consumed by <see cref=\"TowerPerkTable\"/>; freshness-gated by\n");
            sb.Append("    /// TowerPerkRegression's [tower-fallback-parity] check.\n");
            sb.Append("    /// </summary>\n");
            sb.Append("    public static class TowerPerkFallbackData\n");
            sb.Append("    {\n");
            sb.Append("        /// <summary>Repo-relative path of the file this was generated from.</summary>\n");
            sb.Append("        public const string SourcePath = \"" + ResourcesCopy + "\";\n");
            sb.Append("\n");
            sb.Append("        /// <summary>SHA-256 of that file's exact bytes at generation time. The freshness gate's evidence.</summary>\n");
            sb.Append("        public const string SourceSha256 = \"" + sha + "\";\n");
            sb.Append("\n");
            sb.Append("        /// <summary>Byte length of the source file at generation time.</summary>\n");
            sb.Append("        public const int SourceByteLength = " + byteLen + ";\n");
            sb.Append("\n");
            sb.Append("        /// <summary>Tier rows in the embedded table. Reported on the fallback boot line.</summary>\n");
            sb.Append("        public const int SourceTierCount = " + tierCount + ";\n");
            sb.Append("\n");
            sb.Append("        /// <summary>The file's own schema version field.</summary>\n");
            sb.Append("        public const int SourceVersion = " + version + ";\n");
            sb.Append("\n");
            sb.Append("        /// <summary>The exact command that regenerates this file. Quoted verbatim by the freshness gate.</summary>\n");
            sb.Append("        public const string RegenerateCommand =\n");
            sb.Append("            \"" + Escape(RegenCommand) + "\";\n");
            sb.Append("\n");
            sb.Append("        private static string _json;\n");
            sb.Append("\n");
            sb.Append("        /// <summary>\n");
            sb.Append("        /// The perk table JSON, byte-identical to <see cref=\"SourcePath\"/> when UTF-8 encoded.\n");
            sb.Append("        /// Split into literals purely so the generated file stays diff-readable.\n");
            sb.Append("        /// </summary>\n");
            sb.Append("        public static string Json\n");
            sb.Append("        {\n");
            sb.Append("            get { return _json ?? (_json = string.Concat(Parts)); }\n");
            sb.Append("        }\n");
            sb.Append("\n");
            sb.Append("        private static readonly string[] Parts = new string[]\n");
            sb.Append("        {\n");

            int i = 0;
            while (i < json.Length)
            {
                int len = System.Math.Min(ChunkChars, json.Length - i);
                // Never split a surrogate pair across two literals.
                if (i + len < json.Length && char.IsHighSurrogate(json[i + len - 1])) len--;
                sb.Append("            \"").Append(Escape(json.Substring(i, len))).Append("\",\n");
                i += len;
            }

            sb.Append("        };\n");
            sb.Append("    }\n");
            sb.Append("}\n");

            return sb.ToString();
        }

        /// <summary>
        /// Escapes to a PURE-ASCII C# string literal body. Non-ASCII becomes \uXXXX deliberately:
        /// the repo has been bitten by cp1252/UTF-8 confusion on .cs files (CLAUDE.md s0), and an
        /// ASCII-only generated file cannot be mis-decoded by any tool in the chain.
        /// </summary>
        private static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length + 32);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20 || c > 0x7E) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
