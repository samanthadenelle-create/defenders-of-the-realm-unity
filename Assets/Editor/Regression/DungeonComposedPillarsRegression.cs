// =============================================================================
// DungeonComposedPillarsRegression — pins WO-1001 slices 1b through 8.
// -----------------------------------------------------------------------------
// Those eight slices gave composed (Pipeline A) dungeons their runtime pillars:
// stair ports, boss encounters, chests, oil stones, the darkness ambush, traps,
// keys/locks, and per-floor extract pads. They shipped with NO oracle, and the
// way they are built makes that unusually dangerous.
//
// THE CORE RISK. `DeNelle.Editor` cannot reference `DeNelle.Dungeons`, so
// DungeonBaker places every one of these pillars through
// `FindType("DeNelle.Dungeons.X")` reflection. When a type is renamed or moved,
// FindType returns null, the baker emits a WARN, and it places NOTHING - the bake
// still reports saved=True and every other suite stays green. A dungeon would ship
// with no traps, no keys, no chests and no oil, and nothing would go red.
//
// So Case 1 resolves every reflected name PARSED OUT OF THE BAKER'S OWN SOURCE
// (not a copy of the list, which would rot independently), and Case 2 opens the
// baked scenes and counts what actually landed against what the layout authored.
// Case 5 is the content check: a lock whose key is never granted in the same
// dungeon is an unwinnable run, which no compile or bake step can see.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor.Regression
{
    public static class DungeonComposedPillarsRegression
    {
        private const string BakerPath = "Assets/Editor/RoomForge/DungeonBaker.cs";
        private const string AmbushPath = "Assets/_Modules/Dungeons/ComposedAmbushDirector.cs";
        private const string LayoutsDir = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts";
        private const string ScenesDir = "Assets/Scenes/DungeonCompose";

        /// <summary>The composed dungeons whose baked scenes must match their layout.</summary>
        private static readonly string[] ComposedDungeons =
        {
            "dg_starter_loop", "dg_descent_probe", "dg_sunken_vault", "dg_bonecrypt", "dg_ember_deep",
        };

        /// <summary>Standalone batch entry — prints the DUNGEON_COMPOSED_PILLARS_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DUNGEON_COMPOSED_PILLARS_OK - " + reason);
            else Debug.LogError("DUNGEON_COMPOSED_PILLARS_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([dungeon-composed-pillars]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            Case(failures, "reflection-surface", () => Case1_ReflectionSurfaceResolves(failures));
            Case(failures, "authored-placed", () => Case2_AuthoredPillarsWerePlaced(failures, notes));
            Case(failures, "keybag", () => Case3_KeyBagContract(failures));
            Case(failures, "darkness-wired", () => Case4_DarknessActuallyFeeds(failures));
            Case(failures, "lock-has-key", () => Case5_EveryLockHasAKey(failures));
            Case(failures, "refs-resolve", () => Case6_PillarRoomRefsResolve(failures));
            Case(failures, "bake-persists", () => Case7_BakeTimeConfigSurvivesSave(failures));

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "COMPOSED PILLARS OK - 7/7 cases pass (bake-time Configure survives SaveScene, " +
                         "baker reflection surface resolves, " +
                         "authored chests/oil/traps/keys/locks/extracts actually placed in the baked scenes, " +
                         "key bag contract, darkness genuinely feeds the ambush roll, every lock has a " +
                         "reachable key, pillar room refs resolve)" + noteStr;
                return true;
            }
            reason = "COMPOSED PILLARS FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        // =====================================================================
        //  CASE 1 — every reflected name in the baker resolves
        // =====================================================================
        private static void Case1_ReflectionSurfaceResolves(List<string> failures)
        {
            string src = ReadText(BakerPath);
            if (src == null) { failures.Add($"[reflection-surface] cannot read {BakerPath}"); return; }

            // Parsed from the baker's OWN source, so this can never drift out of sync with it -
            // a hardcoded copy of the list would rot silently, which is the bug class in question.
            var typeNames = Regex.Matches(src, @"FindType\(""([^""]+)""\)")
                                 .Cast<Match>().Select(m => m.Groups[1].Value)
                                 .Distinct().ToList();
            if (typeNames.Count < 8)
                failures.Add($"[reflection-surface] only {typeNames.Count} FindType targets parsed from the baker - the placement code moved or the pattern changed; this case is no longer covering it");

            foreach (string tn in typeNames)
            {
                if (ResolveType(tn) == null)
                    failures.Add($"[reflection-surface] DungeonBaker reflects '{tn}' but it does NOT resolve - the baker will WARN and place NOTHING while the bake still reports saved=True");
            }

            // The members the baker pokes by name. A rename here is the same silent-nothing failure.
            RequireMember(failures, "DeNelle.Dungeons.DungeonPortLink", "Configure");
            RequireMember(failures, "DeNelle.Dungeons.ComposedTrapHazard", "Configure");
            RequireMember(failures, "DeNelle.Dungeons.ComposedKeyPickup", "Configure");
            RequireMember(failures, "DeNelle.Dungeons.ComposedLockedPort", "Configure");
            RequireMember(failures, "DeNelle.Dungeons.ComposedOilStone", "Configure");
            RequireMember(failures, "DeNelle.Village.BreakableContainer", "Create");
            RequireMember(failures, "DeNelle.Dungeons.DungeonExitInteractable", "Spawn");

            // Serialized field names written via SerializedObject - misspell one and the encounter
            // silently keeps its prefab default instead of the authored family/boss.
            RequireSerializedField(failures, "DeNelle.Village.OutpostEnemyGroupSpawner", "encounterKind");
            RequireSerializedField(failures, "DeNelle.Village.OutpostEnemyGroupSpawner", "roomId");
        }

        // =====================================================================
        //  CASE 2 — what the layout AUTHORS is what the baked scene CONTAINS
        // =====================================================================
        private static void Case2_AuthoredPillarsWerePlaced(List<string> failures, List<string> notes)
        {
            int scanned = 0;
            foreach (string id in ComposedDungeons)
            {
                string layoutPath = $"{LayoutsDir}/{id}.json";
                string scenePath = $"{ScenesDir}/{id}.unity";
                if (!File.Exists(layoutPath) || !File.Exists(scenePath)) continue;

                JObject layout = ParseJson(layoutPath);
                if (layout == null) { failures.Add($"[authored-placed] {id}: layout JSON does not parse"); continue; }

                int wantChests = layout["rooms"]?.Sum(r => r["chests"]?.Count() ?? 0) ?? 0;
                int wantOil = layout["oilStones"]?.Count() ?? 0;
                int wantTraps = layout["traps"]?.Count() ?? 0;
                int wantKeys = layout["keys"]?.Count() ?? 0;
                int wantLocks = layout["locks"]?.Count() ?? 0;
                int wantExtract = layout["extracts"]?.Count() ?? 0;
                if (wantChests + wantOil + wantTraps + wantKeys + wantLocks + wantExtract == 0) continue;

                Scene scene = default;
                try
                {
                    // SINGLE, not Additive. An additive open in batchmode gave NON-DETERMINISTIC
                    // counts - the same dungeon passed one run and failed the next while the scene
                    // bytes were provably unchanged. A flaky oracle is worse than no oracle: it
                    // trains everyone to ignore it. Single-mode replaces the open scene and
                    // enumerates deterministically.
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    scanned++;
                    CountAndCompare(failures, id, scene, "chests", wantChests, "DeNelle.Village.BreakableContainer");
                    CountAndCompare(failures, id, scene, "oilStones", wantOil, "DeNelle.Dungeons.ComposedOilStone");
                    CountAndCompare(failures, id, scene, "traps", wantTraps, "DeNelle.Dungeons.ComposedTrapHazard");
                    CountAndCompare(failures, id, scene, "keys", wantKeys, "DeNelle.Dungeons.ComposedKeyPickup");
                    CountAndCompare(failures, id, scene, "locks", wantLocks, "DeNelle.Dungeons.ComposedLockedPort");
                }
                finally
                {
                    // Single-mode: the NEXT OpenScene replaces this one, and the last one is left
                    // open harmlessly (no suite after this reads the active scene). Closing the
                    // only open scene is what Unity refuses, and doing it was part of the flake.
                }
            }

            if (scanned == 0)
                failures.Add("[authored-placed] no composed scene could be opened - this case proved nothing; do not read it as a pass");
            else
                notes.Add($"scanned {scanned} baked composed scene(s)");
        }

        private static void CountAndCompare(List<string> failures, string dungeonId, Scene scene,
                                            string authoredKey, int want, string typeName)
        {
            if (want == 0) return;
            Type t = ResolveType(typeName);
            if (t == null) return;   // Case 1 already reports an unresolved type; do not double-fail.

            int got = 0;
            foreach (var root in scene.GetRootGameObjects())
                got += root.GetComponentsInChildren(t, true).Length;

            if (got < want)
                failures.Add($"[authored-placed] {dungeonId}: layout authors {want} '{authoredKey}' but the baked scene contains only {got} {t.Name} - authored content that never reached the scene is invisible to the player and to every other gate");
        }

        // =====================================================================
        //  CASE 3 — the run-local key bag contract
        // =====================================================================
        private static void Case3_KeyBagContract(List<string> failures)
        {
            Type bag = ResolveType("DeNelle.Dungeons.ComposedKeyBag");
            if (bag == null) { failures.Add("[keybag] ComposedKeyBag does not resolve"); return; }

            var clear = bag.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
            var grant = bag.GetMethod("Grant", BindingFlags.Public | BindingFlags.Static);
            var has = bag.GetMethod("Has", BindingFlags.Public | BindingFlags.Static);
            if (clear == null || grant == null || has == null)
            {
                failures.Add("[keybag] ComposedKeyBag is missing Clear/Grant/Has");
                return;
            }

            clear.Invoke(null, null);
            if ((bool)has.Invoke(null, new object[] { "k1" }))
                failures.Add("[keybag] a cleared bag must not hold 'k1'");

            grant.Invoke(null, new object[] { "k1" });
            if (!(bool)has.Invoke(null, new object[] { "k1" }))
                failures.Add("[keybag] Grant('k1') then Has('k1') must be true");
            if ((bool)has.Invoke(null, new object[] { "k2" }))
                failures.Add("[keybag] holding 'k1' must not satisfy 'k2' - a wrong key must never open a lock");

            // Null/empty must be inert, not a wildcard that opens every lock.
            if ((bool)has.Invoke(null, new object[] { (string)null }) ||
                (bool)has.Invoke(null, new object[] { "" }))
                failures.Add("[keybag] Has(null/empty) must be FALSE - otherwise an unset keyId opens every locked port");

            // Leave no residue for other suites in the same batch.
            clear.Invoke(null, null);
            if ((bool)has.Invoke(null, new object[] { "k1" }))
                failures.Add("[keybag] Clear() must empty the bag");
        }

        // =====================================================================
        //  CASE 4 — darkness genuinely feeds the roll (slice 6)
        // =====================================================================
        private static void Case4_DarknessActuallyFeeds(List<string> failures)
        {
            // The pre-WO-1001 defect was RandomEncounterTable.Roll being called with a hardcoded
            // inDarkness:false, which made DarknessRateMult dead. Slice 6's whole point is that a
            // real darkness signal now reaches a roll, so assert it at the source that does it.
            string src = ReadText(AmbushPath);
            if (src == null) { failures.Add($"[darkness-wired] cannot read {AmbushPath}"); return; }

            if (!Regex.IsMatch(src, @"inDarkness\s*:\s*true"))
                failures.Add("[darkness-wired] ComposedAmbushDirector no longer passes inDarkness:true - the darkness multiplier is dead again and 'push into the dark' costs nothing");
            if (!src.Contains("IsInDarkness"))
                failures.Add("[darkness-wired] ComposedAmbushDirector no longer reads Lantern.IsInDarkness - the ambush would fire regardless of oil");

            Type lantern = ResolveType("DeNelle.Dungeons.Lantern");
            if (lantern == null) { failures.Add("[darkness-wired] Lantern does not resolve"); return; }
            if (lantern.GetProperty("IsInDarkness") == null)
                failures.Add("[darkness-wired] Lantern.IsInDarkness is gone - the ambush director and the legendary gate both key off it");

            Type director = ResolveType("DeNelle.Dungeons.ComposedAmbushDirector");
            if (director == null) { failures.Add("[darkness-wired] ComposedAmbushDirector does not resolve"); return; }
            if (director.GetProperty("HasBeenInDarkness") == null)
                failures.Add("[darkness-wired] ComposedAmbushDirector.HasBeenInDarkness is gone - ComposedLegendaryGate reads it to arm deepboss loot");

            if (ResolveType("DeNelle.Dungeons.ComposedLegendaryGate") == null)
                failures.Add("[darkness-wired] ComposedLegendaryGate does not resolve - deepboss loot would be reachable without ever going dark");
        }

        // =====================================================================
        //  CASE 5 — a locked floor must be openable (content soft-lock guard)
        // =====================================================================
        private static void Case5_EveryLockHasAKey(List<string> failures)
        {
            foreach (string id in ComposedDungeons)
            {
                string p = $"{LayoutsDir}/{id}.json";
                if (!File.Exists(p)) continue;
                JObject layout = ParseJson(p);
                var locks = layout?["locks"];
                if (locks == null || !locks.Any()) continue;

                var granted = new HashSet<string>(
                    (layout["keys"] ?? new JArray())
                        .Select(k => (string)k["keyId"])
                        .Where(s => !string.IsNullOrEmpty(s)),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var lk in locks)
                {
                    string need = (string)lk["keyId"];
                    string lockId = (string)lk["id"] ?? "<unnamed>";
                    if (string.IsNullOrEmpty(need))
                        failures.Add($"[lock-has-key] {id}: lock '{lockId}' has no keyId - ComposedKeyBag.Has(empty) is false, so it can NEVER open");
                    else if (!granted.Contains(need))
                        failures.Add($"[lock-has-key] {id}: lock '{lockId}' needs key '{need}' but NO key pickup in this dungeon grants it - the run is unwinnable past that port");
                }
            }
        }

        // =====================================================================
        //  CASE 6 — pillar room references point at rooms that exist
        // =====================================================================
        private static void Case6_PillarRoomRefsResolve(List<string> failures)
        {
            foreach (string id in ComposedDungeons)
            {
                string p = $"{LayoutsDir}/{id}.json";
                if (!File.Exists(p)) continue;
                JObject layout = ParseJson(p);
                if (layout == null) continue;

                var rooms = new HashSet<string>(
                    (layout["rooms"] ?? new JArray())
                        .Select(r => (string)r["instanceId"])
                        .Where(s => !string.IsNullOrEmpty(s)),
                    StringComparer.OrdinalIgnoreCase);
                if (rooms.Count == 0) continue;

                CheckRefs(failures, id, layout, "oilStones", "roomId", rooms);
                CheckRefs(failures, id, layout, "traps", "roomId", rooms);
                CheckRefs(failures, id, layout, "keys", "roomId", rooms);
                CheckRefs(failures, id, layout, "extracts", "roomId", rooms);
                CheckRefs(failures, id, layout, "locks", "fromRoomId", rooms);
                CheckRefs(failures, id, layout, "locks", "toRoomId", rooms);
            }
        }

        private static void CheckRefs(List<string> failures, string dungeonId, JObject layout,
                                      string arrayKey, string field, HashSet<string> rooms)
        {
            var arr = layout[arrayKey];
            if (arr == null) return;
            foreach (var e in arr)
            {
                string rid = (string)e[field];
                if (string.IsNullOrEmpty(rid)) continue;   // absolute placement is legal
                if (!rooms.Contains(rid))
                    failures.Add($"[refs-resolve] {dungeonId}: {arrayKey}[{(string)e["id"] ?? "?"}].{field}='{rid}' names no room in the layout - the baker skips it and the pillar silently never appears");
            }
        }

        // =====================================================================
        //  CASE 7 — bake-time Configure must SURVIVE the scene save
        // =====================================================================
        private static void Case7_BakeTimeConfigSurvivesSave(List<string> failures)
        {
            // THE defect this suite exists for. DungeonBaker configures stair ports and locked
            // ports AT BAKE TIME and then saves the scene. Unity serializes only public fields and
            // [SerializeField] privates - so while these were plain privates, every configured
            // value was silently DISCARDED by SaveScene. Update() bails on `_hero == null`, so
            // every baked port was INERT: no descent, no locked floor. The bake still printed
            // saved=True, the scene still contained the components, and no gate went red.
            //
            // The cottage path masked it completely, because DungeonController re-Configures at
            // runtime. Only the composed path relies on persistence.
            RequirePersisted(failures, "DeNelle.Dungeons.DungeonPortLink",
                "_target", "_targetFacingY", "_hero", "_prompt", "_radius");
            RequirePersisted(failures, "DeNelle.Dungeons.ComposedLockedPort",
                "_target", "_faceY", "_hero", "_keyId", "_radius");

            // ComposedOilStone and ComposedTrapHazard were always correct - pin them so a future
            // "tidy up the serialized fields" pass cannot quietly break the ones that work.
            RequirePersisted(failures, "DeNelle.Dungeons.ComposedOilStone", "_id", "_radius");
            RequirePersisted(failures, "DeNelle.Dungeons.ComposedTrapHazard",
                "_id", "_kind", "_damage", "_radius");
            RequirePersisted(failures, "DeNelle.Dungeons.ComposedKeyPickup", "_keyId");
            // Slice 8 extract pads are Spawn()ed at bake time, so the authored label must persist.
            // (_onLeave is a delegate and deliberately NOT serialized - a baked pad routing to the
            // composed default is the intended behaviour, not a bug.)
            RequirePersisted(failures, "DeNelle.Dungeons.DungeonExitInteractable", "_label");

            // The injector must not treat an EXTRACT PAD as the return exit. Baked pads are also
            // DungeonExitInteractables and they sit on stair landings below floor 0, so a bare
            // FindAnyObjectByType skip left the entry room with no way out.
            // ONE MONOBEHAVIOUR PER FILE, NAMED FOR THE FILE. Unity matches a serialized
            // MonoBehaviour to a script asset BY FILE NAME - a component whose class does not own
            // its file is added fine at bake time and then does NOT survive the scene load. That
            // is how ComposedKeyPickup and ComposedLockedPort (both once declared inside
            // ComposedKeyLock.cs) vanished from every baked dungeon while the bake logged
            // "KEY '...' @ ..." and reported saved=True. Traps and oil stones were unaffected
            // purely because they happened to own their files.
            foreach (string tn in new[]
            {
                "DeNelle.Dungeons.ComposedKeyPickup", "DeNelle.Dungeons.ComposedLockedPort",
                "DeNelle.Dungeons.ComposedTrapHazard", "DeNelle.Dungeons.ComposedOilStone",
                "DeNelle.Dungeons.ComposedLegendaryGate", "DeNelle.Dungeons.DungeonPortLink",
            })
            {
                Type t = ResolveType(tn);
                if (t == null) continue;
                string expected = $"Assets/_Modules/Dungeons/{t.Name}.cs";
                if (!File.Exists(expected))
                    failures.Add($"[bake-persists] {t.Name} is a baked MonoBehaviour but {expected} does not exist - Unity binds serialized components by FILE NAME, so it will not survive the scene load and the baked object silently disappears");
            }

            string exitSrc = ReadText("Assets/_Modules/Dungeons/DungeonExitInteractable.cs");
            if (exitSrc != null && !exitSrc.Contains("Extract_"))
                failures.Add("[bake-persists] the exit injector no longer distinguishes baked 'Extract_' pads from a return exit - a composed dungeon that authors extracts would get NO entry exit");
        }

        private static void RequirePersisted(List<string> failures, string typeName, params string[] fields)
        {
            Type t = ResolveType(typeName);
            if (t == null) { failures.Add($"[bake-persists] {typeName} does not resolve"); return; }
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

            foreach (string f in fields)
            {
                FieldInfo fi = t.GetField(f, F);
                if (fi == null)
                {
                    failures.Add($"[bake-persists] {t.Name}.{f} is gone - DungeonBaker configures this component at BAKE time, so a renamed field means the baked value is lost");
                    continue;
                }
                bool persists = fi.IsPublic || fi.GetCustomAttribute<SerializeField>() != null;
                if (!persists)
                    failures.Add($"[bake-persists] {t.Name}.{f} is a plain private with NO [SerializeField] - bake-time Configure() is DISCARDED by SaveScene and the component is inert in every baked scene (silent: the bake still reports saved=True)");
            }
        }

        // -------- helpers --------

        private static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }

        private static JObject ParseJson(string path)
        {
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch { return null; }
        }

        private static Type ResolveType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        private static void RequireMember(List<string> failures, string typeName, string member)
        {
            Type t = ResolveType(typeName);
            if (t == null) return;   // Case 1's FindType sweep already reports the missing type.
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static;
            if (t.GetMethods(F).All(m => m.Name != member))
                failures.Add($"[reflection-surface] {typeName}.{member} is gone - DungeonBaker invokes it BY NAME and will silently skip wiring");
        }

        private static void RequireSerializedField(List<string> failures, string typeName, string field)
        {
            Type t = ResolveType(typeName);
            if (t == null) return;
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            if (t.GetField(field, F) == null)
                failures.Add($"[reflection-surface] {typeName} has no field '{field}' - the baker writes it via SerializedObject, so the authored value would be dropped and the prefab default used instead");
        }

        // Guard each case so one throw becomes a labelled failure, not a dead suite.
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
