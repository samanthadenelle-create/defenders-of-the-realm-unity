// =============================================================================
// HeartPanel — the HEART OF ELARION surface (WO-2017), and the DIRECT ROUTE to
// raising HEART LEVEL that the game did not have (WO-2003).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// ⛔ WHY THIS FILE EXISTS. Owner, 2026-09-06: "wire the heart." She could not find
// how to raise her realm tier while playing, and it gates nearly everything.
// MEASURED at source the same day: the one runtime writer is
// VillageTierService.TryUpgrade (VillageTierService.cs:73); its ONE caller is
// BuildingUpgradeVM.Select(VillageTierRowId) (BuildingUpgradeVM.cs:1027);
// that is reached ONLY from the action band at BuildingUpgradePanelMvvm.cs:1322-1338,
// which is painted ONLY in the VillageGated state — i.e. only while the player
// already happens to be looking at a building whose NEXT tier is gated. There was
// no direct route. THIS panel is the direct route.
//
// DUMB VIEW (canon §9 / ruling 1). Every number, sentence, CTA face, cost basket
// and unlock line on this screen is composed by HeartProgression. This file
// calculates NOTHING: no affordability, no cost, no lock decision, no unlock list.
// It binds strings and invokes one command.
//
// ⚠ BAND HEIGHTS ARE STATED IN PX, on purpose. TMP CULLS A WHOLE LINE whose
// fontSizeMin cannot seat — a band under ~24px renders BLANK, not small, which
// cost three defects on 2026-09-06. The body well measures 533-612 reference px
// on the captured aspects (Builds/manage-redesign-capture.log: well=612 / 542 /
// 533), so every fraction below is annotated with its px range at BOTH ends of
// that measured span. Nothing here is under 48px, and the CTA clears MinTouchPx
// (112) at the SHORT end — a control that needs ClampMinTouch to rescue it has
// already spilled into its neighbour.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using DeNelle.Village.Buildings.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// The Heart of Elarion management surface. Registered on <see cref="PanelId.Heart"/>, so
    /// every door — the Manage header face, a village-gated building card, a village-gated
    /// research row — opens the SAME model-backed screen (WO-2017 "All entry points must bind
    /// to the same Heart model").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeartPanel : MonoBehaviour
    {
        // ── Chrome/body geometry, shared with ManageScreenPanel's proven arithmetic ──────────
        private const float CloseBandY0 = 0.050f;   // ElarionUiKit's DefaultCloseZone.y (the Close band)
        private const float CloseGapY = 0.020f;     // body floor clears the Close box by this much

        // ── BODY BANDS. Fractions of the body well; px stated at well=533 (short) and 612 (tall).
        //    ⚠ Every one of these clears the ~24px TMP cull floor by a wide margin.
        private const float TitleY0 = 0.900f, TitleY1 = 1.000f;   // 10.0% -> 53px / 61px
        private const float LevelY0 = 0.795f, LevelY1 = 0.885f;   //  9.0% -> 48px / 55px
        private const float BlurbY0 = 0.675f, BlurbY1 = 0.785f;   // 11.0% -> 59px / 67px
        private const float StateY0 = 0.565f, StateY1 = 0.665f;   // 10.0% -> 53px / 61px
        private const float HeadY0 = 0.465f, HeadY1 = 0.555f;     //  9.0% -> 48px / 55px
        private const float ListY0 = 0.255f, ListY1 = 0.455f;     // 20.0% -> 107px / 122px (scrolls)
        private const float ActY0 = 0.020f, ActY1 = 0.235f;       // 21.5% -> 115px / 132px >= MinTouchPx(112)

        private const float TextX0 = 0.27f, TextX1 = 0.99f;       // right of the portrait medallion
        private const float PortX0 = 0.02f, PortX1 = 0.25f;       // medallion column
        private const float CostX0 = 0.02f, CostX1 = 0.46f;       // action band, left half
        private const float CtaX0 = 0.50f, CtaX1 = 0.99f;         // action band, right half

        private const float UnlockRowPx = 46f;                     // one preview row (>= the 24px floor x2)

        /// <summary>The Heart portrait the CLI imported 2026-09-06 (1024x1024, textureType 8 =
        /// Sprite, verified in heart.png.meta). Loaded through the SAME cache/Texture2D-fallback
        /// path the Manage building cards use, so a re-import cannot make this the one art route
        /// that behaves differently.</summary>
        private const string HeartArtKey = "Portraits/Buildings/heart";

        private GameObject _ui;
        private RectTransform _body;
        private PanelHandle _panelHandle;
        private string _notice;

        /// <summary>True while the screen is up (built on open, destroyed on close).</summary>
        public bool IsOpen => _ui != null;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Heart", Close, () => IsOpen);
            PanelRouter.Register(PanelId.Heart, (Action)Open);
            PanelRouter.Register(PanelId.Heart, (Action<string>)OpenWithContext);
        }

        private void OnDestroy()
        {
            PanelRouter.Unregister(PanelId.Heart, (Action)Open);
            PanelRouter.Unregister(PanelId.Heart, (Action<string>)OpenWithContext);
            if (_ui != null) Destroy(_ui);
            _ui = null;
        }

        /// <summary>Context door. The subject id is recorded in the trace so a capture says WHICH
        /// gated thing sent the player here; the screen itself is identical either way (there is
        /// one Heart).</summary>
        private void OpenWithContext(string subject)
        {
            if (!string.IsNullOrEmpty(subject))
                FlowTrace.Step("Heart", "opened from gated subject '" + subject + "'.");
            Open();
        }

        /// <summary>Build and show the screen.</summary>
        public void Open()
        {
            Close();                                   // never stack two canvases

            if (!Guard.Try("Heart", "build heart chrome", BuildChrome))
            {
                FlowTrace.Fail("Heart", "chrome build threw - screen not shown.");
                Close();
                return;
            }
            Render();

            // WO-465: a panel that never notifies reads as an invisible scrim and PanelRouter
            // reports the open as FAILED. This call is the difference between a door and a lie.
            if (!PanelManager.NotifyOpened(_panelHandle))
                FlowTrace.Warn("Heart", "PanelManager refused the open (another exclusive panel holds the screen).");
            FlowTrace.Step("Heart", "Heart screen opened at Heart Level " + HeartProgression.Level
                + "/" + HeartProgression.MaxLevel + " state=" + HeartProgression.State
                + " crystals=" + HeartProgression.Crystals + " cost=" + HeartProgression.NextCost());
        }

        /// <summary>Tear the screen down.</summary>
        public void Close()
        {
            _body = null;
            _notice = null;
            if (_ui != null) { Destroy(_ui); _ui = null; }
            PanelManager.NotifyClosed(_panelHandle);
        }

        /// <summary>Open if closed, close if open.</summary>
        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        // =====================================================================
        //  CHROME
        // =====================================================================

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("HeartPanelUI", 31200);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            var chrome = ElarionUiKit.BuildObsidianPanel(
                _ui.transform, "HEART OF ELARION",
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
                Close, frameName: RpgUiCatalog.FrameCore);
            if (chrome == null)
            {
                FlowTrace.Fail("Heart", "BuildObsidianPanel returned no chrome - the screen has no host.");
                return;
            }
            MedievalUiSkin.ApplyShell(chrome);

            if (chrome.content != null)
            {
                var fill = ElarionUiKit.AddImage(chrome.content.transform, "HeartBodyFill",
                    Vector2.zero, Vector2.one, ElarionUiKit.ObsidianFill, rounded: false);
                var fillImage = fill != null ? fill.GetComponent<Image>() : null;
                if (fillImage != null) fillImage.raycastTarget = false;
                if (fill != null) fill.transform.SetAsFirstSibling();
            }

            // ONE OWNED GEOMETRY PASS, copied from ManageScreenPanel.BuildChrome so both screens
            // reserve the shared Close band identically. A live rect read on the creation frame
            // returns RAW screen px (the CanvasScaler has not applied); PostScaleCanvasHeight
            // replays the scaler's own math, so the numbers below are reference px.
            float canvasH = ElarionUiKit.PostScaleCanvasHeight(
                chrome.root != null ? chrome.root.transform : _ui.transform);
            float panelFracH = 0.90f;
            if (chrome.root != null)
            {
                var rootRt = (RectTransform)chrome.root.transform;
                panelFracH = Mathf.Max(0.05f, rootRt.anchorMax.y - rootRt.anchorMin.y);
            }
            float panelPx = Mathf.Max(1f, canvasH * panelFracH);
            float closeBandTop = CloseBandY0 + ElarionUiKit.CanonCtaHeight / panelPx;
            float bodyFloor = closeBandTop + CloseGapY;

            RectTransform bodyRt = chrome.layout != null ? chrome.layout.body : null;
            float bodyTop = bodyRt != null ? bodyRt.anchorMax.y : 0.835f;
            if (bodyRt != null && bodyTop - bodyFloor > 0.05f)
            {
                bodyRt.anchorMin = new Vector2(bodyRt.anchorMin.x, bodyFloor);
                bodyRt.anchorMax = new Vector2(bodyRt.anchorMax.x, bodyTop);
                bodyRt.offsetMin = new Vector2(bodyRt.offsetMin.x, 0f);
                bodyRt.offsetMax = new Vector2(bodyRt.offsetMax.x, 0f);
            }
            _body = bodyRt ?? MakeZone(
                chrome.content != null ? chrome.content.transform : _ui.transform, "Zone_Body_Heart",
                new Vector2(0.055f, bodyFloor), new Vector2(0.945f, bodyTop));

            // §12 - the geometry is PROVEN by a capture, not by an eyeball. Every band's real px
            // height is printed so a blank line is diagnosable in ONE read instead of a theory.
            float wellPx = Mathf.Max(0f, (bodyTop - bodyFloor) * panelPx);
            FlowTrace.Step("Heart", string.Format(
                "bands(px): canvas={0:0} panel={1:0} well={2:0} || title={3:0} level={4:0} blurb={5:0} " +
                "state={6:0} head={7:0} list={8:0} action={9:0} (MinTouchPx={10:0}, TMP cull floor ~24)",
                canvasH, panelPx, wellPx,
                (TitleY1 - TitleY0) * wellPx, (LevelY1 - LevelY0) * wellPx, (BlurbY1 - BlurbY0) * wellPx,
                (StateY1 - StateY0) * wellPx, (HeadY1 - HeadY0) * wellPx, (ListY1 - ListY0) * wellPx,
                (ActY1 - ActY0) * wellPx, ElarionUiKit.MinTouchPx));
            if ((ActY1 - ActY0) * wellPx < ElarionUiKit.MinTouchPx)
                FlowTrace.Warn("Heart", string.Format(
                    "action band is {0:0}px, under MinTouchPx {1:0} - ClampMinTouch will grow the CTA and it " +
                    "will overprint the unlock list. Author the band taller; do not rely on the clamp.",
                    (ActY1 - ActY0) * wellPx, ElarionUiKit.MinTouchPx));
        }

        private static RectTransform MakeZone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // =====================================================================
        //  RENDER — bind what the model composed. Nothing is decided here.
        // =====================================================================

        private void Render()
        {
            if (_body == null) return;
            for (int i = _body.childCount - 1; i >= 0; i--)
            {
                var child = _body.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            Guard.Try("Heart", "render heart surface", BuildBody);
        }

        private void BuildBody()
        {
            int level = HeartProgression.Level;
            int max = HeartProgression.MaxLevel;
            int next = HeartProgression.NextLevel;
            HeartActionState state = HeartProgression.State;

            // ── PORTRAIT + STATUS MEDALLION ────────────────────────────────────────────────
            var medallion = MakeZone(_body, "HeartPortrait",
                new Vector2(PortX0, StateY0), new Vector2(PortX1, TitleY1));
            var frame = ElarionUiKit.AddImage(medallion, "HeartFrame", Vector2.zero, Vector2.one,
                Color.white, rounded: false);
            var frameImage = frame != null ? frame.GetComponent<Image>() : null;
            if (frameImage != null)
            {
                frameImage.sprite = LoadManageSprite(state == HeartActionState.Max
                    ? "RpgUi/manage/frame-max" : "RpgUi/manage/frame-tile");
                frameImage.preserveAspect = true;
                frameImage.raycastTarget = false;
                if (frameImage.sprite == null) frameImage.color = new Color(1f, 1f, 1f, 0f);
            }
            Sprite heartArt = ManageScreenPanel.LoadManageBuildingSpriteAt(HeartArtKey);
            if (heartArt == null)
                FlowTrace.Warn("Heart", "heart art unresolved at Resources/" + HeartArtKey
                    + " - the medallion falls back to the kit placeholder disc.");
            ElarionUiKit.Portrait(medallion, heartArt, active: state != HeartActionState.Max);

            var status = ElarionUiKit.AddImage(_body, "HeartStatusMedallion",
                new Vector2(0.905f, LevelY0), new Vector2(0.99f, LevelY1), Color.white, rounded: false);
            var statusImage = status != null ? status.GetComponent<Image>() : null;
            if (statusImage != null)
            {
                statusImage.sprite = LoadManageSprite(StatusMedallionKey(state));
                statusImage.preserveAspect = true;
                statusImage.raycastTarget = false;
                if (statusImage.sprite == null) statusImage.color = new Color(1f, 1f, 1f, 0f);
            }

            // ── TITLE / LEVEL / BLURB / STATE ──────────────────────────────────────────────
            // ⚠ "HEART LEVEL" is the ONE player-facing name for this gate (canon §6, ruling 11).
            // The stored field is still GameState.VillageTier; that is a save key, not a word the
            // player ever reads.
            var title = ElarionUiKit.Label(_body, "HEART OF ELARION", TitleY0, TitleY1,
                ElarionUi.Gold, (int)ElarionUi.FontTitle, TextAlignmentOptions.Left, TextX0, TextX1, bold: true);
            ElarionUiKit.FitSingleLine(title, 34f, 52f);   // band 53-61px

            string levelLine = "HEART LEVEL " + level + " of " + max
                             + (state == HeartActionState.Max ? " . MAX" : "");
            var levelLabel = ElarionUiKit.Label(_body, levelLine, LevelY0, LevelY1,
                ElarionUi.Parchment, (int)ElarionUi.FontBody, TextAlignmentOptions.Left,
                TextX0, 0.90f, bold: true);
            ElarionUiKit.FitSingleLine(levelLabel, 28f, 44f);   // band 48-55px

            var blurb = ElarionUiKit.Label(_body, HeartProgression.Blurb, BlurbY0, BlurbY1,
                ElarionUi.ParchmentDim, (int)ElarionUi.FontLabel, TextAlignmentOptions.TopLeft,
                TextX0, TextX1);
            ElarionUiKit.FitBlock(blurb, ElarionUiKit.FontHardFloor, 30f);   // band 59-67px, 2 lines

            string sentence = string.IsNullOrEmpty(_notice) ? HeartProgression.StateSentence() : _notice;
            var stateLabel = ElarionUiKit.Label(_body, sentence, StateY0, StateY1,
                state == HeartActionState.Ready ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.TopLeft, TextX0, TextX1, bold: true);
            stateLabel.gameObject.name = "HeartStateSentence";
            ElarionUiKit.FitBlock(stateLabel, ElarionUiKit.FontHardFloor, 30f);   // band 53-61px

            // ── WHAT THE NEXT LEVEL OPENS ─────────────────────────────────────────────────
            // DERIVED from building-tiers.json by the model (HeartProgression.UnlocksAt). No list
            // is typed here and none may ever be: the whole point is that new authored content
            // shows up with no code edit.
            var unlocks = HeartProgression.UnlocksAt(next);
            string heading = state == HeartActionState.Max
                ? "EVERYTHING GATED ON THE HEART IS OPEN"
                : "WHAT HEART LEVEL " + next + " OPENS";
            var head = ElarionUiKit.Label(_body, heading, HeadY0, HeadY1, ElarionUi.Gold,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.02f, TextX1, bold: true);
            ElarionUiKit.FitSingleLine(head, 26f, 38f);   // band 48-55px

            var listZone = MakeZone(_body, "HeartUnlockList",
                new Vector2(0.02f, ListY0), new Vector2(0.99f, ListY1));
            var scroll = ElarionUiKit.MakeScrollZone(listZone, spacing: 6f, padding: 6);
            var content = scroll != null ? scroll.content : null;
            if (content == null)
            {
                FlowTrace.Fail("Heart", "MakeScrollZone returned no content - the unlock list has no host.");
            }
            else if (state == HeartActionState.Max || unlocks.Count == 0)
            {
                // ⚠ NOT a silent empty. At MAX this is the truth; below max an empty list means the
                // CONTENT authors no gate at this level, and the line says so rather than implying
                // the screen broke.
                AddUnlockRow(content, state == HeartActionState.Max
                    ? "The Heart is fully raised."
                    : "No content is gated on Heart Level " + next + " yet.", false);
                FlowTrace.Step("Heart", "unlock preview for level " + next + " is EMPTY (state=" + state
                    + ") - building-tiers.json authors no requiresVillageTier=" + next + " row.");
            }
            else
            {
                for (int i = 0; i < unlocks.Count; i++)
                    AddUnlockRow(content, unlocks[i].Text, unlocks[i].Kind == HeartUnlockKind.Research);
                FlowTrace.Step("Heart", "unlock preview for level " + next + " = " + unlocks.Count
                    + " authored rows (derived from building-tiers.json, never typed).");
            }

            // ── THE ACTION BAND ────────────────────────────────────────────────────────────
            if (state == HeartActionState.Max)
            {
                // WO-2017 "Max": no upgrade CTA at all. A dead button is not an affordance.
                var maxLine = ElarionUiKit.Label(_body, "HEART LEVEL " + level + " . MAX",
                    ActY0, ActY1, ElarionUi.Gold, (int)ElarionUi.FontBody,
                    TextAlignmentOptions.Center, 0.02f, 0.99f, bold: true);
                maxLine.gameObject.name = "HeartMaxLine";
                ElarionUiKit.FitSingleLine(maxLine, 30f, 46f);   // band 115-132px
                return;
            }

            ElarionUiKit.CostRow(_body, HeartProgression.NextCostParts(),
                new Vector2(CostX0, ActY0), new Vector2(CostX1, ActY1),
                ElarionUi.Parchment, prefix: "Cost:", fontPx: (int)ElarionUi.FontLabel);

            var cta = ElarionUiKit.BuildObsidianButton(_body, HeartProgression.CtaLabel(),
                ElarionUiKit.ObsidianButtonStyle.Style1,
                state == HeartActionState.Ready
                    ? ElarionUiKit.ObsidianButtonColor.Yellow
                    : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(CtaX0, ActY0), new Vector2(CtaX1, ActY1), OnRaiseTapped);
            if (cta != null)
            {
                cta.gameObject.name = "HeartCta_Raise";
                // The refusal is EXPLICIT, never a dead button: the face stays tappable so a short
                // wallet answers in words (HeartProgression.StateSentence) instead of nothing
                // happening. §12 forbids a silent no-op.
                cta.interactable = true;
                MedievalUiSkin.ApplyButton(cta, state == HeartActionState.Ready);
            }
            ElarionUiKit.ClampMinTouch(cta);
        }

        private static void AddUnlockRow(Transform parent, string text, bool isResearch)
        {
            var row = new GameObject("HeartUnlockRow", typeof(RectTransform), typeof(LayoutElement));
            var rt = (RectTransform)row.transform;
            rt.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.minHeight = UnlockRowPx;              // 46px - a REFERENCE-PX height, never a fraction
            le.preferredHeight = UnlockRowPx;
            le.flexibleHeight = 0f;

            var plate = ElarionUiKit.AddImage(rt, "RowPlate", Vector2.zero, Vector2.one,
                new Color(0f, 0f, 0f, 0.28f), rounded: false);
            var plateImage = plate != null ? plate.GetComponent<Image>() : null;
            if (plateImage != null) plateImage.raycastTarget = false;

            var label = ElarionUiKit.Label(rt, (isResearch ? "Research: " : "Build: ") + text,
                0f, 1f, isResearch ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left, 0.03f, 0.98f);
            ElarionUiKit.FitSingleLine(label, ElarionUiKit.FontHardFloor, 32f);   // row 46px
        }

        private void OnRaiseTapped()
        {
            // The VIEW does not decide. It asks the model, and the model answers in words.
            bool raised = HeartProgression.TryRaise(out string status);
            _notice = status;
            FlowTrace.Step("Heart", (raised ? "RAISED" : "refused") + " -> \"" + status + "\"");
            // _notice is read by Render and DELIBERATELY survives it, so the outcome sentence
            // ("Heart raised to Level 2." / the shortfall) stays on screen instead of being
            // replaced by the generic state line on the very frame the player is reading it.
            Render();
        }

        /// <summary>
        /// The WO-2015 status medallion for a state. One sprite per state word, so the Heart reads
        /// as part of the same system as the Manage building cards.
        ///
        /// <para>⚠ MissingCrystals deliberately wears <b>status-available</b>, NOT status-locked.
        /// Owner ruling 15: "do not label the owned item as locked; gate the upgrade action." The
        /// Heart is owned and operating and its upgrade IS available - the player is simply short.
        /// The refusal is carried by the Gray CTA face and by the sentence naming the shortfall,
        /// which is the affordance the ruling asks for. The delivered medallion set has no
        /// "unaffordable" glyph, and painting a padlock here would teach the same false "you cannot
        /// get there" the whole WO exists to kill. status-locked is therefore UNUSED by this
        /// screen; that is deliberate, not an oversight.</para>
        /// </summary>
        private static string StatusMedallionKey(HeartActionState state)
        {
            switch (state)
            {
                case HeartActionState.Max: return "RpgUi/manage/status-max";
                default: return "RpgUi/manage/status-available";
            }
        }

        // The kit's loader with the Texture2D fallback + cache, reused from the Manage card path
        // so the Heart's art cannot become the one route that resolves differently.
        private static Sprite LoadManageSprite(string key) =>
            ManageScreenPanel.LoadManageBuildingSpriteAt(key);
    }
}
