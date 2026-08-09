// =============================================================================
// PackStore — the five-pack store UI + purchase flow (spec Part 3 store row)
// -----------------------------------------------------------------------------
// Renders the five canonical packs from the canonical packs.json (Hearth Spark
// → Founder's Vow), each card showing its USD reference plus per-currency
// amounts (SOL / USDC / SKR). The purchase flow calls WalletService.Pay(); on
// confirmation it applies the pack contents to GameState through
// GameStateService.
//
// WO-F conversion (2026-07-03, coverage matrix PackStore row): UIDocument/UITK
// -> code-built uGUI on the Obsidian master frame (ElarionUiKit.
// BuildObsidianModal: FrameMerchant + coin medallion + the ONE shared Close +
// scrim), per the CosmeticShopPanel / LeaderboardPanel reference recipe.
// Scroll list composed inline (ScrollRect + VerticalLayoutGroup). The modal is
// built LAZILY on first open (OnEnable); open/close = canvas SetActive — the
// MarketplaceInteractor contract (SetActive on this GameObject) is unchanged.
//
// Money flow UNCHANGED: async UniTask purchase (never async void), per-pack
// currency rail selection (SOL / USDC / SKR chips), PackCatalog render loop,
// PackPurchased event, treasury-transparency line, CurrencyDisclaimer, and the
// cozy-covenant reassurance line from StorePage.tsx ("You are never required
// to spend anything. Ever.") rendered verbatim. CloseStore() still routes
// through MarketplaceInteractor via reflection (re-enables HeroLocomotion — the
// soft-lock guard) with the locomotion-re-enable fallback.
//
// Devnet-only in the v2 foundation — the WalletService ships over the
// StubWalletProvider, so the whole store runs end-to-end without the Solana
// Unity SDK installed.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>
    /// The pack-store controller. Builds a card per pack from <see cref="PackCatalog"/>,
    /// drives the SOL / USDC / SKR purchase flow through <see cref="WalletService"/>,
    /// and applies confirmed pack contents to <see cref="GameStateService"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PackStore : MonoBehaviour
    {
        [Tooltip("Default currency rail a pack is bought in. SOL / USDC / SKR.")]
        [SerializeField] private CurrencyKind _defaultCurrency = CurrencyKind.Skr;

        private WalletService _wallet;

        // WO-744 MVVM: game-state ownership + entitlement grant + the interactor close-resolve
        // live in the VM so this View never names GameStateService / FindFirstObjectOfType. The
        // money path (WalletService.Pay orchestration) stays here; it asks the VM to read/apply state.
        private PackStoreVM _vm;

        // Modal-arbiter handle (WO-F door): registers with PanelManager so the
        // Realm Store obeys the one-panel-at-a-time rule AND so PanelRouter.Open's
        // post-open VerifyOpenedVisible sees a panel actually recorded open. Purely
        // additive lifecycle glue — the purchase flow / rails / CloseStore are unchanged.
        private PanelHandle _panelHandle;

        // Kit modal (lazy-built on first open) + the surfaces Render() fills.
        private ElarionUiKit.ObsidianModal _modal;
        private Transform _listContent;                 // ScrollRect content — pack cards
        private TextMeshProUGUI _statusBanner;          // purchase status surface
        private TextMeshProUGUI _treasuryLabel;         // rewards-distributor transparency line
        private TextMeshProUGUI _disclaimerLabel;       // PackCatalog.CurrencyDisclaimer

        // Per-pack currency selection (SKU → chosen rail).
        private readonly Dictionary<string, CurrencyKind> _selectedCurrency = new Dictionary<string, CurrencyKind>();
        private bool _purchaseInFlight;

        /// <summary>Raised when a pack purchase confirms — carries the pack and the tx result.</summary>
        public event Action<PackDef, PaymentResult> PackPurchased;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            // Defaults to the devnet StubWalletProvider — no Solana SDK needed.
            _wallet = new WalletService();

            // The game-state seam (ownership queries + entitlement grant + close-resolve). CreateDefault
            // binds the live GameState lazily (provider), so this is safe before GameStateService is ready.
            _vm = PackStoreVM.CreateDefault();

            // Modal-arbiter handle: the manager may hide this store when another panel
            // opens (Close = SetActive(false) -> OnDisable hides the canvas), and its
            // IsOpen probe reports our real on-screen visibility so PanelRouter.Open's
            // VerifyOpenedVisible passes instead of Fail-logging a "not visible" ghost.
            _panelHandle = PanelManager.Register("Realm Store",
                () => { if (this != null) gameObject.SetActive(false); },
                () => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy);
        }

        private void OnEnable()
        {
            // MarketplaceInteractor opens the store by SetActive(true) on this
            // GameObject — build the kit modal lazily on first open, then show.
            EnsureBuilt();
            if (_modal != null && _modal.canvas != null)
                _modal.canvas.SetActive(true);
            Render();

            // Announce to the modal arbiter (closes any other open panel, records us open).
            if (_panelHandle != null) PanelManager.NotifyOpened(_panelHandle);
        }

        private void OnDisable()
        {
            // MarketplaceInteractor closes by SetActive(false) — hide the canvas.
            if (_modal != null && _modal.canvas != null)
                _modal.canvas.SetActive(false);

            // Clear our record with the arbiter (no-op if we were already swapped out).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
        }

        private void OnDestroy()
        {
            if (_modal != null && _modal.canvas != null)
                Destroy(_modal.canvas);
        }

        /// <summary>
        /// Injects a shared <see cref="WalletService"/> — e.g. the same instance a
        /// <see cref="WalletConnectDialog"/> drives, so a connect there is visible
        /// here. Call before the first <see cref="OnEnable"/> or it re-renders now.
        /// </summary>
        public void SetWalletService(WalletService service)
        {
            if (service == null) return;
            _wallet = service;
            Render();
        }

        // =====================================================================
        //  UI construction (kit modal, lazy on first open)
        // =====================================================================

        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;
            using var _ = FlowTrace.Enter("Store", "EnsureBuilt (kit modal)");

            // Shared store size (owner felt-test 2026-07-15: all stores same size / matching Y).
            _modal = ElarionUiKit.BuildObsidianModal("PackStoreUI", "Realm Store",
                ElarionUiKit.StorePanelAnchorMin, ElarionUiKit.StorePanelAnchorMax, CloseStore,
                frameName: RpgUiCatalog.FrameMerchant, medallionIcon: "coin");

            if (_modal == null || _modal.canvas == null)
            {
                // No modal -> the whole store cannot draw and Render() will bail. This is a
                // blank-screen / soft-lock risk; surface it loudly so it self-reports, never silent.
                FlowTrace.Fail("Store", "EnsureBuilt: kit modal failed to build — store cannot draw, player would see a blank/soft-locked panel.");
                return;
            }

            var layout = _modal.chrome.layout;
            var body = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            // Treasury transparency line (spec Week 7 — the v1 "treasury
            // transparency" pattern) — top of the body well.
            _treasuryLabel = MakeText(body, string.Empty, 12, ElarionUi.ParchmentDim,
                FontStyles.Normal, TextAlignmentOptions.Center,
                new Vector2(0.02f, 0.93f), new Vector2(0.98f, 0.99f));

            // Purchase status banner — the only purchase-feedback surface.
            _statusBanner = MakeText(body, string.Empty, 13, ElarionUi.Gold,
                FontStyles.Normal, TextAlignmentOptions.Center,
                new Vector2(0.02f, 0.865f), new Vector2(0.98f, 0.925f));

            // Scrollable pack-card list (inline ScrollRect column). Mobile-first (owner rule):
            // a compact CENTERED band (0.09–0.91), not full-bleed edge-to-edge cards — the pack
            // plates read as centered cards with thumb-zone side margins on a phone.
            var scrollHost = ZoneRect(body, "PackScroll", new Vector2(0.09f, 0.02f), new Vector2(0.91f, 0.86f));
            _listContent = BuildScrollColumn(scrollHost);

            // Footer zone: currency disclaimer + the cozy-covenant line.
            var footHost = layout != null && layout.footer != null ? (Transform)layout.footer : null;
            if (footHost != null)
            {
                _disclaimerLabel = MakeText(footHost, string.Empty, 11, ElarionUi.ParchmentDim,
                    FontStyles.Normal, TextAlignmentOptions.Center,
                    new Vector2(0.02f, 0.42f), new Vector2(0.98f, 0.98f));
                MakeText(footHost, "You are never required to spend anything. Ever.",
                    11, ElarionUi.Gold, FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.42f));
            }
            else
            {
                // Frame has no footer zone — fold the lines into the base of the body.
                _disclaimerLabel = MakeText(body, string.Empty, 11, ElarionUi.ParchmentDim,
                    FontStyles.Normal, TextAlignmentOptions.Center,
                    new Vector2(0.02f, -0.055f), new Vector2(0.98f, -0.005f));
                MakeText(body, "You are never required to spend anything. Ever.",
                    11, ElarionUi.Gold, FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.02f, -0.105f), new Vector2(0.98f, -0.055f));
            }

            _modal.canvas.SetActive(false);   // built hidden; OnEnable shows it

            // VERIFY the scaffold actually built — the card list container is what Render() fills, and
            // the status banner is the only purchase-feedback surface. If either is null the store would
            // silently render nothing / give no purchase feedback; Fail loudly so it self-reports.
            if (_listContent == null)
                FlowTrace.Fail("Store", "EnsureBuilt: _listContent container is null after build — cards cannot render (blank store).");
            else if (_statusBanner == null)
                FlowTrace.Warn("Store", "EnsureBuilt: _statusBanner is null after build — purchase status/errors will have no on-screen surface.");
            else
                FlowTrace.Step("Store", "EnsureBuilt: kit modal built — pack list + status banner ready.");
        }

        /// <summary>
        /// Closes the store exactly the way MarketplaceInteractor does. PackStore
        /// lives in DeNelle.Wallet, which CANNOT reference DeNelle.Village
        /// (one-way asmdef dependency: Village → Wallet), so we drive the
        /// interactor's private CloseStore() via reflection. That path
        /// re-enables HeroLocomotion AND clears the interactor's _storeOpen
        /// flag, so the hero is fully controllable and the store can be
        /// reopened — no soft-lock. If no interactor is found (e.g. a
        /// standalone test scene) we fall back to disabling this GameObject and
        /// re-enabling any disabled HeroLocomotion by name, so the hero is
        /// never left frozen.
        /// </summary>
        private void CloseStore()
        {
            // The VM drives MarketplaceInteractor.CloseStore (re-enables HeroLocomotion + clears
            // _storeOpen) when present; on the fallback it re-enables a disabled hero locomotion and
            // returns false, so we hide this GameObject ourselves. Behaviour is unchanged.
            if (_vm == null) _vm = PackStoreVM.CreateDefault();
            if (!_vm.CloseViaInteractor())
                gameObject.SetActive(false);
        }

        // =====================================================================
        //  Rendering
        // =====================================================================

        /// <summary>Rebuilds every pack card from the canonical catalogue.</summary>
        public void Render()
        {
            using var _ = FlowTrace.Enter("Store", "Render");
            if (_listContent == null)
            {
                // Lazy build: SetWalletService may legitimately arrive before the
                // first open — not a failure, just nothing to draw into yet.
                FlowTrace.Warn("Store", "Render: modal not built yet (lazy) — skipped.");
                return;
            }

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            // Transparency: show the public Rewards Distributor address
            // (spec Week 7 — the v1 "treasury transparency" pattern).
            if (_treasuryLabel != null)
                _treasuryLabel.text = $"Rewards Distributor - {WalletService.RewardsDistributorAddress}";

            if (_disclaimerLabel != null)
                _disclaimerLabel.text = PackCatalog.CurrencyDisclaimer;

            int built = 0;
            foreach (var pack in PackCatalog.Packs)
            {
                if (pack == null) continue;
                var card = BuildPackCard(pack);
                if (card == null)
                {
                    // BuildPackCard guarded out -> this pack has no card. Skip, don't blank the row.
                    FlowTrace.Warn("Store", $"Render: BuildPackCard returned null for pack '{pack.Sku}' — card skipped.");
                    continue;
                }
                built++;
            }

            // R: never a silently blank store. If the catalogue had packs but nothing built, the
            // player sees an empty store — surface it loudly.
            if (built == 0)
                FlowTrace.Fail("Store", "Render: built 0 pack cards — store is EMPTY (no packs in catalogue or all cards failed).");
            else
                FlowTrace.Step("Store", $"Render: built {built} pack card(s).");
        }

        private GameObject BuildPackCard(PackDef pack)
        {
            if (pack == null)
            {
                FlowTrace.Fail("Store", "BuildPackCard: pack is null — cannot build a card.");
                return null;
            }

            // WO2: analytics — player saw this bundle. Guarded: an analytics throw must never blank
            // the whole card (the player still needs to see + buy the pack).
            FlowTrace.Try("Store", $"track bundle_viewed '{pack.Sku}'", () =>
            {
                DeNelle.Core.Analytics.EventTracker.Track("bundle_viewed", new
                {
                    bundleId   = pack.Sku,
                    bundleName = pack.Name,
                    founderOnly = pack.FounderOnly,
                });
            });

            var cardGo = new GameObject($"pack-{pack.Sku}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            cardGo.transform.SetParent(_listContent, false);
            cardGo.GetComponent<LayoutElement>().preferredHeight = 132f;
            var bg = cardGo.GetComponent<Image>();
            var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, "slot_item");
            if (slotSprite != null) { bg.sprite = slotSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else bg.color = new Color(0f, 0f, 0f, 0.35f);

            var card = cardGo.transform;

            // Launch-window tag (founder packs only).
            if (pack.FounderOnly)
            {
                MakeText(card, "Launch window only", 11, new Color(1.00f, 0.82f, 0.45f, 1f),
                    FontStyles.Bold, TextAlignmentOptions.Left,
                    new Vector2(0.03f, 0.86f), new Vector2(0.68f, 0.98f));
            }

            // Name / tagline / USD reference / contents — left column.
            MakeText(card, pack.Name, 16, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.03f, 0.68f), new Vector2(0.68f, 0.86f));
            MakeText(card, pack.Tagline, 12, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.TopLeft, new Vector2(0.03f, 0.44f), new Vector2(0.68f, 0.68f));

            // USD reference (§4.1 — shown for transparency on every wallet rail).
            MakeText(card, $"{pack.UsdReference} reference", 12, ElarionUi.ParchmentDim,
                FontStyles.Normal, TextAlignmentOptions.Left,
                new Vector2(0.03f, 0.28f), new Vector2(0.68f, 0.44f));

            // Contents summary — cosmetics + economy + convenience (§5).
            MakeText(card, DescribeContents(pack), 12, new Color(0.78f, 0.82f, 0.90f, 1f),
                FontStyles.Normal, TextAlignmentOptions.TopLeft,
                new Vector2(0.03f, 0.04f), new Vector2(0.68f, 0.28f));

            // ── ONE right-anchored Buy button per row (store buy-column fix 2026-07-16) ──
            // WAS: three SOL/USDC/SKR rail chips (0.088 card-width ≈ 50px each) PLUS a Buy button,
            // all crammed into the right third. Every kit button's shortest side is force-grown to
            // MinTouchPx(112) by ClampMinTouch, so the sub-112px chips + button INFLATED past their
            // authored rects and overlapped, stacking across rows into the grey-plate "shelf" the
            // owner saw clip the frame — while each label auto-sized to FontBody(50) rendered giant.
            // FIX: a single SKR Buy button authored ABOVE the 112px touch floor (so ClampMinTouch is
            // a no-op — no inflation, no overlap) with a modest label cap so the text reads as a CTA.
            // NOTE (PO): per-row SOL/USDC selection is dropped — SKR is the canonical rail
            // (_defaultCurrency); re-introduce alt-rails as a global toggle if that's wanted.
            //
            // Rect: right column 0.70–0.985 (x) × 0.06–0.94 (y). On the 132px card that resolves to
            // ~116px tall and ~150px+ wide — BOTH sides > MinTouchPx(112), so the button keeps this
            // size and sits cleanly inside its row, in front of the card plate.
            var buyMin = new Vector2(0.70f, 0.06f);
            var buyMax = new Vector2(0.985f, 0.94f);
            if (_vm.IsOwned(pack.Sku))
            {
                MakeText(card, "Owned", 20, new Color(0.55f, 0.90f, 0.55f, 1f), FontStyles.Bold,
                    TextAlignmentOptions.Center, buyMin, buyMax);
            }
            // SECURITY (store-hardening Path A, M1): the Buy CTA is gated by FeatureFlags.RealmStorePurchase,
            // which declares defaultOn: false — OFF on EVERY build (Editor, Development and RELEASE alike)
            // as of the 2026-08-08 store-submission re-gate. (This comment previously claimed "OFF in
            // release/store, ON in Editor/Development"; that split no longer exists — do not restore it.)
            // In a zero-crypto public build the pack still renders cosmetically (above) but NO dead "Buy"
            // button routed to the stub wallet ships — a "Coming soon" placeholder fills the right rail so
            // the card is not blank.
            // CAVEAT WHEN VERIFYING ON A DEVICE: FeatureFlags.Get reads PlayerPrefs FIRST, so a STORED
            // "ff.realmstorepurchase" = 1 BEATS this default. A device that ever had the rail on keeps the
            // Buy button until that key is cleared/zeroed — clear prefs before concluding the gate is off.
            else if (!DeNelle.Core.FeatureFlags.RealmStorePurchase)
            {
                MakeText(card, "Coming soon", 16, ElarionUi.ParchmentDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, buyMin, buyMax);
            }
            else
            {
                var rail = SelectedCurrency(pack.Sku);   // SKR by default (_defaultCurrency)
                var buy = ElarionUiKit.BuildObsidianButton(card,
                    $"Buy {pack.AmountLabel(rail)}",
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    _purchaseInFlight ? ElarionUiKit.ObsidianButtonColor.Gray
                                      : ElarionUiKit.ObsidianButtonColor.Yellow,
                    buyMin, buyMax,
                    () => Purchase(pack, SelectedCurrency(pack.Sku)).Forget());
                if (buy != null) buy.interactable = !_purchaseInFlight;

                // Cap the label so it reads as a CTA, not the giant FontBody(50) auto-size that
                // overflowed the plate. 20–26 ref px matches this panel's small-body scale (the left
                // column runs 12–16); FitSingleLine hard-floors at FontHardFloor(20), so it can only
                // shrink to fit, never grow back to giant.
                var buyLabel = buy != null ? buy.GetComponentInChildren<TMP_Text>(true) : null;
                if (buyLabel != null)
                    ElarionUiKit.FitSingleLine(buyLabel, 20f, 26f);
            }

            FlowTrace.Step("Store", $"BuildPackCard '{pack.Sku}': one right-anchored SKR Buy button (rect 0.70-0.985 / 0.06-0.94, label cap 20-26px).");
            return cardGo;
        }

        private CurrencyKind SelectedCurrency(string sku)
        {
            return _selectedCurrency.TryGetValue(sku, out var c) ? c : _defaultCurrency;
        }

        private static string DescribeContents(PackDef pack)
        {
            var sb = new StringBuilder();
            var c = pack.Contents;
            if (c != null)
            {
                if (c.Cosmetics != null && c.Cosmetics.Count > 0)
                    sb.Append(c.Cosmetics.Count).Append(c.Cosmetics.Count == 1 ? " cosmetic" : " cosmetics");

                var econ = c.Economy;
                if (econ != null)
                {
                    AppendAmount(sb, econ.Crystals, "crystals");
                    AppendAmount(sb, econ.Food, "food");
                    AppendAmount(sb, econ.Coins, "coins");
                    AppendAmount(sb, econ.Glimmer, "glimmer");
                }

                if (c.Convenience != null && c.Convenience.Count > 0)
                {
                    var tokens = 0;
                    foreach (var item in c.Convenience) tokens += item != null ? item.Count : 0;
                    if (tokens > 0)
                    {
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(tokens).Append(" convenience tokens");
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private static void AppendAmount(StringBuilder sb, int amount, string label)
        {
            if (amount <= 0) return;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(amount.ToString("N0")).Append(' ').Append(label);
        }

        // =====================================================================
        //  Purchase flow
        // =====================================================================

        /// <summary>
        /// Runs the full purchase flow for a pack: ensures a wallet is connected,
        /// calls <see cref="WalletService.Pay"/>, awaits devnet confirmation, then
        /// applies the pack contents to <see cref="GameStateService"/>.
        /// </summary>
        public async UniTask<PaymentResult> Purchase(PackDef pack, CurrencyKind currency)
        {
            using var _ = FlowTrace.Enter("Store", $"Purchase pack='{pack?.Sku ?? "<null>"}' {currency}");

            if (pack == null)
            {
                FlowTrace.Fail("Store", "Purchase: pack is null — aborted.");
                return PaymentResult.Failure(string.Empty, currency, "Pack is null.");
            }

            // SECURITY (store-hardening Path A, M1): defense-in-depth — even though the Buy button is not
            // built when the purchase rail is gated OFF, the public Purchase() entry is refused too, so a
            // zero-crypto release build never routes to the stub wallet regardless of caller.
            if (!DeNelle.Core.FeatureFlags.RealmStorePurchase)
            {
                FlowTrace.Warn("Store", $"Purchase '{pack.Sku}' refused — RealmStorePurchase gate OFF (zero-crypto build).");
                return PaymentResult.Failure(pack.Sku, currency, "Store purchases are not available in this build.");
            }

            if (_purchaseInFlight)
            {
                SetStatus("A purchase is already in progress...");
                return PaymentResult.Failure(pack.Sku, currency, "Purchase already in progress.");
            }

            if (_vm.IsOwned(pack.Sku))
            {
                SetStatus($"{pack.Name} is already in your collection.");
                return PaymentResult.Failure(pack.Sku, currency, "Already owned.");
            }

            _purchaseInFlight = true;
            Render();
            try
            {
                // The USDC / SOL flow (§7.4): wallet must be connected first.
                if (!_wallet.IsConnected)
                {
                    SetStatus("Connecting wallet...");
                    var account = await _wallet.Connect();
                    if (!account.IsValid)
                    {
                        FlowTrace.Warn("Store", $"Purchase '{pack.Sku}': wallet connect cancelled/failed — aborted (player NOT charged).");
                        SetStatus("Wallet connection cancelled.");
                        return PaymentResult.Failure(pack.Sku, currency, "Wallet not connected.");
                    }
                }

                SetStatus($"Confirming {pack.AmountLabel(currency)} on {_wallet.NetworkLabel}...");
                var result = await _wallet.Pay(pack, currency);

                if (result.Ok)
                {
                    // Payment confirmed -> the player IS charged. ApplyPackContents MUST land the
                    // entitlement; it self-reports if GameState is unavailable (paid-for content lost).
                    _vm.ApplyPackContents(pack);
                    FlowTrace.Step("Store", $"Purchase '{pack.Sku}' confirmed — tx {result.TxSignature}, contents applied.");
                    SetStatus($"{pack.Name} unlocked - tx {Shorten(result.TxSignature)}.");
                    PackPurchased?.Invoke(pack, result);

                    // WO2: analytics — purchase confirmed.
                    DeNelle.Core.Analytics.EventTracker.Track("purchase_completed", new
                    {
                        packId   = pack.Sku,
                        packName = pack.Name,
                        currency = currency.ToString(),
                        txSig    = result.TxSignature,
                    });
                }
                else
                {
                    FlowTrace.Fail("Store", $"Purchase '{pack.Sku}' ({currency}) FAILED: {result.Error}");
                    SetStatus($"Purchase failed - {result.Error}");
                }
                return result;
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("Store",
                    $"Purchase '{pack.Sku}' ({currency}) THREW: {ex.GetType().Name}: {ex.Message} — outcome indeterminate; if a charge settled the entitlement may be lost.");
                SetStatus($"Purchase failed - {ex.Message}");
                return PaymentResult.Failure(pack.Sku, currency, ex.Message);
            }
            finally
            {
                _purchaseInFlight = false;
                Render();
            }
        }

        private void SetStatus(string message)
        {
            if (_statusBanner != null) _statusBanner.text = message;
            else FlowTrace.Warn("Store", $"SetStatus (no banner element): {message}");
        }

        private static string Shorten(string signature)
        {
            if (string.IsNullOrEmpty(signature) || signature.Length < 8) return signature ?? string.Empty;
            return $"{signature.Substring(0, 4)}...{signature.Substring(signature.Length - 4)}";
        }

        // =====================================================================
        //  uGUI helpers (same shapes as LeaderboardPanel / CosmeticShopPanel)
        // =====================================================================

        private static Transform ZoneRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static Transform BuildScrollColumn(Transform host)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(host, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return contentGo.transform;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
