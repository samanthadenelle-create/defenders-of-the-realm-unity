// =============================================================================
// DungeonEgressRegression [dungeon-egress] — pins HOW MANY WAYS OUT a dungeon has,
// and WHERE the authored one is.
// -----------------------------------------------------------------------------
// THE RULING THIS SUITE EXISTS TO ENFORCE (owner, F8 seq 2508, 2026-08-15, verbatim):
//   "why can we leave the dungeon in the middle of stairs. Should be single entry
//    point in maybe 2 total out"
//   "the dungeons should be confusing" / "im not trying to make them easy"
//   "generally 1 after the treaure room that exists to back of dungeon"
//
// SO A CONTENT DUNGEON HAS EXACTLY TWO EGRESS POINTS:
//   1. THE FRONT — the runtime-injected true exit at layout.exitRoomId (the room you
//      walked in through). Seated by DungeonExitSpawner.TryInject.
//   2. THE BACK — ONE authored `extracts` entry, in the DEEPEST room, which is also
//      the room DungeonTreasureCache seats the reward in. Past the treasure, at the
//      back of the dungeon. It is the candidate seam to a future zone (WO-827),
//      which is why it is a door onward and not a mid-run bail-out.
//
// WHAT WENT WRONG, WHICH IS WHY THE COUNT IS NOW PINNED
// Every StairwellRoom authored its own "Leave" pad: 5 in dg_ember_deep, 4 in
// dg_bonecrypt, 4 in dg_sunken_vault — 13 pads, plus one injected true exit each, so
// SIX ways out of dg_ember_deep. Every stair landing was an opt-out, so no floor could
// read as deep. Nothing asserted the count, so the pads accreted one dungeon at a
// time and no gate ever went red. They were added as anti-stranding insurance that was
// never needed: dungeon scenes are absent from scene-configs.json, so
// SceneOwnership.IsEnemyOwned is FALSE and death respawns the hero IN PLACE.
//
// THE ORACLE IS READ, NEVER RE-TYPED
//   * "the treasure room" is not a hardcoded room id here — it is whatever
//     DungeonTreasureCache.ResolveDeepestRoomId returns, invoked by reflection (it is
//     `internal` to DeNelle.Dungeons). Retyping "warlord_keep" would make this suite
//     agree with a stale copy of the layout instead of with the shipping code.
//   * "the pads are named Extract_*" is proven BEHAVIOURALLY by running the real
//     DungeonBaker.PlaceComposeExtracts, not by grepping for the literal. That name is
//     the discriminator DungeonExitSpawner.TryInject uses to tell a baked pad from an
//     injected return exit — rename it and the FRONT exit stops being injected at all.
//
// ⚠ THE CONTROL GROUP IS EXEMPT AND ITS EXEMPTION IS ASSERTED, NOT ASSUMED.
// dg_stair_rig and dg_descent_probe are WO-930's quarantined A/B fixtures (see the
// DungeonMultiLevelRegression header: "DO NOT DELETE"). Case 3 goes RED if a tidy-up
// applies the content trim to them, because losing the control group destroys the
// ability to re-run the comparison that proved the StairwellRoom model.
//
// Marker: DUNGEON_EGRESS_OK / DUNGEON_EGRESS_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!DungeonEgressRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-egress] " + r);
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.Regression
{
    public static class DungeonEgressRegression
    {
        // Runtime-winning copy (Resources beats StreamingAssets — CLAUDE.md canonical-JSON law).
        private const string ResourcesDir = "Assets/Resources/Data/Canonical/dungeon-layouts";
        private const string StreamingDir = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts";
        // The UPSTREAM authoring source. GraphDungeonComposer emits a layout FROM these, copying
        // `extracts` verbatim - so this is where a trimmed pad grows back if it is not trimmed here too.
        private const string GraphsDir = "Assets/Resources/Data/Canonical/dungeon-graphs";
        private const string GraphsStreamingDir = "Assets/StreamingAssets/Data/Canonical/dungeon-graphs";

        /// <summary>The SHIPPING dungeons. Content, not fixtures — the trim applies to these.</summary>
        private static readonly string[] ContentLayouts =
        {
            "dg_ember_deep",
            "dg_bonecrypt",
            "dg_sunken_vault",
        };

        /// <summary>WO-930 A/B control group. Exempt from the trim; their extracts are FIXTURES.</summary>
        private static readonly string[] ControlGroupLayouts =
        {
            "dg_stair_rig",
            "dg_descent_probe",
        };

        /// <summary>Max authored extracts a CONTENT layout may carry. One = the back exit.</summary>
        private const int MaxContentExtracts = 1;

        private static readonly List<GameObject> s_spawned = new List<GameObject>();

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- DUNGEON EGRESS (one front exit + one back exit, and no more) ---");

            try
            {
                Case1_ContentAuthorsAtMostOneBackExit(failures, notes, log);
                Case2_TheBackExitSitsInTheTreasureRoom(failures, notes, log);
                Case3_ControlGroupUntouched(failures, notes, log);
                Case4_PadNamingCannotSuppressTheFrontExit(failures, notes, log);
                Case5_BothCanonicalCopiesAgree(failures, notes, log);
                Case6_UpstreamGraphsCannotRegrowThePads(failures, notes, log);
            }
            finally
            {
                Cleanup();
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "DUNGEON_EGRESS_OK");
                reason = $"DUNGEON EGRESS OK -- {ContentLayouts.Length} content layout(s) author <= " +
                         $"{MaxContentExtracts} back exit, each seated in the treasure room, with the " +
                         "WO-930 control group untouched" + noteStr;
                return true;
            }
            reason = "dungeon-egress: " + string.Join("; ", failures) + noteStr;
            Debug.LogError(log.ToString() + "DUNGEON_EGRESS_FAIL: " + reason);
            return false;
        }

        // ── Case 1 — the count. THE case the 13 pads would have failed. ──────────────
        private static void Case1_ContentAuthorsAtMostOneBackExit(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            int checkedCount = 0;
            foreach (string id in ContentLayouts)
            {
                var layout = LoadLayout(id, failures, "[egress-count]");
                if (layout == null) continue;
                checkedCount++;

                int n = layout.extracts != null ? layout.extracts.Count : 0;
                log.AppendLine($"  {id}: {n} authored extract(s), exitRoomId='{layout.exitRoomId}'");

                if (n > MaxContentExtracts)
                    failures.Add($"[egress-count] {id} authors {n} extracts (max {MaxContentExtracts}). " +
                                 "Owner ruling F8 seq 2508: a dungeon has ONE entry and ONE back exit. " +
                                 "Every extra pad is a mid-run opt-out that flattens the descent - this is " +
                                 "exactly the 13-pad state that made every stair landing a way out.");

                // The FRONT exit must still be designated. An empty exitRoomId does not blank the
                // exit (DungeonExitSpawner falls back to "entry"), but it means the layout stopped
                // SAYING where its front door is, and a rename of the entry room would then move it
                // silently. Assert the designation resolves to a real authored room.
                if (string.IsNullOrEmpty(layout.exitRoomId))
                {
                    failures.Add($"[egress-count] {id} has no exitRoomId - the FRONT exit designation is gone, " +
                                 "so the injected return exit falls back to the 'entry' convention by luck");
                }
                else if (!HasRoom(layout, layout.exitRoomId))
                {
                    failures.Add($"[egress-count] {id} exitRoomId='{layout.exitRoomId}' names no authored room - " +
                                 "DungeonExitSpawner.ResolveExitPosition would Warn and fall back, and the front " +
                                 "exit would move to wherever 'entry' happens to be");
                }
            }

            if (checkedCount == 0)
                failures.Add("[egress-count] NO content layout was checked - every one failed to load, so this " +
                             "case passed on nothing. A test that cannot fail advertises coverage that does not exist.");
            else
                notes.Add($"[egress-count] extract ceiling checked on {checkedCount} content layout(s)");
        }

        // ── Case 2 — the placement. "1 after the treaure room". ──────────────────────
        private static void Case2_TheBackExitSitsInTheTreasureRoom(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            // The oracle is the SHIPPING resolver, invoked by reflection (internal to
            // DeNelle.Dungeons). Hardcoding "warlord_keep" here would pin a copy of the answer.
            // ⚠ The resolver lives on DungeonTreasureSpawner (DungeonTreasureCache.cs:148),
            // NOT on DungeonTreasureCache — an earlier draft asked the wrong type and this case
            // hard-failed on a lookup miss rather than on a real placement defect. Both types are
            // probed so a future move fails LOUD only when the method genuinely disappears.
            var cacheT = FindType("DeNelle.Dungeons.DungeonTreasureSpawner")
                         ?? FindType("DeNelle.Dungeons.DungeonTreasureCache");
            MethodInfo deepest = cacheT?.GetMethod("ResolveDeepestRoomId",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (deepest == null)
            {
                failures.Add("[egress-placement] ResolveDeepestRoomId not found on DungeonTreasureSpawner " +
                             "or DungeonTreasureCache " +
                             "(renamed/removed) - the treasure room can no longer be resolved, so this case " +
                             "cannot prove the back exit is PAST the reward rather than next to the entrance");
                return;
            }

            int checkedCount = 0;
            foreach (string id in ContentLayouts)
            {
                var layout = LoadLayout(id, failures, "[egress-placement]");
                if (layout == null) continue;
                string treasureRoom = null;
                try { treasureRoom = deepest.Invoke(null, new object[] { id }) as string; }
                catch (Exception ex)
                {
                    failures.Add($"[egress-placement] {id} ResolveDeepestRoomId threw " +
                                 $"{ex.GetType().Name}: {ex.Message}");
                    continue;
                }
                if (string.IsNullOrEmpty(treasureRoom))
                {
                    failures.Add($"[egress-placement] {id} ResolveDeepestRoomId returned nothing - the layout " +
                                 "is not loadable from Resources at check time, so neither the treasure nor " +
                                 "this assertion can be seated");
                    continue;
                }
                checkedCount++;

                // A one-way expedition may designate its ONE full portal directly in the
                // treasure room and author no secondary extract. This is Ember Deep's owner-
                // approved shape: arrival marker only at entry, one exit after the Warlord.
                if (layout.extracts == null || layout.extracts.Count == 0)
                {
                    log.AppendLine($"  {id}: single true exit in '{layout.exitRoomId}' " +
                                   $"(treasure room = '{treasureRoom}'), no secondary extract");
                    if (!string.Equals(layout.exitRoomId, treasureRoom, StringComparison.OrdinalIgnoreCase))
                        failures.Add($"[egress-placement] {id} authors no back extract, but its only true exit " +
                                     $"is in '{layout.exitRoomId}' instead of treasure room '{treasureRoom}' - " +
                                     "clearing the dungeon would dead-end or require a long walk back");
                    continue;
                }

                var back = layout.extracts[0];
                log.AppendLine($"  {id}: back exit '{back?.id}' in room '{back?.roomId}' " +
                               $"(treasure room = '{treasureRoom}')");

                if (back == null || !string.Equals(back.roomId, treasureRoom, StringComparison.OrdinalIgnoreCase))
                    failures.Add($"[egress-placement] {id} back exit sits in '{back?.roomId ?? "<null>"}' but the " +
                                 $"treasure room is '{treasureRoom}'. The owner's shape is \"1 after the treaure " +
                                 "room that exists to back of dungeon\" - an exit anywhere shallower is a " +
                                 "shortcut out, not a door onward.");

                // A back exit seated at the FRONT room would be two doors in one place.
                if (back != null && !string.IsNullOrEmpty(layout.exitRoomId) &&
                    string.Equals(back.roomId, layout.exitRoomId, StringComparison.OrdinalIgnoreCase))
                    failures.Add($"[egress-placement] {id} back exit is in the same room as the FRONT exit " +
                                 $"('{layout.exitRoomId}') - that is one door drawn twice, not two exits");

                // The offset must actually move it off the room centre, where the treasure sits.
                if (back != null && (back.offset == null || back.offset.Length < 3 ||
                    (Mathf.Abs(back.offset[0]) < 1f && Mathf.Abs(back.offset[2]) < 1f)))
                    failures.Add($"[egress-placement] {id} back exit has no meaningful lateral offset " +
                                 "(RoomSeat applies it in WORLD space from the room's bounds centre, which is " +
                                 "where DungeonTreasureCache seats the reward) - the pad would be stacked on " +
                                 "top of the treasure instead of at the back wall");
            }

            if (checkedCount == 0)
                failures.Add("[egress-placement] NO content layout resolved a treasure room - this case passed " +
                             "on nothing.");
            else
                notes.Add($"[egress-placement] back-exit seat checked on {checkedCount} content layout(s)");
        }

        // ── Case 3 — the WO-930 control group must NOT be trimmed. ───────────────────
        private static void Case3_ControlGroupUntouched(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            foreach (string id in ControlGroupLayouts)
            {
                string path = Path.Combine(ResourcesDir, id + ".json");
                if (!File.Exists(path))
                {
                    failures.Add($"[egress-control] {id}.json is GONE. It is a WO-930 A/B fixture, not stale " +
                                 "content (DungeonMultiLevelRegression header: DO NOT DELETE). Removing it " +
                                 "destroys the ability to re-run the comparison that proved the StairwellRoom model.");
                    continue;
                }
                var layout = LoadLayout(id, failures, "[egress-control]");
                if (layout == null) continue;

                int n = layout.extracts != null ? layout.extracts.Count : 0;
                log.AppendLine($"  {id}: {n} fixture extract(s) (quarantined, exempt from the content trim)");
                if (n < 1)
                    failures.Add($"[egress-control] {id} now authors {n} extracts. The content egress trim was " +
                                 "applied to a QUARANTINED fixture - it is the retired-stair-pair control group " +
                                 "and its extracts are the thing under comparison. Restore them.");
            }
            notes.Add($"[egress-control] {ControlGroupLayouts.Length} quarantined fixture(s) verified intact");
        }

        // ── Case 4 — the pad name is the front exit's own precondition. ──────────────
        private static void Case4_PadNamingCannotSuppressTheFrontExit(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            // DungeonExitSpawner.TryInject skips injecting the front exit when it finds a
            // DungeonExitInteractable whose name does NOT start with "Extract_". So if the baker
            // ever stops prefixing pads, the baked back exit silently EATS the front exit and the
            // dungeon is left with one way out, at the wrong end. Proven by running the real baker.
            var bakerT = FindType("DeNelle.Editor.RoomForge.DungeonBaker");
            MethodInfo place = bakerT?.GetMethod("PlaceComposeExtracts", BindingFlags.NonPublic | BindingFlags.Static);
            if (place == null)
            {
                failures.Add("[egress-padname] DungeonBaker.PlaceComposeExtracts not found (renamed/removed) - " +
                             "nothing else builds the back exit, so its name (the discriminator " +
                             "DungeonExitSpawner.TryInject matches on) cannot be proven");
                return;
            }

            var exitT = FindType("DeNelle.Dungeons.DungeonExitInteractable");
            if (exitT == null)
            {
                failures.Add("[egress-padname] DungeonExitInteractable not found - the pad type is gone");
                return;
            }

            int checkedCount = 0;
            foreach (string id in ContentLayouts)
            {
                var layout = LoadLayout(id, failures, "[egress-padname]");
                if (layout == null) continue;

                var root = new GameObject("__egress_" + id);
                s_spawned.Add(root);
                try
                {
                    place.Invoke(null, new object[]
                    {
                        root.transform, new Dictionary<string, GameObject>(), layout
                    });
                }
                catch (Exception ex)
                {
                    failures.Add($"[egress-padname] {id} PlaceComposeExtracts threw " +
                                 $"{ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                var built = root.GetComponentsInChildren(exitT, true);
                checkedCount++;
                log.AppendLine($"  {id}: baker built {built.Length} pad(s)");
                foreach (var c in built)
                {
                    var comp = c as Component;
                    if (comp == null) continue;
                    if (!comp.gameObject.name.StartsWith("Extract_", StringComparison.Ordinal))
                        failures.Add($"[egress-padname] {id} built a pad named '{comp.gameObject.name}', which " +
                                     "does not start with \"Extract_\". DungeonExitSpawner.TryInject treats any " +
                                     "other name as an already-present return exit and SKIPS injection - the " +
                                     "front door would never be placed and the only way out would be the deepest " +
                                     "room.");
                }
            }

            if (checkedCount == 0)
                failures.Add("[egress-padname] no layout exercised the baker - this case passed on nothing.");
            else
                notes.Add($"[egress-padname] pad naming exercised through the real baker on {checkedCount} layout(s)");
        }

        // ── Case 5 — the dual-copy law (CLAUDE.md: Resources wins, StreamingAssets is fallback). ──
        private static void Case5_BothCanonicalCopiesAgree(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            var all = new List<string>();
            all.AddRange(ContentLayouts);
            all.AddRange(ControlGroupLayouts);

            foreach (string id in all)
            {
                string a = Path.Combine(ResourcesDir, id + ".json");
                string b = Path.Combine(StreamingDir, id + ".json");
                if (!File.Exists(a) || !File.Exists(b))
                {
                    failures.Add($"[egress-dualcopy] {id}.json is missing a canonical copy " +
                                 $"(Resources={File.Exists(a)}, StreamingAssets={File.Exists(b)})");
                    continue;
                }
                string ha = Sha256(a), hb = Sha256(b);
                if (ha != hb)
                    failures.Add($"[egress-dualcopy] {id}.json DIFFERS between Resources ({ha.Substring(0, 12)}) " +
                                 $"and StreamingAssets ({hb.Substring(0, 12)}). Resources wins at runtime, so an " +
                                 "edit made to only one copy ships a layout nobody reviewed - and the egress " +
                                 "count asserted above would be the count of the wrong file.");
                else
                    log.AppendLine($"  {id}: dual copy identical ({ha.Substring(0, 12)})");
            }
            notes.Add($"[egress-dualcopy] {all.Count} layout(s) hash-compared across both canonical copies");
        }

        // ── Case 6 — the UPSTREAM graph, which is where the pads would grow back. ────
        private static void Case6_UpstreamGraphsCannotRegrowThePads(
            List<string> failures, List<string> notes, StringBuilder log)
        {
            // THE LAYOUT IS NOT THE SOURCE. GraphDungeonComposer:512 copies `graph.extracts`
            // straight into the emitted compose layout, so trimming only the layout leaves the
            // old count sitting in the graph — and the next `Compose` regenerates all of it with
            // no warning, no diff anyone reads, and no gate going red. The graph is the file the
            // trim has to hold in, which is why this case exists at all: it is the ONLY one that
            // covers the regrowth path.
            foreach (string id in ContentLayouts)
            {
                string path = Path.Combine(GraphsDir, id + ".json");
                if (!File.Exists(path))
                {
                    notes.Add($"[egress-graph] no upstream graph for '{id}' (layout is hand-authored) - " +
                              "nothing can regenerate its extracts");
                    continue;
                }

                int n;
                try
                {
                    var graph = JsonConvert.DeserializeObject<DungeonComposeGraphExtractsView>(File.ReadAllText(path));
                    n = graph?.extracts != null ? graph.extracts.Count : 0;
                }
                catch (Exception ex)
                {
                    failures.Add($"[egress-graph] {id} graph failed to parse ({ex.GetType().Name}: {ex.Message})");
                    continue;
                }

                log.AppendLine($"  {id} (graph): {n} authored extract(s)");
                if (n > MaxContentExtracts)
                    failures.Add($"[egress-graph] {id}'s GRAPH authors {n} extracts (max {MaxContentExtracts}) " +
                                 "while its layout is trimmed. GraphDungeonComposer copies graph.extracts into " +
                                 "the layout verbatim, so the next re-compose silently restores every pad the " +
                                 "owner asked to be removed. Trim the graph, not just the layout.");

                // Dual copy, same law as Case 5 - the composer reads StreamingAssets.
                string other = Path.Combine(GraphsStreamingDir, id + ".json");
                if (!File.Exists(other))
                    failures.Add($"[egress-graph] {id} graph missing its StreamingAssets copy");
                else if (Sha256(path) != Sha256(other))
                    failures.Add($"[egress-graph] {id} graph DIFFERS between Resources and StreamingAssets - " +
                                 "the composer reads StreamingAssets, so a trim applied to only the Resources " +
                                 "copy would be undone by the very next compose");
            }
            notes.Add("[egress-graph] upstream compose graphs checked for pad regrowth");
        }

        /// <summary>
        /// Minimal view over a dungeon-graph JSON — only the field this suite asserts on.
        /// Deliberately NOT the full graph model: this suite must keep working if the graph
        /// schema grows, and a strict full-model bind would fail on an unrelated new field.
        /// </summary>
        private sealed class DungeonComposeGraphExtractsView
        {
            [JsonProperty("extracts")] public List<ComposeExtract> extracts;
        }

        // ---- helpers --------------------------------------------------------
        private static DungeonComposeLayout LoadLayout(string id, List<string> failures, string tag)
        {
            string path = Path.Combine(ResourcesDir, id + ".json");
            if (!File.Exists(path))
            {
                failures.Add($"{tag} layout missing: {path}");
                return null;
            }
            try
            {
                var layout = JsonConvert.DeserializeObject<DungeonComposeLayout>(File.ReadAllText(path));
                if (layout == null || layout.rooms == null || layout.rooms.Count == 0)
                {
                    failures.Add($"{tag} {id} deserialized with no rooms - the schema drifted and every " +
                                 "assertion below it would be blind");
                    return null;
                }
                return layout;
            }
            catch (Exception ex)
            {
                failures.Add($"{tag} {id} failed to parse ({ex.GetType().Name}: {ex.Message})");
                return null;
            }
        }

        private static bool HasRoom(DungeonComposeLayout layout, string roomId)
        {
            if (layout?.rooms == null) return false;
            foreach (var r in layout.rooms)
            {
                if (r == null) continue;
                string rid = string.IsNullOrEmpty(r.instanceId) ? r.prefab : r.instanceId;
                if (string.Equals(rid, roomId, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }

        private static void Cleanup()
        {
            foreach (var go in s_spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            s_spawned.Clear();
        }
    }
}
