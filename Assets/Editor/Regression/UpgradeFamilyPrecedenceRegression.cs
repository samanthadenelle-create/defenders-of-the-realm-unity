// =============================================================================
// UpgradeFamilyPrecedenceRegression [upgrade-family] -- pins THE INVARIANT that the
// START side and the COMPLETE side of a building upgrade resolve the SAME family
// ladder for a DUAL-FAMILY building (farm / lumbermill / forge, which live in BOTH
// building-tiers.json and ResourceBuildingProgression).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
//
// THE DEFECT THIS EXISTS TO PREVENT (owner F8, 2026-08-15):
//   "when i upgrade the lumbermill, on complete with crystals doesnt seem to trigger
//    lumbermill level up. Seems to dead end."
//   BuildingUpgradeVM (START) resolved "city tiers win; else legacy".
//   CompletedUpgradeApplier (COMPLETE) checked IsResourceBuilding FIRST -- the
//   OPPOSITE order. A lumbermill upgrade was therefore STARTED on the city ladder and
//   APPLIED to the resource ladder: BuildingUpgradeService.ApplyTier (the ONLY writer of
//   GameState.BuildingTiers + Save + ModifierService.Recompute + ApplyStructureHp) never
//   ran, and the trace still printed a success line. The player paid and the tier panel
//   never moved.
//
// THE ASSERTION IS THE INVARIANT, NOT "lumbermill works":
//   1. RULE      -- UpgradeFamilyResolver.Resolve(id) == City for every dual-family id.
//   2. ONE RULE  -- the START site (BuildingUpgradeVM.cs), the COMPLETE site
//                   (CompletedUpgradeApplier.cs) and the dialogue site
//                   (DialogueCommandSink.cs) all call UpgradeFamilyResolver.Resolve, and
//                   NONE of them hand-derives the family from IsUpgradable /
//                   IsResourceBuilding. Two hand-written orders in two files IS the bug.
//                   Matching runs on source with comments AND string literals STRIPPED,
//                   so a doc-comment naming the old call can never green (or red) this.
//   3. BEHAVIOUR -- CompletedUpgradeApplier.Apply(job) for a dual-family id advances
//                   GameState.BuildingTiers (the authoritative store) and leaves the
//                   legacy PlayerPrefs resource level UNTOUCHED.
//
// Marker: UPGRADE_FAMILY_OK / UPGRADE_FAMILY_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!UpgradeFamilyPrecedenceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[upgrade-family] " + r);
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Village.Buildings.Progression;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class UpgradeFamilyPrecedenceRegression
    {
        private const string SaveKey = "dotr-save";
        private const string LegacyPrefPrefix = "dotr.resbuilding.level.";

        // The ids that sit in BOTH ladders today. Held here (not derived) so that if one is
        // REMOVED from a catalog the premise check says so out loud instead of going vacuous.
        private static readonly string[] DualFamilyIds = { "farm", "lumbermill", "forge" };

        // The sites that must all read the ONE resolver. Paths are relative to Assets/.
        private static readonly string[] ResolverSites =
        {
            "_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs",        // START
            "_Modules/Village/Buildings/Progression/CompletedUpgradeApplier.cs", // COMPLETE
            "_Modules/Village/Tutorial/DialogueCommandSink.cs",                  // dialogue verb
        };

        /// <summary>Named stand-downs recorded by the void sections during one Run.</summary>
        private static readonly List<string> s_partialSkips = new List<string>();

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- UPGRADE FAMILY PRECEDENCE (start side and complete side resolve ONE ladder) ---");

            // Sections that stand down (the completion-behaviour case needs a reflectable
            // GameStateService seam) record a NAMED partial skip here, so an "OK" from this
            // suite can never silently mean "the completion side never ran".
            s_partialSkips.Clear();

            CheckRule(failures, log);
            CheckOneRuleSource(failures, log);
            CheckCompletionBehaviour(failures, log);

            reason = Finish(failures, log);
            if (s_partialSkips.Count > 0)
                reason += " -- " + string.Join("; ", s_partialSkips.ToArray());
            return failures.Count == 0;
        }

        // ── 1. RULE: city wins for every dual-family id ───────────────────────────
        private static void CheckRule(List<string> failures, StringBuilder log)
        {
            log.AppendLine("  [rule] UpgradeFamilyResolver.Resolve -> expected City for dual-family ids");
            foreach (var id in DualFamilyIds)
            {
                bool inCity = BuildingTierCatalog.IsUpgradable(id);
                bool inResource = ResourceBuildingProgression.IsResourceBuilding(id);
                var family = UpgradeFamilyResolver.Resolve(id);
                log.AppendLine($"    '{id}': cityCatalog={inCity} resourceCatalog={inResource} -> {family}");

                if (!inCity || !inResource)
                {
                    failures.Add($"[upgrade-family] '{id}' is no longer dual-family (cityCatalog={inCity}, resourceCatalog={inResource}) -- the precedence premise changed; re-confirm the ladders before editing this oracle");
                    continue;
                }
                if (!UpgradeFamilyResolver.IsDualFamily(id))
                    failures.Add($"[upgrade-family] UpgradeFamilyResolver.IsDualFamily('{id}') is false while both catalogs contain it -- the completion trace can no longer flag the overlap");
                if (family != UpgradeFamily.City)
                    failures.Add($"[upgrade-family] UpgradeFamilyResolver.Resolve('{id}') = {family}, expected City -- 'city tiers win; else legacy' is the rule BOTH sides must follow (a Resource answer here re-opens the paid-but-dead-end upgrade)");
            }
        }

        // ── 2. ONE RULE: every site calls the shared resolver, none hand-derives ──
        private static void CheckOneRuleSource(List<string> failures, StringBuilder log)
        {
            log.AppendLine("  [one-rule] every family-resolving site calls UpgradeFamilyResolver.Resolve (comments + string literals stripped)");
            foreach (var rel in ResolverSites)
            {
                string path = Path.Combine(Application.dataPath, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    failures.Add($"[upgrade-family] source site 'Assets/{rel}' not found -- the oracle cannot pin the shared-resolver invariant (file moved? update ResolverSites)");
                    continue;
                }

                string code = StripCommentsAndStrings(File.ReadAllText(path));
                bool callsResolver = code.Contains("UpgradeFamilyResolver.Resolve(");
                bool handDerivesCity = code.Contains("BuildingTierCatalog.IsUpgradable(");
                bool handDerivesResource = code.Contains("ResourceBuildingProgression.IsResourceBuilding(");
                log.AppendLine($"    Assets/{rel}: resolver={callsResolver} rawIsUpgradable={handDerivesCity} rawIsResourceBuilding={handDerivesResource}");

                if (!callsResolver)
                    failures.Add($"[upgrade-family] Assets/{rel} does not call UpgradeFamilyResolver.Resolve( -- it resolves the upgrade family on its own, which is exactly how the START and COMPLETE sides drifted into opposite precedence orders");
                if (handDerivesCity || handDerivesResource)
                    failures.Add($"[upgrade-family] Assets/{rel} hand-derives the upgrade family (BuildingTierCatalog.IsUpgradable={handDerivesCity}, ResourceBuildingProgression.IsResourceBuilding={handDerivesResource}) -- route it through UpgradeFamilyResolver so there is ONE precedence order in the codebase");
            }
        }

        // ── 3. BEHAVIOUR: completion lands on the city ladder, not the legacy pool ─
        private static void CheckCompletionBehaviour(List<string> failures, StringBuilder log)
        {
            log.AppendLine("  [behaviour] CompletedUpgradeApplier.Apply -> GameState.BuildingTiers, legacy PlayerPrefs untouched");

            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            var hadLegacy = new Dictionary<string, bool>();
            var legacyBefore = new Dictionary<string, int>();
            foreach (var id in DualFamilyIds)
            {
                hadLegacy[id] = PlayerPrefs.HasKey(LegacyPrefPrefix + id);
                legacyBefore[id] = PlayerPrefs.GetInt(LegacyPrefPrefix + id, int.MinValue);
            }

            GameStateService priorGss = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (upgrade-family oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    // Bare `return;` from a void section used to be invisible twice over:
                    // it evaded the hollow-pass ratchet (no `return true` to see) AND it
                    // left the suite reporting an unqualified OK. Now the stand-down is
                    // named and rides out in the reason string.
                    string note = DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                        "completion-behaviour section",
                        "GameStateService state seam not reflectable (needs fleet) -- rule + one-rule checks still ran");
                    log.AppendLine("    " + note);
                    s_partialSkips.Add(note);
                    return;
                }
                throwaway.VillageTier = 99;

                foreach (var id in DualFamilyIds)
                {
                    if (!UpgradeFamilyResolver.IsDualFamily(id)) continue;   // premise already failed above

                    int target = 2;
                    if (BuildingTierCatalog.TierOf(id, target) == null)
                    {
                        failures.Add($"[upgrade-family] building-tiers.json has no tier {target} for '{id}' -- the city ladder cannot advance it (catalog gap)");
                        continue;
                    }

                    // The completion seam takes the job VERBATIM -- costs were charged at commit.
                    var job = new BuildJobData { StructureId = id, TargetTier = target };
                    CompletedUpgradeApplier.Apply(job);

                    int tierNow = throwaway.BuildingTiers != null && throwaway.BuildingTiers.TryGetValue(id, out var t) ? t : -1;
                    int legacyAfter = PlayerPrefs.GetInt(LegacyPrefPrefix + id, int.MinValue);
                    log.AppendLine($"    '{id}': BuildingTiers -> {tierNow} (target {target}), legacy pref {legacyBefore[id]} -> {legacyAfter}");

                    if (tierNow != target)
                        failures.Add($"[upgrade-family] a completed upgrade of dual-family '{id}' left GameState.BuildingTiers['{id}']={tierNow}, expected {target} -- the COMPLETE side did NOT land on the city ladder the START side charged for (the paid dead-end defect)");
                    if (legacyAfter != legacyBefore[id])
                        failures.Add($"[upgrade-family] a completed upgrade of dual-family '{id}' wrote the legacy '{LegacyPrefPrefix}{id}' PlayerPrefs ({legacyBefore[id]} -> {legacyAfter}) -- the city ladder is the authority for an overlapping id; writing the resource pool is the mis-route");
                }
            }
            catch (System.Exception ex)
            {
                failures.Add($"upgrade-family oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);
                SetGssInstance(priorGss);
                foreach (var id in DualFamilyIds)
                {
                    if (hadLegacy[id]) PlayerPrefs.SetInt(LegacyPrefPrefix + id, legacyBefore[id]);
                    else PlayerPrefs.DeleteKey(LegacyPrefPrefix + id);
                }
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        // ── Source hygiene: strip comments AND string/char literals ──────────────
        // Three oracles gave FALSE POSITIVES by matching their own prose, so nothing this
        // suite greps may come from a comment, a doc-comment or a literal.
        private static string StripCommentsAndStrings(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];

                // line comment
                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n') i++;
                    continue;
                }
                // block comment
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i = i + 2 <= n ? i + 2 : n;
                    sb.Append(' ');
                    continue;
                }
                // verbatim string @"..."  ("" is an escaped quote)
                if (c == '@' && i + 1 < n && src[i + 1] == '"')
                {
                    i += 2;
                    while (i < n)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < n && src[i + 1] == '"') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    sb.Append(" \"\" ");
                    continue;
                }
                // regular string "..." (interpolated $"..." strips the same way)
                if (c == '"')
                {
                    i++;
                    while (i < n)
                    {
                        if (src[i] == '\\') { i += 2; continue; }
                        if (src[i] == '"') { i++; break; }
                        i++;
                    }
                    sb.Append(" \"\" ");
                    continue;
                }
                // char literal '.'
                if (c == '\'')
                {
                    i++;
                    while (i < n)
                    {
                        if (src[i] == '\\') { i += 2; continue; }
                        if (src[i] == '\'') { i++; break; }
                        i++;
                    }
                    sb.Append(" '' ");
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "UPGRADE_FAMILY_OK");
                return "UPGRADE FAMILY OK -- start and complete sides resolve ONE ladder (city wins) for farm/lumbermill/forge";
            }
            string reason = "upgrade-family: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "UPGRADE_FAMILY_FAIL: " + reason);
            return reason;
        }
    }
}
