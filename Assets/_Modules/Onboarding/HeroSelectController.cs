// =============================================================================
// HeroSelectController — drives the hero-select screen (intro flow).
// -----------------------------------------------------------------------------
// THE SCREEN (WO-C conversion 2026-07-03, coverage matrix row #18; layout per the
// owner's pinned Blink CHARACTER-CREATION design, canon memory hero-select-blink-
// creation-carousel / WO-559):
//   CHROME     : the Blink Obsidian master frame (FrameCore = Core_Panel) via
//                ElarionUiKit.BuildObsidianPanel — code-built uGUI, NO UIDocument,
//                (owner F8 2026-07-03: swapped OFF FrameCharacter/Stats_Panel — its
//                 arch-constrained body zone [y 0.110-0.605] cramped the 3-column
//                 creation layout and pushed the confirm CTA out of the frame; FrameCore
//                 [body y 0.075-0.855] gives the full-height well the layout needs.)
//                NO UXML, NO borrowed PanelSettings. The kit's shared Close is
//                HIDDEN (this is a forced-flow screen; confirm is the only exit).
//   LEFT       : the CLASS COLUMN — one Obsidian button per HeroCatalog entry
//                (data-driven; catalog order). V1: only the playable hero (Grom ==
//                Knight, KnightOnly) is selectable-to-confirm; the other classes
//                are still TAPPABLE for a PREVIEW but render visibly locked
//                ("Coming soon" tag under the button + LOCKED scrim on the stage).
//   CENTER     : the HERO — the large focal portrait (Resources/HeroPortraits/
//                <slug>, sprite-first, texture fallback, accent-glyph last resort)
//                in a dark well, hero name + role beneath it.
//   RIGHT      : the SPECS panel — lore blurb, HP/ATTACK/SPEED pip rows (uGUI
//                image pips — NO unicode glyphs in TMP), signature ability, and
//                the primary Q/F/E/R skill kit, all from HeroCatalog data.
//   FOOTER     : the single confirm CTA (Obsidian GREEN) in the frame's footer
//                zone — "Enter Elarion" enabled on the playable hero, disabled
//                "Coming Soon" on a locked preview.
//
// WHY CODE-BUILT (history, preserved): the original screen bound to named UXML
// elements and blanked whenever the UXML failed to instantiate in a player build
// (CLAUDE.md §8). The first fix rebuilt the tree in code but still hosted on a
// UIDocument/UITK. This conversion finishes the job: the whole screen is kit uGUI
// (HelpMenu is the reference conversion), so neither UXML nor PanelSettings can
// break it. Any UIDocument left on the host GameObject is disabled at build time.
//
// COPY: hero name / role / blurb resolve from en.json via CanonStrings at runtime
// (port-spec Part 4). Stats / ability / skills come from HeroCatalog (pure
// presentation data mirrored from abilities.json, legitimately in C#).
//
// PERSISTENCE + ROUTING (contract preserved EXACTLY from the carousel build):
//   * on confirm, GameStateService.ChooseHero(hero) writes GameState.HeroClass
//     and Save()s it;
//   * FeatureFlags.BypassPetSelect ON (default) -> SceneRouter.GoCastle();
//     flag OFF -> SceneRouter.GoPetSelect() (the reversibility hatch);
//   * the playable hero is PRE-persisted on build so GameState always has a
//     valid class even if the player confirms without navigating;
//   * a save that already records a hero self-skips straight to the Castle.
//
// Lives in DeNelle.Onboarding; references DeNelle.Core only — module isolation.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Drives the hero-select screen (WO-C uGUI conversion of the WO-559 design):
    /// builds — entirely in code, on the Blink Obsidian FrameCharacter chrome — a
    /// class column (left, one Obsidian button per <see cref="HeroCatalog"/> entry),
    /// the focal hero portrait (center) and the specs panel (right), with a single
    /// green confirm CTA in the frame footer. Every class is tappable for a preview;
    /// only the playable hero (Grom == Knight) is confirmable — locked classes show
    /// a "Coming soon" tag and a LOCKED stage scrim with the CTA disabled. On
    /// confirm it writes <see cref="GameState.HeroClass"/> and routes to the home
    /// hub (or PetSelect when <c>FeatureFlags.BypassPetSelect</c> is OFF). A
    /// returning player who already chose a hero is skipped straight to the Castle.
    /// </summary>
    public sealed class HeroSelectController : MonoBehaviour
    {
        // -- en.json keys for the screen's own copy --------------------------
        private const string TitleKey    = "heroSelect.title";
        private const string SubtitleKey = "heroSelect.subtitle";
        // The confirm CTA en.json key (falls back to "Enter Elarion").
        private const string DiveKey     = "heroSelect.diveVillage";

        [Header("Behaviour")]
        [Tooltip("Skip straight to the Castle when the save already records a hero " +
                 "(a returning player who finished the intro). Editor testing: " +
                 "disable to always show the screen.")]
        [SerializeField] private bool _skipWhenIntroComplete = true;

        // -- Built UI (all created in code; one kit canvas per open) ----------
        private GameObject _canvas;                       // the kit modal canvas root
        private ElarionUiKit.PanelChrome _chrome;         // Blink FrameCharacter chrome
        private RectTransform _classColumn;               // LEFT — class buttons (persistent)
        private RectTransform _stageCenter;               // CENTER — portrait (rebuilt per pick)
        private RectTransform _stageRight;                // RIGHT — specs (rebuilt per pick)
        private Button _confirmButton;                    // footer CTA (Obsidian Green)
        private TextMeshProUGUI _confirmLabel;            // the CTA's kit label (retext per pick)
        private Image[] _classButtonFaces;                // per-class button face (selection tint)

        private bool _built;
        private bool _hasSelection;
        private HeroClass _selectedHero;

        // Which catalog slot is on screen (index into HeroCatalog.Heroes).
        private int _shownIndex;

        // The single playable hero in V1 — Grom == HeroClass.Knight (KnightOnly ON).
        private const HeroClass PlayableHero = HeroClass.Knight;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void OnEnable()
        {
            // A returning player who already finished the intro skips this
            // screen entirely — route on before building any UI.
            if (_skipWhenIntroComplete && IsIntroComplete())
            {
                SceneRouter.GoCastle();
                return;
            }

            BuildScreen();
        }

        private void OnDisable()
        {
            if (_canvas != null) Destroy(_canvas);
            _canvas = null;
            _chrome = null;
            _confirmButton = null;
            _confirmLabel = null;
            _classButtonFaces = null;
            _built = false;
        }

        // =====================================================================
        //  Returning-player gate
        // =====================================================================

        /// <summary>
        /// True when the save already records a chosen hero — the intro flow is
        /// finished and this screen has nothing to ask. The gate is HeroClass alone
        /// (the pet-select step is gone for single-hero V1).
        /// </summary>
        private static bool IsIntroComplete()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return false; // first launch
            return svc.State.HeroClass != HeroClassOpt.None;
        }

        // =====================================================================
        //  Code-built layout — kit uGUI on the Blink master frame
        // =====================================================================

        /// <summary>
        /// Builds the entire hero-select screen in code on a fresh kit canvas:
        ///   canvas (ScreenSpaceOverlay, kit-built)
        ///     └─ Obsidian FrameCharacter chrome (title in the header zone; Close hidden)
        ///          ├─ body zone
        ///          │    ├─ class column (LEFT — one Obsidian button per catalog hero)
        ///          │    ├─ hero stage  (CENTER — focal portrait + name/role)
        ///          │    └─ specs panel (RIGHT — lore / stats pips / signature / skills)
        ///          └─ footer zone — the confirm CTA (Obsidian Green)
        /// No UIDocument, no UXML, no PanelSettings — nothing scene-hosted can blank it.
        /// </summary>
        private void BuildScreen()
        {
            using var _ = FlowTrace.Enter("Onboarding", "HeroSelectController.BuildScreen");

            // The IntroFlowSceneBuilder host GameObject may still carry the legacy
            // UIDocument (+ its UXML). Disable it so nothing paints under/over the
            // kit canvas — this screen no longer renders through UITK at all.
            var legacyDoc = GetComponent<UnityEngine.UIElements.UIDocument>();
            if (legacyDoc != null)
            {
                legacyDoc.enabled = false;
                FlowTrace.Step("Onboarding", "BuildScreen: legacy UIDocument disabled (uGUI conversion owns the screen).");
            }

            // Kit canvas — scene-owned (destroyed by the routing scene-load / OnDisable).
            _canvas = ElarionUiKit.BuildModalCanvas("HeroSelectUI", 5000);
            if (_canvas == null)
            {
                // P0 — no canvas means hero-select renders NOTHING (a blank screen the
                // player can't pass). Fail-loud to the break-log rather than a quiet warn.
                FlowTrace.Fail("Onboarding", "BuildScreen: kit canvas FAILED to build — hero-select will NOT display (BLANK SCREEN).");
                return;
            }

            // Blink master-frame chrome. onClose: null — forced flow; we also hide
            // the shared Close below (confirm is the only exit).
            _chrome = ElarionUiKit.BuildObsidianPanel(_canvas.transform,
                FallbackLocale(TitleKey, "Choose Your Hero"),
                new Vector2(0.015f, 0.02f), new Vector2(0.985f, 0.98f), onClose: null,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "crest");
            if (_chrome.close != null) _chrome.close.gameObject.SetActive(false);

            // W9 (WO-714): the kit's shared open ease (PanelOpenCloseFx, P8) on the
            // master-frame chrome — the same eased scale+fade every kit panel opens
            // with. Skin only; layout, routing and the forced-flow contract untouched.
            ElarionUiKit.AttachPanelOpenFx(_canvas,
                _chrome.root != null ? _chrome.root.GetComponent<RectTransform>() : null);

            // Drop-zones (frame art present) or content fallback (procedural panel).
            Transform body = _chrome.layout != null && _chrome.layout.body != null
                ? _chrome.layout.body.transform
                : _chrome.content.transform;

            // Subtitle eyebrow across the top of the body well.
            var subtitle = ElarionUiKit.Label(body, FallbackLocale(SubtitleKey, "Only one may answer the call."),
                0.94f, 1.00f, ElarionUi.Gold, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.02f, 0.98f, spacing: 1f, bold: true);
            subtitle.raycastTarget = false;
            FitLine(subtitle);

            // ── The three stage containers (fraction-anchored inside the body well).
            // Owner F8 2026-07-03: lifted the stage floor from 0.020 -> 0.145 so the
            // bottom of the (now full-height FrameCore) body well is reserved for the
            // confirm CTA — "move everything up a little so the [CTA] stays in the frame."
            _classColumn = MakeZone(body, "ClassColumn", new Vector2(0.000f, 0.145f), new Vector2(0.215f, 0.920f));
            _stageCenter = MakeZone(body, "HeroStage",   new Vector2(0.235f, 0.145f), new Vector2(0.590f, 0.920f));
            _stageRight  = MakeZone(body, "SpecsPanel",  new Vector2(0.610f, 0.145f), new Vector2(1.000f, 0.920f));

            BuildClassColumn();

            // ── Confirm CTA — Obsidian GREEN, the one exit. Anchored in the RESERVED
            // bottom band of the body well (not the thin filigree footer strip) so it is
            // guaranteed to sit comfortably INSIDE the frame art on every aspect (the
            // owner's out-of-frame F8 was the footer-anchored CTA falling below the art).
            Transform ctaParent = body;
            Vector2 ctaMin = new Vector2(0.34f, 0.020f);
            Vector2 ctaMax = new Vector2(0.66f, 0.120f);
            _confirmButton = ElarionUiKit.BuildObsidianButton(ctaParent,
                FallbackLocale(DiveKey, "Enter Elarion"),
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                ctaMin, ctaMax, OnDiveVillageClicked);
            _confirmLabel = _confirmButton != null
                ? _confirmButton.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
            FitLine(_confirmLabel);   // CTA label may never spill out of the button

            // Open ON the playable hero so the screen starts on the selectable,
            // pre-selected Grom (not a locked class).
            _shownIndex = IndexOf(PlayableHero);

            // Pre-persist the playable hero so GameState always has a valid class even
            // if the player confirms without navigating (KnightOnly-forced; idempotent).
            GameStateService.Instance?.ChooseHero(PlayableHero);

            // Paint the opening hero into the stage (sets selection + CTA state).
            PopulateStage(_shownIndex);

            _built = true;

            // V — prove the screen built: chrome + all three stage containers + the
            // class buttons + the CTA exist. A built-but-empty hero-select is a
            // dead-end screen, so a missing piece Fails.
            int classButtons = _classColumn != null ? CountButtons(_classColumn) : 0;
            bool stageOk = _stageCenter != null && _stageRight != null
                           && _stageCenter.childCount > 0 && _stageRight.childCount > 0;
            bool ctaOk = _confirmButton != null;
            if (classButtons != HeroCatalog.Heroes.Length || !stageOk || !ctaOk)
            {
                FlowTrace.Fail("Onboarding",
                    $"BuildScreen VERIFY FAILED — classButtons={classButtons}/{HeroCatalog.Heroes.Length} " +
                    $"stageOk={stageOk} ctaOk={ctaOk}. Hero-select built but EMPTY/incomplete (dead-end screen).");
            }
            else
            {
                FlowTrace.Step("Onboarding",
                    $"BuildScreen VERIFY ok — classButtons={classButtons} stageOk={stageOk} ctaOk={ctaOk}.");
            }
        }

        // =====================================================================
        //  LEFT — the class column (data-driven from HeroCatalog)
        // =====================================================================

        /// <summary>
        /// Builds one Obsidian class button per <see cref="HeroCatalog"/> entry, in
        /// catalog order. The playable class gets the GOLD face; locked classes get
        /// the GRAY face with a "Coming soon" micro-tag under the button. Every
        /// button is tappable (locked classes preview into the stage); only the
        /// playable class can be confirmed.
        /// </summary>
        private void BuildClassColumn()
        {
            var head = ElarionUiKit.Label(_classColumn, "CLASSES",
                0.955f, 1.00f, ElarionUi.Gilt, ElarionUi.FontMicro,
                TextAlignmentOptions.Center, 0f, 1f, spacing: 2f, bold: true);
            head.raycastTarget = false;
            FitLine(head);

            int n = HeroCatalog.Heroes.Length;
            _classButtonFaces = new Image[n];

            // Stack the buttons down the column: each row gets an equal band under
            // the header, with a slice reserved for the locked "Coming soon" tag.
            const float top = 0.94f;
            const float bottom = 0.02f;
            float rowH = (top - bottom) / Mathf.Max(1, n);

            for (int i = 0; i < n; i++)
            {
                HeroCardInfo info = HeroCatalog.Heroes[i];
                bool playable = IsPlayable(info.Hero);
                float y1 = top - i * rowH;
                float y0 = y1 - rowH;

                // Button band (upper ~62% of the row); tag band beneath it.
                var btnMin = new Vector2(0.03f, y0 + rowH * 0.34f);
                var btnMax = new Vector2(0.97f, y1 - rowH * 0.10f);

                int captured = i;   // capture the slot for the click handler
                var btn = ElarionUiKit.BuildObsidianButton(_classColumn,
                    ClassLabelFor(info),
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    playable ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    btnMin, btnMax, () => PopulateStage(captured));
                _classButtonFaces[i] = btn != null ? btn.image : null;
                if (btn != null)
                    FitLine(btn.GetComponentInChildren<TextMeshProUGUI>(true));

                if (!playable)
                {
                    var tag = ElarionUiKit.Label(_classColumn, "Coming soon",
                        y0 + rowH * 0.16f, y0 + rowH * 0.32f,
                        ElarionUi.ParchmentDim, ElarionUi.FontMicro,
                        TextAlignmentOptions.Center, 0.05f, 0.95f);
                    tag.fontStyle = FontStyles.Italic;
                    tag.raycastTarget = false;
                    FitLine(tag);
                }
            }
        }

        /// <summary>
        /// The class-column label for a catalog hero — the CLASS (enum name, the
        /// data-driven "Knight / Ranger / Mage / Cleric" set), never hardcoded copy.
        /// </summary>
        private static string ClassLabelFor(HeroCardInfo info)
            => info != null ? info.Hero.ToString() : "?";

        // =====================================================================
        //  CENTER + RIGHT — the hero stage (rebuilt per selection)
        // =====================================================================

        /// <summary>
        /// Re-paints the stage for the catalog slot at <paramref name="index"/>:
        /// rebuilds the CENTER (focal portrait + LOCKED scrim when locked + name/
        /// role under it) and RIGHT (lore / stats pips / signature / primary skills)
        /// content, then refreshes the class-button highlight and the confirm CTA
        /// (enabled "Enter Elarion" on the playable hero, disabled "Coming Soon" on
        /// a locked one). The selection is the playable hero only — tapping a locked
        /// class is a PREVIEW, not a pick.
        /// </summary>
        private void PopulateStage(int index)
        {
            if (_stageCenter == null || _stageRight == null) return;
            if (HeroCatalog.Heroes.Length == 0) return;

            index = ((index % HeroCatalog.Heroes.Length) + HeroCatalog.Heroes.Length) % HeroCatalog.Heroes.Length;
            _shownIndex = index;
            HeroCardInfo info = HeroCatalog.Heroes[index];
            bool playable = IsPlayable(info.Hero);

            ClearChildren(_stageCenter);
            ClearChildren(_stageRight);

            BuildCenterStage(info, playable);
            BuildSpecsPanel(info, playable);

            // Selection: only the playable hero can be selected. Previewing a
            // locked class leaves no selectable choice.
            if (playable)
            {
                _selectedHero = info.Hero;
                _hasSelection = true;
            }
            else
            {
                _hasSelection = false;
            }

            RefreshConfirm(playable);
            RefreshClassHighlight();
        }

        /// <summary>CENTER — the focal hero portrait in a dark well + name/role beneath.</summary>
        private void BuildCenterStage(HeroCardInfo info, bool playable)
        {
            // Portrait well (dark recess) filling the upper ~78% of the stage.
            var well = ElarionUiKit.Well(_stageCenter, new Vector2(0.02f, 0.22f), new Vector2(0.98f, 1.00f));

            // The hero image itself — sprite-first, texture fallback, glyph last.
            var portraitGo = new GameObject("HeroPortrait", typeof(RectTransform));
            portraitGo.transform.SetParent(well.transform, false);
            var prt = (RectTransform)portraitGo.transform;
            prt.anchorMin = new Vector2(0.04f, 0.03f);
            prt.anchorMax = new Vector2(0.96f, 0.97f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            ApplyPortrait(portraitGo, info, playable);

            // LOCKED scrim over the portrait for a locked class.
            if (!playable)
            {
                var scrim = ElarionUiKit.AddImage(well.transform, "LockScrim",
                    Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.55f), rounded: false);
                var scrimImg = scrim.GetComponent<Image>();
                if (scrimImg != null) scrimImg.raycastTarget = false;

                var locked = ElarionUiKit.Label(scrim.transform, "LOCKED",
                    0.46f, 0.60f, ElarionUi.Parchment, ElarionUi.FontHead,
                    TextAlignmentOptions.Center, 0f, 1f, spacing: 3f, bold: true);
                locked.raycastTarget = false;
                FitLine(locked);

                var soon = ElarionUiKit.Label(scrim.transform, "Coming Soon",
                    0.38f, 0.46f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TextAlignmentOptions.Center, 0f, 1f);
                soon.fontStyle = FontStyles.Italic;
                soon.raycastTarget = false;
                FitLine(soon);
            }

            // Name + role under the portrait. Owner F8 2026-07-03: the playable hero's
            // name (Knight) uses WHITE so it pops against the frame; a locked hero stays dim.
            var nameLabel = ElarionUiKit.Label(_stageCenter, CanonStrings.Locale(info.NameKey),
                0.115f, 0.205f, playable ? Color.white : ElarionUi.ParchmentDim,
                ElarionUi.FontTitle, TextAlignmentOptions.Center, 0.02f, 0.98f, spacing: 1f, bold: true);
            nameLabel.raycastTarget = false;
            FitLine(nameLabel);

            var roleLabel = ElarionUiKit.Label(_stageCenter, CanonStrings.Locale(info.RoleKey),
                0.035f, 0.115f, ElarionUi.Gold, ElarionUi.FontLabel,
                TextAlignmentOptions.Center, 0.02f, 0.98f, spacing: 1.5f, bold: true);
            roleLabel.raycastTarget = false;
            FitLine(roleLabel);
        }

        /// <summary>
        /// RIGHT — the specs panel: lore blurb, HP/ATTACK/SPEED pip rows, signature
        /// ability, and the primary Q/F/E/R skill kit — all from HeroCatalog data.
        /// </summary>
        private void BuildSpecsPanel(HeroCardInfo info, bool playable)
        {
            // — LORE — (upper band)
            SectionHead(_stageRight, "LORE", 0.955f, 1.00f);
            var blurb = ElarionUiKit.Label(_stageRight, CanonStrings.Locale(info.BlurbKey),
                0.760f, 0.950f, ElarionUi.Parchment, ElarionUi.FontLabel,
                TextAlignmentOptions.TopLeft, 0.02f, 0.98f);
            blurb.textWrappingMode = TextWrappingModes.Normal;
            blurb.raycastTarget = false;
            FitBlock(blurb);

            // — STATS — (pip rows; uGUI image pips, no unicode glyphs in TMP)
            SectionHead(_stageRight, "STATS", 0.705f, 0.750f);
            BuildPipRow(_stageRight, "HP",     info.Hp,     0.645f, 0.700f);
            BuildPipRow(_stageRight, "ATTACK", info.Attack, 0.585f, 0.640f);
            BuildPipRow(_stageRight, "SPEED",  info.Speed,  0.525f, 0.580f);

            // — SIGNATURE —
            SectionHead(_stageRight, "SIGNATURE", 0.460f, 0.505f);
            var sigName = ElarionUiKit.Label(_stageRight, info.AbilityName,
                0.405f, 0.458f, ElarionUi.Gold, ElarionUi.FontBody,
                TextAlignmentOptions.Left, 0.02f, 0.98f, bold: true);
            sigName.raycastTarget = false;
            FitLine(sigName);
            var sigDesc = ElarionUiKit.Label(_stageRight, info.AbilityDesc,
                0.330f, 0.403f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TextAlignmentOptions.TopLeft, 0.02f, 0.98f);
            sigDesc.textWrappingMode = TextWrappingModes.Normal;
            sigDesc.raycastTarget = false;
            FitBlock(sigDesc);

            // — PRIMARY SKILLS — the hero's Q/F/E/R kit (mirrored from abilities.json
            // via HeroCatalog). A labelled placeholder shows for a hero whose kit is
            // not yet authored (e.g. the Cleric).
            SectionHead(_stageRight, "PRIMARY SKILLS", 0.265f, 0.310f);
            var skills = info.PrimarySkills;
            if (skills != null && skills.Length > 0)
            {
                const float sTop = 0.255f;
                const float sBottom = 0.015f;
                float sRow = (sTop - sBottom) / Mathf.Max(1, skills.Length);
                for (int s = 0; s < skills.Length; s++)
                {
                    float y1 = sTop - s * sRow;
                    BuildSkillRow(_stageRight, skills[s].Slot, skills[s].Name,
                                  y1 - sRow * 0.92f, y1 - sRow * 0.08f);
                }
            }
            else
            {
                var soon = ElarionUiKit.Label(_stageRight, "Abilities revealed at launch",
                    0.185f, 0.255f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                    TextAlignmentOptions.TopLeft, 0.02f, 0.98f);
                soon.fontStyle = FontStyles.Italic;
                soon.raycastTarget = false;
                FitBlock(soon);
            }
        }

        /// <summary>A small gilt section heading with a hairline rule beneath it.</summary>
        private static void SectionHead(Transform parent, string text, float y0, float y1)
        {
            var head = ElarionUiKit.Label(parent, text, y0, y1,
                ElarionUi.Gilt, ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.02f, 0.98f, spacing: 2f, bold: true);
            head.raycastTarget = false;
            FitLine(head);

            var rule = ElarionUiKit.AddImage(parent, "Rule", new Vector2(0.02f, y0),
                new Vector2(0.98f, y0 + 0.004f),
                new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.45f), rounded: false);
            var ruleImg = rule.GetComponent<Image>();
            if (ruleImg != null) ruleImg.raycastTarget = false;
        }

        /// <summary>
        /// One stat row — a gold key label + five square uGUI pips (gold filled /
        /// dim empty). Image pips, not text glyphs — NO unicode in TMP (ASCII rule).
        /// </summary>
        private static void BuildPipRow(Transform parent, string label, int value, float y0, float y1)
        {
            var key = ElarionUiKit.Label(parent, label, y0, y1,
                ElarionUi.Gold, ElarionUi.FontMicro,
                TextAlignmentOptions.Left, 0.02f, 0.30f, spacing: 1f, bold: true);
            key.raycastTarget = false;
            FitLine(key);

            value = Mathf.Clamp(value, 0, 5);
            const float pipX0 = 0.34f;
            const float pipW = 0.115f;
            const float pipGap = 0.015f;
            float padY = (y1 - y0) * 0.18f;
            for (int p = 0; p < 5; p++)
            {
                float x0 = pipX0 + p * (pipW + pipGap);
                var pip = ElarionUiKit.AddImage(parent, "Pip" + p,
                    new Vector2(x0, y0 + padY), new Vector2(x0 + pipW, y1 - padY),
                    p < value ? ElarionUi.Gold
                              : new Color(ElarionUi.ParchmentDim.r, ElarionUi.ParchmentDim.g,
                                          ElarionUi.ParchmentDim.b, 0.25f),
                    rounded: true);
                var pipImg = pip.GetComponent<Image>();
                if (pipImg != null) pipImg.raycastTarget = false;
            }
        }

        /// <summary>One primary-skill row — a slot badge (Q/F/E/R) + the ability name.
        /// W9 (WO-714): the badge is sprite-FIRST on the pack's Stat_Element plate
        /// (element/element_stat, 9-sliced, chrome-tinted) with the pre-existing
        /// procedural gold chip as the null-art fallback — the badge ink follows the
        /// plate (parchment on the dark pack plate, Ink on the gold fallback).</summary>
        private static void BuildSkillRow(Transform parent, string slot, string name, float y0, float y1)
        {
            var plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementStat);
            var badge = ElarionUiKit.AddImage(parent, "SlotBadge",
                new Vector2(0.02f, y0), new Vector2(0.115f, y1),
                plateSprite != null ? ElarionUiKit.ChromeTint : ElarionUi.GoldButton,
                rounded: plateSprite == null);
            var badgeImg = badge.GetComponent<Image>();
            if (badgeImg != null)
            {
                badgeImg.raycastTarget = false;
                if (plateSprite != null)
                {
                    badgeImg.sprite = plateSprite;
                    badgeImg.type = Image.Type.Sliced;
                    badgeImg.fillCenter = true;
                }
            }
            var badgeLbl = ElarionUiKit.Label(badge.transform, slot, 0f, 1f,
                plateSprite != null ? ElarionUi.Parchment : ElarionUi.Ink, ElarionUi.FontMicro,
                TextAlignmentOptions.Center, 0f, 1f, bold: true);
            badgeLbl.raycastTarget = false;
            FitLine(badgeLbl);

            var nameLbl = ElarionUiKit.Label(parent, name, y0, y1,
                ElarionUi.Parchment, ElarionUi.FontLabel,
                TextAlignmentOptions.Left, 0.15f, 0.98f, bold: true);
            nameLbl.raycastTarget = false;
            FitLine(nameLbl);
        }

        // =====================================================================
        //  Confirm CTA + class-highlight state
        // =====================================================================

        /// <summary>
        /// Refreshes the confirm CTA for the current hero: enabled "Enter Elarion" on
        /// the playable hero, disabled "Coming Soon" on a locked one.
        /// </summary>
        private void RefreshConfirm(bool playable)
        {
            if (_confirmButton == null) return;
            if (_confirmLabel != null)
                _confirmLabel.text = playable ? FallbackLocale(DiveKey, "Enter Elarion") : "Coming Soon";
            _confirmButton.interactable = playable && _hasSelection;
        }

        /// <summary>Brightens the on-screen class's button face, dims the rest.</summary>
        private void RefreshClassHighlight()
        {
            if (_classButtonFaces == null) return;
            for (int i = 0; i < _classButtonFaces.Length; i++)
            {
                if (_classButtonFaces[i] == null) continue;
                _classButtonFaces[i].color = (i == _shownIndex)
                    ? Color.white
                    : new Color(0.62f, 0.62f, 0.62f, 1f);
            }
        }

        // =====================================================================
        //  Confirm — write the hero choice then route (CONTRACT PRESERVED)
        // =====================================================================

        /// <summary>
        /// "Enter Elarion" — the confirm CTA. Persists the chosen (playable) hero, then
        /// routes DIRECTLY to the home hub (MainCastle_Hall) via GoCastle(). A no-op when
        /// the on-screen hero is locked (the CTA is disabled there, but guard anyway).
        ///
        /// The live path is HeroSelect -> Castle — the pet-select step is gone for
        /// single-hero V1. The FeatureFlags.BypassPetSelect branch is kept ONLY as a
        /// reversibility hatch (flag OFF restores the old pet step).
        /// </summary>
        private void OnDiveVillageClicked()
        {
            if (!_hasSelection) return;   // locked hero on screen — nothing to confirm
            PersistHero();

            // Reversibility hatch: flag OFF -> old pet step. DEFAULT path is GoCastle
            // (flag default ON) — single-hero V1 never shows PetSelect.
            if (FeatureFlags.BypassPetSelect)
            {
                // FTUE-01 (2026-07-19): the WO-748 founding choice (Default Town vs Build
                // Your Own) was wired ONLY on the PetSelect route (PetSelectController), so
                // on this default BypassPetSelect path -- HeroSelect straight to the hub --
                // it NEVER showed and every fresh founder silently got the blank template.
                // Present it HERE, at the genuine HeroSelect->hub chokepoint, BEFORE the
                // first Castle load (the choice must set StrategicPlacementMigrated before
                // the Castle-scene migration writer runs). PresentOrContinue self-gates on
                // ShouldOffer (a returning / already-founded player continues straight to
                // GoCastle), so this only fires on a genuine fresh founding and is idempotent.
                FlowTrace.Step("Onboarding", "OnDiveVillageClicked: single-hero V1 -- founding choice then GoCastle (PetSelect skipped).");
                FoundingChoiceController.PresentOrContinue(SceneRouter.GoCastle);
                return;
            }

            SceneRouter.GoPetSelect();
        }

        /// <summary>Writes <see cref="GameState.HeroClass"/> via the service and saves.</summary>
        private void PersistHero()
        {
            var svc = GameStateService.Instance;
            if (svc != null)
            {
                svc.ChooseHero(_selectedHero);
            }
            else
            {
                FlowTrace.Warn("Onboarding", "PersistHero: No GameStateService — the hero choice was NOT persisted. Routing onward anyway.");
            }
        }

        // =====================================================================
        //  Portrait resolution
        // =====================================================================

        /// <summary>
        /// Applies a hero's portrait art to the portrait GameObject — sprite-first
        /// (uGUI Image, aspect kept), Texture2D fallback (RawImage), accent glyph
        /// (the catalog's ASCII letter) last. No missing-asset risk —
        /// Grom/Thrain/Sylas/Elara portraits all exist in Resources/HeroPortraits.
        /// </summary>
        private static void ApplyPortrait(GameObject host, HeroCardInfo info, bool playable)
        {
            if (host == null || info == null) return;
            string slug = SlugFor(info.Hero);
            float dim = playable ? 1f : 0.5f;   // dim a locked hero

            var portraitSprite = Resources.Load<Sprite>($"HeroPortraits/{slug}");
            if (portraitSprite != null)
            {
                var img = host.AddComponent<Image>();
                img.sprite = portraitSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = new Color(dim, dim, dim, 1f);
                img.raycastTarget = false;
                return;
            }

            var portraitTex = Resources.Load<Texture2D>($"HeroPortraits/{slug}");
            if (portraitTex != null)
            {
                var raw = host.AddComponent<RawImage>();
                raw.texture = portraitTex;
                raw.color = new Color(dim, dim, dim, 1f);
                raw.raycastTarget = false;
                return;
            }

            // Last resort: the catalog's ASCII accent glyph, big and centred.
            var glyph = ElarionUiKit.Label(host.transform, info.Glyph, 0f, 1f,
                info.Accent, 96, TextAlignmentOptions.Center, 0f, 1f, bold: true);
            glyph.raycastTarget = false;
            FitLine(glyph);
        }

        // =====================================================================
        //  Small helpers
        // =====================================================================

        /// <summary>True when a hero class is selectable in V1 (only the playable hero).</summary>
        private static bool IsPlayable(HeroClass hero) => hero == PlayableHero;

        /// <summary>Catalog index for a hero class, or 0 when absent (never out of range).</summary>
        private static int IndexOf(HeroClass hero)
        {
            for (int i = 0; i < HeroCatalog.Heroes.Length; i++)
                if (HeroCatalog.Heroes[i].Hero == hero) return i;
            return 0;
        }

        /// <summary>Canon-roster portrait slug for a hero class.</summary>
        private static string SlugFor(HeroClass hero) => hero switch
        {
            HeroClass.Mage   => "Thrain",
            HeroClass.Knight => "Grom",
            HeroClass.Ranger => "Sylas",
            HeroClass.Cleric => "Elara",
            _                => hero.ToString(),
        };

        /// <summary>
        /// Returns the localised string for <paramref name="key"/>, falling back to
        /// <paramref name="fallback"/> when the key is absent (so a label is never
        /// blank if <c>en.json</c> hasn't been updated yet).
        /// </summary>
        private static string FallbackLocale(string key, string fallback)
        {
            var s = CanonStrings.Locale(key);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        // ── Text-fit guards (owner F8 2026-07-06: "screen is writing overtop of
        // itself" at a small window). Every label on this screen lives in a
        // fraction-anchored band; the kit's Label() gives it a FIXED font size with
        // TMP's default Overflow mode, so at small window heights the text spills
        // out of its band and paints over the section below. These two helpers make
        // overflow structurally impossible: TMP autosize shrinks the text to fit
        // its band (down to a legible floor), and Ellipsis truncates anything that
        // still cannot fit — text can never escape its rect again.

        /// <summary>
        /// Fits a SINGLE-LINE label inside its band: no wrapping, autosize between a
        /// legible floor and the authored size, Ellipsis if it still cannot fit.
        /// </summary>
        private static void FitLine(TextMeshProUGUI t)
        {
            if (t == null) return;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = Mathf.Clamp(t.fontSize * 0.5f, 8f, t.fontSize);
        }

        /// <summary>
        /// Fits a MULTI-LINE block (lore / ability copy) inside its band: wrapped,
        /// autosize between a legible floor and the authored size, Ellipsis on the
        /// last visible line if the copy still cannot fit.
        /// </summary>
        private static void FitBlock(TextMeshProUGUI t)
        {
            if (t == null) return;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = Mathf.Clamp(t.fontSize * 0.5f, 8f, t.fontSize);
        }

        /// <summary>A transparent fraction-anchored container RectTransform.</summary>
        private static RectTransform MakeZone(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Destroys all children of a container (stage rebuild).</summary>
        private static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>Counts the Button components directly under a container (verify).</summary>
        private static int CountButtons(Transform t)
        {
            if (t == null) return 0;
            int n = 0;
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).GetComponent<Button>() != null) n++;
            return n;
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the hero-select scene.
// -----------------------------------------------------------------------------
//   1. The HeroSelect scene is generated by DeNelle.Editor.IntroFlowSceneBuilder
//      (menu: Defenders/Intro Flow/Build Hero + Pet Select Scenes). It creates a
//      Camera, an EventSystem, and a host GameObject with this controller
//      attached. The host MAY still carry the legacy UIDocument — BuildScreen()
//      DISABLES it and builds the entire screen as kit uGUI on its own canvas, so
//      neither the UXML nor a missing PanelSettings can affect this screen.
//      (RequireComponent(UIDocument) was removed in the WO-C conversion.)
//
//   2. The intro/story cinematic (StoryIntroController) lives in the TITLE scene,
//      not here. The transition into hero-select is a single-scene LoadScene
//      (SceneRouter.GoHeroSelect), so Unity destroys the Title scene — and the
//      cold-open overlay with it — before this scene's OnEnable runs; they never
//      coexist.
//
//   3. GameStateService must exist before this screen so ChooseHero persists. It is
//      a DontDestroyOnLoad singleton from the Core bootstrap in Title. If a session
//      enters HeroSelect cold, the controller logs a warning and still routes on
//      (the choice just is not saved).
//
//   4. CLASS COLUMN: every catalog hero is tappable for a stage preview; only the
//      playable hero (Grom == Knight, FeatureFlags.KnightOnly) is confirmable. To
//      unlock more heroes later, widen IsPlayable(HeroClass) — no layout change
//      needed; the locked tag/scrim/CTA state all derive from it.
// =============================================================================
