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
            float scale = 1f + 0.10f * Mathf.Max(0, threat);
            return new EnemyDef
            {
                Id = "troll",
                Name = "Garrison Troll",
                DisplayName = "Garrison Troll",
                Family = "troll",
                Role = "brute",
                Ai = "charger",
                Hp = 320f * scale * GlobalDifficultyMult,
                MoveSpeed = 1.8f,
                ContactDamage = 14f * scale * GlobalDifficultyMult,
                AttackInterval = 1.8f,
                Height = 2.6f,
                AggroRadius = 15f,
                XpReward = 34 + threat * 2,
                GlimmerReward = 5,
            };
        }

        // "Stonebelly" — a leaner, faster troll-family raider (still the Troll model,
        // smaller silhouette) so a garrison reads as a varied fight, not clones.
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
                XpReward = 22 + threat * 2,
                GlimmerReward = 4,
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
            float dmgScale = 1f + 0.05f * over;
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
                case "troll":           return BuildTrollDef(2);
                case "orc-berserker":   return BuildGenericDef(id, "Orc Berserker", "orc", "brute",      "charger",    260f, 2.2f, 13f, 1.7f, 2.4f, 30);
                case "orc-shaman":      return BuildGenericDef(id, "Orc Shaman",    "orc", "caster",     "skirmisher", 150f, 2.4f,  9f, 1.4f, 1.9f, 28);
                case "orc-necromancer": return BuildGenericDef(id, "Orc Necromancer","orc","elite",      "skirmisher", 220f, 2.0f, 11f, 1.6f, 2.1f, 40);
                case "orc-raider":      return BuildGenericDef(id, "Orc Raider",    "orc", "skirmisher", "skirmisher", 170f, 2.8f, 10f, 1.3f, 1.9f, 24);
                case "hollow-walker":   return BuildGenericDef(id, "Hollow Walker", "hollow","grunt",    "walker",     120f, 2.4f,  8f, 1.4f, 1.8f, 18);
                case "hollow-warrior":  return BuildGenericDef(id, "Hollow Warrior","hollow","grunt",    "walker",     156f, 2.2f, 10f, 1.3f, 1.88f, 24);
                case "hollow-rogue":    return BuildGenericDef(id, "Hollow Rogue",  "hollow","skirmisher","skirmisher",110f, 3.0f,  9f, 1.1f, 1.7f, 22);
                case "hollow-acolyte":  return BuildGenericDef(id, "Hollow Acolyte","hollow","caster",   "skirmisher", 140f, 2.3f,  8f, 1.4f, 1.8f, 26);
                case "necromancer":     return BuildGenericDef(id, "Necromancer",   "hollow","elite",    "skirmisher", 300f, 2.0f, 12f, 1.6f, 2.1f, 50);
                case "caveman":         return BuildGenericDef(id, "Caveman",       "tribe","brute",     "charger",    220f, 2.2f, 11f, 1.6f, 2.3f, 24);
                case "feral-wolf":      return BuildGenericDef(id, "Feral Wolf",    "beast","skirmisher","skirmisher", 90f,  3.4f,  8f, 1.0f, 1.4f, 16);
                case "tiefling-cultist":return BuildGenericDef(id, "Tiefling Cultist","cult","caster",   "skirmisher", 130f, 2.4f,  9f, 1.4f, 1.8f, 24);
                default:
                    Debug.LogWarning($"[GarrisonStatBlocks] Unknown recipe enemy id '{id}' — using a generic brute (EnemyFactory will model-map or capsule-fallback it).");
                    return BuildGenericDef(id, id, "troll", "brute", "charger", 220f, 2.0f, 11f, 1.6f, 2.2f, 26);
            }
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
                XpReward = xp,
                GlimmerReward = 5,
            };
        }
    }
}
