# WORK ORDER 44 — Jupiter Quote Integration (Phase 2)

**Status:** BLOCKED — PAUSED (owner ruling 2026-08-09: waiting on publish approval, then revisit)
**Date:** 2026-05-26
**Priority:** Medium
**Scope:** Medium / Network dependency
**Depends on:** WO-43 (UI panel + asmdef + interface all in place)

---

## Goal

Replace `MockJupiterSwapService` with a real `JupiterSwapService` that calls
Jupiter's `/v6/quote` endpoint. The UI controller (`JupiterSwapPanelController`)
is unchanged — it already speaks `IJupiterService` — so this phase is purely
a service swap.

---

## What changes from WO-43

| WO-43 (Phase 1) | WO-44 (Phase 2) |
|---|---|
| `MockJupiterSwapService` returns hardcoded fake data | `JupiterSwapService` calls `https://quote-api.jup.ag/v6/quote` |
| No HTTP | `UnityWebRequest` HTTP GET |
| Instant response | ~300–800 ms real latency |
| No JSON parsing | `JsonUtility` DTO parsing |
| `RoutePlan = "{\"mock\":true}"` | Real route plan JSON forwarded to WO-45 |

`JupiterSwapPanelController`, `WalletBridgeStub`, `SwapFeeConfig`, all UXML/USS,
and `CoreServices` are **untouched**.

---

## 1. `JupiterSwapService` — `DeNelle.Web3`

**Path:** `Assets/_Modules/Web3/JupiterSwapService.cs`

Swap this onto the `SwapServiceRoot` GameObject in place of
`MockJupiterSwapService`. Both implement `IJupiterService`; the controller
resolves whichever is present via `GetComponent<IJupiterService>()`.

```csharp
// =============================================================================
// JupiterSwapService — live Jupiter /v6/quote integration.  WO-44.
// Replaces MockJupiterSwapService (WO-43). Swap execution is WO-45.
// =============================================================================

using System;
using System.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Web3;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace DeNelle.Web3
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class JupiterSwapService : MonoBehaviour, IJupiterService
    {
        // ── Jupiter REST ──────────────────────────────────────────────────────
        private const string QuoteUrl = "https://quote-api.jup.ag/v6/quote";

        // ── Token mints (Solana mainnet) ──────────────────────────────────────
        private const string MintUSDC = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
        private const string MintSOL  = "So11111111111111111111111111111111111111112";

        [SerializeField]
        [Tooltip("SKR SPL token mint address (mainnet). Replace before launch.")]
        private string _skrMint = "REPLACE_WITH_SKR_MINT_ADDRESS";

        [SerializeField] private SwapFeeConfig _feeConfig;
        [SerializeField] private UIDocument    _document;

        private JupiterSwapPanelController _panelController;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            CoreServices.RegisterJupiter(this);
        }

        private void Start()
        {
            _panelController = GetComponent<JupiterSwapPanelController>();
            SetPanelVisible(false);
        }

        private void OnDestroy() => CoreServices.UnregisterJupiter(this);

        // =====================================================================
        //  IJupiterService
        // =====================================================================

        public void OpenSwapPanel(decimal minimumSkr = 0m)
        {
            SetPanelVisible(true);
            _panelController?.Initialise(minimumSkr, _feeConfig);
        }

        public void CloseSwapPanel() => SetPanelVisible(false);

        public async Task<SwapQuote> GetQuoteAsync(SwapInputToken input, decimal inputAmount)
        {
            if (_feeConfig == null)
            {
                Debug.LogWarning("[JupiterSwapService] SwapFeeConfig not assigned.");
                return null;
            }

            string inputMint = input == SwapInputToken.SOL ? MintSOL : MintUSDC;
            // USDC = 6 decimals; SOL (wrapped) = 9 decimals.
            int  decimals    = input == SwapInputToken.SOL ? 9 : 6;
            long amountUnits = (long)(inputAmount * (decimal)Math.Pow(10, decimals));

            // Build the URL — feeAccount is required when platformFeeBps > 0.
            bool hasFeeAccount = !string.IsNullOrEmpty(_feeConfig.FeeWalletAddress);
            string feeParams = (hasFeeAccount && _feeConfig.PlatformFeeBps > 0)
                ? $"&platformFeeBps={_feeConfig.PlatformFeeBps}" +
                  $"&feeAccount={_feeConfig.FeeWalletAddress}"
                : "";

            string url = $"{QuoteUrl}" +
                         $"?inputMint={inputMint}" +
                         $"&outputMint={_skrMint}" +
                         $"&amount={amountUnits}" +
                         $"&slippageBps={_feeConfig.SlippageBps}" +
                         feeParams;

            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Accept", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JupiterSwapService] Quote failed ({req.responseCode}): {req.error}");
                return null;
            }

            return ParseQuote(req.downloadHandler.text, inputAmount, decimals);
        }

        // =====================================================================
        //  Quote parsing
        // =====================================================================

        private SwapQuote ParseQuote(string json, decimal inputAmount, int inputDecimals)
        {
            try
            {
                var dto = JsonUtility.FromJson<QuoteResponseDto>(json);

                // SKR output — verify the SKR mint's actual decimal count and
                // update skrDecimals if it differs from 6.
                const int skrDecimals = 6;
                decimal skrOut = dto.outAmount / (decimal)Math.Pow(10, skrDecimals);

                decimal rate = inputAmount > 0 ? skrOut / inputAmount : 0m;

                decimal platformFee = 0m;
                if (dto.platformFee != null
                    && long.TryParse(dto.platformFee.amount, out long pfRaw))
                {
                    platformFee = pfRaw / (decimal)Math.Pow(10, inputDecimals);
                }

                // Network fee: use Jupiter's computed fee if present; fallback
                // to the Solana base fee (~0.000005 SOL per signature).
                decimal networkFee = 0.000005m;
                if (dto.otherAmountThreshold > 0)
                {
                    // otherAmountThreshold is the minimum output after slippage
                    // — not fees. The precise network cost requires a /swap call;
                    // keep the estimate for display purposes.
                }

                return new SwapQuote
                {
                    SkrOut      = skrOut,
                    Rate        = rate,
                    PlatformFee = platformFee,
                    NetworkFee  = networkFee,
                    SlippageBps = _feeConfig.SlippageBps,
                    RoutePlan   = json   // full response forwarded to /swap in WO-45
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JupiterSwapService] Quote parse error: {ex.Message}");
                return null;
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private void SetPanelVisible(bool visible)
        {
            if (_document?.rootVisualElement != null)
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── JSON DTOs (JsonUtility — no extra packages needed) ────────────────

        [Serializable]
        private sealed class QuoteResponseDto
        {
            public long           outAmount;
            public long           otherAmountThreshold;
            public PlatformFeeDto platformFee;
        }

        [Serializable]
        private sealed class PlatformFeeDto
        {
            public string amount;
            public int    feeBps;
        }
    }
}
```

---

## 2. Scene wiring change

On the `SwapServiceRoot` GameObject:
1. Remove `MockJupiterSwapService` component.
2. Add `JupiterSwapService` component.
3. Assign `SwapFeeConfig` asset to the new component.
4. Set `_skrMint` to the SKR SPL mint address (or leave placeholder for
   development; the panel will show an error if the mint is wrong).

`JupiterSwapPanelController` — no changes needed. It resolves
`IJupiterService` via `GetComponent` and now finds `JupiterSwapService`.

---

## 3. Error states to handle

| Condition | `swap-status` text |
|---|---|
| `UnityWebRequest` network failure | "Could not fetch rate. Check connection." |
| HTTP 4xx (bad mint, bad params) | "Rate unavailable — check SKR mint address." |
| `JsonUtility` parse exception | "Unexpected response from Jupiter." |
| `outAmount = 0` (no route found) | "No swap route found for this amount." |

All set via `JupiterSwapPanelController.SetStatus(msg, isError: true)` — the
controller already has this path; the service just needs to return `null` and
let the controller display its generic error, OR the service can surface a richer
error by extending `SwapQuote` with an optional `ErrorMessage` field (add in this
phase if desired).

---

## 4. Testing without live SKR mint

Until the SKR mint address is known, test with a known SPL token pair on mainnet
(e.g., USDC → BONK `DezXAZ8z7PnrnRJjz3wXBoRgixCa6xjnB7YaB1pPB263`). Verify:

1. Quote returns within ~1 s on a good connection.
2. Rate, SKR out, and fee fields populate.
3. Entering 0 or non-numeric clears the display and shows the "Enter a valid
   amount" status.
4. Disconnecting the device mid-quote triggers the error state, not a crash.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Web3/JupiterSwapService.cs` | **Create** — live quote service |
| Scene `SwapServiceRoot` | **Edit** — swap Mock → real component |
| `Assets/_Modules/Web3/MockJupiterSwapService.cs` | Keep (for dev/offline testing); remove from the release scene |

---

## Acceptance Criteria

- [ ] `JupiterSwapService.GetQuoteAsync(USDC, 10)` returns a non-null
      `SwapQuote` in the Editor with internet connected (check Console)
- [ ] Typing a USDC amount shows a live SKR output and rate within ~1 s
- [ ] Platform fee line matches `SwapFeeConfig._platformFeeBps` applied to
      the input amount
- [ ] All four error conditions in §3 display a visible, non-crashing status
- [ ] `RoutePlan` on the returned quote contains the full Jupiter JSON response
      (needed as input to WO-45 swap execution)
- [ ] `MockJupiterSwapService` is NOT referenced by any scene in a Release build
