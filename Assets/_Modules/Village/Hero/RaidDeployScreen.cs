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
//   Right    three DISJOINT bands (WO-1385): the ENEMY BASE line (crest + stats +
//            copy), the ECHO GUIDE block (header / name + CHANGE / two-line quote),
//            then the SCOUT REPORT well. Band constants above BuildCenterColumn.
//   Bottom   total troops / a simple power rating (sum of deployable), an
//            a quiet "Army Ready?" peek (the old "Auto Recommend" stub was REMOVED in
//            the 2026-08-09 honesty pass — it was toast-only with no loadout AI; the
//            handler is still named OnAutoRecommend, which is the stale part),
//            and a big glowing DEPLOY button -> SceneRouter.GoRaid(def.sceneName).
//
// Open(SceneConfigDef) / Close() API; RaidSelectionScreen taps a card -> Open(def).
//
// TODO (out of first-pass scope, noted for the next increment):
//  - Live "Estimated Clear Time" that recomputes as troops are added/removed (this
//    pass shows the config's recommended/2-star band statically).
//  - WO-839 #3 built the SCOUT REPORT intel band (honest config facts: walls /
//    gates / garrison / boss, via vm.ScoutReport). Still TODO: the deeper analysis
//    (AA-tower density / choke vs open) driving the soft RPS + a meaningful
//    Auto-Recommend comp.
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

        // UIF-01: single-modal arbiter handle (opening this closes the raid grid; back/ESC dismisses it).
        private PanelHandle _panelHandle;
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
            if (def == null)
            {
                // WO-1110 §2 — a lone Debug.LogWarning gave the PLAYER nothing: the deploy
                // screen simply never opened. Trace it AND say so on screen, so a missing def
                // never reads as an unresponsive UI.
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Raid",
                    "RaidDeployScreen.Open(null) - no SceneConfigDef, the deploy screen cannot open.");
                ElarionUiKit.ShowToast("That raid could not be opened.",
                    ElarionUiKit.ToastTone.Danger);
                return;
            }
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
            // WO-839 #1: FrameCore now carves a thin SUB-HEADER band (right of the medallion
            // socket, under the title) -- the badge / stars / target-time meta row seats there
            // so the body top is no longer a second stacked header row. Null on the
            // procedural fallback path (frame art absent); builders then keep the legacy
            // body-top strip.
            var subHeader = chrome.layout != null && chrome.layout.subHeader != null
                ? (Transform)chrome.layout.subHeader : null;

            // WO-714 P8: the ONE shared open ease (scale target = the panel rect).
            ElarionUiKit.AttachPanelOpenFx(_ui,
                chrome.root != null ? chrome.root.transform as RectTransform : null);

            BuildHeader(subHeader, body);

            // LEFT column — Your Forces (party row + troop list + cap indicator).
            BuildLeftColumn(body, hasSubHeader: subHeader != null);

            // RIGHT column — battle preview + estimated clear time + summary + scout report.
            BuildCenterColumn(body, hasSubHeader: subHeader != null);

            // FOOTER action strip — Auto Recommend + the big glowing DEPLOY CTA.
            BuildDeployBar(footer);

            // UIF-01: join the single-modal arbiter (closes the raid grid it opened over; back/ESC
            // routes here). A battle-lock rejection tears this down (handle.Close) and returns.
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("Raid Deploy", Close, () => _ui != null);
            if (!PanelManager.NotifyOpened(_panelHandle))
                return;

            Debug.Log($"[RaidDeployScreen] Opened for raid '{_vm.RaidId}' -> scene '{_vm.SceneName}'.");
        }

        // Meta row (difficulty badge + star row + target time). WO-839 #1: on the frame
        // path this seats in the chrome's SUB-HEADER band -- beside/below the title, right
        // of the medallion socket -- so it never stacks a second header row into the body
        // top and the pill can never collide with the medallion. Fallback (procedural
        // chrome, no sub-header zone): the legacy body-top strip.
        private void BuildHeader(Transform subHeader, Transform body)
        {
            Transform host = subHeader != null ? subHeader : body;
            // Fractions OF THE HOST: the sub-header band uses (near) full-height rows; the
            // body fallback keeps the legacy thin top strip.
            float y0 = subHeader != null ? 0.06f : 0.945f;
            float y1 = subHeader != null ? 0.94f : 1.00f;

            Color tint = DifficultyColor(_vm != null ? _vm.Difficulty : null);
            var badge = ElarionUiKit.AddImage(host, "DiffBadge",
                new Vector2(0.00f, y0), new Vector2(0.16f, y1),
                new Color(tint.r, tint.g, tint.b, 0.85f));
            badge.GetComponent<Image>().raycastTarget = false;
            // Colorblind text-first: the tint is decoration, the WORD carries the meaning.
            var badgeLbl = ElarionUiKit.Label(badge.transform, DifficultyLabel(_vm != null ? _vm.Difficulty : null),
                0f, 1f, ElarionUi.Ink, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            badgeLbl.raycastTarget = false;

            // Tofu fix (2026-07-02): ★ (U+2605) is in NO project SDF font (scanned —
            // zero m_Unicode:9733 hits), so the old "★★★" text rendered as boxes in
            // builds. Procedural gold diamonds instead (EndStateView's pattern via
            // the shared StarRatingRow), then a plain font-safe "Target:" label.
            StarRatingRow.Build(host, 3, 3, 0.19f, y0, 0.28f, y1, sizePx: 12f);
            // Honesty: this is the LIVE raid clock (3★ under-time), not a longer decorative target.
            var timeLbl = ElarionUiKit.Label(host, "Clock: " + FormatTime(_vm != null ? _vm.TargetTime : 0f),
                y0, y1, ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.305f, 0.75f, bold: true);
            ElarionUiKit.FitSingleLine(timeLbl);
            timeLbl.raycastTarget = false;
        }

        // WO-839 #3: the columns widen to 0.49 / 0.51 (the old 0.48 / 0.52 left a dead
        // 4%-wide center seam the owner flagged).
        private const float LeftColX1  = 0.49f;
        private const float RightColX0 = 0.51f;

        // LEFT — Your Forces: hero + companions portrait row, then the scrollable
        // troop-card list grouped by TroopDefId, then the army-cap indicator.
        // WO-839 #1: with the meta row gone to the chrome sub-header, the left column
        // starts at the top of the body (spec: "YOUR FORCES" from body yMax ~= 0.90).
        // The fallback (no sub-header) keeps the legacy offsets below the body-top row.
        private void BuildLeftColumn(Transform body, bool hasSubHeader)
        {
            // WO-1385 #2: the party row deepens 0.735 -> 0.690 (62 -> 80 px on the 411 px
            // phone body) so a hero portrait PLATE and a 36 px name row can stack without
            // the label painting over the plate; the cap line and the list top slide down
            // by the same 0.035. YOUR FORCES and the cap line's content are untouched.
            float forcesY0 = hasSubHeader ? 0.900f : 0.855f;
            float forcesY1 = hasSubHeader ? 0.960f : 0.915f;
            float partyY0  = hasSubHeader ? 0.690f : 0.655f;
            float partyY1  = hasSubHeader ? 0.885f : 0.845f;
            float capY0    = hasSubHeader ? 0.630f : 0.600f;
            float capY1    = hasSubHeader ? 0.680f : 0.650f;
            float listY1   = hasSubHeader ? 0.610f : 0.580f;

            var lbl = ElarionUiKit.Label(body, "YOUR FORCES", forcesY0, forcesY1, ElarionUi.Gilt,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.00f, LeftColX1, bold: true);
            ElarionUiKit.FitSingleLine(lbl);
            lbl.raycastTarget = false;

            // Hero + Companions portrait row.
            BuildPartyRow(body, partyY0, partyY1);

            // Army-cap indicator (VM-computed SlotsUsed / MaxArmySize).
            var capLbl = ElarionUiKit.Label(body, _vm != null ? _vm.ArmyCapText : "Army: -", capY0, capY1,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.00f, LeftColX1, bold: true);
            capLbl.raycastTarget = false;

            // Troop list region (left half of the body zone, below the cap line). The body
            // zone's bottom already sits ABOVE the footer + Close band (factory reservation,
            // WO-714 P6) — the old hand-computed 0.19 Close-dodge is retired.
            var listGo = new GameObject("TroopListArea", typeof(RectTransform));
            listGo.transform.SetParent(body, false);
            _troopListArea = listGo.GetComponent<RectTransform>();
            _troopListArea.anchorMin = new Vector2(0.00f, 0.00f);
            _troopListArea.anchorMax = new Vector2(LeftColX1, listY1);
            _troopListArea.offsetMin = Vector2.zero;
            _troopListArea.offsetMax = Vector2.zero;

            BuildTroopList();
        }

        // Hero + companions as small framed class portraits across the top of the
        // left column. The hero's class = GameState.HeroClass; companions = the class
        // strings in PartyMemberIds. (Hero is added explicitly so the row always shows
        // the player even before any companion has joined.)
        // ⚠ THE OLD NOTE HERE WAS RETIRED BY THE RULING IT WAS WAITING ON (2026-08-21).
        // It read: "WO-774.0 FORWARD NOTE (spectator-model ruling PENDING …): when the
        // owner/Grok ruling lands, the hero LEAVES RAID SCENES ENTIRELY and this
        // hero+companion party row is expected to be REPLACED by a troop-loadout row."
        // The ruling landed the OTHER WAY. WO-1109 (2026-08-16, commit 256fa9ee3) shipped
        // Option A — CARRY: SceneRouter.GoRaid now detaches the REAL hero, DontDestroyOnLoad's
        // it across the load and re-homes it at the raid's baked HeroStartPoint_PlayerSpawn.
        // (Before that, every raid silently ran the EMERGENCY pill-hero, which had no
        // HeroAbilities at all — Q/W/E/R were dead in every raid ever played.) So the hero
        // is IN the raid, with its real class body and abilities, and this party row is
        // CORRECT rather than provisional. A seat acting on the retired note would have
        // deleted a row that now reflects who actually fights.
        // Still true, and still the reason this stays thin: do NOT deepen hero-in-raid
        // coupling on this SCREEN — the carry is SceneRouter's, not the deploy UI's.
        private void BuildPartyRow(Transform body, float rowY0, float rowY1)
        {
            var classes = _vm != null ? _vm.PartyClasses : new List<string>();

            // Row host — left half of the body zone, below "YOUR FORCES".
            var rowHost = new GameObject("PartyRow", typeof(RectTransform));
            rowHost.transform.SetParent(body, false);
            var rr = rowHost.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.00f, rowY0);
            rr.anchorMax = new Vector2(LeftColX1, rowY1);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;

            int n = classes.Count;
            if (n == 0) return;
            float slot = Mathf.Min(0.30f, 0.92f / Mathf.Max(n, 1));
            float rowWidth = slot * n;
            float rowStart = (1f - rowWidth) * 0.5f;
            // WO-1385 #2 (owner screenshot: "Thrain" / "Grom" painted OVER the olive plates).
            // The old niche was the whole slot width (225 px) by 0.18-0.98 of a 62 px row,
            // and the name sat in the 11 px left under it -- a FontMicro glyph cannot seat in
            // 11 px, so it overflowed upward onto the plate. Now the plate is a SQUARE-ish
            // alcove centred in the slot (0.028 half-width of the 749 px row = 42 px, y 0.47-
            // 1.00 = 42 px on the 80 px row) and the name owns the bottom 0.00-0.45 (36 px),
            // fitted single-line. Plate is decorative (no Button, raycast off), so the touch
            // floor does not apply to it.
            const float plateHalfW = 0.028f;
            for (int i = 0; i < n; i++)
            {
                string cls = classes[i];
                float x0 = rowStart + i * slot + 0.015f;
                float x1 = rowStart + (i + 1) * slot - 0.015f;
                float cx = (x0 + x1) * 0.5f;

                // Framed portrait niche -- the PLATE, above the label.
                var niche = ElarionUiKit.Niche(rowHost.transform,
                    new Vector2(cx - plateHalfW, 0.47f), new Vector2(cx + plateHalfW, 1.00f));
                niche.GetComponent<Image>().raycastTarget = false;
                var portrait = ElarionUiKit.Portrait(niche.transform, ElarionUiKit.PortraitForClass(cls), active: i == 0);

                // Name BELOW the portrait plate (the canon companion name for the class).
                var nameLbl = ElarionUiKit.Label(rowHost.transform, _vm != null ? _vm.CompanionName(cls) : cls,
                    0.00f, 0.45f, ElarionUi.Parchment,
                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, x0, x1, bold: true);
                ElarionUiKit.FitSingleLine(nameLbl);
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

            // Canonical troop portrait; keep the compact role glyph only as the honest
            // missing-asset fallback (WO-1294).
            var well = ElarionUiKit.AddImage(row.transform, "IconWell",
                new Vector2(0.03f, 0.15f), new Vector2(0.20f, 0.85f), new Color(0f, 0f, 0f, 0.30f));
            var wellImage = well.GetComponent<Image>();
            wellImage.raycastTarget = false;
            var portrait = RpgUiCatalog.Get(RpgUiCatalog.RoleTroop, item.IconName);
            if (portrait != null)
            {
                wellImage.sprite = portrait;
                wellImage.preserveAspect = true;
                wellImage.color = Color.white;
            }
            else
            {
                string glyph = _vm != null ? _vm.RoleGlyph(troopDefId) : "MEL";
                var ic = ElarionUiKit.Label(well.transform, glyph, 0f, 1f, ElarionUi.Gilt,
                    ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
                ic.raycastTarget = false;
            }

            var nameLbl = ElarionUiKit.Label(row.transform, name, 0.45f, 0.95f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, 0.23f, 0.98f, bold: true);
            nameLbl.raycastTarget = false;
            // §1.14 fit-never-truncate: a long troop name shrinks, never clips, at phone aspect.
            ElarionUiKit.FitSingleLine(nameLbl);

            var ownedLbl = ElarionUiKit.Label(row.transform, "x" + owned, 0.05f, 0.5f, ElarionUi.Affordable,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.23f, 0.98f, bold: true);
            ownedLbl.raycastTarget = false;
        }

        // RIGHT — battle preview + estimated clear time + summary + scout report.
        // Fractions are OF THE BODY ZONE (WO-714 W4). WO-839 #3: the column widens to
        // RightColX0 (seam closed) and the bare lower band becomes the SCOUT REPORT
        // intel area (the header TODO's intended occupant).
        // =====================================================================
        //  WO-1385 (owner 2026-09-04, Seeker build 355905: "screenshot. yuck") --
        //  THE RIGHT COLUMN IS THREE DISJOINT BANDS, AND THE BUDGET IS 411 PX.
        // ---------------------------------------------------------------------
        //  The body zone is NOT the 0.075-0.835 that ZonesFor declares. On the phone
        //  canvas (1080x1920 ref, match 0.5, 2670x1200 -> scale 1.243, canvasH 965)
        //  the Close-band reservation in BuildObsidianPanel lifts the footer to
        //  0.217-0.347 and the body floor to 0.362, so the body is 0.473 x 869 =
        //  ~411 ref px tall. The old bands were authored against a taller body:
        //  the guide band (0.455-0.600) was 60 px, its CHANGE button carried a
        //  MinTouchPx=112 floor, and ClampMinTouch grew it symmetrically over the
        //  ENEMY BASE copy above and the SCOUT REPORT header below -- the three
        //  elements the owner saw sharing one band. The fix is a real pixel
        //  budget, top to bottom, with every single-line row >= 36 px so the
        //  30 px FontFloor seats WITHOUT the runtime relax guard (which does not
        //  run in a headless capture):
        //     ENEMY BASE  0.785-0.960  (72 px: 2 rows of 36)
        //     gap         0.775-0.785  ( 4 px)
        //     ECHO GUIDE  0.390-0.775  (158 px: hdr 36 / name 40 / quote 76 -- 2 lines)
        //     gap         0.380-0.390  ( 4 px)
        //     SCOUT       0.000-0.380  (156 px: hdr 37 / 3 lines in 109)
        //  Pinned by RaidDeployUiRegression [deploy-bands-disjoint]. These are body
        //  fractions; the left column is untouched except the party row (see
        //  BuildPartyRow).
        private const float ScoutBandY0 = 0.000f;
        private const float ScoutBandY1 = 0.380f;
        private const float GuideBandY0 = 0.390f;
        private const float GuideBandY1 = 0.775f;
        private const float EnemyBandY0 = 0.785f;
        private const float EnemyBandY1 = 0.960f;

        private void BuildCenterColumn(Transform body, bool hasSubHeader)
        {
            // ENEMY BASE line. WO-839 #4: an INTENTIONAL framed pre-battle state instead of
            // the raw "(enemy base)" stub copy. No runtime thumbnail exists yet
            // (RaidBaseGenerator is a BAKE-TIME editor tool -- nothing renders the target
            // base at runtime), so the band carries a small crest + honest copy until a
            // thumbnail pipeline lands.
            // WO-1385: this used to be a tall preview well with a large crest above a
            // three-line rich-text block; on the 411 px body the third line (Est / Troops /
            // Power) was truncated clean off and the second ran under the guide band. It is
            // now TWO single-line rows: crest + "ENEMY BASE" + the stats readout, then the
            // copy line. Each row is FitSingleLine (shrinks to the floor, then ellipsis --
            // never a silent cut).
            // (#29) A dark recessed Well, not a Niche -- with BlinkChrome off the Niche painted
            // an opaque warm-stone (olive) slab; a dark inset reads as an empty preview panel.
            float enemyTop = hasSubHeader ? EnemyBandY1 : 0.845f;
            var preview = ElarionUiKit.Well(body, new Vector2(RightColX0, EnemyBandY0), new Vector2(1.00f, enemyTop));
            preview.GetComponent<Image>().raycastTarget = false;

            var crest = ElarionUiKit.AddImage(preview.transform, "PreviewCrest",
                new Vector2(0.03f, 0.54f), new Vector2(0.085f, 0.98f),
                new Color(1f, 1f, 1f, 0.55f));
            var crestImg = crest.GetComponent<Image>();
            var crestSprite = UiStyle.Icon("combat", "crest", "shield", "emblem");
            if (crestSprite != null)
            {
                crestImg.sprite = crestSprite;
                crestImg.preserveAspect = true;
            }
            else
            {
                // No icon art in this build: a quiet gilt plate keeps the framed read.
                crestImg.color = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.18f);
            }
            crestImg.raycastTarget = false;

            var pvHdr = ElarionUiKit.Label(preview.transform, "ENEMY BASE", 0.50f, 1.00f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.10f, 0.42f, bold: true);
            ElarionUiKit.FitSingleLine(pvHdr);
            pvHdr.raycastTarget = false;

            // The WORD carries the meaning (no colour-only rich text any more).
            string est = _vm != null ? FormatTime(_vm.EstClearTime) : "--:--";
            int totalTroops = _vm != null ? _vm.DeployableCount : 0;
            int power = _vm != null ? _vm.PowerRating : 0;
            var pvStats = ElarionUiKit.Label(preview.transform,
                "Est. ~" + est + " | Troops " + totalTroops + " | Power " + power, 0.50f, 1.00f,
                ElarionUi.Parchment, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Right, 0.44f, 0.97f);
            ElarionUiKit.FitSingleLine(pvStats);
            pvStats.raycastTarget = false;

            var pvCopy = ElarionUiKit.Label(preview.transform, "Assault to recon - deploy troops on the field",
                0.00f, 0.50f, ElarionUi.Parchment, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.03f, 0.97f);
            ElarionUiKit.FitSingleLine(pvCopy);
            pvCopy.raycastTarget = false;

            // WO-1380: the ECHO GUIDE band, between the enemy line and the scout report.
            BuildGuideBand(body);

            // WO-839 #3: SCOUT REPORT intel band fills the previously bare lower band.
            // Strict MVVM: the View renders vm.ScoutReport lines verbatim — honest config
            // facts only (walls / gates / garrison / boss; never the cosmetic reward
            // fields the loot math ignores).
            // WO-1385: content untouched; the well's top drops 0.44 -> ScoutBandY1 so the
            // guide block above gets a band the CHANGE button actually fits in.
            var intel = ElarionUiKit.Well(body, new Vector2(RightColX0, ScoutBandY0), new Vector2(1.00f, ScoutBandY1));
            intel.GetComponent<Image>().raycastTarget = false;
            var intelHdr = ElarionUiKit.Label(intel.transform, "SCOUT REPORT", 0.75f, 0.99f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            ElarionUiKit.FitSingleLine(intelHdr);
            intelHdr.raycastTarget = false;
            var report = _vm != null ? _vm.ScoutReport : null;
            string intelText = report != null && report.Count > 0
                ? string.Join("\n", report) : "No scout intel available.";
            var intelLbl = ElarionUiKit.Label(intel.transform, intelText, 0.03f, 0.73f,
                ElarionUi.Parchment, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.TopLeft, 0.08f, 0.92f);
            ElarionUiKit.FitBlock(intelLbl);
            intelLbl.raycastTarget = false;
        }

        // =====================================================================
        //  WO-1380 — ECHO GUIDE. THE ECHO DOES NOT FIGHT. IT REMEMBERS.
        // =====================================================================
        // Before each expedition the player picks an Echo Guide (creative canon
        // 2026-09-04 §7). The Heart cannot see clearly beyond its protection; an Echo
        // can recognise fragments of the world that existed before the Realm fell, so
        // the band shows WHO is guiding and WHAT they already remember about this
        // target — the payoff line, right where the player is choosing.
        //
        // ⛔ SCOPE FENCE, owner-ruled and pinned by EchoGuideMemoryRegression: a Guide
        // grants NO stat, NO yield and NO combat effect in V1. Nothing on this band
        // touches power, readiness, loot or the deploy gate — swapping the Guide
        // changes the SENTENCE and nothing else. Adding an effect later is a
        // deliberate design decision, never a quiet one.
        //
        // ⛔ NO SECOND SPAWNER. This is a UI band. The Echo's world body still belongs
        // solely to EchoWorldPresence (WO-1108 Lane B), which escorts the player to the
        // gate and returns ONCE after the battle — and that return is where the Guide
        // actually speaks this line aloud (EchoWorldPresence.SpeakGuideMemory).
        //
        // WO-1385 layout (fractions OF THE BAND, band = GuideBandY0..GuideBandY1 of the
        // body = ~158 px on the phone body): a TEXT COLUMN on the left (x 0.04-0.63) of
        // three rows -- header 0.76-0.99 (36 px), name 0.51-0.76 (40 px), quote 0.02-0.50
        // (76 px = two 30 px lines) -- and the CHANGE button on the RIGHT (x 0.66-0.97,
        // y 0.06-0.94 = 232 x 139 px), beside the name line and clear of every row. The
        // button is taller than the name line ON PURPOSE: MinTouchPx is 112 and the band
        // cannot give a single row that height, so the button owns the column's height
        // instead of ClampMinTouch growing it over the neighbours (the shipped collision).
        // (GuideBandY0/Y1 live with the other band constants above BuildCenterColumn.)

        // Live band labels, so cycling the Guide re-renders two strings instead of
        // rebuilding the screen (a rebuild would drop the player's scroll position).
        private TMPro.TMP_Text _guideNameLabel;
        private TMPro.TMP_Text _guideMemoryLabel;

        private const float GuideTextX0 = 0.04f;
        private const float GuideTextX1 = 0.63f;

        private void BuildGuideBand(Transform body)
        {
            var band = ElarionUiKit.Well(body, new Vector2(RightColX0, GuideBandY0), new Vector2(1.00f, GuideBandY1));
            var bandImg = band.GetComponent<Image>();
            if (bandImg != null) bandImg.raycastTarget = false;

            var hdr = ElarionUiKit.Label(band.transform, "ECHO GUIDE", 0.76f, 0.99f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, GuideTextX0, GuideTextX1, bold: true);
            ElarionUiKit.FitSingleLine(hdr);
            hdr.raycastTarget = false;

            // "Change" cycles through the OWNED Echoes. A picker with one entry would be a
            // dead control, so it is only offered when the player actually has a choice —
            // "don't offer what you can't do" (WO-833), and an early player owning only the
            // founding Echo simply sees who is guiding them.
            var guides = DeNelle.Village.World.Camps.EchoGuideService.AvailableGuides();
            if (guides != null && guides.Count > 1)
            {
                // ⚠ THE ANCHORS MUST SPAN A REAL BAND (y0 != y1). ElarionUiKit.Button writes
                // offsetMin = offsetMax = Vector2.zero on BOTH the procedural path
                // (ElarionUiKit.cs:1599-1600) and the Obsidian sprite path
                // (ElarionUiKitObsidian.cs:154-155), so identical anchor y = a ZERO-PIXEL rect
                // and an untappable control. The y0==y1 idiom used by the footer CTAs in this
                // same file is legal ONLY because SeatFooterCtaAtCanonicalHeight (:654-662)
                // supplies the height afterwards; this button has no such seat, so it carries
                // its own band. UiKitMinTouchGuard is a net, not a layout — it does not run in
                // an edit-mode headless capture (ElarionUiKit.cs:1075-1077).
                // WO-1385: the band spans y 0.06-0.94 of a ~158 px band = 139 px, above
                // the 112 px floor by construction, so the clamp never has to grow it.
                ElarionUiKit.Button(band.transform, "Change", ElarionUiKit.ButtonKind.Quiet,
                    new Vector2(0.66f, 0.06f), new Vector2(0.97f, 0.94f), OnCycleGuide);
            }

            _guideNameLabel = ElarionUiKit.Label(band.transform, string.Empty, 0.51f, 0.76f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, GuideTextX0, GuideTextX1, bold: true);
            _guideNameLabel.raycastTarget = false;

            _guideMemoryLabel = ElarionUiKit.Label(band.transform, string.Empty, 0.02f, 0.50f,
                ElarionUi.ParchmentDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.TopLeft, GuideTextX0, GuideTextX1);
            _guideMemoryLabel.raycastTarget = false;

            RefreshGuideBand();
        }

        // Re-renders the two band strings from the service. Never touches any deploy
        // state — see the scope fence above.
        private void RefreshGuideBand()
        {
            var guide = DeNelle.Village.World.Camps.EchoGuideService.SelectedGuide;
            string raidId = _vm != null ? _vm.RaidId : null;

            if (_guideNameLabel != null)
            {
                _guideNameLabel.text = guide != null && !string.IsNullOrEmpty(guide.DisplayName)
                    ? guide.DisplayName : "No Echo awakened yet";
                ElarionUiKit.FitSingleLine(_guideNameLabel);
            }

            if (_guideMemoryLabel == null) return;

            string line = DeNelle.Village.World.Camps.EchoGuideService.MemoryLineFor(raidId);
            if (string.IsNullOrEmpty(line))
            {
                // Honest empty state, never a blank band: the catalog has already WARNED with
                // both keys, and the player is told the Guide has nothing to offer here rather
                // than being shown an empty rectangle.
                _guideMemoryLabel.text = "This Echo remembers nothing of this place.";
                DeNelle.Core.Diagnostics.FlowTrace.Warn("EchoGuide",
                    "deploy band has NO memory line for guide=" + (guide != null ? guide.Id : "(none)") +
                    " target=" + (raidId ?? "(null)") + ". All 24 lines must ship (WO-1380).");
            }
            else
            {
                _guideMemoryLabel.text = "\"" + line + "\"";
                DeNelle.Core.Diagnostics.FlowTrace.Step("EchoGuide",
                    "deploy band shows " + (guide != null ? guide.Id : "(none)") + " at " +
                    (raidId ?? "(null)") + ". Narrative only - no stat, no yield, no combat effect.");
            }
            ElarionUiKit.FitBlock(_guideMemoryLabel);
            // WO-1385 #3: FitBlock's overflow mode is Truncate, which is how the quote shipped
            // cut mid-word ("...sowed this ground, an") with nothing telling the player the
            // sentence went on. Wrap stays, auto-size stays (down to the 30 px floor), and a
            // line that STILL does not fit the two-line rect ends in an ellipsis instead.
            _guideMemoryLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        }

        // Advance to the next OWNED Echo and re-render. The choice is a preference, not a
        // purchase: nothing is spent and nothing is granted.
        private void OnCycleGuide()
        {
            var guides = DeNelle.Village.World.Camps.EchoGuideService.AvailableGuides();
            if (guides == null || guides.Count == 0)
            {
                ElarionUiKit.ShowToast("No Echo has awakened yet.", ElarionUiKit.ToastTone.Info);
                return;
            }

            string currentId = DeNelle.Village.World.Camps.EchoGuideService.SelectedGuideEchoId;
            int at = 0;
            for (int i = 0; i < guides.Count; i++)
                if (guides[i] != null && guides[i].Id == currentId) { at = i; break; }

            var next = guides[(at + 1) % guides.Count];
            if (next == null) return;
            if (!DeNelle.Village.World.Camps.EchoGuideService.SelectGuide(next.Id, "raid deploy band"))
            {
                // Refused (unowned / unknown) — the service has already WARNED with the reason.
                ElarionUiKit.ShowToast("That Echo cannot guide you yet.", ElarionUiKit.ToastTone.Info);
                return;
            }
            RefreshGuideBand();
        }

        // WO-839 #6 flag (OWNER CONFIRM pending): false = DEPLOY stays enabled at 0 troops
        // so the player can enter to SCOUT (deliberate feature — the spec's default);
        // true = grey it out + toast a reason ("don't offer what you can't do", WO-833).
        // static readonly (not const) so the dead branch never trips CS0162.
        private static readonly bool GateDeployAtZeroTroops = false;

        /// <summary>
        /// WO-823 Phase E - the screen's ONE window onto readiness. Returns the SLOT-weighted
        /// deployable count from <see cref="DeNelle.Village.ArmyReadiness"/>, the single
        /// readiness formula, instead of the raw headcount this screen used to gate on.
        ///
        /// This is deliberately NOT a readiness predicate: it exposes a number the snapshot
        /// already computed and decides nothing. Phase E REMOVED an opinion from this file;
        /// it must never grow a new one. Anything that needs "may this player raid" reads
        /// Snapshot.Ready upstream (RaidEntryGate / RaidSelectionScreen), never here.
        ///
        /// No GameState (headless / AutoPilot) -> Compute returns the never-false-block
        /// snapshot with zero slots, and GateDeployAtZeroTroops is OFF by default, so the
        /// deploy path stays open exactly as it does today.
        /// </summary>
        private static int ReadinessSlots()
        {
            var st = DeNelle.Core.State.GameStateService.Instance != null
                ? DeNelle.Core.State.GameStateService.Instance.State : null;
            return DeNelle.Village.ArmyReadiness.Compute(st).DeployableSlots;
        }

        // FOOTER action strip — Auto Recommend (stub) + the big DEPLOY CTA.
        // WO-839 #5: FrameCore's footer is now an explicit RAISED band tall enough for
        // MinTouchPx buttons (root cause: the inherited thin default band forced
        // ClampMinTouch to grow both buttons past the band into the shared Close below).
        // Auto Recommend and DEPLOY share the band with a real gap; Close keeps its own
        // band underneath.
        private void BuildDeployBar(Transform footer)
        {
            // Honesty pass 2026-08-09: removed the "Auto Recommend" stub (toast-only, no
            // loadout AI). Full-width BEGIN ASSAULT — the army is always the full deployable
            // roster on the battleground tray. Quiet "Army ready" peek stays as optional info.
            var readyBtn = ElarionUiKit.Button(footer, "Army Ready?", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.00f, 0.50f), new Vector2(0.28f, 0.50f), OnAutoRecommend);
            SeatFooterCtaAtCanonicalHeight(readyBtn);

            // DEPLOY -- the primary CTA. WO-1385 #4 (owner 2026-09-04, Seeker screenshot:
            // "yuck"): the WO-839 DeployGlow halo -- a flat gilt image slab behind the
            // button, x 0.30-1.00 / y 0.00-1.00 of the footer -- is GONE. On the phone the
            // footer is ~113 px and the seated button 132 px, so the "halo" read as a raw
            // yellow rectangle sticking out behind a framed button, next to ARMY READY?'s
            // kit frame: two visual languages on one row. The primary emphasis now comes
            // from the kit's own primary face -- BuildObsidianButton Yellow -- on the SAME
            // row geometry as ARMY READY? (y 0.50/0.50 + SeatFooterCtaAtCanonicalHeight).
            // No second image, no second style. Pinned by RaidDeployUiRegression
            // [deploy-bar-kit-button].
            // WO-932: "BEGIN ASSAULT" -- distinct from in-raid ground DROP of troops.
            var deployBtn = ElarionUiKit.BuildObsidianButton(footer, "BEGIN ASSAULT",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.32f, 0.50f), new Vector2(0.985f, 0.50f), OnDeploy);
            SeatFooterCtaAtCanonicalHeight(deployBtn);
            // WO-839 #6: scouting stays the default (GateDeployAtZeroTroops=false). Either
            // way the WO-820 readiness gate upstream (RaidEntryGate / ArmyReadiness.Compute
            // at the HUD button + selection grid) stays the ONE authority - this screen
            // never re-derives or bypasses readiness.
            //
            // WO-823 Phase E: this line USED TO READ _vm.DeployableCount, a raw HEADCOUNT,
            // while ArmyReadiness is SLOT-WEIGHTED. That was the grey-button-versus-open-gate
            // bug in its original form - the button and the door disagreed about what "enough
            // army" means, and neither was lying. It now reads the ONE snapshot, so the two
            // agree by construction rather than by coincidence.
            bool troopsOk = !GateDeployAtZeroTroops || ReadinessSlots() > 0;
            if (deployBtn != null) deployBtn.interactable = _vm != null && _vm.CanDeploy && troopsOk;
        }

        // WO-1075: footer height changes with aspect, so a vertical fraction can fall below
        // the mobile touch floor. Pin both actions to the canonical pixel height instead.
        private static void SeatFooterCtaAtCanonicalHeight(Button button)
        {
            if (button == null) return;
            var rt = button.transform as RectTransform;
            if (rt == null) return;
            float half = ElarionUiKit.CanonCtaHeight * 0.5f;
            rt.offsetMin = new Vector2(rt.offsetMin.x, -half);
            rt.offsetMax = new Vector2(rt.offsetMax.x, half);
        }

        private void OnAutoRecommend()
        {
            // WO-932: no longer a silent stub. Pre-deploy does not pick a subset — the full
            // deployable army is available on the battleground tray. This CTA confirms that
            // and surfaces power/count so the tap is never a dead button.
            int n = _vm != null ? _vm.DeployableCount : 0;
            int power = _vm != null ? _vm.PowerRating : 0;
            if (n <= 0)
            {
                ElarionUiKit.ShowToast(
                    "No deployable troops yet. Train at the Barracks, then return.",
                    ElarionUiKit.ToastTone.Info);
                return;
            }
            ElarionUiKit.ShowToast(
                "Full army ready: " + n + " troop(s), power " + power +
                ". Begin Assault — drop them on the field.",
                ElarionUiKit.ToastTone.Info);
            Debug.Log($"[RaidDeployScreen] Auto Recommend — full army n={n} power={power}.");
        }

        private void OnDeploy()
        {
            if (_vm == null)
            {
                ElarionUiKit.ShowToast("Raid briefing is not ready.", ElarionUiKit.ToastTone.Danger);
                return;
            }
            if (string.IsNullOrEmpty(_vm.SceneName))
            {
                ElarionUiKit.ShowToast("This raid has no battleground yet.", ElarionUiKit.ToastTone.Danger);
                Debug.LogWarning("[RaidDeployScreen] DEPLOY: empty sceneName.");
                return;
            }
            if (!DeNelle.Core.SceneRouter.IsSceneInBuild(_vm.SceneName))
            {
                // WO-932 Phase 2: honest under-construction — never silent strand.
                ElarionUiKit.ShowToast(
                    "Raid under construction — battleground not in this build.",
                    ElarionUiKit.ToastTone.Danger);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Raid",
                    $"BEGIN ASSAULT refused: scene '{_vm.SceneName}' not in Build Settings.");
                return;
            }
            // WO-839 #6 (flag OFF by default - scouting with 0 troops is deliberate).
            // WO-823 Phase E: the second copy of the same bypass, routed through the ONE
            // ArmyReadiness snapshot for the same reason as the button state above.
            if (GateDeployAtZeroTroops && ReadinessSlots() <= 0)
            {
                ElarionUiKit.ShowToast("No troops trained yet. Visit the Barracks.", ElarionUiKit.ToastTone.Danger);
                Debug.Log("[RaidDeployScreen] DEPLOY blocked: 0 deployable troops (GateDeployAtZeroTroops).");
                return;
            }
            // WO-1380: remember WHERE the Guide is being taken, so the Echo has something to
            // remember when EchoWorldPresence brings it back after the battle. Records an id
            // only — it grants nothing and gates nothing, and a target with no authored lines
            // just leaves the Echo silent (warned by the catalog, never a wrong line).
            DeNelle.Village.World.Camps.EchoGuideService.NoteExpeditionTarget(_vm.RaidId, "BEGIN ASSAULT");

            string name = !string.IsNullOrEmpty(_vm.DisplayNameRaw) ? _vm.DisplayNameRaw : _vm.RaidId;
            ElarionUiKit.ShowToast("Assaulting " + name + "…", ElarionUiKit.ToastTone.Info);
            Debug.Log($"[RaidDeployScreen] BEGIN ASSAULT -> SceneRouter.GoRaid('{_vm.SceneName}').");
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
            // UIF-01: release the arbiter slot (no-op if already swapped out).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
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
            // UIF-01: don't leak the arbiter slot if destroyed while open (scene unload).
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            _vm?.Dispose();
            _vm = null;
            if (_instance == this) _instance = null;
            if (_ui != null) Destroy(_ui);
        }
    }
}
