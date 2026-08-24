// =============================================================================
// RepairHudContractRegression — pins the Village->HUD REPAIR REFLECTION CONTRACT.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (owner felt-test 2026-08-24: "Purple shader says repair but no
// option to repair"). Selecting a damaged structure turned the world marker violet
// and its label read "Repair?" (RepairHighlight.ApplyColor) — and nothing
// actionable ever appeared. The cause was captured on the DEVICE, not inferred
// (Logs/device/2026-08-20-equip.log:4580831, once per Bind()):
//
//   W/Unity: [WallRepairHudBridge] One or more HUD repair-prompt methods were not
//            found on 'DeNelle.HUD.VillageHudController'. Is the HUD module up to date?
//
// DeNelle.Village may not reference DeNelle.HUD (CLAUDE.md §5), so
// WallRepairHudBridge.ResolveHudHandles binds the HUD BY REFLECTION. That seam has
// no compile-time check, so when the HUD's ShowRepairPrompt stayed (string,float)
// and ShowRepairFeedback was never added, two of the three lookups silently
// returned null, OnPromptShown's `_hudShowPrompt?.Invoke(...)` became a no-op, and
// the repair loop was unreachable — with the ONLY detector being the owner's eyes.
//
// A reflection seam that nothing verifies WILL drift. This suite is the verifier:
// it resolves the exact members the bridge resolves, the exact way the bridge
// resolves them, and fails the gate the moment either side moves.
//
// ⚠ THIS FILE IS THE TWIN OF WallRepairHudBridge.ResolveHudHandles. If you change
//    a signature there, change it here in the SAME edit — a copy that goes stale
//    reinstates the exact hole it was written to close.
//
// Contract mirrors the other suites: public static bool Run(out string reason).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Editor
{
    public static class RepairHudContractRegression
    {
        private const string HudTypeName = "DeNelle.HUD.VillageHudController";

        /// <summary>Batchmode entry point (run-unity-method.ps1).</summary>
        public static void RunStandalone()
        {
            if (Run(out var reason)) Debug.Log("REPAIR_HUD_CONTRACT_OK " + reason);
            else Debug.LogError("REPAIR_HUD_CONTRACT_FAIL " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== RepairHudContractRegression: WallRepairHudBridge -> VillageHudController ===");

            try
            {
                Type hud = ResolveHudType();
                if (hud == null)
                {
                    // NOT a pass. The bridge would find nothing either.
                    failures.Add($"HUD type '{HudTypeName}' not found in any loaded assembly — " +
                                 "the repair bridge has nothing to bind to.");
                }
                else
                {
                    log.AppendLine($"HUD type resolved: {hud.AssemblyQualifiedName}");

                    // --- the three METHODS, resolved with the bridge's exact-types lookup ---
                    RequireMethod(hud, "ShowRepairPrompt",
                        new[] { typeof(string), typeof(int), typeof(bool) }, failures, log);
                    RequireMethod(hud, "HideRepairPrompt",
                        Type.EmptyTypes, failures, log);
                    RequireMethod(hud, "ShowRepairFeedback",
                        new[] { typeof(string), typeof(bool) }, failures, log);

                    // --- the two COMMAND EVENTS the bridge subscribes ---
                    RequireUnityEvent(hud, "RepairConfirmRequested", failures, log);
                    RequireUnityEvent(hud, "RepairCancelRequested", failures, log);
                }
            }
            catch (Exception ex)
            {
                failures.Add($"RepairHudContractRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                reason = "repair HUD contract intact (3 methods + 2 command events resolve by reflection)";
                log.AppendLine("PASS: " + reason);
                Debug.Log(log.ToString());
                return true;
            }

            reason = "repair HUD contract BROKEN: " + string.Join(" | ", failures);
            log.AppendLine("FAIL: " + reason);
            Debug.LogError(log.ToString());
            return false;
        }

        /// <summary>
        /// Finds the HUD type without an asmdef reference (DeNelle.Editor does not reference
        /// DeNelle.HUD) — the same by-name resolution the runtime bridge is limited to.
        /// </summary>
        private static Type ResolveHudType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(HudTypeName, false); }
                catch (Exception) { continue; }   // logged by the caller's failure list, never silent
                if (t != null) return t;
            }
            return null;
        }

        private static void RequireMethod(Type hud, string name, Type[] argTypes,
                                          List<string> failures, StringBuilder log)
        {
            string sig = name + "(" + string.Join(",", Array.ConvertAll(argTypes, a => a.Name)) + ")";

            var mi = hud.GetMethod(name, BindingFlags.Public | BindingFlags.Instance,
                                   null, argTypes, null);
            if (mi != null) { log.AppendLine("  OK   " + sig); return; }

            // Say WHAT DOES exist — a drifted signature and a missing method are
            // opposite diagnoses, and the old runtime warning could not tell them apart.
            var sameName = hud.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var others = new List<string>();
            foreach (var m in sameName)
            {
                if (m.Name != name) continue;
                var ps = m.GetParameters();
                var names = new string[ps.Length];
                for (int i = 0; i < ps.Length; i++) names[i] = ps[i].ParameterType.Name;
                others.Add(name + "(" + string.Join(",", names) + ")");
            }

            failures.Add(others.Count == 0
                ? $"{sig} MISSING from {hud.Name} (no overload of that name exists)"
                : $"{sig} MISSING from {hud.Name} — SIGNATURE DRIFT, present instead: {string.Join(" / ", others)}");
        }

        private static void RequireUnityEvent(Type hud, string memberName,
                                              List<string> failures, StringBuilder log)
        {
            // The bridge reads a field first, then a property — mirror that order exactly.
            var field = hud.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                if (typeof(UnityEvent).IsAssignableFrom(field.FieldType))
                { log.AppendLine("  OK   " + memberName + " (field)"); return; }
                failures.Add($"{memberName} is a field of type {field.FieldType.Name}, " +
                             "not a plain UnityEvent — the bridge cannot subscribe it");
                return;
            }

            var prop = hud.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanRead && typeof(UnityEvent).IsAssignableFrom(prop.PropertyType))
            { log.AppendLine("  OK   " + memberName + " (property)"); return; }

            failures.Add($"{memberName} MISSING from {hud.Name} (or not a readable plain UnityEvent) — " +
                         "the HUD's Repair/Cancel button press would reach no one");
        }
    }
}
