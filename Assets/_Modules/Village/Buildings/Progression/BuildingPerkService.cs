// =============================================================================
// BuildingPerkService — research a building perk with Gold, over TIME (WO-432,
// made time-based by the owner ruling of 2026-08-07).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// The WC3 "research at the Blacksmith" pillar: numerical upgrades (damage/armor
// Lvl 1/2/3) + creative-owned ability unlocks, bought with GOLD (economy Coins),
// GATED by the building's tier AND the Village/Stronghold Tier.
//
// ⚠ WHAT CHANGED (owner ruling 2026-08-07 — "building perk research must be
//   TIME-BASED, like Warcraft 3"). This service used to spend the gold and add the
//   perk to GameState.OwnedBuildingPerks IN THE SAME BREATH — an instant purchase
//   wearing the word "research". It now ENQUEUES a JobKind.BuildingResearch job on
//   the shared Obsidian RESEARCH channel and the perk lands at COMPLETION, applied
//   by <see cref="BuildingResearchEffect"/>. Nothing about the gate, the price or
//   the modifier compile changed; only WHEN the perk is granted.
//
//   The model copied verbatim is BarracksService.UpgradeTroop — including its
//   ORDERING DISCIPLINE, which exists because of a real bug:
//     1. gate check,
//     2. resolve cost + duration,
//     3. VERIFY THE QUEUE EXISTS **BEFORE** THE SPEND (the old order charged and
//        then returned on a null queue — a charge-loss window),
//     4. spend,
//     5. enqueue; if the enqueue is REFUSED (the WO-911 Q4 depth cap can refuse
//        AFTER the charge landed) REFUND the charge rather than eat it.
//
// ⚠ KNOWN GAP — A CANCEL DOES NOT REFUND THE GOLD (flagged, not worked around).
//   WO-911's 100%-flat cancel refund rides BuildJobData.Paid, a JobCost of
//   wood/food/iron/crystals/magic. It has NO COINS LANE (checked at source:
//   Core/State/BuildJobData.cs), and BuildTimerService.ToJobCost(Village.
//   ResourceCost) explicitly warns that coins "are not part of the refundable
//   basket ... a cancel will not return them". Research is the ONLY gold-priced
//   job in the game, so it is the only kind this bites. Closing it means adding a
//   Coins lane to JobCost + a paidCoins field on BuildJobData (save schema v38)
//   and a coin credit in CancelChannelJobWithRefund — a schema decision that is
//   the owner's to make, so it is NOT smuggled in here. Until then the basket is
//   still recorded (all-zero) and the refusal path above refunds the gold
//   DIRECTLY via EconomyService.AddCoins, which is the case that actually loses
//   money silently.
//
// Pure static surface (mirrors BuildingUpgradeService) — the panel calls
// TryResearch; the VM reads IsOwned / IsResearching / CanResearch.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>Start/own/query building research perks. The Gold-cost, TIMED research layer over the tier ladder.</summary>
    public static class BuildingPerkService
    {
        /// <summary>FlowTrace system tag for every line this service emits.</summary>
        private const string Sys = "Research";

        /// <summary>
        /// Obsidian job-id prefix for a perk research job: <c>building-research:&lt;buildingId&gt;:&lt;perkId&gt;</c>.
        /// Building ids and perk ids both use hyphens and NEVER colons (verified against
        /// building-tiers.json), so a plain <c>Split(':')</c> round-trips the pair.
        /// </summary>
        public const string ResearchJobPrefix = "building-research:";

        /// <summary>The persisted owned-perks key for (building, perk).</summary>
        public static string Key(string buildingId, string perkId) => buildingId + ":" + perkId;

        /// <summary>The Obsidian job id for (building, perk) — the key the Research channel is addressed by.</summary>
        public static string JobId(string buildingId, string perkId) => ResearchJobPrefix + buildingId + ":" + perkId;

        /// <summary>The building id carried by a research job id, or null when it is not one.</summary>
        public static string BuildingIdFromJob(string jobId)
        {
            if (string.IsNullOrEmpty(jobId) || !jobId.StartsWith(ResearchJobPrefix)) return null;
            var parts = jobId.Split(':');
            return parts.Length >= 3 ? parts[1] : null;
        }

        /// <summary>The perk id carried by a research job id, or null when it is not one.</summary>
        public static string PerkIdFromJob(string jobId)
        {
            if (string.IsNullOrEmpty(jobId) || !jobId.StartsWith(ResearchJobPrefix)) return null;
            var parts = jobId.Split(':');
            return parts.Length >= 3 ? parts[2] : null;
        }

        /// <summary>
        /// The player-facing name for a research job id ("Arcane Basics"), or null when the id is
        /// not a research job. Lives HERE, not in the HUD, because a View reading
        /// <see cref="BuildingTierCatalog"/> directly is an MVVM conformance violation - the
        /// oracle caught exactly that and it is the right call: the queue HUD should ask a service
        /// what a job is called, never resolve catalog rows itself.
        /// Falls back to a spaced perk id so a row can never leak a raw job id at the player.
        /// </summary>
        public static string DisplayNameForJob(string jobId)
        {
            string bId = BuildingIdFromJob(jobId);
            string pId = PerkIdFromJob(jobId);
            if (string.IsNullOrEmpty(pId)) return null;

            var perk = BuildingTierCatalog.FindPerk(bId, pId);
            if (perk != null && !string.IsNullOrEmpty(perk.Name)) return perk.Name;

            // Spaced fallback: "arcane_basics" / "arcane-basics" -> "arcane basics".
            return pId.Replace('_', ' ').Replace('-', ' ');
        }

        /// <summary>True if the player has already researched this perk.</summary>
        public static bool IsOwned(string buildingId, string perkId)
        {
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return s != null && s.OwnedBuildingPerks != null && s.OwnedBuildingPerks.Contains(Key(buildingId, perkId));
        }

        /// <summary>
        /// True while this perk's research job is running OR queued on the Research channel — the
        /// double-research guard (mirrors <c>BarracksService.IsUpgradingTroop</c>). False with no
        /// live queue service, which is the correct answer for a headless/pre-boot read.
        /// </summary>
        public static bool IsResearching(string buildingId, string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return false;
            var queue = BuildTimerService.Instance;
            if (queue == null) return false;

            string id = JobId(buildingId, perkId);
            foreach (var j in queue.ActiveJobsOf(ChannelId.Research)) if (j.StructureId == id) return true;
            foreach (var j in queue.PendingJobsOf(ChannelId.Research)) if (j.StructureId == id) return true;
            return false;
        }

        /// <summary>
        /// Wall-clock seconds this perk's research takes. Derived from the perk's authored
        /// <c>goldCost</c> via <see cref="DeNelle.Core.Catalog.BuildTimerConfig.ResearchSecondsForGold"/>
        /// — the tunable curve, NOT a magic number at the call site — because building-tiers.json
        /// carries no per-perk duration field (see that config's block comment). 0 for an unknown perk.
        /// <para>
        /// THE ONE PLACE TO CHANGE if a <c>researchSeconds</c> field is ever authored per perk:
        /// prefer the authored value here and let the gold curve stay as the fallback.
        /// </para>
        /// </summary>
        public static float ResearchSeconds(string buildingId, string perkId)
        {
            var perk = BuildingTierCatalog.FindPerk(buildingId, perkId);
            return perk == null ? 0f : ResearchSecondsOf(perk);
        }

        private static float ResearchSecondsOf(BuildingPerkDef perk)
        {
            var queue = BuildTimerService.Instance;
            var cfg = queue != null ? queue.Config : null;
            if (cfg == null) cfg = DeNelle.Core.Catalog.BuildTimerConfig.CreateDefault();
            return cfg.ResearchSecondsForGold(perk != null ? perk.GoldCost : 0);
        }

        /// <summary>
        /// Whether this perk can be STARTED right now. Gates: it exists, isn't owned, isn't already
        /// being researched, the building has reached the perk's unlock BUILDING tier, AND the Village/
        /// Stronghold Tier meets the VILLAGE tier that perk's own tier row authors.
        /// <paramref name="reason"/> returns a player-facing ASCII lock line for the View.
        /// <para>⚠ WO-1423 — TWO SCALES, TWO ACCESSORS. This doc used to say the Village Tier had to
        /// "meet that tier", and the code did exactly that: it compared the BUILDING tier number to
        /// <c>VillageTierService.Current</c>. Every ladder's tier-1 row authors
        /// <c>requiresVillageTier: 0</c>, so on a fresh save the first perk of every ladder was locked
        /// behind a village gate nobody authored — the owner's "locked till village level 1, which
        /// there is no way to trigger". The village half now reads
        /// <c>BuildingTierCatalog.PerkRequiredVillageTier</c>, the same authored field the tier UPGRADE
        /// gate uses.</para>
        /// <para>
        /// Deliberately does NOT gate on affordability — that stayed the caller's own check
        /// (BuildingUpgradeVM tints the tile from <c>_economy.Coins</c>; the Manage screen states the
        /// shortfall in words) and folding it in here would silently turn every unaffordable perk
        /// tile from "affordable=false" into "locked", which is a different visual contract.
        /// </para>
        /// </summary>
        public static bool CanResearch(string buildingId, string perkId, out string reason)
        {
            reason = null;
            var perk = BuildingTierCatalog.FindPerk(buildingId, perkId);
            if (perk == null) { reason = "Unknown research."; return false; }
            if (IsOwned(buildingId, perkId)) { reason = "Researched."; return false; }
            // TIMED research (2026-08-07): one job per perk. Without this a player could pay N times
            // over and stack N identical jobs, and the engine's own id guard would only refuse the
            // SECOND one AFTER the charge had already landed.
            if (IsResearching(buildingId, perkId)) { reason = "Research already in progress."; return false; }

            int unlock = BuildingTierCatalog.PerkUnlockTier(buildingId, perkId);
            if (ModifierService.TierOf(buildingId) < unlock) { reason = "Upgrade the building to Tier " + unlock + " first."; return false; }
            // WO-1423 — the VILLAGE gate is the perk's own tier row's requiresVillageTier, NEVER the
            // building tier number. The sentence shape is unchanged (a suite asserts LockReason equals
            // this string verbatim); only the number is now on the right scale.
            int villageGate = BuildingTierCatalog.PerkRequiredVillageTier(buildingId, perkId);
            if (VillageTierService.Current < villageGate) { reason = "Locked - needs Village Tier " + villageGate + "."; return false; }
            return true;
        }

        /// <summary>
        /// START the perk's research: spend its Gold (economy Coins) and ENQUEUE a
        /// <see cref="JobKind.BuildingResearch"/> job on the Research channel. The perk itself is
        /// granted at COMPLETION by <see cref="BuildingResearchEffect"/> — this method NEVER touches
        /// OwnedBuildingPerks any more. Returns false (nothing spent, nothing queued) when
        /// <see cref="CanResearch"/> refuses, when there is no queue service, or when the spend fails;
        /// an enqueue refused AFTER the spend refunds the gold.
        /// </summary>
        public static bool TryResearch(string buildingId, string perkId)
        {
            using var _ = FlowTrace.Enter(Sys, $"TryResearch '{buildingId}:{perkId}'");

            if (!CanResearch(buildingId, perkId, out string reason))
            {
                FlowTrace.Warn(Sys, $"research '{buildingId}:{perkId}' refused: {reason}");
                return false;
            }

            var perk = BuildingTierCatalog.FindPerk(buildingId, perkId);
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (perk == null || s == null)
            {
                FlowTrace.Fail(Sys, $"research '{buildingId}:{perkId}': perk={(perk == null ? "NULL" : "ok")} " +
                                    $"state={(s == null ? "NULL" : "ok")} - nothing charged.");
                return false;
            }

            int gold = Mathf.Max(0, perk.GoldCost);
            float seconds = ResearchSecondsOf(perk);
            FlowTrace.Step(Sys, $"research '{buildingId}:{perkId}' resolved: {gold} gold, {seconds:0}s.");

            // ⚠ QUEUE CHECK **BEFORE** THE SPEND. BarracksService learned this the hard way: the
            // old order committed the charge and THEN returned on a null queue, losing the
            // player's resources with no trace. Do not reorder these two blocks.
            var queue = BuildTimerService.Instance;
            if (queue == null)
            {
                FlowTrace.Warn(Sys, $"research '{buildingId}:{perkId}': no BuildTimerService (nothing charged).");
                return false;
            }

            var cost = new DeNelle.Village.ResourceCost { Coins = gold };
            if (gold > 0)
            {
                var econ = EconomyService.Instance;
                if (econ == null)
                {
                    FlowTrace.Warn(Sys, $"research '{buildingId}:{perkId}': no EconomyService (nothing charged).");
                    return false;
                }
                if (!econ.TrySpend(cost))
                {
                    FlowTrace.Warn(Sys, $"research '{buildingId}:{perkId}': spend failed - need {gold} gold, have {econ.Coins}.");
                    return false;
                }
                FlowTrace.Step(Sys, $"research '{buildingId}:{perkId}': charged {gold} gold.");
            }

            // The recorded basket is all-zero for a gold-only job (JobCost has no coins lane — see
            // the file header's KNOWN GAP). ToJobCost is still the ONE adapter used, so the moment a
            // coins lane exists this call starts carrying it with no change here, and the adapter's
            // own Warn keeps the gap visible in the trace instead of silent.
            string jobId = JobId(buildingId, perkId);
            if (queue.Enqueue(JobKind.BuildingResearch, jobId, seconds, 0,
                              BuildTimerService.ToJobCost(cost)) == null)
            {
                RefundGold(gold);
                FlowTrace.Warn(Sys, $"research '{buildingId}:{perkId}' enqueue refused " +
                                    $"({queue.LastEnqueueFailure ?? "unknown"}) - {gold} gold refunded.");
                return false;
            }

            FlowTrace.Step(Sys, $"research '{buildingId}:{perkId}' ENQUEUED jobId={jobId} (Research, {seconds:0}s, {gold} gold).");
            return true;
        }

        /// <summary>
        /// Hand the gold straight back when the ENQUEUE that followed the spend was refused (the
        /// WO-911 Q4 depth cap can refuse AFTER the charge has landed). Credits the same
        /// GameState.Resources.Coins store <see cref="EconomyService.TrySpend"/> debited.
        /// </summary>
        private static void RefundGold(int gold)
        {
            if (gold <= 0) return;
            var econ = EconomyService.Instance;
            if (econ == null)
            {
                // NO SILENT FAILURE (CLAUDE.md section 12.2): the player is out `gold` and there is
                // nothing to credit it to. Say so loudly rather than returning as if refunded.
                FlowTrace.Fail(Sys, $"refund of {gold} gold LOST - EconomyService vanished between the spend and the refund.");
                return;
            }
            econ.AddCoins(gold);
            FlowTrace.Step(Sys, $"refunded {gold} gold (enqueue refused).");
        }

        // ── Completion effect (registered once with the shared JobEffectRegistry) ──

        /// <summary>
        /// BuildingResearch job complete -> record the perk in GameState.OwnedBuildingPerks, persist,
        /// recompute the active GameModifiers. This is EXACTLY what the old instant TryResearch did
        /// inline; it simply happens when the timer lands now. Reached from
        /// BuildTimerService.OnJobCompleted via JobEffectRegistry.Apply (live expiry, paid finish,
        /// ad skip, or the offline sweep), and Guard-wrapped by the registry.
        /// </summary>
        private sealed class BuildingResearchEffect : IJobEffect
        {
            public JobKind Kind => JobKind.BuildingResearch;

            public void Apply(BuildJobData job)
            {
                using var _ = FlowTrace.Enter(Sys, $"BuildingResearchEffect.Apply '{job.StructureId}'");

                string buildingId = BuildingIdFromJob(job.StructureId);
                string perkId = PerkIdFromJob(job.StructureId);
                if (string.IsNullOrEmpty(buildingId) || string.IsNullOrEmpty(perkId))
                {
                    FlowTrace.Fail(Sys, $"research job id '{job.StructureId}' does not parse as " +
                                        $"'{ResearchJobPrefix}<building>:<perk>' - the perk was NOT granted.");
                    return;
                }

                var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                if (s == null)
                {
                    FlowTrace.Fail(Sys, $"research '{buildingId}:{perkId}' completed with NO GameState - the perk is lost.");
                    return;
                }
                if (s.OwnedBuildingPerks == null) s.OwnedBuildingPerks = new System.Collections.Generic.List<string>();

                string key = Key(buildingId, perkId);
                if (s.OwnedBuildingPerks.Contains(key))
                {
                    // Idempotent: an offline sweep plus a live expiry could both route the same job.
                    FlowTrace.Warn(Sys, $"research '{key}' completed but was ALREADY owned - no double-add.");
                    return;
                }

                s.OwnedBuildingPerks.Add(key);
                GameStateService.Instance.Save();
                ModifierService.Recompute();
                FlowTrace.Step(Sys, $"research COMPLETE -> perk '{key}' granted, state saved, modifiers recomputed.");
            }
        }

        // Register the completion effect once at startup (idempotent; re-registered on domain reload,
        // exactly like BarracksService.RegisterEffects). Without this the job would finish and
        // silently grant nothing — an unregistered kind is a no-op by design in JobEffectRegistry.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterEffects()
        {
            JobEffectRegistry.Register(new BuildingResearchEffect());
            FlowTrace.Step(Sys, "BuildingResearch job effect registered (timed WC3 perk research).");
        }
    }
}
