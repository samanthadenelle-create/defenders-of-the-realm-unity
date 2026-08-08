// =============================================================================
// VisualFactory — one runtime "skinner" for any gameplay object: enemy, animal,
// tower, structure, prop.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The same five steps were copy-pasted across the codebase whenever something
// needed a real mesh at runtime — load a model, instantiate it under a host,
// scale it to fit, seat it on the ground, fix its materials for URP, strip its
// stray colliders. This consolidates them behind ONE call:
//
//     VisualFactory.Skin(host, "Enemies/Skeleton_Warrior", SkinOptions.Enemy(1.9f));
//     VisualFactory.Skin(host, "Structures/Tower",          SkinOptions.Structure(17f));
//
// It is deliberately VISUAL-ONLY: it does not touch gameplay. Living things layer
// their animator on top afterwards (EnemyAnimatorFactory) — a static wall simply
// never asks for one. That keeps the skinner universal without pretending a tower
// is animated.
//
// Runtime/Resources-based (the editor scene baker has its own AssetDatabase path +
// the same fit/seat helpers; the two asmdefs can't share without a reference, so
// this mirrors that logic for the runtime side rather than forcing a dependency).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>How <see cref="VisualFactory"/> should dress a model. Use the
    /// <see cref="Enemy"/> / <see cref="Structure"/> / <see cref="Prop"/> presets,
    /// or set fields directly.</summary>
    public struct SkinOptions
    {
        public float FitHeight;          // >0 → scale so world-bounds HEIGHT = this
        public float FitLargest;         // >0 → scale so LARGEST world-bounds dim = this (wins over FitHeight)
        public bool  SeatOnGround;       // shift so the bounds base sits at the host's y
        public bool  StripColliders;     // remove the model's own colliders (the host owns its collider)
        public bool  FixTripoMaterials;  // attach DeNelle.Core.TripoMaterialFixer (Tripo→URP) via reflection

        // DEF-232: a fixed local orientation correction APPLIED BEFORE Fit / SeatOnGround.
        // Hero/companion bodies import facing +X and need a -90° yaw to face the root's +Z.
        // Callers used to apply that yaw AFTER Skin returned — but SeatOnGround had already
        // centred the (off-pivot) bounds over the root while the body was still at identity,
        // so the post-Skin rotation swung the off-centre mesh sideways (camera "stays to her
        // right", body "pivots in place" instead of translating). Set the yaw HERE instead:
        // the body is rotated FIRST, then fit + seated/centred in its FINAL orientation, so
        // the visible mesh sits dead-centre over the hero root regardless of import pivot.
        public Quaternion? LocalRotation;

        // FLAT-SEAT (owner F8 2026-07-04 "iron mine not sitting with flat side down"): derive a
        // natural resting orientation from the model's own geometry — rotate so its NARROWEST
        // world-bounds axis points to world +Y (CLAUDE.md §4: narrowest axis → up/down, so the
        // largest/flattest face rests on the ground). Applied AFTER LocalRotation and BEFORE
        // Fit/SeatOnGround, so the model is measured + seated in its final upright pose. Replaces
        // the guessed magic-euler tips (e.g. z:-129 / z:-90) that laid the harvest props on their
        // side. Robust to import pivot — measured live per instance, no per-prop hand-tuning.
        public bool SeatFlat;

        // WO-928: KEEP the prefab's own authored rotation instead of flattening it to identity.
        // The default below (identity when no LocalRotation is supplied) is DEF-232's and stays —
        // but it silently DESTROYS an orientation correction that was deliberately baked into a
        // prefab, which is how the L3 Archer Tower shipped lying on its side.
        //
        // Captured 2026-08-08, owner felt-test:
        //   after instantiate (prefab-native pose): euler=(270.00, 0.00, 0.00)   <- the bake is THERE
        //   after LocalRotation identity:           euler=(0.00, 0.00, 0.00)     <- and gone
        //   after Fit+SeatOnGround:                 scale=(8.34, 8.34, 8.34)
        //   skinned ... boundsSize=(4.91, 4.80, 8.34)
        // The second-order damage is worse than the wrong pose: once the model is flat, Fit measures
        // the WRONG AXIS to reach its height target, so the tower scales 8.34x instead of L1's 4.74x
        // and sprawls 8.34 m across a 3x3 m nav/physics blocker. Orientation and "the footprint is
        // huge" are ONE defect, not two.
        //
        // This is opt-in, not a default flip, precisely because DEF-232's identity is load-bearing for
        // hero/companion bodies (they import facing +X and are corrected via LocalRotation). Only
        // callers whose art carries its correction IN THE PREFAB set this — see SkinOptions.Structure.
        // ARCHITECTURE_PRINCIPLES law 4: an authored/manual correction is canon and is NEVER
        // overwritten by an automatic pass. Flattening it to identity was exactly that overwrite.
        public bool PreservePrefabRotation;

        /// <summary>An enemy/creature: fit to height, strip its colliders (the root carries the trigger capsule).</summary>
        public static SkinOptions Enemy(float height) =>
            new SkinOptions { FitHeight = height, StripColliders = true };

        /// <summary>A tower/building: fit to largest dimension, seat on ground, URP-fix Tripo materials.
        /// Keeps the prefab's authored rotation (WO-928): structure art carries its orientation
        /// correction baked in by its builder (e.g. DeNelle.Editor.WoodenWatchtowerBuilder), and
        /// structures-catalog's own orientation note records WHY it lives there rather than on the
        /// catalog row — applying it from the row would double-apply on top of the prefab, and
        /// ReskinForLevel deliberately does not apply entry.orientation at all, so a tier model has
        /// nothing BUT its prefab pose to stand it up.</summary>
        // ⛔ PreservePrefabRotation was set TRUE here on 2026-08-08 and REVERTED the same day.
        //
        // It fixed the L3 Archer Tower, whose baked -90 is the only thing standing it up. It also
        // reached EVERY structure, because StructureFactory.OptsFor builds from this factory - and
        // most Tripo building prefabs also instantiate at euler (270, 0, 0), where that same 270 is
        // precisely what DEF-232's identity reset exists to CANCEL. Preserving it laid the whole
        // town on its side.
        //
        // Captured (owner felt-test, returning to town from a dungeon):
        //     after instantiate (prefab-native pose): euler=(270.00, 0.00, 0.00)
        //     prefab rotation PRESERVED (WO-928):     euler=(270.00, 0.00, 0.00)   x10 structures
        //
        // It only surfaced on RE-ENTRY because the first town load seats buildings from the
        // bake/injector path; coming back re-creates them through BaseLayoutLoader ->
        // StructureFactory.Create -> here. A first-load-only test would have missed it entirely.
        //
        // THE LESSON, and it is the reason this comment is long: an opt-in is only as narrow as the
        // thing you opt in. "Structures" is not a narrow set. The correct scope is PER CATALOG
        // ENTRY - the wooden watchtower ladder specifically - not per factory. See WO-928 defect A.
        public static SkinOptions Structure(float largest) =>
            new SkinOptions { FitLargest = largest, SeatOnGround = true, FixTripoMaterials = true };

        /// <summary>A small prop: fit to largest dimension, seat on ground.</summary>
        public static SkinOptions Prop(float largest) =>
            new SkinOptions { FitLargest = largest, SeatOnGround = true };
    }

    public static class VisualFactory
    {
        /// <summary>Loads <paramref name="resourcesPath"/> from Resources and skins it
        /// under <paramref name="host"/>. Returns null (caller falls back) if absent.</summary>
        public static GameObject Skin(Transform host, string resourcesPath, SkinOptions opts)
        {
            using var _ = FlowTrace.Enter("VisualFactory", $"Skin('{resourcesPath}')");

            GameObject prefab = null;
            FlowTrace.Try("VisualFactory", $"Resources.Load '{resourcesPath}'",
                () => prefab = Resources.Load<GameObject>(resourcesPath));

            if (prefab == null)
            {
                // §12: a missing model is a hard miss the caller falls back on — promote from a
                // swallowed Debug.LogWarning to FlowTrace.Fail so it rolls up to the break-log
                // (error severity) and a headless capture pinpoints the unresolved Resources path.
                FlowTrace.Fail("VisualFactory",
                    $"model not found in Resources: '{resourcesPath}' — returning null (caller falls back).");
                return null;
            }
            FlowTrace.Step("VisualFactory", $"resolved Resources model '{resourcesPath}' -> '{prefab.name}'.");
            return Skin(host, prefab, opts);
        }

        /// <summary>Instantiates <paramref name="prefab"/> under <paramref name="host"/>
        /// and applies the skin options.</summary>
        public static GameObject Skin(Transform host, GameObject prefab, SkinOptions opts)
        {
            if (prefab == null)
            {
                FlowTrace.Fail("VisualFactory", "Skin called with a null prefab — returning null (caller falls back).");
                return null;
            }

            using var _ = FlowTrace.Enter("VisualFactory", $"Skin(prefab='{prefab.name}')");

            // Guard the Instantiate: a broken/aborted prefab clone returns null rather than NRE'ing
            // every caller. Treated as a miss (Fail + null) so callers fall back, never get half-built.
            GameObject go = null;
            FlowTrace.Try("VisualFactory", $"Instantiate '{prefab.name}'",
                () => go = Object.Instantiate(prefab, host));
            if (go == null)
            {
                FlowTrace.Fail("VisualFactory",
                    $"Instantiate returned null for prefab '{prefab.name}' — returning null (caller falls back).");
                return null;
            }

            go.transform.localPosition = Vector3.zero;

            // XFORM VALUE-TRACE (owner 2026-07-08: "i want to see everything that happens to it
            // from selecting fbx to placement" — the euler ping-pong RCA): one line per mutation
            // stage with the ACTUAL local euler/pos/scale, so a single placement prints the whole
            // transform journey. Companion census: docs/STRUCTURE_TRANSFORM_CENSUS (agent).
            void TraceXform(string stage)
            {
                var t = go.transform;
                FlowTrace.Step("Xform", $"'{prefab.name}' after {stage}: " +
                    $"euler={t.localEulerAngles} pos={t.localPosition} scale={t.localScale}");
            }
            TraceXform("instantiate (prefab-native pose)");

            // DEF-232: apply the caller's orientation BEFORE Fit/SeatOnGround so the body is
            // measured + centred in its FINAL facing. A post-Skin rotation (the old pattern)
            // swung the off-pivot bounds sideways. Default is identity (unchanged for callers
            // that don't pass LocalRotation, e.g. enemies/structures).
            // WO-928: an explicit LocalRotation always wins. Otherwise we only FORCE identity when the
            // caller has not asked us to preserve the prefab's authored pose — flattening a baked
            // correction is what laid the L3 Archer Tower on its side and then let Fit measure the
            // wrong axis (see SkinOptions.PreservePrefabRotation for the captured trace).
            if (opts.LocalRotation.HasValue)
                go.transform.localRotation = opts.LocalRotation.Value;
            else if (!opts.PreservePrefabRotation)
                go.transform.localRotation = Quaternion.identity;

            TraceXform(opts.LocalRotation.HasValue ? "opts.LocalRotation"
                     : opts.PreservePrefabRotation ? "prefab rotation PRESERVED (WO-928)"
                     : "LocalRotation identity");

            // FLAT-SEAT: derive the natural resting orientation from geometry (narrowest world-bounds
            // axis → +Y, §4) so the model sits flat side down. Runs BEFORE Fit/SeatOnGround so the
            // upright bounds are what gets fit + seated. Replaces guessed magic-euler tips.
            if (opts.SeatFlat)
            {
                FlowTrace.Try("VisualFactory", "seat flat (bounds-derived)", () => SeatFlat(go));
                TraceXform("SeatFlat");
            }

            if (opts.StripColliders)
                FlowTrace.Try("VisualFactory", "strip colliders",
                    () => { foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c); });

            if (opts.FixTripoMaterials)
                FlowTrace.Try("VisualFactory", "add Tripo material fixer", () => TryAddTripoFixer(go));

            FlowTrace.Try("VisualFactory", "fit + seat", () =>
            {
                if (opts.FitLargest > 0f)     Fit(go, opts.FitLargest, largest: true);
                else if (opts.FitHeight > 0f) Fit(go, opts.FitHeight,  largest: false);

                // SeatOnGround centres the (now correctly-oriented) bounds over the host's x/z and
                // drops the bounds-base to the host's y — so the visible mesh sits dead-centre over
                // the hero ROOT, the transform the camera follows and HeroLocomotion drives.
                if (opts.SeatOnGround)
                    SeatOnGround(go, host != null ? host.position : go.transform.position);
            });
            TraceXform("Fit+SeatOnGround");

            // RENDER-VERIFY (owner directive 2026-06-19: "anything that renders can be broken — check
            // render==true and roll back the error"). This is the #1 shared choke point — every
            // enemy/troop/structure/prop/animal/companion body skins through here. A prefab that loads
            // but renders nothing (no enabled renderer, missing mesh, degenerate bounds) reads as a
            // grey/empty body to the player. PROVE it can render before handing it back; a broken build
            // logs Fail (rolls up to break-log) and is destroyed + treated as a MISS (return null) so
            // the caller falls back — we never hand back a render-broken-but-non-null body silently.
            if (!VerifyRenders(go, prefab.name))
            {
                Object.Destroy(go);
                return null;
            }

            // RIG-LEVEL DRESSABLE capability (BlinkWardrobe, owner architecture 2026-06-20): a body that
            // ships outfit-set renderers self-dresses to its default outfit HERE — beside the rig, the one
            // shared path every character skins through — so EVERY dressable humanoid (hero / companion /
            // arena fighter / future human-skinned enemy) starts CLOTHED, never in underwear. Non-dressable
            // bodies (skeletons / animals / structures) ship no outfit renderers → IsDressable=false → skip.
            // The data-driven per-character wardrobe + cosmetic-store feed land on this seam (WO-456).
            FlowTrace.Try("VisualFactory", "wardrobe default-dress",
                () => { if (BlinkWardrobe.IsDressable(go)) BlinkWardrobe.DressInStarter(go); });

            // WO-436 Step 1 (§12): surface the ACTUAL material name on the skinned body so a headless
            // capture PROVES Failure A (URP material not applied → the raw FBX surface renders as Unity's
            // solid unlit-green fallback) instead of guessing. Null-guarded: no renderer/material → Warn
            // (never a silent blank). sharedMaterial (not .material) — no per-instance material leak.
            FlowTrace.Try("VisualFactory", "material trace", () =>
            {
                var renderer = go.GetComponentInChildren<Renderer>();
                if (renderer == null || renderer.sharedMaterial == null)
                    FlowTrace.Warn("EnemyVisual", $"Material on {prefab.name}: NO renderer/material (would render blank/fallback)");
                else
                    FlowTrace.Step("EnemyVisual", $"Material on {prefab.name}: {renderer.sharedMaterial.name}");
            });

            // MAGENTA RECOVERY AT THE SPAWN SEAM (owner defect 2026-08-02: "raid troops are magenta").
            // PROVEN CAUSE (not a theory): MagentaGuard.Init is [RuntimeInitializeOnLoadMethod(
            // AfterSceneLoad)] + SceneManager.sceneLoaded, and its Sweep takes a ONE-TIME
            // Object.FindObjectsByType<Renderer>() SNAPSHOT. It has no Update and had no per-object
            // entry point. A raid troop is built MID-RAID (TroopDeployer.SpawnFromArmy ->
            // TroopFactory.Build -> here), i.e. after every sceneLoaded has already fired, so the
            // guard was structurally BLIND to it and the body stayed magenta forever.
            //
            // THIS overload is the choke point: the (Transform, string, SkinOptions) overload above
            // resolves the prefab then calls straight into this one (:95), and every runtime factory
            // — TroopFactory, EnemyFactory, StructureFactory, GhostPreview, HubStructureVisualInjector,
            // MineNodeVisual, HarvestSite, the station injectors, BuildPreviewModal,
            // StoryCompanionInjector — enters through one of those two. One hook covers them all.
            //
            // Placed AFTER VerifyRenders + the wardrobe dress so it sees the FINAL renderer set
            // (BlinkWardrobe toggles outfit renderers), and it is the last thing before the body is
            // handed back. SweepGameObject never throws and warns-not-errors on a missing art pack.
            FlowTrace.Try("VisualFactory", "magenta sweep (runtime spawn seam)",
                () => DeNelle.Core.MagentaGuard.SweepGameObject(go, "VisualFactory.Skin"));

            return go;
        }

        // RENDER-VERIFY: the instantiated body MUST carry >=1 ENABLED Renderer (SkinnedMeshRenderer or
        // MeshRenderer) with a non-null shared mesh AND non-degenerate world bounds. Traces the exact
        // counts so a headless capture splits "no enabled renderer" vs "missing mesh" vs "degenerate
        // bounds" with zero guessing. Returns false => caller (Skin) treats the build as a miss.
        private static bool VerifyRenders(GameObject go, string what)
        {
            if (go == null)
            {
                FlowTrace.Fail("VisualFactory", $"VerifyRenders: skinned '{what}' instance is null.");
                return false;
            }

            var rends = go.GetComponentsInChildren<Renderer>(true);
            int total = 0, enabled = 0, withMesh = 0;
            foreach (var r in rends)
            {
                if (r == null) continue;
                total++;
                bool on = r.enabled && r.gameObject.activeInHierarchy;
                bool hasMesh = false;
                if (r is SkinnedMeshRenderer smr) hasMesh = smr.sharedMesh != null;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    hasMesh = mf != null && mf.sharedMesh != null;
                }
                if (on) enabled++;
                if (on && hasMesh) withMesh++;
            }

            bool boundsOk = TryBounds(go, out Bounds b) && b.size.sqrMagnitude > 1e-8f;
            bool renders = enabled > 0 && withMesh > 0 && boundsOk;

            FlowTrace.Step("VisualFactory",
                $"skinned '{what}' on '{go.name}': renderers={total} enabled={enabled} withMesh={withMesh} " +
                $"boundsSize={(boundsOk ? b.size.ToString("F2") : "<degenerate>")} => renders={renders}");

            if (!renders)
            {
                FlowTrace.Fail("VisualFactory",
                    $"VerifyRenders FAILED for skinned '{what}' on '{go.name}': renderers={total} enabled={enabled} " +
                    $"withMesh={withMesh} boundsOk={boundsOk} — treating as a MISS (destroy + return null; caller falls back).");
                return false;
            }
            return true;
        }

        // ── Geometry helpers ─────────────────────────────────────────────────
        /// <summary>Uniformly scales so the world-bounds HEIGHT (or largest dimension)
        /// equals <paramref name="target"/> — robust to arbitrary import scale.</summary>
        private static void Fit(GameObject go, float target, bool largest)
        {
            if (!TryBounds(go, out Bounds b)) return;
            float measure = largest ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) : b.size.y;
            if (measure < 0.0001f) return;
            go.transform.localScale *= target / measure;
        }

        /// <summary>Shifts the object so its bounds base sits at <paramref name="basePos"/>.y
        /// (centred on basePos.x/z).</summary>
        private static void SeatOnGround(GameObject go, Vector3 basePos)
        {
            if (!TryBounds(go, out Bounds b)) return;
            Vector3 delta = new Vector3(basePos.x - b.center.x,
                                        basePos.y - b.min.y,
                                        basePos.z - b.center.z);
            go.transform.position += delta;
        }

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            bounds = default;
            if (rends.Length == 0) return false;
            bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
            return true;
        }

        /// <summary>Levels a prop to rest on its flattest face: rotates (world space) so the
        /// NARROWEST world-bounds axis points to world +Y (CLAUDE.md §4). This is the runtime
        /// twin of CatalogOrientationBaker's bake-time keep-vertical heuristic — a geometry
        /// derivation, not a hand-authored euler. A model already flat (Y narrowest) is left
        /// untouched. Measured from live world bounds so it is robust to any import pivot.</summary>
        private static void SeatFlat(GameObject go)
        {
            if (go == null || !TryBounds(go, out Bounds b)) return;
            Vector3 sz = b.size;

            // Index of the SHORTEST world-bounds axis (0=X, 1=Y, 2=Z). That axis should be vertical
            // so the two longer axes span the ground-resting face.
            int shortest = (sz.x <= sz.y && sz.x <= sz.z) ? 0 : (sz.y <= sz.z ? 1 : 2);
            if (shortest == 1)
            {
                FlowTrace.Step("VisualFactory",
                    $"SeatFlat('{go.name}'): already flat (Y narrowest, size {sz:F2}) — no rotation.");
                return;
            }

            // Bring the current shortest WORLD axis onto world +Y with the shortest-arc rotation,
            // pre-multiplied so it applies in world space (parent may be rotated).
            Vector3 shortAxis = shortest == 0 ? Vector3.right : Vector3.forward;
            Quaternion delta = Quaternion.FromToRotation(shortAxis, Vector3.up);
            go.transform.rotation = delta * go.transform.rotation;

            FlowTrace.Step("VisualFactory",
                $"SeatFlat('{go.name}'): narrowest axis was {(shortest == 0 ? "X" : "Z")} " +
                $"(size {sz:F2}) → stood it to +Y so the flat face rests down.");
        }

        // ── Tripo material fix (reflection — type lives in DeNelle.Core) ─────
        private static System.Type _tripoFixerType;
        private static bool _tripoLookedUp;

        private static void TryAddTripoFixer(GameObject go)
        {
            if (!_tripoLookedUp)
            {
                _tripoLookedUp = true;
                _tripoFixerType = System.Type.GetType("DeNelle.Core.TripoMaterialFixer, DeNelle.Core")
                               ?? System.Type.GetType("DeNelle.Core.TripoMaterialFixer");
            }
            if (_tripoFixerType != null && go.GetComponent(_tripoFixerType) == null)
                go.AddComponent(_tripoFixerType);
        }
    }
}
