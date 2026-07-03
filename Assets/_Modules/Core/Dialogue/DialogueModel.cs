// =============================================================================
// DialogueModel — the data shape of OUR dialogue system (WO-455, replaces Yarn).
// -----------------------------------------------------------------------------
// A dialogue is a graph of NODES. Each node shows ordered LINES (speaker + text),
// fires COMMANDS (game verbs), then branches via OPTIONS (player choices) or an
// auto NEXT, gated by simple CONDITIONS. Authored as JSON under
// Resources/Data/Canonical/dialogue/*.json and loaded WebGL-safe via CanonicalJson
// (Resources dual-copy first), exactly like QuestCatalog. Pure data — the runtime
// (DialogueRunner) walks it; the presentation (DialogueView) renders it.
//
// WHY OUR OWN: YarnSpinner's lifecycle fought us (No-node race, Stop()-teardown
// NRE, transactional misuse). We control this runner end-to-end → no races, and
// the view is styled through our presentation layer like every other panel.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.Dialogue
{
    /// <summary>One spoken line: who says it + what they say. speaker may be empty (narration).</summary>
    [Serializable]
    public sealed class DialogueLine
    {
        [JsonProperty("speaker")] public string Speaker;
        [JsonProperty("text")] public string Text;
    }

    /// <summary>
    /// The NPC-card record for one speaker (owner-ratified card standard, 2026-07-02 audit):
    /// every dialogue card shows NAME + guild/shop AFFILIATION + PORTRAIT. Declared once in
    /// the catalog's top-level "speakers" block and looked up by the line's speaker name —
    /// data-driven, replacing the imperative per-node `portrait` command (which remains as a
    /// back-compat override). portrait = Resources sprite path (e.g. "Portraits/farm");
    /// empty/null portrait = the view renders its styled silhouette placeholder.
    /// </summary>
    [Serializable]
    public sealed class DialogueSpeaker
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("affiliation")] public string Affiliation;
        [JsonProperty("portrait")] public string Portrait;   // Resources path; empty => silhouette
    }

    /// <summary>A player choice. requires = optional condition key (hidden if false);
    /// goto = node id to jump to when chosen (empty/"end" ends the dialogue).</summary>
    [Serializable]
    public sealed class DialogueOption
    {
        [JsonProperty("text")] public string Text;
        [JsonProperty("requires")] public string Requires;   // optional condition gate
        [JsonProperty("goto")] public string Goto;           // target node id ("" / "end" => end)
    }

    /// <summary>A game verb fired by a node (reuses the existing command vocabulary —
    /// OpenShop/StartQuest/PlaySfx/... — but invoked directly, not through Yarn).</summary>
    [Serializable]
    public sealed class DialogueCommand
    {
        [JsonProperty("verb")] public string Verb;
        [JsonProperty("args")] public List<string> Args = new List<string>();
    }

    /// <summary>One node: ordered lines, then commands, then options OR an auto next.
    /// condition (optional) gates whether the node is enterable.</summary>
    [Serializable]
    public sealed class DialogueNode
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("condition")] public string Condition;   // optional enter-gate
        [JsonProperty("lines")] public List<DialogueLine> Lines = new List<DialogueLine>();
        [JsonProperty("commands")] public List<DialogueCommand> Commands = new List<DialogueCommand>();
        [JsonProperty("options")] public List<DialogueOption> Options = new List<DialogueOption>();
        [JsonProperty("next")] public string Next;             // auto-advance node id (no options)
    }

    /// <summary>A complete conversation: an ordered node list; entry = startNode or the first node.</summary>
    [Serializable]
    public sealed class DialogueDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("startNode")] public string StartNode;   // optional; defaults to Nodes[0]
        [JsonProperty("nodes")] public List<DialogueNode> Nodes = new List<DialogueNode>();

        public DialogueNode FindNode(string id)
        {
            if (Nodes == null) return null;
            foreach (var n in Nodes) if (n != null && n.Id == id) return n;
            return null;
        }

        public DialogueNode EntryNode()
        {
            if (!string.IsNullOrEmpty(StartNode)) { var n = FindNode(StartNode); if (n != null) return n; }
            return (Nodes != null && Nodes.Count > 0) ? Nodes[0] : null;
        }
    }

    [Serializable]
    public sealed class DialogueCatalogData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("speakers")] public List<DialogueSpeaker> Speakers = new List<DialogueSpeaker>();
        [JsonProperty("dialogues")] public List<DialogueDef> Dialogues = new List<DialogueDef>();
    }

    /// <summary>Static loader over Resources/StreamingAssets Data/Canonical/dialogue/*.json.
    /// Mirrors QuestCatalog: CanonicalJson reads the Resources dual-copy first (WebGL-safe).</summary>
    public static class DialogueCatalog
    {
        // One file holds the lot for now (dialogues.json). Splitting per-area is a later refinement.
        private const string StreamingRelativePath = "Data/Canonical/dialogue/dialogues.json";

        private static DialogueCatalogData _data;

        public static IReadOnlyList<DialogueDef> Dialogues
        { get { EnsureLoaded(); return _data.Dialogues; } }

        /// <summary>The catalog's speaker records (name + affiliation + portrait). May be empty.</summary>
        public static IReadOnlyList<DialogueSpeaker> Speakers
        { get { EnsureLoaded(); return _data.Speakers ?? (IReadOnlyList<DialogueSpeaker>)new List<DialogueSpeaker>(); } }

        /// <summary>Resolve the card record for a line's speaker name (case-insensitive).
        /// Null when the speaker is unknown/narration — the view falls back gracefully.</summary>
        public static DialogueSpeaker FindSpeaker(string speakerName)
        {
            if (string.IsNullOrEmpty(speakerName)) return null;
            EnsureLoaded();
            var list = _data.Speakers;
            if (list == null) return null;
            foreach (var s in list)
                if (s != null && !string.IsNullOrEmpty(s.Name) &&
                    string.Equals(s.Name, speakerName, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        public static DialogueDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var d in _data.Dialogues) if (d != null && d.Id == id) return d;
            return null;
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
                    var parsed = JsonConvert.DeserializeObject<DialogueCatalogData>(text);
                    if (parsed != null && parsed.Dialogues != null)
                    { _data = parsed; return; }
                    Debug.LogError("[DialogueCatalog] dialogues.json parsed empty.");
                }
                else Debug.LogError($"[DialogueCatalog] dialogues.json not found ({StreamingRelativePath}).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogueCatalog] Failed to read dialogues.json: {ex.Message}");
            }
            _data = new DialogueCatalogData();
        }
    }
}
