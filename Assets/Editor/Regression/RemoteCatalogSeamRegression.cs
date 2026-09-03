// =============================================================================
// RemoteCatalogSeamRegression [catalog-seam]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (already references DeNelle.Core - no asmdef
//   edit needed, same as RemoteTunablesDefaultsRegression).
//
// Pins WO-1331's single most important invariant:
//
//     NO ROW / NO NETWORK / NO SERVER / NO PARSE
//         => EVERY CANONICAL CATALOG RESOLVES ITS COMPILED COPY,
//            BYTE FOR BYTE, i.e. TODAY'S BEHAVIOUR.
//
// -----------------------------------------------------------------------------
// WHY THIS ONE MATTERS: THE BLAST RADIUS IS THE WHOLE PRODUCT.
// -----------------------------------------------------------------------------
// CanonicalJson is how EVERY catalog in the game loads - abilities, enemies,
// waves, gear, quests, economy. A regression in this seam does not crash: it
// silently serves the wrong data, or serves half a catalog, and the game keeps
// running while no longer being the build anyone shipped. The game is live on a
// store taking real money.
//
// So the fall-through is ASSERTED, not assumed, and it is asserted on EVERY
// failure mode rather than on a sample: empty body, whitespace, malformed JSON, a
// TRUNCATED body, an oversized body, readOk=false, an empty catalogs map, a
// good-JSON-but-wrong-root payload, a payload missing a top-level key, a payload
// naming a MONEY file, a payload naming a catalog this build has never heard of,
// a corrupt device cache, and garbage arriving after a good payload. After each
// one, all five allowlisted catalogs are compared ORDINAL, FULL STRING against
// the compiled text.
//
// -----------------------------------------------------------------------------
// IT ALSO PROVES THE MECHANISM WORKS, WHICH IS NOT OPTIONAL.
// -----------------------------------------------------------------------------
// An oracle that only ever proves "nothing happened" goes green just as happily
// when the feature is dead code. Case [override-applies] drives a VALID payload
// through and asserts the override IS served, that every other catalog is
// untouched by it, and that Clear() puts the compiled text back. Green requires
// both halves.
//
// -----------------------------------------------------------------------------
// ZERO NETWORK, ZERO DATABASE.
// -----------------------------------------------------------------------------
// RemoteCatalogOverrides is transport-free by design (the same split
// RemoteTunables keeps), so every failure mode is driven by handing it a STRING.
// Nothing here opens a socket or cares whether the machine is online.
//
// PlayerPrefs is the one piece of ambient state that could poison the run: a
// developer machine with ff.catalogremote armed is legitimately NOT the default,
// so the suite SNAPSHOTS it, clears it, and RESTORES it in a finally - and NOTES
// what it found, because an arm left on a build machine is worth knowing about.
//
// Cases:
//   1 [flag-off]         The seam is disarmed by default, is NOT installed, and
//                        CanonicalJson.Source is still a LocalJsonCatalogSource.
//                        Byte-identity is proved by RESOLVING every allowlisted
//                        catalog both ways and comparing the full strings ordinal.
//   2 [failure-modes]    Thirteen rejection paths, each re-asserting ALL FIVE
//                        catalogs resolve their compiled text.
//   3 [override-applies] A valid payload IS served, only for its own path, and
//                        Clear() restores the compiled text.
//   4 [money-boundary]   Deny is non-empty, disjoint from the allowlist, contains
//                        the real-money files, is checked BEFORE the allowlist,
//                        and a payload naming one is rejected WHOLESALE.
//   5 [allowlist-shape]  Five entries, pinned as a literal; all unique, ASCII,
//                        under Data/Canonical/, and all present in this build.
//   6 [never-blocks]     No blocking idiom in RemoteCatalogService, the boot hook
//                        still fires and forgets, and CanonicalJson.Source is
//                        assigned in exactly ONE place in the whole tree.
//
// Markers: CATALOG_SEAM_OK / CATALOG_SEAM_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.RemoteCatalogSeamRegression.RunAll
// Registered in DataRegression.RunAll as "[catalog-seam]".
//
// ⭐ THE MUTATION THAT PROVES THIS SUITE IS ALIVE (apply, expect RED, revert):
//    in RemoteCatalogOverrides.ApplyPayload, change the wholesale rejection at a
//    failed Validate() from `return false;` to `continue;`. That is the exact
//    "partial merge" defect this ticket forbids, and it must red case
//    [failure-modes] on the truncated-body and missing-top-level-key paths.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RemoteCatalogSeamRegression
    {
        // ---------------------------------------------------------------------
        //  PINNED FACTS. Every one is a literal - an oracle that measures the
        //  thing against itself certifies nothing.
        // ---------------------------------------------------------------------

        /// <summary>The proven set is FIVE catalogs. WO-1331 scoped it deliberately small;
        /// widening it is a data decision and must move this literal in the same commit.</summary>
        private const int ExpectedAllowlistCount = 5;

        /// <summary>Stated independently of RemoteCatalogOverrides.Allowlist, and compared
        /// against it. Change one and this suite reds naming which two disagree.</summary>
        private static readonly string[] ExpectedAllowlist =
        {
            "Data/Canonical/enemies.json",
            "Data/Canonical/waves.json",
            "Data/Canonical/echoes-balance.json",
            "Data/Canonical/kill-rewards.json",
            "Data/Canonical/siege-stakes.json",
        };

        /// <summary>The permanent money boundary, stated independently. These are decided
        /// server-side (api/_lib/purchase-catalog.js) and may never be client-overridable.</summary>
        private static readonly string[] ExpectedDenylist =
        {
            "Data/Canonical/packs.json",
            "Data/Canonical/wallets.json",
        };

        private const string LocalArmPref = "ff.catalogremote";
        private const string ServiceSrc = "Assets/_Modules/Core/Data/RemoteCatalogService.cs";
        private const string OverridesSrc = "Assets/_Modules/Core/Data/RemoteCatalogOverrides.cs";

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("CATALOG_SEAM_OK - " + reason);
            else Debug.LogError("CATALOG_SEAM_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            // A developer machine with the seam armed is legitimately NOT the default, and
            // an oracle that reds on the machine rather than on the code is worse than none.
            int armSnapshot = PlayerPrefs.GetInt(LocalArmPref, int.MinValue);
            if (armSnapshot != int.MinValue)
            {
                notes.Add("NOTE: " + LocalArmPref + "=" + armSnapshot + " was armed on this machine; " +
                          "cleared for the run and restored afterwards");
                PlayerPrefs.DeleteKey(LocalArmPref);
            }

            try
            {
                RemoteCatalogOverrides.Clear();

                Case(failures, "flag-off", () => Case1_FlagOff(failures, notes));
                Case(failures, "failure-modes", () => Case2_FailureModes(failures, notes));
                Case(failures, "override-applies", () => Case3_OverrideApplies(failures, notes));
                Case(failures, "money-boundary", () => Case4_MoneyBoundary(failures, notes));
                Case(failures, "allowlist-shape", () => Case5_AllowlistShape(failures, notes));
                Case(failures, "never-blocks", () => Case6_NeverBlocks(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                RemoteCatalogOverrides.Clear();
                if (armSnapshot != int.MinValue) PlayerPrefs.SetInt(LocalArmPref, armSnapshot);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "CATALOG SEAM OK - the remote canonical-catalog seam is DISARMED by default " +
                         "(CanonicalJson.Source untouched, nothing constructed, nothing polled), and on " +
                         "all thirteen rejection paths - empty body, whitespace, malformed JSON, a " +
                         "TRUNCATED body, an oversized body, server readOk=false, an empty catalogs map, " +
                         "a wrong-root payload, a payload missing a top-level key, a MONEY path, an " +
                         "unknown path, a corrupt device cache, and garbage after a good payload - all " +
                         ExpectedAllowlistCount + " allowlisted catalogs resolve their COMPILED text " +
                         "byte for byte. A VALID payload is served (so this is not vacuously green), " +
                         "affects only its own catalog, and Clear() restores the compiled text. The " +
                         "money boundary is enforced in code before the allowlist and rejects wholesale; " +
                         "the allowlist agrees with this oracle's literals; and the fetch still cannot " +
                         "block boot" + noteStr;
                return true;
            }
            reason = "catalog-seam FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - THE FLAG IS OFF, AND OFF MEANS ABSENT
        // =====================================================================
        private static void Case1_FlagOff(List<string> failures, List<string> notes)
        {
            if (FeatureFlags.RemoteCatalogs)
                failures.Add("[flag-off] FeatureFlags.RemoteCatalogs is TRUE with no PlayerPrefs " +
                             "override. WO-1331 ships this OFF: it changes how EVERY catalog in the " +
                             "game loads and the product is live on a store.");

            if (RemoteCatalogService.Enabled)
                failures.Add("[flag-off] RemoteCatalogService.Enabled is TRUE by default. The seam must " +
                             "be armed deliberately, never by shipping.");

            if (RemoteCatalogService.Installed)
                failures.Add("[flag-off] RemoteCatalogService.Installed is TRUE - the seam was wrapped " +
                             "around CanonicalJson.Source while disarmed. Disarmed must mean ABSENT.");

            if (!(CanonicalJson.Source is LocalJsonCatalogSource))
                failures.Add("[flag-off] CanonicalJson.Source is a " +
                             (CanonicalJson.Source == null ? "NULL" : CanonicalJson.Source.GetType().Name) +
                             ", not the LocalJsonCatalogSource its own field initializer sets. With the " +
                             "flag off nothing may assign it at all.");

            // BYTE IDENTITY, RESOLVED BOTH WAYS. Not asserted - measured.
            var local = new LocalJsonCatalogSource();
            var wrapped = new RemoteCatalogSource(local);
            foreach (string path in ExpectedAllowlist)
            {
                string direct = local.Read(path);
                string through = wrapped.Read(path);
                if (string.IsNullOrEmpty(direct))
                {
                    failures.Add("[flag-off] the COMPILED copy of '" + path + "' did not resolve at all. " +
                                 "The whole fall-through argument rests on it being there.");
                    continue;
                }
                if (!string.Equals(direct, through, StringComparison.Ordinal))
                    failures.Add("[flag-off] '" + path + "' differs through the seam with no overrides: " +
                                 direct.Length + " chars direct vs " + through.Length + " through. The " +
                                 "no-row path MUST be byte-identical.");
            }

            if (RemoteCatalogOverrides.RowCount != 0)
                failures.Add("[flag-off] the standing override table holds " +
                             RemoteCatalogOverrides.RowCount + " row(s) with nothing applied.");

            if (!string.Equals(RemoteCatalogOverrides.TableProvenance,
                               RemoteCatalogOverrides.ProvenanceDefault, StringComparison.Ordinal))
                failures.Add("[flag-off] tableProvenance is '" + RemoteCatalogOverrides.TableProvenance +
                             "', expected '" + RemoteCatalogOverrides.ProvenanceDefault + "'.");
        }

        // =====================================================================
        //  Case 2 - EVERY FAILURE MODE, EVERY CATALOG, EVERY TIME
        // =====================================================================
        private static void Case2_FailureModes(List<string> failures, List<string> notes)
        {
            string goodPath = ExpectedAllowlist[3];              // kill-rewards.json
            string compiled = new LocalJsonCatalogSource().Read(goodPath);
            if (string.IsNullOrEmpty(compiled))
            {
                failures.Add("[failure-modes] cannot run: '" + goodPath + "' has no compiled copy.");
                return;
            }
            string goodPayload = BuildPayload(goodPath, MarkedCopy(compiled));

            // path name -> the string handed to ApplyPayload
            var modes = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("null body", null),
                new KeyValuePair<string, string>("empty body", ""),
                new KeyValuePair<string, string>("whitespace body", "   \n\t  "),
                new KeyValuePair<string, string>("malformed JSON", "{ this is not json ,,, }"),
                new KeyValuePair<string, string>("truncated payload",
                    goodPayload.Substring(0, Math.Max(1, (int)(goodPayload.Length * 0.6)))),
                new KeyValuePair<string, string>("truncated CATALOG inside a well-formed payload",
                    BuildPayload(goodPath, compiled.Substring(0, Math.Max(1, compiled.Length / 2)))),
                new KeyValuePair<string, string>("oversized catalog body",
                    BuildPayload(goodPath, "{\"x\":\"" +
                        new string('y', RemoteCatalogOverrides.MaxCatalogBytes + 16) + "\"}")),
                new KeyValuePair<string, string>("server readOk=false",
                    "{\"version\":1,\"readOk\":false,\"reason\":\"table unreadable\"}"),
                new KeyValuePair<string, string>("empty catalogs map",
                    "{\"version\":1,\"readOk\":true,\"catalogs\":{}}"),
                new KeyValuePair<string, string>("wrong root kind",
                    BuildPayload(goodPath, "[1,2,3]")),
                new KeyValuePair<string, string>("missing top-level key",
                    BuildPayload(goodPath, StrippedFirstKey(compiled))),
                new KeyValuePair<string, string>("MONEY path (deny list)",
                    BuildPayload("Data/Canonical/packs.json", "{\"packs\":[]}")),
                new KeyValuePair<string, string>("catalog this build never heard of",
                    BuildPayload("Data/Canonical/not-a-real-catalog.json", "{\"a\":1}")),
            };

            foreach (var mode in modes)
            {
                RemoteCatalogOverrides.Clear();
                RemoteCatalogOverrides.ApplyPayload(mode.Value, RemoteCatalogOverrides.ProvenanceRemote);
                AssertAllCompiled(failures, "failure-modes", mode.Key);
            }

            // The corrupt DEVICE CACHE path, driven through the service's own public entry
            // point - the same code the [RuntimeInitializeOnLoadMethod] hook runs.
            RemoteCatalogOverrides.Clear();
            if (RemoteCatalogService.ApplyCachedPayload("{\"version\":1,\"readOk\":true,\"catalogs\":}"))
                failures.Add("[failure-modes] a corrupt device cache was ACCEPTED.");
            AssertAllCompiled(failures, "failure-modes", "corrupt device cache");

            // GARBAGE AFTER A GOOD PAYLOAD - the standing table must be left exactly as it
            // was, never half-replaced.
            RemoteCatalogOverrides.Clear();
            if (!RemoteCatalogOverrides.ApplyPayload(goodPayload, RemoteCatalogOverrides.ProvenanceRemote))
            {
                failures.Add("[failure-modes] a VALID payload was rejected, so the garbage-after-good " +
                             "path cannot be driven. Check RemoteCatalogOverrides.Validate.");
            }
            else
            {
                int rowsBefore = RemoteCatalogOverrides.RowCount;
                RemoteCatalogOverrides.ApplyPayload("}}}not json{{{", RemoteCatalogOverrides.ProvenanceRemote);
                if (RemoteCatalogOverrides.RowCount != rowsBefore)
                    failures.Add("[failure-modes] garbage after a good payload changed the standing " +
                                 "table (" + rowsBefore + " -> " + RemoteCatalogOverrides.RowCount +
                                 "). A rejected payload must leave the previous one exactly as it was.");
                if (!RemoteCatalogOverrides.TryGet(goodPath, out _))
                    failures.Add("[failure-modes] garbage after a good payload DROPPED the standing " +
                                 "override for '" + goodPath + "'.");
            }
            RemoteCatalogOverrides.Clear();
        }

        // =====================================================================
        //  Case 3 - AND IT ACTUALLY WORKS (this suite is not vacuously green)
        // =====================================================================
        private static void Case3_OverrideApplies(List<string> failures, List<string> notes)
        {
            string path = ExpectedAllowlist[2];                  // echoes-balance.json
            var local = new LocalJsonCatalogSource();
            string compiled = local.Read(path);
            if (string.IsNullOrEmpty(compiled))
            {
                failures.Add("[override-applies] cannot run: '" + path + "' has no compiled copy.");
                return;
            }

            string marked = MarkedCopy(compiled);
            RemoteCatalogOverrides.Clear();
            if (!RemoteCatalogOverrides.ApplyPayload(BuildPayload(path, marked),
                                                     RemoteCatalogOverrides.ProvenanceRemote))
            {
                failures.Add("[override-applies] a VALID payload for '" + path + "' was REJECTED. The " +
                             "seam would be dead code, and every other case in this suite would pass " +
                             "for the wrong reason.");
                RemoteCatalogOverrides.Clear();
                return;
            }

            var wrapped = new RemoteCatalogSource(local);
            string served = wrapped.Read(path);
            if (!string.Equals(served, marked, StringComparison.Ordinal))
                failures.Add("[override-applies] the seam did NOT serve the accepted override for '" +
                             path + "' (got " + (served == null ? "null" : served.Length + " chars") +
                             ", expected " + marked.Length + ").");

            // ONLY that catalog. An override must never leak across paths.
            foreach (string other in ExpectedAllowlist)
            {
                if (string.Equals(other, path, StringComparison.Ordinal)) continue;
                string direct = local.Read(other);
                string through = wrapped.Read(other);
                if (!string.Equals(direct, through, StringComparison.Ordinal))
                    failures.Add("[override-applies] overriding '" + path + "' also changed '" + other +
                                 "'. Overrides are per-catalog and must never leak.");
            }

            // And Clear() puts the compiled text back, on the same instance.
            RemoteCatalogOverrides.Clear();
            if (!string.Equals(wrapped.Read(path), compiled, StringComparison.Ordinal))
                failures.Add("[override-applies] after Clear() the seam did not go back to the COMPILED " +
                             "copy of '" + path + "'. Clearing is the one-word way back to today.");
        }

        // =====================================================================
        //  Case 4 - THE MONEY BOUNDARY, ENFORCED IN CODE
        // =====================================================================
        private static void Case4_MoneyBoundary(List<string> failures, List<string> notes)
        {
            var deny = RemoteCatalogOverrides.Denylist;
            if (deny == null || deny.Length == 0)
            {
                failures.Add("[money-boundary] the deny list is EMPTY. Prices, entitlements and grants " +
                             "are server-authoritative (api/_lib/purchase-catalog.js) and the boundary " +
                             "must be code, not prose.");
                return;
            }

            foreach (string expected in ExpectedDenylist)
            {
                if (!RemoteCatalogOverrides.IsDenied(expected))
                    failures.Add("[money-boundary] '" + expected + "' is NOT denied. It carries " +
                                 "real-money shape and the game takes real money on mainnet.");
            }

            // Disjoint - so widening the allowlist later can never admit a money file.
            foreach (string a in RemoteCatalogOverrides.Allowlist)
            {
                if (RemoteCatalogOverrides.IsDenied(a))
                    failures.Add("[money-boundary] '" + a + "' is on BOTH lists. Deny wins, but a list " +
                                 "that contradicts itself is a list nobody can reason about.");
            }

            // Deny is checked BEFORE allow: a denied path is never 'allowed', whatever else.
            foreach (string d in RemoteCatalogOverrides.Denylist)
            {
                if (RemoteCatalogOverrides.IsAllowed(d))
                    failures.Add("[money-boundary] IsAllowed('" + d + "') is TRUE for a DENIED path - " +
                                 "the deny check is not running first.");
            }

            // A payload naming a denied path is rejected WHOLESALE - the good rows in it do
            // not land either. Quietly skipping the money row would be the subtler bug.
            string ok = ExpectedAllowlist[0];
            string compiled = new LocalJsonCatalogSource().Read(ok);
            if (!string.IsNullOrEmpty(compiled))
            {
                RemoteCatalogOverrides.Clear();
                string mixed = BuildPayload(
                    new[] { ok, "Data/Canonical/packs.json" },
                    new[] { MarkedCopy(compiled), "{\"packs\":[]}" });
                if (RemoteCatalogOverrides.ApplyPayload(mixed, RemoteCatalogOverrides.ProvenanceRemote))
                    failures.Add("[money-boundary] a payload containing a DENIED path was ACCEPTED.");
                if (RemoteCatalogOverrides.RowCount != 0)
                    failures.Add("[money-boundary] a payload containing a DENIED path still landed " +
                                 RemoteCatalogOverrides.RowCount + " row(s). It must be rejected " +
                                 "WHOLESALE - the honest rows do not get a pass.");
                RemoteCatalogOverrides.Clear();
            }
        }

        // =====================================================================
        //  Case 5 - THE ALLOWLIST'S SHAPE
        // =====================================================================
        private static void Case5_AllowlistShape(List<string> failures, List<string> notes)
        {
            var allow = RemoteCatalogOverrides.Allowlist;
            if (allow == null)
            {
                failures.Add("[allowlist-shape] Allowlist is NULL.");
                return;
            }
            if (allow.Length != ExpectedAllowlistCount)
                failures.Add("[allowlist-shape] the allowlist holds " + allow.Length + " catalog(s), " +
                             "pinned at " + ExpectedAllowlistCount + ". Widening it is a DATA decision " +
                             "and is fine - it just has to move this literal in the same commit " +
                             "(CLAUDE.md section 15), or nobody can tell a deliberate widening from a " +
                             "drift.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var local = new LocalJsonCatalogSource();
            foreach (string p in allow)
            {
                if (string.IsNullOrWhiteSpace(p)) { failures.Add("[allowlist-shape] empty entry"); continue; }
                if (!seen.Add(p)) failures.Add("[allowlist-shape] duplicate entry '" + p + "'");
                if (!IsAscii(p))
                    failures.Add("[allowlist-shape] '" + p + "' is not ASCII - it is a database key and " +
                                 "a Resources path; both must be plain ASCII");
                if (!p.StartsWith(RemoteCatalogOverrides.RequiredPrefix, StringComparison.Ordinal))
                    failures.Add("[allowlist-shape] '" + p + "' is outside " +
                                 RemoteCatalogOverrides.RequiredPrefix + " - only canonical catalog data " +
                                 "is overridable");
                if (!RemoteCatalogOverrides.IsAllowed(p))
                    failures.Add("[allowlist-shape] IsAllowed('" + p + "') is FALSE for an allowlisted " +
                                 "path - the two disagree");
                if (string.IsNullOrEmpty(local.Read(p)))
                    failures.Add("[allowlist-shape] '" + p + "' has NO compiled copy in this build, so " +
                                 "there is nothing to validate an override against and nothing to fall " +
                                 "back to");
            }

            // The literals in this file vs the code's list.
            var codeSet = new HashSet<string>(allow, StringComparer.Ordinal);
            foreach (string e in ExpectedAllowlist)
                if (!codeSet.Contains(e))
                    failures.Add("[allowlist-shape] this oracle expects '" + e + "' on the allowlist and " +
                                 "the code does not carry it");
            foreach (string c in allow)
                if (Array.IndexOf(ExpectedAllowlist, c) < 0)
                    failures.Add("[allowlist-shape] the code allows '" + c + "' and this oracle does not " +
                                 "know about it - state the widening here too");

            // Nothing outside the allowlist can be served, whatever a payload says.
            if (RemoteCatalogOverrides.IsAllowed("Data/Canonical/abilities.json"))
                failures.Add("[allowlist-shape] a catalog outside the proven five is allowed. The scope " +
                             "is deliberately small until the mechanism is proven.");
            if (RemoteCatalogOverrides.IsAllowed("../../etc/passwd"))
                failures.Add("[allowlist-shape] a path outside the canonical tree is allowed.");
        }

        // =====================================================================
        //  Case 6 - THE FETCH CANNOT BLOCK BOOT
        // =====================================================================
        private static void Case6_NeverBlocks(List<string> failures, List<string> notes)
        {
            string src = ReadRepoText(ServiceSrc);
            if (src == null)
            {
                failures.Add("[never-blocks] could not read " + ServiceSrc);
                return;
            }

            string[] banned = { ".WaitForCompletion(", ".GetAwaiter().GetResult(", "Thread.Sleep(", ".Wait()" };
            foreach (string b in banned)
                if (src.Contains(b))
                    failures.Add("[never-blocks] " + ServiceSrc + " contains '" + b + "'. Catalog loading " +
                                 "runs at boot; a blocking idiom here would be a boot hazard on every " +
                                 "launch, for every player.");

            if (!src.Contains("PollForeverAsync().Forget()"))
                failures.Add("[never-blocks] the boot hook no longer fires and forgets the poll. The " +
                             "non-blocking property is STRUCTURAL (no await at the call site), not a " +
                             "comment.");

            // CanonicalJson.Source is assigned in EXACTLY ONE place, and it is behind the flag.
            int assignments = CountSourceAssignments();
            if (assignments != 1)
                failures.Add("[never-blocks] CanonicalJson.Source is assigned in " + assignments +
                             " place(s) under Assets/_Modules; WO-1331 leaves exactly ONE, inside the " +
                             "flag-gated RemoteCatalogService.Install(). A second assignment would make " +
                             "the flag-off byte-identity claim unprovable by reading.");

            string ov = ReadRepoText(OverridesSrc);
            if (ov != null && !ov.Contains("Guard.Try"))
                failures.Add("[never-blocks] " + OverridesSrc + " no longer Guards its parses. A parse " +
                             "that can throw outward is a catalog that can blank (CLAUDE.md section 12).");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static void AssertAllCompiled(List<string> failures, string caseName, string mode)
        {
            var local = new LocalJsonCatalogSource();
            var wrapped = new RemoteCatalogSource(local);
            foreach (string p in ExpectedAllowlist)
            {
                string direct = local.Read(p);
                string through = wrapped.Read(p);
                if (!string.Equals(direct, through, StringComparison.Ordinal))
                    failures.Add("[" + caseName + "] after '" + mode + "', '" + p + "' did NOT resolve " +
                                 "its COMPILED text (" + (direct == null ? "null" : direct.Length + " chars") +
                                 " expected, " + (through == null ? "null" : through.Length + " chars") +
                                 " served). NO ROW / NO PARSE => TODAY'S BEHAVIOUR, byte for byte.");
                if (string.IsNullOrEmpty(through))
                    failures.Add("[" + caseName + "] after '" + mode + "', '" + p + "' resolved EMPTY. A " +
                                 "blank catalog is the outcome this seam exists to make impossible.");
            }
        }

        /// <summary>A valid override text: the compiled catalog with one extra top-level key, so
        /// it passes validation and is still distinguishable from the compiled copy.</summary>
        private static string MarkedCopy(string compiled)
        {
            var o = JObject.Parse(compiled);
            o["_wo1331OracleMarker"] = "remote";
            return o.ToString(Formatting.None);
        }

        /// <summary>The compiled catalog with its FIRST top-level key removed - a well-formed
        /// JSON body that is nonetheless half a catalog.</summary>
        private static string StrippedFirstKey(string compiled)
        {
            var o = JObject.Parse(compiled);
            string first = null;
            foreach (var prop in o.Properties()) { first = prop.Name; break; }
            if (first != null) o.Remove(first);
            return o.ToString(Formatting.None);
        }

        private static string BuildPayload(string path, string body)
        {
            return BuildPayload(new[] { path }, new[] { body });
        }

        private static string BuildPayload(string[] paths, string[] bodies)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < paths.Length; i++) map[paths[i]] = bodies[i];
            var dto = new JObject
            {
                ["version"] = RemoteCatalogOverrides.PayloadVersion,
                ["readOk"] = true,
                ["reason"] = "oracle",
                ["catalogs"] = JObject.FromObject(map),
            };
            return dto.ToString(Formatting.None);
        }

        private static int CountSourceAssignments()
        {
            int n = 0;
            string root = Path.Combine(Application.dataPath, "_Modules");
            if (!Directory.Exists(root)) return -1;
            foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(f); } catch { continue; }
                int idx = 0;
                while ((idx = text.IndexOf("CanonicalJson.Source =", idx, StringComparison.Ordinal)) >= 0)
                {
                    // Skip the doc-comment references that predate the connection.
                    int lineStart = text.LastIndexOf('\n', Math.Max(0, idx - 1)) + 1;
                    string line = text.Substring(lineStart, idx - lineStart).TrimStart();
                    bool isComment = line.StartsWith("//", StringComparison.Ordinal) ||
                                     line.StartsWith("///", StringComparison.Ordinal) ||
                                     line.StartsWith("*", StringComparison.Ordinal) ||
                                     line.Contains("<c>");
                    if (!isComment) n++;
                    idx += 22;
                }
            }
            return n;
        }

        private static string ReadRepoText(string relative)
        {
            try
            {
                string full = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", relative);
                return File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8) : null;
            }
            catch { return null; }
        }

        private static bool IsAscii(string s)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] > 127) return false;
            return true;
        }
    }
}
