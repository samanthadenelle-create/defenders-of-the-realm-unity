// =============================================================================
// VillageHudController — SLEEK / MINIMAL / CONTEXT-AWARE combat HUD (code uGUI).
// -----------------------------------------------------------------------------
// WO-334 REDO. The previous ornate parchment/stone reskin was hated AND didn't
// display in the WebGL build. This is the replacement, to owner spec:
//
//   SLEEK · MINIMAL · RESPONSIVE · CONTEXT-AWARE · skinned to feel like Elarion.
//
//   • SLEEK/MINIMAL  — flat dark-glass bars, thin chrome, one hairline gold
//                      accent line per cluster (a HINT of fantasy via HudTheme,
//                      not heavy stone frames). Maximised play area.
//   • RESPONSIVE     — CanvasScaler ScaleWithScreenSize, match ~0.5, anchored
//                      clusters that reflow for portrait (mobile) AND landscape
//                      (web/tablet). Safe-area aware (notch inset applied).
//   • CONTEXT-AWARE  — the Build button, Defend button, Castle/Heart HP bar and
//                      build entry ONLY show in the VILLAGE. Out in the open
//                      world (hero past the town ring / OuterWorld) they hide,
//                      leaving the clean essentials: hero HP/mana, ability bar,
//                      party + compass (compass is a separate component).
//   • MOBILE THUMBS  — the LEFT thumb drives MOVEMENT (the VirtualJoystick lives
//                      bottom-LEFT), so the ABILITY cluster lives bottom-RIGHT
//                      (right thumb hits skills) as a compact 2×2 grid, and the
//                      hero HP/mana float bottom-LEFT ABOVE the joystick. The
//                      bottom-left joystick zone is kept clear in both orientations.
//   • WO-334 FIX     — the whole UI build is wrapped in try/catch so a single
//                      element throwing (WebGL ExplicitlyThrownExceptionsOnly,
//                      a null sprite/font) can NEVER blank the HUD or halt the
//                      player. Procedural sprites are failure-safe in HudTheme.
//
// Context detection: Village2 stays the ACTIVE scene while OuterWorld loads
// ADDITIVELY over it (WorldSceneLoader) — the hero physically walks out. So
// "in the village" = the active scene is Village2 AND the hero is within the
// town ring (~TownRadius of the Heart at origin). Past that = open world.
// Mirrors GromOuterWorldReturnJoin's radial model. Hero resolved by reflection
// (DeNelle.Village.HeroLocomotion) like CompassHudBootstrap — keeps HUD→Core.
//
// EVERY public data-binding setter is preserved byte-for-byte in signature
// (SetHp/SetMana/SetWave/SetEnemyCount/SetComboCount/SetAbilitySlot/
// SetPartyMember/SetHeartHp/...). Restyle + re-layout + context gating only.
// Pure UnityEngine.UI + TMPro. No UXML. Passive IVillageHud. HUD→Core asmdef.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.HUD;
using DeNelle.Core.UI;   // shared ElarionUiKit — ONE visual language with the inventory
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace DeNelle.HUD
{
    public sealed class VillageHudController : MonoBehaviour, IVillageHud
    {
        public const int AbilitySlotCount = 4;
        private const int PartySlotCount = 4; // hero + up to 3 companions
        private const float PartyRowHeight = 112f;   // 200% (dark-stone player frame: portrait + HP/MP)
        private const float PartyRowGap = 7f;

        // ── Context-awareness constants ───────────────────────────────────────
        // The town footprint reaches ~45u; OuterWorld regions lie beyond ~70u
        // (see GromOuterWorldReturnJoin). We treat inside the ring as "village".
        private const string VillageSceneName = "Village2";
        private const float TownRadius = 60f;          // hero within = village context
        private const float TownRadiusHyst = 8f;       // hysteresis band (avoid flicker at the edge)
        private const float ContextPollInterval = 0.35f;

        [Header("Events (read by the Village-side bridges via reflection)")]
        public UnityEvent BuildRequested = new UnityEvent();
        public UnityEvent SkillsRequested = new UnityEvent();
        public UnityEvent ShopRequested = new UnityEvent();
        // Context-gated Talk affordance. Raised when the player taps Talk; a Village-side
        // bridge (TalkHudBridge) reflects this event + drives SetTalkAvailable from the
        // talkable-NPC-in-range registry. Kept OFF IVillageHud (Core) on purpose — least
        // cross-level exposure: only HUD + the Village bridge know "Talk" exists.
        public UnityEvent TalkRequested = new UnityEvent();
        // TOWN ACTIONS row — least-exposed: the HUD raises these; Village-side bridges open the
        // actual panels (HUD → Core/event only; never references HeroInventoryController/quests).
        public UnityEvent InventoryRequested = new UnityEvent();   // BAG → HeroInventoryController (Village bridge)
        public UnityEvent QuestsRequested = new UnityEvent();      // QUESTS → quest modal (follow-up; dimmed for now)
        public UnityEvent IntelRequested = new UnityEvent();       // far top-right (periscope) → enemy scout report / lookout
        public AbilitySlotEvent AbilityRequested = new AbilitySlotEvent();
        public UnityEvent RepairConfirmRequested = new UnityEvent();
        public UnityEvent RepairCancelRequested = new UnityEvent();
        public UnityEvent StartWaveRequested = new UnityEvent();

        [System.Serializable] public sealed class AbilitySlotEvent : UnityEvent<int> { }

        // ── Canvas + cached widgets ──────────────────────────────────────────
        private Canvas _hudCanvas;
        private CanvasScaler _scaler;
        private RectTransform _safeArea;   // safe-area inset root (notch/rounded corners)

        private TextMeshProUGUI[] _resourceTexts; // 0 Wood, 1 Iron, 2 Crystal, 3 Gold

        private TextMeshProUGUI _waveText;
        private TextMeshProUGUI _waveStateText;
        private TextMeshProUGUI _enemyCountText;
        private int _lastWaveNumber = 1;
        private string _lastWaveState = "Defend";

        // Combo / kill-streak momentum badge
        private RectTransform _momentumBadge;
        private TextMeshProUGUI _comboText;
        private TextMeshProUGUI _streakText;
        private CanvasGroup _momentumGroup;
        private int _lastCombo, _lastStreak;
        private float _momentumPop;
        private float _momentumHold;

        // Castle (Heart) HP — top-centre (VILLAGE-ONLY).
        private Image _castleFill;
        private TextMeshProUGUI _castleText;

        // Hero vitals (in the bottom skill bar)
        private Image _hpFill;
        private TextMeshProUGUI _hpText;
        private Image _manaFill;
        private TextMeshProUGUI _manaText;
        private float _hpCurrent, _hpMax = 1f;

        // Hero XP — a thin yellow line ABOVE the HP bar, in the same vitals panel
        // (owner: no full-screen XP bar; show progress visually next to health).
        // Driven by polling HeroProgression via reflection (HUD→Core; no Village ref).
        private Image _xpLineFill;          // the yellow fill (width = XP fraction)
        private float _xpFraction;          // 0..1 toward next level
        private object _heroProg;           // cached HeroProgression instance (reflection)
        private System.Type _heroProgType;
        private System.Reflection.PropertyInfo _xpProp;      // HeroProgression.Xp (float)
        private System.Reflection.PropertyInfo _xpToNextProp; // HeroProgression.XpToNext (float)
        private float _xpPollTimer;
        private const float XpPollInterval = 0.25f;

        // ── Wave-timer fallback poll (HUD→Core; WaveManager is in DeNelle.Village) ─
        // The town countdown is primarily fed by SetCountdown (WaveManager.OnCountdownTick,
        // pushed via a Village-side bridge). To guarantee a LIVE timer even when no bridge
        // pushes (and a clean source of truth), the HUD also polls WaveManager via reflection
        // for CountdownRemaining + Phase (same HUD→Core-safe pattern as the hero XP poll).
        // The poll only WRITES the timer when the manager is actively counting down; it never
        // overrides the SetCountdown-driven combat/clear states.
        private object _waveMgr;                              // cached WaveManager instance (reflection)
        private System.Type _waveMgrType;
        private System.Reflection.PropertyInfo _waveCountdownProp; // WaveManager.CountdownRemaining (float)
        private System.Reflection.PropertyInfo _wavePhaseProp;     // WaveManager.Phase (enum; 1 == Countdown)
        private System.Reflection.PropertyInfo _waveCurIdProp;     // WaveManager.CurrentWaveId (int)
        private float _wavePollTimer;
        private const float WavePollInterval = 0.2f;

        // Skill bar cells
        private TextMeshProUGUI[] _slotKey;
        private TextMeshProUGUI[] _slotGlyph;
        private TextMeshProUGUI[] _slotName;
        private Image[] _slotAccent;
        private Image[] _slotIcon;     // real ability art (by hero class + slot), replaces the glyph
        private Image[] _slotCooldown;
        private float[] _slotCdFill;

        // Party frames (slot 0 = hero, 1..3 = companions) — dark-stone player frame:
        // portrait (class) + red HP + blue MP.
        private GameObject[] _partyFrame;
        private Image[] _partyHpFill;
        private Image[] _partyMpFill;
        private Image[] _partyPortrait;
        private TextMeshProUGUI[] _partyName;
        private TextMeshProUGUI[] _partyHpText;

        // Repair prompt (transient, village-only by nature)
        private GameObject _repairPanel;
        private TextMeshProUGUI _repairLabel;

        private CanvasGroup _rootGroup;

        // ── WO-337: BATTLE-HUD group ──────────────────────────────────────────
        // The combat-only clusters (abilities, hero vitals, wave/enemy readout,
        // momentum badge) live under their OWN canvas + CanvasGroup at a higher
        // sortingOrder so BattleHudVisibilityManager can fade the whole battle HUD
        // in/out (active combat only) WITHOUT touching the IDLE/village UI
        // (resource strip, castle/Heart HP banner, build button) which stays on
        // the base HUD canvas. Exposed read-only for the visibility manager.
        private Canvas _battleCanvas;
        private CanvasGroup _battleHudGroup;
        public CanvasGroup BattleHudGroup => _battleHudGroup;

        // ── WO-339: TOWN-HUD group ────────────────────────────────────────────
        // The idle-village TOWN HUD (wave-management cluster top-left, resource
        // badges top-centre, lightweight 2D mini-map top-right, town-metrics
        // bottom strip) lives under its OWN canvas + CanvasGroup so the new
        // HudModeManager can cross-fade TOWN ⇄ BATTLE ⇄ hidden WITHOUT disturbing
        // the BATTLE-HUD group (abilities/vitals) or the always-on base chrome.
        // Exposed read-only for the mode manager (same contract as BattleHudGroup).
        private Canvas _townCanvas;
        private CanvasGroup _townHudGroup;
        public CanvasGroup TownHudGroup => _townHudGroup;

        /// <summary>
        /// WO-339: the live village/world context (Village2 active + hero inside the
        /// town ring). Shared with BattleHudVisibilityManager so the TOWN↔hidden mode
        /// reuses ONE context evaluation instead of duplicating the radial/scene test.
        /// </summary>
        public bool InVillage => _inVillage || _villageOnlyForced;

        // ── Responsive layout cluster roots ──────────────────────────────────
        private RectTransform _resourceStrip;
        private RectTransform _waveReadout;
        private RectTransform _castleBanner;
        private RectTransform _partyStack;
        private RectTransform _skillBar;      // bottom-RIGHT ability cluster (right thumb)
        private RectTransform _vitalsCluster; // bottom-LEFT-ABOVE joystick: hero HP + mana
        private RectTransform _buildBtn;
        private RectTransform _startWaveBtn;
        private bool _startWaveAvailable;
        private bool _isPortrait = true;
        private int _lastScreenW, _lastScreenH;

        private bool _combatHudVisible = true;
        private bool _built;

        // ── WO-339: TOWN-HUD widgets ──────────────────────────────────────────
        // Top-left WAVE MANAGEMENT cluster.
        private RectTransform _townWaveCluster;
        private TextMeshProUGUI _townTimerText;      // MM:SS to next wave (colour by urgency)
        private TextMeshProUGUI _townWaveProgText;   // "Wave N / M"
        private Image _townWaveProgFill;             // progress bar
        private Image _townLookoutBadge;             // GREEN/YELLOW/RED/PURPLE status pip
        private TextMeshProUGUI _townLookoutText;
        // Lookout BELL — a bell glyph beside the timer that pulses/highlights when a
        // wave is imminent (lookout INCOMING) or active (COMBAT). Bound to the existing
        // _lookoutStatus (driven by SetCountdown / SetWaveImminent / SetLookoutStatus),
        // so it shares ONE signal with the status pip — no new data binding.
        private TextMeshProUGUI _townBellGlyph;      // 🔔 alert glyph
        private RectTransform _townBell;             // bell rect (pulsed/scaled)
        private float _bellPulse;                    // 0..1 pulse phase driver
        private float _townTimerSeconds = -1f;       // last countdown (negative = none)
        private int _townWaveCur = 1, _townWaveMax = 0;
        private int _lookoutStatus;                  // 0 safe,1 alert,2 incoming,3 combat
        private bool _townWaveActive;                // wave currently in combat

        // Top-centre RESOURCE badges (icon + number) with +/- flash + low-warn outline.
        private RectTransform _townResStrip;
        private TextMeshProUGUI[] _townResText;      // 0 Gold,1 Wood,2 Crystal,3 Iron
        private Image[] _townResBadge;               // badge bg (for low-warn red outline / flash)
        private Image[] _townResOutline;             // red low-warning outline overlay
        private int[] _townResLast = { -1, -1, -1, -1 };
        private float[] _townResFlash = { 0f, 0f, 0f, 0f };
        private bool[] _townResFlashUp = { false, false, false, false };
        private const int TownResLowThreshold = 50;

        // Top-right LIGHTWEIGHT 2D mini-map (icon markers, no RenderTexture).
        private RectTransform _townMiniMap;
        private RectTransform _townMiniMapInner;     // square draw area markers live under
        private readonly System.Collections.Generic.List<MiniMapMarker> _miniMarkers
            = new System.Collections.Generic.List<MiniMapMarker>();
        private const float MiniMapWorldRadius = 120f; // world half-extent mapped to map edge

        // Bottom TOWN METRICS strip (3-col: Heart HP %, Towers built/max, Population).
        private RectTransform _townMetrics;
        private TextMeshProUGUI _townHeartText;
        private TextMeshProUGUI _townTowerText;
        private TextMeshProUGUI _townPopText;
        private float _townHeartPct = 1f;
        private int _townTowersBuilt, _townTowersMax, _townPopulation;

        // Passive-XP badge (WO-361): "⚡ Towers earning N XP/min". Compact, toggleable.
        private RectTransform _townPassiveXp;
        private TextMeshProUGUI _townPassiveXpText;
        private bool _townPassiveXpVisible = true;

        // A single mini-map marker (POI icon positioned by world→map projection).
        private sealed class MiniMapMarker
        {
            public RectTransform Rect;
            public Image Icon;
            public Transform WorldTarget; // optional live world anchor
            public Vector3 WorldPos;      // static fallback world position
            public bool IsHero;
        }

        // ── Context (village vs open world) ──────────────────────────────────
        private bool _inVillage = true;             // last evaluated context
        private bool _villageOnlyForced;            // a bridge can force village UI on
        private Transform _hero;
        private System.Type _heroType;
        private float _contextPollTimer;

        // =====================================================================
        //  LIGHT PALETTE (self-contained tone inversion — WO light-restyle)
        // ---------------------------------------------------------------------
        //  Owner north-star: LIGHT warm-PARCHMENT panels (NOT dark glass), DARK
        //  ink text, THIN glowing gilt/rune borders, soft low-opacity gold glow,
        //  airy/ethereal. This is a SKIN, not a relayout — every position, data
        //  binding and update path is byte-for-byte unchanged. We keep these
        //  colours LOCAL (do NOT mutate the shared ElarionUiKit/HudTheme tokens)
        //  so the other not-yet-restyled screens keep their current look.
        //
        //  READABILITY (the #1 risk of a tone inversion): the kit's body-text
        //  token (HudTheme.Text) is CREAM — invisible on light parchment. So we
        //  drive ALL text off LInk/LInkDim (dark) instead, and flip the legacy
        //  black drop-shadow outlines (built for light-text-on-dark) to a faint
        //  PARCHMENT glow (LGlow) — dark ink on warm parchment needs a soft light
        //  halo, never a black one. Gilt/Gold accents + the gold CTA buttons keep
        //  their existing ink-on-gold contrast (already correct on a light bg).
        // =====================================================================

        // Panel fills — light warm parchment, low opacity for the airy/ethereal feel.
        private static readonly Color LParch     = new Color(0.929f, 0.902f, 0.839f, 0.93f); // #EDE6D6 main panel
        private static readonly Color LParchDeep = new Color(0.910f, 0.875f, 0.784f, 0.96f); // #E8DFC8 heavier tray
        private static readonly Color LParchSoft = new Color(0.945f, 0.925f, 0.875f, 0.82f); // airy inset cell
        // Recessed bar track — a soft warm shadow on light (NOT near-black).
        private static readonly Color LWell      = new Color(0.40f, 0.34f, 0.24f, 0.30f);
        // Text — dark ink on the new light bg (ElarionUi.Ink = #231910 dark brown).
        private static readonly Color LInk        = new Color(0.137f, 0.098f, 0.055f, 1f); // primary dark ink
        private static readonly Color LInkDim      = new Color(0.314f, 0.255f, 0.176f, 1f); // muted ink (secondary)
        // Thin glowing gilt/rune rim + soft gold glow (the spec border treatment).
        private static readonly Color LGilt       = new Color(0.831f, 0.686f, 0.216f, 0.95f); // crisp gilt rune line
        private static readonly Color LGiltSoft    = new Color(0.831f, 0.686f, 0.216f, 0.42f); // soft glow underline
        // Faint parchment halo replacing the legacy black text outlines on light.
        private static readonly Color32 LGlow      = new Color32(255, 250, 238, 150);
        // Portrait / slot rune-frame fill on light.
        private static readonly Color LSlotFill    = new Color(0.882f, 0.847f, 0.769f, 0.95f);
        private static readonly Color LPortrait    = new Color(0.871f, 0.831f, 0.741f, 0.97f);

        /// <summary>
        /// LIGHT panel frame — warm parchment fill + a thin glowing gilt rune rim
        /// (crisp inner hairline) + a soft gold glow underline. The light-restyle
        /// analogue of the dark <see cref="FramePanel"/>: same three decorative,
        /// non-raycast children, so behaviour is unchanged — only the tone flips.
        /// </summary>
        private static void FramePanelLight(GameObject go, Color fill)
        {
            HudTheme.StylePanel(go, fill);
            ElarionUiKit.AddInnerRim(go, LGilt);   // thin glowing gilt rune line
            HudTheme.AddRim(go, LGiltSoft);        // soft low-opacity gold glow
        }

        /// <summary>Frame a thin HUD strip with the Tech-pack menu bar (hud_strip_bar);
        /// parchment fallback if the sprite is missing. 9-sliced so the ornate ends hold.</summary>
        private static void ApplyStripBar(GameObject go)
        {
            var sprite = WidgetSprite("hud_strip_bar");
            if (sprite == null) { FramePanelLight(go, LParch); return; }
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        /// <summary>LIGHT recessed bar track — soft warm shadow (not near-black).</summary>
        private static Image StyleWellLight(GameObject go)
        {
            var img = HudTheme.StyleWell(go);
            if (img != null) img.color = LWell;
            return img;
        }

        /// <summary>Re-tint a TMP label to dark ink on light + a faint parchment halo.</summary>
        private static TextMeshProUGUI Ink(TextMeshProUGUI t, bool dim = false)
        {
            if (t == null) return t;
            t.color = dim ? LInkDim : LInk;
            t.outlineColor = LGlow;
            t.outlineWidth = 0.06f;
            return t;
        }

        // =====================================================================
        //  HUD WIDGET ICONS (WO-403/404) — real artwork sprites, sprite-first.
        // ---------------------------------------------------------------------
        //  The widget sheet is sliced by DeNelle.Editor.HudIconSlicer + mirrored to
        //  Resources/HudIcons (WebGL-safe). We load every sub-sprite once via
        //  Resources.LoadAll<Sprite>("HudIcons/<sheet>") and index by sprite name —
        //  EXACTLY the ItemIconCatalog pattern (no AssetDatabase / StreamingAssets /
        //  File IO → safe on every target). Every consumer is sprite-FIRST with the
        //  existing code-drawn glyph kept as the fallback, so the HUD is correct
        //  whether or not the art is present (sheet missing → null → glyph shows).
        // =====================================================================
        private const string HudIconSheet = "HudIcons/hud_widgets_sheet";
        private static System.Collections.Generic.Dictionary<string, Sprite> _hudIcons;
        private static bool _hudIconsLoaded;

        // Widget sprite names — kept in lockstep with HudIconSlicer.WidgetNames.
        private const string IconTree         = "hud_tree";
        private const string IconCompass      = "hud_compass";
        private const string IconSettings     = "hud_settings";
        private const string IconInventory    = "hud_inventory";
        private const string IconTalk         = "hud_talk";
        private const string IconQuest        = "hud_quest";
        private const string IconBuild        = "hud_build";    // standalone Resources/HudIcons/hud_build (tower)
        private const string IconIntel        = "hud_intel";    // standalone Resources/HudIcons/hud_intel (periscope/lookout)
        private const string IconHeart        = "hud_heart";    // standalone Resources/HudIcons/hud_heart (Heart-of-Elarion crest)
        private const string IconAbilityFrame = "hud_ability_frame";

        /// <summary>Widget sprite by name, or null (caller keeps its glyph fallback).</summary>
        // SPRITE-FIRST priority: the owner's polished RPG UI pack (RpgUiCatalog) wins,
        // then the legacy HudIcons sheet, then null → the caller's code-drawn glyph. The
        // pack lookup maps each HUD widget name to the matching bronze RPG icon role so
        // Settings/Inventory/Talk/Quest/Tree/Compass come up as real artwork when the
        // pack is imported. All paths are WebGL-safe (Resources only) and null-safe.
        private static Sprite WidgetSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // Custom standalone icons (Resources/HudIcons/<name>) WIN over the pack — themed art the
            // owner drops in (hud_build tower, hud_intel periscope, hud_compass, …). Single Sprites.
            var custom = Resources.Load<Sprite>("HudIcons/" + name);
            if (custom != null) return custom;

            var packed = RpgIconForWidget(name);
            if (packed != null) return packed;

            EnsureHudIconsLoaded();
            if (_hudIcons == null) return null;
            return _hudIcons.TryGetValue(name, out var s) ? s : null;
        }

        // Map a HUD widget name to the RpgUiCatalog "icons" sprite (or null when the
        // pack isn't imported / has no matching icon → fall through to HudIcons/glyph).
        private static Sprite RpgIconForWidget(string name)
        {
            switch (name)
            {
                case IconSettings:  return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSettings);
                case IconInventory: return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconInventory);
                case IconTalk:      return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconTalk);
                case IconQuest:     return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconQuest);
                case IconTree:      return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconTree);
                case IconCompass:   return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconCompass);
                default:            return null;
            }
        }

        private static void EnsureHudIconsLoaded()
        {
            if (_hudIconsLoaded) return;
            _hudIconsLoaded = true;
            _hudIcons = new System.Collections.Generic.Dictionary<string, Sprite>(
                System.StringComparer.OrdinalIgnoreCase);
            Sprite[] subs = null;
            try { subs = Resources.LoadAll<Sprite>(HudIconSheet); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[VillageHudController] HUD-icon load failed for " + HudIconSheet + ": " + e.Message);
            }
            if (subs != null)
                for (int i = 0; i < subs.Length; i++)
                {
                    var sp = subs[i];
                    if (sp != null && !string.IsNullOrEmpty(sp.name)) _hudIcons[sp.name] = sp;
                }
            if (_hudIcons.Count == 0)
                Debug.Log("[VillageHudController] no HUD-widget sprites under Resources/HudIcons — " +
                          "run Defenders/Art/Slice HUD Icons. Falling back to code-drawn glyphs.");
        }

        /// <summary>
        /// Try to apply a widget sprite to an Image (sprite-FIRST). Returns true when
        /// the art was found + applied (the caller then hides/keeps its glyph), false
        /// when missing (caller keeps the code-drawn fallback visible). Tints to white
        /// so the artwork shows in its own colours; non-raycast.
        /// </summary>
        private static bool TrySetWidget(Image img, string name)
        {
            if (img == null) return false;
            var sp = WidgetSprite(name);
            if (sp == null) return false;
            img.sprite = sp;
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return true;
        }

        // =====================================================================
        //  PACK BAR DRESSING (WO RPG-UI) — ornate gold-frame bar skin, sprite-FIRST.
        // ---------------------------------------------------------------------
        //  The RPG pack ships matched bar pairs: a gilded frame "bg" (gem socket +
        //  pointed ornate ends) and a glossy colored "fill". We dress an EXISTING
        //  bar (a track rect + its Image.Type.Filled fill) without changing any data
        //  binding: the fill keeps its fillAmount-driven width, we just swap its
        //  sprite to the pack's colored fill, and we drop the gilded frame over the
        //  track as a NON-RAYCAST overlay so the bar reads as the ornate art. When the
        //  pack is absent both lookups return null → the procedural look is untouched.
        // =====================================================================

        /// <summary>
        /// Dress an existing bar (track + its fill Image) with the pack's ornate frame +
        /// colored fill, sprite-FIRST. `frameName`/`fillName` are RpgUiCatalog "bars"
        /// sprite ids; `fillTint` re-tints the pack fill (e.g. blue for MP). No-op when
        /// the pack is missing (procedural look preserved). Returns true when dressed.
        /// </summary>
        private static bool TryDressBar(RectTransform track, Image fill, string frameName,
            string fillName, Color fillTint, bool tintFill)
        {
            if (track == null) return false;
            var frameSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, frameName);
            var fillSprite  = string.IsNullOrEmpty(fillName)
                ? null : RpgUiCatalog.Get(RpgUiCatalog.RoleBars, fillName);
            if (frameSprite == null && fillSprite == null) return false;

            // Swap the fill sprite (keeps Image.Type.Filled + fillAmount binding).
            if (fill != null && fillSprite != null)
            {
                fill.sprite = fillSprite;
                if (tintFill) fill.color = fillTint;
                else fill.color = Color.white; // show the pack art's own colours
            }

            // Drop the gilded frame over the track as a decorative, non-raycast overlay,
            // rendered LAST so it sits above the fill (the frame art is hollow in the
            // middle, so the fill shows through). preserveAspect off → it stretches to
            // the existing bar rect (the bar layout is unchanged).
            if (frameSprite != null)
            {
                var fr = NewRect("PackFrame", track, Vector2.zero, Vector2.one);
                fr.offsetMin = new Vector2(-10f, -8f);
                fr.offsetMax = new Vector2(10f, 8f); // ornate ends slightly overhang the track
                var fimg = fr.gameObject.AddComponent<Image>();
                fimg.sprite = frameSprite;
                fimg.type = Image.Type.Simple;
                fimg.color = Color.white;
                fimg.raycastTarget = false;
                fr.SetAsLastSibling();
            }
            return true;
        }

        /// <summary>
        /// Build a widget ICON cell that is sprite-FIRST: an Image that shows the named
        /// widget artwork when present, otherwise a code-drawn glyph label (kept as the
        /// fallback). Returns the host rect so callers can position/parent it. The glyph
        /// label is hidden when the sprite resolves so the two never overlap.
        /// </summary>
        private static RectTransform AddWidgetIcon(string objName, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, string widgetName, string glyphFallback,
            float glyphSize, Color glyphColor)
        {
            var rt = NewRect(objName, parent, anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            bool hasArt = TrySetWidget(img, widgetName);
            if (!hasArt)
            {
                // No artwork — make the Image inert and show the code-drawn glyph.
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = false;
                var t = AddText(rt, glyphFallback, glyphSize, glyphColor, TextAlignmentOptions.Center);
                t.outlineColor = LGlow;
                t.outlineWidth = 0.06f;
            }
            return rt;
        }

        private void Awake()
        {
            CoreServices.RegisterHud(this);
        }

        private void OnDestroy()
        {
            CoreServices.UnregisterHud(this);
        }

        private void Start()
        {
            // WO-334: never let a build-time exception blank the HUD or halt the
            // player. If Build throws, log it and keep whatever was constructed.
            try
            {
                Build();
                ApplyResponsiveLayout(force: true);
                ApplyContext(force: true);
                Debug.Log("[VillageHudController] WO-334 sleek/minimal context-aware HUD active.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[VillageHudController] HUD build failed (HUD may be partial): " + e);
            }
        }

        private void Update()
        {
            if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            {
                ApplySafeArea();
                ApplyResponsiveLayout(force: false);
            }

            // Cheap context poll (not every frame): village vs open world.
            _contextPollTimer -= Time.unscaledDeltaTime;
            if (_contextPollTimer <= 0f)
            {
                _contextPollTimer = ContextPollInterval;
                ApplyContext(force: false);
            }

            if (Input.GetKeyDown(KeyCode.H))
                SetCombatHudVisible(!_combatHudVisible);

            AnimateMomentumBadge();
            AnimateLookoutBell();
            UpdateTownHud();
            UpdateHeroXpLine();
        }

        // ── Hero XP yellow line (above the HP bar) — cheap poll of HeroProgression. ─
        // HUD cannot reference DeNelle.Village, so the level/XP source is read via
        // reflection (same pattern as ResolveHeroIfNeeded / XPBarController). The fill
        // width = current XP / XP-to-next-level (0..1), lerped for a smooth slide.
        private void UpdateHeroXpLine()
        {
            if (_xpLineFill == null) return;

            _xpPollTimer -= Time.unscaledDeltaTime;
            if (_xpPollTimer <= 0f)
            {
                _xpPollTimer = XpPollInterval;
                ResolveHeroProgIfNeeded();
                if (_heroProg != null && _xpProp != null && _xpToNextProp != null)
                {
                    try
                    {
                        float xp     = (float)_xpProp.GetValue(_heroProg);
                        float toNext = (float)_xpToNextProp.GetValue(_heroProg);
                        _xpFraction = toNext > 0f ? Mathf.Clamp01(xp / toNext) : 0f;
                    }
                    catch { /* hero swapped out mid-poll — re-resolve next tick */ _heroProg = null; }
                }
            }

            // Smooth slide toward the target fraction.
            float cur = _xpLineFill.fillAmount;
            if (!Mathf.Approximately(cur, _xpFraction))
                _xpLineFill.fillAmount = Mathf.Lerp(cur, _xpFraction, Time.unscaledDeltaTime * 6f);
        }

        private void ResolveHeroProgIfNeeded()
        {
            if (_heroProg != null) return;
            if (_heroProgType == null)
                _heroProgType = System.Type.GetType("DeNelle.Village.HeroProgression, DeNelle.Village");
            if (_heroProgType == null) return;

            // HeroProgression.Instance is the canonical single hero per run.
            var instProp = _heroProgType.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            object prog = instProp != null ? instProp.GetValue(null) : null;
            if (prog == null)
                prog = UnityEngine.Object.FindObjectOfType(_heroProgType);
            if (prog == null) return;

            _heroProg     = prog;
            _xpProp       = _heroProgType.GetProperty("Xp");
            _xpToNextProp = _heroProgType.GetProperty("XpToNext");
        }

        // ── Wave-timer fallback poll — keep the town countdown LIVE from WaveManager. ─
        // Reflection (HUD cannot reference DeNelle.Village). Only writes the timer while
        // the manager is in the Countdown phase; combat/clear states stay owned by the
        // SetCountdown/SetWaveImminent push path so we never fight the bridge.
        private void PollWaveTimer()
        {
            _wavePollTimer -= Time.unscaledDeltaTime;
            if (_wavePollTimer > 0f) return;
            _wavePollTimer = WavePollInterval;

            ResolveWaveMgrIfNeeded();
            if (_waveMgr == null || _wavePhaseProp == null || _waveCountdownProp == null) return;

            try
            {
                // WavePhase.Countdown == 1 (see DeNelle.Village.WavePhase). Compare by int
                // so we don't need the Village enum type at HUD compile time.
                int phase = System.Convert.ToInt32(_wavePhaseProp.GetValue(_waveMgr));
                if (phase == 1) // Countdown
                {
                    float remaining = (float)_waveCountdownProp.GetValue(_waveMgr);
                    _townTimerSeconds = remaining;
                    _townWaveActive = false;
                    if (_waveCurIdProp != null)
                    {
                        int cur = (int)_waveCurIdProp.GetValue(_waveMgr);
                        if (cur > 0 && cur != _townWaveCur) { _townWaveCur = cur; RefreshTownWaveProgress(); }
                    }
                }
            }
            catch { _waveMgr = null; /* manager torn down across a reload — re-resolve next tick */ }
        }

        private void ResolveWaveMgrIfNeeded()
        {
            if (_waveMgr != null) return;
            if (_waveMgrType == null)
                _waveMgrType = System.Type.GetType("DeNelle.Village.WaveManager, DeNelle.Village");
            if (_waveMgrType == null) return;
            var inst = UnityEngine.Object.FindObjectOfType(_waveMgrType);
            if (inst == null) return;
            _waveMgr = inst;
            _waveCountdownProp = _waveMgrType.GetProperty("CountdownRemaining");
            _wavePhaseProp     = _waveMgrType.GetProperty("Phase");
            _waveCurIdProp     = _waveMgrType.GetProperty("CurrentWaveId");
        }

        // ── WO-339: per-frame TOWN-HUD animation (timer urgency, res flash, map). ─
        private void UpdateTownHud()
        {
            float dt = Time.unscaledDeltaTime;

            // Keep the countdown sourced live from WaveManager (fallback to the
            // SetCountdown push path when no manager is present).
            PollWaveTimer();

            // Countdown timer text + urgency colour (only when a wave is pending).
            if (_townTimerText != null)
            {
                if (_townWaveActive)
                {
                    _townTimerText.text = "IN WAVE";
                    _townTimerText.color = HudTheme.LookoutCombat;
                }
                else if (_townTimerSeconds >= 0f)
                {
                    int total = Mathf.Max(0, Mathf.CeilToInt(_townTimerSeconds));
                    _townTimerText.text = string.Format("{0:00}:{1:00}", total / 60, total % 60);   // MM:SS only — the clock face has the NEXT WAVE etch
                    _townTimerText.color = _townTimerSeconds < 10f ? HudTheme.LookoutIncoming
                        : _townTimerSeconds < 30f ? HudTheme.LookoutAlert : LInk;
                }
                else
                {
                    _townTimerText.text = string.Empty;   // idle: clock alone, no center word (owner)
                }
            }

            // Resource +/- flash fade.
            if (_townResBadge != null)
            {
                for (int i = 0; i < _townResBadge.Length; i++)
                {
                    if (_townResFlash[i] <= 0f || _townResBadge[i] == null) continue;
                    _townResFlash[i] = Mathf.Max(0f, _townResFlash[i] - dt * 2.2f);
                    Color flash = _townResFlashUp[i] ? HudTheme.LookoutSafe : HudTheme.HpRed;
                    // LIGHT: rest baseline is the light parchment badge, not dark glass.
                    _townResBadge[i].color = Color.Lerp(LParch, flash, _townResFlash[i] * 0.6f);
                }
            }

            // Mini-map marker projection (world → map). Hero marker tracks the hero.
            ProjectMiniMap();

            // WO-339: the TOWN HUD supersedes the legacy top resource strip + castle
            // banner whenever it's visible (avoids a double resource bar / HP banner).
            // When the town group fades out (BATTLE / exploration) the legacy strip +
            // banner return so combat still shows resources + Heart HP as before.
            bool townShown = _townHudGroup != null && _townHudGroup.alpha > 0.5f;
            SetActiveSafe(_resourceStrip, !townShown);
            // Castle banner is ALSO village-context gated (ApplyContext); only override
            // it OFF while the town HUD owns the readout, never force it on outside.
            if (townShown) SetActiveSafe(_castleBanner, false);
            else if (_inVillage || _villageOnlyForced) SetActiveSafe(_castleBanner, true);
        }

        private void ProjectMiniMap()
        {
            if (_townMiniMapInner == null || _miniMarkers.Count == 0) return;
            ResolveHeroIfNeeded();
            float half = _townMiniMapInner.rect.width * 0.5f;
            if (half <= 0f) half = 60f;
            for (int i = 0; i < _miniMarkers.Count; i++)
            {
                var m = _miniMarkers[i];
                if (m == null || m.Rect == null) continue;
                Vector3 world = m.IsHero
                    ? (_hero != null ? _hero.position : m.WorldPos)
                    : (m.WorldTarget != null ? m.WorldTarget.position : m.WorldPos);
                // flat XZ projection centred on the Heart (world origin).
                float nx = Mathf.Clamp(world.x / MiniMapWorldRadius, -1f, 1f);
                float nz = Mathf.Clamp(world.z / MiniMapWorldRadius, -1f, 1f);
                m.Rect.anchoredPosition = new Vector2(nx * half, nz * half);
            }
        }

        // Pan the main camera toward a world point (best-effort, asmdef-safe).
        private void PanCameraToward(Vector3 worldPoint)
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Slide the camera rig parent (or the camera) horizontally over the point,
            // keeping its current height + pitch. Best-effort: no Village dependency.
            Transform rig = cam.transform.parent != null ? cam.transform.parent : cam.transform;
            Vector3 p = rig.position;
            rig.position = new Vector3(worldPoint.x, p.y, worldPoint.z - 6f);
        }

        // ── Lookout bell — pulse + highlight when a wave is imminent/active. ──────
        // Bound to _lookoutStatus (the lookout signal driven by SetCountdown /
        // SetWaveImminent / SetLookoutStatus). status 2 = INCOMING (<30s) → amber
        // pulse; status 3 = COMBAT → red pulse; otherwise the bell rests dim + still.
        private void AnimateLookoutBell()
        {
            if (_townBell == null || _townBellGlyph == null) return;
            bool ringing = _lookoutStatus >= 2;
            if (ringing)
            {
                // advance the pulse phase (loops); ping-pong gives a clean swing.
                _bellPulse += Time.unscaledDeltaTime * (_lookoutStatus >= 3 ? 6f : 4f);
                float swing = Mathf.PingPong(_bellPulse, 1f);
                float scale = 1f + 0.22f * swing;
                _townBell.localScale = new Vector3(scale, scale, 1f);
                // a slight rock so it reads as a ringing bell, not just a throb.
                _townBell.localRotation = Quaternion.Euler(0f, 0f, (swing - 0.5f) * 22f);
                Color hot = _lookoutStatus >= 3 ? HudTheme.LookoutCombat : HudTheme.LookoutIncoming;
                _townBellGlyph.color = Color.Lerp(HudTheme.Gold, hot, swing);
            }
            else if (_townBell.localScale.x != 1f || _bellPulse != 0f)
            {
                // settle back to rest.
                _bellPulse = 0f;
                _townBell.localScale = Vector3.one;
                _townBell.localRotation = Quaternion.identity;
                _townBellGlyph.color = LInkDim;
            }
        }

        private void AnimateMomentumBadge()
        {
            if (_momentumBadge == null || _momentumGroup == null) return;
            float dt = Time.unscaledDeltaTime;
            if (_momentumPop > 0f)
                _momentumPop = Mathf.Max(0f, _momentumPop - dt * 6f);
            float scale = 1f + 0.30f * _momentumPop;
            _momentumBadge.localScale = new Vector3(scale, scale, 1f);
            if (_momentumHold > 0f)
            {
                _momentumHold -= dt;
                _momentumGroup.alpha = Mathf.MoveTowards(_momentumGroup.alpha, 1f, dt * 8f);
            }
            else if (_momentumGroup.alpha > 0f)
            {
                _momentumGroup.alpha = Mathf.MoveTowards(_momentumGroup.alpha, 0f, dt * 2.2f);
            }
        }

        // =====================================================================
        //  BUILD
        // =====================================================================
        private void Build()
        {
            if (_built) return;
            _built = true;

            var go = new GameObject("VillageHUD");
            go.transform.SetParent(transform, false);
            _hudCanvas = go.AddComponent<Canvas>();
            _hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hudCanvas.sortingOrder = 100;

            // RESPONSIVE: scale with screen, balanced width/height match so the
            // reference layout holds across mobile portrait + web landscape.
            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1080, 1920);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            _rootGroup = go.AddComponent<CanvasGroup>();

            // Safe-area root — all clusters live under this so notch/rounded
            // corners never clip the HUD on phones.
            _safeArea = NewRect("SafeArea", go.transform, Vector2.zero, Vector2.one);
            ApplySafeArea();

            // WO-337: dedicated BATTLE-HUD canvas (its own CanvasGroup, sortingOrder
            // ~150) layered over the base HUD. The combat clusters live UNDER this so
            // the visibility manager can fade the whole battle HUD without disturbing
            // the idle/village UI on the base canvas. Full-stretch inside the safe area.
            var battleGo = new GameObject("BattleHUD");
            battleGo.transform.SetParent(_safeArea, false);
            var battleRt = battleGo.AddComponent<RectTransform>();
            battleRt.anchorMin = Vector2.zero;
            battleRt.anchorMax = Vector2.one;
            battleRt.offsetMin = Vector2.zero;
            battleRt.offsetMax = Vector2.zero;
            _battleCanvas = battleGo.AddComponent<Canvas>();
            _battleCanvas.overrideSorting = true;
            _battleCanvas.sortingOrder = 150;
            battleGo.AddComponent<GraphicRaycaster>();
            _battleHudGroup = battleGo.AddComponent<CanvasGroup>();
            var battleRoot = battleRt;

            // WO-339: dedicated TOWN-HUD canvas (its own CanvasGroup, sortingOrder
            // ~140 — under the battle HUD so an active wave's combat chrome always
            // wins, over the base chrome). The HudModeManager cross-fades this group
            // against the battle group; the always-on base chrome stays untouched.
            var townGo = new GameObject("TownHUD");
            townGo.transform.SetParent(_safeArea, false);
            var townRt = townGo.AddComponent<RectTransform>();
            townRt.anchorMin = Vector2.zero;
            townRt.anchorMax = Vector2.one;
            townRt.offsetMin = Vector2.zero;
            townRt.offsetMax = Vector2.zero;
            _townCanvas = townGo.AddComponent<Canvas>();
            _townCanvas.overrideSorting = true;
            _townCanvas.sortingOrder = 140;
            townGo.AddComponent<GraphicRaycaster>();
            _townHudGroup = townGo.AddComponent<CanvasGroup>();
            var townRoot = townRt;

            // TOWN HUD — idle-village clusters (faded in/out by the HudModeManager).
            BuildTownWaveCluster(townRoot);
            BuildTownPassiveXp(townRoot);
            BuildTownResourceBadges(townRoot);
            // WO-380: minimap cut. In the compact castle hub everything is in-frame, so a
            // navigation minimap adds no value and was crowding the settings gear in the
            // top-right corner. Threat awareness ("enemies attacking") is already handled
            // in-world by StructureAttackAlert (red flash + bobbing "!" on hit buildings).
            // BuildTownMiniMap intentionally not called; _townMiniMap stays null and every
            // consumer (ProjectMiniMap, the mode-toggle reflow, markers) null-checks → inert.
            // BuildTownMiniMap(townRoot);
            BuildTownMetrics(townRoot);

            // WO-403/404: full-screen RUNIC BORDER frame — a light, thin gilt/rune
            // frame around the whole play area (the mockup's framed-parchment feel).
            // Always-on base chrome, non-raycast, behind every cluster.
            BuildRunicBorderFrame(_safeArea);

            // WO-403: top-centre ornate COMPASS + top-right Settings / Inventory icons.
            BuildTopChrome(_safeArea);

            // IDLE / village UI — base canvas (NEVER hidden by the battle-HUD gate).
            BuildResourceStrip(_safeArea);
            BuildCastleBanner(_safeArea);
            // BUILD now lives in the TOWN ACTIONS row (BuildTownActionPanel) — the separate pill is a
            // duplicate (WO-411). _buildBtn stays null; ApplyContext's SetActiveSafe is null-safe.
            // BuildBuildButton(_safeArea);
            BuildStartWaveButton(_safeArea);
            BuildRepairPrompt(_safeArea);
            BuildPartyFrames(_safeArea);

            // WO-403/404: RIGHT-edge action panel — Talk + Quest (town) buttons. The
            // combat Skills panel is the existing bottom-right skill bar (rune cells).
            BuildTownActionPanel(_safeArea);

            // BATTLE HUD — combat-only clusters (faded in/out by the visibility mgr).
            BuildWaveReadout(battleRoot);
            BuildMomentumBadge(battleRoot);
            BuildVitalsCluster(battleRoot);
            BuildSkillBar(battleRoot);
        }

        // ── WO-403/404 · Full-screen RUNIC BORDER frame ───────────────────────
        // A light, thin gilt/rune frame hugging the safe-area edges — four hairline
        // gilt bars (top/bottom/left/right) + a soft glow inset + small corner runes,
        // all NON-RAYCAST decorative children behind every cluster. Code-drawn so it
        // needs no art and is WebGL-safe; reads as the mockup's framed-parchment feel
        // without boxing in / darkening the play area.
        private RectTransform _runicFrame;
        private void BuildRunicBorderFrame(Transform parent)
        {
            _runicFrame = NewRect("RunicFrame", parent, Vector2.zero, Vector2.one);
            _runicFrame.SetAsFirstSibling(); // behind everything

            const float bar = 3f;     // crisp gilt hairline thickness
            const float glow = 7f;     // soft glow band just inside the hairline
            const float inset = 6f;    // pull off the very edge so it reads as a frame

            // Soft gold glow band (low alpha, slightly inside the hairline).
            AddFrameEdge("GlowTop",    new Vector2(0f, 1f), new Vector2(1f, 1f), inset, glow, true,  LGiltSoft);
            AddFrameEdge("GlowBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), inset, glow, true,  LGiltSoft);
            AddFrameEdge("GlowLeft",   new Vector2(0f, 0f), new Vector2(0f, 1f), inset, glow, false, LGiltSoft);
            AddFrameEdge("GlowRight",  new Vector2(1f, 0f), new Vector2(1f, 1f), inset, glow, false, LGiltSoft);

            // Crisp gilt hairline on the outer edge of the glow.
            AddFrameEdge("EdgeTop",    new Vector2(0f, 1f), new Vector2(1f, 1f), inset, bar, true,  LGilt);
            AddFrameEdge("EdgeBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), inset, bar, true,  LGilt);
            AddFrameEdge("EdgeLeft",   new Vector2(0f, 0f), new Vector2(0f, 1f), inset, bar, false, LGilt);
            AddFrameEdge("EdgeRight",  new Vector2(1f, 0f), new Vector2(1f, 1f), inset, bar, false, LGilt);

            // Small corner rune flourishes (decorative glyphs anchored to each corner).
            AddCornerRune("RuneTL", new Vector2(0f, 1f), new Vector2(14f, -14f));
            AddCornerRune("RuneTR", new Vector2(1f, 1f), new Vector2(-14f, -14f));
            AddCornerRune("RuneBL", new Vector2(0f, 0f), new Vector2(14f, 14f));
            AddCornerRune("RuneBR", new Vector2(1f, 0f), new Vector2(-14f, 14f));
        }

        // One edge bar of the runic frame. `horizontal` = a full-width top/bottom bar
        // (thickness in px); otherwise a full-height left/right bar. `inset` pulls it
        // off the screen edge. Decorative, non-raycast.
        private void AddFrameEdge(string name, Vector2 anchorMin, Vector2 anchorMax,
            float inset, float thickness, bool horizontal, Color color)
        {
            var rt = NewRect(name, _runicFrame, anchorMin, anchorMax);
            if (horizontal)
            {
                // stretch across X, fixed height; anchored to the top or bottom edge.
                bool top = anchorMin.y > 0.5f;
                rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
                rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
                rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
                rt.sizeDelta = new Vector2(-2f * inset, thickness);
                rt.anchoredPosition = new Vector2(0f, top ? -inset : inset);
            }
            else
            {
                bool right = anchorMin.x > 0.5f;
                rt.anchorMin = new Vector2(right ? 1f : 0f, 0f);
                rt.anchorMax = new Vector2(right ? 1f : 0f, 1f);
                rt.pivot = new Vector2(right ? 1f : 0f, 0.5f);
                rt.sizeDelta = new Vector2(thickness, -2f * inset);
                rt.anchoredPosition = new Vector2(right ? -inset : inset, 0f);
            }
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = HudTheme.RoundedFrame;
            img.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
        }

        private void AddCornerRune(string name, Vector2 corner, Vector2 offset)
        {
            var rt = NewRect(name, _runicFrame, corner, corner);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(22f, 22f);
            rt.anchoredPosition = offset;
            var t = AddText(rt, "*", 18, LGilt, TextAlignmentOptions.Center);
            t.raycastTarget = false;
        }

        // ── WO-403 · Top-centre COMPASS + top-right SETTINGS / INVENTORY icons. ─
        // The compass is a passive ornate widget (top-centre, above the resource
        // strip). Settings (gear) + Inventory (backpack) are tappable icons top-right.
        // Both icons raise the existing event hooks: Settings → ShopRequested (the HUD
        // already routes ShopRequested to the menu bridge); Inventory → BuildRequested
        // (loadout/build entry). All sprite-first with glyph fallback. By-name extras.
        private RectTransform _compassWidget;
        private void BuildTopChrome(Transform parent)
        {
            // Ornate compass — top-centre, small, above the resource strip. Passive.
            _compassWidget = AddWidgetIcon("Compass", parent,
                new Vector2(0.465f, 0.90f), new Vector2(0.535f, 0.955f),
                IconCompass, "*", 30, LGilt);

            // Top-right icon cluster — Settings gear + Inventory backpack.
            var cluster = NewRect("TopRightIcons", parent, new Vector2(1f, 1f), new Vector2(1f, 1f));
            cluster.anchorMin = new Vector2(1f, 1f);
            cluster.anchorMax = new Vector2(1f, 1f);
            cluster.pivot = new Vector2(1f, 1f);
            // Drop below the top resource strip band so the gear/backpack never
            // overlap the resources (battle) or the compass row (town).
            cluster.anchoredPosition = new Vector2(-55f, -55f);   // inset ≈ resource-bar height from top + right
            cluster.sizeDelta = new Vector2(280f, 135f);   // ~250% up — same ≈140px size as the TOWN ACTIONS row

            BuildIconButton(cluster, new Vector2(0f, 0f), new Vector2(0.48f, 1f),
                IconSettings, "*", () => HelpMenu.Instance?.ToggleOverlay());   // gear → Help/Settings menu (Report bug, Controls, Dev tools[dev], Credits)
            // Far top-right = enemy scout report / lookout (periscope icon — self-evident).
            BuildIconButton(cluster, new Vector2(0.52f, 0f), new Vector2(1f, 1f),
                IconIntel, "o", () => IntelRequested?.Invoke());
        }

        // A round rune-framed icon BUTTON: gilt ring seat + sprite-first widget icon
        // (glyph fallback) + a Button raising the given action.
        private Button BuildIconButton(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            string widgetName, string glyph, UnityEngine.Events.UnityAction action)
        {
            var cell = NewRect("Icon_" + widgetName, parent, anchorMin, anchorMax);
            // TRANSPARENT backing (owner: no yellow background) — the seat stays as the
            // invisible click target (raycast on a clear Image still receives taps); the
            // icon floats on the dark HUD. Seat + rim both clear.
            var seat = cell.gameObject.AddComponent<Image>();
            seat.color = new Color(0f, 0f, 0f, 0f);
            seat.sprite = HudTheme.Disc;
            var ring = NewRect("Ring", cell, Vector2.zero, Vector2.one);
            ring.offsetMin = new Vector2(-1.5f, -1.5f); ring.offsetMax = new Vector2(1.5f, 1.5f);
            var ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.color = new Color(0f, 0f, 0f, 0f);
            ringImg.sprite = HudTheme.Disc;
            ringImg.raycastTarget = false;
            ring.SetAsFirstSibling(); // behind the seat = rim

            // sprite-first widget icon (glyph fallback) inset inside the ring.
            AddWidgetIcon("Glyph", cell, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f),
                widgetName, glyph, 22, LInk);

            var btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = seat;
            HudTheme.StyleButtonColors(btn, LPortrait);
            if (action != null) btn.onClick.AddListener(action);
            return btn;
        }

        // ── WO-403/404 · RIGHT-edge TOWN action panel — Talk + Quest rune buttons. ─
        // Circular rune-framed buttons on the right edge (town mode). Talk →
        // ShopRequested (vendor/dialogue entry — DialogueService is the interaction
        // layer); Quest → BuildRequested (quest/board entry hook). VILLAGE-ONLY:
        // gated with the rest of the town chrome by ApplyContext. The COMBAT mode's
        // right-edge Skills panel is the existing bottom-right rune skill bar.
        private RectTransform _townActionPanel;
        private Button _talkButton;    // context-gated: only interactable when an NPC is in range
        private AttentionGlowUi _talkGlow;   // chasing-comet attention cue around the Talk button
        private Button _questButton;   // dimmed until the hub quest modal exists (follow-up)
        private void BuildTownActionPanel(Transform parent)
        {
            // TOWN ACTIONS row (mockup #42): BUILD · TALK · BAG · QUESTS, bottom-right edge.
            _townActionPanel = NewRect("TownActions", parent, new Vector2(1f, 0f), new Vector2(1f, 0f));
            _townActionPanel.anchorMin = new Vector2(1f, 0f);
            _townActionPanel.anchorMax = new Vector2(1f, 0f);
            _townActionPanel.pivot = new Vector2(1f, 0f);
            _townActionPanel.anchoredPosition = new Vector2(-20f, 20f);
            _townActionPanel.sizeDelta = new Vector2(300f, 300f);   // square footprint for a compact DIAMOND cluster (clears the metrics bar)

            // DIAMOND layout (mobile-thumb-friendly, bottom-right corner):
            // BUILD top · TALK left · BAG right · QUESTS bottom.
            BuildIconButton(_townActionPanel, new Vector2(0.30f, 0.56f), new Vector2(0.70f, 0.98f),
                IconBuild, "B", () => BuildRequested?.Invoke());
            _talkButton = BuildIconButton(_townActionPanel, new Vector2(0.02f, 0.29f), new Vector2(0.42f, 0.71f),
                IconTalk, "T", () => TalkRequested?.Invoke());
            BuildIconButton(_townActionPanel, new Vector2(0.58f, 0.29f), new Vector2(0.98f, 0.71f),
                IconInventory, "G", () => InventoryRequested?.Invoke());
            _questButton = BuildIconButton(_townActionPanel, new Vector2(0.30f, 0.02f), new Vector2(0.70f, 0.44f),
                IconQuest, "!", () => DailyQuestHud.Instance?.Toggle());

            // Reusable chasing-comet attention cue around the Talk button (also for tutorial focusing).
            _talkGlow = AttentionGlowUi.Attach((RectTransform)_talkButton.transform,
                new Color(1f, 0.85f, 0.35f, 1f), HudTheme.Disc);

            SetTalkAvailable(false);   // gated until a talkable NPC is in range
        }

        /// <summary>
        /// Enable/disable + brighten/dim the Talk button. Driven by the Village-side
        /// TalkHudBridge from the talkable-NPC-in-range registry. Presentation only — the
        /// HUD knows nothing about which NPC or its dialogue.
        /// </summary>
        public void SetTalkAvailable(bool available)
        {
            if (_talkButton == null) return;
            _talkButton.interactable = available;
            // Dim the WHOLE button via a CanvasGroup — NOT the targetGraphic's alpha.
            // The seat (targetGraphic) is intentionally a fully-transparent black disc
            // that only exists as the click/raycast target (owner: "no yellow/black
            // backing"); writing alpha 0.55 onto it painted a visible 55%-opacity BLACK
            // DISC under the Talk icon (the "black shade under talk" the owner flagged).
            // A CanvasGroup fades the icon/glyph for the idle state while the seat stays
            // clear, so dimming reads as a faded icon, never a dark backing plate.
            // CRITICAL (telemetry 2026-06-12): UnityEngine.Object must NOT use the C# ?? operator.
            // GetComponent returns a Unity "fake-null" that ?? does NOT treat as null, so the
            // AddComponent fallback never runs, cg stays the fake-null, and cg.alpha throws
            // MissingComponentException — which (called from BuildTownActionPanel during Build())
            // aborted the ENTIRE HUD build, shipping a PARTIAL HUD (broken talk/portrait/comet/BAG/
            // overlapping text). TryGetComponent returns a real bool and is the safe form.
            if (!_talkButton.TryGetComponent(out CanvasGroup cg))
                cg = _talkButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = available ? 1f : 0.55f;
            if (_talkGlow != null) _talkGlow.gameObject.SetActive(available);   // chasing comet only when a talkable NPC is in range
        }

        // ── Currency strip — thin glass bar, tiny colour dot + amount. ─────────
        private void BuildResourceStrip(Transform parent)
        {
            _resourceStrip = NewRect("ResourceStrip", parent, new Vector2(0.50f, 0.955f), new Vector2(1f, 1f));
            FramePanelLight(_resourceStrip.gameObject, LParch);

            string[] names  = { "Wood", "Iron", "Crystal", "Gold" };
            string[] glyphs = { "^", "+", "*", "o" };
            Color[] tints   = { HudTheme.Wood, HudTheme.Iron, HudTheme.Crystal, HudTheme.GoldRes };
            _resourceTexts = new TextMeshProUGUI[4];

            float w = 1f / 4f;
            for (int i = 0; i < 4; i++)
            {
                var cell = NewRect("Res_" + names[i], _resourceStrip, new Vector2(i * w, 0f), new Vector2((i + 1) * w, 1f));

                // small colour dot (left)
                var disc = NewRect("Dot", cell, new Vector2(0.10f, 0.30f), new Vector2(0.34f, 0.70f));
                var dimg = disc.gameObject.AddComponent<Image>();
                dimg.color = tints[i];
                dimg.sprite = HudTheme.Disc;
                dimg.type = HudTheme.Disc != null ? Image.Type.Simple : Image.Type.Simple;
                dimg.raycastTarget = false;
                AddText(disc, glyphs[i], 16, HudTheme.Ink, TextAlignmentOptions.Center);

                var amt = NewRect("Amt", cell, new Vector2(0.36f, 0f), new Vector2(0.98f, 1f));
                _resourceTexts[i] = Ink(AddText(amt, "0", 26, LInk, TextAlignmentOptions.Left));
                _resourceTexts[i].fontStyle = FontStyles.Bold;
            }
        }

        // ── Castle (Heart) HP — top-centre banner. VILLAGE-ONLY. ───────────────
        // RESTYLED (premium chrome to match the inventory): the Tree-of-Life banner
        // was "too basic" (a flat glass strip + a bare fill). It now reads as a
        // framed reliquary plate — kit-framed glass panel + inner rim, a crest leaf
        // glyph + a clear "HEART OF ELARION" caption, a recessed well track with its
        // OWN inner rim, and a styled fill bar that carries a soft gilt highlight
        // strip along its top edge for depth. The HP value + colour-lerp logic
        // (SetHeartHp) is UNTOUCHED — only the visual chrome changed; _castleFill /
        // _castleText are the same objects SetHeartHp drives.
        private void BuildCastleBanner(Transform parent)
        {
            _castleBanner = NewRect("CastleBanner", parent, new Vector2(0.30f, 0.94f), new Vector2(0.70f, 1f));
            // LIGHT: warm parchment plate + thin glowing gilt rune rim + soft glow.
            FramePanelLight(_castleBanner.gameObject, LParchDeep);

            // Tree-of-Life crest tucked top-left of the banner — sprite-FIRST (the
            // hud_tree widget art), with the world-tree glyph kept as the fallback.
            AddWidgetIcon("Crest", _castleBanner, new Vector2(0.015f, 0.42f), new Vector2(0.11f, 0.96f),
                IconHeart, "*", HudTheme.FontHead, LGilt);

            // Caption row — small spaced gilt label so the bar reads as the Heart.
            var caption = NewRect("Caption", _castleBanner, new Vector2(0.11f, 0.56f), new Vector2(0.99f, 0.97f));
            // Dark-ink caption (gilt-on-light cream washed out); keep the regal spacing.
            var cap = AddText(caption, "HEART OF ELARION", HudTheme.FontLabel, LInk, TextAlignmentOptions.MidlineLeft);
            cap.fontStyle = FontStyles.Bold;
            cap.characterSpacing = 3f;
            cap.outlineColor = LGlow;
            cap.outlineWidth = 0.06f;

            // Recessed well track for the fill — soft warm shadow + a thin gilt rim
            // so it reads inset/depthful on the light plate instead of a flat bar.
            var track = NewRect("Track", _castleBanner, new Vector2(0.11f, 0.10f), new Vector2(0.99f, 0.52f));
            StyleWellLight(track.gameObject);
            ElarionUiKit.AddInnerRim(track.gameObject, LGiltSoft);

            var fill = NewRect("Fill", track, Vector2.zero, Vector2.one);
            fill.offsetMin = new Vector2(2f, 2f); fill.offsetMax = new Vector2(-2f, -2f);
            _castleFill = fill.gameObject.AddComponent<Image>();
            _castleFill.color = HudTheme.CastleGold;
            _castleFill.sprite = HudTheme.RoundedFrame;
            _castleFill.type = HudTheme.RoundedFrame != null ? Image.Type.Filled : Image.Type.Filled;
            _castleFill.fillMethod = Image.FillMethod.Horizontal;
            _castleFill.fillOrigin = 0;
            _castleFill.fillAmount = 1f;
            _castleFill.raycastTarget = false;

            // Soft gilt highlight strip along the TOP of the fill — a glassy sheen so
            // the bar has dimension (decorative child of the fill, non-raycast). It
            // shares the fill's clip so it tracks the HP width automatically.
            var sheen = NewRect("Sheen", fill, new Vector2(0f, 0.62f), new Vector2(1f, 1f));
            var sheenImg = sheen.gameObject.AddComponent<Image>();
            sheenImg.color = new Color(1f, 1f, 1f, 0.16f);
            sheenImg.sprite = HudTheme.RoundedFrame;
            sheenImg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            sheenImg.raycastTarget = false;

            // Sprite-FIRST ornate dressing — drop the pack's gilded green gem-socket
            // frame over the Heart bar (FRAME ONLY: the fill keeps its dynamic red↔gold
            // lerp from SetHeartHp, so we don't swap the fill sprite/colour here). No-op
            // when the pack isn't imported (procedural look preserved). Text is added
            // AFTER so it stays on top of the frame.
            TryDressBar(track, null, RpgUiCatalog.BarFrameGreen, null, Color.white, false);

            // Percentage value centred over the track (kept as _castleText for SetHeartHp).
            // Dark ink + faint parchment halo so it stays legible over the HP fill.
            _castleText = AddText(track, "Heart of Elarion — 100%", HudTheme.FontLabel, LInk, TextAlignmentOptions.Center);
            _castleText.fontStyle = FontStyles.Bold;
            _castleText.outlineColor = LGlow;
            _castleText.outlineWidth = 0.12f;
        }

        // ── Wave readout — floating minimal text, no panel. ───────────────────
        private void BuildWaveReadout(Transform parent)
        {
            _waveReadout = NewRect("WaveReadout", parent, new Vector2(0.30f, 0.895f), new Vector2(0.70f, 0.95f));

            _waveText = AddText(_waveReadout, "WAVE 1", HudTheme.FontHead + 2, HudTheme.Text, TextAlignmentOptions.Center);
            _waveText.fontStyle = FontStyles.Bold;
            _waveText.characterSpacing = 4f;
            _waveText.outlineColor = new Color32(0, 0, 0, 200);
            _waveText.outlineWidth = 0.18f;

            var stateRect = NewRect("State", _waveReadout, new Vector2(0f, 0.30f), new Vector2(1f, 0.58f));
            _waveStateText = AddText(stateRect, _lastWaveState, HudTheme.FontBody, HudTheme.Gold, TextAlignmentOptions.Center);
            _waveStateText.outlineColor = new Color32(0, 0, 0, 160);
            _waveStateText.outlineWidth = 0.12f;

            var countRect = NewRect("EnemyCount", _waveReadout, new Vector2(0f, 0f), new Vector2(1f, 0.30f));
            _enemyCountText = AddText(countRect, "", HudTheme.FontLabel, HudTheme.TextDim, TextAlignmentOptions.Center);
            _enemyCountText.fontStyle = FontStyles.Bold;
            _enemyCountText.outlineColor = new Color32(0, 0, 0, 140);
            _enemyCountText.outlineWidth = 0.1f;
        }

        // ── Combo / kill-streak momentum badge — minimal, pops + fades. ───────
        private void BuildMomentumBadge(Transform parent)
        {
            _momentumBadge = NewRect("MomentumBadge", parent, new Vector2(0.30f, 0.32f), new Vector2(0.70f, 0.44f));
            _momentumGroup = _momentumBadge.gameObject.AddComponent<CanvasGroup>();
            _momentumGroup.alpha = 0f;
            _momentumGroup.interactable = false;
            _momentumGroup.blocksRaycasts = false;

            var comboRect = NewRect("Combo", _momentumBadge, new Vector2(0f, 0.42f), new Vector2(1f, 1f));
            _comboText = AddText(comboRect, "", HudTheme.FontTitle + 8, HudTheme.Gilt, TextAlignmentOptions.Center);
            _comboText.fontStyle = FontStyles.Bold;
            _comboText.characterSpacing = 3f;
            _comboText.outlineColor = new Color32(0, 0, 0, 210);
            _comboText.outlineWidth = 0.2f;

            var streakRect = NewRect("Streak", _momentumBadge, new Vector2(0f, 0f), new Vector2(1f, 0.42f));
            _streakText = AddText(streakRect, "", HudTheme.FontHead, HudTheme.HpRed, TextAlignmentOptions.Center);
            _streakText.fontStyle = FontStyles.Bold;
            _streakText.characterSpacing = 2f;
            _streakText.outlineColor = new Color32(0, 0, 0, 200);
            _streakText.outlineWidth = 0.16f;
        }

        // ── Top-left party stack — slim glass rows. ───────────────────────────
        private void BuildPartyFrames(Transform parent)
        {
            _partyFrame    = new GameObject[PartySlotCount];
            _partyHpFill   = new Image[PartySlotCount];
            _partyMpFill   = new Image[PartySlotCount];
            _partyPortrait = new Image[PartySlotCount];
            _partyName     = new TextMeshProUGUI[PartySlotCount];
            _partyHpText   = new TextMeshProUGUI[PartySlotCount];

            var frameSprite = WidgetSprite("player_frame_bg");   // dark-stone P1 frame (ring + banner + tracks)
            var hpSprite    = WidgetSprite("player_hp_fill");    // red HP fill
            var mpSprite    = WidgetSprite("player_mp_fill");    // blue MP fill

            _partyStack = NewRect("PartyStack", parent, new Vector2(0f, 1f), new Vector2(0f, 1f));
            AnchorTopLeft(_partyStack, x: 10f, y: 10f, width: 268f,
                height: PartyRowHeight * PartySlotCount + PartyRowGap * (PartySlotCount - 1));

            for (int i = 0; i < PartySlotCount; i++)
            {
                var frame = NewRect("Party" + i, _partyStack, new Vector2(0f, 1f), new Vector2(1f, 1f));
                frame.pivot = new Vector2(0.5f, 1f);
                frame.anchoredPosition = new Vector2(0f, -i * (PartyRowHeight + PartyRowGap));
                frame.sizeDelta = new Vector2(0f, PartyRowHeight);

                // Dark-stone frame art (P1) as the backing — dark panel fallback if absent.
                var frImg = frame.gameObject.AddComponent<Image>();
                if (frameSprite != null) { frImg.sprite = frameSprite; frImg.color = Color.white; }
                else frImg.color = new Color(0.10f, 0.09f, 0.11f, 0.96f);
                frImg.raycastTarget = false;
                _partyFrame[i] = frame.gameObject;

                // Portrait (class image) in the circle on the left.
                var port = NewRect("Portrait", frame, new Vector2(0.035f, 0.12f), new Vector2(0.26f, 0.94f));
                var pimg = port.gameObject.AddComponent<Image>();
                pimg.raycastTarget = false; pimg.preserveAspect = true; pimg.color = Color.white;
                _partyPortrait[i] = pimg;

                // Name (banner, upper-right) — gold ink.
                var nameRect = NewRect("Name", frame, new Vector2(0.31f, 0.52f), new Vector2(0.97f, 0.95f));
                _partyName[i] = AddText(nameRect, i == 0 ? "Hero" : "—", 15,
                    new Color(0.95f, 0.88f, 0.62f), TextAlignmentOptions.Left);
                _partyName[i].fontStyle = FontStyles.Bold;
                _partyName[i].enableAutoSizing = true; _partyName[i].fontSizeMin = 9f; _partyName[i].fontSizeMax = 15f;

                // HP bar (red, mid-right).
                var hpTrack = NewRect("HPTrack", frame, new Vector2(0.31f, 0.30f), new Vector2(0.985f, 0.50f));
                var hpFill  = NewRect("HPFill", hpTrack, Vector2.zero, Vector2.one);
                var hfimg = hpFill.gameObject.AddComponent<Image>();
                hfimg.sprite = hpSprite; hfimg.color = hpSprite != null ? Color.white : HudTheme.HpRed;
                hfimg.type = Image.Type.Filled; hfimg.fillMethod = Image.FillMethod.Horizontal; hfimg.fillOrigin = 0; hfimg.fillAmount = 1f;
                hfimg.raycastTarget = false;
                _partyHpFill[i] = hfimg;
                _partyHpText[i] = AddText(hpTrack, "", 11, HudTheme.Text, TextAlignmentOptions.Center);
                _partyHpText[i].outlineColor = new Color32(40, 16, 16, 200); _partyHpText[i].outlineWidth = 0.14f;

                // MP bar (blue, lower-right).
                var mpTrack = NewRect("MPTrack", frame, new Vector2(0.31f, 0.07f), new Vector2(0.985f, 0.27f));
                var mpFill  = NewRect("MPFill", mpTrack, Vector2.zero, Vector2.one);
                var mfimg = mpFill.gameObject.AddComponent<Image>();
                mfimg.sprite = mpSprite; mfimg.color = mpSprite != null ? Color.white : new Color(0.30f, 0.50f, 0.95f);
                mfimg.type = Image.Type.Filled; mfimg.fillMethod = Image.FillMethod.Horizontal; mfimg.fillOrigin = 0; mfimg.fillAmount = 1f;
                mfimg.raycastTarget = false;
                _partyMpFill[i] = mfimg;

                _partyFrame[i].SetActive(i == 0);
            }

            RefreshHeroPortrait();
        }

        /// <summary>Loads the hero's class portrait (slot 0) from GameState.HeroClass.</summary>
        private void RefreshHeroPortrait()
        {
            if (_partyPortrait == null || _partyPortrait.Length == 0 || _partyPortrait[0] == null) return;
            var svc = DeNelle.Core.State.GameStateService.Instance;
            var hc = svc != null && svc.State != null ? svc.State.HeroClass : DeNelle.Core.State.HeroClassOpt.None;
            var sp = WidgetSprite(PortraitNameForClass(hc));
            if (sp != null) _partyPortrait[0].sprite = sp;
            // Hero's roster name (Mage→Thrain, Knight→Grom, Ranger→Sylas, Cleric→Elara) — not "Hero".
            if (_partyName != null && _partyName.Length > 0 && _partyName[0] != null)
                _partyName[0].text = NameForClass(hc);
        }

        private static string NameForClass(DeNelle.Core.State.HeroClassOpt hc)
        {
            switch (hc)
            {
                case DeNelle.Core.State.HeroClassOpt.Mage:   return "Thrain";
                case DeNelle.Core.State.HeroClassOpt.Knight: return "Grom";
                case DeNelle.Core.State.HeroClassOpt.Ranger: return "Sylas";
                case DeNelle.Core.State.HeroClassOpt.Cleric: return "Elara";
                default: return "Hero";
            }
        }

        private static string PortraitNameForClass(DeNelle.Core.State.HeroClassOpt hc)
        {
            switch (hc)
            {
                case DeNelle.Core.State.HeroClassOpt.Knight: return "Knight/knight";
                case DeNelle.Core.State.HeroClassOpt.Ranger: return "Ranger/ranger";
                case DeNelle.Core.State.HeroClassOpt.Mage:   return "Wizard/wiard";
                case DeNelle.Core.State.HeroClassOpt.Cleric: return "Healer/healer";
                default: return null;
            }
        }

        // ── Bottom-LEFT-ABOVE-joystick vitals — hero HP + mana stacked bars. ──
        // MOBILE ERGONOMICS: the LEFT thumb drives the VirtualJoystick (bottom-left
        // engage zone, centre ~radius*1.35 from the corner, claimed to radius*1.7).
        // We keep that quadrant CLEAR and float the hero vitals ABOVE it, anchored
        // to the bottom-left but lifted well over the stick (≈y 0.165→0.235).
        private void BuildVitalsCluster(Transform parent)
        {
            _vitalsCluster = NewRect("VitalsCluster", parent, new Vector2(0.02f, 0.165f), new Vector2(0.40f, 0.235f));
            FramePanelLight(_vitalsCluster.gameObject, LParch);

            // HP bar (top half) — REMOVED (WO-382): the hero's HP was shown twice —
            // here (bottom-left red bar) AND in the top-left party panel (slot 0).
            // The party panel is now the single source of truth for hero HP, so we
            // no longer build the duplicate HP bar/text here. _hpFill and _hpText
            // stay null; SetHeroHp() null-guards them and still feeds the party
            // panel via SetPartyMember(0, ...). The mana bar + XP line below are
            // UNIQUE to this cluster and are intentionally kept.
            // (Former HP-bar build block removed to de-duplicate the display.)

            // XP line — a THIN yellow strip directly ABOVE the HP bar (owner request:
            // no full-screen XP bar; show level progress visually next to health).
            // Sits in the gap between the HP track top (0.92) and the panel top (1.0).
            var xpTrack = NewRect("XPTrack", _vitalsCluster, new Vector2(0.05f, 0.935f), new Vector2(0.95f, 0.99f));
            StyleWellLight(xpTrack.gameObject);
            var xpFill = NewRect("XPFill", xpTrack, Vector2.zero, Vector2.one);
            xpFill.offsetMin = new Vector2(1f, 1f); xpFill.offsetMax = new Vector2(-1f, -1f);
            _xpLineFill = xpFill.gameObject.AddComponent<Image>();
            _xpLineFill.color = new Color(1f, 0.85f, 0.15f, 1f); // yellow
            _xpLineFill.sprite = HudTheme.RoundedFrame;
            _xpLineFill.type = HudTheme.RoundedFrame != null ? Image.Type.Filled : Image.Type.Filled;
            _xpLineFill.fillMethod = Image.FillMethod.Horizontal;
            _xpLineFill.fillOrigin = 0;
            _xpLineFill.fillAmount = 0f;
            _xpLineFill.raycastTarget = false;

            // Mana bar (bottom half)
            var mTrack = NewRect("ManaTrack", _vitalsCluster, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.48f));
            StyleWellLight(mTrack.gameObject);
            var mFill = NewRect("ManaFill", mTrack, Vector2.zero, Vector2.one);
            mFill.offsetMin = new Vector2(1.5f, 1.5f); mFill.offsetMax = new Vector2(-1.5f, -1.5f);
            _manaFill = mFill.gameObject.AddComponent<Image>();
            _manaFill.color = HudTheme.ManaBlue;
            _manaFill.sprite = HudTheme.RoundedFrame;
            _manaFill.type = HudTheme.RoundedFrame != null ? Image.Type.Filled : Image.Type.Filled;
            _manaFill.fillMethod = Image.FillMethod.Horizontal;
            _manaFill.fillOrigin = 0;
            _manaFill.fillAmount = 1f;
            _manaFill.raycastTarget = false;

            // Sprite-FIRST ornate dressing for the mana bar — the pack's gilded gem-socket
            // frame + a blue-tinted glossy fill (mana has no dynamic colour change, so
            // tinting the pack fill blue is safe). No-op when the pack isn't imported.
            TryDressBar(mTrack, _manaFill, RpgUiCatalog.BarFrameBlue, RpgUiCatalog.BarFillBlue,
                HudTheme.ManaBlue, true);
            // Value over the blue mana fill — cream + dark halo keeps it crisp on the
            // saturated blue (and on the light empty track the dark halo still reads).
            _manaText = AddText(mTrack, "", 13, HudTheme.Text, TextAlignmentOptions.Center);
            _manaText.fontStyle = FontStyles.Bold;
            _manaText.outlineColor = new Color32(10, 18, 44, 200); _manaText.outlineWidth = 0.14f;
        }

        // ── Bottom-RIGHT ability cluster — 2×2 grid of skill cells (RIGHT thumb). ─
        // MOBILE ERGONOMICS: the RIGHT thumb hits skills, so the ability cluster
        // hugs the bottom-RIGHT corner as a compact 2×2 grid (reachable arc for a
        // right thumb). The LEFT thumb owns the joystick — the bottom-left stays
        // clear. Cells/cooldowns/labels/accents are unchanged; only the container
        // anchor + per-cell grid layout moved. All SetAbility* bindings are intact.
        private void BuildSkillBar(Transform parent)
        {
            _skillBar = NewRect("SkillBar", parent, new Vector2(0.62f, 0.0f), new Vector2(1.0f, 0.22f));
            FramePanelLight(_skillBar.gameObject, LParchDeep);

            _slotKey      = new TextMeshProUGUI[AbilitySlotCount];
            _slotGlyph    = new TextMeshProUGUI[AbilitySlotCount];
            _slotName     = new TextMeshProUGUI[AbilitySlotCount];
            _slotAccent   = new Image[AbilitySlotCount];
            _slotIcon     = new Image[AbilitySlotCount];
            _slotCooldown = new Image[AbilitySlotCount];
            _slotCdFill   = new float[AbilitySlotCount];

            string[] defaultKeys = { "Q", "W", "E", "R" };

            // 2×2 grid inside the cluster. Slot 0 bottom-right (closest to thumb),
            // 1 bottom-left, 2 top-right, 3 top-left — a natural right-thumb arc.
            const int cols = 2, rows = 2;
            float gapX = 0.04f, gapY = 0.05f;
            float marginX = 0.05f, marginY = 0.05f;
            float cellW = (1f - 2f * marginX - (cols - 1) * gapX) / cols;
            float cellH = (1f - 2f * marginY - (rows - 1) * gapY) / rows;

            for (int i = 0; i < AbilitySlotCount; i++)
            {
                int col = i % cols;            // 0 = right column, 1 = left column
                int row = i / cols;            // 0 = bottom row,   1 = top row
                // place column 0 on the RIGHT (nearest the screen edge / thumb).
                float x = marginX + (cols - 1 - col) * (cellW + gapX);
                float y = marginY + row * (cellH + gapY);
                var cell = NewRect("Slot" + i, _skillBar, new Vector2(x, y), new Vector2(x + cellW, y + cellH));
                var cellImg = cell.gameObject.AddComponent<Image>();
                cellImg.color = LParchSoft;   // light parchment cell seat
                cellImg.sprite = HudTheme.RoundedFrame;
                cellImg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                // thin gilt rune rim per cell (the airy framed look).
                ElarionUiKit.AddInnerRim(cell.gameObject, LGiltSoft);

                // Circular RUNIC ability frame ringing the ability disc (sprite-first;
                // the hud_ability_frame widget art). A decorative ring behind the
                // accent so each skill reads as a rune-framed circular button per the
                // mockup. When the art is missing it stays inert (the accent shows).
                var runeFrame = NewRect("RuneFrame", cell, new Vector2(0.04f, 0.30f), new Vector2(0.96f, 1.0f));
                var runeImg = runeFrame.gameObject.AddComponent<Image>();
                runeImg.raycastTarget = false;
                if (!TrySetWidget(runeImg, IconAbilityFrame))
                    runeImg.color = new Color(0f, 0f, 0f, 0f); // no art → inert

                // Accent disc (tinted per ability) — fills most of the cell as a CIRCLE
                // now (circular rune button). Light seat so an unset slot reads
                // parchment; SetAbilitySlot still recolours this per-ability (kept).
                var disc = NewRect("Accent", cell, new Vector2(0.14f, 0.40f), new Vector2(0.86f, 0.94f));
                _slotAccent[i] = disc.gameObject.AddComponent<Image>();
                _slotAccent[i].color = LSlotFill;
                _slotAccent[i].sprite = HudTheme.Disc;   // circular ability seat
                _slotAccent[i].type = HudTheme.Disc != null ? Image.Type.Simple : Image.Type.Simple;
                _slotAccent[i].raycastTarget = false;

                // Real ability ICON art (by class+slot) — on the disc, under the glyph + cooldown.
                // Hidden (zero alpha) until SetAbilitySlot resolves it; falls back to the glyph.
                var abIcon = NewRect("AbIcon", disc, new Vector2(0.13f, 0.13f), new Vector2(0.87f, 0.87f));
                _slotIcon[i] = abIcon.gameObject.AddComponent<Image>();
                _slotIcon[i].raycastTarget = false;
                _slotIcon[i].preserveAspect = true;
                _slotIcon[i].color = new Color(1f, 1f, 1f, 0f);

                // Glyph: dark ink (legible on the light seat AND on tinted ability
                // accents, which SetAbilitySlot sets at 0.85 alpha over the light cell).
                _slotGlyph[i] = AddText(disc, "", 30, LInk, TextAlignmentOptions.Center);
                _slotGlyph[i].outlineColor = LGlow;
                _slotGlyph[i].outlineWidth = 0.06f;

                // Cooldown radial overlay
                var cd = NewRect("CD", disc, Vector2.zero, Vector2.one);
                _slotCooldown[i] = cd.gameObject.AddComponent<Image>();
                _slotCooldown[i].color = HudTheme.CdShade;
                _slotCooldown[i].sprite = HudTheme.Disc;
                _slotCooldown[i].type = HudTheme.Disc != null ? Image.Type.Filled : Image.Type.Filled;
                _slotCooldown[i].fillMethod = Image.FillMethod.Radial360;
                _slotCooldown[i].fillOrigin = (int)Image.Origin360.Top;
                _slotCooldown[i].fillClockwise = false;
                _slotCooldown[i].fillAmount = 0f;
                _slotCooldown[i].raycastTarget = false;

                // Ability NAME label
                var nameRect = NewRect("Name", cell, new Vector2(0.02f, 0.0f), new Vector2(0.98f, 0.32f));
                _slotName[i] = AddText(nameRect, "", 14, LInk, TextAlignmentOptions.Center);
                _slotName[i].fontStyle = FontStyles.Bold;
                _slotName[i].enableAutoSizing = true;
                _slotName[i].fontSizeMin = 8f;
                _slotName[i].fontSizeMax = 14f;
                _slotName[i].raycastTarget = false;
                _slotName[i].outlineColor = LGlow;
                _slotName[i].outlineWidth = 0.06f;

                // Hotkey badge (top-right) — small gilt chip with dark ink letter.
                var keyBadge = NewRect("KeyBadge", cell, new Vector2(0.70f, 0.70f), new Vector2(1.0f, 1.0f));
                var keyImg = keyBadge.gameObject.AddComponent<Image>();
                keyImg.sprite = HudTheme.RoundedFrame;
                keyImg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                keyImg.color = new Color(LGilt.r, LGilt.g, LGilt.b, 0.85f);
                keyImg.raycastTarget = false;
                _slotKey[i] = AddText(keyBadge, defaultKeys[i], 14, LInk, TextAlignmentOptions.Center);
                _slotKey[i].fontStyle = FontStyles.Bold;

                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = cellImg;
                HudTheme.StyleButtonColors(btn, LParchSoft);
                int slot = i;
                btn.onClick.AddListener(() => AbilityRequested?.Invoke(slot));
            }
        }

        // ── Build entry — bottom-right, clean gold pill. VILLAGE-ONLY. ────────
        private void BuildBuildButton(Transform parent)
        {
            _buildBtn = NewRect("BuildBtn", parent, new Vector2(0.86f, 0.135f), new Vector2(0.99f, 0.205f));
            var bimg = _buildBtn.gameObject.AddComponent<Image>();
            bimg.color = HudTheme.GoldButton;
            bimg.sprite = HudTheme.RoundedFrame;
            bimg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            var btn = _buildBtn.gameObject.AddComponent<Button>();
            btn.targetGraphic = bimg;
            HudTheme.StyleButtonColors(btn, HudTheme.GoldButton);
            btn.onClick.AddListener(() => BuildRequested?.Invoke());
            var t = AddText(_buildBtn, "BUILD", HudTheme.FontBody, HudTheme.Ink, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
        }

        // ── "Start Next Wave" button. VILLAGE-ONLY + between-waves only. ──────
        // OWNER ASK (2026-06-08): the old big centre "DEFEND!" button was too large
        // and ambiguous (read like a pet command, not "begin the next wave"). It's
        // now a SMALL pill relabelled "Start Next Wave" sitting BESIDE the town wave
        // TIMER (top-left, just right of the TownWave cluster). Same action — it
        // still raises StartWaveRequested (the wave-start signal); only the label,
        // size and position changed. Final anchors set in ApplyResponsiveLayout.
        private void BuildStartWaveButton(Transform parent)
        {
            _startWaveBtn = NewRect("StartWaveBtn", parent, new Vector2(0.30f, 0.945f), new Vector2(0.43f, 0.99f));
            var bimg = _startWaveBtn.gameObject.AddComponent<Image>();
            bimg.color = HudTheme.GoldButton;
            bimg.sprite = HudTheme.RoundedFrame;
            bimg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            var btn = _startWaveBtn.gameObject.AddComponent<Button>();
            btn.targetGraphic = bimg;
            HudTheme.StyleButtonColors(btn, HudTheme.GoldButton);
            btn.onClick.AddListener(() => StartWaveRequested?.Invoke());
            var t = AddText(_startWaveBtn, "> Start Next Wave", 14, HudTheme.Ink, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true;
            t.fontSizeMin = 9f;
            t.fontSizeMax = 14f;
            _startWaveBtn.gameObject.SetActive(false);
        }

        private void BuildRepairPrompt(Transform parent)
        {
            var p = NewRect("RepairPrompt", parent, new Vector2(0.30f, 0.42f), new Vector2(0.70f, 0.58f));
            FramePanelLight(p.gameObject, LParchDeep);
            _repairPanel = p.gameObject;

            var labelRect = NewRect("Label", p, new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.95f));
            _repairLabel = Ink(AddText(labelRect, "", 20, LInk, TextAlignmentOptions.Center));
            _repairLabel.fontStyle = FontStyles.Bold;

            var yes = NewRect("Yes", p, new Vector2(0.10f, 0.10f), new Vector2(0.46f, 0.44f));
            var yimg = yes.gameObject.AddComponent<Image>();
            yimg.color = HudTheme.GoldButton;
            yimg.sprite = HudTheme.RoundedFrame;
            yimg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            var yesBtn = yes.gameObject.AddComponent<Button>();
            yesBtn.targetGraphic = yimg;
            HudTheme.StyleButtonColors(yesBtn, HudTheme.GoldButton);
            yesBtn.onClick.AddListener(() => { RepairConfirmRequested?.Invoke(); HideRepairPrompt(); });
            AddText(yes, "Repair", HudTheme.FontBody, HudTheme.Ink, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

            var no = NewRect("No", p, new Vector2(0.54f, 0.10f), new Vector2(0.90f, 0.44f));
            var nimg = no.gameObject.AddComponent<Image>();
            nimg.color = LParchSoft;
            nimg.sprite = HudTheme.RoundedFrame;
            nimg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            var noBtn = no.gameObject.AddComponent<Button>();
            noBtn.targetGraphic = nimg;
            HudTheme.StyleButtonColors(noBtn, LParchSoft);
            ElarionUiKit.AddInnerRim(no.gameObject, LGiltSoft);
            noBtn.onClick.AddListener(() => { RepairCancelRequested?.Invoke(); HideRepairPrompt(); });
            AddText(no, "Later", HudTheme.FontBody, LInk, TextAlignmentOptions.Center);

            _repairPanel.SetActive(false);
        }

        // =====================================================================
        //  WO-339 · TOWN HUD builders (idle-village mode)
        // =====================================================================

        // ── Top-LEFT · WAVE MANAGEMENT — countdown + progress + lookout badge. ─
        private void BuildTownWaveCluster(Transform parent)
        {
            _townWaveCluster = NewRect("TownWave", parent, new Vector2(0f, 1f), new Vector2(0f, 1f));
            AnchorTopLeft(_townWaveCluster, x: 12f, y: 8f, width: 380f, height: 140f);

            var clockSprite = WidgetSprite("hud_wave_clock");   // gear-clock bezel + parchment face
            var plateSprite = WidgetSprite("hud_wave_plate");   // pointed dark name plate

            // ── Gear-clock (left) — bezel + face; countdown overlays the face. ──
            var clock = NewRect("Clock", _townWaveCluster, new Vector2(0f, 0.04f), new Vector2(0.42f, 1f));
            var clockImg = clock.gameObject.AddComponent<Image>();
            clockImg.raycastTarget = false;
            if (clockSprite != null) { clockImg.sprite = clockSprite; clockImg.color = Color.white; clockImg.preserveAspect = true; }
            else { clockImg.color = new Color(0.93f, 0.88f, 0.74f, 0.96f); }   // parchment fallback

            // Static "NEXT WAVE" etch (top of the face).
            var subRect = NewRect("ClockSub", clock, new Vector2(0.22f, 0.58f), new Vector2(0.78f, 0.74f));
            AddText(subRect, "NEXT WAVE", 9, new Color(0.36f, 0.27f, 0.15f, 0.85f), TextAlignmentOptions.Center)
                .fontStyle = FontStyles.Bold;

            // Countdown (MM:SS) — centre of the clock face.
            var timerRect = NewRect("Timer", clock, new Vector2(0.14f, 0.34f), new Vector2(0.86f, 0.60f));
            _townTimerText = AddText(timerRect, "", HudTheme.FontHead + 6, new Color(0.18f, 0.12f, 0.07f), TextAlignmentOptions.Center);
            _townTimerText.fontStyle = FontStyles.Bold;

            // Lookout BELL — small, lower-right of the clock face; pulses when status ≥ 2.
            _townBell = NewRect("LookoutBell", clock, new Vector2(0.40f, 0.16f), new Vector2(0.60f, 0.34f));
            _townBellGlyph = AddText(_townBell, "‹", HudTheme.FontHead, new Color(0.42f, 0.31f, 0.16f, 0.9f), TextAlignmentOptions.Center);

            // ── Pointed name plate (right) — state label; reddens on INCOMING. ──
            var plate = NewRect("Plate", _townWaveCluster, new Vector2(0.40f, 0.40f), new Vector2(1f, 0.78f));
            _townLookoutBadge = plate.gameObject.AddComponent<Image>();
            _townLookoutBadge.raycastTarget = false;
            if (plateSprite != null) { _townLookoutBadge.sprite = plateSprite; _townLookoutBadge.color = Color.white; _townLookoutBadge.type = Image.Type.Simple; }
            else _townLookoutBadge.color = new Color(0.12f, 0.09f, 0.06f, 0.95f);
            _townLookoutText = AddText(plate, "", 15, new Color(0.95f, 0.82f, 0.45f), TextAlignmentOptions.Center);
            _townLookoutText.fontStyle = FontStyles.Bold;

            // Wave progress (small label + thin bar, below the plate).
            var progLabel = NewRect("ProgLabel", _townWaveCluster, new Vector2(0.42f, 0.18f), new Vector2(1f, 0.40f));
            _townWaveProgText = AddText(progLabel, "Wave 1", 12, new Color(0.90f, 0.80f, 0.55f), TextAlignmentOptions.Center);
            _townWaveProgText.fontStyle = FontStyles.Bold;

            var progTrack = NewRect("ProgTrack", _townWaveCluster, new Vector2(0.42f, 0.04f), new Vector2(1f, 0.16f));
            StyleWellLight(progTrack.gameObject);
            var progFill = NewRect("ProgFill", progTrack, Vector2.zero, Vector2.one);
            progFill.offsetMin = new Vector2(1.5f, 1.5f); progFill.offsetMax = new Vector2(-1.5f, -1.5f);
            _townWaveProgFill = progFill.gameObject.AddComponent<Image>();
            _townWaveProgFill.color = HudTheme.Gold;
            _townWaveProgFill.sprite = HudTheme.RoundedFrame;
            _townWaveProgFill.type = Image.Type.Filled;
            _townWaveProgFill.fillMethod = Image.FillMethod.Horizontal;
            _townWaveProgFill.fillOrigin = 0;
            _townWaveProgFill.fillAmount = 0f;
            _townWaveProgFill.raycastTarget = false;
        }

        // ── Top-LEFT (under the wave cluster) · PASSIVE-XP badge (WO-361). ──────
        // Compact "⚡ Towers earning N XP/min" pill. Toggleable via SetPassiveXp /
        // the visibility flag; hidden when there are no towers / zero rate.
        private void BuildTownPassiveXp(Transform parent)
        {
            _townPassiveXp = NewRect("TownPassiveXp", parent, new Vector2(0f, 1f), new Vector2(0f, 1f));
            // Sits just below the 118px-tall wave cluster (top-left, y:12).
            AnchorTopLeft(_townPassiveXp, x: 12f, y: 136f, width: 248f, height: 26f);
            FramePanelLight(_townPassiveXp.gameObject, LParch);

            var label = NewRect("Label", _townPassiveXp, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            // Aether-violet accent reads on parchment; add a faint light halo for lift.
            _townPassiveXpText = AddText(label, "⚡ Towers earning 0 XP/min", 13, HudTheme.Crystal, TextAlignmentOptions.MidlineLeft);
            _townPassiveXpText.outlineColor = LGlow;
            _townPassiveXpText.outlineWidth = 0.06f;
            _townPassiveXpText.fontStyle = FontStyles.Bold;
            _townPassiveXpText.raycastTarget = false;

            // Hidden until a non-zero rate is fed.
            _townPassiveXp.gameObject.SetActive(false);
        }

        // ── Top-CENTRE · RESOURCE badges (icon + number, distinct colours). ────
        private void BuildTownResourceBadges(Transform parent)
        {
            _townResStrip = NewRect("TownResources", parent, new Vector2(0.30f, 0.75f), new Vector2(0.70f, 1f));   // 2.5× taller for the resource icons
            ApplyStripBar(_townResStrip.gameObject);   // same Tech-pack wood bar as the bottom strip (cohesive)

            string[] names  = { "Food", "Wood", "Crystal", "Iron" };   // icon = hud_food/hud_wood/hud_crystal/hud_iron
            string[] glyphs = { "o", "^", "*", "+" };
            Color[] tints   = { HudTheme.GoldRes, HudTheme.Wood, HudTheme.Crystal, HudTheme.Iron };
            _townResText    = new TextMeshProUGUI[4];
            _townResBadge   = new Image[4];
            _townResOutline = new Image[4];

            float w = 0.205f;   // 4 cells inset into 0.09–0.91 of the strip (clear of the wood bar's rolled ends)
            for (int i = 0; i < 4; i++)
            {
                var cell = NewRect("Res_" + names[i], _townResStrip, new Vector2(0.09f + i * w, 0.14f), new Vector2(0.09f + (i + 1) * w - 0.012f, 0.86f));
                // LIGHT parchment badge; the +/- flash logic lerps from this base
                // (LParch) toward green/red — see UpdateTownHud (baseline flipped too).
                _townResBadge[i] = HudTheme.StylePanel(cell.gameObject, LParch);
                _townResBadge[i].color = new Color(0f, 0f, 0f, 0f);   // transparent — the wood strip bar frames the whole row now

                // red low-warn outline overlay (hidden until value < threshold).
                var outline = NewRect("Low", cell, Vector2.zero, Vector2.one);
                _townResOutline[i] = outline.gameObject.AddComponent<Image>();
                _townResOutline[i].sprite = HudTheme.RoundedFrame;
                _townResOutline[i].type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                _townResOutline[i].color = new Color(HudTheme.HpRed.r, HudTheme.HpRed.g, HudTheme.HpRed.b, 0f);
                _townResOutline[i].raycastTarget = false;

                var dot = NewRect("Dot", cell, new Vector2(0.02f, 0.08f), new Vector2(0.44f, 0.92f));
                var dimg = dot.gameObject.AddComponent<Image>();
                dimg.raycastTarget = false;
                var resIcon = WidgetSprite("hud_" + names[i].ToLower());   // hud_gold / hud_wood / hud_crystal / hud_iron
                if (resIcon != null) { dimg.sprite = resIcon; dimg.color = Color.white; dimg.preserveAspect = true; }
                else { dimg.color = tints[i]; dimg.sprite = HudTheme.Disc; AddText(dot, glyphs[i], 14, HudTheme.Ink, TextAlignmentOptions.Center); }   // dot fallback until the icon's dropped

                var amt = NewRect("Amt", cell, new Vector2(0.46f, 0f), new Vector2(0.98f, 1f));
                _townResText[i] = AddText(amt, "0", 22, new Color(1f, 0.94f, 0.72f), TextAlignmentOptions.Left);   // bright cream — pops on the wood bar
                _townResText[i].fontStyle = FontStyles.Bold;
                _townResText[i].outlineColor = new Color32(28, 16, 6, 235);
                _townResText[i].outlineWidth = 0.2f;
            }
        }

        // ── Top-RIGHT · LIGHTWEIGHT 2D mini-map (icon markers, NOT a RenderTexture).
        // Markers are positioned by a flat world→map projection (origin = Heart at
        // 0,0,0). A RenderTexture/second-camera upgrade is FUTURE — too heavy/risky
        // for WebGL. Tapping a marker pans the main camera toward its world point.
        private void BuildTownMiniMap(Transform parent)
        {
            _townMiniMap = NewRect("TownMiniMap", parent, new Vector2(1f, 1f), new Vector2(1f, 1f));
            _townMiniMap.anchorMin = new Vector2(1f, 1f);
            _townMiniMap.anchorMax = new Vector2(1f, 1f);
            _townMiniMap.pivot = new Vector2(1f, 1f);
            _townMiniMap.anchoredPosition = new Vector2(-12f, -12f);
            _townMiniMap.sizeDelta = new Vector2(140f, 140f);
            FramePanel(_townMiniMap.gameObject, ElarionUiKit.GlassDeep);

            _townMiniMapInner = NewRect("Inner", _townMiniMap, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));

            // Hero dot — always centred (the map is hero-relative? No: world-anchored
            // at Heart origin; the hero marker tracks the hero's world position).
            AddMiniMapMarker("Hero", Vector3.zero, HudTheme.HeroDot, "*", isHero: true);
        }

        /// <summary>Add a POI marker icon to the mini-map (projected world→map each frame).</summary>
        private MiniMapMarker AddMiniMapMarker(string label, Vector3 worldPos, Color tint, string glyph, bool isHero)
        {
            if (_townMiniMapInner == null) return null;
            var rect = NewRect("M_" + label, _townMiniMapInner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rect.sizeDelta = isHero ? new Vector2(14f, 14f) : new Vector2(12f, 12f);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = tint;
            img.sprite = HudTheme.Disc;
            AddText(rect, glyph, isHero ? 12 : 10, HudTheme.Ink, TextAlignmentOptions.Center);

            var marker = new MiniMapMarker { Rect = rect, Icon = img, WorldPos = worldPos, IsHero = isHero };

            // Tap-a-marker → pan the main camera toward its world point (best-effort).
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => PanCameraToward(marker.WorldTarget != null ? marker.WorldTarget.position : marker.WorldPos));

            _miniMarkers.Add(marker);
            return marker;
        }

        // ── Bottom · TOWN METRICS (3-col: Heart HP %, Towers built/max, Population).
        private void BuildTownMetrics(Transform parent)
        {
            _townMetrics = NewRect("TownMetrics", parent, new Vector2(0.30f, 0f), new Vector2(0.70f, 0.05f));
            ApplyStripBar(_townMetrics.gameObject);   // Tech-pack menu bar frame (was cream parchment)

            // Inset the 3 columns from the bar's bracket ends so the icons sit grouped, not spread.
            _townHeartText = BuildMetricCol(_townMetrics, 0.11f, 0.37f, "Elarion", "Heart", "100%", HudTheme.HpRed);
            _townTowerText = BuildMetricCol(_townMetrics, 0.37f, 0.63f, IconBuild, "Towers", "0/0", HudTheme.Gold);
            _townPopText   = BuildMetricCol(_townMetrics, 0.63f, 0.89f, "population", "Pop", "0", HudTheme.Crystal);
        }

        private TextMeshProUGUI BuildMetricCol(Transform parent, float x0, float x1, string iconName, string label, string value, Color tint)
        {
            var col = NewRect("Col_" + label, parent, new Vector2(x0, 0f), new Vector2(x1, 1f));
            // Icon on TOP, big number BELOW — vertical stack stays readable in a narrow (mobile) column.
            var lab = NewRect("Lab", col, new Vector2(0.10f, 0.46f), new Vector2(0.90f, 0.99f));
            var icon = iconName != null ? WidgetSprite(iconName) : null;
            if (icon != null)
            {
                var img = lab.gameObject.AddComponent<Image>();
                img.sprite = icon; img.color = Color.white; img.preserveAspect = true; img.raycastTarget = false;
            }
            else
            {
                var lt = AddText(lab, label, 12, new Color(tint.r, tint.g, tint.b, 0.9f), TextAlignmentOptions.Center);
                lt.fontStyle = FontStyles.Bold;
            }
            var valRect = NewRect("Val", col, new Vector2(0.0f, 0.02f), new Vector2(1f, 0.44f));
            var vt = AddText(valRect, value, 24, new Color(1f, 0.94f, 0.72f), TextAlignmentOptions.Center);  // bright cream — pops on the wood bar
            vt.fontStyle = FontStyles.Bold;
            vt.outlineColor = new Color32(28, 16, 6, 235);   // dark outline → numbers jump off the bar
            vt.outlineWidth = 0.25f;
            vt.enableAutoSizing = true; vt.fontSizeMin = 14f; vt.fontSizeMax = 30f;
            return vt;
        }

        // =====================================================================
        //  SAFE AREA — inset the whole HUD inside the device safe area.
        // =====================================================================
        private void ApplySafeArea()
        {
            if (_safeArea == null) return;
            Rect sa = Screen.safeArea;
            float sw = Screen.width, sh = Screen.height;
            if (sw <= 0f || sh <= 0f) return;
            Vector2 min = new Vector2(sa.xMin / sw, sa.yMin / sh);
            Vector2 max = new Vector2(sa.xMax / sw, sa.yMax / sh);
            _safeArea.anchorMin = min;
            _safeArea.anchorMax = max;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        // =====================================================================
        //  RESPONSIVE LAYOUT — re-anchor clusters for portrait vs landscape.
        // =====================================================================
        private void ApplyResponsiveLayout(bool force)
        {
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
            float aspect = _lastScreenH > 0 ? (float)_lastScreenW / _lastScreenH : 0.5f;
            bool portrait = aspect < 1f;
            if (!force && portrait == _isPortrait) return;
            _isPortrait = portrait;

            if (_scaler != null)
                _scaler.matchWidthOrHeight = portrait ? 0.5f : 0.35f;

            // MOBILE ERGONOMICS (both orientations):
            //  • bottom-LEFT  = movement joystick zone → kept CLEAR.
            //  • bottom-RIGHT = ability cluster (right thumb).
            //  • hero HP/mana = bottom-left ABOVE the joystick.
            //  • Build button = upper-RIGHT, lifted off the ability cluster.
            if (portrait)
            {
                AnchorTopLeft(_partyStack, x: 10f, y: 160f, width: 595f,   // BELOW the wave-status cluster; −15%
                    height: PartyRowHeight * PartySlotCount + PartyRowGap * (PartySlotCount - 1));
                SetAnchors(_castleBanner,   new Vector2(0.20f, 0.955f), new Vector2(0.72f, 1f));
                SetAnchors(_waveReadout,    new Vector2(0.20f, 0.89f),  new Vector2(0.80f, 0.95f));
                SetAnchors(_resourceStrip,  new Vector2(0.48f, 0.955f), new Vector2(1f, 1f));
                // Ability cluster hugs the bottom-RIGHT corner (right-thumb arc).
                SetAnchors(_skillBar,       new Vector2(0.58f, 0.0f),   new Vector2(1.0f, 0.225f));
                // Hero vitals float on the bottom-LEFT, lifted ABOVE the joystick.
                SetAnchors(_vitalsCluster,  new Vector2(0.02f, 0.235f), new Vector2(0.46f, 0.30f));
                // Build entry lifts to the upper-right, clear of the skill cluster.
                SetAnchors(_buildBtn,       new Vector2(0.84f, 0.255f), new Vector2(0.99f, 0.33f));
                // Small "Start Next Wave" pill BESIDE the wave timer (top-left), just
                // right of the narrowed TownWave cluster. Portrait: cluster width 260px.
                SetAnchors(_startWaveBtn,   new Vector2(0.38f, 0.70f),  new Vector2(0.62f, 0.765f));   // centered BELOW the resource bar (was hidden under it)

                // WO-339 TOWN HUD portrait reflow: wave/timer cluster top-LEFT
                // narrows; resources STACK on the left under it; mini-map shrinks
                // to ~100×100 top-right; metrics bottom span wider as a 2-col feel.
                AnchorTopLeft(_townWaveCluster, x: 10f, y: 8f, width: 380f, height: 140f);   // gear-clock + plate
                SetAnchors(_townResStrip, new Vector2(0.0f, 0.74f), new Vector2(0.46f, 0.875f));   // bottom extended down for the resource icons
                if (_townMiniMap != null) { _townMiniMap.anchoredPosition = new Vector2(-10f, -10f); _townMiniMap.sizeDelta = new Vector2(100f, 100f); }
                SetAnchors(_townMetrics, new Vector2(0.148f, 0f), new Vector2(0.852f, 0.095f));   // −20%, centered, taller
            }
            else
            {
                AnchorTopLeft(_partyStack, x: 10f, y: 160f, width: 510f,   // BELOW the wave-status cluster; −15%
                    height: PartyRowHeight * PartySlotCount + PartyRowGap * (PartySlotCount - 1));
                SetAnchors(_castleBanner,   new Vector2(0.36f, 0.94f), new Vector2(0.64f, 0.99f));
                SetAnchors(_waveReadout,    new Vector2(0.36f, 0.86f), new Vector2(0.64f, 0.925f));
                SetAnchors(_resourceStrip,  new Vector2(0.76f, 0.94f), new Vector2(0.995f, 0.99f));
                // Landscape: more width — ability cluster sits tight in the corner.
                SetAnchors(_skillBar,       new Vector2(0.74f, 0.0f),   new Vector2(1.0f, 0.34f));
                // Vitals bottom-left above the (smaller) landscape joystick.
                SetAnchors(_vitalsCluster,  new Vector2(0.02f, 0.30f),  new Vector2(0.30f, 0.37f));
                SetAnchors(_buildBtn,       new Vector2(0.88f, 0.36f),  new Vector2(0.995f, 0.45f));
                // Small "Start Next Wave" pill BESIDE the wave timer (top-left), just
                // right of the TownWave cluster (x:12 + width 300px ≈ 0.29 on 1080-ref).
                SetAnchors(_startWaveBtn,   new Vector2(0.41f, 0.705f), new Vector2(0.59f, 0.77f));   // centered BELOW the resource bar (was hidden under it)

                // WO-339 TOWN HUD landscape: wide top spread — wave cluster top-left,
                // resource badges top-centre, full-size 140 mini-map top-right.
                AnchorTopLeft(_townWaveCluster, x: 12f, y: 8f, width: 380f, height: 140f);   // gear-clock + plate
                SetAnchors(_townResStrip, new Vector2(0.30f, 0.78f), new Vector2(0.70f, 1f));   // aligned L/R with the bottom bar; tall for the resource icons
                if (_townMiniMap != null) { _townMiniMap.anchoredPosition = new Vector2(-12f, -12f); _townMiniMap.sizeDelta = new Vector2(140f, 140f); }
                SetAnchors(_townMetrics, new Vector2(0.30f, 0f), new Vector2(0.70f, 0.13f));   // aligned L/R with the top bar
            }
        }

        // =====================================================================
        //  CONTEXT — village vs open world. Hide village-only chrome outside.
        // =====================================================================
        // VILLAGE-ONLY elements: Castle/Heart HP banner, Build button, Defend
        // (start-wave) button, the wave readout + repair prompt. They show ONLY
        // when the hero is inside the town ring of Village2. In the open world
        // (hero past TownRadius / a non-village active scene) they hide, leaving
        // the clean essentials: hero HP/mana + ability bar (skill bar) + party
        // frames (+ the separate CompassHud). The build MENU is opened by the
        // Build button → gated here too. Data bindings are untouched throughout.
        private void ApplyContext(bool force)
        {
            bool inVillage = EvaluateInVillage();
            if (!force && inVillage == _inVillage) return;
            _inVillage = inVillage;

            bool showVillage = inVillage || _villageOnlyForced;
            SetActiveSafe(_castleBanner, showVillage);
            SetActiveSafe(_buildBtn, showVillage);
            // WO-403: the right-edge Talk/Quest action panel is town-only (replaced by
            // the combat Skills bar out in a fight / the open world).
            SetActiveSafe(_townActionPanel, showVillage);
            // NOTE (WO-337): the wave readout (N/M + enemy count + combat status) is
            // part of the BATTLE HUD now — its visibility is driven by
            // BattleHudVisibilityManager (active-combat fade), NOT the village/world
            // context gate. Left out of this gate deliberately.
            // The Defend button is village-only AND gated by wave availability.
            SetActiveSafe(_startWaveBtn, showVillage && _startWaveAvailable);
            if (!showVillage) HideRepairPrompt();
        }

        /// <summary>
        /// True when the player is in the VILLAGE context: Village2 is the active
        /// scene AND the hero is within the town ring of the Heart (origin). Out
        /// past the ring (or any non-village scene) = open world. Hysteresis on
        /// the radius avoids show/hide flicker right at the town edge.
        /// </summary>
        private bool EvaluateInVillage()
        {
            // Town context = any recognized HOME/HUB scene (shared source). Was a single
            // "Village2" check, which hid the whole town chrome on MainCastle_Hall (WO-411
            // root cause A). Non-hub scenes (dungeon, ATB, OuterWorld field) → not village.
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && !DeNelle.Core.HubScenes.IsHub(active.name)) return false;

            ResolveHeroIfNeeded();
            if (_hero == null) return true; // no hero resolved yet → default to village (safe; shows full HUD)

            Vector3 d = _hero.position; d.y = 0f;
            float distSqr = d.sqrMagnitude;
            // Hysteresis: leaving the village needs a slightly larger radius than re-entering.
            float r = _inVillage ? (TownRadius + TownRadiusHyst) : (TownRadius - TownRadiusHyst);
            return distSqr <= r * r;
        }

        private void ResolveHeroIfNeeded()
        {
            if (_hero != null) return;
            // Reflection lookup keeps HUD→Core (no DeNelle.Village reference),
            // exactly like CompassHudBootstrap.
            if (_heroType == null)
                _heroType = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
            if (_heroType != null)
            {
                var obj = UnityEngine.Object.FindObjectOfType(_heroType) as Component;
                if (obj != null) { _hero = obj.transform; return; }
            }
            // Fallback to the Player tag if the type isn't present.
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) _hero = tagged.transform;
        }

        private static void SetActiveSafe(RectTransform rt, bool active)
        {
            if (rt != null && rt.gameObject.activeSelf != active) rt.gameObject.SetActive(active);
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorTopLeft(RectTransform rt, float x, float y, float width, float height)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        // =====================================================================
        //  IVillageHud — passive setters pushed by the Village-side bridges
        // =====================================================================
        public void SetWave(int waveNumber)
        {
            _lastWaveNumber = waveNumber;
            if (_waveText != null) _waveText.text = "WAVE " + waveNumber;
            // WO-339: feed the TOWN wave-progress readout too.
            _townWaveCur = waveNumber;
            RefreshTownWaveProgress();
        }

        public void SetCountdown(float secondsRemaining)
        {
            if (secondsRemaining > 0.1f)
            {
                _lastWaveState = "Prepare — " + secondsRemaining.ToString("0.0") + "s";
                if (_waveStateText != null) _waveStateText.text = _lastWaveState;
                // WO-339: live town countdown timer + auto lookout escalation.
                _townTimerSeconds = secondsRemaining;
                _townWaveActive = false;
                if (_lookoutStatus != 3)
                    ApplyLookout(secondsRemaining < 30f ? 2 : 1);
            }
            else
            {
                _lastWaveState = "Defend";
                if (_waveStateText != null) _waveStateText.text = _lastWaveState;
                _townTimerSeconds = 0f;
                _townWaveActive = true;
                ApplyLookout(3); // combat
            }
        }

        public void SetHeartHp(float current, float maxHp)
        {
            if (maxHp <= 0f) return;
            float pct = Mathf.Clamp01(current / maxHp);
            if (_castleFill != null) _castleFill.fillAmount = pct;
            if (_castleText != null) _castleText.text = "Heart of Elarion — " + Mathf.RoundToInt(pct * 100f) + "%";
            if (_castleFill != null) _castleFill.color = Color.Lerp(HudTheme.HpRed, HudTheme.CastleGold, Mathf.Clamp01(pct / 0.5f));
            // WO-339: town metric (Heart HP %).
            _townHeartPct = pct;
            if (_townHeartText != null)
            {
                _townHeartText.text = Mathf.RoundToInt(pct * 100f) + "%";
                // healthy endpoint = dark ink (was cream — invisible on light parchment).
                _townHeartText.color = Color.Lerp(HudTheme.HpRed, LInk, Mathf.Clamp01(pct / 0.5f));
            }
        }

        public void SetCrystals(int amount)
        {
            if (_resourceTexts != null && _resourceTexts.Length >= 4 && _resourceTexts[2] != null)
                _resourceTexts[2].text = amount.ToString();
            // WO-339 town crystal badge (index 2).
            SetTownResource(2, amount);
        }

        public void SetResources(int wood, int iron, int food, int gems)
        {
            if (_resourceTexts != null && _resourceTexts.Length >= 4)
            {
                _resourceTexts[0].text = wood.ToString();
                _resourceTexts[1].text = iron.ToString();
                _resourceTexts[2].text = gems.ToString();
                _resourceTexts[3].text = food.ToString();
            }
            // WO-339 TOWN badges — order: 0 Gold(food), 1 Wood, 2 Crystal(gems), 3 Iron.
            // (the legacy battle strip has no Gold slot; the town strip surfaces it as
            //  the existing "food" wallet bucket which is the soft currency here.)
            SetTownResource(0, food);
            SetTownResource(1, wood);
            SetTownResource(2, gems);
            SetTownResource(3, iron);
        }

        // WO-339: update one town resource badge — number, +/- flash, low-warn outline.
        private void SetTownResource(int idx, int value)
        {
            if (_townResText == null || idx < 0 || idx >= _townResText.Length || _townResText[idx] == null) return;
            _townResText[idx].text = value.ToString();

            int prev = _townResLast[idx];
            if (prev >= 0 && value != prev)
            {
                _townResFlash[idx] = 1f;
                _townResFlashUp[idx] = value > prev;
            }
            _townResLast[idx] = value;

            // red outline when this resource runs low (< 50).
            if (_townResOutline != null && _townResOutline[idx] != null)
            {
                var c = HudTheme.HpRed;
                c.a = value < TownResLowThreshold ? 0.9f : 0f;
                _townResOutline[idx].color = c;
            }
        }

        public void SetAttackDirections(bool north, bool east, bool south, bool west) { /* compass is the separate CompassHud component */ }

        public void SetWaveImminent(bool imminent)
        {
            if (_waveText != null) _waveText.color = imminent ? HudTheme.HpRed : HudTheme.Text;
            if (_waveStateText != null)
            {
                _lastWaveState = imminent ? "Horde Approaching" : "Defend";
                _waveStateText.text = _lastWaveState;
                _waveStateText.color = imminent ? HudTheme.HpRed : HudTheme.Gold;
            }
            // WO-339: escalate the town lookout pip (unless already in combat).
            if (_lookoutStatus != 3) ApplyLookout(imminent ? 2 : 0);
        }

        public void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine)
        {
            if (_waveText != null) _waveText.text = "WAVE " + waveNumber + " CLEAR";
            if (_waveStateText != null)
            {
                _waveStateText.text = enemiesDefeated > 0 ? enemiesDefeated + " slain" : "Cleared";
                _waveStateText.color = HudTheme.Gold;
            }
            // WO-339: wave cleared → exit combat lookout.
            _townWaveActive = false;
            ApplyLookout(0);
        }

        public void HideWaveClearBanner()
        {
            if (_waveText != null) _waveText.text = "WAVE " + _lastWaveNumber;
            if (_waveStateText != null) { _waveStateText.text = _lastWaveState; _waveStateText.color = HudTheme.Gold; }
            // WO-339: wave done → lookout returns to SAFE, timer awaits next countdown.
            _townWaveActive = false;
            _townTimerSeconds = -1f;
            ApplyLookout(0);
        }

        public void ShowRepairPrompt(string wallLabel, float damagePercent)
        {
            if (_repairPanel == null) return;
            if (_repairLabel != null)
                _repairLabel.text = string.Format("Repair {0}? ({1}% damaged)", wallLabel, Mathf.RoundToInt(damagePercent * 100f));
            _repairPanel.SetActive(true);
        }

        public void HideRepairPrompt()
        {
            if (_repairPanel != null) _repairPanel.SetActive(false);
        }

        public void SetForgettingLevel(float level01)
        {
            if (_rootGroup != null) _rootGroup.alpha = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(level01));
        }

        public void SetWardsReadout(int wardsLit, int wardsTotal, string summary) { /* surfaced in the Arcane Tower panel */ }

        // =====================================================================
        //  Bridge-reflected extras (not on IVillageHud — resolved by name)
        // =====================================================================

        /// <summary>
        /// Show / hide the "Defend!" start-wave button. Village-only: even when a
        /// wave is ready, the button stays hidden in the open world (the context
        /// gate ANDs availability with village context). Resolved by name.
        /// </summary>
        public void SetStartWaveAvailable(bool available)
        {
            _startWaveAvailable = available;
            // Apply through the context gate so it never shows in the open world.
            SetActiveSafe(_startWaveBtn, available && (_inVillage || _villageOnlyForced));
        }

        /// <summary>
        /// Optional override so a bridge can FORCE village-only chrome on regardless
        /// of position (e.g. a defend event triggered outside the ring). Resolved by
        /// name; harmless if never called. Pass false to return to auto context.
        /// </summary>
        public void SetVillageContextForced(bool forced)
        {
            _villageOnlyForced = forced;
            ApplyContext(force: true);
        }

        /// <summary>
        /// Show / hide the COMBAT cluster — the bottom-RIGHT ability cells
        /// (<see cref="_skillBar"/>) AND the bottom-LEFT hero HP/mana vitals
        /// (<see cref="_vitalsCluster"/>). Driven by the Village-side
        /// BuildModeHudBridge (hide on Build Enter) + the H hotkey.
        /// VISIBILITY ONLY — data bindings keep writing while hidden. By name.
        /// </summary>
        public void SetCombatHudVisible(bool visible)
        {
            _combatHudVisible = visible;
            if (_skillBar != null && _skillBar.gameObject.activeSelf != visible)
                _skillBar.gameObject.SetActive(visible);
            if (_vitalsCluster != null && _vitalsCluster.gameObject.activeSelf != visible)
                _vitalsCluster.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Show / hide the ENTIRE village HUD (wave/lookout/resources/heart banner +
        /// the combat clusters) by fading the root CanvasGroup. Used by full-screen
        /// modal overlays (Arena recruit / defense setup) so the live town/wave HUD
        /// doesn't bleed through and clutter the modal. VISIBILITY ONLY — every data
        /// binding keeps writing while hidden, so the HUD is correct on restore.
        /// Resolved BY NAME (reflection) from the Village-side Arena controllers — it
        /// is a HUD extra, not on the IVillageHud interface (same decoupling contract
        /// as SetCombatHudVisible). Idempotent + harmless if _rootGroup is null.
        /// </summary>
        public void SetHudVisible(bool visible)
        {
            if (_rootGroup == null) return;
            _rootGroup.alpha = visible ? 1f : 0f;
            _rootGroup.interactable = visible;
            _rootGroup.blocksRaycasts = visible;
        }

        // =====================================================================
        //  WO-339 · TOWN-HUD data setters (by-name reflected extras, NOT on
        //  IVillageHud — same decoupling contract as SetStartWaveAvailable). The
        //  Village-side bridges may call these to enrich the town readouts; all
        //  are harmless no-ops if never wired. Core/Village asmdefs unchanged.
        // =====================================================================

        /// <summary>Town wave progress — "Wave N / M" label + bar fill. By name.</summary>
        public void SetWaveProgress(int current, int total)
        {
            _townWaveCur = current;
            _townWaveMax = total;
            RefreshTownWaveProgress();
        }

        private void RefreshTownWaveProgress()
        {
            if (_townWaveProgText != null)
                _townWaveProgText.text = _townWaveMax > 0
                    ? "Wave " + _townWaveCur + " / " + _townWaveMax
                    : "Wave " + _townWaveCur;
            if (_townWaveProgFill != null)
                _townWaveProgFill.fillAmount = _townWaveMax > 0
                    ? Mathf.Clamp01((float)_townWaveCur / _townWaveMax) : 0f;
        }

        /// <summary>
        /// Lookout status pip: 0 SAFE(green) · 1 ALERT(yellow) · 2 INCOMING(red,
        /// &lt;30s) · 3 COMBAT(purple). Auto-driven by SetCountdown/SetWaveImminent;
        /// a bridge can also push it explicitly by name. (3 latches until cleared.)
        /// </summary>
        public void SetLookoutStatus(int status) => ApplyLookout(status);

        private void ApplyLookout(int status)
        {
            _lookoutStatus = status;
            if (_townLookoutBadge == null) return;
            // No plate text (implied by the clock + overlapped the timer). The plate just reddens
            // with urgency as a silent state cue; the clock face carries the countdown.
            _townLookoutBadge.color = (status >= 2) ? HudTheme.LookoutIncoming : Color.white;
            if (_townLookoutText != null) _townLookoutText.text = string.Empty;
        }

        /// <summary>
        /// Town metrics strip: Heart HP % (0..1, or -1 to keep the SetHeartHp value),
        /// towers built/max, population. By name; any field harmless if unwired.
        /// </summary>
        public void SetTownMetrics(float heartPct01, int towersBuilt, int towersMax, int population)
        {
            if (heartPct01 >= 0f)
            {
                _townHeartPct = Mathf.Clamp01(heartPct01);
                if (_townHeartText != null) _townHeartText.text = Mathf.RoundToInt(_townHeartPct * 100f) + "%";
            }
            _townTowersBuilt = towersBuilt;
            _townTowersMax = towersMax;
            _townPopulation = population;
            if (_townTowerText != null) _townTowerText.text = towersBuilt + "/" + Mathf.Max(towersBuilt, towersMax);
            if (_townPopText != null) _townPopText.text = population.ToString();
        }

        /// <summary>
        /// WO-361 — passive-XP badge: "⚡ Towers earning N XP/min" where N is the
        /// aggregate idle XP rate (towerCount × per-tower XP/min, computed by the feed).
        /// Compact + auto-toggling: hidden when there are no towers or a zero rate, shown
        /// otherwise. Resolved by name (reflection) from the Village-side TownHudBridge.
        /// </summary>
        public void SetPassiveXp(int xpPerMin, int towerCount)
        {
            bool show = _townPassiveXpVisible && towerCount > 0 && xpPerMin > 0;
            if (_townPassiveXp != null) _townPassiveXp.gameObject.SetActive(show);
            if (show && _townPassiveXpText != null)
                _townPassiveXpText.text = "⚡ Towers earning " + xpPerMin + " XP/min";
        }

        /// <summary>Toggle the passive-XP badge on/off (compact mode / player preference).</summary>
        public void SetPassiveXpVisible(bool visible)
        {
            _townPassiveXpVisible = visible;
            if (!visible && _townPassiveXp != null) _townPassiveXp.gameObject.SetActive(false);
        }

        /// <summary>
        /// Add a static mini-map POI marker by world position (shop/forge/warehouse,
        /// resource node, enemy gate, …). kind picks the glyph+tint. By name; the
        /// hero dot is added automatically. Call <see cref="ClearMinimapPois"/> first
        /// to rebuild. A RenderTexture mini-map is a FUTURE upgrade (this is the
        /// lightweight 2D-icon version, WebGL-safe).
        /// </summary>
        public void SetMinimapPoi(string kind, float worldX, float worldZ)
        {
            string k = string.IsNullOrEmpty(kind) ? "poi" : kind.ToLowerInvariant();
            string glyph; Color tint;
            switch (k)
            {
                case "shop":      glyph = "$"; tint = HudTheme.GoldRes; break;
                case "forge":     glyph = "F"; tint = HudTheme.Iron;    break;
                case "warehouse": glyph = "W"; tint = HudTheme.Wood;    break;
                case "tree":
                case "resource":  glyph = "^"; tint = HudTheme.LookoutSafe; break;
                case "gate":
                case "enemy":     glyph = "x"; tint = HudTheme.LookoutIncoming; break;
                default:          glyph = "*"; tint = HudTheme.TextDim; break;
            }
            AddMiniMapMarker(k + "_" + _miniMarkers.Count, new Vector3(worldX, 0f, worldZ), tint, glyph, isHero: false);
        }

        /// <summary>Clear all mini-map markers EXCEPT the hero dot (rebuild support).</summary>
        public void ClearMinimapPois()
        {
            for (int i = _miniMarkers.Count - 1; i >= 0; i--)
            {
                var m = _miniMarkers[i];
                if (m == null || m.IsHero) continue;
                if (m.Rect != null) Destroy(m.Rect.gameObject);
                _miniMarkers.RemoveAt(i);
            }
        }

        public void SetComboCount(int count)
        {
            if (_comboText == null) return;
            if (count <= 1)
            {
                _comboText.text = "";
                _lastCombo = count;
                return;
            }
            _comboText.text = count + "× COMBO";
            if (count > _lastCombo) PopMomentum();
            _lastCombo = count;
        }

        public void SetKillStreak(int streak)
        {
            if (_streakText == null) return;
            if (streak <= 1)
            {
                _streakText.text = "";
                _lastStreak = streak;
                return;
            }
            _streakText.text = streak + "× KILLS";
            if (streak > _lastStreak) PopMomentum();
            _lastStreak = streak;
        }

        private void PopMomentum()
        {
            _momentumPop = 1f;
            _momentumHold = 1.1f;
        }

        public void SetEnemyCount(int live, int total)
        {
            if (_enemyCountText == null) return;
            if (total <= 0)
            {
                _enemyCountText.text = "";
                return;
            }
            _enemyCountText.text = live + " / " + total + " enemies";
            float clearPct = total > 0 ? 1f - Mathf.Clamp01((float)live / total) : 0f;
            _enemyCountText.color = Color.Lerp(HudTheme.TextDim, HudTheme.Gold, clearPct);
        }

        /// <summary>Live mana bar — pushed every frame by HeroAbilitiesHudBridge.</summary>
        public void SetMana(float current, float max)
        {
            if (_manaFill != null) _manaFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (_manaText != null) _manaText.text = Mathf.RoundToInt(current) + "/" + Mathf.RoundToInt(max);
        }

        /// <summary>Live hero HP bar — pushed every frame by HeroAbilitiesHudBridge.</summary>
        public void SetHeroHp(float current, float max)
        {
            _hpCurrent = current;
            _hpMax = max > 0f ? max : 1f;
            if (_hpFill != null) _hpFill.fillAmount = Mathf.Clamp01(_hpCurrent / _hpMax);
            if (_hpText != null) _hpText.text = Mathf.RoundToInt(current) + "/" + Mathf.RoundToInt(max);
            // Re-resolve the hero name/portrait once the class loads (in case it wasn't ready at build).
            if (_partyName != null && _partyName.Length > 0 && _partyName[0] != null && _partyName[0].text == "Hero")
                RefreshHeroPortrait();
            SetPartyMember(0, _partyName != null && _partyName[0] != null ? _partyName[0].text : "Hero", current, max);
        }

        /// <summary>Per-slot cooldown sweep (radial drains as the ability returns).</summary>
        public void SetAbilityCooldown(int slot, float remaining, float total)
        {
            if (_slotCooldown == null || slot < 0 || slot >= _slotCooldown.Length || _slotCooldown[slot] == null) return;
            float fill = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            _slotCooldown[slot].fillAmount = fill;
            if (_slotCdFill != null) _slotCdFill[slot] = fill;
            bool ready = fill <= 0.001f;
            // LIGHT: dark ink when ready, muted ink while on cooldown (was cream/dim).
            if (_slotName != null && _slotName[slot] != null)
                _slotName[slot].color = ready ? LInk : LInkDim;
            if (_slotKey != null && _slotKey[slot] != null)
                _slotKey[slot].color = ready ? LInk : LInkDim;
        }

        /// <summary>Per-class ability cell content (key/glyph/name) — 5-arg path.</summary>
        public void SetAbilitySlot(int slot, string key, string glyph, string name, string description)
        {
            SetAbilitySlot(slot, key, glyph, name, description, null);
        }

        /// <summary>Per-class ability cell content + accent colour — 6-arg path (preferred).</summary>
        public void SetAbilitySlot(int slot, string key, string glyph, string name, string description, string accentHex)
        {
            if (slot < 0 || slot >= AbilitySlotCount) return;
            if (_slotKey != null && _slotKey[slot] != null && !string.IsNullOrEmpty(key)) _slotKey[slot].text = key;
            if (_slotGlyph != null && _slotGlyph[slot] != null) _slotGlyph[slot].text = string.IsNullOrEmpty(glyph) ? "?" : glyph;
            // Real ability art (by hero class + slot) wins over the glyph when present.
            if (_slotIcon != null && _slotIcon[slot] != null)
            {
                var svc = DeNelle.Core.State.GameStateService.Instance;
                var hc = svc != null && svc.State != null ? svc.State.HeroClass : DeNelle.Core.State.HeroClassOpt.None;
                var iconKey = AbilityIconForClassSlot(hc, slot);
                var sp = iconKey != null ? WidgetSprite(iconKey) : null;
                if (sp != null)
                {
                    _slotIcon[slot].sprite = sp;
                    _slotIcon[slot].color = Color.white;
                    if (_slotGlyph != null && _slotGlyph[slot] != null) _slotGlyph[slot].text = "";
                }
            }
            if (_slotName != null && _slotName[slot] != null)
                _slotName[slot].text = string.IsNullOrEmpty(name) ? "" : name;
            if (_slotAccent != null && _slotAccent[slot] != null && !string.IsNullOrEmpty(accentHex)
                && ColorUtility.TryParseHtmlString(accentHex, out var c))
            {
                c.a = 0.85f;
                _slotAccent[slot].color = c;
            }
        }

        // Per-class ability icon by slot (Q/W/E/R). Names match the staged Resources/HudIcons/<Class>/
        // art; WidgetSprite is null-safe so a miss falls back to the glyph.
        private static string AbilityIconForClassSlot(DeNelle.Core.State.HeroClassOpt hc, int slot)
        {
            switch (hc)
            {
                case DeNelle.Core.State.HeroClassOpt.Knight:
                    return slot == 0 ? "Knight/Knight_Charge" : slot == 1 ? "Knight/knight_parry"
                         : slot == 2 ? "Knight/Knight_Cleave" : "Knight/knight_thrust";
                case DeNelle.Core.State.HeroClassOpt.Mage:
                    return slot == 0 ? "Wizard/Wizard_Plasma" : slot == 1 ? "Wizard/Wizard_Fireball"
                         : slot == 2 ? "Wizard/Wizard_Lightining" : "Wizard/Wizard_Meteor";
                case DeNelle.Core.State.HeroClassOpt.Ranger:
                    return slot == 0 ? "Ranger/Ranger_Ranged_Attack" : slot == 1 ? "Ranger/Ranger_Barrage"
                         : slot == 2 ? "Ranger/Ranger_Poison_Arrow" : "Ranger/ranger_rapid_fire";
                case DeNelle.Core.State.HeroClassOpt.Cleric:
                    return slot == 0 ? "Healer/Healer_Heal" : slot == 1 ? "Healer/Healer_Group_Heal"
                         : slot == 2 ? "Healer/Healer_Holy" : "Healer/Healer_Smite";
                default: return null;
            }
        }

        /// <summary>Party-frame setter (slot 0 = hero). Pushed by a Village-side bridge.</summary>
        public void SetPartyMember(int slot, string name, float current, float max)
        {
            if (_partyFrame == null || slot < 0 || slot >= _partyFrame.Length) return;
            if (_partyFrame[slot] != null) _partyFrame[slot].SetActive(true);
            if (_partyName != null && _partyName[slot] != null && !string.IsNullOrEmpty(name)) _partyName[slot].text = name;
            float pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (_partyHpFill != null && _partyHpFill[slot] != null) _partyHpFill[slot].fillAmount = pct;
            if (_partyHpText != null && _partyHpText[slot] != null) _partyHpText[slot].text = Mathf.RoundToInt(current) + "/" + Mathf.RoundToInt(max);
        }

        public void SetPartyMemberVisible(int slot, bool visible)
        {
            if (_partyFrame == null || slot < 0 || slot >= _partyFrame.Length || _partyFrame[slot] == null) return;
            _partyFrame[slot].SetActive(visible);
        }

        // =====================================================================
        //  Kit coherence helpers — bring the HUD onto the shared ElarionUiKit
        //  visual language (matches the freshly-styled inventory) WITHOUT moving
        //  anything. Pure construction/decoration; no data/layout change.
        // =====================================================================

        /// <summary>
        /// Frame an existing panel GameObject the way the inventory's panels read:
        /// dark-glass rounded fill (HudTheme.StylePanel) + the soft gold bottom
        /// underline (HudTheme.AddRim) + the kit's crisp inner hairline rim
        /// (ElarionUiKit.AddInnerRim) — that inner rim is the depth cue the old
        /// flat HUD panels lacked. All three are non-raycast decorative children,
        /// so behaviour is unchanged. Mirrors ElarionUiKit.Panel's framed look on
        /// a pre-anchored rect.
        /// </summary>
        private static void FramePanel(GameObject go, Color fill)
        {
            HudTheme.StylePanel(go, fill);
            ElarionUiKit.AddInnerRim(go, ElarionUiKit.AccentSoft);
            HudTheme.AddRim(go, HudTheme.AccentSoft);
        }

        // =====================================================================
        //  Tiny uGUI helpers
        // =====================================================================
        private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4, 2);
            rt.offsetMax = new Vector2(-4, -2);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }
    }
}
