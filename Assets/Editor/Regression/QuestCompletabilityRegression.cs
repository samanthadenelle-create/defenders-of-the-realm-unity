// =============================================================================
// QuestCompletabilityRegression [quest-reach]        WO-854 Phase 0, Silo R
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// regression-registry: DataRegression.RunAll (single registration, DataRegression.cs:510)
//
// ⚠ THE OLD HEADER HERE SAID "regression-registry: standalone" AND "0/63 stages are
// completable". BOTH WERE STALE and are corrected (2026-08-21). The suite HAS been wired
// into DataRegression.RunAll since WO-854's final wave; the baseline it was written against
// (zero sourced stages, three open owner rulings) closed the same week and all 63 stages are
// sourced today -- MinCompletableStages below is the live floor. A header that still argued
// "wire it in the moment those clear" invited exactly one of the two errors this file must
// never make: a SECOND registration (double-counted failures) or a seat "re-wiring" an
// already-wired suite. There is ONE registration line and it lives in DataRegression.cs.
// It still publishes its number standalone for a fast read:
//     run-unity-method -Method DeNelle.Editor.Regression.QuestCompletabilityRegression.RunAll
//
// WHAT THIS ORACLE PROVES, AND WHAT IT DOES NOT -- read this before quoting it.
//
//   IT PROVES: a completion path EXISTS in the shipped data + source for a stage.
//   IT DOES NOT PROVE THE RUNTIME WALKS THAT PATH. No scene is loaded, no frame is
//   ticked, no save is round-tripped. A stage this suite counts as "completable"
//   is a stage the authored data says CAN be completed -- not one anybody watched
//   complete.
//
//   The four layers, and which artifact owns each (WO-854 sec.2):
//     data/logic (this file) .. a reachable path exists for <n> of the stages
//     unit ..................... Assets/Tests/EditMode/QuestCompletionTests.cs
//     PlayMode headless ........ AssertStoryQuestAdvance in AutoPilotDriver.cs (P1)
//                                -- Accept -> signal -> BeatIndex -> reward paid ->
//                                survives a save round-trip. THAT is "the runtime
//                                walks it".
//     felt ..................... the PO closes the ticket. No marker substitutes.
//
//   Quoting a green QUEST_REACH_OK as "the quests work" is the exact category error
//   AegisSetReachabilityRegression already shipped once: that suite passes today
//   while the whole aegis set is UNOBTAINABLE, because it proves co-EQUIPPABLE, not
//   ACQUIRABLE. Case 6 below closes that particular gap; nothing closes the gap
//   between "a path exists" and "the game walks it" except PlayMode and a human.
//
// THE DEFECT CLASS THIS PINS (owner ruling 2026-08-03): a quest that can be
// ACCEPTED and TRACKED but never COMPLETED is a BUG, not an unbuilt feature -- the
// game makes a promise it cannot keep. As of the baseline every one of the 24
// shipped quests was acceptable off the rumor board with no gate at all and NONE
// of their stages had a completion source of any kind. Since then RumorBoardVM
// honours QuestDef.RequiresQuestId, so the board offers a quest only once its
// prerequisite is completed - and the mirror-image defect is now in scope too: a
// quest gated behind one nobody can finish is just as unkeepable a promise, and
// Case 1's chain walk stops its stages counting toward the score.
//
// ---------------------------------------------------------------------------
//  THE THIRTEEN CASES  (0-8 story quests; 9-12 added 2026-08-21, quest audit)
// ---------------------------------------------------------------------------
//   0 [catalog-shape]   Both copies of quests.json are byte-identical; quest ids
//                       are unique and non-empty; every quest has at least one
//                       stage; stage ids are unique WITHIN a quest (the per-index
//                       key distinctness depends on). Plus: the count of stages the
//                       real load path (QuestCatalog) sees equals the count in the
//                       raw JSON -- a DTO field that stops mapping is how a
//                       completion condition can be authored and silently ignored.
//
//   1 [entry-live]      Every quest is ENTERABLE, in TWO layers.
//                       (a) OFFERED: the rumor board renders every non-completed
//                       catalog quest and its Accept path calls StartQuest
//                       (source-lint of RumorBoardVM.cs), and the board itself has
//                       an opener. If that lint ever fails, each quest must instead
//                       be named by a StartQuest author.
//                       (b) UNGATED: a quest carrying requiresQuestId is offered
//                       only once that prerequisite is COMPLETED, so it is reachable
//                       only if the prerequisite is itself reachable AND fully
//                       completable -- walked TRANSITIVELY down the chain
//                       (forgemasters_act1 -> act2 -> act3 -> act4). A prerequisite
//                       naming an unknown quest, or a CYCLE, makes every quest in it
//                       unreachable and is reported once, never recursed into.
//                       An unreachable quest contributes ZERO completable stages no
//                       matter what else is authored -- which is the whole point: an
//                       act nobody can reach must not pad the score, or the number
//                       would say the terminal reward is earnable when it is not.
//
//   2 [advance-live]    THE SPINE. Every stage index needs a DISTINCT completion
//                       source: a completeOn whose composed signal has a LIVE
//                       emitter under Assets/_Modules, or a dialogue node that
//                       authors AdvanceQuest for that quest, in a dialogue the
//                       player can open, at a node reachable from the entry node.
//
//                       TRAP (a) -- DISTINCTNESS. QuestService.AdvanceQuest is
//                       ORDINAL (QuestService.cs:119-146): it advances whatever
//                       stage is CURRENT and takes no stage id. One dialogue node
//                       re-opened four times therefore "completes" a four-stage
//                       quest, and a naive oracle scores it 4/4. So each source is
//                       consumed by exactly ONE stage index: n distinct reachable
//                       AdvanceQuest nodes back the FIRST n stages and no more, and
//                       two stages of one quest carrying the same completeOn is a
//                       hard failure.
//
//                       ORDINALITY also makes completability a PREFIX: stage 2
//                       cannot be reached until stage 1 has been left, so a quest's
//                       completable count is the run of sourced stages starting at
//                       index 0, and a later sourced stage behind an unsourced one
//                       is reported blocked-ordinally, never counted.
//
//                       TRAP (b) -- LATCH POISONING. TutorialSignals LATCHES
//                       (TutorialSignals.cs:55-56 the _fired set, :77-78 HasFired).
//                       A stage whose completeOn names an id that ALREADY fired
//                       would complete the instant the quest is accepted. The
//                       completion bridge must call TutorialSignals.Clear(awaitedId)
//                       when a stage becomes current. This suite lints for that
//                       Clear call the moment the bridge file exists.
//
//                       NOT a source: CompleteQuest. It jumps a quest straight to
//                       Completed (QuestService.cs:150-160) WITHOUT paying any
//                       stage reward and WITHOUT granting any keystone. A stage
//                       skipped is not a stage completed.
//
//   3 [speaker-embodied] Every dialogue a Case 2 source names resolves in
//                       dialogues.json and is openable; and every speaker on every
//                       authored line resolves to a speakers[] record, so no quest
//                       conversation renders a nameless card.
//
//   4 [referent-resolves] A completeOn targetId must resolve in the catalog its
//                       kind implies (hard failure -- a stage awaiting a structure
//                       or species that does not ship can never complete). Proper
//                       nouns in objectiveText are a NOTE LEDGER, not a failure:
//                       prose naming a place or creature that ships nowhere is a
//                       content decision for the PO, and failing on it would be
//                       failing on missing CONTENT.
//
//   5 [reward-payable]  The dispenser is live end to end (QuestService raises
//                       RewardEarned; QuestRewardBridge subscribes it and routes
//                       crystals/food, magic and grantItemId), and every non-empty
//                       grantItemId resolves in a shipped item catalog. An item id
//                       that resolves nowhere is a reward that can never be paid.
//
//   6 [terminal-consumer] Every gear-recipes.json requiresQuestId names a real
//                       quest AND a quest whose every stage is completable. This is
//                       the gap AegisSetReachabilityRegression cannot see: it
//                       reports the Oathweld set green because the set is
//                       co-equippable, while the five legendary recipes that mint
//                       those items are locked behind a quest nobody can finish.
//
//   7 [no-orphan-verbs] Every quest id a dialogue verb (or a literal C# call)
//                       passes to QuestService resolves in quests.json. An unknown
//                       id is a SILENT NO-OP at QuestService.cs:92 -- the beat plays,
//                       the player is told a thing happened, and no state moves.
//
//   8 [flag-satisfiable] Every non-empty requiresFlag on a stage has a matching
//                       SetQuestFlag/SetFlag emitter for the OWNING quest. A stage
//                       gated on a flag nothing sets can never satisfy its gate.
//
// ---------------------------------------------------------------------------
//  CASES 9-12 -- ADDED 2026-08-21 BY THE QUEST AUDIT. Read WHY before touching them.
// ---------------------------------------------------------------------------
//  The audit found TWO OF THE THREE DAILY SLOTS could never complete, and had been
//  dead for MONTHS with nothing detecting it. Dailies are how a brand-new player earns
//  extra resources, so this was the on-ramp, broken for everyone:
//    * combat.build-towers (day1Guaranteed) ticked only from
//      TowerPlacementSystem.OnTowerPlaced, whose sole caller (BuildMenu) is in NO scene
//      and NO prefab. Worst of the three: DailyQuests FORCE-RETURNS a day1Guaranteed
//      template for its slot on EVERY roll while Day1QuestDone is false, and that latch
//      is set only on COMPLETION -- so an uncompletable day-1 quest does not cost one
//      day, it PINS the combat slot forever.
//    * the 17 combat.clear-waves.* templates reported through DailyQuestCombatBridge,
//      which was attached ONLY by the editor-only VillageSceneBuilder -- it never
//      existed in a player build.
//    * the 12 explore.visit-gate.* templates ticked from GateProximityOpener, attached
//      only by VillageController, whose guid is in no scene either.
//  Every one of those is a MISSING TICK, and a missing tick is invisible: it looks
//  exactly like "the player did not do it yet". So the oracle proves the tick PATH
//  exists for every template, by enumeration, so a template added tomorrow is covered
//  the day it lands and nobody has to remember this file exists.
//
//   9 [kind-incrementer] For EVERY completeOn.kind authored in quests.json, a NAMED live
//                       incrementer must exist in source. Case 2 already proves the
//                       composed SIGNAL has a Raise site; this case pins the seam that
//                       feeds the Raise, so deleting the subscription (not the Raise) is
//                       caught too: talk -> DialogueService.EndedWithId; panel ->
//                       PanelRouter.PanelOpened (both in TutorialCoreSignalAdapter);
//                       wave -> WaveManager.OnWaveCleared; arena ->
//                       BattleArena.OnBattleEnded; build ->
//                       BuildModeController.StructurePlaced (all three in
//                       TutorialSignalAdapters); pet -> PetAcquisitionService raising
//                       TutorialSignals.PetBondedPrefix. A kind with NO row in that table
//                       is reported as a NOTE naming itself as UNJUDGED by this case (Case 2
//                       still judges its signal) -- never as a silent pass, because "the
//                       oracle is not looking here" must be visible in the output.
//
//  10 [daily-reachable] EVERY template in daily-quests.json must be satisfiable by some
//                       DailyQuestService.Report call site, under the SERVICE'S OWN
//                       matching rule (DailyQuests.cs:259 -- exact, or template starts
//                       with reportedId + "."). Enumerated from the JSON, never a
//                       hand-list. AND THE REVERSE: a reporter whose id matches NO
//                       template is a dead call -- the code believes it is crediting the
//                       player and nothing moves. Both directions fail hard.
//
//  11 [daily-bridge-live] The three audit fixes, pinned so they cannot regress:
//                       DailyQuestTowerBridge subscribes BuildModeController.
//                       StructurePlaced (NOT only the legacy TowerPlacementSystem event);
//                       WaveSystemBridgeBootstrap contains Ensure<DailyQuestCombatBridge>;
//                       StructureFactory's "Gate" case attaches GateProximityOpener.
//                       PLUS THE INVARIANT THAT MAKES THE PREFIX FILTER SAFE: every
//                       structures-catalog.json row typed "Tower" has an id starting
//                       "tower_". No runtime code reads that catalog's type field, so the
//                       bridge uses a prefix test as its stand-in -- a Tower row added
//                       without the prefix would silently never count toward the day-1
//                       quest, which is the same invisible-miss class all over again.
//
//  12 [day1-guaranteed] Every day1Guaranteed template must be PROVABLY completable by
//                       Case 10's evidence. Separated from Case 10 on purpose: the force-
//                       return latch makes an uncompletable day-1 template categorically
//                       worse than an uncompletable ordinary one, and the failure string
//                       has to say so or the reader triages it as one row of 41.
//
// ---------------------------------------------------------------------------
//  THE RATCHET -- how "repeat till 100%" is enforced
// ---------------------------------------------------------------------------
//   MinCompletableStages is the stage count proven completable as of the last
//   SHIPPED phase. It only ever goes UP, and every phase's acceptance criterion is
//   "raise the floor and still pass". Without it either nothing ships until 63 or
//   nothing is enforced at all. Backsliding below the floor is a hard failure.
//
//   A PREREQUISITE CAN LOWER THE NUMBER, and that is correct, not a regression to
//   soften: gating act4 behind act3 removes act4's stages from the score unless act3
//   is itself finishable. If that ever drops the count below the floor, the honest
//   move is to make the earlier act completable (or to have the PO rule the gate
//   wrong) - never to stop counting the gate.
//
//   NOT-YET-AUTHORED IS NOT A CASE 2 FAILURE. A stage with no completion source is
//   counted (it lowers <n>) and listed in the ledger; it does not fail Case 2. What
//   fails Case 2 is a source that is BROKEN -- a dead emitter, an unopenable or
//   unreachable dialogue, or two stages sharing one source. That split is what lets
//   the floor rise phase by phase instead of the suite screaming 63 times a run.
//
// Markers: QUEST_REACH_OK <n>/<total> stages completable / QUEST_REACH_FAIL: ...
//          QUEST_COMPLETABILITY_OK / QUEST_COMPLETABILITY_FAIL are emitted as ALIASES on
//          the same line. QUEST_REACH_* is the ORIGINAL and is kept because WO-854's
//          RESULT, the lanes banner and the true-status doc all grep for it; the alias
//          exists so a reader who greps the file's own name finds the verdict too. Never
//          rename one without the other, and never emit only one.
// Standalone: run-unity-method DeNelle.Editor.Regression.QuestCompletabilityRegression.RunAll
//
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced, WO-854
// sec.5 -- same convention as ItemIdentityRegression.cs:53-55):
//
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "quest-reach suite", () => { if (!QuestCompletabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[quest-reach] " + r); });
//
// COMMITTER, READ THIS BEFORE WIRING: on the P0 baseline tree this suite is RED and
// it is red HONESTLY -- Case 5 (a grantItemId that resolves in no catalog) and
// Case 7 (a quest verb naming an id that is not in quests.json) are both real
// shipped defects, and WO-854 sec.6 routes both to owner rulings (D6, D4). Wiring
// this into DataRegression.RunAll turns the aggregate suite red until those land.
// That timing is the committer's call. The oracle does not soften a live defect
// into a note to keep a gate green -- that is how a gate stops meaning anything.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class QuestCompletabilityRegression
    {
        // =====================================================================
        //  THE RATCHET
        // =====================================================================

        /// <summary>Stages proven completable as of the last SHIPPED phase. This only
        /// ever goes UP. Raise it in the SAME commit that raises the real count.</summary>
        private const int MinCompletableStages = 63;  // WO-854 final wave (2026-08-04): all 63 stages sourced. ONLY EVER GOES UP - any backslide is a hard failure.

        /// <summary>The stage count the program was scoped against (24 quests / 63
        /// stages). Drift is a NOTE, not a failure -- content may legitimately grow --
        /// but it is called out so nobody reads a shifting denominator as progress.</summary>
        private const int BaselineTotalStages = 63;

        // =====================================================================
        //  Sources of truth
        // =====================================================================

        private const string QuestsRes      = "Assets/Resources/Data/Canonical/quests.json";
        private const string QuestsSA       = "Assets/StreamingAssets/Data/Canonical/quests.json";
        private const string DialoguesRes   = "Assets/Resources/Data/Canonical/dialogue/dialogues.json";
        private const string DialoguesSA    = "Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json";
        private const string GearRecipesRes = "Assets/Resources/Data/Canonical/gear-recipes.json";
        private const string GearRecipesSA  = "Assets/StreamingAssets/Data/Canonical/gear-recipes.json";

        private const string StructuresRes  = "Assets/Resources/Data/Canonical/structures-catalog.json";
        private const string PetsRes        = "Assets/Resources/Data/Canonical/pets.json";
        private const string EnemiesRes     = "Assets/Resources/Data/Canonical/enemies.json";
        private const string RealmMapRes    = "Assets/Resources/Data/Canonical/realm-map.json";
        private const string WeaponsRes     = "Assets/Resources/Data/Canonical/weapons.json";
        private const string ArmorRes       = "Assets/Resources/Data/Canonical/armor.json";
        private const string AccessoriesRes = "Assets/Resources/Data/Canonical/accessories.json";
        private const string ConsumablesRes = "Assets/Resources/Data/Canonical/consumables.json";
        private const string MaterialsRes   = "Assets/Resources/Data/Canonical/materials.json";
        private const string GlossaryRes    = "Assets/Resources/Data/Canonical/glossary.json";
        private const string CanonStringsRes = "Assets/Resources/Data/Canonical/canon-strings.json";

        // --- daily quests (cases 10-12) --------------------------------------
        private const string DailyQuestsRes = "Assets/Resources/Data/Canonical/daily-quests.json";
        private const string DailyQuestsSA  = "Assets/StreamingAssets/Data/Canonical/daily-quests.json";

        private const string TowerBridgeSrc      = "Assets/_Modules/Village/Buildings/DailyQuestTowerBridge.cs";
        private const string WaveBootstrapSrc    = "Assets/_Modules/Village/WaveSystemBridgeBootstrap.cs";
        private const string StructureFactorySrc = "Assets/_Modules/Village/Catalog/StructureFactory.cs";
        private const string GateOpenerSrc       = "Assets/_Modules/Village/Gates/GateProximityOpener.cs";
        private const string CombatBridgeSrc     = "Assets/_Modules/Village/Waves/DailyQuestCombatBridge.cs";

        private const string ModulesRoot        = "Assets/_Modules";
        private const string RumorBoardSrc      = "Assets/_Modules/Village/Hero/RumorBoardVM.cs";
        private const string QuestServiceSrc    = "Assets/_Modules/Core/Quests/QuestService.cs";
        private const string RewardBridgeSrc    = "Assets/_Modules/Village/Quests/QuestRewardBridge.cs";
        private const string SignalBridgeSrc    = "Assets/_Modules/Village/Quests/StoryQuestSignalBridge.cs";
        private const string SignalAdaptersSrc  = "Assets/_Modules/Village/Tutorial/V2/TutorialSignalAdapters.cs";
        private const string TutorialFlowSrc    = "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs";

        /// <summary>The verbs that reach QuestService with a quest id as arg 0.</summary>
        private static readonly string[] QuestVerbs =
        { "StartQuest", "AdvanceQuest", "CompleteQuest", "SetQuestFlag", "SetFlag" };

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Completable stage count from the most recent Run (marker payload).</summary>
        public static int LastCompletableStages { get; private set; }

        /// <summary>Total authored stage count from the most recent Run (marker payload).</summary>
        public static int LastTotalStages { get; private set; }

        /// <summary>Daily templates enumerated from daily-quests.json on the most recent Run.</summary>
        public static int LastDailyTemplates { get; private set; }

        /// <summary>Daily templates PROVEN reachable (some Report call site can satisfy them).</summary>
        public static int LastDailyTemplatesProven { get; private set; }

        /// <summary>Standalone batch entry - prints the marker, which always carries n/total.</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            string tally = LastCompletableStages + "/" + LastTotalStages + " stages completable, " +
                           LastDailyTemplatesProven + "/" + LastDailyTemplates + " daily templates reachable";
            if (ok) Debug.Log("QUEST_REACH_OK QUEST_COMPLETABILITY_OK " + tally + " - " + reason);
            else Debug.LogError("QUEST_REACH_FAIL QUEST_COMPLETABILITY_FAIL: " + tally + " - " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var ledger = new List<string>();
            LastCompletableStages = 0;
            LastTotalStages = 0;

            LastDailyTemplates = 0;
            LastDailyTemplatesProven = 0;

            var ctx = new Ctx();
            var daily = new DailyCtx();
            try
            {
                // The DAILY half runs FIRST and UNCONDITIONALLY. It shares no state with the
                // story half on purpose: a broken quests.json must not silently take the daily
                // coverage down with it, because the dailies are the thing every new player
                // touches and the thing that was dead for months.
                Case(failures, "daily-reachable",   () => Case10_DailyReachable(daily, failures, notes));
                Case(failures, "daily-bridge-live", () => Case11_DailyBridgeLive(daily, failures, notes));
                Case(failures, "day1-guaranteed",   () => Case12_Day1Guaranteed(daily, failures, notes));

                Case(failures, "catalog-shape",     () => Case0_CatalogShape(ctx, failures, notes));
                if (ctx.Quests.Count > 0)
                {
                    LoadSupportData(ctx, failures, notes);
                    Case(failures, "entry-live",        () => Case1_EntryLive(ctx, failures, notes));
                    Case(failures, "advance-live",      () => Case2_AdvanceLive(ctx, failures, notes, ledger));
                    Case(failures, "speaker-embodied",  () => Case3_SpeakerEmbodied(ctx, failures, notes));
                    Case(failures, "referent-resolves", () => Case4_ReferentResolves(ctx, failures, notes));
                    Case(failures, "reward-payable",    () => Case5_RewardPayable(ctx, failures, notes));
                    Case(failures, "terminal-consumer", () => Case6_TerminalConsumer(ctx, failures, notes));
                    Case(failures, "no-orphan-verbs",   () => Case7_NoOrphanVerbs(ctx, failures, notes));
                    Case(failures, "flag-satisfiable",  () => Case8_FlagSatisfiable(ctx, failures, notes));
                    Case(failures, "kind-incrementer",  () => Case9_KindIncrementer(ctx, failures, notes));
                }
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            LastCompletableStages = ctx.Completable;
            LastTotalStages = ctx.TotalStages;

            // THE RATCHET. Backsliding is a hard failure: a phase that lands and loses
            // ground has broken something the previous phase paid for.
            if (ctx.TotalStages > 0 && ctx.Completable < MinCompletableStages)
                failures.Add("[ratchet] only " + ctx.Completable + " of " + ctx.TotalStages +
                             " stages are completable, BELOW the shipped floor of " + MinCompletableStages +
                             " - a completion source that used to exist is gone. Restore it, or (if the " +
                             "removal was deliberate) lower MinCompletableStages in the SAME commit and say why.");

            if (ctx.TotalStages != BaselineTotalStages && ctx.TotalStages > 0)
                notes.Add("stage count is " + ctx.TotalStages + ", not the " + BaselineTotalStages +
                          " this program was scoped against - the denominator moved, so compare percentages with care");

            string tally = ctx.Completable + "/" + ctx.TotalStages + " stages completable across " +
                           ctx.Quests.Count + " quests (floor " + MinCompletableStages + "); " +
                           LastDailyTemplatesProven + "/" + LastDailyTemplates +
                           " daily templates reachable across " + daily.Slots.Count + " slots";

            // FULL detail goes to the log; the reason string that DataRegression aggregates
            // stays bounded. Truncation always names how many entries it dropped, so the log
            // is the complete ledger and the reason is never a quiet lie about its size.
            LogDetail(ctx, daily, failures, ledger, notes, tally);

            string ledgerStr = ledger.Count > 0
                ? " [not-yet-completable x" + ledger.Count + ": " + Join(ledger, 10) + "]"
                : "";
            string noteStr = notes.Count > 0 ? " [notes x" + notes.Count + ": " + Join(notes, 10) + "]" : "";

            if (failures.Count == 0)
            {
                reason = "QUEST REACHABILITY OK - " + tally + "; every authored completion source is " +
                         "distinct per stage index and backed by a live emitter or an openable dialogue, " +
                         "every quest is enterable, every reward is payable, every quest verb resolves, " +
                         "every daily template has a Report call site that can satisfy it and every " +
                         "Report call site has a template it can reach" +
                         ledgerStr + noteStr;
                return true;
            }
            reason = "quest-reach FAIL x" + failures.Count + " - " + tally + ": " +
                     Join(failures, 12) + ledgerStr + noteStr;
            return false;
        }

        /// <summary>Writes the complete per-quest tally, the full not-yet-completable ledger and
        /// the full note list to the log. This is the artifact a phase reads to decide what to
        /// author next; the reason string is only the headline.</summary>
        private static void LogDetail(Ctx ctx, DailyCtx daily, List<string> failures, List<string> ledger,
                                      List<string> notes, string tally)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- QUEST COMPLETABILITY (WO-854 + 2026-08-21 daily audit) --- " + tally);
            if (daily != null && daily.Templates.Count > 0)
            {
                sb.AppendLine("  DAILY REPORT CALL SITES x" + daily.Reporters.Count + ":");
                var shown = new HashSet<string>(StringComparer.Ordinal);
                foreach (var r in daily.Reporters)
                {
                    if (!shown.Add(r.Id + "|" + r.File)) continue;
                    int hits = 0;
                    foreach (var t in daily.Templates) if (DailyMatches(t.Id, r.Id)) hits++;
                    sb.AppendLine("    '" + r.Id + "' -> " + hits + " template(s)   [" + r.File + "]");
                }
                foreach (var t in daily.Templates)
                    if (!daily.Proven.Contains(t.Id))
                        sb.AppendLine("    UNREACHABLE TEMPLATE: " + t.Id + " (slot " + t.Slot + ")");
            }
            foreach (var q in ctx.Quests)
            {
                string why = q.EntryLive
                    ? (string.IsNullOrEmpty(q.RequiresQuestId) ? "" : "  [after " + q.RequiresQuestId + "]")
                    : (q.EntryUngated && !string.IsNullOrEmpty(q.RequiresQuestId)
                        ? "  [GATED behind " + q.RequiresQuestId + "; " + q.SourcedPrefix + " sourced]"
                        : "  [NOT ENTERABLE]");
                sb.AppendLine("  " + q.Id + ": " + q.CompletablePrefix + "/" + q.Stages.Count +
                              " completable" + why);
            }
            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES x" + failures.Count + ":");
                foreach (var f in failures) sb.AppendLine("  * " + f);
            }
            if (ledger.Count > 0)
            {
                sb.AppendLine("NOT YET COMPLETABLE x" + ledger.Count + ":");
                foreach (var l in ledger) sb.AppendLine("  - " + l);
            }
            if (notes.Count > 0)
            {
                sb.AppendLine("NOTES x" + notes.Count + ":");
                foreach (var n in notes) sb.AppendLine("  . " + n);
            }
            Debug.Log(sb.ToString());
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Model -- read from the AUTHORED JSON on purpose.
        //  The file is the truth; reading it directly means the suite still reports
        //  the real defect if a C# DTO field mapping is ever dropped (and Case 0
        //  cross-checks the DTO against it precisely to catch that).
        // =====================================================================

        private sealed class StageRec
        {
            public int Index;
            public string StageId = "";
            public string ObjectiveText = "";
            public string RequiresFlag = "";
            public bool GrantsKeystone;
            public int Crystals, Food, Magic;
            public string GrantItemId = "";
            public bool HasCompleteOn;
            public string OnKind = "";
            public string OnTarget = "";
            public int OnCount = 1;
        }

        private sealed class QuestRec
        {
            public string Id = "";
            public string Title = "";
            /// <summary>The quest that must be completed first, or empty.</summary>
            public string RequiresQuestId = "";
            public List<StageRec> Stages = new List<StageRec>();
            /// <summary>Case 1 layer (a): the board offers this quest (or a StartQuest author
            /// names it), ignoring any prerequisite.</summary>
            public bool EntryUngated;
            /// <summary>Case 1 layer (b): OFFERED and its prerequisite chain is satisfiable.
            /// Resolved after Case 2 has sourced every quest, because the gate asks whether the
            /// prerequisite is fully COMPLETABLE, which is Case 2's answer.</summary>
            public bool EntryLive;
            /// <summary>The run of SOURCED stages from index 0, computed with no reference to
            /// reachability. Sourcing (Case 2) and reachability (Case 1) are deliberately
            /// separate: the score is their product, so a gated quest reports "0 of n, gated"
            /// instead of n bogus "blocked ordinally" lines.</summary>
            public int SourcedPrefix;
            /// <summary>What this quest actually contributes: SourcedPrefix when reachable, else 0.</summary>
            public int CompletablePrefix;
        }

        private sealed class DlgNode
        {
            public string Id = "";
            public string Next = "";
            public List<string> Gotos = new List<string>();
            public List<string> OptionConditions = new List<string>();
            public List<KeyValuePair<string, List<string>>> Commands = new List<KeyValuePair<string, List<string>>>();
            public List<string> Speakers = new List<string>();
        }

        private sealed class DlgRec
        {
            public string Id = "";
            public string StartNode = "";
            public List<DlgNode> Nodes = new List<DlgNode>();
        }

        private sealed class Ctx
        {
            public List<QuestRec> Quests = new List<QuestRec>();
            public HashSet<string> QuestIds = new HashSet<string>(StringComparer.Ordinal);
            public int TotalStages;
            public int Completable;

            public List<DlgRec> Dialogues = new List<DlgRec>();
            public HashSet<string> DialogueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SpeakerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Every "questId|flag" pair some SetQuestFlag/SetFlag verb authors.</summary>
            public HashSet<string> FlagSetters = new HashSet<string>(StringComparer.Ordinal);

            // Source scan of Assets/_Modules
            public HashSet<string> SourceLiterals = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> ExactSignals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<string> SignalPrefixes = new List<string>();
            public int RaiseSites;

            // Case 2 output, consumed by Case 3 / Case 6
            public HashSet<string> QuestNamedDialogues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, bool> QuestFullyCompletable = new Dictionary<string, bool>(StringComparer.Ordinal);

            // Referent vocabularies
            public HashSet<string> StructureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> PetSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> RegionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> PanelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ProseVocabulary = new HashSet<string>(StringComparer.Ordinal);
        }

        // =====================================================================
        //  CASE 0 - catalog shape
        // =====================================================================

        private static void Case0_CatalogShape(Ctx ctx, List<string> failures, List<string> notes)
        {
            string res = ReadText(QuestsRes, failures);
            string sa = ReadText(QuestsSA, failures);
            if (res == null) return;

            if (sa != null && !string.Equals(res, sa, StringComparison.Ordinal))
                failures.Add("[catalog-shape] the Resources and StreamingAssets copies of quests.json DIFFER. " +
                             "CanonicalJson reads Resources FIRST, so the shipped player never sees an edit made " +
                             "only in StreamingAssets - the fix is to write BOTH copies, always, in the same edit.");

            JObject root;
            try { root = JObject.Parse(res); }
            catch (Exception ex)
            {
                failures.Add("[catalog-shape] quests.json is not valid JSON (" + ex.Message +
                             ") - the whole quest catalog loads EMPTY at runtime, so no quest can be accepted at all");
                return;
            }

            var arr = root["quests"] as JArray;
            if (arr == null || arr.Count == 0)
            {
                failures.Add("[catalog-shape] quests.json has no 'quests' array - QuestCatalog would load empty " +
                             "and every rumor-board row would vanish");
                return;
            }

            var seenQuestIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var qt in arr)
            {
                var o = qt as JObject;
                if (o == null) continue;
                var q = new QuestRec
                {
                    Id = Str(o["id"]),
                    Title = Str(o["title"]),
                    RequiresQuestId = Str(o["requiresQuestId"]).Trim(),
                };
                if (string.IsNullOrEmpty(q.Id))
                {
                    failures.Add("[catalog-shape] a quest entry has NO id - QuestService keys every lookup on the " +
                                 "id, so an id-less quest can be rendered but never started, advanced or completed");
                    continue;
                }
                if (!seenQuestIds.Add(q.Id))
                {
                    failures.Add("[catalog-shape] duplicate quest id '" + q.Id + "'. QuestCatalog.FindQuest returns " +
                                 "the FIRST match, so the second definition's stages are unreachable content.");
                    continue;
                }

                var stages = o["stages"] as JArray;
                if (stages == null || stages.Count == 0)
                {
                    failures.Add("[catalog-shape] quest '" + q.Id + "' has NO stages. StartQuest seeds BeatIndex 0 " +
                                 "with a null stage id and the tracker has nothing to show - author at least one stage " +
                                 "or remove the quest.");
                    ctx.Quests.Add(q);
                    ctx.QuestIds.Add(q.Id);
                    continue;
                }

                var seenStageIds = new HashSet<string>(StringComparer.Ordinal);
                int idx = 0;
                foreach (var st in stages)
                {
                    var so = st as JObject;
                    if (so == null) { idx++; continue; }
                    var s = new StageRec
                    {
                        Index = idx,
                        StageId = Str(so["stageId"]),
                        ObjectiveText = Str(so["objectiveText"]),
                        RequiresFlag = Str(so["requiresFlag"]),
                        GrantsKeystone = Bool(so["grantsKeystone"]),
                    };
                    var rw = so["reward"] as JObject;
                    if (rw != null)
                    {
                        s.Crystals = Int(rw["crystals"]);
                        s.Food = Int(rw["food"]);
                        s.Magic = Int(rw["magic"]);
                        s.GrantItemId = Str(rw["grantItemId"]);
                    }
                    var on = so["completeOn"] as JObject;
                    if (on != null)
                    {
                        s.HasCompleteOn = true;
                        s.OnKind = Str(on["kind"]);
                        s.OnTarget = Str(on["targetId"]);
                        s.OnCount = on["count"] != null ? Math.Max(1, Int(on["count"])) : 1;
                    }

                    if (string.IsNullOrEmpty(s.StageId))
                        failures.Add("[catalog-shape] quest '" + q.Id + "' stage index " + idx + " has NO stageId. " +
                                     "The stage id is the per-index key this suite proves distinctness against AND " +
                                     "what QuestState.StageId persists - an empty one makes the save ambiguous.");
                    else if (!seenStageIds.Add(s.StageId))
                        failures.Add("[catalog-shape] quest '" + q.Id + "' repeats stageId '" + s.StageId + "'. " +
                                     "Two stages sharing one id cannot be told apart in the save or in this oracle's " +
                                     "distinctness proof - rename one.");

                    q.Stages.Add(s);
                    idx++;
                }

                ctx.Quests.Add(q);
                ctx.QuestIds.Add(q.Id);
                ctx.TotalStages += q.Stages.Count;
            }

            // The real load path must agree with the raw file. A DTO field that stops
            // mapping is exactly how a completion condition can be authored into
            // quests.json and silently ignored by the runtime that must honour it.
            try
            {
                DeNelle.Core.Quests.QuestCatalog.Reload();
                var live = DeNelle.Core.Quests.QuestCatalog.Quests;
                int liveQuests = live != null ? live.Count : 0;
                int liveStages = 0;
                if (live != null)
                    foreach (var q in live) if (q != null && q.Stages != null) liveStages += q.Stages.Count;

                if (liveQuests != ctx.Quests.Count || liveStages != ctx.TotalStages)
                    failures.Add("[catalog-shape] the raw quests.json holds " + ctx.Quests.Count + " quests / " +
                                 ctx.TotalStages + " stages but the REAL load path (QuestCatalog) sees " +
                                 liveQuests + " / " + liveStages + " - a DTO mapping has broken, so authored " +
                                 "content the game is supposed to honour is being dropped on load");
                else
                    notes.Add("QuestCatalog load path agrees with the raw file (" + liveQuests + " quests, " +
                              liveStages + " stages)");

                // Same reasoning, one field deeper. A prerequisite the DTO does not map loads as
                // null, so the board offers every act at once while the file claims an order --
                // the gate would be authored, shipped and inert, which is the exact failure mode
                // the count cross-check above exists to catch.
                foreach (var q in ctx.Quests)
                {
                    if (string.IsNullOrEmpty(q.RequiresQuestId)) continue;
                    string mapped = ReadRequiresQuestId(DeNelle.Core.Quests.QuestCatalog.FindQuest(q.Id));
                    if (string.Equals((mapped ?? "").Trim(), q.RequiresQuestId, StringComparison.Ordinal)) continue;
                    failures.Add("[catalog-shape] quest '" + q.Id + "' authors requiresQuestId '" +
                                 q.RequiresQuestId + "' but the REAL load path reads '" +
                                 (mapped == null ? "<no such DTO field>" : mapped) + "' - QuestDef is not " +
                                 "carrying the prerequisite, so the gate is inert data and every act is " +
                                 "startable at once no matter what the file says.");
                }
            }
            catch (Exception ex)
            {
                failures.Add("[catalog-shape] QuestCatalog.Reload threw " + ex.GetType().Name + ": " + ex.Message +
                             " - the runtime cannot load the quest catalog at all");
            }
        }

        // =====================================================================
        //  Support data - dialogues, module source scan, referent vocabularies
        // =====================================================================

        private static void LoadSupportData(Ctx ctx, List<string> failures, List<string> notes)
        {
            LoadDialogues(ctx, failures);
            CollectFlagSetters(ctx);
            ScanModuleSources(ctx, failures);
            LoadReferentVocabularies(ctx, failures, notes);
        }

        /// <summary>Every (questId, flag) pair a dialogue verb sets. Both Case 2's flag-kind
        /// completeOn and Case 8's requiresFlag gate resolve against this one set, so the two
        /// can never disagree about what "a flag has a setter" means.</summary>
        private static void CollectFlagSetters(Ctx ctx)
        {
            foreach (var d in ctx.Dialogues)
                foreach (var n in d.Nodes)
                    foreach (var c in n.Commands)
                    {
                        if (!string.Equals(c.Key, "SetQuestFlag", StringComparison.Ordinal) &&
                            !string.Equals(c.Key, "SetFlag", StringComparison.Ordinal)) continue;
                        if (c.Value.Count < 2) continue;
                        ctx.FlagSetters.Add(c.Value[0] + "|" + c.Value[1]);
                    }
        }

        private static void LoadDialogues(Ctx ctx, List<string> failures)
        {
            string res = ReadText(DialoguesRes, failures);
            string sa = ReadText(DialoguesSA, failures);
            if (res == null) return;

            if (sa != null && !string.Equals(res, sa, StringComparison.Ordinal))
                failures.Add("[catalog-shape] the Resources and StreamingAssets copies of dialogues.json DIFFER - " +
                             "Resources wins at load, so a quest beat authored only in StreamingAssets never plays " +
                             "on device. Write both copies in the same edit.");

            JObject root;
            try { root = JObject.Parse(res); }
            catch (Exception ex)
            {
                failures.Add("[catalog-shape] dialogues.json is not valid JSON (" + ex.Message +
                             ") - every conversation, including every quest beat, fails to load");
                return;
            }

            var speakers = root["speakers"] as JArray;
            if (speakers != null)
                foreach (var s in speakers)
                {
                    string n = Str(Get(s, "name"));
                    if (!string.IsNullOrEmpty(n)) ctx.SpeakerNames.Add(n);
                }

            var dlgs = root["dialogues"] as JArray;
            if (dlgs == null) return;
            foreach (var dt in dlgs)
            {
                var o = dt as JObject;
                if (o == null) continue;
                var d = new DlgRec { Id = Str(o["id"]), StartNode = Str(o["startNode"]) };
                if (string.IsNullOrEmpty(d.Id)) continue;
                var nodes = o["nodes"] as JArray;
                if (nodes != null)
                    foreach (var nt in nodes)
                    {
                        var no = nt as JObject;
                        if (no == null) continue;
                        var n = new DlgNode { Id = Str(no["id"]), Next = Str(no["next"]) };
                        var opts = no["options"] as JArray;
                        if (opts != null)
                            foreach (var ot in opts)
                            {
                                string g = Str(Get(ot, "goto"));
                                if (!string.IsNullOrEmpty(g)) n.Gotos.Add(g);
                                string rq = Str(Get(ot, "requires"));
                                if (!string.IsNullOrEmpty(rq)) n.OptionConditions.Add(rq);
                            }
                        var cmds = no["commands"] as JArray;
                        if (cmds != null)
                            foreach (var ct in cmds)
                            {
                                string verb = Str(Get(ct, "verb"));
                                var argList = new List<string>();
                                var args = Get(ct, "args") as JArray;
                                if (args != null) foreach (var a in args) argList.Add(Str(a));
                                if (!string.IsNullOrEmpty(verb))
                                    n.Commands.Add(new KeyValuePair<string, List<string>>(verb, argList));
                            }
                        var lines = no["lines"] as JArray;
                        if (lines != null)
                            foreach (var lt in lines)
                            {
                                string sp = Str(Get(lt, "speaker"));
                                if (!string.IsNullOrEmpty(sp)) n.Speakers.Add(sp);
                            }
                        d.Nodes.Add(n);
                    }
                ctx.Dialogues.Add(d);
                ctx.DialogueIds.Add(d.Id);
            }
        }

        /// <summary>One pass over Assets/_Modules that collects (a) every string literal in
        /// live code -- which is how a dialogue id is proven OPENABLE, since every open route
        /// (DialogueService.Play / PlayStructure / an injector's dialogue field) names the id
        /// as a literal -- and (b) every TutorialSignals.Raise argument, resolved into an exact
        /// signal id or a dynamic Prefix+expr family, which is how a completeOn signal is
        /// proven to have a LIVE emitter.</summary>
        private static void ScanModuleSources(Ctx ctx, List<string> failures)
        {
            string[] files;
            try { files = Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                failures.Add("[advance-live] could not enumerate " + ModulesRoot + ": " + ex.Message +
                             " - without the source scan no dialogue can be proven openable and no signal " +
                             "can be proven emitted, so no stage can be honestly scored");
                return;
            }

            var consts = SignalConstants();
            var constToken = new Regex(@"TutorialSignals\s*\.\s*([A-Za-z_]\w*)");
            var literal = new Regex("\"([^\"\\\\]*)\"");

            foreach (var file in files)
            {
                string src;
                try { src = File.ReadAllText(file); }
                catch { continue; }
                string code = StripComments(src);

                foreach (Match m in literal.Matches(code))
                {
                    string v = m.Groups[1].Value;
                    if (v.Length > 0 && v.Length < 128) ctx.SourceLiterals.Add(v);
                }

                if (code.IndexOf("TutorialSignals", StringComparison.Ordinal) < 0) continue;
                int at = 0;
                while (true)
                {
                    int idx = code.IndexOf("TutorialSignals.Raise", at, StringComparison.Ordinal);
                    if (idx < 0) break;
                    int open = code.IndexOf('(', idx);
                    if (open < 0) break;
                    string arg = ExtractBalanced(code, open);
                    at = open + 1;
                    if (arg == null) continue;
                    ctx.RaiseSites++;

                    bool isConcat = arg.IndexOf('+') >= 0;
                    foreach (Match m in constToken.Matches(arg))
                    {
                        string name = m.Groups[1].Value;
                        if (name == "Raise") continue;
                        string val;
                        if (!consts.TryGetValue(name, out val) || string.IsNullOrEmpty(val)) continue;
                        if (isConcat)
                        {
                            if (val.EndsWith(":", StringComparison.Ordinal) && !ctx.SignalPrefixes.Contains(val))
                                ctx.SignalPrefixes.Add(val);
                        }
                        else ctx.ExactSignals.Add(val);
                    }
                    if (!isConcat)
                        foreach (Match m in literal.Matches(arg))
                            if (!string.IsNullOrEmpty(m.Groups[1].Value)) ctx.ExactSignals.Add(m.Groups[1].Value);
                }
            }

            // hero.reached:<anchor> is raised by TutorialFlow's own proximity probe with a
            // variable argument no source scan can resolve. Accept the family ONLY while the
            // probe is really there.
            string flow = File.Exists(TutorialFlowSrc) ? File.ReadAllText(TutorialFlowSrc) : null;
            if (flow != null && StripComments(flow).Contains("TickProximityProbe") &&
                !ctx.SignalPrefixes.Contains("hero.reached:"))
                ctx.SignalPrefixes.Add("hero.reached:");

            if (ctx.RaiseSites == 0)
                failures.Add("[advance-live] found ZERO TutorialSignals.Raise call sites under " + ModulesRoot +
                             " - either the scan root moved or the signal bus lost every emitter. Any completeOn " +
                             "riding that bus is dead, and this suite cannot tell which.");
        }

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

        private static void LoadReferentVocabularies(Ctx ctx, List<string> failures, List<string> notes)
        {
            CollectIds(ctx.StructureIds, StructuresRes, "entries", "id");
            CollectIds(ctx.StructureIds, StructuresRes, "entries", "displayName");
            CollectIds(ctx.PetSpecies, PetsRes, "pets", "species");
            CollectIds(ctx.PetSpecies, PetsRes, "pets", "id");
            CollectIds(ctx.RegionIds, RealmMapRes, "regions", "id");
            foreach (var p in new[] { WeaponsRes, ArmorRes, AccessoriesRes, ConsumablesRes, MaterialsRes })
            {
                CollectIds(ctx.ItemIds, p, "weapons", "id");
                CollectIds(ctx.ItemIds, p, "armor", "id");
                CollectIds(ctx.ItemIds, p, "armors", "id");
                CollectIds(ctx.ItemIds, p, "accessories", "id");
                CollectIds(ctx.ItemIds, p, "consumables", "id");
                CollectIds(ctx.ItemIds, p, "materials", "id");
                CollectIds(ctx.ItemIds, p, "entries", "id");
                CollectIds(ctx.ItemIds, p, "items", "id");
            }
            if (ctx.ItemIds.Count == 0)
                failures.Add("[reward-payable] no item catalog resolved any ids (weapons/armor/accessories/" +
                             "consumables/materials) - grantItemId cannot be checked, so a reward that can never " +
                             "be paid would pass unseen. Check the catalog paths.");

            try
            {
                foreach (var n in Enum.GetNames(typeof(DeNelle.Core.UI.PanelId))) ctx.PanelIds.Add(n);
            }
            catch (Exception ex)
            {
                notes.Add("PanelId enum unreadable (" + ex.GetType().Name + ") - completeOn kind 'panel' targets " +
                          "are unverified");
            }

            // Prose vocabulary: everything the shipped data calls something, normalized.
            AddProse(ctx, ctx.StructureIds);
            AddProse(ctx, ctx.PetSpecies);
            AddProse(ctx, ctx.RegionIds);
            AddProse(ctx, ctx.ItemIds);
            AddProse(ctx, ctx.SpeakerNames);
            AddProse(ctx, ctx.DialogueIds);
            foreach (var q in ctx.Quests) { ctx.ProseVocabulary.Add(Norm(q.Id)); ctx.ProseVocabulary.Add(Norm(q.Title)); }
            CollectDisplayNames(ctx, StructuresRes);
            CollectDisplayNames(ctx, EnemiesRes);
            CollectDisplayNames(ctx, PetsRes);
            CollectDisplayNames(ctx, RealmMapRes);
            CollectDisplayNames(ctx, WeaponsRes);
            CollectDisplayNames(ctx, ArmorRes);
            CollectDisplayNames(ctx, AccessoriesRes);
            CollectDisplayNames(ctx, ConsumablesRes);
            CollectDisplayNames(ctx, MaterialsRes);
            CollectAllStrings(ctx, GlossaryRes);
            CollectAllStrings(ctx, CanonStringsRes);
        }

        private static void CollectIds(HashSet<string> into, string path, string arrayKey, string field)
        {
            if (!File.Exists(path)) return;
            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var arr = root[arrayKey] as JArray;
                if (arr == null) return;
                foreach (var e in arr)
                {
                    string v = Str(Get(e, field));
                    if (!string.IsNullOrEmpty(v)) into.Add(v);
                }
            }
            catch { }
        }

        /// <summary>Adds every displayName/name/title/species/id string anywhere in a catalog to
        /// the prose vocabulary. Whole names only - indexing individual WORDS would let
        /// "Stone Wall" satisfy "Stone Mountains" and hide exactly the gaps this ledger exists
        /// to surface.</summary>
        private static void CollectDisplayNames(Ctx ctx, string path)
        {
            var root = ParseContainer(path);
            if (root == null) return;
            foreach (var t in root.DescendantsAndSelf())
            {
                var p = t as JProperty;
                if (p == null || p.Value.Type != JTokenType.String) continue;
                string key = p.Name;
                if (key != "displayName" && key != "name" && key != "title" && key != "species" && key != "id")
                    continue;
                ctx.ProseVocabulary.Add(Norm((string)p.Value));
            }
        }

        private static void CollectAllStrings(Ctx ctx, string path)
        {
            var root = ParseContainer(path);
            if (root == null) return;
            foreach (var t in root.DescendantsAndSelf())
            {
                if (t.Type != JTokenType.String) continue;
                string v = (string)t;
                if (string.IsNullOrEmpty(v) || v.Length > 64) continue;
                ctx.ProseVocabulary.Add(Norm(v));
            }
        }

        private static JContainer ParseContainer(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JToken.Parse(File.ReadAllText(path)) as JContainer; }
            catch { return null; }
        }

        private static void AddProse(Ctx ctx, HashSet<string> src)
        {
            foreach (var s in src) ctx.ProseVocabulary.Add(Norm(s));
        }

        // =====================================================================
        //  CASE 1 - every quest is enterable
        // =====================================================================

        private static void Case1_EntryLive(Ctx ctx, List<string> failures, List<string> notes)
        {
            string board = ReadText(RumorBoardSrc, failures);
            bool boardOffers = false;
            bool boardHonoursPrereq = false;

            if (board != null)
            {
                string code = StripComments(board);
                bool enumeratesCatalog = Regex.IsMatch(code, @"foreach\s*\(\s*var\s+\w+\s+in\s+catalog\s*\)");
                bool addsAvailable = code.IndexOf("_available.Add(", StringComparison.Ordinal) >= 0;
                bool acceptStarts = Regex.IsMatch(code, @"_backend\s*\.\s*StartQuest\s*\(");
                boardHonoursPrereq = code.IndexOf("RequiresQuestId", StringComparison.Ordinal) >= 0;

                boardOffers = enumeratesCatalog && addsAvailable && acceptStarts;
                if (boardOffers)
                    notes.Add("entry route: RumorBoardVM.Rebuild walks the catalog and Accept calls StartQuest, " +
                              "so every quest whose prerequisite is satisfied is offered (prereq-gate honoured=" +
                              boardHonoursPrereq + ")");
                else
                    notes.Add("RumorBoardVM no longer offers the catalog (enumerates=" + enumeratesCatalog +
                              " adds=" + addsAvailable + " accept-starts=" + acceptStarts +
                              ") - falling back to per-quest StartQuest authorship");
            }

            // The data promising an order the board does not enforce is the defect this case now
            // pins: without the gate, the TERMINAL act is startable on a fresh save and whatever
            // it unlocks stops being a reward.
            int authoredGates = 0;
            foreach (var q in ctx.Quests) if (!string.IsNullOrEmpty(q.RequiresQuestId)) authoredGates++;
            if (authoredGates > 0 && board != null && !boardHonoursPrereq)
                failures.Add("[entry-live] quests.json authors " + authoredGates + " requiresQuestId " +
                             "prerequisite(s) but " + RumorBoardSrc + " never reads RequiresQuestId - the board " +
                             "offers every act at once, so the ordering the data promises is fiction and the last " +
                             "act's rewards are a first-session freebie. Gate the Available list on it.");
            if (authoredGates == 0)
                notes.Add("no quest carries a requiresQuestId, so the prerequisite chain is unexercised - the " +
                          "walk below is armed for the moment one is authored");

            bool boardOpenable = ctx.SourceLiterals.Contains("OpenRumorBoard")
                              || FileContains(ModulesRoot, "PanelId.RumorBoard");
            if (!boardOpenable)
            {
                failures.Add("[entry-live] nothing under " + ModulesRoot + " opens PanelId.RumorBoard - the board " +
                             "that is the ONLY entry to all " + ctx.Quests.Count + " quests cannot be " +
                             "reached, so no quest can be accepted. Restore an opener (HUD button or the " +
                             "OpenRumorBoard dialogue verb).");
                boardOffers = false;
            }

            foreach (var q in ctx.Quests)
            {
                if (boardOffers) { q.EntryUngated = true; continue; }
                bool authored = DialogueAuthorsVerb(ctx, "StartQuest", q.Id) || ctx.SourceLiterals.Contains(q.Id);
                q.EntryUngated = authored;
                if (!authored)
                    failures.Add("[entry-live] quest '" + q.Id + "' has NO way in: the rumor board no longer " +
                                 "offers the catalog and no dialogue or source authors StartQuest for it. " +
                                 "A quest nothing can start is dead content - either restore the board's " +
                                 "render or author a StartQuest for this id.");
            }
            // Layer (b) -- the prerequisite walk -- runs at the END of Case 2, because it asks
            // whether a prerequisite is fully COMPLETABLE and only Case 2 knows that.
        }

        /// <summary>
        /// Case 1 layer (b). Turns SOURCING (Case 2's per-quest SourcedPrefix) plus OFFERING
        /// (layer (a)'s EntryUngated) into the final score, by walking each quest's
        /// requiresQuestId chain: a quest is reachable when it is offered AND either has no
        /// prerequisite, or its prerequisite is reachable and every one of its stages is
        /// completable. A quest that is not reachable contributes ZERO -- counting its stages
        /// would be the oracle asserting a reward is earnable that the gate makes unreachable.
        ///
        /// CYCLE SAFETY: the walk marks a quest ON-STACK while resolving it, so re-entering one
        /// is detected as a cycle, reported once, and collapsed to unreachable - it never
        /// recurses forever. Memoized (each quest resolves once), so the walk is linear.
        /// </summary>
        private static void ResolveEntryChain(Ctx ctx, List<string> failures, List<string> ledger)
        {
            var byId = new Dictionary<string, QuestRec>(StringComparer.Ordinal);
            foreach (var q in ctx.Quests) if (!string.IsNullOrEmpty(q.Id)) byId[q.Id] = q;

            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var q in ctx.Quests) ResolveReachable(q, byId, state, failures);

            foreach (var q in ctx.Quests)
            {
                q.CompletablePrefix = q.EntryLive ? q.SourcedPrefix : 0;
                ctx.Completable += q.CompletablePrefix;
                ctx.QuestFullyCompletable[q.Id] =
                    q.EntryLive && q.Stages.Count > 0 && q.SourcedPrefix == q.Stages.Count;

                if (q.EntryLive || q.Stages.Count == 0) continue;
                if (!q.EntryUngated)
                    ledger.Add(q.Id + " contributes 0 of " + q.Stages.Count +
                               " stages: the quest is not enterable");
                else
                    ledger.Add(q.Id + " contributes 0 of " + q.Stages.Count + " stages (" + q.SourcedPrefix +
                               " of them ARE sourced): it is gated behind prerequisite quest '" +
                               q.RequiresQuestId + "', which is not itself reachable and fully completable - " +
                               "finish that chain and these stages start counting");
            }
        }

        private const int WalkUnvisited = 0, WalkOnStack = 1, WalkResolved = 2;

        private static bool ResolveReachable(QuestRec q, Dictionary<string, QuestRec> byId,
                                             Dictionary<string, int> state, List<string> failures)
        {
            int st;
            if (state.TryGetValue(q.Id, out st))
            {
                if (st != WalkOnStack) return q.EntryLive;
                failures.Add("[entry-live] prerequisite CYCLE through quest '" + q.Id + "' - following " +
                             "requiresQuestId leads back to it, so every quest in the loop waits on one that " +
                             "waits on it and NONE of them can ever be offered. Break the cycle.");
                q.EntryLive = false;
                state[q.Id] = WalkResolved;
                return false;
            }
            state[q.Id] = WalkOnStack;

            bool live = q.EntryUngated;
            string prereq = q.RequiresQuestId ?? "";
            if (live && prereq.Length > 0)
            {
                QuestRec p;
                if (string.Equals(prereq, q.Id, StringComparison.Ordinal))
                {
                    failures.Add("[entry-live] quest '" + q.Id + "' names ITSELF as its requiresQuestId - it can " +
                                 "never be offered, because it can only be offered once it is completed.");
                    live = false;
                }
                else if (!byId.TryGetValue(prereq, out p))
                {
                    failures.Add("[entry-live] quest '" + q.Id + "' requires quest '" + prereq + "' which is NOT " +
                                 "in quests.json - QuestService.IsCompleted answers false forever, so the gate " +
                                 "never opens and the quest is dead content. Fix the id or drop the gate.");
                    live = false;
                }
                else
                {
                    bool prereqReachable = ResolveReachable(p, byId, state, failures);
                    live = prereqReachable && p.Stages.Count > 0 && p.SourcedPrefix == p.Stages.Count;
                }
            }

            q.EntryLive = live;
            state[q.Id] = WalkResolved;
            return live;
        }

        // =====================================================================
        //  CASE 2 - THE SPINE: a DISTINCT completion source per stage index
        // =====================================================================

        private static void Case2_AdvanceLive(Ctx ctx, List<string> failures, List<string> notes, List<string> ledger)
        {
            LintLatchClear(ctx, failures, notes);
            LintEmitterBootstrap(ctx, failures, notes);
            LintEmitterTableFreshness(ctx, notes);

            foreach (var q in ctx.Quests)
            {
                // The legacy pool: DISTINCT reachable AdvanceQuest nodes, in openable
                // dialogues, naming this quest. Each backs exactly ONE stage index -- that
                // is the whole answer to trap (a): AdvanceQuest is ordinal, so re-opening
                // one node four times is ONE source, not four.
                var pool = AdvanceNodesFor(ctx, q.Id, failures, notes);
                int poolCursor = 0;

                // SOURCING ONLY. Reachability (is this quest offered, is its prerequisite
                // finishable) is Case 1's job and is multiplied in by ResolveEntryChain below,
                // so an unreachable quest reports one honest "gated" ledger line instead of a
                // per-stage "blocked ordinally" line that names the wrong cause.
                var usedKeys = new Dictionary<string, int>(StringComparer.Ordinal);
                bool prefixOpen = true;
                int prefix = 0;

                for (int i = 0; i < q.Stages.Count; i++)
                {
                    var s = q.Stages[i];
                    string key = null;
                    string problem = null;

                    if (s.HasCompleteOn && !string.Equals(s.OnKind, "dialogueCommand", StringComparison.OrdinalIgnoreCase))
                    {
                        key = "completeOn:" + s.OnKind.ToLowerInvariant() + "|" +
                              (s.OnTarget ?? "").ToLowerInvariant() + "|" + s.OnCount;
                        string signal = ComposeSignal(s, out bool known);
                        string runtimeSignal = RuntimeComposedSignal(s, out bool runtimeAvailable);
                        if (runtimeAvailable && !string.Equals(signal, runtimeSignal, StringComparison.Ordinal))
                        {
                            failures.Add("[advance-live] GRAMMAR DRIFT on '" + q.Id + "' stage '" + s.StageId +
                                         "': this oracle composes signal '" + (signal ?? "<none>") +
                                         "' but the runtime's QuestCompletion.ToSignalId composes '" +
                                         (runtimeSignal ?? "<none>") + "'. The oracle would judge a signal the " +
                                         "game never awaits, which makes every verdict it gives about this kind " +
                                         "confidently wrong. Bring ComposeSignal back in line with the runtime - " +
                                         "the runtime is the authority.");
                            signal = runtimeSignal;
                        }
                        else if (runtimeAvailable) signal = runtimeSignal;

                        bool isFlagKind = string.Equals(s.OnKind, "flag", StringComparison.OrdinalIgnoreCase);
                        if (!known)
                            problem = "completeOn kind '" + s.OnKind + "' is not in the v1 kind vocabulary " +
                                      "(talk/wave/build/panel/arena/reach/pet/upgrade/population/region/flag/" +
                                      "dialogueCommand) - nothing composes a signal for it, so nothing can ever " +
                                      "satisfy the stage";
                        else if (isFlagKind)
                        {
                            // A flag condition is polled through QuestService.HasFlag, so what
                            // must exist is a SetQuestFlag author for THIS quest and flag.
                            if (string.IsNullOrEmpty(s.OnTarget))
                                problem = "completeOn kind 'flag' names no targetId, so there is no flag to wait on";
                            else if (!ctx.FlagSetters.Contains(q.Id + "|" + s.OnTarget))
                                problem = "completeOn awaits quest flag '" + s.OnTarget + "' but nothing authors " +
                                          "SetQuestFlag for quest '" + q.Id + "' and that flag - the condition can " +
                                          "never become true. Author the SetQuestFlag on the beat that earns it.";
                        }
                        else if (signal == null)
                            problem = "completeOn kind '" + s.OnKind + "' composes NO signal (its targetId is " +
                                      "empty), so the stage waits on nothing and can never complete. Give it the " +
                                      "id of the thing the objective actually names.";
                        else if (!SignalHasEmitter(ctx, signal))
                            problem = "completeOn composes signal '" + signal + "' but NOTHING under " + ModulesRoot +
                                      " raises it (" + ctx.RaiseSites + " Raise call sites scanned). Add the single " +
                                      "TutorialSignals.Raise in the system that already owns that event - a stage " +
                                      "awaiting a signal no code emits is a beat that can never end.";
                        if (string.Equals(s.OnKind, "talk", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(s.OnTarget))
                            ctx.QuestNamedDialogues.Add(s.OnTarget);
                    }
                    else if (poolCursor < pool.Count)
                    {
                        key = pool[poolCursor];
                        string dlgId = key.Substring("dialogue:".Length);
                        int hash = dlgId.IndexOf('#');
                        if (hash > 0) ctx.QuestNamedDialogues.Add(dlgId.Substring(0, hash));
                        poolCursor++;
                    }

                    if (key == null)
                    {
                        // NOT a failure -- this is the "not yet authored" ledger the ratchet
                        // measures. Failing here would scream once per unauthored stage and
                        // make the floor useless.
                        ledger.Add(q.Id + "#" + i + " '" + s.StageId + "' has no completion source (author a " +
                                   "completeOn, or an AdvanceQuest node in an openable dialogue)");
                        prefixOpen = false;
                        continue;
                    }

                    bool broken = false;
                    int firstOwner;
                    if (usedKeys.TryGetValue(key, out firstOwner))
                    {
                        failures.Add("[advance-live] quest '" + q.Id + "' stages " + firstOwner + " and " + i +
                                     " share ONE completion source (" + key + "). AdvanceQuest is ORDINAL " +
                                     "(QuestService.cs:119-146) - it advances whatever stage is current and takes " +
                                     "no stage id - so a single act would silently clear both and the player would " +
                                     "be credited for a beat they never played. Give each stage its own source.");
                        broken = true;
                    }
                    else usedKeys[key] = i;

                    if (problem != null)
                    {
                        failures.Add("[advance-live] quest '" + q.Id + "' stage " + i + " '" + s.StageId + "': " +
                                     problem);
                        broken = true;
                    }
                    if (broken) { prefixOpen = false; continue; }

                    if (prefixOpen) prefix++;
                    else
                        ledger.Add(q.Id + "#" + i + " '" + s.StageId + "' HAS a source but is BLOCKED ORDINALLY " +
                                   "behind an earlier stage that has none - it can never become current");
                }

                q.SourcedPrefix = prefix;

                if (poolCursor < pool.Count)
                    notes.Add("quest '" + q.Id + "' has " + (pool.Count - poolCursor) + " more AdvanceQuest " +
                              "node(s) than it has stages - surplus advance authors are a no-op once the quest " +
                              "completes");
            }

            // Every quest is now sourced, so the prerequisite chain can be resolved and the score
            // taken. This is Case 1 layer (b) and it MUST run last: it asks whether a prerequisite
            // is fully completable, which is only knowable after the loop above.
            ResolveEntryChain(ctx, failures, ledger);
        }

        /// <summary>Distinct, reachable, openable dialogue nodes that author AdvanceQuest for a
        /// quest, in a stable order. Reachability is walked from the entry node through next +
        /// option gotos; an option's `requires` condition is treated as passable (statically
        /// evaluating game conditions is out of scope) and reported as a note.</summary>
        private static List<string> AdvanceNodesFor(Ctx ctx, string questId, List<string> failures, List<string> notes)
        {
            var result = new List<string>();
            foreach (var d in ctx.Dialogues)
            {
                bool authorsAdvance = false;
                foreach (var n in d.Nodes)
                    foreach (var c in n.Commands)
                        if (string.Equals(c.Key, "AdvanceQuest", StringComparison.Ordinal) &&
                            c.Value.Count > 0 && string.Equals(c.Value[0], questId, StringComparison.Ordinal))
                            authorsAdvance = true;
                if (!authorsAdvance) continue;

                bool openable = ctx.SourceLiterals.Contains(d.Id);
                if (!openable)
                {
                    failures.Add("[advance-live] dialogue '" + d.Id + "' authors AdvanceQuest for '" + questId +
                                 "' but its id appears in NO live source under " + ModulesRoot + " - nothing calls " +
                                 "DialogueService.Play/PlayStructure with it and no injector names it, so the " +
                                 "player can never open the conversation that advances the quest. Give it a talker " +
                                 "or a trigger.");
                    continue;
                }

                var reachable = ReachableNodes(d, notes);
                foreach (var n in d.Nodes)
                {
                    bool hit = false;
                    foreach (var c in n.Commands)
                        if (string.Equals(c.Key, "AdvanceQuest", StringComparison.Ordinal) &&
                            c.Value.Count > 0 && string.Equals(c.Value[0], questId, StringComparison.Ordinal))
                            hit = true;
                    if (!hit) continue;

                    if (!reachable.Contains(n.Id))
                    {
                        failures.Add("[advance-live] dialogue '" + d.Id + "' node '" + n.Id + "' authors " +
                                     "AdvanceQuest for '" + questId + "' but is UNREACHABLE from the entry node " +
                                     "(no next/goto path leads to it) - the verb is written and can never fire. " +
                                     "Wire the node into the graph or delete it.");
                        continue;
                    }
                    string key = "dialogue:" + d.Id + "#" + n.Id;
                    if (!result.Contains(key)) result.Add(key);
                }
            }
            return result;
        }

        private static HashSet<string> ReachableNodes(DlgRec d, List<string> notes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var byId = new Dictionary<string, DlgNode>(StringComparer.Ordinal);
            foreach (var n in d.Nodes) if (!string.IsNullOrEmpty(n.Id)) byId[n.Id] = n;

            string entry = !string.IsNullOrEmpty(d.StartNode) && byId.ContainsKey(d.StartNode)
                ? d.StartNode
                : (d.Nodes.Count > 0 ? d.Nodes[0].Id : null);
            if (string.IsNullOrEmpty(entry)) return seen;

            var stack = new Stack<string>();
            stack.Push(entry);
            while (stack.Count > 0)
            {
                string id = stack.Pop();
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                DlgNode n;
                if (!byId.TryGetValue(id, out n)) continue;
                if (!string.IsNullOrEmpty(n.Next) && !string.Equals(n.Next, "end", StringComparison.OrdinalIgnoreCase))
                    stack.Push(n.Next);
                foreach (var g in n.Gotos)
                    if (!string.Equals(g, "end", StringComparison.OrdinalIgnoreCase)) stack.Push(g);
                foreach (var c in n.OptionConditions)
                    notes.Add("dialogue '" + d.Id + "' node '" + n.Id + "' gates an option on condition '" + c +
                              "' - treated as passable; if that condition can never be true the beat behind it " +
                              "is unreachable in fact");
            }
            return seen;
        }

        /// <summary>TRAP (b). TutorialSignals LATCHES, so a stage whose completeOn names an id
        /// that already fired completes the instant the quest is accepted. The bridge must
        /// Clear the awaited id when a stage becomes current. Armed the moment the bridge
        /// lands -- until then it is a note, not a fabricated pass.</summary>
        private static void LintLatchClear(Ctx ctx, List<string> failures, List<string> notes)
        {
            if (!File.Exists(SignalBridgeSrc))
            {
                bool anyCompleteOn = false;
                foreach (var q in ctx.Quests)
                    foreach (var s in q.Stages) if (s.HasCompleteOn) anyCompleteOn = true;

                if (anyCompleteOn)
                    failures.Add("[advance-live] stages carry completeOn but " + SignalBridgeSrc + " does not " +
                                 "exist - nothing listens to TutorialSignals.Raised on behalf of a story quest, " +
                                 "so every authored completion condition is inert data");
                else
                    notes.Add("StoryQuestSignalBridge is not built yet (WO-854 Phase 2); the latch-clear lint is " +
                              "armed and fires the moment the file lands");
                return;
            }

            string code = StripComments(File.ReadAllText(SignalBridgeSrc));
            if (code.IndexOf("TutorialSignals.Raised", StringComparison.Ordinal) < 0)
                failures.Add("[advance-live] " + SignalBridgeSrc + " does not subscribe TutorialSignals.Raised - " +
                             "no completeOn can ever be observed, so every stage that carries one is inert");
            if (!Regex.IsMatch(code, @"TutorialSignals\s*\.\s*Clear\s*\("))
                failures.Add("[advance-live] " + SignalBridgeSrc + " never calls TutorialSignals.Clear - " +
                             "the bus LATCHES (TutorialSignals.cs:55-56, 77-78), so a stage awaiting an id that " +
                             "already fired earlier in the session completes the INSTANT the quest is accepted and " +
                             "the player is paid for a beat they never played. Clear the awaited id when a stage " +
                             "becomes current, exactly as TutorialFlow does when it arms an await.");
        }

        /// <summary>The Village-side signal emitters (wave.cleared, build.structure_placed:*,
        /// arena.resolved:*) are installed ONLY by TutorialFlow's flag-gated bootstrap, so a
        /// quest riding that bus silently inherits a TUTORIAL feature flag. Fails once a
        /// completeOn actually depends on one of them.</summary>
        private static void LintEmitterBootstrap(Ctx ctx, List<string> failures, List<string> notes)
        {
            if (!File.Exists(SignalAdaptersSrc))
            {
                notes.Add(SignalAdaptersSrc + " not found - the Village-side emitter bootstrap is unverified");
                return;
            }
            string code = StripComments(File.ReadAllText(SignalAdaptersSrc));
            if (code.IndexOf("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal) >= 0) return;

            bool ridesBus = false;
            foreach (var q in ctx.Quests)
                foreach (var s in q.Stages)
                    if (s.HasCompleteOn && IsVillageBusKind(s.OnKind)) ridesBus = true;

            string msg = "TutorialSignalAdapters has no RuntimeInitializeOnLoadMethod of its own - it is added " +
                         "only by TutorialFlow's bootstrap, which is gated on the tutorial feature flag and on a " +
                         "non-enemy-owned hub. Every wave/build/arena signal therefore exists only while the " +
                         "TUTORIAL is armed";
            if (ridesBus)
                failures.Add("[advance-live] " + msg + ", and stages now depend on those signals - hoist the " +
                             "adapters into their own bootstrap or those stages complete only for players still " +
                             "inside the FTUE");
            else
                notes.Add(msg + "; no stage depends on them yet, so this is armed rather than live");
        }

        /// <summary>The runtime keeps a hand-maintained table (QuestCompletion.IsEmitterLive) that
        /// decides whether the bridge logs "armed" or "armed but unreachable". This compares that
        /// table against the SOURCE SCAN, which is the only thing that actually knows. A stale row
        /// does not break a beat, so it is a note - but a log that confidently says the wrong
        /// thing is how the next debugging session starts from a lie.</summary>
        private static void LintEmitterTableFreshness(Ctx ctx, List<string> notes)
        {
            ResolveRuntimeComposer();
            if (_completionType == null) return;
            MethodInfo isLive = _completionType.GetMethod("IsEmitterLive",
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (isLive == null) return;

            string[] kinds = { "talk", "wave", "build", "panel", "arena", "reach",
                               "pet", "upgrade", "population", "region" };
            foreach (var kind in kinds)
            {
                var probe = new StageRec { OnKind = kind, OnTarget = "probe", OnCount = 1 };
                bool dummy;
                string signal = RuntimeComposedSignal(probe, out dummy);
                if (string.IsNullOrEmpty(signal)) signal = ComposeSignal(probe, out dummy);
                if (string.IsNullOrEmpty(signal)) continue;

                bool scanned = SignalHasEmitter(ctx, signal);
                bool declared;
                try { declared = (bool)isLive.Invoke(null, new object[] { kind }); }
                catch { continue; }

                if (declared != scanned)
                    notes.Add("emitter table drift: QuestCompletion.IsEmitterLive('" + kind + "') says " +
                              declared + " but the source scan of " + ModulesRoot + " says " + scanned +
                              " for '" + signal + "' - update the table in the same edit that adds or removes " +
                              "the Raise, or the bridge's arm log states the opposite of the truth");
            }
        }

        private static bool IsVillageBusKind(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return false;
            switch (kind.ToLowerInvariant())
            {
                case "wave":
                case "build":
                case "arena":
                case "upgrade": return true;
                default: return false;
            }
        }

        /// <summary>Composes the bus signal a completeOn awaits (WO-854 sec.4 kind vocabulary).
        /// Returns null for kinds satisfied off the bus (flag is polled through
        /// QuestService.HasFlag; dialogueCommand is the dialogue calling AdvanceQuest itself);
        /// `known` is false for a kind outside the vocabulary entirely.
        ///
        /// This table is a MIRROR of the runtime's QuestCompletion.ToSignalId. It exists so the
        /// oracle still compiles and reports honestly if the schema silo's type is absent or
        /// reverted -- and RuntimeComposedSignal below cross-checks the two on every stage, so
        /// the mirror can never silently drift into modelling a grammar the game does not
        /// speak. An oracle that composes a different id than the runtime awaits does not report
        /// a wrong number; it reports a confident one.</summary>
        private static string ComposeSignal(StageRec s, out bool known)
        {
            known = true;
            string t = (s.OnTarget ?? "").Trim();
            switch ((s.OnKind ?? "").Trim().ToLowerInvariant())
            {
                case "talk":       return string.IsNullOrEmpty(t) ? null : "dialogue.ended:" + t;
                case "wave":       return "wave.cleared";
                case "build":      return string.IsNullOrEmpty(t) ? null : "build.structure_placed:" + t;
                case "panel":      return string.IsNullOrEmpty(t) ? null : "panel.opened:" + t;
                case "arena":      return "arena.resolved:win";
                case "reach":      return string.IsNullOrEmpty(t) ? null : "hero.reached:" + t;
                case "pet":        return string.IsNullOrEmpty(t) ? null : "pet.bonded:" + t;
                case "upgrade":    return string.IsNullOrEmpty(t) ? null : "structure.upgraded:" + t;
                case "population": return string.IsNullOrEmpty(t) ? null : "population.threshold:" + t;
                case "region":     return string.IsNullOrEmpty(t) ? null : "region.cleared:" + t;
                case "flag":       return null;   // satisfied by SetQuestFlag - Case 8 owns it
                case "dialoguecommand": return null;
                default: known = false; return null;
            }
        }

        // The runtime's own composer, reached by reflection so this file compiles whether or not
        // the schema silo's QuestCompletion type is present in the tree.
        private static bool _runtimeComposerResolved;
        private static Type _completionType;
        private static MethodInfo _toSignalId;
        private static FieldInfo _kindField, _targetField, _countField;

        private static void ResolveRuntimeComposer()
        {
            if (_runtimeComposerResolved) return;
            _runtimeComposerResolved = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("DeNelle.Core.Quests.QuestCompletion", false);
                    if (t == null) continue;
                    _completionType = t;
                    _toSignalId = t.GetMethod("ToSignalId", BindingFlags.Public | BindingFlags.Instance);
                    _kindField = t.GetField("Kind", BindingFlags.Public | BindingFlags.Instance);
                    _targetField = t.GetField("TargetId", BindingFlags.Public | BindingFlags.Instance);
                    _countField = t.GetField("Count", BindingFlags.Public | BindingFlags.Instance);
                    break;
                }
            }
            catch { _completionType = null; }
        }

        /// <summary>The signal the RUNTIME would await for this stage, or null when the schema
        /// type is not in the tree. <paramref name="available"/> distinguishes "the runtime says
        /// no signal" from "there is no runtime composer to ask".</summary>
        private static string RuntimeComposedSignal(StageRec s, out bool available)
        {
            available = false;
            ResolveRuntimeComposer();
            if (_completionType == null || _toSignalId == null ||
                _kindField == null || _targetField == null || _countField == null) return null;
            try
            {
                object inst = Activator.CreateInstance(_completionType);
                _kindField.SetValue(inst, s.OnKind);
                _targetField.SetValue(inst, s.OnTarget);
                _countField.SetValue(inst, s.OnCount);
                available = true;
                return _toSignalId.Invoke(inst, null) as string;
            }
            catch { available = false; return null; }
        }

        private static bool SignalHasEmitter(Ctx ctx, string signal)
        {
            if (string.IsNullOrEmpty(signal)) return true;
            if (ctx.ExactSignals.Contains(signal)) return true;
            foreach (var p in ctx.SignalPrefixes)
                if (signal.StartsWith(p, StringComparison.OrdinalIgnoreCase) && signal.Length > p.Length) return true;
            return false;
        }

        // =====================================================================
        //  CASE 3 - the mouths a quest names are real
        // =====================================================================

        private static void Case3_SpeakerEmbodied(Ctx ctx, List<string> failures, List<string> notes)
        {
            foreach (var id in ctx.QuestNamedDialogues)
            {
                if (!ctx.DialogueIds.Contains(id))
                {
                    failures.Add("[speaker-embodied] a quest stage names dialogue '" + id + "' which is NOT in " +
                                 "dialogues.json - DialogueService.Play returns false and the beat never opens. " +
                                 "Author the conversation or retarget the stage.");
                    continue;
                }
                if (!ctx.SourceLiterals.Contains(id))
                    failures.Add("[speaker-embodied] quest dialogue '" + id + "' exists but its id appears in no " +
                                 "live source under " + ModulesRoot + " - no NPC, injector or trigger opens it, so " +
                                 "the stage points at a conversation the player cannot start.");
            }

            // Broad, and deliberately not scoped to quest dialogues: an unresolvable speaker
            // renders a nameless card in ANY conversation, and the quest cast is the newest
            // content in the file.
            int checkedLines = 0;
            foreach (var d in ctx.Dialogues)
                foreach (var n in d.Nodes)
                    foreach (var sp in n.Speakers)
                    {
                        checkedLines++;
                        if (ctx.SpeakerNames.Contains(sp)) continue;
                        failures.Add("[speaker-embodied] dialogue '" + d.Id + "' node '" + n.Id + "' has a line " +
                                     "spoken by '" + sp + "' with no matching speakers[] record - the card renders " +
                                     "with no affiliation and no portrait, because DialogueCatalog.FindSpeaker " +
                                     "returns null and the view falls back to a silhouette. Add the record.");
                    }
            if (checkedLines == 0)
                failures.Add("[speaker-embodied] no authored dialogue line carries a speaker at all - the speaker " +
                             "contract has gone missing from the catalog");
            else
                notes.Add(checkedLines + " authored dialogue lines checked for a speakers[] record");
        }

        // =====================================================================
        //  CASE 4 - referents resolve
        // =====================================================================

        private static void Case4_ReferentResolves(Ctx ctx, List<string> failures, List<string> notes)
        {
            foreach (var q in ctx.Quests)
                foreach (var s in q.Stages)
                {
                    if (!s.HasCompleteOn || string.IsNullOrEmpty(s.OnTarget)) continue;
                    string kind = (s.OnKind ?? "").ToLowerInvariant();
                    bool ok = true;
                    string where = null;
                    switch (kind)
                    {
                        case "talk": ok = ctx.DialogueIds.Contains(s.OnTarget); where = "dialogues.json"; break;
                        case "build":
                        case "upgrade": ok = ctx.StructureIds.Contains(s.OnTarget); where = "structures-catalog.json"; break;
                        case "pet": ok = ctx.PetSpecies.Contains(s.OnTarget); where = "pets.json"; break;
                        case "region": ok = ctx.RegionIds.Contains(s.OnTarget); where = "realm-map.json"; break;
                        case "panel": ok = ctx.PanelIds.Count == 0 || ctx.PanelIds.Contains(s.OnTarget); where = "the PanelId enum"; break;
                        default: ok = true; break;   // wave/arena/population carry no catalog target
                    }
                    if (!ok)
                        failures.Add("[referent-resolves] quest '" + q.Id + "' stage '" + s.StageId +
                                     "' awaits completeOn kind '" + kind + "' targetId '" + s.OnTarget +
                                     "' which does NOT resolve in " + where + " - the player cannot do a thing to " +
                                     "an object that does not ship, so the stage can never complete. Retarget it at " +
                                     "something shipped, or build the thing.");
                }

            // Prose ledger. NOT failures: objective text naming a place or creature that ships
            // nowhere is a CONTENT decision for the PO (WO-854 sec.6 rulings D1/D2/D5/D7), and
            // failing on it would be failing on missing content.
            var unresolved = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var q in ctx.Quests)
                foreach (var s in q.Stages)
                    foreach (var phrase in ProperNouns(s.ObjectiveText))
                    {
                        if (ctx.ProseVocabulary.Contains(Norm(phrase))) continue;
                        if (!seen.Add(phrase)) continue;
                        unresolved.Add("'" + phrase + "' (" + q.Id + "#" + s.Index + ")");
                    }
            if (unresolved.Count > 0)
                notes.Add("prose referents that resolve in NO shipped catalog x" + unresolved.Count + ": " +
                          Join(unresolved, 15) + " - each is either a retarget onto shipped content or a build, " +
                          "and is the PO's call, not this suite's (heuristic ledger: capitalized phrases minus " +
                          "sentence-initial words, matched whole against shipped ids and display names)");
        }

        /// <summary>Capitalized noun phrases in objective prose, minus sentence-initial words
        /// (which are capitalized by grammar, not by being names) and a short stop list of
        /// capitalized non-referents. A ledger heuristic, honest about being one.</summary>
        private static readonly Regex NounRun = new Regex(
            @"[A-Z][a-z]{2,}(?:\s+(?:of|the)\s+[A-Z][a-z]{2,}|\s+[A-Z][a-z]{2,})*");

        private static readonly HashSet<string> ProseStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Tier", "Act", "The", "And", "But", "For", "With", "From", "Bond", "Bring", "Clear", "Walk", "Carry" };

        private static List<string> ProperNouns(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            var sentenceStarts = new HashSet<int>();
            bool atStart = true;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (atStart && !char.IsWhiteSpace(c)) { sentenceStarts.Add(i); atStart = false; }
                if (c == '.' || c == ':' || c == '!' || c == '?' || c == ';') atStart = true;
            }

            foreach (Match m in NounRun.Matches(text))
            {
                if (sentenceStarts.Contains(m.Index)) continue;
                string phrase = m.Value.Trim();
                if (phrase.Length < 4) continue;
                if (ProseStopWords.Contains(phrase)) continue;
                result.Add(phrase);
            }
            return result;
        }

        // =====================================================================
        //  CASE 5 - every reward can actually be paid
        // =====================================================================

        private static void Case5_RewardPayable(Ctx ctx, List<string> failures, List<string> notes)
        {
            string svc = ReadText(QuestServiceSrc, failures);
            if (svc != null && !Regex.IsMatch(StripComments(svc), @"RewardEarned\s*\?\s*\.\s*Invoke\s*\("))
                failures.Add("[reward-payable] QuestService never invokes RewardEarned - Core raises the numbers " +
                             "and a Village bridge grants them (Core cannot touch the wallet), so with the raise " +
                             "gone EVERY quest reward in the game is silently dropped.");

            string bridge = ReadText(RewardBridgeSrc, failures);
            bool paysCrystalsFood = false, paysMagic = false, paysItem = false;
            if (bridge != null)
            {
                string code = StripComments(bridge);
                if (code.IndexOf("RewardEarned", StringComparison.Ordinal) < 0)
                    failures.Add("[reward-payable] QuestRewardBridge no longer subscribes RewardEarned - nothing " +
                                 "listens for a quest reward, so nothing is ever granted.");
                paysCrystalsFood = Regex.IsMatch(code, @"Grant\s*\(\s*crystals\s*:");
                paysMagic = code.IndexOf("State.Magic", StringComparison.Ordinal) >= 0;
                paysItem = Regex.IsMatch(code, @"\binv\s*\.\s*Add\s*\(") ||
                           code.IndexOf("VillageInventory", StringComparison.Ordinal) >= 0;
            }

            int rewardedStages = 0;
            foreach (var q in ctx.Quests)
                foreach (var s in q.Stages)
                {
                    bool any = s.Crystals > 0 || s.Food > 0 || s.Magic > 0 || !string.IsNullOrEmpty(s.GrantItemId);
                    if (!any) continue;
                    rewardedStages++;

                    if ((s.Crystals > 0 || s.Food > 0) && bridge != null && !paysCrystalsFood)
                        failures.Add("[reward-payable] quest '" + q.Id + "' stage '" + s.StageId + "' pays " +
                                     "crystals/food but QuestRewardBridge has no EconomyService.Grant call - the " +
                                     "wallet route is gone and the reward evaporates.");
                    if (s.Magic > 0 && bridge != null && !paysMagic)
                        failures.Add("[reward-payable] quest '" + q.Id + "' stage '" + s.StageId + "' pays magic " +
                                     "but QuestRewardBridge no longer writes GameState.Magic - magic has no " +
                                     "EconomyService bucket, so that write IS the payment.");
                    if (!string.IsNullOrEmpty(s.GrantItemId))
                    {
                        if (bridge != null && !paysItem)
                            failures.Add("[reward-payable] quest '" + q.Id + "' stage '" + s.StageId + "' grants an " +
                                         "item but QuestRewardBridge no longer writes VillageInventory - the grant " +
                                         "is log-only again and the item never reaches the persisted bag.");
                        if (ctx.ItemIds.Count > 0 && !ctx.ItemIds.Contains(s.GrantItemId))
                            failures.Add("[reward-payable] quest '" + q.Id + "' stage '" + s.StageId +
                                         "' grants item id '" + s.GrantItemId + "' which resolves in NO shipped " +
                                         "catalog (weapons/armor/accessories/consumables/materials). " +
                                         "VillageInventory.Add would store a key nothing can render, name or equip - " +
                                         "the promised reward is unpayable. Retarget it at a shipped id, or author " +
                                         "the item.");
                    }
                }
            notes.Add(rewardedStages + " of " + ctx.TotalStages + " stages carry a non-zero reward; a reward on a " +
                      "stage that is not yet completable has never been dispensed even once");
        }

        // =====================================================================
        //  CASE 6 - what consumes a completed quest downstream
        // =====================================================================

        private static void Case6_TerminalConsumer(Ctx ctx, List<string> failures, List<string> notes)
        {
            string res = ReadText(GearRecipesRes, failures);
            string sa = ReadText(GearRecipesSA, failures);
            if (res == null) return;
            if (sa != null && !string.Equals(res, sa, StringComparison.Ordinal))
                failures.Add("[terminal-consumer] the two copies of gear-recipes.json DIFFER - Resources wins at " +
                             "load, so a gate edited only in StreamingAssets is invisible on device");

            JArray recipes;
            try { recipes = JObject.Parse(res)["recipes"] as JArray; }
            catch (Exception ex)
            {
                failures.Add("[terminal-consumer] gear-recipes.json is not valid JSON: " + ex.Message);
                return;
            }
            if (recipes == null) { failures.Add("[terminal-consumer] gear-recipes.json has no 'recipes' array"); return; }

            var blocked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var r in recipes)
            {
                string gate = Str(Get(r, "requiresQuestId"));
                if (string.IsNullOrEmpty(gate)) continue;
                string rid = Str(Get(r, "id"));

                if (!ctx.QuestIds.Contains(gate))
                {
                    failures.Add("[terminal-consumer] gear recipe '" + rid + "' is locked behind quest id '" + gate +
                                 "' which is NOT in quests.json - QuestService.IsCompleted answers false forever, " +
                                 "so the recipe is unobtainable by construction. Fix the id or drop the gate.");
                    continue;
                }
                bool completable;
                if (!ctx.QuestFullyCompletable.TryGetValue(gate, out completable) || !completable)
                {
                    List<string> list;
                    if (!blocked.TryGetValue(gate, out list)) { list = new List<string>(); blocked[gate] = list; }
                    list.Add(rid);
                }
            }

            foreach (var kv in blocked)
                failures.Add("[terminal-consumer] " + kv.Value.Count + " gear recipe(s) (" + Join(kv.Value, 8) +
                             ") are locked behind quest '" + kv.Key + "', which cannot be completed - the recipes " +
                             "are unobtainable BY CONSTRUCTION. This is the gap a set-reachability oracle cannot " +
                             "see: proving a set is co-EQUIPPABLE says nothing about whether it is ACQUIRABLE. " +
                             "Resolve it by making the gating quest completable, or by re-gating the recipes on a " +
                             "quest that is.");

            if (blocked.Count == 0) notes.Add("every gear-recipes requiresQuestId names a completable quest");
        }

        // =====================================================================
        //  CASE 7 - no quest verb points at nothing
        // =====================================================================

        private static void Case7_NoOrphanVerbs(Ctx ctx, List<string> failures, List<string> notes)
        {
            var orphans = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var d in ctx.Dialogues)
                foreach (var n in d.Nodes)
                    foreach (var c in n.Commands)
                    {
                        if (Array.IndexOf(QuestVerbs, c.Key) < 0) continue;
                        if (c.Value.Count == 0) continue;
                        string id = c.Value[0];
                        if (string.IsNullOrEmpty(id) || ctx.QuestIds.Contains(id)) continue;
                        List<string> sites;
                        if (!orphans.TryGetValue(id, out sites)) { sites = new List<string>(); orphans[id] = sites; }
                        sites.Add(d.Id + "#" + n.Id + " <" + c.Key + ">");
                    }

            foreach (var kv in orphans)
                failures.Add("[no-orphan-verbs] quest verb argument '" + kv.Key + "' is NOT a quest id in " +
                             "quests.json, fired from " + kv.Value.Count + " site(s): " + Join(kv.Value, 8) +
                             ". QuestService.StartQuest logs the unknown id and RETURNS (QuestService.cs:92), so " +
                             "the beat plays, the player is told something happened, and no quest state moves - a " +
                             "silent no-op dressed as progress. Either add '" + kv.Key + "' to quests.json or " +
                             "retarget the verbs at a shipped quest id. " +
                             "(SetQuestFlag is worse: QuestService.SetFlag seeds an Active entry at beat 0 for an " +
                             "unknown id, bypassing the Available bookkeeping entirely.)");

            if (orphans.Count == 0)
                notes.Add("every quest verb argument in dialogues.json resolves to a shipped quest id");
        }

        // =====================================================================
        //  CASE 8 - a required flag has something that sets it
        // =====================================================================

        private static void Case8_FlagSatisfiable(Ctx ctx, List<string> failures, List<string> notes)
        {
            int gated = 0;
            foreach (var q in ctx.Quests)
                foreach (var s in q.Stages)
                {
                    if (string.IsNullOrEmpty(s.RequiresFlag)) continue;
                    gated++;
                    if (ctx.FlagSetters.Contains(q.Id + "|" + s.RequiresFlag)) continue;
                    failures.Add("[flag-satisfiable] quest '" + q.Id + "' stage '" + s.StageId + "' requires flag '" +
                                 s.RequiresFlag + "' but nothing authors SetQuestFlag for that quest and flag - the " +
                                 "gate can never open, so the stage is un-completable no matter what else advances " +
                                 "it. Author the SetQuestFlag on the beat that earns it, or clear requiresFlag.");
                }

            if (gated == 0)
                notes.Add("no stage carries a requiresFlag today, so the flag gate is unexercised - the check is " +
                          "armed for the moment one is authored");
        }

        // =====================================================================
        //  CASE 9 - every authored completeOn KIND has a named live incrementer
        // =====================================================================

        /// <summary>
        /// Case 2 proves the composed SIGNAL has a TutorialSignals.Raise site. This case proves
        /// the SEAM THAT FEEDS THAT RAISE is still subscribed, which is a different failure:
        /// deleting `_wave.OnWaveCleared.AddListener(...)` leaves the Raise line sitting in the
        /// file, so Case 2 stays green while every wave stage in the game goes dead. One row per
        /// kind, each naming the exact event to re-wire.
        /// </summary>
        private static void Case9_KindIncrementer(Ctx ctx, List<string> failures, List<string> notes)
        {
            // kind -> (source file, tokens that must ALL appear, the sentence a 2am reader needs)
            var table = new Dictionary<string, KindIncrementer>(StringComparer.OrdinalIgnoreCase)
            {
                ["talk"] = new KindIncrementer(
                    "Assets/_Modules/Core/Tutorial/TutorialSignals.cs",
                    new[] { "DialogueService.EndedWithId", "DialogueEndedPrefix" },
                    "TutorialCoreSignalAdapter.Wire must subscribe DialogueService.EndedWithId and " +
                    "raise TutorialSignals.DialogueEndedPrefix + id"),
                ["panel"] = new KindIncrementer(
                    "Assets/_Modules/Core/Tutorial/TutorialSignals.cs",
                    new[] { "PanelRouter.PanelOpened", "PanelOpenedPrefix" },
                    "TutorialCoreSignalAdapter.Wire must subscribe PanelRouter.PanelOpened and raise " +
                    "TutorialSignals.PanelOpenedPrefix + id"),
                ["wave"] = new KindIncrementer(
                    SignalAdaptersSrc,
                    new[] { "OnWaveCleared", "TutorialSignals.WaveCleared" },
                    "TutorialSignalAdapters must add a listener to WaveManager.OnWaveCleared that raises " +
                    "TutorialSignals.WaveCleared"),
                ["arena"] = new KindIncrementer(
                    SignalAdaptersSrc,
                    new[] { "OnBattleEnded", "TutorialSignals.ArenaWin" },
                    "TutorialSignalAdapters must subscribe BattleArena.OnBattleEnded and raise " +
                    "TutorialSignals.ArenaWin / ArenaLoss"),
                ["build"] = new KindIncrementer(
                    SignalAdaptersSrc,
                    new[] { "BuildModeController.StructurePlaced", "StructurePlacedPrefix" },
                    "TutorialSignalAdapters must subscribe BuildModeController.StructurePlaced (the LIVE " +
                    "placement path - TowerPlacementSystem/BuildMenu is the legacy one and is in no scene) " +
                    "and raise TutorialSignals.StructurePlacedPrefix + entryId"),
                ["pet"] = new KindIncrementer(
                    "Assets/_Modules/Pets/PetAcquisitionService.cs",
                    new[] { "TutorialSignals.PetBondedPrefix" },
                    "PetAcquisitionService.Acquire must raise TutorialSignals.PetBondedPrefix + def.Species " +
                    "on every new bond"),
            };

            // Kinds the SPINE resolves without an incrementer of their own: a flag is polled
            // through QuestService.HasFlag (Case 8 owns it) and a dialogueCommand is an
            // AdvanceQuest node (Case 2's pool owns it). Listing them here is what keeps the
            // "no row" branch below meaningful instead of noisy.
            var selfSourced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "flag", "dialogueCommand" };

            var authored = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var q in ctx.Quests)
                foreach (var s in q.Stages)
                {
                    if (!s.HasCompleteOn || string.IsNullOrEmpty(s.OnKind)) continue;
                    authored.TryGetValue(s.OnKind, out int n);
                    authored[s.OnKind] = n + 1;
                }

            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in authored)
            {
                string kind = kv.Key;
                int stages = kv.Value;
                if (selfSourced.Contains(kind)) continue;

                KindIncrementer inc;
                if (!table.TryGetValue(kind, out inc))
                {
                    notes.Add("completeOn kind '" + kind + "' (" + stages + " stage(s)) has NO row in Case 9's " +
                              "incrementer table - it is judged only by Case 2's signal scan, which cannot see a " +
                              "deleted subscription. Add a row naming the event that feeds its Raise.");
                    continue;
                }

                string code;
                if (!cache.TryGetValue(inc.File, out code))
                {
                    code = File.Exists(inc.File) ? StripComments(File.ReadAllText(inc.File)) : null;
                    cache[inc.File] = code;
                }
                if (code == null)
                {
                    failures.Add("[kind-incrementer] completeOn kind '" + kind + "' backs " + stages +
                                 " stage(s) and its incrementer file " + inc.File + " DOES NOT EXIST - nothing " +
                                 "can advance those stages. " + inc.Fix);
                    continue;
                }

                foreach (var token in inc.Tokens)
                {
                    if (code.IndexOf(token, StringComparison.Ordinal) >= 0) continue;
                    failures.Add("[kind-incrementer] completeOn kind '" + kind + "' backs " + stages +
                                 " stage(s) but " + inc.File + " no longer contains '" + token + "' - the seam " +
                                 "that feeds the signal is gone, so every one of those stages is dead while the " +
                                 "Raise line still reads as live. " + inc.Fix + ".");
                }
            }

            if (authored.Count == 0)
                notes.Add("no stage carries a completeOn today, so the incrementer table is unexercised - it is " +
                          "armed for the moment one is authored");
        }

        private sealed class KindIncrementer
        {
            public readonly string File;
            public readonly string[] Tokens;
            public readonly string Fix;
            public KindIncrementer(string file, string[] tokens, string fix)
            { File = file; Tokens = tokens; Fix = fix; }
        }

        // =====================================================================
        //  CASES 10-12 - THE DAILY QUESTS
        // =====================================================================

        private sealed class DailyTemplateRec
        {
            public string Id = "";
            public string Slot = "";
            public int Target = 1;
            public bool Day1Guaranteed;
            public string RequiresFeature = "";
        }

        private sealed class ReporterRec
        {
            public string Id = "";
            public string File = "";
        }

        private sealed class DailyCtx
        {
            public List<DailyTemplateRec> Templates = new List<DailyTemplateRec>();
            public List<string> Slots = new List<string>();
            public List<ReporterRec> Reporters = new List<ReporterRec>();
            /// <summary>Template ids some Report call site can actually satisfy.</summary>
            public HashSet<string> Proven = new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// THE SERVICE'S OWN MATCHING RULE, mirrored exactly (DailyQuests.cs:259):
        /// <c>q.TemplateId == eventId || q.TemplateId.StartsWith(eventId + ".")</c>.
        /// Ordinal, like the runtime's default string comparisons. If that line ever changes,
        /// change THIS one in the same edit - an oracle judging by a different rule than the
        /// game is worse than no oracle, because it is confidently wrong.
        /// </summary>
        private static bool DailyMatches(string templateId, string reportedId)
        {
            if (string.IsNullOrEmpty(templateId) || string.IsNullOrEmpty(reportedId)) return false;
            return string.Equals(templateId, reportedId, StringComparison.Ordinal)
                || templateId.StartsWith(reportedId + ".", StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------
        //  CASE 10 - every daily template is reachable, and every reporter reaches one
        // ---------------------------------------------------------------------

        private static void Case10_DailyReachable(DailyCtx d, List<string> failures, List<string> notes)
        {
            string res = ReadText(DailyQuestsRes, failures);
            string sa = ReadText(DailyQuestsSA, failures);
            if (res == null) return;

            if (sa != null && !string.Equals(res, sa, StringComparison.Ordinal))
                failures.Add("[daily-reachable] the Resources and StreamingAssets copies of daily-quests.json " +
                             "DIFFER. The catalog reads Resources first, so a template authored only in " +
                             "StreamingAssets never rolls on device. Write BOTH copies in the same edit.");

            JObject root;
            try { root = JObject.Parse(res); }
            catch (Exception ex)
            {
                failures.Add("[daily-reachable] daily-quests.json is not valid JSON (" + ex.Message +
                             ") - the daily catalog loads EMPTY, so all three slots are blank for every player");
                return;
            }

            var slots = root["slots"] as JArray;
            if (slots != null)
                foreach (var s in slots)
                {
                    string id = Str(Get(s, "slot"));
                    if (!string.IsNullOrEmpty(id) && !d.Slots.Contains(id)) d.Slots.Add(id);
                }

            var arr = root["templates"] as JArray;
            if (arr == null || arr.Count == 0)
            {
                failures.Add("[daily-reachable] daily-quests.json has no 'templates' array - every slot rolls " +
                             "nothing, so the whole daily-resource on-ramp is gone");
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in arr)
            {
                var o = t as JObject;
                if (o == null) continue;
                var rec = new DailyTemplateRec
                {
                    Id = Str(o["id"]),
                    Slot = Str(o["slot"]),
                    Target = Math.Max(1, o["target"] != null ? Int(o["target"]) : 1),
                    Day1Guaranteed = Bool(o["day1Guaranteed"]),
                    RequiresFeature = Str(o["requiresFeature"]),
                };
                if (string.IsNullOrEmpty(rec.Id))
                {
                    failures.Add("[daily-reachable] a daily template has NO id - Report() matches on the id, so " +
                                 "an id-less template can roll into a slot and can never be advanced");
                    continue;
                }
                if (!seen.Add(rec.Id))
                {
                    failures.Add("[daily-reachable] duplicate daily template id '" + rec.Id + "' - FindTemplate " +
                                 "returns the first match, so the second row's target/label are dead content");
                    continue;
                }
                if (!string.IsNullOrEmpty(rec.Slot) && !d.Slots.Contains(rec.Slot))
                    failures.Add("[daily-reachable] template '" + rec.Id + "' declares slot '" + rec.Slot +
                                 "' which is in no slots[] row - it can never be rolled into any slot. Fix the " +
                                 "slot name or add the slot.");
                d.Templates.Add(rec);
            }
            LastDailyTemplates = d.Templates.Count;

            // The REAL load path must agree with the raw file, for the same reason Case 0 makes
            // QuestCatalog agree: a DTO field that stops mapping is how a template can be
            // authored, shipped and silently ignored.
            try
            {
                DeNelle.Core.Quests.DailyQuestCatalog.Reload();
                var live = DeNelle.Core.Quests.DailyQuestCatalog.Templates;
                int liveCount = live != null ? live.Count : 0;
                if (liveCount != d.Templates.Count)
                    failures.Add("[daily-reachable] the raw daily-quests.json holds " + d.Templates.Count +
                                 " templates but the REAL load path (DailyQuestCatalog) sees " + liveCount +
                                 " - a DTO mapping has broken, so authored dailies are being dropped on load");
            }
            catch (Exception ex)
            {
                failures.Add("[daily-reachable] DailyQuestCatalog.Reload threw " + ex.GetType().Name + ": " +
                             ex.Message + " - the runtime cannot load the daily catalog at all");
            }

            d.Reporters = CollectDailyReporters(failures, notes);
            if (d.Reporters.Count == 0)
            {
                failures.Add("[daily-reachable] found ZERO DailyQuestService Report call sites under " +
                             ModulesRoot + " - every one of the " + d.Templates.Count + " templates is " +
                             "uncompletable and no player can finish a single daily. Either the scan root moved " +
                             "or every bridge was removed.");
                return;
            }

            // ---- FORWARD: every template must have a reporter that can satisfy it ----
            foreach (var t in d.Templates)
            {
                bool reachable = false;
                foreach (var r in d.Reporters) if (DailyMatches(t.Id, r.Id)) { reachable = true; break; }
                if (reachable) { d.Proven.Add(t.Id); continue; }

                failures.Add("[daily-reachable] daily template '" + t.Id + "' (slot '" + t.Slot + "', target " +
                             t.Target + ") can NEVER be completed: no DailyQuestService.Report call site under " +
                             ModulesRoot + " uses an id that matches it. Report matches EXACTLY or by the " +
                             "'<reportedId>.' prefix, so wire a Report(\"" + DailyPrefixHint(t.Id) + "\", 1) into " +
                             "the system that already owns that event (the way DailyQuestCombatBridge does for " +
                             "combat.clear-waves). A template with no reporter looks identical to a player who " +
                             "just has not done it yet - that is why it went unnoticed for months.");
            }
            LastDailyTemplatesProven = d.Proven.Count;

            // ---- REVERSE: every reporter must be able to reach some template ----
            // A dead reporter is the mirror defect and just as invisible: the code believes it
            // is crediting the player, the tick lands nowhere, and nothing says so.
            var deadReported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in d.Reporters)
            {
                bool hits = false;
                foreach (var t in d.Templates) if (DailyMatches(t.Id, r.Id)) { hits = true; break; }
                if (hits || !deadReported.Add(r.Id)) continue;

                failures.Add("[daily-reachable] DEAD REPORTER: " + r.File + " calls DailyQuestService.Report(\"" +
                             r.Id + "\") but NO template in daily-quests.json matches that id (exact or '" +
                             r.Id + ".' prefix). The tick is thrown away every time it fires. Either author the " +
                             "template(s) it is meant to advance, or delete the reporter - leaving it is a " +
                             "standing lie in the code about what the game credits.");
            }

            // A slot whose every template is unreachable is a slot that shows the player a quest
            // and can never pay it. That is the shape the audit actually found, twice.
            foreach (var slot in d.Slots)
            {
                int total = 0, ok = 0;
                foreach (var t in d.Templates)
                {
                    if (!string.Equals(t.Slot, slot, StringComparison.Ordinal)) continue;
                    total++;
                    if (d.Proven.Contains(t.Id)) ok++;
                }
                if (total == 0)
                    failures.Add("[daily-reachable] slot '" + slot + "' has NO templates at all - it rolls empty " +
                                 "for every player, every day");
                else if (ok == 0)
                    failures.Add("[daily-reachable] slot '" + slot + "' has " + total + " template(s) and NOT ONE " +
                                 "of them is reachable - whatever rolls into that slot can never be completed. " +
                                 "This is exactly the state the 2026-08-21 audit found for the combat and " +
                                 "exploration slots.");
                else
                    notes.Add("daily slot '" + slot + "': " + ok + "/" + total + " templates reachable");
            }
        }

        /// <summary>The id a fix should report, given a template id: everything before the last
        /// dot-segment, which is the family prefix the bridges use (combat.clear-waves.warband
        /// -> combat.clear-waves). Purely advisory text inside a failure string.</summary>
        private static string DailyPrefixHint(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return "";
            int dot = templateId.LastIndexOf('.');
            return dot > 0 ? templateId.Substring(0, dot) : templateId;
        }

        /// <summary>
        /// Every DailyQuestService.Report call site under Assets/_Modules, with its first
        /// argument resolved to a literal. Const identifiers are resolved from the declaring
        /// file first, then from a repo-wide const map, so
        /// <c>Report(DailyQuestService.Day1QuestTemplateId, 1)</c> is judged as
        /// "combat.build-towers" rather than skipped. Comments are stripped first, so a Report
        /// call that only exists in prose (there are three) never counts as a live reporter.
        /// </summary>
        private static List<ReporterRec> CollectDailyReporters(List<string> failures, List<string> notes)
        {
            var result = new List<ReporterRec>();
            string[] files;
            try { files = Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories); }
            catch (Exception ex)
            {
                failures.Add("[daily-reachable] could not enumerate " + ModulesRoot + ": " + ex.Message +
                             " - without the source scan no daily template can be proven reachable");
                return result;
            }

            var constRx = new Regex("const\\s+string\\s+(\\w+)\\s*=\\s*\"([^\"]*)\"\\s*;");
            var codes = new Dictionary<string, string>(StringComparer.Ordinal);
            var perFileConsts = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var globalConsts = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                string src;
                try { src = File.ReadAllText(file); } catch { continue; }
                string code = StripComments(src);
                codes[file] = code;

                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match m in constRx.Matches(code))
                {
                    map[m.Groups[1].Value] = m.Groups[2].Value;
                    if (!globalConsts.ContainsKey(m.Groups[1].Value))
                        globalConsts[m.Groups[1].Value] = m.Groups[2].Value;
                }
                perFileConsts[file] = map;
            }

            foreach (var kv in codes)
            {
                string file = kv.Key, code = kv.Value;
                if (code.IndexOf("DailyQuest", StringComparison.Ordinal) < 0) continue;

                int at = 0;
                while (true)
                {
                    int idx = code.IndexOf(".Report(", at, StringComparison.Ordinal);
                    if (idx < 0) break;
                    at = idx + 8;

                    // Only DailyQuestService receivers. The window is deliberately short so an
                    // unrelated Report( on another service cannot be captured by a mention of
                    // DailyQuest elsewhere in the file.
                    int from = Math.Max(0, idx - 80);
                    if (code.Substring(from, idx - from).IndexOf("DailyQuest", StringComparison.Ordinal) < 0)
                        continue;

                    string args = ExtractBalanced(code, idx + 7);
                    if (args == null) continue;
                    string first = FirstArgument(args);
                    string id = ResolveStringExpr(first, perFileConsts[file], globalConsts);
                    if (id == null)
                    {
                        notes.Add("UNJUDGED daily reporter in " + file + ": Report(" + Trim(first, 48) +
                                  ") - the first argument is not a literal or a resolvable string const, so this " +
                                  "oracle cannot tell which templates it can satisfy. Make it a const string.");
                        continue;
                    }
                    if (id.Length == 0) continue;
                    result.Add(new ReporterRec { Id = id, File = file });
                }
            }
            return result;
        }

        /// <summary>The first argument of an argument list, split at depth-0 commas.</summary>
        private static string FirstArgument(string args)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = 0; i < args.Length; i++)
            {
                char c = args[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '(' || c == '[' || c == '<') depth++;
                else if (c == ')' || c == ']' || c == '>') depth--;
                else if (c == ',' && depth <= 0) return args.Substring(0, i);
            }
            return args;
        }

        /// <summary>A string literal's value, or a const identifier's value, or null when the
        /// expression cannot be resolved statically (which is reported as UNJUDGED, never
        /// silently treated as absent).</summary>
        private static string ResolveStringExpr(string expr, Dictionary<string, string> local,
                                                Dictionary<string, string> global)
        {
            if (expr == null) return null;
            string e = expr.Trim();
            if (e.Length == 0) return null;

            var lit = Regex.Match(e, "^\"([^\"]*)\"$");
            if (lit.Success) return lit.Groups[1].Value;

            if (!Regex.IsMatch(e, @"^[A-Za-z_][\w\.]*$")) return null;
            int dot = e.LastIndexOf('.');
            string name = dot >= 0 ? e.Substring(dot + 1) : e;
            string v;
            if (local != null && local.TryGetValue(name, out v)) return v;
            if (global != null && global.TryGetValue(name, out v)) return v;
            return null;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        // ---------------------------------------------------------------------
        //  CASE 11 - the three audit fixes, and the invariant that makes one safe
        // ---------------------------------------------------------------------

        private static void Case11_DailyBridgeLive(DailyCtx d, List<string> failures, List<string> notes)
        {
            // (1) The tower bridge listens to the LIVE placement event.
            string tower = ReadText(TowerBridgeSrc, failures);
            string towerPrefix = "tower_";
            if (tower != null)
            {
                string code = StripComments(tower);
                if (code.IndexOf("BuildModeController.StructurePlaced", StringComparison.Ordinal) < 0)
                    failures.Add("[daily-bridge-live] " + TowerBridgeSrc + " no longer subscribes " +
                                 "BuildModeController.StructurePlaced. That is the ONLY live placement event: " +
                                 "TowerPlacementSystem.OnTowerPlaced fires solely from BuildMenu, whose guid is " +
                                 "in no scene and no prefab. Without it 'combat.build-towers' never ticks - and " +
                                 "because it is day1Guaranteed the combat slot force-returns it on EVERY roll " +
                                 "until it is completed, so the slot is pinned to an uncompletable quest FOREVER.");
                var pfx = Regex.Match(code, "TowerIdPrefix\\s*=\\s*\"([^\"]*)\"");
                if (pfx.Success && pfx.Groups[1].Value.Length > 0) towerPrefix = pfx.Groups[1].Value;
                else
                    notes.Add("could not read TowerIdPrefix from " + TowerBridgeSrc + "; the Tower-row invariant " +
                              "below is checked against the default '" + towerPrefix + "'");
                if (code.IndexOf("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal) < 0)
                    failures.Add("[daily-bridge-live] " + TowerBridgeSrc + " lost its " +
                                 "RuntimeInitializeOnLoadMethod self-bootstrap - nothing else AddComponents it " +
                                 "(its guid is in no scene and no prefab), so the component would never exist at " +
                                 "runtime and the subscription above would never happen.");
            }

            // (2) The combat bridge is attached at RUNTIME, not only by the editor scene builder.
            string boot = ReadText(WaveBootstrapSrc, failures);
            if (boot != null && StripComments(boot).IndexOf("Ensure<DailyQuestCombatBridge>", StringComparison.Ordinal) < 0)
                failures.Add("[daily-bridge-live] " + WaveBootstrapSrc + " no longer contains " +
                             "Ensure<DailyQuestCombatBridge>. The component's guid is in NO .unity and NO " +
                             ".prefab - it was attached only by the EDITOR-ONLY VillageSceneBuilder, which does " +
                             "not run in a player build. Without this one line the 17 combat.clear-waves.* " +
                             "templates have no listener and can never tick.");
            if (!File.Exists(CombatBridgeSrc))
                failures.Add("[daily-bridge-live] " + CombatBridgeSrc + " is missing - the component " +
                             WaveBootstrapSrc + " attaches does not exist, so every combat.clear-waves.* daily " +
                             "is dead.");

            // (3) A gate built by the real placement path carries its proximity opener.
            string factory = ReadText(StructureFactorySrc, failures);
            if (factory != null)
            {
                string code = StripComments(factory);
                int gateCase = code.IndexOf("case \"Gate\":", StringComparison.Ordinal);
                bool attached = false;
                if (gateCase >= 0)
                {
                    int end = code.IndexOf("break;", gateCase, StringComparison.Ordinal);
                    if (end < 0) end = Math.Min(code.Length, gateCase + 2000);
                    attached = code.IndexOf("AddComponent<GateProximityOpener>", gateCase,
                                            Math.Max(0, end - gateCase), StringComparison.Ordinal) >= 0;
                }
                if (gateCase < 0)
                    failures.Add("[daily-bridge-live] " + StructureFactorySrc + " has no `case \"Gate\":` - the " +
                                 "oracle cannot find where a gate is built, so it cannot prove the 12 " +
                                 "explore.visit-gate.* templates have a ticker. Restore the case or update this " +
                                 "check in the same edit.");
                else if (!attached)
                    failures.Add("[daily-bridge-live] " + StructureFactorySrc + "'s `case \"Gate\":` no longer " +
                                 "calls AddComponent<GateProximityOpener>(). That opener is what raises " +
                                 "OnHeroEntered, which is the ONLY tick behind the 12 explore.visit-gate.* " +
                                 "templates. The only other attacher is VillageController, whose guid is in no " +
                                 "scene - so on a player-built town the whole exploration slot goes dead.");
            }
            if (!File.Exists(GateOpenerSrc))
                failures.Add("[daily-bridge-live] " + GateOpenerSrc + " is missing - nothing can report " +
                             "explore.visit-gate, so the exploration slot is uncompletable.");

            // (4) ⛔ THE INVARIANT THAT MAKES THE PREFIX FILTER SAFE.
            // The bridge tests structureId.StartsWith("tower_") because NO runtime code reads
            // structures-catalog.json's "type" field. That stand-in is only correct while the
            // naming holds: a row typed Tower without the prefix would place fine, look like a
            // tower, and silently never count toward the day-1 quest.
            int towerRows = 0;
            try
            {
                if (File.Exists(StructuresRes))
                {
                    var root = JObject.Parse(File.ReadAllText(StructuresRes));
                    var entries = root["entries"] as JArray;
                    if (entries != null)
                        foreach (var e in entries)
                        {
                            if (!string.Equals(Str(Get(e, "type")), "Tower", StringComparison.Ordinal)) continue;
                            towerRows++;
                            string id = Str(Get(e, "id"));
                            if (id.StartsWith(towerPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                            failures.Add("[daily-bridge-live] structures-catalog.json row '" + id + "' is typed " +
                                         "\"Tower\" but its id does not start with '" + towerPrefix + "'. " +
                                         "DailyQuestTowerBridge filters placements by that PREFIX (no runtime " +
                                         "code reads the catalog's type field), so placing this tower would " +
                                         "silently NOT count toward 'combat.build-towers' - the day1Guaranteed " +
                                         "quest that pins the combat slot until it is completed. Rename the row " +
                                         "to '" + towerPrefix + "...', or teach the bridge to read the type.");
                        }
                }
                else failures.Add("[daily-bridge-live] " + StructuresRes + " is missing - the Tower-prefix " +
                                  "invariant cannot be checked");
            }
            catch (Exception ex)
            {
                failures.Add("[daily-bridge-live] could not read " + StructuresRes + " (" + ex.GetType().Name +
                             ") - the Tower-prefix invariant is unchecked");
            }

            bool wantsTowers = false;
            foreach (var t in d.Templates)
                if (t.Id.StartsWith("combat.build-towers", StringComparison.Ordinal)) wantsTowers = true;
            if (wantsTowers && towerRows == 0)
                failures.Add("[daily-bridge-live] daily-quests.json asks the player to build towers but " +
                             "structures-catalog.json ships ZERO rows typed \"Tower\" - there is nothing to " +
                             "build, so the quest is uncompletable by construction.");
            else if (towerRows > 0)
                notes.Add(towerRows + " structures-catalog rows are typed Tower and all carry the '" +
                          towerPrefix + "' prefix the bridge filters on");
        }

        // ---------------------------------------------------------------------
        //  CASE 12 - day1Guaranteed templates must be PROVABLY completable
        // ---------------------------------------------------------------------

        private static void Case12_Day1Guaranteed(DailyCtx d, List<string> failures, List<string> notes)
        {
            int flagged = 0;
            foreach (var t in d.Templates)
            {
                if (!t.Day1Guaranteed) continue;
                flagged++;
                if (d.Proven.Contains(t.Id)) continue;

                failures.Add("[day1-guaranteed] template '" + t.Id + "' is day1Guaranteed AND unreachable - this " +
                             "is categorically worse than an ordinary dead template. DailyQuests FORCE-RETURNS a " +
                             "day1Guaranteed template for its slot ('" + t.Slot + "') on EVERY roll while " +
                             "Day1QuestDone is false, and that latch is set ONLY when the quest completes " +
                             "(DailyQuests.cs Report -> Day1DonePrefKey). So an uncompletable one does not cost " +
                             "the player one day - it PINS that slot to an unwinnable quest permanently, for " +
                             "every new player, and re-rolls cannot escape it. Wire its Report tick (see the " +
                             "[daily-reachable] failure for the same id) or clear the day1Guaranteed flag.");
            }

            if (flagged == 0 && d.Templates.Count > 0)
                notes.Add("no daily template is day1Guaranteed today, so the force-return path is unexercised - " +
                          "this check is armed for the moment one is flagged");
            else if (flagged > 1)
                notes.Add(flagged + " templates are day1Guaranteed; DailyQuests force-returns one PER SLOT, so " +
                          "two flagged in the same slot means one of them can never appear on day 1");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>The prerequisite the RUNTIME's QuestDef carries for a quest, read by
        /// reflection so this oracle still compiles (and still reports honestly) if the field is
        /// absent from the DTO. Null means the DTO has no such field at all - which is itself the
        /// defect Case 0 reports, since the authored gate would then be inert.</summary>
        private static string ReadRequiresQuestId(object questDef)
        {
            if (questDef == null) return null;
            var f = questDef.GetType().GetField("RequiresQuestId", BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return null;
            return f.GetValue(questDef) as string;
        }

        private static bool DialogueAuthorsVerb(Ctx ctx, string verb, string questId)
        {
            foreach (var d in ctx.Dialogues)
                foreach (var n in d.Nodes)
                    foreach (var c in n.Commands)
                        if (string.Equals(c.Key, verb, StringComparison.Ordinal) &&
                            c.Value.Count > 0 && string.Equals(c.Value[0], questId, StringComparison.Ordinal))
                            return true;
            return false;
        }

        private static bool FileContains(string root, string token)
        {
            try
            {
                foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string src;
                    try { src = File.ReadAllText(f); } catch { continue; }
                    if (StripComments(src).IndexOf(token, StringComparison.Ordinal) >= 0) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>String value of a token, or empty. Non-scalar tokens answer empty rather
        /// than throwing, so one malformed row is reported by its own case instead of aborting
        /// the whole suite.</summary>
        private static string Str(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return "";
            var v = t as JValue;
            if (v == null) return "";
            return v.Value != null ? v.Value.ToString() : "";
        }

        private static int Int(JToken t)
        {
            string s = Str(t);
            int v;
            return int.TryParse(s, out v) ? v : 0;
        }

        private static bool Bool(JToken t)
        {
            string s = Str(t);
            bool v;
            return bool.TryParse(s, out v) && v;
        }

        /// <summary>Child token by key, or null when the parent is not an object.</summary>
        private static JToken Get(JToken parent, string key)
        {
            var o = parent as JObject;
            return o != null ? o[key] : null;
        }

        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        private static string Join(List<string> items, int max)
        {
            if (items.Count <= max) return string.Join(" | ", items.ToArray());
            var head = items.GetRange(0, max);
            return string.Join(" | ", head.ToArray()) + " | ...and " + (items.Count - max) + " more";
        }

        private static string ReadText(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[source] missing file: " + path +
                             " - the oracle cannot judge what it cannot read, so this is a failure, never a skip");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[source] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and block comments so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }

        /// <summary>Text inside the parens starting at <paramref name="open"/>, honouring
        /// nesting; null when unbalanced.</summary>
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
    }
}
