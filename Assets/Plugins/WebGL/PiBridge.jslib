// PiBridge.jslib — Unity WebGL ↔ Pi JS SDK bridge.
// Spec: PI_INTEGRATION_SPEC.md §2. The Pi SDK (window.Pi) is page-level JS injected by
// pi-sdk.js in the WebGL template's index.html; it exists ONLY inside Pi Browser.
//
// C# → JS via [DllImport("__Internal")]:  PiInit, PiAuthenticate, PiCreatePayment, PiShowAd, PiIsAvailable.
// JS → C# via SendMessage("PiBridge","OnPiCallback", <json>):
//   { "type": "ready"|"auth"|"approvalReady"|"completionReady"|"adReady"|"error"|"cancelled",
//     "paymentId": "<id-or-empty>", "data": { ... } }
// A persistent GameObject named "PiBridge" (DontDestroyOnLoad) receives OnPiCallback.

var PiBridgeLib = {
  // --- helper: marshal a result object back to the C# receiver as JSON ---
  $PiBridgeState: {
    send: function (obj) {
      try {
        // SendMessage is provided by the Unity loader on the global scope.
        SendMessage('PiBridge', 'OnPiCallback', JSON.stringify(obj));
      } catch (e) {
        // Unity instance not ready yet — swallow; the C# side times out gracefully.
        console.warn('[PiBridge] SendMessage failed: ' + e);
      }
    }
  },

  // true when the Pi SDK object exists (pi-sdk.js loaded). NOTE (WO-678): this is TRUE in
  // ANY browser once the script loads — it does NOT mean we are inside Pi Browser. Use
  // PiIsPiBrowser for the environment check.
  PiIsAvailable: function () {
    return (typeof window !== 'undefined' && typeof window.Pi !== 'undefined') ? 1 : 0;
  },

  // WO-678 Lane C: true only in the real Pi Browser app. Detection: the Pi Browser app
  // ships the token "PiBrowser" in its user agent (case-insensitive match to be safe).
  // Conservative by design — an unrecognised UA returns 0, which only skips AUTO
  // sign-in; the manual "Sign in with Pi" button still works everywhere.
  PiIsPiBrowser: function () {
    try {
      var ua = (typeof navigator !== 'undefined' && navigator.userAgent) ? navigator.userAgent : '';
      return /pibrowser/i.test(ua) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  // Pi.init({version:"2.0", sandbox}). sandbox != 0 → Testnet sandbox.
  // Per the Pi SDK docs, treat Pi.init as a Promise and AWAIT it fully before any
  // authenticate/createPayment — Promise.resolve() handles both promise + void returns.
  PiInit: function (sandboxFlag) {
    try {
      if (typeof window === 'undefined' || typeof window.Pi === 'undefined') {
        PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'init', message: 'window.Pi undefined (not in Pi Browser)' } });
        return;
      }
      Promise.resolve(window.Pi.init({ version: '2.0', sandbox: sandboxFlag !== 0 }))
        .then(function () {
          PiBridgeState.send({ type: 'ready', paymentId: '', data: { sandbox: sandboxFlag !== 0 } });
        })
        .catch(function (e) {
          PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'init', message: '' + e } });
        });
    } catch (e) {
      PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'init', message: '' + e } });
    }
  },

  // Pi.authenticate(scopes, onIncompletePaymentFound). scopesPtr = comma-separated, e.g. "username,payments".
  PiAuthenticate: function (scopesPtr) {
    try {
      var scopes = UTF8ToString(scopesPtr).split(',').map(function (s) { return s.trim(); }).filter(Boolean);
      if (typeof window === 'undefined' || typeof window.Pi === 'undefined') {
        PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'auth', message: 'window.Pi undefined' } });
        return;
      }
      var onIncomplete = function (payment) {
        PiBridgeState.send({ type: 'approvalReady', paymentId: (payment && payment.identifier) || '', data: { incomplete: true, payment: payment } });
      };
      window.Pi.authenticate(scopes, onIncomplete).then(function (authResult) {
        PiBridgeState.send({ type: 'auth', paymentId: '', data: {
          accessToken: authResult.accessToken,
          uid: authResult.user && authResult.user.uid,
          username: authResult.user && authResult.user.username
        }});
      }).catch(function (err) {
        PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'auth', message: '' + err } });
      });
    } catch (e) {
      PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'auth', message: '' + e } });
    }
  },

  // Pi.createPayment({amount, memo, metadata}, callbacks). paymentId is OUR correlation id (carried in metadata).
  PiCreatePayment: function (paymentIdPtr, amount, memoPtr, metadataJsonPtr) {
    try {
      var paymentId = UTF8ToString(paymentIdPtr);
      var memo = UTF8ToString(memoPtr);
      var metadata = {};
      try { metadata = JSON.parse(UTF8ToString(metadataJsonPtr) || '{}'); } catch (e) { metadata = {}; }
      metadata.correlationId = paymentId;

      if (typeof window === 'undefined' || typeof window.Pi === 'undefined') {
        PiBridgeState.send({ type: 'error', paymentId: paymentId, data: { where: 'createPayment', message: 'window.Pi undefined' } });
        return;
      }
      window.Pi.createPayment(
        { amount: amount, memo: memo, metadata: metadata },
        {
          onReadyForServerApproval: function (piPaymentId) {
            PiBridgeState.send({ type: 'approvalReady', paymentId: paymentId, data: { piPaymentId: piPaymentId } });
          },
          onReadyForServerCompletion: function (piPaymentId, txid) {
            PiBridgeState.send({ type: 'completionReady', paymentId: paymentId, data: { piPaymentId: piPaymentId, txid: txid } });
          },
          onCancel: function (piPaymentId) {
            PiBridgeState.send({ type: 'cancelled', paymentId: paymentId, data: { piPaymentId: piPaymentId } });
          },
          onError: function (error, payment) {
            PiBridgeState.send({ type: 'error', paymentId: paymentId, data: { where: 'createPayment', message: '' + error } });
          }
        }
      );
    } catch (e) {
      PiBridgeState.send({ type: 'error', paymentId: UTF8ToString(paymentIdPtr), data: { where: 'createPayment', message: '' + e } });
    }
  },

  // Pi.Ads.showAd("rewarded" | "interstitial").
  PiShowAd: function (adTypePtr) {
    try {
      var adType = UTF8ToString(adTypePtr) || 'rewarded';
      if (typeof window === 'undefined' || typeof window.Pi === 'undefined' || !window.Pi.Ads) {
        PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'showAd', message: 'Pi.Ads unavailable' } });
        return;
      }
      window.Pi.Ads.showAd(adType).then(function (result) {
        PiBridgeState.send({ type: 'adReady', paymentId: '', data: { adType: adType, result: result } });
      }).catch(function (err) {
        PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'showAd', message: '' + err } });
      });
    } catch (e) {
      PiBridgeState.send({ type: 'error', paymentId: '', data: { where: 'showAd', message: '' + e } });
    }
  }
};

autoAddDeps(PiBridgeLib, '$PiBridgeState');
mergeInto(LibraryManager.library, PiBridgeLib);
