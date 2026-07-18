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
        private RaidDeployVM _vm;                       // owns the party/army/power math (no GameState in the View)
        private RectTransform _troopListArea;          // list region inside the body zone
        private ElarionUiKit.ScrollZoneHandle _scroll; // kit fit-or-scroll handle (§1.14)

        // Cached self-instance so the static entry never FindObjectsByType-scans the scene.
        private static RaidDeployScreen _instance;

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
            var existing = _instance;
            if (existing == null)
            {
                var host = new GameObject("RaidDeployScreen");
                existing = host.AddComponent<RaidDeployScreen>();   // Awake caches _instance
            }
            existing.OpenInternal(def);
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void OpenInternal(SceneConfigDef def)
        {
            Close();

            // VM FIRST — it resolves the army roster + party + troop facts from GameState/
            // TroopCatalog, so this View never touches either.
            _vm = RaidDeployVM.CreateDefault(def, Close);

            // 31050: one band above RaidSelectionScreen (31000) so deploy sits over the grid.
            _ui = ElarionUiKit.BuildModalCanvas("RaidDeployScreenUI", 31050);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // WO-562: canonical obsidian chrome (black + gold trim + shared Close) replaces
            // PanelFramed + a per-panel "X" Danger button. The chrome's header zone carries the
            // ONE title (UI matrix 2026-07-03: the extra procedural Header was a duplicate);
            // BuildHeader now adds only the badge / stars / target-time sub-row.
            // WO-714 P10: a raw id is never player-visible — missing displayName routes
            // through the ONE kit formatter.
            string raidName = !string.IsNullOrEmpty(_vm.DisplayNameRaw)
                ? _vm.DisplayNameRaw
                : (!string.IsNullOrEmpty(_vm.RaidId) ? ElarionUiKit.SpacedDisplayName(_vm.RaidId) : "Raid");
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "RAID: " + raidName,
                new Vector2(0.10f, 0.05f), new Vector2(0.90f, 0.95f), Close, withBackdrop: false,
                frameName: RpgUiCatalog.FrameCore);

            // WO-714 W4: ALL content drops into the FACTORY zones (chrome.layout.body /
            // .footer) — the kit owns the close-band reservation and the frame-filigree
            // margins, so the years of hand-tuned "dodge the medallion / dodge the Close"
            // panel fractions below are retired. Fractions in the builders are OF THE ZONE.
            var body = chrome.layout != null && chrome.layout.body != null
                ? (Transform)chrome.layout.body : chrome.content.transform;
            var footer = chrome.layout != null && chrome.layout.footer != null
                ? (Transform)chrome.layout.footer : body;

            // WO-714 P8: the ONE shared open ease (scale target = the panel rect).
            ElarionUiKit.AttachPanelOpenFx(_ui,
                chrome.root != null ? chrome.root.transform as RectTransform : null);

            BuildHeader(body);

            // LEFT column — Your Forces (party row + troop list + cap indicator).
            BuildLeftColumn(body);

            // CENTER/RIGHT column — battle preview + estimated clear time + summary.
            BuildCenterColumn(body);

            // FOOTER action strip — Auto Recommend + the big glowing DEPLOY CTA.
            BuildDeployBar(footer);

            Debug.Log($"[RaidDeployScreen] Opened for raid '{_vm.RaidId}' -> scene '{_vm.SceneName}'.");
        }

        // Sub-row (difficulty badge + star row + target time) across the TOP band of the
        // BODY zone. The chrome's header zone carries the ONE title; the body zone starts
        // below the FrameCore medallion socket, so no per-screen dodge fractions remain
        // (WO-714 W4 — the old sweep-9413 / #29 hand-tuned offsets are the kit's job now).
        private void BuildHeader(Transform body)
        {
            Color tint = DifficultyColor(_vm != null ? _vm.Difficulty : null);
            var badge = ElarionUiKit.AddImage(body, "DiffBadge",
                new Vector2(0.00f, 0.945f), new Vector2(0.20f, 1.00f),
                new Color(tint.r, tint.g, tint.b, 0.85f));
            badge.GetComponent<Image>().raycastTarget = false;
            var badgeLbl = ElarionUiKit.Label(badge.transform, DifficultyLabel(_vm != null ? _vm.Difficulty : null),
                0f, 1f, ElarionUi.Ink, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            badgeLbl.raycastTarget = false;

            // Tofu fix (2026-07-02): ★ (U+2605) is in NO project SDF font (scanned —
            // zero m_Unicode:9733 hits), so the old "★★★" text rendered as boxes in
            // builds. Procedural gold diamonds instead (EndStateView's pattern via
            // the shared StarRatingRow), then a plain font-safe "Target:" label.
            StarRatingRow.Build(body, 3, 3, 0.23f, 0.945f, 0.32f, 1.00f, sizePx: 12f);
            var timeLbl = ElarionUiKit.Label(body, "Target: " + FormatTime(_vm != null ? _vm.TargetTime : 0f),
                0.945f, 1.00f, ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.335f, 0.75f, bold: true);
            timeLbl.raycastTarget = false;
        }

        // LEFT — Your Forces: hero + companions portrait row, then the scrollable
        // troop-card list grouped by TroopDefId, then the army-cap indicator.
        private void BuildLeftColumn(Transform body)
        {
            // Section label — top of the left half of the body zone, below the sub-row.
            var lbl = ElarionUiKit.Label(body, "YOUR FORCES", 0.855f, 0.915f, ElarionUi.Gilt,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Left, 0.00f, 0.48f, bold: true);
            lbl.raycastTarget = false;

            // Hero + Companions portrait row.
            BuildPartyRow(body);

            // Army-cap indicator (VM-computed SlotsUsed / MaxArmySize).
            var capLbl = ElarionUiKit.Label(body, _vm != null ? _vm.ArmyCapText : "Army: -", 0.635f, 0.685f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.00f, 0.48f, bold: true);
            capLbl.raycastTarget = false;

            // Troop list region (left half of the body zone, below the cap line). The body
            // zone's bottom already sits ABOVE the footer + Close band (factory reservation,
            // WO-714 P6) — the old hand-computed 0.19 Close-dodge is retired.
            var listGo = new GameObject("TroopListArea", typeof(RectTransform));
            listGo.transform.SetParent(body, false);
            _troopListArea = listGo.GetComponent<RectTransform>();
            _troopListArea.anchorMin = new Vector2(0.00f, 0.00f);
            _troopListArea.anchorMax = new Vector2(0.48f, 0.615f);
            _troopListArea.offsetMin = Vector2.zero;
            _troopListArea.offsetMax = Vector2.zero;

            BuildTroopList();
        }

        // Hero + companions as small framed class portraits across the top of the
        // left column. The hero's class = GameState.HeroClass; companions = the class
        // strings in PartyMemberIds. (Hero is added explicitly so the row always shows
        // the player even before any companion has joined.)
        private void BuildPartyRow(Transform body)
        {
            var classes = _vm != null ? _vm.PartyClasses : new List<string>();

            // Row host — left half of the body zone, below "YOUR FORCES".
            var rowHost = new GameObject("PartyRow", typeof(RectTransform));
            rowHost.transform.SetParent(body, false);
            var rr = rowHost.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.00f, 0.700f);
            rr.anchorMax = new Vector2(0.48f, 0.845f);
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
                var nameLbl = ElarionUiKit.Label(rowHost.transform, _vm != null ? _vm.CompanionName(cls) : cls,
                    0f, 0.18f, ElarionUi.Parchment,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, x0, x1, bold: true);
                nameLbl.raycastTarget = false;
            }
        }

        // The scrollable troop list, grouped by TroopDefId (one row per troop type the
        // player owns + can deploy), each row = icon glyph + name + owned count.
        private void BuildTroopList()
        {
            ClearTroopList();

            var troops = _vm != null ? _vm.Troops : null;
            if (troops == null || troops.Count == 0)
            {
                // Empty state sits directly on the list area (a stretched label inside the
                // scroll column reports height 0 under the kit's childControlHeight:false law).
                ElarionUiKit.Label(_troopListArea, "No troops trained yet. Visit the Barracks.", 0.3f, 0.7f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center);
                return;
            }

            // WO-714 W4: the ONE kit scroll zone (§1.14) replaces the hand-rolled
            // viewport/content/fitter plumbing — screens add no scroll plumbing of their own.
            _scroll = ElarionUiKit.MakeScrollZone(_troopListArea, spacing: RowGapPx, padding: 3);
            foreach (var item in troops)
                CreateTroopRow(_scroll.content, item);

            FinalizeScroll();
        }

        private void CreateTroopRow(Transform parent, DeNelle.Core.UI.Mvvm.ItemVM item)
        {
            string troopDefId = item.Id;
            int owned = item.Price;   // owned count carried on Price by the VM
            // WO-714 P10: a raw troopDefId is never player-visible.
            string name = !string.IsNullOrEmpty(item.Name)
                ? item.Name : ElarionUiKit.SpacedDisplayName(troopDefId);

            var row = new GameObject("TroopRow_" + troopDefId, typeof(Image));
            row.transform.SetParent(parent, false);
            // Kit scroll-column row law (MakeScrollZone runs childControlHeight:false): rows
            // carry their own height via sizeDelta, not a LayoutElement.
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, RowHeightPx);
            var rowImg = row.GetComponent<Image>();
            rowImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);

            // Icon well (glyph placeholder — troops have no portrait art yet).
            var well = ElarionUiKit.AddImage(row.transform, "IconWell",
                new Vector2(0.03f, 0.15f), new Vector2(0.20f, 0.85f), new Color(0f, 0f, 0f, 0.30f));
            well.GetComponent<Image>().raycastTarget = false;
            string glyph = (_vm != null && _vm.IsRanged(troopDefId)) ? "RNG" : "MEL";
            var ic = ElarionUiKit.Label(well.transform, glyph, 0f, 1f, ElarionUi.Gilt,
                ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            ic.raycastTarget = false;

            var nameLbl = ElarionUiKit.Label(row.transform, name, 0.45f, 0.95f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.23f, 0.98f, bold: true);
            nameLbl.raycastTarget = false;
            // §1.14 fit-never-truncate: a long troop name shrinks, never clips, at phone aspect.
            ElarionUiKit.FitSingleLine(nameLbl);

            var ownedLbl = ElarionUiKit.Label(row.transform, "x" + owned, 0.05f, 0.5f, ElarionUi.Affordable,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.23f, 0.98f, bold: true);
            ownedLbl.raycastTarget = false;
        }

        // CENTER/RIGHT — battle-preview placeholder + estimated clear time + summary.
        // Fractions are OF THE BODY ZONE (WO-714 W4).
        private void BuildCenterColumn(Transform body)
        {
            // Battle preview placeholder (the RaidBaseGenerator thumbnail goes here later).
            // (#29) A dark recessed Well, not a Niche — with BlinkChrome off the Niche painted an
            // opaque warm-stone (olive) slab; a dark inset reads as an empty preview panel.
            var preview = ElarionUiKit.Well(body, new Vector2(0.52f, 0.36f), new Vector2(1.00f, 0.845f));
            preview.GetComponent<Image>().raycastTarget = false;
            var pvLbl = ElarionUiKit.Label(preview.transform, "Battle Preview\n(enemy base)", 0.40f, 0.60f,
                ElarionUi.ParchmentDim, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
            pvLbl.raycastTarget = false;

            // Estimated Clear Time readout (FIRST PASS: static from the config; TODO live).
            string est = _vm != null ? FormatTime(_vm.EstClearTime) : "--:--";
            var estLbl = ElarionUiKit.Label(body, "Est. Clear Time: ~" + est, 0.27f, 0.33f, ElarionUi.Gilt,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.52f, 1.00f, bold: true);
            estLbl.raycastTarget = false;

            // Summary — total deployable troops + a simple power rating (VM-computed).
            int totalTroops = _vm != null ? _vm.DeployableCount : 0;
            int power = _vm != null ? _vm.PowerRating : 0;
            var sumLbl = ElarionUiKit.Label(body, $"Troops: {totalTroops}    Power: {power}", 0.19f, 0.25f,
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.52f, 1.00f, bold: true);
            sumLbl.raycastTarget = false;
        }

        // FOOTER action strip — Auto Recommend (stub) + the big glowing DEPLOY CTA.
        // The footer zone is re-seated ABOVE the shared Close band at the factory
        // (WO-714 P6), so the old hand-computed Close-dodge row math is retired:
        //   Auto Recommend (left) | DEPLOY (right); Close sits in its own band below.
        private void BuildDeployBar(Transform footer)
        {
            // Auto Recommend — FIRST PASS stub: selects all deployable (no comp logic yet).
            ElarionUiKit.Button(footer, "Auto Recommend", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.00f, 0.05f), new Vector2(0.32f, 0.95f), OnAutoRecommend);

            // DEPLOY — the big glowing primary CTA. Confirm-green with a gilt ember glow
            // ring behind it so it reads as the dominant action.
            var glow = ElarionUiKit.AddImage(footer, "DeployGlow",
                new Vector2(0.60f, 0.00f), new Vector2(1.00f, 1.00f),
                new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.35f));
            glow.GetComponent<Image>().raycastTarget = false;

            var deployBtn = ElarionUiKit.Button(footer, "DEPLOY", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.615f, 0.05f), new Vector2(0.985f, 0.95f), OnDeploy);
            // Gold-ink-on-green reads as the ember CTA; keep it enabled (the raid can be
            // entered to scout even with no troops — the in-raid tray handles placement).
            if (deployBtn != null) deployBtn.interactable = _vm != null && _vm.CanDeploy;
        }

        private void OnAutoRecommend()
        {
            // FIRST PASS stub: "select all deployable" is the whole roster already shown.
            // A real comp picker (driven by a scout report) is a later increment (see TODO).
            int n = _vm != null ? _vm.DeployableCount : 0;
            // WO-714 P5: transient feedback through the ONE kit toast — a button tap must
            // never be a silent no-op (dead-button law), and no status label to go stale.
            ElarionUiKit.ShowToast("Auto Recommend: all " + n + " deployable troop(s) selected.",
                ElarionUiKit.ToastTone.Info);
            Debug.Log($"[RaidDeployScreen] Auto Recommend (stub) — would deploy all {n} deployable troop(s).");
        }

        private void OnDeploy()
        {
            if (_vm == null || !_vm.CanDeploy)
            {
                // WO-714 P5: player-visible transient feedback, not just a console line.
                ElarionUiKit.ShowToast("This raid has no battleground yet.", ElarionUiKit.ToastTone.Danger);
                Debug.LogWarning("[RaidDeployScreen] DEPLOY: no scene to load for this raid.");
                return;
            }
            Debug.Log($"[RaidDeployScreen] DEPLOY -> SceneRouter.GoRaid('{_vm.SceneName}').");
            // SHARED CONTRACT: the VM loads the raid scene; the in-raid deploy tray handles
            // the actual unit placement.
            _vm.Deploy();
        }

        // ── Data helpers ───────────────────────────────────────────────────────

        // (Army roster / party / deployable-count / power-rating all moved to RaidDeployVM —
        //  the View no longer reads GameState / TroopCatalog.)

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

        // ── Scroll list — the kit scroll zone owns all plumbing (WO-714 W4) ────

        private void FinalizeScroll()
        {
            if (_scroll == null || _scroll.content == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.content);
        }

        private void ClearTroopList()
        {
            _scroll = null;
            if (_troopListArea == null) return;
            for (int i = _troopListArea.childCount - 1; i >= 0; i--)
            {
                var c = _troopListArea.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        public void Close()
        {
            _vm?.Dispose();
            _vm = null;
            // WO-714 P8: eased fade/scale-out through the ONE kit FX (falls back to an
            // immediate Destroy when the FX is absent / not playing).
            if (_ui != null) ElarionUiKit.ClosePanelWithFx(_ui);
            _ui = null;
            _troopListArea = null;
            _scroll = null;
        }

        private void OnDestroy()
        {
            _vm?.Dispose();
            _vm = null;
            if (_instance == this) _instance = null;
            if (_ui != null) Destroy(_ui);
        }
    }
}
