// =============================================================================
// MarketplaceInteractor — walk-up proximity trigger that opens the PackStore.
// -----------------------------------------------------------------------------
// Attach to the "Marketplace" GameObject placed in the village scene.
// Mirrors DungeonPortal's interaction pattern exactly:
//   • Throttled hero proximity check (0.15 s interval).
//   • "[F] The Realm Store" prompt bubble appears when in range.
//   • F-key opens the store; Escape or the injected Close button dismisses it.
//   • HeroLocomotion is disabled while the store is open so the hero can't
//     wander during browsing — re-enabled on close.
//
// Inspector setup (after placing this component on the Marketplace GameObject):
//   1. Create an empty child GameObject named "PackStoreUI".
//   2. Add UIDocument to it; assign PackStore.uxml as the Source Asset.
//   3. Add PackStore.cs to it.
//   4. Disable the "PackStoreUI" GameObject in the scene (store starts hidden).
//   5. Drag "PackStoreUI" into the Store Ui Root field on this component.
//
// If Store Ui Root is left blank the component will try to find PackStore in
// the scene automatically (fine for prototyping).
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class MarketplaceInteractor : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Store panel (assign the PackStoreUI GameObject)")]
        [Tooltip("The GameObject that carries UIDocument + PackStore. " +
                 "Disabled by default in scene; this component enables/disables it.")]
        [SerializeField] private GameObject _storeUiRoot;

        [Tooltip("World-space Y offset for the [F] prompt bubble above the stall.")]
        [SerializeField] private float _promptHeight = 3.8f;

        [Tooltip("Distance at which the [F] prompt appears.")]
        [SerializeField, Min(1f)] private float _activateRadius = 3.5f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private Transform       _hero;
        private HeroLocomotion  _heroLoco;
        private bool            _heroFound;
        private bool            _storeOpen;
        private GameObject      _promptGo;
        private float           _nextProximityCheck;

        private const float CheckInterval = 0.15f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            ResolveHero();

            // Ensure store panel starts hidden regardless of scene save state.
            if (_storeUiRoot != null) _storeUiRoot.SetActive(false);

            // Auto-find the storefront host if root not assigned.
            //
            // WO-1282 - this used to be FindAnyObjectByType<DeNelle.Wallet.PackStore>(...Include).
            // DeNelle.Village no longer references DeNelle.Wallet, so the SAME search now lives with
            // the storefront (PackStoreBootstrap.ResolveStorefrontRoot) and is reached through the
            // rail-neutral registry. Inactive objects are still included there - the store host is
            // disabled in the scene by design, which is why this was never a plain registry lookup.
            //
            // A null answer is ORDINARY, not an error: a Google Play build carries no Solana
            // storefront at all. StorefrontRegistry traces the distinction; OpenStore below already
            // warns when it is asked to open a store it does not have.
            if (_storeUiRoot == null)
            {
                _storeUiRoot = DeNelle.Commerce.StorefrontRegistry.ResolveRoot();
            }
        }

        private void OnDisable()
        {
            // Safety: always close the store if this component is disabled
            // (e.g. scene unload) so HeroLocomotion isn't left frozen.
            if (_storeOpen) CloseStore();
            MobileInteractButton.Release(this);
        }

        private void Update()
        {
            if (!_heroFound) { ResolveHero(); return; }
            if (_hero == null) { _heroFound = false; return; }

            // Store open: its own ✕ close button handles dismissal (kept in case the
            // monetization Realm Store is opened from another entry point). The desktop
            // Escape key trigger was removed — close stays reachable via the panel button.
            if (_storeOpen)
            {
                MobileInteractButton.Release(this);
                return;
            }

            // PROXIMITY-F OPEN REMOVED: the market building routes through
            // BuildingInteractable's parameterized Yarn hook (merchant NPC) now, so this
            // legacy watcher no longer grabs F / the shared button / shows a prompt (that
            // would double-open with the hook). The Realm Store keeps its own entry points.
            HidePrompt();
            MobileInteractButton.Release(this);
        }

        // ── Store open / close ────────────────────────────────────────────────

        private void OpenStore()
        {
            if (_storeUiRoot == null)
            {
                Debug.LogWarning("[MarketplaceInteractor] No Store UI Root assigned — " +
                                 "add the PackStoreUI GameObject to the inspector field.");
                return;
            }

            HidePrompt();
            _storeOpen = true;

            // Freeze hero so they can't wander during browsing.
            if (_heroLoco != null) _heroLoco.enabled = false;

            _storeUiRoot.SetActive(true);

            // Inject a Close button into the store root so the player can dismiss
            // without a keyboard shortcut. Fires CloseStore() when clicked.
            InjectCloseButton();

            Debug.Log("[MarketplaceInteractor] Realm Store opened.");
        }

        private void CloseStore()
        {
            _storeOpen = false;

            if (_storeUiRoot != null) _storeUiRoot.SetActive(false);

            // Restore hero movement.
            if (_heroLoco != null) _heroLoco.enabled = true;

            Debug.Log("[MarketplaceInteractor] Realm Store closed.");
        }

        // ── Close button injection ─────────────────────────────────────────────

        /// <summary>
        /// Adds a "✕  Close" button to the store's root VisualElement so the
        /// player can dismiss the store with the pointer. The button is destroyed
        /// with the UIDocument when the store GameObject is disabled, so there's
        /// no leak across open/close cycles.
        /// </summary>
        private void InjectCloseButton()
        {
            var doc = _storeUiRoot != null ? _storeUiRoot.GetComponent<UIDocument>() : null;
            if (doc == null || doc.rootVisualElement == null) return;

            var root = doc.rootVisualElement;

            // Don't double-inject if already present.
            if (root.Q<Button>("marketplace-close-btn") != null) return;

            // PackStore's themed UI now builds its own corner close button
            // ("store-close-btn", WO-175 shop re-skin). When that exists we skip
            // injecting a second, unthemed close so the shop window stays clean.
            if (root.Q<Button>("store-close-btn") != null) return;

            var closeBtn = new Button(CloseStore)
            {
                name = "marketplace-close-btn",
                text = "X  Close"
            };

            // Absolute position — top-right corner of the store panel.
            closeBtn.style.position         = Position.Absolute;
            closeBtn.style.top              = 12;
            closeBtn.style.right            = 12;
            // Raw device px (no PanelSettings ref scaler): ~148px box ≈ 60 dp on the Seeker
            // (VISUAL_TOUCH_CONTRAST_AUDIT 2026-07-14, P0 — was ~26px ≈ 11 dp).
            closeBtn.style.minHeight        = 148;
            closeBtn.style.minWidth         = 148;
            closeBtn.style.paddingTop       = 10;
            closeBtn.style.paddingBottom    = 10;
            closeBtn.style.paddingLeft      = 20;
            closeBtn.style.paddingRight     = 20;
            closeBtn.style.fontSize         = 34;
            closeBtn.style.backgroundColor  = new StyleColor(new Color(0.15f, 0.06f, 0.28f, 0.92f));
            closeBtn.style.color            = new StyleColor(new Color(0.97f, 0.88f, 1.00f, 1f));
            closeBtn.style.borderTopLeftRadius     = new StyleLength(6);
            closeBtn.style.borderTopRightRadius    = new StyleLength(6);
            closeBtn.style.borderBottomLeftRadius  = new StyleLength(6);
            closeBtn.style.borderBottomRightRadius = new StyleLength(6);

            root.Add(closeBtn);
        }

        // ── Prompt bubble ──────────────────────────────────────────────────────

        private void ShowPrompt()
        {
            _promptGo = BuildBubble(
                "[ Tap / F ]  The Realm Store",
                _promptHeight,
                new Color(0.08f, 0.04f, 0.16f, 0.96f),   // dark plum bg
                new Color(0.85f, 0.60f, 1.00f, 1f));      // violet outline
        }

        private void HidePrompt()
        {
            if (_promptGo != null) Destroy(_promptGo);
            _promptGo = null;
        }

        // ── Hero resolution ────────────────────────────────────────────────────

        private void ResolveHero()
        {
            if (_heroFound) return;
            var loco = Object.FindAnyObjectByType<HeroLocomotion>();
            if (loco != null)
            {
                _hero     = loco.transform;
                _heroLoco = loco;
                _heroFound = true;
            }
        }

        // ── Prompt bubble builder (mirrors DungeonPortal.BuildBubble) ──────────

        private GameObject BuildBubble(string text, float localY, Color bgColor, Color outlineColor)
        {
            var go = new GameObject("MarketplacePrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * localY;

            float charsApprox = Mathf.Max(text.Length, 8);
            float w = Mathf.Clamp(charsApprox * 0.10f + 0.4f, 1.2f, 3.6f);
            float h = 0.38f;

            // Outline quad.
            var outline = GameObject.CreatePrimitive(PrimitiveType.Quad);
            outline.name = "Outline";
            DestroyImmediate(outline.GetComponent<Collider>());
            outline.transform.SetParent(go.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            outline.transform.localScale    = new Vector3(w + 0.06f, h + 0.06f, 1f);
            ApplyFlat(outline.GetComponent<Renderer>(), outlineColor);

            // Background quad.
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Bg";
            DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            bg.transform.localScale    = new Vector3(w, h, 1f);
            ApplyFlat(bg.GetComponent<Renderer>(), bgColor);

            // Text.
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localPosition = Vector3.zero;
            txtGo.transform.localScale    = Vector3.one * 0.055f;
            var tm = txtGo.AddComponent<TextMesh>();
            tm.text          = text;
            tm.fontSize      = 96;
            tm.characterSize = 0.30f;
            tm.anchor        = TextAnchor.MiddleCenter;
            tm.alignment     = TextAlignment.Center;
            tm.color         = new Color(0.97f, 0.93f, 1.00f);

            // Billboard so prompt faces the camera regardless of placement.
            var billboard  = go.AddComponent<PromptBillboard>();
            billboard.Camera = Camera.main;

            return go;
        }

        private static void ApplyFlat(Renderer renderer, Color colour)
        {
            if (renderer == null) return;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
            if (s == null) return;
            var mat = new Material(s);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     colour);
            renderer.sharedMaterial = mat;
        }

        // ── Gizmo — show activate radius in editor ─────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.75f, 0.40f, 1.00f, 0.30f);
            Gizmos.DrawWireSphere(transform.position, _activateRadius);
        }
#endif
    }
}
