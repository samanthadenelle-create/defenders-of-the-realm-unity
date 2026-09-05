// =============================================================================
// StoreFocusRequest - the rail-neutral "open the store on THIS sku" latch (WO-1282).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Commerce   Namespace: DeNelle.Commerce   (STATIC)
//
// WHY IT EXISTS. DeNelle.Village used to call PackStore.RequestFocusSku directly
// (ManageScreenVM.BuySlot, WO-1253). PackStore is 3546 lines, constructs a
// WalletService, and CANNOT leave DeNelle.Wallet - so that one call was the last
// thing keeping Village bound to the Solana rail on this path. The LATCH moves; the
// 3546-line store does not.
//
// ⛔ THIS IS A LATCH, NOT AN EVENT, AND THE DIFFERENCE IS THE WHOLE DESIGN.
//    PackStore.RequestFocusSku was already static precisely because "the host may
//    not exist yet" - the request is made BEFORE the panel is built and is consumed
//    on its next Render. An event would fire into an empty room and be lost. A latch
//    survives the gap, which is the behaviour WO-1253 shipped and depends on.
//
// ⛔ AND THAT IS WHY THIS SEAM CANNOT FAIL SILENTLY THE WAY A REGISTERED HOOK CAN
//    (WO-1282's own correction block warns about exactly that class of seam). There
//    is no registration and no ordering: the caller writes a string here, and
//    whichever storefront is compiled into this build reads it back. In a Google Play
//    artifact NOTHING reads it, and that is correct and intended - there is no Solana
//    storefront to focus. The latch simply sits, and Consume() never runs.
// =============================================================================

using DeNelle.Core.Diagnostics;

namespace DeNelle.Commerce
{
    /// <summary>
    /// A pending "open the storefront focused on this SKU" request. Written by any assembly that
    /// can name Commerce; read by whichever storefront implementation this build carries.
    /// </summary>
    public static class StoreFocusRequest
    {
        /// <summary>FlowTrace system tag for every line this seam emits. Matches PackStore's.</summary>
        private const string TraceSystem = "Store";

        private static string _pendingSku;

        /// <summary>
        /// WO-1253 - ask the storefront to open pre-focused on a named SKU (the Manage
        /// "Buy builder" route). Static because the host may not exist yet; the storefront
        /// consumes it on its next render.
        /// </summary>
        public static void RequestFocusSku(string sku)
        {
            _pendingSku = sku;
            FlowTrace.Step(TraceSystem, "RequestFocusSku '" + (sku ?? "<null>") + "' latched.");
        }

        /// <summary>
        /// Takes the pending SKU and CLEARS it, so a focus request is honoured exactly once.
        /// Returns null when nothing is pending - the normal case on almost every render.
        /// </summary>
        public static string Consume()
        {
            var sku = _pendingSku;
            _pendingSku = null;
            return sku;
        }

        /// <summary>True when a focus request is waiting. Does NOT consume it.</summary>
        public static bool HasPending => !string.IsNullOrEmpty(_pendingSku);

        // ---------------------------------------------------------------------
        //  WO-1388 - THE DOOR. Which entry point opened the store, for the funnel's
        //  store_opened {door}. Same latch shape as the SKU above, for the same reason:
        //  the host may not exist when the door is walked through, and the read happens
        //  on the next OnEnable. Written by PackStoreBootstrap from the PanelRouter
        //  context string; callers that open with no context leave it unset and the
        //  store infers (shortfall / manage / hud-card) from the other latches.
        // ---------------------------------------------------------------------
        private static string _pendingDoor;

        /// <summary>Latch the door name (hud-card | shortfall | settings | vendor ...) for the next open.</summary>
        public static void RequestDoor(string door)
        {
            _pendingDoor = string.IsNullOrWhiteSpace(door) ? null : door.Trim();
            FlowTrace.Step(TraceSystem, "RequestDoor '" + (_pendingDoor ?? "<null>") + "' latched.");
        }

        /// <summary>Takes the pending door and CLEARS it. Null when no caller named one.</summary>
        public static string ConsumeDoor()
        {
            var door = _pendingDoor;
            _pendingDoor = null;
            return door;
        }
    }
}
