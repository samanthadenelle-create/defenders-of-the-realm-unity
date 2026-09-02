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
// =============================================================================
// ⛔⛔ 2026-08-20 — P0 HANG. THIS FILE USED TO DEADLOCK THE GAME. READ THIS BEFORE
// YOU TOUCH THE RESOLVE ORDER.
//
// Captured on a Seeker device, returning from a dungeon to the town hub:
//   08-20 10:25:14.917 [Flow:VisualFactory] -> Skin('Structures/barracks')
//       DeNelle.Village.VisualFactory:Skin(Transform, String, SkinOptions)
//       DeNelle.Village.HubStructureVisualInjector:SkinStorefront(Swap, Transform)
//       DeNelle.Village.HubStructureVisualInjector:TrySwap(Swap)
//       DeNelle.Village.HubStructureVisualInjector:ApplyAll()
//       DeNelle.Village.HubStructureVisualInjector:OnSceneLoaded(Scene, LoadSceneMode)
//   ...and then NOTHING, ever again. The device clock reached 10:28:35 while the
//   game's last log line was still 10:25:14.917 — three minutes of total silence
//   from a process that was alive and foregrounded, showing a stale frame.
//
// IT WAS NOT THE NETWORK. The owner tested the device while it was hung: Wi-Fi
// associated, ping to the R2 CDN 2/2 packets, 0% loss, 31.5 ms. The CDN was healthy
// while the main thread was dead. A TIMEOUT WOULD NOT HAVE HELPED — and Addressables
// 2.9.1 does not offer one anyway: AsyncOperationBase.WaitForCompletion is literally
//     while (!InvokeWaitForCompletion()) { }
// with no timeout, no yield and no exit. The full three-part proof (including the
// provider paths that return false forever and Thread.Sleep the main thread) is in
// the header of StructureContentWarmer.cs, cited to file and line.
//
// THE MECHANISM: this file called WaitForCompletion() from inside a
// SceneManager.sceneLoaded ENGINE CALLBACK. Addressables operations are driven by the
// ResourceManager, which is pumped from the player loop. Blocking inside a nested
// engine callback means the thread that would drive the operation to completion is
// the thread waiting for it. Classic deadlock. It was intermittent because content
// ALREADY RESIDENT in memory returns without needing to pump — and the owner's
// constant town -> dungeon -> town loop is exactly what evicts it.
//
// TWO THINGS MADE IT UNSURVIVABLE RATHER THAN MERELY SLOW, and both are now fixed:
//   1. Every guard here is EXCEPTION-shaped. Guard.Try handles a throw perfectly and
//      does nothing whatsoever for a HANG. A stalled operation is not an exception;
//      it is silence. Guards are still here — they are just not the safety net.
//   2. There is NO Resources fallback tier for structures any more: the CDN migration
//      deleted Assets/Resources/Structures. So Addressables was the ONLY tier, and
//      when it stalled there was nothing to fall back to. The residency cache is now
//      that tier.
//
// THE RULE THIS FILE NOW KEEPS, and the gate that enforces it:
//   ⛔ ZERO occurrences of WaitForCompletion in this file. Not one, not under an #if.
//   Assets/Editor/Regression/StructureLoadBoundedRegression.cs fails the build if a
//   single one reappears, and it blanks comments AND string literals first so it
//   cannot match its own tombstone. The one allowlisted site in the whole seam is
//   StructureEditorSyncResolver.cs, which is entirely inside #if UNITY_EDITOR.
// =============================================================================
//
// CONTRACT (V1-SAFE, NON-NEGOTIABLE): Addressables-FIRST, Resources-FALLBACK.
//   • The address is the extension-less, Resources-relative key used VERBATIM as
//     BOTH the Addressable address AND the Resources.Load key — e.g.
//     "Structures/Ballista_L1", "Structures/Forge", "Structures/armorer".
//     These are the exact strings already authored in structures-catalog.json as
//     repo.visualPrefabPath / repo.upgradeVisualPath, so NOTHING in the catalog
//     changes. Do NOT invent a second address scheme; the grouper registers these
//     same strings (same rule as Hero/Enemy/Vfx/Audio).
//   • "Addressables-first" is now served by the RESIDENT CACHE
//     (StructureContentWarmer.TryGet) rather than by a synchronous load. Same
//     precedence, same addresses — the difference is that a miss now costs a frame
//     of wrong-looking building instead of the whole game.
//   • A miss on ALL paths returns null and is reported. Callers already treat null as
//     "no art" (StructureFactory logs a LogWarning per CLAUDE.md §4), so a null return
//     is never a crash — and HubStructureVisualInjector.SkinStorefront specifically
//     RESTORES THE BAKED TWIN on null, which is the graceful degradation this fix
//     depends on. A slightly wrong-looking building beats a dead game.
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
    /// Resident-first, Resources-fallback loader for structure art
    /// (<c>repo.visualPrefabPath</c> / <c>repo.upgradeVisualPath</c>).
    /// <para>⛔ EVERY PATH IN HERE IS NON-BLOCKING BY CONSTRUCTION. See the file header.</para>
    /// </summary>
    public static class StructureAssetLoader
    {
        /// <summary>FlowTrace system tag for every line this seam emits.</summary>
        public const string System = "StructureAssets";

        /// <summary>Address/Resources prefix every structure key carries.</summary>
        public const string StructureAddrPrefix = "Structures/";

        /// <summary>Keys whose all-paths-missed failure has already been escalated (once per key).</summary>
        private static readonly HashSet<string> s_reportedMisses = new HashSet<string>();

        /// <summary>
        /// Load a structure's visual prefab by its catalog key — the value authored in
        /// <c>repo.visualPrefabPath</c> / <c>repo.upgradeVisualPath</c>, e.g.
        /// "Structures/Ballista_L1". Null when nothing is resolvable RIGHT NOW (caller logs +
        /// keeps whatever it already had). Never blocks.
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

            // ---- 1. RESIDENT CACHE (this is the Addressables tier now) -------
            // A dictionary probe. It cannot download, cannot pump, cannot sleep and cannot
            // deadlock — which is the entire difference between this file and the one that
            // hung the game for three minutes on 2026-08-20.
            if (StructureContentWarmer.TryGet(address, out result) && result != null)
            {
                FlowTrace.Once(System, "addr-hit-" + address,
                    $"'{address}' served RESIDENT from the structure warm cache " +
                    "(out of the force-included Resources payload, and off the blocking path).");
                DependencyClosureTrace.Verify(System, address, result, viaFallback: false);
                return result;
            }

#if UNITY_EDITOR
            // ---- 2. EDITOR-ONLY synchronous Addressables ---------------------
            // Kept AHEAD of Resources so editor precedence matches the original
            // Addressables-first contract byte for byte. Safe only because the Editor uses the
            // AssetDatabase provider — no bundle, no UnityWebRequest, no player loop to starve.
            // See StructureEditorSyncResolver.cs; it is the ONE allowlisted blocking site.
            result = StructureEditorSyncResolver.Resolve<T>(address);
            if (result != null)
            {
                DependencyClosureTrace.Verify(System, address, result, viaFallback: false);
                return result;
            }
#endif

            // ---- 3. Resources fallback ---------------------------------------
            // Instant and local; kept for pre-migration content and for anything that never
            // left Resources. Note Assets/Resources/Structures itself is GONE, so in a shipped
            // build this tier answers nothing for structures — it is not the safety net any
            // more, the residency cache is.
            Guard.Try(System, $"Resources.Load {address} ({typeof(T).Name})", () =>
            {
                result = Resources.Load<T>(address);
            });

            // Trace the FALLBACK closure too. This is the branch that matters most during a
            // migration: a moved asset that still answers from Resources means the address is wrong
            // and the bytes are shipping twice — invisible in game, and invisible to every gate
            // except this line.
            if (result != null)
            {
                DependencyClosureTrace.Verify(System, address, result, viaFallback: true);
                return result;
            }

            // ---- 4. NOT RESIDENT. SKIP THIS FRAME — DO NOT WAIT. -------------
            // Ask for it asynchronously (idempotent; the warmer dedupes) and return null. The
            // caller degrades: HubStructureVisualInjector.SkinStorefront re-enables the baked
            // renderers and keeps the baked twin, and the injector re-applies once the warmer
            // settles. A slightly wrong-looking building beats a dead game.
            //
            // ⛔ THE REPORTING IS THE POINT. Three minutes were lost to unexplained silence
            // because nothing announced the wait. Every skip names the address AND how long it
            // has been waiting, and escalates to Fail once it stops looking transient.
            bool knownAbsent = StructureContentWarmer.IsKnownAbsent(address);
            float waited = StructureContentWarmer.SecondsWaiting(address);

            if (!knownAbsent) StructureContentWarmer.Request(address);

            if (knownAbsent || waited > StructureContentWarmer.MissEscalateSeconds)
            {
                if (s_reportedMisses.Add(address))
                {
                    // ⛔ NO SYNTH FALLBACK EXISTS FOR A BUILDING. Unlike AudioAssetLoader — where a miss
                    // can be a designed state — a structure that resolves nothing is an INVISIBLE
                    // BUILDING the player has paid resources for. Always error-level, always once per key.
                    FlowTrace.Fail(System,
                        $"structure asset '{address}' ({typeof(T).Name}) still unresolved after " +
                        $"{waited:F1}s via the resident cache OR Resources — the caller is keeping its " +
                        $"baked/previous visual (warmerState={StructureContentWarmer.State}, " +
                        $"knownAbsent={knownAbsent}, resident={StructureContentWarmer.ResidentCount}, " +
                        $"pending={StructureContentWarmer.PendingRequests}). Check repo.visualPrefabPath " +
                        "against the assets on disk, and that the grouper registered this exact address. " +
                        // PROD-022 Lane B: state the CAUSE here, not just the effect. This line used to
                        // list only our own bookkeeping, so a reader had to correlate it by hand with a
                        // warmer line that might be hundreds of lines earlier — or, on Pi Browser, might
                        // have been in the previous (killed) session entirely.
                        $"UNDERLYING FETCH CAUSE: {StructureContentWarmer.LastFailureCause(address) ?? "none recorded — no async fetch has failed for this address"}. " +
                        $"attempts={StructureContentWarmer.AttemptsFor(address)}/{StructureContentWarmer.MaxRequestAttempts}, " +
                        $"lastTransportUrl={StructureContentWarmer.LastRequestUrl ?? "(none)"}. " +
                        "NOTE: this is a VISUAL defect. The game did not stall — that is deliberate.");
                }
            }
            else
            {
                FlowTrace.Throttle(System, "skip-" + address, 1f,
                    $"'{address}' ({typeof(T).Name}) is not resident yet — SKIPPING this frame after " +
                    $"{waited:F1}s and requesting it asynchronously (warmerState={StructureContentWarmer.State}, " +
                    $"pending={StructureContentWarmer.PendingRequests}). The caller keeps its current visual. " +
                    "This deliberately does NOT wait: waiting here is what deadlocked the game on 2026-08-20.");
            }

            return null;
        }
    }
}
