// =============================================================================
// DungeonTreasureTests (WO-850) - unit tests for the PURE deepest-room math.
// -----------------------------------------------------------------------------
// The treasure cache is seated by BFS hop distance from the entry room over the
// layout's connections[], because no shipped layout authors a depth or boss flag.
// That single pure function decides where the reward of an entire dungeon run
// lands, so it is pinned here on hand-built layouts - no scene, no Resources, no
// Unity object graph.
//
// THE CONTRACT UNDER TEST (DungeonTreasureCache.DeepestRoomId):
//   * furthest room from the entry by HOP COUNT
//   * connections are UNDIRECTED (a corridor is walkable both ways)
//   * ties break on the LOWEST ordinal instanceId -> deterministic for a layout
//   * null when the layout is empty/absent, when the entry is not in rooms[], or
//     when nothing is deeper than the entry (seating the reward ON the entry is
//     worse than not seating it at all)
//
// ACCESS NOTE: the math surface is `internal` to DeNelle.Dungeons and the tree has
// NO InternalsVisibleTo, so these tests bind it by reflection. Widening
// DeepestRoomId / FirstClearKey to public (or adding an InternalsVisibleTo for
// DeNelle.Tests.EditMode) would let them be called directly - see the WO RESULT.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Tests.EditMode
{
    public class DungeonTreasureTests
    {
        private const string CacheTypeName = "DeNelle.Dungeons.DungeonTreasureCache";

        private static MethodInfo s_deepest;
        private static MethodInfo s_firstClearKey;

        [OneTimeSetUp]
        public void ResolveMathSurface()
        {
            Type cache = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                cache = asm.GetType(CacheTypeName, false);
                if (cache != null) break;
            }
            Assert.NotNull(cache,
                "DungeonTreasureCache type not found - WO-850 is not in this tree, so nothing seats the deepest-room reward.");

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            s_deepest = cache.GetMethod("DeepestRoomId", flags, null,
                new[] { typeof(DungeonComposeLayout), typeof(string) }, null);
            Assert.NotNull(s_deepest,
                "DungeonTreasureCache.DeepestRoomId(DungeonComposeLayout,string) not found - the pure math the whole " +
                "feature stands on was renamed or re-signed.");

            s_firstClearKey = cache.GetMethod("FirstClearKey", flags, null, new[] { typeof(string) }, null);
            Assert.NotNull(s_firstClearKey,
                "DungeonTreasureCache.FirstClearKey(string) not found - the per-dungeon first-clear one-shot has no key.");
        }

        private static string Deepest(DungeonComposeLayout layout, string entryId)
        {
            try { return s_deepest.Invoke(null, new object[] { layout, entryId }) as string; }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                Assert.Fail("DeepestRoomId threw " + inner.GetType().Name + ": " + inner.Message +
                            " - the spawner calls this during scene load, so a throw kills the injection silently.");
                return null;
            }
        }

        private static string FirstClearKey(string dungeonId)
        {
            return s_firstClearKey.Invoke(null, new object[] { dungeonId }) as string;
        }

        // ---- layout builders -------------------------------------------------

        private static DungeonComposeLayout Layout(params string[] roomIds)
        {
            var l = new DungeonComposeLayout
            {
                dungeonId = "unit_test",
                // Inert for this fixture (no room is instantiated), but kept on the kit canon so
                // it does not read as a stale 6u claim after WO-922 widened the cell.
                cellSize = RoomForgeCanon.Cell,
                rooms = new List<ComposeRoomPlacement>(),
                connections = new List<ComposeConnection>()
            };
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

        // =====================================================================
        //  HOP DISTANCE
        // =====================================================================

        [Test]
        public void StraightChain_SeatsTheRewardAtTheTail()
        {
            var l = Layout("entry", "a", "b", "c");
            Connect(l, "entry", "a");
            Connect(l, "a", "b");
            Connect(l, "b", "c");

            Assert.AreEqual("c", Deepest(l, "entry"),
                "a straight chain must seat the treasure in the LAST room - any other answer means hop distance " +
                "is not what decides the seat.");
        }

        [Test]
        public void Branching_DeeperBranchWins_EvenWhenItsIdsSortLast()
        {
            // entry -> a1 (1 hop, low ordinal)   vs   entry -> z1 -> z2 -> z3 (3 hops, high ordinal)
            var l = Layout("entry", "a1", "z1", "z2", "z3");
            Connect(l, "entry", "a1");
            Connect(l, "entry", "z1");
            Connect(l, "z1", "z2");
            Connect(l, "z2", "z3");

            Assert.AreEqual("z3", Deepest(l, "entry"),
                "depth must beat ordinal: the deeper branch wins even when its ids sort last. Ordinal is ONLY the " +
                "tie-breaker - if it were the primary key the chest would sit one room from the door.");
        }

        [Test]
        public void Loop_ReturnsTheFarSideOfTheRing()
        {
            // A 4-room ring: entry - a - b - c - entry. 'b' is 2 hops either way; a and c are 1.
            var l = Layout("entry", "a", "b", "c");
            Connect(l, "entry", "a");
            Connect(l, "a", "b");
            Connect(l, "b", "c");
            Connect(l, "c", "entry");

            Assert.AreEqual("b", Deepest(l, "entry"),
                "on a ring the far side is the deepest room - a loop must be walked as a graph, not unrolled as a chain " +
                "(dg_starter_loop is literally a loop, so this is the shipped shape).");
        }

        [Test]
        public void ReversedConnection_IsTraversed_ConnectionsAreUndirected()
        {
            // The ONLY connection is authored child -> entry. A directed BFS finds nothing.
            var l = Layout("entry", "far");
            Connect(l, "far", "entry");

            Assert.AreEqual("far", Deepest(l, "entry"),
                "connections are UNDIRECTED - a corridor authored child->parent must still be traversed, or an " +
                "authoring-order accident would silently delete the dungeon's reward.");
        }

        // =====================================================================
        //  TIE-BREAK + DETERMINISM
        // =====================================================================

        [Test]
        public void EqualDepth_BreaksOnLowestOrdinalId()
        {
            // Both leaves sit at depth 2. 'zulu' is reached through the first-sorted parent, so a
            // first-found-wins implementation answers 'zulu'; the contract says 'alpha'.
            var l = Layout("entry", "p1", "p2", "zulu", "alpha");
            Connect(l, "entry", "p1");
            Connect(l, "entry", "p2");
            Connect(l, "p1", "zulu");
            Connect(l, "p2", "alpha");

            Assert.AreEqual("alpha", Deepest(l, "entry"),
                "an equal-depth tie must break on the LOWEST ordinal id, not on traversal order - otherwise the chest " +
                "moves when the layout is re-serialized and no regression can ever pin it.");
        }

        [Test]
        public void RepeatedCalls_AreDeterministic()
        {
            var l = Layout("entry", "p1", "p2", "zulu", "alpha", "mike");
            Connect(l, "entry", "p1");
            Connect(l, "entry", "p2");
            Connect(l, "p1", "zulu");
            Connect(l, "p1", "mike");
            Connect(l, "p2", "alpha");

            string first = Deepest(l, "entry");
            for (int i = 0; i < 25; i++)
            {
                Assert.AreEqual(first, Deepest(l, "entry"),
                    "DeepestRoomId must be deterministic for a given layout (call " + i + " disagreed) - a wandering " +
                    "chest cannot be regressed and would read to the player as a bug.");
            }
            Assert.AreEqual("alpha", first,
                "the deterministic answer for this layout is the lowest-ordinal room at max depth.");
        }

        // =====================================================================
        //  DEGENERATE INPUT - never guess, never throw
        // =====================================================================

        [Test]
        public void NullLayout_ReturnsNull()
        {
            Assert.IsNull(Deepest(null, "entry"),
                "a null layout must return null, not throw - the spawner calls this during scene load.");
        }

        [Test]
        public void EmptyLayout_ReturnsNull()
        {
            Assert.IsNull(Deepest(new DungeonComposeLayout(), "entry"),
                "an empty layout has no room to seat a reward in.");
            Assert.IsNull(Deepest(Layout(), "entry"),
                "a layout with an empty rooms[] must return null.");
        }

        [Test]
        public void MissingEntryRoom_ReturnsNull()
        {
            var l = Layout("a", "b");
            Connect(l, "a", "b");

            Assert.IsNull(Deepest(l, "entry"),
                "an entry id that is not in rooms[] must return null - guessing a BFS source would seat the chest " +
                "in an arbitrary room.");
            Assert.IsNull(Deepest(l, null), "a null entry id must return null.");
            Assert.IsNull(Deepest(l, ""), "an empty entry id must return null.");
        }

        [Test]
        public void NoConnections_ReturnsNull_NeverSeatsOnTheEntry()
        {
            // NOTE: this path logs a FlowTrace.Warn (Debug.LogWarning). The test runner only fails
            // on unexpected Error/Assert/Exception logs, so no LogAssert.Expect is needed here -
            // and adding one would make the test fail if the diagnostic were ever quietened.
            var l = Layout("entry", "a", "b");

            Assert.IsNull(Deepest(l, "entry"),
                "with no connections authored, nothing is deeper than the entry - returning the entry would put the " +
                "dungeon's reward two metres from the door.");
        }

        [Test]
        public void ConnectionToAnUnplacedRoom_IsIgnored()
        {
            var l = Layout("entry", "a");
            Connect(l, "entry", "a");
            Connect(l, "a", "ghost_room");   // never placed in rooms[]

            Assert.AreEqual("a", Deepest(l, "entry"),
                "a connection naming a room that was never placed must be ignored - the spawner looks the room up by " +
                "name under the compose root, so a ghost id means no chest at all.");
        }

        [Test]
        public void NullRoomEntries_AreSkipped_NotThrown()
        {
            var l = Layout("entry", "a");
            l.rooms.Add(null);
            l.connections.Add(null);
            Connect(l, "entry", "a");

            Assert.AreEqual("a", Deepest(l, "entry"),
                "a null row in rooms[]/connections[] (a hand-edited layout) must be skipped, never thrown on.");
        }

        // =====================================================================
        //  FIRST-CLEAR ONE-SHOT KEY (pure)
        // =====================================================================

        [Test]
        public void FirstClearKey_IsNamespacedPerDungeon()
        {
            string a = FirstClearKey("dg_starter_loop");
            string b = FirstClearKey("d4_sunken_crypt_spine");

            Assert.IsNotEmpty(a, "the first-clear key must never be empty.");
            Assert.AreNotEqual(a, b,
                "two dungeons must not share a first-clear key - clearing one would silently consume the other's " +
                "recipe unlock.");
            StringAssert.Contains("dg_starter_loop", a,
                "the key must carry the dungeon id or it is not actually namespaced per dungeon.");
            Assert.AreEqual(a, FirstClearKey("dg_starter_loop"),
                "the key must be stable across calls or the one-shot would re-grant forever.");
            Assert.IsNotEmpty(FirstClearKey(null),
                "an unnamed dungeon must still produce a usable key, not a blank SeenTutorials slot that poisons " +
                "every other dungeon's one-shot.");
        }
    }
}
