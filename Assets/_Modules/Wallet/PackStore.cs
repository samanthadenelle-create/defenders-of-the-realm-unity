// =============================================================================
// PackStore — the five-pack store UI + purchase flow (spec Part 3 store row)
// -----------------------------------------------------------------------------
// C# + UI Toolkit port of src/modules/store/ (StorePage.tsx + storeItems.ts).
// Renders the five canonical packs from the canonical packs.json (Hearth Spark
// → Founder's Vow) into a UI Toolkit document, each card showing its USD
// reference plus per-currency amounts (SOL / USDC / SKR). The purchase flow
// calls WalletService.Pay(); on confirmation it applies the pack contents to
// GameState through GameStateService.
//
// Devnet-only in the v2 foundation — the WalletService ships over the
// StubWalletProvider, so the whole store runs end-to-end without the Solana
// Unity SDK installed.
//
// async UniTask for the purchase flow (never async void). The cozy-covenant
// reassurance line from StorePage.tsx ("You are never required to spend
// anything. Ever.") is rendered verbatim.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.State;
using DeNelle.Core.UI;

namespace DeNelle.Wallet
{
    /// <summary>
    /// The pack-store controller. Builds a card per pack from <see cref="PackCatalog"/>,
    /// drives the SOL / USDC / SKR purchase flow through <see cref="WalletService"/>,
    /// and applies confirmed pack contents to <see cref="GameStateService"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PackStore : MonoBehaviour
    {
        [Tooltip("UIDocument hosting PackStore.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Tooltip("Default currency rail a pack is bought in. SOL / USDC / SKR.")]
        [SerializeField] private CurrencyKind _defaultCurrency = CurrencyKind.Skr;

        // Element names expected in PackStore.uxml.
        private const string PackListName = "pack-list";
        private const string StatusBannerName = "store-status";
        private const string TreasuryLabelName = "store-treasury";
        private const string DisclaimerLabelName = "store-disclaimer";

        // USS class names — styled by PackStore.uss.
        private const string CardClass = "pack-card";
        private const string CardNameClass = "pack-card__name";
        private const string CardTaglineClass = "pack-card__tagline";
        private const string CardUsdClass = "pack-card__usd";
        private const string CardPricesClass = "pack-card__prices";
        private const string CardPriceChipClass = "pack-card__price-chip";
        private const string CardPriceChipSelectedClass = "pack-card__price-chip--selected";
        private const string CardContentsClass = "pack-card__contents";
        private const string CardBuyClass = "pack-card__buy";
        private const string CardOwnedClass = "pack-card__owned";
        private const string CardFounderTagClass = "pack-card__founder-tag";

        private WalletService _wallet;

        private VisualElement _packList;
        private Label _statusBanner;
        private Label _treasuryLabel;
        private Label _disclaimerLabel;

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
            if (_document == null) _document = GetComponent<UIDocument>();
            // Defaults to the devnet StubWalletProvider — no Solana SDK needed.
            _wallet = new WalletService();
        }

        private void OnEnable()
        {
            BindElements();
            Render();
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
        //  UI Toolkit binding
        // =====================================================================

        // -----------------------------------------------------------------
        // PLAYER-BUILD FIX: this project's UXML templates render EMPTY in
        // player builds (a known, recurring trap), so root.Q<>() lookups
        // against PackStore.uxml return null and Render() bails — the player
        // sees the panel behind us (the talent tree, shared PanelSettings)
        // and feels soft-locked. We therefore IGNORE the UXML entirely and
        // construct the whole container scaffold in code with inline styles,
        // assigning _packList / _statusBanner / _treasuryLabel /
        // _disclaimerLabel to the code-built elements. Render() is unchanged.
        // -----------------------------------------------------------------
        private void BindElements()
        {
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null) return;

            // 1) Wipe any blank/garbage UXML content.
            root.Clear();

            // 2) Full-screen scrim so the store visibly covers whatever is behind
            //    it (no see-through to the talent tree).
            var overlay = new VisualElement { name = "store-overlay-root" };
            ShopTheme.StyleScrim(overlay);
            overlay.style.justifyContent = Justify.Center;
            overlay.style.paddingTop = 24;
            overlay.style.paddingBottom = 24;
            overlay.style.paddingLeft = 24;
            overlay.style.paddingRight = 24;
            root.Add(overlay);

            // Centered themed shop-window frame — the cards live in a column inside.
            var panel = new VisualElement { name = "store-content-panel" };
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.flexGrow = 1;
            panel.style.width = new StyleLength(Length.Percent(100f));
            panel.style.maxWidth = 720;
            panel.style.maxHeight = new StyleLength(Length.Percent(94f));
            panel.style.alignSelf = Align.Center;
            ShopTheme.StylePanelFrame(panel);
            overlay.Add(panel);

            // Header: crest title + close, with a gilt rule beneath.
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            panel.Add(header);

            header.Add(ShopTheme.MakeTitle("Realm Store"));

            var closeBtn = new Button(CloseStore) { name = "store-close-btn" };
            ShopTheme.StyleCloseButton(closeBtn);
            header.Add(closeBtn);

            panel.Add(ShopTheme.MakeRule());

            // Subtitle / treasury line (assigned to _treasuryLabel).
            _treasuryLabel = new Label(string.Empty) { name = TreasuryLabelName };
            _treasuryLabel.style.fontSize = 12;
            _treasuryLabel.style.color = ShopTheme.ParchmentDim;
            _treasuryLabel.style.marginBottom = 6;
            _treasuryLabel.style.whiteSpace = WhiteSpace.Normal;
            _treasuryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(_treasuryLabel);

            // Status banner (assigned to _statusBanner).
            _statusBanner = new Label(string.Empty) { name = StatusBannerName };
            _statusBanner.style.fontSize = 13;
            _statusBanner.style.color = ShopTheme.Glimmer;
            _statusBanner.style.marginBottom = 8;
            _statusBanner.style.minHeight = 18;
            _statusBanner.style.whiteSpace = WhiteSpace.Normal;
            _statusBanner.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(_statusBanner);

            // Scrollable card list (themed well, no OS scrollbar) — assign its
            // contentContainer to _packList so the existing Render() loop keeps
            // working verbatim.
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "pack-scroll" };
            scroll.style.flexGrow = 1;
            scroll.style.marginBottom = 10;
            scroll.style.width = new StyleLength(Length.Percent(100f));
            ShopTheme.StyleScrollWell(scroll);
            panel.Add(scroll);
            _packList = scroll.contentContainer;
            if (_packList != null) _packList.name = PackListName;

            panel.Add(ShopTheme.MakeRule());

            // Disclaimer (assigned to _disclaimerLabel).
            _disclaimerLabel = new Label(string.Empty) { name = DisclaimerLabelName };
            _disclaimerLabel.style.fontSize = 11;
            _disclaimerLabel.style.color = ShopTheme.ParchmentDim;
            _disclaimerLabel.style.marginBottom = 4;
            _disclaimerLabel.style.whiteSpace = WhiteSpace.Normal;
            _disclaimerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(_disclaimerLabel);

            // Cozy-covenant reassurance line (verbatim from StorePage.tsx).
            var covenant = new Label("You are never required to spend anything. Ever.")
            {
                name = "store-covenant"
            };
            covenant.style.fontSize = 11;
            covenant.style.color = ShopTheme.Aether;
            covenant.style.marginBottom = 4;
            covenant.style.unityFontStyleAndWeight = FontStyle.Italic;
            covenant.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(covenant);
        }

        /// <summary>
        /// Closes the store exactly the way the Escape key does in
        /// MarketplaceInteractor. PackStore lives in DeNelle.Wallet, which
        /// CANNOT reference DeNelle.Village (one-way asmdef dependency:
        /// Village → Wallet), so we drive the interactor's private CloseStore()
        /// via reflection. That path re-enables HeroLocomotion AND clears the
        /// interactor's _storeOpen flag, so the hero is fully controllable and
        /// the store can be reopened — no soft-lock. If no interactor is found
        /// (e.g. a standalone test scene) we fall back to disabling this
        /// GameObject and re-enabling any disabled HeroLocomotion by name, so
        /// the hero is never left frozen.
        /// </summary>
        private void CloseStore()
        {
            if (TryCloseViaInteractor()) return;

            // Fallback: re-enable a disabled hero locomotion, then hide self.
            ReEnableDisabledHeroLocomotion();
            gameObject.SetActive(false);
        }

        private bool TryCloseViaInteractor()
        {
            // Find MarketplaceInteractor by type name across loaded assemblies
            // (we can't reference the Village asmdef directly).
            Type interactorType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                interactorType = asm.GetType("DeNelle.Village.MarketplaceInteractor");
                if (interactorType != null) break;
            }
            if (interactorType == null) return false;

            var interactor = FindFirstObjectOfType(interactorType, true);
            if (interactor == null) return false;

            var closeMethod = interactorType.GetMethod(
                "CloseStore",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (closeMethod == null) return false;

            closeMethod.Invoke(interactor, null);
            return true;
        }

        private static UnityEngine.Object FindFirstObjectOfType(Type t, bool includeInactive)
        {
            var found = Resources.FindObjectsOfTypeAll(t);
            if (found == null) return null;
            foreach (var obj in found)
            {
                // Skip assets / prefabs not in a live scene.
                if (obj is Component comp && comp.gameObject.scene.IsValid())
                {
                    if (includeInactive || comp.gameObject.activeInHierarchy)
                        return obj;
                }
            }
            return null;
        }

        private void ReEnableDisabledHeroLocomotion()
        {
            Type locoType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                locoType = asm.GetType("DeNelle.Village.HeroLocomotion");
                if (locoType != null) break;
            }
            if (locoType == null) return;

            var found = Resources.FindObjectsOfTypeAll(locoType);
            if (found == null) return;
            foreach (var obj in found)
            {
                if (obj is Behaviour behaviour && behaviour.gameObject.scene.IsValid())
                    behaviour.enabled = true;
            }
        }

        // =====================================================================
        //  Rendering
        // =====================================================================

        /// <summary>Rebuilds every pack card from the canonical catalogue.</summary>
        public void Render()
        {
            if (_packList == null) return;
            _packList.Clear();

            // Transparency: show the public Rewards Distributor address
            // (spec Week 7 — the v1 "treasury transparency" pattern).
            if (_treasuryLabel != null)
                _treasuryLabel.text = $"Rewards Distributor — {WalletService.RewardsDistributorAddress}";

            if (_disclaimerLabel != null)
                _disclaimerLabel.text = PackCatalog.CurrencyDisclaimer;

            foreach (var pack in PackCatalog.Packs)
            {
                if (pack == null) continue;
                _packList.Add(BuildPackCard(pack));
            }
        }

        private VisualElement BuildPackCard(PackDef pack)
        {
            // WO2: analytics — player saw this bundle.
            DeNelle.Core.Analytics.EventTracker.Track("bundle_viewed", new
            {
                bundleId   = pack.Sku,
                bundleName = pack.Name,
                founderOnly = pack.FounderOnly,
            });

            var card = new VisualElement { name = $"pack-{pack.Sku}" };
            card.AddToClassList(CardClass);
            // Themed pack-card slot (inline styles survive the build USS trap).
            ShopTheme.StyleCard(card);
            card.style.flexDirection = FlexDirection.Column;

            if (pack.FounderOnly)
            {
                var tag = new Label("Launch window only") { name = $"pack-{pack.Sku}-founder" };
                tag.AddToClassList(CardFounderTagClass);
                tag.style.fontSize = 11;
                tag.style.color = new Color(1.00f, 0.82f, 0.45f, 1f);
                tag.style.unityFontStyleAndWeight = FontStyle.Bold;
                tag.style.marginBottom = 2;
                card.Add(tag);
            }

            var nameLabel = new Label(pack.Name);
            nameLabel.AddToClassList(CardNameClass);
            nameLabel.style.fontSize = 18;
            nameLabel.style.color = ShopTheme.Parchment;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(nameLabel);

            var tagline = new Label(pack.Tagline);
            tagline.AddToClassList(CardTaglineClass);
            tagline.style.fontSize = 13;
            tagline.style.color = ShopTheme.ParchmentDim;
            tagline.style.whiteSpace = WhiteSpace.Normal;
            tagline.style.marginBottom = 4;
            card.Add(tagline);

            // USD reference (§4.1 — shown for transparency on every wallet rail).
            var usd = new Label($"{pack.UsdReference} reference");
            usd.AddToClassList(CardUsdClass);
            usd.style.fontSize = 12;
            usd.style.color = ShopTheme.ParchmentDim;
            usd.style.marginBottom = 4;
            card.Add(usd);

            // Per-currency rail chips — SOL / USDC / SKR (the React "currency rail tabs").
            var prices = new VisualElement();
            prices.AddToClassList(CardPricesClass);
            prices.style.flexDirection = FlexDirection.Row;
            prices.style.flexWrap = Wrap.Wrap;
            prices.style.marginBottom = 4;
            foreach (var currency in new[] { CurrencyKind.Sol, CurrencyKind.Usdc, CurrencyKind.Skr })
            {
                var rail = currency; // capture
                var chip = new Button { text = pack.AmountLabel(rail) };
                chip.AddToClassList(CardPriceChipClass);
                bool selected = SelectedCurrency(pack.Sku) == rail;
                if (selected)
                    chip.AddToClassList(CardPriceChipSelectedClass);
                ShopTheme.StyleChip(chip, selected);
                chip.clicked += () =>
                {
                    _selectedCurrency[pack.Sku] = rail;
                    Render();
                };
                prices.Add(chip);
            }
            card.Add(prices);

            // Contents summary — cosmetics + economy + convenience (§5).
            var contents = new Label(DescribeContents(pack));
            contents.AddToClassList(CardContentsClass);
            contents.style.fontSize = 12;
            contents.style.color = new Color(0.78f, 0.82f, 0.90f, 1f);
            contents.style.whiteSpace = WhiteSpace.Normal;
            contents.style.marginBottom = 6;
            card.Add(contents);

            // Buy / Owned control.
            if (IsOwned(pack.Sku))
            {
                var owned = new Label("Owned");
                owned.AddToClassList(CardOwnedClass);
                owned.style.fontSize = 14;
                owned.style.color = ShopTheme.Owned;
                owned.style.unityFontStyleAndWeight = FontStyle.Bold;
                card.Add(owned);
            }
            else
            {
                var rail = SelectedCurrency(pack.Sku);
                var buy = new Button { text = $"{ShopTheme.CoinGlyph}  Buy — {pack.AmountLabel(rail)}" };
                buy.AddToClassList(CardBuyClass);
                ShopTheme.StyleButton(buy, ShopTheme.ButtonKind.Confirm);
                buy.style.alignSelf = Align.FlexStart;
                buy.style.minWidth = 190;
                buy.SetEnabled(!_purchaseInFlight);
                buy.clicked += () => Purchase(pack, rail).Forget();
                card.Add(buy);
            }

            return card;
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
                        if (sb.Length > 0) sb.Append("  •  ");
                        sb.Append(tokens).Append(" convenience tokens");
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : "—";
        }

        private static void AppendAmount(StringBuilder sb, int amount, string label)
        {
            if (amount <= 0) return;
            if (sb.Length > 0) sb.Append("  •  ");
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
            if (pack == null)
                return PaymentResult.Failure(string.Empty, currency, "Pack is null.");

            if (_purchaseInFlight)
            {
                SetStatus("A purchase is already in progress…");
                return PaymentResult.Failure(pack.Sku, currency, "Purchase already in progress.");
            }

            if (IsOwned(pack.Sku))
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
                    SetStatus("Connecting wallet…");
                    var account = await _wallet.Connect();
                    if (!account.IsValid)
                    {
                        SetStatus("Wallet connection cancelled.");
                        return PaymentResult.Failure(pack.Sku, currency, "Wallet not connected.");
                    }
                }

                SetStatus($"Confirming {pack.AmountLabel(currency)} on {_wallet.NetworkLabel}…");
                var result = await _wallet.Pay(pack, currency);

                if (result.Ok)
                {
                    ApplyPackContents(pack);
                    SetStatus($"{pack.Name} unlocked — tx {Shorten(result.TxSignature)}.");
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
                    SetStatus($"Purchase failed — {result.Error}");
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PackStore] Purchase of '{pack.Sku}' failed: {ex.Message}");
                SetStatus($"Purchase failed — {ex.Message}");
                return PaymentResult.Failure(pack.Sku, currency, ex.Message);
            }
            finally
            {
                _purchaseInFlight = false;
                Render();
            }
        }

        /// <summary>
        /// Applies a purchased pack's contents to the live game state — the
        /// economy top-up lands in the resource wallet and the pack SKU plus its
        /// cosmetic SKUs are recorded as owned. Mirrors the React entitlement
        /// fulfilment (storeItems.ts <c>purchaseGrantFor</c> + <c>grantItem</c>).
        /// </summary>
        private void ApplyPackContents(PackDef pack)
        {
            var service = GameStateService.Instance;
            if (service == null || service.State == null)
            {
                Debug.LogWarning("[PackStore] No GameStateService — pack contents not applied (devnet test run).");
                return;
            }

            var state = service.State;

            // Economy layer — crystals / food / coins into the resource wallet.
            var econ = pack.Contents != null ? pack.Contents.Economy : null;
            if (econ != null)
            {
                var r = state.Resources;
                r.Crystals += econ.Crystals;
                r.Food += econ.Food;
                r.Coins += econ.Coins;
                state.Resources = r;
            }

            // Ownership — the pack SKU + every cosmetic SKU it grants.
            RecordOwned(state.OwnedItemIds, pack.Sku);
            if (pack.Contents != null && pack.Contents.Cosmetics != null)
                foreach (var sku in pack.Contents.Cosmetics)
                    RecordOwned(state.OwnedItemIds, sku);

            // Convenience tokens are consumable items — the v2 foundation has no
            // token tray yet; they are flagged for the Week-8 inventory pass.
            // (Recording the pack SKU above is enough for the entitlement check.)

            // Persist through the service so the save round-trips.
            service.Save();
        }

        private static void RecordOwned(List<string> owned, string sku)
        {
            if (owned == null || string.IsNullOrEmpty(sku)) return;
            if (!owned.Contains(sku)) owned.Add(sku);
        }

        /// <summary>True when the pack SKU is already in the player's owned items.</summary>
        private static bool IsOwned(string sku)
        {
            var service = GameStateService.Instance;
            if (service == null || service.State == null || service.State.OwnedItemIds == null)
                return false;
            return service.State.OwnedItemIds.Contains(sku);
        }

        private void SetStatus(string message)
        {
            if (_statusBanner != null) _statusBanner.text = message;
            else Debug.Log($"[PackStore] {message}");
        }

        private static string Shorten(string signature)
        {
            if (string.IsNullOrEmpty(signature) || signature.Length < 8) return signature ?? string.Empty;
            return $"{signature.Substring(0, 4)}…{signature.Substring(signature.Length - 4)}";
        }
    }
}
