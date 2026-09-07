// =============================================================================
// AllowlistExpiryRegression [allowlist-expiry]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
// Markers : ALLOWLIST_EXPIRY_OK / ALLOWLIST_EXPIRY_FAIL
//
// THE DEFECT THIS EXISTS FOR (WO-1495, read-only audit fleet 2026-09-06):
// thirteen exemption blocks across the regression suites carried no owning ticket,
// no date and no expiry. An exemption in that shape is indistinguishable from a
// defect somebody decided to stop looking at, and because nothing ever forces a
// re-read, the suite reports GREEN forever on the exact content the block was
// written to cover. The audit's four largest were MageAbilityIconRegression
// KnownGaps, EnemyPoolResetRegression BrainExempt, UiObsidianConformance
// AllowList (it cited WO-178 but no date) and ShaderPredicateSingleAuthority
// KnownInlineDebt.
//
// WHAT IT ASSERTS. Every allowlist/exemption/known-debt collection declared in a
// file under Assets/Editor/Regression must carry, in the FIVE source lines
// immediately above its declaration:
//   * a WO pointer            - WO-<n>, so the block has an owner and a paper trail
//   * an ORIGIN date          - YYYY-MM-DD, so the one-week validity threshold
//                               (SUNDAY_HOUSEKEEPING.md section 4) can be applied
//   * an expiry               - remove-by YYYY-MM-DD, so the block is re-read on a
//                               date somebody chose, not never
// and the remove-by must not already be in the past.
//
// The origin date is tested on the context with every `remove-by <date>` clause
// STRIPPED OUT first. Without that, an annotation carrying only a remove-by would
// satisfy the origin-date check with the expiry's own date and the pointer would
// prove nothing about when the block was written.
//
// SHAPE-BASED EXCLUSIONS, NOT AN OPT-OUT TOKEN. Four of the matched blocks are
// not parked debt at all - they are DEFINITIONAL (a restatement of a production
// constant, a regex that scopes a ban, a BCL false-positive suppression, a data
// rule the suite re-verifies on every run). A `remove-by` on one of those would be
// fiction, and CLAUDE.md section 11B forbids stating an unproven thing as fact.
// They are named one-by-one in DefinitionalAllowlists below WITH a reason, rather
// than given a comment token any future author could paste over real debt - a
// token would be exactly the hole WO-1495 section 3 warns about. That exclusion
// list is itself matched by this suite's own name scan, so it carries the same
// WO + date + remove-by every other block does, and case [definitional-accurate]
// fails when one of its entries no longer exists.
//
// NO HOLLOW PASS: if the root does not exist, or the scan finds fewer than
// MinBlocksExpected declarations, the suite FAILS rather than passing over a
// detector that has gone blind.
//
// OUT OF LANE (WO-1495 section 2, deliberately not built here): the GROWTH half of
// the ratchet - failing when a block gains entries - needs a per-block committed
// baseline and was not part of this lane's dispatch. Recorded so the next seat
// does not read its absence as an oversight.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class AllowlistExpiryRegression
    {
        private const string Root = "Assets/Editor/Regression";

        /// <summary>Below this the name scan has stopped seeing the suites and cases 2/3 would
        /// pass vacuously. Set well under the 2026-09-06 reading (17 blocks) so ordinary
        /// cleanup never trips it, but a detector pointed at a moved tree does.</summary>
        private const int MinBlocksExpected = 10;

        /// <summary>Name fragments that mark a collection as an allowlist / exemption / parked
        /// debt. Lower-cased substring match on the declared identifier.</summary>
        private static readonly string[] DebtNameTokens =
        {
            "exempt", "exemption", "allowlist", "whitelist", "waiver", "knowngaps",
        };

        /// <summary>
        /// DEFINITIONAL blocks: matched by the name scan above, but they are not parked debt and
        /// an expiry on them would be a claim nobody can honour. Key is "FileName.cs:Identifier",
        /// value is why the shape excludes it. Case [definitional-accurate] FAILS when an entry no
        /// longer matches anything, so this list cannot rot into a hiding place of its own.
        /// </summary>
        // WO-1495 2026-09-06 remove-by 2026-12-06 - this exclusion list is itself an allowlist and
        // is held to the same annotation contract it enforces; re-read the four entries then.
        private static readonly Dictionary<string, string> DefinitionalAllowlists =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "RemoteCatalogSeamRegression.cs:ExpectedAllowlist",
                "a PINNED FACT - an independent restatement of RemoteCatalogOverrides.Allowlist " +
                "that the suite compares against the production constant. It exempts nothing; it " +
                "reds when the two disagree. Removing it would delete an oracle."
            },
            {
                "GooglePlayPackagingGate.cs:FalsePositiveAllowlist",
                "BCL name suppression (cryptograph/cryptoconfig/cryptostream). System.Security." +
                "Cryptography is in every IL2CPP metadata blob ever produced, so this can never " +
                "be removed and any remove-by date on it would be fiction."
            },
            {
                "BannedVfxRegression.cs:VariantExempt",
                "a Regex that SCOPES the owner's VFX ban to the un-suffixed prefab - the ban " +
                "deliberately never covered the colour variants. It is the rule's definition, " +
                "not an exemption from it."
            },
            {
                "ArmourCatalogJobRegression.cs:Whitelist",
                "the job:\"any\" armour list, whose every entry is RE-VERIFIED against the data " +
                "on every run (case 3 fails an entry that stops carrying no weight). It is " +
                "self-expiring by construction, so a calendar date adds nothing."
            },
        };

        // Declaration shape: `... readonly|const <type...> <Name> =`. Matched on the raw line so
        // the five lines of context above it are the author's real comment block.
        private static readonly Regex DeclLine = new Regex(
            @"\b(?:readonly|const)\s+[^;=\r\n]*?\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=",
            RegexOptions.Compiled);

        private static readonly Regex WoPointer = new Regex(@"\bWO-\d+\b", RegexOptions.Compiled);
        private static readonly Regex AnyDate = new Regex(@"\b\d\d\d\d-\d\d-\d\d\b", RegexOptions.Compiled);
        private static readonly Regex RemoveBy = new Regex(
            @"remove-by\s+(?<date>\d\d\d\d-\d\d-\d\d)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private sealed class Block
        {
            public string File;      // file name only
            public string Name;      // declared identifier
            public int Line;         // 1-based
            public string Context;   // the five lines above the declaration
            public string Key { get { return File + ":" + Name; } }
        }

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ALLOWLIST_EXPIRY_OK - " + reason);
            else Debug.LogError("ALLOWLIST_EXPIRY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            int annotated = 0, definitional = 0;

            try
            {
                var blocks = Scan(failures);

                Case(failures, "scan-alive", () =>
                {
                    if (blocks.Count < MinBlocksExpected)
                        failures.Add("[scan-alive] the name scan matched only " + blocks.Count +
                                     " allowlist/exemption declarations under " + Root + ", below the " +
                                     MinBlocksExpected + " floor. This is NOT a pass - the 2026-09-06 " +
                                     "audit measured 17. Either the suites moved, or DeclLine / " +
                                     "DebtNameTokens stopped matching real declarations. A scan that " +
                                     "finds nothing is a green check over a hole.");
                });

                Case(failures, "pointer-present", () =>
                {
                    foreach (var b in blocks)
                    {
                        if (DefinitionalAllowlists.ContainsKey(b.Key)) { definitional++; continue; }

                        string ctx = b.Context;
                        bool hasWo = WoPointer.IsMatch(ctx);
                        // Strip the expiry clause first: without this, `remove-by 2026-12-06`
                        // alone would satisfy the origin-date test with its own date.
                        string ctxNoExpiry = RemoveBy.Replace(ctx, " ");
                        bool hasDate = AnyDate.IsMatch(ctxNoExpiry);
                        bool hasRemoveBy = RemoveBy.IsMatch(ctx);

                        if (hasWo && hasDate && hasRemoveBy) { annotated++; continue; }

                        var missing = new List<string>();
                        if (!hasWo) missing.Add("a WO-<n> pointer");
                        if (!hasDate) missing.Add("an origin YYYY-MM-DD date");
                        if (!hasRemoveBy) missing.Add("a remove-by YYYY-MM-DD");

                        failures.Add("[pointer-present] " + b.File + ":" + b.Line + " '" + b.Name +
                                     "' is an allowlist/exemption block missing " +
                                     string.Join(" and ", missing.ToArray()) +
                                     " in the five lines above its declaration. An exemption with no " +
                                     "owner and no expiry is indistinguishable from a defect someone " +
                                     "stopped looking at (WO-1495). Annotate it with the ticket, the " +
                                     "date it was measured and a remove-by - or delete the entries and " +
                                     "let the suite go red, which is the honest state.");
                    }
                });

                Case(failures, "not-expired", () =>
                {
                    DateTime today = DateTime.Now.Date;
                    foreach (var b in blocks)
                    {
                        if (DefinitionalAllowlists.ContainsKey(b.Key)) continue;
                        Match m = RemoveBy.Match(b.Context);
                        if (!m.Success) continue;   // already failed above

                        DateTime due;
                        if (!DateTime.TryParse(m.Groups["date"].Value, out due))
                        {
                            failures.Add("[not-expired] " + b.File + ":" + b.Line + " '" + b.Name +
                                         "' carries an unparseable remove-by '" +
                                         m.Groups["date"].Value + "'.");
                            continue;
                        }

                        if (due.Date < today)
                            failures.Add("[not-expired] " + b.File + ":" + b.Line + " '" + b.Name +
                                         "' EXPIRED on " + due.ToString("yyyy-MM-dd") + ". The block was " +
                                         "parked with a date somebody chose and that date has passed: " +
                                         "either the entries come out and the suite goes red on what is " +
                                         "genuinely broken, or the block is re-measured and re-dated " +
                                         "with the ticket that re-measured it. Pushing the date without " +
                                         "re-reading the entries is the rot this case exists to stop.");
                    }
                });

                Case(failures, "definitional-accurate", () =>
                {
                    foreach (var kv in DefinitionalAllowlists)
                    {
                        bool found = false;
                        foreach (var b in blocks)
                            if (string.Equals(b.Key, kv.Key, StringComparison.OrdinalIgnoreCase)) { found = true; break; }

                        if (!found)
                            failures.Add("[definitional-accurate] '" + kv.Key + "' is listed as a " +
                                         "definitional (non-expiring) block, but the scan no longer " +
                                         "matches any declaration by that name. It was renamed, moved or " +
                                         "deleted. A dead exclusion guards nothing and hides the next " +
                                         "block that lands in its place - delete the line, here, in the " +
                                         "same change. (Recorded reason: " + kv.Value + ")");
                    }
                });

                notes.Add("blocks=" + blocks.Count + " annotated=" + annotated +
                          " definitional=" + definitional);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [" + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "ALLOWLIST EXPIRY OK - every allowlist/exemption block under " + Root +
                         " carries a WO pointer, an origin date and an unexpired remove-by, and every " +
                         "definitional exclusion still names a real declaration" + noteStr;
                return true;
            }

            reason = "allowlist-expiry FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  SCAN - one pass over the regression suites, line-oriented
        // =====================================================================

        private static List<Block> Scan(List<string> failures)
        {
            var result = new List<Block>();

            if (!Directory.Exists(Root))
            {
                failures.Add("[scan] regression root '" + Root + "' does not exist - this oracle is " +
                             "pointed at a tree that moved; every case below would pass vacuously.");
                return result;
            }

            string[] paths;
            try { paths = Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                failures.Add("[scan] could not enumerate '" + Root + "': " +
                             ex.GetType().Name + ": " + ex.Message);
                return result;
            }

            foreach (var raw in paths)
            {
                string fileName = Path.GetFileName(raw);
                string[] lines;
                try { lines = File.ReadAllLines(raw); }
                catch (Exception ex)
                {
                    failures.Add("[scan] could not read " + fileName + ": " +
                                 ex.GetType().Name + ": " + ex.Message);
                    continue;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.IndexOf("static", StringComparison.Ordinal) < 0) continue;

                    // A commented-out declaration is prose, not a live block.
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                        trimmed.StartsWith("*", StringComparison.Ordinal)) continue;

                    Match m = DeclLine.Match(line);
                    if (!m.Success) continue;

                    string name = m.Groups["name"].Value;
                    if (!IsDebtName(name)) continue;

                    var ctx = new StringBuilder();
                    for (int k = Math.Max(0, i - 5); k < i; k++) ctx.AppendLine(lines[k]);

                    result.Add(new Block
                    {
                        File = fileName,
                        Name = name,
                        Line = i + 1,
                        Context = ctx.ToString(),
                    });
                }
            }

            return result;
        }

        private static bool IsDebtName(string name)
        {
            string lower = name.ToLowerInvariant();
            foreach (var t in DebtNameTokens)
                if (lower.IndexOf(t, StringComparison.Ordinal) >= 0) return true;

            // `Known...Debt` - the shape ShaderPredicateSingleAuthorityRegression and
            // ForgeShelfClassKindRegression use for parked debt that is not called an exemption.
            if (lower.StartsWith("known", StringComparison.Ordinal) &&
                lower.EndsWith("debt", StringComparison.Ordinal)) return true;

            return false;
        }
    }
}
