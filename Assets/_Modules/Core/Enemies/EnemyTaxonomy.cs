// =============================================================================
// EnemyTaxonomy — the shared enemy family / class data model (WO-772 Phase 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Enemies
//
// Operationalises the RATIFIED enemy codex (docs/enemy-codex.md; ruling
// PAIN_POINTS_2026-07-26 §1.1) into a typed taxonomy that BOTH the village wave
// loop and the dungeon spawn path can resolve through ONE authority
// (EnemyResolver). It is the fix for the "distinct enemy ids collapse to the
// SAME generic skeleton" bug: every approved Hollow id now maps to its OWN
// family -> class -> distinct model (+ variant + the part-key SCHEMA for the
// Phase-2 armed/armored art).
//
// PHASED (ruling 2026-07-26): Phase 1 = the Hollow Ones ONLY (the approved
// roster + the canon-locked ids). The Wildlands faction is a RESERVED ENUM STUB
// here — no members, no art, no ship dependency — so Phase 2 can slot living
// bodies + the wolf rig in without a schema change.
//
// Pure data — no MonoBehaviour, no UnityEngine dependency beyond arrays — so it
// unit-tests cleanly and the resolver stays a pure, headless-provable function.
// =============================================================================

namespace DeNelle.Core.Enemies
{
    /// <summary>
    /// The faction (codex §1.1–1.3) an enemy belongs to. Drives spawn context +
    /// the model source set.
    /// </summary>
    public enum EnemyFaction
    {
        /// <summary>The undead skeleton wave faction — risen Folk of Elarion.
        /// The ONLY faction implemented in WO-772 Phase 1.</summary>
        HollowOnes = 0,

        /// <summary>RESERVED STUB (WO-772 Phase 2, ruling 2026-07-26 "DEFER"). The
        /// living second faction (orcs / beasts / cavemen / tieflings). No members,
        /// no art, and NO ship dependency in Phase 1 — this value exists only so the
        /// schema reserves the family and Phase 2 slots bodies in without a change.</summary>
        Wildlands = 1,

        /// <summary>Set-piece / wave bosses + the realm antagonist (codex §4).</summary>
        Boss = 2,
    }

    /// <summary>
    /// The modular equip part-key SCHEMA for an enemy class (codex "enemies get
    /// armor + weapons"). WO-772 Phase 1 DEFINES the schema only — no weapon MODEL
    /// is attached in this lane (the "one perfect armed type first" pass is A2 /
    /// Phase 2, PAIN_POINTS §1.1 "Perfect one first"). These keys are the contract a
    /// future <c>KayKitUnitBuilder</c> / weapon-attach will read.
    /// </summary>
    public sealed class EnemyEquipParts
    {
        /// <summary>KayKit modular armor part keys (chest / helmet / …). Empty = bare.</summary>
        public string[] ArmorPartKeys;

        /// <summary>Held-weapon prop key (e.g. a sword / staff / scythe). Null = unarmed
        /// silhouette. Phase 1 stores the INTENT only; nothing attaches a model yet.</summary>
        public string WeaponPartKey;

        /// <summary>The empty (bare, unarmed) equip set — the Phase-1 default.</summary>
        public static readonly EnemyEquipParts None = new EnemyEquipParts
        {
            ArmorPartKeys = System.Array.Empty<string>(),
            WeaponPartKey = null,
        };
    }

    /// <summary>
    /// One combat archetype within a family (codex roster "role" column — Walker /
    /// Warrior / Skirmisher / Acolyte / Caster / Reaper / Brute / …). Maps an
    /// enemy id to its DISTINCT model + (optional) variant + the equip schema.
    /// </summary>
    public sealed class EnemyClass
    {
        /// <summary>Stable class id (e.g. "walker", "warrior", "reaper").</summary>
        public string Id;

        /// <summary>enemy-roles.json-style role token ("grunt" / "skirmisher" /
        /// "caster" / "brute" / "elite") — used for tactical/behaviour selection.</summary>
        public string RoleKey;

        /// <summary>The canonical Resources/Enemies mesh key (e.g. "Skeleton_Warrior").
        /// Several classes may legitimately SHARE a base mesh (the codex distinguishes
        /// them by <see cref="Variant"/>): Reaper = Warrior mesh, Cellar = Minion mesh,
        /// Apprentice = Mage mesh.</summary>
        public string ModelKey;

        /// <summary>The differentiator when the base mesh is SHARED (tint / scale /
        /// animator-state). Null when the base mesh IS the whole identity. Combined
        /// with <see cref="ModelKey"/> it yields a per-id-distinct resolved key so
        /// two ids never read as the same generic body.</summary>
        public string Variant;

        /// <summary>The EXPECTED shared animator rig family (EnemyRig name — metadata
        /// only; EnemyAnimatorFactory.RigFor(model) remains the runtime authority).
        /// The AccuRig Hollow silhouettes all share "SkeletonHumanoid"; KayKit legacy
        /// bodies use "HumanoidMedium" / "HumanoidLarge" / "Boss".</summary>
        public string AnimatorRig;

        /// <summary>False for a non-combat entry (the Alduin dialogue NPC). Combat
        /// distinctness oracles exclude these.</summary>
        public bool CombatSpawnable = true;

        /// <summary>The modular armor/weapon part-key SCHEMA (Phase 2 art). Defaults
        /// to <see cref="EnemyEquipParts.None"/> — Phase 1 ships bare silhouettes.</summary>
        public EnemyEquipParts Equip = EnemyEquipParts.None;
    }

    /// <summary>
    /// A family (faction clade) and the class ids that belong to it (codex §1.1).
    /// Nesting is possible via <see cref="ParentId"/> but is unused in Phase 1.
    /// </summary>
    public sealed class EnemyFamily
    {
        public string Id;
        public string DisplayName;
        public string ParentId;
        public EnemyFaction Faction;
        public string[] MemberClassIds;
    }

    /// <summary>
    /// The fully-resolved model identity for one enemy id — what <see cref="EnemyResolver"/>
    /// hands back. <see cref="ResolvedKey"/> is the per-id-distinct signature the
    /// spawn-distinctness oracle asserts on.
    /// </summary>
    public sealed class ResolvedEnemyModel
    {
        public string EnemyId;
        public EnemyFaction Faction;
        public string ClassId;
        public string RoleKey;
        public string ModelKey;      // base Resources/Enemies mesh
        public string Variant;       // null when the base mesh is the whole identity
        public string AnimatorRig;   // expected EnemyRig (metadata)
        public bool IsCombatSpawnable;
        public EnemyEquipParts Equip;

        /// <summary>Per-id-distinct key: the base mesh, plus the variant when the base
        /// mesh is deliberately shared. Two DIFFERENT enemy ids must never produce the
        /// same ResolvedKey (that IS the generic-skeleton bug).</summary>
        public string ResolvedKey =>
            string.IsNullOrEmpty(Variant) ? ModelKey : ModelKey + "#" + Variant;
    }
}
