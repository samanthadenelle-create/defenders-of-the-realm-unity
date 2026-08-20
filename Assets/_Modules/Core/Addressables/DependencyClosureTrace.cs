// =============================================================================
// DependencyClosureTrace — step-in / step-out tracing for a loaded asset's
// DEPENDENCIES, shared by every *AssetLoader seam.
// -----------------------------------------------------------------------------
// OWNER DESIGN (2026-08-17): "then we track its dependency with step in step out",
// with the phone as the instrument — "with the raw data feed from the phone you
// can do this in a single session". This is CLAUDE.md §12 applied to asset
// resolution: Enter the thing, Step each part, Fail the part that missed, Exit
// with a count.
//
// ⛔ WHY THE DEPENDENCIES AND NOT JUST THE ASSET. A loader that reports
// "loaded Structures/Forge OK" is answering the easy question. The building still
// renders grey, untextured or magenta when a MATERIAL or a TEXTURE one level down
// failed — and today proved that exact gap twice: a Tripo FBX whose material
// carried a NULL albedo while the prefab loaded fine, and a navmesh bake that
// reported success having written nothing. A top-level "OK" is compatible with a
// broken asset. The closure is where the truth is.
//
// ⛔ AND IT MUST WRAP BOTH BRANCHES. The seams are Addressables-FIRST,
// Resources-FALLBACK. A half-migrated slice shows up as a silent fallback: the
// address is wrong, Resources still answers, the game looks fine and the bytes
// ship TWICE. So the fallback path is traced too, and a fallback on an asset that
// was supposed to have moved is reported as an anomaly, not a success.
//
// Tag is [Flow:<system>] so it is greppable straight off the device:
//   adb logcat | grep "\[Flow:StructureAssets\]"
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>Traces the dependency closure of a resolved asset (materials + their textures).</summary>
    public static class DependencyClosureTrace
    {
        /// <summary>
        /// Walks a loaded prefab's renderers, materials and albedo slots, emitting one Step per
        /// resolved dependency and a Fail per missing one. Returns true when the closure is whole.
        /// <para>
        /// Cheap by construction: it reads already-loaded references and allocates nothing beyond a
        /// small set. It is safe to call on every resolve — and it MUST be, because the failure this
        /// catches is intermittent (one building, one material) and a sampling trace would miss it.
        /// </para>
        /// </summary>
        /// <param name="system">FlowTrace system tag — the caller's, so the line greps with its seam.</param>
        /// <param name="address">The address/key that was resolved, for the log line.</param>
        /// <param name="asset">The loaded object. Null is reported by the CALLER, not here.</param>
        /// <param name="viaFallback">
        /// True when this came from the Resources fallback rather than Addressables. For a MIGRATED
        /// asset that is a defect (wrong address, bytes double-shipping), so it is surfaced.
        /// </param>
        public static bool Verify(string system, string address, Object asset, bool viaFallback)
        {
            if (asset == null) return false;

            var go = asset as GameObject;
            if (go == null)
            {
                // Non-prefab (texture, controller, ScriptableObject): nothing to walk. Still worth a
                // line so the trace shows the resolve happened at all.
                FlowTrace.Step(system, $"resolve '{address}' -> {asset.GetType().Name} " +
                                       $"({(viaFallback ? "Resources FALLBACK" : "Addressables")}), no closure to walk.");
                return true;
            }

            // STEP IN. FlowTrace has no Enter/Exit pair — Measure IS the scoped in/out primitive
            // (CLAUDE.md's "Enter/Step/Warn/Fail" is shorthand for the discipline, not the API), and
            // it adds the elapsed time for free, which turns a slow Addressables resolve into data
            // rather than a hunch.
            using var scope = FlowTrace.Measure(system,
                $"resolve '{address}' ({(viaFallback ? "Resources FALLBACK" : "Addressables")})");

            int ok = 0, missing = 0;
            var seen = new HashSet<int>();

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                FlowTrace.Step(system, $"'{address}' has NO renderers — nothing to verify (marker or logic-only prefab). deps 0/0");
                return true;
            }

            foreach (var r in renderers)
            {
                if (r == null) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null)
                    {
                        missing++;
                        // A NULL material slot draws engine-default MAGENTA. Naming the renderer is
                        // what turns "something is pink" into a one-line fix.
                        FlowTrace.Fail(system, $"dep MISS on '{address}': renderer '{r.name}' has a NULL material slot " +
                                               "— that renderer draws engine-default MAGENTA in game.");
                        continue;
                    }
                    if (!seen.Add(mat.GetInstanceID())) continue;

                    Texture albedo = null;
                    if (mat.HasProperty("_BaseMap")) albedo = mat.GetTexture("_BaseMap");
                    if (albedo == null && mat.HasProperty("_MainTex")) albedo = mat.GetTexture("_MainTex");

                    // A null albedo is only a defect when the material is ALSO untinted — the
                    // Polyperfect flat-colour materials legitimately carry no map. That distinction
                    // was learned the hard way today: the first version of this check reported 21
                    // working prefabs as broken, and an oracle that cries wolf gets ignored on the
                    // day it is right.
                    Color tint = Color.white;
                    if (mat.HasProperty("_BaseColor")) tint = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color")) tint = mat.GetColor("_Color");
                    bool tinted = Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b)) < 0.92f;

                    if (albedo != null || tinted) { ok++; }
                    else
                    {
                        missing++;
                        FlowTrace.Fail(system, $"dep MISS on '{address}': material '{mat.name}' has NO albedo and NO tint " +
                                               "— renders as an untextured grey blob.");
                    }
                }
            }

            if (viaFallback)
            {
                // ⛔ THIS BRANCH USED TO CRY WOLF, AND IT COST A TRIAGE CYCLE ON 2026-08-20.
                // It Warned on EVERY fallback with the words "its bytes are shipping TWICE",
                // hedged only by an "if this asset has been migrated" the reader cannot evaluate.
                // The device log then carried four of them — Harvest/crystals, /food, /iron, /wood
                // — which read as a shipping defect and are nothing of the kind: there is NO
                // Harvest/* key in Addressables (verified against
                // Assets/AddressableAssetsData/AssetGroups/*.asset: the only prefixes registered
                // are gear/, Enemies/, Structures/, hero, dungeon and the localization tables), and
                // those four FBXs are ~33-156 KB each, deliberately left in Resources for WebGL
                // (HarvestSite.cs:274). Nothing is duplicated. An oracle that cries wolf gets
                // ignored on the day it is right — the same lesson the albedo check above learned.
                //
                // So ASK, do not guess. A fallback is a double-ship ONLY when the address is also
                // registered with Addressables; otherwise Resources is the only place it lives.
                if (StructureContentWarmer.IsRegisteredAddress(address))
                {
                    FlowTrace.Fail(system, $"'{address}' is REGISTERED with Addressables but resolved from the " +
                                           "RESOURCES FALLBACK. Its bytes are shipping TWICE — once in the bundle and " +
                                           "once in the force-included Resources payload — and the Addressables copy " +
                                           "is the one nobody is reading. Delete the Resources copy or fix the address.");
                }
                else
                {
                    FlowTrace.Step(system, $"'{address}' resolved from Resources, which is the ONLY place it lives " +
                                           "(no Addressables key for it). Not a double-ship, not a migration gap — " +
                                           "content that was deliberately never moved.");
                }
            }

            // STEP OUT. The count is the whole point: "deps 7/7 ok" is a pass you can grep for on
            // the device, and "deps 6/7" names a partial closure that a top-level "loaded OK" hides.
            FlowTrace.Step(system, $"resolve '{address}' deps {ok}/{ok + missing} ok");
            return missing == 0;
        }
    }
}
