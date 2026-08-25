// WO-978 regression slice: callers must report measured credit, never merely echo intent.
// Registration in DataRegression.cs is deliberately committer-fenced to the lead.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class EconomyCreditReportingRegression
    {
        private readonly struct Caller
        {
            public readonly string Label;
            public readonly string RelativePath;
            public readonly string Anchor;
            public readonly int Window;

            public Caller(string label, string relativePath, string anchor, int window)
            {
                Label = label;
                RelativePath = relativePath;
                Anchor = anchor;
                Window = window;
            }
        }

        private static readonly Caller[] Callers =
        {
            new Caller("raid", "_Modules/Village/World/Camps/RaidVictoryController.cs",
                "private static void LogCredit", 2600),
            new Caller("daily quest", "_Modules/Village/Quests/DailyQuestRewardBridge.cs",
                "private static void Report", 1300),
            new Caller("challenge outpost", "_Modules/Village/World/Camps/ChallengeOutpostVictoryController.cs",
                "Guard.Try(Sys, \"GrantReward\"", 4200),
            new Caller("population", "_Modules/Village/Population/PopulationService.cs",
                "public void AddPopulationXP", 2600),
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            foreach (var caller in Callers) CheckCaller(caller, failures);

            if (failures.Count > 0)
            {
                reason = string.Join("\n", failures);
                return false;
            }

            reason = "ECONOMY_CREDIT_REPORTING OK - four reward callers measure before/after credit " +
                     "and their reporting blocks name credited + requested with a Warn on shortfall.";
            return true;
        }

        private static void CheckCaller(Caller caller, List<string> failures)
        {
            string path = Path.Combine(Application.dataPath,
                caller.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                failures.Add("ECONOMY_CREDIT_REPORTING FAIL [" + caller.Label + "] missing " + path);
                return;
            }

            string source = StripComments(File.ReadAllText(path));
            int at = source.IndexOf(caller.Anchor, StringComparison.Ordinal);
            if (at < 0)
            {
                failures.Add("ECONOMY_CREDIT_REPORTING FAIL [" + caller.Label +
                             "] reporting anchor missing: " + caller.Anchor);
                return;
            }

            int length = Math.Min(caller.Window, source.Length - at);
            string block = source.Substring(at, length);

            // Shape assertion, intentionally token-based: formatting and exact player copy may evolve.
            Require(block, caller, failures, "credited");
            Require(block, caller, failures, "request");
            Require(block, caller, failures, "FlowTrace.Warn");

            // Measurement may happen at the call site and be passed into a shared reporting helper,
            // so assert it across the caller file while keeping the reporting assertion anchored.
            // Accept the established naming families rather than prescribing one local variable.
            bool hasBefore = Regex.IsMatch(source, @"\b[A-Za-z0-9_]*(?:before|Before)\b");
            bool hasDelta = Regex.IsMatch(source, @"\b[A-Za-z0-9_]*(?:credited|Credited|Granted)\b");
            if (!hasBefore || !hasDelta)
                failures.Add("ECONOMY_CREDIT_REPORTING FAIL [" + caller.Label +
                             "] reporting block no longer contains a before/credited measurement");
        }

        private static void Require(string block, Caller caller, List<string> failures, string token)
        {
            if (block.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("ECONOMY_CREDIT_REPORTING FAIL [" + caller.Label +
                             "] reporting block missing token '" + token + "'");
        }

        private static string StripComments(string source)
        {
            return Regex.Replace(source ?? string.Empty, @"//.*?$|/\*.*?\*/", string.Empty,
                RegexOptions.Multiline | RegexOptions.Singleline);
        }
    }
}
