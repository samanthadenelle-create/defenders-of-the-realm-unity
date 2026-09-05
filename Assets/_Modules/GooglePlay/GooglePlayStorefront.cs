// =============================================================================
// GooglePlayStorefront - the ONE PanelId.RealmStore registrar in a GOOGLE_PLAY artifact.
// -----------------------------------------------------------------------------
// WO-1395 (2026-09-05). The WO was minted on the premise that this class and
// PackStoreBootstrap both register PanelId.RealmStore in the SAME build and race on
// static-init order. Proven false at source: DeNelle.Wallet.asmdef carries
// defineConstraints ["!GOOGLE_PLAY"] (WO-1282 Lane B, commit c06a66de5) and
// DeNelle.GooglePlay.asmdef carries ["GOOGLE_PLAY"], so exactly ONE of the two
// registrars is compiled into any artifact and they can never coexist. This is the
// Play build's whole store, not a second store beside the Night Market - the Night
// Market (PackStore, DeNelle.Wallet) does not exist in this artifact at all.
//
// WHAT WAS REAL in the WO's finding, and what this file now closes:
//   * a door-tagged open (PanelRouter.Open(RealmStore, "settings"|"vendor")) fell back
//     to the plain opener here because no Action<string> was registered, so the WO-1388
//     funnel's store_opened {door} was never recorded under Play. Both call shapes now
//     land on THIS one modal and the door is latched exactly as PackStoreBootstrap does.
//   * registration was untraced and the open was unguarded. Both are now [Flow:Store].
//   * a second registrar in the same build is now DETECTED (PanelRouter.IsRegistered
//     before we register -> FlowTrace.Fail) instead of silently replaced.
// Pinned by Assets/Editor/Regression/RealmStoreSingleRegistrarRegression.cs.
// =============================================================================

using System;
using DeNelle.Commerce;          // StoreFocusRequest - the rail-neutral door latch (WO-1388)
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Payments;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;

namespace DeNelle.GooglePlay
{
    internal sealed class GooglePlayStorefront : MonoBehaviour
    {
        private static GooglePlayStorefront _active;
        private GooglePlayStorefrontVM _vm;
        private ElarionUiKit.ObsidianModal _modal;
        private TextMeshProUGUI _status;
        private PanelHandle _panelHandle;
        private bool _open;

        /// <summary>The door the funnel records for a plain (context-free) open. Mirrors
        /// PackStore.DoorHudCard: every remaining plain open is the HUD Night Market card.</summary>
        private const string DoorHudCard = "hud-card";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRoute()
        {
            // WO-1395 - a registrar that finds the id already taken is the collision the WO
            // feared. It cannot happen while the asmdef constraints hold (see header); if it
            // ever does, say so instead of letting PanelRouter.Register replace it silently.
            if (PanelRouter.IsRegistered(PanelId.RealmStore))
                FlowTrace.Fail("Store",
                    "second PanelId.RealmStore registrar detected: GooglePlayStorefront found the id already " +
                    "registered at BeforeSceneLoad. Exactly one storefront may register this id per artifact " +
                    "(DeNelle.Wallet is !GOOGLE_PLAY, DeNelle.GooglePlay is GOOGLE_PLAY).");

            PanelRouter.Register(PanelId.RealmStore, Open);
            // The CONTEXT opener (WO-1388 door funnel): a caller that names its door lands on the
            // SAME modal as a plain open, with the door latched for store_opened {door}.
            PanelRouter.Register(PanelId.RealmStore, (Action<string>)OpenFromDoor);
            FlowTrace.Step("Store",
                "RealmStore registrar=GooglePlayStorefront skin=play channel=" + PaymentChannelResolver.ResolveStampedChannel() +
                " (plain + door context; DeNelle.Wallet/PackStore is compiled out of this GOOGLE_PLAY artifact).");
        }

        /// <summary>The door-naming open (PanelRouter context opener). Latches the door for the
        /// funnel, then opens exactly as the plain <see cref="Open"/>.</summary>
        private static void OpenFromDoor(string door)
        {
            FlowTrace.Step("Store", "GooglePlayStorefront.OpenFromDoor door='" + (door ?? "<null>") + "'.");
            StoreFocusRequest.RequestDoor(door);
            Open();
        }

        private static void Open()
        {
            using var _ = FlowTrace.Enter("Store", "GooglePlayStorefront.Open");
            if (_active == null)
            {
                bool built = Guard.Try("Store", "build the Google Play storefront host", () =>
                {
                    var canvas = ElarionUiKit.BuildModalCanvas("GooglePlayStoreHost", 31000);
                    _active = canvas.AddComponent<GooglePlayStorefront>();
                    _active.Build();
                });
                if (!built || _active == null)
                {
                    FlowTrace.Fail("Store", "GooglePlayStorefront.Open: host build failed - the store did NOT open.");
                    return;
                }
                FlowTrace.Step("Store", "GooglePlayStorefront: host spawned (first open).");
            }
            // WO-1388 funnel step 1 - store_opened {door}, the one emit site in THIS artifact
            // (PackStore.TrackStoreOpened is the one in a DAPP_STORE artifact; never both compiled).
            TrackStoreOpened();
            _active.SetOpen(true);
        }

        /// <summary>store_opened {door}: a named door comes from the latch; a plain open is the HUD card.</summary>
        private static void TrackStoreOpened()
        {
            string named = StoreFocusRequest.ConsumeDoor();
            string door = named ?? DoorHudCard;
            FlowTrace.Step("Store", "funnel store_opened door=" + door + (named == null ? " (inferred)" : " (named)") + " [play].");
            Guard.Try("Store", "track store_opened",
                () => DeNelle.Core.Analytics.EventTracker.Track("store_opened", new { door }));
        }

        private void Awake()
        {
            _vm = GooglePlayStorefrontVM.CreateDefault(SetStatus);
            _panelHandle = PanelManager.Register("Google Play Realm Store", Close, () => _open);
        }

        private void OnDestroy()
        {
            if (_active == this) _active = null;
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
        }

        private void Build()
        {
            // WO-1398: the Play skin titles itself with the store's ONE canon name (storeWordmark),
            // the same words the HUD card that opened it rendered - never a typed literal.
            _modal = ElarionUiKit.BuildObsidianModal("GooglePlayRealmStore", HudStrings.StoreFaceLabel("play-skin"),
                new Vector2(.08f, .04f), new Vector2(.92f, .96f), Close,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "shop");
            var body = _modal.chrome.layout != null && _modal.chrome.layout.body != null
                ? _modal.chrome.layout.body.transform : _modal.chrome.content.transform;
            ElarionUiKit.Label(body, "Secure purchases through Google Play", .92f, .99f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TextAlignmentOptions.Center, .02f, .98f);

            var rows = _vm.Rows;
            float top = .90f, height = .095f;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float y1 = top - i * height, y0 = y1 - .082f;
                ElarionUiKit.BuildObsidianButton(body, row.Label,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    row.Available ? ElarionUiKit.ObsidianButtonColor.Green : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(.03f, y0), new Vector2(.97f, y1),
                    row.Available ? () => _vm.Purchase(row.Sku) : (System.Action)null);
            }

            ElarionUiKit.BuildObsidianButton(body, "Restore purchases",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(.03f, .20f), new Vector2(.97f, .285f), _vm.Restore);
            ElarionUiKit.BuildObsidianButton(body, "Request account and data deletion",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(.03f, .105f), new Vector2(.97f, .19f), _vm.RequestDeletion);
            _status = ElarionUiKit.Label(body, "", .01f, .095f, ElarionUi.ParchmentDim,
                ElarionUi.FontBody, TextAlignmentOptions.Center, .03f, .97f);
            _modal.canvas.SetActive(false);
        }

        private void SetOpen(bool open)
        {
            if (_modal == null || _modal.canvas == null) return;
            _open = open;
            _modal.canvas.SetActive(open);
            if (open && !PanelManager.NotifyOpened(_panelHandle))
            {
                _open = false;
                _modal.canvas.SetActive(false);
            }
            else if (!open) PanelManager.NotifyClosed(_panelHandle);
        }

        private void Close() => SetOpen(false);
        private void SetStatus(string value) { if (_status != null) _status.text = value ?? string.Empty; }
    }
}
