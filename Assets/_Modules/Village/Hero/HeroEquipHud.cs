// =============================================================================
// HeroEquipHud — single compact "open inventory" icon button on the in-world HUD.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// One small, clearly-tappable bag/satchel icon button anchored bottom-right.
// Tapping it opens the full inventory modal (HeroInventoryController). Replaces
// the former 4-slot quick-equip cluster (Weapon/Armor/2 consumables) — the owner
// wanted a single compact entry point instead of the always-on slot strip.
// Code-built uGUI ONLY (same reliable path + helper recipe as ArenaPanel /
// HeroInventoryController — UXML doesn't render in builds, PIPELINE_STATE §8).
//
// Entry points mirror the controller idiom: EnsureExists() spawns a persistent
// host; it self-builds its overlay. ASCII/glyph-only runtime strings (sprite/
// text). WebGL-safe (rounded sprite fallback). No new equip system.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>Single compact bag-icon button on the HUD; tap opens the inventory modal.</summary>
    public sealed class HeroEquipHud : MonoBehaviour
    {
        public static HeroEquipHud Instance { get; private set; }

        private GameObject _ui;

        public static HeroEquipHud EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("HeroEquipHud");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<HeroEquipHud>();
            return Instance;
        }

        // Auto-spawn the inventory button in the gameplay hubs.
        // Mirrors the injector bootstrap idiom (NPCs/HUD).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (IsHubScene(scene.name)) EnsureExists();
            };
            if (IsHubScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
                EnsureExists();
        }

        // HUB GATE -> the ONE canonical list (DeNelle.Core.HubScenes), NOT a private copy.
        //
        // THE BUG THIS FIXES: this was `n == "MainCastle_Hall" || n == "Village2" ||
        // n == "CastleHub"`. The live home hub is `Main_Castle_Overworld` (CLAUDE.md sec.7), which
        // that list never named - so the equip HUD never self-installed on the scene the player
        // actually plays, and the bag button simply did not exist. A private hub list drifting
        // behind canon is precisely the failure HubScenes was created to end (WO-411 root cause A).
        //
        // DELIBERATE BEHAVIOUR CHANGE, ACCEPTED: HubScenes.IsHub matches by `==` OR `Contains`,
        // so it is WIDER than the `==` list it replaces - "CastleHub_MainKeep_Backup" now counts,
        // where it did not before. That is accepted here rather than tightened globally: IsHub has
        // ~40 callers across live lanes, and every other self-installing injector in the project
        // (StoryCompanionInjector, CraftingStationInjector, JewelerStationInjector, EchoWispInjector,
        // SylasStewardInjector, ...) already gates on exactly this predicate. The worst case of the
        // widening here is one extra bag ICON in a hypothetically-named hub variant - not a gameplay
        // gate - so consistency with every sibling injector beats a bespoke narrower predicate.
        private static bool IsHubScene(string n) => DeNelle.Core.HubScenes.IsHub(n);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_invEvent != null && _invListener != null) _invEvent.RemoveListener(_invListener);
            DetachStaticHook();
            if (_ui != null) Destroy(_ui);
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // WO-411: NO free-floating BAG panel anymore — the TOWN ACTIONS row's BAG drives this. Wire
            // the HUD's InventoryRequested (DeNelle.HUD; Village can't reference it → reflection) to
            // OpenInventory. Re-wire on each scene load (the HUD may not be up when we Start).
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // ROOT-CAUSE FIX (T-022 BAG dead): subscribe ONCE to the HUD's STATIC inventory
            // event. The per-instance InventoryRequested below is recreated every scene load
            // (the HUD is NOT DontDestroyOnLoad — VillageHudBootstrap re-spawns it), so the
            // reflection self-heal can bind to a dying HUD and the BAG fires into the void.
            // The static event is instance-independent: bind it once here (DontDestroyOnLoad
            // singleton) and every HUD instance's BAG reaches OpenInventory. Kept ALONGSIDE the
            // per-instance wire (belt-and-braces); OpenInventory guards against double-open.
            TryWireStaticHook();
            TryWireToHud();
        }

        // ── Instance-independent static bridge (survives HUD re-instancing) ──────
        private bool _staticWired;

        private void TryWireStaticHook()
        {
            if (_staticWired) return;
            if (_hudType == null) _hudType = ResolveHudType();
            if (_hudType == null) return;                 // HUD assembly not loaded yet — retry next frame
            var evt = _hudType.GetEvent("InventoryRequestedStatic",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (evt == null) return;                      // older HUD without the static hook — fall back to instance wire
            var handler = System.Delegate.CreateDelegate(
                evt.EventHandlerType, this,
                typeof(HeroEquipHud).GetMethod(nameof(OpenInventory),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            evt.AddEventHandler(null, handler);
            _staticWired = true;
            Debug.Log("[HeroEquipHud] static InventoryRequested hook attached (instance-independent).");
        }

        private void DetachStaticHook()
        {
            if (!_staticWired || _hudType == null) return;
            var evt = _hudType.GetEvent("InventoryRequestedStatic",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (evt == null) return;
            var handler = System.Delegate.CreateDelegate(
                evt.EventHandlerType, this,
                typeof(HeroEquipHud).GetMethod(nameof(OpenInventory),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            evt.RemoveEventHandler(null, handler);
            _staticWired = false;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            // A scene just (re)loaded — the HUD it carries is a fresh instance, so the
            // event we previously bound to is stale. Force a re-resolve + re-wire.
            _wired = false;
            TryWireStaticHook();
            TryWireToHud();
        }

        // ROOT-CAUSE FIX (BAG dead in MainCastle_Hall): the old wiring fired ONCE at
        // Start + on sceneLoaded. In the castle hub the HUD is spawned by a sibling
        // [RuntimeInitializeOnLoadMethod] bootstrap (VillageHudBootstrap) in the SAME
        // scene-load — so when our Start ran the VillageHudController could not yet be
        // found (or had not built InventoryRequested), the one-shot wire silently
        // no-op'd, and nothing ever re-attempted → BAG raised InventoryRequested into
        // the void. We now retry every frame until the wire lands (then stop), the
        // same self-healing idiom BuildButtonBridge/TalkHudBridge rely on.
        private bool _wired;

        private void Update()
        {
            if (!_staticWired) TryWireStaticHook();
            if (_wired) return;
            TryWireToHud();
        }

        private UnityEngine.Events.UnityAction _invListener;
        private UnityEngine.Events.UnityEvent _invEvent;
        private static System.Type _hudType;

        private void TryWireToHud()
        {
            if (_hudType == null) _hudType = ResolveHudType();
            if (_hudType == null) return;                 // HUD assembly not loaded yet — retry next frame
            var hud = FindAnyObjectByType(_hudType) as Component;
            if (hud == null) return;                      // HUD not spawned yet — retry next frame
            var field = _hudType.GetField("InventoryRequested",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var evt = field != null ? field.GetValue(hud) as UnityEngine.Events.UnityEvent : null;
            if (evt == null) return;                      // event not constructed yet — retry next frame
            // Already bound to THIS scene's HUD event → done; stop the per-frame retry.
            if (ReferenceEquals(evt, _invEvent)) { _wired = true; return; }
            // New (or first) event instance — detach the stale listener, bind the new one.
            if (_invEvent != null && _invListener != null) _invEvent.RemoveListener(_invListener);
            _invListener = OpenInventory;
            evt.AddListener(_invListener);
            _invEvent = evt;
            _wired = true;                                // wired — Update() goes quiet until the next scene load
            Debug.Log("[HeroEquipHud] per-instance InventoryRequested listener attached to live HUD.");
        }

        private static System.Type ResolveHudType()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("DeNelle.HUD.VillageHudController", false);
                if (t != null) return t;
            }
            return null;
        }

        // ====================================================================
        // BUILD — one compact bag-icon button, bottom-right.
        // ====================================================================
        private void BuildRoot()
        {
            if (_ui != null) return;
            _ui = new GameObject("HeroEquipHudUI");
            _ui.transform.SetParent(transform, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;                  // under modals (Arena 1100, Inventory 2600)

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            // Single framed, gold-accent square button anchored bottom-right —
            // sits where the old 4-slot cluster lived (~0.84..0.995 x, lower band).
            // Routed through the shared presentation kit (dark glass + gold rune)
            // so it reads as the same designed game as the town HUD / inventory.
            var frame = ElarionUiKit.Panel(_ui.transform,
                                           new Vector2(0.855f, 0.305f), new Vector2(0.985f, 0.405f),
                                           deep: false, innerRim: false);

            // The BAG button: a kit Gold CTA carrying the chest/bag icon from the RPG
            // pack (IconInventory) instead of the old emoji glyph. The icon is dropped
            // over the button as a decorative, non-raycast overlay so the whole tile is
            // one tap target. A small "BAG" caption keeps the affordance legible when
            // the pack art is absent.
            var btn = ElarionUiKit.Button(frame.transform, "", ElarionUiKit.ButtonKind.Gold,
                                          new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.94f),
                                          OpenInventory);

            var chest = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconInventory);
            var iconGo = ElarionUiKit.AddImage(btn.transform, "BagIcon",
                                               new Vector2(0.18f, 0.05f), new Vector2(0.82f, 0.95f),
                                               Color.white, rounded: false);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            if (chest != null)
            {
                iconImg.sprite = chest;
                iconImg.type = Image.Type.Simple;
                iconImg.preserveAspect = true;
            }
            else
            {
                // Pack not imported — keep the affordance via a clear text glyph.
                iconImg.color = new Color(0f, 0f, 0f, 0f);
                ElarionUiKit.Label(iconGo.transform, "[ ]", 0f, 1f, ElarionUi.Ink,
                                   ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center,
                                   0f, 1f, bold: true);
            }

            ElarionUiKit.Label(btn.transform,
                HudStrings.HeroFaceLabel(HudStrings.KeyHeroBag, "button"),
                0.02f, 0.26f, ElarionUi.Ink,
                               ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center,
                               0.02f, 0.98f, spacing: 2f);
        }

        // Both the static bridge and the per-instance UnityEvent fire on a single BAG tap
        // (belt-and-braces). Debounce to one Open() per frame so the two paths don't
        // double-open / fight the panel.
        private int _lastOpenFrame = -1;

        private void OpenInventory()
        {
            if (Time.frameCount == _lastOpenFrame) return;   // already handled this tap this frame
            _lastOpenFrame = Time.frameCount;
            Debug.Log("[HeroEquipHud] OpenInventory entered — opening inventory modal.");
            try { HeroInventoryController.EnsureExists().Open(); }
            catch (System.Exception e) { Debug.LogError("[HeroEquipHud] open inventory failed: " + e); }
        }
    }
}
