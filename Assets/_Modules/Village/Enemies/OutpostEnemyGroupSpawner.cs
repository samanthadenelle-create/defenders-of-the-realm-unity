// =============================================================================
// OutpostEnemyGroupSpawner — spawns a small, seeded ENEMY group at a choke in
// the Phase-2 outpost/dungeon chain. A runtime hook (or a placed marker carrying
// this component) calls SpawnGroup(center, seed) to populate the room with a
// weighted mix drawn from ONE enemy family that aggros the hero.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// REUSES (does NOT reinvent, CLAUDE.md §9):
//   - EnemyFactory.Build (the ONE skinned-enemy creation path) + Enemy.Configure
//   - EnemyBrain (Role + SetHeroOnlyTarget) for hero-aggro behaviour
//   - EnemyCatalog + CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath) — the
//     SAME synchronous enemies.json read WildlandsRoster uses. NO new parser.
//   - The roster ids auto-resolve to models in EnemyFactory.ModelForEnemy.
//
// WO-1001 Phase 1 SLICE 2 — PER-ENCOUNTER ENEMY FAMILY.
//   BEFORE: WeightedSkeletonId() was four hardcoded hollow-* literals and DefFor()
//   hand-wrote four EnemyDefs in C# that IGNORED enemies.json outright (and had
//   DRIFTED from it: code hollow-walker Hp 40 vs json 52, hollow-rogue Hp 34 vs
//   json 70, hollow-acolyte Hp 60 vs json 90). Every composed dungeon room spawned
//   hollows no matter what EncounterSpec.kind said — authoring "orc-group"
//   SILENTLY SPAWNED HOLLOWS.
//   NOW: the baked `encounterKind` field selects the family table, and the stat
//   block comes from enemies.json (the catalog of record) with a code fallback
//   whose numbers MATCH the json, so the divergence cannot come back.
//
//   The ID TABLES + WEIGHTS + ROLES below are DESIGN, deliberately kept in C#
//   (weights are tuning, not content). The IDS are real enemies.json roster ids —
//   DungeonEncounterFamilyRegression fails the gate if any of them stops existing.
//   Roles stay an explicit design table rather than EnemyDef.RoleKind on purpose:
//   RoleKind would map hollow-rogue (json role "skirmisher") to EnemyRole.Ranged,
//   which is a stand-off/bow posture in EnemyBrain — a felt behaviour change to
//   the shipped hollow rooms that this slice does NOT authorise.
//
// Each spawned enemy is configured with heart=null (hero-aggro, not a siege
// wave) and SetHeroOnlyTarget(true). Seeded System.Random => repeatable layouts.
// ASCII strings only. Canon: the village is Elarion.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using Newtonsoft.Json;            // enemies.json deserialise (same as WildlandsRoster)
using DeNelle.Core;               // CanonicalJson (WebGL-safe synchronous catalog read)
using DeNelle.Core.Diagnostics;   // FlowTrace (TGVRU, CLAUDE.md §12)
using System.Collections.Generic;

namespace DeNelle.Village
{
    /// <summary>Spawns a seeded weighted skeleton group around a choke point (hero-aggro).</summary>
    public sealed class OutpostEnemyGroupSpawner : MonoBehaviour
    {
        private const string Sys = "OutpostEnemies";

        [Tooltip("Ring radius (world units) the group is spread across around the center.")]
        [SerializeField] private float formationRadius = 3.5f;

        [Tooltip("When true, a placed marker spawns its group automatically on Start (seeded from scene + position).")]
        [SerializeField] private bool autoSpawnOnStart = true;
        [SerializeField] private int minCount = 3;
        [SerializeField] private int maxCount = 7;

        [Tooltip("WO-770.11 dungeon leash: each skeleton stays dormant at its spawn slot " +
                 "until the hero comes within this radius (world units). Prevents the whole " +
                 "room beelining the entry. ~10m = room-sized. <= 0 disables the leash. " +
                 "WO-797: superseded by the room wake gate when a room area is configured.")]
        [SerializeField] private float leashRadius = 10f;

        // ── WO-797 room ownership (F8 seq 461/622 "all enemies at the entrance") ──
        // When areaSize is non-zero this spawner OWNS a room: spawn slots are seated
        // strictly inside the room AABB, and every spawned brain is bound to it
        // (EnemyBrain.SetRoomArea) so mobs wake off the ROOM FOOTPRINT and are
        // confined to the room + slack even while provoked. Fields are serialized so
        // DungeonBaker can write them into the SCENE via SerializedObject at bake;
        // DungeonRoomBinder configures them at runtime for already-baked scenes.
        [Header("Room ownership (WO-797)")]
        [Tooltip("Owning room's instance id (e.g. 'junction'). Diagnostic + contract only.")]
        [SerializeField] private string roomId = "";
        [Tooltip("World-space center of the owning room's AABB.")]
        [SerializeField] private Vector3 areaCenter;
        [Tooltip("World-space size of the owning room's AABB. Zero = room ownership OFF.")]
        [SerializeField] private Vector3 areaSize;
        [Tooltip("Metres a mob may step outside the room AABB (through a doorway) while fighting.")]
        [SerializeField] private float areaSlack = 2f;
        [Tooltip("Wake distance measured from the ROOM FOOTPRINT (not a ring slot) to the hero.")]
        [SerializeField] private float wakeRadius = 6f;

        // ── WO-1001 slice 2: which FAMILY this room fields ────────────────────────
        // Written into the scene by DungeonBaker.WriteEncounterFields via
        // SerializedObject (same path as the room-ownership fields above) straight
        // from the layout's EncounterSpec.kind, and by DungeonRoomBinder at runtime
        // for scenes baked before this field existed. The C# default keeps every
        // pre-WO-1001 baked scene on the hollow group it already had.
        [Header("Encounter family (WO-1001)")]
        [Tooltip("EncounterSpec.kind for this room: none / hollow-group / orc-group / troll-group / mixed. " +
                 "Empty or unknown falls back to hollow-group AND logs a FlowTrace warning - it is never silent.")]
        [SerializeField] private string encounterKind = DefaultKind;

        [Header("Boss / elite (WO-1001 slice 3)")]
        [Tooltip("When true, force a single elite spawn (count=1) using fixedEnemyId or the family's heaviest weight.")]
        [SerializeField] private bool isBoss;
        [Tooltip("Optional fixed enemies.json id for boss/elite. Empty = pick heaviest weight in the family table.")]
        [SerializeField] private string fixedEnemyId = "";

        private Transform _root;
        private int _counter;
        private bool _autoSpawned;
        // WO-797: brains this spawner created — lets a late ConfigureRoomArea retro-bind
        // enemies that already spawned (binder-after-Start ordering safety net).
        private readonly System.Collections.Generic.List<EnemyBrain> _spawnedBrains =
            new System.Collections.Generic.List<EnemyBrain>();

        /// <summary>True when this spawner owns a room AABB (WO-797).</summary>
        public bool HasRoomArea => areaSize.sqrMagnitude > 0.01f;

        /// <summary>The owning room's instance id ("" when unbound).</summary>
        public string RoomId => roomId;

        /// <summary>
        /// WO-797: bind this spawner to its room. Called by DungeonRoomBinder at scene load
        /// (before Start's auto-spawn) for already-baked composed scenes; the re-bake path
        /// writes the same serialized fields via SerializedObject instead. If enemies were
        /// already spawned (late call), they are retro-bound so no mob is ever ownerless.
        /// min/max &lt; 0 = keep the serialized counts (no creative re-authoring).
        /// WO-1001: <paramref name="kind"/> null/empty = keep the serialized
        /// <c>encounterKind</c> (so an already-baked scene is never re-familied by
        /// accident); a non-empty value applies the layout's authored kind to a
        /// scene baked before the field existed.
        /// </summary>
        public void ConfigureRoomArea(string room, Bounds area, float wake, float slack,
                                      int min = -1, int max = -1, float formation = -1f,
                                      string kind = null)
        {
            roomId = room ?? string.Empty;
            if (!string.IsNullOrEmpty(kind)) encounterKind = kind;
            areaCenter = area.center;
            areaSize = area.size;
            wakeRadius = Mathf.Max(0f, wake);
            areaSlack = Mathf.Max(0f, slack);
            if (min > 0) minCount = min;
            if (max > 0) maxCount = Mathf.Max(min > 0 ? min : minCount, max);
            if (formation > 0f) formationRadius = formation;
            FlowTrace.Step(Sys, $"room area configured: room '{roomId}' center {areaCenter} " +
                $"size {areaSize} wake {wakeRadius:F1} slack {areaSlack:F1} kind '{encounterKind}' " +
                $"boss={isBoss} fixed='{(string.IsNullOrEmpty(fixedEnemyId) ? "-" : fixedEnemyId)}' " +
                $"(spawned so far {_spawnedBrains.Count})");

            // Retro-bind anything already spawned (defensive: the binder normally runs
            // before Start, so this list is empty on the happy path).
            for (int i = 0; i < _spawnedBrains.Count; i++)
            {
                var brain = _spawnedBrains[i];
                if (brain == null) continue;
                brain.SetRoomArea(roomId, area, areaSlack, wakeRadius);
                FlowTrace.Step(Sys, $"retro-assigned '{brain.gameObject.name}' -> room '{roomId}'");
            }
        }

        // Tiny runtime bootstrapper: a marker baked into the chain spawns its group once
        // on Start, seeded deterministically from the scene name + its world position so the
        // layout is repeatable. Disable autoSpawnOnStart to drive SpawnGroup from an external hook.
        private void Start()
        {
            if (!autoSpawnOnStart || _autoSpawned) return;
            _autoSpawned = true;
            SpawnGroup(transform.position, ComputeSeed(), minCount, maxCount);
        }

        private int ComputeSeed()
        {
            var scene = gameObject.scene;
            string key = (scene.IsValid() ? scene.name : "scene") + ":" +
                         Mathf.RoundToInt(transform.position.x) + "," + Mathf.RoundToInt(transform.position.z);
            return key.GetHashCode();
        }

        /// <summary>
        /// Spawn a formation ring of [<paramref name="min"/>..<paramref name="max"/>]
        /// weighted enemies around <paramref name="center"/>, seeded by
        /// <paramref name="seed"/> (repeatable). The FAMILY comes from the baked
        /// <c>encounterKind</c> (WO-1001). Each enemy aggros the hero.
        /// </summary>
        public void SpawnGroup(Vector3 center, int seed, int min = 3, int max = 7)
        {
            // WO-1001: resolve the family FIRST - an unauthored / misspelled kind must
            // never silently spawn the wrong faction (that was the whole defect).
            string kind = ResolveKind(encounterKind, out bool kindFellBack);
            if (kindFellBack)
                FlowTrace.Warn(Sys, $"unknown encounter kind '{(string.IsNullOrEmpty(encounterKind) ? "<empty>" : encounterKind)}' " +
                    $"on room '{roomId}' - falling back to '{DefaultKind}'. Author EncounterSpec.kind as one of: " +
                    string.Join(", ", KnownKinds));
            if (kind == KindNone)
            {
                FlowTrace.Step(Sys, $"encounter kind 'none' on room '{roomId}' - no enemies spawned (authored empty room)");
                return;
            }

            if (min < 1) min = 1;
            if (max < min) max = min;

            var rng = new System.Random(seed);
            // WO-1001 slice 3: boss rooms are always a single elite — not a ring of trash.
            int count = isBoss ? 1 : rng.Next(min, max + 1);

            if (_root == null)
                _root = new GameObject("[OutpostEnemyGroup]").transform;

            // WO-797: when this spawner owns a room, seat every slot STRICTLY INSIDE the
            // room AABB (negative slack shrinks by 0.5m) — the old unclamped ring let
            // junction slots land in the neighbouring corridor, inside one leash radius
            // of the entry hero seat (data-proven cause 1 of the entrance camp).
            bool hasArea = HasRoomArea;
            Bounds area = new Bounds(areaCenter, areaSize);
            Vector3[] formation = BuildFormationOffsets(count, formationRadius, seed);
            var occupied = new List<Vector3>(count);
            float minimumSeparation = Mathf.Clamp(formationRadius * 0.55f, 1.75f, 3f);

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 slot = SeatFormationSlot(center, formation, i, hasArea, area,
                    occupied, minimumSeparation);
                occupied.Add(slot);

                string id = isBoss
                    ? ResolveBossId(kind, fixedEnemyId, rng)
                    : WeightedIdFor(kind, rng);
                EnemyDef def = DefFor(id, _counter++);
                EnemyRole role = isBoss ? EnemyRole.MiniBoss : RoleForId(id);

                Vector3 toCenter = center - slot; toCenter.y = 0f;
                Quaternion rot = toCenter.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(toCenter)
                    : Quaternion.identity;

                var enemy = EnemyFactory.Build(def, slot, rot, _root);
                if (enemy == null) continue;
                enemy.gameObject.name = $"OutpostEnemy ({def.Id})";
                enemy.Configure($"outpost-{def.Id}-{_counter}", def, null);   // heart=null -> hero-aggro

                var brain = enemy.gameObject.AddComponent<EnemyBrain>();
                brain.Role = role;
                brain.SetHeroOnlyTarget(true);
                // WO-770.11 hotfix: tether each skeleton to its own spawn slot so a
                // distant room's group stays dormant until the hero approaches, instead
                // of beelining the global hero across the whole dungeon.
                brain.SetLeash(slot, leashRadius);
                // WO-797: bind the mob to its OWNING ROOM — wake measured from the room
                // footprint, every nav destination (incl. provoked chases) confined to
                // the room AABB + slack. Room-assignment is a captured data line per
                // enemy (CLAUDE.md sec.12).
                if (hasArea)
                {
                    brain.SetRoomArea(roomId, area, areaSlack, wakeRadius);
                    FlowTrace.Step(Sys, $"assigned 'outpost-{def.Id}-{_counter}' -> room '{roomId}' " +
                        $"anchor {slot} (wake {wakeRadius:F1}m from footprint, slack {areaSlack:F1}m)");
                }
                else
                {
                    FlowTrace.Warn(Sys, $"'outpost-{def.Id}-{_counter}' spawned with NO room ownership " +
                        $"(anchor-leash only, {leashRadius:F1}m) - WO-797 binder/bake did not configure this spawner");
                }
                _spawnedBrains.Add(brain);

                spawned++;
            }

            FlowTrace.Step(Sys, $"spawned {spawned} enemies of family kind '{kind}' @ {center} seed {seed} " +
                $"(rolled count {count}, formation=staggered, minSeparation={minimumSeparation:F1}m) " +
                (hasArea ? $"room '{roomId}'" : "NO room area"));
        }

        /// <summary>
        /// Deterministic two-rank formation used by dungeon encounters. The older random ring
        /// regularly produced a single mob knot after NavMesh sampling. Alternating depth gives
        /// melee and ranged roles different sight-lines while the golden-angle yaw prevents a
        /// rigid parade-ground look.
        /// </summary>
        public static Vector3[] BuildFormationOffsets(int count, float radius, int seed)
        {
            count = Mathf.Max(0, count);
            radius = Mathf.Max(1.5f, radius);
            var result = new Vector3[count];
            if (count == 0) return result;

            var rng = new System.Random(seed ^ 0x51ED270B);
            float yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
            const float GoldenAngle = 2.39996323f;
            for (int i = 0; i < count; i++)
            {
                // Alternate an outer pressure rank with an inner support rank. Never seat at
                // the exact centre: that is where room props, loot, and trigger anchors live.
                float rank = (i & 1) == 0 ? 0.92f : 0.58f;
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.12f;
                float distance = radius * Mathf.Clamp(rank + jitter, 0.5f, 1.05f);
                float angle = yaw + i * GoldenAngle;
                result[i] = new Vector3(Mathf.Cos(angle) * distance, 0f,
                                        Mathf.Sin(angle) * distance);
            }
            return result;
        }

        private static Vector3 SeatFormationSlot(Vector3 center, Vector3[] formation, int index,
                                                 bool hasArea, Bounds area,
                                                 List<Vector3> occupied, float minimumSeparation)
        {
            // Try the authored seat first, then rotate it in 45-degree steps. This matters on
            // narrow rooms: NavMesh.SamplePosition can otherwise snap several candidates onto
            // the same walkable point and recreate the bunching after good offsets were chosen.
            Vector3 offset = formation[index];
            Vector3 best = center + offset;
            float bestClearance = -1f;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 candidate = center + Quaternion.Euler(0f, attempt * 45f, 0f) * offset;
                if (hasArea) candidate = EnemyBrain.ConfineToArea(candidate, area, -0.75f);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                    candidate = hit.position;

                float clearance = float.MaxValue;
                for (int j = 0; j < occupied.Count; j++)
                {
                    Vector3 delta = candidate - occupied[j];
                    delta.y = 0f;
                    clearance = Mathf.Min(clearance, delta.magnitude);
                }

                if (clearance > bestClearance)
                {
                    best = candidate;
                    bestClearance = clearance;
                }
                if (clearance >= minimumSeparation) return candidate;
            }

            FlowTrace.Warn(Sys, $"formation seat {index} could only secure {bestClearance:F1}m clearance " +
                $"(wanted {minimumSeparation:F1}m); using best candidate {best}");
            return best;
        }

        // =====================================================================
        //  WO-1001 slice 2 — FAMILY TABLES (design data, deliberately in C#)
        // ---------------------------------------------------------------------
        //  The WEIGHTS and ROLES here are tuning. The IDS are real
        //  Data/Canonical/enemies.json roster ids and nothing else -
        //  DungeonEncounterFamilyRegression fails the gate the moment one of them
        //  stops existing in the catalog, so this table can never drift into
        //  inventing enemies again.
        // =====================================================================

        /// <summary>EncounterSpec.kind that disables the room's encounter entirely.</summary>
        public const string KindNone = "none";
        /// <summary>The kind every unauthored / unknown value falls back to.</summary>
        public const string DefaultKind = "hollow-group";

        /// <summary>Every EncounterSpec.kind this spawner understands (ASCII, lower-case).</summary>
        public static readonly string[] KnownKinds =
            { KindNone, "hollow-group", "orc-group", "troll-group", "mixed" };

        // hollow-group: UNCHANGED from the pre-WO-1001 hardcoded picker - the same four
        // ids in the same order with the same weights (walker 5 / rogue 2 / warrior 2 /
        // acolyte 1, total 10), so the cumulative roll over rng.Next(0, 10) reproduces
        // the old stream EXACTLY. Every shipped dungeon room is behaviour-compatible.
        private static readonly string[] HollowIds =
            { "hollow-walker", "hollow-rogue", "hollow-warrior", "hollow-acolyte" };
        private static readonly int[] HollowWeights = { 5, 2, 2, 1 };

        // orc-group: the Warband. orc-necromancer is the family's elite - rare on purpose.
        private static readonly string[] OrcIds =
            { "orc-raider", "orc-berserker", "orc-shaman", "orc-necromancer" };
        private static readonly int[] OrcWeights = { 4, 3, 2, 1 };

        // troll-group: family "troll" fields exactly two members in enemies.json.
        // 2026-08-09: the troll family gained real art (Troll_Mage / Troll_Overlord), so the
        // group is no longer two brutes — it is a brute core with a caster pair behind it, which
        // is what makes a troll room play differently from an orc room. The Overlord is a camp
        // BOSS and is deliberately NOT here; group tables carry no boss ids.
        private static readonly string[] TrollIds = { "troll", "ogre", "troll-mage", "troll-shaman" };
        private static readonly int[] TrollWeights = { 4, 2, 2, 1 };

        // mixed: a cross-faction room - mostly hollow rank-and-file with orc muscle and
        // a rare troll. Deliberately excludes every elite/boss id.
        private static readonly string[] MixedIds =
            { "hollow-walker", "hollow-rogue", "orc-raider", "orc-berserker", "troll" };
        private static readonly int[] MixedWeights = { 4, 2, 3, 2, 1 };

        private static readonly string[] NoIds = new string[0];
        private static readonly int[] NoWeights = new int[0];

        /// <summary>
        /// Normalises an authored EncounterSpec.kind to one of <see cref="KnownKinds"/>.
        /// PURE - it never logs, so a caller (and the regression oracle) decides what a
        /// fallback means. <paramref name="fellBack"/> is TRUE only when the input was
        /// not a known kind, which is what makes an unknown kind impossible to accept
        /// silently.
        /// </summary>
        public static string ResolveKind(string kind, out bool fellBack)
        {
            string k = (kind ?? string.Empty).Trim().ToLowerInvariant();
            for (int i = 0; i < KnownKinds.Length; i++)
            {
                if (!string.Equals(k, KnownKinds[i], System.StringComparison.Ordinal)) continue;
                fellBack = false;
                return KnownKinds[i];
            }
            fellBack = true;
            return DefaultKind;
        }

        /// <summary>
        /// The roster ids + matching weights for an ALREADY-RESOLVED kind (pass the
        /// output of <see cref="ResolveKind"/>). Both arrays are the same length; an
        /// empty pair means "spawn nothing" (kind none).
        /// </summary>
        public static void FamilyTable(string resolvedKind, out string[] ids, out int[] weights)
        {
            switch (resolvedKind)
            {
                case KindNone:      ids = NoIds;    weights = NoWeights;    return;
                case "orc-group":   ids = OrcIds;   weights = OrcWeights;   return;
                case "troll-group": ids = TrollIds; weights = TrollWeights; return;
                case "mixed":       ids = MixedIds; weights = MixedWeights; return;
                default:            ids = HollowIds; weights = HollowWeights; return;
            }
        }

        /// <summary>
        /// Weighted roster-id pick for an already-resolved kind. Cumulative over
        /// <c>rng.Next(0, totalWeight)</c> - for hollow-group that is byte-identical to
        /// the retired hardcoded picker. Returns null only for kind none.
        /// </summary>
        public static string WeightedIdFor(string resolvedKind, System.Random rng)
        {
            FamilyTable(resolvedKind, out string[] ids, out int[] weights);
            if (ids.Length == 0) return null;

            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0, weights[i]);
            if (total <= 0 || rng == null) return ids[0];

            int roll = rng.Next(0, total);
            int acc = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                acc += Mathf.Max(0, weights[i]);
                if (roll < acc) return ids[i];
            }
            return ids[ids.Length - 1];
        }

        /// <summary>
        /// WO-1001 slice 3: pick the boss enemy id — fixedEnemyId wins when set, else the
        /// family's highest-weight (elite) entry. Never returns null for a non-none family.
        /// </summary>
        public static string ResolveBossId(string resolvedKind, string fixedId, System.Random rng)
        {
            if (!string.IsNullOrEmpty(fixedId))
                return fixedId.Trim();
            FamilyTable(resolvedKind, out string[] ids, out int[] weights);
            if (ids.Length == 0) return "hollow-warrior";
            int best = 0;
            for (int i = 1; i < weights.Length; i++)
            {
                if (weights[i] > weights[best]) best = i;
            }
            // Prefer the rarest/elite end when weights are equal (last id).
            int maxW = weights[best];
            for (int i = ids.Length - 1; i >= 0; i--)
            {
                if (weights[i] == maxW) return ids[i];
            }
            return ids[ids.Length - 1];
        }

        /// <summary>
        /// Tactical role for a roster id. DESIGN table, not EnemyDef.RoleKind: RoleKind
        /// maps json role "skirmisher" to <see cref="EnemyRole.Ranged"/>, which puts a
        /// melee hollow-rogue into EnemyBrain's stand-off/bow posture - a felt change to
        /// the shipped hollow rooms this slice does not authorise. The four hollow ids
        /// keep EXACTLY their pre-WO-1001 roles.
        /// </summary>
        public static EnemyRole RoleForId(string id)
        {
            switch ((id ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "hollow-acolyte":  return EnemyRole.Healer;    // unchanged
                case "orc-shaman":      return EnemyRole.Healer;    // the family's support caster
                case "orc-berserker":   return EnemyRole.Tank;      // json role "brute"
                case "orc-necromancer": return EnemyRole.MiniBoss;  // json role "elite"
                case "troll":           return EnemyRole.Tank;      // the heavy of its pair
                // 2026-08-09 troll family. The pair of casters shares one mesh and is separated
                // HERE: the Bonesinger is the support the player must drop first, the Stonecaller
                // is ranged damage. Without these cases both fall to the DPS default and the
                // healer never heals — the role, not the json "caster" string, is what the brain reads.
                case "troll-shaman":    return EnemyRole.Healer;    // heals faster + more often than the orc shaman
                case "troll-mage":      return EnemyRole.DPS;       // ranged damage (explicit, not defaulted)
                case "troll-overlord":  return EnemyRole.MiniBoss;  // json role "elite"
                default:                return EnemyRole.DPS;       // walker / rogue / warrior / raider / ogre
            }
        }

        // =====================================================================
        //  STAT BLOCKS — enemies.json is the catalog of record
        // =====================================================================

        private static EnemyCatalog _enemies;
        private static bool _enemiesLoadAttempted;

        /// <summary>
        /// The enemies.json def for an id, or null when the catalog is unreadable or has
        /// no such row. Reuses the SAME synchronous WebGL-safe read WildlandsRoster uses
        /// (CanonicalJson -> Resources dual-copy first). One attempt per session.
        /// </summary>
        private static EnemyDef CatalogDef(string id)
        {
            if (!_enemiesLoadAttempted)
            {
                _enemiesLoadAttempted = true;
                try
                {
                    string json = CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
                    if (!string.IsNullOrEmpty(json))
                        _enemies = JsonConvert.DeserializeObject<EnemyCatalog>(json);
                    int n = _enemies != null && _enemies.Enemies != null ? _enemies.Enemies.Count : 0;
                    if (n > 0) FlowTrace.Step(Sys, $"enemies.json roster loaded - {n} defs (stat source of record)");
                    else FlowTrace.Warn(Sys, "enemies.json read produced 0 defs - dungeon groups fall back to code stat blocks");
                }
                catch (System.Exception ex)
                {
                    _enemies = null;
                    FlowTrace.Warn(Sys, $"enemies.json read/parse failed ({ex.GetType().Name}: {ex.Message}) - " +
                        "dungeon groups fall back to code stat blocks");
                }
            }
            return _enemies != null ? _enemies.Find(id) : null;
        }

        // Per-id stat block. enemies.json FIRST (the pre-WO-1001 hand-written numbers had
        // drifted from it); the code fallback below only covers an unreadable catalog.
        private static EnemyDef DefFor(string id, int n)
        {
            EnemyDef fromJson = CatalogDef(id);
            if (fromJson != null) return Clone(fromJson);

            FlowTrace.Warn(Sys, $"enemies.json has no def for '{id}' - using the code fallback stat block");
            return Fallback(id);
        }

        // Fresh copy so a spawned enemy can never mutate the cached catalog row.
        private static EnemyDef Clone(EnemyDef s)
        {
            return new EnemyDef
            {
                Id                = s.Id,
                Name              = s.Name,
                DisplayName       = s.DisplayName,
                Family            = s.Family,
                Role              = s.Role,
                ModelKey          = s.ModelKey,
                Ai                = s.Ai,
                Movement          = s.Movement,
                Hp                = s.Hp,
                MoveSpeed         = s.MoveSpeed,
                ContactDamage     = s.ContactDamage,
                AttackInterval    = s.AttackInterval,
                Height            = s.Height,
                Boss              = s.Boss,
                AggroRadius       = s.AggroRadius,
                GroupStaggerDelay = s.GroupStaggerDelay,
                XpReward          = s.XpReward,
                GlimmerReward     = s.GlimmerReward,
                CoinReward        = s.CoinReward,
            };
        }

        // Code fallback for an UNREADABLE catalog only. Numbers are copied from
        // Assets/(Resources|StreamingAssets)/Data/Canonical/enemies.json verbatim - the
        // WildlandsRoster discipline - so a missing catalog can never reintroduce the
        // stat divergence WO-1001 removed. Keep in sync with enemies.json.
        private static EnemyDef Fallback(string id)
        {
            switch ((id ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "hollow-rogue":
                    return new EnemyDef
                    {
                        Id = "hollow-rogue", Name = "Hollow Rogue", DisplayName = "Hollow Skirmisher",
                        Family = "hollow", Role = "skirmisher", ModelKey = "Skeleton_Rogue", Ai = "skirmisher",
                        Hp = 70f, MoveSpeed = 3.8f, ContactDamage = 5f, AttackInterval = 1.0f, Height = 1.78f,
                        XpReward = 14, CoinReward = 6,
                    };
                case "hollow-warrior":
                    return new EnemyDef
                    {
                        Id = "hollow-warrior", Name = "Hollow Warrior", DisplayName = "Hollow Warrior",
                        Family = "hollow", Role = "grunt", ModelKey = "Skeleton_Warrior", Ai = "walker",
                        Hp = 156f, MoveSpeed = 2.2f, ContactDamage = 10f, AttackInterval = 1.3f, Height = 1.88f,
                        XpReward = 24, CoinReward = 10,
                    };
                case "hollow-acolyte":
                    return new EnemyDef
                    {
                        Id = "hollow-acolyte", Name = "Hollow Acolyte", DisplayName = "Hollow Acolyte",
                        Family = "hollow", Role = "caster", ModelKey = "Skeleton_Healer", Ai = "walker",
                        Hp = 90f, MoveSpeed = 2.2f, ContactDamage = 4f, AttackInterval = 1.4f, Height = 1.8f,
                        XpReward = 18, CoinReward = 8,
                    };
                case "orc-raider":
                    return new EnemyDef
                    {
                        Id = "orc-raider", Name = "Orc Raider", DisplayName = "Orc Raider",
                        Family = "orc", Role = "skirmisher", ModelKey = "Orc_Berserker", Ai = "charger",
                        Hp = 130f, MoveSpeed = 3.1f, ContactDamage = 12f, AttackInterval = 1.3f, Height = 2.0f,
                        XpReward = 24, CoinReward = 11,
                    };
                case "orc-berserker":
                    return new EnemyDef
                    {
                        Id = "orc-berserker", Name = "Orc Berserker", DisplayName = "Orc Berserker",
                        Family = "orc", Role = "brute", ModelKey = "Orc_Berserker", Ai = "charger",
                        Hp = 117f, MoveSpeed = 2.8f, ContactDamage = 10f, AttackInterval = 1.2f, Height = 2.0f,
                        XpReward = 22, CoinReward = 10,
                    };
                case "orc-shaman":
                    return new EnemyDef
                    {
                        Id = "orc-shaman", Name = "Orc Shaman", DisplayName = "Orc Shaman",
                        Family = "orc", Role = "caster", ModelKey = "Orc_Shaman", Ai = "skirmisher",
                        Hp = 78f, MoveSpeed = 2.4f, ContactDamage = 3f, AttackInterval = 1.5f, Height = 1.9f,
                        XpReward = 16, CoinReward = 7,
                    };
                case "orc-necromancer":
                    return new EnemyDef
                    {
                        Id = "orc-necromancer", Name = "Orc Necromancer", DisplayName = "Warband Deathspeaker",
                        Family = "orc", Role = "elite", ModelKey = "Orc_Necromancer", Ai = "walker",
                        Hp = 600f, MoveSpeed = 1.8f, ContactDamage = 18f, AttackInterval = 1.3f, Height = 2.2f,
                        XpReward = 90, CoinReward = 50,
                    };
                case "troll":
                    return new EnemyDef
                    {
                        Id = "troll", Name = "Cave Troll", DisplayName = "Cave Troll",
                        Family = "troll", Role = "brute", ModelKey = "Troll", Ai = "charger",
                        Hp = 320f, MoveSpeed = 1.8f, ContactDamage = 14f, AttackInterval = 1.8f, Height = 2.6f,
                        XpReward = 46, CoinReward = 24,
                    };
                case "ogre":
                    return new EnemyDef
                    {
                        Id = "ogre", Name = "Ogre", DisplayName = "Ogre",
                        Family = "troll", Role = "brute", ModelKey = "OgreMage", Ai = "charger",
                        Hp = 280f, MoveSpeed = 2.0f, ContactDamage = 12f, AttackInterval = 1.6f, Height = 2.4f,
                        XpReward = 42, CoinReward = 22,
                    };
                default: // hollow-walker
                    return new EnemyDef
                    {
                        Id = "hollow-walker", Name = "Hollow Walker", DisplayName = "Hollow Walker",
                        Family = "hollow", Role = "grunt", ModelKey = "Skeleton_Minion", Ai = "walker",
                        Hp = 52f, MoveSpeed = 2.5f, ContactDamage = 8f, AttackInterval = 1.3f, Height = 1.7f,
                        XpReward = 10, CoinReward = 4,
                    };
            }
        }
    }
}
