// =============================================================================
// PackStore — THE NIGHT MARKET (WO-1050). The pack store UI + purchase flow.
// -----------------------------------------------------------------------------
// WHAT THIS SCREEN IS NOW. Two columns inside the SAME Obsidian modal:
//
//   ┌ header ──────────────────────────────────────────────────────┐
//   │ The Night Market                     [ your wallet: N SKR ]  │
//   ├───────────────┬──────────────────────────────────────────────┤
//   │ SPOTLIGHT     │ SHELF (scroll; four bands, FIXED order)      │
//   │  aurora       │  ▌FREE TONIGHT      redeem a code           │
//   │  orb/name     │  ▌CLOSE THE GAP     wagon · crate · cart    │
//   │  bar ledger   │  ▌GET THE HEART MOVING  spark · hand · ...  │
//   │  comparison   │  ▌PATRONAGE         patron · founder        │
//   │  CTA          │                                             │
//   ├───────────────┴──────────────────────────────────────────────┤
//   │ 0% STORE FEE · REWARDS DISTRIBUTOR … · TIME AND BEAUTY …     │
//   │      You are never required to spend anything. Ever.         │
//   └──────────────────────────────────────────────────────────────┘
//
// ⛔ THIS PASS IS PRESENTATION ONLY, AND THAT IS LOAD-BEARING, NOT A DISCLAIMER.
// Untouched, in shape and in behaviour: Purchase(), WalletService.Pay,
// PackStoreVM.ApplyPackContents, the PackPurchased event, the analytics calls, and
// EVERY refusal — PurchaseGate.CanBuy is still consulted by BOTH the CTA builder and
// the charge path, so the button and the charge can never disagree.
// FeatureFlags.RealmStorePurchase stays defaultOn:false and is NOT read here.
// With the rail closed the ENTIRE screen still renders — bands, spotlight, ledger,
// trust strip — with "Coming soon" in the CTA slot and no tappable Buy anywhere.
//
// ⛔ THE HONESTY RULE THIS FILE IS WRITTEN TO. The shelf may only advertise what the
// grant seam actually pays. The bar ledger is sourced from PackCatalog.
// LedgerEconomyKeys — the same keys ApplyPackContents routes — so a good that IS
// granted can never be un-drawn and a good that is NOT granted can never appear. The
// comparison line is PURE ARITHMETIC over two real SKUs or it is ABSENT. The wallet
// chip is a READ-ONLY MIRROR of the player's own wallet and never implies the game
// holds SKR. Every one of those is the same rule that got keepers-satchel hidden.
//
// PROVENANCE (CLAUDE.md — Grok drafts, UI/CLI refine, CLI implements): the shape of
// this screen came from a GROK SUGGESTION in WO-1050. What was refined and why is
// recorded in that work order's "CLI refinement" section — the short list is: no new
// SKU was minted, no row ships anchorOnly, the fiat half of the chip is quote-backed
// or absent, the shelf is two cards to a row (the modal is 0.325–0.675 of the screen,
// not full-bleed), the free band carries the promo door only, and the patronage sheen
// sits on the band head rather than on each card.
//
// Landscape only; code-built uGUI (UXML does not work in player builds).
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
using DeNelle.Core.Promo;
using DeNelle.Core.Web3;

namespace DeNelle.Wallet
{
    /// <summary>
    /// The pack-store controller. Builds a banded shelf + a spotlight from <see cref="PackCatalog"/>,
    /// drives the SKR purchase flow through <see cref="WalletService"/>, and applies confirmed pack
    /// contents to <see cref="GameStateService"/> via <see cref="PackStoreVM"/>.
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
        // post-open VerifyOpenedVisible sees a panel actually recorded open.
        private PanelHandle _panelHandle;

        // UI-001: one responsive landscape composition. These values live in one component-style
        // table rather than being scattered through individual card builders.
        private static class NightMarketLayout
        {
            internal static readonly Vector2 PanelMin     = new Vector2(0.055f, 0.045f);
            internal static readonly Vector2 PanelMax     = new Vector2(0.945f, 0.955f);
            internal static readonly Vector2 HeaderMin    = new Vector2(0.018f, 0.925f);
            internal static readonly Vector2 HeaderMax    = new Vector2(0.982f, 0.995f);
            internal static readonly Vector2 StatusMin    = new Vector2(0.018f, 0.865f);
            internal static readonly Vector2 StatusMax    = new Vector2(0.982f, 0.920f);
            internal static readonly Vector2 SpotlightMin = new Vector2(0.018f, 0.135f);
            internal static readonly Vector2 SpotlightMax = new Vector2(0.365f, 0.855f);
            internal static readonly Vector2 ShelfMin     = new Vector2(0.382f, 0.135f);
            internal static readonly Vector2 ShelfMax     = new Vector2(0.982f, 0.855f);
            internal static readonly Vector2 TrustMin     = new Vector2(0.018f, 0.012f);
            internal static readonly Vector2 TrustMax     = new Vector2(0.982f, 0.118f);
            internal const float CardHeightPx = 240f;
            internal const int CardsPerRow = 2;
        }

        /// <summary>
        /// Card height in reference px. ⛔ BOTH SIDES OF A CARD MUST CLEAR
        /// <see cref="ElarionUiKit.MinTouchPx"/> (112) so <see cref="ElarionUiKit.ClampMinTouch"/> is
        /// a NO-OP — a stronger statement than "it passes". A sub-112 control does not fail the
        /// clamp, it INFLATES past its authored rect and stacks into its neighbours; that is the
        /// precise defect that produced the grey-plate shelf the owner saw clip the frame on
        /// 2026-07-16. Author above the floor; never rely on the clamp being kind.
        /// </summary>
        private const float CardHeightPx = NightMarketLayout.CardHeightPx;

        /// <summary>
        /// Cards per shelf row. Two is the device-verified readability ruling: the earlier three-up
        /// pass cleared the touch floor but made names, contents and prices illegible at play distance.
        /// Readability outranks catalogue density; the shelf scrolls.
        /// </summary>
        private const int CardsPerRow = NightMarketLayout.CardsPerRow;

        // Kit modal (lazy-built on first open) + the surfaces Render() fills.
        private ElarionUiKit.ObsidianModal _modal;
        private Transform _body;
        private Transform _shelfContent;                // band strips + card rows
        private int _persistentShelfChildren;           // the FREE band, built once, never re-rendered
        private Transform _spotlightHost;               // rebuilt whole on each focus change
        private TextMeshProUGUI _statusBanner;          // purchase status surface
        private TextMeshProUGUI _balanceLabel;          // the read-only wallet mirror
        private TextMeshProUGUI _disclaimerLabel;       // PackCatalog.CurrencyDisclaimer
        private StoreAurora _aurora;                    // Lane G — the four motion moments

        // The promo-code door. A CHILD overlay on this store's own canvas.
        private RedeemCodePanel _redeem;

        // Per-pack currency selection (SKU → chosen rail).
        private readonly Dictionary<string, CurrencyKind> _selectedCurrency = new Dictionary<string, CurrencyKind>();
        private bool _purchaseInFlight;

        private enum CommerceState
        {
            Ready, OpeningWallet, AwaitingApproval, Submitted, Verifying,
            Delivering, Fulfilled, Cancelled, Failed, Delayed
        }

        private CommerceState _commerceState = CommerceState.Ready;
        private string _commerceDetail = string.Empty;
        private float _commerceStateSince;

        // ── Focus (the spotlight) ────────────────────────────────────────────
        private string _focusSku;
        private string _pendingShortfallLabel;
        private int _pendingShortfallMissing;

        // Selection marks, so a focus change repaints two cards instead of the whole shelf.
        private readonly Dictionary<string, Image> _cardRails = new Dictionary<string, Image>();
        private readonly Dictionary<string, Outline> _cardBorders = new Dictionary<string, Outline>();

        // ── The wallet mirror ────────────────────────────────────────────────
        private enum BalanceState { NoWallet, Checking, Unavailable, Known }
        private BalanceState _balanceState = BalanceState.NoWallet;
        private double _balanceSkr;
        private double _fiatUsd;
        private bool _hasFiat;
        private float _fiatAtRealtime;

        /// <summary>
        /// How long a Jupiter quote may sit on screen. A quote is a MOVING PRICE; past this the fiat
        /// half is dropped rather than left to rot, and the next open re-asks. A wrong dollar figure
        /// beside a real balance is worse than no dollar figure.
        /// </summary>
        private const float FiatStaleSeconds = 120f;

        /// <summary>Raised when a pack purchase confirms — carries the pack and the tx result.</summary>
        public event Action<PackDef, PaymentResult> PackPurchased;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            _wallet = new WalletService();
            _vm = PackStoreVM.CreateDefault();

            _panelHandle = PanelManager.Register("Realm Store",
                () => { if (this != null) gameObject.SetActive(false); },
                () => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy);
        }

        private void OnEnable()
        {
            EnsureBuilt();
            if (_modal != null && _modal.canvas != null)
                _modal.canvas.SetActive(true);
            Render();
            RefreshWalletMirror().Forget();
            RestorePendingPresentation();

            if (_panelHandle != null) PanelManager.NotifyOpened(_panelHandle);
        }

        private void OnDisable()
        {
            if (_redeem != null) _redeem.Close();
            if (_modal != null && _modal.canvas != null)
                _modal.canvas.SetActive(false);
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
        }

        private void OnDestroy()
        {
            if (_modal != null && _modal.canvas != null)
                Destroy(_modal.canvas);
        }

        private void Update()
        {
            if (!_purchaseInFlight) return;
            float elapsed = Time.realtimeSinceStartup - _commerceStateSince;
            if (_commerceState == CommerceState.OpeningWallet && elapsed >= 5f)
                RenderCommerceStatus("Wallet is taking longer than usual. Check the wallet app; the request times out at 30 seconds.");
            else if (_commerceState == CommerceState.Verifying && elapsed >= 60f)
                SetCommerceState(CommerceState.Delayed,
                    "Your payment may still settle. It is recorded for reconciliation; do not pay again.");
        }

        /// <summary>
        /// Injects a shared <see cref="WalletService"/> — e.g. the same instance a
        /// <see cref="WalletConnectDialog"/> drives, so a connect there is visible here.
        /// </summary>
        public void SetWalletService(WalletService service)
        {
            if (service == null) return;
            _wallet = service;
            Render();
            RefreshWalletMirror().Forget();
        }

        /// <summary>
        /// Opens the store pre-focused on the pack that closes a REAL shortfall the caller is looking
        /// at (label = "Wood"/"Iron"/"Food"/"Crystals", missing = the gap). Call BEFORE enabling this
        /// GameObject; a zero/absent shortfall focuses <c>starters-hand</c> instead.
        ///
        /// <para>⚠ THE STORE CANNOT COMPUTE A SHORTFALL BY ITSELF and must not pretend to. A shortfall
        /// only exists relative to a thing the player is blocked ON — an upgrade, a build cost — and
        /// that context lives with the caller, not here. Grok's draft had the store resolve the
        /// player's "live shortfall" on open; there is no such value to read. This seam is that idea,
        /// made honest: whoever HAS the gap hands it over.</para>
        ///
        /// <para>⛔ <see cref="ShortfallPackOffer"/> gains exactly ONE new caller and NO new
        /// capability. It still only returns a <see cref="PackDef"/>; it never grants, charges or
        /// routes, and nothing here gives it a way to. WO-931 is that mistake.</para>
        /// </summary>
        public void FocusShortfall(string resourceLabel, int missing)
        {
            _pendingShortfallLabel = resourceLabel;
            _pendingShortfallMissing = missing;
            FlowTrace.Step("Store", $"FocusShortfall requested: {missing} {resourceLabel}.");
        }

        // =====================================================================
        //  UI construction (kit modal, lazy on first open)
        // =====================================================================

        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;
            using var _ = FlowTrace.Enter("Store", "EnsureBuilt (Night Market)");

            // UI-001: the Night Market is a browse surface, not a portrait vendor card. It keeps
            // the shared Obsidian chrome and ONE shared Close while using a landscape footprint.
            _modal = ElarionUiKit.BuildObsidianModal("PackStoreUI", StoreStrings.Get(StoreStrings.KeyWordmark),
                NightMarketLayout.PanelMin, NightMarketLayout.PanelMax, CloseStore,
                frameName: RpgUiCatalog.FrameMerchant, medallionIcon: "coin");

            if (_modal == null || _modal.canvas == null)
            {
                FlowTrace.Fail("Store", "EnsureBuilt: kit modal failed to build — store cannot draw, player would see a blank/soft-locked panel.");
                return;
            }

            var layout = _modal.chrome.layout;
            _body = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            // Lane G host. Built ONCE and reused; under the reduced-motion preference it disables
            // itself on enable and registers nothing, so the flat lights remain.
            _aurora = _modal.canvas.GetComponent<StoreAurora>();
            if (_aurora == null) _aurora = _modal.canvas.AddComponent<StoreAurora>();

            BuildHeader(_body);

            // Purchase status banner — the only purchase-feedback surface. It holds STILL: it is
            // read to make a decision, so nothing animates near it (Lane G rule 2).
            _statusBanner = MakeText(_body, string.Empty, 20, ElarionUi.Gold,
                FontStyles.Normal, TextAlignmentOptions.Center, NightMarketLayout.StatusMin, NightMarketLayout.StatusMax);

            // Left column — the spotlight. Rebuilt whole on each focus change.
            _spotlightHost = ZoneRect(_body, "Spotlight", NightMarketLayout.SpotlightMin, NightMarketLayout.SpotlightMax);
            Plate(_spotlightHost, NightMarketPalette.GroundRaised);

            // Right column — the banded shelf.
            var shelfHost = ZoneRect(_body, "Shelf", NightMarketLayout.ShelfMin, NightMarketLayout.ShelfMax);
            _shelfContent = BuildScrollColumn(shelfHost);

            // ── BAND 1 — FREE TONIGHT, built HERE and never rebuilt ──────────
            // ⛔ THE PROMO DOOR IS CONSTRUCTED AT BUILD TIME, NOT AT RENDER TIME, AND THAT IS THE
            // WHOLE POINT. Render() clears and rebuilds the priced bands from the catalogue; if the
            // free band were rebuilt with them, an empty catalogue, an early Render bail or a single
            // failed card would take the promo system's only player entry point down with it — which
            // is the exact state the promo-redeem-door WO closed, and PromoRedeemEntryRegression
            // pins it here on purpose. Built once, first, unconditionally: there is NO branch it can
            // fall out of, and it is deliberately OUTSIDE the purchase-flag test because redeeming
            // spends no money and must work today with buying disabled.
            //
            // Render() protects it by clearing only the children ABOVE _persistentShelfChildren, so
            // Free also stays FIRST in the fixed band order for free — nothing is asked for before
            // something is given.
            BuildFreeBand(PromoStrings.Get(PromoStrings.KeyEntry));
            _persistentShelfChildren = _shelfContent != null ? _shelfContent.childCount : 0;

            BuildTrustStrip(_body);

            _modal.canvas.SetActive(false);   // built hidden; OnEnable shows it

            if (_shelfContent == null)
                FlowTrace.Fail("Store", "EnsureBuilt: shelf container is null after build — cards cannot render (blank store).");
            else if (_statusBanner == null)
                FlowTrace.Warn("Store", "EnsureBuilt: _statusBanner is null after build — purchase status/errors will have no on-screen surface.");
            else
                FlowTrace.Step("Store", "EnsureBuilt: Night Market built — header, spotlight, banded shelf, trust strip.");
        }

        /// <summary>Wordmark on the left, the read-only wallet mirror on the right.</summary>
        private void BuildHeader(Transform body)
        {
            var host = ZoneRect(body, "StoreHeader", NightMarketLayout.HeaderMin, NightMarketLayout.HeaderMax);

            // ── The wallet mirror ────────────────────────────────────────────
            // ⛔ THE GAME NEVER HOLDS SKR AND MUST NEVER READ AS IF IT DOES. SKR is Solana Mobile's
            // own governance token — the owner did not mint it, does not own it, and is not
            // releasing a token of her own; it is the settlement rail a dApp Store title converts
            // out through. There is NO in-game SKR ledger, earn loop or spend loop and there must
            // never be one. This label is a READ-ONLY MIRROR of the player's OWN wallet, read
            // through the existing SolanaWalletProvider.GetBalance path. Never written, never
            // granted, never deducted in-game. The copy says "your wallet" for exactly that reason.
            _balanceLabel = MakeText(host, string.Empty, 20, ElarionUi.Parchment,
                FontStyles.Normal, TextAlignmentOptions.Right, new Vector2(0.35f, 0f), new Vector2(1f, 1f));
        }

        /// <summary>
        /// Lane E — four claims, each verifiable, promoted from footer legalese to a permanent floor.
        /// Nothing here animates: it is the part of the screen a sceptical player reads slowest.
        /// </summary>
        private void BuildTrustStrip(Transform body)
        {
            var host = ZoneRect(body, "StoreLegalFooter", NightMarketLayout.TrustMin, NightMarketLayout.TrustMax);
            Plate(host, new Color(0f, 0f, 0f, 0.30f));

            string treasury = Shorten(WalletService.RewardsDistributorAddress);
            string claims = string.Join("   -   ", new[]
            {
                StoreStrings.Get(StoreStrings.KeyTrustFee),
                StoreStrings.Format(StoreStrings.KeyTrustTreasury, treasury),
                StoreStrings.Get(StoreStrings.KeyTrustNeverPower),
            });

            MakeText(host, claims, 16, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.01f, 0.52f), new Vector2(0.99f, 0.98f));

            // The covenant is VERBATIM, italic, right-anchored and the LAST thing read.
            MakeText(host, StoreStrings.Get(StoreStrings.KeyCovenant), 16, ElarionUi.Gold,
                FontStyles.Italic, TextAlignmentOptions.Right, new Vector2(0.01f, 0.06f), new Vector2(0.99f, 0.50f));

            // The market disclaimer keeps its place beneath the claims.
            _disclaimerLabel = MakeText(host, PackCatalog.CurrencyDisclaimer, 15, ElarionUi.Parchment,
                FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0.01f, 0.06f), new Vector2(0.55f, 0.50f));
        }

        /// <summary>
        /// Opens the Redeem-a-Code overlay on THIS store's canvas.
        /// </summary>
        private void OpenRedeemPanel()
        {
            if (_modal == null || _modal.canvas == null)
            {
                FlowTrace.Fail("Store", "Redeem tapped but the store modal is gone — cannot host the redeem overlay.");
                return;
            }
            if (_redeem == null) _redeem = new RedeemCodePanel(_modal.canvas.transform);
            _redeem.Open();
        }

        /// <summary>
        /// Closes the store exactly the way MarketplaceInteractor does. PackStore lives in
        /// DeNelle.Wallet, which CANNOT reference DeNelle.Village (one-way asmdef dependency:
        /// Village → Wallet), so the VM drives the interactor's private CloseStore() — that path
        /// re-enables HeroLocomotion AND clears _storeOpen, so the hero is never left frozen.
        /// This is the soft-lock guard and WO-1050 does not touch it.
        /// </summary>
        private void CloseStore()
        {
            if (_vm == null) _vm = PackStoreVM.CreateDefault();
            if (!_vm.CloseViaInteractor())
                gameObject.SetActive(false);
        }

        // =====================================================================
        //  Rendering — the shelf
        // =====================================================================

        /// <summary>Rebuilds the banded shelf and the spotlight from the canonical catalogue.</summary>
        public void Render()
        {
            using var _ = FlowTrace.Enter("Store", "Render");
            if (_shelfContent == null)
            {
                FlowTrace.Warn("Store", "Render: modal not built yet (lazy) — skipped.");
                return;
            }

            // ⛔ CLEAR ONLY THE PRICED BANDS. The first _persistentShelfChildren children are the
            // FREE band, built once in EnsureBuilt so no render failure can take the promo door with
            // it (see the note at that build site).
            for (int i = _shelfContent.childCount - 1; i >= _persistentShelfChildren; i--)
                Destroy(_shelfContent.GetChild(i).gameObject);
            _cardRails.Clear();
            _cardBorders.Clear();

            if (_disclaimerLabel != null)
                _disclaimerLabel.text = PackCatalog.CurrencyDisclaimer;

            // Shared ledger scale, computed ONCE per render over every browsable pack. Per GOOD, not
            // global: one scale across all goods would make every crystals bar a sliver next to a
            // wood bar and the comparison it exists to make would be lost.
            var scale = BuildLedgerScale();

            int built = 0;
            // ⛔ FIXED BAND ORDER, AND IT IS THE ENUM ORDER, NOT A SORT. Free is first because
            // nothing is asked for before something is given; Patronage is last so it is the read on
            // the way out. (StoreBand's own declaration order is the authority — see PackCatalog.)
            // StoreBand.Free is ABSENT from this walk on purpose: it is already on the shelf, built
            // once in EnsureBuilt and preserved above. The order is still Free -> Gap -> Basket ->
            // Patronage because Free occupies the first children and these append after them.
            foreach (StoreBand band in new[] { StoreBand.Gap, StoreBand.Basket, StoreBand.Patronage })
            {
                var rows = PacksInBand(band);
                if (rows.Count == 0) continue;

                BuildBandHead(band);

                for (int i = 0; i < rows.Count; i += CardsPerRow)
                {
                    var strip = BuildCardRow();
                    for (int c = 0; c < CardsPerRow; c++)
                    {
                        if (i + c < rows.Count)
                        {
                            if (BuildPackCard(strip, rows[i + c], band) != null) built++;
                            else FlowTrace.Warn("Store", $"Render: BuildPackCard returned null for pack '{rows[i + c].Sku}' — card skipped.");
                        }
                        else
                        {
                            // A lone card must stay card-sized, not stretch to fill the strip.
                            Spacer(strip);
                        }
                    }
                }
            }

            if (built == 0)
                FlowTrace.Fail("Store", "Render: built 0 pack cards — shelf is EMPTY (no packs in catalogue or all cards failed).");
            else
                FlowTrace.Step("Store", $"Render: built {built} pack card(s) across the four bands.");

            FocusPack(ResolveFocusSku(), scale, animate: false);
        }

        /// <summary>
        /// The browsable rows of one band, in catalogue order.
        /// </summary>
        private List<PackDef> PacksInBand(StoreBand band)
        {
            var list = new List<PackDef>();
            foreach (var pack in PackCatalog.Packs)
            {
                if (pack == null || !pack.StoreVisible) continue;

                // ── Impulse SKUs on the shelf: THREE curated, the rest contextual ──────────────
                // OWNER RULING 2026-08-21 ("Middle — one impulse tier per resource"). The twelve
                // single-resource impulse SKUs split into two populations:
                //   * shelfCurated == true  -> a browsable shelf row. Exactly THREE: the MEDIUM
                //     rung of wood, iron and food.
                //   * shelfCurated == false -> contextual only, reachable ONLY through
                //     ShortfallPackOffer, exactly as before. Nine SKUs, unchanged.
                //
                // ⚠ THIS NARROWS THE WO-947 §12c.4 GUARDRAIL; IT DOES NOT REPEAL IT. What that
                // ruling refused was a WALL of twelve resource-for-cash listings. Three curated
                // tiers is not that wall, and the wall is still structurally prevented: the other
                // nine cannot reach this loop. Re-tagging more rows shelfCurated walks back toward
                // the thing WO-947 refused, so that is an OWNER call, not a code call.
                //
                // The decision lives in packs.json (`shelfCurated`), not in a SKU list here. Do not
                // reintroduce a hardcoded list in this file.
                if (pack.Impulse && !pack.ShelfCurated) continue;

                if (PackCatalog.BandOf(pack) != band) continue;
                list.Add(pack);
            }
            return list;
        }

        /// <summary>
        /// The band head: a 3 px MARK, a mono EYEBROW and a right-aligned sub-label.
        /// <para>⛔ THE MARK AND THE EYEBROW ARE THE BAND'S IDENTITY, not the colour. The owner is
        /// red/green colourblind, so the hue is the FIRST thing allowed to be lost: strip every
        /// colour and the eyebrow still names the band and the mark still separates it. The four
        /// lights additionally step apart in rec.709 value (NightMarketPalette.MinGreyscaleStep), and
        /// that step is asserted by a regression rather than trusted from a comment.</para>
        /// </summary>
        private void BuildBandHead(StoreBand band)
        {
            var go = new GameObject("band-" + band, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(_shelfContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 46f;
            var host = go.transform;

            var light = NightMarketPalette.For(band);

            // The 3 px mark.
            var mark = Plate(host, light);
            if (mark != null)
            {
                var rt = mark.rectTransform;
                rt.anchorMin = new Vector2(0.005f, 0.12f);
                rt.anchorMax = new Vector2(0.013f, 0.86f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }

            MakeText(host, BandEyebrow(band), 15, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.BottomLeft, new Vector2(0.03f, 0.10f), new Vector2(0.55f, 0.92f));
            MakeText(host, BandSubLabel(band), 12, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.BottomRight, new Vector2(0.55f, 0.10f), new Vector2(0.99f, 0.80f));

            // G4 — the patronage sheen. A slow iridescent roll along the band head's top edge.
            // ⚠ ON THE BAND HEAD, NOT ON EACH CARD (Grok drew it per-row). One strip is one extra
            // Graphic instead of one per patronage card, which keeps the draw-call budget honest,
            // and it still does the only job it has: it is the ONLY motion anywhere on the shelf,
            // which is what marks the tier.
            if (band == StoreBand.Patronage && _aurora != null)
            {
                _aurora.AddDrift(host, "sheen", new Vector2(0.005f, 0.88f), new Vector2(0.99f, 0.97f),
                    new Color(light.r, light.g, light.b, 0.55f), 14f, new Vector2(1f, 0f),
                    followsBandLight: false, strip: true);
            }
        }

        private static string BandEyebrow(StoreBand band)
        {
            switch (band)
            {
                case StoreBand.Free:      return StoreStrings.Get(StoreStrings.KeyBandFree);
                case StoreBand.Gap:       return StoreStrings.Get(StoreStrings.KeyBandGap);
                case StoreBand.Patronage: return StoreStrings.Get(StoreStrings.KeyBandPatronage);
                default:                  return StoreStrings.Get(StoreStrings.KeyBandBasket);
            }
        }

        private static string BandSubLabel(StoreBand band)
        {
            switch (band)
            {
                case StoreBand.Free:      return StoreStrings.Get(StoreStrings.KeyBandFreeSub);
                case StoreBand.Gap:       return StoreStrings.Get(StoreStrings.KeyBandGapSub);
                case StoreBand.Patronage: return StoreStrings.Get(StoreStrings.KeyBandPatronageSub);
                default:                  return StoreStrings.Get(StoreStrings.KeyBandBasketSub);
            }
        }

        /// <summary>
        /// Lane D — the free band.
        ///
        /// <para>⛔ IT CARRIES THE PROMO DOOR AND NOTHING ELSE, AND THAT IS AN ASSEMBLY FACT, NOT A
        /// SHORTCUT. Grok's draft put the daily chest and the rewarded lantern here.
        /// <c>DailyChestController</c> lives in <b>DeNelle.Village</b>, and the dependency runs
        /// Village → Wallet, one way: this file cannot see the chest's claim state, cannot ask
        /// whether it is claimable and cannot claim it. Drawing a chest card anyway would be a
        /// control that reports nothing and does nothing — the exact dishonesty this pass is
        /// removing everywhere else. The remaining routes were both refused on purpose: reflection
        /// is banned (CLAUDE.md §10) and widening the asmdef to reach one MonoBehaviour is the
        /// dependency the port spec forbids. Surfacing the chest needs a Core-level status seam
        /// (an interface + a CoreServices registration, the IVillageHud shape) — a real, small piece
        /// of work, and NOT one to smuggle into a presentation pass.</para>
        ///
        /// <para>⛔ THE PROMO DOOR STAYS OUTSIDE THE PURCHASE-FLAG TEST. Redeeming spends no money,
        /// touches no wallet rail and must work TODAY with purchases disabled. It is built here, in
        /// a band that is rendered unconditionally, so there is NO branch it can fall out of — the
        /// same structural guarantee it had when it sat outside the card loop.</para>
        /// </summary>
        private void BuildFreeBand(string entryLabel)
        {
            if (_shelfContent == null) return;
            BuildBandHead(StoreBand.Free);
            // Keep every free-door row genuinely two-up, matching the priced shelf.
            var firstStrip = BuildCardRow();

            var slot = new GameObject("free-redeem", typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(firstStrip, false);
            var le = slot.GetComponent<LayoutElement>();
            le.preferredHeight = CardHeightPx;
            le.flexibleWidth = 1f;
            le.minWidth = ElarionUiKit.MinTouchPx;

            Plate(slot.transform, NightMarketPalette.GroundRaised);
            var light = NightMarketPalette.For(StoreBand.Free);
            Orb(slot.transform, light);

            MakeText(slot.transform, PromoStrings.Get(PromoStrings.KeyTitle), 15, ElarionUi.Parchment,
                FontStyles.Bold, TextAlignmentOptions.TopLeft,
                new Vector2(0.24f, 0.70f), new Vector2(0.96f, 0.92f));
            MakeText(slot.transform, PromoStrings.Get(PromoStrings.KeyBlurb), 11, ElarionUi.ParchmentDim,
                FontStyles.Italic, TextAlignmentOptions.TopLeft,
                new Vector2(0.06f, 0.54f), new Vector2(0.96f, 0.70f));

            // The button fills the lower half of a 168 px card: ~78 px tall, which is UNDER the
            // 112 px floor, so ClampMinTouch would grow it. Give it the whole card height instead
            // and let the plate carry the label above it — authored over the floor, clamp is a
            // no-op, nothing inflates into the row beside it.
            var redeemBtn = ElarionUiKit.BuildObsidianButton(slot.transform,
                entryLabel,
                ElarionUiKit.ObsidianButtonStyle.Style1,
                ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.54f),
                OpenRedeemPanel);
            if (redeemBtn == null)
                FlowTrace.Fail("Store", "BuildFreeBand: Redeem-a-Code button failed to build — the promo system has NO player entry point again.");
            else
            {
                var redeemLabel = redeemBtn.GetComponentInChildren<TMP_Text>(true);
                if (redeemLabel != null) ElarionUiKit.FitSingleLine(redeemLabel, 20f, 28f);
                FlowTrace.Step("Store", "BuildFreeBand: Redeem-a-Code entry built (ungated by design — the purchase flag gates BUYING only).");
            }

            BuildFreeDoor(firstStrip, "SEASON TRACK", "Play battles. Earn every tier.", PanelId.BattlePass);

            var secondStrip = BuildCardRow();
            BuildFreeDoor(secondStrip, "MONTHLY LEDGER", "Thirty claims. Missed days stay yours.", PanelId.MonthlyLedger);
            Spacer(secondStrip);
        }

        private void BuildFreeDoor(Transform strip, string title, string blurb, PanelId panel)
        {
            var slot = new GameObject("free-" + panel, typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(strip, false);
            var le = slot.GetComponent<LayoutElement>(); le.preferredHeight = CardHeightPx; le.flexibleWidth = 1f; le.minWidth = ElarionUiKit.MinTouchPx;
            Plate(slot.transform, NightMarketPalette.GroundRaised);
            Orb(slot.transform, NightMarketPalette.For(StoreBand.Free));
            MakeText(slot.transform, title, 15, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, new Vector2(0.24f, 0.70f), new Vector2(0.96f, 0.92f));
            MakeText(slot.transform, blurb, 11, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.TopLeft, new Vector2(0.06f, 0.54f), new Vector2(0.96f, 0.70f));
            ElarionUiKit.BuildObsidianButton(slot.transform, "OPEN",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.54f), () => PanelRouter.Open(panel));
        }

        /// <summary>One horizontal strip of <see cref="CardsPerRow"/> flex cards.</summary>
        private Transform BuildCardRow()
        {
            var go = new GameObject("row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(_shelfContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = CardHeightPx;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 9f;
            h.padding = new RectOffset(4, 4, 3, 3);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return go.transform;
        }

        private static void Spacer(Transform strip)
        {
            var go = new GameObject("spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(strip, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private GameObject BuildPackCard(Transform strip, PackDef pack, StoreBand band)
        {
            if (pack == null)
            {
                FlowTrace.Fail("Store", "BuildPackCard: pack is null — cannot build a card.");
                return null;
            }

            // WO2: analytics — player saw this bundle. Guarded: an analytics throw must never blank
            // the whole card.
            FlowTrace.Try("Store", $"track bundle_viewed '{pack.Sku}'", () =>
            {
                DeNelle.Core.Analytics.EventTracker.Track("bundle_viewed", new
                {
                    bundleId   = pack.Sku,
                    bundleName = pack.Name,
                    founderOnly = pack.FounderOnly,
                });
            });

            var cardGo = new GameObject($"pack-{pack.Sku}", typeof(RectTransform), typeof(Image),
                typeof(LayoutElement), typeof(Button), typeof(Outline));
            cardGo.transform.SetParent(strip, false);

            var le = cardGo.GetComponent<LayoutElement>();
            le.preferredHeight = CardHeightPx;
            le.minHeight = CardHeightPx;
            le.flexibleWidth = 1f;
            le.minWidth = ElarionUiKit.MinTouchPx;

            var bg = cardGo.GetComponent<Image>();
            var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, "slot_item");
            if (slotSprite != null) { bg.sprite = slotSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else bg.color = NightMarketPalette.GroundRaised;

            var card = cardGo.transform;
            var light = NightMarketPalette.For(band);
            var orbTint = NightMarketPalette.ParseTint(pack.OrbTint, light);

            // ── SELECTION IS NEVER COLOUR-ONLY ────────────────────────────────
            // Three simultaneous carriers: a 2 px left RAIL in the band light, a BORDER, and — the
            // one that cannot be missed by any eye — the card MOVES TO THE SPOTLIGHT. Colour is the
            // third-most-important of the three and the only one that can be lost.
            var rail = Plate(card, light);
            if (rail != null)
            {
                var rt = rail.rectTransform;
                rt.anchorMin = new Vector2(0.012f, 0.06f);
                rt.anchorMax = new Vector2(0.028f, 0.94f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _cardRails[pack.Sku] = rail;
            }
            var border = cardGo.GetComponent<Outline>();
            border.effectColor = new Color(light.r, light.g, light.b, 0f);
            border.effectDistance = new Vector2(2f, 2f);
            _cardBorders[pack.Sku] = border;

            Orb(card, orbTint);

            MakeText(card, pack.Name, 24, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, new Vector2(0.24f, 0.58f), new Vector2(0.97f, 0.82f));

            // ONE goods line, sourced from the same describer the spotlight ledger draws from, so
            // the card and the spotlight can never disagree about what a pack contains.
            MakeText(card, DescribeContents(pack), 18, new Color(0.90f, 0.93f, 0.98f, 1f),
                FontStyles.Normal, TextAlignmentOptions.TopLeft,
                new Vector2(0.06f, 0.30f), new Vector2(0.97f, 0.66f));

            // Price block: SKR large, USD small. HOLDS STILL — it is read to decide (Lane G rule 2).
            MakeText(card, pack.AmountLabel(_defaultCurrency), 26, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.BottomLeft, new Vector2(0.06f, 0.04f), new Vector2(0.60f, 0.28f));
            MakeText(card, pack.UsdReference, 18, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.BottomRight, new Vector2(0.60f, 0.04f), new Vector2(0.97f, 0.24f));

            // ── EVERY STATE CARRIES A WORD ───────────────────────────────────
            string flag = CardStateWord(pack);
            if (!string.IsNullOrEmpty(flag))
                MakeText(card, flag, 15, ElarionUi.Gold, FontStyles.Bold,
                    TextAlignmentOptions.TopRight, new Vector2(0.24f, 0.83f), new Vector2(0.97f, 0.97f));
            else if (!string.IsNullOrEmpty(pack.StoreBadge))
                MakeText(card, pack.StoreBadge, 15, ElarionUi.Gold, FontStyles.Bold,
                    TextAlignmentOptions.TopRight, new Vector2(0.24f, 0.83f), new Vector2(0.97f, 0.97f));

            // Tapping the card moves the spotlight. It NEVER buys — the only Buy control on this
            // screen is the spotlight CTA, which runs through PurchaseGate.
            string sku = pack.Sku;
            var btn = cardGo.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => FocusPack(sku, null, animate: true));

            // ⛔ THE CLAMP MUST HAVE NOTHING TO DO. The card is ~225 x 168 reference px — both sides
            // clear MinTouchPx(112) — so this call is a NO-OP and is here to PROVE it is, at the one
            // site where a future re-layout could quietly drop under the floor and start inflating.
            ElarionUiKit.ClampMinTouch(btn);

            return cardGo;
        }

        /// <summary>
        /// The word a card carries for its state — never a colour alone. Empty when the card has no
        /// special state (the store badge is then shown instead).
        /// </summary>
        private string CardStateWord(PackDef pack)
        {
            if (_vm != null && _vm.IsOwned(pack.Sku)) return StoreStrings.Get(StoreStrings.KeyCardOwned);
            if (pack.AnchorOnly) return StoreStrings.Get(StoreStrings.KeyCardAnchor);
            if (!string.IsNullOrEmpty(_pendingShortfallLabel) && pack.Impulse &&
                string.Equals(pack.ImpulseResource, _pendingShortfallLabel, StringComparison.OrdinalIgnoreCase))
                return StoreStrings.Get(StoreStrings.KeyCardGap);
            return string.Empty;
        }

        // =====================================================================
        //  Rendering — the spotlight
        // =====================================================================

        /// <summary>
        /// Which pack the spotlight opens on: the pack that closes a shortfall a caller handed us
        /// (via <see cref="FocusShortfall"/>), else <c>starters-hand</c>, else the first browsable row.
        /// </summary>
        private string ResolveFocusSku()
        {
            if (!string.IsNullOrEmpty(_pendingShortfallLabel) && _pendingShortfallMissing > 0)
            {
                // The ONE new caller of the resolver. It returns a PackDef and nothing else.
                var offer = ShortfallPackOffer.Resolve(_pendingShortfallLabel, _pendingShortfallMissing);
                if (offer.HasOffer && offer.Pack != null)
                {
                    FlowTrace.Step("Store", $"spotlight opens on the shortfall remedy '{offer.Pack.Sku}' " +
                                            $"({_pendingShortfallMissing} {_pendingShortfallLabel} short).");
                    return offer.Pack.Sku;
                }
                FlowTrace.Step("Store", $"no impulse pack resolves {_pendingShortfallMissing} {_pendingShortfallLabel} — " +
                                        "spotlight falls back to the starter pack.");
            }

            if (!string.IsNullOrEmpty(_focusSku)) return _focusSku;

            var starter = PackCatalog.Find("starters-hand");
            if (starter != null && starter.StoreVisible) return starter.Sku;

            foreach (StoreBand band in new[] { StoreBand.Gap, StoreBand.Basket, StoreBand.Patronage })
            {
                var rows = PacksInBand(band);
                if (rows.Count > 0) return rows[0].Sku;
            }
            return null;
        }

        /// <summary>
        /// Moves the spotlight. Repaints the two selection marks, rebuilds the spotlight column, and
        /// (G2) crossfades the aurora from the old band's light to the new one over 400 ms.
        /// </summary>
        private void FocusPack(string sku, Dictionary<string, int> scale, bool animate)
        {
            if (_spotlightHost == null) return;

            _focusSku = sku;
            var pack = string.IsNullOrEmpty(sku) ? null : PackCatalog.Find(sku);
            var band = pack != null ? PackCatalog.BandOf(pack) : StoreBand.Basket;
            var light = NightMarketPalette.For(band);

            // Repaint the marks. Rails carry the band light on the focused card and a dim version
            // elsewhere; the border only appears on the focused one.
            foreach (var kv in _cardRails)
            {
                if (kv.Value == null) continue;
                bool on = string.Equals(kv.Key, sku, StringComparison.Ordinal);
                var c = kv.Value.color;
                c.a = on ? 1f : 0.35f;
                kv.Value.color = c;
            }
            foreach (var kv in _cardBorders)
            {
                if (kv.Value == null) continue;
                bool on = string.Equals(kv.Key, sku, StringComparison.Ordinal);
                var c = kv.Value.effectColor;
                c = new Color(light.r, light.g, light.b, on ? 0.95f : 0f);
                kv.Value.effectColor = c;
            }

            BuildSpotlight(pack, band, scale ?? BuildLedgerScale());

            if (_aurora != null)
            {
                if (animate) _aurora.CrossfadeTo(light);
                else _aurora.SetLightImmediate(light);
            }
        }

        private void BuildSpotlight(PackDef pack, StoreBand band, Dictionary<string, int> scale)
        {
            for (int i = _spotlightHost.childCount - 1; i >= 0; i--)
                Destroy(_spotlightHost.GetChild(i).gameObject);

            Plate(_spotlightHost, NightMarketPalette.GroundRaised);

            var light = NightMarketPalette.For(band);

            // G1 — the rolling-colour moment. TWO offset soft gradients on OPPOSED slow paths, so
            // their sum never repeats. Registered first so they sit behind everything else.
            if (_aurora != null)
            {
                _aurora.AddDrift(_spotlightHost, "aurora-a", new Vector2(0f, 0.52f), new Vector2(1f, 1f),
                    new Color(light.r, light.g, light.b, 0.30f), 22f, new Vector2(1f, 0.35f), followsBandLight: true);
                _aurora.AddDrift(_spotlightHost, "aurora-b", new Vector2(0f, 0.52f), new Vector2(1f, 1f),
                    new Color(light.r, light.g, light.b, 0.18f), 29f, new Vector2(-0.7f, -1f), followsBandLight: true);
            }

            if (pack == null)
            {
                MakeText(_spotlightHost, StoreStrings.Get(StoreStrings.KeySpotlightEmpty), 12,
                    ElarionUi.ParchmentDim, FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.06f, 0.44f), new Vector2(0.94f, 0.60f));
                FlowTrace.Warn("Store", "BuildSpotlight: no focused pack — the spotlight shows its empty line.");
                return;
            }

            var orbTint = NightMarketPalette.ParseTint(pack.OrbTint, light);
            Orb(_spotlightHost, orbTint, new Vector2(0.08f, 0.79f), new Vector2(0.30f, 0.95f));

            if (!string.IsNullOrEmpty(pack.StoreBadge))
                MakeText(_spotlightHost, pack.StoreBadge, 15, ElarionUi.Gold, FontStyles.Bold,
                    TextAlignmentOptions.TopRight, new Vector2(0.40f, 0.90f), new Vector2(0.94f, 0.97f));

            MakeText(_spotlightHost, pack.Name, 26, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.BottomLeft, new Vector2(0.06f, 0.70f), new Vector2(0.94f, 0.80f));
            MakeText(_spotlightHost, pack.Tagline, 17, ElarionUi.Parchment, FontStyles.Italic,
                TextAlignmentOptions.TopLeft, new Vector2(0.06f, 0.575f), new Vector2(0.94f, 0.695f));

            // ── The bar ledger ───────────────────────────────────────────────
            MakeText(_spotlightHost, StoreStrings.Get(StoreStrings.KeyLedgerHeading), 15,
                ElarionUi.ParchmentDim, FontStyles.Bold, TextAlignmentOptions.BottomLeft,
                new Vector2(0.06f, 0.535f), new Vector2(0.94f, 0.575f));
            float ledgerTop = 0.525f, rowH = 0.052f;
            int drawn = 0;
            foreach (string key in PackCatalog.LedgerEconomyKeys)
            {
                int amount = pack.EconomyAmount(key);
                if (amount <= 0) continue;
                int max = scale != null && scale.TryGetValue(key, out int m) && m > 0 ? m : amount;
                float y1 = ledgerTop - drawn * rowH;
                float y0 = y1 - rowH + 0.010f;
                BuildLedgerRow(_spotlightHost, key, amount, max, light, y0, y1);
                drawn++;
                if (drawn >= 6) break;
            }
            if (drawn == 0)
                FlowTrace.Warn("Store", $"BuildSpotlight '{pack.Sku}': the grant seam pays out NOTHING this ledger can draw — " +
                                        "the card advertises no goods at all.");

            float cursor = ledgerTop - drawn * rowH - 0.012f;

            // ── The comparison line ──────────────────────────────────────────
            string compare = BuildComparisonLine(pack);
            if (!string.IsNullOrEmpty(compare))
            {
                MakeText(_spotlightHost, compare, 15, ElarionUi.Parchment, FontStyles.Normal,
                    TextAlignmentOptions.TopLeft, new Vector2(0.06f, cursor - 0.055f), new Vector2(0.94f, cursor));
                cursor -= 0.065f;
            }

            // ── Balance-after preview, above the CTA ─────────────────────────
            // Only when the wallet mirror actually KNOWS a number. Never computed from an assumed
            // balance: "what you will have left" is a promise, and a promise off a guessed figure is
            // the same lie as a fabricated balance.
            if (_balanceState == BalanceState.Known)
            {
                double after = _balanceSkr - pack.AmountFor(CurrencyKind.Skr);
                MakeText(_spotlightHost, StoreStrings.Format(StoreStrings.KeyBalanceAfter, after.ToString("N0")),
                    15, ElarionUi.Parchment, FontStyles.Normal, TextAlignmentOptions.TopLeft,
                    new Vector2(0.06f, cursor - 0.05f), new Vector2(0.94f, cursor));
            }

            BuildSpotlightCta(pack);
        }

        /// <summary>
        /// One ledger row: the good, its NUMBER, and a bar on a scale shared with every other pack.
        /// <para>The number is the truth and the bar is the comparison. The bar keeps a small visible
        /// stub at the bottom of its range so a $1.99 pack next to a $49.99 pack still shows
        /// something — but the stub is never allowed to overstate: the printed figure beside it is
        /// exact, and it is the figure the grant seam pays.</para>
        /// </summary>
        private void BuildLedgerRow(Transform host, string key, int amount, int max, Color light, float y0, float y1)
        {
            var go = new GameObject("ledger-" + key, typeof(RectTransform));
            go.transform.SetParent(host, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, y0); rt.anchorMax = new Vector2(0.94f, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            MakeText(go.transform, key, 14, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(0.26f, 1f));
            MakeText(go.transform, amount.ToString("N0"), 15, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.Right, new Vector2(0.72f, 0f), new Vector2(1f, 1f));

            var track = Plate(go.transform, new Color(1f, 1f, 1f, 0.10f));
            if (track != null)
            {
                var trt = track.rectTransform;
                trt.anchorMin = new Vector2(0.28f, 0.22f); trt.anchorMax = new Vector2(0.70f, 0.72f);
                trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            }
            float frac = max > 0 ? Mathf.Clamp01(amount / (float)max) : 0f;
            frac = Mathf.Max(frac, 0.05f);
            var fill = Plate(go.transform, new Color(light.r, light.g, light.b, 0.85f));
            if (fill != null)
            {
                var frt = fill.rectTransform;
                frt.anchorMin = new Vector2(0.28f, 0.22f);
                frt.anchorMax = new Vector2(0.28f + 0.42f * frac, 0.72f);
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// The per-good maximum across every browsable pack — the SHARED ledger scale, so a bigger
        /// pack is visibly bigger. Recomputed per render because the browsable set can change
        /// (ownership, a data edit) and a cached scale would silently mis-draw every bar.
        /// </summary>
        private Dictionary<string, int> BuildLedgerScale()
        {
            var scale = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string key in PackCatalog.LedgerEconomyKeys) scale[key] = 0;

            foreach (var pack in PackCatalog.Packs)
            {
                if (pack == null || !pack.StoreVisible) continue;
                if (pack.Impulse && !pack.ShelfCurated) continue;
                foreach (string key in PackCatalog.LedgerEconomyKeys)
                {
                    int a = pack.EconomyAmount(key);
                    if (a > scale[key]) scale[key] = a;
                }
            }
            return scale;
        }

        /// <summary>
        /// The comparison line — PURE ARITHMETIC over two real SKUs, or ABSENT.
        ///
        /// <para>⛔ NO ADJECTIVES AND NO INVENTED VALUE INDEX. Two ratios, both computable from the
        /// catalogue: how much more of one good this pack grants than its <c>compareTo</c> row, and
        /// how much more it costs. If <c>compareTo</c> is missing, names a SKU that does not exist,
        /// the two rows share no economy key, or either denominator is zero, this returns EMPTY and
        /// nothing is drawn. A comparison the code cannot compute is a comparison the store does not
        /// make.</para>
        /// </summary>
        private static string BuildComparisonLine(PackDef pack)
        {
            if (pack == null || string.IsNullOrEmpty(pack.CompareTo)) return string.Empty;
            var other = PackCatalog.Find(pack.CompareTo);
            if (other == null || ReferenceEquals(other, pack)) return string.Empty;

            // The shared key with the largest amount in THIS pack — the good the comparison is most
            // honestly about.
            string bestKey = null;
            int bestMine = 0, bestTheirs = 0;
            foreach (string key in PackCatalog.LedgerEconomyKeys)
            {
                int mine = pack.EconomyAmount(key);
                int theirs = other.EconomyAmount(key);
                if (mine <= 0 || theirs <= 0) continue;
                if (mine > bestMine) { bestKey = key; bestMine = mine; bestTheirs = theirs; }
            }
            if (bestKey == null || bestTheirs <= 0) return string.Empty;

            double usdMine = pack.Pricing != null ? pack.Pricing.Usd : 0d;
            double usdTheirs = other.Pricing != null ? other.Pricing.Usd : 0d;
            if (usdMine <= 0d || usdTheirs <= 0d) return string.Empty;

            double goodsRatio = bestMine / (double)bestTheirs;
            double priceRatio = usdMine / usdTheirs;

            return StoreStrings.Format(StoreStrings.KeyCompareLine,
                goodsRatio.ToString("0.#"), other.Name, bestKey, priceRatio.ToString("0.#"));
        }

        /// <summary>
        /// The ONE Buy control on this screen. Same gate, same refusals, same words as before — the
        /// only change is that it lives in the spotlight rather than on every row.
        /// </summary>
        private void BuildSpotlightCta(PackDef pack)
        {
            // ⛔ THE CTA IS AUTHORED AT ~115 px TALL AND FULL COLUMN WIDTH, both over
            // MinTouchPx(112), so ClampMinTouch is a NO-OP. That is the same discipline the 2026-07-16
            // buy-column fix landed: a sub-112 control does not fail, it INFLATES and overlaps.
            var ctaMin = new Vector2(0.06f, 0.025f);
            var ctaMax = new Vector2(0.94f, 0.245f);

            // ── anchorOnly: NO BUY CONTROL IS EVER BUILT ─────────────────────
            // On EITHER side of the purchase flag. The row renders fully priced and simply has no
            // button — it cannot be bought, so it cannot disappoint. NO ROW CARRIES THIS TODAY
            // (see PackDef.AnchorOnly for why: WO-1121 is the same-day ruling that made the top
            // rungs buyable behind the wallet rule, and flagging one here would walk that back).
            if (pack.AnchorOnly)
            {
                MakeText(_spotlightHost, StoreStrings.Get(StoreStrings.KeyCardAnchor), 15,
                    ElarionUi.ParchmentDim, FontStyles.Italic, TextAlignmentOptions.Center, ctaMin, ctaMax);
                FlowTrace.Step("Store", $"BuildSpotlightCta '{pack.Sku}': anchorOnly — NO Buy control built (either side of the flag).");
                return;
            }

            if (_vm.IsOwned(pack.Sku))
            {
                MakeText(_spotlightHost, StoreStrings.Get(StoreStrings.KeyCardOwned), 18,
                    new Color(0.55f, 0.90f, 0.55f, 1f), FontStyles.Bold,
                    TextAlignmentOptions.Center, ctaMin, ctaMax);
                return;
            }

            // A durable submitted payment owns the CTA until reconciliation resolves it.
            // Rendering another Buy face here would invite a duplicate charge.
            if (PurchaseEntitlementVerifier.HasPending(pack.Sku))
            {
                // P0 (device, 2026-08-22): this used to be plain text. The screen instructed the
                // player to "reopen this offer to reconcile", but reopening rebuilt the same inert
                // label, leaving a finalized payment permanently undeliverable. This is deliberately
                // a REAL button which re-enters Purchase(). Purchase() sees HasPending before it can
                // call SendPayment, so this path verifies the recorded receipt and cannot pay twice.
                var reconcile = ElarionUiKit.BuildObsidianButton(_spotlightHost,
                    "Reconcile - no new payment",
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    _purchaseInFlight ? ElarionUiKit.ObsidianButtonColor.Gray
                                      : ElarionUiKit.ObsidianButtonColor.Yellow,
                    ctaMin, ctaMax,
                    () => Purchase(pack, SelectedCurrency(pack.Sku)).Forget());
                if (reconcile != null) reconcile.interactable = !_purchaseInFlight;

                var reconcileLabel = reconcile != null
                    ? reconcile.GetComponentInChildren<TMP_Text>(true)
                    : null;
                if (reconcileLabel != null) ElarionUiKit.FitSingleLine(reconcileLabel, 18f, 24f);
                return;
            }

            // WO-1121: the flag test lives BEHIND PurchaseGate.CanBuy() and is not read here. The
            // gate answers the same question plus two more the raw flag cannot (a flag-ON build whose
            // rail has no resolvable mint; and the owner's 2026-08-21 wallet rule above $4.99) — and,
            // the load-bearing part, PackStore.Purchase() consults the SAME method, so the button and
            // the charge path can never disagree. Re-reading FeatureFlags here would re-open exactly
            // the UI-only gate the ruling forbids.
            if (!PurchaseGate.CanBuy(pack, out string gateReason))
            {
                // Two shapes of refusal, and the difference matters to the player:
                //   * the whole rail is closed  -> nothing they can do, so a plain "Coming soon"
                //     label, never a button that looks tappable and is not (WO-1121 §3.5).
                //   * the WALLET rule is the blocker -> there IS something they can do right now, so
                //     this is a REAL button that starts the connect flow.
                // The state is carried by the WORDS in both cases — the owner is red/green
                // colourblind, so a colour swap alone would not be readable.
                string blockedLabel = PurchaseGate.BlockedCtaLabel(pack);
                bool walletIsTheBlocker = PurchaseGate.WalletIsTheBlocker(pack);

                if (!walletIsTheBlocker)
                {
                    MakeText(_spotlightHost, blockedLabel, 15, ElarionUi.ParchmentDim, FontStyles.Italic,
                        TextAlignmentOptions.Center, ctaMin, ctaMax);
                }
                else
                {
                    string reasonForBanner = gateReason;
                    var connect = ElarionUiKit.BuildObsidianButton(_spotlightHost,
                        blockedLabel,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        _purchaseInFlight ? ElarionUiKit.ObsidianButtonColor.Gray
                                          : ElarionUiKit.ObsidianButtonColor.Yellow,
                        ctaMin, ctaMax,
                        () => ConnectForWalletGate(reasonForBanner).Forget());
                    if (connect != null) connect.interactable = !_purchaseInFlight;

                    var connectLabel = connect != null ? connect.GetComponentInChildren<TMP_Text>(true) : null;
                    if (connectLabel != null) ElarionUiKit.FitSingleLine(connectLabel, 20f, 26f);
                }

                FlowTrace.Step("Store", $"BuildSpotlightCta '{pack.Sku}': Buy REFUSED by PurchaseGate — \"{gateReason}\" " +
                                        $"(face='{blockedLabel}', actionable={walletIsTheBlocker}).");
                return;
            }

            var rail = SelectedCurrency(pack.Sku);   // SKR canary by default (MON-1147)
            if (_wallet != null && _wallet.Network == WalletNetwork.Devnet)
                MakeText(_spotlightHost, "DEVNET - TEST TOKEN", 12, ElarionUi.Gold,
                    FontStyles.Bold, TextAlignmentOptions.Center,
                    new Vector2(0.06f, 0.245f), new Vector2(0.94f, 0.285f));
            var buy = ElarionUiKit.BuildObsidianButton(_spotlightHost,
                $"Buy - {pack.AmountLabel(rail)}",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                _purchaseInFlight ? ElarionUiKit.ObsidianButtonColor.Gray
                                  : ElarionUiKit.ObsidianButtonColor.Yellow,
                ctaMin, ctaMax,
                () => Purchase(pack, SelectedCurrency(pack.Sku)).Forget());
            if (buy != null) buy.interactable = !_purchaseInFlight;

            var buyLabel = buy != null ? buy.GetComponentInChildren<TMP_Text>(true) : null;
            if (buyLabel != null) ElarionUiKit.FitSingleLine(buyLabel, 20f, 26f);

            // G3 — the specular sweep. The one element that must never look asleep. It rides ON the
            // button and carries no information: strip it and the CTA is identical.
            if (buy != null && _aurora != null)
                _aurora.AddSweep(buy.transform, "cta-sweep", Vector2.zero, Vector2.one,
                    new Color(1f, 1f, 1f, 0.55f), 6f, 0.7f);
        }

        private CurrencyKind SelectedCurrency(string sku)
        {
            return _selectedCurrency.TryGetValue(sku, out var c) ? c : _defaultCurrency;
        }

        // =====================================================================
        //  The wallet mirror — READ-ONLY, ASYNC, and never a laundered zero
        // =====================================================================

        /// <summary>
        /// Fills the header chip from the player's OWN wallet. Never blocks the store open: the chip
        /// renders its state first and is refilled when (and if) the reads return.
        ///
        /// <para>⛔ THE ZERO IS AMBIGUOUS AND IS NOT LAUNDERED. <c>balance.Skr == 0</c> can mean the
        /// player genuinely holds none, OR that the SKR mint is unprovisioned on this network
        /// (<c>WalletEndpoints.SkrMint</c> ships EMPTY on both networks today, so the provider's own
        /// comment applies: "leave 0"), OR that the RPC failed (<c>WalletService.GetBalance</c>
        /// catches and returns a zeroed struct). Three different facts. Printing a confident "0 SKR"
        /// would collapse them into one number and tell the player something we do not know — the
        /// same defect class that got keepers-satchel hidden. So: an unconfigured mint is caught
        /// BEFORE the call, and a returned zero renders as UNAVAILABLE rather than as none.
        /// Distinguishing a genuine zero from a failed read needs <c>WalletService.GetBalance</c> to
        /// report success separately; that is a money-path file this WO does not touch.</para>
        /// </summary>
        private async UniTaskVoid RefreshWalletMirror()
        {
            using var _ = FlowTrace.Enter("Store", "RefreshWalletMirror (read-only)");
            _hasFiat = false;

            if (_wallet == null || !_wallet.IsConnected)
            {
                // ⛔ NO WALLET = NO NUMBER. A "0" here would read as "you have none" when the truth
                // is "we did not ask".
                SetBalanceState(BalanceState.NoWallet);
                return;
            }

            string mint = WalletEndpoints.SkrMint(_wallet.Network);
            if (string.IsNullOrEmpty(mint))
            {
                FlowTrace.Step("Store", $"wallet mirror: no SKR mint configured for {_wallet.NetworkLabel} — " +
                                        "showing UNAVAILABLE rather than a zero that would read as 'you have none'.");
                SetBalanceState(BalanceState.Unavailable);
                return;
            }

            SetBalanceState(BalanceState.Checking);

            WalletBalance balance;
            try
            {
                balance = await _wallet.GetBalance();
            }
            catch (Exception ex)
            {
                // No silent catch (§12). The chip degrades; the store is untouched.
                FlowTrace.Fail("Store", $"wallet mirror: GetBalance THREW: {ex.GetType().Name}: {ex.Message} — " +
                                        "chip shows UNAVAILABLE. Nothing else on the screen depends on it.");
                SetBalanceState(BalanceState.Unavailable);
                return;
            }

            if (balance.Skr <= 0d)
            {
                SetBalanceState(BalanceState.Unavailable);
                return;
            }

            _balanceSkr = balance.Skr;
            SetBalanceState(BalanceState.Known);

            await RefreshFiatApproximation();
        }

        /// <summary>
        /// The APPROXIMATE fiat half, from a live Jupiter quote. Silently absent whenever it cannot
        /// be trusted — a wrong dollar figure beside a real balance is worse than no dollar figure.
        ///
        /// <para>Four documented failure modes, all handled: (1) the quote may be taken against the
        /// UNSET SKR mint placeholder, which round-trips a number that means nothing — excluded
        /// transitively, because the fiat half only renders when the BALANCE is Known, and that
        /// requires a configured <c>WalletEndpoints.SkrMint</c>; (2) Jupiter is MAINNET and the
        /// wallet may be on devnet, an expected mismatch in testing that must degrade to SKR-only
        /// quietly, so a null quote is not an error the player sees; (3) it is a network call, so it
        /// never blocks the open — the SKR half is already on screen by the time this runs; (4) a
        /// quote is a MOVING PRICE, so it keeps its tilde and is dropped after
        /// <see cref="FiatStaleSeconds"/> rather than left to rot.</para>
        ///
        /// <para><c>GetQuoteAsync</c> already emits its own FlowTrace on entry, warn and failure —
        /// no parallel trace is added here.</para>
        /// </summary>
        private async UniTask RefreshFiatApproximation()
        {
            var jupiter = DeNelle.Core.CoreServices.Jupiter;
            if (jupiter == null) return;

            try
            {
                // 1 USDC in, quote out: Rate is "1 input token = N SKR", and USDC is dollar-pegged,
                // so USD ~= SKR / Rate. One quote covers the whole chip; no per-pack quoting.
                var task = jupiter.GetQuoteAsync(SwapInputToken.USDC, 1m);
                if (task == null) return;
                SwapQuote quote = await task;
                if (quote == null || quote.Rate <= 0m) return;

                _fiatUsd = _balanceSkr / (double)quote.Rate;
                _hasFiat = true;
                _fiatAtRealtime = Time.realtimeSinceStartup;
                RenderBalanceLabel();
            }
            catch (Exception ex)
            {
                // Degrade to SKR-only. This is an EXPECTED path (mainnet quote vs devnet wallet), so
                // it is a Step, not a Fail — but it is never silent.
                FlowTrace.Step("Store", $"wallet mirror: no fiat approximation ({ex.GetType().Name}) — showing SKR only.");
                _hasFiat = false;
            }
        }

        private void SetBalanceState(BalanceState state)
        {
            _balanceState = state;
            RenderBalanceLabel();
        }

        private void RenderBalanceLabel()
        {
            if (_balanceLabel == null) return;

            switch (_balanceState)
            {
                case BalanceState.NoWallet:
                    if (_wallet != null && _wallet.Account.IsValid)
                        _balanceLabel.text = $"Wallet {Shorten(_wallet.Account.Address)} bound - authorize to purchase";
                    else if (PurchaseGate.HasDurableIdentity)
                        _balanceLabel.text = "Wallet identity bound - authorize to purchase";
                    else
                        _balanceLabel.text = StoreStrings.Get(StoreStrings.KeyBalanceNoWallet);
                    return;
                case BalanceState.Checking:
                    _balanceLabel.text = StoreStrings.Get(StoreStrings.KeyBalanceChecking);
                    return;
                case BalanceState.Unavailable:
                    _balanceLabel.text = StoreStrings.Get(StoreStrings.KeyBalanceUnavailable);
                    return;
            }

            string identity = _wallet != null && _wallet.Account.IsValid
                ? Shorten(_wallet.Account.Address) + "  " + _wallet.NetworkLabel + "  SKR"
                : (_wallet != null ? _wallet.NetworkLabel + "  SKR" : "SKR");
            string text = identity + "\n" + StoreStrings.Format(StoreStrings.KeyBalanceValue, _balanceSkr.ToString("N0"));
            bool fresh = _hasFiat && (Time.realtimeSinceStartup - _fiatAtRealtime) < FiatStaleSeconds;
            if (fresh)
                text += "  " + StoreStrings.Format(StoreStrings.KeyBalanceFiat, _fiatUsd.ToString("N2"));
            else if (_hasFiat)
                _hasFiat = false;   // stale: drop it rather than show a price that has moved
            _balanceLabel.text = text;
        }

        // =====================================================================
        //  Contents description (shared by the card line and the ledger's honesty)
        // =====================================================================

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
                    // ⚠ Wood and Iron were MISSING from this list while PackStoreVM.ApplyPackContents
                    // has granted them since ECON-01. Describe every key the grant seam actually pays
                    // out, in grant order — the same rule PackCatalog.LedgerEconomyKeys encodes for
                    // the spotlight bars.
                    AppendAmount(sb, econ.Wood, "wood");
                    AppendAmount(sb, econ.Iron, "iron");
                    AppendAmount(sb, econ.Crystals, "crystals");
                    AppendAmount(sb, econ.Food, "food");
                    AppendAmount(sb, econ.Coins, "coins");
                    AppendAmount(sb, econ.Glimmer, "glimmer");
                }

                // WO-1118 §2.3 — a card may only list convenience the player can actually SPEND.
                // The redeemable set is PackCatalog.IsRedeemableConvenience (single source).
                // Legal-but-unredeemable kinds are still GRANTED into GearInventory — they are
                // simply not advertised until a redeemer ships.
                if (c.Convenience != null && c.Convenience.Count > 0)
                {
                    foreach (var item in c.Convenience)
                    {
                        if (item == null || item.Count <= 0 || string.IsNullOrEmpty(item.Kind)) continue;
                        if (!PackCatalog.IsRedeemableConvenience(item.Kind)) continue;
                        if (sb.Length > 0) sb.Append(", ");
                        if (item.Kind.IndexOf("lantern", StringComparison.OrdinalIgnoreCase) >= 0)
                            sb.Append(item.Kind.Contains("3x") ? "3x" : "2x")
                              .Append(" lantern x").Append(item.Count).Append(" runs");
                        else
                            sb.Append(item.Count).Append("x ").Append(item.Kind.Replace('-', ' ').Replace('_', ' '));
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        /// <summary>Exact receipt inventory, including grants intentionally omitted from shelf copy.</summary>
        private static string DescribeGrantedContents(PackDef pack)
        {
            var sb = new StringBuilder();
            var c = pack != null ? pack.Contents : null;
            if (c == null) return "No contents recorded";

            var econ = c.Economy;
            if (econ != null)
            {
                AppendAmount(sb, econ.Wood, "wood");
                AppendAmount(sb, econ.Iron, "iron");
                AppendAmount(sb, econ.Crystals, "crystals");
                AppendAmount(sb, econ.Food, "food");
                AppendAmount(sb, econ.Coins, "coins");
                AppendAmount(sb, econ.Glimmer, "glimmer");
            }
            if (c.Cosmetics != null)
                foreach (string sku in c.Cosmetics)
                {
                    if (string.IsNullOrWhiteSpace(sku)) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append("cosmetic ").Append(sku);
                }
            if (c.Convenience != null)
                foreach (var item in c.Convenience)
                {
                    if (item == null || item.Count <= 0 || string.IsNullOrWhiteSpace(item.Kind)) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(item.Count).Append("x ").Append(item.Kind.Replace('-', ' ').Replace('_', ' '));
                }
            return sb.Length > 0 ? sb.ToString() : "No item grants";
        }

        private static void AppendAmount(StringBuilder sb, int amount, string label)
        {
            if (amount <= 0) return;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(amount.ToString("N0")).Append(' ').Append(label);
        }

        // =====================================================================
        //  Purchase flow — UNCHANGED (WO-1050 is presentation only)
        // =====================================================================

        /// <summary>
        /// Runs the full purchase flow for a pack: ensures a wallet is connected,
        /// calls <see cref="WalletService.Pay"/>, awaits devnet confirmation, then
        /// applies the pack contents to <see cref="GameStateService"/>.
        /// </summary>
        public async UniTask<PaymentResult> Purchase(PackDef pack, CurrencyKind currency)
        {
            // ⛔ WO-1149 — STOP THE WORLD FOR THE WHOLE TRANSACTION. Owner, on device 2026-08-22:
            // "we need to stop game during transactions got killed while making purchase test."
            // A purchase is not instant (wallet signs -> chain confirms -> server verifies ->
            // entitlement recorded -> grant -> save verifies), and for all of it the player could
            // neither defend themselves nor cancel without abandoning a transaction that may already
            // have been signed. That is the worst possible moment to force a choice between dying
            // and losing money.
            //
            // ⛔ THIS LINE IS FIRST IN THE METHOD ON PURPOSE, AND IT IS A `using` ON PURPOSE.
            // A hold that fails to release is WORSE than no hold — a frozen game after a completed
            // purchase is a support ticket AND a refund. Placed here, the C# compiler covers EVERY
            // exit below without anyone remembering to: the null-pack guard, the PurchaseGate
            // refusal, the devnet-canary refusal, the already-in-flight and already-owned returns,
            // the wallet-connect cancellation, all four verification outcomes, the pay failure, the
            // catch, and any branch added after this comment was written. Do not convert it to a
            // paired Acquire()/Dispose() — that is exactly the shape that leaves three branches out.
            //
            // (The one exit a `using` cannot cover — the app backgrounded mid-flight and an await
            // that never resumes — is covered by WorldHold's stuck-hold watchdog.)
            //
            // Whole-simulation scope, not combat-only: WorldHold zeroes Time.timeScale, which is the
            // proven, already-wired freeze the pause menu uses. It also stops build/queue timers the
            // player may be watching — accepted, because a transaction is short and bounded while
            // "killed mid-purchase" is unrecoverable.
            using var worldHold = DeNelle.Core.UI.WorldHold.Acquire(DeNelle.Core.UI.WorldHold.ReasonPurchase);
            using var _ = FlowTrace.Enter("Store", $"Purchase pack='{pack?.Sku ?? "<null>"}' {currency}");

            if (pack == null)
            {
                FlowTrace.Fail("Store", "Purchase: pack is null — aborted.");
                return PaymentResult.Failure(string.Empty, currency, "Pack is null.");
            }

#if MAINNET_CANARY_TEST
            bool isMainnetCanary = string.Equals(pack.Sku, PurchaseGate.MainnetCanarySku,
                StringComparison.Ordinal);
            if (isMainnetCanary)
            {
                // MWA authorization is chain-scoped. Reconnect after selecting Mainnet instead of
                // reusing an earlier Devnet association for this real-value canary.
                if (_wallet.IsConnected) await _wallet.Disconnect();
                _wallet.SetNetwork(WalletNetwork.Mainnet);
            }
#endif

            // ⛔ THE GATE, ON THE CHARGE PATH ITSELF (WO-1121). Defense-in-depth is the weak reading
            // of this check; the strong one is that THIS is where the rule is actually enforced. The
            // CTA builder calls the same PurchaseGate.CanBuy(pack, ...) and can only ever REMOVE a
            // button — but Purchase() is public and is reached from surfaces that never drew that
            // button (a deep link, a future promo). A rule enforced only in the UI is not enforced.
            //
            // The refusal text is the gate's PLAYER-READABLE reason, surfaced on the status banner and
            // returned as the PaymentResult error. Nothing has been charged at this point.
            if (!PurchaseGate.CanBuy(pack, out string gateReason))
            {
                FlowTrace.Warn("Store", $"Purchase '{pack.Sku}' REFUSED at PurchaseGate — \"{gateReason}\" (nothing charged).");
                SetStatus(gateReason);
                return PaymentResult.Failure(pack.Sku, currency, gateReason);
            }

            if (_wallet.Network == WalletNetwork.Devnet && currency != CurrencyKind.Skr)
            {
                const string skrCanaryOnly = "Today's verified devnet purchase uses test SKR. No other rail was charged.";
                FlowTrace.Warn("Store", $"Purchase '{pack.Sku}' refused: MON-1147 devnet canary is SKR-only.");
                SetStatus(skrCanaryOnly);
                return PaymentResult.Failure(pack.Sku, currency, skrCanaryOnly);
            }

            if (_purchaseInFlight)
            {
                SetCommerceState(CommerceState.Delayed, "A purchase is already in progress. Do not pay again.");
                return PaymentResult.Failure(pack.Sku, currency, "Purchase already in progress.");
            }

            if (_vm.IsOwned(pack.Sku))
            {
                SetCommerceState(CommerceState.Fulfilled, $"{pack.Name} is already in your collection.");
                return PaymentResult.Failure(pack.Sku, currency, "Already owned.");
            }

            _purchaseInFlight = true;
            Render();
            try
            {
                // The USDC / SOL flow (§7.4): wallet must be connected first.
                if (!_wallet.IsConnected)
                {
                    SetCommerceState(CommerceState.OpeningWallet);
                    var account = await _wallet.Connect();
                    if (!account.IsValid)
                    {
                        FlowTrace.Warn("Store", $"Purchase '{pack.Sku}': wallet connect cancelled/failed — aborted (player NOT charged).");
                        SetCommerceState(CommerceState.Cancelled,
                            "Wallet did not respond. Check that it is installed, then retry.");
                        return PaymentResult.Failure(pack.Sku, currency, "Wallet not connected.");
                    }
                }

#if MAINNET_CANARY_TEST
                if (isMainnetCanary &&
                    !string.Equals(_wallet.Account.Address, MainnetCanaryCatalog.OwnerWallet,
                        StringComparison.Ordinal))
                {
                    const string ownerOnly = "Mainnet Verification is restricted to the owner test wallet.";
                    SetCommerceState(CommerceState.Failed, ownerOnly);
                    return PaymentResult.Failure(pack.Sku, currency, ownerOnly);
                }
#endif

#if STORE_RAIL_LOCAL_TEST
                // One-time recovery of the owner's first successful Devnet canary. The transfer
                // finalized while the old client was frozen in scaled-time confirmation, before it
                // could persist the returned signature. Production builds do not compile this block.
                // /verify still re-checks the full finalized transaction contract and remains the
                // entitlement authority; this only restores the receipt the client failed to save.
                if (!PurchaseEntitlementVerifier.HasPending(pack.Sku) &&
                    string.Equals(pack.Sku, PurchaseGate.DevnetCanarySku, StringComparison.Ordinal) &&
                    string.Equals(_wallet.Account.Address,
                        "CHKKFkPGz8VZfjpsZjJTqfAUW7vMpdNkkqCVuCcZsfkC", StringComparison.Ordinal))
                {
                    const string recoveredSignature =
                        "5FA9ygfVAiDQKywjM7WaZADGhjA6QJCUhGdKgDGNCBKWhfuxtjpBRDpFnrQzSpAsx72HT9LvdT9vLn9NLZJyyGGX";
                    var recoveredPayment = PaymentResult.Success(pack.Sku, CurrencyKind.Skr,
                        pack.AmountFor(CurrencyKind.Skr), recoveredSignature);
                    PurchaseEntitlementVerifier.Remember(pack, recoveredPayment, _wallet);
                    FlowTrace.Warn("Store", "Recovered finalized Devnet canary receipt 5FA9...yyGGX; verifying without a second payment.");
                }
#endif

                // Ask the durable authority before a new charge. This is the reinstall/new-device
                // restore path: local PlayerPrefs may be empty while the server still owns proof.
                if (!PurchaseEntitlementVerifier.HasPending(pack.Sku))
                {
                    SetCommerceState(CommerceState.Verifying,
                        $"Checking {_wallet.NetworkLabel} for an existing entitlement. No new payment is being requested.");
                    var durable = await PurchaseEntitlementVerifier.ReconcileAsync(pack, _wallet);
                    if (durable.State == EntitlementVerificationState.Fulfilled)
                    {
                        var restored = PaymentResult.Success(pack.Sku, currency,
                            pack.AmountFor(currency), durable.TransactionSignature);
                        if (await RestoreFulfilledOwnershipAsync(pack, restored)) return restored;
                        return Indeterminate(restored,
                            "Your fulfilled purchase was found, but ownership restore is pending.");
                    }
                    if (durable.State == EntitlementVerificationState.Verified)
                    {
                        var restored = PaymentResult.Success(pack.Sku, currency,
                            pack.AmountFor(currency), durable.TransactionSignature);
                        if (await CompleteVerifiedPurchaseAsync(pack, restored)) return restored;
                        return Indeterminate(restored,
                            "Your purchase was found, but delivery is pending. Reopen the store to retry.");
                    }
                }

                // Recovery is checked before a new charge. A submitted signature survives process
                // death; reconnecting the paying wallet resumes verification instead of paying twice.
                if (PurchaseEntitlementVerifier.HasPending(pack.Sku))
                {
                    SetCommerceState(CommerceState.Delayed,
                        "Recovering the recorded payment. Do not pay again.");
                    var recovered = await PurchaseEntitlementVerifier.VerifyPendingAsync(pack, currency, _wallet);
                    if (recovered.State == EntitlementVerificationState.Fulfilled)
                    {
                        var restored = PaymentResult.Success(pack.Sku, currency,
                            pack.AmountFor(currency), recovered.TransactionSignature);
                        if (await RestoreFulfilledOwnershipAsync(pack, restored)) return restored;
                        return Indeterminate(restored,
                            "Your fulfilled purchase was found, but ownership restore is pending.");
                    }
                    if (recovered.State == EntitlementVerificationState.Verified)
                    {
                        var restored = PaymentResult.Success(pack.Sku, currency,
                            pack.AmountFor(currency), recovered.TransactionSignature);
                        if (await CompleteVerifiedPurchaseAsync(pack, restored)) return restored;
                        return Indeterminate(restored,
                            "Payment verified, but delivery is pending. Reopen the store to retry.");
                    }

                    string recoveryMessage = recovered.State == EntitlementVerificationState.Pending
                        ? "Payment found; waiting for final verification. No second charge was made."
                        : "The pending payment needs support review. No second charge was made.";
                    SetCommerceState(CommerceState.Delayed, recoveryMessage);
                    return PaymentResult.Failure(pack.Sku, currency, recoveryMessage);
                }

                SetCommerceState(CommerceState.AwaitingApproval,
                    $"{pack.Name}: {pack.AmountLabel(currency)} on {_wallet.NetworkLabel}. Human approval has no countdown.");
                var result = await _wallet.Pay(pack, currency);

                // A wallet-signed receipt exists before RPC transport completes. Persist it even
                // when submission response is ambiguous; any retry must reconcile, never pay again.
                if (!string.IsNullOrEmpty(result.TxSignature))
                    PurchaseEntitlementVerifier.Remember(pack, result, _wallet);

                if (result.Ok)
                {
#if false
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
#endif
                    // Chain confirmation alone is not entitlement authority. Persist first so a
                    // crash cannot strand the charge, then require independent backend proof.
                    SetCommerceState(CommerceState.Submitted, $"Transaction {Shorten(result.TxSignature)}.");
                    SetCommerceState(CommerceState.Verifying,
                        $"Transaction {Shorten(result.TxSignature)}. Do not pay again.");
                    var verified = await PurchaseEntitlementVerifier.VerifyPendingAsync(pack, currency, _wallet);
                    if (verified.State == EntitlementVerificationState.Fulfilled)
                    {
                        if (!await RestoreFulfilledOwnershipAsync(pack, result))
                            return Indeterminate(result,
                                "Fulfilled payment found, but ownership restore is pending.");
                    }
                    else if (verified.State == EntitlementVerificationState.Verified)
                    {
                        if (!await CompleteVerifiedPurchaseAsync(pack, result))
                            return Indeterminate(result,
                                "Payment verified, but delivery is pending. Reopen the store to retry.");
                    }
                    else
                    {
                        string pending = verified.State == EntitlementVerificationState.Pending
                            ? "Payment submitted; verification is pending. Reopen the store to resume - do not pay again."
                            : "Payment recorded but could not be verified. Contact support with the transaction receipt.";
                        FlowTrace.Warn("Store", $"Purchase '{pack.Sku}' tx {Shorten(result.TxSignature)}: {pending}");
                        SetCommerceState(CommerceState.Delayed,
                            $"Transaction {Shorten(result.TxSignature)}. {pending}");
                        return Indeterminate(result, pending);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(result.TxSignature))
                    {
                        SetCommerceState(CommerceState.Delayed,
                            $"Transaction {Shorten(result.TxSignature)} has an unknown submission outcome. " +
                            "It is recorded for reconciliation; do not pay again.");
                        return result;
                    }
                    FlowTrace.Fail("Store", $"Purchase '{pack.Sku}' ({currency}) FAILED: {result.Error}");
                    SetCommerceState(CommerceState.Failed,
                        "Reopen the store to reconcile before trying another payment.");
                }
                return result;
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("Store",
                    $"Purchase '{pack.Sku}' ({currency}) THREW: {ex.GetType().Name}: {ex.Message} — outcome indeterminate; if a charge settled the entitlement may be lost.");
                SetCommerceState(CommerceState.Failed,
                    "Reopen the store to reconcile before trying another payment.");
                return PaymentResult.Failure(pack.Sku, currency, ex.Message);
            }
            finally
            {
                _purchaseInFlight = false;
                Render();
                RefreshWalletMirror().Forget();
            }
        }

        private static PaymentResult Indeterminate(PaymentResult paid, string error)
        {
            paid.Ok = false;
            paid.Error = error;
            return paid;
        }

        private async UniTask<bool> RestoreFulfilledOwnershipAsync(PackDef pack, PaymentResult payment)
        {
            if (pack == null || string.IsNullOrEmpty(payment.TxSignature)) return false;
            // The server says consumables were already delivered. Restore only durable ownership;
            // replaying economy/convenience here would make reinstall an infinite-grant exploit.
            _vm.RestoreFulfilledOwnership(pack);
            if (!_vm.IsOwned(pack.Sku)) return false;
            PurchaseGate.TryClaimGrant(payment.TxSignature);
            await PurchaseEntitlementVerifier.MarkFulfilledAsync(
                pack.Sku, payment.TxSignature, _wallet);
            SetCommerceState(CommerceState.Fulfilled,
                $"{pack.Name} ownership restored. Transaction {Shorten(payment.TxSignature)}.");
            return true;
        }

        /// <summary>Exactly-once local fulfilment after the backend created a durable entitlement.</summary>
        private async UniTask<bool> CompleteVerifiedPurchaseAsync(PackDef pack, PaymentResult payment)
        {
            if (pack == null || string.IsNullOrEmpty(payment.TxSignature)) return false;

            if (!PurchaseGate.TryClaimGrant(payment.TxSignature))
            {
                if (_vm.IsOwned(pack.Sku))
                {
                    bool durablyFulfilled = await PurchaseEntitlementVerifier.MarkFulfilledAsync(
                        pack.Sku, payment.TxSignature, _wallet);
                    if (!durablyFulfilled) return false;
                    ShowFulfillmentReceipt(pack, payment);
                    return true;
                }
                PurchaseGate.ReportGrantFailed(payment.TxSignature,
                    $"ledger claimed but pack '{pack.Sku}' is not owned");
                return false;
            }

            SetCommerceState(CommerceState.Delivering,
                $"Transaction {Shorten(payment.TxSignature)}.");
            _vm.ApplyPackContents(pack);
            if (!_vm.IsOwned(pack.Sku))
            {
                PurchaseGate.ReportGrantFailed(payment.TxSignature,
                    $"pack '{pack.Sku}' was not owned after ApplyPackContents");
                return false;
            }

            FlowTrace.Step("Store", $"Purchase '{pack.Sku}' backend-verified; tx {payment.TxSignature}, contents applied once.");
            bool durableFulfillmentSucceeded = await PurchaseEntitlementVerifier.MarkFulfilledAsync(
                pack.Sku, payment.TxSignature, _wallet);
            if (!durableFulfillmentSucceeded)
            {
                PurchaseGate.ReportGrantFailed(payment.TxSignature,
                    $"pack '{pack.Sku}' was locally owned but durable fulfillment did not acknowledge");
                return false;
            }
            ShowFulfillmentReceipt(pack, payment);
            PackPurchased?.Invoke(pack, payment);
            DeNelle.Core.Analytics.EventTracker.Track("purchase_completed", new
            {
                packId = pack.Sku,
                packName = pack.Name,
                currency = payment.Currency.ToString(),
                txSig = payment.TxSignature,
                authority = "server_verified_entitlement"
            });
            return true;
        }

        private void ShowFulfillmentReceipt(PackDef pack, PaymentResult payment)
        {
            string receipt = $"{pack.Name} received\n{DescribeGrantedContents(pack)}";
            SetCommerceState(CommerceState.Fulfilled,
                $"{receipt}\nTransaction {Shorten(payment.TxSignature)}.");
            // Shared HUD feedback. Wallet cannot reference Village's world-space resource popup;
            // ApplyPackContents already raises the established resource change notifications.
            ElarionUiKit.ShowToast(receipt, ElarionUiKit.ToastTone.Confirm,
                lifeSeconds: 5f, cardWidth: 760f, cardHeight: 132f);
        }

        /// <summary>
        /// The remedy behind the "Connect Wallet" face on a pack the owner's price rule has gated
        /// (WO-1121). It CHARGES NOTHING — it only runs the connect handshake and re-renders.
        ///
        /// <para>Deliberately NOT "connect, then immediately buy": the player tapped a button that
        /// said Connect Wallet, and auto-charging them for a $49.99 pack off that tap would be a
        /// purchase they never confirmed.</para>
        /// </summary>
        private async UniTaskVoid ConnectForWalletGate(string reason)
        {
            using var _ = FlowTrace.Enter("Store", "ConnectForWalletGate");
            SetCommerceState(CommerceState.OpeningWallet, reason);
            try
            {
                if (_wallet != null && !_wallet.IsConnected)
                {
                    var account = await _wallet.Connect();
                    if (!account.IsValid)
                    {
                        FlowTrace.Warn("Store", "wallet-gate connect cancelled/failed — pack stays gated (nothing charged).");
                        SetCommerceState(CommerceState.Cancelled,
                            "Wallet did not respond. Check that it is installed, then retry. No payment was requested.");
                        return;
                    }
                }

                if (PurchaseGate.HasDurableIdentity)
                    FlowTrace.Step("Store", "wallet-gate satisfied — the higher tiers are now buyable on this save.");
                else
                    SetCommerceState(CommerceState.Failed, reason);
            }
            catch (Exception ex)
            {
                // No silent catch (§12) — a swallowed throw here looks to the player like a button
                // that does nothing, on the one screen where that reads as dishonest.
                FlowTrace.Fail("Store", $"ConnectForWalletGate THREW: {ex.GetType().Name}: {ex.Message} — " +
                                        "nothing was charged; the pack stays gated.");
                SetCommerceState(CommerceState.Failed,
                    "Wallet authorization failed before any payment request. Retry authorization when ready.");
            }
            finally
            {
                Render();
                RefreshWalletMirror().Forget();
            }
        }

        private void SetStatus(string message)
        {
            if (_statusBanner != null) _statusBanner.text = message;
            else FlowTrace.Warn("Store", $"SetStatus (no banner element): {message}");
        }

        private void SetCommerceState(CommerceState state, string detail = null)
        {
            _commerceState = state;
            _commerceDetail = detail ?? string.Empty;
            _commerceStateSince = Time.realtimeSinceStartup;
            RenderCommerceStatus();
        }

        private void RenderCommerceStatus(string temporaryDetail = null)
        {
            string key;
            switch (_commerceState)
            {
                case CommerceState.OpeningWallet: key = StoreStrings.KeyCommerceOpeningWallet; break;
                case CommerceState.AwaitingApproval: key = StoreStrings.KeyCommerceAwaitingApproval; break;
                case CommerceState.Submitted: key = StoreStrings.KeyCommerceSubmitted; break;
                case CommerceState.Verifying: key = StoreStrings.KeyCommerceVerifying; break;
                case CommerceState.Delivering: key = StoreStrings.KeyCommerceDelivering; break;
                case CommerceState.Fulfilled: key = StoreStrings.KeyCommerceFulfilled; break;
                case CommerceState.Cancelled: key = StoreStrings.KeyCommerceCancelled; break;
                case CommerceState.Failed: key = StoreStrings.KeyCommerceFailed; break;
                case CommerceState.Delayed: key = StoreStrings.KeyCommerceDelayed; break;
                default: key = StoreStrings.KeyCommerceReady; break;
            }

            string headline = _commerceState == CommerceState.Verifying
                ? StoreStrings.Format(key, _wallet != null ? _wallet.NetworkLabel : "network")
                : StoreStrings.Get(key);
            string detail = temporaryDetail ?? _commerceDetail;
            SetStatus(string.IsNullOrEmpty(detail) ? headline : headline + "\n" + detail);
        }

        private void RestorePendingPresentation()
        {
            foreach (var pack in PackCatalog.Packs)
            {
                if (pack == null || !PurchaseEntitlementVerifier.HasPending(pack.Sku)) continue;
                SetCommerceState(CommerceState.Delayed,
                    $"{pack.Name} has a recorded payment. Reopen this offer to reconcile; do not pay again.");
                return;
            }
            if (!_purchaseInFlight && _commerceState == CommerceState.Ready)
                RenderCommerceStatus();
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

        /// <summary>A flat, non-interactive colour plate filling its parent. Never eats a tap.</summary>
        private static Image Plate(Transform parent, Color color)
        {
            if (parent == null) return null;
            var go = new GameObject("plate", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            go.transform.SetAsFirstSibling();
            return img;
        }

        /// <summary>The card gem. DECORATION: it never carries a state or a meaning by itself.</summary>
        private static void Orb(Transform parent, Color tint)
            => Orb(parent, tint, new Vector2(0.05f, 0.68f), new Vector2(0.20f, 0.94f));

        private static void Orb(Transform parent, Color tint, Vector2 min, Vector2 max)
        {
            if (parent == null) return;
            var go = new GameObject("orb", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            var sprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, "slot_item");
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
            img.color = new Color(tint.r, tint.g, tint.b, 0.85f);
            img.raycastTarget = false;
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
