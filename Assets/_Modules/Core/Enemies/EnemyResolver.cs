// =============================================================================
// EnemyResolver — the ONE authority that maps an enemy id -> family -> class ->
// a DISTINCT model (WO-772 Phase 1, ruling PAIN_POINTS_2026-07-26 §1.1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Enemies
//
// THE BUG THIS FIXES: EnemyFactory.ModelForEnemy hard-cased only 5 Hollow ids;
// every OTHER approved Hollow id (hollow-mage, hollow-reaper, hollow-brute,
// cellar-hollow, and the canon mini-boss hollow-apprentice) fell through to the
// size DEFAULT and spawned as a generic Skeleton_Minion / Skeleton_Golem — so
// distinct ids collapsed to the SAME generic skeleton. Routing resolution through
// this table gives each approved Hollow id its OWN model (+ variant), so
// hollow-warrior / hollow-rogue / hollow-mage / the mini-boss each spawn as
// themselves.
//
// SHARED: the same resolver serves the village wave loop AND the dungeon spawn
// path (codex "appear in the wave loop AND every dungeon") — no duplicated map.
//
// ART SOURCE (ruling): prefer the LIVE committed AccuRig family (Mage / Warrior /
// Rogue / Healer, in Resources/Enemies) + the KayKit legacy Minion / Golem /
// Necromancer that already ship. NO gitignored-KayKit dependency in Phase 1.
//
// ANIMATOR: one shared humanoid path — the AccuRig bodies all animate through
// SkeletonHumanoid; this resolver does NOT fork a controller per type (the
// runtime authority is still EnemyAnimatorFactory.RigFor(model)).
//
// Pure static, no UnityEngine — headless-provable (EnemyResolverRegression).
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.Enemies
{
    /// <summary>
    /// Maps an enemy id to its resolved family / class / distinct model. Phase 1
    /// implements the Hollow Ones; the Wildlands faction is a reserved stub.
    /// </summary>
    public static class EnemyResolver
    {
        /// <summary>The source-lint marker DataRegression prints when the resolver
        /// contract holds (every approved Hollow id resolves distinctly).</summary>
        public const string LintMarker = "ENEMY_RESOLVER_OK";

        // The base meshes the resolver is allowed to honor from enemies.json data
        // (all VERIFIED present in Assets/Resources/Enemies as of 2026-07-26). Data
        // (a modelKey field) may only OVERRIDE the class table to one of these — so a
        // typo / a Phase-2 mesh that isn't committed can never silently degrade an
        // approved Hollow spawn to a tinted capsule.
        private static readonly HashSet<string> KnownHollowModels = new HashSet<string>
        {
            "Skeleton_Minion",
            "Skeleton_Warrior",
            "Skeleton_Rogue",
            "Skeleton_Healer",
            "Skeleton_Mage",
            "Skeleton_Golem",
            "Necromancer",
        };

        // ── The Hollow Ones family/class table (codex §2, ratified 2026-07-26) ──────
        // id -> class. Shared base meshes are deliberate (codex): Reaper is a dark
        // scythe Warrior, Cellar is the sorrow-variant Minion, the Apprentice is the
        // robed Mage body — each gets a Variant so its ResolvedKey stays distinct.
        private static readonly Dictionary<string, EnemyClass> HollowTable =
            new Dictionary<string, EnemyClass>
            {
                ["hollow-walker"] = new EnemyClass
                {
                    Id = "walker", RoleKey = "grunt",
                    ModelKey = "Skeleton_Minion", Variant = null,
                    AnimatorRig = "HumanoidMedium",      // KayKit legacy Generic rig
                    Equip = EnemyEquipParts.None,
                },
                ["hollow-warrior"] = new EnemyClass
                {
                    Id = "warrior", RoleKey = "grunt",
                    ModelKey = "Skeleton_Warrior", Variant = null,
                    AnimatorRig = "SkeletonHumanoid",    // AccuRig CC_Base humanoid
                    // Phase-2 "one perfect armed type first" (A2): the Warrior is the
                    // first Hollow to get a real weapon prop. Schema only in Phase 1.
                    Equip = new EnemyEquipParts { ArmorPartKeys = System.Array.Empty<string>(), WeaponPartKey = "sword_A" },
                },
                ["hollow-rogue"] = new EnemyClass
                {
                    Id = "skirmisher", RoleKey = "skirmisher",
                    ModelKey = "Skeleton_Rogue", Variant = null,
                    AnimatorRig = "SkeletonHumanoid",    // AccuRig Ranger mesh
                    Equip = EnemyEquipParts.None,
                },
                ["hollow-acolyte"] = new EnemyClass
                {
                    Id = "acolyte", RoleKey = "caster",
                    ModelKey = "Skeleton_Healer", Variant = null,
                    AnimatorRig = "SkeletonHumanoid",
                    Equip = EnemyEquipParts.None,
                },
                ["hollow-mage"] = new EnemyClass
                {
                    Id = "caster", RoleKey = "caster",
                    ModelKey = "Skeleton_Mage", Variant = null,
                    AnimatorRig = "SkeletonHumanoid",
                    Equip = EnemyEquipParts.None,
                },
                ["hollow-reaper"] = new EnemyClass
                {
                    Id = "reaper", RoleKey = "elite",
                    ModelKey = "Skeleton_Warrior", Variant = "reaper",   // dark-tinted scythe Warrior
                    AnimatorRig = "SkeletonHumanoid",
                    Equip = new EnemyEquipParts { ArmorPartKeys = System.Array.Empty<string>(), WeaponPartKey = "scythe_A" },
                },
                ["hollow-brute"] = new EnemyClass
                {
                    Id = "brute", RoleKey = "brute",
                    ModelKey = "Skeleton_Golem", Variant = null,
                    AnimatorRig = "HumanoidLarge",       // KayKit large rig
                    Equip = EnemyEquipParts.None,
                },
                ["cellar-hollow"] = new EnemyClass
                {
                    Id = "cellar", RoleKey = "grunt",
                    ModelKey = "Skeleton_Minion", Variant = "cellar",    // sorrow variant (kneel-rock idle)
                    AnimatorRig = "HumanoidMedium",
                    Equip = EnemyEquipParts.None,
                },
                // ── Canon-locked ids (ruling: APPROVED as-written, never rename) ──
                ["necromancer"] = new EnemyClass
                {
                    Id = "wave-boss", RoleKey = "elite",
                    ModelKey = "Necromancer", Variant = null,
                    AnimatorRig = "Boss",
                    Equip = EnemyEquipParts.None,
                },
                ["hollow-apprentice"] = new EnemyClass
                {
                    Id = "mini-boss", RoleKey = "elite",
                    ModelKey = "Skeleton_Mage", Variant = "apprentice",  // robed apron body, ~1.2x
                    AnimatorRig = "SkeletonHumanoid",
                    Equip = EnemyEquipParts.None,
                },
                // Alduin the Mournful — canon-locked, DIALOGUE NPC (codex §4.8: "not a
                // boss fight"). Registered so the id resolves to a body when he shows one
                // of his faces, but flagged non-combat so distinctness oracles skip it.
                ["alduin"] = new EnemyClass
                {
                    Id = "antagonist", RoleKey = "elite",
                    ModelKey = "Necromancer", Variant = "alduin",
                    AnimatorRig = "Boss",
                    CombatSpawnable = false,
                    Equip = EnemyEquipParts.None,
                },
            };

        // Combat-spawnable approved Hollow ids, in codex roster order (the set the
        // distinctness oracle + the PlayMode spawn test iterate). Alduin excluded.
        private static readonly string[] _approvedHollowCombatIds =
        {
            "hollow-walker", "hollow-warrior", "hollow-rogue", "hollow-acolyte",
            "hollow-mage", "hollow-reaper", "hollow-brute", "cellar-hollow",
            "necromancer", "hollow-apprentice",
        };

        /// <summary>The combat-spawnable approved Hollow ids (codex roster order).</summary>
        public static IReadOnlyList<string> ApprovedHollowCombatIds => _approvedHollowCombatIds;

        // ── The declared families (codex §1.1 + the reserved Wildlands stub) ────────
        private static readonly EnemyFamily HollowOnesFamily = new EnemyFamily
        {
            Id = "hollow-ones", DisplayName = "The Hollow Ones", Faction = EnemyFaction.HollowOnes,
            MemberClassIds = new[]
            {
                "walker", "warrior", "skirmisher", "acolyte", "caster",
                "reaper", "brute", "cellar", "wave-boss", "mini-boss",
            },
        };

        // RESERVED STUB — Phase 2. Empty membership by design; no art dependency.
        private static readonly EnemyFamily WildlandsFamily = new EnemyFamily
        {
            Id = "wildlands", DisplayName = "The Wildlands", Faction = EnemyFaction.Wildlands,
            MemberClassIds = System.Array.Empty<string>(),
        };

        /// <summary>The declared enemy families (Hollow Ones + the Wildlands stub).</summary>
        public static IReadOnlyList<EnemyFamily> Families => new[] { HollowOnesFamily, WildlandsFamily };

        private static string Norm(string id) =>
            string.IsNullOrEmpty(id) ? "" : id.Trim().ToLowerInvariant();

        /// <summary>True when <paramref name="id"/> is an approved Hollow id this
        /// resolver owns (combat or the Alduin NPC).</summary>
        public static bool IsHollowId(string id) => HollowTable.ContainsKey(Norm(id));

        /// <summary>Maps an enemies.json <c>family</c> token to the typed faction.
        /// Absent / "hollow" ⇒ HollowOnes; every living token (orc / troll / ogre /
        /// beast / cult / caveman / wolf / tiefling) ⇒ the reserved Wildlands stub.</summary>
        public static EnemyFaction FactionForFamily(string family)
        {
            switch (Norm(family))
            {
                case "":
                case "hollow":
                case "hollow-ones":
                case "undead":
                    return EnemyFaction.HollowOnes;
                default:
                    return EnemyFaction.Wildlands;   // Phase-2 stub — no Phase-1 members
            }
        }

        /// <summary>
        /// THE FACTORY HOOK. Resolves an approved Hollow id to its base Resources
        /// mesh. <paramref name="dataModelKey"/> is the enemies.json <c>modelKey</c>
        /// (A4 data-driven variety): when it names a KNOWN committed Hollow mesh it
        /// WINS over the class table (so authors tune variety in data); otherwise the
        /// class-table canonical model is used. Returns false for any non-Hollow id —
        /// the caller keeps its existing (Wildlands / size-default) path untouched.
        /// </summary>
        public static bool TryResolveHollowModel(string id, string dataModelKey, out string model)
        {
            model = null;
            if (!HollowTable.TryGetValue(Norm(id), out var cls) || cls == null) return false;

            if (!string.IsNullOrEmpty(dataModelKey) && KnownHollowModels.Contains(dataModelKey))
                model = dataModelKey;      // data wins (A4) — but only to a committed mesh
            else
                model = cls.ModelKey;      // class-table canonical
            return true;
        }

        /// <summary>
        /// Full resolution for an approved Hollow id — the family/class/model/variant/
        /// equip identity used by oracles + future dungeon/raid consumers. Returns
        /// null for a non-Hollow id.
        /// </summary>
        public static ResolvedEnemyModel Resolve(string id, string dataModelKey = null)
        {
            if (!HollowTable.TryGetValue(Norm(id), out var cls) || cls == null) return null;

            string baseModel = cls.ModelKey;
            if (!string.IsNullOrEmpty(dataModelKey) && KnownHollowModels.Contains(dataModelKey))
                baseModel = dataModelKey;

            return new ResolvedEnemyModel
            {
                EnemyId = Norm(id),
                Faction = cls.CombatSpawnable ? EnemyFaction.HollowOnes
                         : EnemyFaction.Boss,   // Alduin = antagonist tier
                ClassId = cls.Id,
                RoleKey = cls.RoleKey,
                ModelKey = baseModel,
                Variant = cls.Variant,
                AnimatorRig = cls.AnimatorRig,
                IsCombatSpawnable = cls.CombatSpawnable,
                Equip = cls.Equip ?? EnemyEquipParts.None,
            };
        }
    }
}
