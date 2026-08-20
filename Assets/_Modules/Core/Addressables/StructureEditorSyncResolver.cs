// =============================================================================
// StructureEditorSyncResolver — the ONE allowlisted WaitForCompletion site for
// structure art, and it exists only in the Editor.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core (Core/Addressables). WHOLE FILE inside #if UNITY_EDITOR.
//
// ⛔ WHY A SEPARATE FILE RATHER THAN AN #if BLOCK INSIDE StructureAssetLoader.
// The gate (Assets/Editor/Regression/StructureLoadBoundedRegression.cs) asserts a
// property that has to be simple enough to be true at a glance: StructureAssetLoader
// contains ZERO occurrences of WaitForCompletion, full stop. A conditional block
// inside it would make the rule "…except under an #if", which is the kind of rule
// that erodes — the next author adds a second #if and the gate keeps passing. One
// file, entirely editor-only, allowlisted by PATH, cannot erode that way.
//
// ⛔ WHY THIS IS SAFE HERE AND LETHAL AT RUNTIME.
// The deadlock proven in StructureContentWarmer's header needs three things: an
// AssetBundle provider, a UnityWebRequest, and a blocked player loop. The Editor has
// none of them — the default play-mode script resolves through the AssetDatabase
// provider, which returns without pumping the network or sleeping the main thread.
// And the batchmode gates (DataRegression, BuildEconomyRegression, WallBuildL1) run
// in EDIT MODE, where there is no player loop to warm from at all: they need a
// synchronous answer or they cannot check structure art exists.
//
// ⚠ It is still instrumented. If a project ever switches the editor to "Use Existing
// Build", this path CAN reach a bundle — so it times itself and says so loudly rather
// than becoming the same silent hole in the log that cost three minutes on device.
// =============================================================================

#if UNITY_EDITOR

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DeNelle.Core
{
    /// <summary>
    /// Editor-only synchronous Addressables resolve for structure art. Never compiled into a
    /// player — <see cref="StructureAssetLoader"/> serves runtime callers from
    /// <see cref="StructureContentWarmer"/> instead.
    /// </summary>
    internal static class StructureEditorSyncResolver
    {
        private const string Sys = StructureAssetLoader.System;

        /// <summary>Above this, the editor path is behaving like the runtime path used to and
        /// the author needs to know before it ships.</summary>
        private const double SlowMs = 250.0;

        /// <summary>
        /// Resolve <paramref name="address"/> synchronously via Addressables, or null.
        /// Never throws (every step is Guard-wrapped) and always releases its handles —
        /// the pre-fix loader leaked a location handle on EVERY call.
        /// </summary>
        internal static T Resolve<T>(string address) where T : Object
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            T result = null;

            bool registered = false;
            Guard.Try(Sys, $"editor probe '{address}' registration", () =>
            {
                var locHandle = Addressables.LoadResourceLocationsAsync(address, typeof(T));
                var locations = locHandle.WaitForCompletion();
                registered = locations != null && locations.Count > 0;
                // ⛔ THE LEAK THE PRE-FIX LOADER SHIPPED: this Release was missing, so every
                // single structure load leaked one location handle for the life of the process.
                Addressables.Release(locHandle);
            });

            if (registered)
            {
                Guard.Try(Sys, $"editor Addressables load {address} ({typeof(T).Name})", () =>
                {
                    var handle = Addressables.LoadAssetAsync<T>(address);
                    result = handle.WaitForCompletion();
                    // Handle intentionally NOT released: the editor caller keeps using the asset,
                    // and editor lifetime is a domain reload. Releasing here would hand back a
                    // destroyed object, which is a worse bug than an editor-only retain.
                });
            }

            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds > SlowMs)
            {
                FlowTrace.Warn(Sys,
                    $"EDITOR synchronous resolve of '{address}' took {sw.Elapsed.TotalMilliseconds:F0} ms. " +
                    "In the Editor this path uses the AssetDatabase provider and should be ~instant. " +
                    "A slow one means it reached a real bundle — the same shape that DEADLOCKED the " +
                    "device on 2026-08-20. Check the Addressables play-mode script before shipping.");
            }

            return result;
        }
    }
}

#endif
