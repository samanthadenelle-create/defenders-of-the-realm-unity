using System;
using DeNelle.Core.Jobs;
using DeNelle.Village;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class TemporaryBuilderRegression
    {
        [MenuItem("Tools/DeNelle/Regression/Run Temporary Builder Regression")]
        public static void RunMenu()
        {
            if (!Run(out string reason)) throw new InvalidOperationException(reason);
            Debug.Log(reason);
        }

        public static bool Run(out string reason)
        {
            const double now = 1_900_000_000_000d;
            var channel = new ChannelState
            {
                BoughtSlots = 2,
                TemporarySlotClaimed = true,
                TemporarySlotEndsAtUnixMs = now + 86_400_000d
            };

            string json = JsonConvert.SerializeObject(channel);
            var loaded = JsonConvert.DeserializeObject<ChannelState>(json);
            if (loaded == null || !loaded.TemporarySlotClaimed ||
                loaded.TemporarySlotEndsAtUnixMs != channel.TemporarySlotEndsAtUnixMs)
            {
                reason = "TEMP_BUILDER_FAIL: expiry did not round-trip";
                return false;
            }
            if (!BuildTimerService.IsTemporarySlotActive(loaded.TemporarySlotEndsAtUnixMs, now) ||
                BuildTimerService.IsTemporarySlotActive(loaded.TemporarySlotEndsAtUnixMs, loaded.TemporarySlotEndsAtUnixMs))
            {
                reason = "TEMP_BUILDER_FAIL: wall-clock boundary is not [grant, expiry)";
                return false;
            }
            if (BuildTimerService.CanClaimTemporarySlot(loaded.TemporarySlotClaimed) ||
                !BuildTimerService.CanClaimTemporarySlot(false))
            {
                reason = "TEMP_BUILDER_FAIL: claimed taste can stack or be reclaimed";
                return false;
            }

            // The temporary flag is a boolean axis: it contributes at most one and never mutates
            // the permanent purchase count. At expiry only capacity falls; active job data is untouched.
            int during = BuildTimerService.ConcurrencyOf(2, loaded.BoughtSlots, false) + 1;
            int after = BuildTimerService.ConcurrencyOf(2, loaded.BoughtSlots, false);
            if (during != 5 || after != 4 || loaded.BoughtSlots != 2 || loaded.ActiveJobs.Count != 0)
            {
                reason = "TEMP_BUILDER_FAIL: temporary/permanent axes conflated";
                return false;
            }

            reason = "TEMP_BUILDER_OK: persisted wall-clock expiry, +1 capped concurrency, permanent count unchanged, expiry boundary holds";
            return true;
        }
    }
}
