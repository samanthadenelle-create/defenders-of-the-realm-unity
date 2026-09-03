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

                CheckNeedsRepairBothDirections(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add($"RepairHudContractRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                reason = "repair HUD contract intact (3 methods + 2 command events resolve by reflection); " +
                         "needs-repair oracle green in BOTH directions (pristine offers nothing, " +
                         "60%-HP building still repairable, full repair clears it)";
                log.AppendLine("PASS: " + reason);
                Debug.Log(log.ToString());
                return true;
            }

            reason = "repair HUD contract BROKEN: " + string.Join(" | ", failures);
            log.AppendLine("FAIL: " + reason);
            Debug.LogError(log.ToString());
            return false;
        }

        // =====================================================================
        //  NEEDS-REPAIR ORACLE — BOTH DIRECTIONS
        // ---------------------------------------------------------------------
        //  Added 2026-09-03 alongside the WO-1296 recurrence ("yellow item not
        //  damaged is still showing up"). The tempting one-sided fix for that
        //  report is to make the repair affordance stop appearing — and a
        //  one-sided test would happily certify it, because "an undamaged
        //  structure offers nothing" passes just as well when NOTHING is ever
        //  offered. So this pins the pair:
        //
        //    1. a PRISTINE structure must NOT be repairable  (the report), and
        //    2. a genuinely DAMAGED one must STILL be repairable  (the feature).
        //
        //  ⚠ These two cases are GREEN as written today, deliberately: the
        //  recurrence is a THRESHOLD-RECONCILIATION ruling, not a logic error.
        //  RepairTarget.NeedsRepair trips at DamageFraction > 0.0001 while the
        //  first visible damage tell is the smolder loop at HP <= 0.5
        //  (damage-states.json), so HP 50%..99.99% is repairable-and-pristine and
        //  the code is doing exactly what it says. Whichever threshold the owner
        //  moves, direction 2 is the one that must not be collateral damage.
        //
        //  EditMode-safe: Awake/Start do not run on AddComponent outside play
        //  mode, so this leans only on Building's serialized field initialisers
        //  (_hp = _maxHp = 100) and on ApplyDamage/Repair, which are pure state.
        // =====================================================================
        private static void CheckNeedsRepairBothDirections(List<string> failures, StringBuilder log)
        {
            GameObject go = null;
            try
            {
                Type targetType = ResolveVillageType("DeNelle.Village.RepairTarget");
                Type buildingType = ResolveVillageType("DeNelle.Village.Building");
                if (targetType == null || buildingType == null)
                {
                    failures.Add("needs-repair oracle: could not resolve DeNelle.Village.RepairTarget / " +
                                 "Building — the repair predicate cannot be verified.");
                    return;
                }

                go = new GameObject("~RepairOracleBuilding") { hideFlags = HideFlags.HideAndDontSave };
                go.AddComponent<BoxCollider>();
                var building = go.AddComponent(buildingType) as Component;
                if (building == null)
                {
                    failures.Add("needs-repair oracle: Building component could not be added.");
                    return;
                }

                var tryWrap = targetType.GetMethod("TryWrap",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Component) }, null);
                var needsRepair = targetType.GetProperty("NeedsRepair",
                    BindingFlags.Public | BindingFlags.Instance);
                var damageFraction = targetType.GetProperty("DamageFraction",
                    BindingFlags.Public | BindingFlags.Instance);
                var applyDamage = buildingType.GetMethod("ApplyDamage",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null);
                var repair = buildingType.GetMethod("Repair",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null);

                if (tryWrap == null || needsRepair == null || damageFraction == null ||
                    applyDamage == null || repair == null)
                {
                    failures.Add("needs-repair oracle: RepairTarget.TryWrap/NeedsRepair/DamageFraction " +
                                 "or Building.ApplyDamage/Repair moved — the oracle cannot bind.");
                    return;
                }

                // ── Direction 1: PRISTINE offers nothing ──────────────────────
                object wrapped = tryWrap.Invoke(null, new object[] { building });
                if (wrapped == null)
                {
                    failures.Add("needs-repair oracle: RepairTarget.TryWrap returned null for a plain " +
                                 "Building — the repair loop cannot reach any building at all.");
                    return;
                }
                bool pristineNeeds = (bool)needsRepair.GetValue(wrapped);
                float pristineFrac = (float)damageFraction.GetValue(wrapped);
                if (pristineNeeds)
                    failures.Add($"needs-repair oracle DIRECTION 1: an UNDAMAGED building reports " +
                                 $"NeedsRepair=true (damageFraction={pristineFrac:0.0000}) — a repair / " +
                                 "rebuild prompt would be offered on a pristine structure.");
                else
                    log.AppendLine($"  OK   undamaged building -> NeedsRepair=false (frac {pristineFrac:0.0000})");

                // ── Direction 2: genuinely DAMAGED still offers ───────────────
                applyDamage.Invoke(building, new object[] { 40f });   // 100 -> 60 hp
                object wrappedDamaged = tryWrap.Invoke(null, new object[] { building });
                bool damagedNeeds = wrappedDamaged != null && (bool)needsRepair.GetValue(wrappedDamaged);
                float damagedFrac = wrappedDamaged != null ? (float)damageFraction.GetValue(wrappedDamaged) : 0f;
                if (!damagedNeeds)
                    failures.Add($"needs-repair oracle DIRECTION 2: a building at 60% HP reports " +
                                 $"NeedsRepair=false (damageFraction={damagedFrac:0.0000}) — the repair " +
                                 "feature has been suppressed, not corrected. A one-sided 'stop showing " +
                                 "the prompt' fix is exactly what this case exists to catch.");
                else
                    log.AppendLine($"  OK   damaged building -> NeedsRepair=true (frac {damagedFrac:0.0000})");

                // ── And a full repair returns it to direction 1 ───────────────
                repair.Invoke(building, new object[] { 1000f });
                object wrappedHealed = tryWrap.Invoke(null, new object[] { building });
                bool healedNeeds = wrappedHealed != null && (bool)needsRepair.GetValue(wrappedHealed);
                if (healedNeeds)
                    failures.Add("needs-repair oracle: a FULLY repaired building still reports " +
                                 "NeedsRepair=true — the affordance would never clear after a repair.");
                else
                    log.AppendLine("  OK   fully repaired building -> NeedsRepair=false");
            }
            catch (Exception ex)
            {
                failures.Add($"needs-repair oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Resolves a DeNelle.Village type by name. DeNelle.Editor does reference the Village
        /// assembly, but this suite is deliberately reflection-only end to end so a moved type
        /// is a readable FAILURE line rather than a compile break in the gate itself.
        /// </summary>
        private static Type ResolveVillageType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(fullName, false); }
                catch (Exception) { continue; }
                if (t != null) return t;
            }
            return null;
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
