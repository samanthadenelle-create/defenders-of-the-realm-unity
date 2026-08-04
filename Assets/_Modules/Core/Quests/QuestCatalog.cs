// =============================================================================
// QuestCatalog — the GENERAL (story / vendor / forgemaster / pet-narrative)
// quest catalog, distinct from the daily-quest system. Loads
// StreamingAssets/Data/Canonical/quests.json (WebGL-safe via CanonicalJson —
// Resources dual-copy wins at load).
// -----------------------------------------------------------------------------
// One file holds:
//   • QuestReward / QuestStage / QuestDef — the JSON shape of quests.json.
//   • QuestCatalogData — the file root ({ version, quests:[…] }).
//   • QuestCatalog — static loader (mirrors DailyQuestCatalog EXACTLY).
//
// SCOPE: data + lookup only. Runtime progress lives in QuestService (reads/writes
// GameState.Quests); reward dispensing happens on the Village side via the
// RewardEarned event (Core never references the wallet).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.Quests
{
    // ── JSON DTOs ────────────────────────────────────────────────────────────

    /// <summary>One stage's reward. Zero / empty slots mean "no reward there".
    /// Economy grants happen on the Village side (QuestRewardBridge) — Core only
    /// carries the numbers.</summary>
    [Serializable]
    public sealed class QuestReward
    {
        [JsonProperty("crystals")] public int Crystals;
        [JsonProperty("food")] public int Food;
        [JsonProperty("magic")] public int Magic;
        [JsonProperty("grantItemId")] public string GrantItemId;
    }

    /// <summary>
    /// WO-854 Phase 2 -- the completion CONDITION for one stage. Optional and
    /// additive: a stage with no completeOn keeps the legacy path (a dialogue
    /// authoring an explicit AdvanceQuest command), so every quest that shipped
    /// before this field behaves exactly as it did.
    ///
    /// Shape in quests.json:
    ///   "completeOn": { "kind": "talk", "targetId": "village_elder", "count": 1 }
    ///
    /// ToSignalId() composes the TutorialSignals bus id the stage waits for. The
    /// Village-side StoryQuestSignalBridge subscribes the bus and advances the
    /// quest on a match; Core only describes the condition (Core raises, Village
    /// bridges -- the QuestRewardBridge pattern).
    /// </summary>
    [Serializable]
    public sealed class QuestCompletion
    {
        /// <summary>Condition family. See KindTalk..KindDialogueCommand below.</summary>
        [JsonProperty("kind")] public string Kind;
        /// <summary>The thing the kind points at (dialogue id, structure id, panel id,
        /// anchor id, region id, species id, quest-flag name). Unused by kinds whose
        /// signal carries no target (wave, arena).</summary>
        [JsonProperty("targetId")] public string TargetId;
        /// <summary>How many times the signal must fire. 0 or 1 both mean "once".</summary>
        [JsonProperty("count")] public int Count;

        // -- kind vocabulary v1 (WO-854 sec.4) --------------------------------
        // Emitter LIVE today:
        public const string KindTalk            = "talk";
        public const string KindWave            = "wave";
        public const string KindBuild           = "build";
        public const string KindPanel           = "panel";
        public const string KindArena           = "arena";
        public const string KindReach           = "reach";
        public const string KindFlag            = "flag";
        public const string KindDialogueCommand = "dialoguecommand";
        // KindPet is LIVE too (WO-854 final wave): PetAcquisitionService.Acquire raises
        // pet.bonded:<species> on every new bond.
        public const string KindPet        = "pet";
        // Emitter NOT built yet (Silo E / WO-827) -- composed here so authoring and
        // the oracle share one grammar, but nothing Raises these ids yet:
        public const string KindUpgrade    = "upgrade";
        public const string KindPopulation = "population";
        public const string KindRegion     = "region";

        // Signal prefixes. A prefix lives in TutorialSignals once an emitter raises it
        // and is ALIASED here, so one literal keeps one owner; the rest stay local
        // until Silo E / WO-827 lands the matching Raise and promotes them.
        //
        // "pet.bonded:" is promoted (WO-854 final wave): PetAcquisitionService.Acquire
        // raises TutorialSignals.PetBondedPrefix + species, so the constant belongs to
        // the emitter's vocabulary and this row points at it rather than repeating it.
        public const string PetBondedPrefix          = DeNelle.Core.Tutorial.TutorialSignals.PetBondedPrefix;
        public const string StructureUpgradedPrefix  = "structure.upgraded:";
        public const string PopulationThresholdPrefix = "population.threshold:";
        public const string RegionClearedPrefix      = "region.cleared:";

        /// <summary>Kind lowercased + trimmed, so authoring is case-insensitive.</summary>
        public string NormalizedKind =>
            string.IsNullOrEmpty(Kind) ? string.Empty : Kind.Trim().ToLowerInvariant();

        /// <summary>Firings needed to satisfy the stage (never below 1).</summary>
        public int RequiredCount => Count > 1 ? Count : 1;

        /// <summary>
        /// The TutorialSignals bus id this condition waits for, or null when the kind
        /// is not bus-driven (flag = polled through QuestService.HasFlag;
        /// dialogueCommand = the dialogue calls AdvanceQuest itself) or unrecognised.
        /// </summary>
        public string ToSignalId()
        {
            string target = string.IsNullOrEmpty(TargetId) ? string.Empty : TargetId.Trim();
            switch (NormalizedKind)
            {
                case KindTalk:
                    return string.IsNullOrEmpty(target) ? null : DeNelle.Core.Tutorial.TutorialSignals.DialogueEndedPrefix + target;
                case KindWave:
                    return DeNelle.Core.Tutorial.TutorialSignals.WaveCleared;
                case KindBuild:
                    return string.IsNullOrEmpty(target) ? null : DeNelle.Core.Tutorial.TutorialSignals.StructurePlacedPrefix + target;
                case KindPanel:
                    return string.IsNullOrEmpty(target) ? null : DeNelle.Core.Tutorial.TutorialSignals.PanelOpenedPrefix + target;
                case KindArena:
                    return DeNelle.Core.Tutorial.TutorialSignals.ArenaWin;
                case KindReach:
                    return string.IsNullOrEmpty(target) ? null : DeNelle.Core.Tutorial.TutorialSignals.HeroReachedPrefix + target;
                case KindPet:
                    return string.IsNullOrEmpty(target) ? null : PetBondedPrefix + target;
                case KindUpgrade:
                    return string.IsNullOrEmpty(target) ? null : StructureUpgradedPrefix + target;
                case KindPopulation:
                    return string.IsNullOrEmpty(target) ? null : PopulationThresholdPrefix + target;
                case KindRegion:
                    return string.IsNullOrEmpty(target) ? null : RegionClearedPrefix + target;
                default:
                    return null;   // flag / dialogueCommand / unknown -- not bus-driven
            }
        }

        /// <summary>
        /// True when something in the shipped build actually Raises this kind's signal.
        /// The remaining false rows are Silo E / WO-827 work; the oracle reports a stage
        /// authored against them as unreachable rather than scoring it completable.
        /// KindPet moved into the live group when PetAcquisitionService.Acquire started
        /// raising TutorialSignals.PetBondedPrefix (WO-854 final wave) -- leaving it
        /// false would make the bridge log a working stage as "armed but unreachable".
        /// </summary>
        public static bool IsEmitterLive(string kind)
        {
            string k = string.IsNullOrEmpty(kind) ? string.Empty : kind.Trim().ToLowerInvariant();
            switch (k)
            {
                case KindTalk:
                case KindWave:
                case KindBuild:
                case KindPanel:
                case KindArena:
                case KindReach:
                case KindFlag:
                case KindDialogueCommand:
                case KindPet:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Key this condition's progress is counted under inside QuestState.Counters.
        /// Scoped by stage id so a later stage of the same quest never inherits an
        /// earlier stage's tally.
        /// </summary>
        public string CounterKey(string stageId) =>
            (string.IsNullOrEmpty(stageId) ? "?" : stageId) + "#" + NormalizedKind + ":" +
            (string.IsNullOrEmpty(TargetId) ? string.Empty : TargetId.Trim());
    }

    /// <summary>One ordered step of a quest.</summary>
    [Serializable]
    public sealed class QuestStage
    {
        [JsonProperty("stageId")] public string StageId;
        [JsonProperty("objectiveText")] public string ObjectiveText;
        [JsonProperty("reward")] public QuestReward Reward;
        [JsonProperty("requiresFlag")] public string RequiresFlag;
        [JsonProperty("grantsKeystone")] public bool GrantsKeystone;
        // WO-854 Phase 2: how the player finishes this stage. Absent/null keeps the
        // legacy behaviour (a dialogue must author an explicit AdvanceQuest verb), so
        // this is purely additive over the quests.json that shipped at version 2.
        [JsonProperty("completeOn")] public QuestCompletion CompleteOn;
    }

    /// <summary>A complete quest definition (a stage chain).</summary>
    [Serializable]
    public sealed class QuestDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("title")] public string Title;
        // WO-454 Phase 2: quest category/source — a free string parsed case-insensitively
        // (e.g. "main"/"story"/"side"/"gear"/"endgame"). Empty/null = a normal Story/Side
        // quest. Drives the board's tab filter + the HUD pin's type-aware fallback. No enum
        // churn — the board normalizes (NormalizedType) so unknown values fall back to "story".
        [JsonProperty("type")] public string Type;
        // The quest that must be COMPLETED before this one may be accepted. Absent, null or
        // empty means no prerequisite, so every quest that shipped before this field behaves
        // exactly as it did. The field name mirrors the one gear-recipes.json already ships for
        // the same concept, so one idea keeps one word across the codebase.
        //
        // Catalog content, NOT the persisted contract: the save serializes QuestState /
        // QuestProgress (Core/State/NestedTypes.cs:218-226), never a QuestDef, so adding a
        // field here needs no save-schema bump -- same precedent as QuestState.stageId there.
        //
        // Honoured by RumorBoardVM.Rebuild (a quest whose prerequisite is unfinished never
        // enters the Available list) and by RumorBoardVM.Accept (which refuses to start one).
        [JsonProperty("requiresQuestId")] public string RequiresQuestId;
        [JsonProperty("stages")] public List<QuestStage> Stages = new List<QuestStage>();
    }

    [Serializable]
    public sealed class QuestCatalogData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("quests")] public List<QuestDef> Quests = new List<QuestDef>();
    }

    // ── Loader ───────────────────────────────────────────────────────────────

    /// <summary>Static surface over StreamingAssets/Data/Canonical/quests.json.</summary>
    public static class QuestCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/quests.json";

        private static QuestCatalogData _data;

        public static IReadOnlyList<QuestDef> Quests
        { get { EnsureLoaded(); return _data.Quests; } }

        public static QuestDef FindQuest(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var q in _data.Quests) if (q != null && q.Id == id) return q;
            return null;
        }

        /// <summary>Stage list for a quest id (empty list if unknown).</summary>
        public static IReadOnlyList<QuestStage> Stages(string id)
        {
            var q = FindQuest(id);
            return q != null && q.Stages != null ? q.Stages : new List<QuestStage>();
        }

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            // WebGL-safe: CanonicalJson reads the Resources dual-copy first (works in
            // a browser build) and falls back to StreamingAssets on desktop. Raw
            // File.ReadAllText would throw in WebGL → empty quest list.
            DeNelle.Core.Diagnostics.FlowTrace.Step("QuestCat", "EnsureLoaded — reading quests.json.");
            try
            {
                string text = CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(text))
                {
                    var parsed = JsonConvert.DeserializeObject<QuestCatalogData>(text);
                    if (parsed != null && parsed.Quests != null && parsed.Quests.Count > 0)
                    {
                        DeNelle.Core.Diagnostics.FlowTrace.Step("QuestCat", $"loaded {parsed.Quests.Count} quest(s) (v{parsed.Version}).");
                        _data = parsed; return;
                    }
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("QuestCat", "quests.json parsed EMPTY (json present but 0 quests — mapping break) -> empty catalog.");
                    Debug.LogError("[QuestCatalog] quests.json parsed empty.");
                }
                else
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("QuestCat", $"quests.json not found/empty ({StreamingRelativePath}) -> empty catalog.");
                    Debug.LogError($"[QuestCatalog] quests.json not found ({StreamingRelativePath}).");
                }
            }
            catch (Exception ex)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("QuestCat", $"read/parse quests.json threw {ex.GetType().Name}: {ex.Message} -> empty catalog.");
                Debug.LogError($"[QuestCatalog] Failed to read quests.json: {ex.Message}");
            }
            _data = new QuestCatalogData();
        }
    }
}
