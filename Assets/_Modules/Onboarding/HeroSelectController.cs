// =============================================================================
// HeroSelectController — drives the hero-select screen (intro flow).
// -----------------------------------------------------------------------------
// THE SCREEN (WO-503 single-hero V1):
//   TOP        : a light brand block (title + subtitle) + the eyebrow
//                "— YOUR HERO —".
//   CENTERPIECE: the HERO STAGE for Grom (the single playable hero == Knight) —
//                a LARGE gold-framed portrait with his backstory UNDER it (name /
//                role / blurb) on the LEFT, and a STATS panel (HP / Attack / Speed
//                pips + signature ability) BESIDE it on the RIGHT. Responsive: a
//                ROW in landscape, re-flows to a COLUMN in portrait (never fixed
//                pixel columns).
//   BELOW      : a LOCKED "coming later" carousel of the other three heroes
//                (Thrain/Mage, Sylas/Ranger, Elara/Cleric) — dimmed, scrim +
//                LOCK badge, PickingMode.Ignore so they are PROVABLY non-selectable.
//   FOOTER     : a single confirm CTA "Enter Elarion" (Grom pre-selected, enabled
//                on load) that persists the hero and routes straight to the home
//                hub (MainCastle_Hall) — no pet-select step.
//
// WHY THIS KEPT REGRESSING (root cause, fixed here):
//   The old screen BOUND to named elements inside HeroSelectScreen.uxml
//   (_root.Q<VisualElement>("hero-dragon-stage") …). Per CLAUDE.md §8 / PIPELINE
//   §8, UXML + USS do NOT render reliably in the WebGL player build — when the
//   UXML tree failed to instantiate, every Q<>() returned null, every inline
//   styling pass silently no-op'd on a null guard, and the screen came up empty
//   or half-built. Each past "fix" layered more inline styles ON TOP of the UXML
//   elements but still DEPENDED on those elements existing — so it broke again
//   the next time UXML didn't resolve. Additionally there were no 4-even panels
//   (fixed 240px cards overflowed in portrait) and no selected-hero detail card.
//
//   THE FIX: this controller now CLEARS the root and BUILDS THE ENTIRE LAYOUT IN
//   CODE. It does not read a single name out of the UXML. A UXML edit (or a UXML
//   that doesn't render at all) can no longer break this screen — there is
//   nothing for it to break. The whole tree is code, sized with flex / percent
//   so it re-centres in both orientations, and a GeometryChangedEvent handler
//   re-flows the columns whenever the screen size / orientation changes. A
//   post-build self-assert verifies the four panels exist and are evenly spread.
//
// COPY: every hero name / role / blurb is resolved from en.json via CanonStrings
// at runtime (port-spec Part 4). The card accent colour + glyph come from
// HeroCatalog (pure presentation data, legitimately in C#).
//
// PERSISTENCE: on confirm, GameStateService.ChooseHero(hero) writes
// GameState.HeroClass and Save()s it, then routes onward.
//
// RETURNING-PLAYER SKIP: a save that already records a hero has finished the
// intro flow — the screen self-skips straight to the Castle so a returning
// player is not re-asked. (WO-503: the gate no longer requires a starter pet,
// because the pet-select step is gone and no longer writes StarterPetId.)
//
// Lives in DeNelle.Onboarding; references DeNelle.Core only — module isolation.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Drives the single-hero select screen (WO-503): builds (in code) the hero
    /// stage for Grom (large image + backstory + stats panel beside it) over a
    /// LOCKED, non-selectable "coming later" carousel of the other heroes. Grom is
    /// pre-selected; on confirm it writes <see cref="GameState.HeroClass"/> and
    /// routes straight to the home hub (MainCastle_Hall). Fully responsive —
    /// the stage re-flows row↔column on orientation change. A returning player who
    /// already chose a hero is skipped straight to the Castle.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HeroSelectController : MonoBehaviour
    {
        // -- en.json keys for the screen's own copy --------------------------
        private const string TitleKey    = "heroSelect.title";
        private const string SubtitleKey = "heroSelect.subtitle";
        // The confirm CTA en.json key (falls back to "Enter Elarion" — WO-503 D2).
        // Routes straight to the home hub (no pet-select step).
        private const string DiveKey    = "heroSelect.diveVillage";
        // WO-327: ConfirmKey ("heroSelect.jumpAction" / "Jump into the Action") was
        // removed with the DTT-crashing button.

        // -- Palette — SOURCED from the shared ElarionUi town-HUD language ----
        // (WO restyle: matches the town HUD — dark glass + ornate gold-rune
        // frames — not the retired hardcoded purple/amber. Role-named locals keep
        // the layout below unchanged while every colour resolves canonically.)
        private static readonly Color ColBackground   = ElarionUi.PanelStoneDark;                                     // full-screen stone
        private static readonly Color ColPanelDark     = ElarionUi.PanelStone;                                         // roster panel fill
        private static readonly Color ColAmber         = ElarionUi.Gold;                                               // accents / eyebrow → runic gold
        private static readonly Color ColTextBright    = ElarionUi.Parchment;                                          // primary text
        private static readonly Color ColTextMuted     = ElarionUi.ParchmentDim;                                       // secondary text

        [Header("UI")]
        [Tooltip("UIDocument hosting the hero-select panel. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Behaviour")]
        [Tooltip("Skip straight to the Castle when the save already records a hero " +
                 "(a returning player who finished the intro). Editor testing: " +
                 "disable to always show the screen.")]
        [SerializeField] private bool _skipWhenIntroComplete = true;

        // -- Built UI elements (all created in code) -------------------------
        // WO-503 single-hero layout: a HERO STAGE (large Grom image + backstory
        // under it, stats panel beside it) + a LOCKED CAROUSEL of the other heroes
        // ("coming later", non-selectable). Replaces the old four-even-card grid.
        private VisualElement _root;
        private VisualElement _heroStage;     // row (landscape) / column (portrait): image+story | stats
        private VisualElement _stageLeft;     // image + backstory column
        private VisualElement _stageRight;    // stats panel column
        private VisualElement _lockedRow;     // bottom band — the 3 locked carousel cards

        private bool _built;
        private bool _hasSelection;
        private HeroClass _selectedHero;

        // The single playable hero in V1 — Grom == HeroClass.Knight (KnightOnly ON).
        private const HeroClass PlayableHero = HeroClass.Knight;

        // WO-503 confirm CTA — "Enter Elarion" (D2): the destination is the home
        // hub, not a TD raid. Pre-enabled (single hero is pre-selected).
        private Button _diveButton;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

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
            if (_diveButton    != null) _diveButton.clicked    -= OnDiveVillageClicked;
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _built = false;
        }

        // =====================================================================
        //  Returning-player gate
        // =====================================================================

        /// <summary>
        /// True when the save already records a chosen hero — the intro flow is
        /// finished and this screen has nothing to ask.
        ///
        /// WO-503 dependency fix: this gate USED to require BOTH a hero AND a
        /// non-empty StarterPetId. The pet-select step is gone (single-hero V1) and
        /// no longer writes StarterPetId, so requiring it would NEVER be satisfied —
        /// a returning player who already picked Grom would be re-shown hero-select
        /// every launch. The gate is now HeroClass alone. (GameState.StarterPetId
        /// the FIELD stays — Echo Hollow + save migration still use it; only this
        /// boot gate stops depending on it.)
        /// </summary>
        private static bool IsIntroComplete()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return false; // first launch
            return svc.State.HeroClass != HeroClassOpt.None;
        }

        // =====================================================================
        //  Code-built layout — NO UXML dependency (regression-proof)
        // =====================================================================

        /// <summary>
        /// Builds the entire hero-select layout in code on a cleared root. Does
        /// not read any UXML element by name, so a UXML that fails to render in
        /// the build (CLAUDE.md §8) cannot break this screen. Structure:
        ///   root (column)
        ///     └─ roster panel  (full screen, opaque)
        ///          ├─ brand       (title + subtitle)
        ///          ├─ eyebrow     ("— YOUR HERO —")
        ///          ├─ hero stage  (image + backstory | stats — row/column reflow)
        ///          ├─ locked row  (3 dimmed, non-selectable "coming later" cards)
        ///          └─ footer      (confirm CTA "Enter Elarion")
        /// </summary>
        private void BuildScreen()
        {
            using var _ = FlowTrace.Enter("Onboarding", "HeroSelectController.BuildScreen");
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                // P0 — no root means the hero-select renders NOTHING (a blank screen the
                // player can't pass). Fail-loud to the break-log rather than a quiet warn.
                FlowTrace.Fail("Onboarding", "BuildScreen: NO UIDocument root — hero-select will NOT display (BLANK SCREEN).");
                return;
            }

            // Wipe whatever the UXML instantiated (or didn't) — we own the tree now.
            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.alignItems = Align.Stretch;
            _root.style.justifyContent = Justify.FlexStart;
            _root.style.backgroundColor = ColBackground;

            // WO-503: single-hero stage (large Grom image + backstory + stats) over
            // a locked "coming later" carousel of the other heroes. The roster panel
            // carries brand title/subtitle + the eyebrow at the top.
            BuildRosterPanel();   // full screen (brand + eyebrow + hero stage + locked carousel + CTA)

            // Grom (Knight) is PRE-SELECTED — the big stage IS the selection. The
            // confirm CTA is enabled immediately (there is nothing to choose).
            _selectedHero = PlayableHero;
            _hasSelection = true;
            GameStateService.Instance?.ChooseHero(PlayableHero); // KnightOnly-forced; idempotent
            RefreshConfirmEnabled();

            // Re-flow the responsive layout on every screen-size / orientation
            // change — keeps the hero stage row (landscape) re-flowing to a column
            // (portrait) without fixed pixel positions.
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            _built = true;

            // Run the orientation re-flow + self-assert once now in case the
            // panel already has its geometry (so we never wait a frame to verify).
            ReflowForSize(_root.resolvedStyle.width, _root.resolvedStyle.height);

            // V — prove the screen built: root has children AND the hero stage +
            // the 3 locked carousel cards exist. A built-but-empty hero-select is a
            // dead-end screen, so a zero count Fails.
            int rootChildren = _root.childCount;
            int lockedCount = _lockedRow != null ? _lockedRow.childCount : 0;
            bool stageOk = _heroStage != null && _stageLeft != null && _stageRight != null;
            if (rootChildren == 0 || !stageOk || lockedCount == 0)
            {
                FlowTrace.Fail("Onboarding",
                    $"BuildScreen VERIFY FAILED — rootChildren={rootChildren} stageOk={stageOk} locked={lockedCount}. " +
                    "Hero-select built but EMPTY/incomplete (blank/dead-end screen).");
            }
            else
            {
                FlowTrace.Step("Onboarding",
                    $"BuildScreen VERIFY ok — rootChildren={rootChildren} stageOk={stageOk} locked={lockedCount}.");
            }
        }

        // ── Full screen: roster panel ───────────────────────────────────────

        private void BuildRosterPanel()
        {
            var roster = new VisualElement { name = "hero-roster-panel" };
            roster.style.flexGrow = 1f;          // takes the full screen now
            roster.style.flexShrink = 1f;
            roster.style.flexBasis = 0f;
            roster.style.backgroundColor = ColPanelDark;
            roster.style.flexDirection = FlexDirection.Column;
            roster.style.alignItems = Align.Stretch;
            roster.style.justifyContent = Justify.FlexStart;
            roster.style.paddingTop = 18f;
            roster.style.paddingBottom = 16f;
            roster.style.paddingLeft = 10f;
            roster.style.paddingRight = 10f;

            // Brand block — title + subtitle at the top of the roster.
            var brand = new VisualElement { name = "hero-brand-block" };
            brand.style.alignSelf = Align.Stretch;
            brand.style.alignItems = Align.Center;
            brand.style.marginBottom = 12f;
            brand.style.flexShrink = 0f;
            brand.pickingMode = PickingMode.Ignore;

            var title = new Label(CanonStrings.Locale(TitleKey));
            title.style.fontSize = 28f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = ColTextBright;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            brand.Add(title);

            var subtitle = new Label(CanonStrings.Locale(SubtitleKey));
            subtitle.style.marginTop = 4f;
            subtitle.style.fontSize = 13f;
            subtitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            subtitle.style.color = ColAmber;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            brand.Add(subtitle);

            roster.Add(brand);

            // Amber divider seam below the brand.
            var divider = new VisualElement();
            divider.style.height = 2f;
            divider.style.flexShrink = 0f;
            divider.style.marginBottom = 10f;
            divider.style.alignSelf = Align.Stretch;
            divider.style.backgroundColor = ColAmber;
            roster.Add(divider);

            // Eyebrow — WO-503 D3: inline literal "— YOUR HERO —" (only one to pick).
            var eyebrow = new Label("— YOUR HERO —");
            eyebrow.style.fontSize = 12f;
            eyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            eyebrow.style.color = ColAmber;
            eyebrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            eyebrow.style.marginBottom = 8f;
            eyebrow.style.flexShrink = 0f;
            roster.Add(eyebrow);

            // ── HERO STAGE — the centerpiece. Row in landscape (image+story left,
            //    stats right); re-flows to a column in portrait (ReflowForSize).
            BuildHeroStage();
            roster.Add(_heroStage);

            // ── LOCKED CAROUSEL — the other heroes, "coming later", non-selectable.
            BuildLockedCarousel();
            roster.Add(_lockedRow);

            // Footer — the single confirm CTA.
            var footer = new VisualElement { name = "select-footer" };
            footer.style.marginTop = 12f;
            footer.style.flexShrink = 0f;
            footer.style.alignSelf = Align.Stretch;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.Center;
            footer.style.alignItems = Align.Center;

            // WO-503 D2: "Enter Elarion" — the destination is the home hub. Grom is
            // pre-selected, so the CTA is enabled on load.
            _diveButton = new Button { text = FallbackLocale(DiveKey, "Enter Elarion") };
            StyleCta(_diveButton, secondary: false);
            _diveButton.clicked += OnDiveVillageClicked;
            footer.Add(_diveButton);

            roster.Add(footer);
            _root.Add(roster);
        }

        /// <summary>
        /// Builds the single-hero stage for Grom (Knight): a LARGE gold-framed
        /// portrait with the backstory UNDER it on the left, and the stats panel
        /// (HP/Attack/Speed pips + signature ability + role) BESIDE it on the right.
        /// Row layout in landscape; ReflowForSize switches it to a column in portrait.
        /// </summary>
        private void BuildHeroStage()
        {
            HeroCardInfo info = FindInfo(PlayableHero);

            _heroStage = new VisualElement { name = "hero-stage" };
            _heroStage.style.flexGrow = 1f;
            _heroStage.style.flexShrink = 1f;
            _heroStage.style.flexDirection = FlexDirection.Row;   // ReflowForSize flips to Column in portrait
            _heroStage.style.alignItems = Align.Stretch;
            _heroStage.style.justifyContent = Justify.Center;

            // ── LEFT column: large image + backstory under it (the larger half).
            _stageLeft = new VisualElement { name = "hero-stage-left" };
            _stageLeft.style.flexGrow = 1.3f;
            _stageLeft.style.flexShrink = 1f;
            _stageLeft.style.flexBasis = 0f;
            _stageLeft.style.minWidth = 0f;
            _stageLeft.style.flexDirection = FlexDirection.Column;
            _stageLeft.style.alignItems = Align.Stretch;
            _stageLeft.style.marginRight = 8f;

            // Large hero image — gold double-rim, dark backing, cover-fit Grom.jpg.
            var imageFrame = new VisualElement { name = "hero-image-frame" };
            imageFrame.style.flexGrow = 1f;
            imageFrame.style.flexShrink = 1f;
            imageFrame.style.minHeight = 160f;
            imageFrame.style.overflow = Overflow.Hidden;
            imageFrame.style.backgroundColor = new Color(0f, 0f, 0f, 0.30f);
            float fr = 10f;
            imageFrame.style.borderTopLeftRadius = fr;
            imageFrame.style.borderTopRightRadius = fr;
            imageFrame.style.borderBottomLeftRadius = fr;
            imageFrame.style.borderBottomRightRadius = fr;
            SetBorderWidth(imageFrame, 3f);
            SetBorderColor(imageFrame, ElarionUi.Gilt);

            var portrait = new VisualElement { name = "hero-portrait" };
            portrait.style.position = Position.Absolute;
            portrait.style.left = 0f;
            portrait.style.right = 0f;
            portrait.style.top = 0f;
            portrait.style.bottom = 0f;
            portrait.style.alignItems = Align.Center;
            portrait.style.justifyContent = Justify.Center;
            ApplyPortrait(portrait, info, glyphSize: 96f);
            imageFrame.Add(portrait);
            _stageLeft.Add(imageFrame);

            // Backstory UNDER the image: name (gold, big) + role (amber) + blurb.
            var story = new VisualElement { name = "hero-backstory" };
            story.style.flexShrink = 0f;
            story.style.marginTop = 10f;
            story.style.paddingLeft = 2f;
            story.style.paddingRight = 2f;

            var nameLabel = new Label(info != null ? CanonStrings.Locale(info.NameKey) : "Grom");
            nameLabel.style.fontSize = 24f;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = ElarionUi.Gold;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            story.Add(nameLabel);

            var roleLabel = new Label(info != null ? CanonStrings.Locale(info.RoleKey) : string.Empty);
            roleLabel.style.marginTop = 2f;
            roleLabel.style.fontSize = 13f;
            roleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            roleLabel.style.color = ColAmber;
            roleLabel.style.whiteSpace = WhiteSpace.Normal;
            story.Add(roleLabel);

            var blurbLabel = new Label(info != null ? CanonStrings.Locale(info.BlurbKey) : string.Empty);
            blurbLabel.style.marginTop = 6f;
            blurbLabel.style.fontSize = 13f;
            blurbLabel.style.color = ColTextBright;
            blurbLabel.style.whiteSpace = WhiteSpace.Normal;
            story.Add(blurbLabel);

            _stageLeft.Add(story);
            _heroStage.Add(_stageLeft);

            // ── RIGHT column: the stats panel BESIDE the image.
            _stageRight = new VisualElement { name = "hero-stage-right" };
            _stageRight.style.flexGrow = 1f;
            _stageRight.style.flexShrink = 1f;
            _stageRight.style.flexBasis = 0f;
            _stageRight.style.minWidth = 0f;
            _stageRight.style.justifyContent = Justify.Center;
            _stageRight.style.marginLeft = 8f;

            var statsPanel = new VisualElement { name = "hero-stats-panel" };
            statsPanel.style.paddingTop = 14f;
            statsPanel.style.paddingBottom = 14f;
            statsPanel.style.paddingLeft = 16f;
            statsPanel.style.paddingRight = 16f;
            float pr = 12f;
            statsPanel.style.borderTopLeftRadius = pr;
            statsPanel.style.borderTopRightRadius = pr;
            statsPanel.style.borderBottomLeftRadius = pr;
            statsPanel.style.borderBottomRightRadius = pr;
            SetBorderWidth(statsPanel, 2f);
            SetBorderColor(statsPanel, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f));
            statsPanel.style.backgroundColor = ElarionUi.PanelStone;

            BuildStatRow(statsPanel, "HP",     PipString(info != null ? info.Hp : 5));
            BuildStatRow(statsPanel, "ATTACK", PipString(info != null ? info.Attack : 3));
            BuildStatRow(statsPanel, "SPEED",  PipString(info != null ? info.Speed : 2));

            // Divider then the signature ability.
            var statDivider = new VisualElement();
            statDivider.style.height = 1f;
            statDivider.style.marginTop = 10f;
            statDivider.style.marginBottom = 8f;
            statDivider.style.backgroundColor = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.4f);
            statsPanel.Add(statDivider);

            var abilityName = new Label(info != null ? info.AbilityName : "Bulwark Slam");
            abilityName.style.fontSize = 15f;
            abilityName.style.unityFontStyleAndWeight = FontStyle.Bold;
            abilityName.style.color = ColAmber;
            abilityName.style.whiteSpace = WhiteSpace.Normal;
            statsPanel.Add(abilityName);

            var abilityDesc = new Label(info != null ? info.AbilityDesc : string.Empty);
            abilityDesc.style.marginTop = 2f;
            abilityDesc.style.fontSize = 12f;
            abilityDesc.style.color = ColTextMuted;
            abilityDesc.style.whiteSpace = WhiteSpace.Normal;
            statsPanel.Add(abilityDesc);

            _stageRight.Add(statsPanel);
            _heroStage.Add(_stageRight);
        }

        /// <summary>
        /// Builds the locked "coming later" carousel — one dimmed, NON-SELECTABLE
        /// card per locked hero (Thrain/Mage, Sylas/Ranger, Elara/Cleric). Each card
        /// is PickingMode.Ignore with NO click handler, so a tap provably does
        /// nothing — it can never select a locked hero.
        /// </summary>
        private void BuildLockedCarousel()
        {
            _lockedRow = new VisualElement { name = "hero-locked-row" };
            _lockedRow.style.flexDirection = FlexDirection.Row;
            _lockedRow.style.justifyContent = Justify.Center;
            _lockedRow.style.alignItems = Align.Stretch;
            _lockedRow.style.flexShrink = 0f;
            _lockedRow.style.marginTop = 12f;
            // The whole band is inert — provably non-interactive.
            _lockedRow.pickingMode = PickingMode.Ignore;

            for (int i = 0; i < HeroCatalog.Heroes.Length; i++)
            {
                HeroCardInfo info = HeroCatalog.Heroes[i];
                if (info.Hero == PlayableHero) continue;   // Grom is the stage, not a locked card
                _lockedRow.Add(BuildLockedCard(info));
            }
        }

        /// <summary>
        /// Builds one dimmed, non-selectable locked card (portrait dimmed under a
        /// dark scrim + a LOCK badge + "Coming later" caption + dim name/class).
        /// Provably inert: PickingMode.Ignore and no PointerDownEvent.
        /// </summary>
        private VisualElement BuildLockedCard(HeroCardInfo info)
        {
            var card = new VisualElement { name = $"hero-locked-{info.Hero}" };
            card.style.flexGrow = 1f;
            card.style.flexShrink = 1f;
            card.style.flexBasis = 0f;
            card.style.maxWidth = Length.Percent(30f);
            card.style.minWidth = 0f;
            card.style.marginLeft = 5f;
            card.style.marginRight = 5f;
            float radius = 10f;
            card.style.borderTopLeftRadius = radius;
            card.style.borderTopRightRadius = radius;
            card.style.borderBottomLeftRadius = radius;
            card.style.borderBottomRightRadius = radius;
            SetBorderWidth(card, 2f);
            SetBorderColor(card, ElarionUi.Disabled);
            card.style.backgroundColor = ElarionUi.Disabled;
            card.style.overflow = Overflow.Hidden;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Stretch;
            card.style.paddingTop = 6f;
            card.style.paddingBottom = 6f;
            card.style.paddingLeft = 6f;
            card.style.paddingRight = 6f;
            // PROVABLY NON-SELECTABLE: the card ignores pointer events and registers
            // NO click handler — a tap does nothing, never selects a locked hero.
            card.pickingMode = PickingMode.Ignore;

            var imageFrame = new VisualElement { name = "locked-image-frame" };
            imageFrame.style.flexGrow = 1f;
            imageFrame.style.flexShrink = 1f;
            imageFrame.style.minHeight = 56f;
            imageFrame.style.overflow = Overflow.Hidden;
            imageFrame.style.backgroundColor = new Color(0f, 0f, 0f, 0.30f);
            float fr = 6f;
            imageFrame.style.borderTopLeftRadius = fr;
            imageFrame.style.borderTopRightRadius = fr;
            imageFrame.style.borderBottomLeftRadius = fr;
            imageFrame.style.borderBottomRightRadius = fr;
            SetBorderWidth(imageFrame, 1f);
            SetBorderColor(imageFrame, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.35f));

            // Dimmed portrait.
            var portrait = new VisualElement { name = "locked-portrait" };
            portrait.style.position = Position.Absolute;
            portrait.style.left = 0f;
            portrait.style.right = 0f;
            portrait.style.top = 0f;
            portrait.style.bottom = 0f;
            portrait.style.alignItems = Align.Center;
            portrait.style.justifyContent = Justify.Center;
            portrait.style.opacity = 0.35f;   // greyed/dimmed
            ApplyPortrait(portrait, info, glyphSize: 36f);
            imageFrame.Add(portrait);

            // Dark scrim + centered LOCK badge over the portrait.
            var scrim = new VisualElement { name = "locked-scrim" };
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0f;
            scrim.style.right = 0f;
            scrim.style.top = 0f;
            scrim.style.bottom = 0f;
            scrim.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            scrim.style.alignItems = Align.Center;
            scrim.style.justifyContent = Justify.Center;
            scrim.pickingMode = PickingMode.Ignore;

            var lockGlyph = new Label("LOCKED");
            lockGlyph.style.fontSize = 12f;
            lockGlyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            lockGlyph.style.color = ElarionUi.Parchment;
            lockGlyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            scrim.Add(lockGlyph);
            imageFrame.Add(scrim);

            card.Add(imageFrame);

            // Dim name + class so the player sees who is coming.
            var nameLabel = new Label(CanonStrings.Locale(info.NameKey));
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLabel.style.marginTop = 4f;
            nameLabel.style.fontSize = 12f;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = ColTextMuted;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(nameLabel);

            var classLabel = new Label(CanonStrings.Locale(info.RoleKey));
            classLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            classLabel.style.fontSize = 10f;
            classLabel.style.color = ColTextMuted;
            classLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(classLabel);

            var coming = new Label("Coming later");
            coming.style.unityTextAlign = TextAnchor.MiddleCenter;
            coming.style.marginTop = 1f;
            coming.style.fontSize = 9f;
            coming.style.unityFontStyleAndWeight = FontStyle.Italic;
            coming.style.color = ColTextMuted;
            card.Add(coming);

            return card;
        }

        /// <summary>
        /// Applies a hero's portrait art (cover-fit) to a portrait element, falling
        /// back to the accent glyph when the art is absent (shared by the hero stage
        /// and the locked cards). No missing-asset risk — Grom/Thrain/Sylas/Elara
        /// portraits all exist in Resources/HeroPortraits.
        /// </summary>
        private static void ApplyPortrait(VisualElement portrait, HeroCardInfo info, float glyphSize)
        {
            if (portrait == null || info == null) return;
            string slug = SlugFor(info.Hero);
            var portraitSprite = Resources.Load<Sprite>($"HeroPortraits/{slug}");
            if (portraitSprite != null)
            {
                portrait.style.backgroundImage = new StyleBackground(portraitSprite);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                return;
            }
            var portraitTex = Resources.Load<Texture2D>($"HeroPortraits/{slug}");
            if (portraitTex != null)
            {
                portrait.style.backgroundImage = new StyleBackground(portraitTex);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                return;
            }
            var glyph = new Label(info.Glyph);
            glyph.style.fontSize = glyphSize;
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.color = info.Accent;
            portrait.Add(glyph);
        }

        /// <summary>
        /// Builds one stat row into the stats panel — a fixed-width gold label on
        /// the left and a pre-filled 1-5 pip value on the right.
        /// </summary>
        private void BuildStatRow(VisualElement parent, string label, string pipValue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2f;

            var key = new Label(label);
            key.style.width = 64f;
            key.style.flexShrink = 0f;
            key.style.fontSize = 11f;
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            key.style.color = ColAmber;
            row.Add(key);

            var value = new Label(pipValue);
            value.style.fontSize = 14f;
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            value.style.color = ColTextBright;
            value.style.letterSpacing = 2f;
            row.Add(value);

            parent.Add(row);
        }

        /// <summary>
        /// Renders a 1-5 rating as filled / empty pips (e.g. ●●●○○ for 3). A clear,
        /// glanceable archetype read that needs no extra art dependency.
        /// </summary>
        private static string PipString(int value)
        {
            if (value < 0) value = 0;
            if (value > 5) value = 5;
            return new string('●', value) + new string('○', 5 - value);
        }

        // =====================================================================
        //  Responsive re-flow + self-assert
        // =====================================================================

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            ReflowForSize(evt.newRect.width, evt.newRect.height);
        }

        /// <summary>
        /// Re-flows the hero stage for the current screen size / orientation:
        /// LANDSCAPE = a ROW (large image + backstory on the left, stats panel on
        /// the right); PORTRAIT = a COLUMN (image on top, stats below, backstory
        /// under the image) so nothing crowds. No fixed x positions — purely flex —
        /// so it re-centres identically in both orientations. Then self-asserts.
        /// </summary>
        private void ReflowForSize(float width, float height)
        {
            if (!_built || _heroStage == null) return;
            if (width <= 0f || height <= 0f) return;

            bool portrait = height >= width;

            // Stage flips between row (landscape) and column (portrait).
            _heroStage.style.flexDirection = portrait ? FlexDirection.Column : FlexDirection.Row;

            // In portrait the columns stack — drop the side margins, add a small
            // vertical gap; in landscape they sit side-by-side with side margins.
            if (_stageLeft != null)
            {
                _stageLeft.style.marginRight = portrait ? 0f : 8f;
                _stageLeft.style.marginBottom = portrait ? 10f : 0f;
            }
            if (_stageRight != null)
            {
                _stageRight.style.marginLeft = portrait ? 0f : 8f;
            }

            VerifyStage();
        }

        /// <summary>
        /// Regression guard: asserts the hero stage (image + backstory + stats
        /// beside it) is present AND exactly the locked-carousel cards exist and are
        /// provably non-interactive (PickingMode.Ignore). Logs loudly if a future
        /// edit breaks the contract so it is caught immediately.
        /// </summary>
        private void VerifyStage()
        {
            if (_heroStage == null || _stageLeft == null || _stageRight == null)
            {
                FlowTrace.Fail("Onboarding",
                    "VerifyStage: HERO STAGE ASSERT FAILED — stage / left / right column missing.");
                return;
            }

            // The locked carousel = HeroCatalog minus the single playable hero.
            int expectedLocked = HeroCatalog.Heroes.Length - 1;
            int actualLocked = _lockedRow != null ? _lockedRow.childCount : 0;
            if (actualLocked != expectedLocked)
            {
                FlowTrace.Fail("Onboarding",
                    $"VerifyStage: LOCKED CAROUSEL ASSERT FAILED — expected {expectedLocked} locked cards, found {actualLocked}.");
                return;
            }

            // Every locked card MUST be non-interactive (PickingMode.Ignore) — a
            // tap can never select a locked hero.
            for (int i = 0; i < _lockedRow.childCount; i++)
            {
                var card = _lockedRow[i];
                if (card.pickingMode != PickingMode.Ignore)
                {
                    FlowTrace.Fail("Onboarding",
                        $"VerifyStage: locked card {i} is INTERACTIVE (pickingMode != Ignore) — it must be non-selectable.");
                    return;
                }
            }
        }

        // =====================================================================
        //  Selection (single hero — pre-selected, no in-screen choice)
        // =====================================================================

        /// <summary>Enables the confirm CTA when a hero is selected (always, for the single hero).</summary>
        private void RefreshConfirmEnabled()
        {
            if (_diveButton != null) _diveButton.SetEnabled(_hasSelection);
        }

        // =====================================================================
        //  Entry-point CTAs — write the hero choice then route
        // =====================================================================

        // WO-327: OnConfirmClicked() — the "Jump into the Action" handler that
        // routed into Defend the Tower — was REMOVED with its button.

        /// <summary>
        /// "Enter Elarion" — the confirm CTA. Persists the chosen hero (Grom), then
        /// routes DIRECTLY to the home hub (MainCastle_Hall) via GoCastle().
        ///
        /// WO-503 (D1, hard-wire): the live path is now UNCONDITIONAL HeroSelect ->
        /// Castle — the pet-select step is gone for single-hero V1. The
        /// FeatureFlags.BypassPetSelect branch is kept ONLY as a reversibility hatch
        /// (flag OFF restores the old pet step); the DEFAULT/normal path never loads
        /// PetSelect. The flag + the pet-step route are retired in the WO-504 purge.
        /// </summary>
        private void OnDiveVillageClicked()
        {
            if (!_hasSelection) return;
            PersistHero();

            // DELETION-PENDING (WO-503, after felt-verify): the WO-504 pet-select
            // purge removes the entire boot pet step. Once the owner felt-verifies
            // Title -> HeroSelect(Grom) -> Castle, delete the following assets:
            //   - Assets/_Modules/Onboarding/PetSelectController.cs (+ .meta)
            //   - Assets/Scenes/PetSelect.unity (+ .meta) and de-register it from Build Settings
            //   - Assets/_Modules/Onboarding/UI/PetSelectScreen.uxml (+ .meta)
            //   - SceneRouter.PetSelect const + SceneRouter.GoPetSelect()
            //   - IntroFlowSceneBuilder.cs PetSelect generation + Build-Settings insert
            //   - FeatureFlags.BypassPetSelect (the reversibility hatch below) once nothing references it
            // BOUNDARY: this is the pet-SELECT boot screen ONLY. Do NOT touch the
            // Echo WORKFORCE / harvest system or Assets/_Modules/Pets/** or the
            // GameState.StarterPetId field — those are the live pet/echo gameplay.

            // Reversibility hatch (D1): flag OFF -> old pet step. DEFAULT path is
            // GoCastle (flag default ON) — single-hero V1 never shows PetSelect.
            if (FeatureFlags.BypassPetSelect)
            {
                FlowTrace.Step("Onboarding", "OnDiveVillageClicked: single-hero V1 — GoCastle (PetSelect skipped).");
                SceneRouter.GoCastle();
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
        //  Small helpers
        // =====================================================================

        /// <summary>Canon-roster portrait slug for a hero class.</summary>
        private static string SlugFor(HeroClass hero) => hero switch
        {
            HeroClass.Mage   => "Thrain",
            HeroClass.Knight => "Grom",
            HeroClass.Ranger => "Sylas",
            HeroClass.Cleric => "Elara",
            _                => hero.ToString(),
        };

        /// <summary>Catalog entry for a hero class, or null if absent.</summary>
        private static HeroCardInfo FindInfo(HeroClass hero)
        {
            for (int i = 0; i < HeroCatalog.Heroes.Length; i++)
                if (HeroCatalog.Heroes[i].Hero == hero) return HeroCatalog.Heroes[i];
            return null;
        }

        /// <summary>
        /// Returns the localised string for <paramref name="key"/>, falling back to
        /// <paramref name="fallback"/> when the key is absent (so a button is never
        /// blank if <c>en.json</c> hasn't been updated yet).
        /// </summary>
        private static string FallbackLocale(string key, string fallback)
        {
            var s = CanonStrings.Locale(key);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        /// <summary>Sets all four border widths of an element.</summary>
        private static void SetBorderWidth(VisualElement el, float width)
        {
            if (el == null) return;
            el.style.borderTopWidth = width;
            el.style.borderBottomWidth = width;
            el.style.borderLeftWidth = width;
            el.style.borderRightWidth = width;
        }

        /// <summary>Sets all four border colours of an element.</summary>
        private static void SetBorderColor(VisualElement el, Color color)
        {
            if (el == null) return;
            el.style.borderTopColor = color;
            el.style.borderBottomColor = color;
            el.style.borderLeftColor = color;
            el.style.borderRightColor = color;
        }

        /// <summary>Inline-styles a footer CTA (primary amber, or secondary outline).</summary>
        private static void StyleCta(Button btn, bool secondary)
        {
            if (btn == null) return;
            btn.style.flexGrow = 1f;
            btn.style.flexBasis = 0f;
            btn.style.maxWidth = 280f;
            btn.style.height = 48f;
            btn.style.fontSize = 15f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.marginLeft = 6f;
            btn.style.marginRight = 6f;
            float r = 12f;
            btn.style.borderTopLeftRadius = r;
            btn.style.borderTopRightRadius = r;
            btn.style.borderBottomLeftRadius = r;
            btn.style.borderBottomRightRadius = r;
            if (secondary)
            {
                // Neutral stone "quiet" button — gold hairline rim, parchment text.
                SetBorderWidth(btn, 2f);
                SetBorderColor(btn, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
                btn.style.backgroundColor = ElarionUi.PanelStone;
                btn.style.color = ElarionUi.Parchment;
            }
            else
            {
                // Primary gold CTA — dark ink on runic gold (town-HUD button).
                SetBorderWidth(btn, 1f);
                SetBorderColor(btn, new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.9f));
                btn.style.backgroundColor = ElarionUi.GoldButton;
                btn.style.color = ElarionUi.Ink;
            }
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the hero-select scene.
// -----------------------------------------------------------------------------
//   1. The HeroSelect scene is generated by DeNelle.Editor.IntroFlowSceneBuilder
//      (menu: Defenders/Intro Flow/Build Hero + Pet Select Scenes). It creates a
//      Camera, an EventSystem, and a UIDocument GameObject with this controller
//      attached. The UIDocument MAY still carry HeroSelectScreen.uxml — that is
//      now harmless: BuildScreen() clears the root and builds the entire layout
//      in code, so whether or not the UXML renders, the screen is identical. This
//      is the regression fix: there is no longer any UXML element this screen
//      depends on, so a UXML that doesn't render in the WebGL build (CLAUDE.md §8)
//      cannot break it.
//
//   2. The intro/story cinematic (StoryIntroController) lives in the TITLE scene,
//      not here. The transition into hero-select is a single-scene
//      SceneManager.LoadScene (SceneRouter.GoHeroSelect), so Unity destroys the
//      Title scene — and the cold-open overlay with it — before this scene's
//      OnEnable runs. There is therefore zero possibility of the intro rendering
//      on top of hero-select: they never coexist in the same scene. (Title-side,
//      StoryIntroController.HideImmediate already fully tears its overlay down.)
//
//   3. GameStateService must exist before this screen so ChooseHero persists.
//      It is a DontDestroyOnLoad singleton from the Core bootstrap in Title. If a
//      session enters HeroSelect cold, the controller logs a warning and still
//      routes on (the choice just is not saved).
// =============================================================================
