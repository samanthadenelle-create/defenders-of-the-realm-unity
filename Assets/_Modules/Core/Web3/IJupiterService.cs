// =============================================================================
// IJupiterService — cross-assembly contract for the Jupiter swap panel (WO-43).
// -----------------------------------------------------------------------------
// Registered in CoreServices; callers (Village / HeroSelect / HUD) open the
// swap panel via CoreServices.Jupiter?.OpenSwapPanel(...) and NEVER reference
// the DeNelle.Web3 assembly directly. The concrete implementation
// (JupiterSwapService) lives in DeNelle.Web3.
//
// DEVNET / SECURITY NOTE: the project's wallet stack (DeNelle.Wallet) is
// devnet-only and owner-gated to mainnet (v2-unity-port-spec Part 10). The
// Jupiter aggregator REST API is mainnet. No real swap transaction is signed
// or submitted by this scaffold — see JupiterSwapService for the explicit
// TODO where real wallet signing must be wired before going live.
// =============================================================================

using System.Threading.Tasks;

namespace DeNelle.Core.Web3
{
    /// <summary>
    /// Fetches swap quotes from Jupiter and opens the in-game swap panel.
    /// Implemented by <c>JupiterSwapService</c> in DeNelle.Web3.
    /// </summary>
    public interface IJupiterService
    {
        /// <summary>
        /// Opens the swap panel pre-filled to acquire at least
        /// <paramref name="minimumSkr"/> SKR. Pass 0 to open blank.
        /// </summary>
        void OpenSwapPanel(decimal minimumSkr = 0m);

        /// <summary>Closes the panel if it is currently visible.</summary>
        void CloseSwapPanel();

        /// <summary>
        /// Fetches a live quote without opening the panel.
        /// Returns null on network error.
        /// </summary>
        Task<SwapQuote> GetQuoteAsync(SwapInputToken input, decimal inputAmount);
    }

    /// <summary>
    /// Quote returned by the Jupiter quote endpoint. Plain class (not a C# 9
    /// record with init-only setters) so it compiles on the project's C#
    /// language level without requiring the <c>IsExternalInit</c> shim.
    /// </summary>
    public sealed class SwapQuote
    {
        /// <summary>SKR the player will receive (after fees).</summary>
        public decimal SkrOut { get; set; }
        /// <summary>Exchange rate: 1 input token = N SKR.</summary>
        public decimal Rate { get; set; }
        /// <summary>Platform fee taken by Defenders (in input-token units).</summary>
        public decimal PlatformFee { get; set; }
        /// <summary>Network + Jupiter route fees (in input-token units).</summary>
        public decimal NetworkFee { get; set; }
        /// <summary>Slippage tolerance used for this quote, in bps.</summary>
        public int SlippageBps { get; set; }
        /// <summary>Opaque quote-response JSON from Jupiter — forwarded verbatim to /swap.</summary>
        public string RoutePlan { get; set; }
    }

    /// <summary>Input token options for the swap panel. v1 exposes USDC only.</summary>
    public enum SwapInputToken
    {
        /// <summary>USDC — the only input surfaced in the v1 UI.</summary>
        USDC = 0,
        /// <summary>SOL — reserved for v2; not shown in the UI until explicitly enabled.</summary>
        SOL = 1
    }
}
