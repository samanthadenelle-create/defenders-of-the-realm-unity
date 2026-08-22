// =============================================================================
// DefenseReport — the persisted DEFENCE artifact (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Defense
//
// The record of ONE attack ON THE PLAYER'S TOWN: who came, what the base looked
// like at the time, where they broke through, what broke, and what it cost.
//
// WHY IT LIVES IN CORE, NOT VILLAGE:
//   It is persisted by DeNelle.Core.State.SaveSchema. A record type in
//   DeNelle.Village could never be a field on PersistedState. Core also means the
//   HUD assembly (Core + Data only) could render it later without breaching the one
//   enforced cross-assembly invariant (CLAUDE.md SS5).
//
// PURE DATA. NO UnityEngine TYPES ON A PERSISTED FIELD.
//   Breach positions are three plain floats, NOT a Vector3: Newtonsoft round-trips
//   Vector3 badly (it serialises the derived properties too) and the save wire must
//   stay human-inspectable JSON.
//
// THE MODEL-(c) SEAM — read this before adding a field:
//   The owner ruled model (a) PvE siege, built so model (c) "ghost PvP" is a SOURCE
//   SWAP, not a rewrite. That swap lives in exactly ONE place: AttackerIdentity.Source
//   plus AttackerIdentity.SnapshotId. Every reader of this record renders
//   DisplayName / PowerRating / Units and NEVER branches on Source to decide WHAT to
//   show. The only sanctioned Source read in presentation is a small label chip
//   ("Raiders" / "Ghost of <name>") — a lookup, not a layout branch.
//   DefenseReportContractRegression pins that a GhostSnapshot-sourced record
//   round-trips identically today, so (c) needs no schema change.
//
// ⛔ THE STAKES ARE UNRULED — StakesLedger IS ALL ZERO ON PURPOSE.
//   What the player LOSES on a failed defence is an owner design call that has not
//   been made. This build RESOLVES and REPORTS an attack; it TAKES NOTHING. The
//   basket exists in the wire now (WO-947 shape) so a later ruling plugs in at ONE
//   method — DefenseReportBuilder.BuildStakes — with no schema change and no second
//   economy writer. StakesRuleId makes an old report self-describing, so a build that
//   HAS stakes can never mis-read an interim report as "they took nothing that day".
//   Do not invent a rule here. Do not "just take 10% of stockpile" (that collides with
//   the stockpiles-cap-capacity and WO-947 basket rulings).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeNelle.Core.Defense
{
    /// <summary>
    /// The provenance of the attacking force. THE MODEL-(c) SEAM.
    /// Values are load-bearing on the wire — APPEND ONLY, never renumber, never delete.
    /// </summary>
    public enum AttackerSource
    {
        /// <summary>Model (a): the live PvE wave roster the town generated. The only value
        /// anything produces today, and it is WRITTEN by the producer, never assumed by a reader.</summary>
        GeneratedPve = 0,
        /// <summary>Model (c): a real player's snapshotted layout/army, replayed by AI.
        /// Nothing produces this yet; the shape is proven by the contract oracle so the day a
        /// snapshot source exists it is a producer swap, not a schema change.</summary>
        GhostSnapshot = 1,
        /// <summary>Model (b): reserved for true live PvP. Nothing produces this and nothing
        /// should. The value exists so the enum converter never drops it. NEVER DELETE.</summary>
        LivePvp = 2,
    }

    /// <summary>How the defence ended. Wire values are load-bearing — append only.</summary>
    public enum DefenseOutcome
    {
        /// <summary>Cleared with the wall/gate ring intact — nothing crossed.</summary>
        Held = 0,
        /// <summary>Cleared, but at least one attacker crossed the inner ring.</summary>
        Breached = 1,
        /// <summary>The Heart fell. The defence failed outright.</summary>
        Overrun = 2,
    }

    /// <summary>Whether the player watched it or it was resolved for them. Append only.</summary>
    public enum DefenseResolution
    {
        /// <summary>The player was present; the fight happened at the gate in front of them.
        /// The ONLY value produced today (see SiegeScheduler — away time becomes PRESSURE, not
        /// a simulated battle).</summary>
        Live = 0,
        /// <summary>Resolved in absentia by a fast-forward sim. The WO-430-F seam. Nothing
        /// produces this yet, and nothing should until the stakes are ruled — an absentee raid
        /// with no consequence writes a record that says nothing happened.</summary>
        ResolvedInAbsentia = 1,
    }

    /// <summary>One unit type in a force — the attacker's roster row AND the defender's
    /// garrison row, deliberately the same shape so one renderer serves both sides.</summary>
    [Serializable]
    public sealed class AttackerUnitRecord
    {
        /// <summary>Enemy/troop def id ("hollow_one", "footman"). Opaque to every reader.</summary>
        [JsonProperty("def")] public string DefId;
        /// <summary>How many of this type were fielded across the whole assault (union over
        /// time — WO-1113 releases a roster in reinforcement SLICES, so a scene census at any
        /// one instant undercounts).</summary>
        [JsonProperty("n")] public int Count;
        /// <summary>Highest level seen for this type (1 when unscaled).</summary>
        [JsonProperty("lvl")] public int Level;
    }

    /// <summary>WHO attacked. The model-(c) source swap lives entirely in this class.</summary>
    [Serializable]
    public sealed class AttackerIdentity
    {
        /// <summary>The swap. Written by the producer; never inferred by a reader.</summary>
        [JsonProperty("src")] public AttackerSource Source;
        /// <summary>"pve.warband.t3" today; a player/snapshot-owner id under (c). Opaque.</summary>
        [JsonProperty("id")] public string AttackerProfileId;
        /// <summary>"Hollow Warband" today; a player's town name under (c). The panel renders
        /// THIS STRING — it never composes a name from the Source enum.</summary>
        [JsonProperty("name")] public string DisplayName;
        /// <summary>Roster strength, so the player reads "I lost to something stronger" rather
        /// than guessing. Derived from the composed roster under (a); read off the snapshot under (c).</summary>
        [JsonProperty("pow")] public int PowerRating;
        /// <summary>EMPTY under (a). Under (c) this is the key of the stored base/army snapshot
        /// the replay was driven from — present from day one so a replay button has a target.</summary>
        [JsonProperty("snap")] public string SnapshotId;
        /// <summary>The force that was fielded.</summary>
        [JsonProperty("units")] public List<AttackerUnitRecord> Units;
    }

    /// <summary>What the player's base looked like AT ATTACK TIME — captured before the
    /// first spawn, so an old report stays legible after the town has been rebuilt.</summary>
    [Serializable]
    public sealed class DefenderSnapshot
    {
        /// <summary>Stable, order-independent hash over the BaseLayout records
        /// (itemId + cell + yaw + level). Move a structure and the NEXT report's hash differs —
        /// which is how "a redesign has a visible effect" becomes a DATA assertion instead of a
        /// vibe. Also the model-(c) precondition: a snapshot IS a layout.</summary>
        [JsonProperty("hash")] public string LayoutHash;
        /// <summary>Placed-structure count at attack time.</summary>
        [JsonProperty("sc")] public int StructureCount;
        /// <summary>Wall-segment count at attack time.</summary>
        [JsonProperty("wc")] public int WallCount;
        /// <summary>Tower count at attack time.</summary>
        [JsonProperty("tc")] public int TowerCount;
        /// <summary>Was the hero on the field. Explains outcome swings between reports.</summary>
        [JsonProperty("hero")] public bool HeroPresent;
        /// <summary>EMPTY today. The WO-430-F offline-troop-garrison seam — same unit-record
        /// shape as the attacker so one renderer serves both sides.</summary>
        [JsonProperty("gar")] public List<AttackerUnitRecord> Garrison;

        /// <summary>
        /// Radius of the Heart's inner ring at attack time — the <see cref="DefenseBand.Core"/>
        /// boundary. This is WaveManager's OWN breach-ring radius, not a second number: the
        /// line that defines a breach is the line that defines the core.
        /// </summary>
        [JsonProperty("rcore")] public float CoreRadius;
        /// <summary>
        /// Median wall distance at attack time — the <see cref="DefenseBand.Front"/> boundary.
        /// DERIVED FROM THE PLAYER'S OWN WALLS, so it means "your front line", not a designer's
        /// guess at one. 0 when the base has no walls, which correctly collapses the Front band
        /// and lets the report say "you have no front line".
        /// </summary>
        [JsonProperty("rfront")] public float FrontRadius;
        /// <summary>World X of the Heart at attack time — the plate's origin.</summary>
        [JsonProperty("cx")] public float CoreX;
        /// <summary>World Z of the Heart at attack time — the plate's origin.</summary>
        [JsonProperty("cz")] public float CoreZ;
    }

    /// <summary>WHERE they broke through. The redesign signal — the "move that tower" moment.</summary>
    [Serializable]
    public sealed class BreachRecord
    {
        /// <summary>Instance/structure id of whatever was crossed (may be empty when the
        /// crossing was open ground rather than a named gate).</summary>
        [JsonProperty("id")] public string BreachedId;
        /// <summary>"North Gate" — so the row still reads after that gate is gone.</summary>
        [JsonProperty("name")] public string DisplayName;
        /// <summary>Plain floats, NOT a Vector3 (Core stays engine-type-free on the wire).</summary>
        [JsonProperty("x")] public float WorldX;
        /// <summary>Plain floats, NOT a Vector3.</summary>
        [JsonProperty("y")] public float WorldY;
        /// <summary>Plain floats, NOT a Vector3.</summary>
        [JsonProperty("z")] public float WorldZ;
        /// <summary>Seconds into the assault. Ordering makes "they came from the north first" legible.</summary>
        [JsonProperty("t")] public float AtSeconds;
        /// <summary>Which unit type got through — "the flyers ignored my walls".</summary>
        [JsonProperty("by")] public string AttackerDefId;
    }

    /// <summary>
    /// Which band of the base a structure stands in. THE GROUPING THAT TURNS A LIST INTO A
    /// DIAGNOSIS: "everything on my front line fell and nothing behind it was touched" is a
    /// sentence a player can act on; "Tower A destroyed, Wall B destroyed" is not.
    ///
    /// <para>⚠ THE BANDS ARE DERIVED FROM THE PLAYER'S OWN BASE, NOT FROM MAGIC CONSTANTS.
    /// Core = inside WaveManager's own inner ring radius (the same geometry that defines a
    /// breach — no second number). Front = at or beyond the MEDIAN WALL DISTANCE, because the
    /// front line IS the wall ring by definition. Second = everything between. Both radii are
    /// STORED on the report (<see cref="DefenderSnapshot.CoreRadius"/> /
    /// <see cref="DefenderSnapshot.FrontRadius"/>) so an old report keeps its original
    /// classification after the town is rebuilt, instead of silently re-banding itself.
    /// A base with no walls has no front line — and the report saying exactly that is a true
    /// and useful statement, not a degenerate case.</para>
    /// Append only; wire values are load-bearing.
    /// </summary>
    /// <summary>
    /// How one structure came out of the assault.
    /// <para>⚠ <see cref="Held"/> IS RESERVED AND NOTHING PRODUCES IT TODAY, deliberately.
    /// The rows come from <c>WaveDamageReport.Collect()</c>, which only enumerates structures
    /// that were actually damaged — an untouched wall has no row. Emitting a Held row for every
    /// intact structure would bury the eight rows that matter under a census of the whole town.
    /// The value exists so the shape is complete the day a producer wants a full roster (a
    /// model-(c) ghost report is the obvious candidate); it is not a gap.</para>
    /// Append only; wire values are load-bearing.
    /// </summary>
    public enum StructureState
    {
        /// <summary>Untouched. RESERVED — see the type doc.</summary>
        Held = 0,
        /// <summary>Took damage and survived.</summary>
        Damaged = 1,
        /// <summary>Broken.</summary>
        Destroyed = 2,
    }

    public enum DefenseBand
    {
        /// <summary>Deep inside — the Heart's ring. If these took damage, the ring failed.</summary>
        Core = 0,
        /// <summary>Between the wall ring and the Heart's ring.</summary>
        Second = 1,
        /// <summary>At or beyond the wall ring — the outer shell that meets them first.</summary>
        Front = 2,
    }

    /// <summary>
    /// One sample of where the attacking force was, as a whole, at a moment in the assault.
    /// Strung together these form the PATH polyline on the report plate — "they came around
    /// the east side" is a thing the player can move a tower against.
    /// <para>It is the force CENTROID, not a per-unit trail: one cheap sample instead of N,
    /// and it is what actually answers "which way did they come".</para>
    /// </summary>
    [Serializable]
    public sealed class AttackPathPoint
    {
        /// <summary>World X of the force centroid. Plain float — Core stays engine-type-free.</summary>
        [JsonProperty("x")] public float WorldX;
        /// <summary>World Z of the force centroid.</summary>
        [JsonProperty("z")] public float WorldZ;
        /// <summary>Seconds into the assault.</summary>
        [JsonProperty("t")] public float AtSeconds;
        /// <summary>How many attackers were alive at this sample. Thins to 0 as you kill them,
        /// so the polyline also carries WHERE the force died.</summary>
        [JsonProperty("n")] public int LiveCount;
    }

    /// <summary>One damaged/destroyed structure. A 1:1 adapt of DeNelle.Village
    /// WaveDamageReport.Entry — the aggregation already exists and is NOT re-implemented —
    /// PLUS the legibility fields (position, band, and hold TIME) that turn the row from a
    /// fact into a diagnosis.</summary>
    [Serializable]
    public sealed class StructureOutcome
    {
        /// <summary>
        /// Scene-instance key of the structure ("NorthGate", "Tower_04"). The SAME key
        /// <see cref="BreachRecord.BreachedId"/> uses, which is what lets
        /// <see cref="BreachOrdinal"/> be correlated.
        /// <para>⚠ IT IS A SCENE KEY, NOT A CATALOG KEY. It does not survive a rebuild and
        /// nothing may persist against it. Said explicitly because an id that LOOKS stable is
        /// exactly the sort of thing a later ticket keys a save off by accident.</para>
        /// </summary>
        [JsonProperty("sid")] public string StructureId;
        /// <summary>Family of thing this was: "Wall", "Gate", "Building", "Collector", "Tower",
        /// "HarvestSite". Lets the report say "3 of your WALLS fell" rather than naming six
        /// individual segments — the aggregate is what suggests a redesign.</summary>
        [JsonProperty("stype")] public string StructureType;
        /// <summary>Player-facing structure name.</summary>
        [JsonProperty("name")] public string DisplayName;
        /// <summary>Normalised damage 0..1 (1 = destroyed).</summary>
        [JsonProperty("dmg")] public float DamageFraction;
        /// <summary>How this structure came out of the assault. The single source of truth for
        /// the row's condition — <see cref="Destroyed"/> is derived from it, never stored
        /// alongside it, so the two can never disagree.</summary>
        [JsonProperty("state")] public StructureState State;
        /// <summary>
        /// Which breach happened AT this structure: 1 = they came through here FIRST, 2 = second,
        /// 0 = they did not cross here. Correlated from <see cref="BreachRecord.BreachedId"/>.
        /// <para>This is the field that answers "is this the wall I should move, or just one
        /// that took splash damage" — the two look identical in a flat list.</para>
        /// </summary>
        [JsonProperty("bord")] public int BreachOrdinal;
        /// <summary>Fully destroyed / broken. DERIVED from <see cref="State"/>.</summary>
        [JsonIgnore] public bool Destroyed => State == StructureState.Destroyed;
        /// <summary>ResourceCollector row — the label carries a production hit.</summary>
        [JsonProperty("coll")] public bool IsCollector;
        /// <summary>Collectors only: pending resources stolen when the collector broke.
        /// ⚠ This is TODAY'S EXISTING MECHANIC being RECORDED, not a new stake. Do not confuse
        /// it with <see cref="StakesLedger"/>, which is the unruled loss and is all zero.</summary>
        [JsonProperty("loot")] public int LootStolen;
        /// <summary>Flattened repair cost. Flattened rather than embedding Catalog.ResourceCost
        /// so the wire stays stable if that struct changes.</summary>
        [JsonProperty("rw")] public int RepairWood;
        /// <summary>Flattened repair cost.</summary>
        [JsonProperty("ri")] public int RepairIron;
        /// <summary>Flattened repair cost.</summary>
        [JsonProperty("rf")] public int RepairFood;
        /// <summary>Flattened repair cost.</summary>
        [JsonProperty("rc")] public int RepairCrystals;
        /// <summary>False = nothing alive could price the row. Cost is OMITTED, never faked
        /// (the existing WaveDamageReport contract).</summary>
        [JsonProperty("hc")] public bool HasCost;

        // ── LEGIBILITY (WO-1026 follow-up: "players need to form the thought
        //    'I know what to move'"). Everything below exists to answer WHERE and WHEN,
        //    because WHAT alone is not actionable. ──────────────────────────────────

        /// <summary>World X. Plain float, not a Vector3 (Core stays engine-type-free on the
        /// wire). Feeds the loss pins on the report plate.</summary>
        [JsonProperty("x")] public float WorldX;
        /// <summary>World Z.</summary>
        [JsonProperty("z")] public float WorldZ;
        /// <summary>Planar distance from the Heart at attack time. The input the band was
        /// classified from, kept so the classification is auditable rather than asserted.</summary>
        [JsonProperty("d")] public float DistanceFromCore;
        /// <summary>Which band of the base this stood in. See <see cref="DefenseBand"/>.
        /// STORED, not recomputed on read — an old report must keep the classification it was
        /// written with, or rebuilding the town silently rewrites history.</summary>
        [JsonProperty("line")] public DefenseBand Band;

        /// <summary>
        /// ⭐ HOW LONG IT HELD, in seconds, once they started hitting it. THE HIGHEST-SIGNAL
        /// FIELD ON THE RECORD: "this wall held 40s" vs "this wall fell in 4s" is the
        /// difference between a list of losses and a diagnosis. A structure that survived the
        /// assault holds for the rest of it, so this is still meaningful for a damaged-but-alive
        /// row ("held the whole fight at 40% damage").
        /// <para>-1 = UNKNOWN, and that is a real state, not a zero: see
        /// <see cref="WasAlreadyDamaged"/>. Never render -1 as "fell in 0s".</para>
        /// </summary>
        [JsonProperty("held")] public float HoldTimeSeconds;
        /// <summary>Seconds into the assault when it first took damage. -1 = unknown.</summary>
        [JsonProperty("hit")] public float FirstHitAtSeconds;
        /// <summary>Seconds into the assault when it was destroyed. -1 = it survived.</summary>
        [JsonProperty("fell")] public float FellAtSeconds;
        /// <summary>
        /// TRUE when the structure was ALREADY damaged before this assault began, so its hold
        /// time describes an earlier fight, not this one. The row says so out loud instead of
        /// reporting a fast collapse that never happened — a hold time that quietly lies is
        /// worse than no hold time, because the player would move the wrong tower.
        /// </summary>
        [JsonProperty("pre")] public bool WasAlreadyDamaged;

        /// <summary>True when a usable hold time was measured for THIS assault.</summary>
        [JsonIgnore]
        public bool HasHoldTime => HoldTimeSeconds >= 0f && !WasAlreadyDamaged;
    }

    /// <summary>
    /// ⛔ WHAT THE ATTACK TOOK — ALL ZERO UNDER THE INTERIM, AND SELF-DESCRIBING ABOUT WHY.
    /// The buckets are shaped per the WO-947 basket ruling so a later loss ruling has somewhere
    /// to land with no schema change. THE ONLY PLACE A RULING PLUGS IN IS
    /// DefenseReportBuilder.BuildStakes.
    /// </summary>
    [Serializable]
    public sealed class StakesLedger
    {
        /// <summary>0 under the interim.</summary>
        [JsonProperty("w")] public int Wood;
        /// <summary>0 under the interim.</summary>
        [JsonProperty("i")] public int Iron;
        /// <summary>0 under the interim.</summary>
        [JsonProperty("f")] public int Food;
        /// <summary>0 under the interim.</summary>
        [JsonProperty("c")] public int Crystals;
        /// <summary>0 under the interim.</summary>
        [JsonProperty("m")] public int Magic;
        /// <summary>Which ruling produced these numbers. <see cref="InterimRuleId"/> today; a
        /// future ruling stamps its OWN id. This is what stops a stakes-carrying build from
        /// mis-reading an interim report as "the player lost nothing that day".</summary>
        [JsonProperty("rule")] public string StakesRuleId;

        /// <summary>The id stamped while the loss consequence is UNRULED.</summary>
        public const string InterimRuleId = "none.interim.wo1026";

        /// <summary>True when every bucket is zero (the interim contract).</summary>
        [JsonIgnore]
        public bool IsEmpty => Wood == 0 && Iron == 0 && Food == 0 && Crystals == 0 && Magic == 0;

        /// <summary>The interim ledger: nothing taken, stamped with the interim rule id.</summary>
        public static StakesLedger Interim()
        {
            return new StakesLedger { StakesRuleId = InterimRuleId };
        }
    }

    /// <summary>
    /// ONE attack on the player's town, start to finish. Persisted; source-agnostic.
    /// </summary>
    [Serializable]
    public sealed class DefenseOutcomeRecord
    {
        /// <summary>The CURRENT record shape version. Deliberately INDEPENDENT of
        /// SaveSchema.CurrentVersion so the report can evolve (a model-(c) upgrade adds attacker
        /// fields) without bumping the whole save every time.</summary>
        public const int CurrentRecordVersion = 1;

        /// <summary>Record-level shape version. See <see cref="CurrentRecordVersion"/>.</summary>
        [JsonProperty("v")] public int RecordVersion;
        /// <summary>GUID. The stable key the panel selects by and a future server would dedupe on.</summary>
        [JsonProperty("id")] public string Id;
        /// <summary>Unix ms the assault began (the house clock — DeNelle.Village.TimeSource.NowUnixMs).</summary>
        [JsonProperty("t0")] public double StartedAtUnixMs;
        /// <summary>Unix ms the assault ended.</summary>
        [JsonProperty("t1")] public double EndedAtUnixMs;
        /// <summary>Live vs resolved-in-absentia. Only Live is produced today.</summary>
        [JsonProperty("res")] public DefenseResolution Resolution;
        /// <summary>Held / Breached / Overrun — the one-line verdict.</summary>
        [JsonProperty("out")] public DefenseOutcome Outcome;
        /// <summary>Which wave ordinal produced it (the difficulty context). Neutral under (c).</summary>
        [JsonProperty("wave")] public int WaveId;
        /// <summary>How long it took. The compare-across-attempts axis.</summary>
        [JsonProperty("dur")] public float DurationSeconds;
        /// <summary>Who attacked (see <see cref="AttackerIdentity"/> — the (c) seam).</summary>
        [JsonProperty("atk")] public AttackerIdentity Attacker;
        /// <summary>The base as it stood (see <see cref="DefenderSnapshot"/>).</summary>
        [JsonProperty("def")] public DefenderSnapshot Defender;
        /// <summary>Where they crossed. Empty on a clean hold.</summary>
        [JsonProperty("brk")] public List<BreachRecord> Breaches;
        /// <summary>What broke, worst-first (adapted from WaveDamageReport, capped at its MaxRows).</summary>
        [JsonProperty("loss")] public List<StructureOutcome> Rows;
        /// <summary>The attacking force's centroid, sampled through the assault — the PATH
        /// polyline on the report plate. Empty on a report written before the path sampler,
        /// which the plate handles by simply drawing no line.</summary>
        [JsonProperty("path")] public List<AttackPathPoint> Path;
        /// <summary>⛔ ALL ZERO under the interim. See <see cref="StakesLedger"/>.</summary>
        [JsonProperty("stk")] public StakesLedger ResourcesLost;

        /// <summary>
        /// 0-100 summary of how the defence went, or <see cref="NotScored"/> (-1) when the
        /// inputs are too thin to say. FROZEN at Close, never recomputed on read — the same rule
        /// as the band radii, so a rebuilt town cannot silently re-score an old report.
        ///
        /// <para>⛔ IT DECLINES RATHER THAN GUESSING. Same discipline as the hold time: a score
        /// assembled from one input while looking like it came from three is worse than no
        /// score, because the player would trust it. See
        /// <c>DefenseReportBuilder.ComputeDefenseScore</c> for the derivation and the exact
        /// condition under which it returns <see cref="NotScored"/>.</para>
        ///
        /// <para>⚠ PRESENTATION ONLY. Nothing gameplay-facing reads this — no reward, no
        /// matchmaking, no stake. It is a label. Keeping it inert is what stops a display
        /// weighting quietly becoming a balance rule.</para>
        /// </summary>
        [JsonProperty("score")] public int DefenseScore;

        /// <summary>Sentinel for "not enough information to score this defence".</summary>
        public const int NotScored = -1;

        /// <summary>True when <see cref="DefenseScore"/> holds a real score.</summary>
        [JsonIgnore] public bool HasDefenseScore => DefenseScore >= 0;

        /// <summary>
        /// When the attack happened — the canonical sort key, and the field a reader means by
        /// "timestamp". It is an ALIAS for <see cref="EndedAtUnixMs"/> rather than a fourth
        /// stored time: start + end + duration already over-determine the timeline, and a
        /// separately-stored Timestamp would be a fourth value free to disagree with the other
        /// three.
        /// </summary>
        [JsonIgnore] public double Timestamp => EndedAtUnixMs;
        /// <summary>Unread badge state. PLAYER state, not outcome — hence a mutable field on the
        /// record rather than a parallel list that could drift out of sync with it.</summary>
        [JsonProperty("read")] public bool Read;

        /// <summary>A blank, well-formed record: never null collections, interim stakes.
        /// Every producer starts here so no reader has to null-check a sub-object.</summary>
        public static DefenseOutcomeRecord NewEmpty()
        {
            return new DefenseOutcomeRecord
            {
                RecordVersion = CurrentRecordVersion,
                Id = Guid.NewGuid().ToString("N"),
                Resolution = DefenseResolution.Live,
                Outcome = DefenseOutcome.Held,
                Attacker = new AttackerIdentity
                {
                    // Source is DELIBERATELY NOT SET HERE. It is left at default(AttackerSource)
                    // and the PRODUCER stamps it (DefenseReportBuilder is the only file in the
                    // repo that writes GeneratedPve, and SiegeSpawnAuthorityRegression enforces
                    // that). Setting it here would be a second place that hardcodes "the attacker
                    // is generated" -- which is exactly what stops model (c) being a source swap.
                    AttackerProfileId = string.Empty,
                    DisplayName = string.Empty,
                    SnapshotId = string.Empty,
                    Units = new List<AttackerUnitRecord>(),
                },
                Defender = new DefenderSnapshot
                {
                    LayoutHash = string.Empty,
                    Garrison = new List<AttackerUnitRecord>(),
                },
                Breaches = new List<BreachRecord>(),
                Rows = new List<StructureOutcome>(),
                Path = new List<AttackPathPoint>(),
                ResourcesLost = StakesLedger.Interim(),
                DefenseScore = NotScored,
                Read = false,
            };
        }

        /// <summary>Repairs a deserialised record in place so no reader ever sees a null
        /// sub-object or list. Deliberately tolerant: an older/partial wire is normalised,
        /// never rejected (the SaveSchema .partial() convention).</summary>
        public static DefenseOutcomeRecord Normalize(DefenseOutcomeRecord r)
        {
            if (r == null) return NewEmpty();
            if (r.RecordVersion <= 0) r.RecordVersion = CurrentRecordVersion;
            if (string.IsNullOrEmpty(r.Id)) r.Id = Guid.NewGuid().ToString("N");
            if (r.Attacker == null) r.Attacker = new AttackerIdentity();
            if (r.Attacker.Units == null) r.Attacker.Units = new List<AttackerUnitRecord>();
            if (r.Attacker.AttackerProfileId == null) r.Attacker.AttackerProfileId = string.Empty;
            if (r.Attacker.DisplayName == null) r.Attacker.DisplayName = string.Empty;
            if (r.Attacker.SnapshotId == null) r.Attacker.SnapshotId = string.Empty;
            if (r.Defender == null) r.Defender = new DefenderSnapshot();
            if (r.Defender.Garrison == null) r.Defender.Garrison = new List<AttackerUnitRecord>();
            if (r.Defender.LayoutHash == null) r.Defender.LayoutHash = string.Empty;
            if (r.Breaches == null) r.Breaches = new List<BreachRecord>();
            if (r.Rows == null) r.Rows = new List<StructureOutcome>();
            if (r.Path == null) r.Path = new List<AttackPathPoint>();
            if (r.ResourcesLost == null) r.ResourcesLost = StakesLedger.Interim();
            if (string.IsNullOrEmpty(r.ResourcesLost.StakesRuleId)) r.ResourcesLost.StakesRuleId = StakesLedger.InterimRuleId;
            return r;
        }
    }

    /// <summary>
    /// The layout fingerprint used by <see cref="DefenderSnapshot.LayoutHash"/>.
    /// Lives in Core (not Village) so the contract oracle can prove AC "a redesign has a
    /// visible effect" without a scene: two layouts differing by one moved structure MUST
    /// hash differently, and the SAME layout in a different ORDER must hash the SAME.
    /// </summary>
    public static class LayoutFingerprint
    {
        /// <summary>Hash of the empty/absent layout.</summary>
        public const string Empty = "0000000000000000";

        /// <summary>
        /// Order-independent 64-bit FNV-1a over one normalised token per structure.
        /// Deliberately NOT string.GetHashCode — that is not stable across processes or
        /// runtimes, so a hash written to a save could never be compared to one computed
        /// later, which is the whole job of this field.
        /// </summary>
        public static string Compute(IEnumerable<string> tokens)
        {
            if (tokens == null) return Empty;
            var list = new List<string>();
            foreach (var t in tokens)
                if (!string.IsNullOrEmpty(t)) list.Add(t);
            if (list.Count == 0) return Empty;

            // SORT = order independence. Two saves that list the same structures in a
            // different order describe the SAME base and must produce the SAME hash.
            list.Sort(StringComparer.Ordinal);

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong h = offset;
            for (int i = 0; i < list.Count; i++)
            {
                string s = list[i];
                for (int c = 0; c < s.Length; c++)
                {
                    h ^= s[c];
                    h *= prime;
                }
                h ^= (byte)'|';   // explicit separator so "ab"+"c" cannot collide with "a"+"bc"
                h *= prime;
            }
            return h.ToString("x16");
        }
    }
}
