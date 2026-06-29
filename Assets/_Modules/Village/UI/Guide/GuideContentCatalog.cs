// =============================================================================
// GuideContentCatalog — typed model + loader for the canonical guide-content.json
// (WO-588 — the in-game opt-in Game Guide / tutorial codex).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The guide's COPY is CONTENT, not code. This loader reads
// guide-content.json (Resources first, StreamingAssets fallback) through the
// same WebGL-safe CanonicalJson seam every other catalog uses, and hydrates a
// cached list of typed GuideSection records. Adding / editing a section is a
// DATA change — no code edit. Mirrors BuildingCatalog.LoadCatalog exactly.
//
// Schema (guide-content.json):
//   { "version": 1, "sections": [ { id, tab, title, status, body[], tips[] } ] }
//
// No silent failure (§12): a missing file or a parse-to-empty is logged via
// FlowTrace.Fail so a content break self-reports instead of a blank panel.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// One guide section — the C# port of an entry in <c>guide-content.json</c>.
    /// Hydrated by <see cref="GuideContentCatalog"/>; never constructed inline.
    /// </summary>
    [Serializable]
    public sealed class GuideSection
    {
        /// <summary>Stable id — e.g. <c>basic_mechanics</c>.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>The LEFT tab-rail label — e.g. "Basic Mechanics".</summary>
        [JsonProperty("tab")] public string Tab;

        /// <summary>The body header shown on the right — usually the full title.</summary>
        [JsonProperty("title")] public string Title;

        /// <summary>"live" (shipped) or "coming" (not-yet-built — tagged in the UI).</summary>
        [JsonProperty("status")] public string Status = "live";

        /// <summary>The body paragraphs, in order.</summary>
        [JsonProperty("body")] public List<string> Body = new List<string>();

        /// <summary>Optional "Tips" bullet lines.</summary>
        [JsonProperty("tips")] public List<string> Tips = new List<string>();

        /// <summary>True when this section documents a not-yet-built system.</summary>
        [JsonIgnore] public bool IsComing =>
            !string.IsNullOrEmpty(Status) &&
            Status.Trim().Equals("coming", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Top-level shape of guide-content.json.</summary>
    [Serializable]
    public sealed class GuideContentData
    {
        [JsonProperty("version")] public int Version = 1;
        [JsonProperty("sections")] public List<GuideSection> Sections = new List<GuideSection>();
    }

    /// <summary>
    /// Loads + caches guide-content.json. <see cref="Sections"/> is the ordered,
    /// non-null section list the guide panel renders. <see cref="Reload"/> forces a
    /// fresh read (tests / the data regression).
    /// </summary>
    public static class GuideContentCatalog
    {
        /// <summary>StreamingAssets-relative path; mirrored under Resources for WebGL.</summary>
        public const string StreamingRelativePath = "Data/Canonical/guide-content.json";

        private static GuideContentData _data;

        /// <summary>The ordered guide sections (never null; empty on a load failure).</summary>
        public static IReadOnlyList<GuideSection> Sections
        {
            get
            {
                EnsureLoaded();
                return _data.Sections;
            }
        }

        /// <summary>Forces a re-read of guide-content.json.</summary>
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

        private static GuideContentData Load()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<GuideContentData>(json);
                    if (parsed != null && parsed.Sections != null)
                    {
                        // Drop any wholly-empty rows so the rail never shows a blank tab.
                        parsed.Sections.RemoveAll(s => s == null || string.IsNullOrEmpty(s.Tab));
                        return parsed;
                    }
                    FlowTrace.Fail("UI", "GuideContentCatalog: guide-content.json parsed empty (mapping break or empty 'sections').");
                }
                else
                {
                    FlowTrace.Fail("UI", "GuideContentCatalog: guide-content.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("UI", "GuideContentCatalog: failed to read guide-content.json: " + ex.Message);
            }

            return new GuideContentData { Sections = new List<GuideSection>() };
        }
    }
}
