// =============================================================================
// MaintenanceBannerDriver - WO-1243, the rolling banner that tells EVERY player.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Ops
//
// Owner ruling 2026-08-27, verbatim: "there should be a rolling banner wit the
// notice to all players mainatance on farming, or maintance on raids".
//
// WHAT "ROLLS": when more than one area is sealed the line CYCLES between them
// on <see cref="RollSeconds"/>, so a player who can still farm but cannot raid
// reads both facts rather than one truncated string. With a single seal the line
// is steady, which is the same behaviour with a cycle of length one.
//
// -----------------------------------------------------------------------------
// IT READS AS MAINTENANCE FROM ITS WORDS, NOT FROM ITS COLOUR.
// -----------------------------------------------------------------------------
// The owner is red/green colourblind, and CLAUDE.md is explicit that no meaning
// in this game may live in hue alone. Every line this driver shows begins with
// the literal word MAINTENANCE and names the area. Strip the colour, the plate
// and the font entirely and the sentence still says exactly what is happening.
// That is not a nicety here - it is the acceptance criterion.
//
// -----------------------------------------------------------------------------
// SURFACE: ObjectiveBannerUi, which this WO CLAIMS.
// -----------------------------------------------------------------------------
// ObjectiveBannerUi.cs has carried a header since WO-1012 saying it is retired
// from the FTUE and has ZERO callers, "kept compiling for any non-tutorial
// caller a future WO may add". This is that caller. It is a persistent,
// top-centre, non-blocking one-liner that already exists and is already built in
// the kit language, so no new HUD chrome is minted and nothing contends for it.
//
// DO NOT: Do NOT move this to ObjectiveStripUi - that one is owned by the FTUE
// (TutorialFlow) and a maintenance banner would fight the tutorial for it.
//
// Never passes onSkip/onSkipAll: a maintenance notice is not dismissible. The
// player cannot skip the outage, so the banner must not offer to.
//
// LIFECYCLE: self-bootstrapping, one hidden GameObject, DontDestroyOnLoad, so
// the banner survives every scene change - a full `server` window must be
// visible at the title screen and in a raid alike.
//
// ASCII only. Instrumentation: FlowTrace tag "Maintenance".
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using UnityEngine;

namespace DeNelle.Core.Ops
{
    /// <summary>
    /// Polls <see cref="MaintenanceCatalog"/> and drives the persistent banner.
    /// Presentation only: it reads state and shows words, and decides nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MaintenanceBannerDriver : MonoBehaviour
    {
        private const string Sys = MaintenanceCatalog.Sys;

        /// <summary>How long each sealed area holds the line before the next one.</summary>
        public const float RollSeconds = 4f;

        /// <summary>How often the driver re-reads the catalog. Cheap: a dictionary
        /// lookup, no allocation unless the seal set actually changed.</summary>
        private const float ReadSeconds = 1f;

        private static MaintenanceBannerDriver s_instance;

        private readonly List<string> _lines = new List<string>(6);
        private float _nextRead;
        private float _nextRoll;
        private int _cursor;
        private string _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_instance != null) return;
            if (!MaintenanceService.Enabled)
            {
                // The kill switch suppresses the sign along with the poll. Said out
                // loud because a missing banner is otherwise indistinguishable from
                // "nothing is sealed", and those are very different situations.
                FlowTrace.Step(Sys, "banner not started: the maintenance kill switch is off.");
                return;
            }

            var go = new GameObject("MaintenanceBannerDriver");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            s_instance = go.AddComponent<MaintenanceBannerDriver>();
            FlowTrace.Step(Sys, "banner driver up (roll=" + RollSeconds + "s, read=" + ReadSeconds + "s).");
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (now >= _nextRead)
            {
                _nextRead = now + ReadSeconds;
                RebuildLines();
            }

            if (_lines.Count == 0)
            {
                if (_shown != null)
                {
                    ObjectiveBannerUi.Hide();
                    FlowTrace.Step(Sys, "banner cleared - nothing is sealed.");
                    _shown = null;
                    _cursor = 0;
                }
                return;
            }

            if (_shown == null || now >= _nextRoll)
            {
                _nextRoll = now + RollSeconds;
                if (_cursor >= _lines.Count) _cursor = 0;
                string line = _lines[_cursor];
                _cursor = (_cursor + 1) % _lines.Count;

                if (!string.Equals(line, _shown))
                {
                    _shown = line;
                    // No skip affordance: an outage is not dismissible.
                    ObjectiveBannerUi.Show(line);
                }
            }
        }

        /// <summary>
        /// Rebuild the roll. One line per sealed area, or a single line for a full
        /// `server` window (which outranks everything, so nothing else is listed -
        /// naming five areas when the whole realm is down is noise, not information).
        /// </summary>
        private void RebuildLines()
        {
            int before = _lines.Count;
            _lines.Clear();

            var server = MaintenanceCatalog.For(MaintenanceArea.Server);
            if (server.Closed)
            {
                _lines.Add(Line(MaintenanceArea.Server, server));
            }
            else
            {
                for (int i = 0; i < MaintenanceCatalog.AreaIds.Length; i++)
                {
                    var area = (MaintenanceArea)i;
                    if (area == MaintenanceArea.Server) continue;
                    var st = MaintenanceCatalog.For(area);
                    if (st.Closed) _lines.Add(Line(area, st));
                }
            }

            if (_lines.Count != before)
            {
                FlowTrace.Step(Sys, "banner roll rebuilt: " + _lines.Count + " sealed area(s) (was " +
                                    before + "), provenance=" + MaintenanceCatalog.Provenance + ".");
                _cursor = 0;
                _nextRoll = 0f;   // show the new head immediately, not after a full dwell
            }
        }

        /// <summary>One banner line. Leads with MAINTENANCE ON &lt;AREA&gt; every time,
        /// then the operator's own sentence when she wrote one.</summary>
        private static string Line(MaintenanceArea area, MaintenanceState state)
        {
            string head = "MAINTENANCE ON " +
                          MaintenanceCatalog.DisplayName(state.ClosedBy ?? MaintenanceCatalog.IdOf(area));
            if (string.IsNullOrWhiteSpace(state.Message)) return head;
            return head + " - " + state.Message;
        }
    }
}
