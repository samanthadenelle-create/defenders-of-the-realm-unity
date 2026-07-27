// =============================================================================
// OverworldCombatGateRegression (WO-771) — proves the two gates that stop the
// unwanted / DEPRECATED overworld combat stay in place.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village),
// so it can drive the REAL FeatureFlags read. The behavioural no-ops themselves
// (FindObjectsByType / scene population) need play mode, so — exactly like the
// WO-777 dungeon-entry oracle in SceneRoutingRegression — the STATIC preconditions
// are proven from the actual .cs on disk (the shipped source), which is fully
// data-decidable in seconds with no scene drive.
//
// WHAT IT PROVES:
//   FIX A (ff.raidwalk OFF — walk-up outpost retired):
//     • CavePortalRepointInjector.RepointCavePortals early-returns on
//       !FeatureFlags.RaidContinuousWalk and instead NEUTRALIZES the baked seam
//       (disables the SceneTransitionTrigger component + its collider — no scene edit).
//     • ChallengeOutpostVictoryController.TryInstall early-returns on the same gate.
//   FIX B (ff.regionroam OFF by default — ambient roamers peaceful):
//     • FeatureFlags.RegionRoam exists, reads "regionroam", and DEFAULTS OFF (driven
//       under both PlayerPrefs states — the data-decidable half).
//     • RegionMobSpawner gates BOTH its RuntimeInitializeOnLoad bootstrap and its
//       Update on !FeatureFlags.RegionRoam, so with the flag off it spawns nothing.
//
// Contract mirrors SceneRoutingRegression.Run(out string reason):
//   true  = pass  (reason = one-line summary)
//   false = fail  (reason = exact broken gate)
// Orchestrator (DataRegression.RunAll) registers it covenant-style:
//   if (!OverworldCombatGateRegression.Run(out var owCombatReason)) failures.Add(owCombatReason); else log.AppendLine("[overworld-combat-gate] " + owCombatReason);
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor
{
    public static class OverworldCombatGateRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- OVERWORLD COMBAT GATE (FIX A: walk-up outpost retired / FIX B: ambient roam off) ---");

            string assetsRoot = Application.dataPath; // ".../Assets"

            // ── FIX B (behavioural half): ff.regionroam defaults OFF and honours prefs. ──
            int priorRoam = PlayerPrefs.GetInt("ff.regionroam", -1);
            try
            {
                PlayerPrefs.DeleteKey("ff.regionroam");
                if (FeatureFlags.RegionRoam)
                    failures.Add("FIX B: FeatureFlags.RegionRoam is ON with no PlayerPrefs override — ambient overworld roaming must DEFAULT OFF (owner 2026-07-26)");
                else
                    log.AppendLine("OK: FIX B — RegionRoam defaults OFF (no pref)");

                PlayerPrefs.SetInt("ff.regionroam", 1);
                if (!FeatureFlags.RegionRoam)
                    failures.Add("FIX B: FeatureFlags.RegionRoam did NOT honour PlayerPrefs ff.regionroam=1 — the reversible re-enable is broken");
                else
                    log.AppendLine("OK: FIX B — RegionRoam reads ff.regionroam=1 (reversible ON)");

                PlayerPrefs.SetInt("ff.regionroam", 0);
                if (FeatureFlags.RegionRoam)
                    failures.Add("FIX B: FeatureFlags.RegionRoam did NOT honour PlayerPrefs ff.regionroam=0");
            }
            finally
            {
                if (priorRoam == -1) PlayerPrefs.DeleteKey("ff.regionroam");
                else PlayerPrefs.SetInt("ff.regionroam", priorRoam);
                PlayerPrefs.Save();
            }

            // Sanity: FIX A rides on RaidContinuousWalk, which must default OFF (WO-771 locked).
            int priorWalk = PlayerPrefs.GetInt("ff.raidwalk", -1);
            try
            {
                PlayerPrefs.DeleteKey("ff.raidwalk");
                if (FeatureFlags.RaidContinuousWalk)
                    failures.Add("FIX A: FeatureFlags.RaidContinuousWalk is ON by default — the walk-up outpost loop is retired and must default OFF (WO-771)");
                else
                    log.AppendLine("OK: FIX A — RaidContinuousWalk defaults OFF (walk-up outpost retired)");
            }
            finally
            {
                if (priorWalk == -1) PlayerPrefs.DeleteKey("ff.raidwalk");
                else PlayerPrefs.SetInt("ff.raidwalk", priorWalk);
                PlayerPrefs.Save();
            }

            // ── FIX A (source-structural): the two ungated seams are now gated + neutralized. ──
            // CavePortalRepointInjector.RepointCavePortals — gate + neutralize.
            string injRel = "_Modules/Village/World/CavePortalRepointInjector.cs";
            string injPath = Path.Combine(assetsRoot, injRel);
            if (!File.Exists(injPath))
            {
                failures.Add($"FIX A: {injRel} missing — repoint injector gone");
            }
            else
            {
                string injSrc = File.ReadAllText(injPath);
                if (TryExtractMethodBody(injSrc, "void RepointCavePortals(", out string repointBody))
                {
                    bool gated = repointBody.IndexOf("RaidContinuousWalk", System.StringComparison.Ordinal) >= 0;
                    bool neutralizes = repointBody.IndexOf("NeutralizeOutpostTriggers", System.StringComparison.Ordinal) >= 0;
                    bool returns = repointBody.IndexOf("return", System.StringComparison.Ordinal) >= 0;
                    if (!(gated && returns))
                        failures.Add("FIX A: CavePortalRepointInjector.RepointCavePortals does NOT early-return on !FeatureFlags.RaidContinuousWalk — the retired walk-up repoint can still run");
                    else if (!neutralizes)
                        failures.Add("FIX A: CavePortalRepointInjector.RepointCavePortals gates raidwalk but does NOT call NeutralizeOutpostTriggers — the baked cave trigger stays live and walkable");
                    else
                        log.AppendLine("OK: FIX A — RepointCavePortals gated on RaidContinuousWalk + neutralizes the baked seam when OFF");
                }
                else
                {
                    failures.Add("FIX A: could not locate CavePortalRepointInjector.RepointCavePortals( body — source shape changed; gate unverifiable");
                }

                // The neutralize path must DISABLE (not destroy) the trigger + collider.
                if (TryExtractMethodBody(injSrc, "void NeutralizeOutpostTriggers(", out string neutBody))
                {
                    bool disablesTrigger = neutBody.IndexOf("enabled = false", System.StringComparison.Ordinal) >= 0;
                    bool disablesCollider = neutBody.IndexOf("Collider", System.StringComparison.Ordinal) >= 0
                                            && neutBody.IndexOf("col.enabled = false", System.StringComparison.Ordinal) >= 0;
                    bool doesNotDestroy = neutBody.IndexOf("Destroy(", System.StringComparison.Ordinal) < 0;
                    if (!disablesTrigger)
                        failures.Add("FIX A: NeutralizeOutpostTriggers does not set the trigger '.enabled = false' — the seam is not neutralized");
                    if (!disablesCollider)
                        failures.Add("FIX A: NeutralizeOutpostTriggers does not disable the trigger's Collider — the OnTriggerEnter fallback can still fire");
                    if (!doesNotDestroy)
                        failures.Add("FIX A: NeutralizeOutpostTriggers calls Destroy() — it must DISABLE the component/collider, never destroy the GameObject (no scene mutation)");
                    if (disablesTrigger && disablesCollider && doesNotDestroy)
                        log.AppendLine("OK: FIX A — NeutralizeOutpostTriggers disables trigger + collider, never destroys (no scene edit)");
                }
                else
                {
                    failures.Add("FIX A: NeutralizeOutpostTriggers( body not found — neutralize path missing");
                }
            }

            // ChallengeOutpostVictoryController.TryInstall — gated on RaidContinuousWalk.
            string covRel = "_Modules/Village/World/Camps/ChallengeOutpostVictoryController.cs";
            string covPath = Path.Combine(assetsRoot, covRel);
            if (!File.Exists(covPath))
            {
                failures.Add($"FIX A: {covRel} missing");
            }
            else if (TryExtractMethodBody(File.ReadAllText(covPath), "void TryInstall(Scene", out string tiBody))
            {
                bool gated = tiBody.IndexOf("RaidContinuousWalk", System.StringComparison.Ordinal) >= 0
                             && tiBody.IndexOf("return", System.StringComparison.Ordinal) >= 0;
                if (!gated)
                    failures.Add("FIX A: ChallengeOutpostVictoryController.TryInstall does NOT early-return on !FeatureFlags.RaidContinuousWalk — the retired outpost victory installer can still self-arm");
                else
                    log.AppendLine("OK: FIX A — ChallengeOutpostVictoryController.TryInstall gated on RaidContinuousWalk");
            }
            else
            {
                failures.Add("FIX A: could not locate ChallengeOutpostVictoryController.TryInstall(Scene ...) body");
            }

            // ── FIX B (source-structural): RegionMobSpawner Bootstrap + Update gated. ──
            string spawnRel = "_Modules/Village/World/RegionMobSpawner.cs";
            string spawnPath = Path.Combine(assetsRoot, spawnRel);
            if (!File.Exists(spawnPath))
            {
                failures.Add($"FIX B: {spawnRel} missing");
            }
            else
            {
                string spawnSrc = File.ReadAllText(spawnPath);
                bool bootstrapGated = TryExtractMethodBody(spawnSrc, "void Bootstrap(", out string bootBody)
                                      && bootBody.IndexOf("RegionRoam", System.StringComparison.Ordinal) >= 0
                                      && bootBody.IndexOf("return", System.StringComparison.Ordinal) >= 0;
                bool updateGated = TryExtractMethodBody(spawnSrc, "void Update(", out string updBody)
                                   && updBody.IndexOf("RegionRoam", System.StringComparison.Ordinal) >= 0
                                   && updBody.IndexOf("return", System.StringComparison.Ordinal) >= 0;
                if (!bootstrapGated)
                    failures.Add("FIX B: RegionMobSpawner.Bootstrap does NOT gate on !FeatureFlags.RegionRoam — the spawner still self-bootstraps");
                if (!updateGated)
                    failures.Add("FIX B: RegionMobSpawner.Update does NOT gate on !FeatureFlags.RegionRoam — a stale instance can still spawn roamers");
                if (bootstrapGated && updateGated)
                    log.AppendLine("OK: FIX B — RegionMobSpawner Bootstrap + Update both gated on RegionRoam (nothing spawns when OFF)");
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "OVERWORLD_COMBAT_GATE_OK");
                reason = "OVERWORLD COMBAT GATE OK — FIX A (raidwalk OFF: cave repoint no-ops + neutralizes seam, challenge-victory installer no-ops) + FIX B (regionroam defaults OFF: RegionMobSpawner bootstrap+update gated)";
                return true;
            }

            reason = "overworld-combat-gate: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "OVERWORLD_COMBAT_GATE_FAIL: " + reason);
            return false;
        }

        // Extracts the balanced-brace body of the first method whose signature contains
        // <paramref name="signatureNeedle"/>. Brace chars via code point so this file's own
        // brace balance stays clean under the §1 gate. (Same shape as SceneRoutingRegression.)
        private static bool TryExtractMethodBody(string source, string signatureNeedle, out string body)
        {
            body = null;
            char openBrace = (char)123;
            char closeBrace = (char)125;
            int sig = source.IndexOf(signatureNeedle, System.StringComparison.Ordinal);
            if (sig < 0) return false;
            int open = source.IndexOf(openBrace, sig);
            if (open < 0) return false;
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == openBrace) depth++;
                else if (c == closeBrace)
                {
                    depth--;
                    if (depth == 0) { body = source.Substring(open, i - open + 1); return true; }
                }
            }
            return false;
        }
    }
}
