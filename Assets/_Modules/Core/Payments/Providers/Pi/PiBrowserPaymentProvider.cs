// =============================================================================
// PiBrowserPaymentProvider - the Pi Network U2A payment rail.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.PaymentProviders.Pi   Namespace: DeNelle.Core.Payments.Providers
//
// WO-1318. The FIRST revenue path on Pi, on ONE sku (hearth-spark), through the
// EXISTING provider seam: PaymentChannel.PiBrowser already existed in IPaymentProvider
// and PaymentChannelResolver already resolved it - there was simply no implementation.
// This is that implementation. It is NOT a second store, catalog, quote table or grant
// path (ARCHITECTURE_PRINCIPLES 2b): the shelf is still PackStore, the quote is still
// server-owned, and delivery is still PackGrantBridge.
//
// ── THE FLOW, IN ORDER, AND WHY EACH STEP IS WHERE IT IS ─────────────────────
//   1. Pi.init AWAITED to resolution. The Pi SDK docs require init to have resolved
//      before any createPayment; PiBridge.jslib treats it as a promise and this awaits
//      that promise. Do not "optimise" the await away.
//   2. Pi.authenticate(['username','payments']) - the payments scope is requested HERE,
//      lazily, and NOT at sign-in. See PiSignInController for the full reasoning: an
//      existing player granted 'username' alone, and widening the SIGN-IN scope would
//      turn a dismissed consent into a failed sign-in for a purchase feature they never
//      touched. Here, a refusal costs a purchase and nothing else.
//   3. POST /api/pi/quote. The server computes the Pi amount from CoinGecko low_24h,
//      persists it against a quote id, and re-validates it at approve.
//      ⛔ THE CLIENT NEVER DECIDES THE AMOUNT and has NO fallback price. If the quote
//         fails the purchase is refused in words. Charging a wrong price is worse than
//         not charging (the SKR rail's fail-closed ruling, inherited).
//   4. Pi.createPayment(amount, memo, metadata) with all four callbacks wired.
//   5. onReadyForServerApproval -> POST /api/pi/approve
//   6. onReadyForServerCompletion -> POST /api/pi/complete -> ONLY THEN grant.
//
// ── HOW THIS IS VERIFIED ─────────────────────────────────────────────────────
// On a phone, inside Pi Browser, with no debugger. The FlowTrace lines below ARE the
// instrument (CLAUDE.md sec.12). Every one of them is permanent; never strip them.
// A successful purchase writes, in order, under [Flow:PiPay]:
//   Purchase requested / EnsurePaymentsScope / init ok / auth ok / quote requested /
//   quote received / createPayment / onReadyForServerApproval / POST approve /
//   approve OK / onReadyForServerCompletion / POST complete / complete OK /
//   grant applied / purchase COMPLETE
// A gap in that list names the dead step without a single code read.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Platform;

namespace DeNelle.Core.Payments.Providers
{
    public sealed class PiBrowserPaymentProvider : IPaymentProvider
    {
        private const string TraceSystem = PiPaymentEndpoints.TraceSystem;

        /// <summary>
        /// ⭐ ONE SKU, DELIBERATELY (owner ruling, WO-1318). No purchase has ever completed in this
        /// game, so proving approve -> complete -> grant on a single pack beats shipping 28 that could
        /// all fail identically. Widening this is a reviewed decision, not a convenience edit - and it
        /// must widen on the SERVER in the same change, or a quote request for the new sku is refused.
        /// </summary>
        public const string EnabledSku = "hearth-spark";

        /// <summary>Fallback memo. The server sends one on the quote and that one wins; this is only
        /// used if the quote omits it, and it matches the WO's authored string exactly. ASCII only.</summary>
        private const string FallbackMemo = "Echoes of Elarion - Hearth Spark";

        private static readonly TimeSpan InitTimeout     = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan AuthTimeout     = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan PaymentTimeout  = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SettleTimeout   = TimeSpan.FromSeconds(90);

        private readonly IPiPlatform _pi;
        private bool _inFlight;

        // The one in-flight purchase's correlation state. Null between purchases.
        private string _correlationId;
        private string _quoteId;
        private UniTaskCompletionSource<PiBackendResult> _settleTcs;

        // Uid from the most recent payments-scoped authenticate, used for the quote request.
        private string _lastAuthUid;

        // Last server quote, kept only so the store can show what the player is about to pay.
        private string _lastQuoteSku;
        private string _lastQuoteAmountText;

        public PiBrowserPaymentProvider(IPiPlatform pi)
        {
            _pi = pi ?? throw new ArgumentNullException(nameof(pi));
        }

        public PaymentChannel Channel => PaymentChannel.PiBrowser;

        // -----------------------------------------------------------------
        //  display
        // -----------------------------------------------------------------

        /// <summary>
        /// ⛔ NEVER AN INVENTED NUMBER. The Pi price exists only once the SERVER has quoted it, so
        /// before the first quote this reports UNAVAILABLE rather than converting a USD anchor on the
        /// client. That refusal is the same shape as the SKR rail's "no server quote -> no price on the
        /// button" rule (PackStore BuildSpotlightCta), and for the same reason.
        /// </summary>
        public DisplayPrice GetDisplayPrice(string sku)
        {
            if (!string.IsNullOrEmpty(sku) &&
                string.Equals(sku, _lastQuoteSku, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(_lastQuoteAmountText))
                return DisplayPrice.Ready(_lastQuoteAmountText + " Pi", "PI");

            return DisplayPrice.Unavailable("Priced in Pi when you tap Buy.");
        }

        // -----------------------------------------------------------------
        //  gate
        // -----------------------------------------------------------------

        public bool CanBuy(string sku, out string reason)
        {
            if (_pi == null || !_pi.IsAvailable)
            {
                reason = "Pi payments need the Pi Browser. Open the game in Pi Browser to buy.";
                FlowTrace.Once(TraceSystem, "canbuy-no-pi",
                    "CanBuy refused: the Pi platform reports unavailable (window.Pi missing).");
                return false;
            }

            if (string.IsNullOrEmpty(sku) || !string.Equals(sku, EnabledSku, StringComparison.Ordinal))
            {
                reason = "This pack is not on sale in Pi yet. The Hearth Spark starter pack is.";
                FlowTrace.Once(TraceSystem, "canbuy-sku-" + (sku ?? "null"),
                    $"CanBuy refused for '{sku}': only '{EnabledSku}' is enabled on the Pi rail " +
                    "(owner ruling - one sku first). This is the feature, not a gap.");
                return false;
            }

            if (_inFlight)
            {
                reason = "A Pi payment is already in progress. Please finish or cancel it first.";
                return false;
            }

            reason = null;
            return true;
        }

        // -----------------------------------------------------------------
        //  purchase
        // -----------------------------------------------------------------

        public void Purchase(string sku, Action<ProviderPurchaseResult> onComplete)
        {
            PurchaseAsync(sku, onComplete).Forget();
        }

        private async UniTaskVoid PurchaseAsync(string sku, Action<ProviderPurchaseResult> onComplete)
        {
            bool answered = false;
            Action<ProviderPurchaseResult> answer = r =>
            {
                if (answered) return;
                answered = true;
                // Guard the caller: a throw in the store's callback must not leave _inFlight stuck.
                try { onComplete?.Invoke(r); }
                catch (Exception e) { FlowTrace.Fail(TraceSystem, $"purchase callback threw: {e.GetType().Name}: {e.Message}"); }
            };

            using var _ = FlowTrace.Enter(TraceSystem, $"Purchase '{sku}'");
            FlowTrace.Step(TraceSystem, $"Purchase requested sku={sku} env={PiEnvironment.Label}");

            if (!CanBuy(sku, out string gateReason))
            {
                answer(ProviderPurchaseResult.Failure(sku, gateReason));
                return;
            }

            _inFlight = true;
            _correlationId = Guid.NewGuid().ToString("N");
            _quoteId = null;
            _settleTcs = new UniTaskCompletionSource<PiBackendResult>();

            _pi.OnApprovalReady   += HandleApprovalReady;
            _pi.OnCompletionReady += HandleCompletionReady;

            try
            {
                // 1 + 2 -- init MUST have resolved before any createPayment, and the payments scope
                // must be granted. Both are awaited; neither is assumed.
                if (!await EnsurePaymentsScope())
                {
                    answer(ProviderPurchaseResult.Failure(sku,
                        "Pi could not confirm permission to pay. Nothing was charged."));
                    return;
                }

                string uid = ResolveUid();

                // 3 -- the server owns the amount.
                var attempt = await PiPaymentEndpoints.RequestQuoteAsync(sku, uid);
                if (!attempt.Ok)
                {
                    answer(ProviderPurchaseResult.Failure(sku, attempt.Message));
                    return;
                }

                var quote = attempt.Quote;
                _quoteId = quote.QuoteId;
                _lastQuoteSku = quote.Sku;
                _lastQuoteAmountText = quote.AmountText;

                string memo = string.IsNullOrEmpty(quote.Memo) ? FallbackMemo : quote.Memo;
                if (string.IsNullOrEmpty(quote.Memo))
                    FlowTrace.Warn(TraceSystem,
                        $"quote carried no memo; using the authored fallback '{FallbackMemo}'. The server " +
                        "should send the memo so both sides agree on it.");

                // metadata MUST agree with what the backend validates at approve/complete.
                string metadata =
                    "{\"sku\":" + PiPaymentEndpoints.Json(quote.Sku) +
                    ",\"quoteId\":" + PiPaymentEndpoints.Json(quote.QuoteId) +
                    ",\"uid\":" + PiPaymentEndpoints.Json(uid) + "}";

                // 4 -- hand it to the Pi SDK. The amount is the server's, verbatim.
                FlowTrace.Step(TraceSystem,
                    $"createPayment corr={_correlationId} sku={quote.Sku} amount={quote.AmountText} Pi " +
                    $"quoteId={quote.QuoteId} memo='{memo}'");

                PiPaymentResult payment;
                try
                {
                    payment = await _pi.CreatePayment(_correlationId, quote.Amount, memo, metadata)
                                       .Timeout(PaymentTimeout);
                }
                catch (TimeoutException)
                {
                    // NOT a failure the player should retry blindly: the Pi sheet may still settle.
                    FlowTrace.Fail(TraceSystem,
                        $"createPayment produced no terminal callback within {PaymentTimeout.TotalMinutes} min " +
                        $"(corr={_correlationId}). Reporting PENDING, never failed - a retry here could double-charge. " +
                        "onIncompletePaymentFound settles it on next launch.");
                    answer(ProviderPurchaseResult.AwaitingSettlement(sku, _correlationId));
                    return;
                }

                if (payment.Status == PiPaymentStatus.Cancelled)
                {
                    FlowTrace.Warn(TraceSystem, $"purchase CANCELLED by the player (corr={_correlationId}).");
                    answer(ProviderPurchaseResult.Failure(sku, "Payment cancelled. Nothing was charged."));
                    return;
                }

                if (!payment.Ok)
                {
                    FlowTrace.Fail(TraceSystem,
                        $"purchase ERRORED (corr={_correlationId}): {payment.Error}");
                    answer(ProviderPurchaseResult.Failure(sku,
                        "Pi could not complete this payment. If you were charged, reopen the game and it " +
                        "will finish automatically."));
                    return;
                }

                // 6 -- Pi says the transaction is on chain. The GRANT waits on OUR backend saying so.
                PiBackendResult settled;
                try { settled = await _settleTcs.Task.Timeout(SettleTimeout); }
                catch (TimeoutException)
                {
                    FlowTrace.Fail(TraceSystem,
                        $"complete did not answer within {SettleTimeout.TotalSeconds}s (paymentId=" +
                        $"{payment.PiPaymentId}). Player may have paid - reporting PENDING so nothing is " +
                        "granted AND nothing is re-charged. onIncompletePaymentFound retries next launch.");
                    answer(ProviderPurchaseResult.AwaitingSettlement(sku, payment.PiPaymentId));
                    return;
                }

                if (!settled.Ok)
                {
                    answer(ProviderPurchaseResult.AwaitingSettlement(sku, payment.PiPaymentId));
                    return;
                }

                if (!PiGrantApplier.ApplyExactlyOnce(sku, payment.PiPaymentId))
                {
                    // Settled on the server, not delivered locally. NEVER report success.
                    answer(ProviderPurchaseResult.AwaitingSettlement(sku, payment.PiPaymentId));
                    return;
                }

                FlowTrace.Step(TraceSystem,
                    $"purchase COMPLETE sku={sku} paymentId={payment.PiPaymentId} txid={payment.Txid}");
                answer(ProviderPurchaseResult.Success(sku, payment.PiPaymentId));
            }
            catch (Exception e)
            {
                FlowTrace.Fail(TraceSystem, $"Purchase threw: {e.GetType().Name}: {e.Message}");
                answer(ProviderPurchaseResult.Failure(sku, "Something went wrong. Nothing was charged."));
            }
            finally
            {
                _pi.OnApprovalReady   -= HandleApprovalReady;
                _pi.OnCompletionReady -= HandleCompletionReady;
                _inFlight = false;
                _correlationId = null;
                _settleTcs = null;
                // A path that somehow escapes without answering must still not hang the store.
                answer(ProviderPurchaseResult.Failure(sku, "Payment ended without a result. Nothing was charged."));
            }
        }

        // -----------------------------------------------------------------
        //  SDK callbacks -> backend
        // -----------------------------------------------------------------

        private void HandleApprovalReady(string correlationId, string piPaymentId)
        {
            if (!string.Equals(correlationId, _correlationId, StringComparison.Ordinal))
            {
                FlowTrace.Warn(TraceSystem,
                    $"approvalReady for an unknown correlation id ({correlationId}) - ignoring. This is a " +
                    "callback from a payment this session did not start.");
                return;
            }
            ApproveAsync(piPaymentId, _quoteId).Forget();
        }

        private static async UniTaskVoid ApproveAsync(string piPaymentId, string quoteId)
        {
            // Fire-and-forget by design: Pi drives the next step itself once approve lands. A failure
            // here is loud (PiPaymentEndpoints traces it) and simply means completion never arrives,
            // which the outer PaymentTimeout turns into a PENDING result rather than a silent stall.
            await PiPaymentEndpoints.ApproveAsync(piPaymentId, quoteId);
        }

        private void HandleCompletionReady(string correlationId, string piPaymentId, string txid)
        {
            if (!string.Equals(correlationId, _correlationId, StringComparison.Ordinal))
            {
                FlowTrace.Warn(TraceSystem,
                    $"completionReady for an unknown correlation id ({correlationId}) - ignoring.");
                return;
            }
            CompleteAsync(piPaymentId, txid, _quoteId, _settleTcs).Forget();
        }

        private static async UniTaskVoid CompleteAsync(string piPaymentId, string txid, string quoteId,
                                                       UniTaskCompletionSource<PiBackendResult> tcs)
        {
            var result = await PiPaymentEndpoints.CompleteAsync(piPaymentId, txid, quoteId);
            tcs?.TrySetResult(result);
        }

        // -----------------------------------------------------------------
        //  scope + identity
        // -----------------------------------------------------------------

        /// <summary>
        /// Awaits Pi.init to RESOLUTION, then asks for the payments scope. Both awaits are bounded, so
        /// a stalled SDK promise leaves a retryable refusal instead of a dead store (the 2026-07-01
        /// "Signing in..." hang, same lesson).
        /// </summary>
        private async UniTask<bool> EnsurePaymentsScope()
        {
            FlowTrace.Step(TraceSystem,
                $"EnsurePaymentsScope: scopes=username,payments (lazy - sign-in asks for username only) " +
                $"env={PiEnvironment.Label}");

            bool inited;
            try { inited = await _pi.Init(PiEnvironment.Sandbox).Timeout(InitTimeout); }
            catch (TimeoutException)
            {
                FlowTrace.Fail(TraceSystem, $"Pi.init did not resolve within {InitTimeout.TotalSeconds}s - " +
                                            "refusing to call createPayment before init has resolved.");
                return false;
            }
            if (!inited)
            {
                FlowTrace.Fail(TraceSystem, "Pi.init reported failure/unavailable - no payment attempted.");
                return false;
            }
            FlowTrace.Step(TraceSystem, "init ok");

            PiAuthResult auth;
            try { auth = await _pi.Authenticate(new[] { "username", "payments" }).Timeout(AuthTimeout); }
            catch (TimeoutException)
            {
                FlowTrace.Fail(TraceSystem,
                    $"Pi.authenticate(payments) did not resolve within {AuthTimeout.TotalSeconds}s - the " +
                    "consent sheet was likely left open. Nothing was charged.");
                return false;
            }

            if (!auth.Ok)
            {
                FlowTrace.Fail(TraceSystem,
                    $"Pi.authenticate(payments) refused: {auth.Error}. The player declined the payments " +
                    "scope, or the grant failed. THEIR SIGN-IN IS UNAFFECTED - this scope is asked for " +
                    "here and nowhere else, precisely so a refusal costs a purchase and not a session.");
                return false;
            }

            _lastAuthUid = auth.Uid;
            FlowTrace.Step(TraceSystem, $"auth ok uid={PiPaymentEndpoints.Mask(auth.Uid)} scopes=username,payments");
            return true;
        }

        private string ResolveUid()
        {
            if (!string.IsNullOrEmpty(_lastAuthUid)) return _lastAuthUid;
            string signedIn = PiSignInController.SignedInUid;
            if (!string.IsNullOrEmpty(signedIn)) return signedIn;
            FlowTrace.Warn(TraceSystem,
                "no Pi uid available for the quote request - the server will have to identify the payer " +
                "from the Pi payment itself.");
            return string.Empty;
        }

        // -----------------------------------------------------------------
        //  restore
        // -----------------------------------------------------------------

        /// <summary>
        /// Pi has no purchase-restore API. What it HAS is onIncompletePaymentFound, which fires on every
        /// authenticate - so "restore" here means: re-authenticate, and let the incomplete-payment
        /// handler (PiPaymentBootstrap) settle anything stranded. Reporting success only means the
        /// re-auth ran, never that something was found.
        /// </summary>
        public void RestorePurchases(Action<bool, string> onComplete)
        {
            RestoreAsync(onComplete).Forget();
        }

        private async UniTaskVoid RestoreAsync(Action<bool, string> onComplete)
        {
            FlowTrace.Step(TraceSystem, "RestorePurchases: re-authenticating to re-surface incomplete payments.");
            bool ok = await EnsurePaymentsScope();
            try
            {
                onComplete?.Invoke(ok, ok
                    ? "Checked with Pi for unfinished payments."
                    : "Could not reach Pi to check for unfinished payments.");
            }
            catch (Exception e)
            {
                FlowTrace.Fail(TraceSystem, $"restore callback threw: {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
