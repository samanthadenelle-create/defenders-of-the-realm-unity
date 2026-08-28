// WO-1265: the shipped clan/chat implementation is a local PlayerPrefs prototype.
// Until a signed-wallet backend, moderation and two-wallet proof exist, neither
// player entry point may expose it. Marker: CLAN_FEATURE_GATE_OK/FAIL.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class ClanFeatureGateRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string root = Application.dataPath;
            string gate = Read(root, "_Modules/Core/Services/ClanFeatureGate.cs", failures);
            string bootstrap = Read(root, "_Modules/HUD/ClanChatPanelBootstrap.cs", failures);
            string hud = Read(root, "_Modules/HUD/Kit/HudKitController.cs", failures);

            Require(gate, "public const bool PlayerFacingEnabled = false;", failures,
                "the local-only prototype is player-facing before backend readiness");
            Require(bootstrap, "if (!ClanFeatureGate.PlayerFacingEnabled) return;", failures,
                "direct ClanChatPanel bootstrap bypasses the release gate");
            Require(hud, "if (DeNelle.Core.Services.ClanFeatureGate.PlayerFacingEnabled)", failures,
                "the HUD dock can expose Chat while the prototype is gated");

            if (bootstrap.IndexOf("if (!ClanFeatureGate.PlayerFacingEnabled) return;", StringComparison.Ordinal) >
                bootstrap.IndexOf("new GameObject(\"ClanChatPanel\")", StringComparison.Ordinal))
                failures.Add("[clan-feature-gate] bootstrap checks the gate only after constructing the panel");
            if (hud.IndexOf("if (DeNelle.Core.Services.ClanFeatureGate.PlayerFacingEnabled)", StringComparison.Ordinal) >
                hud.IndexOf("AddDockTab(_slideDock.panel, dockRow++, \"Chat\"", StringComparison.Ordinal))
                failures.Add("[clan-feature-gate] HUD checks the gate only after adding the Chat door");

            if (failures.Count == 0)
            {
                Debug.Log("CLAN_FEATURE_GATE_OK");
                reason = "clan/chat local prototype has no player door and direct bootstrap is gated";
                return true;
            }
            reason = "clan-feature-gate: " + string.Join("; ", failures);
            Debug.LogError("CLAN_FEATURE_GATE_FAIL: " + reason);
            return false;
        }

        private static string Read(string root, string relative, List<string> failures)
        {
            string path = Path.Combine(root, relative);
            if (!File.Exists(path)) { failures.Add("[clan-feature-gate] missing " + relative); return string.Empty; }
            try { return File.ReadAllText(path); }
            catch (Exception ex) { failures.Add("[clan-feature-gate] unreadable " + relative + ": " + ex.Message); return string.Empty; }
        }

        private static void Require(string text, string needle, List<string> failures, string why)
        {
            if (text.IndexOf(needle, StringComparison.Ordinal) < 0)
                failures.Add("[clan-feature-gate] missing '" + needle + "' - " + why);
        }
    }
}
