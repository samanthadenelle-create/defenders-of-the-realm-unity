// =============================================================================
// CollectorTellRegression — WO-900: the collector "I am full" tell, both halves.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Markers: COLLECTOR_TELL_OK / COLLECTOR_TELL_FAIL
//
// THE DEFECT THIS EXISTS FOR: CollectorStackView is a complete 437-line CoC tell — the
// pile, the near-full band, the "N/20", the "!", the glint, the toast — that sat in the
// tree with ZERO CALLERS. A collector filling up showed the player NOTHING; Accrue clamped
// silently and the wallet number just stopped moving. §3 wired the diegetic half; §4 adds
// the ambient half so the same fact is readable from anywhere in town with no modal open.
//
// Both halves are pinned here so neither can silently die again — a built-but-uncalled
// view is the exact failure mode, and it is invisible to a compile gate.
//
// ⚠ COPY LAW (§4): "Storage" / "Bank" / current-max is the WALLET's word (WO-857).
// This surface says "Collectors". The player must never meet two notions of "full".
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class CollectorTellRegression
    {
        private const string FactorySrc = "Assets/_Modules/Village/Catalog/StructureFactory.cs";
        private const string ViewSrc = "Assets/_Modules/HUD/Kit/HudKitController.cs";
        private const string GateSrc = "Assets/_Modules/Core/UI/CollectorStatusGate.cs";
        private const string PublisherSrc = "Assets/_Modules/Village/Buildings/Progression/CollectorStatusPublisher.cs";
        private const string BootstrapSrc = "Assets/_Modules/Village/Buildings/Progression/ResourceCollectorBootstrap.cs";
        private const string AreasSrc = "Assets/Resources/Data/Canonical/hud-areas.json";
        private const string AreasStreamSrc = "Assets/StreamingAssets/Data/Canonical/hud-areas.json";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("COLLECTOR_TELL_OK - " + reason);
            else Debug.LogError("COLLECTOR_TELL_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "diegetic-wired", () => Case1_DiegeticWired(failures, notes));
                Case(failures, "ambient-chip", () => Case2_AmbientChip(failures, notes));
                Case(failures, "occupancy-row", () => Case3_OccupancyRow(failures, notes));
                Case(failures, "publisher-wired", () => Case4_PublisherWired(failures, notes));
                Case(failures, "copy-law", () => Case5_CopyLaw(failures, notes));
                Case(failures, "no-reflection", () => Case6_NoReflection(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "COLLECTOR TELL OK - the diegetic view is attached at placement, the ambient " +
                         "chip is built + occupied + published to, and the copy never says 'Storage'" + noteStr;
                return true;
            }
            reason = "collector-tell FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - §3: the diegetic view has a CALLER (the original defect)
        // =====================================================================
        private static void Case1_DiegeticWired(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(FactorySrc);
            if (src == null) { failures.Add("[diegetic-wired] missing source " + FactorySrc); return; }
            if (src.IndexOf("CollectorStackView.Attach", StringComparison.Ordinal) < 0)
                failures.Add("[diegetic-wired] StructureFactory no longer calls CollectorStackView.Attach - " +
                             "a placed collector would fill up and show the player nothing at all, which " +
                             "is exactly the state WO-900 was opened for");
            notes.Add("StructureFactory attaches the collector stack view");
        }

        // =====================================================================
        //  CASE 2 - §4: the AMBIENT chip is built, labelled and polled
        // =====================================================================
        private static void Case2_AmbientChip(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(ViewSrc);
            if (src == null) { failures.Add("[ambient-chip] missing source " + ViewSrc); return; }

            foreach (var token in new[] { "BuildCollectorsChip", "FormatCollectorChip",
                                          "collectorsChip", "CollectorStatusGate.Status" })
                if (src.IndexOf(token, StringComparison.Ordinal) < 0)
                    failures.Add("[ambient-chip] HudKitController no longer references '" + token +
                                 "' - the ambient tell is gone and the only remaining signal that a " +
                                 "collector stopped earning is the wallet number failing to move");

            // The tap must reuse the EXISTING collect command, never mint a second one.
            if (src.IndexOf("CollectorStatusGate.RequestCollectAll", StringComparison.Ordinal) < 0)
                failures.Add("[ambient-chip] the chip does not route its tap through the gate");

            // Behavioural: the gate is honest before anyone publishes.
            var blank = new CollectorStatusGate.CollectorStatus();
            if (blank.Available)
                failures.Add("[ambient-chip] a default CollectorStatus claims to be Available");

            int before = CollectorStatusGate.Status.Version;
            CollectorStatusGate.PublishStatus(new CollectorStatusGate.CollectorStatus
            { Available = true, FullCount = 2, TotalCount = 3, MaxFillPct = 100, TotalPending = 40 });
            var after = CollectorStatusGate.Status;
            if (after.Version == before)
                failures.Add("[ambient-chip] PublishStatus did not bump Version - the HUD repaints off " +
                             "that number, so the chip would freeze on its first reading");
            if (after.FullCount != 2 || after.TotalCount != 3)
                failures.Add("[ambient-chip] the published snapshot did not round-trip");

            // Never throw on a tap with no Village listener (a boot race is not a crash).
            CollectorStatusGate.RequestCollectAll();
            notes.Add("ambient chip built, gate publishes + bumps version, tap is null-safe");
        }

        // =====================================================================
        //  CASE 3 - the chip has an occupancy row, or it never renders
        // =====================================================================
        private static void Case3_OccupancyRow(List<string> failures, List<string> notes)
        {
            foreach (var p in new[] { AreasSrc, AreasStreamSrc })
            {
                string j = ReadSrc(p);
                if (j == null) { failures.Add("[occupancy-row] missing " + p); continue; }
                if (j.IndexOf("collectorsChip", StringComparison.Ordinal) < 0)
                    failures.Add("[occupancy-row] hud-areas.json has no collectorsChip row (" + p +
                                 ") - a registered widget with no occupancy row is switched OFF in " +
                                 "every posture, so the chip would exist and never be seen");
                // The retired Builders chip's own row must survive untouched (its oracle asserts it).
                if (j.IndexOf("queueStatusChip", StringComparison.Ordinal) < 0)
                    failures.Add("[occupancy-row] the queueStatusChip row was removed from " + p);
            }
            notes.Add("collectorsChip occupies the queueStatus band in both canonical copies");
        }

        // =====================================================================
        //  CASE 4 - the Village publisher is installed on the DDOL host
        // =====================================================================
        private static void Case4_PublisherWired(List<string> failures, List<string> notes)
        {
            string boot = ReadSrc(BootstrapSrc);
            if (boot == null) { failures.Add("[publisher-wired] missing source " + BootstrapSrc); return; }
            if (boot.IndexOf("CollectorStatusPublisher", StringComparison.Ordinal) < 0)
                failures.Add("[publisher-wired] ResourceCollectorBootstrap no longer installs " +
                             "CollectorStatusPublisher - the gate would never be published to and the " +
                             "chip would sit on its bare word forever");

            string pub = ReadSrc(PublisherSrc);
            if (pub == null) { failures.Add("[publisher-wired] missing source " + PublisherSrc); return; }
            if (pub.IndexOf("ResourceCollectorService.CollectAll", StringComparison.Ordinal) < 0)
                failures.Add("[publisher-wired] the publisher does not answer the tap with the EXISTING " +
                             "CollectAll - a second collect command must never be minted");
            if (pub.IndexOf("FlowTrace.Warn", StringComparison.Ordinal) >= 0)
                failures.Add("[publisher-wired] the publisher logs at WARN severity - a full collector " +
                             "is a NORMAL player state, and warning on it would bury real F8 signals");
            notes.Add("publisher installed on the DDOL host, answers with the existing CollectAll");
        }

        // =====================================================================
        //  CASE 5 - the two-"full"s problem: this surface never says "Storage"
        // =====================================================================
        private static void Case5_CopyLaw(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(ViewSrc);
            if (src == null) { failures.Add("[copy-law] missing source " + ViewSrc); return; }

            string region = Region(src, "private static string FormatCollectorChip", "private Button BuildRailChip");
            if (region == null)
            {
                failures.Add("[copy-law] could not locate FormatCollectorChip - the chip's copy is " +
                             "unpinned and 'Storage' could drift back in");
                return;
            }
            if (region.IndexOf("Storage", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("[copy-law] the collector chip copy says 'Storage' - that word belongs to " +
                             "the WALLET (WO-857). Two different notions of 'full' on one screen is the " +
                             "exact confusion the copy law exists to prevent");
            if (region.IndexOf("Collectors ", StringComparison.Ordinal) < 0)
                failures.Add("[copy-law] the chip no longer says 'Collectors'");
            if (region.IndexOf("full", StringComparison.Ordinal) < 0)
                failures.Add("[copy-law] the chip no longer states FULLNESS in words - the owner is " +
                             "red/green colourblind; state is text-encoded here, never colour");
            notes.Add("chip copy: 'Collectors N/M full', never 'Storage'");
        }

        // =====================================================================
        //  CASE 6 - no new reflection bridge (§7: nothing joins the static_gate allowlist)
        // =====================================================================
        private static void Case6_NoReflection(List<string> failures, List<string> notes)
        {
            foreach (var p in new[] { GateSrc, PublisherSrc })
            {
                string src = ReadSrc(p);
                if (src == null) { failures.Add("[no-reflection] missing source " + p); continue; }
                if (src.IndexOf("System.Reflection", StringComparison.Ordinal) >= 0 ||
                    src.IndexOf("GetType().GetMethod", StringComparison.Ordinal) >= 0)
                    failures.Add("[no-reflection] " + p + " introduces reflection - the whole point of " +
                                 "routing through a Core gate is that no bridge is needed");
            }
            notes.Add("no reflection added by the ambient tell");
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        private static string ReadSrc(string relPath)
        {
            try { return File.Exists(relPath) ? File.ReadAllText(relPath) : null; }
            catch { return null; }
        }

        private static string Region(string src, string startToken, string endToken)
        {
            int a = src.IndexOf(startToken, StringComparison.Ordinal);
            if (a < 0) return null;
            int b = src.IndexOf(endToken, a, StringComparison.Ordinal);
            return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
        }
    }
}
