// =============================================================================
// GarrisonStatBlocks — the SHARED enemy stat-block + level-scale helpers for the
// garrison family (Troll/Stonebelly templates, the recipe id -> EnemyDef map, and
// the level-scale fold). EXTRACTED VERBATIM from GarrisonController so BOTH the
// additive-scene GarrisonController AND the config-driven RaidGarrisonSpawner build
// their defenders from the EXACT same numbers — no duplication, no drift.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// These were private static helpers on GarrisonController; they carry NO instance
// state (pure def builders + an in-place scaler), so promoting them to a static
// helper class is behaviour-identical. GarrisonController now forwards to these.
//
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// LogWarning, never error, on an unknown recipe id (EnemyFactory model-maps or
// capsule-falls-back it — never a crash).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;   // WO-1530 — the PERMANENT [Flow:EnemyScale] spawn measurement
// EnemyDef lives in the parent namespace DeNelle.Village, visible here because
// DeNelle.Village.World.Camps nests under it.

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Shared, stateless stat-block builders for garrison defenders + the level-scale
    /// fold. <see cref="GarrisonController"/> and <see cref="RaidGarrisonSpawner"/>
    /// both call these so a garrison reads identically however it is spawned.
    /// </summary>
    public static class GarrisonStatBlocks
    {
        // WO-433 — the single global difficulty dial for ALL garrison/raid defenders. Multiplies HP +
        // contact damage on top of each enemy's base stats AND the per-level ApplyLevelScale (which
        // already adds ~8% HP / ~5% dmg per level — do NOT duplicate that here). ONE knob, tune + rebuild:
        //   1.0  = current live feel        1.2-1.4 = solid Village2 / early-outpost challenge
        //   1.6+ = hard late-game raids
        public const float GlobalDifficultyMult = 1.2f;

        // =====================================================================
        // Stat blocks (code-built EnemyDef, threat-scaled) — the Troll family.
        // Mirrors EnemyOutpost.BuildGuardDef / CampGuards so garrison defenders
        // read like every other open-world enemy. EnemyFactory maps id=="troll"
        // to the "Troll" model (capsule fallback if the mesh is not imported).
        // =====================================================================

        public static EnemyDef BuildTrollDef(int threat)
        {
            // WO-1535 — SSOT: the BASE scalars come from enemies.json ('troll'), never a
            // literal here. The THREAT fold and GlobalDifficultyMult are this path's own
            // CONTEXT multipliers and stay: they are not a second stat table, they are what
            // makes a garrison troll a garrison troll. The garrison-authored identity
            // strings (Name/Family/Role/Ai) also stay — the orc-raider precedent is that
            // this ticket unifies the STAT table, not the behaviour/flavour tokens.
            var b = WildlandsRoster.BaseDef("troll");
            float scale = 1f + 0.10f * Mathf.Max(0, threat);
            return new EnemyDef
            {
                Id = "troll",
                Name = "Garrison Troll",
                DisplayName = "Garrison Troll",
                Family = "troll",
                Role = "brute",
                Ai = "charger",
                Hp = b.Hp * scale * GlobalDifficultyMult,
                MoveSpeed = b.MoveSpeed,
                ContactDamage = b.ContactDamage * scale * GlobalDifficultyMult,
                AttackInterval = b.AttackInterval,
                Height = b.Height,
                AggroRadius = 15f,
                XpReward = b.XpReward + threat * 2
            };
        }

        // "Stonebelly" — a leaner, faster troll-family raider (still the Troll model,
        // smaller silhouette) so a garrison reads as a varied fight, not clones.
        //
        // ⚠ WO-1535 DELIBERATELY LEFT THESE NUMBERS IN CODE, and it is NOT an oversight.
        //   Stonebelly borrows id "troll" for the MODEL MAP only — it is a DIFFERENT
        //   creature (180/10 against the Garrison Troll's 320/14), so pointing it at the
        //   enemies.json 'troll' row would not unify a table, it would DELETE an enemy.
        //   The honest fix is its own id + its own catalog row; that is a content call
        //   (a new stable save/spawn key), not this ticket. Same shape as the
        //   'necromancer' collision below, but comment-sanctioned rather than accidental.
        //   The SSOT oracle therefore asserts BuildTypedDef("troll"), not this builder.
        public static EnemyDef BuildStonebellyDef(int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);
            return new EnemyDef
            {
                Id = "troll",                 // EnemyFactory model map: troll -> "Troll"
                Name = "Stonebelly Raider",
                DisplayName = "Stonebelly Raider",
                Family = "troll",
                Role = "skirmisher",
                Ai = "skirmisher",
                Hp = 180f * scale * GlobalDifficultyMult,
                MoveSpeed = 2.6f,
                ContactDamage = 10f * scale * GlobalDifficultyMult,
                AttackInterval = 1.3f,
                Height = 2.1f,
                AggroRadius = 16f,
                XpReward = 22 + threat * 2
            };
        }

        // Apply the rolled level on top of whatever base def we already built. Level 0/1 is
        // a no-op (keeps legacy garrisons identical). Each level above 1 adds ~8% HP and
        // ~5% contact damage + a touch of size so higher-level forts read as tougher.
        public static void ApplyLevelScale(EnemyDef def, int level)
        {
            if (def == null || level <= 1) return;
            int over = level - 1;
            float hpScale  = 1f + 0.08f * over;
            // Towers are the raid's primary defensive threat. Guard HP continues to scale,
            // but their contact damage reaches a ceiling so hero-level scaling cannot make
            // ordinary defenders instantly erase a fully upgraded army.
            float earlyDamageLevels = Mathf.Min(over, 10);
            float lateDamageLevels  = Mathf.Min(Mathf.Max(0, over - 10), 10);
            float dmgScale = 1f + 0.04f * earlyDamageLevels + 0.02f * lateDamageLevels;
            def.Hp            *= hpScale;
            def.ContactDamage *= dmgScale;
            def.Height         = def.Height * (1f + 0.012f * over);
            def.XpReward      += over * 3;       // higher level => more XP
            def.DisplayName    = (string.IsNullOrEmpty(def.DisplayName) ? def.Name : def.DisplayName)
                                 + " (Lv " + level + ")";
        }

        // A stat block for an arbitrary recipe enemy id. Known family ids reuse a matching
        // template; everything else gets a generic mid-tier brute (still a real, hittable
        // Enemy — EnemyFactory.ModelForEnemy maps the id to a model or a capsule fallback).
        public static EnemyDef BuildTypedDef(string id, int level)
        {
            string key = (id ?? "troll").ToLowerInvariant();
            switch (key)
            {
                // ── RESOLVED FROM THE TABLE (WO-1535) ────────────────────────────────
                // Nine ids. Scalars come from enemies.json via WildlandsRoster.BaseDef;
                // the identity strings stay garrison-authored (the orc-raider precedent
                // established that this ticket unifies the STAT table, not the flavour).
                // BuildGenericDef still folds in GlobalDifficultyMult — that is this
                // path's CONTEXT multiplier, not a second stat table. The SSOT oracle
                // asserts row * fold == built (it does NOT divide the fold back out), so
                // a hidden extra multiplier fails there instead of hiding in a division.
                case "troll":           return BuildTrollDef(2);
                case "orc-berserker":   return FromTable(id, "Orc Berserker",   "orc",    "brute",      "charger");
                case "orc-shaman":      return FromTable(id, "Orc Shaman",      "orc",    "caster",     "skirmisher");
                case "orc-necromancer": return FromTable(id, "Orc Necromancer", "orc",    "elite",      "skirmisher");
                case "orc-raider":      return FromTable(id, "Orc Raider",      "orc",    "skirmisher", "skirmisher");
                case "hollow-walker":   return FromTable(id, "Hollow Walker",   "hollow", "grunt",      "walker");
                case "hollow-warrior":  return FromTable(id, "Hollow Warrior",  "hollow", "grunt",      "walker");
                case "hollow-rogue":    return FromTable(id, "Hollow Rogue",    "hollow", "skirmisher", "skirmisher");
                case "hollow-acolyte":  return FromTable(id, "Hollow Acolyte",  "hollow", "caster",     "skirmisher");

                // ── PINNED, CODE-BUILT — EACH AWAITING AN OWNER CONTENT RULING ────────
                // ⛔ DO NOT "finish the migration" by pointing these four at the table.
                //    They are not leftovers; each is a recorded reason, and the
                //    CombatAtbRegression SSOT oracle carries the same list as a DATED
                //    RATCHET so a NEW hardcode still hard-fails.
                //
                // necromancer — an ID COLLISION, not a stat drift. enemies.json's row is
                //   the boss:true / spawn:["wave"] / 1700 Hp "Alduin's Necromancer"; this
                //   is a 300 Hp camp elite. CombatAtbRegression already records the ruling
                //   in code (KnownSpawnContextViolations): "Raising the raider to 1700
                //   would be the WRONG fix — it drops a wave boss into a roaming tribe...
                //   a content decision, not a gate action." Migrating it here would put a
                //   2040 Hp boss (1700 x 1.2) into an ordinary raid garrison.
                case "necromancer":     return BuildGenericDef(id, "Necromancer",   "hollow","elite",    "skirmisher", 300f, 2.0f, 12f, 1.6f, 2.1f, 50);

                // caveman / feral-wolf / tiefling-cultist — NO enemies.json row exists
                //   (audit finding F46), and the three code tables that DO stat them
                //   disagree by ~3x: here 220/90/130 Hp, RegionMobSpawner.cs:579-584 and
                //   CampDefenseWave.cs:301-306 both 70/42/80. Seeding either side into the
                //   catalog would PICK a balance winner — the content call WO-1535 sec.3
                //   forbids ("do not migrate by copying the hardcoded values into
                //   enemies.json; that preserves the divergence and calls it authored").
                //   docs/enemy-codex.md sec.2.10-2.12 carries only ATB-scale anchors
                //   (BaseHp 95 / BaseAttack 19), explicitly "agent-authored — owner to
                //   ratify", so there is no authored world stat block to seed from either.
                case "caveman":         return BuildGenericDef(id, "Caveman",       "tribe","brute",     "charger",    220f, 2.2f, 11f, 1.6f, 2.3f, 24);
                case "feral-wolf":      return BuildGenericDef(id, "Feral Wolf",    "beast","skirmisher","skirmisher", 90f,  3.4f,  8f, 1.0f, 1.4f, 16);
                case "tiefling-cultist":return BuildGenericDef(id, "Tiefling Cultist","cult","caster",   "skirmisher", 130f, 2.4f,  9f, 1.4f, 1.8f, 24);

                default:
                    // NOT a stat table — the last-resort body for an id nobody authored
                    // anywhere. Kept deliberately generic and warned about by name.
                    Debug.LogWarning($"[GarrisonStatBlocks] Unknown recipe enemy id '{id}' — using a generic brute (EnemyFactory will model-map or capsule-fallback it).");
                    return BuildGenericDef(id, id, "troll", "brute", "charger", 220f, 2.0f, 11f, 1.6f, 2.2f, 26);
            }
        }

        /// <summary>
        /// WO-1535 — build a garrison def whose SCALARS come from the enemies.json SSOT
        /// (<see cref="WildlandsRoster.BaseDef"/>) and whose identity strings stay
        /// garrison-authored. The one seam every migrated id goes through, so there is
        /// exactly one place a stat can enter this class from.
        /// </summary>
        private static EnemyDef FromTable(string id, string display, string family, string role, string ai)
        {
            var b = WildlandsRoster.BaseDef(id);
            return BuildGenericDef(id, display, family, role, ai,
                b.Hp, b.MoveSpeed, b.ContactDamage, b.AttackInterval, b.Height, b.XpReward);
        }

        // =====================================================================
        // WO-1530 — THE ONE PERMANENT SPAWN MEASUREMENT. Every garrison/raid defender
        // passes through here immediately before EnemyFactory.Build, so a single
        // [Flow:EnemyScale] line names the WHOLE chain the player actually meets:
        //
        //   built   = BuildTypedDef/BuildTrollDef/BuildStonebellyDef output. ALREADY
        //             carries GlobalDifficultyMult (x1.2) folded in by the builder —
        //             it is NOT the authored literal, and is deliberately NOT divided
        //             back out here (measure, never reconstruct).
        //   lvl     = after ApplyLevelScale(def, level).
        //   final   = after the caller's own folds (RaidGarrisonSpawner.FoldDifficulty,
        //             the boss HP/damage multipliers). Read straight off the def that
        //             is handed to EnemyFactory.
        //
        // PERMANENT instrumentation (CLAUDE.md §12) — flag it off, never strip it.
        // =====================================================================
        public static void TraceSpawnScale(string context, EnemyDef def, int level,
            float builtHp, float builtDmg, float leveledHp, float leveledDmg)
        {
            if (def == null) return;
            FlowTrace.Step("EnemyScale",
                (string.IsNullOrEmpty(context) ? "spawn" : context) +
                " id='" + def.Id + "' name='" + def.Name + "' lv=" + level +
                " | hp built=" + builtHp.ToString("0.#") +
                " -> lvl=" + leveledHp.ToString("0.#") +
                " -> final=" + def.Hp.ToString("0.#") +
                " | dmg built=" + builtDmg.ToString("0.##") +
                " -> lvl=" + leveledDmg.ToString("0.##") +
                " -> final=" + def.ContactDamage.ToString("0.##") +
                " | built already includes GlobalDifficultyMult x" +
                GlobalDifficultyMult.ToString("0.##"));
        }

        public static EnemyDef BuildGenericDef(string id, string display, string family, string role,
            string ai, float hp, float moveSpeed, float dmg, float interval, float height, int xp)
        {
            return new EnemyDef
            {
                Id = id,
                Name = display,
                DisplayName = display,
                Family = family,
                Role = role,
                Ai = ai,
                Hp = hp * GlobalDifficultyMult,
                MoveSpeed = moveSpeed,
                ContactDamage = dmg * GlobalDifficultyMult,
                AttackInterval = interval,
                Height = height,
                AggroRadius = 15f,
                XpReward = xp
            };
        }
    }
}
