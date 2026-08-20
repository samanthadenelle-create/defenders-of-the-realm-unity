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

        // ── THE COMMITTED-MESH REGISTRY (WO-954) ───────────────────────────────────
        // Every mesh/prefab that ACTUALLY EXISTS under Assets/Resources/Enemies (verified
        // from the directory listing 2026-08-14, all TRACKED — none of these live under a
        // gitignored vendor root, so a fresh clone loads them). This is what makes the
        // enemies.json `modelKey` field SAFE to honour for EVERY family, not just the
        // Hollows: data may only steer to a name in here, so a typo, a renamed FBX, or a
        // row naming art that was never imported can never silently degrade a spawn to a
        // tinted capsule — it is REJECTED, and the caller says so by name (§1.4b).
        //
        // PROVING ROW: enemies.json's `ogre` row asks for modelKey "OgreMage", and there is
        // NO OgreMage.fbx in Resources/Enemies. Without this gate a data-first resolve would
        // return "OgreMage" and the ogre would spawn as an untextured capsule; with it the
        // key is rejected by name and EnemyFactory's documented Orc_Shaman stand-in is used.
        //
        // Keep in sync with Assets/Resources/Enemies (EnemyResolverRegression check 11
        // Resources.Loads every name below and FAILS on a missing one, so this set cannot
        // rot silently). Pure strings — DeNelle.Core carries no UnityEngine dependency.
        private static readonly HashSet<string> CommittedModels = new HashSet<string>
        {
            // Hollow Ones (KayKit legacy + AccuRig)
            "Skeleton_Minion", "Skeleton_Warrior", "Skeleton_Rogue", "Skeleton_Healer",
            "Skeleton_Mage", "Skeleton_Golem", "Skeleton_Golem_NEW",
            "Necromancer", "Necromancer_NEW",
            // Orc Warband (Tripo) + WO-481 orc family + the outpost raid boss
            "Orc_Berserker", "Orc_Shaman", "Orc_Necromancer",
            "Orc_Warrior", "Orc_Tank", "Orc_Mage", "Orc_Warlord",
            // Troll / Stonebelly (AccuRig, 2026-08-09)
            "Troll", "Troll_Mage", "Troll_Overlord",
            // Misc committed bodies
            "Demon", "Boss_Dragon",
            // ⛔ THE FOUR BLINK ORCS WERE REMOVED 2026-08-18 (owner ruling) — do not re-add here
            // without also re-staging the art, or every key listed in this registry that cannot
            // resolve spawns a CAPSULE in front of a player.
            // WHY THEY WENT: Resources/Enemies/Blink was 427 MB and Unity FORCE-INCLUDES everything
            // under a Resources/ folder, used or not. An audit found the ids blink-orc-warrior /
            // hunter / warlock / boss in NO enemies.json row — the only things that could spawn them
            // were EnemyFamilyTestSpawner (a debug side-by-side comparison) and an editor capture
            // tool. 427 MB shipped in every APK for a debug spawner.
            // ⚠ REVERSIBLE, which is why removal was safe: the vendor pack at Assets/Blink/ is
            // gitignored but PRESENT, and Assets/Editor/BlinkOrcImporter.cs is intact — re-running it
            // re-stages all four. Unlike the owner-purchased Tripo art (identified by its
            // .tripo-extracted marker, of which these had NONE), the Resources copy was never the
            // only copy.
            // ⚠ UNRELATED TO THE OBSIDIAN UI, despite sharing the "Blink" name: that ships from
            // Assets/Blink/Art/UI/Obsidian_UI via BlinkUiImporter into Resources/RpgUi, is consumed
            // by ElarionUiKit, and is untouched by this.
        };

        /// <summary>The committed Resources/Enemies mesh keys data is allowed to name.</summary>
        public static IReadOnlyCollection<string> CommittedModelKeys => CommittedModels;

        /// <summary>True when <paramref name="modelKey"/> names a mesh committed under
        /// Resources/Enemies (i.e. it is safe for data to steer a spawn to it).</summary>
        public static bool IsCommittedModel(string modelKey) =>
            !string.IsNullOrEmpty(modelKey) && CommittedModels.Contains(modelKey);

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
                    ModelKey = "Skeleton_Golem_NEW", Variant = null,   // WO-954: retired the KayKit mesh (owner 2026-08-19 "I hate the KayKat enemies"); same character, Tripo re-make
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
                    ModelKey = "Necromancer_NEW", Variant = null,      // WO-954: retired the KayKit mesh; _NEW also fixes the legacy Generic rig -> Humanoid
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
                // ── DUNGEON LAYOUT ALIASES (underscore ids, ratified 2026-07-26) ──
                // healers-cottage.json (+ the stub dungeons) name their Hollow encounters
                // with UNDERSCORE ids the village roster never uses. Registered here so
                // each resolves to its OWN distinct model instead of collapsing to the
                // size-default Skeleton_Minion in EnemyFactory.ModelForEnemy. Norm() folds
                // '_' -> '-', so the JSON's hollow_villager_a arrives here as
                // hollow-villager-a. villager-a -> Walker body, villager-b -> Rogue body
                // (two distinct Folk silhouettes); apprentice-minor -> the robed Apprentice
                // mini-boss body; healer -> the Acolyte. Four DISTINCT ResolvedKeys.
                ["hollow-villager-a"] = new EnemyClass
                {
                    Id = "villager-a", RoleKey = "grunt",
                    ModelKey = "Skeleton_Minion", Variant = null,
                    AnimatorRig = "HumanoidMedium",
                    Equip = EnemyEquipParts.None,
                },
                ["hollow-villager-b"] = new EnemyClass
                {
                    Id = "villager-b", RoleKey = "skirmisher",
                    ModelKey = "Skeleton_Rogue", Variant = null,
                    AnimatorRig = "SkeletonHumanoid",
                    Equip = EnemyEquipParts.None,
                },
                ["hollow-apprentice-minor"] = new EnemyClass
                {
                    Id = "apprentice-minor", RoleKey = "elite",
                    ModelKey = "Skeleton_Mage", Variant = "apprentice",  // shares the mini-boss robed body
                    AnimatorRig = "SkeletonHumanoid",
                    Equip = EnemyEquipParts.None,
                },
                ["hollow-healer"] = new EnemyClass
                {
                    Id = "healer", RoleKey = "caster",
                    ModelKey = "Skeleton_Healer", Variant = null,
                    AnimatorRig = "SkeletonHumanoid",
                    Equip = EnemyEquipParts.None,
                },
                // Alduin the Mournful — canon-locked, DIALOGUE NPC (codex §4.8: "not a
                // boss fight"). Registered so the id resolves to a body when he shows one
                // of his faces, but flagged non-combat so distinctness oracles skip it.
                ["alduin"] = new EnemyClass
                {
                    Id = "antagonist", RoleKey = "elite",
                    ModelKey = "Necromancer_NEW", Variant = "alduin",  // WO-954: same swap - alduin is a VARIANT of the same character, so it follows the base mesh
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

        // Normalizes an id/family token: trim + lowercase, AND fold '_' -> '-'. The
        // dungeon layouts (healers-cottage.json scriptedEncounters) key Hollow ids with
        // UNDERSCORES (hollow_villager_a / hollow_apprentice_minor / hollow_healer) while
        // the HollowTable + village roster use HYPHENS — so without this fold those
        // dungeon ids miss the table, bypass TryResolveHollowModel, and collapse to the
        // generic size-default Skeleton_Minion in EnemyFactory.ModelForEnemy (the exact
        // distinctness the resolver exists to prevent). Folding here makes both spellings
        // resolve to the same canonical key.
        private static string Norm(string id) =>
            string.IsNullOrEmpty(id) ? "" : id.Trim().ToLowerInvariant().Replace('_', '-');

        /// <summary>True when <paramref name="id"/> is an approved Hollow id this
        /// resolver owns (combat or the Alduin NPC).</summary>
        public static bool IsHollowId(string id) => HollowTable.ContainsKey(Norm(id));

        // ── WILDLANDS DEFERRAL GATE (PAIN_POINTS_2026-07-26 §1.1, BINDING) ──────────
        // The living Wildlands roster is DEFERRED — it has NO shippable art (the
        // Orc_Berserker Tripo body retargets to EXPLODED geometry), so these ids must
        // NOT spawn as themselves. Every spawner funnels through EnemyFactory.Build,
        // which asks IsCombatApproved and redirects a deferred id to a ratified Hollow
        // substitute. Keyed to the Wildlands faction (FactionForFamily maps their family
        // token -> EnemyFaction.Wildlands); listed EXPLICITLY because the region roamer
        // path (RegionMobSpawner.BuildRoamerDef) leaves def.Family at its default, so the
        // ID is the only reliable signal at the Build chokepoint. The SHIPPING Orc Warband
        // (orc-berserker / orc-shaman / …) + bosses are NOT in this set — they stay approved.
        private static readonly HashSet<string> _deferredWildlandsIds =
            new HashSet<string>
            {
                "orc-raider", "caveman", "feral-wolf", "tiefling-cultist",
            };

        /// <summary>
        /// False for a DEFERRED Wildlands id (PAIN_POINTS §1.1: no shippable art) — the
        /// caller (EnemyFactory.Build) redirects these to a ratified Hollow substitute so
        /// no exploded/placeholder living body ever spawns. True for every approved id
        /// (the Hollow Ones + the shipping Orc Warband + bosses).
        /// </summary>
        public static bool IsCombatApproved(string id) =>
            !_deferredWildlandsIds.Contains(Norm(id));

        /// <summary>
        /// The ratified Hollow stand-in id a DEFERRED Wildlands request is redirected to,
        /// chosen by the requested archetype so the region still reads varied: a heavy /
        /// melee body (brute/elite/charger role, or a tall silhouette) becomes the armed
        /// <c>hollow-warrior</c>; anything basic/light becomes <c>hollow-walker</c>. Both
        /// targets are ratified, real-art Hollow ids that resolve through this table — so
        /// the substituted body is always valid.
        /// </summary>
        public static string SubstituteHollowId(string deferredId, string roleKey, float height)
        {
            string role = Norm(roleKey);
            bool heavy = role == "brute" || role == "elite" || role == "warrior"
                      || role == "charger" || role == "tank" || height >= 1.8f;
            return heavy ? "hollow-warrior" : "hollow-walker";
        }

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
        /// THE DATA-FIRST HOOK for EVERY family (WO-954). Where TryResolveHollowModel only
        /// serves ids in the Hollow class table, this honours the enemies.json
        /// <c>modelKey</c> for ANY id — orc, troll, ogre, a future faction — provided the
        /// key names a mesh in <see cref="CommittedModels"/>.
        ///
        /// WHY IT EXISTS: the id→model mapping was scattered across independent CODE
        /// tables (EnemyFactory's switch, AtbCombatantSwapper's slug map, the outpost
        /// spawner's fallback defs, a dead roamer table in RegionMobSpawner), so
        /// enemies.json could say "Troll" while a code table said "Orc_Berserker" and
        /// nothing failed — the divergence class WO-772 removed for STATS and WO-954
        /// removes for MODELS. Data is now the first authority; the code tables stay only
        /// as the last-resort fallback.
        ///
        /// Returns false when there is no usable data key, and sets
        /// <paramref name="rejectReason"/> to a §1.4b-grade explanation naming the id, the
        /// key that was tried, and WHY it was not honoured — so the caller's trace line can
        /// never degrade to a hollow "model load failed".
        /// </summary>
        public static bool TryResolveDataModel(string id, string dataModelKey,
                                               out string model, out string rejectReason)
        {
            model = null;
            rejectReason = null;

            if (string.IsNullOrEmpty(dataModelKey))
            {
                // Not a failure — most synthesised defs (region roamers, outpost bosses)
                // legitimately carry no data key. Say so precisely rather than crying wolf.
                rejectReason = $"enemy id '{id}' carries NO enemies.json modelKey " +
                               "(row absent, or a code-synthesised def) — falling back to the code table.";
                return false;
            }

            if (!CommittedModels.Contains(dataModelKey))
            {
                rejectReason = $"enemy id '{id}' asks for model '{dataModelKey}', but that key is NOT in the " +
                               "committed Resources/Enemies registry (EnemyResolver.CommittedModels) — the art " +
                               "was never imported under that name, or the row has a typo. Honouring it would " +
                               "spawn a tinted capsule, so the code table's stand-in is used instead. FIX: import " +
                               $"'{dataModelKey}' into Assets/Resources/Enemies and add it to CommittedModels, or " +
                               "correct the modelKey in enemies.json.";
                return false;
            }

            model = dataModelKey;
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
