// =============================================================================
// DialogueCommandSink (DeNelle.Village) — wires OUR dialogue's commands + conditions
// to the game, registered at boot (WO-455). Verbs route DIRECTLY to PanelRouter /
// QuestService — no Yarn, no source generator, no register-once-globally constraint.
// -----------------------------------------------------------------------------
// PHASED: the common panel + quest verbs are wired here; the full ~40-verb parity
// (camera/audio/movement/combat/pets) lands as narrative is converted off Yarn.
// Unknown verbs FlowTrace.Warn (no silent failure). Flag-gated on CustomDialogue.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Dialogue;
using DeNelle.Core.UI;
using DeNelle.Core.Quests;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    public sealed class DialogueCommandSink : IDialogueCommandSink, IDialogueConditionSource
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (!DeNelle.Core.FeatureFlags.CustomDialogue) return; // migration flag (default off)
            var sink = new DialogueCommandSink();
            // Fully-qualify: DeNelle.Village also has a (Yarn) DialogueService that would shadow this.
            DeNelle.Core.Dialogue.DialogueService.RegisterSink(sink);
            DeNelle.Core.Dialogue.DialogueService.RegisterConditions(sink);
            FlowTrace.Step("Dialogue", "Custom dialogue command sink registered (Yarn-free).");
        }

        // ── Commands → direct panel/quest calls ──────────────────────────────────
        public void Run(string verb, IReadOnlyList<string> args)
        {
            if (string.IsNullOrEmpty(verb)) return;
            string a0 = (args != null && args.Count > 0) ? args[0] : null;
            switch (verb)
            {
                case "OpenRumorBoard": PanelRouter.Open(PanelId.RumorBoard); break;
                case "OpenUpgrade":    PanelRouter.Open(PanelId.BuildingUpgrade, a0); break;
                case "OpenShop":       PanelRouter.Open(PanelId.PartyShop, a0); break;
                case "OpenCraft":      PanelRouter.Open(PanelId.Crafting); break;
                case "OpenTalents":    PanelRouter.Open(PanelId.HeroTalents); break;
                case "OpenCosmetics":  PanelRouter.Open(PanelId.CosmeticShop); break;
                case "OpenPetSkills":  PanelRouter.Open(PanelId.PetSkillTree); break;

                case "StartQuest":   { var q = QuestService.Instance; if (q != null && a0 != null) q.StartQuest(a0); } break;
                case "AdvanceQuest": { var q = QuestService.Instance; if (q != null && a0 != null) q.AdvanceQuest(a0); } break;
                case "GiveKeystone": { var q = QuestService.Instance; if (q != null && a0 != null) q.GiveKeystone(a0); } break;
                case "SetFlag":      { var q = QuestService.Instance; if (q != null && args != null && args.Count >= 2) q.SetFlag(args[0], args[1]); } break;

                default:
                    FlowTrace.Warn("Dialogue",
                        $"command sink: verb '{verb}' not yet wired (custom-dialogue migration — Phase 2/3).");
                    break;
            }
        }

        // ── Conditions → quest/keystone state ────────────────────────────────────
        // Keys: quest_<id>_active · quest_<id>_done · keystone_<name>. Unknown => false.
        public bool Check(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            var svc = QuestService.Instance;
            if (svc == null) return false;

            const string qp = "quest_";
            if (condition.StartsWith(qp) && condition.EndsWith("_active"))
                return svc.IsActive(condition.Substring(qp.Length, condition.Length - qp.Length - "_active".Length));
            if (condition.StartsWith(qp) && condition.EndsWith("_done"))
                return svc.IsCompleted(condition.Substring(qp.Length, condition.Length - qp.Length - "_done".Length));
            if (condition.StartsWith("keystone_"))
                return svc.HasKeystone(condition.Substring("keystone_".Length));

            FlowTrace.Warn("Dialogue", $"condition '{condition}' unknown — treated false.");
            return false;
        }
    }
}
