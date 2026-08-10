// =============================================================================
// TutorialStepReachabilityRegression [tutorial-reach]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// THE DEFECT CLASS THIS PINS (owner F8 seq 632, 2026-08-02): the FTUE sat 300
// SECONDS on founding_hollow awaiting "build.structure_placed:pet-house" and was
// then auto-advanced by the watchdog. That was not slowness - it was a
// CAN-NEVER-COMPLETE state, and nothing in the build gates could see it, because
// every existing check validated the tutorial data against a VOCABULARY (is this
// a known signal string? is this a known highlight id?) and never against the
// RUNNING GAME (does anything actually RAISE that signal? can the player actually
// reach the thing they are told to place?).
//
// A tutorial step is a CONTRACT with three ends. This suite asserts all three for
// every MANDATORY step, so an authored beat can never again be un-completable:
//
//   Case 1 [emitter-live]  Every mandatory completion signal has a LIVE EMITTER in
//                          Assets/_Modules. Source-scan of every TutorialSignals
//                          .Raise(...) call site, resolving both forms: a constant
//                          / literal argument (exact id) and a Prefix + expr concat
//                          (dynamic family, e.g. StructurePlacedPrefix + entryId).
//                          A step awaiting an id nothing emits is a dead beat.
//
//   Case 2 [palette-reach] For every "build.structure_placed:<id>" step, <id>
//                          resolves in structures-catalog.json AND is reachable in
//                          a palette the player can open: its catalog `type` is in
//                          some Town/Defense/Walls category's catalogTypes AND the
//                          id is NOT in that category's lockedIds. This is the
//                          founding_stores half of seq 632 - the step awaited
//                          `lumberyard` while the card that harvests timber (and
//                          that the copy described) is `collector_lumbermill`.
//
//   Case 3 [teach-present] Any step whose completion is a PLAYER ACTION (place a
//                          structure, raise a tower, clear a wave, walk somewhere,
//                          open a panel) authors at least one highlight that is a
//                          real TutorialHighlightRegistry.KnownIds member. A step
//                          that demands an action and points at nothing is the
//                          defect: founding_hollow highlighted only the Build
//                          BUTTON - the door - and never the CARD, leaving the
//                          player hunting ~10 Town cards unaided.
//                          dialogue.ended:* completions are EXEMPT on purpose: the
//                          dialogue IS the teach and it closes itself.
//
//   Case 4 [arm-safety]    The seq 632 P0 itself: TutorialFlow must refuse to arm
//                          in a hub scene that is ENEMY-OWNED. Village2 is BOTH a
//                          HubScenes.Names entry AND ownership:"Enemy" in
//                          scene-configs.json, and BuildModeController.Enter()
//                          refuses outright there - so every placement step is
//                          un-completable in that hub. Asserts (a) the enemy-owned
//                          hub really exists (the gate is load-bearing, not
//                          decorative) and (b) TutorialFlow.TryArm checks it.
//
//   Case 6 [arc-shape]     WO-1012 P3 (2026-08-10): the owner's 8-beat founding arc
//                          holds its shape — the mandatory chain is EXACTLY
//                          greet/walk/stores/ack/defense/timers/defend/win in order;
//                          ARRIVE carries the starterPet grant (the guide must exist
//                          before it speaks); WALK completes on the follow-proximity
//                          signal (hero.reached:guide_gate) and TutorialWorldAnchors
//                          actually resolves that anchor; ENEMIES AT THE GATE
//                          completes on the band-scoped wave.tutorial_band_repelled
//                          and TutorialFlow arms the scripted spawner on it; and the
//                          WIN beat's OUTRO feeds the 2c-bis nudge chain — its
//                          dialogue.ended id IS ctx_build_weapons' trigger, so the
//                          tutorial can never again end in silence (the owner's
//                          "now what?" gap). Both nudges stay oneShot + non-blocking.
//
//   Case 5 [refusal-loud]  BuildModeController's enemy-owned refusal is AUDIBLE:
//                          a player-facing toast + a FlowTrace line, not the bare
//                          Debug.Log it used to be. CLAUDE.md sec.12: a refusal the
//                          player can feel must be one they can READ and one the CLI
//                          can find in the capture. Plus the soft-lock guard: every
//                          awaited placement id is in BuildModeController.FoundingKit
//                          (the free-placement exemption), or a v32 zero-resource
//                          founding cannot afford the thing the FTUE demands.
//
// Contextual (flowId "contextual") steps are NOT failed - they are hints, they never
// gate, and one of them (inventory.gear_added:first) is a documented known-unwired
// trigger. They are reported as notes so the gap stays visible.
//
// Markers: TUTORIAL_REACH_OK / TUTORIAL_REACH_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.TutorialStepReachabilityRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced):
//
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "tutorial-reach suite", () => { if (!TutorialStepReachabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tutorial-reach] " + r); });
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class TutorialStepReachabilityRegression
    {
        private const string StepsRes = "Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json";
        private const string StepsSA = "Assets/StreamingAssets/Data/Canonical/tutorial/tutorial-steps.json";
        private const string StructuresRes = "Assets/Resources/Data/Canonical/structures-catalog.json";
        private const string BuildCategoriesRes = "Assets/Resources/Data/Canonical/build-categories.json";
        private const string SceneConfigsRes = "Assets/Resources/Data/Canonical/scene-configs.json";
        private const string ModulesRoot = "Assets/_Modules";
        private const string TutorialFlowSrc = "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs";
        private const string BuildModeSrc = "Assets/_Modules/Village/BuildMode/BuildModeController.cs";

        /// <summary>The build verbs a player can actually open from the Build HUD tab row
        /// (BuildTabRow registers Town + Defenses; Walls is the third authored palette).
        /// A structure reachable ONLY through a legacy standalone verb is NOT reachable.</summary>
        private static readonly string[] PlayerOpenableVerbs = { "Town", "Defense", "Walls" };

        // =====================================================================

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TUTORIAL_REACH_OK - " + reason);
            else Debug.LogError("TUTORIAL_REACH_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                var steps = LoadSteps(failures);
                if (steps != null)
                {
                    Case(failures, "emitter-live", () => Case1_EmitterLive(steps, failures, notes));
                    Case(failures, "palette-reach", () => Case2_PaletteReach(steps, failures, notes));
                    Case(failures, "teach-present", () => Case3_TeachPresent(steps, failures, notes));
                    Case(failures, "refusal-loud", () => Case5_RefusalLoud(steps, failures, notes));
                    Case(failures, "arc-shape", () => Case6_ArcShape(steps, failures, notes));
                }
                Case(failures, "arm-safety", () => Case4_ArmSafety(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "TUTORIAL REACHABILITY OK - every mandatory step's completion signal has a live " +
                         "emitter in Assets/_Modules, every awaited placement id resolves in the catalog and " +
                         "sits in a palette the player can open (and is free-placement exempt), every " +
                         "player-action step points at a real registered highlight, TutorialFlow refuses to " +
                         "arm in an enemy-owned hub, the build refusal is player-audible, and the WO-1012 " +
                         "8-beat arc + 2c-bis nudge chain hold their shape" + noteStr;
                return true;
            }
            reason = "tutorial-reach FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Step model (read straight from JSON on purpose: the authored file is the
        //  truth, and reading it directly means the suite still reports the real
        //  defect if a C# field mapping is ever dropped).
        // =====================================================================

        private sealed class Step
        {
            public string Id;
            public int Order;
            public bool Contextual;
            public string Signal;
            public List<string> Highlight = new List<string>();
            public string ObjectiveText;
            // WO-1012 P3 (arc-shape): the extra ends of the step contract Case 6 pins.
            public string TriggerSignal;
            public string IntroDialogue;
            public string OutroDialogue;
            public bool GrantStarterPet;
            public bool GrantPrepaidTower;
            public bool OneShot;
            public bool PausePressure;
        }

        private static List<Step> LoadSteps(List<string> failures)
        {
            string res = ReadText(StepsRes, failures);
            string sa = ReadText(StepsSA, failures);
            if (res == null) return null;

            // tutorial-steps.json IS a byte-identical canonical dual pair (unlike weapons.json).
            if (sa != null && !string.Equals(res, sa, StringComparison.Ordinal))
                failures.Add("[dual-copy] tutorial-steps.json Resources and StreamingAssets copies DIFFER - " +
                             "the shipped player loads Resources, so an edit made in only one copy is invisible on device");

            var list = new List<Step>();
            JObject root;
            try { root = JObject.Parse(res); }
            catch (Exception ex)
            {
                failures.Add("[parse] tutorial-steps.json is not valid JSON: " + ex.Message);
                return null;
            }

            var arr = root["steps"] as JArray;
            if (arr == null || arr.Count == 0)
            {
                failures.Add("[parse] tutorial-steps.json has no 'steps' array");
                return null;
            }

            foreach (var t in arr)
            {
                var o = t as JObject;
                if (o == null) continue;
                var s = new Step
                {
                    Id = (string)o["id"],
                    Order = o["order"] != null ? (int)o["order"] : 0,
                    Contextual = string.Equals((string)o["flowId"], "contextual", StringComparison.OrdinalIgnoreCase),
                    Signal = o["completion"] != null ? (string)o["completion"]["signal"] : null,
                    ObjectiveText = o["objective"] != null ? (string)o["objective"]["text"] : null,
                    TriggerSignal = o["trigger"] != null ? (string)o["trigger"]["signal"] : null,
                    IntroDialogue = o["dialogue"] != null ? (string)o["dialogue"]["intro"] : null,
                    OutroDialogue = o["dialogue"] != null ? (string)o["dialogue"]["outro"] : null,
                    GrantStarterPet = o["grant"] != null && o["grant"]["starterPet"] != null && (bool)o["grant"]["starterPet"],
                    GrantPrepaidTower = o["grant"] != null && o["grant"]["prepaidTower"] != null && (bool)o["grant"]["prepaidTower"],
                    OneShot = o["oneShot"] != null && (bool)o["oneShot"],
                    PausePressure = o["pausePressure"] != null && (bool)o["pausePressure"],
                };
                var hl = o["highlight"] as JArray;
                if (hl != null)
                    foreach (var h in hl)
                    {
                        string v = (string)h;
                        if (!string.IsNullOrEmpty(v)) s.Highlight.Add(v);
                    }
                if (!string.IsNullOrEmpty(s.Id)) list.Add(s);
            }
            return list;
        }

        // =====================================================================
        //  CASE 1 - every mandatory completion signal has a LIVE EMITTER
        // =====================================================================

        private static void Case1_EmitterLive(List<Step> steps, List<string> failures, List<string> notes)
        {
            var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefixes = new List<string>();
            int callSites = ScanEmitters(exact, prefixes, failures);

            if (callSites == 0)
            {
                failures.Add("[emitter-live] found ZERO TutorialSignals.Raise call sites under " + ModulesRoot +
                             " - either the scan root moved or the whole signal bus lost its emitters");
                return;
            }

            // hero.reached:<anchor> is emitted by TutorialFlow's own proximity probe, which
            // raises the awaited id directly (a variable argument the source scan cannot
            // resolve). Accept the prefix ONLY if that probe is actually still there.
            string flowSrc = ReadText(TutorialFlowSrc, failures);
            if (flowSrc != null && StripComments(flowSrc).Contains("TickProximityProbe"))
                prefixes.Add("hero.reached:");

            foreach (var s in steps)
            {
                if (string.IsNullOrEmpty(s.Signal))
                {
                    string msg = "step '" + s.Id + "' has NO completion signal - nothing can ever complete it";
                    if (s.Contextual) notes.Add("[contextual] " + msg); else failures.Add("[emitter-live] " + msg);
                    continue;
                }

                if (exact.Contains(s.Signal)) continue;

                string matched = null;
                foreach (var p in prefixes)
                    if (s.Signal.StartsWith(p, StringComparison.OrdinalIgnoreCase) && s.Signal.Length > p.Length)
                    { matched = p; break; }
                if (matched != null) continue;

                string fail = "step '" + s.Id + "' completion signal '" + s.Signal + "' has NO EMITTER anywhere in " +
                              ModulesRoot + " (" + callSites + " Raise call sites scanned; no exact constant/literal " +
                              "and no matching Prefix+expr family) - the step can never complete";
                if (s.Contextual) notes.Add("[contextual] " + fail);
                else failures.Add("[emitter-live] " + fail);
            }
        }

        /// <summary>Scans every .cs under Assets/_Modules for TutorialSignals.Raise(...) and
        /// resolves each argument into either an EXACT signal id (string literal, or a
        /// TutorialSignals constant - including both arms of a ternary) or a dynamic PREFIX
        /// family (any concat whose TutorialSignals constant ends in ':'). Returns the call-site
        /// count so a scan that silently found nothing is itself a failure.</summary>
        private static int ScanEmitters(HashSet<string> exact, List<string> prefixes, List<string> failures)
        {
            var consts = SignalConstants();
            int sites = 0;

            string[] files;
            try { files = Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                failures.Add("[emitter-live] could not enumerate " + ModulesRoot + ": " + ex.Message);
                return 0;
            }

            var constToken = new Regex(@"TutorialSignals\s*\.\s*([A-Za-z_]\w*)");
            var literal = new Regex("\"([^\"\\\\]*)\"");

            foreach (var file in files)
            {
                string src;
                try { src = File.ReadAllText(file); }
                catch { continue; }
                if (src.IndexOf("TutorialSignals", StringComparison.Ordinal) < 0) continue;
                src = StripComments(src);

                int at = 0;
                while (true)
                {
                    int idx = src.IndexOf("TutorialSignals.Raise", at, StringComparison.Ordinal);
                    if (idx < 0) break;
                    int open = src.IndexOf('(', idx);
                    if (open < 0) break;
                    string arg = ExtractBalanced(src, open);
                    at = open + 1;
                    if (arg == null) continue;
                    sites++;

                    bool isConcat = arg.IndexOf('+') >= 0;
                    foreach (Match m in constToken.Matches(arg))
                    {
                        string name = m.Groups[1].Value;
                        if (name == "Raise") continue;
                        if (!consts.TryGetValue(name, out string val) || string.IsNullOrEmpty(val)) continue;
                        if (isConcat)
                        {
                            // A Prefix + expr concat emits the WHOLE family under that prefix.
                            if (val.EndsWith(":", StringComparison.Ordinal) && !prefixes.Contains(val))
                                prefixes.Add(val);
                        }
                        else exact.Add(val);
                    }
                    if (!isConcat)
                        foreach (Match m in literal.Matches(arg))
                            if (!string.IsNullOrEmpty(m.Groups[1].Value)) exact.Add(m.Groups[1].Value);
                }
            }
            return sites;
        }

        /// <summary>Every public const string on DeNelle.Core.Tutorial.TutorialSignals, by name.</summary>
        private static Dictionary<string, string> SignalConstants()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var t = typeof(DeNelle.Core.Tutorial.TutorialSignals);
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                map[f.Name] = (string)f.GetRawConstantValue();
            }
            return map;
        }

        /// <summary>Returns the text inside the parens starting at <paramref name="open"/>,
        /// honouring nesting; null when unbalanced.</summary>
        private static string ExtractBalanced(string src, int open)
        {
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0) return src.Substring(open + 1, i - open - 1);
                }
            }
            return null;
        }

        // =====================================================================
        //  CASE 2 - every awaited placement id resolves AND sits in an openable palette
        // =====================================================================

        private static void Case2_PaletteReach(List<Step> steps, List<string> failures, List<string> notes)
        {
            string prefix = DeNelle.Core.Tutorial.TutorialSignals.StructurePlacedPrefix;

            var catalogType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string catRaw = ReadText(StructuresRes, failures);
            if (catRaw != null)
            {
                var entries = JObject.Parse(catRaw)["entries"] as JArray;
                if (entries == null) failures.Add("[palette-reach] structures-catalog.json has no 'entries' array");
                else
                    foreach (var e in entries)
                    {
                        string id = (string)e["id"];
                        if (!string.IsNullOrEmpty(id)) catalogType[id] = (string)e["type"] ?? "";
                    }
            }

            var verbTypes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var verbLocked = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            string bcRaw = ReadText(BuildCategoriesRes, failures);
            if (bcRaw != null)
            {
                var cats = JObject.Parse(bcRaw)["categories"] as JArray;
                if (cats == null) failures.Add("[palette-reach] build-categories.json has no 'categories' array");
                else
                    foreach (var c in cats)
                    {
                        string verb = (string)c["buildType"];
                        if (string.IsNullOrEmpty(verb)) continue;
                        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var locked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (c["catalogTypes"] is JArray ta) foreach (var x in ta) types.Add((string)x ?? "");
                        if (c["lockedIds"] is JArray la) foreach (var x in la) locked.Add((string)x ?? "");
                        verbTypes[verb] = types;
                        verbLocked[verb] = locked;
                    }
            }
            if (catalogType.Count == 0 || verbTypes.Count == 0) return;   // already reported

            foreach (var s in steps)
            {
                if (string.IsNullOrEmpty(s.Signal)) continue;
                if (!s.Signal.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string wantId = s.Signal.Substring(prefix.Length);
                string where = s.Contextual ? "[contextual] " : "";

                if (string.IsNullOrEmpty(wantId))
                { failures.Add("[palette-reach] step '" + s.Id + "' awaits a placement signal with an EMPTY id"); continue; }

                if (!catalogType.TryGetValue(wantId, out string type))
                {
                    string msg = "step '" + s.Id + "' awaits placement of '" + wantId + "' which does NOT resolve in " +
                                 "structures-catalog.json - the player can never place a structure that does not exist";
                    if (s.Contextual) notes.Add(where + msg); else failures.Add("[palette-reach] " + msg);
                    continue;
                }

                string reachableVia = null;
                var blockedBy = new List<string>();
                foreach (var verb in PlayerOpenableVerbs)
                {
                    if (!verbTypes.TryGetValue(verb, out var types) || !types.Contains(type)) continue;
                    if (verbLocked.TryGetValue(verb, out var locked) && locked.Contains(wantId))
                    { blockedBy.Add(verb + " (lockedIds)"); continue; }
                    reachableVia = verb;
                    break;
                }

                if (reachableVia == null)
                {
                    failures.Add("[palette-reach] step '" + s.Id + "' awaits placement of '" + wantId + "' (catalog type '" +
                                 type + "') but that id is NOT reachable in any palette the player can open (" +
                                 string.Join("/", PlayerOpenableVerbs) + ")" +
                                 (blockedBy.Count > 0 ? " - filtered out by " + string.Join(", ", blockedBy) : "") +
                                 " - the step demands a card that never renders");
                }
                else
                {
                    notes.Add("'" + s.Id + "' -> '" + wantId + "' (type " + type + ") reachable via the " +
                              reachableVia + " palette");
                }
            }
        }

        // =====================================================================
        //  CASE 3 - a step that demands an ACTION must point at something real
        // =====================================================================

        private static void Case3_TeachPresent(List<Step> steps, List<string> failures, List<string> notes)
        {
            var known = new HashSet<string>(DeNelle.Core.UI.TutorialHighlightRegistry.KnownIds, StringComparer.OrdinalIgnoreCase);
            if (known.Count == 0)
            { failures.Add("[teach-present] TutorialHighlightRegistry.KnownIds is EMPTY - the highlight contract is gone"); return; }

            foreach (var s in steps)
            {
                if (s.Contextual) continue;                    // hints never gate; not a demanded action
                if (!IsPlayerAction(s.Signal)) continue;        // dialogue.ended:* teaches itself

                var real = new List<string>();
                foreach (var h in s.Highlight)
                {
                    if (known.Contains(h)) real.Add(h);
                    else failures.Add("[teach-present] step '" + s.Id + "' highlight '" + h + "' is not a " +
                                      "TutorialHighlightRegistry.KnownIds member - nothing can draw it");
                }

                if (real.Count == 0)
                {
                    failures.Add("[teach-present] step '" + s.Id + "' completes on the PLAYER ACTION '" + s.Signal +
                                 "' but authors NO real highlight" +
                                 (s.Highlight.Count > 0 ? " (its " + s.Highlight.Count + " authored id(s) are all unknown)" : "") +
                                 " - it demands a thing and points at nothing, which is the seq 632 defect");
                    continue;
                }

                // A PLACEMENT step must point at the CARD, not only at the door into the
                // builder: founding_hollow highlighted hud.build_button alone and left the
                // player hunting ~10 Town cards (seq 632 root cause 3).
                if (!string.IsNullOrEmpty(s.Signal) &&
                    s.Signal.StartsWith(DeNelle.Core.Tutorial.TutorialSignals.StructurePlacedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    bool hasCard = false;
                    foreach (var h in real)
                        if (h.StartsWith("build.card.", StringComparison.OrdinalIgnoreCase)) { hasCard = true; break; }
                    if (!hasCard)
                        failures.Add("[teach-present] placement step '" + s.Id + "' points only at [" +
                                     string.Join(", ", real) + "] - no 'build.card.<id>' highlight, so the player is " +
                                     "sent into the builder and left to find the right card unaided");
                }

                if (string.IsNullOrEmpty(s.ObjectiveText))
                    failures.Add("[teach-present] step '" + s.Id + "' demands an action but authors no objective text - " +
                                 "the banner and the escalating coach nudge have nothing honest to say");
            }
        }

        /// <summary>True when the completion demands the player DO something in the world/UI
        /// (as opposed to closing a dialogue, which closes itself).</summary>
        private static bool IsPlayerAction(string signal)
        {
            if (string.IsNullOrEmpty(signal)) return false;
            var sig = DeNelle.Core.Tutorial.TutorialSignals.DialogueEndedPrefix;
            if (signal.StartsWith(sig, StringComparison.OrdinalIgnoreCase)) return false;
            return signal.StartsWith(DeNelle.Core.Tutorial.TutorialSignals.StructurePlacedPrefix, StringComparison.OrdinalIgnoreCase)
                || signal.StartsWith(DeNelle.Core.Tutorial.TutorialSignals.HeroReachedPrefix, StringComparison.OrdinalIgnoreCase)
                || signal.StartsWith(DeNelle.Core.Tutorial.TutorialSignals.PanelOpenedPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(signal, DeNelle.Core.Tutorial.TutorialSignals.TowerPlaced, StringComparison.OrdinalIgnoreCase)
                || string.Equals(signal, DeNelle.Core.Tutorial.TutorialSignals.WaveCleared, StringComparison.OrdinalIgnoreCase)
                // WO-1012 P3: the arc's ENEMIES beat is a combat action — it must point
                // somewhere (world.gate_direction), same rule as wave.cleared.
                || string.Equals(signal, DeNelle.Core.Tutorial.TutorialSignals.TutorialBandRepelled, StringComparison.OrdinalIgnoreCase)
                || string.Equals(signal, DeNelle.Core.Tutorial.TutorialSignals.BuildModeEntered, StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        //  CASE 4 - the flow must not arm where its steps cannot complete
        // =====================================================================

        private static void Case4_ArmSafety(List<string> failures, List<string> notes)
        {
            // (a) The gate is load-bearing: at least one HubScenes.Names entry really IS
            //     ownership:"Enemy". If that ever stops being true the gate is dead weight
            //     and this note tells the next reader why it is still here.
            string raw = ReadText(SceneConfigsRes, failures);
            var enemyHubs = new List<string>();
            if (raw != null)
            {
                var cfgs = JObject.Parse(raw)["configs"] as JArray;
                if (cfgs == null) failures.Add("[arm-safety] scene-configs.json has no 'configs' array");
                else
                    foreach (var c in cfgs)
                    {
                        string name = (string)c["sceneName"];
                        string owner = (string)c["ownership"];
                        if (string.IsNullOrEmpty(name)) continue;
                        if (!string.Equals(owner, "Enemy", StringComparison.OrdinalIgnoreCase)) continue;
                        if (DeNelle.Core.HubScenes.IsHub(name)) enemyHubs.Add(name);
                    }
            }
            if (enemyHubs.Count == 0)
                notes.Add("no HubScenes entry is currently ownership:Enemy - the TutorialFlow arm gate is " +
                          "presently inert but MUST stay (it is one scene-configs edit away from live again)");
            else
                notes.Add("enemy-owned hub scene(s) where building is impossible: " + string.Join(", ", enemyHubs));

            // (b) TutorialFlow's arm path checks it. Comment-stripped so prose can never satisfy this.
            string src = ReadText(TutorialFlowSrc, failures);
            if (src == null) return;
            string code = StripComments(src);

            if (code.IndexOf("IsEnemyOwnedScene", StringComparison.Ordinal) < 0)
                failures.Add("[arm-safety] TutorialFlow no longer calls HubScenes.IsEnemyOwnedScene - the flow can " +
                             "again arm in an enemy-owned hub where BuildModeController.Enter refuses outright, " +
                             "making every placement step un-completable (owner F8 seq 632, 300s stall)");

            // The check must live in the ARM path, not somewhere incidental.
            int arm = code.IndexOf("TryArm", StringComparison.Ordinal);
            int gate = code.IndexOf("IsEnemyOwnedScene", StringComparison.Ordinal);
            if (arm >= 0 && gate >= 0 && gate < arm)
                failures.Add("[arm-safety] TutorialFlow's IsEnemyOwnedScene check is not inside the TryArm bootstrap " +
                             "path - the arm decision is what must be gated");

            // The rescue must be recorded honestly (seq 632 root cause 4): the watchdog
            // completes as SKIPPED, never as a genuine completion (which played the outro
            // and narrated a Hollow that was never built).
            if (Regex.IsMatch(code, @"CompleteCurrentStep\s*\(\s*skipped\s*:\s*false\s*\)[^;]*;", RegexOptions.None) &&
                !Regex.IsMatch(code, @"CompleteCurrentStep\s*\(\s*skipped\s*:\s*true\s*\)"))
                failures.Add("[arm-safety] TutorialFlow never calls CompleteCurrentStep(skipped: true) - the watchdog " +
                             "rescue is being credited as a genuine completion again, so a step the player never did " +
                             "plays its outro and narrates a fiction (seq 632 root cause 4)");

            int wd = code.IndexOf("private void TickWatchdog", StringComparison.Ordinal);
            if (wd >= 0)
            {
                string window = code.Substring(wd, Math.Min(4000, code.Length - wd));
                if (!Regex.IsMatch(window, @"CompleteCurrentStep\s*\(\s*skipped\s*:\s*true\s*\)"))
                    failures.Add("[arm-safety] the STEP-STUCK watchdog rescue in TickWatchdog does not complete the " +
                                 "step as skipped:true - a watchdog trip must never be recorded as a real completion " +
                                 "(it fires the outro and narrates a beat the player never did)");
            }

            // Every authored highlight must be walked, not just Highlight[0] (root cause 3).
            if (Regex.IsMatch(code, @"UiSpotlight\s*\.\s*Show\s*\(\s*step\s*\.\s*Highlight\s*\[\s*0\s*\]"))
                failures.Add("[arm-safety] TutorialFlow shows only step.Highlight[0] again - every id after the first " +
                             "is silently dropped (founding_defend's gate callout never renders)");
        }

        // =====================================================================
        //  CASE 5 - the build refusal is audible, and founding placements are affordable
        // =====================================================================

        private static void Case5_RefusalLoud(List<Step> steps, List<string> failures, List<string> notes)
        {
            string src = ReadText(BuildModeSrc, failures);
            if (src == null) return;
            string code = StripComments(src);

            int gate = code.IndexOf("SceneOwnership.IsEnemyOwned", StringComparison.Ordinal);
            if (gate < 0)
            {
                notes.Add("BuildModeController no longer gates on SceneOwnership.IsEnemyOwned - building in enemy " +
                          "territory may now be allowed; re-check the tutorial arm gate if so");
            }
            else
            {
                // The refusal body: everything up to the return that ends the guard.
                int end = code.IndexOf("return;", gate, StringComparison.Ordinal);
                string body = end > gate ? code.Substring(gate, end - gate) : code.Substring(gate, Math.Min(600, code.Length - gate));
                bool toast = body.IndexOf("ShowToast", StringComparison.Ordinal) >= 0;
                bool traced = body.IndexOf("FlowTrace.", StringComparison.Ordinal) >= 0;
                if (!toast)
                    failures.Add("[refusal-loud] BuildModeController's enemy-owned refusal shows NO player-facing toast - " +
                                 "the player taps the spotlit BUILD button and nothing happens with no reason given " +
                                 "(owner F8 seq 632; CLAUDE.md sec.12 forbids a silent failure)");
                if (!traced)
                    failures.Add("[refusal-loud] BuildModeController's enemy-owned refusal writes no FlowTrace line - " +
                                 "the CLI cannot find this refusal in a capture, which is how seq 632 hid for 300s");
            }

            // Soft-lock guard: v32 zeroed StartingBudget, so a founding placement the FTUE
            // DEMANDS must be free-placement exempt or the player cannot obey the step.
            int kit = code.IndexOf("HashSet<string> FoundingKit", StringComparison.Ordinal);
            if (kit < 0)
            {
                notes.Add("BuildModeController.FoundingKit not found - the founding free-placement exemption may have " +
                          "been renamed; awaited placement ids are unverified for affordability");
                return;
            }
            int open = code.IndexOf('{', kit);
            int close = open >= 0 ? code.IndexOf('}', open) : -1;   // flat id list, no nesting
            string kitBlock = (open >= 0 && close > open) ? code.Substring(open, close - open) : string.Empty;
            if (kitBlock.Length == 0)
            {
                notes.Add("BuildModeController.FoundingKit initializer could not be delimited; awaited placement ids " +
                          "are unverified for affordability");
                return;
            }

            string prefix = DeNelle.Core.Tutorial.TutorialSignals.StructurePlacedPrefix;
            foreach (var s in steps)
            {
                if (s.Contextual || string.IsNullOrEmpty(s.Signal)) continue;
                if (!s.Signal.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string id = s.Signal.Substring(prefix.Length);
                if (string.IsNullOrEmpty(id)) continue;
                if (kitBlock.IndexOf("\"" + id + "\"", StringComparison.OrdinalIgnoreCase) < 0)
                    failures.Add("[refusal-loud] step '" + s.Id + "' demands placement of '" + id + "' but that id is NOT " +
                                 "in BuildModeController.FoundingKit - with the v32 zeroed starting budget the player " +
                                 "cannot afford the thing the FTUE forces them to place (soft-lock)");
            }
        }

        // =====================================================================
        //  CASE 6 - the WO-1012 P3 8-beat arc holds its shape (2026-08-10)
        // =====================================================================

        /// <summary>The owner's dynamic arc (WO-1012 §2c): ARRIVE, WALK, BUILD ONE, ACK,
        /// ONE CANNON, TIMERS, ENEMIES AT THE GATE, WIN + HANDOFF — by step id, in order.</summary>
        private static readonly string[] ArcIds =
        {
            "founding_greet", "founding_walk", "founding_stores", "founding_ack",
            "founding_defense", "founding_timers", "founding_defend", "founding_win",
        };

        private static void Case6_ArcShape(List<Step> steps, List<string> failures, List<string> notes)
        {
            // (a) The mandatory chain is EXACTLY the 8 arc beats, in order.
            var mandatory = new List<Step>();
            foreach (var s in steps) if (!s.Contextual) mandatory.Add(s);
            mandatory.Sort((x, y) => x.Order.CompareTo(y.Order));

            if (mandatory.Count != ArcIds.Length)
                failures.Add("[arc-shape] mandatory chain has " + mandatory.Count + " steps - the WO-1012 P3 arc is " +
                             "exactly " + ArcIds.Length + " beats (owner 2026-08-09/10; supersedes the 7-step 2026-07-24 chain)");
            int n = Math.Min(mandatory.Count, ArcIds.Length);
            for (int i = 0; i < n; i++)
                if (!string.Equals(mandatory[i].Id, ArcIds[i], StringComparison.OrdinalIgnoreCase))
                    failures.Add("[arc-shape] beat " + (i + 1) + " is '" + mandatory[i].Id + "' - the arc authors '" +
                                 ArcIds[i] + "' there (ARRIVE/WALK/BUILD ONE/ACK/ONE CANNON/TIMERS/ENEMIES/WIN)");

            var byId = new Dictionary<string, Step>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in steps) if (!string.IsNullOrEmpty(s.Id) && !byId.ContainsKey(s.Id)) byId[s.Id] = s;

            // (b) ARRIVE wakes the guide: the starterPet grant rides founding_greet (the
            //     pet-Echo must exist before it speaks - WO-1012 P2 enter-side rule).
            if (byId.TryGetValue("founding_greet", out var greet) && !greet.GrantStarterPet)
                failures.Add("[arc-shape] founding_greet no longer carries grant.starterPet - the guide IS the pet-Echo " +
                             "and must be granted on the ARRIVE beat, before its first line plays");

            // (c) WALK completes on the follow-proximity signal, and the anchor resolves:
            //     TutorialWorldAnchors must actually implement the 'guide_gate' case.
            if (byId.TryGetValue("founding_walk", out var walk))
            {
                if (!string.Equals(walk.Signal, DeNelle.Core.Tutorial.TutorialSignals.GuideGateReached, StringComparison.OrdinalIgnoreCase))
                    failures.Add("[arc-shape] founding_walk completes on '" + (walk.Signal ?? "<null>") + "' - the WALK beat's " +
                                 "contract is '" + DeNelle.Core.Tutorial.TutorialSignals.GuideGateReached + "' (follow-proximity, " +
                                 "the guide leads via PetHeroLeash)");
                string anchorsSrc = ReadText("Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs", failures);
                if (anchorsSrc != null && !StripComments(anchorsSrc).Contains("\"guide_gate\""))
                    failures.Add("[arc-shape] TutorialWorldAnchors no longer resolves the 'guide_gate' anchor - the WALK " +
                                 "beat's hero.reached:guide_gate can never be raised (TryResolveAnchor returns false forever)");
            }

            // (d) ENEMIES AT THE GATE completes on the band-scoped signal, and TutorialFlow
            //     arms the scripted spawner on it (not only on legacy wave.cleared).
            if (byId.TryGetValue("founding_defend", out var defend))
            {
                if (!string.Equals(defend.Signal, DeNelle.Core.Tutorial.TutorialSignals.TutorialBandRepelled, StringComparison.OrdinalIgnoreCase))
                    failures.Add("[arc-shape] founding_defend completes on '" + (defend.Signal ?? "<null>") + "' - the payoff " +
                                 "beat's contract is '" + DeNelle.Core.Tutorial.TutorialSignals.TutorialBandRepelled + "' so an " +
                                 "ambient wave clear can never complete it");
                string flowSrc = ReadText(TutorialFlowSrc, failures);
                if (flowSrc != null)
                {
                    string code = StripComments(flowSrc);
                    int arm = code.IndexOf("StartScriptedTownWave", StringComparison.Ordinal);
                    if (arm < 0 || code.IndexOf("TutorialBandRepelled", StringComparison.Ordinal) < 0)
                        failures.Add("[arc-shape] TutorialFlow does not key StartScriptedTownWave/TickScriptedWave on " +
                                     "TutorialSignals.TutorialBandRepelled - the ENEMIES beat's band never spawns or never completes");
                }
            }

            // (e) WIN + HANDOFF never ends in silence: its OUTRO's dialogue.ended id is the
            //     first nudge's trigger (owner directive 2026-08-10, the "now what?" gap).
            byId.TryGetValue("founding_win", out var win);
            byId.TryGetValue("ctx_build_weapons", out var nudge1);
            byId.TryGetValue("ctx_build_armor", out var nudge2);
            if (win == null || string.IsNullOrEmpty(win.OutroDialogue))
                failures.Add("[arc-shape] founding_win has no OUTRO dialogue - the handoff ends in silence and the 2c-bis " +
                             "nudge chain has no trigger (owner 2026-08-10: 'okay. Now what?')");
            if (nudge1 == null)
                failures.Add("[arc-shape] ctx_build_weapons (2c-bis nudge 1, the weapons building) is missing");
            if (nudge2 == null)
                failures.Add("[arc-shape] ctx_build_armor (2c-bis nudge 2, the armor building) is missing");
            if (win != null && !string.IsNullOrEmpty(win.OutroDialogue) && nudge1 != null)
            {
                string expected = DeNelle.Core.Tutorial.TutorialSignals.DialogueEndedPrefix + win.OutroDialogue;
                if (!string.Equals(nudge1.TriggerSignal, expected, StringComparison.OrdinalIgnoreCase))
                    failures.Add("[arc-shape] ctx_build_weapons triggers on '" + (nudge1.TriggerSignal ?? "<null>") + "' but the " +
                                 "WIN outro raises '" + expected + "' - the nudge chain is disconnected from the handoff");
            }
            if (nudge2 != null &&
                !string.Equals(nudge2.TriggerSignal,
                    DeNelle.Core.Tutorial.TutorialSignals.StructurePlacedPrefix + "workshop", StringComparison.OrdinalIgnoreCase))
                failures.Add("[arc-shape] ctx_build_armor triggers on '" + (nudge2.TriggerSignal ?? "<null>") + "' - the chain " +
                             "contract is build.structure_placed:workshop (armor follows the weapons roof)");

            // (f) Nudges are nudges: oneShot, never pausePressure (never blocking).
            foreach (var nudge in new[] { nudge1, nudge2 })
            {
                if (nudge == null) continue;
                if (!nudge.OneShot)
                    failures.Add("[arc-shape] '" + nudge.Id + "' is not oneShot:true - a nudge that repeats is nagging, not guidance");
                if (nudge.PausePressure)
                    failures.Add("[arc-shape] '" + nudge.Id + "' sets pausePressure - a post-handoff nudge must never gate free play");
            }

            // (g) The nudged buildings exist in the catalog (id workshop = weapons, id forge
            //     = armor - QR-5.7). A nudge toward a card that cannot render is the seq 632
            //     defect wearing a new hat.
            string catRaw = ReadText(StructuresRes, failures);
            if (catRaw != null)
            {
                var entries = JObject.Parse(catRaw)["entries"] as JArray;
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (entries != null) foreach (var e in entries) { string id = (string)e["id"]; if (!string.IsNullOrEmpty(id)) ids.Add(id); }
                foreach (var want in new[] { "workshop", "forge" })
                    if (!ids.Contains(want))
                        failures.Add("[arc-shape] nudge-chain building id '" + want + "' does not resolve in structures-catalog.json");
            }

            notes.Add("arc-shape: " + n + "/" + ArcIds.Length + " beats verified in order; nudge chain past " +
                      "weapons->armor is an OWNER CREATIVE PIN (WO-1012 2c-bis) - do not author more without her sequence");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static string ReadText(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[source] missing file: " + path);
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[source] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and /* */ comments so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }
    }
}
