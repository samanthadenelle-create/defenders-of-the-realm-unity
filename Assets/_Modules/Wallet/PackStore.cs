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

        private void BindElements()
        {
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null) return;

            _packList = root.Q<VisualElement>(PackListName);
            _statusBanner = root.Q<Label>(StatusBannerName);
            _treasuryLabel = root.Q<Label>(TreasuryLabelName);
            _disclaimerLabel = root.Q<Label>(DisclaimerLabelName);
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

            if (pack.FounderOnly)
            {
                var tag = new Label("Launch window only") { name = $"pack-{pack.Sku}-founder" };
                tag.AddToClassList(CardFounderTagClass);
                card.Add(tag);
            }

            var nameLabel = new Label(pack.Name);
            nameLabel.AddToClassList(CardNameClass);
            card.Add(nameLabel);

            var tagline = new Label(pack.Tagline);
            tagline.AddToClassList(CardTaglineClass);
            card.Add(tagline);

            // USD reference (§4.1 — shown for transparency on every wallet rail).
            var usd = new Label($"{pack.UsdReference} reference");
            usd.AddToClassList(CardUsdClass);
            card.Add(usd);

            // Per-currency rail chips — SOL / USDC / SKR (the React "currency rail tabs").
            var prices = new VisualElement();
            prices.AddToClassList(CardPricesClass);
            foreach (var currency in new[] { CurrencyKind.Sol, CurrencyKind.Usdc, CurrencyKind.Skr })
            {
                var rail = currency; // capture
                var chip = new Button { text = pack.AmountLabel(rail) };
                chip.AddToClassList(CardPriceChipClass);
                if (SelectedCurrency(pack.Sku) == rail)
                    chip.AddToClassList(CardPriceChipSelectedClass);
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
            card.Add(contents);

            // Buy / Owned control.
            if (IsOwned(pack.Sku))
            {
                var owned = new Label("Owned");
                owned.AddToClassList(CardOwnedClass);
                card.Add(owned);
            }
            else
            {
                var rail = SelectedCurrency(pack.Sku);
                var buy = new Button { text = $"Buy — {pack.AmountLabel(rail)}" };
                buy.AddToClassList(CardBuyClass);
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
