// =============================================================================
// QuestCompletionTests (EditMode)                    WO-854 Phase 0, Silo R
// -----------------------------------------------------------------------------
// The UNIT half of the quest-completability program. Its sibling,
// Assets/Editor/Regression/QuestCompletabilityRegression.cs, is the ORACLE: it
// walks the whole catalog and answers "how many of the 63 stages have a
// completion path". This fixture proves the MECHANICS that oracle's answer rests
// on are what everyone assumes they are.
//
// NEITHER FILE PROVES THE RUNTIME WALKS THE PATH. That is AssertStoryQuestAdvance
// (PlayMode headless, WO-854 Phase 1) and the PO's felt-close. Nothing here loads
// a scene or ticks a frame.
//
// ASSEMBLY BOUNDARY, stated so nobody wastes an hour on it: this fixture is in
// DeNelle.Tests.EditMode, which references DeNelle.Core / DeNelle.Village /
// DeNelle.Editor -- but NOT DeNelle.EditorRegression, where the oracle lives. So
// these tests deliberately assert the LIVE CONTRACTS the oracle depends on
// (TutorialSignals' latch behaviour, AdvanceQuest's ordinality, the catalog's
// shape, the authored data's baselines) rather than calling the oracle. Testing
// the oracle directly needs an asmdef reference that is not this silo's to add.
//
// WHAT IS OWED ONCE PHASE 2 LANDS (the completeOn matcher + StoryQuestSignalBridge):
// right-stage matching, wrong-signal rejection at the MATCHER level, the bridge's
// Clear-on-arm, and matcher idempotence. Today the matcher does not exist; the
// signal-bus tests below pin the exact semantics it will inherit, so those four
// assertions become a small edit rather than a redesign.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.Quests;
using DeNelle.Core.Tutorial;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class QuestCompletionTests
    {
        // ---------------------------------------------------------------------
        //  Baselines. Both are RATCHETS: they may only ever shrink. Each records a
        //  defect the oracle reports LOUDLY (it hard-fails on both) -- recording the
        //  count here is not a way to hide them, it is how a REGRESSION becomes
        //  impossible while the fix waits on an owner ruling.
        // ---------------------------------------------------------------------

        /// <summary>Distinct quest ids that dialogue verbs pass to QuestService and that do NOT
        /// exist in quests.json. Baseline 1: "companion.sylas", fired by SylasFirstMeeting and
        /// CompanionMeeting. QuestService.StartQuest logs the unknown id and returns, so the
        /// recruit beat plays and no quest state moves. WO-854 sec.6 ruling D4.</summary>
        private const int KnownOrphanQuestIds = 0;   // PAID 2026-08-04: Silo D removed the 6 companion.sylas verb sites. Ratchet tightened - a new orphan now FAILS.

        /// <summary>Distinct grantItemId values on quest stages that resolve in no shipped item
        /// catalog. Baseline 1: "iron-sword" on forgemaster.first-commission/claim-weapon.
        /// WO-854 sec.6 ruling D6.</summary>
        private const int KnownUnresolvedGrantItemIds = 0;   // PAID 2026-08-04: Silo C retargeted iron-sword -> knight_iron. Ratchet tightened - a new unresolvable grantItemId now FAILS.

        /// <summary>The denominator the whole program reports against (24 quests / 63 stages).
        /// If content legitimately changes this, the oracle's marker text, its
        /// BaselineTotalStages and WO-854 all move in the SAME edit -- otherwise every
        /// "n of 63" percentage quietly starts meaning something else.</summary>
        private const int ProgramStageCount = 63;

        private const string CanonicalRoot = "Resources/Data/Canonical/";

        private readonly List<string> _raisedInTest = new List<string>();

        [TearDown]
        public void ClearSignalsRaisedByThisTest()
        {
            // TutorialSignals is process-wide static state. Clear exactly what this test
            // raised (never ClearAll -- that would stomp state another fixture depends on).
            foreach (var id in _raisedInTest) TutorialSignals.Clear(id);
            _raisedInTest.Clear();
        }

        private void Raise(string id)
        {
            _raisedInTest.Add(id);
            TutorialSignals.Raise(id);
        }

        // =====================================================================
        //  THE SIGNAL BUS -- the semantics the completeOn matcher will inherit.
        //  These are the executable statement of WO-854 sec.2.3 trap (b).
        // =====================================================================

        [Test]
        public void Signal_Latches_AndStaysLatchedUntilCleared()
        {
            const string id = "test.quest.latch:holds";
            Assert.That(TutorialSignals.HasFired(id), Is.False, "precondition: the id must start unfired");

            Raise(id);
            Assert.That(TutorialSignals.HasFired(id), Is.True, "a raised signal must be observable");
            Assert.That(TutorialSignals.HasFired(id), Is.True,
                "the bus LATCHES: HasFired must keep answering true, which is exactly why a stage " +
                "awaiting an id that fired earlier in the session would complete the instant the quest " +
                "is accepted unless the bridge clears the latch first");

            TutorialSignals.Clear(id);
            Assert.That(TutorialSignals.HasFired(id), Is.False,
                "Clear is the ONLY antidote to latch poisoning - the completion bridge must call it " +
                "when a stage becomes current");
        }

        [Test]
        public void Signal_Clear_IsIdempotent_AndOnlyClearsTheNamedId()
        {
            const string mine = "test.quest.latch:mine";
            const string other = "test.quest.latch:other";
            Raise(mine);
            Raise(other);

            TutorialSignals.Clear(mine);
            TutorialSignals.Clear(mine);   // clearing twice must not throw and must not resurrect
            Assert.That(TutorialSignals.HasFired(mine), Is.False, "Clear must be idempotent");
            Assert.That(TutorialSignals.HasFired(other), Is.True,
                "Clear must be surgical - a bridge arming one stage must not wipe latches other " +
                "systems (the tutorial, a second active quest) are still waiting on");
        }

        [Test]
        public void Signal_DoubleRaise_IsOneLatch_AndOneClearRemovesIt()
        {
            const string id = "test.quest.latch:double";
            Raise(id);
            Raise(id);
            TutorialSignals.Clear(id);
            Assert.That(TutorialSignals.HasFired(id), Is.False,
                "raising twice must not require clearing twice - the latch is a set membership, not " +
                "a counter, so a matcher can treat completion as idempotent");
        }

        [Test]
        public void Signal_WrongId_DoesNotSatisfyAnAwaitedId()
        {
            const string awaited = "dialogue.ended:test_quest_elder";
            const string wrong = "dialogue.ended:test_quest_someone_else";
            const string prefixOnly = "dialogue.ended:";

            Raise(wrong);
            Raise(prefixOnly);

            Assert.That(TutorialSignals.HasFired(awaited), Is.False,
                "matching is on the WHOLE composed id. A different target, or the bare family prefix, " +
                "must never satisfy a stage - otherwise talking to any NPC would close a beat that " +
                "named a specific one");
        }

        [Test]
        public void Signal_MatchingIsCaseInsensitive()
        {
            const string raised = "dialogue.ended:Test_Quest_Case";
            Raise(raised);
            Assert.That(TutorialSignals.HasFired("dialogue.ended:test_quest_case"), Is.True,
                "the bus compares case-insensitively (TutorialSignals uses an OrdinalIgnoreCase set). " +
                "A completeOn targetId that differs only in case therefore MATCHES - authored data " +
                "must not rely on case to tell two targets apart");
        }

        // =====================================================================
        //  ORDINALITY -- the premise behind WO-854 sec.2.3 trap (a)
        // =====================================================================

        [Test]
        public void AdvanceQuest_IsOrdinal_TakingOnlyAQuestId()
        {
            var m = typeof(QuestService).GetMethod("AdvanceQuest", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(m, Is.Not.Null, "QuestService.AdvanceQuest is the single runtime advance seam");

            var ps = m.GetParameters();
            Assert.That(ps.Length, Is.EqualTo(1),
                "AdvanceQuest takes ONLY a quest id: it advances whatever stage is CURRENT and cannot be " +
                "addressed at a specific stage. That is why a completion oracle must prove a DISTINCT " +
                "source per stage INDEX - one dialogue node re-opened four times would otherwise 'complete' " +
                "a four-stage quest. If a stage-addressed overload is ever added, revisit " +
                "QuestCompletabilityRegression's distinctness rule in the SAME change.");
            Assert.That(ps[0].ParameterType, Is.EqualTo(typeof(string)));
            Assert.That(m.ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test]
        public void CompleteQuest_SkipsStages_SoItIsNotAStageCompletionSource()
        {
            var m = typeof(QuestService).GetMethod("CompleteQuest", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(m, Is.Not.Null);
            Assert.That(m.GetParameters().Length, Is.EqualTo(1),
                "CompleteQuest moves a quest straight to Completed by id. It pays no stage reward and " +
                "grants no keystone, so the oracle must NOT count it as a stage completion source - a " +
                "stage skipped is not a stage completed.");
        }

        // =====================================================================
        //  CATALOG SHAPE -- what the oracle counts
        // =====================================================================

        [Test]
        public void Catalog_LoadsThroughTheRealPath_WithTheProgramStageCount()
        {
            QuestCatalog.Reload();
            var quests = QuestCatalog.Quests;
            Assert.That(quests, Is.Not.Null.And.Count.GreaterThan(0),
                "QuestCatalog loaded EMPTY - every rumor-board row and every tracker line comes from here");

            int stages = 0;
            foreach (var q in quests) if (q != null && q.Stages != null) stages += q.Stages.Count;

            Assert.That(stages, Is.EqualTo(ProgramStageCount),
                "the program reports every result as 'n of " + ProgramStageCount + "'. The catalog now " +
                "holds " + stages + ". If that change is intended, update ProgramStageCount here, " +
                "BaselineTotalStages in QuestCompletabilityRegression and the denominator in WO-854 in " +
                "the same edit - a denominator that drifts silently makes every percentage a lie.");
        }

        [Test]
        public void Catalog_QuestIdsAreUnique_AndEveryQuestHasStages()
        {
            QuestCatalog.Reload();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var q in QuestCatalog.Quests)
            {
                Assert.That(q, Is.Not.Null);
                Assert.That(string.IsNullOrEmpty(q.Id), Is.False,
                    "every lookup in QuestService keys on the quest id; an id-less quest can be rendered " +
                    "but never started, advanced or completed");
                Assert.That(seen.Add(q.Id), Is.True,
                    "duplicate quest id '" + q.Id + "' - QuestCatalog.FindQuest returns the FIRST match, " +
                    "so the second definition's stages are unreachable content");
                Assert.That(q.Stages, Is.Not.Null.And.Count.GreaterThan(0),
                    "quest '" + q.Id + "' has no stages - StartQuest would seed beat 0 with a null stage " +
                    "id and the tracker would have nothing honest to show");
            }
        }

        [Test]
        public void Catalog_StageIdsAreUniqueWithinAQuest()
        {
            QuestCatalog.Reload();
            foreach (var q in QuestCatalog.Quests)
            {
                if (q == null || q.Stages == null) continue;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var s in q.Stages)
                {
                    Assert.That(s, Is.Not.Null);
                    Assert.That(string.IsNullOrEmpty(s.StageId), Is.False,
                        "quest '" + q.Id + "' has a stage with no stageId - it is what QuestState persists " +
                        "and what completability is proven per");
                    Assert.That(seen.Add(s.StageId), Is.True,
                        "quest '" + q.Id + "' repeats stageId '" + s.StageId + "' - two stages sharing one " +
                        "id cannot be told apart in the save or in the oracle's distinctness proof");
                }
            }
        }

        // =====================================================================
        //  FORWARD COMPATIBILITY -- completeOn arrives in Phase 2 (Silo S)
        // =====================================================================

        [Test]
        public void QuestStage_CompletionCondition_IsEitherAbsentOrANullableReferenceType()
        {
            var t = typeof(QuestStage);
            MemberInfo found = null;
            Type memberType = null;

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.Name.IndexOf("CompleteOn", StringComparison.OrdinalIgnoreCase) >= 0)
                { found = f; memberType = f.FieldType; }
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (p.Name.IndexOf("CompleteOn", StringComparison.OrdinalIgnoreCase) >= 0)
                { found = p; memberType = p.PropertyType; }

            if (found == null)
            {
                // Today's tree: no data-driven completion condition exists, so the ONLY route to a
                // stage completion is a dialogue authoring AdvanceQuest. That is a fact about the
                // build, not a failure - the oracle reports the resulting count honestly.
                Assert.Pass("QuestStage carries no completion condition yet (WO-854 Phase 2, Silo S owns " +
                            "adding it). The legacy dialogue-command path is the only completion route, " +
                            "and the oracle counts accordingly.");
                return;
            }

            Assert.That(memberType.IsValueType, Is.False,
                "the completion condition must be a REFERENCE type so a stage that omits it deserializes " +
                "to null. A value type would make 'no condition authored' indistinguishable from a " +
                "zero-valued condition, and every legacy stage would silently gain one.");
        }

        // =====================================================================
        //  AUTHORED-DATA RATCHETS -- these may only ever shrink
        // =====================================================================

        [Test]
        public void DialogueQuestVerbs_OrphanQuestIdCount_OnlyEverGoesDown()
        {
            var questIds = QuestIdsFromJson();
            var dialogues = ReadCanonical("dialogue/dialogues.json");
            string[] verbs = { "StartQuest", "AdvanceQuest", "CompleteQuest", "SetQuestFlag", "SetFlag" };

            var orphans = new SortedSet<string>(StringComparer.Ordinal);
            var dlgArray = dialogues["dialogues"] as JArray;
            Assert.That(dlgArray, Is.Not.Null, "dialogues.json has no 'dialogues' array");

            foreach (var d in dlgArray)
            {
                var nodes = d["nodes"] as JArray;
                if (nodes == null) continue;
                foreach (var n in nodes)
                {
                    var cmds = n["commands"] as JArray;
                    if (cmds == null) continue;
                    foreach (var c in cmds)
                    {
                        string verb = (string)c["verb"];
                        if (string.IsNullOrEmpty(verb) || Array.IndexOf(verbs, verb) < 0) continue;
                        var args = c["args"] as JArray;
                        if (args == null || args.Count == 0) continue;
                        string id = (string)args[0];
                        if (string.IsNullOrEmpty(id) || questIds.Contains(id)) continue;
                        orphans.Add(id);
                    }
                }
            }

            Assert.That(orphans.Count, Is.LessThanOrEqualTo(KnownOrphanQuestIds),
                "a NEW quest verb points at an id that is not in quests.json (" +
                string.Join(", ", new List<string>(orphans).ToArray()) + "). QuestService.StartQuest logs " +
                "the unknown id and returns, so the beat plays and no quest state moves - a silent no-op " +
                "dressed as progress. Fix the new one; the baseline of " + KnownOrphanQuestIds +
                " is a recorded defect (companion.sylas, WO-854 ruling D4) that the oracle still hard-fails on.");
        }

        [Test]
        public void QuestRewards_UnresolvedGrantItemIdCount_OnlyEverGoesDown()
        {
            var items = ItemIdsFromCatalogs();
            Assert.That(items.Count, Is.GreaterThan(0),
                "no item catalog resolved any ids - a reward that can never be paid would pass unseen");

            var unresolved = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var quests = ReadCanonical("quests.json")["quests"] as JArray;
            Assert.That(quests, Is.Not.Null);
            foreach (var q in quests)
            {
                var stages = q["stages"] as JArray;
                if (stages == null) continue;
                foreach (var s in stages)
                {
                    var reward = s["reward"];
                    if (reward == null) continue;
                    // WO-1202: reward is a typed list of {kind,id}/{kind,amount}.
                    if (reward is JArray lines)
                    {
                        foreach (var line in lines)
                        {
                            if ((string)line["kind"] != "item") continue;
                            string item = (string)line["id"];
                            if (string.IsNullOrEmpty(item) || items.Contains(item)) continue;
                            unresolved.Add(item);
                        }
                    }
                    else
                    {
                        string item = (string)reward["grantItemId"];
                        if (string.IsNullOrEmpty(item) || items.Contains(item)) continue;
                        unresolved.Add(item);
                    }
                }
            }

            Assert.That(unresolved.Count, Is.LessThanOrEqualTo(KnownUnresolvedGrantItemIds),
                "a NEW quest stage grants an item id that resolves in no shipped catalog (" +
                string.Join(", ", new List<string>(unresolved).ToArray()) + "). VillageInventory.Add would " +
                "store a key nothing can render, name or equip, so the promised reward is unpayable. The " +
                "baseline of " + KnownUnresolvedGrantItemIds + " is a recorded defect (iron-sword, WO-854 " +
                "ruling D6) that the oracle still hard-fails on.");
        }

        [Test]
        public void CanonicalQuestData_IsByteIdenticalAcrossBothCopies()
        {
            AssertDualCopiesMatch("quests.json");
            AssertDualCopiesMatch("dialogue/dialogues.json");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static string ResourcesPath(string relative) =>
            Path.Combine(Application.dataPath, CanonicalRoot + relative);

        private static string StreamingPath(string relative) =>
            Path.Combine(Application.streamingAssetsPath, "Data/Canonical/" + relative);

        private static JObject ReadCanonical(string relative)
        {
            string path = ResourcesPath(relative);
            Assert.That(File.Exists(path), Is.True, "missing canonical file: " + path);
            return JObject.Parse(File.ReadAllText(path));
        }

        private static void AssertDualCopiesMatch(string relative)
        {
            string res = ResourcesPath(relative);
            string sa = StreamingPath(relative);
            Assert.That(File.Exists(res), Is.True, "missing Resources copy: " + res);
            Assert.That(File.Exists(sa), Is.True, "missing StreamingAssets copy: " + sa);
            Assert.That(File.ReadAllText(res), Is.EqualTo(File.ReadAllText(sa)),
                relative + " differs between its two copies. CanonicalJson reads Resources FIRST, so an " +
                "edit made in only one copy is invisible to the shipped player. Write both, always.");
        }

        private static HashSet<string> QuestIdsFromJson()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var quests = ReadCanonical("quests.json")["quests"] as JArray;
            Assert.That(quests, Is.Not.Null, "quests.json has no 'quests' array");
            foreach (var q in quests)
            {
                string id = (string)q["id"];
                if (!string.IsNullOrEmpty(id)) set.Add(id);
            }
            return set;
        }

        private static HashSet<string> ItemIdsFromCatalogs()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new Dictionary<string, string>
            {
                { "weapons.json", "weapons" },
                { "armor.json", "armor" },
                { "accessories.json", "accessories" },
                { "consumables.json", "consumables" },
                { "materials.json", "materials" },
            };
            foreach (var kv in files)
            {
                string path = ResourcesPath(kv.Key);
                if (!File.Exists(path)) continue;
                var arr = JObject.Parse(File.ReadAllText(path))[kv.Value] as JArray;
                if (arr == null) continue;
                foreach (var e in arr)
                {
                    string id = (string)e["id"];
                    if (!string.IsNullOrEmpty(id)) set.Add(id);
                }
            }
            return set;
        }
    }
}
