// =============================================================================
// ChatPhraseCatalog — loads chat-phrases.json (StreamingAssets/Data/Canonical/)
// and exposes typed lookup over the templated phrase catalogue used by the
// clan team-chat picker.
// -----------------------------------------------------------------------------
// Source spec: docs/clans-and-team-chat-design.md §6.4 (templated-only, no
// free text). The Unity port collapses the React `chat-phrases.ts` +
// `clan-phrases.ts` pair into one JSON file under StreamingAssets so it
// matches the existing pets / daily-quests catalogue pattern.
//
// LOCAL-ONLY STUB. No server, no PHRASE_ID_RE validator on the wire — the
// only consumer is ClanService running in single-player. Mirrors
// PetSkillTreeCatalog.cs in shape so future maintainers find one familiar
// pattern across the catalogues.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.Services
{
    [Serializable]
    public sealed class ChatPhraseCategoryDef
    {
        [JsonProperty("key")]   public string Key;
        [JsonProperty("label")] public string Label;
    }

    [Serializable]
    public sealed class ChatPhraseDef
    {
        [JsonProperty("id")]       public string Id;
        [JsonProperty("category")] public string Category;
        [JsonProperty("text")]     public string Text;
        [JsonProperty("emoji")]    public string Emoji;
    }

    [Serializable]
    public sealed class ChatPhraseCatalogData
    {
        [JsonProperty("version")]    public int Version;
        [JsonProperty("source")]     public string Source;
        [JsonProperty("categories")] public List<ChatPhraseCategoryDef> Categories = new List<ChatPhraseCategoryDef>();
        [JsonProperty("phrases")]    public List<ChatPhraseDef> Phrases = new List<ChatPhraseDef>();
    }

    /// <summary>Static surface over StreamingAssets/Data/Canonical/chat-phrases.json.</summary>
    public static class ChatPhraseCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/chat-phrases.json";
        private static ChatPhraseCatalogData _data;
        private static Dictionary<string, ChatPhraseDef> _byId;

        public static IReadOnlyList<ChatPhraseCategoryDef> Categories
        { get { EnsureLoaded(); return _data.Categories; } }

        public static IReadOnlyList<ChatPhraseDef> Phrases
        { get { EnsureLoaded(); return _data.Phrases; } }

        public static ChatPhraseDef FindPhrase(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            _byId.TryGetValue(id, out var p);
            return p;
        }

        public static bool IsValidPhraseId(string id) => FindPhrase(id) != null;

        /// <summary>Resolved display text, falling back to a neutral glyph if unknown.</summary>
        public static string TextFor(string id)
        {
            var p = FindPhrase(id);
            return p != null ? p.Text : "…";
        }

        public static IEnumerable<ChatPhraseDef> PhrasesByCategory(string categoryKey)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(categoryKey)) yield break;
            foreach (var p in _data.Phrases)
                if (p != null && p.Category == categoryKey) yield return p;
        }

        public static void Reload() { _data = null; _byId = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            var full = Path.Combine(Application.streamingAssetsPath, StreamingRelativePath);
            try
            {
                if (File.Exists(full))
                {
                    var parsed = JsonConvert.DeserializeObject<ChatPhraseCatalogData>(File.ReadAllText(full));
                    if (parsed != null && parsed.Phrases != null && parsed.Phrases.Count > 0)
                    {
                        _data = parsed;
                        _byId = BuildIndex(_data.Phrases);
                        return;
                    }
                    Debug.LogError($"[ChatPhraseCatalog] {StreamingRelativePath} parsed empty.");
                }
                else
                {
                    Debug.LogError($"[ChatPhraseCatalog] file not found at {full}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatPhraseCatalog] Read failed: {ex.Message}");
            }
            _data = new ChatPhraseCatalogData();
            _byId = new Dictionary<string, ChatPhraseDef>(0);
        }

        private static Dictionary<string, ChatPhraseDef> BuildIndex(List<ChatPhraseDef> phrases)
        {
            var d = new Dictionary<string, ChatPhraseDef>(phrases.Count);
            foreach (var p in phrases)
            {
                if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                d[p.Id] = p;
            }
            return d;
        }
    }
}
