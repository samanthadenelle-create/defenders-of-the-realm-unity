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

using System.Collections.Generic;   // ReskinForLevel old-visual collection
using UnityEngine;
using UnityEngine.AI;          // NavMeshObstacle footprint self-report (invisible-blocker class)
using DeNelle.Core.Catalog;
using DeNelle.Core.Combat;     // DamageElement literals (WO-113 ArcaneTower default element)
using DeNelle.Core.Diagnostics; // FlowTrace / Guard — TGVRU instrumentation (§12)

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
            using var _ = FlowTrace.Enter("Structure", $"Create id='{entry?.id ?? "<null>"}'");

            if (entry == null)
            {
                // U: was Debug.LogWarning — roll up via FlowTrace so a null-entry create self-reports.
                FlowTrace.Fail("Structure", "Create called with a null entry — skipped (returning null; caller falls back).");
                return null;
            }

            // Composites delegate to CreateGroup so a single id can build a bundle.
            if (entry.kind == EntryKind.Composite)
            {
                FlowTrace.Step("Structure", $"'{entry.id}' is Composite — delegating to CreateGroup.");
                return CreateGroup(entry, pose, parent);
            }

            // Root host — owns the world transform; the visual is skinned under it.
            var root = new GameObject(string.IsNullOrEmpty(entry.displayName)
                ? $"Structure-{entry.id}" : entry.displayName);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pose.position, pose.rotation);
            FlowTrace.Step("Structure", $"'{entry.id}' root '{root.name}' created at {pose.position}.");

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

                // G: Guard the Skin — a throwing VisualFactory (bad prefab path / Addressables
                // hiccup) logs + rolls up instead of aborting the whole create. null => meshless.
                GameObject visual = Guard.Try("Structure",
                    $"skin '{entry.id}' visual '{entry.visualPrefabPath}'",
                    () => VisualFactory.Skin(root.transform, entry.visualPrefabPath, opts),
                    fallback: null);

                if (visual == null)
                {
                    // U: was Debug.LogWarning — Fail-loud so a meshless (grey/invisible) structure
                    // self-reports. R: meshless is render-broken — destroy the empty root and return
                    // null so the caller falls back, never seats a silent invisible blocker.
                    FlowTrace.Fail("Structure", $"'{entry.id}': visual '{entry.visualPrefabPath}' " +
                        "not found / failed to skin — destroying empty root, returning null (caller falls back).");
                    DestroyRoot(root);
                    return null;
                }

                if (entry.orientation != null && entry.orientation.manual)
                {
                    // Apply ONLY human-verified (Inspector) orientation corrections — auto-baked
                    // ones are advisory (a bounds heuristic can't be trusted to not tip good assets).
                    // G: guarded so a malformed orientation row logs + leaves the raw mesh, never throws.
                    Guard.Try("Structure", $"apply orientation '{entry.id}'", () =>
                    {
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
                    });
                }

                // WO-707 texPath escape hatch (ported verbatim from HubStructureVisualInjector's
                // swap table): force a Resources texture onto the skinned materials when the
                // model's embedded material lost its map link (renders colorless — the arcane
                // tower FBX). The Tripo fixer reads the SOURCE material's _MainTex/_BaseMap, so
                // a model whose FBX material lost that link needs it forced here, same ordering
                // as the proven swap path (after Skin added the fixer). G: guarded — a missing
                // texture logs + leaves the untinted mesh, never aborts the create.
                if (!string.IsNullOrEmpty(entry.visualTexturePath))
                    Guard.Try("Structure", $"force texture '{entry.id}'",
                        () => ApplyForcedTexture(visual, entry.visualTexturePath, entry.id));

                // V + R: PROVE the skinned structure can render (>=1 enabled renderer with a
                // sharedMesh) — the grey-foundation / floating-untextured class self-reports here.
                // On a render-broken create, Fail + destroy + return null so the caller falls back,
                // never seats a silent broken structure. A NavMeshObstacle/collider footprint is
                // logged so an "invisible blocker" announces its size.
                if (!VerifyStructureRenders(root, entry.id))
                {
                    DestroyRoot(root);
                    return null;
                }
            }

            // BEHAVIOR — resolve the Core string id to a real Village component.
            AttachBehavior(root, entry);

            FlowTrace.Step("Structure", $"'{entry.id}' created OK -> '{root.name}'.");
            return root;
        }

        /// <summary>
        /// WO-707 — force a Resources texture onto every material of the skinned visual.
        /// Ported from HubStructureVisualInjector.TrySwap's texPath escape hatch (the
        /// owner-dialed swap table): a Tripo FBX whose embedded material lost its
        /// _MainTex/_BaseMap link renders colorless; this rebinds the authored texture.
        /// Play mode uses instance materials (safe to retint a one-off building — same
        /// as the proven swap path); edit mode uses sharedMaterials to avoid the
        /// edit-time material-instantiation leak.
        /// </summary>
        private static void ApplyForcedTexture(GameObject visual, string texPath, string id)
        {
            if (visual == null || string.IsNullOrEmpty(texPath)) return;
            var tex = Resources.Load<Texture2D>(texPath);
            if (tex == null)
            {
                FlowTrace.Warn("Structure",
                    $"'{id}': visualTexturePath '{texPath}' not found in Resources — leaving materials as-is (may render colorless).");
                return;
            }
            int touched = 0;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = Application.isPlaying ? r.materials : r.sharedMaterials;
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_BaseMap"))   m.SetTexture("_BaseMap", tex);
                    if (m.HasProperty("_MainTex"))   m.SetTexture("_MainTex", tex);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                    touched++;
                }
            }
            FlowTrace.Step("Structure",
                $"'{id}': forced texture '{texPath}' onto {touched} material(s) (WO-707 texPath port).");
        }

        /// <summary>The Resources visual path a structure shows at <paramref name="level"/>:
        /// repo.upgradeVisualPath[level-2] when authored (L2 = [0], L3 = [1] — the RepoProps
        /// contract), else the base visualPrefabPath. Data was write-only until the owner F8
        /// 2026-07-06 "on tower upgrade it's just making bigger, need to replace with new
        /// structure" — this + ReskinForLevel make the ladder real.</summary>
        public static string VisualPathForLevel(CatalogEntry entry, int level)
        {
            if (entry == null) return null;
            var ladder = entry.repo != null ? entry.repo.upgradeVisualPath : null;
            if (level >= 2 && ladder != null && ladder.Length >= level - 1
                && !string.IsNullOrEmpty(ladder[level - 2]))
                return ladder[level - 2];
            return entry.visualPrefabPath;
        }

        /// <summary>
        /// Swap the skinned visual to the per-tier model for <paramref name="level"/>.
        /// Returns TRUE only when a real per-tier model (different from the base path) is
        /// now worn — the caller then SKIPS the legacy StructureTierVisual scale-step (the
        /// model IS the progression). New visual is skinned BEFORE the old is destroyed, so
        /// a bad path keeps the old look (never a blank structure). No-op-true when the
        /// tier model is already worn (idempotent across re-loads).
        /// </summary>
        public static bool ReskinForLevel(GameObject root, CatalogEntry entry, int level)
        {
            if (root == null || entry == null) return false;
            string path = VisualPathForLevel(entry, level);
            if (string.IsNullOrEmpty(path) || path == entry.visualPrefabPath)
                return false;   // no authored tier model — legacy scale/tint applies

            // Already wearing it? (Skin instantiates '<prefab>(Clone)' under the root.)
            string stem = path.Substring(path.LastIndexOf('/') + 1);
            for (int i = 0; i < root.transform.childCount; i++)
                if (root.transform.GetChild(i).name.StartsWith(stem)) return true;

            // Collect the current visual children BEFORE adding the new one (renderer-bearing
            // direct children; leaves non-visual children like the WO-612 BuildCountdown alone).
            var old = new List<GameObject>();
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var c = root.transform.GetChild(i);
                if (c.GetComponentInChildren<Renderer>(true) != null) old.Add(c.gameObject);
            }

            GameObject visual = Guard.Try("Structure",
                $"reskin '{entry.id}' L{level} visual '{path}'",
                () => VisualFactory.Skin(root.transform, path, OptsFor(entry)),
                fallback: null);
            if (visual == null)
            {
                FlowTrace.Fail("Structure", $"'{entry.id}': tier-{level} visual '{path}' failed to " +
                    "skin — keeping the previous visual (structure never blanks).");
                return false;
            }

            // Orientation entries are authored against the BASE visualPrefabPath model (the
            // CatalogOrientationBaker / owner-manual contract). ReskinForLevel only ever runs when a
            // DIFFERENT tier model is worn (the early-return above), so applying the base euler here
            // tips tier models that are already upright — F8-2 2026-07-07: tower_wall_wizard's Tripo
            // base needs Z-90 while its L2 Tower_Medieval_Big (polyperfect) is upright. Tier models
            // rely on their prefab-native orientation; a tier needing its own correction gets its own
            // authoring seam when that real need exists.

            foreach (var g in old) Object.Destroy(g);
            FlowTrace.Step("Structure", $"'{entry.id}' reskinned to tier-{level} model '{stem}' " +
                $"(replaced {old.Count} old visual(s)).");
            return true;
        }

        /// <summary>DEF-208 skin options for an entry — fit-to-height when repo.visualHeight
        /// is authored, else legacy fit-to-footprint. Shared by Create + ReskinForLevel.</summary>
        private static SkinOptions OptsFor(CatalogEntry entry)
        {
            float visualHeight = entry.repo != null ? entry.repo.visualHeight : 0f;
            if (visualHeight > 0f)
            {
                var o = SkinOptions.Structure(0f);
                o.FitHeight = visualHeight;
                return o;
            }
            float fit = entry.repo != null && entry.repo.placement != null
                ? Mathf.Max(1f, entry.repo.placement.footprint)
                : 3f;
            return SkinOptions.Structure(fit);
        }

        // V (render-verify) + footprint self-report. Mirrors HeroArmorVisual.VerifyArmorRendersNow:
        // the created structure MUST carry >=1 ENABLED Renderer with a non-null sharedMesh, else it
        // reads as the grey-foundation / invisible-untextured class the owner flagged. Traces the exact
        // counts so a capture splits "no renderer" vs "renderer but no mesh" vs "all disabled" with zero
        // guessing. ALSO logs the footprint of any NavMeshObstacle/Collider so an INVISIBLE BLOCKER
        // (collider with no visible mesh, or a too-large obstacle) self-reports its size. Returns false
        // => caller rolls back (destroy + null) so callers fall back rather than seat a broken structure.
        private static bool VerifyStructureRenders(GameObject root, string id)
        {
            if (root == null)
            {
                FlowTrace.Fail("Structure", $"VerifyStructureRenders: root for '{id}' is null.");
                return false;
            }

            int total = 0, enabledRend = 0, withMesh = 0;
            Guard.Try("Structure", $"render-verify '{id}'", () =>
            {
                var rends = root.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    total++;
                    if (r.enabled) enabledRend++;
                    // sharedMesh lives on MeshRenderer's sibling MeshFilter, or on a
                    // SkinnedMeshRenderer directly — check both so any renderer kind counts.
                    if (RendererHasMesh(r)) withMesh++;
                }
            });

            // Footprint self-report (invisible-blocker class). Any blocking volume is logged so a
            // collider/obstacle that occupies space WITHOUT a visible mesh announces itself.
            Guard.Try("Structure", $"footprint-probe '{id}'", () =>
            {
                var obstacle = root.GetComponentInChildren<NavMeshObstacle>(true);
                var col = root.GetComponentInChildren<Collider>(true);
                if (obstacle != null)
                    FlowTrace.Step("Structure",
                        $"'{id}' carries NavMeshObstacle (carving={obstacle.carving}, size={obstacle.size}, center={obstacle.center}) — blocking footprint.");
                if (col != null)
                {
                    Bounds cb = col.bounds;
                    FlowTrace.Step("Structure",
                        $"'{id}' carries Collider '{col.GetType().Name}' bounds size={cb.size} center={cb.center} — physical footprint.");
                    if (withMesh == 0)
                        FlowTrace.Warn("Structure",
                            $"'{id}' has a Collider/obstacle but NO visible mesh — this is the INVISIBLE-BLOCKER shape (footprint {cb.size}).");
                }
            });

            bool renders = enabledRend > 0 && withMesh > 0;
            FlowTrace.Step("Structure",
                $"VerifyStructureRenders '{id}' on '{root.name}': renderers total={total} enabled={enabledRend} withMesh={withMesh} => renders={renders}.");

            if (!renders)
            {
                FlowTrace.Fail("Structure",
                    $"VerifyStructureRenders FAILED '{id}' on '{root.name}': renders={renders} " +
                    $"(total={total}, enabled={enabledRend}, withMesh={withMesh}) — grey/invisible/untextured structure; " +
                    "destroying + returning null so the caller falls back (no silent broken structure).");
                return false;
            }
            return true;
        }

        /// <summary>True when <paramref name="r"/> resolves a non-null mesh (MeshFilter sibling for a
        /// MeshRenderer, sharedMesh for a SkinnedMeshRenderer). The grey-foundation symptom is a
        /// renderer present but no mesh bound.</summary>
        private static bool RendererHasMesh(Renderer r)
        {
            if (r == null) return false;
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh != null;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null;
        }

        /// <summary>Destroy a half-built root on a render-broken create (play vs. edit safe). Control-flow
        /// safety — runs regardless of the FlowTrace toggle so a broken structure is never left in-scene.</summary>
        private static void DestroyRoot(GameObject root)
        {
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root);
            else                       Object.DestroyImmediate(root);
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
            using var _ = FlowTrace.Enter("Structure", $"CreateGroup id='{composite?.id ?? "<null>"}'");

            if (composite == null)
            {
                // U: was Debug.LogWarning — roll up.
                FlowTrace.Fail("Structure", "CreateGroup called with a null entry — skipped (returning null).");
                return null;
            }

            var groupRoot = new GameObject(string.IsNullOrEmpty(composite.displayName)
                ? $"Group-{composite.id}" : composite.displayName);
            groupRoot.transform.SetParent(parent, false);
            groupRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);

            if (composite.composite == null || composite.composite.Length == 0)
            {
                // U: was Debug.LogWarning — Warn (empty composite is recoverable, returns the bare root).
                FlowTrace.Warn("Structure", $"composite '{composite.id}' has no cell placements — empty group returned.");
                return groupRoot;
            }

            // G: TryEach the member loop — ONE bad member (throwing Create / bad pose) logs + is
            // SKIPPED, never aborts the whole group (a castle must not vanish because one wall threw).
            int built = 0;
            var result = Guard.TryEach("Structure", $"build member of '{composite.id}'",
                composite.composite, cell =>
            {
                if (cell == null || string.IsNullOrEmpty(cell.cellEntryId)) return;

                var member = CatalogRegistry.Get(cell.cellEntryId);
                if (member == null)
                {
                    // U: was Debug.LogWarning — Warn + skip this member, keep building the rest.
                    FlowTrace.Warn("Structure", $"composite '{composite.id}': member " +
                        $"'{cell.cellEntryId}' not in registry — skipped.");
                    return;
                }

                // Member pose is the cell offset/rotation expressed in the group's
                // local space, composed onto the group's world pose.
                Vector3 worldPos = groupRoot.transform.TransformPoint(cell.offset);
                Quaternion worldRot = groupRoot.transform.rotation *
                                      Quaternion.Euler(0f, cell.yRotation, 0f);

                if (Create(member, new Pose(worldPos, worldRot), groupRoot.transform) != null)
                    built++;
                else
                    FlowTrace.Warn("Structure",
                        $"composite '{composite.id}': member '{cell.cellEntryId}' Create returned null (render-broken/missing) — skipped.");
            });

            FlowTrace.Step("Structure", $"composite '{composite.id}' built {built}/" +
                $"{composite.composite.Length} member(s) (loop built={result.built}, failed={result.failed}).");
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
            // G: guard the bounds op — a degenerate/NaN mesh bound logs + leaves the seat unchanged
            // (the float self-reports via the bounds-miss path) rather than throwing mid-create.
            Guard.Try("Structure", "reseat corrected bottom", () =>
            {
                if (!TryWorldBounds(visual, out Bounds b))
                {
                    FlowTrace.Warn("Structure", $"ReseatCorrectedBottom: no measurable bounds on '{visual.name}' — left at seat (may float/sink).");
                    return;
                }
                float dy = groundY - b.min.y;
                if (!Mathf.Approximately(dy, 0f))
                    visual.transform.position += new Vector3(0f, dy, 0f);
            });
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
                // G: guard the off-screen skin+measure — a throwing prefab logs + falls back to the
                // authored footprint (the temp object is still destroyed in finally), never throws up.
                Guard.Try("Structure", $"measure upright footprint '{entry.id}'", () =>
                {
                    SkinOptions opts;
                    float visualHeight = entry.repo != null ? entry.repo.visualHeight : 0f;
                    if (visualHeight > 0f) { opts = SkinOptions.Structure(0f); opts.FitHeight = visualHeight; }
                    else                   { opts = SkinOptions.Structure(authored); }

                    var visual = VisualFactory.Skin(probe.transform, entry.visualPrefabPath, opts);
                    if (visual == null)
                    {
                        FlowTrace.Warn("Structure",
                            $"MeasureUprightFootprint '{entry.id}': visual '{entry.visualPrefabPath}' failed to skin — using authored footprint {authored:0.##}m.");
                        return;
                    }
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
                    else
                        FlowTrace.Warn("Structure",
                            $"MeasureUprightFootprint '{entry.id}': no measurable bounds — using authored footprint {authored:0.##}m.");
                });
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

            // G: guard the whole attach — a throwing AddComponent/Configure logs + leaves the
            // already-rendered structure standing (visual is verified before this), never throws
            // out of Create and never blanks a built structure.
            Guard.Try("Structure", $"attach behavior '{behaviorId}' to '{entry.id}'", () =>
            AttachBehaviorImpl(root, entry, behaviorId));
        }

        private static void AttachBehaviorImpl(GameObject root, CatalogEntry entry, string behaviorId)
        {
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
                    // ANTI-AIR SPECIALIST (owner 2026-07-08): the Ballista's airOnly flag makes it
                    // acquire ONLY flying targets. Implies CanHitAir so it reaches the cruising dragon.
                    t.AirOnly   = r.airOnly;
                    if (r.airOnly) t.CanHitAir = true;
                    t.Element   = r.element;
                    // TOWER IDENTITY (owner 2026-07-08): optional per-entry projectile
                    // VISUAL style ("pellet"|"bolt"|"spell"; null = pellet). Data-driven —
                    // the component resolves + instruments the string itself.
                    t.ProjectileStyle = r.projectileStyle;
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

                // ResourceCollector — CoC-style typed town collector (Farm / Lumbermill /
                // Forge). A collector places EXACTLY like any other structure (same grid /
                // ghost / persist path) — only the attached behaviour differs. Configure it
                // with the ResourceBuildingProgression id from the row (collectorBuildingId),
                // falling back to the entry id when the row omits it.
                case "ResourceCollector":
                {
                    var col = root.AddComponent<DeNelle.Village.Buildings.Progression.ResourceCollector>();
                    var r = entry.repo;
                    string buildingId = !string.IsNullOrEmpty(r != null ? r.collectorBuildingId : null)
                        ? r.collectorBuildingId : entry.id;
                    col.Configure(buildingId);
                    break;
                }

                // CrystalMine — passive Aether-Crystal generator (banks +1/wave at
                // L3 via WaveManager.OnWaveCleared). Self-resolves hero/wave/economy
                // in Start and builds its own placeholder visual when no prefab is
                // assigned, so a placed mine is a real, upgradeable gameplay object.
                case "CrystalMine":
                    root.AddComponent<CrystalMine>();
                    break;

                // HealingFountain — Wellspring of Elarion. A SUPPORT structure that heals
                // the Heart of Elarion out of battle only (rate scales L1=1.0/L2=2.0/L3=3.5
                // HP/s). Self-resolves Heart + WaveManager in Start; Configure reads the
                // level ceiling from RepoProps. Gated behind the arcane-tower research perk
                // 'arcane-wellspring' at the build-palette layer.
                case "HealingFountain":
                {
                    var f = root.AddComponent<HealingFountain>();
                    f.Configure(entry);
                    break;
                }

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
                    var controller = Object.FindAnyObjectByType<VillageController>();
                    if (controller != null) controller.RegisterBuilding(b);

                    // Owner 2026-07-15 "arcane towers should have an aura": the arcane-tower
                    // landmark (this GameplayBuilding path replays it from BaseLayout) holds a
                    // persistent magic-circle aura. Idempotent; colorblind-safe (motion, not hue).
                    if (string.Equals(entry.id, "arcane-tower", System.StringComparison.OrdinalIgnoreCase))
                        ArcaneAura.Ensure(root);
                    break;
                }

                default:
                    // U: was Debug.LogWarning — Warn (the structure still renders; it just has no
                    // gameplay behaviour, which a capture should surface, not silently swallow).
                    FlowTrace.Warn("Structure", $"'{entry.id}': unknown behaviorId " +
                        $"'{behaviorId}' — no behaviour attached (visual-only structure).");
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
                case "forge":        return BuildingType.Forge;      // weapons
                case "armorer":
                case "blacksmith":   return BuildingType.Armorer;    // armor vendor
                case "jeweler":      return BuildingType.Workshop;   // Sable the Jeweler (crafting/upgrade station; Yarn route resolves by name -> TalkToJeweler)
                // WO-707 storage containers (lumberyard/foundry/silo): no Storage-ish
                // BuildingType exists, so they take the generic CrystalMine ordinal —
                // its default Upgrade panel is the right route for a capacity-upgradeable
                // container. Explicit cases so the mapping is a decision, not a fallthrough.
                case "lumberyard":
                case "foundry":
                case "silo":         return BuildingType.CrystalMine;
                default:             return BuildingType.CrystalMine;
            }
        }
    }
}
