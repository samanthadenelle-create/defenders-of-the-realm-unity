// =============================================================================
// SurfaceImpactVfxMirrors (WO-887, surface half) - builds the five TRACKED,
// SHIPPABLE copies of the owner's five surface-impact tags.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
//   Run:    Defenders/VFX/Mirror Surface Impact VFX
//           (batchmode: DeNelle.Editor.SurfaceImpactVfxMirrors.Run)
//   Marker: SURFACE_IMPACT_VFX_MIRROR_OK <n> clean  /  SURFACE_IMPACT_VFX_MIRROR_FAIL
//
// -----------------------------------------------------------------------------
// WHY A DEDICATED BUILDER, AND NOT A ROW IN ParticlePackVfxBatchBuilder.
//
// That builder REFUSES these five by design. Its ROOT DEMO-GEOMETRY GUARD (added
// by WO-892/893 precisely to make WO-887's refusal mechanical) throws the moment a
// source prefab's ROOT carries a MeshFilter / MeshRenderer / Collider, because a
// straight CopyAsset of such a prefab ships an effect that renders a lit primitive
// and DROPS A PHYSICS COLLIDER at every position it plays. That guard is correct
// and is NOT relaxed here - relaxing it would re-open the hole for every future row
// that guard protects. This builder does the thing the guard says a CopyAsset alone
// must never do: it copies AND THEN REPAIRS, and then PROVES the repair.
//
// MEASURED AT SOURCE 2026-08-22, not taken from the ticket (all five identical):
//   5 GameObjects, 4 ParticleSystems, 1 MeshFilter, 1 MeshRenderer,
//   1 SphereCollider, 0 MonoBehaviours.
//   The single GameObject WITHOUT a ParticleSystem is the ROOT
//   (m_Father: {fileID: 0}), carrying Transform + MeshFilter + MeshRenderer +
//   SphereCollider. The four particle layers hang underneath it.
//   Exactly ONE layer in each of the five is looping:1 with rateOverTime scalar 5 -
//   the "5/sec on loop" the ticket names. The others are burst-shaped
//   (rate 0, or rate 300/500/1000 on a NON-looping system).
//   ZERO MonoBehaviours anywhere, so unlike Misc/Respawn and Misc/Dissolve these
//   carry no pack script and no missing-script hazard on a clone.
//
// THE THREE REPAIRS, all correctness invariants (applied EVERY run, like
// playOnAwake in the batch builder - never "taste", so never opt-in):
//
//   1. STRIP THE ROOT DEMO GEOMETRY. The root holds no ParticleSystem, so removing
//      its MeshFilter/MeshRenderer/Collider cannot move the derivation authority or
//      change the measured family - it leaves a bare Transform with the four real
//      layers under it, which is the ordinary shape of every other recipe in this
//      tree. Colliders are stripped ANYWHERE in the copy, not just the root: a
//      physics collider riding a pooled impact burst is a bug waiting for a
//      rigidbody to find it.
//
//   2. FORCE ONE-SHOT. main.loop and main.prewarm cleared on EVERY layer. This is
//      the reason the owner's own isLoop:true in VfxManualPicks.json cannot simply
//      be honoured: a loop-flagged row played fire-and-forget NEVER returns its pool
//      slot (VFXManager.Hovl.cs registers no reclaim deadline on the loop branch; the
//      only loop reclaim frees DESTROYED hosts and pooled hosts are never destroyed),
//      so every melee connect would permanently burn one of the 20 global loop slots.
//      Six F8 captures have already caught that cap saturated at 20/20.
//      ⚠ THE FLAG IS NOT WRITTEN HERE. Clearing main.loop changes what the ART DOES;
//      the catalog's IsLoop is then DERIVED from the repaired prefab by the single
//      shared authority (VfxLoopFlagRegression.TryResolveExpected), which
//      HovlVfxCatalogGenerator already calls for every row including manual ones. So
//      no OwnerPinned entry is needed and the derivation is NOT widened - the two
//      things the vfx-loop-flag oracle's own header warns against. One row was pinned
//      on 2026-08-21 precisely because widening would re-open the leak on seven
//      sibling rows; this change touches neither the pin table nor the rule.
//
//   3. CLEAR playOnAwake on every layer. Same invariant the batch builder states:
//      a prewarmed pool instance would otherwise emit a stray burst at the world
//      origin the moment it is created.
//
// HOW THE OWNER'S VERBATIM TAG REACHES THE MIRROR (no second table, no re-pick):
//   VfxManualPicks.json holds her five PP_* rows pointing at the PACK path, because
//   that is where she browsed the art. HovlVfxCatalogGenerator sends every path -
//   Map row and manual row alike - through ResolveMirror -> VfxMirrorRedirect, which
//   swaps a gitignored pack path for a committed mirror when a BUILDER DECLARES the
//   pair. Declaring Mirrors below is therefore the whole wiring: her pick ships, and
//   nothing here chooses, substitutes or re-points a single effect.
//   (VfxMirrorRedirect reads the pairs off the builders themselves rather than
//   keeping its own copy - a second hand-copied table is how a tool and its consumer
//   come to disagree while both report success.)
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Rule = DeNelle.Editor.Regression.VfxResourceSelfContainmentRegression;
using SurfaceImpactMirrorSet = DeNelle.Editor.Regression.SurfaceImpactMirrorSet;

namespace DeNelle.Editor
{
    public static class SurfaceImpactVfxMirrors
    {
        private const string MarkerOk   = "SURFACE_IMPACT_VFX_MIRROR_OK";
        private const string MarkerFail = "SURFACE_IMPACT_VFX_MIRROR_FAIL";
        private const string Tag        = "[SurfaceImpactVfxMirrors] ";

        /// <summary>
        /// The owner's 2026-08-21 tags, source -> tracked mirror. Read by
        /// <see cref="VfxMirrorRedirect"/> so her VfxManualPicks rows resolve to these
        /// copies automatically.
        /// <para>
        /// ⚠ DECLARED IN <see cref="SurfaceImpactMirrorSet"/>, NOT HERE, and that is
        /// deliberate: this builder's own GATE (SurfaceImpactVfxRegression) lives in
        /// DeNelle.EditorRegression, which this assembly references ONE WAY. A table
        /// declared here would be invisible to the gate, so the gate would need a
        /// hand-copied second copy - the exact drift this project keeps paying for. The
        /// declaration therefore sits where both can reach it, the same inversion
        /// VfxLoopFlagRegression already uses for the shared loop derivation.
        /// </para>
        /// The catalog KEY for each is declared once more still, in
        /// <c>DeNelle.Village.HitSurfaceVfx</c>, next to the resolution that chooses it.
        /// </summary>
        public static (string src, string dst)[] Mirrors => SurfaceImpactMirrorSet.Pairs;

        /// <summary>Layer count every recipe in this set must carry - see the set.</summary>
        private const int RequiredLayers = SurfaceImpactMirrorSet.RequiredLayers;

        [MenuItem("Defenders/VFX/Mirror Surface Impact VFX")]
        public static void Run()
        {
            try
            {
                int repaired = 0;

                foreach (var (src, dst) in Mirrors)
                {
                    if (!File.Exists(Absolute(dst)))
                    {
                        if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null)
                            throw new Exception("source would not load (Particle Pack absent?): '" + src +
                                                "'. The pack is gitignored; import it and re-run.");

                        // Layer count is asserted on the SOURCE, before a byte is copied, so a
                        // trimmed pack fails here rather than after we have written a mirror.
                        var srcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(src);
                        int srcLayers = srcPrefab.GetComponentsInChildren<ParticleSystem>(true).Length;
                        if (srcLayers != RequiredLayers)
                            throw new Exception("RECIPE LAYER COUNT: '" + src + "' carries " + srcLayers +
                                                " ParticleSystem(s), this set REQUIRES exactly " + RequiredLayers +
                                                ". Pooled WHOLE, never trimmed - a short recipe still renders " +
                                                "something plausible, which is why this is asserted, not eyeballed.");

                        EnsureFolder(Path.GetDirectoryName(dst).Replace('\\', '/'));
                        if (!AssetDatabase.CopyAsset(src, dst))
                            throw new Exception("CopyAsset failed: '" + src + "' -> '" + dst + "'.");
                        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                        Debug.Log(Tag + "copied '" + src + "' -> '" + dst + "'.");
                    }
                    else
                    {
                        Debug.Log(Tag + "'" + dst + "' already on disk - adopted (repairs re-applied).");
                    }

                    if (Repair(dst)) repaired++;
                }

                AssetDatabase.Refresh();

                // The ONE sanctioned self-containment pass over the whole VFX tree - a
                // CopyAsset duplicates the PREFAB ONLY, so without this the mirrors keep
                // pointing their materials/textures at the gitignored pack and render
                // magenta on any machine that lacks it (WO-1100 / Casting_Fire class).
                VfxResourceArtMirror.Run();

                // A builder may have just created a mirror this session; drop the cache so
                // a catalog regenerate in the same batch picks the new pairs up.
                VfxMirrorRedirect.Invalidate();

                Verify();

                Debug.Log(MarkerOk + " " + Mirrors.Length + " clean - every surface-impact mirror is " +
                          "root-geometry-free, collider-free, one-shot on every layer, playOnAwake-clear, " +
                          "and resolves with zero gitignored-pack dependencies (" + repaired +
                          " needed repair this run).");
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "FAILED: " + e.Message);
                Debug.LogError(MarkerFail + " - " + e.Message);
            }
        }

        // -- The three repairs ---------------------------------------------------

        /// <summary>
        /// Apply the demo-geometry strip, the one-shot forcing and the playOnAwake clear to
        /// the mirror at <paramref name="assetPath"/>. Idempotent; saves only when something
        /// actually changed. Returns true when it wrote.
        /// </summary>
        private static bool Repair(string assetPath)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
            if (contents == null)
                throw new Exception("could not open mirror for repair: '" + assetPath + "'.");

            bool dirty = false;
            var notes = new List<string>();

            try
            {
                // 1. STRIP GEOMETRY. Mesh components on the ROOT only (the root is the demo
                //    target); colliders ANYWHERE (a pooled burst must never carry physics).
                var mf = contents.GetComponent<MeshFilter>();
                if (mf != null) { UnityEngine.Object.DestroyImmediate(mf, true); dirty = true; notes.Add("root MeshFilter"); }

                var mr = contents.GetComponent<MeshRenderer>();
                if (mr != null) { UnityEngine.Object.DestroyImmediate(mr, true); dirty = true; notes.Add("root MeshRenderer"); }

                var colliders = contents.GetComponentsInChildren<Collider>(true);
                foreach (var col in colliders)
                {
                    if (col == null) continue;
                    notes.Add(col.GetType().Name + " on '" + col.gameObject.name + "'");
                    UnityEngine.Object.DestroyImmediate(col, true);
                    dirty = true;
                }

                // 2 + 3. FORCE ONE-SHOT and CLEAR playOnAwake, on every layer.
                var systems = contents.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length != RequiredLayers)
                    throw new Exception("LAYER LOSS in '" + assetPath + "': " + systems.Length +
                                        " ParticleSystem(s) after repair, expected " + RequiredLayers +
                                        ". The strip removed a real particle layer - refuse rather than ship it.");

                foreach (var ps in systems)
                {
                    var main = ps.main;

                    // prewarm FIRST: Unity only permits prewarm on a looping system, so
                    // clearing loop while prewarm is set leaves an invalid combination.
                    if (main.prewarm) { main.prewarm = false; dirty = true; notes.Add("prewarm off '" + ps.name + "'"); }
                    if (main.loop)    { main.loop    = false; dirty = true; notes.Add("loop off '" + ps.name + "'"); }
                    if (main.playOnAwake)
                    {
                        main.playOnAwake = false;
                        dirty = true;
                        notes.Add("playOnAwake off '" + ps.name + "'");
                    }
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                    Debug.Log(Tag + "repaired '" + assetPath + "': " + string.Join(", ", notes.ToArray()) + ".");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            if (dirty) AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return dirty;
        }

        // -- Proof ---------------------------------------------------------------

        /// <summary>
        /// Re-read every mirror FROM DISK and prove all four properties. Deliberately a
        /// separate pass over freshly loaded assets rather than an assertion on the
        /// in-memory copy Repair just edited - a check that reads the object it wrote is a
        /// check that cannot fail.
        /// </summary>
        private static void Verify()
        {
            var bad = new List<string>();

            foreach (var (_, dst) in Mirrors)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dst);
                if (prefab == null) { bad.Add(dst + " (does not load)"); continue; }

                var faults = new List<string>();

                if (prefab.GetComponentsInChildren<MeshFilter>(true).Length > 0)   faults.Add("MeshFilter present");
                if (prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0) faults.Add("MeshRenderer present");
                int cols = prefab.GetComponentsInChildren<Collider>(true).Length;
                if (cols > 0) faults.Add(cols + " Collider(s) present");

                var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length != RequiredLayers)
                    faults.Add(systems.Length + " layers, expected " + RequiredLayers);
                foreach (var ps in systems)
                {
                    var main = ps.main;
                    if (main.loop)        faults.Add("'" + ps.name + "' still loops");
                    if (main.prewarm)     faults.Add("'" + ps.name + "' still prewarms");
                    if (main.playOnAwake) faults.Add("'" + ps.name + "' still plays on awake");
                }

                var offenders = Rule.PackDependenciesOf(dst);
                if (offenders.Count > 0)
                    faults.Add(offenders.Count + " gitignored pack dep(s): " +
                               string.Join(", ", offenders.ToArray()));

                if (faults.Count > 0)
                    bad.Add(dst + " -> " + string.Join("; ", faults.ToArray()));
            }

            if (bad.Count > 0)
                throw new Exception(bad.Count + " surface-impact mirror(s) failed verification: " +
                                    string.Join(" | ", bad.ToArray()));
        }

        // -- Plumbing ------------------------------------------------------------

        private static string Absolute(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }
}
