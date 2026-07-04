// =============================================================================
// StakeRewardsPanel — the Seekerthon "stake -> in-game rewards" display surface.
// -----------------------------------------------------------------------------
// PRESENTATION ONLY. Reads DeNelle.Core.Platform.StakeRewardsResolver (a read-only
// standing: active stake -> tier -> unlocked rewards) and renders it. It mutates NO
// game state, holds NO SKR, and never triggers a transfer — it just SHOWS the perks
// the player's ACTIVE NATIVE stake unlocks. The whole message the panel conveys:
// "active stake gives you in-game rewards, automatically, just by staking natively -
// no deposit, we never hold your SKR."
//
// Built on the shared ElarionUiKit Obsidian modal (black panel + gold trim + the ONE
// shared Close) — code-built uGUI only, procedural chrome path (frameName null) so it
// has NO gitignored-art dependency and can NEVER render blank in a WebGL build. Mobile-
// first: a compact centered column that reads well in a ~90s screen capture.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Platform;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// The read-only stake-rewards panel. Call <see cref="Open()"/> to build + show it (used by the
    /// Seekerthon demo bootstrap). Presentation only — it reflects <see cref="StakeRewardsResolver"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StakeRewardsPanel : MonoBehaviour
    {
        private ElarionUiKit.ObsidianModal _modal;
        private static StakeRewardsPanel _open;   // single live instance guard

        // Copy that carries the non-custodial / native-staking message, reused for the capture.
        private const string StakeNativeLine =
            "Stake natively at Stake.solanamobile - rewards apply automatically. We never hold your SKR.";
        private const string SmallThankYouLine =
            "A small thank-you for staking - modest perks, never pay-to-win.";

        // =====================================================================
        //  Open / close
        // =====================================================================

        /// <summary>Build + show the panel for the CURRENT resolver query (reads it once). Idempotent:
        /// a second call re-opens a fresh panel. Returns the live instance.</summary>
        public static StakeRewardsPanel Open()
        {
            return Open(StakeRewardsResolver.Resolve());
        }

        /// <summary>Build + show the panel for an explicit standing (tests / a seeded demo value).</summary>
        public static StakeRewardsPanel Open(StakeStanding standing)
        {
            using var _ = FlowTrace.Enter("Stake", "StakeRewardsPanel.Open");
            if (_open != null)
            {
                // Replace an already-open panel so we never stack duplicates on-screen.
                Object.Destroy(_open.gameObject);
                _open = null;
            }

            var host = new GameObject("StakeRewardsPanel");
            Object.DontDestroyOnLoad(host);   // survive the boot->hub scene load for the capture
            var panel = host.AddComponent<StakeRewardsPanel>();
            panel.Build(standing ?? StakeRewardsResolver.Resolve(0));
            _open = panel;
            return panel;
        }

        private void Close()
        {
            if (_open == this) _open = null;
            if (_modal != null && _modal.canvas != null) Object.Destroy(_modal.canvas);
            Object.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_open == this) _open = null;
        }

        // =====================================================================
        //  Build
        // =====================================================================

        private void Build(StakeStanding standing)
        {
            _modal = ElarionUiKit.BuildObsidianModal("StakeRewardsUI", "Stake Rewards",
                new Vector2(0.16f, 0.10f), new Vector2(0.84f, 0.92f), Close);

            if (_modal == null || _modal.chrome == null || _modal.chrome.content == null)
            {
                // Without a content root nothing can draw — surface it loudly (never a silent blank panel).
                FlowTrace.Fail("Stake", "StakeRewardsPanel.Build: Obsidian modal/content failed to build — panel cannot draw.");
                return;
            }

            var body = _modal.chrome.content.transform;
            string sym = standing != null ? standing.CurrencySymbol : "SKR";

            // --- Active stake (the headline number) ---
            string stakeText = standing != null && standing.HasStake
                ? $"Active Stake:  {standing.ActiveStake:N0} {sym}"
                : $"Active Stake:  0 {sym}";
            ElarionUiKit.Label(body, stakeText, 0.855f, 0.925f, ElarionUi.Gilt, 34,
                TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 1f, bold: true);

            // --- Tier line ---
            if (standing != null && standing.HasStake && standing.CurrentTier != null)
            {
                ElarionUiKit.Label(body, $"Tier:  {standing.CurrentTier.Name}", 0.795f, 0.855f,
                    ElarionUi.Gold, 26, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
                if (!string.IsNullOrEmpty(standing.CurrentTier.Tagline))
                    ElarionUiKit.Label(body, standing.CurrentTier.Tagline, 0.752f, 0.795f,
                        ElarionUi.ParchmentDim, 18, TextAlignmentOptions.Center, 0.06f, 0.94f);
            }
            else
            {
                ElarionUiKit.Label(body, "No active stake yet", 0.795f, 0.855f,
                    ElarionUi.ParchmentDim, 24, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            }

            // --- "Rewards Unlocked" header ---
            ElarionUiKit.Label(body, "Rewards Unlocked", 0.695f, 0.748f, ElarionUi.Gilt, 22,
                TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);

            // --- The unlocked reward list (scrollable well) ---
            var well = ElarionUiKit.Well(body.transform, new Vector2(0.06f, 0.235f), new Vector2(0.94f, 0.688f));
            BuildRewardList(well.transform, standing);

            // --- The message: native staking, automatic, non-custodial ---
            ElarionUiKit.Label(body, StakeNativeLine, 0.150f, 0.225f, ElarionUi.Parchment, 18,
                TextAlignmentOptions.Center, 0.05f, 0.95f);
            ElarionUiKit.Label(body, SmallThankYouLine, 0.110f, 0.150f, ElarionUi.Gold, 16,
                TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);

            FlowTrace.Step("Stake",
                $"StakeRewardsPanel built: stake={standing?.ActiveStake ?? 0} {sym}, " +
                $"tier='{standing?.CurrentTier?.Name ?? "(none)"}', rewards={standing?.UnlockedRewards?.Count ?? 0}.");
        }

        private void BuildRewardList(Transform well, StakeStanding standing)
        {
            IReadOnlyList<StakeReward> rewards = standing != null ? standing.UnlockedRewards : null;

            if (rewards == null || rewards.Count == 0)
            {
                ElarionUiKit.Label(well, "Stake SKR natively to unlock your first reward.", 0.40f, 0.60f,
                    ElarionUi.ParchmentDim, 18, TextAlignmentOptions.Center, 0.06f, 0.94f);
                return;
            }

            // A scroll column so a long unlock list stays reachable (mobile-safe). Cards are laid out
            // by a VerticalLayoutGroup; the ContentSizeFitter grows the content so it scrolls.
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(well, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // near-invisible raycast surface

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            for (int i = 0; i < rewards.Count; i++)
                BuildRewardRow(contentGo.transform, rewards[i]);
        }

        private void BuildRewardRow(Transform parent, StakeReward reward)
        {
            if (reward == null) return;

            var rowGo = new GameObject("reward-" + reward.Label, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);
            rowGo.GetComponent<LayoutElement>().preferredHeight = 78f;
            rowGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.30f);
            var row = rowGo.transform;

            // A small tone chip on the left marks the reward kind (badge/title/cosmetic/trickle).
            var chip = ElarionUiKit.AddImage(row, "KindChip",
                new Vector2(0.015f, 0.22f), new Vector2(0.045f, 0.78f), KindColor(reward.Kind), rounded: false);
            var chipImg = chip.GetComponent<Image>();
            if (chipImg != null) chipImg.raycastTarget = false;

            // Kind tag (tiny), reward label (bold), and the small-print detail.
            ElarionUiKit.Label(row, KindLabel(reward.Kind), 0.58f, 0.96f, ElarionUi.ParchmentDim, 13,
                TextAlignmentOptions.TopLeft, 0.075f, 0.55f);
            ElarionUiKit.Label(row, reward.Label, 0.50f, 0.96f, ElarionUi.Parchment, 18,
                TextAlignmentOptions.TopLeft, 0.075f, 0.98f, bold: true);
            ElarionUiKit.Label(row, reward.Detail, 0.06f, 0.50f, ElarionUi.ParchmentDim, 14,
                TextAlignmentOptions.TopLeft, 0.075f, 0.98f);
        }

        private static Color KindColor(StakeRewardKind kind)
        {
            switch (kind)
            {
                case StakeRewardKind.Badge:    return ElarionUi.Gold;
                case StakeRewardKind.Title:    return ElarionUi.Gilt;
                case StakeRewardKind.Cosmetic: return ElarionUi.Aether;
                case StakeRewardKind.Trickle:  return ElarionUi.Affordable;
                default:                       return ElarionUi.ParchmentDim;
            }
        }

        private static string KindLabel(StakeRewardKind kind)
        {
            switch (kind)
            {
                case StakeRewardKind.Badge:    return "BADGE";
                case StakeRewardKind.Title:    return "TITLE";
                case StakeRewardKind.Cosmetic: return "COSMETIC";
                case StakeRewardKind.Trickle:  return "TRICKLE";
                default:                       return "PERK";
            }
        }
    }
}
