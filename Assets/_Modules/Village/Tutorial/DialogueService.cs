// =============================================================================
// DialogueService (DeNelle.Village) — the ONE launch seam for game dialogue.
// -----------------------------------------------------------------------------
// YARN REMOVED (WO-557, full removal). This is now a thin Yarn-FREE compatibility
// shim that forwards every legacy call site (NPC talk, companion meeting, intro,
// structure interaction, tutorial) to OUR code-built dialogue stack
// (DeNelle.Core.Dialogue.DialogueService + dialogues.json). No DialogueRunner,
// no YarnProject, no shared prefab, no command bridge — the verbs/conditions run
// through DialogueCommandSink (registered at boot under ff.customdialogue).
//
// CONVERSATIONS run as data-driven dialogue (Play / Play-by-id). TRANSACTIONS
// (shop / upgrade / training menus) are NOT dialogue — PlayStructure opens the
// building's panel DIRECTLY (the "transactions = direct panels" rule). Every
// cross-call is null-guarded so a content gap logs and returns false, never throws.
// =============================================================================

using DeNelle.Core.UI;
using DeNelle.Core.Dialogue;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Game-wide entry point for starting dialogue. Forwards to the Yarn-free
    /// DeNelle.Core.Dialogue stack; structure (building) interactions that are
    /// transactions route straight to their panel instead of a conversation.
    /// </summary>
    public static class DialogueService
    {
        /// <summary>True while any dialogue is currently playing (custom runner).</summary>
        public static bool IsRunning => DeNelle.Core.Dialogue.DialogueService.IsRunning;

        /// <summary>
        /// True if a conversation with this id is authored in dialogues.json. Callers
        /// gate OPTIONAL beats (e.g. a welcome line that may not exist yet) with this
        /// so an unauthored id is a clean no-op, never a fault.
        /// </summary>
        public static bool NodeExists(string node)
        {
            if (string.IsNullOrEmpty(node)) return false;
            return DialogueCatalog.Find(node) != null;
        }

        /// <summary>
        /// Start the conversation <paramref name="node"/> on the custom dialogue
        /// runner. Returns false (and logs) if the id isn't authored in dialogues.json
        /// or a dialogue is already running (an in-progress line is never interrupted).
        /// </summary>
        public static bool Play(string node)
        {
            if (string.IsNullOrEmpty(node))
            {
                FlowTrace.Warn("UI", "DialogueService.Play called with an empty node name — ignored.");
                return false;
            }
            if (IsRunning)
            {
                FlowTrace.Warn("UI", $"DialogueService.Play: a dialogue is already running — '{node}' not started.");
                return false;
            }
            if (DialogueCatalog.Find(node) == null)
            {
                FlowTrace.Warn("UI", $"DialogueService.Play: conversation '{node}' is not authored in dialogues.json — skipped.");
                return false;
            }
            return DeNelle.Core.Dialogue.DialogueService.Play(node);
        }

        /// <summary>The structureId of the most recent <see cref="PlayStructure"/> call.
        /// One interaction runs at a time, so panels that need the focused building read this.</summary>
        public static string CurrentStructureId { get; private set; }

        /// <summary>The player-facing sign LABEL of the most recent <see cref="PlayStructure"/>
        /// call (e.g. "Jeweler", "Marketplace") for the opened panel's header.</summary>
        public static string CurrentStructureName { get; private set; }

        /// <summary>
        /// Open a building/structure interaction. CONVERSATIONAL structures (e.g. the
        /// Echo Hollow "pet-house") run as authored dialogue; everything else is a
        /// TRANSACTION and opens its panel directly (shop for shoppable vendors). A
        /// structure with neither returns false so the caller's own panel fallback runs.
        /// </summary>
        public static bool PlayStructure(string structureId, string displayName = null)
        {
            if (string.IsNullOrEmpty(structureId)) return false;
            CurrentStructureId = structureId;
            CurrentStructureName = displayName;

            // 1) Conversational structure authored in dialogues.json -> custom runner.
            if (DialogueCatalog.Find(structureId) != null)
            {
                if (IsRunning) return false; // don't interrupt an active line
                return DeNelle.Core.Dialogue.DialogueService.Play(structureId);
            }

            // 2) Transaction structures -> direct panels (Yarn StructureMenu retired).
            //    A shoppable vendor opens the gear store; non-shoppable structures fall
            //    through (false) so the building's own TryPanelFor mapping handles them.
            var entry = BuildingCatalog.Find(structureId);
            if (entry != null && entry.IsShoppable)
            {
                // Ticket F8-14 ("disable shopping" during the wave): the vendor NPCs are
                // hidden by CastleVendorWaveHider, so Talk is normally unreachable — this
                // guards any remaining direct shop route on the SAME combat authority the
                // townsfolk flee on. Warn (never a silent no-op) + surface the reason.
                if (AmbientNPC.IsCombatActive)
                {
                    FlowTrace.Warn("UI",
                        $"PlayStructure('{structureId}'): shop open BLOCKED — combat active (shops closed during the assault).");
                    BuildFeedbackToast.Show("Shops closed during the assault!");
                    return true;   // handled (blocked + toast) — don't fall through to a panel fallback
                }
                bool opened = PanelRouter.Open(PanelId.PartyShop, structureId);
                if (opened) FlowTrace.Step("UI", $"DialogueService.PlayStructure('{structureId}') -> shop panel (transaction).");
                return opened;
            }

            FlowTrace.Step("UI", $"DialogueService.PlayStructure('{structureId}') has no conversation/shop — caller opens its mapped panel.");
            return false;
        }

        /// <summary>Stop the current dialogue immediately (walk-away / auto-close).
        /// Synchronous + race-free in the custom runner — no Yarn "No node" hazard.</summary>
        public static void Stop()
        {
            if (DeNelle.Core.Dialogue.DialogueService.IsRunning)
            {
                FlowTrace.Step("UI", "Dialogue ended via DialogueService.Stop() (walk-away/auto-close).");
                DeNelle.Core.Dialogue.DialogueService.Stop();
            }
        }

        // ── New-Game dialogue reset (DeNelle.Core decoupling hook) ────────────
        // Onboarding's "Start New" calls DeNelle.Core.DialogueResetService.ResetForNewGame(),
        // which invokes the hook we register here. The custom runner keeps no persisted
        // variable storage (state lives in QuestService / GameState), so the reset just
        // stops any active conversation; the event-bus latches are cleared by Core.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterResetHook()
        {
            DeNelle.Core.DialogueResetService.YarnVariableClear = Stop;
        }
    }
}
