// =============================================================================
// TutorialStepModel — the data shape of Tutorial V2 (WO-T1, spec §2.1).
// -----------------------------------------------------------------------------
// The tutorial is a DATA-DRIVEN step registry (tutorial-steps.json) walked by a
// thin interpreter (DeNelle.Village.TutorialFlow). Each step declares a trigger,
// dialogue ids (into dialogues.json), highlight-registry targets, ONE completion
// signal (TutorialSignals bus id), and a skippable flag. Contextual one-shot
// steps ride the SAME registry with flowId "contextual" + oneShot:true (spec
// CREATIVE SCOPE — no second system).
//
// Authored under Data/Canonical/tutorial/tutorial-steps.json (Resources +
// StreamingAssets mirrors, byte-identical) and loaded WebGL-safe via
// CanonicalJson — the exact DialogueCatalog/QuestCatalog convention.
// Pure data — the interpreter owns zero content, this file owns zero behavior.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Data;

namespace DeNelle.Core.Tutorial
{
    /// <summary>How a step arms. type: "prev_complete" (default chain),
    /// "scene_enter" (+ scene name), or "signal" (+ a TutorialSignals bus id).</summary>
    [Serializable]
    public sealed class TutorialTrigger
    {
        [JsonProperty("type")] public string Type = "prev_complete";
        [JsonProperty("signal")] public string Signal;   // for type "signal"
        [JsonProperty("scene")] public string Scene;     // for type "scene_enter"
    }

    /// <summary>Dialogue ids into dialogues.json — Sylas speaks through the SAME
    /// custom dialogue system + master-frame template as every NPC (no bubble fork).</summary>
    [Serializable]
    public sealed class TutorialDialogueRef
    {
        [JsonProperty("intro")] public string Intro;
        [JsonProperty("outro")] public string Outro;
    }

    /// <summary>The step's completion condition: ONE TutorialSignals bus id.</summary>
    [Serializable]
    public sealed class TutorialCompletion
    {
        [JsonProperty("signal")] public string Signal;
    }

    /// <summary>Kit objective-banner text + optional progress count.</summary>
    [Serializable]
    public sealed class TutorialObjective
    {
        [JsonProperty("text")] public string Text;
        [JsonProperty("count")] public int Count;
    }

    /// <summary>One-time grants applied by the interpreter (prepaidTower on step ENTER —
    /// WO-T3 funds the guided build; starterPet on step COMPLETION — WO-702: the reward
    /// follows the placement, granting the starter pet when the Echo Hollow is placed).</summary>
    [Serializable]
    public sealed class TutorialGrant
    {
        [JsonProperty("prepaidTower")] public bool PrepaidTower;
        /// <summary>WO-702 founding arc: on COMPLETION of this step, grant the starter pet
        /// (PetAcquisitionService.Acquire + GameState.StarterPetId) with a visible birth at
        /// the placed Echo Hollow. Idempotent per save (tutorial_v2_grant key + Owns check).</summary>
        [JsonProperty("starterPet")] public bool StarterPet;
    }

    /// <summary>One declarative tutorial step (mandatory-chain or contextual one-shot).</summary>
    [Serializable]
    public sealed class TutorialStepDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("order")] public int Order;
        [JsonProperty("scene")] public string Scene;
        /// <summary>Optional per-step flow override; "contextual" = the just-in-time
        /// one-shot registry (never gates, never pauses pressure). Empty = the file's flow.</summary>
        [JsonProperty("flowId")] public string FlowId;
        [JsonProperty("trigger")] public TutorialTrigger Trigger = new TutorialTrigger();
        [JsonProperty("pausePressure")] public bool PausePressure;
        [JsonProperty("dialogue")] public TutorialDialogueRef Dialogue = new TutorialDialogueRef();
        [JsonProperty("highlight")] public List<string> Highlight = new List<string>();
        [JsonProperty("grant")] public TutorialGrant Grant;
        [JsonProperty("completion")] public TutorialCompletion Completion = new TutorialCompletion();
        [JsonProperty("skippable")] public bool Skippable;
        [JsonProperty("objective")] public TutorialObjective Objective;
        /// <summary>Contextual steps only: fire once per save, ever (persisted via the
        /// SeenTutorials key "tutorial_ctx:&lt;stepId&gt;").</summary>
        [JsonProperty("oneShot")] public bool OneShot;

        public bool IsContextual =>
            string.Equals(FlowId, "contextual", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public sealed class TutorialStepsData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("flowId")] public string FlowId = "ftue_v2";
        [JsonProperty("steps")] public List<TutorialStepDef> Steps = new List<TutorialStepDef>();
    }

    /// <summary>Static loader over Data/Canonical/tutorial/tutorial-steps.json.
    /// Mirrors DialogueCatalog: CanonicalJson reads the Resources dual-copy first (WebGL-safe).</summary>
    public static class TutorialStepCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/tutorial/tutorial-steps.json";

        private static TutorialStepsData _data;

        /// <summary>The registry's flow id (e.g. "ftue_v2").</summary>
        public static string FlowId { get { EnsureLoaded(); return _data.FlowId; } }

        /// <summary>Every authored step, mandatory + contextual, file order.</summary>
        public static IReadOnlyList<TutorialStepDef> All
        { get { EnsureLoaded(); return _data.Steps; } }

        /// <summary>The mandatory chain, sorted by ascending order.</summary>
        public static List<TutorialStepDef> MandatorySteps()
        {
            EnsureLoaded();
            var list = new List<TutorialStepDef>();
            foreach (var s in _data.Steps)
                if (s != null && !s.IsContextual) list.Add(s);
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
            return list;
        }

        /// <summary>The contextual (flowId "contextual") one-shot steps, file order.</summary>
        public static List<TutorialStepDef> ContextualSteps()
        {
            EnsureLoaded();
            var list = new List<TutorialStepDef>();
            foreach (var s in _data.Steps)
                if (s != null && s.IsContextual) list.Add(s);
            return list;
        }

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            try
            {
                string text = CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(text))
                {
                    var parsed = JsonConvert.DeserializeObject<TutorialStepsData>(text);
                    if (parsed != null && parsed.Steps != null)
                    { _data = parsed; return; }
                    Debug.LogError("[TutorialStepCatalog] tutorial-steps.json parsed empty.");
                }
                else Debug.LogError($"[TutorialStepCatalog] tutorial-steps.json not found ({StreamingRelativePath}).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TutorialStepCatalog] Failed to read tutorial-steps.json: {ex.Message}");
            }
            _data = new TutorialStepsData();
        }
    }
}
