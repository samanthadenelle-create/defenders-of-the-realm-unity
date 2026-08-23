using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class GlimmerEconomyRegression
    {
        public static bool Run(out string reason)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    if (asm.GetType("DeNelle.Cosmetics.GlimmerCurrencyService", false) != null ||
                        asm.GetType("DeNelle.Cosmetics.BattlePassManager", false) != null)
                    { reason = "CURRENCY RETIREMENT FAIL: a retired runtime type is still compiled"; return false; }

                string[] twins = { "cosmetics.json", "packs.json", "daily-quests.json", "quests.json", "battle_monthly.json" };
                foreach (string name in twins)
                {
                    string resources = Path.Combine(Application.dataPath, "Resources/Data/Canonical", name);
                    string streaming = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical", name);
                    if (!File.Exists(resources) || !File.Exists(streaming) ||
                        !string.Equals(File.ReadAllText(resources), File.ReadAllText(streaming), StringComparison.Ordinal))
                    { reason = "CURRENCY RETIREMENT FAIL: canonical twins differ for " + name; return false; }
                    if (File.ReadAllText(resources).IndexOf("\"glimmer", StringComparison.OrdinalIgnoreCase) >= 0)
                    { reason = "CURRENCY RETIREMENT FAIL: retired schema key remains in " + name; return false; }
                }

                const string legacy = "{\"glimmer\":999,\"ownedCosmetics\":[\"probe\"],\"equippedByCategory\":{\"hero\":\"probe\"}}";
                var state = JsonConvert.DeserializeObject<DeNelle.Cosmetics.CosmeticOwnershipSaveData>(legacy);
                if (state == null || state.OwnedCosmetics.Count != 1 || state.EquippedByCategory["hero"] != "probe")
                { reason = "CURRENCY RETIREMENT FAIL: legacy field handling lost cosmetic ownership"; return false; }

                reason = "CURRENCY RETIREMENT OK: runtimes absent, canonical schema clean/twinned, legacy field ignored and ownership preserved";
                return true;
            }
            catch (Exception ex)
            {
                reason = "CURRENCY RETIREMENT FAIL: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }
    }
}
