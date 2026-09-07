// =============================================================================
// WildlandsRoster - the SINGLE SOURCE OF TRUTH for the overworld "Wildlands"
// enemy BASE stat blocks that are spawned OUTSIDE the village wave loop's own
// spawn path (roaming mobs, enemy outposts, camp counterattacks, and additive
// garrisons).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS: the overworld roster ids (orc-raider, ...) were historically
// NOT in enemies.json, so FOUR different spawners each CODE-BUILT their own
// EnemyDef for the same id via a private switch - and those switches DRIFTED.
// The CombatAtbRegression divergence oracle (Check H) caught it: RegionMobSpawner
// / EnemyOutpost / CampDefenseWave stat "orc-raider" at Hp 95, while the
// GarrisonController path (GarrisonStatBlocks) stat the SAME id at base Hp 170.
// One id, two stat blocks - a raider was nearly 2x tougher depending on which
// system spawned it.
//
// THE FIX: resolve the BASE stat block from the enemies.json catalog-of-record
// (read ONCE, cached), so every spawner reads the SAME numbers BY CONSTRUCTION.
// WO-1535 WIDENED the owned set from the single orc-raider seed to NINE garrison
// ids and DELETED the per-id code fallback (its own promise to mirror the catalog
// had already drifted — audit finding F47, XpReward 22 vs 24). An unresolvable id
// now gets ONE loud emergency sentinel, never a stale copy of a real enemy.
// Each spawner still applies its OWN CONTEXT
// multiplier on top (threat scale / early-game ease / garrison GlobalDifficultyMult
// / per-level scale) - the BASE is unified here; the context stays per-system.
//
// The oracle compares RegionMobSpawner.BuildRoamerDef(id, threat:0).Hp (the raw
// base) against GarrisonStatBlocks.BuildTypedDef(id, 0).Hp / GlobalDifficultyMult
// (the garrison base recovered by dividing its 1.2x global dial back out). Both
// now source their base from THIS class, so they are equal by construction.
//
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// LogWarning / FlowTrace.Warn, never a hard error, if the catalog can't be read
// (the code fallback keeps spawns alive AND divergence-free).
// =============================================================================

using System;
using Newtonsoft.Json;
using DeNelle.Core;               // CanonicalJson
using DeNelle.Core.Diagnostics;   // FlowTrace

namespace DeNelle.Village
{
    /// <summary>
    /// Single source of truth for the overworld Wildlands enemy BASE stat blocks
    /// (no context multipliers applied). Backed by enemies.json (read once, cached)
    /// with a matching code fallback. See <see cref="BaseDef"/>.
    /// </summary>
    public static class WildlandsRoster
    {
        private static EnemyCatalog _catalog;
        private static bool _loaded;

        // ── THE OWNED SET (WO-1535) ────────────────────────────────────────────────
        // Every id below resolves its BASE stat block from enemies.json and NOWHERE
        // else. Widened from the single orc-raider seed (WO-????, Check H) to the NINE
        // garrison ids that carry an authored catalog row.
        //
        // ⛔ FOUR GARRISON IDS ARE DELIBERATELY *NOT* HERE — they stay code-built in
        //    GarrisonStatBlocks.BuildTypedDef, each for a named reason, and the
        //    CombatAtbRegression SSOT oracle logs them as a DATED RATCHET rather than
        //    passing them silently:
        //      necromancer        - ID COLLISION, not a stat drift. enemies.json's row is
        //                           boss:true / spawn:["wave"] / 1700 Hp "Alduin's
        //                           Necromancer"; the garrison wants a 300 Hp camp elite.
        //                           CombatAtbRegression.KnownSpawnContextViolations already
        //                           records the ruling in code: "Raising the raider to 1700
        //                           would be the WRONG fix - it drops a wave boss into a
        //                           roaming tribe... a content decision, not a gate action."
        //      caveman            - NO catalog row exists (audit finding F46), and the three
        //      feral-wolf           code tables that DO stat them disagree by ~3x
        //      tiefling-cultist     (GarrisonStatBlocks 220/90/130 Hp vs RegionMobSpawner and
        //                           CampDefenseWave 70/42/80). Seeding either side into
        //                           enemies.json would PICK a balance winner, which is the
        //                           content call WO-1535 sec.3 forbids. docs/enemy-codex.md
        //                           sec.2.10-2.12 carries only ATB-scale anchors (BaseHp 95 /
        //                           BaseAttack 19), explicitly "agent-authored - owner to
        //                           ratify", so there is no authored world stat block to seed
        //                           from either. Owner ruling required.
        private static readonly System.Collections.Generic.HashSet<string> OwnedIds =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "orc-raider",
                "troll",
                "orc-berserker",
                "orc-shaman",
                "orc-necromancer",
                "hollow-walker",
                "hollow-warrior",
                "hollow-rogue",
                "hollow-acolyte",
            };

        /// <summary>
        /// True when this roster owns <paramref name="id"/> as a single source — i.e. its
        /// BASE stat block comes from enemies.json and no code table may restate it.
        /// The CombatAtbRegression SSOT oracle (H4) cross-checks this set against the ids
        /// GarrisonStatBlocks.BuildTypedDef actually switches on, so the promise made here
        /// and the builder that relies on it cannot drift apart.
        /// </summary>
        public static bool Owns(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return OwnedIds.Contains(id.Trim());
        }

        /// <summary>The ids this roster owns, for the regression oracle to iterate.</summary>
        public static System.Collections.Generic.IEnumerable<string> OwnedIdList => OwnedIds;

        /// <summary>
        /// The canonical BASE stat block for a Wildlands id, as a FRESH
        /// <see cref="EnemyDef"/> with NO context multipliers applied. Resolves from
        /// enemies.json first (the catalog-of-record) and falls back to identical code
        /// constants when the catalog entry is absent - so every spawner reads the same
        /// base numbers whether or not the JSON is present. Callers apply their own
        /// threat / ease / global / level scaling on top.
        /// </summary>
        public static EnemyDef BaseDef(string id)
        {
            string key = string.IsNullOrEmpty(id) ? "orc-raider" : id.Trim().ToLowerInvariant();

            EnemyDef fromJson = LookupCatalog(key);
            if (fromJson != null)
            {
                // WO-1535 — PERMANENT instrumentation (CLAUDE.md sec.12). The whole point of
                // this ticket is that a stat could come from the TABLE or from a CODE
                // hardcode and nothing said which. One line per id per session names the
                // resolve source, so "did this enemy read the authored row?" is a log read,
                // never a code read. Once(), so it cannot flood a spawn loop.
                // KEYED BY ID (the 2nd arg) — Once() dedupes on system+key, so a bare system
                // tag would log the FIRST id only and silently swallow the other eight.
                FlowTrace.Once("WildlandsRoster", "base-" + key,
                    "base '" + key + "' <- enemies.json (TABLE) hp=" + fromJson.Hp.ToString("0.#") +
                    " dmg=" + fromJson.ContactDamage.ToString("0.##") +
                    " spd=" + fromJson.MoveSpeed.ToString("0.##") +
                    " int=" + fromJson.AttackInterval.ToString("0.##") +
                    " h=" + fromJson.Height.ToString("0.##") +
                    " xp=" + fromJson.XpReward);
                return Clone(fromJson);
            }

            // No row, or the catalog could not be read. There is deliberately NO per-id code
            // copy left to fall back to (WO-1535 deleted it — a silent duplicate table is the
            // defect this class exists to remove), so this is LOUD and returns the one shared
            // emergency sentinel. Never null: CampDefenseWave.cs:296, CampGuards.cs:212,
            // EnemyOutpost.cs:935 and RegionMobSpawner.cs:574 all dereference the result
            // unguarded, so a null here is four NREs in four spawners.
            FlowTrace.Fail("WildlandsRoster",
                "base '" + key + "' has NO enemies.json row (or the catalog is unreadable) — " +
                "returning the EMERGENCY SENTINEL (hp=" + SentinelHp.ToString("0.#") + "). This enemy is " +
                "NOT authored: add a row to Data/Canonical/enemies.json, or stop asking this roster for it.");
            return Sentinel(key);
        }

        private static EnemyDef LookupCatalog(string id)
        {
            EnsureLoaded();
            return _catalog != null ? _catalog.Find(id) : null;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;   // one attempt per session; a failed read stays on the fallback
            try
            {
                string json = CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
                if (!string.IsNullOrEmpty(json))
                    _catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("WildlandsRoster",
                    $"enemies.json read/parse failed ({ex.GetType().Name}: {ex.Message}) - using code fallback base stats (divergence-free).");
                _catalog = null;
            }
        }

        // A fresh EnemyDef carrying the same scalar stats. Callers read scalars into
        // their own new EnemyDef, but returning a fresh object guards the cached
        // catalog entry against any accidental mutation.
        private static EnemyDef Clone(EnemyDef s)
        {
            return new EnemyDef
            {
                Id             = s.Id,
                Name           = s.Name,
                DisplayName    = s.DisplayName,
                Family         = s.Family,
                Role           = s.Role,
                Ai             = s.Ai,
                Hp             = s.Hp,
                MoveSpeed      = s.MoveSpeed,
                ContactDamage  = s.ContactDamage,
                AttackInterval = s.AttackInterval,
                Height         = s.Height,
                AggroRadius    = s.AggroRadius,
                XpReward       = s.XpReward,
                RewardVariance = s.RewardVariance,   // WO-1103: variance travels with the base
            };
        }

        // ── THE EMERGENCY SENTINEL (WO-1535) ───────────────────────────────────────
        // ⛔ THE PER-ID CODE FALLBACK IS DELETED. It used to restate the enemies.json
        //    orc-raider row "so a missing catalog can never reintroduce the divergence" —
        //    and the 2026-08-09 audit's finding F47 is that the copy had ALREADY drifted
        //    from the row it promised to mirror (XpReward 22 vs the catalog's 24). A
        //    hand-maintained mirror of a live table is duplicated state and fails the same
        //    way every time (CLAUDE.md sec.2 / sec.5 / sec.16). The cure is not a better copy.
        //
        // What replaces it is ONE shape, for ANY unresolvable id: a visibly-generic body
        // that keeps a spawn alive and hittable while FlowTrace.Fail names the id. It is
        // deliberately NOT tuned to resemble any real enemy — if a player meets it, that is
        // a data defect and it should read as one, not masquerade as an orc raider (which
        // is exactly what the old `case "orc-raider": default:` did for EVERY id).
        public const float SentinelHp = 120f;

        private static EnemyDef Sentinel(string id)
        {
            return new EnemyDef
            {
                Id             = string.IsNullOrEmpty(id) ? "unauthored" : id,
                Name           = "Unauthored " + (string.IsNullOrEmpty(id) ? "enemy" : id),
                DisplayName    = "Unauthored " + (string.IsNullOrEmpty(id) ? "enemy" : id),
                Family         = "hollow",
                Role           = "grunt",
                Ai             = "walker",
                Hp             = SentinelHp,
                MoveSpeed      = 2.4f,
                ContactDamage  = 8f,
                AttackInterval = 1.4f,
                Height         = 1.8f,
                AggroRadius    = 14f,
                XpReward       = 15,
                RewardVariance = 0.15f,
            };
        }
    }
}
