// =============================================================================
// DailyQuestRewardBridge — WO-564. The SINGLE Village-side dispenser that pays
// out a completed daily quest's slot reward.
// -----------------------------------------------------------------------------
// DeNelle.Core (where DailyQuestService lives) cannot reference the Village
// wallet / currency services, so the grant happens here, on the other side of the
// DailyQuestService.QuestCompleted event (the same cross-assembly pattern as
// QuestRewardBridge / DailyQuestGateBridge).
//
// WHY A DEDICATED BRIDGE (WO-564): reward dispense was previously bolted onto
// DailyQuestTowerBridge, which (a) only granted crystals/wisdom/glimmer — it
// silently DROPPED the schema's rewardFood (exploration slot = 20 food) and
// rewardRandomItem (wildcard slot) — and (b) mixed an unrelated tower-placement
// hook into a "reward" responsibility. This bridge owns the WHOLE reward schema
// in ONE place; DailyQuestTowerBridge now does only its tower-placement tick.
// Exactly ONE listener subscribes to QuestCompleted, so the ClaimedAtUnix latch
// is never raced by two dispensers.
//
// DATA-DRIVEN: every amount comes from DailyQuestCatalog.RewardFor(slot) (the
// daily-quests.json `slots` block) — nothing is hardcoded per quest. The reward
// schema (DailyQuestSlotReward) is: rewardCrystals, rewardFood, rewardGlimmer,
// rewardWisdom, rewardRandomItem.
//
// Grant routes (all the canonical earn sites):
//   crystals  -> GameStateService.AddCrystals (Resources.Crystals wallet)
//   food      -> GameStateService.AddFood      (Resources.Food wallet)
//   glimmer   -> GlimmerCurrencyService.TryAddGlimmer (cosmetic currency)
//   wisdom    -> WisdomCurrencyService.Grant         (talent currency)
//   item      -> VillageInventory.Add  (persisted larder/gear store -> GameState.GearInventory)
//
// Self-bootstraps via RuntimeInitializeOnLoadMethod into a DontDestroyOnLoad
// object. Village -> Core only; all cross-calls are null-conditional. Each grant
// emits a [Flow:Economy] line (CLAUDE.md §12) so a headless run proves payout.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Quests;
using DeNelle.Core.State;
using DeNelle.Cosmetics;
using DeNelle.Village.Crafting;
using DeNelle.Village.Items;
using DeNelle.Village.Talents;
using UnityEngine;

namespace DeNelle.Village.Quests
{
    [DisallowMultipleComponent]
    public sealed class DailyQuestRewardBridge : MonoBehaviour
    {
        private static DailyQuestRewardBridge _instance;
        private bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("DailyQuestRewardBridge");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<DailyQuestRewardBridge>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            Hook();
        }

        private void OnEnable() => Hook();

        private void Update()
        {
            // DailyQuestService self-bootstraps too; if it wasn't up yet at Awake,
            // keep retrying cheaply until the subscription lands.
            if (!_hooked) Hook();
        }

        private void OnDisable() => Unhook();

        private void OnDestroy()
        {
            Unhook();
            if (_instance == this) _instance = null;
        }

        private void Hook()
        {
            if (_hooked) return;
            var svc = DailyQuestService.Instance;
            if (svc == null) return;
            svc.QuestCompleted += HandleQuestCompleted;
            _hooked = true;
            FlowTrace.Step("Economy", "DailyQuestRewardBridge subscribed to QuestCompleted");
        }

        private void Unhook()
        {
            if (!_hooked) return;
            var svc = DailyQuestService.Instance;
            if (svc != null) svc.QuestCompleted -= HandleQuestCompleted;
            _hooked = false;
        }

        // ── Reward dispense ────────────────────────────────────────────────────

        private void HandleQuestCompleted(DailyQuestInstance q)
        {
            if (q == null || q.ClaimedAtUnix != 0) return; // already paid out
            var reward = DailyQuestCatalog.RewardFor(q.Slot);
            if (reward == null)
            {
                FlowTrace.Warn("Economy", $"DailyQuest '{q.TemplateId}' complete but no reward row for slot '{q.Slot}'");
                return;
            }

            // Latch FIRST so any re-fire (a later Repaint / double event) cannot
            // double-grant, even if a grant call below early-returns.
            q.ClaimedAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Crystals -> the canonical resource wallet (the same store the build
            // menu reads/spends). AddCrystals clamps, persists, raises ResourcesChanged.
            if (reward.RewardCrystals > 0)
            {
                GameStateService.Instance?.AddCrystals(reward.RewardCrystals);
                FlowTrace.Step("Economy", $"DailyQuest '{q.TemplateId}' granted {reward.RewardCrystals} crystals");
            }

            // Food -> the food wallet (Resources.Food).
            // WO-857 Phase F: a daily-quest payout is EARNED income, the same category as the story
            // quest rewards QuestRewardBridge already routes through EconomyService.Grant, so it is
            // subject to the town bank cap (clamp-and-warn, owner ruling WO-901 §5). It used to call
            // GameStateService.AddFood directly, which is BELOW the cap seam - an unclamped back door
            // that would have let dailies alone push food past a full bank with no warn. AddFood stays
            // as the no-EconomyService fallback only (EditMode / headless boots).
            if (reward.RewardFood > 0)
            {
                var eco = EconomyService.Instance;
                if (eco != null) eco.Grant(food: reward.RewardFood);
                else GameStateService.Instance?.AddFood(reward.RewardFood);
                FlowTrace.Step("Economy", $"DailyQuest '{q.TemplateId}' granted {reward.RewardFood} food");
            }

            // Wisdom (WO-763, owner 2026-07-25): dailies NO LONGER pay Wisdom — Wisdom is
            // a LEVEL-UP reward only so new skills/magic feel EARNED, not handed out. The
            // daily's value is PRESERVED by redirecting the RewardWisdom amount into
            // crystals (the canonical wallet) rather than dropping it, so dailies stay
            // worth claiming without cheapening the skill economy.
            if (reward.RewardWisdom > 0)
            {
                GameStateService.Instance?.AddCrystals(reward.RewardWisdom);
                FlowTrace.Step("Economy", $"DailyQuest '{q.TemplateId}' redirected {reward.RewardWisdom} wisdom -> crystals (WO-763)");
            }

            // Glimmer -> the cosmetic-shop currency. A steady, non-grindy trickle.
            if (reward.RewardGlimmer > 0)
            {
                GlimmerCurrencyService.Instance?.TryAddGlimmer(reward.RewardGlimmer);
                FlowTrace.Step("Economy", $"DailyQuest '{q.TemplateId}' granted {reward.RewardGlimmer} glimmer");
            }

            // Random item -> roll a consumable from the catalog and grant it into the
            // persisted larder (VillageInventory.Add -> GameState.GearInventory). The
            // pool is the data-driven ConsumableCatalog, not a hardcoded id.
            if (reward.RewardRandomItem)
                GrantRandomItem(q);
        }

        private void GrantRandomItem(DailyQuestInstance q)
        {
            var pool = ConsumableCatalog.All;
            if (pool == null || pool.Count == 0)
            {
                FlowTrace.Warn("Economy", $"DailyQuest '{q.TemplateId}' random-item reward skipped — empty consumable catalog");
                return;
            }

            // Deterministic-enough roll; no need for a seeded RNG for a cosmetic drop.
            var def = pool[Random.Range(0, pool.Count)];
            if (def == null || string.IsNullOrEmpty(def.Id))
            {
                FlowTrace.Warn("Economy", $"DailyQuest '{q.TemplateId}' random-item reward skipped — null catalog entry");
                return;
            }

            var inv = VillageInventory.Instance;
            if (inv == null)
            {
                FlowTrace.Warn("Economy", $"DailyQuest '{q.TemplateId}' random-item '{def.Id}' lost — VillageInventory not ready");
                return;
            }

            inv.Add(def.Id, 1);
            FlowTrace.Step("Economy", $"DailyQuest '{q.TemplateId}' granted random item '{def.Id}' x1");
        }
    }
}
