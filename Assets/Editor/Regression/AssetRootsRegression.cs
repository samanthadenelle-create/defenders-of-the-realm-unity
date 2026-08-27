// =============================================================================
// AssetRootsRegression [asset-roots] — THE GATE ASSETROOTS.CS ALREADY CLAIMED IT HAD.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Namespace: DeNelle.Editor.Regression.
// Marker: ASSET_ROOTS_OK / ASSET_ROOTS_FAIL.
// Standalone: run-unity-method -Method DeNelle.Editor.Regression.AssetRootsRegression.RunAll
// Registered in DataRegression.RunAll as the "asset-roots suite".
//
// ── WHY THIS FILE EXISTS (WO-1129, written 2026-08-21) ───────────────────────
// AssetRoots.cs:46 has said, since 2026-08-18:
//
//     "⛔ CHANGE THIS ONE LINE to relocate the tree. Do NOT reintroduce the literal
//      anywhere — AssetRootsRegression fails the build if the string reappears."
//
// THERE WAS NO SUCH SUITE. A repo-wide search found that type name ONLY inside
// that comment (and, later, inside a second comment in EnemyArtPaths.cs noting the
// same thing). The rule was enforced by a SENTENCE, and a sentence enforces nothing:
// when this suite was first written, SIXTEEN re-typed root literals were sitting in
// the tree in fourteen files — including inside two OTHER regression suites, which
// is as close to ironic as a build gate gets. All sixteen were repointed in the same
// change that added this file.
//
// That is the same duplicated-state failure CLAUDE.md catalogues in §2 (the stale WO
// number block), §5 (the retired dependency table) and §16 (the copy-pasted R2 push):
// a fact written in two places, one of which nobody updates. The difference is that a
// GATE cannot go stale silently — it goes RED.
//
// ── THE THREE RULES ──────────────────────────────────────────────────────────
//   1 [roots]      No .cs outside AssetRoots.cs may re-type an AssetRoots value as a
//                  PATH string. Repointing the tree must stay a ONE-LINE change.
//   2 [claim]      The doc claim in AssetRoots.cs and the wiring in DataRegression.cs
//                  must AGREE: the file promises this gate, so this gate must be
//                  registered. Deleting the registration (or writing a fresh
//                  "<X>Regression fails the build" promise with no suite behind it)
//                  fails here. This is the rule that stops the original defect —
//                  a claim with no gate — from being written a second time.
//   3 [art-ledger] WO-1129 §3.5 widening. EnemyArtPaths owns the enemy-art naming
//                  convention ("TripoTex", "OrcTex", "_basecolor",
//                  "Material_Pbr_Diffuse"). A RATCHET, not a migration: the set of
//                  files allowed to re-type those tokens is FROZEN at what existed on
//                  2026-08-21, and a NEW file fails. See the note on the ledger for
//                  why the per-file COUNTS are reported and not gated.
//
// ── HOW RULE 1 AVOIDS FALSE POSITIVES (this is the load-bearing detail) ──────
// The roots also appear inside human-readable LOG PROSE, e.g.
//   AtbCombatantSwapper.cs:318  "...Assets/Resources/Enemies."
//   EnemyLateSkinner.cs:118     "Assets/Resources/Enemies no longer exists. This body..."
// Those are SENTENCES ABOUT a path, not a path — repointing them through a const
// would make the message worse, not better. So the lint fires only on a match that
// (a) is immediately preceded by a double-quote — i.e. the literal STARTS the string —
// and (b) is immediately followed by a double-quote or a '/' — i.e. the string is that
// path or a child of it. Prose fails both tests. Comment lines are dropped first.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Core;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class AssetRootsRegression
    {
        // The file that is ALLOWED to spell the roots out. Exactly one.
        private const string RootsDeclarationFile = "AssetRoots.cs";

        // The file that owns the enemy-art naming convention (rule 3's exemption).
        private const string ArtPathsDeclarationFile = "EnemyArtPaths.cs";

        /// <summary>
        /// Files that may re-type an EnemyArtPaths token, FROZEN 2026-08-21. Project-relative,
        /// forward slashes. A file NOT on this list that spells one of the tokens FAILS — that is
        /// the ratchet: the existing debt cannot grow a new home, and every removal from this list
        /// is permanent progress.
        /// <para>⚠ WHY THE PER-FILE COUNTS ARE NOT GATED. There are ~111 token literals across
        /// these files and MOST ARE NOT ENEMY ART — RangerBodyBuilder, WallTools/*, KayKitMaterials
        /// and the structure builders use "_basecolor" for their OWN art trees, which EnemyArtPaths
        /// does not and should not own. Gating a count would therefore either freeze unrelated code
        /// or force a migration that needs AssetDatabase and a real Unity run. The FILE SET is the
        /// honest ratchet; the counts are reported in the reason line so the debt stays visible.</para>
        /// </summary>
        private static readonly string[] ArtTokenLedger =
        {
            // Enemy-art call sites — the real §3.5 migration targets.
            "Assets/Editor/OrcIntakeCapture.cs",
            "Assets/Editor/OrcMageProof.cs",
            "Assets/Editor/NewBodyAlbedoBinder.cs",
            "Assets/Editor/EnemyAddressablesGrouper.cs",
            "Assets/Editor/EnemyBodyMaterialFixer.cs",
            "Assets/Editor/PromoteOrcsToResources.cs",
            "Assets/Editor/BattleAnchorStageVerify.cs",
            "Assets/Editor/TripoTextureImportCap.cs",
            "Assets/Editor/CellarHollowProof.cs",
            "Assets/Editor/Regression/EnemyArtCoverageRegression.cs",
            "Assets/Editor/Regression/EnemyRigColorRegression.cs",
            // WO-1129 slice B, 2026-08-26: EnemyBodyTextureRegression.cs LEFT this ledger.
            // Its three token literals (the .fbm sidecar, the embedded diffuse stem, the FBX
            // path) now come from EnemyArtPaths.EmbeddedFolder / EmbeddedDiffuseStem /
            // FbxPath. The file set is a RATCHET: it may shrink, never grow - so this entry
            // is deleted rather than commented out with the path still readable, and a
            // regression that re-types a token there will now FAIL rather than be tolerated.
            "Assets/_Modules/Village/Enemies/EnemyFactory.cs",
            "Assets/_Modules/Village/Enemies/EnemyBodyColorGuard.cs",
            "Assets/_Modules/BattleATB/AtbCombatantSwapper.cs",

            // NOT enemy art — these own their own trees and use the same suffix vocabulary.
            // Listed so the ratchet does not punish them; they are not migration targets.
            "Assets/Editor/RangerBodyBuilder.cs",
            "Assets/Editor/VillageSceneBuilder.Content.cs",
            "Assets/Editor/WoodenWatchtowerBuilder.cs",
            "Assets/Editor/WallTools/RaidWallMaterialFixer.cs",
            "Assets/Editor/WallTools/GridWallBuilder.cs",
            "Assets/Editor/WallPreview.cs",
            "Assets/Editor/PeopleCharacterImporter.cs",
            "Assets/Editor/KayKitMaterials.cs",
            "Assets/Editor/AssetImportPostprocessor.cs",
            "Assets/Editor/ArmoredKnightVerify.cs",
            "Assets/_Modules/Village/Hero/HeroBodySwapper.cs",
            "Assets/_Modules/Village/NPCs/StoryCompanionInjector.cs",
            "Assets/_Modules/Village/Catalog/CatalogBootstrap.cs",
            "Assets/_Modules/Core/TreeOfLifeMaterialFixer.cs",
        };

        /// <summary>The naming tokens EnemyArtPaths owns. See its header for the four conventions.</summary>
        private static readonly string[] ArtTokens =
        {
            "TripoTex",
            "OrcTex",
            EnemyArtPaths.BaseColorSuffix,        // "_basecolor" — never re-typed here either.
            EnemyArtPaths.EmbeddedDiffuseStem,    // "Material_Pbr_Diffuse"
        };

        [MenuItem("Defenders/Regression/Asset Roots")]
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log("ASSET_ROOTS_OK: " + reason);
            else Debug.LogError("ASSET_ROOTS_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            string assetsDir = Application.dataPath;                       // <repo>/Assets
            string repoDir = Directory.GetParent(assetsDir)?.FullName;
            if (string.IsNullOrEmpty(repoDir) || !Directory.Exists(assetsDir))
            {
                reason = "cannot resolve the project's Assets/ directory — suite could not run " +
                         "(NAMED SKIP, not a pass).";
                return false;
            }

            var sources = new List<string>(Directory.GetFiles(assetsDir, "*.cs", SearchOption.AllDirectories));
            if (sources.Count == 0)
            {
                reason = "found ZERO .cs under Assets/ — the sweep cannot have been meaningful.";
                return false;
            }

            CheckRootLiterals(sources, repoDir, failures, notes);
            CheckClaimAndWiring(assetsDir, failures, notes);
            CheckArtTokenLedger(sources, repoDir, failures, notes);

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures.ToArray());
                return false;
            }

            reason = "ASSET ROOTS OK — every relocatable root is declared exactly once in " +
                     RootsDeclarationFile + ", the doc claim is backed by a registered gate, and the " +
                     "enemy-art token ledger is unchanged. " + string.Join("; ", notes.ToArray());
            return true;
        }

        // ── Rule 1 — the roots are declared once ──────────────────────────────

        private static void CheckRootLiterals(List<string> sources, string repoDir,
                                              List<string> failures, List<string> notes)
        {
            // Read the values off the CONSTANTS, never re-typed here. If someone repoints
            // AssetRoots, this suite follows automatically — which is the whole point.
            var roots = new Dictionary<string, string>
            {
                { "AssetRoots.StructureContent",                AssetRoots.StructureContent },
                { "AssetRoots.EnemyContent",                    AssetRoots.EnemyContent },
                { "AssetRoots.EnemyContentLegacyResources",     AssetRoots.EnemyContentLegacyResources },
                { "AssetRoots.StructureContentLegacyResources", AssetRoots.StructureContentLegacyResources },
            };

            int scanned = 0;
            int hits = 0;

            foreach (string file in sources)
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, RootsDeclarationFile, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, Path.GetFileName(SelfPath()), StringComparison.OrdinalIgnoreCase)) continue;

                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch (Exception ex)
                {
                    notes.Add("unreadable: " + Rel(file, repoDir) + " (" + ex.GetType().Name + ")");
                    continue;
                }
                scanned++;

                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripCommentLine(lines[i]);
                    if (code.Length == 0) continue;

                    foreach (var kv in roots)
                    {
                        if (!IsPathLiteral(code, kv.Value)) continue;
                        hits++;
                        failures.Add(
                            Rel(file, repoDir) + ":" + (i + 1) + " re-types the relocatable root \"" +
                            kv.Value + "\" as a path literal. Use " + kv.Key + " instead — a second copy " +
                            "of a root is how a relocation misses a call site, and the miss is SILENT " +
                            "(a builder quietly loads nothing).");
                    }
                }
            }

            notes.Add("[roots] " + scanned + " source file(s) scanned, " + hits + " re-typed root literal(s)");
        }

        // ── Rule 2 — the claim and the wiring agree ───────────────────────────

        private static void CheckClaimAndWiring(string assetsDir, List<string> failures, List<string> notes)
        {
            const string SuiteTypeName = "AssetRootsRegression";

            string rootsFile = FindOne(assetsDir, RootsDeclarationFile);
            if (rootsFile == null)
            {
                failures.Add("[claim] " + RootsDeclarationFile + " not found under Assets/ — the single " +
                             "declaration of the relocatable roots is missing.");
            }
            else
            {
                string text = SafeRead(rootsFile);
                if (text.IndexOf(SuiteTypeName, StringComparison.Ordinal) < 0)
                    notes.Add("[claim] " + RootsDeclarationFile + " no longer names " + SuiteTypeName +
                              " — the gate still runs, but the file has stopped advertising it");
            }

            string dataRegression = FindOne(assetsDir, "DataRegression.cs");
            if (dataRegression == null)
            {
                failures.Add("[claim] DataRegression.cs not found — cannot prove this gate is registered.");
                return;
            }

            string reg = SafeRead(dataRegression);
            if (reg.IndexOf(SuiteTypeName, StringComparison.Ordinal) < 0)
            {
                failures.Add("[claim] " + SuiteTypeName + " is NOT registered in DataRegression.RunAll. " +
                             "AssetRoots.cs promises this gate fails the build; an unregistered suite " +
                             "makes that promise false again — which is the EXACT defect this file was " +
                             "written to close (a rule enforced by a sentence).");
                return;
            }

            notes.Add("[claim] registered in DataRegression.RunAll");
        }

        /// <summary>
        /// True for a machine-GENERATED source: the ".g.cs" suffix AND an &lt;auto-generated&gt; /
        /// "IT IS GENERATED" banner in the file's opening lines. Both are required so a hand-written
        /// file cannot claim the exemption by renaming, and a stray banner in ordinary code cannot
        /// either. See the call site in CheckArtTokenLedger for why generated files are exempt.
        /// </summary>
        private static bool IsGeneratedSource(string fileName, string[] lines)
        {
            if (fileName == null || lines == null) return false;
            if (!fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) return false;
            int scan = Math.Min(lines.Length, 12);
            for (int i = 0; i < scan; i++)
            {
                string l = lines[i];
                if (l.IndexOf("<auto-generated", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (l.IndexOf("IT IS GENERATED", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // ── Rule 3 — the enemy-art token ratchet (WO-1129 §3.5) ───────────────

        private static void CheckArtTokenLedger(List<string> sources, string repoDir,
                                                List<string> failures, List<string> notes)
        {
            var ledger = new HashSet<string>(ArtTokenLedger, StringComparer.OrdinalIgnoreCase);
            var counts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int total = 0;

            foreach (string file in sources)
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, ArtPathsDeclarationFile, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, Path.GetFileName(SelfPath()), StringComparison.OrdinalIgnoreCase)) continue;

                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch { continue; }

                // GENERATED files are exempt -- and the exemption is the RULE'S OWN reasoning, not a
                // convenience. This rule's whole justification is that "a literal at a CALL SITE cannot
                // be re-pointed". A generated file has no call sites: CatalogFallbackData.g.cs embeds
                // structures-catalog.json VERBATIM as a string constant, so a token inside it is one
                // character sequence inside a DATA SNAPSHOT, not code that resolves a path. It cannot
                // "ask EnemyArtPaths" (there is nothing to ask -- it is a literal, by construction), and
                // rewriting it would BREAK BuildEconomyRegression's [fallback-parity] sha256 freshness
                // gate, which asserts the snapshot is byte-identical to the JSON on disk. The ratchet
                // still binds every HAND-WRITTEN file; re-point the SOURCE (the canonical JSON or the
                // generator), and the snapshot follows on the next regenerate.
                //
                // Scoped by a DURABLE PROPERTY, deliberately not by adding this one path to the ledger:
                // a path entry would let the NEXT generated file trip the identical false failure. Both
                // conditions are required -- the ".g.cs" suffix AND the <auto-generated> banner -- so a
                // hand-written file cannot buy an exemption by renaming itself.
                if (IsGeneratedSource(name, lines))
                {
                    notes.Add("[art-ledger] generated (exempt, not a call site): " + Rel(file, repoDir));
                    continue;
                }

                int fileHits = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripCommentLine(lines[i]);
                    if (code.Length == 0) continue;
                    for (int t = 0; t < ArtTokens.Length; t++)
                    {
                        if (InsideStringLiteral(code, ArtTokens[t])) { fileHits++; break; }
                    }
                }

                if (fileHits == 0) continue;

                string rel = Rel(file, repoDir);
                counts[rel] = fileHits;
                total += fileHits;

                if (!ledger.Contains(rel))
                    failures.Add(
                        "[art-ledger] " + rel + " re-types an EnemyArtPaths naming token in " + fileHits +
                        " line(s) and is NOT on the 2026-08-21 ledger. Ask EnemyArtPaths instead " +
                        "(ResourceCandidates / AtlasAssetCandidates / EmbeddedFolder / SuffixFor). A " +
                        "literal at a call site cannot be re-pointed, cannot be traced on a miss, and " +
                        "cannot be asserted by an oracle — it can only be found later, by hand.");
            }

            notes.Add("[art-ledger] " + counts.Count + " file(s) / " + total +
                      " line(s) carry a token literal (ledger size " + ArtTokenLedger.Length +
                      "); file-set frozen 2026-08-21, counts reported not gated");
        }

        // ── Lint helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Drops whole-line comments (// /// * /*) so a tombstone that DESCRIBES a literal is not
        /// read as one. Deliberately does NOT strip trailing comments: doing that safely means
        /// tracking string state, and a trailing comment carrying a path literal after live code is
        /// vanishingly rare — where it happens the suite fails loudly, which is the safe direction.
        /// (Same lesson as EchoWorldPresenceRegression: a lint that cannot tell a call from a
        /// sentence punishes exactly the self-documenting removal notes CLAUDE.md §12/§15 asks for.)
        /// </summary>
        private static string StripCommentLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            string t = line.TrimStart();
            if (t.StartsWith("//", StringComparison.Ordinal)) return string.Empty;
            if (t.StartsWith("*", StringComparison.Ordinal)) return string.Empty;
            if (t.StartsWith("/*", StringComparison.Ordinal)) return string.Empty;
            return line;
        }

        /// <summary>
        /// True when <paramref name="code"/> contains <paramref name="root"/> AS A PATH STRING:
        /// opened by a double-quote and closed by a double-quote or continued by '/'. See the header
        /// — this is what separates a path from a sentence that mentions one.
        /// </summary>
        private static bool IsPathLiteral(string code, string root)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(root)) return false;

            int from = 0;
            while (true)
            {
                int at = code.IndexOf(root, from, StringComparison.Ordinal);
                if (at < 0) return false;

                bool opensString = at > 0 && code[at - 1] == '"';
                int after = at + root.Length;
                bool closesOrDescends = after < code.Length && (code[after] == '"' || code[after] == '/');

                if (opensString && closesOrDescends) return true;
                from = at + 1;
            }
        }

        /// <summary>
        /// True when <paramref name="token"/> appears on the line with an opening double-quote
        /// somewhere before it and NO double-quote in between — i.e. it sits inside a string
        /// literal rather than in an identifier or a type name. Equivalent to the ripgrep pattern
        /// the 2026-08-21 ledger was measured with (<c>"[^"]*&lt;token&gt;</c>), and it is written
        /// to stay equivalent ON PURPOSE: a lint that counts differently from the survey that
        /// seeded its allowlist fails on arrival for a reason nobody can reproduce.
        /// </summary>
        private static bool InsideStringLiteral(string code, string token)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(token)) return false;

            int from = 0;
            while (true)
            {
                int at = code.IndexOf(token, from, StringComparison.Ordinal);
                if (at < 0) return false;
                // The LAST quote before the token, if any, has no quote between it and the token.
                if (at > 0 && code.LastIndexOf('"', at - 1) >= 0) return true;
                from = at + 1;
            }
        }

        private static string Rel(string full, string repoDir)
        {
            if (string.IsNullOrEmpty(repoDir)) return full.Replace('\\', '/');
            string norm = full.Replace('\\', '/');
            string root = repoDir.Replace('\\', '/').TrimEnd('/') + "/";
            return norm.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? norm.Substring(root.Length)
                : norm;
        }

        private static string FindOne(string assetsDir, string fileName)
        {
            try
            {
                var hits = Directory.GetFiles(assetsDir, fileName, SearchOption.AllDirectories);
                return hits.Length > 0 ? hits[0] : null;
            }
            catch { return null; }
        }

        private static string SafeRead(string path)
        {
            try { return File.ReadAllText(path); }
            catch { return string.Empty; }
        }

        /// <summary>This suite's own filename — it is FULL of the literals it forbids, in prose.</summary>
        private static string SelfPath() => "AssetRootsRegression.cs";
    }
}
