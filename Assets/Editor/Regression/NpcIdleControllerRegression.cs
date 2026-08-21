// =============================================================================
// NpcIdleControllerRegression [npc-idle-controller] — pins that a townsperson idles
// like a townsperson, not like a knight waiting to be attacked.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Markers: NPC_IDLE_CONTROLLER_OK / _FAIL.
//
// OWNER, 2026-08-20, on the quest cast: "replace the quest kaykat for a person",
// "they need to use ide not combat idle", "they have full access to the regular
// controller animations", "they are human rig".
//
// THE DEFECT THIS EXISTS FOR, and why it was invisible:
//
// WO-833 armed staged KayKit bodies with a shared controller, because a KayKit FBX
// imports Humanoid with an avatar but NO controller and would otherwise render its
// bind pose ("NPC Stuck in T Pose"). That controller, KayKitNpcIdle, plays
// m-standby-idle out of Assets/Action/Knight/Motion/studio-mocap-series-magical-moves —
// the HERO'S COMBAT STANDBY. Correct for a knight; wrong for a shopkeeper.
//
// Then PROD-002 retagged every structure row to a purchased CraftPix person, and
// KayKitNpcBody.Load happily returns a resolvedRes for those slugs too. The arming
// call sites gated on `resolvedRes != null` — which is true for BOTH pack families —
// so all twelve vendor speakers plus the barracks drillmaster silently had their
// civilian controller REPLACED by the knight's fighting stance. Nothing threw,
// nothing logged, nothing rendered wrong: the NPCs simply stood in a combat guard in
// a peaceful town, which is only detectable by looking at them.
//
// THE CASES:
//
//   1. [arm-guard]     Every ArmIdle call site must gate on KayKitNpcBody.IsKayKitPath,
//                      not on a null check. This is the actual bug, and a null check
//                      re-appearing is the actual regression. Source-scanned because
//                      the guard is a control-flow property, not a value any runtime
//                      probe can hand back.
//
//   2. [one-rule]      The slug -> Resources path rule lives in exactly ONE method
//                      (KayKitNpcBody.ResolveResPath). A second copy is how this repo
//                      has already lost a WO number block and a dependency table:
//                      the same fact in two files drifts, and here a drifted copy
//                      would silently re-point bodies at the wrong pack folder.
//
//   3. [quest-person]  No quest-cast row may name a bare KayKit slug. The owner asked
//                      for people; a bare slug resolves under NPCs/KayKit/ and would
//                      drag the combat controller back in with it.
//
//   4. [resolves]      Every quest-cast body slug must actually load from Resources.
//                      A typo does not fail to compile — it degrades to the People
//                      fallback chain and the owner sees the wrong character.
//
//   5. [self-armed]    Each quest-cast person prefab must carry its OWN Animator
//                      controller. This is the load-bearing half of case 1: skipping
//                      ArmIdle is only safe BECAUSE the person is already animated.
//                      If a future prefab rebuild drops the controller override, case
//                      1 stops being a fix and starts being a T-pose.
//
//   6. [civilian]      That controller must not be the KayKit combat one, and its
//                      clips must not come from the Knight combat mocap tree. Checking
//                      the controller's identity alone would pass a future controller
//                      that was rebuilt from the same combat clips.
//
//   7. [no-twin]       A quest-cast member anchored at a building must not wear the
//                      body that building's own vendor wears. Twins standing five
//                      metres apart read as a bug to a player, and the pool is small
//                      enough (14 bodies) that this is easy to do by accident.
//
// SOURCE-SCAN NOTE: cases 1-3 read the .cs text. That is deliberate and narrow — they
// pin CONTROL FLOW and AUTHORSHIP, which no reflected value exposes. Everything that
// can be proven against a real loaded asset (4-7) is, because a regex over source is
// weaker evidence than the asset itself.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.NpcIdleControllerRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class NpcIdleControllerRegression
    {
        // The three injectors that arm an idle controller onto a spawned NPC body.
        private static readonly string[] ArmCallSites =
        {
            "Assets/_Modules/Village/NPCs/QuestCastNpcInjector.cs",
            "Assets/_Modules/Village/NPCs/CastleVendorNpcInjector.cs",
            "Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs",
        };

        private const string BodyResolver = "Assets/_Modules/Village/NPCs/KayKitNpcBody.cs";
        private const string QuestInjector = "Assets/_Modules/Village/NPCs/QuestCastNpcInjector.cs";
        private const string CatalogPath = "Assets/Resources/Data/Canonical/structures-catalog.json";

        /// <summary>The combat mocap tree KayKitNpcIdle draws from. A townsperson clip must
        /// never resolve into it.</summary>
        private const string CombatMocapFolder = "Assets/Action/Knight/Motion";

        /// <summary>The KayKit pack folder, as a Resources path prefix.</summary>
        private const string KayKitFolder = "NPCs/KayKit/";

        [MenuItem("Defenders/Regression/NPC Idle Controller")]
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("NPC_IDLE_CONTROLLER_OK - " + reason);
            else Debug.LogError("NPC_IDLE_CONTROLLER_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            // ── 1. Every ArmIdle call site gates on the pack, not on null ───────────
            Case(failures, "arm-guard", () =>
            {
                foreach (string path in ArmCallSites)
                {
                    if (!File.Exists(path)) { failures.Add($"[arm-guard] missing call site {path}"); continue; }
                    string src = File.ReadAllText(path);

                    foreach (Match m in Regex.Matches(src, @"^[^\r\n/]*\bArmIdle\s*\(", RegexOptions.Multiline))
                    {
                        // Walk back to the start of the statement's line and read its guard.
                        int lineStart = src.LastIndexOf('\n', m.Index) + 1;
                        int lineEnd = src.IndexOf('\n', m.Index);
                        string line = src.Substring(lineStart, (lineEnd < 0 ? src.Length : lineEnd) - lineStart);

                        if (!line.Contains("IsKayKitPath"))
                        {
                            failures.Add($"[arm-guard] {Path.GetFileName(path)}: ArmIdle is called without " +
                                         "KayKitNpcBody.IsKayKitPath guarding it -> \"" + line.Trim() + "\". " +
                                         "A null check is NOT enough: KayKitNpcBody.Load returns a non-null path " +
                                         "for CraftPixPeople slugs too, so this arms a person that already has a " +
                                         "civilian controller with the Knight's combat standby idle.");
                        }
                    }
                }
                notes.Add($"arm-guard {ArmCallSites.Length} call sites gated on IsKayKitPath");
            });

            // ── 2. The slug -> path rule has exactly one home ───────────────────────
            Case(failures, "one-rule", () =>
            {
                var offenders = new List<string>();
                foreach (string path in Directory.GetFiles("Assets/_Modules/Village/NPCs", "*.cs"))
                {
                    string src = File.ReadAllText(path);
                    // The rule is literally 'slug.Contains("/") ? NpcResourcesRoot + ... : ResourcesRoot + ...'.
                    // Any file OTHER than the resolver re-deriving it is a second copy.
                    bool rederives = Regex.IsMatch(src, @"Contains\s*\(\s*""/""\s*\)\s*\?[^;]*ResourcesRoot");
                    if (rederives && !path.Replace('\\', '/').EndsWith("KayKitNpcBody.cs"))
                        offenders.Add(Path.GetFileName(path));
                }
                if (offenders.Count > 0)
                    failures.Add("[one-rule] the slug->Resources path rule is re-derived outside " +
                                 $"KayKitNpcBody.ResolveResPath in: {string.Join(", ", offenders)}. " +
                                 "Call ResolveResPath instead - a second copy of this rule drifts, and a " +
                                 "drifted copy points bodies at the wrong pack folder silently.");
                else notes.Add("one-rule single resolver");
            });

            // ── 3-7. The quest cast, read off the source rows ───────────────────────
            var cast = new List<(string name, string slug, string anchor)>();
            Case(failures, "quest-person", () =>
            {
                string src = File.ReadAllText(QuestInjector);

                // Parse the CastMember INITIALISER BLOCKS, then pull fields from inside each one.
                // A single flat "Name = ... BodySlug = ..." regex over the file is what the first
                // draft did, and it silently paired the Elder's slug with the holder GameObject's
                // name ("QuestCastNPCs (runtime)") from hundreds of lines earlier - the assertions
                // still ran on the right slug, but a future failure message would have named the
                // WRONG NPC. An oracle that misidentifies its subject is worse than a loud one.
                foreach (Match block in Regex.Matches(src, @"new\s+CastMember\s*\{(?<body>[^{}]*)\}"))
                {
                    string b = block.Groups["body"].Value;
                    var name = Regex.Match(b, @"\bName\s*=\s*""([^""]*)""");
                    var slug = Regex.Match(b, @"\bBodySlug\s*=\s*""([^""]*)""");
                    var anchor = Regex.Match(b, @"\bAnchorBuildingId\s*=\s*(null|""[^""]*"")");
                    if (!name.Success || !slug.Success) continue;
                    // A literal `null` anchor means "stands at the Heart" — normalise it to a real
                    // null so [no-twin] skips instead of hunting a structure called "null".
                    string anchorId = anchor.Success ? anchor.Groups[1].Value : "null";
                    if (anchorId == "null") anchorId = null; else anchorId = anchorId.Trim('"');
                    cast.Add((name.Groups[1].Value, slug.Groups[1].Value, anchorId));
                }

                if (cast.Count == 0)
                {
                    failures.Add("[quest-person] parsed ZERO quest-cast rows out of QuestCastNpcInjector. " +
                                 "The CastMember shape changed - fix this suite deliberately rather than " +
                                 "letting it pass vacuously (a suite that asserts nothing is worse than none).");
                    return;
                }

                foreach (var c in cast)
                {
                    if (!c.slug.Contains("/"))
                        failures.Add($"[quest-person] '{c.name}' wears bare slug '{c.slug}', which resolves " +
                                     $"under {KayKitFolder} and drags the combat idle back with it. The owner " +
                                     "asked for people: use a folder-qualified person slug.");
                }
                notes.Add($"quest-person {cast.Count} rows, all folder-qualified");
            });

            foreach (var member in cast)
            {
                var c = member;   // capture per iteration
                string res = c.slug.Contains("/") ? "NPCs/" + c.slug : KayKitFolder + c.slug;

                GameObject prefab = null;
                Case(failures, "resolves", () =>
                {
                    prefab = Resources.Load<GameObject>(res);
                    if (prefab == null)
                        failures.Add($"[resolves] '{c.name}': body '{c.slug}' does not load from " +
                                     $"Resources/{res}. At runtime this degrades to the People fallback " +
                                     "chain, so the owner sees the WRONG character rather than an error.");
                });
                if (prefab == null) continue;

                // ── 5. The person animates itself ───────────────────────────────────
                RuntimeAnimatorController ctrl = null;
                Case(failures, "self-armed", () =>
                {
                    var anim = prefab.GetComponentInChildren<Animator>(true);
                    if (anim == null)
                    {
                        failures.Add($"[self-armed] '{c.name}': prefab {res} has no Animator. Skipping ArmIdle " +
                                     "is only safe because the person animates itself - with no Animator this " +
                                     "NPC stands in its bind pose.");
                        return;
                    }
                    ctrl = anim.runtimeAnimatorController;
                    if (ctrl == null)
                        failures.Add($"[self-armed] '{c.name}': prefab {res} has an Animator with NO controller. " +
                                     "The ArmIdle skip turns this into a T-pose. Restore the controller override " +
                                     "on the prefab (AC_CraftPixTownsfolk) rather than re-arming the combat idle.");
                });
                if (ctrl == null) continue;

                // ── 6. And it animates itself like a civilian ───────────────────────
                Case(failures, "civilian", () =>
                {
                    if (ctrl.name.Contains("KayKitNpcIdle"))
                    {
                        failures.Add($"[civilian] '{c.name}': wears KayKitNpcIdle, which plays the Knight's " +
                                     "combat standby. That is the exact defect this suite exists for.");
                        return;
                    }

                    var combatClips = new List<string>();
                    foreach (var clip in ctrl.animationClips ?? Array.Empty<AnimationClip>())
                    {
                        if (clip == null) continue;
                        string src = AssetDatabase.GetAssetPath(clip) ?? string.Empty;
                        if (src.Replace('\\', '/').StartsWith(CombatMocapFolder))
                            combatClips.Add($"{clip.name} <- {src}");
                    }
                    if (combatClips.Count > 0)
                        failures.Add($"[civilian] '{c.name}': controller '{ctrl.name}' sources clips from the " +
                                     $"Knight COMBAT mocap tree ({CombatMocapFolder}): " +
                                     string.Join("; ", combatClips) + ". Checking the controller's NAME alone " +
                                     "would have passed this - the clips are what the player sees.");
                    else
                        notes.Add($"{c.name}->{ctrl.name}");
                });

                // ── 7. No twin at the same anchor ───────────────────────────────────
                Case(failures, "no-twin", () =>
                {
                    if (string.IsNullOrEmpty(c.anchor)) return;
                    if (!File.Exists(CatalogPath)) return;

                    string json = File.ReadAllText(CatalogPath);
                    // Find the anchoring structure's own npcModel: the nearest npcModel that
                    // follows its id. Deliberately simple - a miss just skips the case rather
                    // than inventing a failure.
                    var idm = Regex.Match(json, "\"id\"\\s*:\\s*\"" + Regex.Escape(c.anchor) + "\"");
                    if (!idm.Success) return;
                    var nm = Regex.Match(json.Substring(idm.Index), "\"npcModel\"\\s*:\\s*\"([^\"]+)\"");
                    if (!nm.Success) return;

                    string anchorBody = nm.Groups[1].Value;
                    if (string.Equals(anchorBody, c.slug, StringComparison.OrdinalIgnoreCase))
                        failures.Add($"[no-twin] '{c.name}' is anchored at '{c.anchor}' and wears the SAME body " +
                                     $"('{c.slug}') as that building's own vendor. Two identical faces standing " +
                                     "metres apart reads as a bug. Retag one of them - the pool has 14 bodies.");
                    else
                        notes.Add($"no-twin {c.name}@{c.anchor} != {anchorBody}");
                });
            }

            if (failures.Count > 0)
            {
                reason = $"{failures.Count} failure(s): {string.Join(" | ", failures)}";
                return false;
            }
            reason = string.Join("; ", notes);
            return true;
        }

        // Guard each case so one throw becomes a labelled failure, not a dead suite.
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
