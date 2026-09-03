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

    /// <summary>Dialogue ids into dialogues.json — the GUIDE (the player's first
    /// pet-Echo, WO-1012 P2; lines author the "{guide}" token resolved via
    /// TutorialGuide) speaks through the SAME custom dialogue system +
    /// master-frame template as every NPC (no bubble fork).</summary>
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

    /// <summary>
    /// WO-1340 — ONE hop of a contextual teach step's ROUTE: while the hint is live, the
    /// spotlight/pointer RE-POINTS to <see cref="Highlight"/> the moment
    /// <see cref="Signal"/> is raised. This is what lets a single hint walk a player down
    /// a multi-tap path (bar face -> deck -> panel) without a chain of separate one-shots,
    /// each of which would have to guess a trigger and could fire out of order.
    ///
    /// An EMPTY/absent Highlight means "stop pointing" — the player has arrived and the
    /// spotlight should get out of the way of the screen they now need to read.
    ///
    /// Purely presentational: a hop that never fires costs nothing and can never hold the
    /// step, because completion is the step's own completion.signal and the escape bound
    /// runs regardless (TutorialFlow.TickContextual).
    /// </summary>
    [Serializable]
    public sealed class TutorialRouteHop
    {
        [JsonProperty("signal")] public string Signal;
        [JsonProperty("highlight")] public string Highlight;
    }

    /// <summary>Kit objective-banner text + optional progress count.</summary>
    [Serializable]
    public sealed class TutorialObjective
    {
        [JsonProperty("text")] public string Text;
        [JsonProperty("count")] public int Count;
    }

    /// <summary>One-time grants applied by the interpreter, both on step ENTER —
    /// prepaidTower (WO-T3) funds the guided build; starterPet (WO-1012 P2) wakes the
    /// pet-Echo GUIDE at the ARRIVE beat, before its first line plays.</summary>
    [Serializable]
    public sealed class TutorialGrant
    {
        [JsonProperty("prepaidTower")] public bool PrepaidTower;
        /// <summary>On ENTER of this step, grant the starter pet — the GUIDE
        /// (PetAcquisitionService.Acquire + GameState.StarterPetId). WO-1012 P2 re-ruling
        /// 2026-08-09: authored on the ARRIVE beat (founding_greet) so the pet-Echo exists
        /// before it speaks; history: WO-702 granted it on founding_hollow COMPLETION.
        /// Idempotent per save (tutorial_v2_grant key + Owns check).</summary>
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
        /// <summary>Prebuilt/Default-Town skip (owner ruling 2026-07-24): when the town is already laid
        /// out (GameState.BaseLayout carries the seeded pet-house / collector_lumbermill signature),
        /// this build-teaching step is SKIPPED by the interpreter -- its grants STILL apply (critically
        /// founding_hollow's starterPet, so a Default-Town player is never left pet-less) but its intro
        /// dialogue never plays. A Build-Your-Own (blank template) town leaves BaseLayout empty, so
        /// these steps run in full. No effect on contextual steps.</summary>
        [JsonProperty("skipIfPrebuilt")] public bool SkipIfPrebuilt;
        [JsonProperty("objective")] public TutorialObjective Objective;
        /// <summary>Contextual steps only: fire once per save, ever (persisted via the
        /// SeenTutorials key "tutorial_ctx:&lt;stepId&gt;").</summary>
        [JsonProperty("oneShot")] public bool OneShot;
        /// <summary>WO-1340 — contextual teach steps only: ordered spotlight hand-offs that
        /// follow the player along the route to the thing being taught. Null/empty for every
        /// existing step (the hint just lights <c>highlight[0]</c> and stays there).</summary>
        [JsonProperty("route")] public List<TutorialRouteHop> Route;

        /// <summary>
        /// WO-1340 — TRUE when this contextual hint waits on a REAL GAMEPLAY completion
        /// signal rather than on its own dialogue closing. That distinction is the whole
        /// difference between a hint that says a thing and a beat that teaches it: the
        /// ordinary contextual completes the instant the player dismisses the text box,
        /// which proves only that they closed a box.
        ///
        /// Detected rather than authored as a second flag so the two fields can never
        /// disagree: a contextual step whose completion.signal is anything OTHER than its
        /// own <c>dialogue.ended:&lt;intro&gt;</c> is by definition waiting on the world.
        /// </summary>
        public bool AwaitsGameplayCompletion
        {
            get
            {
                if (!IsContextual) return false;
                string sig = Completion != null ? Completion.Signal : null;
                if (string.IsNullOrEmpty(sig)) return false;
                string intro = Dialogue != null ? Dialogue.Intro : null;
                if (string.IsNullOrEmpty(intro)) return true;
                return !string.Equals(sig, "dialogue.ended:" + intro,
                                      StringComparison.OrdinalIgnoreCase);
            }
        }

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
