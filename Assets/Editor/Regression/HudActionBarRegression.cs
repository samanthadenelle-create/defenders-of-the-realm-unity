// =============================================================================
// HudActionBarRegression — headless oracle for the WO-835 action-bar
// applicability repack. Marker: HUD_ACTIONBAR_OK / HUD_ACTIONBAR_FAIL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Registered by the
// orchestrator into DataRegression.RunAll (sibling-suite protocol).
// Style/contract mirrors the other Run(out reason) oracles.
//
// Proves, with the REAL Core model (no scene/play mode):
//   1. MODEL INVARIANTS — HudActionBarModel emits the exact ordered active set
//      per signal combination (WO-835 §3b table): town baseline Build/Bag/Map/
//      Quests; Talk packs in/out; Raids HIDES when not capable yet DIMS-visible
//      when capable-but-not-full (WO-820/823 semantics preserved); Map obeys
//      Onboarded (WO-825 R4); Quests is never replaced by Upgrade (§3c split);
//      explore = Talk?/Bag only; non-calm postures empty the bar; events are
//      edge-triggered (never per-frame relayout).
//   2. VIEW PURITY (source oracle on HudKitController.cs — DeNelle.HUD is not
//      referenced by this asmdef): the View binds the model and no longer holds
//      the retired gate reads (GameStateService / RaidEntryGate.ArmyStatus /
//      the Talk interactable dim), and the Upgrade face is registered.
//   3. WIRING — PostureSignals carries the RaidCapable mirror seam; the Village
//      RaidCapabilityHudBridge exists and cites ArmyReadiness.Compute (WO-823
//      single-source law); hud-areas.json dual copies carry the upgradeButton
//      row and have not diverged.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DeNelle.Core.HudModel;

namespace DeNelle.Editor
{
    public static class HudActionBarRegression
    {
        // Deterministic fake source (mirrors the EditMode fixture) — drives the
        // REAL compute through every combination.
        private sealed class FakeSource : HudActionBarModel.ISource
        {
            public bool Talk;
            public bool Capable;
            public bool ArmyReady = true;
            public bool Onboarded = true;
            public bool Focused;

            public bool TalkAvailable => Talk;
            public bool RaidCapable => Capable;
            public bool RaidArmyReady => ArmyReady;
            public bool MapUnlocked => Onboarded;
            public bool BuildingFocused => Focused;
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== HudActionBarRegression: WO-835 applicability repack ===");

            try
            {
                CheckModelInvariants(failures, log);
                CheckViewPurity(failures, log);
                CheckWiring(failures, log);
            }
            catch (System.Exception ex)
            {
                failures.Add($"HudActionBarRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            return Verdict(failures, log, out reason);
        }

        // ── 1. model invariants (the §3b predicate table, REAL compute) ───────
        private static void CheckModelInvariants(List<string> failures, StringBuilder log)
        {
            var src = new FakeSource();
            var model = new HudActionBarModel(src);
            model.SetPosture(HudActionBarModel.PostureTown);

            // ⚠ WO-911 (owner ruling Q10+Q13, 2026-08-06) — THE EXPECTED SET CHANGED, 7 -> 6.
            // Map LEFT the bar (it is a tab inside Bag now) and the Upgrade face was RE-POINTED to
            // the unified Manage/Queues screen, which makes it always-applicable in town instead of
            // context-gated on a focused building. These oracles are UPDATED to the new set and
            // order — never deleted or weakened to make a smaller bar pass.
            ExpectSet(model, failures, "town baseline (no NPC/raid)",
                ActionBarButtonId.Build, ActionBarButtonId.Bag, ActionBarButtonId.Quests,
                ActionBarButtonId.Upgrade);

            src.Talk = true; model.Tick();
            ExpectSet(model, failures, "NPC in range",
                ActionBarButtonId.Build, ActionBarButtonId.Talk, ActionBarButtonId.Bag,
                ActionBarButtonId.Quests, ActionBarButtonId.Upgrade);
            src.Talk = false; model.Tick();
            if (model.Active.Contains(ActionBarButtonId.Talk))
                failures.Add("Talk did not repack OUT when the NPC left range (hide, not dim)");

            // Raids: not capable => absent (WO-835 §3d); capable + not full => visible AND dimmed (WO-820).
            src.Capable = false; src.ArmyReady = false; model.Tick();
            if (model.Active.Contains(ActionBarButtonId.Raids))
                failures.Add("Raids visible while NOT capable (no building/troops) — WO-835 hide default broken");
            if (model.RaidsDimmed)
                failures.Add("RaidsDimmed true while the face is absent");
            src.Capable = true; model.Tick();
            if (!model.Active.Contains(ActionBarButtonId.Raids))
                failures.Add("Raids absent while capable — capability must show the face");
            if (!model.RaidsDimmed)
                failures.Add("capable-but-not-full army did not DIM the visible Raids face (WO-820/823 semantics lost)");
            src.ArmyReady = true; model.Tick();
            if (model.RaidsDimmed)
                failures.Add("full army did not restore the Raids face");

            // WO-911 — Map must NEVER pack into the bar again, in EITHER Onboarded state. The
            // WO-825 R4 gate is not "lost": the whole face moved into Bag (flag-gated there by
            // FeatureFlags.MapTab). A returning Map face is the regression now.
            src.Onboarded = false; model.Tick();
            if (model.Active.Contains(ActionBarButtonId.Map))
                failures.Add("Map is back on the bar (pre-onboard) — WO-911 moved it into Bag as a tab");
            src.Onboarded = true; model.Tick();
            if (model.Active.Contains(ActionBarButtonId.Map))
                failures.Add("Map is back on the bar (Onboarded) — WO-911 moved it into Bag as a tab; the bar is 6 faces");

            // WO-911 — the re-pointed Manage face is ALWAYS applicable in town, focus or not.
            // Gating it on a focused building is exactly the undiscoverability the WO removes.
            src.Focused = false; model.Tick();
            if (!model.Active.Contains(ActionBarButtonId.Upgrade))
                failures.Add("the Manage face (ActionBarButtonId.Upgrade, re-pointed) is absent with no focused " +
                             "building — it is the single door to the queues and must always be in town");
            src.Focused = true; model.Tick();
            if (!model.Active.Contains(ActionBarButtonId.Upgrade))
                failures.Add("the Manage face vanished when a building took focus");
            if (!model.Active.Contains(ActionBarButtonId.Quests))
                failures.Add("Quests vanished while a building is focused — the §3c split regressed to the relabel hijack");

            // Canonical order at the 6-face MAX (WO-911: Build, Talk, Bag, Raids, Quests, Manage).
            src.Talk = true; model.Tick();
            ExpectSet(model, failures, "6-face MAX order",
                ActionBarButtonId.Build, ActionBarButtonId.Talk, ActionBarButtonId.Bag,
                ActionBarButtonId.Raids, ActionBarButtonId.Quests, ActionBarButtonId.Upgrade);
            if (model.Active.Count > HudActionBarModel.MaxVisibleFaces)
                failures.Add($"the bar renders {model.Active.Count} faces but the View sizes slots from " +
                             $"MaxVisibleFaces = {HudActionBarModel.MaxVisibleFaces} — the group would overflow its zone");

            // Explore parity with its occupancy row; non-calm postures drop the bar.
            model.SetPosture(HudActionBarModel.PostureExplore);
            ExpectSet(model, failures, "explore (talk on)", ActionBarButtonId.Talk, ActionBarButtonId.Bag);
            model.SetPosture("build");
            if (model.Active.Count != 0)
                failures.Add("build posture did not empty the bar set");

            // Edge-triggered event contract.
            model.SetPosture(HudActionBarModel.PostureTown);
            int events = 0;
            model.ActiveButtonsChanged += () => events++;
            model.Tick(); model.Tick();
            if (events != 0)
                failures.Add($"ActiveButtonsChanged fired {events}x with no input change — per-frame relayout risk");
            src.Talk = false; model.Tick();
            if (events != 1)
                failures.Add($"one set change raised {events} events (expected exactly 1)");

            log.AppendLine("  model invariants (baseline/talk/raids hide+dim/map/quests-upgrade/explore/edge-trigger) OK-checked");
        }

        private static void ExpectSet(HudActionBarModel model, List<string> failures,
                                      string label, params ActionBarButtonId[] expected)
        {
            var got = model.Active;
            bool match = got.Count == expected.Length;
            if (match)
                for (int i = 0; i < expected.Length; i++)
                    if (got[i] != expected[i]) { match = false; break; }
            if (!match)
                failures.Add($"{label}: expected [{string.Join(" ", expected)}], got [{string.Join(" ", got)}]");
        }

        // ── 2. View purity (source oracle — the WO-835 architecture law) ──────
        private static void CheckViewPurity(List<string> failures, StringBuilder log)
        {
            string kitPath = Path.Combine(Application.dataPath, "_Modules/HUD/Kit/HudKitController.cs");
            if (!File.Exists(kitPath))
            {
                failures.Add("HudKitController.cs missing at " + kitPath);
                return;
            }
            string kitSrc = File.ReadAllText(kitPath);

            if (kitSrc.IndexOf("HudActionBarModel.Shared") < 0)
                failures.Add("HudKitController does not bind HudActionBarModel.Shared (the View lost its one action-bar input)");
            if (kitSrc.IndexOf("ActiveButtonsChanged") < 0)
                failures.Add("HudKitController does not consume ActiveButtonsChanged (repack render pass unbound)");
            if (kitSrc.IndexOf("\"upgradeButton\"") < 0)
                failures.Add("HudKitController does not register the upgradeButton face (WO-835 §3c split missing)");

            // The retired gate reads must NOT return to the View (predicates live in Core).
            if (kitSrc.IndexOf("GameStateService") >= 0)
                failures.Add("HudKitController reads GameStateService again — the Onboarded predicate belongs to HudActionBarModel");
            if (kitSrc.IndexOf("RaidEntryGate.ArmyStatus") >= 0)
                failures.Add("HudKitController reads RaidEntryGate.ArmyStatus again — the army-dim predicate belongs to HudActionBarModel");
            if (kitSrc.IndexOf(".interactable = PostureSignals.TalkAvailable") >= 0)
                failures.Add("HudKitController re-grew the Talk dim gate — availability belongs to HudActionBarModel");

            log.AppendLine("  View purity (model bound, upgrade face present, retired gate reads absent) OK");
        }

        // ── 3. wiring — mirror seam + Village publisher + occupancy rows ──────
        private static void CheckWiring(List<string> failures, StringBuilder log)
        {
            // Core mirror seam (the SetTalkAvailable pattern).
            var sig = typeof(PostureSignals);
            if (sig.GetProperty("RaidCapable") == null)
                failures.Add("PostureSignals.RaidCapable missing (the Village->Core mirror target)");
            if (sig.GetMethod("SetRaidCapable") == null)
                failures.Add("PostureSignals.SetRaidCapable missing (the producer seam)");
            if (sig.GetEvent("RaidCapableChanged") == null)
                failures.Add("PostureSignals.RaidCapableChanged missing (the edge event)");

            // Village publisher exists (typed — same asmdef family) + single-source law.
            var bridge = typeof(DeNelle.Village.RaidCapabilityHudBridge);
            if (!typeof(MonoBehaviour).IsAssignableFrom(bridge))
                failures.Add("RaidCapabilityHudBridge is not a MonoBehaviour bridge");
            string bridgePath = Path.Combine(Application.dataPath, "_Modules/Village/Troops/RaidCapabilityHudBridge.cs");
            if (!File.Exists(bridgePath))
            {
                failures.Add("RaidCapabilityHudBridge.cs missing at " + bridgePath);
            }
            else
            {
                string bridgeSrc = File.ReadAllText(bridgePath);
                if (bridgeSrc.IndexOf("ArmyReadiness.Compute") < 0)
                    failures.Add("RaidCapabilityHudBridge does not use ArmyReadiness.Compute — WO-823 single-source law (never re-roll the army math)");
                if (bridgeSrc.IndexOf("StructureSingleton.IsBuilt") < 0)
                    failures.Add("RaidCapabilityHudBridge does not check StructureSingleton.IsBuilt (the raid-building half of the predicate)");
                if (bridgeSrc.IndexOf("SetRaidCapable") < 0)
                    failures.Add("RaidCapabilityHudBridge never publishes SetRaidCapable");
            }

            // Occupancy rows: upgradeButton in BOTH dual copies; copies identical
            // (CanonicalJson law — the Work-button-dark lesson).
            string resJson = Path.Combine(Application.dataPath, "Resources/Data/Canonical/hud-areas.json");
            string samJson = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/hud-areas.json");
            foreach (var p in new[] { resJson, samJson })
            {
                if (!File.Exists(p)) { failures.Add("hud-areas.json missing: " + p); continue; }
                if (File.ReadAllText(p).IndexOf("upgradeButton") < 0)
                    failures.Add("hud-areas.json missing the upgradeButton row (face would be code-present, behavior-absent): " + p);
            }
            if (File.Exists(resJson) && File.Exists(samJson) &&
                File.ReadAllText(resJson) != File.ReadAllText(samJson))
                failures.Add("hud-areas.json dual copies diverged (Resources vs StreamingAssets — CanonicalJson law)");

            log.AppendLine("  wiring (RaidCapable seam + Village bridge + ArmyReadiness single-source + occupancy rows) OK");
        }

        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "HUD ACTION BAR OK — WO-835 applicability model invariants + View purity + " +
                         "RaidCapable seam/bridge + occupancy rows all hold";
                Debug.Log("HUD_ACTIONBAR_OK\n" + log);
                return true;
            }
            reason = $"HUD ACTION BAR: {failures.Count} failure(s): " + string.Join(" | ", failures);
            Debug.LogError($"HUD_ACTIONBAR_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }
    }
}
