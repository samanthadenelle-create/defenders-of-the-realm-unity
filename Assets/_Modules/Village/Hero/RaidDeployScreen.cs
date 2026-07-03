// =============================================================================
// RaidDeployScreen — the PRE-raid tactical deploy screen (screen 3 of
// docs/RAID_TROOP_UI.md). Code-built uGUI (NO UXML — UXML does not render in
// player builds, project hard rule), routed through the SHARED presentation kit
// (DeNelle.Core.UI.ElarionUiKit) so it reads as the SAME designed game as the
// town HUD / ShopPanel / TroopTrainingPanel: dark-wood + gold framing, gold serif
// header, framed portraits, scroll list, and a big glowing DEPLOY CTA.
// -----------------------------------------------------------------------------
// MIRRORS ShopPanel / TroopTrainingPanel: BuildModalCanvas (sortingOrder 31050 —
// one band ABOVE RaidSelectionScreen so the deploy screen sits over the grid) +
// tap-outside Scrim + a framed dark-glass panel + a Header.
//
// LAYOUT (portrait):
//   Header   "RAID: <displayName>" + difficulty badge + ★★★ target time.
//   Left     Hero + Companions row (PartyMemberIds → small framed class portraits),
//            then a scrollable troop-card list from ArmyStorage.GetDeployable()
//            grouped by TroopDefId (icon glyph, name, owned count), with an army-cap
//            indicator (SlotsUsed / MaxArmySize).
//   Center   a battle-preview placeholder + an "Estimated Clear Time" readout
//            (FIRST PASS: static from the config; TODO live update as troops change).
//   Bottom   total troops / a simple power rating (sum of deployable), an
//            "Auto Recommend" button (FIRST PASS: stub — selects all deployable),
//            and a big glowing DEPLOY button -> SceneRouter.GoRaid(def.sceneName).
//
// Open(SceneConfigDef) / Close() API; RaidSelectionScreen taps a card -> Open(def).
//
// TODO (out of first-pass scope, noted for the next increment):
//  - Live "Estimated Clear Time" that recomputes as troops are added/removed (this
//    pass shows the config's recommended/2-star band statically).
//  - A real SCOUT REPORT (wall tier / AA-tower density / choke vs open / boss) in
//    the intel area to drive the soft RPS + a meaningful Auto-Recommend comp.
//  - Per-troop quantity sliders + deploy slots (this pass shows owned counts; the
//    in-raid deploy tray handles the actual unit placement).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.UI;   // StarRatingRow (tofu-proof star row)

namespace DeNelle.Village.Hero
{
    public sealed class RaidDeployScreen : MonoBehaviour
    {
        private GameObject _ui;
        private SceneConfigDef _def;
        private GameObject _troopListRoot;
        private RectTransform _scrollContent;

        private const float RowHeightPx = 60f;
        private const float RowGapPx    = 3f;

        // ── Entry ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Self-healing static entry: finds or creates a host GameObject carrying a
        /// RaidDeployScreen and opens it for <paramref name="def"/> (the raid the
        /// player tapped on the selection grid).
        /// </summary>
        public static void Open(SceneConfigDef def)
        {
            if (def == null) { Debug.LogWarning("[RaidDeployScreen] Open(null) ignored."); return; }
            var existing = FindAnyObjectByType<RaidDeployScreen>();
            if (existing == null)
            {
                var host = new GameObject("RaidDeployScreen");
                existing = host.AddComponent<RaidDeployScreen>();
            }
            existing.OpenInternal(def);
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void OpenInternal(SceneConfigDef def)
        {
            Close();
            _def = def;

            // 31050: one band above RaidSelectionScreen (31000) so deploy sits over the grid.
            _ui = ElarionUiKit.BuildModalCanvas("RaidDeployScreenUI", 31050);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // WO-562: canonical obsidian chrome (black + gold trim + shared Close) replaces
            // PanelFramed + a per-panel "X" Danger button. BuildHeader still adds the deploy sub-title.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "",
                new Vector2(0.10f, 0.05f), new Vector2(0.90f, 0.95f), Close, withBackdrop: false);
            var panel = chrome.content.transform;

            BuildHeader(panel);

            // LEFT column — Your Forces (party row + troop list + cap indicator).
            BuildLeftColumn(panel);

            // CENTER/RIGHT column — battle preview + estimated clear time + summary.
            BuildCenterColumn(panel);

            // BOTTOM — Auto Recommend + the big glowing DEPLOY CTA.
            BuildDeployBar(panel);

            Debug.Log($"[RaidDeployScreen] Opened for raid '{_def?.id}' -> scene '{_def?.sceneName}'.");
        }

        private void BuildHeader(Transform panel)
        {
            string name = _def != null && !string.IsNullOrEmpty(_def.displayName) ? _def.displayName : (_def != null ? _def.id : "Raid");
            ElarionUiKit.Header(panel, "RAID: " + name, x0: 0.05f, x1: 0.90f, y0: 0.93f, y1: 0.985f);

            // Difficulty badge + ★★★ target time, just under the header.
            Color tint = DifficultyColor(_def != null ? _def.difficulty : null);
            var badge = ElarionUiKit.AddImage(panel, "DiffBadge",
                new Vector2(0.05f, 0.885f), new Vector2(0.30f, 0.925f),
                new Color(tint.r, tint.g, tint.b, 0.85f));
            badge.GetComponent<Image>().raycastTarget = false;
            var badgeLbl = ElarionUiKit.Label(badge.transform, DifficultyLabel(_def != null ? _def.difficulty : null),
                0f, 1f, ElarionUi.Ink, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            badgeLbl.raycastTarget = false;

            // Tofu fix (2026-07-02): ★ (U+2605) is in NO project SDF font (scanned —
            // zero m_Unicode:9733 hits), so the old "★★★" text rendered as boxes in
            // builds. Procedural gold diamonds instead (EndStateView's pattern via
            // the shared StarRatingRow), then a plain font-safe "Target:" label.
            StarRatingRow.Build(panel, 3, 3, 0.45f, 0.885f, 0.515f, 0.925f, sizePx: 12f);
            var timeLbl = ElarionUiKit.Label(panel, "Target: " + FormatTime(_def != null ? _def.recommendedClearTime : 0f),
                0.885f, 0.925f, ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.525f, 0.90f, bold: true);
            timeLbl.raycastTarget = false;
        }

        // LEFT — Your Forces: hero + companions portrait row, then the scrollable
        // troop-card list grouped by TroopDefId, then the army-cap indicator.
        private void BuildLeftColumn(Transform panel)
        {
            // Section label.
            var lbl = ElarionUiKit.Label(panel, "YOUR FORCES", 0.835f, 0.875f, ElarionUi.Gilt,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Left, 0.05f, 0.50f, bold: true);
            lbl.raycastTarget = false;

            // Hero + Companions portrait row.
            BuildPartyRow(panel);

            // Army-cap indicator (SlotsUsed / MaxArmySize).
            var army = Army();
            string capText;
            if (army != null)
            {
                int used = army.SlotsUsed(TroopDialogueCommands.SlotOf);
                capText = $"Army: {used} / {army.MaxArmySize} slots";
            }
            else capText = "Army: —";
            var capLbl = ElarionUiKit.Label(panel, capText, 0.70f, 0.735f, ElarionUi.Parchment,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.05f, 0.50f, bold: true);
            capLbl.raycastTarget = false;

            // Troop list content area (left half, below the party row).
            _troopListRoot = new GameObject("TroopListArea", typeof(RectTransform));
            _troopListRoot.transform.SetParent(panel, false);
            var cr = _troopListRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.05f, 0.14f);
            cr.anchorMax = new Vector2(0.49f, 0.69f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            BuildTroopList();
        }

        // Hero + companions as small framed class portraits across the top of the
        // left column. The hero's class = GameState.HeroClass; companions = the class
        // strings in PartyMemberIds. (Hero is added explicitly so the row always shows
        // the player even before any companion has joined.)
        private void BuildPartyRow(Transform panel)
        {
            var classes = PartyClasses();

            // Row host.
            var rowHost = new GameObject("PartyRow", typeof(RectTransform));
            rowHost.transform.SetParent(panel, false);
            var rr = rowHost.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.05f, 0.745f);
            rr.anchorMax = new Vector2(0.49f, 0.83f);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;

            int n = classes.Count;
            if (n == 0) return;
            float slot = 1f / Mathf.Max(n, 1);
            for (int i = 0; i < n; i++)
            {
                string cls = classes[i];
                float x0 = i * slot + 0.02f;
                float x1 = (i + 1) * slot - 0.02f;

                // Framed portrait niche.
                var niche = ElarionUiKit.Niche(rowHost.transform, new Vector2(x0, 0.18f), new Vector2(x1, 0.98f));
                niche.GetComponent<Image>().raycastTarget = false;
                var portrait = ElarionUiKit.Portrait(niche.transform, ElarionUiKit.PortraitForClass(cls), active: i == 0);

                // Name under the portrait (the canon companion name for the class).
                var nameLbl = ElarionUiKit.Label(rowHost.transform, CompanionName(cls), 0f, 0.18f, ElarionUi.Parchment,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, x0, x1, bold: true);
                nameLbl.raycastTarget = false;
            }
        }

        // The scrollable troop list, grouped by TroopDefId (one row per troop type the
        // player owns + can deploy), each row = icon glyph + name + owned count.
        private void BuildTroopList()
        {
            ClearTroopList();

            var army = Army();
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            if (army != null)
            {
                foreach (var t in army.GetDeployable())
                {
                    if (t == null || string.IsNullOrEmpty(t.TroopDefId)) continue;
                    if (!counts.ContainsKey(t.TroopDefId)) { counts[t.TroopDefId] = 0; order.Add(t.TroopDefId); }
                    counts[t.TroopDefId]++;
                }
            }

            var listRoot = BuildScrollContent(order.Count);

            if (order.Count == 0)
            {
                ElarionUiKit.Label(listRoot, "No troops trained yet. Visit the Barracks.", 0.3f, 0.7f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center);
                FinalizeScroll();
                return;
            }

            foreach (var defId in order)
                CreateTroopRow(listRoot, defId, counts[defId]);

            FinalizeScroll();
        }

        private void CreateTroopRow(Transform parent, string troopDefId, int owned)
        {
            var def = TroopCatalog.Find(troopDefId);
            string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : troopDefId;

            var row = new GameObject("TroopRow_" + troopDefId, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = row.GetComponent<Image>();
            rowImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);

            // Icon well (glyph placeholder — troops have no portrait art yet).
            var well = ElarionUiKit.AddImage(row.transform, "IconWell",
                new Vector2(0.03f, 0.15f), new Vector2(0.20f, 0.85f), new Color(0f, 0f, 0f, 0.30f));
            well.GetComponent<Image>().raycastTarget = false;
            string glyph = (def != null && def.Role != null && def.Role.ToLowerInvariant().Contains("ranged")) ? "»" : "⚔";
            var ic = ElarionUiKit.Label(well.transform, glyph, 0f, 1f, ElarionUi.Gilt,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            ic.raycastTarget = false;

            var nameLbl = ElarionUiKit.Label(row.transform, name, 0.45f, 0.95f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.23f, 0.98f, bold: true);
            nameLbl.raycastTarget = false;

            var ownedLbl = ElarionUiKit.Label(row.transform, "x" + owned, 0.05f, 0.5f, ElarionUi.Affordable,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.23f, 0.98f, bold: true);
            ownedLbl.raycastTarget = false;
        }

        // CENTER/RIGHT — battle-preview placeholder + estimated clear time + summary.
        private void BuildCenterColumn(Transform panel)
        {
            // Battle preview placeholder niche (the RaidBaseGenerator thumbnail goes here later).
            var preview = ElarionUiKit.Niche(panel, new Vector2(0.52f, 0.42f), new Vector2(0.95f, 0.83f));
            preview.GetComponent<Image>().raycastTarget = false;
            var pvLbl = ElarionUiKit.Label(preview.transform, "Battle Preview\n(enemy base)", 0.40f, 0.60f,
                ElarionUi.ParchmentDim, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
            pvLbl.raycastTarget = false;

            // Estimated Clear Time readout (FIRST PASS: static from the config; TODO live).
            string est = _def != null ? FormatTime(_def.twoStarTime > 0f ? _def.twoStarTime : _def.recommendedClearTime) : "--:--";
            var estLbl = ElarionUiKit.Label(panel, "Est. Clear Time: ~" + est, 0.355f, 0.40f, ElarionUi.Gilt,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.52f, 0.95f, bold: true);
            estLbl.raycastTarget = false;

            // Summary — total deployable troops + a simple power rating (sum of deployable).
            int totalTroops = DeployableCount();
            int power = PowerRating();
            var sumLbl = ElarionUiKit.Label(panel, $"Troops: {totalTroops}    Power: {power}", 0.30f, 0.35f,
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.52f, 0.95f, bold: true);
            sumLbl.raycastTarget = false;
        }

        // BOTTOM — Auto Recommend (stub) + the big glowing DEPLOY CTA.
        private void BuildDeployBar(Transform panel)
        {
            // Auto Recommend — FIRST PASS stub: selects all deployable (no comp logic yet).
            ElarionUiKit.Button(panel, "Auto Recommend", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.10f, 0.05f), new Vector2(0.42f, 0.12f), OnAutoRecommend);

            // DEPLOY — the big glowing primary CTA. Confirm-green with a gilt ember glow
            // ring behind it so it reads as the dominant action.
            var glow = ElarionUiKit.AddImage(panel, "DeployGlow",
                new Vector2(0.475f, 0.035f), new Vector2(0.915f, 0.135f),
                new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.35f));
            glow.GetComponent<Image>().raycastTarget = false;

            var deployBtn = ElarionUiKit.Button(panel, "DEPLOY", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.49f, 0.05f), new Vector2(0.90f, 0.12f), OnDeploy);
            // Gold-ink-on-green reads as the ember CTA; keep it enabled (the raid can be
            // entered to scout even with no troops — the in-raid tray handles placement).
            if (deployBtn != null) deployBtn.interactable = _def != null && !string.IsNullOrEmpty(_def.sceneName);
        }

        private void OnAutoRecommend()
        {
            // FIRST PASS stub: "select all deployable" is the whole roster already shown.
            // A real comp picker (driven by a scout report) is a later increment (see TODO).
            int n = DeployableCount();
            Debug.Log($"[RaidDeployScreen] Auto Recommend (stub) — would deploy all {n} deployable troop(s).");
        }

        private void OnDeploy()
        {
            if (_def == null || string.IsNullOrEmpty(_def.sceneName))
            {
                Debug.LogWarning("[RaidDeployScreen] DEPLOY: no scene to load for this raid.");
                return;
            }
            Debug.Log($"[RaidDeployScreen] DEPLOY -> SceneRouter.GoRaid('{_def.sceneName}').");
            // SHARED CONTRACT: SceneRouter.GoRaid(sceneName) loads the raid scene; the
            // in-raid deploy tray handles the actual unit placement.
            DeNelle.Core.SceneRouter.GoRaid(_def.sceneName);
        }

        // ── Data helpers ───────────────────────────────────────────────────────

        // The persisted army roster (GameState.Army), null when no save service is live.
        private static ArmyStorage Army()
        {
            var svc = GameStateService.Instance;
            return svc != null && svc.State != null ? svc.State.Army : null;
        }

        // Hero class first, then companion classes from PartyMemberIds (deduped).
        private static List<string> PartyClasses()
        {
            var list = new List<string>();
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;

            // Hero's own class.
            if (state != null && state.HeroClass != HeroClassOpt.None)
                list.Add(state.HeroClass.ToString());

            // Companions (class-name strings).
            if (state != null && state.PartyMemberIds != null)
            {
                foreach (var id in state.PartyMemberIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!list.Contains(id)) list.Add(id);
                }
            }

            // Always show at least the hero placeholder so the row never reads empty.
            if (list.Count == 0) list.Add("Knight");
            return list;
        }

        // The canon companion name for a class word (Wizard=Thrain, Knight=Grom,
        // Ranger=Sylas, Healer/Cleric=Elara, Mage=Thrain). Unknown -> the class word.
        private static string CompanionName(string cls)
        {
            switch ((cls ?? "").Trim().ToLowerInvariant())
            {
                case "mage":
                case "wizard": return "Thrain";
                case "knight": return "Grom";
                case "ranger": return "Sylas";
                case "cleric":
                case "healer": return "Elara";
                default: return string.IsNullOrEmpty(cls) ? "Hero" : cls;
            }
        }

        private static int DeployableCount()
        {
            var army = Army();
            if (army == null) return 0;
            int n = 0;
            foreach (var t in army.GetDeployable()) if (t != null) n++;
            return n;
        }

        // A simple power rating: sum of each deployable troop's AttackDamage * veterancy
        // multiplier, rounded. A coarse first-pass scalar (the scout-report-driven soft
        // RPS rating is a later increment).
        private static int PowerRating()
        {
            var army = Army();
            if (army == null) return 0;
            float total = 0f;
            foreach (var t in army.GetDeployable())
            {
                if (t == null) continue;
                var def = TroopCatalog.Find(t.TroopDefId);
                float atk = def != null ? def.AttackDamage : 10f;
                total += atk * t.DamageMultiplier;
            }
            return Mathf.RoundToInt(total);
        }

        private static Color DifficultyColor(string difficulty)
        {
            switch ((difficulty ?? "Regular").Trim().ToLowerInvariant())
            {
                case "extreme": return ElarionUi.Danger;
                case "hard":    return new Color(0.92f, 0.78f, 0.28f, 1f);
                default:        return ElarionUi.Affordable;
            }
        }

        private static string DifficultyLabel(string difficulty)
        {
            if (string.IsNullOrEmpty(difficulty)) return "Regular";
            string d = difficulty.Trim();
            return char.ToUpper(d[0]) + (d.Length > 1 ? d.Substring(1).ToLowerInvariant() : "");
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--";
            int total = Mathf.RoundToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        // ── Scroll list (the ShopPanel anti-collapse rendering mechanism) ──────

        private Transform BuildScrollContent(int rowCount)
        {
            var well = ElarionUiKit.Well(_troopListRoot.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null) wImg.raycastTarget = false;

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_troopListRoot.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f);

            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = RowGapPx;
            vlg.padding = new RectOffset(3, 3, 3, 3);
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
            var contentArea = _troopListRoot != null ? _troopListRoot.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        private void ClearTroopList()
        {
            _scrollContent = null;
            if (_troopListRoot == null) return;
            for (int i = _troopListRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _troopListRoot.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        public void Close()
        {
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _troopListRoot = null;
            _scrollContent = null;
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }
    }
}
