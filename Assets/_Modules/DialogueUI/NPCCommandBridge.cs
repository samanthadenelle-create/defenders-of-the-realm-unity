using UnityEngine;
using Yarn.Unity;

namespace DeNelle.DialogueUI
{
    [DisallowMultipleComponent]
    public sealed class NPCCommandBridge : MonoBehaviour
    {
        private DialogueRunner _runner;

        public void Install(DialogueRunner runner)
        {
            if (runner == null) return;
            _runner = runner;

            var reg = (IActionRegistration)runner;
            reg.AddCommandHandler("OpenUpgrade", (System.Action<string>)CmdOpenUpgrade);
            reg.AddCommandHandler("OpenCraft", (System.Action<string>)CmdOpenCraft);
            reg.AddCommandHandler("OpenEquip", CmdOpenEquip);
            reg.AddCommandHandler("OpenShop", (System.Action<string>)CmdOpenShop);
            reg.AddCommandHandler("OpenArena", CmdOpenArena);
            reg.AddCommandHandler("LearnRecipe", (System.Action<string>)CmdLearnRecipe);

            // Quests (WO-290) — vendor / forgemaster / pet narrative verbs delegate
            // to the general QuestService (Core). These mirror the FTUE bridge's
            // registrations so quest dialogue works from EITHER bridge.
            reg.AddCommandHandler("StartQuest",    (System.Action<string>)CmdStartQuest);
            reg.AddCommandHandler("AdvanceQuest",  (System.Action<string>)CmdAdvanceQuest);
            reg.AddCommandHandler("CompleteQuest", (System.Action<string>)CmdCompleteQuest);
            reg.AddCommandHandler("SetQuestFlag",  (System.Action<string, string>)CmdSetQuestFlag);
            reg.AddCommandHandler("GiveKeystone",  (System.Action<string>)CmdGiveKeystone);
            reg.AddFunction("HasKeystone",  (System.Func<string, bool>)FnHasKeystone);
            reg.AddFunction("KeystoneCount", (System.Func<int>)FnKeystoneCount);

            Debug.Log("[NPCCommandBridge] Commands registered successfully.");
        }

        private void CmdOpenUpgrade(string stationType)
        {
            Debug.Log($"[NPC] OpenUpgrade: {stationType}");
            var panel = FindFirstObjectByType<DeNelle.Village.Buildings.Progression.BuildingUpgradePanel>();
            if (panel == null)
            {
                var host = new GameObject("BuildingUpgradePanelHost");
                panel = host.AddComponent<DeNelle.Village.Buildings.Progression.BuildingUpgradePanel>();
            }
            if (!string.IsNullOrEmpty(stationType)) panel.OpenFocused(stationType);
            else panel.Open();
        }

        private void CmdOpenCraft(string context)
        {
            Debug.Log($"[NPC] OpenCraft: {context}");
            var panel = FindFirstObjectByType<DeNelle.Village.Crafting.VillageCraftingPanel>();
            if (panel == null)
            {
                var host = new GameObject("VillageCraftingPanelHost");
                panel = host.AddComponent<DeNelle.Village.Crafting.VillageCraftingPanel>();
            }
            panel.Open();
        }

        private void CmdOpenEquip()
        {
            Debug.Log("[NPC] OpenEquip called");
            var panel = FindFirstObjectByType<DeNelle.Village.Hero.EquipmentPanel>();
            if (panel == null)
            {
                var host = new GameObject("EquipmentPanelHost");
                panel = host.AddComponent<DeNelle.Village.Hero.EquipmentPanel>();
            }
            panel.Open();
        }

        private void CmdOpenShop(string vendor)
        {
            Debug.Log($"[NPC] OpenShop: {vendor}");
            var panel = FindFirstObjectByType<DeNelle.Village.Hero.ShopPanel>();
            if (panel == null)
            {
                var host = new GameObject("ShopPanelHost");
                panel = host.AddComponent<DeNelle.Village.Hero.ShopPanel>();
            }
            panel.Open(vendor);
        }

        private void CmdOpenArena()
        {
            Debug.Log("[NPC] OpenArena called");
            var panel = FindFirstObjectByType<DeNelle.Village.Arena.ArenaPanel>();
            if (panel == null)
            {
                var host = new GameObject("ArenaPanelHost");
                panel = host.AddComponent<DeNelle.Village.Arena.ArenaPanel>();
            }
            panel.Open();
        }

        private void CmdLearnRecipe(string recipeId)
        {
            Debug.Log($"[NPC] Learned recipe: {recipeId}");
        }

        // ── Quests (WO-290) — delegate to QuestService (Core) ─────────────────
        private void CmdStartQuest(string id)        => DeNelle.Core.Quests.QuestService.Instance?.StartQuest(id);
        private void CmdAdvanceQuest(string id)       => DeNelle.Core.Quests.QuestService.Instance?.AdvanceQuest(id);
        private void CmdCompleteQuest(string id)      => DeNelle.Core.Quests.QuestService.Instance?.CompleteQuest(id);
        private void CmdSetQuestFlag(string id, string flag) => DeNelle.Core.Quests.QuestService.Instance?.SetFlag(id, flag);
        private void CmdGiveKeystone(string name)     => DeNelle.Core.Quests.QuestService.Instance?.GiveKeystone(name);
        private static bool FnHasKeystone(string name) =>
            DeNelle.Core.Quests.QuestService.Instance != null && DeNelle.Core.Quests.QuestService.Instance.HasKeystone(name);
        private static int FnKeystoneCount() =>
            DeNelle.Core.Quests.QuestService.Instance != null ? DeNelle.Core.Quests.QuestService.Instance.KeystoneCount : 0;
    }
}