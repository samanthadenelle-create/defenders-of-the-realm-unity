// =============================================================================
// EchoGuideCatalog -- the 24 Echo Guide MEMORY LINES (WO-1380).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE MECHANIC (docs/CREATIVE_CANON_ELARION_2026-09-04.md sec.7): before each
// expedition the player picks an Echo Guide. THE ECHO DOES NOT FIGHT. IT REMEMBERS.
// This file is the read-only projection of that authored copy; the world body it
// speaks through is still owned solely by EchoWorldPresence (WO-1108 Lane B) --
// there is no second spawner anywhere in this lane.
//
// WHY A JSON (and not a code table like EchoRosterCatalog): the roster is IDENTITY
// and lives in code; these 24 lines are COPY, they are the thing most likely to be
// re-authored, and the owner is the author. They go through the ordinary canonical
// dual-copy rail (Resources wins at runtime, StreamingAssets is the desktop
// fallback + source) exactly like garrison-recipes.json -- no new loader, no new
// spelling.
//
// SCOPE FENCE, owner-ruled 2026-09-04 and enforced by EchoGuideMemoryRegression:
//   * NARRATIVE ONLY. A Guide grants NO stat, NO yield, NO combat effect in V1.
//     There is deliberately no bonus/multiplier/modifier field in this schema, and
//     nothing in this file may compute one.
//   * ALL 24 LINES SHIP OR THE FEATURE DOES NOT. Six Echoes x four targets. The
//     regression counts them and FAILS below 24.
//
// A missing file / a short file is a LOUD, LOGGED, EMPTY catalog -- never a throw
// and never a silent blank (CLAUDE.md sec.12). Every anomaly self-reports through
// FlowTrace under the "EchoGuide" system tag.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World.Camps
{
    /// <summary>One authored memory line: what ONE Echo recognises at ONE raid target.</summary>
    public sealed class EchoGuideMemory
    {
        /// <summary>Stable roster id from EchoRosterCatalog (echo-voidwing-raven). Never renamed.</summary>
        [JsonProperty("echoId")] public string EchoId;
        /// <summary>Canonical raid-target id (raider_camp_small .. iron_bastion).</summary>
        [JsonProperty("targetId")] public string TargetId;
        /// <summary>The authored ASCII line the Guide speaks. Player-facing copy.</summary>
        [JsonProperty("line")] public string Line;
    }

    /// <summary>One raid target the Guides can recognise, plus the spellings that resolve to it.</summary>
    public sealed class EchoGuideTarget
    {
        /// <summary>Canonical target id -- the live scene-config id where one exists.</summary>
        [JsonProperty("targetId")] public string TargetId;
        /// <summary>Creative-canon display name (canon sec.3). Player-facing.</summary>
        [JsonProperty("displayName")] public string DisplayName;
        /// <summary>Extra spellings that resolve to this target -- today the raid SCENE name, so a
        /// caller holding either a config id or a scene name gets an answer without knowing which.</summary>
        [JsonProperty("aliases")] public List<string> Aliases;
    }

    /// <summary>Deserialization shape for echo-guide-memories.json.</summary>
    internal sealed class EchoGuideMemoryFile
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion;
        [JsonProperty("defaultGuideEchoId")] public string DefaultGuideEchoId;
        [JsonProperty("targets")] public List<EchoGuideTarget> Targets;
        [JsonProperty("memories")] public List<EchoGuideMemory> Memories;
    }

    /// <summary>Loads + caches the authored Echo Guide memory lines. Read-only; grants nothing.</summary>
    public static class EchoGuideCatalog
    {
        /// <summary>StreamingAssets-relative path (CanonicalJson strips the extension for Resources).</summary>
        public const string StreamingRelativePath = "Data/Canonical/echo-guide-memories.json";

        /// <summary>Six Echoes x four raid targets. The owner ruling is that all of them ship.</summary>
        public const int ExpectedLineCount = 24;

        /// <summary>Corvin, the Void Echo -- the scout who ranged the far dark for Elarion.
        /// The natural first Guide and the only Echo who has already been out there. Used when
        /// the JSON omits defaultGuideEchoId so the default survives a data mistake.</summary>
        public const string FallbackDefaultGuideEchoId = "echo-voidwing-raven";

        private const string Sys = "EchoGuide";

        private static List<EchoGuideMemory> _memories;
        private static List<EchoGuideTarget> _targets;
        private static string _defaultGuideEchoId;

        /// <summary>Every authored line (empty list when the file is missing -- never null).</summary>
        public static IReadOnlyList<EchoGuideMemory> All { get { EnsureLoaded(); return _memories; } }

        /// <summary>Every raid target the Guides have lines for (never null).</summary>
        public static IReadOnlyList<EchoGuideTarget> Targets { get { EnsureLoaded(); return _targets; } }

        /// <summary>The Echo the Guide picker starts on when the player has never chosen (Corvin).</summary>
        public static string DefaultGuideEchoId
        {
            get
            {
                EnsureLoaded();
                return string.IsNullOrEmpty(_defaultGuideEchoId)
                    ? FallbackDefaultGuideEchoId : _defaultGuideEchoId;
            }
        }

        /// <summary>Force a fresh read (after editing the JSON, or from a regression).</summary>
        public static void Reload()
        {
            _memories = null;
            _targets = null;
            _defaultGuideEchoId = null;
            EnsureLoaded();
        }

        /// <summary>
        /// Resolve a target id OR a raid scene name to the canonical target id.
        /// Returns null (WARNED, never thrown) when nothing matches, so a caller can say
        /// "this target has no authored memories yet" instead of showing a blank band.
        /// </summary>
        public static string ResolveTargetId(string idOrSceneName)
        {
            if (string.IsNullOrEmpty(idOrSceneName)) return null;
            EnsureLoaded();
            string probe = idOrSceneName.Trim();

            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (t == null || string.IsNullOrEmpty(t.TargetId)) continue;
                if (string.Equals(t.TargetId, probe, StringComparison.OrdinalIgnoreCase)) return t.TargetId;
                if (t.Aliases == null) continue;
                for (int a = 0; a < t.Aliases.Count; a++)
                    if (string.Equals(t.Aliases[a], probe, StringComparison.OrdinalIgnoreCase)) return t.TargetId;
            }

            FlowTrace.Warn(Sys,
                "ResolveTargetId(" + probe + ") matched NO authored target (known: " + DescribeTargets() +
                "). The Guide band says it has no memory of this place rather than render blank.");
            return null;
        }

        /// <summary>Display name for a resolved target id, or null when unknown.</summary>
        public static string DisplayNameFor(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return null;
            EnsureLoaded();
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i] != null &&
                    string.Equals(_targets[i].TargetId, targetId, StringComparison.OrdinalIgnoreCase))
                    return _targets[i].DisplayName;
            return null;
        }

        /// <summary>
        /// The one line <paramref name="echoId"/> speaks at <paramref name="targetIdOrSceneName"/>.
        /// Null when either side is unknown -- WARNED with both keys, because a silent null here is
        /// exactly the "fires for two Guides, silent for four" failure the owner ruled against.
        /// </summary>
        public static string LineFor(string echoId, string targetIdOrSceneName)
        {
            if (string.IsNullOrEmpty(echoId)) return null;
            string targetId = ResolveTargetId(targetIdOrSceneName);
            if (string.IsNullOrEmpty(targetId)) return null;

            EnsureLoaded();
            for (int i = 0; i < _memories.Count; i++)
            {
                var m = _memories[i];
                if (m == null) continue;
                if (string.Equals(m.EchoId, echoId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
                    return m.Line;
            }

            FlowTrace.Warn(Sys,
                "LineFor(echo=" + echoId + ", target=" + targetId + ") found NO authored line among " +
                _memories.Count + " row(s). A Guide with nothing to say reads as broken -- all " +
                ExpectedLineCount + " lines must ship (WO-1380 scope fence).");
            return null;
        }

        private static string DescribeTargets()
        {
            if (_targets == null || _targets.Count == 0) return "(none)";
            var parts = new List<string>(_targets.Count);
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i] != null) parts.Add(_targets[i].TargetId);
            return string.Join(",", parts.ToArray());
        }

        private static void EnsureLoaded()
        {
            if (_memories != null) return;
            _memories = new List<EchoGuideMemory>();
            _targets = new List<EchoGuideTarget>();

            FlowTrace.Step(Sys, "EnsureLoaded -- reading echo-guide-memories.json.");
            try
            {
                string text = CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(text))
                {
                    FlowTrace.Fail(Sys,
                        StreamingRelativePath + " not found/empty -- 0 memory lines. The Guide picker " +
                        "still opens and every Guide is SILENT. Check the Resources/StreamingAssets dual-copy.");
                    Debug.LogWarning("[EchoGuideCatalog] " + StreamingRelativePath + " not found (0 memory lines).");
                    return;
                }

                var file = JsonConvert.DeserializeObject<EchoGuideMemoryFile>(text);
                if (file == null)
                {
                    FlowTrace.Fail(Sys, "echo-guide-memories.json parsed to NULL (json present, mapping break?).");
                    return;
                }

                _defaultGuideEchoId = file.DefaultGuideEchoId;

                int skippedTargets = 0;
                if (file.Targets != null)
                {
                    foreach (var t in file.Targets)
                    {
                        if (t != null && !string.IsNullOrEmpty(t.TargetId)) _targets.Add(t);
                        else skippedTargets++;
                    }
                }

                int skippedLines = 0;
                if (file.Memories != null)
                {
                    foreach (var m in file.Memories)
                    {
                        if (m != null && !string.IsNullOrEmpty(m.EchoId) &&
                            !string.IsNullOrEmpty(m.TargetId) && !string.IsNullOrEmpty(m.Line))
                            _memories.Add(m);
                        else skippedLines++;
                    }
                }

                if (skippedTargets > 0 || skippedLines > 0)
                    FlowTrace.Warn(Sys,
                        "skipped " + skippedTargets + " malformed target row(s) and " + skippedLines +
                        " malformed memory row(s) (null / missing echoId / targetId / line).");

                if (_memories.Count != ExpectedLineCount)
                    FlowTrace.Fail(Sys,
                        "loaded " + _memories.Count + " memory line(s), expected " + ExpectedLineCount +
                        " (6 Echoes x 4 targets). Owner ruling WO-1380: all 24 ship or the feature does not. " +
                        "Some Guide is about to stand at a target with nothing to say.");
                else
                    FlowTrace.Step(Sys,
                        "loaded " + _memories.Count + " memory line(s) across " + _targets.Count +
                        " target(s); default guide=" + DefaultGuideEchoId + ".");
            }
            catch (Exception ex)
            {
                FlowTrace.Fail(Sys,
                    "read/parse echo-guide-memories.json threw " + ex.GetType().Name + ": " + ex.Message +
                    ". Catalog stays empty; every Guide is silent until this is fixed.");
                Debug.LogWarning("[EchoGuideCatalog] Failed to read echo-guide-memories.json: " + ex.Message);
            }
        }
    }
}
