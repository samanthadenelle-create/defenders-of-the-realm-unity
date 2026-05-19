// =============================================================================
// LoreFragments — typed records + loader for the canonical lore-fragments.json.
// -----------------------------------------------------------------------------
// Port spec Part 4 (data-extraction protocol): data/lore-fragments.json is a
// shared canonical data file — "Lore-stone text, Alduin's journal fragments,
// Mira's letters — all canon-voice prose". This file is the strongly-typed C#
// mirror — Newtonsoft.Json deserialises the JSON into these PLAIN records.
//
// CANON: every fragment body here is canon prose, lifted verbatim from the
// React v1 source (wandererCopy.ts, healersCottage.lore.ts) and the dungeon
// design docs. This loader NEVER authors or paraphrases lore — it only reads
// and serves what the JSON carries. A fragment flagged `placeholder == true`
// is NOT canon; callers should treat it as unreviewed copy.
//
// LOADER PATTERN: mirrors DeNelle.Dungeons.DungeonLayoutLoader and
// DeNelle.Village.WaveDataLoader — canonical JSON under
// Application.streamingAssetsPath, read via UniTask (async because Android
// packs StreamingAssets inside the APK), parsed by Newtonsoft.Json. See
// unity-decisions.md 2026-05-18 (Newtonsoft over JsonUtility).
//
// JSON is the source of truth (port spec Part 4 "JSON wins"). Bryn.cs reads the
// `bryn-cottage-entry` dialogue fragment; LoreStone.cs maps each lore-stone id
// to its `journal-*` fragment.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// One lore fragment — the deserialised shape of a <c>lore-fragments.json</c>
    /// <c>fragments[]</c> entry. Plain data; carries canon prose verbatim.
    /// </summary>
    [Serializable]
    public sealed class LoreFragment
    {
        /// <summary>Stable id — keys the fragment (e.g. <c>journal-1</c>, <c>bryn-cottage-entry</c>).</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>Fragment kind — <c>"lore-stone"</c> or <c>"dialogue"</c>.</summary>
        [JsonProperty("kind")] public string Kind;

        /// <summary>The in-fiction speaker — e.g. "Alduin the Mournful", "Bryn the Wanderer".</summary>
        [JsonProperty("speaker")] public string Speaker;

        /// <summary>The fragment heading shown on the reading modal.</summary>
        [JsonProperty("title")] public string Title;

        /// <summary>
        /// True when this fragment is NOT canon — an unreviewed placeholder that
        /// must be sourced or cut before ship. Callers may surface a dev warning.
        /// </summary>
        [JsonProperty("placeholder")] public bool Placeholder;

        /// <summary>The fragment body — one paragraph per array entry. Canon prose.</summary>
        [JsonProperty("body")] public string[] Body = Array.Empty<string>();

        /// <summary>The fragment body joined into a single line (for a speech bubble).</summary>
        public string OneLine => Body != null ? string.Join(" ", Body) : string.Empty;
    }

    /// <summary>The deserialised root of <c>lore-fragments.json</c>.</summary>
    [Serializable]
    public sealed class LoreFragmentSet
    {
        /// <summary>Schema version — bumped when the fragment shape changes.</summary>
        [JsonProperty("version")] public int Version;

        /// <summary>Every lore fragment in the file.</summary>
        [JsonProperty("fragments")] public List<LoreFragment> Fragments = new List<LoreFragment>();

        /// <summary>Finds a fragment by id, or null when the set has no such fragment.</summary>
        public LoreFragment Find(string id)
        {
            if (string.IsNullOrEmpty(id) || Fragments == null) return null;
            foreach (var f in Fragments)
                if (f != null && f.Id == id) return f;
            return null;
        }
    }

    /// <summary>
    /// Loads the canonical <c>lore-fragments.json</c> from
    /// <c>StreamingAssets/Data/Canonical/</c>. Reading from StreamingAssets is
    /// async on Android (the files live inside the compressed APK), so the load
    /// returns a <see cref="UniTask"/> — never <c>async void</c> (port spec
    /// Part 3 mandate). Mirrors <see cref="DungeonLayoutLoader"/>.
    /// </summary>
    public static class LoreFragmentsLoader
    {
        /// <summary>StreamingAssets-relative path to the canonical lore fragments.</summary>
        public const string LoreFragmentsRelativePath = "Data/Canonical/lore-fragments.json";

        private static LoreFragmentSet _cached;

        /// <summary>The most recently loaded fragment set, or null before the first load.</summary>
        public static LoreFragmentSet Cached => _cached;

        /// <summary>
        /// Loads and deserialises the lore-fragment set, caching the result.
        /// Returns null and logs an error when the file is missing or malformed.
        /// </summary>
        public static async UniTask<LoreFragmentSet> LoadAsync()
        {
            if (_cached != null) return _cached;

            string fullPath = Path.Combine(
                Application.streamingAssetsPath, LoreFragmentsRelativePath);

            string json = await ReadTextAsync(fullPath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[LoreFragmentsLoader] Could not read '{fullPath}'.");
                return null;
            }

            try
            {
                var set = JsonConvert.DeserializeObject<LoreFragmentSet>(json);
                if (set == null || set.Fragments == null || set.Fragments.Count == 0)
                {
                    Debug.LogError(
                        "[LoreFragmentsLoader] lore-fragments.json deserialised to an empty set.");
                    return null;
                }
                _cached = set;
                return set;
            }
            catch (JsonException ex)
            {
                Debug.LogError(
                    $"[LoreFragmentsLoader] Malformed JSON in lore-fragments.json: {ex.Message}");
                return null;
            }
        }

        /// <summary>Forces a re-read on the next <see cref="LoadAsync"/> (tests / Monday sync).</summary>
        public static void Invalidate()
        {
            _cached = null;
        }

        /// <summary>
        /// Reads a text file. On platforms where StreamingAssets is a plain
        /// directory (editor / Windows / macOS) it reads directly; on Android it
        /// must go through <see cref="UnityWebRequest"/> because the file lives
        /// inside the compressed APK. Same caveat as <see cref="DungeonLayoutLoader"/>.
        /// </summary>
        private static async UniTask<string> ReadTextAsync(string fullPath)
        {
            bool needsWebRequest = fullPath.Contains("://") || fullPath.Contains(":///");

            if (!needsWebRequest)
            {
                if (!File.Exists(fullPath)) return null;
                return await File.ReadAllTextAsync(fullPath);
            }

            using var req = UnityWebRequest.Get(fullPath);
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[LoreFragmentsLoader] UnityWebRequest failed: {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
