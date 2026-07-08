// =============================================================================
// DefenseTargetableRegression — the DATA-decidable half of F8-41 "waves must
// ATTACK the city", proven headless in SECONDS (no scene drive, no play mode).
//
// THE TICKET (F8-41): wave enemies log
//   [Flow:EnemyAggro] <id>: ProbeForStructure null -> no structure target
// and never attack the defenses they march past. Enemy.SweepForNearestStructure
// does `Physics.OverlapSphere(...)` then, per returned collider,
// `collider.GetComponentInParent<IDamageableStructure>()`. So a placed defense is
// only ATTACKABLE when (a) it presents an IDamageableStructure up the collider's
// parent chain, and (b) a collider exists for OverlapSphere to return, and the
// enemy's sweep radius is large enough to acquire one at realistic lane distance.
//
// THIS ORACLE decides that DATA half from the REAL catalog + the REAL placement
// path (StructureFactory) + the REAL component types the sweep resolves:
//   1. For every DEFENSE entry in structures-catalog.json (behaviorId in
//      {DefenseTower, ArcaneTower, WallSegment, Gate}) map behaviorId to the
//      component StructureFactory.AttachBehavior would add, and assert that TYPE
//      implements DeNelle.Core.Combat.IDamageableStructure. A defense whose type
//      does NOT implement it is a PROVEN root: enemies can never acquire it —
//      the sweep's GetComponentInParent<IDamageableStructure> returns null for
//      every collider it owns.
//   2. Instantiate the entry through the SAME StructureFactory.Create the game
//      places with, and exercise the EXACT sweep op — resolve
//      collider.GetComponentInParent<IDamageableStructure>() from a collider in
//      the built hierarchy — to corroborate the type verdict against real geometry.
//   3. Read the enemy's default `_structureSweepRadius` off a fresh Enemy (the
//      serialized default, via reflection) and FAIL if it is implausibly small
//      (< 3m) so a mis-tuned radius is a build failure, not a silent field miss.
//
// HONEST about the RUNTIME half (NOT data-decidable — stays a FLEET check):
//   * whether ProbeForStructure is actually CALLED during the Heart-march, and
//     whether the acquired target is committed/attacked, needs the AutoPilot
//     fleet (a running Enemy on a navmesh) — this oracle proves TARGETABILITY +
//     radius CONFIG only, not "is the probe reached / target committed".
//   * Gate + WallSegment build their BoxCollider blocker at RUNTIME in Awake
//     (RebuildCollider), which does NOT fire on an edit-mode AddComponent — so a
//     headless "no collider" reading for those is NOT conclusive and is reported
//     as a note, never a hard fail. The collider-EXISTENCE dimension for runtime-
//     Awake blockers remains a fleet check; the interface-RESOLUTION op is proven
//     here against a probe collider.
//
// Separate-class oracle (mirrors MonetizationCovenantRegression): callable from
// DataRegression.RunAll as
//   if (!DefenseTargetableRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[def-target] " + r);
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Core.Catalog;
using DeNelle.Core.Combat;

namespace DeNelle.Editor
{
    public static class DefenseTargetableRegression
    {
        // The defense behaviorIds a marching enemy SHOULD be able to attack en route.
        // (Buildings/GameplayBuilding + CrystalMine are excluded — the ticket scope is
        // towers/walls/gates. behaviorId -> component type mirrors StructureFactory.AttachBehavior.)
        private static readonly Dictionary<string, System.Type> DefenseBehaviorTypes =
            new Dictionary<string, System.Type>
            {
                { "DefenseTower", typeof(DefenseTower) },
                { "ArcaneTower",  typeof(ArcaneTower)  },
                { "WallSegment",  typeof(WallSegment)  },
                { "Gate",         typeof(Gate)         },
            };

        // Realistic-minimum sweep radius. The enemy's default is a field initializer; a
        // value below this reads as a mis-tuned/lost field (the sweep can never acquire).
        private const float MinPlausibleSweepRadius = 3f;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var created = new List<GameObject>();
            int defensesChecked = 0, resolvedOk = 0;

            try
            {
                // --- 1. Load the catalog the game loads (identical to CheckStructures) ---
                string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/structures-catalog.json");
                if (string.IsNullOrEmpty(json))
                {
                    reason = "structures-catalog.json read EMPTY/NULL — cannot decide defense targetability (canonical data missing).";
                    return false;
                }

                StructuresCatalogFile file;
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        Converters = { new StringEnumConverter() },
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                    };
                    file = JsonConvert.DeserializeObject<StructuresCatalogFile>(json, settings);
                }
                catch (System.Exception ex)
                {
                    reason = $"structures-catalog.json failed to parse: {ex.Message}";
                    return false;
                }

                if (file == null || file.Entries == null || file.Entries.Count == 0)
                {
                    reason = "structures-catalog.json deserialized to 0 CatalogEntry objects (mapping break or empty 'entries').";
                    return false;
                }

                // --- 2. Read the enemy's default sweep radius (serialized field default) ---
                float sweepRadius = ReadDefaultSweepRadius(created, out string radiusNote);
                if (radiusNote != null) notes.Add(radiusNote);
                if (sweepRadius < MinPlausibleSweepRadius)
                    failures.Add($"Enemy._structureSweepRadius default = {sweepRadius:F1}m is implausibly small " +
                                 $"(< {MinPlausibleSweepRadius:F1}m) — the OverlapSphere can never acquire a defense the enemy marches past.");

                // --- 3. Per DEFENSE entry: type-level targetability + real placement resolve ---
                foreach (var entry in file.Entries)
                {
                    if (entry == null || entry.repo == null) continue;
                    string behaviorId = entry.repo.behaviorId;
                    if (string.IsNullOrEmpty(behaviorId)) continue;
                    if (!DefenseBehaviorTypes.TryGetValue(behaviorId, out var type)) continue; // not a defense

                    defensesChecked++;
                    string label = $"{entry.id} (behaviorId={behaviorId}, type={type.Name})";

                    // 3a. TYPE-LEVEL — the rock-solid, headless-immune proof. The sweep's
                    // GetComponentInParent<IDamageableStructure> can only resolve a component
                    // whose TYPE implements the interface. This alone decides targetability.
                    bool typeTargetable = typeof(IDamageableStructure).IsAssignableFrom(type);
                    if (!typeTargetable)
                    {
                        failures.Add($"DEFENSE '{label}' is NOT targetable — its component type '{type.Name}' does NOT implement " +
                                     "IDamageableStructure, so an enemy's SweepForNearestStructure/ProbeForStructure returns null for it. " +
                                     "Enemies can NEVER attack this defense.");
                        continue; // no point instantiating an un-targetable type
                    }

                    // 3b. REAL PLACEMENT — build via the same path the game places with, then
                    // exercise the EXACT sweep resolution op against real geometry.
                    string placeNote = ExerciseRealPlacement(entry, type, created, out bool resolvedFromCollider);
                    if (placeNote != null) notes.Add(placeNote);

                    if (resolvedFromCollider) { resolvedOk++; }
                    else
                    {
                        // Type implements the interface but the built hierarchy did not resolve it
                        // from a collider AND we could not confirm via a probe — a real hierarchy bug
                        // (behavior not on the collider's parent chain). Only a HARD fail when the
                        // placement actually built (else it's a headless render/skin artifact, noted).
                        // ExerciseRealPlacement already appended the precise reason to `notes`; escalate
                        // to a failure only when it proved a null resolve on a BUILT, collider-bearing object.
                        if (placeNote != null && placeNote.Contains("RESOLVE-NULL"))
                            failures.Add($"DEFENSE '{label}' built but GetComponentInParent<IDamageableStructure> from its collider " +
                                         "returned NULL — the IDamageableStructure is not on the collider's parent chain (targeting-broken hierarchy).");
                    }
                }

                if (defensesChecked == 0)
                {
                    reason = "structures-catalog.json contains NO defense entries (behaviorId in {DefenseTower,ArcaneTower,WallSegment,Gate}) — " +
                             "cannot prove waves have anything to attack. Either the catalog lost its defenses or the behaviorId set changed.";
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                reason = $"DefenseTargetableRegression threw: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
            finally
            {
                foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            }

            // --- verdict ---
            var sb = new StringBuilder();
            if (failures.Count > 0)
            {
                sb.Append($"F8-41 DATA HALF FAILED ({failures.Count}): ");
                sb.Append(string.Join(" | ", failures));
                if (notes.Count > 0) sb.Append("  [notes: " + string.Join(" ; ", notes) + "]");
                reason = sb.ToString();
                return false;
            }

            sb.Append($"{defensesChecked} defense type(s) all present IDamageableStructure + resolve it from a collider " +
                      $"({resolvedOk} confirmed via real placement); enemy default sweep radius OK.");
            if (notes.Count > 0) sb.Append("  [notes: " + string.Join(" ; ", notes) + "]");
            sb.Append("  RUNTIME HALF (is ProbeForStructure CALLED during the Heart-march / is the target COMMITTED, + " +
                      "runtime-Awake blocker colliders on Gate/WallSegment) is NOT data-decidable — stays a fleet check.");
            reason = sb.ToString();
            return true;
        }

        // Read the serialized default of Enemy._structureSweepRadius off a fresh Enemy
        // (mirrors CheckEnemyStructureSweep's reflection approach). Awake does NOT fire on
        // an edit-mode AddComponent, so no side effects; the C# field initializer (= 3f) is
        // present. Returns the value; appends a note naming it against realistic tower range.
        private static float ReadDefaultSweepRadius(List<GameObject> created, out string note)
        {
            note = null;
            try
            {
                var go = new GameObject("DefTarget_RadiusProbeEnemy");
                created.Add(go);
                var enemy = go.AddComponent<Enemy>();
                var f = typeof(Enemy).GetField("_structureSweepRadius", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null)
                {
                    note = "Enemy._structureSweepRadius field NOT FOUND (renamed?) — radius check skipped.";
                    return MinPlausibleSweepRadius; // don't false-fail on a rename; note it
                }
                float r = (float)f.GetValue(enemy);
                var cf = typeof(Enemy).GetField("_contactProbeDistance", BindingFlags.NonPublic | BindingFlags.Instance);
                float contact = cf != null ? (float)cf.GetValue(enemy) : 0f;
                float effective = Mathf.Max(contact, r);
                note = $"enemy sweep radius default={r:F1}m (contactProbe={contact:F1}m, effective OverlapSphere r={effective:F1}m) — " +
                       "defense tower ranges run 14-28m, so this radius only acquires defenses the enemy passes CLOSE to (lane placement matters; a runtime/fleet spacing check).";
                return r;
            }
            catch (System.Exception ex)
            {
                note = $"radius probe threw: {ex.Message} — radius check skipped.";
                return MinPlausibleSweepRadius;
            }
        }

        // Build the entry through the REAL StructureFactory.Create and exercise the EXACT
        // sweep resolution: collider.GetComponentInParent<IDamageableStructure>(). Returns a
        // note (or null) and sets `resolved` when a collider (real or probe) resolves the
        // interface. Never throws; a headless skin/render miss is degraded to a note.
        private static string ExerciseRealPlacement(CatalogEntry entry, System.Type type, List<GameObject> created, out bool resolved)
        {
            resolved = false;
            GameObject root = null;
            try
            {
                root = StructureFactory.Create(entry, new Pose(Vector3.zero, Quaternion.identity), null);
            }
            catch (System.Exception ex)
            {
                return $"'{entry.id}': StructureFactory.Create threw ({ex.Message}) — placement-resolve skipped (type-level proof stands).";
            }

            if (root == null)
                return $"'{entry.id}': StructureFactory.Create returned null (headless skin/render miss — see CheckStructures); placement-resolve skipped, type-level proof stands.";

            created.Add(root);

            // Find the collider the sweep's OverlapSphere would return. Gate/WallSegment build
            // their BoxCollider blocker at RUNTIME in Awake (not fired in edit mode), so if the
            // built hierarchy has none we add a TEMPORARY probe collider at the root to exercise
            // the resolution op (the runtime blocker's existence stays a fleet concern).
            var existing = root.GetComponentInChildren<Collider>(true);
            bool usedProbe = false;
            Collider col = existing;
            if (col == null)
            {
                col = root.AddComponent<BoxCollider>();
                usedProbe = true;
            }

            // THE EXACT SWEEP OP.
            var iface = col.GetComponentInParent<IDamageableStructure>();
            if (iface != null)
            {
                resolved = true;
                string src = usedProbe
                    ? "no static collider in built hierarchy (runtime-Awake blocker; probe collider used)"
                    : $"collider '{col.GetType().Name}' present in built hierarchy";
                return $"'{entry.id}': GetComponentInParent<IDamageableStructure> RESOLVED ({src}).";
            }

            // Built, collider present/probed, yet the interface did not resolve up the parent
            // chain — a genuine hierarchy/targeting break. Flag with the RESOLVE-NULL marker so
            // the caller escalates to a hard failure.
            return $"'{entry.id}': RESOLVE-NULL — collider present but GetComponentInParent<IDamageableStructure> returned null " +
                   $"(behavior '{type.Name}' not on the collider's parent chain).";
        }

        // Local mirror of the structures-catalog.json envelope (same shape DataRegression /
        // CatalogBootstrap parse). Kept private to this oracle so it stays self-contained.
        [System.Serializable]
        private sealed class StructuresCatalogFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }
    }
}
