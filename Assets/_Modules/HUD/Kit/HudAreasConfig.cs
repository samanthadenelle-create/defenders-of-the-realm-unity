// =============================================================================
// HudAreasConfig — hud-areas.json loader: posture -> area -> widget rows (A4).
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 A4.3/A4.4 + A5 — P23 HUDKIT.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit
//
// CONTEXTS ARE DATA ROWS, not code branches (A4): the six postures' area
// occupancy lives in Data/Canonical/hud-areas.json (Resources dual-copy +
// StreamingAssets mirror, byte-identical — the CanonicalJson pattern). The
// evaluator names the posture; this table says which widgets stand in which
// area. No per-mode code branches anywhere in the kit.
//
// Parse = JsonUtility (WebGL-safe, no Newtonsoft dependency in DeNelle.HUD).
// Absent/unparseable JSON => FlowTrace.Fail + a minimal AUTHORED fallback
// (vitals + move cluster in every posture) so a data mistake can never blank
// the whole HUD (no-silent-failure law).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD.Kit
{
    /// <summary>The parsed hud-areas.json occupancy table (see header).</summary>
    public sealed class HudAreasConfig
    {
#pragma warning disable 0649 // fields assigned by JsonUtility
        [Serializable] private class FileShape    { public int version; public PostureRow[] postures; }
        [Serializable] private class PostureRow   { public string posture; public AreaRow[] areas; }
        [Serializable] private class AreaRow      { public string area; public string[] widgets; }
#pragma warning restore 0649

        // posture key -> (widget id -> area) occupancy map.
        private readonly Dictionary<string, Dictionary<string, HudArea>> _rows =
            new Dictionary<string, Dictionary<string, HudArea>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The widget->area occupancy for a posture (empty map when the posture
        /// has no row — e.g. modal / hostile(postbattle) stand-down).</summary>
        public IReadOnlyDictionary<string, HudArea> Occupancy(HudPosture posture)
        {
            Dictionary<string, HudArea> map;
            if (_rows.TryGetValue(HudPostureKeys.Key(posture), out map)) return map;
            return Empty;
        }

        private static readonly Dictionary<string, HudArea> Empty = new Dictionary<string, HudArea>();

        /// <summary>Load Data/Canonical/hud-areas.json (Resources first; authored fallback on failure).</summary>
        public static HudAreasConfig Load()
        {
            var cfg = new HudAreasConfig();
            string text = Guard.Try("HudKit", "read hud-areas.json", () =>
            {
                var ta = Resources.Load<TextAsset>("Data/Canonical/hud-areas");
                return ta != null ? ta.text : null;
            }, null);

            if (!string.IsNullOrEmpty(text))
            {
                var parsed = Guard.Try("HudKit", "parse hud-areas.json",
                    () => JsonUtility.FromJson<FileShape>(text), null);
                if (parsed != null && parsed.postures != null && parsed.postures.Length > 0)
                {
                    int widgetRows = 0;
                    foreach (var p in parsed.postures)
                    {
                        if (p == null || string.IsNullOrEmpty(p.posture)) continue;
                        var map = new Dictionary<string, HudArea>(StringComparer.OrdinalIgnoreCase);
                        cfg._rows[p.posture] = map;
                        if (p.areas == null) continue;
                        foreach (var a in p.areas)
                        {
                            if (a == null || a.widgets == null) continue;
                            HudArea area;
                            if (!TryParseArea(a.area, out area))
                            {
                                FlowTrace.Warn("HudKit", "hud-areas.json: unknown area '" + a.area +
                                               "' in posture '" + p.posture + "' — row skipped");
                                continue;
                            }
                            foreach (var w in a.widgets)
                            {
                                if (string.IsNullOrEmpty(w)) continue;
                                map[w] = area;
                                widgetRows++;
                            }
                        }
                    }
                    FlowTrace.Step("HudKit", "hud-areas.json loaded: " + parsed.postures.Length +
                                   " postures, " + widgetRows + " widget rows (v" + parsed.version + ")");
                    return cfg;
                }
            }

            FlowTrace.Fail("HudKit", "hud-areas.json absent/unparseable — using the minimal authored fallback occupancy");
            cfg.BuildFallback();
            return cfg;
        }

        private static bool TryParseArea(string key, out HudArea area)
        {
            switch ((key ?? "").Trim().ToLowerInvariant())
            {
                case "vitals":      area = HudArea.Vitals;      return true;
                case "status":      area = HudArea.Status;      return true;
                case "system":      area = HudArea.System;      return true;
                case "targetinfo":  area = HudArea.TargetInfo;  return true;
                case "actionrail":  area = HudArea.ActionRail;  return true;
                case "actionbar":   area = HudArea.ActionBar;   return true;
                case "movecluster": area = HudArea.MoveCluster; return true;
                case "feedback":    area = HudArea.Feedback;    return true;
                case "dock":        area = HudArea.Dock;        return true;
                case "heartstatus": area = HudArea.HeartStatus; return true;
                // WO-778: MANDATORY — an unknown area string is row-skipped with a Warn,
                // which is exactly how the Work button went dark. Never add a json area
                // without its parser case.
                case "queuestatus": area = HudArea.QueueStatus; return true;
                default:            area = HudArea.Vitals;      return false;
            }
        }

        // Minimal survival occupancy: the hero can always see vitals + move (never a blank HUD).
        private void BuildFallback()
        {
            string[] live = { "calm(town)", "calm(explore)", "hostile(prebattle)", "hostile(activebattle)" };
            foreach (var key in live)
            {
                _rows[key] = new Dictionary<string, HudArea>(StringComparer.OrdinalIgnoreCase)
                {
                    { "playerNameplate", HudArea.Vitals },
                    { "moveCluster",     HudArea.MoveCluster },
                    { "settingsButton",  HudArea.System },
                };
            }
        }
    }
}
