// =============================================================================
// StructureFactory — the ONE creation path for catalog structures (WO-148).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE VISION (owner): catalog "buckets" of indexed CatalogEntry; the factory
// turns an entry (+ pose) into a live structure. There is exactly ONE creation
// method, called by THREE callers:
//   1. editor / bake-time builder  (the WO-136 castle rewrite authors via this)
//   2. runtime player placement    (TowerPlacementSystem, generalized)
//   3. persistence replay          (WO-149 load: recipe -> Create)
// Making structure-creation a repeatable process instead of bespoke geometry.
//
// RUNTIME-SAFE: this file has NO UnityEditor dependency, so the runtime + save
// paths use it directly. A thin DeNelle.Editor wrapper (StructureFactoryEditor)
// adds bake-time concerns (static flags, Undo) on top — it calls THIS, never
// duplicates the creation logic.
//
// CORE STAYS PURE: CatalogEntry/RepoProps (DeNelle.Core.Catalog) carry a STRING
// behaviorId, never a Village MonoBehaviour ref. The behaviorId -> component map
// is the switch in AttachBehavior() below — that switch IS the Core/Village
// boundary (no reflection, per CLAUDE.md §10).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Catalog;

namespace DeNelle.Village
{
    /// <summary>
    /// Instantiates <see cref="CatalogEntry"/> defs into live village structures.
    /// Runtime-safe (no editor APIs); shared by builder, player placement, and
    /// save-replay. Every step null-guards and logs rather than throwing, so a
    /// missing prefab / unknown behaviorId degrades gracefully (pack-missing-safe).
    /// </summary>
    public static class StructureFactory
    {
        /// <summary>
        /// Create one structure from <paramref name="entry"/> at <paramref name="pose"/>.
        /// Resolves the visual (via <see cref="VisualFactory"/>), attaches the
        /// behaviour component named by <c>entry.repo.behaviorId</c>, and parents
        /// it under <paramref name="parent"/>. Returns the root GameObject, or null
        /// when the entry is unusable (logged).
        /// </summary>
        public static GameObject Create(CatalogEntry entry, Pose pose, Transform parent)
        {
            if (entry == null)
            {
                Debug.LogWarning("[StructureFactory] Create called with a null entry — skipped.");
                return null;
            }

            // Composites delegate to CreateGroup so a single id can build a bundle.
            if (entry.kind == EntryKind.Composite)
                return CreateGroup(entry, pose, parent);

            // Root host — owns the world transform; the visual is skinned under it.
            var root = new GameObject(string.IsNullOrEmpty(entry.displayName)
                ? $"Structure-{entry.id}" : entry.displayName);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pose.position, pose.rotation);

            // LOOK — skin the polyperfect/Resources visual under the root.
            // DEF-208: a tall structure (tower) must fit to HEIGHT, not to its largest
            // bounds dim. Fit-to-largest scaled a tower so its tallest axis = footprint
            // (~2.5 m) → a squashed/wrong-scaled tower. When repo.visualHeight > 0 we
            // fit-to-height (correct, data-driven right-size); otherwise keep the legacy
            // footprint-largest fit for walls / props that read fine that way.
            if (!string.IsNullOrEmpty(entry.visualPrefabPath))
            {
                SkinOptions opts;
                float visualHeight = entry.repo != null ? entry.repo.visualHeight : 0f;
                if (visualHeight > 0f)
                {
                    opts = SkinOptions.Structure(0f);   // clear FitLargest
                    opts.FitHeight = visualHeight;       // fit to real-world height instead
                }
                else
                {
                    float fit = entry.repo != null && entry.repo.placement != null
                        ? Mathf.Max(1f, entry.repo.placement.footprint)
                        : 3f;
                    opts = SkinOptions.Structure(fit);
                }

                var visual = VisualFactory.Skin(root.transform, entry.visualPrefabPath, opts);
                if (visual == null)
                    Debug.LogWarning($"[StructureFactory] '{entry.id}': visual '{entry.visualPrefabPath}' " +
                                     "not found — structure created without a mesh.");
                else if (entry.orientation != null && entry.orientation.manual)
                {
                    // Apply ONLY human-verified (Inspector) orientation corrections — auto-baked
                    // ones are advisory (a bounds heuristic can't be trusted to not tip good assets).
                    visual.transform.localRotation = Quaternion.Euler(entry.orientation.Euler) * visual.transform.localRotation;
                    visual.transform.localPosition += entry.orientation.Offset;
                    if (entry.orientation.scale > 0f && !Mathf.Approximately(entry.orientation.scale, 1f))
                        visual.transform.localScale *= entry.orientation.scale;
                }
            }

            // BEHAVIOR — resolve the Core string id to a real Village component.
            AttachBehavior(root, entry);

            return root;
        }

        /// <summary>
        /// Create a composite (a pre-snapped bundle of cells — e.g. a castle as a
        /// group of wall/tower/gate entries). Each member is placed relative to
        /// <paramref name="pose"/> by its <see cref="CellPlacement"/> offset+rotation,
        /// reusing <see cref="Create"/> per member. Missing member ids are skipped
        /// (logged). Returns the group root.
        /// </summary>
        public static GameObject CreateGroup(CatalogEntry composite, Pose pose, Transform parent)
        {
            if (composite == null)
            {
                Debug.LogWarning("[StructureFactory] CreateGroup called with a null entry — skipped.");
                return null;
            }

            var groupRoot = new GameObject(string.IsNullOrEmpty(composite.displayName)
                ? $"Group-{composite.id}" : composite.displayName);
            groupRoot.transform.SetParent(parent, false);
            groupRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);

            if (composite.composite == null || composite.composite.Length == 0)
            {
                Debug.LogWarning($"[StructureFactory] composite '{composite.id}' has no cell placements.");
                return groupRoot;
            }

            int built = 0;
            foreach (var cell in composite.composite)
            {
                if (cell == null || string.IsNullOrEmpty(cell.cellEntryId)) continue;

                var member = CatalogRegistry.Get(cell.cellEntryId);
                if (member == null)
                {
                    Debug.LogWarning($"[StructureFactory] composite '{composite.id}': member " +
                                     $"'{cell.cellEntryId}' not in registry — skipped.");
                    continue;
                }

                // Member pose is the cell offset/rotation expressed in the group's
                // local space, composed onto the group's world pose.
                Vector3 worldPos = groupRoot.transform.TransformPoint(cell.offset);
                Quaternion worldRot = groupRoot.transform.rotation *
                                      Quaternion.Euler(0f, cell.yRotation, 0f);

                if (Create(member, new Pose(worldPos, worldRot), groupRoot.transform) != null)
                    built++;
            }

            Debug.Log($"[StructureFactory] composite '{composite.id}' built {built}/" +
                      $"{composite.composite.Length} member(s).");
            return groupRoot;
        }

        // ── behaviorId -> component bridge (the Core/Village boundary) ─────────
        // A plain switch, NOT reflection. Adding a new behaviour = a new case here.
        private static void AttachBehavior(GameObject root, CatalogEntry entry)
        {
            string behaviorId = entry.repo != null ? entry.repo.behaviorId : null;
            if (string.IsNullOrEmpty(behaviorId)) return;   // decoration / no behaviour

            switch (behaviorId)
            {
                case "DefenseTower":
                {
                    var t = root.AddComponent<DefenseTower>();
                    var r = entry.repo;
                    t.Range     = r.range;
                    t.Damage    = r.damage;
                    t.FireRate  = r.fireRate;
                    t.CanHitAir = r.canHitAir;
                    t.Element   = r.element;
                    break;
                }

                // WallSegment is authored with id/index/length by the builder, not
                // from RepoProps stats — attach it bare; the caller configures it.
                case "WallSegment":
                    root.AddComponent<WallSegment>();
                    break;

                // Gate — the cardinal force-field gate. Attach bare (same pattern as
                // WallSegment): Awake null-guards its collider/renderer + builds the
                // MPB, so a player-/save-placed gate is a live IDamageableStructure
                // (takes damage, collapses below 25%). The WallLayout builder calls
                // Configure() to size the opening; a free-placed gate keeps defaults.
                case "Gate":
                    root.AddComponent<Gate>();
                    break;

                // CrystalMine — passive Aether-Crystal generator (banks +1/wave at
                // L3 via WaveManager.OnWaveCleared). Self-resolves hero/wave/economy
                // in Start and builds its own placeholder visual when no prefab is
                // assigned, so a placed mine is a real, upgradeable gameplay object.
                case "CrystalMine":
                    root.AddComponent<CrystalMine>();
                    break;

                default:
                    Debug.LogWarning($"[StructureFactory] '{entry.id}': unknown behaviorId " +
                                     $"'{behaviorId}' — no behaviour attached.");
                    break;
            }
        }
    }
}
