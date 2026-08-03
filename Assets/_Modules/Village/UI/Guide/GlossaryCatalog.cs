// =============================================================================
// GlossaryCatalog — typed model + loader for the canonical glossary.json
// (owner request 2026-08-02: "make sure the full glossary and help guide is in
// settings if needed"). The GUIDE existed; a glossary did not.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The glossary's COPY is CONTENT, not code. This loader reads glossary.json
// (Resources first, StreamingAssets fallback) through the same WebGL-safe
// CanonicalJson seam every other catalog uses — a byte-for-byte mirror of
// GuideContentCatalog, deliberately, so there is ONE shape to learn.
//
// Schema (glossary.json):
//   { "version": 1,
//     "groups": [ { id, tab, title, intro } ],
//     "terms":  [ { term, group, definition } ] }
//
// It does NOT own a panel. GuideVM projects these groups into extra
// GuideSections appended to the Game Guide's left rail, so the glossary opens
// exactly where the player already looks for help (Settings -> Game Guide) and
// no new uGUI is hand-rolled anywhere.
//
// No silent failure (§12): a missing file or a parse-to-empty is logged via
// FlowTrace.Fail so a content break self-reports instead of a blank tab.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>One glossary heading — the rail tab a set of terms lives under.</summary>
    [Serializable]
    public sealed class GlossaryGroup
    {
        /// <summary>Stable id — e.g. <c>world</c>. Referenced by <see cref="GlossaryTerm.Group"/>.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>The LEFT tab-rail label — e.g. "Glossary: World".</summary>
        [JsonProperty("tab")] public string Tab;

        /// <summary>The body header shown on the right.</summary>
        [JsonProperty("title")] public string Title;

        /// <summary>Optional one-line lead-in above the terms.</summary>
        [JsonProperty("intro")] public string Intro;
    }

    /// <summary>One defined term.</summary>
    [Serializable]
    public sealed class GlossaryTerm
    {
        /// <summary>The word as the player sees it — e.g. "Heart of Elarion".</summary>
        [JsonProperty("term")] public string Term;

        /// <summary>The <see cref="GlossaryGroup.Id"/> this term files under.</summary>
        [JsonProperty("group")] public string Group;

        /// <summary>The player-facing definition. Sourced from canon, never invented.</summary>
        [JsonProperty("definition")] public string Definition;
    }

    /// <summary>Top-level shape of glossary.json.</summary>
    [Serializable]
    public sealed class GlossaryData
    {
        [JsonProperty("version")] public int Version = 1;
        [JsonProperty("groups")] public List<GlossaryGroup> Groups = new List<GlossaryGroup>();
        [JsonProperty("terms")] public List<GlossaryTerm> Terms = new List<GlossaryTerm>();
    }

    /// <summary>
    /// Loads + caches glossary.json. <see cref="Groups"/> / <see cref="Terms"/> are the
    /// ordered, non-null lists the guide rail renders. <see cref="Reload"/> forces a
    /// fresh read (tests / the data regression).
    /// </summary>
    public static class GlossaryCatalog
    {
        /// <summary>StreamingAssets-relative path; mirrored under Resources for WebGL.</summary>
        public const string StreamingRelativePath = "Data/Canonical/glossary.json";

        private static GlossaryData _data;

        /// <summary>The ordered glossary groups (never null; empty on a load failure).</summary>
        public static IReadOnlyList<GlossaryGroup> Groups
        {
            get { EnsureLoaded(); return _data.Groups; }
        }

        /// <summary>Every term, in file order (never null; empty on a load failure).</summary>
        public static IReadOnlyList<GlossaryTerm> Terms
        {
            get { EnsureLoaded(); return _data.Terms; }
        }

        /// <summary>Total defined terms — the number the guide rail can show.</summary>
        public static int TermCount
        {
            get { EnsureLoaded(); return _data.Terms.Count; }
        }

        /// <summary>The terms filed under <paramref name="groupId"/>, in file order.</summary>
        public static List<GlossaryTerm> TermsIn(string groupId)
        {
            var result = new List<GlossaryTerm>();
            if (string.IsNullOrEmpty(groupId)) return result;
            EnsureLoaded();
            foreach (var t in _data.Terms)
            {
                if (t == null || string.IsNullOrEmpty(t.Group)) continue;
                if (string.Equals(t.Group.Trim(), groupId.Trim(), StringComparison.OrdinalIgnoreCase))
                    result.Add(t);
            }
            return result;
        }

        /// <summary>Case-insensitive term lookup; null when the word is not defined.</summary>
        public static GlossaryTerm Find(string term)
        {
            if (string.IsNullOrEmpty(term)) return null;
            EnsureLoaded();
            foreach (var t in _data.Terms)
            {
                if (t == null || string.IsNullOrEmpty(t.Term)) continue;
                if (string.Equals(t.Term.Trim(), term.Trim(), StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }

        /// <summary>Forces a re-read of glossary.json.</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = Load();
        }

        private static GlossaryData Load()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<GlossaryData>(json);
                    if (parsed != null && parsed.Groups != null && parsed.Terms != null)
                    {
                        // Drop rows that could only render as a blank tab / blank line.
                        parsed.Groups.RemoveAll(g => g == null || string.IsNullOrEmpty(g.Tab));
                        parsed.Terms.RemoveAll(t => t == null ||
                                                    string.IsNullOrEmpty(t.Term) ||
                                                    string.IsNullOrEmpty(t.Definition));
                        return parsed;
                    }
                    FlowTrace.Fail("UI", "GlossaryCatalog: glossary.json parsed empty (mapping break or empty 'groups'/'terms').");
                }
                else
                {
                    FlowTrace.Fail("UI", "GlossaryCatalog: glossary.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("UI", "GlossaryCatalog: failed to read glossary.json: " + ex.Message);
            }

            return new GlossaryData
            {
                Groups = new List<GlossaryGroup>(),
                Terms = new List<GlossaryTerm>()
            };
        }
    }
}
