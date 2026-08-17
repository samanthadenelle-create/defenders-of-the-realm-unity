// =============================================================================
// StructureAssetLoader — Tier-1 Addressables seam for structure art.
// Sibling of HeroAssetLoader (WO-545) / EnemyAssetLoader / VfxAssetLoader /
// AudioAssetLoader; identical contract, structure address space.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (owner, 2026-08-17): the APK went 570.9 -> 603.6 MB the moment
// the owner-purchased buildings landed — "its the new buildings" / "from 100k to
// 3mb". Assets/Resources/Structures is 62.5 MB and Unity FORCE-INCLUDES every
// byte under a Resources/ folder in EVERY build, whether the player ever builds
// that structure or not. Owner ruling on the fix: "do both everything else is
// addressable so its how we designed it" — this folder is the outlier, not the
// precedent.
//
// ⛔ THE SEAM LANDS BEFORE THE ASSETS MOVE. That ordering is the whole point and
// is not negotiable: every call site can be pointed here while the art still
// lives in Resources (the fallback keeps it working, byte-identical behaviour),
// and only then do the assets physically move. Move the assets first and every
// unconverted call site returns null — an invisible town, in a live build.
//
// CONTRACT (V1-SAFE, NON-NEGOTIABLE): Addressables-FIRST, Resources-FALLBACK.
//   • The address is the extension-less, Resources-relative key used VERBATIM as
//     BOTH the Addressable address AND the Resources.Load key — e.g.
//     "Structures/Ballista_L1", "Structures/Forge", "Structures/armorer".
//     These are the exact strings already authored in structures-catalog.json as
//     repo.visualPrefabPath / repo.upgradeVisualPath, so NOTHING in the catalog
//     changes. Do NOT invent a second address scheme; the grouper registers these
//     same strings (same rule as Hero/Enemy/Vfx/Audio).
//   • A miss on BOTH paths returns null and is reported ONCE per key. Callers
//     already treat null as "no art" (StructureFactory logs a LogWarning per
//     CLAUDE.md §4), so a null return is never a crash — but it IS a defect for a
//     structure, unlike the audio seam where a miss can be by design. Structures
//     have no synth fallback: a missing prefab is an invisible building.
//
// ⚠ THE MIGRATION HAS A TRAP, RECORDED HERE BECAUSE IT HAS ALREADY BEEN HIT ONCE
// TODAY IN ANOTHER FOLDER: CatalogPrefabImporter COPIES pack prefabs INTO
// Assets/Resources/Structures. Move the folder to Addressables and leave that
// importer aimed at Resources, and its next run silently re-inflates the build —
// exactly the BlinkOrcImporter trap fixed this morning (its StageDir const was
// re-populating Resources/Enemies after that migration). The importer's
// destination MUST be repointed in the SAME change as the asset move.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeNelle.Core
{
    /// <summary>
    /// Addressables-first, Resources-fallback loader for structure art
    /// (<c>repo.visualPrefabPath</c> / <c>repo.upgradeVisualPath</c>).
    /// </summary>
    public static class StructureAssetLoader
    {
        /// <summary>FlowTrace system tag for every line this seam emits.</summary>
        public const string System = "StructureAssets";

        /// <summary>Address/Resources prefix every structure key carries.</summary>
        public const string StructureAddrPrefix = "Structures/";

        /// <summary>Keys whose both-paths-missed failure has already been reported (once per key).</summary>
        private static readonly HashSet<string> s_reportedMisses = new HashSet<string>();

        /// <summary>
        /// Load a structure's visual prefab by its catalog key — the value authored in
        /// <c>repo.visualPrefabPath</c> / <c>repo.upgradeVisualPath</c>, e.g.
        /// "Structures/Ballista_L1". Null when both paths miss (caller logs + renders nothing).
        /// </summary>
        public static GameObject LoadStructurePrefab(string key) => Load<GameObject>(key);

        /// <summary>
        /// Load any structure-adjacent asset by the same key rule — e.g. the tier textures
        /// StructureFactory resolves for <c>upgradeTexturePath</c>.
        /// </summary>
        public static T LoadStructureAsset<T>(string key) where T : Object => Load<T>(key);

        // ---------------------------------------------------------------------

        private static T Load<T>(string address) where T : Object
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            T result = null;

            // ---- Addressables first -----------------------------------------
            // Guarded: a catalog that is absent (editor, pre-migration) or malformed must degrade
            // to Resources, never throw into the caller. Guard.Try reports via FlowTrace so a
            // broken catalog is visible instead of silently costing every load its fast path.
            bool wasRegistered = false;
            Guard.Try(System, $"probe '{address}' registration", () =>
            {
                wasRegistered = AddressableRegistered<T>(address);
            });

            if (wasRegistered)
            {
                Guard.Try(System, $"Addressables load {address} ({typeof(T).Name})", () =>
                {
                    var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(address);
                    result = handle.WaitForCompletion();
                });

                if (result != null)
                {
                    FlowTrace.Once(System, "addr-hit-" + address,
                        $"'{address}' resolved from ADDRESSABLES (out of the force-included Resources payload).");
                    return result;
                }

                FlowTrace.Warn(System,
                    $"Addressables '{address}' is registered but resolved null — falling back to " +
                    $"Resources.Load(\"{address}\").");
            }
            else
            {
                FlowTrace.Step(System,
                    $"no Addressables entry for '{address}' (expected pre-migration) — using Resources.Load(\"{address}\").");
            }

            // ---- Resources fallback ------------------------------------------
            Guard.Try(System, $"Resources.Load {address} ({typeof(T).Name})", () =>
            {
                result = Resources.Load<T>(address);
            });

            if (result == null && s_reportedMisses.Add(address))
            {
                // ⛔ NO SYNTH FALLBACK EXISTS FOR A BUILDING. Unlike AudioAssetLoader — where a miss
                // can be a designed state — a structure that resolves nothing is an INVISIBLE
                // BUILDING the player has paid resources for. Always error-level, always once per key.
                FlowTrace.Fail(System,
                    $"structure asset '{address}' ({typeof(T).Name}) not found via Addressables OR Resources — " +
                    "the structure will render NOTHING. Check repo.visualPrefabPath against the assets on " +
                    "disk, and (post-migration) that the grouper registered this exact address.");
            }

            return result;
        }

        /// <summary>
        /// True when the Addressables catalog has at least one location for <paramref name="address"/>
        /// providing type <typeparamref name="T"/>. Type-filtered so a prefab and a texture sharing an
        /// address resolve apart. Silent on the common pre-migration miss — that is the expected state
        /// until the grouper runs, and warning on it would bury the real failures.
        /// </summary>
        private static bool AddressableRegistered<T>(string address) where T : Object
        {
            var locHandle = UnityEngine.AddressableAssets.Addressables
                .LoadResourceLocationsAsync(address, typeof(T));
            var locations = locHandle.WaitForCompletion();
            return locations != null && locations.Count > 0;
        }
    }
}
