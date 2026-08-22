// WO-1143: pins remote troop recovery and a horizontal siege fallback silhouette.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class AddressableTroopVisualRegression
    {
        public static bool Run(out string reason)
        {
            try
            {
                string source = File.ReadAllText(Path.Combine(Application.dataPath,
                    "_Modules/Village/Troops/TroopFactory.cs"));
                var failures = new List<string>();
                Require(source, "StructureContentWarmer.WhenSettled", failures,
                    "path-form troop visuals are not retried after Addressables settle");
                Require(source, "IsAddressableModelPath(resourcesPath)", failures,
                    "retry is not gated by the shared addressable-path convention");
                Require(source, "Object.Destroy(capturedFallback)", failures,
                    "successful remote art does not replace its temporary fallback");
                Require(source, "if (arrived == null)", failures,
                    "failed retry can remove the only readable visual");
                Require(source, "BuildSiegeFallback(go.transform)", failures,
                    "siege misses still share the humanoid fallback");
                Require(source, "TroopFallback_SiegeMachine", failures,
                    "siege fallback has no explicit machine identity");
                Require(source, "\"Chassis\"", failures, "siege proxy has no low chassis");
                Require(source, "\"ThrowingArm\"", failures, "siege proxy has no throwing arm");
                Require(source, "\"Wheel_", failures, "siege proxy has no wheels");

                string legacy = "new Vector3(1.4f, bodyHeight * 0.5f, 1.8f)";
                if (source.IndexOf(legacy, StringComparison.Ordinal) >= 0)
                    failures.Add("legacy oversized vertical siege capsule is still present");

                if (failures.Count > 0)
                {
                    reason = "addressable-troop-visual: " + string.Join(" | ", failures);
                    return false;
                }

                reason = "addressable-troop-visual: remote troop art retries once after settle; " +
                         "siege degrades to a low machine silhouette and remains visible on failure";
                return true;
            }
            catch (Exception ex)
            {
                reason = "addressable-troop-visual: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("ADDRESSABLE_TROOP_VISUAL_OK - " + reason);
            else Debug.LogError("ADDRESSABLE_TROOP_VISUAL_FAIL - " + reason);
        }

        private static void Require(string source, string token, List<string> failures, string message)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0) failures.Add(message);
        }
    }
}
