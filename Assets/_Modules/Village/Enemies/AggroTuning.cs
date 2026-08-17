// =============================================================================
// AggroTuning — owner-tunable CHASE / LEASH reach for aggro'd enemies.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS (owner finding, live play 2026-08-16):
//   "i was trying to target and bait an enemy out and i think we need to allow
//    aggro targets to extend leash alot more"
//
// This is a DESIGN-COHERENCE fix, not a tuning nudge. The ranger's design is
// "bow roots you while firing, dagger is the mobile option", so pulling ONE enemy
// off a pack is the ranged player's core skill expression - it is HOW an archer
// survives a group. Every leash in the tree was authored to solve a DIFFERENT
// problem (a fled kiter that never resolves; a dungeon room that beelines the
// entry) and each one silently deletes baiting as a side effect. The reach these
// leashes allow is therefore a BALANCE dial the owner must be able to turn, not a
// constant buried in three different .cs files.
//
// The numbers below are the SEEDS, deliberately generous, and they are HERS to
// tune - Data/Canonical/aggro-tuning.json, DUAL-COPY (Resources +
// StreamingAssets, byte-identical, versioned), no recompile.
//
// WHAT IS DELIBERATELY *NOT* HERE: no leash is removed. An unleashed enemy
// follows the player across the whole map and into town, which is a worse bug
// than a short leash. Every knob keeps an upper bound; the bound just became
// generous enough that a bait is a real tactic inside it.
//
// Same loader shape as HarvestTuning: lazy CanonicalJson read, Guard.Try'd,
// FlowTrace on load AND on the missing-file fallback (never a silent default,
// CLAUDE.md sec.12), Reload() for the headless oracle.
// =============================================================================
using System;
using Newtonsoft.Json;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Static read-only access to the chase/leash reach knobs
    /// (<c>Data/Canonical/aggro-tuning.json</c>). Falls back to the shipped
    /// defaults when the file is absent or unreadable - logged, never silent.
    /// </summary>
    public static class AggroTuning
    {
        /// <summary>Canonical-data relative path (dual-copy law: Resources copy wins,
        /// StreamingAssets is the desktop source - keep byte-identical).</summary>
        public const string RelativePath = "Data/Canonical/aggro-tuning.json";

        /// <summary>The schema version this build authors/expects. A different on-disk
        /// version loads anyway (additive law) with a warn trace.</summary>
        public const int ExpectedVersion = 1;

        // ── ARENA (BattleArena staged fight) ──────────────────────────────────
        // BEFORE: 16 / 18 / 7. The arena footprint is 45 x 36 (ArenaHalfWidth 22.5,
        // ArenaHalfDepth 18) and the enemy formation's rear rank sits at Z ~ +15,
        // so a bait that uses the arena end-to-end is ~33m. A 16m leash SNAPPED
        // every staged enemy back to within 15.2m of the hero every 0.25s - the
        // whole pack teleported after her the moment she backed off, which is
        // precisely the tactic she was trying to use.
        // AFTER: 34m covers the entire playable arena, so a legitimate cross-arena
        // bait is never yanked, while an enemy that has genuinely LEFT the fight is
        // still pulled back (the original fled-pack softlock stays fixed).
        public const float DefaultArenaChaseLeashRadius   = 34f;
        // INVARIANT: must stay ABOVE the leash, or a leashed (still-in-play) enemy
        // reads as out-of-contact and the disengage watchdog force-resolves a LOSS.
        public const float DefaultArenaEngageContactRadius = 38f;
        // A legitimate long bait now takes longer to close; the no-contact window
        // grows with it. Still far under the 240s battle timeout (no softlock).
        public const float DefaultArenaDisengageSeconds    = 10f;

        // ── BRAIN (dungeon / outpost room + anchor leash) ─────────────────────
        // BEFORE: the wake radius WAS the chase cap. A room mob woke at 6m from its
        // room footprint (or 10m from its spawn anchor for an unroomed group) and
        // went DORMANT + walked home the instant the hero stepped past that ring -
        // so a dungeon bait died after ~6m.
        // AFTER: wake stays exactly as authored (it is the NOTICE ring, unchanged),
        // but once a mob is ENGAGED it holds the chase until the hero is more than
        // this far from its home. Bounded: it can never follow her out of the wing.
        public const float DefaultBrainChaseLeashRadius = 30f;
        // How far outside its room an ENGAGED mob may path. Applies ONLY while
        // engaged - a dormant mob keeps max(roomSlack, wakeRadius) EXACTLY, so the
        // WO-797 "all enemies gathered at the entrance" camp cannot return (that
        // defect was dormant rooms beelining the entry, which the wake gate owns).
        public const float DefaultBrainEngagedPursuitSlack = 14f;

        // ── WORLD (wave / overworld enemies with NO brain leash) ─────────────
        // BEFORE: a wave enemy dropped aggro at aggroRadius + _heroAggroDropMargin,
        // default 7 + 2.5 = 9.5m (8 + 2.5 = 10.5m for overworld packs) and turned
        // back to its Heart-march. That break-off sits INSIDE bow range, so a ranger
        // could not hold aggro long enough to pull one body off a pack at all.
        // AFTER: this is a FLOOR on that margin (Max(), so a def authoring a LARGER
        // margin keeps it). ~18m puts break-off just past bow range - a shot holds
        // aggro - while the enemy still gives up well before it reaches town.
        // Owner: "we need to allow aggro targets to extend leash alot more".
        public const float DefaultWorldChaseDropMargin = 18f;

        // ── DTO (flat; additive fields only, per the canonical-data law) ──────
        [Serializable]
        private sealed class TuningDoc
        {
            [JsonProperty("version")] public int Version = ExpectedVersion;
            [JsonProperty("arena")]   public ArenaDoc Arena = new ArenaDoc();
            [JsonProperty("brain")]   public BrainDoc Brain = new BrainDoc();
            [JsonProperty("world")]   public WorldDoc World = new WorldDoc();
        }

        [Serializable]
        private sealed class WorldDoc
        {
            [JsonProperty("chaseDropMargin")] public float ChaseDropMargin = DefaultWorldChaseDropMargin;
        }

        [Serializable]
        private sealed class ArenaDoc
        {
            [JsonProperty("chaseLeashRadius")]    public float ChaseLeashRadius    = DefaultArenaChaseLeashRadius;
            [JsonProperty("engageContactRadius")] public float EngageContactRadius = DefaultArenaEngageContactRadius;
            [JsonProperty("disengageSeconds")]    public float DisengageSeconds    = DefaultArenaDisengageSeconds;
        }

        [Serializable]
        private sealed class BrainDoc
        {
            [JsonProperty("chaseLeashRadius")]    public float ChaseLeashRadius    = DefaultBrainChaseLeashRadius;
            [JsonProperty("engagedPursuitSlack")] public float EngagedPursuitSlack = DefaultBrainEngagedPursuitSlack;
        }

        private static TuningDoc _doc;

        /// <summary>How far a staged arena enemy may be from the hero before BattleArena
        /// pulls it back into the fight. Generous enough to cover the whole arena so a
        /// bait is never snapped; still bounded so a fled pack cannot softlock.</summary>
        public static float ArenaChaseLeashRadius => Doc().Arena.ChaseLeashRadius;

        /// <summary>Distance at which a staged enemy still counts as "in contact" for the
        /// disengage watchdog. Kept above <see cref="ArenaChaseLeashRadius"/> by
        /// <see cref="EffectiveArenaEngageContactRadius"/>.</summary>
        public static float ArenaEngageContactRadius => Doc().Arena.EngageContactRadius;

        /// <summary>The engage-contact radius ACTUALLY used, with the invariant enforced:
        /// always strictly greater than the leash, whatever the JSON says. An owner retune
        /// that inverts the pair would otherwise force-resolve live fights as losses.</summary>
        public static float EffectiveArenaEngageContactRadius
        {
            get
            {
                float leash = ArenaChaseLeashRadius;
                float contact = ArenaEngageContactRadius;
                if (contact > leash) return contact;
                float fixedUp = leash * 1.1f + 1f;
                FlowTrace.Throttle("EnemyAggro", "arena-contact-invariant", 10f,
                    $"aggro-tuning: engageContactRadius {contact:0.#} <= chaseLeashRadius {leash:0.#} " +
                    $"-- raised to {fixedUp:0.#} so a leashed enemy still reads in-contact " +
                    "(otherwise the disengage watchdog resolves a live fight as a LOSS).");
                return fixedUp;
            }
        }

        /// <summary>Seconds with no staged enemy in contact before BattleArena breaks off
        /// the encounter as a loss.</summary>
        public static float ArenaDisengageSeconds => Doc().Arena.DisengageSeconds;

        /// <summary>How far the HERO may be from an engaged mob's home (room footprint or
        /// spawn anchor) before that mob gives up the chase and walks home. This is the
        /// BAIT allowance: the wake radius stays the notice ring, this is the chase bound.
        /// &lt;= 0 restores the pre-fix behaviour exactly (wake radius == chase cap).</summary>
        public static float BrainChaseLeashRadius => Doc().Brain.ChaseLeashRadius;

        /// <summary>Extra metres outside its room an ENGAGED room-bound mob may path.
        /// Dormant mobs are unaffected.</summary>
        public static float BrainEngagedPursuitSlack => Doc().Brain.EngagedPursuitSlack;

        /// <summary>FLOOR on how far past its aggro ring a brain-less world enemy (wave /
        /// overworld pack) keeps chasing before dropping to its Heart-march. Consumed by
        /// Enemy.TryGetHeroTarget via Max(authored margin, this) — a def authoring a LARGER
        /// margin keeps it. 0 restores stock behaviour exactly.</summary>
        public static float WorldChaseDropMargin => Doc().World.ChaseDropMargin;

        /// <summary>The loaded schema version (the fallback doc reports <see cref="ExpectedVersion"/>).</summary>
        public static int Version => Doc().Version;

        /// <summary>Drop the cached doc so the next read reloads from disk (headless oracle hook).</summary>
        public static void Reload() => _doc = null;

        private static TuningDoc Doc()
        {
            if (_doc != null) return _doc;
            _doc = Guard.Try("EnemyAggro", "load aggro-tuning.json", Load,
                fallback: (TuningDoc)null) ?? Fallback();
            return _doc;
        }

        private static TuningDoc Load()
        {
            string json = DeNelle.Core.CanonicalJson.Read(RelativePath);
            if (string.IsNullOrEmpty(json))
            {
                // sec.12: a missing canonical file must be a LOGGED fallback, never a
                // silent default - otherwise an owner retune that fails to deploy reads
                // as "the tuning did nothing".
                FlowTrace.Warn("EnemyAggro",
                    $"aggro-tuning.json not found ({RelativePath}) -- using shipped defaults " +
                    $"(arena leash {DefaultArenaChaseLeashRadius:0.#}m, brain chase leash {DefaultBrainChaseLeashRadius:0.#}m).");
                return null;
            }

            var d = JsonConvert.DeserializeObject<TuningDoc>(json);
            if (d == null || d.Arena == null || d.Brain == null)
            {
                FlowTrace.Warn("EnemyAggro", "aggro-tuning.json parsed empty -- using shipped defaults.");
                return null;
            }
            if (d.Version != ExpectedVersion)
                FlowTrace.Warn("EnemyAggro",
                    $"aggro-tuning.json version {d.Version} != expected {ExpectedVersion} -- loading anyway (additive).");
            FlowTrace.Step("EnemyAggro",
                $"AggroTuning loaded (version {d.Version}): arena leash {d.Arena.ChaseLeashRadius:0.#}m / " +
                $"contact {d.Arena.EngageContactRadius:0.#}m / disengage {d.Arena.DisengageSeconds:0.#}s, " +
                $"brain chase leash {d.Brain.ChaseLeashRadius:0.#}m / engaged pursuit slack {d.Brain.EngagedPursuitSlack:0.#}m.");
            return d;
        }

        private static TuningDoc Fallback() => new TuningDoc();
    }
}
