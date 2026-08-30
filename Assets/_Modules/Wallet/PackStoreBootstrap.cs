// =============================================================================
// PackStoreBootstrap — the runtime DOOR to the SKR Realm Store (PackStore).
// -----------------------------------------------------------------------------
// THE PROBLEM (Seekerthon demo blocker): PackStore.cs is fully built (SKR/SOL/USDC
// rails over the StubWalletProvider mock-connect) but NOTHING opened it in the live
// build — MarketplaceInteractor.OpenStore() had zero callers, the store was never
// registered with PanelRouter, and it only existed (commented out) in the abandoned
// Village.unity. So the store never appeared in MainCastle_Hall.
//
// THE FIX (host-free, like the other WO-F panels): this self-bootstrapping static
//   1. REGISTERS a PanelRouter opener for PanelId.RealmStore at boot, so ANY assembly
//      can open the store by id with no cross-asmdef reference (the merchant's
//      "Realm Store" dialogue option routes through DialogueCommandSink -> here).
//   2. FIND-OR-SPAWNS the PackStore on first open — no dependency on a scene-placed
//      instance or the dead Village.unity. PackStore auto-shows in its OnEnable
//      (the MarketplaceInteractor SetActive contract is preserved), so the opener
//      just SetActive(true)s a found-or-created host.
//   3. DEMO URL trigger — ?realmstore=1 (or the SKR skin ?skin=skr) on the WebGL
//      page auto-opens the store once the hub is up, mirroring how
//      CurrencySkinResolver reads Application.absoluteURL. This guarantees the store
//      is ONE URL away for the capture; it is demo-scoped (URL-gated), not always-on.
//
// DeNelle.Wallet -> DeNelle.Core only (PanelRouter/PanelId live in Core.UI); no
// reflection, no reference to DeNelle.Village. Every step is FlowTrace/Guard-instrumented.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Commerce;   // WO-1282 - StorefrontRegistry, the rail-neutral host handle

namespace DeNelle.Wallet
{
    /// <summary>Registers the PanelId.RealmStore opener and provides the demo URL door
    /// for the SKR pack store (<see cref="PackStore"/>). Pure static, self-bootstrapping.</summary>
    public static class PackStoreBootstrap
    {
        // One-shot latch so the ?realmstore=1 URL only auto-opens once per session.
        private static bool _urlOpenConsumed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOpener()
        {
            // Reflection-free cross-assembly door: the merchant dialogue verb + any
            // future entry point open the store via PanelRouter.Open(PanelId.RealmStore).
            PanelRouter.Register(PanelId.RealmStore, OpenRealmStore);
            FlowTrace.Step("Store", "PackStoreBootstrap: PanelId.RealmStore opener registered.");

            // WO-1282 - the second door, for callers that need the HOST rather than an open request.
            // MarketplaceInteractor (DeNelle.Village) used to reach it with
            // FindAnyObjectByType<PackStore>(FindObjectsInactive.Include); Village no longer
            // references DeNelle.Wallet, so the SAME search is installed here as a lazy resolver and
            // still runs at call time with inactive objects included. Registering a resolver rather
            // than an instance is load-bearing: the store host is DISABLED in the scene by design,
            // so it never runs Awake and could never push itself into a registry.
            StorefrontRegistry.RegisterResolver(ResolveStorefrontRoot);
        }

        /// <summary>
        /// The <see cref="StorefrontRegistry"/> resolver. Returns the PackStore host if one exists
        /// in the loaded scenes (inactive included), else null. Deliberately does NOT spawn one:
        /// a handle lookup must not have the side effect of creating a storefront - that is what
        /// <see cref="OpenRealmStore"/> is for.
        /// </summary>
        private static GameObject ResolveStorefrontRoot()
        {
            var store = UnityEngine.Object.FindAnyObjectByType<PackStore>(FindObjectsInactive.Include);
            return store != null ? store.gameObject : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookScenes()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryUrlAutoOpen(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryUrlAutoOpen(scene);

        /// <summary>
        /// Opens the Realm Store, find-or-spawning the <see cref="PackStore"/> host so it
        /// works with no scene-placed instance (host-free). PackStore's OnEnable builds the
        /// kit modal lazily and shows it, and registers with PanelManager so the arbiter
        /// sees a panel open. Idempotent — a re-open of a live store just re-shows it.
        /// </summary>
        public static void OpenRealmStore()
        {
            using var _ = FlowTrace.Enter("Store", "PackStoreBootstrap.OpenRealmStore");

            var store = UnityEngine.Object.FindAnyObjectByType<PackStore>(FindObjectsInactive.Include);
            if (store == null)
            {
                var go = new GameObject("RealmStore (PackStore)");
                var active = SceneManager.GetActiveScene();
                if (active.IsValid()) SceneManager.MoveGameObjectToScene(go, active);
                // AddComponent runs Awake (PanelManager.Register) then, because the GameObject
                // is active, OnEnable — which builds + shows the modal + NotifyOpened.
                store = go.AddComponent<PackStore>();
                FlowTrace.Step("Store", "PackStoreBootstrap: PackStore host spawned (host-free first open).");
                return;
            }

            // Existing (hidden) host — SetActive(true) fires OnEnable -> show.
            if (!store.gameObject.activeSelf)
            {
                store.gameObject.SetActive(true);
                FlowTrace.Step("Store", "PackStoreBootstrap: existing PackStore re-shown.");
            }
            else
            {
                // Already showing: OnEnable will not fire, so Render again to consume a
                // RequestFocusSku from Manage's Buy builder route (WO-1253).
                store.Render();
                FlowTrace.Step("Store", "PackStoreBootstrap: PackStore already open — re-rendered for pending focus.");
            }
        }

        // ── Demo URL trigger (?realmstore=1 / ?skin=skr) ─────────────────────────────
        private static void TryUrlAutoOpen(Scene scene)
        {
            if (_urlOpenConsumed) return;
            if (!scene.IsValid()) return;
            if (!UrlRequestsRealmStore()) return;
            if (FindHero() == null) return; // wait for a gameplay scene (skip Title / HeroSelect)

            _urlOpenConsumed = true;
            FlowTrace.Step("Store", "PackStoreBootstrap: demo URL requested the Realm Store — auto-opening.");
            OpenRealmStore();
        }

        /// <summary>
        /// True when the WebGL page URL carries <c>?realmstore=1</c> (explicit open) or the
        /// SKR skin (<c>?skin=skr</c>) — the demo lives on the SKR deployment, so the store is
        /// the headline there. Empty off-web (Application.absoluteURL is empty in editor/standalone),
        /// so this is inert outside a URL-driven WebGL boot. Never throws.
        /// </summary>
        private static bool UrlRequestsRealmStore()
        {
            try
            {
                string url = Application.absoluteURL;
                if (string.IsNullOrEmpty(url)) return false;
                int q = url.IndexOf('?');
                if (q < 0) return false;

                foreach (var pair in url.Substring(q + 1).Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    string key = (eq < 0 ? pair : pair.Substring(0, eq)).Trim().ToLowerInvariant();
                    string val = (eq < 0 ? "" : pair.Substring(eq + 1)).Trim().ToLowerInvariant();

                    if (key == "realmstore" && (val == "1" || val == "true" || val == "yes"))
                        return true;
                    if (key == "skin" && val == "skr")
                        return true;
                }
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Store", "PackStoreBootstrap: URL parse skipped — " + ex.Message);
            }
            return false;
        }

        // Hero presence gate — reused idiom (CosmeticShopPanelBootstrap.FindHero): reflect the
        // Village HeroLocomotion by name (Wallet must not reference DeNelle.Village).
        private static Component FindHero()
        {
            var t = Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (t == null) return null;
            return UnityEngine.Object.FindAnyObjectByType(t) as Component;
        }
    }
}
