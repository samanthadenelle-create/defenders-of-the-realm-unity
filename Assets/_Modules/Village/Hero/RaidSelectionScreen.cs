// =============================================================================
// RaidSelectionScreen — the Raids-tab grid of raid CARDS (screen 2 of
// docs/RAID_TROOP_UI.md). Code-built uGUI (NO UXML — UXML does not render in
// player builds, project hard rule), routed through the SHARED presentation kit
// (DeNelle.Core.UI.ElarionUiKit) so it reads as the SAME designed game as the
// town HUD / ShopPanel / TroopTrainingPanel: dark-wood + gold framing, gold serif
// title, framed cards.
// -----------------------------------------------------------------------------
// MIRRORS ShopPanel / TroopTrainingPanel: BuildModalCanvas (sortingOrder 31000 +
// overrideSorting, above the world-HUD band) + tap-outside Scrim + a framed
// dark-glass panel + a Header. The RAIDS banner heads the panel (Resources.Load,
// null-safe — decorative; the panel works without it). A scrollable grid of raid
// cards is built from SceneConfigCatalog.All, filtered to the 3 flagship enemy
// raids (raider_camp_small / fortified_garrison / mage_enclave).
//
// Each card reads SceneConfigDef: displayName (gold serif), difficulty (a colour-
// tinted badge: green/yellow/red = Regular/Hard/Extreme), recommendedClearTime
// (the 3-star target, rendered m:ss), and a reward hint from rewardMultiplier +
// shardDropChance (resource icon + an Echo-Shard hint). Tapping a card opens
// RaidDeployScreen.Open(def).
//
// ENTRY: static RaidSelectionScreen.Open() self-heals a host GameObject and opens
// the screen — call it from a Raids-tab button / dev panel. (No town button is
// wired here to avoid colliding with the other lane; see Open() docs.)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Village;

namespace DeNelle.Village.Hero
{
    public sealed class RaidSelectionScreen : MonoBehaviour
    {
        // The three flagship enemy raids, in card order (the spec's grid).
        private static readonly string[] FlagshipRaidIds =
        {
            "raider_camp_small",
            "fortified_garrison",
            "mage_enclave",
        };

        private GameObject _ui;
        private GameObject _contentRoot;
        private RectTransform _scrollContent;

        // Card pixel height in the scroll list (tall plaque — banner + badge + time + reward).
        private const float CardHeightPx = 168f;
        private const float CardGapPx    = 10f;

        // ── Entry hook ───────────────────────────────────────────────────────

        /// <summary>
        /// Self-healing static entry: finds or creates a host GameObject carrying a
        /// RaidSelectionScreen and opens the grid. The intended trigger is the town /
        /// castle Raids-tab button (or the dev panel) — wire that to call this. Not
        /// auto-wired to a town button here to avoid colliding with the parallel
        /// raids-tab lane.
        /// </summary>
        public static void Open()
        {
            var existing = FindFirstObjectByType<RaidSelectionScreen>();
            if (existing == null)
            {
                var host = new GameObject("RaidSelectionScreen");
                existing = host.AddComponent<RaidSelectionScreen>();
            }
            existing.OpenInternal();
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void OpenInternal()
        {
            Close();

            // Modal canvas + tap-outside scrim, both from the shared kit. Pin
            // sortingOrder 31000 + overrideSorting (mirrors ShopPanel) so the panel +
            // its scrim render ABOVE the world-HUD band but below the top overlays.
            _ui = ElarionUiKit.BuildModalCanvas("RaidSelectionScreenUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.16f, 0.06f), new Vector2(0.84f, 0.94f),
                                                   deep: true, packSpriteName: RpgUiCatalog.PanelWindowDark);
            var panel = panelGo.transform;

            // The RAIDS banner image heads the panel (decorative, null-safe).
            BuildBanner(panel);

            // Gold serif title under the banner.
            ElarionUiKit.Header(panel, "RAIDS", x0: 0.06f, x1: 0.94f, y0: 0.82f, y1: 0.89f);

            // Close (X) — top-right ornate pack-frame danger button (drawn over the banner).
            ElarionUiKit.ButtonPack(panel, "X", ElarionUiKit.ButtonKind.Danger,
                                    new Vector2(0.9f, 0.92f), new Vector2(0.985f, 0.985f), Close);

            // Content area (the card grid lives here).
            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.04f, 0.04f);
            cr.anchorMax = new Vector2(0.96f, 0.80f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            BuildCards();

            Debug.Log("[RaidSelectionScreen] Opened — raid card grid.");
        }

        // The RAIDS banner across the top of the panel. The asset lives at
        // Assets/Art/UI/Raids/Raids_banner.jpg — NOT under Resources, so a runtime
        // Resources.Load may miss it; this is decorative and fully null-safe (a soft
        // dark-stone niche backs it so the header still reads if the art is absent).
        private void BuildBanner(Transform panel)
        {
            var bannerHost = ElarionUiKit.Niche(panel, new Vector2(0.04f, 0.83f), new Vector2(0.96f, 0.965f));
            var bImg = bannerHost.GetComponent<Image>();
            if (bImg != null) bImg.raycastTarget = false;

            Sprite banner = null;
            try { banner = Resources.Load<Sprite>("Raids/Raids_banner"); }
            catch { banner = null; }
            if (banner == null)
            {
                try { banner = Resources.Load<Sprite>("UI/Raids/Raids_banner"); }
                catch { banner = null; }
            }
            if (banner != null && bImg != null)
            {
                bImg.sprite = banner;
                bImg.type = Image.Type.Simple;
                bImg.preserveAspect = true;
                bImg.color = Color.white;
            }
        }

        private void BuildCards()
        {
            ClearContent();

            // Gather the 3 flagship enemy raids from the catalog (in card order).
            var defs = new List<SceneConfigDef>();
            foreach (var id in FlagshipRaidIds)
            {
                var def = SceneConfigCatalog.Find(id);
                if (def != null) defs.Add(def);
            }

            // Fallback: if the named flagship ids aren't present, show all enemy raids
            // so the screen is never empty when the catalog loaded SOMETHING.
            if (defs.Count == 0)
            {
                foreach (var def in SceneConfigCatalog.All)
                    if (def != null && def.IsEnemy) defs.Add(def);
            }

            var listRoot = BuildScrollContent(defs.Count);

            if (defs.Count == 0)
            {
                ElarionUiKit.Label(listRoot, "No raids available.", 0.4f, 0.6f, ElarionUi.ParchmentDim,
                    ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center);
                FinalizeScroll();
                Debug.LogWarning("[RaidSelectionScreen] No enemy raids in SceneConfigCatalog — empty grid.");
                return;
            }

            foreach (var def in defs)
                CreateRaidCard(listRoot, def);

            FinalizeScroll();
        }

        // One framed raid plaque: difficulty-tinted frame, fortress name (gold serif),
        // a difficulty badge, the 3-star target time (m:ss), and a reward hint
        // (resource + Echo-Shard). The whole card is one tap target -> RaidDeployScreen.
        private void CreateRaidCard(Transform parent, SceneConfigDef def)
        {
            Color tint = DifficultyColor(def.difficulty);

            // Card root: a Cell tile (LayoutElement-sized for the scroll layout) with a
            // difficulty-tinted inner rim, and a Button so the whole plaque taps.
            var card = new GameObject("RaidCard_" + def.id, typeof(Image), typeof(Button), typeof(LayoutElement));
            card.transform.SetParent(parent, false);
            var le = card.GetComponent<LayoutElement>();
            le.preferredHeight = CardHeightPx;
            le.minHeight = CardHeightPx;
            var cardImg = card.GetComponent<Image>();
            cardImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(cardImg);
            var cardBtn = card.GetComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            ElarionUiKit.StyleButtonColors(cardBtn);
            var defCopy = def;
            cardBtn.onClick.AddListener(() => OnCardTapped(defCopy));

            // Difficulty-tinted inner rim = the framed plaque edge.
            ElarionUiKit.AddInnerRim(card, new Color(tint.r, tint.g, tint.b, 0.7f));

            // Fortress name — gold serif title, top band.
            string name = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            var nameLabel = ElarionUiKit.Label(card.transform, name, 0.66f, 0.94f, ElarionUi.Gilt,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Left, 0.05f, 0.70f, bold: true);
            nameLabel.raycastTarget = false;

            // Difficulty badge — colour-tinted chip, top-right.
            var badge = ElarionUiKit.AddImage(card.transform, "DiffBadge",
                new Vector2(0.72f, 0.68f), new Vector2(0.96f, 0.92f),
                new Color(tint.r, tint.g, tint.b, 0.85f));
            badge.GetComponent<Image>().raycastTarget = false;
            var badgeLbl = ElarionUiKit.Label(badge.transform, DifficultyLabel(def.difficulty), 0f, 1f,
                ElarionUi.Ink, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            badgeLbl.raycastTarget = false;

            // 3-star target time — m:ss in gilt, mid band.
            var timeLabel = ElarionUiKit.Label(card.transform,
                "★★★  Target: " + FormatTime(def.recommendedClearTime), 0.38f, 0.60f,
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.05f, 0.95f);
            timeLabel.raycastTarget = false;

            // Reward hint — resource multiplier + Echo-Shard drop, bottom band.
            var rewardLabel = ElarionUiKit.Label(card.transform, RewardHint(def), 0.10f, 0.32f,
                ElarionUi.Affordable, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.05f, 0.95f, bold: true);
            rewardLabel.raycastTarget = false;
        }

        private void OnCardTapped(SceneConfigDef def)
        {
            if (def == null) return;
            RaidDeployScreen.Open(def);
            // Leave the selection screen open underneath so closing the deploy screen
            // returns the player to the raid grid (mirrors a drill-down flow).
        }

        // ── Card data helpers (read straight off SceneConfigDef) ───────────────

        // Difficulty -> tint: green (Regular) / yellow (Hard) / red (Extreme).
        private static Color DifficultyColor(string difficulty)
        {
            switch ((difficulty ?? "Regular").Trim().ToLowerInvariant())
            {
                case "extreme": return ElarionUi.Danger;                       // red
                case "hard":    return new Color(0.92f, 0.78f, 0.28f, 1f);      // yellow/gold
                default:        return ElarionUi.Affordable;                    // green (Regular)
            }
        }

        private static string DifficultyLabel(string difficulty)
        {
            if (string.IsNullOrEmpty(difficulty)) return "Regular";
            string d = difficulty.Trim();
            return char.ToUpper(d[0]) + (d.Length > 1 ? d.Substring(1).ToLowerInvariant() : "");
        }

        // Seconds -> m:ss. A non-positive time reads "--:--".
        private static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--";
            int total = Mathf.RoundToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            return m + ":" + s.ToString("00");
        }

        // A short reward hint from rewardMultiplier + shardDropChance: a resource yield
        // multiplier ("x1.5 Loot") plus an Echo-Shard drop chance ("+ Echo Shard 25%").
        private static string RewardHint(SceneConfigDef def)
        {
            var parts = new List<string>();
            float mult = def.rewardMultiplier <= 0f ? 1f : def.rewardMultiplier;
            parts.Add("◆ x" + mult.ToString("0.#") + " Loot");
            if (def.shardDropChance > 0f)
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp01(def.shardDropChance) * 100f);
                parts.Add("Echo Shard " + pct + "%");
            }
            return string.Join("   ", parts);
        }

        // ── Scroll list (the ShopPanel anti-collapse rendering mechanism) ──────

        private Transform BuildScrollContent(int rowCount)
        {
            var well = ElarionUiKit.Well(_contentRoot.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null) wImg.raycastTarget = false;

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_contentRoot.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f); // near-invisible; ScrollRect needs a raycast graphic

            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(0f, 0f); // height driven by the ContentSizeFitter

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = CardGapPx;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            _scrollContent = cr;
            return content.transform;
        }

        private void FinalizeScroll()
        {
            if (_scrollContent == null) return;
            Canvas.ForceUpdateCanvases();
            var contentArea = _contentRoot != null ? _contentRoot.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        private void ClearContent()
        {
            _scrollContent = null;
            if (_contentRoot == null) return;
            for (int i = _contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentRoot.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        public void Close()
        {
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _scrollContent = null;
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }
    }
}
