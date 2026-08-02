// =============================================================================
// DungeonTreasureRegression [dungeon-treasure] (WO-850) - the deepest-room cache.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Dungeons).
//
// Pins the WO-850 contract: "treasure at the deepest room, a simple crafting
// supply", with the two owner rulings baked in (FIXED bundle, no roll; recipe
// unlock on FIRST CLEAR only; prompt -> confirm panel with ONE Take CTA).
//
//   1 [bundle]        Every id in DungeonTreasureCache.FixedBundle resolves to a REAL
//                     row in materials.json - in BOTH the StreamingAssets and Resources
//                     dual-copies, which must be content-identical - and every Count > 0.
//                     A typo id is the whole point: it would deposit a nameless, icon-less
//                     item into the larder and the payout panel would print a raw id.
//   2 [deepest]       DeepestRoomId against the REAL dg_starter_loop layout returns a
//                     non-null room that is NOT the entry, EXISTS in rooms[], matches an
//                     INDEPENDENT BFS oracle computed here from the JSON, and is
//                     DETERMINISTIC across calls (a random / furthest-euclidean pick would
//                     make the chest un-regressable - see the cache's header note that
//                     furthest-euclidean and furthest-by-hops disagree on this layout).
//   3 [deepest-math]  Synthetic layouts pin the PURE math: chain -> the tail; NO
//                     connections -> null (never seat the reward on the entry); equal depth
//                     -> lowest ordinal id; a REVERSED connection still traverses (the
//                     undirected law); depth beats ordinal; missing/empty/null -> null.
//   4 [oneshot]       FirstClearKey is namespaced per dungeon (two dungeons cannot collide)
//                     and rides SeenTutorials (free-form string->bool) - i.e. NO save-schema
//                     bump. A new persisted field would be a silent schema break.
//   5 [panel]         DungeonTreasurePanel source law: built through ElarionUiKit (no
//                     hand-rolled uGUI), the shared Close is RETIRED (single-exit law, owner
//                     F8 seq 628), a "Take" CTA exists, it registers with PanelManager, it is
//                     ASCII-only, and it NEVER grants directly (no VillageInventory).
//   6 [grant-seam]    The cache grants ONLY through DungeonLootGrant and only via the
//                     panel's Take callback - never VillageInventory directly.
//   7 [spawner]       DungeonTreasureSpawner exists and carries its
//                     [RuntimeInitializeOnLoadMethod] hook - without it the chest is never
//                     injected and the deepest room is empty in every shipped build.
//
// Markers: DUNGEON_TREASURE_OK / DUNGEON_TREASURE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.DungeonTreasureRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
//
// ACCESS NOTE: DungeonTreasureCache's math surface (DeepestRoomId / FixedBundle /
// FirstClearKey / EntryRoomId) is `internal` to DeNelle.Dungeons and there is NO
// InternalsVisibleTo anywhere in the tree, so this oracle binds them by REFLECTION.
// A rename therefore FAILS the suite loudly instead of failing to compile - which is
// the behaviour we want from a gate, but see the RESULT note: widening them to public
// would let this file (and the EditMode tests) call them directly.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Dungeons.RoomForge;
using DeNelle.Village.Items;

namespace DeNelle.Editor.Regression
{
    public static class DungeonTreasureRegression
    {
        private const string LayoutSA = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts/dg_starter_loop.json";
        private const string MaterialsSA = "Assets/StreamingAssets/Data/Canonical/materials.json";
        private const string MaterialsRes = "Assets/Resources/Data/Canonical/materials.json";
        private const string CacheSrc = "Assets/_Modules/Dungeons/DungeonTreasureCache.cs";
        private const string PanelSrc = "Assets/_Modules/Dungeons/DungeonTreasurePanel.cs";

        private const string CacheType = "DeNelle.Dungeons.DungeonTreasureCache";
        private const string SpawnerType = "DeNelle.Dungeons.DungeonTreasureSpawner";

        // The captured value at authoring time (2026-08-02) for dg_starter_loop:
        // entry(0) -> corr1(1) -> junction(2) -> loop1(3) -> turn1(4) -> loop2(5)
        // -> turn2(6) -> loop3(7) -> turn3(8). Reported as a NOTE, not a failure -
        // re-authoring the layout legitimately moves the chest; the structural
        // assertions below are what must never break.
        private const string CapturedDeepest = "turn3";
        private const int CapturedDepth = 8;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DUNGEON_TREASURE_OK - " + reason);
            else Debug.LogError("DUNGEON_TREASURE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "bundle", () => Case1_Bundle(failures, notes));
                Case(failures, "deepest", () => Case2_DeepestRealLayout(failures, notes));
                Case(failures, "deepest-math", () => Case3_DeepestMath(failures));
                Case(failures, "oneshot", () => Case4_OneShotKey(failures));
                Case(failures, "panel", () => Case5_PanelLaws(failures));
                Case(failures, "grant-seam", () => Case6_GrantSeam(failures));
                Case(failures, "spawner", () => Case7_SpawnerHook(failures));
            }
            catch (Exception ex)
            {
                failures.Add($"[suite] THREW {ex.GetType().Name}: {ex.Message}");
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DUNGEON TREASURE OK - fixed bundle resolves in both materials.json copies, " +
                         "deepest-room BFS deterministic + matches an independent oracle, pure math " +
                         "(chain/tie/undirected/degenerate), per-dungeon one-shot on SeenTutorials, " +
                         "panel kit+single-exit+Take, single granting seam, spawner hooked" + noteStr;
                return true;
            }
            reason = "dungeon-treasure FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  CASE 1 - the FIXED bundle is real, payable loot
        // =====================================================================
        private static void Case1_Bundle(List<string> failures, List<string> notes)
        {
            var bundle = GetFixedBundle(failures);
            if (bundle == null) return;

            if (bundle.Length == 0)
            {
                failures.Add("[bundle] FixedBundle is EMPTY - the deepest room would pay nothing, " +
                             "and the reward panel would render '(empty)' after a full dungeon run");
                return;
            }

            // Dual-copy law: the Resources copy is what a build loads, the StreamingAssets copy
            // is what the tools edit. Drift means the editor and the player disagree on loot.
            var saIds = LoadMaterialIds(MaterialsSA, "StreamingAssets", failures);
            var resIds = LoadMaterialIds(MaterialsRes, "Resources", failures);
            if (saIds != null && resIds != null)
            {
                string na = Normalize(File.ReadAllText(MaterialsSA));
                string nb = Normalize(File.ReadAllText(MaterialsRes));
                if (na != nb)
                    failures.Add($"[bundle] materials.json dual-copy DRIFT: StreamingAssets({na.Length}b) != " +
                                 $"Resources({nb.Length}b) - the editor and the shipped player would disagree " +
                                 "on what the cache pays");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in bundle)
            {
                string id = entry.Item1;
                int count = entry.Item2;

                if (string.IsNullOrEmpty(id))
                {
                    failures.Add("[bundle] a FixedBundle row has an EMPTY id - it would be silently skipped, " +
                                 "shrinking the advertised payout without anyone noticing");
                    continue;
                }
                if (!seen.Add(id))
                    failures.Add($"[bundle] duplicate id '{id}' in FixedBundle - the panel would print the same " +
                                 "line twice while the larder receives one merged pile (payout reads dishonest)");
                if (count <= 0)
                    failures.Add($"[bundle] '{id}' has Count {count} - a non-positive count is dropped by the panel " +
                                 "AND by the grant, so the row is dead weight pretending to be loot");

                if (saIds != null && !saIds.Contains(id))
                    failures.Add($"[bundle] '{id}' is NOT a row in {MaterialsSA} - a typo'd id deposits a larder key " +
                                 "with no display name and no icon; the reward panel falls back to printing the raw id");
                if (resIds != null && !resIds.Contains(id))
                    failures.Add($"[bundle] '{id}' is NOT a row in {MaterialsRes} - the SHIPPED build (which loads the " +
                                 "Resources copy) could not name or icon this reward");

                // The live loader must agree with the raw JSON - this catches a catalog that
                // parses but drops rows (schema drift), not just a missing file.
                var def = MaterialCatalog.Find(id);
                if (def == null)
                    failures.Add($"[bundle] MaterialCatalog.Find('{id}') returned null - the runtime catalog cannot " +
                                 "resolve a bundle id even though it may exist in the JSON (loader/schema drift)");
                else if (string.IsNullOrEmpty(def.DisplayName))
                    failures.Add($"[bundle] material '{id}' has no displayName - the reward panel would print a blank " +
                                 "or raw-id line for a reward the player just earned a whole dungeon for");
            }

            notes.Add($"bundle = {bundle.Length} row(s)");
        }

        private static HashSet<string> LoadMaterialIds(string path, string label, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add($"[bundle] materials.json {label} copy missing: {path} - the cache's loot cannot be validated");
                return null;
            }
            MaterialData data = null;
            try { data = JsonConvert.DeserializeObject<MaterialData>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add($"[bundle] materials.json {label} copy failed to parse ({ex.GetType().Name}: {ex.Message})");
                return null;
            }
            if (data == null || data.Materials == null || data.Materials.Count == 0)
            {
                failures.Add($"[bundle] materials.json {label} copy deserialized EMPTY - every bundle id would look like a typo");
                return null;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in data.Materials) if (m != null && !string.IsNullOrEmpty(m.Id)) ids.Add(m.Id);
            return ids;
        }

        // =====================================================================
        //  CASE 2 - the REAL dg_starter_loop layout
        // =====================================================================
        private static void Case2_DeepestRealLayout(List<string> failures, List<string> notes)
        {
            if (!File.Exists(LayoutSA))
            {
                failures.Add("[deepest] layout missing: " + LayoutSA + " - cannot prove the chest lands past the entrance");
                return;
            }
            DungeonComposeLayout layout;
            try { layout = JsonConvert.DeserializeObject<DungeonComposeLayout>(File.ReadAllText(LayoutSA)); }
            catch (Exception ex)
            {
                failures.Add($"[deepest] dg_starter_loop failed to parse ({ex.GetType().Name}: {ex.Message})");
                return;
            }
            if (layout == null || layout.rooms == null || layout.rooms.Count == 0)
            {
                failures.Add("[deepest] dg_starter_loop deserialized EMPTY");
                return;
            }

            string entry = GetEntryRoomId(failures);
            if (entry == null) return;

            string got = InvokeDeepest(layout, entry, failures);
            if (string.IsNullOrEmpty(got))
            {
                failures.Add($"[deepest] DeepestRoomId returned null for dg_starter_loop (entry='{entry}') - " +
                             "the treasure would NEVER be injected in the one dungeon we ship");
                return;
            }
            if (string.Equals(got, entry, StringComparison.Ordinal))
            {
                failures.Add("[deepest] DeepestRoomId returned the ENTRY room - the reward would sit two metres " +
                             "from the door, which is the exact thing WO-850 exists to prevent");
                return;
            }
            var ids = RoomIds(layout);
            if (!ids.Contains(got))
            {
                failures.Add($"[deepest] DeepestRoomId returned '{got}', which is NOT in the layout's rooms[] - " +
                             "the spawner's FindChild would miss it and the chest would be silently skipped");
                return;
            }

            // DETERMINISM: a second call must give the same answer, or the chest moves between
            // runs and no oracle (or player memory) can ever pin it.
            string again = InvokeDeepest(layout, entry, failures);
            if (!string.Equals(got, again, StringComparison.Ordinal))
                failures.Add($"[deepest] NON-DETERMINISTIC: two calls returned '{got}' then '{again}' - the chest " +
                             "would move between runs (dictionary/enumeration order leaked into the pick)");

            // INDEPENDENT ORACLE: a BFS written here, straight from the JSON, must agree.
            string expected = OracleDeepest(layout, entry, out int depth);
            if (expected == null)
                failures.Add("[deepest] the independent BFS oracle found nothing beyond the entry in dg_starter_loop - " +
                             "the layout's connections[] were lost");
            else if (!string.Equals(expected, got, StringComparison.Ordinal))
                failures.Add($"[deepest] DeepestRoomId returned '{got}' but the independent hop-BFS oracle says " +
                             $"'{expected}' (depth {depth}) - the implementation drifted from 'furthest by hops from " +
                             "the entry, ties on lowest ordinal id'");

            if (!string.Equals(got, CapturedDeepest, StringComparison.Ordinal))
                notes.Add($"dg_starter_loop deepest is now '{got}' (was '{CapturedDeepest}' @depth {CapturedDepth} " +
                          "when WO-850 landed) - expected if the layout was re-authored");
        }

        // =====================================================================
        //  CASE 3 - the PURE math, on synthetic layouts
        // =====================================================================
        private static void Case3_DeepestMath(List<string> failures)
        {
            // (a) straight chain: entry -> a -> b -> c. The tail wins.
            var chain = Layout("entry", "a", "b", "c");
            Connect(chain, "entry", "a"); Connect(chain, "a", "b"); Connect(chain, "b", "c");
            Expect(failures, chain, "entry", "c",
                "a straight chain must seat the reward at the TAIL - anything else means hop distance is not being measured");

            // (b) NO connections at all: the reward must NOT land on the entry.
            var island = Layout("entry", "a", "b");
            Expect(failures, island, "entry", null,
                "a layout with no connections must return NULL - seating the reward ON the entry is worse than " +
                "not seating it at all (the player would find the chest before the dungeon)");

            // (c) equal depth -> LOWEST ordinal id. Built so a naive enumeration order would
            //     answer 'zulu' (it is reached through the first-sorted parent).
            var tie = Layout("entry", "p1", "p2", "zulu", "alpha");
            Connect(tie, "entry", "p1"); Connect(tie, "entry", "p2");
            Connect(tie, "p1", "zulu"); Connect(tie, "p2", "alpha");
            Expect(failures, tie, "entry", "alpha",
                "an equal-depth tie must break on the LOWEST ordinal id - otherwise the chest hops between rooms " +
                "when the layout is re-serialized and no regression can pin it");

            // (d) UNDIRECTED law: the only connection is authored CHILD -> ENTRY. A corridor is
            //     walkable both ways; a directed BFS would return null here.
            var reversed = Layout("entry", "far");
            Connect(reversed, "far", "entry");
            Expect(failures, reversed, "entry", "far",
                "connections are UNDIRECTED - a corridor authored child->parent must still be traversed, or " +
                "half the shipped layouts would report no depth at all");

            // (e) depth BEATS ordinal: a deep branch with high-ordinal ids must beat a shallow
            //     low-ordinal room.
            var branch = Layout("entry", "a1", "z1", "z2", "z3");
            Connect(branch, "entry", "a1");
            Connect(branch, "entry", "z1"); Connect(branch, "z1", "z2"); Connect(branch, "z2", "z3");
            Expect(failures, branch, "entry", "z3",
                "the DEEPER branch must win even when its ids sort last - ordinal is only the tie-breaker");

            // (f) a loop: both directions reach 'c' at depth 2 either way; the far side of the
            //     ring is the answer and it must not depend on which way BFS walks first.
            var loop = Layout("entry", "a", "b", "c");
            Connect(loop, "entry", "a"); Connect(loop, "a", "b");
            Connect(loop, "b", "c"); Connect(loop, "c", "entry");
            Expect(failures, loop, "entry", "b",
                "on a ring the far side (2 hops either way) is the deepest room - a loop must not be walked as a chain");

            // (g) degenerate inputs never crash and never guess.
            Expect(failures, null, "entry", null, "a NULL layout must return null, not throw into the spawner");
            Expect(failures, new DungeonComposeLayout(), "entry", null, "an EMPTY layout must return null");
            var noEntry = Layout("a", "b"); Connect(noEntry, "a", "b");
            Expect(failures, noEntry, "entry", null,
                "an entry room that is not in rooms[] must return null - guessing a source room would seat the " +
                "chest somewhere arbitrary");
            Expect(failures, chain, null, null, "a null entryId must return null");
            Expect(failures, chain, "", null, "an empty entryId must return null");

            // (h) a connection naming a room that does not exist must be IGNORED, not followed.
            var ghost = Layout("entry", "a");
            Connect(ghost, "entry", "a"); Connect(ghost, "a", "ghost_room");
            Expect(failures, ghost, "entry", "a",
                "a connection to a room that is not in rooms[] must be ignored - the spawner cannot find a room " +
                "that was never placed, and the chest would vanish");
        }

        // =====================================================================
        //  CASE 4 - the first-clear one-shot key
        // =====================================================================
        private static void Case4_OneShotKey(List<string> failures)
        {
            string a = InvokeFirstClearKey("dg_starter_loop", failures);
            string b = InvokeFirstClearKey("d4_sunken_crypt_spine", failures);
            if (a == null || b == null) return;

            if (string.IsNullOrEmpty(a))
                failures.Add("[oneshot] FirstClearKey returned an EMPTY key - every dungeon would share the blank " +
                             "SeenTutorials slot and only the FIRST dungeon ever cleared would grant a recipe");
            if (string.Equals(a, b, StringComparison.Ordinal))
                failures.Add($"[oneshot] FirstClearKey COLLIDES across dungeons ('{a}') - clearing dg_starter_loop " +
                             "would silently consume the crypt's first-clear recipe unlock");
            if (a.IndexOf("dg_starter_loop", StringComparison.Ordinal) < 0)
                failures.Add($"[oneshot] FirstClearKey('dg_starter_loop') = '{a}' does not carry the dungeon id - " +
                             "the key is not actually namespaced per dungeon");
            if (!string.Equals(a, InvokeFirstClearKey("dg_starter_loop", failures), StringComparison.Ordinal))
                failures.Add("[oneshot] FirstClearKey is not STABLE across calls - the one-shot would re-grant forever");

            string nullKey = InvokeFirstClearKey(null, failures);
            if (string.IsNullOrEmpty(nullKey))
                failures.Add("[oneshot] FirstClearKey(null) produced an empty key - an unnamed dungeon would write a " +
                             "blank SeenTutorials entry and poison every other dungeon's one-shot");

            // SeenTutorials is a free-form string->bool map, so a new one-shot needs NO save-schema
            // bump. If this ever stops being true, the "no schema bump" claim in the WO is a lie.
            var gsT = FindType("DeNelle.Core.State.GameState");
            if (gsT == null) { failures.Add("[oneshot] GameState type not found - cannot prove the no-schema-bump claim"); return; }
            var field = gsT.GetField("SeenTutorials", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                failures.Add("[oneshot] GameState.SeenTutorials is GONE - the WO-850 one-shot rides it, so the " +
                             "first-clear recipe unlock would need a save-schema bump nobody authored");
            }
            else
            {
                var args = field.FieldType.IsGenericType ? field.FieldType.GetGenericArguments() : Type.EmptyTypes;
                bool freeform = args.Length == 2 && args[0] == typeof(string) && args[1] == typeof(bool);
                if (!freeform)
                    failures.Add($"[oneshot] GameState.SeenTutorials is {field.FieldType.Name}, not a free-form " +
                                 "string->bool map - an arbitrary per-dungeon key can no longer be stored without a schema change");
            }

            var svcT = FindType("DeNelle.Core.State.GameStateService");
            if (svcT == null || svcT.GetMethod("MarkTutorialSeen", new[] { typeof(string) }) == null)
                failures.Add("[oneshot] GameStateService.MarkTutorialSeen(string) not found - the persist-the-one-shot " +
                             "idiom (TorchWardenDress.GrantTorchOnce) the cache copies is gone");

            string cache = ReadSource(CacheSrc, failures);
            if (cache == null) return;
            if (cache.IndexOf("MarkTutorialSeen", StringComparison.Ordinal) < 0)
                failures.Add("[oneshot] DungeonTreasureCache no longer calls MarkTutorialSeen - the first-clear unlock " +
                             "would not PERSIST, so it would re-grant on every load");
        }

        // =====================================================================
        //  CASE 5 - the reward panel's UI laws (source lint)
        // =====================================================================
        private static void Case5_PanelLaws(List<string> failures)
        {
            string src = ReadSource(PanelSrc, failures);
            if (src == null) return;

            if (src.IndexOf("ElarionUiKit", StringComparison.Ordinal) < 0)
                failures.Add("[panel] DungeonTreasurePanel does not go through ElarionUiKit - the style-everything-" +
                             "obsidian law; a hand-rolled panel would read as a different game mid-dungeon");

            // Hand-rolled uGUI: the shape UiObsidianConformanceRegression HardFailOnNew rejects.
            var handRolled = new Regex(@"new\s+GameObject\s*\([^;]*typeof\s*\(", RegexOptions.Singleline);
            if (handRolled.IsMatch(src))
                failures.Add("[panel] DungeonTreasurePanel hand-rolls uGUI (new GameObject(..., typeof(...))) - " +
                             "kit-bypassing UI is exactly what the obsidian conformance gate exists to stop");
            foreach (var comp in new[] { "AddComponent<Image>", "AddComponent<RawImage>", "AddComponent<Text>",
                                         "AddComponent<Button>", "AddComponent<Canvas>" })
            {
                if (src.IndexOf(comp, StringComparison.Ordinal) >= 0)
                    failures.Add($"[panel] DungeonTreasurePanel calls {comp}(...) directly - build it through the kit " +
                                 "or the panel drifts off the shared frame/typography");
            }

            // Single-exit law (owner F8 seq 628): the shared Close is retired, Take is the ONE way out.
            bool retiresClose = new Regex(@"close[^;\r\n]*SetActive\s*\(\s*false\s*\)",
                                          RegexOptions.IgnoreCase).IsMatch(src);
            if (!retiresClose)
                failures.Add("[panel] the shared Close is NOT retired - two exits on a linear reward beat read as " +
                             "one choice offered twice (owner F8 seq 628), and a Close-dismiss risks eating the reward");

            if (!new Regex("\"Take\"").IsMatch(src))
                failures.Add("[panel] no \"Take\" CTA found - the confirm beat has no button, so the owner ruling " +
                             "'prompt then confirm' is not implemented");

            if (src.IndexOf("PanelManager.Register", StringComparison.Ordinal) < 0)
                failures.Add("[panel] the panel does not register with PanelManager - the shared Interact button " +
                             "would stay armed under the modal (the modal-arbiter law)");

            if (src.IndexOf("VillageInventory", StringComparison.Ordinal) >= 0)
                failures.Add("[panel] DungeonTreasurePanel references VillageInventory - presentation must NEVER " +
                             "grant; the ONLY grant path is the caller's onTake callback (HP B2B: presentation is a " +
                             "separate layer that never touches the objects)");

            int nonAscii = FirstNonAsciiLine(src);
            if (nonAscii > 0)
                failures.Add($"[panel] non-ASCII character at line {nonAscii} - the TMP font atlas renders it as tofu " +
                             "on device (the HudUiRegression tofu law)");
        }

        // =====================================================================
        //  CASE 6 - ONE granting seam
        // =====================================================================
        private static void Case6_GrantSeam(List<string> failures)
        {
            string src = ReadSource(CacheSrc, failures);
            if (src == null) return;

            if (src.IndexOf("VillageInventory", StringComparison.Ordinal) >= 0)
                failures.Add("[grant-seam] DungeonTreasureCache touches VillageInventory directly - dungeon loot has " +
                             "ONE seam (DungeonLootGrant); a second path means loot that skips its logging, its " +
                             "capacity rules and its save flush");
            if (src.IndexOf("DungeonLootGrant", StringComparison.Ordinal) < 0)
                failures.Add("[grant-seam] DungeonTreasureCache no longer routes through DungeonLootGrant - the cache " +
                             "is granting through some other path");
            if (src.IndexOf("DungeonTreasurePanel", StringComparison.Ordinal) < 0)
                failures.Add("[grant-seam] DungeonTreasureCache no longer shows DungeonTreasurePanel - the owner's " +
                             "'prompt then confirm' beat has been replaced by a walk-in auto-claim");

            int nonAscii = FirstNonAsciiLine(src);
            if (nonAscii > 0)
                failures.Add($"[grant-seam] non-ASCII character at line {nonAscii} in DungeonTreasureCache");
        }

        // =====================================================================
        //  CASE 7 - the injector actually arms
        // =====================================================================
        private static void Case7_SpawnerHook(List<string> failures)
        {
            var t = FindType(SpawnerType);
            if (t == null)
            {
                failures.Add("[spawner] DungeonTreasureSpawner not found - nothing injects the cache, so every " +
                             "composed dungeon ships with an empty deepest room");
                return;
            }
            var install = t.GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (install == null)
            {
                failures.Add("[spawner] DungeonTreasureSpawner.Install not found - the auto-inject entry point is gone");
                return;
            }
            if (install.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length == 0)
                failures.Add("[spawner] DungeonTreasureSpawner.Install lacks [RuntimeInitializeOnLoadMethod] - it would " +
                             "never arm, and the treasure would exist only in this suite");

            if (t.GetMethod("ResolveDeepestRoomId", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static) == null)
                failures.Add("[spawner] DungeonTreasureSpawner.ResolveDeepestRoomId not found - the seat-the-chest " +
                             "path this suite pins no longer exists");
        }

        // =====================================================================
        //  REFLECTION BRIDGE (DungeonTreasureCache's math surface is `internal`)
        // =====================================================================

        private static Type CacheT() => FindType(CacheType);

        private static (string, int)[] GetFixedBundle(List<string> failures)
        {
            var t = CacheT();
            if (t == null) { failures.Add("[bundle] DungeonTreasureCache type not found - WO-850 is not in this tree"); return null; }
            var f = t.GetField("FixedBundle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (f == null)
            {
                failures.Add("[bundle] DungeonTreasureCache.FixedBundle not found - the ONE tuning point for the " +
                             "cache payout was renamed or removed, so nothing pins what the chest pays");
                return null;
            }
            var value = f.GetValue(null) as (string, int)[];
            if (value == null)
                failures.Add($"[bundle] FixedBundle is not a (string,int)[] (got {f.FieldType.Name}) - the oracle " +
                             "cannot read the payout, so it is unpinned");
            return value;
        }

        private static string GetEntryRoomId(List<string> failures)
        {
            var t = CacheT();
            if (t == null) { failures.Add("[deepest] DungeonTreasureCache type not found"); return null; }
            var f = t.GetField("EntryRoomId", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (f == null)
            {
                failures.Add("[deepest] DungeonTreasureCache.EntryRoomId not found - the BFS source room is unpinned");
                return null;
            }
            return f.GetValue(null) as string;
        }

        private static MethodInfo s_deepest;
        private static MethodInfo DeepestMethod(List<string> failures)
        {
            if (s_deepest != null) return s_deepest;
            var t = CacheT();
            if (t == null) { failures.Add("[deepest] DungeonTreasureCache type not found"); return null; }
            s_deepest = t.GetMethod("DeepestRoomId", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
                                    null, new[] { typeof(DungeonComposeLayout), typeof(string) }, null);
            if (s_deepest == null)
                failures.Add("[deepest] DungeonTreasureCache.DeepestRoomId(DungeonComposeLayout,string) not found - " +
                             "the deepest-room math this whole feature stands on was renamed or re-signed");
            return s_deepest;
        }

        private static string InvokeDeepest(DungeonComposeLayout layout, string entryId, List<string> failures)
        {
            var m = DeepestMethod(failures);
            if (m == null) return null;
            try { return m.Invoke(null, new object[] { layout, entryId }) as string; }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                failures.Add($"[deepest] DeepestRoomId THREW {inner.GetType().Name}: {inner.Message} - the spawner " +
                             "calls this during scene load, so a throw here kills the injection silently");
                return null;
            }
        }

        private static string InvokeFirstClearKey(string dungeonId, List<string> failures)
        {
            var t = CacheT();
            if (t == null) { failures.Add("[oneshot] DungeonTreasureCache type not found"); return null; }
            var m = t.GetMethod("FirstClearKey", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
                                null, new[] { typeof(string) }, null);
            if (m == null)
            {
                failures.Add("[oneshot] DungeonTreasureCache.FirstClearKey(string) not found - the per-dungeon " +
                             "first-clear one-shot has no key, so the recipe unlock would fire every single run");
                return null;
            }
            try { return m.Invoke(null, new object[] { dungeonId }) as string; }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                failures.Add($"[oneshot] FirstClearKey THREW {inner.GetType().Name}: {inner.Message}");
                return null;
            }
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        private static void Expect(List<string> failures, DungeonComposeLayout layout, string entryId,
                                   string expected, string why)
        {
            string got = InvokeDeepest(layout, entryId, failures);
            if (string.Equals(got, expected, StringComparison.Ordinal)) return;
            failures.Add($"[deepest-math] expected '{expected ?? "<null>"}' got '{got ?? "<null>"}' - {why}");
        }

        private static DungeonComposeLayout Layout(params string[] roomIds)
        {
            var l = new DungeonComposeLayout { dungeonId = "synthetic", cellSize = 6f };
            l.rooms = new List<ComposeRoomPlacement>();
            l.connections = new List<ComposeConnection>();
            foreach (var id in roomIds)
                l.rooms.Add(new ComposeRoomPlacement { prefab = "Straight", instanceId = id });
            return l;
        }

        private static void Connect(DungeonComposeLayout l, string from, string to)
        {
            l.connections.Add(new ComposeConnection
            {
                fromInstance = from,
                fromSocket = "n_door_01",
                toInstance = to,
                toSocket = "s_door_01"
            });
        }

        private static HashSet<string> RoomIds(DungeonComposeLayout layout)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (layout?.rooms == null) return ids;
            foreach (var r in layout.rooms)
            {
                if (r == null) continue;
                string id = string.IsNullOrEmpty(r.instanceId) ? r.prefab : r.instanceId;
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            return ids;
        }

        /// <summary>
        /// An INDEPENDENT hop-BFS: furthest room from the entry over UNDIRECTED connections,
        /// ties on lowest ordinal id. Deliberately written straight from the JSON so it can
        /// disagree with the implementation instead of inheriting its bugs.
        /// </summary>
        private static string OracleDeepest(DungeonComposeLayout layout, string entryId, out int maxDepth)
        {
            maxDepth = 0;
            var ids = RoomIds(layout);
            if (!ids.Contains(entryId)) return null;

            var adj = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            if (layout.connections != null)
            {
                foreach (var c in layout.connections)
                {
                    if (c == null) continue;
                    if (!ids.Contains(c.fromInstance) || !ids.Contains(c.toInstance)) continue;
                    if (!adj.TryGetValue(c.fromInstance, out var fa)) { fa = new SortedSet<string>(StringComparer.Ordinal); adj[c.fromInstance] = fa; }
                    if (!adj.TryGetValue(c.toInstance, out var ta)) { ta = new SortedSet<string>(StringComparer.Ordinal); adj[c.toInstance] = ta; }
                    fa.Add(c.toInstance);
                    ta.Add(c.fromInstance);
                }
            }

            var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [entryId] = 0 };
            var q = new Queue<string>();
            q.Enqueue(entryId);
            while (q.Count > 0)
            {
                string cur = q.Dequeue();
                if (!adj.TryGetValue(cur, out var ns)) continue;
                foreach (var n in ns)
                {
                    if (dist.ContainsKey(n)) continue;
                    dist[n] = dist[cur] + 1;
                    q.Enqueue(n);
                }
            }

            string best = null;
            foreach (var kv in dist)
            {
                if (kv.Value <= 0) continue;
                if (kv.Value > maxDepth || (kv.Value == maxDepth && string.CompareOrdinal(kv.Key, best) < 0))
                {
                    maxDepth = kv.Value;
                    best = kv.Key;
                }
            }
            return best;
        }

        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add($"[source] {path} not found - WO-850 is not in this tree (or the file moved without " +
                             "updating this oracle)");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add($"[source] could not read {path}: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static int FirstNonAsciiLine(string src)
        {
            int line = 1;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '\n') { line++; continue; }
                if (c > (char)126 && c != '\r') return line;
            }
            return 0;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.Length > 0 && s[0] == (char)0xFEFF) s = s.Substring(1);
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
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
    }
}
