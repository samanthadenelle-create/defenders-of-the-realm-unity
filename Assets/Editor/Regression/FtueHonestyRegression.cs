// =============================================================================
// FtueHonestyRegression [ftue-honesty] -- proves the founding tutorial teaches the
// truth (points at a real control; never teaches a fiction).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core). Two proofs, from the
// real catalogs the runtime reads:
//   (1) the founding_echo step HIGHLIGHTS a real control -- its highlight list is
//       non-empty AND every id resolves to a TutorialHighlightRegistry.KnownIds member
//       (a highlight the HUD can actually draw), AND
//   (2) NO tut_founding_* dialogue teaches "storefront defense" -- the honest FTUE
//       teaches stores are NOT defended and walls/towers win the defense; the word
//       "storefront" must never appear in a founding dialogue.
//
// Marker: FTUE_HONESTY_OK / FTUE_HONESTY_FAIL. Expected: RED today -- founding_echo's
// highlight is empty ([]); flips green when it highlights the pets control (a KnownIds
// member). FAIL-BY-DESIGN.
//
// Wire (DataRegression.RunAll):
//   if (!FtueHonestyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[ftue-honesty] " + r);
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class FtueHonestyRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- FTUE HONESTY (founding_echo highlights a real control + no storefront-defense teaching) ---");

            // (1) founding_echo highlight is non-empty and resolves to a KnownIds member.
            try
            {
                DeNelle.Core.Tutorial.TutorialStepCatalog.Reload();
                var known = new HashSet<string>(DeNelle.Core.UI.TutorialHighlightRegistry.KnownIds);

                object echoStep = null;
                foreach (var s in DeNelle.Core.Tutorial.TutorialStepCatalog.All)
                    if (s != null && s.Id == "founding_echo") { echoStep = s; break; }

                if (echoStep == null)
                {
                    failures.Add("[ftue-honesty] tutorial-steps.json has no 'founding_echo' step");
                }
                else
                {
                    var highlights = new List<string>();
                    var hlMember = echoStep.GetType().GetProperty("Highlight", BindingFlags.Public | BindingFlags.Instance);
                    object hlVal = hlMember != null ? hlMember.GetValue(echoStep)
                        : echoStep.GetType().GetField("Highlight", BindingFlags.Public | BindingFlags.Instance)?.GetValue(echoStep);
                    if (hlVal is IEnumerable hl) foreach (var h in hl) if (h != null) highlights.Add(h.ToString());
                    log.AppendLine($"  founding_echo.highlight = [{string.Join(", ", highlights)}]");

                    if (highlights.Count == 0)
                        failures.Add("[ftue-honesty] founding_echo.highlight is EMPTY -- the step teaches the Echo without pointing at any control (dishonest/orphan teaching). Point it at the pets control (a KnownIds highlight).");
                    foreach (var h in highlights)
                        if (!known.Contains(h))
                            failures.Add($"[ftue-honesty] founding_echo highlight '{h}' is not a TutorialHighlightRegistry.KnownIds member -- the HUD cannot draw it");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"[ftue-honesty] highlight check threw: {ex.GetType().Name}: {ex.Message}");
            }

            // (2) No tut_founding_* dialogue teaches storefront defense.
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/dialogue/dialogues.json");
                if (string.IsNullOrEmpty(json))
                {
                    failures.Add("[ftue-honesty] dialogues.json not found/empty -- cannot verify founding copy");
                }
                else
                {
                    var root = JObject.Parse(json);
                    var arr = FindDialogueArray(root);
                    int scanned = 0, offend = 0;
                    if (arr != null)
                    {
                        foreach (var tok in arr)
                        {
                            if (!(tok is JObject o)) continue;
                            string id = o["id"]?.ToString();
                            if (string.IsNullOrEmpty(id) || !id.StartsWith("tut_founding", StringComparison.OrdinalIgnoreCase)) continue;
                            scanned++;
                            if (o.ToString().IndexOf("storefront", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                offend++;
                                failures.Add($"[ftue-honesty] founding dialogue '{id}' mentions 'storefront' -- the FTUE must never teach storefront defense");
                            }
                        }
                    }
                    log.AppendLine($"  scanned {scanned} tut_founding_* dialogue(s); storefront mentions: {offend}");
                    if (scanned == 0)
                        log.AppendLine("  NOTE: no tut_founding_* dialogues located in dialogues.json (schema shape?)");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"[ftue-honesty] dialogue scan threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "FTUE_HONESTY_OK");
                reason = "FTUE HONESTY OK -- founding_echo highlights a real KnownIds control and no founding dialogue teaches storefront defense";
                return true;
            }
            reason = "ftue-honesty: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "FTUE_HONESTY_FAIL: " + reason);
            return false;
        }

        // dialogues.json may root the array under "dialogues" or be a bare array-in-object.
        private static JArray FindDialogueArray(JObject root)
        {
            if (root["dialogues"] is JArray a) return a;
            foreach (var prop in root.Properties())
                if (prop.Value is JArray arr && arr.Count > 0 && arr[0] is JObject o && o["id"] != null) return arr;
            return null;
        }
    }
}
