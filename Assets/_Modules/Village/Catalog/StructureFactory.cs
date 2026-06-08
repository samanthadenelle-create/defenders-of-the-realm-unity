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
using DeNelle.Core.Combat;   // DamageElement literals (WO-113 ArcaneTower default element)

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
                    // Per-axis (non-uniform) scale: uniform `scale` × `scaleAxis` per component.
                    // For legacy entries (scale only) EffectiveScale is (s,s,s) — identical to
                    // the old `localScale *= scale`. A stretched wall (e.g. X=2) widens here, and
                    // the ReseatCorrectedBottom below uses the SCALED bounds so it still sits flat.
                    if (entry.orientation.HasScale)
                        visual.transform.localScale = Vector3.Scale(visual.transform.localScale, entry.orientation.EffectiveScale);

                    // FIX (build-placed FLOAT) — VisualFactory.SeatOnGround already seated the
                    // RAW (un-corrected, often lying-down) bounds base at the root y. Applying the
                    // upright correction AFTER that re-tips the mesh so its real base is no longer
                    // at root y → the placed tower floats (or sinks). Re-seat the CORRECTED bounds:
                    // drop the now-upright bounds.min.y back to the root's y. Same corrected mesh the
                    // ghost shows, so WYSIWYG holds and pieces sit flat on the ground.
                    ReseatCorrectedBottom(visual, root.transform.position.y);
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

        // ── corrected-bounds seat + footprint (fixes float + tight-to-wall) ───
        // The placement pipeline (ghost + footprint + seat) must measure the UPRIGHT,
        // OrientationFix-applied bounds, not the raw lying-down prefab. These helpers
        // make that the single source of truth used by Create (seat) and the loader/
        // validity (footprint) so all three agree on the same corrected geometry.

        /// <summary>
        /// Re-seat a skinned visual so its CURRENT (post-correction) world-bounds base
        /// sits at <paramref name="groundY"/>. Called AFTER the OrientationFix re-rotates
        /// the mesh, undoing the float/sink VisualFactory.SeatOnGround introduced when it
        /// seated the raw (un-corrected) bounds. XZ is left as VisualFactory centred it.
        /// </summary>
        private static void ReseatCorrectedBottom(GameObject visual, float groundY)
        {
            if (visual == null) return;
            if (!TryWorldBounds(visual, out Bounds b)) return;
            float dy = groundY - b.min.y;
            if (!Mathf.Approximately(dy, 0f))
                visual.transform.position += new Vector3(0f, dy, 0f);
        }

        /// <summary>
        /// Build the entry's visual OFF-SCREEN, apply its OrientationFix, and measure the
        /// resulting UPRIGHT XZ footprint (the larger of width/depth, in metres). Used by
        /// the placement/loader path so the footprint matches the corrected mesh the ghost
        /// shows — a lying-down prefab would otherwise report a long, wrong footprint and
        /// the tower couldn't sit tight to a wall. Returns the entry's authored
        /// repo.placement.footprint as a fallback when the visual can't be measured.
        /// The temp object is destroyed before return (no scene side-effects).
        /// </summary>
        // Cache the (relatively expensive) upright measurement per entry id — the ghost
        // loop calls this every frame while arming. Keyed by id + a hash of the orientation
        // so a live re-orient (build-mode orient editor) invalidates the stale value.
        private static readonly System.Collections.Generic.Dictionary<string, float> s_footprintCache =
            new System.Collections.Generic.Dictionary<string, float>();

        public static float MeasureUprightFootprintMetres(CatalogEntry entry)
        {
            float authored = entry != null && entry.repo != null && entry.repo.placement != null
                ? Mathf.Max(1f, entry.repo.placement.footprint) : 3f;
            if (entry == null || string.IsNullOrEmpty(entry.visualPrefabPath)) return authored;

            // Cache key folds in the orientation so a live re-orient re-measures.
            // Includes the per-axis EffectiveScale so a non-uniform stretch invalidates
            // the stale footprint (a wider wall must report a wider footprint).
            var o = entry.orientation;
            Vector3 es = o != null ? o.EffectiveScale : Vector3.one;
            string key = o != null && o.manual
                ? $"{entry.id}|{o.Euler.x:0.#},{o.Euler.y:0.#},{o.Euler.z:0.#}|{es.x:0.##},{es.y:0.##},{es.z:0.##}"
                : entry.id;
            if (s_footprintCache.TryGetValue(key, out float cached)) return cached;

            var probe = new GameObject("FootprintProbe");
            probe.hideFlags = HideFlags.HideAndDontSave;
            float result = authored;
            try
            {
                SkinOptions opts;
                float visualHeight = entry.repo != null ? entry.repo.visualHeight : 0f;
                if (visualHeight > 0f) { opts = SkinOptions.Structure(0f); opts.FitHeight = visualHeight; }
                else                   { opts = SkinOptions.Structure(authored); }

                var visual = VisualFactory.Skin(probe.transform, entry.visualPrefabPath, opts);
                if (visual != null)
                {
                    if (entry.orientation != null && entry.orientation.manual)
                    {
                        visual.transform.localRotation = Quaternion.Euler(entry.orientation.Euler) * visual.transform.localRotation;
                        visual.transform.localPosition += entry.orientation.Offset;
                        // Same per-axis effective scale as Create() so the measured footprint
                        // matches the stretched mesh that actually gets placed.
                        if (entry.orientation.HasScale)
                            visual.transform.localScale = Vector3.Scale(visual.transform.localScale, entry.orientation.EffectiveScale);
                    }
                    if (TryWorldBounds(visual, out Bounds b))
                        result = Mathf.Max(0.1f, Mathf.Max(b.size.x, b.size.z));
                }
            }
            finally
            {
                if (Application.isPlaying) Object.Destroy(probe);
                else                       Object.DestroyImmediate(probe);
            }
            s_footprintCache[key] = result;
            return result;
        }

        /// <summary>World-space renderer bounds of <paramref name="go"/> (renderer-first, collider fallback).</summary>
        private static bool TryWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (go == null) return false;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends != null && rends.Length > 0)
            {
                bounds = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
                return true;
            }
            var col = go.GetComponentInChildren<Collider>(true);
            if (col != null) { bounds = col.bounds; return true; }
            return false;
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

                // ArcaneTower — WO-113: the slow-firing AoE MAGIC tower. Same copy-
                // stats-off-RepoProps pattern as DefenseTower, plus the optional AoE
                // fields (aoeRadius / slowSeconds / splashFraction). Each optional field
                // only overrides the component's serialized default when authored > 0,
                // so a sparse row still gets a sensible blast + slow.
                case "ArcaneTower":
                {
                    var a = root.AddComponent<ArcaneTower>();
                    var r = entry.repo;
                    a.Range     = r.range;
                    a.Damage    = r.damage;
                    a.FireRate  = r.fireRate;
                    a.CanHitAir = r.canHitAir;
                    a.Element   = r.element != DamageElement.None ? r.element : DamageElement.Aether;
                    if (r.aoeRadius      > 0f) a.AoeRadius            = r.aoeRadius;
                    if (r.slowSeconds    > 0f) a.SlowSeconds          = r.slowSeconds;
                    if (r.splashFraction > 0f) a.SplashDamageFraction = r.splashFraction;
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

                // GameplayBuilding — Phase 2: the village's economy/upgrade buildings
                // (pet-house / workshop / market / mill / lumbermill / forge / arcane-
                // tower) as first-class droppable catalog entries. Mirrors the hard-coded
                // VillageSceneBuilder.Buildings[] inject (which stays as the parity
                // fallback): attach + Configure a Building from the catalog row, add the
                // proximity-prompt BuildingInteractable, and register the building with
                // the scene's VillageController so it shows in the roster / wave damage
                // accounting exactly like a baked one.
                //
                // The catalog 'id' MUST equal Building.Id verbatim (pet flow / gear /
                // talents key on it) — BuildingType is derived from the id so a Type
                // enum collision (Market & Lumbermill both ordinal 5) stays benign:
                // identity is by id, the enum only steers the default panel route.
                case "GameplayBuilding":
                {
                    var b = root.AddComponent<Building>();
                    b.Configure(BuildingTypeForId(entry.id), entry.id,
                                string.IsNullOrEmpty(entry.displayName) ? entry.id : entry.displayName);

                    // Proximity F-prompt / mobile interact button (RequireComponent(Building)).
                    root.AddComponent<BuildingInteractable>();

                    // Register with the live scene controller so the placed building joins
                    // the roster (null-safe: a headless / controller-less scene just skips it).
                    var controller = Object.FindObjectOfType<VillageController>();
                    if (controller != null) controller.RegisterBuilding(b);
                    break;
                }

                default:
                    Debug.LogWarning($"[StructureFactory] '{entry.id}': unknown behaviorId " +
                                     $"'{behaviorId}' — no behaviour attached.");
                    break;
            }
        }

        /// <summary>
        /// Maps a catalog building id to its <see cref="BuildingType"/>, matching the
        /// hard-coded VillageSceneBuilder.Buildings[] Type assignments so a catalog-placed
        /// building routes its interaction panel the same as a baked one. Identity is by
        /// id (Building.Id), not by this enum — Market and Lumbermill share ordinal 5 in
        /// the Buildings[] table, which is benign (the enum only picks the default panel;
        /// BuildingInteractable re-resolves the actual route from the id first). Unknown
        /// ids fall back to CrystalMine (ordinal 0 / Upgrade panel), never throwing.
        /// </summary>
        private static BuildingType BuildingTypeForId(string id)
        {
            switch ((id ?? "").ToLowerInvariant())
            {
                case "pet-house":    return BuildingType.PetHouse;
                case "arcane-tower": return BuildingType.ArcaneTower;
                case "workshop":     return BuildingType.Workshop;   // labelled "Forge" in-world
                case "farm":
                case "mill":         return BuildingType.Farm;
                case "market":       return BuildingType.Lumbermill; // matches Buildings[] Type=5
                case "lumbermill":   return BuildingType.Lumbermill;
                case "forge":        return BuildingType.Forge;      // the Armorer
                default:             return BuildingType.CrystalMine;
            }
        }
    }
}
