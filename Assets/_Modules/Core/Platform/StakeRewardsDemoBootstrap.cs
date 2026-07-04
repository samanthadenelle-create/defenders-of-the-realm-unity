// =============================================================================
// StakeRewardsDemoBootstrap — lights up the Seekerthon stake-rewards DEMO surface.
// -----------------------------------------------------------------------------
// The video has NO live wallet, so this is the seeded/mock path: when the ff.stakedemo
// flag is ON (default OFF — never in prod), it injects a MockStakeQuery seeded with a
// real-looking Genesis-holder amount (~1M SKR) into StakeRewardsResolver and opens the
// StakeRewardsPanel automatically, so the panel shows a live-looking stake + unlocked
// rewards WITHOUT any connection. Self-installs via [RuntimeInitializeOnLoadMethod] so it
// needs NO scene edit (monetization lane, §9 isolated).
//
// FORCE IT ON in a WebGL build without touching prod defaults: append ?stakedemo=1 to the
// page URL (allow-listed in FeatureFlags.ApplyUrlActivationOnce) — flips ff.stakedemo ON
// for that session only. Off-web: set PlayerPrefs "ff.stakedemo" = 1 (or the editor menu).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Platform
{
    /// <summary>Boot hook that opens the stake-rewards demo panel (seeded mock stake) when
    /// <see cref="DeNelle.Core.FeatureFlags.StakeDemo"/> is ON. No-op otherwise.</summary>
    public static class StakeRewardsDemoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Pick up ?stakedemo=1 from the WebGL URL first (idempotent; safe on every platform).
            DeNelle.Core.FeatureFlags.ApplyUrlActivationOnce();

            if (!DeNelle.Core.FeatureFlags.StakeDemo)
                return;

            using var _ = FlowTrace.Enter("Stake", "StakeRewardsDemoBootstrap (ff.stakedemo ON)");

            // Seed a real-looking active stake WITHOUT any wallet connection (owner is a Genesis
            // holder ~1M staked SKR). Read-only mock — no funds move, nothing is custodied.
            StakeRewardsResolver.Query = new StakeRewardsResolver.MockStakeQuery(StakeRewardsResolver.DemoMockStakeSkr);

            var standing = StakeRewardsResolver.Resolve();
            FlowTrace.Step("Stake",
                $"Demo seeded: {standing.ActiveStake:N0} {standing.CurrencySymbol} -> " +
                $"tier '{standing.CurrentTier?.Name ?? "(none)"}', {standing.UnlockedRewards.Count} reward(s). Opening panel.");

            // Open on the next frame via a tiny driver so the UI/Canvas system is warm and the
            // panel survives the boot->hub scene load (the panel canvas is DontDestroyOnLoad).
            var driverGo = new GameObject("StakeRewardsDemoDriver");
            Object.DontDestroyOnLoad(driverGo);
            driverGo.AddComponent<StakeRewardsDemoDriver>();
        }
    }

    /// <summary>One-shot driver: waits a couple of frames after boot, opens the panel, then removes
    /// itself. Keeps the open off the RuntimeInitialize call stack (Canvas build is happier a frame in).</summary>
    internal sealed class StakeRewardsDemoDriver : MonoBehaviour
    {
        private int _frames;

        private void Update()
        {
            _frames++;
            if (_frames < 2) return;   // let the first scene settle a frame
            DeNelle.Core.UI.StakeRewardsPanel.Open();
            Destroy(gameObject);
        }
    }
}
