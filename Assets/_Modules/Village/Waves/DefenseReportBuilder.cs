// =============================================================================
// DefenseReportBuilder — the ADAPTER between the live town and the persisted report.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village   (WO-1026)
//
// Everything that translates LIVE Village objects into Core.Defense DATA lives here,
// and only here:
//   WaveDamageReport.Entry[]  -> StructureOutcome[]
//   GameState.BaseLayout      -> DefenderSnapshot + LayoutHash
//   a wave ordinal            -> AttackerIdentity        (model (a))
//   a settled record          -> StakesLedger            (THE RULED LOSS, §BuildStakes/§ApplyStakes)
//
// ⛔ THIS IS THE ONLY FILE THAT WRITES AttackerSource.GeneratedPve.
//    That is the "do not hardcode that the attacker is generated" ruling made concrete:
//    every READER branches on the record's Source field (or on nothing), so the day a
//    ghost-snapshot producer exists it writes GhostSnapshot here and NOTHING downstream
//    changes. SiegeSpawnAuthorityRegression fails the gate if a second file writes it.
//
// NOTHING IN HERE RE-AGGREGATES DAMAGE. WaveDamageReport.Collect() already enumerates
// every damaged/destroyed player structure worst-first and priced, Guard-wrapped, capped
// and trace-truncated. We serialise its output. Re-scanning would be a second aggregator
// that drifts from the one the wave-clear banner shows — the player would then read two
// different accounts of the same attack.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Defense;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Economy;
using DeNelle.Core.State;
using DeNelle.Village.Buildings.Progression;   // ResourceCollector(+Registry) — the ONE theft in the game
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Live-town → persisted-report adapters. Pure translation; no scanning of its own.</summary>
    public static class DefenseReportBuilder
    {
        // =====================================================================
        //  Rows — a 1:1 adapt of the EXISTING aggregate
        // =====================================================================

        /// <summary>
        /// Adapts <see cref="WaveDamageReport.Entry"/> rows into <see cref="StructureOutcome"/>s,
        /// appending into <paramref name="into"/>. Guard.TryEach per row, so one malformed entry is
        /// skipped and logged rather than losing the whole report.
        /// </summary>
        public static void AdaptRows(List<WaveDamageReport.Entry> entries, List<StructureOutcome> into)
        {
            if (into == null) return;
            if (entries == null || entries.Count == 0) return;

            var built = Guard.TryEach("Siege", "adapt damage entry", entries, e =>
            {
                if (e == null) return;
                into.Add(new StructureOutcome
                {
                    DisplayName = string.IsNullOrEmpty(e.Name) ? "Structure" : e.Name,
                    DamageFraction = Mathf.Clamp01(e.DamageFraction),
                    // ONE source of truth for the row's condition — StructureOutcome.Destroyed
                    // is derived from this, never stored beside it, so they cannot disagree.
                    // (StructureState.Held is unreachable here by design: WaveDamageReport only
                    // emits rows for structures that were actually damaged. See that enum's doc.)
                    State = e.Destroyed ? StructureState.Destroyed : StructureState.Damaged,
                    IsCollector = e.IsCollector,
                    LootStolen = Mathf.Max(0, e.LootStolen),
                    // Cost is OMITTED, never faked — HasCost carries that, exactly as the
                    // WaveDamageReport contract already does for the live banner.
                    HasCost = e.HasCost,
                    RepairWood = e.HasCost ? Mathf.Max(0, e.RepairCost.wood) : 0,
                    RepairIron = e.HasCost ? Mathf.Max(0, e.RepairCost.iron) : 0,
                    RepairFood = e.HasCost ? Mathf.Max(0, e.RepairCost.food) : 0,
                    RepairCrystals = e.HasCost ? Mathf.Max(0, e.RepairCost.crystals) : 0,
                });
            });

            if (built.failed > 0)
                FlowTrace.Warn("Siege", $"adapt losses: {built.failed} of {entries.Count} rows skipped (see Guard lines).");
        }

        // =====================================================================
        //  ⭐ LEGIBILITY — the merge that turns a loss LIST into a DIAGNOSIS
        // =====================================================================

        /// <summary>
        /// Stamps position, defence BAND and HOLD TIME onto rows that already exist.
        ///
        /// <para>The owner's bar for this feature is felt, not structural:
        /// <i>"Does losing feel like it was my fault, and do I know what to change?"</i>
        /// A row that says "Wall B destroyed" fails that bar. The same row saying
        /// "your east wall fell in 4s, front line" passes it, because the player can act on it.
        /// That is the entire job of this method.</para>
        ///
        /// <para>⛔ MERGE ONLY. It adds no row, drops no row and re-prices nothing —
        /// <c>WaveDamageReport</c> remains the single authority on WHAT was damaged. Rows the
        /// watch never saw keep <c>HoldTimeSeconds = -1</c> (UNKNOWN), which the panel renders as an
        /// honest blank. It never renders as "fell in 0s": a fabricated hold time would send the
        /// player to move the wrong structure, which is worse than telling them nothing.</para>
        /// </summary>
        public static void StampLegibility(DefenseOutcomeRecord record, StructureVitalsWatch vitals)
        {
            if (record == null) return;

            Guard.Try("Siege", "stamp report legibility", () =>
            {
                float assault = Mathf.Max(0f, record.DurationSeconds);
                float coreR = record.Defender.CoreRadius;
                float frontR = record.Defender.FrontRadius;

                int timed = 0, unknown = 0;
                for (int i = 0; i < record.Rows.Count; i++)
                {
                    var l = record.Rows[i];
                    if (l == null) continue;

                    l.HoldTimeSeconds = -1f;
                    l.FirstHitAtSeconds = -1f;
                    l.FellAtSeconds = -1f;

                    if (vitals == null) { unknown++; l.Band = DefenseBand.Second; continue; }

                    var t = vitals.Resolve(l.DisplayName, assault);
                    if (!t.Found) { unknown++; l.Band = DefenseBand.Second; continue; }

                    l.WorldX = t.Position.x;
                    l.WorldZ = t.Position.z;
                    l.DistanceFromCore = t.DistanceFromCore;
                    l.Band = ClassifyBand(t.DistanceFromCore, coreR, frontR);
                    l.WasAlreadyDamaged = t.WasAlreadyDamaged;
                    l.FirstHitAtSeconds = t.FirstHitAtSeconds;
                    l.FellAtSeconds = t.FellAtSeconds;
                    l.HoldTimeSeconds = t.HoldTimeSeconds;

                    l.StructureId = t.StructureId;
                    l.StructureType = t.StructureType;
                    l.BreachOrdinal = ResolveBreachOrdinal(record, t.StructureId);

                    if (l.HasHoldTime) timed++; else unknown++;
                }

                // FROZEN here, after every input above is settled. Never recomputed on read.
                record.DefenseScore = ComputeDefenseScore(record);

                FlowTrace.Step("Siege",
                    $"legibility: {timed} row(s) carry a hold time, {unknown} unknown; " +
                    $"bands coreR={coreR:F1}m frontR={frontR:F1}m; path={record.Path.Count} samples; " +
                    $"first breach={(FirstBreach(record) != null ? FirstBreach(record).DisplayName : "none")}.");
            });
        }

        /// <summary>
        /// Which band a distance falls in. Both radii come from the record, so an old report
        /// keeps the classification it was WRITTEN with — recomputing on read would let a
        /// rebuilt town silently rewrite history.
        /// <para>A base with no walls has <c>frontRadius = 0</c>; rather than banding every
        /// structure Front (which would be meaningless), the Front band collapses and rows read
        /// Second/Core. The report then honestly shows a base with no front line.</para>
        /// </summary>
        /// <summary>
        /// Which breach (1-based) happened AT this structure, or 0 if they never crossed here.
        /// Correlated by the scene-instance key both sides already carry.
        /// <para>This separates "the wall they came through" from "a wall that took splash
        /// damage" — identical rows in a flat list, opposite instructions to the player.</para>
        /// </summary>
        public static int ResolveBreachOrdinal(DefenseOutcomeRecord record, string structureId)
        {
            if (record == null || string.IsNullOrEmpty(structureId)) return 0;
            for (int i = 0; i < record.Breaches.Count; i++)
            {
                var b = record.Breaches[i];
                if (b != null && b.BreachedId == structureId) return i + 1;   // breaches are time-ordered
            }
            return 0;
        }

        // =====================================================================
        //  DefenseScore — a LABEL, derived, and honest enough to decline
        // =====================================================================

        // ⚠ PRESENTATION WEIGHTING, NOT GAME BALANCE. Nothing gameplay-facing reads the score:
        // no reward, no matchmaking, no stake, no gate. It exists to put one number on a screen.
        // Keeping it inert is what stops a display weighting quietly becoming an economy rule --
        // the failure the WO-947 / stockpile-cap rulings were expensive to settle.
        private const int ScoreHeld = 100;
        private const int ScoreBreached = 75;
        private const int ScoreOverrun = 40;
        private const int BreachPenaltyEach = 5;
        private const int BreachPenaltyCap = 20;
        private const int DestructionPenaltyMax = 35;

        /// <summary>
        /// 0-100, or <see cref="DefenseOutcomeRecord.NotScored"/> (-1) when the inputs are too
        /// thin to say anything honest.
        ///
        /// <para><b>Derivation:</b>
        /// start from the OUTCOME (Held 100 / Breached 75 / Overrun 40);
        /// subtract 5 per recorded breach, capped at 20;
        /// subtract up to 35 scaled by the FRACTION of the base destroyed
        /// (destroyed rows / StructureCount); clamp 0..100.</para>
        ///
        /// <para><b>⛔ WHEN IT DECLINES:</b> when <c>Defender.StructureCount &lt;= 0</c> — i.e.
        /// there is no census of the base that was defended. Without it the destruction term is
        /// undefined, and the remaining number would be the outcome enum wearing three inputs'
        /// clothes: a confident-looking 75 that actually means "it was breached, and we know
        /// nothing else". Same discipline as an unmeasured hold time printing nothing. The panel
        /// then shows no score rather than a fake one.</para>
        /// </summary>
        public static int ComputeDefenseScore(DefenseOutcomeRecord record)
        {
            if (record == null) return DefenseOutcomeRecord.NotScored;

            int census = record.Defender != null ? record.Defender.StructureCount : 0;
            if (census <= 0)
            {
                FlowTrace.Step("Siege",
                    "defense score DECLINED -- no structure census on the defender snapshot, so the " +
                    "destroyed-fraction term is undefined. Showing no score beats showing a confident one.");
                return DefenseOutcomeRecord.NotScored;
            }

            int score;
            switch (record.Outcome)
            {
                case DefenseOutcome.Overrun: score = ScoreOverrun; break;
                case DefenseOutcome.Breached: score = ScoreBreached; break;
                default: score = ScoreHeld; break;
            }

            score -= Mathf.Min(BreachPenaltyCap, BreachPenaltyEach * record.Breaches.Count);

            int destroyed = 0;
            for (int i = 0; i < record.Rows.Count; i++)
                if (record.Rows[i] != null && record.Rows[i].Destroyed) destroyed++;
            score -= Mathf.RoundToInt(DestructionPenaltyMax * Mathf.Clamp01((float)destroyed / census));

            return Mathf.Clamp(score, 0, 100);
        }

        /// <summary>
        /// The score as a WORD. The number alone is a grade; the word is what makes it readable
        /// at a glance and keeps the plate/list honest under greyscale (nothing here is a colour).
        /// Returns empty when the score declined, so the panel prints nothing.
        /// </summary>
        public static string DefenseScoreWord(int score)
        {
            if (score < 0) return string.Empty;
            if (score >= 90) return "Clean hold";
            if (score >= 70) return "Solid";
            if (score >= 45) return "Shaky";
            return "Bad";
        }

        public static DefenseBand ClassifyBand(float distance, float coreRadius, float frontRadius)
        {
            if (coreRadius > 0f && distance <= coreRadius) return DefenseBand.Core;
            if (frontRadius > 0f && distance >= frontRadius) return DefenseBand.Front;
            return DefenseBand.Second;
        }

        /// <summary>
        /// THE FIRST place they got through, or null if nothing crossed. Breaches are appended
        /// in time order by the observer, so this is Breaches[0] — but it is a NAMED helper
        /// because "the first breach" is the report's headline and every reader must agree on
        /// what it is rather than each indexing the list by hand.
        /// </summary>
        public static BreachRecord FirstBreach(DefenseOutcomeRecord record)
        {
            if (record == null || record.Breaches == null || record.Breaches.Count == 0) return null;
            return record.Breaches[0];
        }

        // =====================================================================
        //  Defender snapshot — what the base looked like at attack time
        // =====================================================================

        /// <summary>
        /// Captures the base as it stands RIGHT NOW. Called before the first spawn: this is the
        /// layout the player is about to be judged on.
        ///
        /// StructureCount + LayoutHash come from the PERSISTED BaseLayout (that is what a model-(c)
        /// snapshot would also be built from, so the hash means the same thing on both sides).
        /// WallCount/TowerCount come from the LIVE SCENE components, deliberately: there is no
        /// structure-ROLE field in the catalog yet (see memory `structure-role-enum-and-format-
        /// normalization` — it is queued, not landed), and guessing a role from an itemId substring
        /// would silently mis-count the day someone renames an id. When the role enum lands, move
        /// these two counts onto it and delete the scene scan.
        /// </summary>
        public static DefenderSnapshot CaptureDefender()
        {
            var snap = new DefenderSnapshot
            {
                LayoutHash = LayoutFingerprint.Empty,
                Garrison = new List<AttackerUnitRecord>(),   // EMPTY today — the WO-430-F seam
            };

            Guard.Try("Siege", "capture defender snapshot", () =>
            {
                var svc = GameStateService.Instance;
                var state = svc != null ? svc.State : null;
                var layout = state != null ? state.BaseLayout : null;

                snap.StructureCount = layout != null ? layout.Count : 0;
                snap.LayoutHash = ComputeLayoutHash(layout);

                snap.WallCount = Object.FindObjectsByType<WallSegment>(FindObjectsSortMode.None).Length;
                snap.TowerCount = Object.FindObjectsByType<Tower>(FindObjectsSortMode.None).Length;
                snap.HeroPresent = Object.FindFirstObjectByType<HeroLocomotion>() != null;
            });

            return snap;
        }

        /// <summary>
        /// The layout fingerprint: one normalised token per placed structure, hashed
        /// order-independently by <see cref="LayoutFingerprint"/>.
        ///
        /// The token carries itemId + cell + yaw + level, so MOVING a structure changes the hash
        /// (which is what makes "a redesign has a visible effect" a data assertion) while merely
        /// re-ordering the save list does not. Public + static so the contract oracle can prove
        /// both halves with no scene.
        /// </summary>
        public static string ComputeLayoutHash(IReadOnlyList<PlacedStructureData> layout)
        {
            if (layout == null || layout.Count == 0) return LayoutFingerprint.Empty;
            var tokens = new List<string>(layout.Count);
            for (int i = 0; i < layout.Count; i++)
            {
                var p = layout[i];
                if (string.IsNullOrEmpty(p.itemId)) continue;
                tokens.Add($"{p.itemId}@{p.cellX},{p.cellZ}:{p.yawSteps}:{p.level}");
            }
            return LayoutFingerprint.Compute(tokens);
        }

        // =====================================================================
        //  Attacker identity — model (a). THE ONLY WRITER OF GeneratedPve.
        // =====================================================================

        /// <summary>Tier bands for the PvE warband's name + id. Cosmetic only — nothing branches
        /// on them, and the panel renders the STRING, never re-derives a name from the source.</summary>
        private static readonly string[] TierNames = { "Hollow Warband", "Hollow Host", "Hollow Legion" };

        /// <summary>
        /// Builds the (a)-model attacker for a given wave ordinal. Units + PowerRating are filled
        /// in at <see cref="SiegeSession.Close"/> from what was ACTUALLY fielded (WO-1113 drips a
        /// roster in slices, so it cannot be known up front).
        /// <para>SnapshotId is EMPTY under (a) and must stay empty — a non-empty snapshot id on a
        /// GeneratedPve record would be a contradiction, and the contract oracle says so.</para>
        /// </summary>
        public static AttackerIdentity BuildPveAttacker(int waveId)
        {
            int tier = waveId >= 20 ? 2 : (waveId >= 10 ? 1 : 0);
            return new AttackerIdentity
            {
                Source = AttackerSource.GeneratedPve,   // ⛔ THE ONLY WRITE OF THIS VALUE IN THE REPO
                AttackerProfileId = $"pve.warband.t{tier + 1}",
                DisplayName = TierNames[tier],
                PowerRating = 0,            // settled at Close from the fielded roster
                SnapshotId = string.Empty,  // EMPTY under (a) — the model-(c) seam, unused today
                Units = new List<AttackerUnitRecord>(),
            };
        }

        // =====================================================================
        //  ★★★ THE SEAM — the loss consequence (WO-1139, ruling 2026-08-22) ★★★
        // =====================================================================
        //
        // ⭐ THE RULING: COLLECTOR LOOTING ONLY. NO BANK THEFT.
        //    "What you have COLLECTED is safe. What is still sitting in the building is at risk."
        //
        // ⛔⛔ NOTHING IN THIS FILE MAY EVER DEBIT THE WALLET FOR A SIEGE. Read that twice.
        //    `ResourceCollector.OnSiegeDestroyed` ALREADY removed the resources — it subtracted
        //    them from its own `_pending` at the moment it broke (WO-664, `RaidLootFraction 0.5`).
        //    A wallet debit here would charge the player a SECOND time for one siege: once in the
        //    collector, once in the bank. That is not a balance question, it is a double-charge.
        //    An earlier pass on this WO did exactly that (a flat 15%-of-banked take through
        //    EconomyService.TrySpend); it is DELETED, along with every `using` and capacity read
        //    that existed only to feed it. SiegeLossStakesRegression measures the bank across a
        //    full BuildStakes + ApplyStakes and fails if a single point moves.
        //
        // ⭐ ONE NUMBER, BY IDENTITY RATHER THAN BY AGREEMENT. BuildStakes does not compute a
        //    loss; it SUMS `ResourceCollector.LastLootStolen` — the very field the collector wrote
        //    when it lost the resources. The figure the player READS and the figure the collector
        //    LOST are the same value read from the same place, so there is no second computation
        //    available to drift. A report that lies about a loss is worse than no report.
        //
        // ⛔ CRYSTAL COLLECTORS ARE NOT LOOTABLE, and it is enforced on BOTH sides independently:
        //    `ResourceCollector.IsLootable` means nothing is ever taken, and `StakeRules.IsLootable`
        //    means nothing could be reported even if it were. Neither relies on the other.

        /// <summary>
        /// WHAT THE ATTACK CARRIED OFF — REPORTED, never computed.
        ///
        /// <para>Sums <see cref="ResourceCollector.LastLootStolen"/> across every collector that
        /// BROKE during this siege, bucketed by that collector's harvest resource. Crystals are
        /// absent because a crystal collector is never robbed in the first place.</para>
        ///
        /// <para><b>Scoped to THIS siege by the break stamp.</b> A destroyed collector is not
        /// repairable (WO-753) and stands as a broken shell carrying its loot figure for the rest
        /// of the session, so "every broken collector" would re-report an old robbery on every
        /// future siege. Only breaks at or after <c>settled.StartedAtUnixMs</c> count.</para>
        ///
        /// <para><b>It takes nothing.</b> No wallet, no economy service, no capacity read — the
        /// theft already happened inside the collector. This method is a READ.</para>
        ///
        /// <para>Guard-wrapped: if the scan throws, the fallback is an ALL-ZERO ledger. Failing
        /// towards "nothing was carried off" is the only safe direction — an exception must never
        /// be able to invent a loss.</para>
        /// </summary>
        public static StakesLedger BuildStakes(DefenseOutcomeRecord settled)
        {
            var ledger = Guard.Try<StakesLedger>("Siege", "report collector loot stakes", () =>
            {
                var built = StakeRules.Empty();

                // 0 = a record with no start stamp (hand-built, or an older shape). We then cannot
                // scope the loot to ONE siege, and an unscoped count would re-announce every break
                // still standing in the town. Report NOTHING and say so — the same failure
                // direction as the Guard fallback below: nothing here may invent a loss.
                // (SiegeSession stamps this at Arm, so the live path never lands here.)
                double since = settled != null ? settled.StartedAtUnixMs : 0.0;
                if (since <= 0.0)
                {
                    FlowTrace.Warn("Siege",
                        "stakes: the record carries no StartedAtUnixMs, so the loot cannot be scoped to " +
                        "this siege. Filing an all-zero ledger rather than re-reporting older breaks.");
                    return built;
                }

                int counted = 0, skippedStale = 0, skippedExempt = 0;

                foreach (var c in ResourceCollectorRegistry.All)
                {
                    if (c == null || !c.IsBroken) continue;

                    if (c.LastLootStolenAtUnixMs < since) { skippedStale++; continue; }

                    if (!c.IsLootable) { skippedExempt++; continue; }   // crystal collector

                    int stolen = Mathf.RoundToInt(c.LastLootStolen);
                    if (stolen <= 0) continue;

                    // StakeRules.Add is the ONLY writer, and it drops any bucket that is not
                    // lootable -- so an unmapped or exempt resource cannot land in the ledger.
                    if (StakeRules.Add(built, ToBankResource(c.Resource), stolen)) counted++;
                }

                FlowTrace.Step("Siege",
                    $"stakes REPORTED rule={built.StakesRuleId} outcome={(settled != null ? settled.Outcome : DefenseOutcome.Held)} " +
                    $"from {counted} broken collector(s) -> -W{built.Wood} -I{built.Iron} -F{built.Food}; " +
                    $"{skippedStale} stale (broke before this siege), {skippedExempt} crystal (never robbed); " +
                    "THE BANK WAS NOT TOUCHED -- what is collected is safe.");

                return built;
            }, fallback: null);

            if (ledger != null) return ledger;

            FlowTrace.Warn("Siege",
                "stakes report FAILED -- filing an all-zero ledger. A throw must never be " +
                "able to invent a loss, so the failure direction is 'nothing was carried off'.");
            return StakeRules.Empty();
        }

        /// <summary>
        /// Maps a collector's harvest type onto the ledger's bank bucket. Crystals map to
        /// <see cref="BankResource.Crystals"/>, which <see cref="StakeRules.IsLootable"/> refuses —
        /// so even a caller that forgets the exemption cannot write one.
        /// </summary>
        private static BankResource ToBankResource(HarvestResource resource)
        {
            switch (resource)
            {
                case HarvestResource.Wood: return BankResource.Wood;
                case HarvestResource.Iron: return BankResource.Iron;
                case HarvestResource.Food: return BankResource.Food;
                default: return BankResource.Crystals;   // never lootable — see StakeRules.IsLootable
            }
        }

        /// <summary>
        /// SEALS the stakes ledger onto the settled record. Called ONCE, from
        /// <c>SiegeScheduler.Settle</c>, between <c>SiegeSession.Close</c> and
        /// <c>DefenseReportLedger.Append</c>.
        ///
        /// <para>⛔⛔ <b>IT DEBITS NOTHING, AND IT NEVER MAY.</b> The name is historical: under the
        /// superseded 2026-08-21 ruling this method took 15% of the banked wallet through
        /// <c>EconomyService.TrySpend</c>. That debit is DELETED. Under the live ruling the
        /// resources were ALREADY removed by <c>ResourceCollector.OnSiegeDestroyed</c> when the
        /// collector broke, so any debit here would charge the player twice for one siege — once
        /// in the collector, once in the bank. There is deliberately no economy reference left in
        /// this method for a future edit to reach for.</para>
        ///
        /// <para>What it still does, all of which is bookkeeping:
        /// stamps the rule id so the record names the ruling that wrote it; enforces the crystal /
        /// magic backstop; and latches <see cref="StakesLedger.Applied"/> so a re-filed or
        /// re-opened record cannot be re-counted.</para>
        ///
        /// <para>⛔ Nothing here downgrades a building, destroys permanent progress, or touches
        /// stars / cleared-camp state. There is no code path in this file that could.</para>
        /// </summary>
        /// <returns>True when the record carries a real, non-zero loot loss.</returns>
        public static bool ApplyStakes(DefenseOutcomeRecord settled)
        {
            if (settled == null) return false;
            if (settled.ResourcesLost == null)
                settled.ResourcesLost = StakeRules.Empty();

            var ledger = settled.ResourcesLost;

            if (ledger.Applied)
            {
                FlowTrace.Warn("Siege",
                    $"ApplyStakes called AGAIN on report {settled.Id} -- already sealed " +
                    $"(-W{ledger.Wood} -I{ledger.Iron} -F{ledger.Food}). Refusing to re-count.");
                return false;
            }

            // ⛔ THE CRYSTAL BACKSTOP. StakeRules.Add cannot write a crystal bucket, so a non-zero
            //    one here means something else wrote the ledger (a bad migration, a hand-edited
            //    save, a future caller). Zero it: a player cannot tell a harvested crystal from a
            //    purchased one, so a crystal on a loss screen reads as losing bought currency.
            if (ledger.Crystals != 0 || ledger.Magic != 0)
            {
                FlowTrace.Fail("Siege",
                    $"stakes ledger carried crystals={ledger.Crystals} magic={ledger.Magic} -- " +
                    "NEITHER IS EVER LOOTABLE (owner ruling; crystal collectors are exempt at the " +
                    "steal AND at the ledger). Zeroed before the report is sealed.");
                ledger.Crystals = 0;
                ledger.Magic = 0;
            }

            ledger.StakesRuleId = StakeRules.RuleId;

            if (ledger.Wood <= 0 && ledger.Iron <= 0 && ledger.Food <= 0)
            {
                FlowTrace.Step("Siege",
                    $"stakes: nothing was carried off (outcome={settled.Outcome}) -- no collector broke " +
                    "with pending in it. The bank is not part of this mechanic.");
                return false;
            }

            // ⭐ THE ONE NUMBER, UNTOUCHED. These buckets ARE the collectors' own LastLootStolen
            //    figures, summed. Rewriting them here from any other source would recreate the two
            //    -accounts-of-one-loss defect this design exists to prevent.
            ledger.Applied = true;

            FlowTrace.Step("Siege",
                $"stakes SEALED rule={ledger.StakesRuleId} -W{ledger.Wood} -I{ledger.Iron} -F{ledger.Food} " +
                "(carried off from broken collectors; THE BANK WAS NOT DEBITED -- what is collected is safe).");

            return true;
        }
    }
}
