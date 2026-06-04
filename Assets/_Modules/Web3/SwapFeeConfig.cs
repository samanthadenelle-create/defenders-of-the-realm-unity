// =============================================================================
// SwapFeeConfig — project-level swap fee + slippage settings (WO-43).
// -----------------------------------------------------------------------------
// Create one asset via  Assets > Create > Defenders > Swap Fee Config  and
// assign it on the JupiterSwapService component (or it will fall back to
// safe defaults). The fee-wallet field is an OWNER-CONFIG placeholder — it must
// hold the SKR associated-token-account address before any live swap can take a
// platform fee (Jupiter requires the feeAccount to be a token account for the
// OUTPUT mint — see Jupiter docs: https://station.jup.ag/docs/apis/adding-fees).
// =============================================================================

using UnityEngine;

namespace DeNelle.Web3
{
    /// <summary>
    /// Project-level swap fee settings. Create one asset via
    /// <c>Assets &gt; Create &gt; Defenders &gt; Swap Fee Config</c>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Defenders/Swap Fee Config",
        fileName = "SwapFeeConfig")]
    public sealed class SwapFeeConfig : ScriptableObject
    {
        [Tooltip("Platform fee in basis points (100 bps = 1%). " +
                 "Recommended range: 10-30 (0.1 %-0.3 %). " +
                 "Jupiter supports 0-10000.")]
        [Range(0, 100)]
        [SerializeField] private int _platformFeeBps = 20;   // 0.2 % default

        [Tooltip("OWNER-CONFIG: Solana token account (NOT a wallet address) that " +
                 "receives the platform fee. Jupiter requires the feeAccount to be " +
                 "the SKR associated-token-account of the fee wallet. Leave empty " +
                 "in the editor scaffold; fill before going live.")]
        [SerializeField] private string _feeWalletAddress = "";

        [Tooltip("Slippage tolerance in basis points. 50 = 0.5 %.")]
        [Range(10, 500)]
        [SerializeField] private int _slippageBps = 50;

        [Tooltip("Show the SOL input option in the swap panel (v2 feature). " +
                 "Leave off for v1.")]
        [SerializeField] private bool _enableSolInput = false;

        /// <summary>Platform fee in basis points (100 bps = 1%).</summary>
        public int PlatformFeeBps => _platformFeeBps;
        /// <summary>OWNER-CONFIG: SKR token account that receives the platform fee.</summary>
        public string FeeWalletAddress => _feeWalletAddress;
        /// <summary>Slippage tolerance in basis points.</summary>
        public int SlippageBps => _slippageBps;
        /// <summary>Whether the SOL input option is shown (v2 feature, off for v1).</summary>
        public bool EnableSolInput => _enableSolInput;
    }
}
