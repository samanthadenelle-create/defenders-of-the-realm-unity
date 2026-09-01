// =============================================================================
// FoundingReachabilityRegression [founding-reach] -- proves the founding choice is
// actually REACHABLE on a fresh save and correctly suppressed for a returning player.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. FoundingChoiceController lives in
// DeNelle.Onboarding (not referenced by this asmdef), so ShouldOffer is driven by
// reflection over a throwaway GameState:
//   * fresh state (Onboarded=false, empty BaseLayout) -> ShouldOffer == true, AND
//   * post-onboard state (Onboarded=true)            -> ShouldOffer == false.
// Plus a source-scan that the reachability wiring is present: the HeroSelect bypass
// path AND PetSelect both reference PresentOrContinue (the founding entry point).
//
// Marker: FOUNDING_REACH_OK / FOUNDING_REACH_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!FoundingReachabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[founding-reach] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class FoundingReachabilityRegression
    {
        private const string SaveKey = "dotr-save";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- FOUNDING REACHABILITY (ShouldOffer fresh=true / post-onboard=false + PresentOrContinue wiring) ---");

            var fccType = FindType("DeNelle.Onboarding.FoundingChoiceController");
            if (fccType == null)
            {
                failures.Add("FoundingChoiceController type not found (DeNelle.Onboarding not compiled?)");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }
            var shouldOffer = fccType.GetProperty("ShouldOffer", BindingFlags.Public | BindingFlags.Static);
            if (shouldOffer == null)
            {
                failures.Add("FoundingChoiceController.ShouldOffer static property not found (reachability seam renamed)");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }
            var decidedLatch = fccType.GetField("_decidedThisSession", BindingFlags.NonPublic | BindingFlags.Static);

            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            object priorLatch = decidedLatch != null ? decidedLatch.GetValue(null) : null;
            GameStateService priorGss = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            try
            {
                // Clear the once-per-session latch so ShouldOffer reflects state, not a prior present.
                if (decidedLatch != null) decidedLatch.SetValue(null, false);

                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (founding-reach oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "FOUNDING REACH", "GameStateService state seam not reflectable (needs fleet)");
                }

                // FRESH: not onboarded, nothing founded -> offer the founding choice.
                throwaway.Onboarded = false;
                if (throwaway.BaseLayout != null) throwaway.BaseLayout.Clear();
                if (decidedLatch != null) decidedLatch.SetValue(null, false);
                bool fresh = (bool)shouldOffer.GetValue(null);
                log.AppendLine($"  fresh state -> ShouldOffer={fresh} (want true)");
                if (!fresh)
                    failures.Add("[founding-reach] ShouldOffer is FALSE on a fresh save (Onboarded=false, empty BaseLayout) -- the founding choice is unreachable for a new player");

                // POST-ONBOARD: returning player -> suppress.
                throwaway.Onboarded = true;
                if (decidedLatch != null) decidedLatch.SetValue(null, false);
                bool post = (bool)shouldOffer.GetValue(null);
                log.AppendLine($"  post-onboard state -> ShouldOffer={post} (want false)");
                if (post)
                    failures.Add("[founding-reach] ShouldOffer is TRUE after Onboarded=true -- a returning player would be re-offered the founding choice");
            }
            catch (Exception ex)
            {
                failures.Add($"founding-reach oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetGssInstance(priorGss);
                if (decidedLatch != null && priorLatch != null) decidedLatch.SetValue(null, priorLatch);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            // Source-scan: the reachability wiring calls PresentOrContinue.
            CheckReferences("Onboarding/HeroSelectController.cs", "HeroSelect bypass", failures, log);
            CheckReferences("Onboarding/PetSelectController.cs", "PetSelect", failures, log);
            CheckRecommendedDefault(failures, log);

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static void CheckRecommendedDefault(List<string> failures, StringBuilder log)
        {
            string flagsPath = Path.Combine(Application.dataPath, "_Modules/Core/FeatureFlags.cs");
            string choicePath = Path.Combine(Application.dataPath, "_Modules/Onboarding/FoundingChoiceController.cs");
            string flags = File.Exists(flagsPath) ? File.ReadAllText(flagsPath) : "";
            string choice = File.Exists(choicePath) ? File.ReadAllText(choicePath) : "";
            if (!flags.Contains("Get(\"defaulttown\", defaultOn: true)"))
                failures.Add("[founding-reach] Default Town is not default-on -- fresh players still fall into blank founding");
            if (!choice.Contains("READY SETTLEMENT  (Recommended)") || !choice.Contains("OnDefaultTown"))
                failures.Add("[founding-reach] recommended starter-settlement CTA is absent");
            if (!choice.Contains("EMPTY REALM  (Build It Yourself)") || !choice.Contains("OnBuildYourOwn"))
                failures.Add("[founding-reach] blank-canvas secondary path is no longer exposed");
            if (!choice.Contains("BodyFillHorizontalOverscan") ||
                !choice.Contains("new Vector2(-BodyFillHorizontalOverscan, 0f)") ||
                !choice.Contains("new Vector2(1f + BodyFillHorizontalOverscan, 1f)"))
                failures.Add("[founding-reach] founding modal body fill no longer overscans beneath both frame shoulders; the previous screen can bleed through at the sides");
            string completionPath = Path.Combine(Application.dataPath, "_Modules/Village/BuildMode/StarterSettlementCompletion.cs");
            string layoutRes = Path.Combine(Application.dataPath, "Resources/Data/Canonical/starter-settlement-layout.json");
            string layoutStream = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/starter-settlement-layout.json");
            string completion = File.Exists(completionPath) ? File.ReadAllText(completionPath) : "";
            if (!completion.Contains("CanonicalJson.Read(LayoutRelativePath)") ||
                !completion.Contains("CatalogRegistry.Get(item.id)"))
                failures.Add("[founding-reach] ready-settlement layout no longer reads canonical ids and resolves their current catalog art");
            if (completion.Contains("private static readonly Entry[] Template"))
                failures.Add("[founding-reach] starter transforms returned to a hardcoded C# table instead of canonical data");
            if (!File.Exists(layoutRes) || !File.Exists(layoutStream))
                failures.Add("[founding-reach] starter-settlement-layout.json dual copies are missing");
            else if (!StructuralComparisons.StructuralEqualityComparer.Equals(
                         File.ReadAllBytes(layoutRes), File.ReadAllBytes(layoutStream)))
                failures.Add("[founding-reach] starter-settlement-layout.json Resources/StreamingAssets copies diverged");
            string structuresRes = Path.Combine(Application.dataPath, "Resources/Data/Canonical/structures-catalog.json");
            string structures = File.Exists(structuresRes) ? File.ReadAllText(structuresRes) : "";
            if (!structures.Contains("Structures/Tower_Castle_Round") ||
                !structures.Contains("Structures/Tower_Castle_Square") ||
                !structures.Contains("Structures/Tower_Medieval_Big"))
                failures.Add("[founding-reach] Archer Tower no longer uses the owner-approved legacy stone tower family");
            log.AppendLine("  recommended starter settlement default + scratch secondary path checked");
        }

        private static void CheckReferences(string rel, string label, List<string> failures, StringBuilder log)
        {
            string path = Path.Combine(Application.dataPath, "_Modules", rel);
            if (!File.Exists(path)) { failures.Add($"[founding-reach] {label} source '{rel}' not found"); return; }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex) { failures.Add($"[founding-reach] {label} source unreadable ({ex.Message})"); return; }
            if (text.IndexOf("PresentOrContinue", StringComparison.Ordinal) < 0)
                failures.Add($"[founding-reach] {label} ('{rel}') does NOT reference PresentOrContinue -- the founding entry point is not wired into this route");
            else
                log.AppendLine($"  {label} references PresentOrContinue OK");
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
                Debug.Log(log.ToString() + "FOUNDING_REACH_OK");
                return "FOUNDING REACH OK -- ShouldOffer true on a fresh save, false post-onboard, and HeroSelect+PetSelect wire PresentOrContinue";
            }
            string reason = "founding-reach: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "FOUNDING_REACH_FAIL: " + reason);
            return reason;
        }
    }
}
