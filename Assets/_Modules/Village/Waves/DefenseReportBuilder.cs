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
//   a settled record          -> StakesLedger            (⛔ THE UNRULED SEAM, §BuildStakes)
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
using DeNelle.Core.State;
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
        //  ★★★ THE SEAM — where the UNRULED loss consequence plugs in ★★★
        // =====================================================================

        /// <summary>
        /// ⛔ WHAT THE ATTACK TOOK. TODAY: NOTHING, DELIBERATELY.
        ///
        /// The owner has NOT ruled what a failed defence costs the player, and WO-1026 records
        /// that as open. Inventing a rule here would be the worst kind of guess — it collides with
        /// the stockpile CAP progression (memory `stockpiles-cap-capacity`) and with the WO-947
        /// cost-basket split (regular = wood+iron, magical = crystals; never all three in one
        /// basket). So this build RESOLVES and REPORTS an attack and TAKES NOTHING, and the report
        /// says so out loud ("Nothing was taken.") rather than leaving a blank the player reads as
        /// a bug.
        ///
        /// <para><b>WHEN THE OWNER RULES, the change is three steps and nothing else moves:</b>
        /// (1) implement the arithmetic in THIS METHOD and stamp a NEW StakesRuleId
        ///     (e.g. "stakes.stockpile.wo1XXX");
        /// (2) apply the debit through the EXISTING wallet path (EconomyService / GameStateService)
        ///     at the SiegeSession.Close call site — ONE guarded, traced debit call, never a second
        ///     economy writer;
        /// (3) update DefenseReportContractRegression's stakes case to assert the new rule id.
        /// The record already carries the basket, the panel already renders a stakes line, and the
        /// save wire already holds it — which is the entire point of authoring the ZERO ledger now
        /// instead of omitting the field.</para>
        ///
        /// <para><b>What must NOT be pre-built while the ruling is open:</b> any shield/immunity
        /// timer, any revenge target, any trophy/rating number (those are (b)/(c) balancing and mean
        /// nothing under (a)), and any interaction with storage caps or the basket split.</para>
        /// </summary>
        public static StakesLedger BuildStakes(DefenseOutcomeRecord settled)
        {
            FlowTrace.Step("Siege",
                $"stakes: none (interim -- UNRULED). outcome={(settled != null ? settled.Outcome.ToString() : "?")} " +
                $"rule={StakesLedger.InterimRuleId}.");
            return StakesLedger.Interim();
        }
    }
}
