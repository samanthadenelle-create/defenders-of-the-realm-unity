using System;
using System.Collections.Generic;
using System.IO;

namespace DeNelle.Editor.Regression
{
    /// <summary>Pins the load-time seam that restores a persisted shield onto a bare Knight body.</summary>
    public static class ShieldLoadRestoreRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string swapper = Read("Assets/_Modules/Village/Hero/HeroBodySwapper.cs", failures);
            string equipment = Read("Assets/_Modules/Village/Hero/EquipmentController.cs", failures);

            int clear = swapper.IndexOf("!usePackage && TryGetComponent(out PackageBakedGearMarker", StringComparison.Ordinal);
            int addController = swapper.IndexOf("GetComponent<EquipmentController>() == null", StringComparison.Ordinal);
            if (clear < 0 || addController < 0 || clear > addController)
                failures.Add("a stale baked-gear marker is not cleared before EquipmentController is created/reused");
            if (!swapper.Contains("Destroy(staleBakedGearMarker)"))
                failures.Add("bare-body restoration does not remove the stale package marker");
            if (!swapper.Contains("staleBakedGearMarker.enabled = false"))
                failures.Add("stale marker is not invalidated synchronously before end-of-frame destruction");
            if (!equipment.Contains("PackageBakedGearMarker marker) && marker.enabled"))
                failures.Add("equipment suppression does not respect the synchronous marker invalidation");
            if (!equipment.Contains("needOffHand = _loadout != null && _loadout.EquippedOffHand != null && _currentOffHandProp == null"))
                failures.Add("late rig/load retry no longer treats an equipped-but-unrendered off-hand as pending");
            if (!equipment.Contains("EquipOffHand(_loadout != null ? _loadout.EquippedOffHand : null)"))
                failures.Add("visual refresh no longer projects the authoritative equipped off-hand");

            if (failures.Count > 0)
            {
                reason = "SHIELD_LOAD_RESTORE_FAIL: " + string.Join(" | ", failures);
                return false;
            }
            reason = "SHIELD LOAD RESTORE OK - stale baked-body authority is removed before a bare Knight rebind, and the existing retry restores the authoritative equipped off-hand when its prop is absent";
            return true;
        }

        private static string Read(string relative, List<string> failures)
        {
            string path = Path.GetFullPath(relative);
            if (File.Exists(path)) return File.ReadAllText(path);
            failures.Add("missing source: " + relative);
            return string.Empty;
        }
    }
}
