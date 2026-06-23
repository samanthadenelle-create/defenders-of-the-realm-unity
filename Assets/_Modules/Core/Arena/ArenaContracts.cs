// =============================================================================
// ArenaContracts — the BOUNDED-CONTEXT data boundary for the battle module.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Arena
//
// Owner-ratified architecture (2026-06-23, "JSON in, JSON out"): the BattleArena
// is a bounded context. The ONLY thing that crosses the seam is serialized data —
// an ArenaRequest in, an ArenaResult out. The arena never holds a reference to a
// world object and the world never reaches into the arena; even the HERO crosses
// as data (the arena REBUILDS the Knight from ArenaRequest.hero, it does not warp
// the live GameObject). This is what makes combat provably HEADLESS-testable:
// feed a request JSON to ArenaSim/DataRegression, assert the result JSON — no open
// world required. Spec: WorkOrders/WORK_ORDER_482_battlearena_bounded_json_module.md.
//
// These are PLAIN DTOs (no UnityEngine types — Vec3 instead of Vector3 keeps the
// JSON clean and keeps Core's contract free of engine coupling). Serialized via the
// same Newtonsoft path the other catalogs use (ArenaJson, added with ArenaSim).
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.Arena
{
    /// <summary>Clean, engine-free 3-vector for the data boundary (avoids Newtonsoft
    /// serializing UnityEngine.Vector3's self-referencing normalized/magnitude props).</summary>
    public struct Vec3
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    // ── REQUEST (world -> arena) ──────────────────────────────────────────────

    /// <summary>Everything the arena needs to stage + run one isolated battle. Built by the
    /// engage hook (RepEngageWatcher), consumed by the arena scene. The hero is rebuilt FROM
    /// this data — no live object crosses.</summary>
    public sealed class ArenaRequest
    {
        /// <summary>The hero to rebuild inside the arena (class/level/loadout/HP).</summary>
        public HeroSpec Hero = new HeroSpec();

        /// <summary>The enemy family staged against the hero.</summary>
        public List<FamilyMemberSpec> Family = new List<FamilyMemberSpec>();

        /// <summary>Backdrop theme for the arena dressing: "castle" | "outerworld" | "cavern".
        /// Matches the scene the player engaged in so the fight never looks like a void.</summary>
        public string Context = "outerworld";

        /// <summary>Where to port the hero back to when the battle resolves.</summary>
        public ReturnSpec Return = new ReturnSpec();

        /// <summary>The throttle dial — count/difficulty/fidelity, driven by the progression
        /// seed-budget. The world stays cheap; ALL battle spend is scaled here.</summary>
        public ScaleSpec Scale = new ScaleSpec();

        /// <summary>Deterministic repro seed — same seed + request => identical fight (headless regression).</summary>
        public int Seed;
    }

    public sealed class HeroSpec
    {
        public string Class = "Knight";   // V1: Knight
        public int Level = 1;
        public string[] Loadout;          // 4 ability ids (slot 0 = Q basic attack, locked)
        public float CurrentHp = -1f;     // <0 => full (MaxHp from class/level)
    }

    public sealed class FamilyMemberSpec
    {
        public string Id;                 // e.g. "orc-warrior" / "orc-tank" / "orc-mage"
        public int Level = 1;
        public string Role;               // optional explicit role override (else derived from Id)
    }

    public sealed class ReturnSpec
    {
        public string Scene = "OuterWorld";
        public Vec3 Position;
        public float Yaw;
    }

    public sealed class ScaleSpec
    {
        public int EnemyCount = -1;       // <0 => use Family.Count as-is
        public int LevelBand = 1;         // progression threat band
        public int AiBudget = 0;          // strategy/aggression points (0 = baseline)
        public string QualityTier = "auto"; // "low" | "high" | "auto" (mobile-down / desktop-up)
    }

    // ── RESULT (arena -> world) ───────────────────────────────────────────────

    /// <summary>The outcome the world applies after the battle (rewards, hero HP). Serialized
    /// out of the arena; the source world stays resident and consumes this.</summary>
    public sealed class ArenaResult
    {
        /// <summary>"win" | "lose" | "flee".</summary>
        public string Outcome = "lose";

        public int EnemiesDowned;
        public float Duration;

        public RewardSpec Rewards = new RewardSpec();

        public HeroEndSpec HeroEndState = new HeroEndSpec();

        public bool Won => Outcome == "win";
    }

    public sealed class RewardSpec
    {
        public int Xp;
        public int SkillPoints;                 // Wisdom currency for the tree
        public List<string> Gear = new List<string>();
        public Dictionary<string, int> Resources = new Dictionary<string, int>(); // wood/iron/grain etc.
    }

    public sealed class HeroEndSpec
    {
        public float Hp;
    }
}
