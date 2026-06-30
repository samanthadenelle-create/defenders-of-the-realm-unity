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

using System.Collections;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;   // TGVRU §12 — FlowTrace/Guard: a dropped HUD push self-reports, never silent
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
        // ROOT-CAUSE BRIDGE (T-022 "no inventory"): the per-instance InventoryRequested above is
        // recreated EVERY scene load — VillageHudController is NOT DontDestroyOnLoad; the bootstrap
        // re-spawns a fresh controller (and a fresh `new UnityEvent()`) on each hub load (see
        // VillageHudBootstrap header "We do NOT DontDestroyOnLoad the host"). The Village-side bridge
        // (HeroEquipHud, a DontDestroyOnLoad singleton) re-binds via reflection self-heal, but the
        // hand-off is timing-fragile across the destroy-old / spawn-new HUD swap on entering
        // MainCastle_Hall — when it binds to the dying HUD's event the BAG fires into the void and
        // OpenInventory never runs (verified: no [HeroEquipHud] log on tap). A STATIC event is
        // instance-independent: the bridge subscribes ONCE and every HUD instance's BAG fires it, so
        // the link can never go stale across a HUD re-instance. The BAG raises BOTH (belt-and-braces).
        public static event System.Action InventoryRequestedStatic;
        public static void RaiseInventoryRequested() => InventoryRequestedStatic?.Invoke();
        public UnityEvent QuestsRequested = new UnityEvent();      // QUESTS → quest modal (follow-up; dimmed for now)
        public UnityEvent IntelRequested = new UnityEvent();       // far top-right (periscope) → enemy scout report / lookout
        public UnityEvent RaidRequested = new UnityEvent();        // top-right (crossed swords) → enter raids (RaidEntryBridge wires the entry, WO-457)
        public UnityEvent RallyRequested = new UnityEvent();       // WO-457: raid combat HUD → arm a rally-point tap (a Village bridge sets TroopRally.Point + flag)
        public UnityEvent RetreatRequested = new UnityEvent();     // WO-457: raid combat HUD → pull the warband out (a Village bridge calls RaidDeployController retreat)
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

        // WO-563: the OLD battle-HUD wave readout (_waveText/_waveStateText/_enemyCountText)
        // was REMOVED — the town wave cluster (_townTimerText/_townWaveProgText/lookout) is the
        // sole wave readout and the 9-zone owns the in-battle chrome. _lastWaveNumber/_lastWaveState
        // are kept (the town readout + state strings still use them).
        private int _lastWaveNumber = 1;
        private string _lastWaveState = "Defend";

        // Castle (Heart) HP — top-centre (VILLAGE-ONLY).
        private Image _castleFill;
        private TextMeshProUGUI _castleText;

        // Hero HP state — fed by SetHeroHp; the OLD vitals/skill-bar HP widgets were removed
        // (WO-563). These plain floats are still read by ApplyCombatGate (heroHurt gate) and
        // pushed onward to the party frame, so they stay.
        private float _hpCurrent, _hpMax = 1f;

        // ── WO-541 Stage 3a: live Core HUD-context gate ──────────────────────────
        // Hide the party-frame stack (the Knight HP/MP card) in the Battle context so it
        // no longer DUPLICATES BattleHud9Zone's own canonical battle hero card. Read from
        // the live HudContextModel via CoreServices.HudModel (registered by HudModelHost
        // AFTER scene load — may be null when this HUD builds). Degrades to the existing
        // ApplyCombatGate behaviour when the model is unavailable.
        private DeNelle.Core.HudModel.IHudModel _hookedHudModel;
        private DeNelle.Core.HudModel.HudContext _hudCtx = DeNelle.Core.HudModel.HudContext.Town;
        // WO-410: change-gate per-frame HUD string rebuilds (timer + enemy count) so the
        // TMP mesh only regenerates when the displayed integer actually changes.
        private int _lastTimerTotal = int.MinValue, _lastLive = int.MinValue, _lastTotal = int.MinValue;

        // WO-563: the hero XP yellow line lived inside the removed OLD vitals cluster, so it
        // is gone with it (UpdateHeroXpLine + the HeroProgression reflection poll were removed).

        // ── Wisdom skill-tree badge (owner 2026-06-24) ───────────────────────────
        // A small, non-intrusive corner badge that ANNOUNCES unspent Wisdom + is the
        // entry point to the skill tree. Replaces the retired level-up allocate popup
        // (LevelUpSkillPopup): hidden/dim when no unspent Wisdom; appears + gently
        // pulses when Wisdom > 0; tap → PanelRouter.Open(PanelId.HeroSkillTree). Only
        // in town/exploration (hidden during the arena battle). Wisdom is read by
        // reflection (HUD→Core asmdef can't reference DeNelle.Village.Talents), matching
        // the XP-line reflection pattern. Polled on the same cadence as the XP line.
        private RectTransform _wisdomBadge;       // the badge rect (pulsed scale/alpha)
        private CanvasGroup _wisdomBadgeGroup;     // alpha fade (hidden when no Wisdom)
        private object _wisdomSvc;                 // cached WisdomCurrencyService.Instance (reflection)
        private System.Type _wisdomSvcType;
        private System.Reflection.PropertyInfo _wisdomProp;   // WisdomCurrencyService.Wisdom (int)
        private float _wisdomPollTimer;
        private int _unspentWisdom;                // last polled unspent Wisdom
        private float _wisdomPulse;                // 0..1 pulse phase driver
        // Tunables — position/size/pulse (kept here so the badge is easy to nudge).
        private const float WisdomBadgeInsetX = 20f;   // from the right edge
        private const float WisdomBadgeInsetY = 150f;  // from the top (below the gear/raid cluster)
        private const float WisdomBadgeSize = 72f;     // small corner badge
        private const float WisdomPulseSpeed = 3.2f;   // pulse cycles/sec driver
        private const float WisdomPollInterval = 0.25f;

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

        // ── T-004/T-015: combat-vs-hub gate for the party/vitals/mana cluster ──────
        // The combat party-frame stack + hero vitals (mana "10/10", green party-HP fill)
        // must NOT show in the non-combat castle hub (they read as a context-free green
        // bar). We gate them ON only while a wave is actively counting down or fighting
        // (WaveManager.Phase == Countdown(1) or Active(2)), read by reflection in
        // PollWaveTimer (HUD→Core; WaveManager is in DeNelle.Village — never edited here).
        // Default OFF so a hub with no armed wave loop shows no combat cluster.
        private int _wavePhaseCached;        // last reflected WaveManager phase (0 Idle,1 Countdown,2 Active,…)
        private bool _waveCombatActive;      // derived: phase is Countdown or Active
        private bool _lastCombatGate;        // last applied gate (avoid redundant SetActive churn)

        // WO-563: the OLD skill-bar ability cells (Q/W/E/R discs + cooldown rings) were REMOVED —
        // the 9-zone battle HUD (BattleHud9Zone) owns the in-battle ability bar now. The Set*
        // ability setters below are kept as no-ops so existing Village-side pushes don't break.

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

        // WO-421 RC2: the ward "forgetting" ambiance dim. SetForgettingLevel only
        // STORES the target here; Update() applies it conditionally + lerped so the
        // hero plate / vitals never wash out while combat is live (see ApplyForgettingDim).
        private float _forgettingLevel;       // 0 = wards lit, 1 = deep / forgotten
        private float _rootDimAlpha = 1f;     // current eased root alpha for the dim
        private bool _hudHiddenByModal;       // SetHudVisible(false) — modal owns root alpha

        // ── WO-563: the OLD WO-337 BATTLE-HUD group was REMOVED ───────────────
        // It hosted the legacy combat clusters (abilities, hero vitals, wave/enemy
        // readout, momentum badge) faded by BattleHudVisibilityManager. The owner
        // kept the NEW 9-zone battle HUD (BattleHud9Zone) and removed this old group.
        // The BattleHudGroup property + _battleCanvas/_battleHudGroup fields are gone.

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
        // WO-563: _waveReadout (OLD battle wave readout container) removed with the battle HUD.
        private RectTransform _castleBanner;
        private RectTransform _partyStack;
        // WO-563: _skillBar (bottom-right abilities) + _vitalsCluster (bottom-left HP/mana) were
        // removed with the OLD battle HUD — the 9-zone owns them now.
        private RectTransform _buildBtn;
        private RectTransform _startWaveBtn;
        private bool _startWaveAvailable;
        private bool _isPortrait = true;
        private int _lastScreenW, _lastScreenH;

        private bool _combatHudVisible = true;
        private bool _built;
        // TGVRU: latched true if Build threw (HUD is partial) — lets every Set* push tell a
        // single missing element apart from a whole-HUD build failure when it self-reports.
        private bool _hudBuildFailed;

        // ── WO-339: TOWN-HUD widgets ──────────────────────────────────────────
        // Top-left WAVE MANAGEMENT cluster.
        private RectTransform _townWaveCluster;
        private RectTransform _townWavePlate;        // dark pointed name-plate (Start-Wave button parents here)
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
        private TextMeshProUGUI[] _townResText;      // 0 Food,1 Wood,2 Crystal,3 Iron,4 Gold
        private Image[] _townResBadge;               // badge bg (for low-warn red outline / flash)
        private Image[] _townResOutline;             // red low-warning outline overlay
        private int[] _townResLast = { -1, -1, -1, -1, -1 };
        private float[] _townResFlash = { 0f, 0f, 0f, 0f, 0f };
        private bool[] _townResFlashUp = { false, false, false, false, false };
        private const int TownResLowThreshold = 50;
        // ── WO-572: gain-flash THROTTLE — stop the passive-drip strobe ───────────
        // The echo workforce / harvest faucet banks small amounts (+1..+few) very
        // frequently; flashing the badge green on EVERY increase made the resource
        // HUD strobe (owner F8 2026-06-28). We split increases into two classes:
        //   • DISCRETE gain (single update ≥ ResGainBigDelta — wave reward, sell, big
        //     node extract): flash green INSTANTLY, every time (keep the nice feedback).
        //   • DRIP gain (small per-tick increment): COALESCE — accumulate and fire ONE
        //     flash only when the trickle PAUSES (no new gain for ResGainCoalesceWindow).
        //     A continuous fast faucet refreshes the window every tick → it never
        //     expires while flowing → NO strobe; one gentle pulse once it settles.
        // Spends (decreases) are untouched — they still flash red immediately.
        private readonly int[] _townResGainAccum = { 0, 0, 0, 0, 0 };      // pending coalesced up-gain
        private readonly float[] _townResGainWindow = { 0f, 0f, 0f, 0f, 0f }; // quiet-gap countdown (s)
        private const int ResGainBigDelta = 8;            // single-update gain that flashes instantly
        private const float ResGainCoalesceWindow = 0.6f; // quiet gap before a coalesced drip flashes once
        // Gold is the LAST town resource cell (Food/Wood/Crystal/Iron/Gold). It is a
        // currency, not a gatherable stock — it must never raise the red low-warn box
        // (it legitimately starts < 50 and otherwise paints a solid red box over the coin).
        private const int TownResGoldIndex = 4;

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
        private bool _raidSceneActive;              // WO-457: active scene is a RaidBase_* raid
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
            if (!go.TryGetComponent(out Image img)) img = go.AddComponent<Image>();
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
        private const string IconBuySell      = "hud_gold";     // TKT-15 reskin: the vendor button reads BUY/SELL (gold coin), not "Talk"
        private const string IconQuest        = "hud_quest";
        private const string IconBuild        = "hud_build";    // standalone Resources/HudIcons/hud_build (tower)
        private const string IconUpgrade      = "Upgrade";      // standalone Resources/HudIcons/Upgrade.png (owner-made); glyph fallback below
        private const string IconIntel        = "hud_intel";    // standalone Resources/HudIcons/hud_intel (periscope/lookout)
        private const string IconRaid         = "hud_raid_2";   // standalone Resources/HudIcons/hud_raid_2 (crossed swords → enter raids)
        private const string IconHeart        = "hud_heart";    // standalone Resources/HudIcons/hud_heart (Heart-of-Elarion crest)
        private const string IconHarvest      = "hud_harvest";  // WO-555: standalone Resources/HudIcons/hud_harvest (Echo silo); glyph fallback below
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
                FlowTrace.Warn("HUD", "HUD-icon load failed for " + HudIconSheet + ": " + e.Message +
                    " — falling back to code-drawn glyphs.");
            }
            if (subs != null)
                for (int i = 0; i < subs.Length; i++)
                {
                    var sp = subs[i];
                    if (sp != null && !string.IsNullOrEmpty(sp.name)) _hudIcons[sp.name] = sp;
                }
            if (_hudIcons.Count == 0)
                FlowTrace.Once("HUD", "no-hud-icons",
                    "no HUD-widget sprites under Resources/HudIcons — run Defenders/Art/Slice HUD Icons. " +
                    "Falling back to code-drawn glyphs.");
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
            if (_hookedHudModel != null)
            {
                _hookedHudModel.Context.Changed -= OnHudContextChanged;
                _hookedHudModel = null;
            }
        }

        // WO-541 Stage 3a: CoreServices.HudModel registers AFTER scene load; this HUD may
        // build first. Poll until available, then subscribe + apply once. Re-hook if the
        // model instance is replaced (host re-spawn). Cheap (one ref compare/frame).
        private void HookHudContext()
        {
            var hm = CoreServices.HudModel;
            if (hm != null && !ReferenceEquals(hm, _hookedHudModel))
            {
                if (_hookedHudModel != null) _hookedHudModel.Context.Changed -= OnHudContextChanged;
                _hookedHudModel = hm;
                _hookedHudModel.Context.Changed += OnHudContextChanged;
                OnHudContextChanged();   // apply-immediately so a mid-context build gates correctly
            }
        }

        private void OnHudContextChanged()
        {
            var hm = CoreServices.HudModel;
            if (hm == null) return;
            _hudCtx = hm.Context.Context;
            DeNelle.Core.Diagnostics.FlowTrace.Step("HUD",
                $"VillageHudController gate context={_hudCtx} partyFrameHiddenInBattle={(_hudCtx == DeNelle.Core.HudModel.HudContext.Battle)}");
            // Re-apply the combat gate now so the party stack flips the instant context changes
            // (don't wait for the next UpdateTownHud tick).
            ApplyCombatGate();
        }

        private void Start()
        {
            // WO-334: never let a build-time exception blank the HUD or halt the
            // player. If Build throws, log it and keep whatever was constructed.
            // TGVRU (§12): the build is GUARDED so a single element throwing self-reports
            // (FlowTrace.Fail -> break-log) instead of shipping a silently-partial HUD via
            // a Debug-only line. _hudBuildFailed latches so every later Set* push can tell
            // a missing element apart from "the whole HUD failed to build".
            using var _ = FlowTrace.Enter("HUD", "VillageHudController.Start");
            try
            {
                Build();
                ApplyResponsiveLayout(force: true);
                ApplyContext(force: true);
                FlowTrace.Step("HUD", "WO-334 sleek/minimal context-aware HUD built + active.");
            }
            catch (System.Exception e)
            {
                // ROLLBACK SIGNAL: the HUD is now partial — Fail-loud so a run self-reports
                // it (and the per-setter Once-Fails below can name it as the root cause), and
                // verify what (if anything) actually came up so the capture splits
                // "nothing built" from "built but one cluster threw".
                _hudBuildFailed = true;
                FlowTrace.Fail("HUD",
                    $"HUD BUILD FAILED (HUD is PARTIAL — Village->HUD pushes may drop): {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                VerifyHudBuilt();
            }
        }

        // TGVRU verify (mirrors HeroArmorVisual.VerifyArmorRendersNow): after a build attempt,
        // prove which top-level clusters actually came up so a capture pinpoints the dead one
        // instead of guessing. A null core element on a NON-failed build is itself a Warn-once
        // (the cluster's builder silently produced nothing).
        private void VerifyHudBuilt()
        {
            int present = 0, missing = 0;
            void Check(string what, object element)
            {
                bool ok = !(element is null) && !(element is Object o && o == null);
                if (ok) present++;
                else
                {
                    missing++;
                    FlowTrace.Once("HUD", "build-missing:" + what,
                        $"VerifyHudBuilt: HUD element '{what}' is null after build — " +
                        (_hudBuildFailed ? "build threw (HUD partial)." : "its builder produced nothing; pushes to it will drop."));
                }
            }
            Check("hudCanvas", _hudCanvas);
            Check("rootGroup", _rootGroup);
            Check("castleFill", _castleFill);
            // WO-563: waveText/hpFill/skillBar belonged to the removed OLD battle HUD — not checked.
            Check("partyFrame", _partyFrame);
            Check("resourceTexts", _resourceTexts);
            FlowTrace.Step("HUD", $"VerifyHudBuilt: {present} present, {missing} missing (buildFailed={_hudBuildFailed}).");
        }

        // TGVRU: a setter writing to a HUD element that may be null (HUD didn't build, or the
        // owning cluster's builder produced nothing) routes through here so a DROPPED Village->HUD
        // push self-reports ONCE per setter (keyed by name) instead of vanishing silently. Returns
        // true when the target is missing (caller has already no-op'd). Per-frame-safe via Once.
        private bool ReportMissingTarget(string setter, string element)
        {
            FlowTrace.Once("HUD", "drop:" + setter,
                $"{setter}: target '{element}' is null — Village->HUD push DROPPED " +
                (_hudBuildFailed ? "(HUD build failed — partial)." : "(element didn't build / cluster gated off)."));
            return true;
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

            // WO-541 Stage 3a: hook the live Core HUD context once HudModelHost registers it.
            HookHudContext();

            // WO-563: AnimateMomentumBadge + UpdateHeroXpLine removed with the OLD battle HUD.
            AnimateLookoutBell();
            UpdateTownHud();
            UpdateWisdomBadge();
            ApplyForgettingDim();
        }

        // WO-421 RC2: apply the ward "forgetting" ambiance dim to the root group,
        // but ONLY out of combat — and eased, never a hard snap. While combat is
        // live (the BATTLE-HUD group is shown) hold the root at full alpha so the
        // hero plate / vitals stay bright in deep OuterWorld where no wards are lit.
        private void ApplyForgettingDim()
        {
            if (_rootGroup == null) return;
            // A full-screen modal (SetHudVisible) owns the root alpha while open — don't fight it.
            if (_hudHiddenByModal) return;

            // Combat is "live" when the Core HUD context says Battle (WO-563: the old
            // _battleHudGroup alpha probe is gone with that group).
            bool combatLive = _hudCtx == DeNelle.Core.HudModel.HudContext.Battle;

            // Out of combat the dim follows the forgetting level (1 → 0.55); in combat
            // it eases back to full so the hero plate never reads washed out.
            float target = combatLive ? 1f : Mathf.Lerp(1f, 0.55f, _forgettingLevel);
            _rootDimAlpha = Mathf.MoveTowards(_rootDimAlpha, target, 0.8f * Time.unscaledDeltaTime);
            _rootGroup.alpha = _rootDimAlpha;
        }

        // WO-563: UpdateHeroXpLine + ResolveHeroProgIfNeeded were removed with the OLD vitals
        // cluster (which hosted the hero XP yellow line). The skill tree / progression UI now
        // surfaces XP; the in-HUD line is gone.

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
            if (_waveMgr == null || _wavePhaseProp == null || _waveCountdownProp == null)
            {
                // No wave loop present (e.g. a bare hub) → not in combat, gate the
                // party/vitals cluster OFF and leave the idle clock blank.
                _wavePhaseCached = 0;
                _waveCombatActive = false;
                _townWaveActive = false;
                return;
            }

            try
            {
                // WavePhase: Idle=0, Countdown=1, Active=2 (see DeNelle.Village.WavePhase).
                // Compare by int so we don't need the Village enum type at HUD compile time.
                int phase = System.Convert.ToInt32(_wavePhaseProp.GetValue(_waveMgr));
                _wavePhaseCached = phase;
                // T-004/T-015: combat cluster is gated ON only during an ACTIVE wave loop
                // (Countdown or Active). Idle/Complete/Breached/Defeated → hub, gate OFF.
                _waveCombatActive = phase == 1 || phase == 2;

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
                else if (phase == 2) // Active — wave in combat
                {
                    _townWaveActive = true;
                }
                else
                {
                    // T-022: no live countdown (Idle/Complete/…). CountdownRemaining can still
                    // read >0 right after EnterCountdown; surface it so the timer shows a value
                    // the instant the loop arms instead of staying blank. Otherwise clear it so
                    // the idle clock face reads empty (owner: no center word when idle).
                    float remaining = (float)_waveCountdownProp.GetValue(_waveMgr);
                    _townTimerSeconds = remaining > 0.05f ? remaining : -1f;
                    _townWaveActive = false;
                }
            }
            catch (System.Exception e)
            {
                // TGVRU: no silent catch (§12). Normally a reload tearing the manager down, but a
                // reflection/enum-shape mismatch would also land here and silently kill the town
                // timer — Warn so a real binding break self-reports. Throttled (hot poll).
                FlowTrace.Throttle("HUD", "wavetimer-read", 2f,
                    $"PollWaveTimer: WaveManager read threw ({e.GetType().Name}: {e.Message}) — re-resolving next tick.");
                _waveMgr = null;
            }
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

        // ── T-004/T-015: show the combat party-frame + hero vitals/mana ONLY in combat. ─
        // The party stack (slot 0 hero + companions) and the bottom-left vitals cluster
        // (mana "10/10" + XP line) are COMBAT chrome — in the idle castle hub they read
        // as a context-free green bar. We gate them on `_waveCombatActive` (Countdown or
        // Active wave) AND the existing `_combatHudVisible` build-mode flag. Visibility
        // only — every data binding keeps writing while hidden, so the HUD is correct on
        // restore (same contract as SetCombatHudVisible).
        private void ApplyCombatGate()
        {
            // WO-428: HeroHealth deals contact damage EVERY frame regardless of wave phase,
            // so gate the vitals/party cluster on "wave active OR hero hurt" — otherwise the
            // HP bar is hidden exactly when it changes (hero damaged in the idle hub = bar
            // never appears to move). Still hidden at full HP in a quiet hub (the T-004 intent).
            bool heroHurt = _hpMax > 0f && _hpCurrent < _hpMax - 0.01f;
            // WO-457: a RaidBase_* scene IS combat — the village wave loop's phase flag
            // (_waveCombatActive) is never set there, so OR the raid-scene flag in so the
            // vitals cluster (mana/XP) + party frames show during a raid the same as a wave.
            bool show = (_waveCombatActive || _raidSceneActive || heroHurt) && _combatHudVisible;
            // Owner: hero + companion HEALTH BARS should be visible in TOWN, not only in combat.
            // Show the party stack (HP bars/portraits) in the hub OR combat (incl. raids); keep the
            // bottom-left vitals cluster (mana/XP — the context-free bit T-004 hid) combat-only.
            bool showParty = (InVillage || show) && _combatHudVisible;
            // WO-541 Stage 3a: in the BATTLE context, BattleHud9Zone owns the canonical battle
            // hero card — hiding the base-canvas party frame here kills the DUPLICATE Knight card.
            // Town/Overworld/Modal keep the party frame (companion HP bars) as before. When the
            // Core HUD model is unavailable, _hudCtx stays Town so behaviour is unchanged.
            if (_hudCtx == DeNelle.Core.HudModel.HudContext.Battle) showParty = false;
            // WO-563: the bottom-left vitals cluster was removed; this gate now drives only the
            // party-frame stack (companion HP bars on the base canvas).
            if (show == _lastCombatGate
                && _partyStack != null && _partyStack.gameObject.activeSelf == showParty) return;
            _lastCombatGate = show;
            SetActiveSafe(_partyStack, showParty);
        }

        // ── WO-339: per-frame TOWN-HUD animation (timer urgency, res flash, map). ─
        private void UpdateTownHud()
        {
            float dt = Time.unscaledDeltaTime;

            // Keep the countdown sourced live from WaveManager (fallback to the
            // SetCountdown push path when no manager is present).
            PollWaveTimer();

            // T-004/T-015: gate the combat party-frame + hero vitals/mana cluster to
            // an active wave only (Countdown/Active). In the non-combat castle hub they
            // would otherwise show a context-free green party-HP bar + "10/10" mana.
            ApplyCombatGate();

            // Context action button: swap Quest <-> Upgrade by building proximity (owner
            // 2026-06-20). Cheap — only re-skins the button when the focus state flips.
            RefreshContextActionButton();

            // Countdown timer text + urgency colour (only when a wave is pending).
            if (_townTimerText != null)
            {
                if (_townWaveActive)
                {
                    _townTimerText.text = "IN WAVE";
                    _townTimerText.color = HudTheme.LookoutCombat;
                    _lastTimerTotal = int.MinValue;
                }
                else if (_townTimerSeconds >= 0f)
                {
                    int total = Mathf.Max(0, Mathf.CeilToInt(_townTimerSeconds));
                    if (total != _lastTimerTotal)   // WO-410: reformat only when the second ticks, not every frame
                    {
                        _lastTimerTotal = total;
                        _townTimerText.text = string.Format("{0:00}:{1:00}", total / 60, total % 60);   // MM:SS only — the clock face has the NEXT WAVE etch
                    }
                    _townTimerText.color = _townTimerSeconds < 10f ? HudTheme.LookoutIncoming
                        : _townTimerSeconds < 30f ? HudTheme.LookoutAlert : LInk;
                }
                else
                {
                    _townTimerText.text = string.Empty;   // idle: clock alone, no center word (owner)
                    _lastTimerTotal = int.MinValue;
                }
            }

            // WO-572: coalesced drip-gain flush — when a passive trickle PAUSES (no new
            // gain for ResGainCoalesceWindow), fire ONE green flash for the net gain. A
            // continuous faucet keeps re-arming the window in SetTownResource so this
            // never fires while resources are actively flowing → no per-tick strobe.
            for (int i = 0; i < _townResGainWindow.Length; i++)
            {
                if (_townResGainWindow[i] <= 0f) continue;
                _townResGainWindow[i] -= dt;
                if (_townResGainWindow[i] <= 0f && _townResGainAccum[i] > 0)
                {
                    _townResFlash[i] = 1f;
                    _townResFlashUp[i] = true;
                    DeNelle.Core.Diagnostics.FlowTrace.Step("Eco",
                        $"ResFlash coalesced drip gain idx{i} +{_townResGainAccum[i]} -> single flash (per-tick strobe suppressed)");
                    _townResGainAccum[i] = 0;
                    _townResGainWindow[i] = 0f;
                }
            }

            // Resource +/- flash fade.
            if (_townResBadge != null)
            {
                for (int i = 0; i < _townResBadge.Length; i++)
                {
                    if (_townResFlash[i] <= 0f || _townResBadge[i] == null) continue;
                    _townResFlash[i] = Mathf.Max(0f, _townResFlash[i] - dt * 2.2f);
                    // The badge's rest colour is TRANSPARENT (the wood strip bar frames the
                    // row now, line ~1994). Lerp from that transparent base — NOT from LParch
                    // — so a value update flashes a tinted glow that FADES BACK TO CLEAR and
                    // never leaves a permanent near-white parchment box behind the number.
                    Color flash = _townResFlashUp[i] ? HudTheme.LookoutSafe : HudTheme.HpRed;
                    Color rest = new Color(flash.r, flash.g, flash.b, 0f);
                    _townResBadge[i].color = Color.Lerp(rest, flash, _townResFlash[i] * 0.6f);
                }
            }

            // Mini-map marker projection (world → map). Hero marker tracks the hero.
            ProjectMiniMap();

            // WO-339: the TOWN HUD supersedes the legacy top resource strip + castle
            // banner whenever it's visible (avoids a double resource bar / HP banner).
            // When the town group fades out (BATTLE / exploration) the legacy strip +
            // banner return so combat still shows resources + Heart HP as before.
            bool townShown = _townHudGroup != null && _townHudGroup.alpha > 0.5f;

            // WO-507 (owner 2026-06-23: "the currency is still on the battle map — don't
            // need it there"): inside an ISOLATED BattleArena encounter the town economy
            // chrome has no meaning and clutters the fight. BattleLock.IsInBattle() is the
            // Core-clean signal that the arena fight is live (HUD -> Core only). When in a
            // battle, FORCE the legacy currency strip + castle banner + compass OFF so the
            // arena is clean (the 9-zone HUD owns the combat readout). They restore the
            // instant the battle ends (this gate runs per-frame). Note: the town-resource
            // badges + mini-map live in _townHudGroup, which the visibility manager already
            // fades out in Battle mode — only these base-canvas widgets leaked through.
            bool inArenaBattle = DeNelle.Core.Combat.BattleLock.IsInBattle();

            SetActiveSafe(_resourceStrip, !townShown && !inArenaBattle);
            // Compass is base-canvas top chrome (never in either CanvasGroup) — hide it in
            // battle so the arena top-centre is clear for the 9-zone family overview.
            if (inArenaBattle) SetActiveSafe(_compassWidget, false);
            else SetActiveSafe(_compassWidget, true);
            // Castle banner is ALSO village-context gated (ApplyContext); only override
            // it OFF while the town HUD owns the readout OR a battle is live, never force
            // it on outside.
            if (townShown || inArenaBattle) SetActiveSafe(_castleBanner, false);
            else if (_inVillage || _villageOnlyForced) SetActiveSafe(_castleBanner, true);

            // WO-563: the legacy double-HUD suppression is gone — the OLD _battleHudGroup it
            // hid no longer exists. The NEW 9-zone HUD (BattleHud9Zone) is the only battle HUD
            // and gates its own canvas on the Core HudContext, so there is nothing to suppress.
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

        // WO-563: AnimateMomentumBadge was removed with the OLD battle HUD's momentum badge.

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
            // Failsafe (HUD-disappearing/BAG-dead/controls-frozen): a fresh HUD must ALWAYS
            // start visible + interactive. If an Arena setup screen latched SetHudVisible(false)
            // (alpha 0 + blocksRaycasts off) and didn't restore, a respawn could otherwise
            // inherit a suppressed look. Explicitly reset so the base HUD can't come up dead.
            _rootGroup.alpha = 1f; _rootGroup.interactable = true; _rootGroup.blocksRaycasts = true;

            // Safe-area root — all clusters live under this so notch/rounded
            // corners never clip the HUD on phones.
            _safeArea = NewRect("SafeArea", go.transform, Vector2.zero, Vector2.one);
            ApplySafeArea();

            // WO-563: the OLD WO-337 BATTLE-HUD canvas/group + its four clusters (wave readout,
            // momentum badge, vitals cluster, skill bar) were REMOVED. The NEW 9-zone battle HUD
            // (BattleHud9Zone) is the sole battle HUD, spawned per-battle by BattleArenaHud (arena)
            // and BattleHudVisibilityManager (enemy-owned + raid scenes).

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

            // Owner 2026-06-24: small pulsing skill-tree badge (unspent-Wisdom announcement
            // + skill-tree entry point). Base chrome (always-on canvas); gated to town/
            // exploration + hidden during the arena battle in its poll. Replaces the retired
            // level-up allocate popup.
            BuildWisdomBadge(_safeArea);

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

            // WO-563: the OLD battle clusters (BuildWaveReadout / BuildMomentumBadge /
            // BuildVitalsCluster / BuildSkillBar) are no longer built — the 9-zone HUD owns combat.

            // Self-reveal: the freshly-built HUD fades in from alpha 0 → 1 over ~0.3s
            // so it animates onto the screen on load instead of popping in hard.
            AnimateIn();
        }

        /// <summary>
        /// Reveal the HUD with a short fade-in. Starts the root CanvasGroup at alpha 0
        /// and eases to 1 over ~0.3s. Coroutine-driven on unscaled time (no DOTween
        /// dependency, WebGL-safe, no reflection). Idempotent + null-safe.
        /// </summary>
        public void AnimateIn()
        {
            if (_rootGroup == null) return;
            if (!isActiveAndEnabled) { _rootGroup.alpha = 1f; return; }   // can't run a coroutine while disabled — just show
            _rootGroup.alpha = 0f;
            StopCoroutine(nameof(FadeInHud));
            StartCoroutine(FadeInHud());
        }

        private IEnumerator FadeInHud()
        {
            const float dur = 0.3f;
            float t = 0f;
            while (t < dur && _rootGroup != null)
            {
                t += Time.unscaledDeltaTime;
                _rootGroup.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            if (_rootGroup != null) _rootGroup.alpha = 1f;
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
            // Decorative compass widget THROWN OUT (owner 2026-06-30): the functional
            // CompassHud (NSEW heading + enemy-bearing ticks, its own overlay canvas) is the
            // single compass now, repositioned to sit right below the resource strip. We leave
            // the _compassWidget field null on purpose — SetActiveSafe(_compassWidget, …) in
            // ApplyContext null-guards, so the (now-absent) icon is simply never shown.

            // Mobile-first (keyboard-removal sweep): the Escape key was the SOLE trigger for
            // BOTH "close the open modal" and "toggle pause". On a phone there is no Escape,
            // so add an always-on on-screen PAUSE/BACK button (top-LEFT — clear of the
            // top-right gear cluster and the town-only wave cluster). It routes through the
            // Core PauseGate: an open modal closes (PanelManager.CloseOpen), else pause
            // toggles (PauseController, via PauseGate.PauseToggleRequested) — behaviour-
            // identical to the retired Escape handler, now reachable by TAP. The "||" glyph
            // fallback shows until a hud_pause sprite is dropped in.
            var pauseCell = NewRect("PauseBack", parent, new Vector2(0f, 1f), new Vector2(0f, 1f));
            pauseCell.anchorMin = new Vector2(0f, 1f);
            pauseCell.anchorMax = new Vector2(0f, 1f);
            pauseCell.pivot = new Vector2(0f, 1f);
            pauseCell.anchoredPosition = new Vector2(20f, -20f);
            pauseCell.sizeDelta = new Vector2(96f, 96f);
            BuildIconButton(pauseCell, Vector2.zero, Vector2.one,
                "hud_pause", "||", () => PauseGate.RequestBack());

            // Top-right icon cluster — Settings gear + Inventory backpack.
            var cluster = NewRect("TopRightIcons", parent, new Vector2(1f, 1f), new Vector2(1f, 1f));
            cluster.anchorMin = new Vector2(1f, 1f);
            cluster.anchorMax = new Vector2(1f, 1f);
            cluster.pivot = new Vector2(1f, 1f);
            // Drop below the top resource strip band so the gear/backpack never
            // overlap the resources (battle) or the compass row (town).
            cluster.anchoredPosition = new Vector2(-55f, -55f);   // inset ≈ resource-bar height from top + right
            cluster.sizeDelta = new Vector2(410f, 135f);   // widened from 280 → fits THREE evenly-spaced icons (gear · intel · raid)

            // Owner 2026-06-23: INTEL HIDDEN (phase-2 feature) and SETTINGS pushed to the far-right
            // corner (phase-2-adjacent / de-emphasized). Raid moves to the left slot. Two icons now;
            // the IntelRequested event stays declared for when intel returns in phase 2.
            // Left = enter RAIDS (crossed-swords icon; glyph fallback until art lands).
            BuildIconButton(cluster, new Vector2(0f, 0f), new Vector2(0.31f, 1f),
                IconRaid, "x", () => RaidRequested?.Invoke());
            // MIDDLE = ECHO HARVEST panel toggle (owner F8 2026-06-28, WO-555). The offline/echo
            // harvest readout used to be ALWAYS-ON top-left chrome; it's a side thought, so it now
            // lives in a tucked-away Obsidian panel opened by this button, sat right next to the
            // Settings gear. Routes through the Core HarvestPanelGate seam (HUD never references the
            // Village panel that owns the UI, §5). Glyph "Y" fallback until hud_harvest art lands.
            BuildIconButton(cluster, new Vector2(0.35f, 0f), new Vector2(0.65f, 1f),
                IconHarvest, "Y", () => HarvestPanelGate.RequestToggle());
            // FAR top-right = SETTINGS gear → Help/Settings menu (Report bug, Controls, Dev tools[dev], Credits).
            BuildIconButton(cluster, new Vector2(0.69f, 0f), new Vector2(1f, 1f),
                IconSettings, "*", () => HelpMenu.Instance?.ToggleOverlay());
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

        // ── Wisdom skill-tree badge (owner 2026-06-24) ───────────────────────────
        // A minimal top-right corner badge: a sprite-first skill-tree icon (IconTree,
        // glyph fallback) inside a CanvasGroup we fade in/out + a rect we gently pulse.
        // Hidden when no unspent Wisdom; on tap opens the Knight skill tree. Built on the
        // base canvas so it shows in town/exploration; PollWisdomBadge() gates it OFF in
        // the arena battle. Starts hidden (alpha 0) until the first poll finds Wisdom > 0.
        private void BuildWisdomBadge(Transform parent)
        {
            var cell = NewRect("WisdomSkillBadge", parent, new Vector2(1f, 1f), new Vector2(1f, 1f));
            cell.pivot = new Vector2(1f, 1f);
            cell.anchoredPosition = new Vector2(-WisdomBadgeInsetX, -WisdomBadgeInsetY);
            cell.sizeDelta = new Vector2(WisdomBadgeSize, WisdomBadgeSize);
            _wisdomBadge = cell;

            // Fade group — alpha 0 hides it cleanly (no Wisdom = invisible + not clickable).
            _wisdomBadgeGroup = cell.gameObject.AddComponent<CanvasGroup>();
            _wisdomBadgeGroup.alpha = 0f;
            _wisdomBadgeGroup.interactable = false;
            _wisdomBadgeGroup.blocksRaycasts = false;

            // Transparent click target (raycast on a clear Image still receives taps).
            var seat = cell.gameObject.AddComponent<Image>();
            seat.color = new Color(0f, 0f, 0f, 0f);
            seat.sprite = HudTheme.Disc;

            // Sprite-first skill-tree icon (glyph fallback) inset inside the cell.
            AddWidgetIcon("Glyph", cell, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f),
                IconTree, "Y", 30, LGilt);

            var btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = seat;
            HudTheme.StyleButtonColors(btn, LPortrait);
            btn.onClick.AddListener(OpenSkillTree);
        }

        // Tap → open the Knight skill tree (HUD → Core router; the badge is both the
        // unspent-Wisdom announcement AND the door to spend it).
        private static void OpenSkillTree()
        {
            PanelRouter.Open(PanelId.HeroSkillTree);
        }

        // Poll unspent Wisdom (reflection, HUD → Core) + gate visibility to town/
        // exploration (hidden in the arena battle) + drive the gentle pulse. Called from
        // Update on the XP-line cadence.
        private void UpdateWisdomBadge()
        {
            if (_wisdomBadge == null || _wisdomBadgeGroup == null) return;

            _wisdomPollTimer -= Time.unscaledDeltaTime;
            if (_wisdomPollTimer <= 0f)
            {
                _wisdomPollTimer = WisdomPollInterval;
                ResolveWisdomSvcIfNeeded();
                if (_wisdomSvc != null && _wisdomProp != null)
                {
                    try { _unspentWisdom = (int)_wisdomProp.GetValue(_wisdomSvc); }
                    catch (System.Exception e)
                    {
                        FlowTrace.Throttle("HUD", "wisdom-read", 2f,
                            $"UpdateWisdomBadge: Wisdom read threw ({e.GetType().Name}: {e.Message}) — re-resolving next tick.");
                        _wisdomSvc = null;
                    }
                }
            }

            // Show only in town/exploration with unspent Wisdom; never during the arena battle.
            bool inBattle = DeNelle.Core.Combat.BattleLock.IsInBattle();
            bool show = _unspentWisdom > 0 && !inBattle;

            float targetAlpha = show ? 1f : 0f;
            float a = Mathf.MoveTowards(_wisdomBadgeGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 4f);
            _wisdomBadgeGroup.alpha = a;
            bool live = a > 0.5f;
            _wisdomBadgeGroup.interactable = live;
            _wisdomBadgeGroup.blocksRaycasts = live;

            if (show)
            {
                // Gentle pulse: a subtle scale swing (+ a touch of alpha shimmer) saying
                // "go spend skill points whenever" — calm, not attention-grabbing.
                _wisdomPulse += Time.unscaledDeltaTime * WisdomPulseSpeed;
                float swing = Mathf.PingPong(_wisdomPulse, 1f);
                float scale = 1f + 0.10f * swing;
                _wisdomBadge.localScale = new Vector3(scale, scale, 1f);
                _wisdomBadgeGroup.alpha = Mathf.Min(a, Mathf.Lerp(0.78f, 1f, swing));
            }
            else if (_wisdomBadge.localScale.x != 1f)
            {
                _wisdomPulse = 0f;
                _wisdomBadge.localScale = Vector3.one;
            }
        }

        private void ResolveWisdomSvcIfNeeded()
        {
            if (_wisdomSvc != null) return;
            if (_wisdomSvcType == null)
                _wisdomSvcType = System.Type.GetType(
                    "DeNelle.Village.Talents.WisdomCurrencyService, DeNelle.Village");
            if (_wisdomSvcType == null) return;

            var instProp = _wisdomSvcType.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            object svc = instProp != null ? instProp.GetValue(null) : null;
            if (svc == null) svc = UnityEngine.Object.FindObjectOfType(_wisdomSvcType);
            if (svc == null) return;

            _wisdomSvc = svc;
            _wisdomProp = _wisdomSvcType.GetProperty("Wisdom");
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
        private Button _upgradeButton;   // HUD shortcut to the building Upgrade panel (was the quest button)
        private AttentionGlowUi _upgradeGlow;   // chasing-comet cue around the Upgrade button (like Talk)
        private void BuildTownActionPanel(Transform parent)
        {
            // TOWN ACTIONS diamond: BUILD · TALK · BAG · QUESTS. Owner 2026-06-20: moved to the
            // bottom-CENTER 9-slice (mobile-first natural thumb zone) — it was bottom-RIGHT and
            // overlapped the right-side quest tracker pin. Centred + lifted off the bottom edge.
            _townActionPanel = NewRect("TownActions", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            _townActionPanel.anchorMin = new Vector2(0.5f, 0f);
            _townActionPanel.anchorMax = new Vector2(0.5f, 0f);
            _townActionPanel.pivot = new Vector2(0.5f, 0f);
            _townActionPanel.anchoredPosition = new Vector2(0f, 20f);
            _townActionPanel.sizeDelta = new Vector2(300f, 300f);   // square footprint for a compact DIAMOND cluster

            // DIAMOND layout (mobile-thumb-friendly, bottom-right corner):
            // BUILD top · TALK left · BAG right · QUESTS bottom.
            BuildIconButton(_townActionPanel, new Vector2(0.30f, 0.56f), new Vector2(0.70f, 0.98f),
                IconBuild, "B", () => BuildRequested?.Invoke());
            // TKT-15 REVERTED (owner 2026-06-21): this is the TALK button (talk icon, "T" glyph). The
            // earlier coin/BUY-SELL reskin was a misunderstanding. It fires TalkRequested, which
            // TalkHudBridge routes to the in-range vendor — Talk opens the vendor DIALOGUE (Buy/Sell/
            // Leave, plus quest options when a questline is active). Upgrade is its OWN affordance (the
            // bottom-diamond context toggle), so Talk never short-circuits to the upgrade screen.
            _talkButton = BuildIconButton(_townActionPanel, new Vector2(0.02f, 0.29f), new Vector2(0.42f, 0.71f),
                IconTalk, "T", () =>
                {
                    FlowTrace.Step("HUD", "Talk button tapped -> raising TalkRequested (routes to nearest vendor dialogue).");
                    TalkRequested?.Invoke();
                });
            // T-010/T-016 (the "black shape under the Talk icon"): Talk is the only icon button
            // that gets DISABLED (when no NPC is in range). Unity's ColorTint then paints the
            // button's targetGraphic (the seat, which carries the HudTheme.Disc sprite) with the
            // dark disabledColor -> a visible black disc under the icon. Dimming is already done
            // via the CanvasGroup in SetTalkAvailable, so disable the ColorTint transition here so
            // the disabled state never tints the seat dark. interactable still gates the tap.
            _talkButton.transition = UnityEngine.UI.Selectable.Transition.None;
            // BAG → InventoryRequested. Same shared treatment as its BUILD/TALK/QUESTS
            // siblings: BuildIconButton (gilt ring seat + HudTheme.StyleButtonColors) with
            // the IconInventory chest sprite from RpgUiCatalog (RoleIcons → icon_inventory).
            // Glyph fallback "I" (Inventory) is a legible mnemonic in the sibling-initial
            // convention (B/T) for when the RPG sprite pack isn't imported — was a stray "G".
            BuildIconButton(_townActionPanel, new Vector2(0.58f, 0.29f), new Vector2(0.98f, 0.71f),
                IconInventory, "I", () =>
                {
                    FlowTrace.Step("HUD", "BAG tapped -> raising InventoryRequested (instance + static).");
                    InventoryRequested?.Invoke();        // legacy per-instance event (Village bridge self-heal)
                    RaiseInventoryRequested();           // instance-independent static bridge (never goes stale)
                });
            // Bottom of the diamond: CONTEXT button (owner 2026-06-20). It TOGGLES between
            // QUESTS and UPGRADE by proximity — Upgrade (focused on that building) while the
            // hero stands next to an upgradable, not-maxed building; otherwise the Quest/Rumor
            // board. One button, one persistent tap handler that reads the live focus; only the
            // icon + glow swap (RefreshContextActionButton, driven from UpdateTownHud). Built in
            // the default Quest state; the first town tick swaps it if a building is already in
            // reach. Glyph fallbacks: "^" Upgrade / "!" Quest until the sprites import.
            _upgradeButton = BuildIconButton(_townActionPanel, new Vector2(0.30f, 0.02f), new Vector2(0.70f, 0.44f),
                IconQuest, "!", OnContextActionTapped);
            // Chasing-comet attention cue — same cue Talk uses, but ONLY shown in Upgrade mode
            // (an actionable building is in reach). Starts hidden; RefreshContextActionButton
            // toggles it. Quest mode = no comet (the board is always available, not a prompt).
            _upgradeGlow = AttentionGlowUi.Attach((RectTransform)_upgradeButton.transform,
                new Color(1f, 0.85f, 0.35f, 1f), HudTheme.Disc);
            if (_upgradeGlow != null)
            {
                _upgradeGlow.transform.SetAsLastSibling();
                _upgradeGlow.gameObject.SetActive(false);   // default Quest mode: comet off
            }

            // Reusable chasing-comet attention cue around the Talk button (also for tutorial focusing).
            _talkGlow = AttentionGlowUi.Attach((RectTransform)_talkButton.transform,
                new Color(1f, 0.85f, 0.35f, 1f), HudTheme.Disc);
            // T-016: render the comet ABOVE the Talk icon art so it isn't hidden under it
            // (the owner reported the spinning comet "missing"). Non-raycast, so it never
            // eats the Talk tap.
            if (_talkGlow != null) _talkGlow.transform.SetAsLastSibling();

            SetTalkAvailable(false);   // gated until a talkable NPC is in range
            RefreshContextActionButton();   // set the initial Quest/Upgrade face from current focus
        }

        // ── Context action button (owner 2026-06-20) ────────────────────────────
        // The bottom diamond button toggles Quest <-> Upgrade by proximity. The focus
        // (which upgradable building, if any, the hero is next to) is the cross-assembly
        // HudBuildingFocus signal that BuildingInteractable sets/clears. We only swap the
        // icon + glow when the mode flips; the tap handler is persistent and reads the
        // live focus at click time, so no per-frame listener churn.
        private bool _ctxUpgradeMode;   // current face: true = Upgrade, false = Quest
        private bool _ctxModeInit;      // false until the first RefreshContextActionButton runs

        /// <summary>Persistent tap handler — routes to the building Upgrade panel (focused on
        /// the in-range building) when one is in reach, else the real Quest/Rumor board.</summary>
        private void OnContextActionTapped()
        {
            string id = DeNelle.Core.UI.HudBuildingFocus.CurrentBuildingId;
            var customUpgrade = DeNelle.Core.UI.HudBuildingFocus.CurrentUpgradeAction;
            if (customUpgrade != null)
            {
                // Tower-upgrade consolidation (owner 2026-06-27): a focused TOWER injects its
                // cost-enforced Tower.TryUpgrade through HudBuildingFocus, so the SAME context
                // button runs the tower transaction (no HUD->Village coupling, no panel).
                FlowTrace.Step("HUD", "Context button -> custom upgrade action (focus='" + (id ?? "<none>") + "').");
                customUpgrade();
            }
            else if (!string.IsNullOrEmpty(id))
            {
                FlowTrace.Step("HUD", "Context button -> Building Upgrade (focus='" + id + "').");
                PanelRouter.Open(PanelId.BuildingUpgrade, id);
            }
            else
            {
                FlowTrace.Step("HUD", "Context button -> Quest/Rumor board.");
                if (!PanelRouter.Open(PanelId.RumorBoard))
                    FlowTrace.Warn("HUD", "RumorBoard opener not registered — quest board unreachable from HUD.");
            }
        }

        /// <summary>Re-evaluate the context button face from the live building focus and
        /// swap the icon (sprite-first, glyph fallback) + comet only when the mode flips.
        /// Cheap no-op when nothing changed; called every town tick from UpdateTownHud.</summary>
        private void RefreshContextActionButton()
        {
            if (_upgradeButton == null) return;
            bool upgrade = !string.IsNullOrEmpty(DeNelle.Core.UI.HudBuildingFocus.CurrentBuildingId);
            if (_ctxModeInit && upgrade == _ctxUpgradeMode) return;   // unchanged — skip the swap
            _ctxModeInit = true;
            _ctxUpgradeMode = upgrade;
            FlowTrace.Step("HUD", "Context button face -> " + (upgrade ? "UPGRADE" : "QUEST") +
                " (focus='" + (DeNelle.Core.UI.HudBuildingFocus.CurrentBuildingId ?? "<none>") + "')");

            // Swap the art on the button's "Glyph" child (built by AddWidgetIcon under the cell).
            // Sprite-first; if the pack art is missing, fall back to the code glyph
            // ("^" Upgrade / "!" Quest).
            var iconRt = _upgradeButton.transform.Find("Glyph");
            if (iconRt != null)
            {
                var img = iconRt.GetComponent<Image>();
                var txt = iconRt.GetComponentInChildren<TextMeshProUGUI>();
                string widget   = upgrade ? IconUpgrade : IconQuest;
                string fallback = upgrade ? "^" : "!";
                bool hasArt = img != null && TrySetWidget(img, widget);
                if (img != null && !hasArt) { img.color = new Color(0f, 0f, 0f, 0f); img.raycastTarget = false; }
                if (txt != null)
                {
                    txt.gameObject.SetActive(!hasArt);
                    if (!hasArt) txt.text = fallback;
                }
            }

            // Comet invites the Upgrade action only (a building is in reach); Quest = no comet.
            if (_upgradeGlow != null) _upgradeGlow.gameObject.SetActive(upgrade);
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
                // T-013/T-014: shrink-to-fit so large totals don't overflow / overlap the
                // neighbouring cell. One line, auto-size down.
                _resourceTexts[i].enableAutoSizing = true;
                _resourceTexts[i].fontSizeMin = 13f; _resourceTexts[i].fontSizeMax = 26f;
                _resourceTexts[i].enableWordWrapping = false;
                _resourceTexts[i].overflowMode = TextOverflowModes.Ellipsis;
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
            // MOCKUP ALIGN (hud_mobile_combat): the Heart-of-Elarion objective bar sits
            // TOP-LEFT, directly under the resource strip, in the SHARED dark-glass +
            // ornate-gold-rune-frame + GREEN-fill language (NOT the light parchment skin).
            // Final anchors are (re)set in ApplyResponsiveLayout; these are placeholders.
            _castleBanner = NewRect("CastleBanner", parent, new Vector2(0.0f, 0.90f), new Vector2(0.40f, 0.955f));

            // Tree-of-Life crest, tucked top-left of the bar — sprite-FIRST (hud_heart /
            // hud_tree widget art), world-tree glyph fallback. Gilt on the dark glass.
            AddWidgetIcon("Crest", _castleBanner, new Vector2(0.0f, 0.40f), new Vector2(0.11f, 1.0f),
                IconHeart, "*", HudTheme.FontHead, HudTheme.Gilt);

            // "Heart of Elarion" caption above the bar — spaced gilt label (cream/gilt is
            // legible on the dark glass, matching the town HUD's header treatment).
            var caption = NewRect("Caption", _castleBanner, new Vector2(0.11f, 0.62f), new Vector2(0.99f, 1.05f));
            var cap = AddText(caption, "HEART OF ELARION", HudTheme.FontLabel, HudTheme.Gilt, TextAlignmentOptions.MidlineLeft);
            cap.fontStyle = FontStyles.Bold;
            cap.characterSpacing = 3f;
            cap.outlineColor = new Color32(8, 6, 4, 200);
            cap.outlineWidth = 0.08f;

            // The objective HP bar — built through the SHARED kit (BarKind.Castle = ornate
            // gold gem-socket frame + GREEN-tinted fill, dark recessed track) so it reads
            // as the same designed game as the town HUD. _castleFill keeps its dynamic
            // red↔gold lerp from SetHeartHp (we don't pin the fill colour here).
            var bar = ElarionUiKit.Bar(_castleBanner.transform, ElarionUiKit.BarKind.Castle,
                new Vector2(0.11f, 0.06f), new Vector2(0.99f, 0.58f), withValue: true);
            _castleFill = bar.fill;
            _castleText = (TextMeshProUGUI)bar.valueLabel;
            // TGVRU: the kit returned no fill -> SetHeartHp can't drive the Heart objective bar.
            // Warn-once so a "Heart HP never moves" self-reports instead of a silent dead bar.
            if (_castleFill == null)
                FlowTrace.Once("HUD", "build-castle-fill-null",
                    "BuildCastleBanner: ElarionUiKit.Bar(Castle) returned a null fill — Heart HP bar will not render.");
            if (_castleText != null) _castleText.text = "Heart of Elarion — 100%";
        }

        // ── Wave readout — floating minimal text, no panel. ───────────────────
        // WO-563: BuildWaveReadout + BuildMomentumBadge were REMOVED with the OLD battle HUD.
        // The town wave cluster is the sole wave readout; combo/kill-streak momentum is gone.

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
            // TGVRU: party-frame art is the glyph-fallback class — a null sprite means the row
            // reads as a procedural dark-glass panel (the inline else-branch handles the look).
            // Warn-once so "blank/un-arted party frame" self-reports (pack not imported / typo'd key).
            if (frameSprite == null)
                FlowTrace.Once("HUD", "build-party-frame-sprite-null",
                    "BuildPartyFrames: 'player_frame_bg' sprite null — party rows use the procedural fallback frame.");

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
                else
                {
                    // MOCKUP ALIGN: no frame art → dark glass + ornate gilt rune rim so the
                    // row reads as an ornate gold-framed party frame (the shared language),
                    // not a flat dark panel.
                    frImg.color = ElarionUiKit.GlassDeep;
                    frImg.sprite = HudTheme.RoundedFrame;
                    frImg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                    ElarionUiKit.AddInnerRim(frame.gameObject, ElarionUiKit.Accent);
                }
                frImg.raycastTarget = false;
                _partyFrame[i] = frame.gameObject;

                // Portrait (class image) in the circle on the left, ringed by an ornate
                // gilt frame so it matches the mockup's circular gold-framed portraits.
                var portWrap = NewRect("PortraitWrap", frame, new Vector2(0.035f, 0.12f), new Vector2(0.26f, 0.94f));
                var port = NewRect("Portrait", portWrap, Vector2.zero, Vector2.one);
                var pimg = port.gameObject.AddComponent<Image>();
                pimg.raycastTarget = false; pimg.preserveAspect = true; pimg.color = Color.white;
                _partyPortrait[i] = pimg;
                // Gilt ring frame over the portrait (sprite-first ring from the kit; inert
                // when the ring sprite build fails under WebGL).
                var ring = NewRect("PortraitRing", portWrap, Vector2.zero, Vector2.one);
                var ringImg = ring.gameObject.AddComponent<Image>();
                ringImg.sprite = ElarionUiKit.RingSprite;
                ringImg.color = new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b,
                    ElarionUiKit.RingSprite != null ? 1f : 0f);
                ringImg.raycastTarget = false;
                ring.SetAsLastSibling();

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
                // MOCKUP ALIGN: party HP fills read GREEN (the defender vitals colour in
                // hud_mobile_combat) when there's no HP-fill art; the art wins when present.
                hfimg.sprite = hpSprite; hfimg.color = hpSprite != null ? Color.white : ElarionUi.Affordable;
                hfimg.type = Image.Type.Filled; hfimg.fillMethod = Image.FillMethod.Horizontal; hfimg.fillOrigin = 0; hfimg.fillAmount = 1f;
                hfimg.raycastTarget = false;
                _partyHpFill[i] = hfimg;
                _partyHpText[i] = AddText(hpTrack, "", 11, HudTheme.Text, TextAlignmentOptions.Center);
                _partyHpText[i].outlineColor = new Color32(40, 16, 16, 200); _partyHpText[i].outlineWidth = 0.14f;
                // T-004: tiny "HP" caption so the green party fill is never context-free.
                var hpCap = AddText(hpTrack, "HP", 9, HudTheme.Gilt, TextAlignmentOptions.MidlineLeft);
                ((RectTransform)hpCap.transform).anchorMin = new Vector2(0.02f, 0f);
                ((RectTransform)hpCap.transform).anchorMax = new Vector2(0.22f, 1f);
                ((RectTransform)hpCap.transform).offsetMin = Vector2.zero; ((RectTransform)hpCap.transform).offsetMax = Vector2.zero;
                hpCap.fontStyle = FontStyles.Bold; hpCap.outlineColor = new Color32(40, 16, 16, 200);
                hpCap.outlineWidth = 0.14f; hpCap.raycastTarget = false;

                // MP bar (blue, lower-right).
                var mpTrack = NewRect("MPTrack", frame, new Vector2(0.31f, 0.07f), new Vector2(0.985f, 0.27f));
                var mpFill  = NewRect("MPFill", mpTrack, Vector2.zero, Vector2.one);
                var mfimg = mpFill.gameObject.AddComponent<Image>();
                mfimg.sprite = mpSprite; mfimg.color = mpSprite != null ? Color.white : new Color(0.30f, 0.50f, 0.95f);
                mfimg.type = Image.Type.Filled; mfimg.fillMethod = Image.FillMethod.Horizontal; mfimg.fillOrigin = 0; mfimg.fillAmount = 1f;
                mfimg.raycastTarget = false;
                _partyMpFill[i] = mfimg;
                // T-004: tiny "MP" caption on the blue bar.
                var mpCapP = AddText(mpTrack, "MP", 9, HudTheme.Gilt, TextAlignmentOptions.MidlineLeft);
                ((RectTransform)mpCapP.transform).anchorMin = new Vector2(0.02f, 0f);
                ((RectTransform)mpCapP.transform).anchorMax = new Vector2(0.22f, 1f);
                ((RectTransform)mpCapP.transform).offsetMin = Vector2.zero; ((RectTransform)mpCapP.transform).offsetMax = Vector2.zero;
                mpCapP.fontStyle = FontStyles.Bold; mpCapP.outlineColor = new Color32(10, 18, 44, 200);
                mpCapP.outlineWidth = 0.14f; mpCapP.raycastTarget = false;

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
            // SINGLE-HERO: slot 0 IS the player's hero — it must NOT show a COMPANION name
            // (Grom/Thrain/Sylas/Elara are the recruited companions, NameForClass). Use the
            // CLASS LABEL (Knight/Mage/Ranger/Cleric). The hero's real display name is a
            // later owner/data choice (player-named or data-driven); until then show the class.
            if (_partyName != null && _partyName.Length > 0 && _partyName[0] != null)
                _partyName[0].text = HeroNameForClass(hc);
        }

        // SINGLE-HERO: the HERO portrait label (slot 0). Returns the CLASS label, never a
        // companion identity. The hero's real display name is a later owner/data choice.
        private static string HeroNameForClass(DeNelle.Core.State.HeroClassOpt hc)
        {
            switch (hc)
            {
                case DeNelle.Core.State.HeroClassOpt.Knight: return "Knight";
                case DeNelle.Core.State.HeroClassOpt.Mage:   return "Mage";
                case DeNelle.Core.State.HeroClassOpt.Ranger: return "Ranger";
                case DeNelle.Core.State.HeroClassOpt.Cleric: return "Cleric";
                default: return "Hero";
            }
        }

        // Companion roster names (used for companion party slots, not the hero).
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
                case DeNelle.Core.State.HeroClassOpt.Mage:   return "Wizard/wizard";
                case DeNelle.Core.State.HeroClassOpt.Cleric: return "Healer/healer";
                default: return null;
            }
        }

        // T-035: companion portrait by roster NAME. PartyHudBridge pushes the canonical
        // companion display names (Thrain/Grom/Sylas/Elara — CompanionDialogue.NameFor),
        // so the HUD maps name→class→portrait without referencing DeNelle.Village (the
        // companion-class enum lives in Village). Falls back through PortraitNameForClass
        // so the same per-class art is reused. Returns null for an unknown name (caller
        // keeps the existing portrait sprite).
        private static string PortraitNameForRosterName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            switch (name.Trim())
            {
                case "Grom":   return PortraitNameForClass(DeNelle.Core.State.HeroClassOpt.Knight);
                case "Sylas":  return PortraitNameForClass(DeNelle.Core.State.HeroClassOpt.Ranger);
                case "Thrain": return PortraitNameForClass(DeNelle.Core.State.HeroClassOpt.Mage);
                case "Elara":  return PortraitNameForClass(DeNelle.Core.State.HeroClassOpt.Cleric);
                default:       return null;
            }
        }

        // ── Bottom-LEFT-ABOVE-joystick vitals — hero HP + mana stacked bars. ──
        // MOBILE ERGONOMICS: the LEFT thumb drives the VirtualJoystick (bottom-left
        // engage zone, centre ~radius*1.35 from the corner, claimed to radius*1.7).
        // We keep that quadrant CLEAR and float the hero vitals ABOVE it, anchored
        // to the bottom-left but lifted well over the stick (≈y 0.165→0.235).
        // WO-563: BuildVitalsCluster + BuildSkillBar were REMOVED with the OLD battle HUD.
        // The hero HP/mana/XP and the Q/W/E/R ability cells they built are now owned by the
        // 9-zone battle HUD (BattleHud9Zone) + the party-frame stack. The IVillageHud Set*
        // setters that fed them are kept (below) as null-safe no-ops so Village-side pushes
        // (HudModelProducers / ComboHudBridge / WaveHudBridge) still resolve. The AbilityRequested
        // event stays declared for the same reason.

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
            // Built through the shared kit so the CTA reads in ONE visual language.
            // OWNER ASK: the yellow CTA must sit INSIDE the dark pointed arrow plate
            // (top-left wave cluster) at ALL screen sizes. We therefore parent it to
            // the plate rect and fill it — no root anchors to be yanked back by the
            // responsive pass. BuildTownWaveCluster runs first, so _townWavePlate is
            // set; fall back to the supplied parent if it's somehow absent.
            Transform host = _townWavePlate != null ? (Transform)_townWavePlate : parent;
            Vector2 aMin = _townWavePlate != null ? new Vector2(0.06f, 0.12f) : new Vector2(0.085f, 0.865f);
            Vector2 aMax = _townWavePlate != null ? new Vector2(0.94f, 0.88f) : new Vector2(0.205f, 0.915f);
            var swBtn = ElarionUiKit.Button(host, "> Start Wave",
                ElarionUiKit.ButtonKind.Gold,
                aMin, aMax,
                onClick: () => StartWaveRequested?.Invoke());
            _startWaveBtn = swBtn.GetComponent<RectTransform>();
            // Render ABOVE the wave-plate art so the label/CTA reads and stays tappable.
            _startWaveBtn.SetAsLastSibling();
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

            // ── Pointed name plate (right) — reddens on INCOMING. The Start-Wave
            // button reparents INSIDE this plate (see BuildStartWaveButton), so the
            // state label is lifted to a thin strip ABOVE the plate to avoid overlap.
            var plate = NewRect("Plate", _townWaveCluster, new Vector2(0.40f, 0.40f), new Vector2(1f, 0.78f));
            _townWavePlate = plate;   // stash so the Start-Wave button can parent here (built after this cluster)
            _townLookoutBadge = plate.gameObject.AddComponent<Image>();
            _townLookoutBadge.raycastTarget = false;
            if (plateSprite != null) { _townLookoutBadge.sprite = plateSprite; _townLookoutBadge.color = Color.white; _townLookoutBadge.type = Image.Type.Simple; }
            else _townLookoutBadge.color = new Color(0.12f, 0.09f, 0.06f, 0.95f);
            // State label moved OUT of the plate (the button fills it) — thin strip just above.
            var stateRect = NewRect("State", _townWaveCluster, new Vector2(0.42f, 0.78f), new Vector2(1f, 0.95f));
            _townLookoutText = AddText(stateRect, "", 13, new Color(0.95f, 0.82f, 0.45f), TextAlignmentOptions.Center);
            _townLookoutText.fontStyle = FontStyles.Bold;
            _townLookoutText.raycastTarget = false;

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

            string[] names  = { "Food", "Wood", "Crystal", "Iron", "Gold" };   // icon = hud_food/hud_wood/hud_crystal/hud_iron/hud_gold
            string[] glyphs = { "o", "^", "*", "+", "$" };   // "$" = coin fallback glyph for Gold (until hud_gold.png lands)
            Color[] tints   = { HudTheme.GoldRes, HudTheme.Wood, HudTheme.Crystal, HudTheme.Iron, HudTheme.Gold };
            _townResText    = new TextMeshProUGUI[5];
            _townResBadge   = new Image[5];
            _townResOutline = new Image[5];

            float w = 0.164f;   // 5 cells inset into 0.09–0.91 of the strip (0.09 + 5*0.164 = 0.91; clear of the wood bar's rolled ends)
            for (int i = 0; i < 5; i++)
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
                // T-014: responsive to text growth — a 6-digit resource total must shrink to
                // fit its cell instead of overflowing into the next badge. Auto-size down,
                // no wrap (one line), so big numbers stay readable + on their own badge.
                _townResText[i].enableAutoSizing = true;
                _townResText[i].fontSizeMin = 12f; _townResText[i].fontSizeMax = 22f;
                _townResText[i].enableWordWrapping = false;
                _townResText[i].overflowMode = TextOverflowModes.Ellipsis;
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
                // Owner 2026-06-16: "minimize the team to the left side in town — takes up too
                // much room." Compact LEFT strip (248px) instead of the wide 595px town frame;
                // rows are fraction-laid so everything scales down proportionally.
                AnchorTopLeft(_partyStack, x: 10f, y: 160f, width: 248f,
                    height: PartyRowHeight * PartySlotCount + PartyRowGap * (PartySlotCount - 1));
                // MOCKUP ALIGN: Heart-of-Elarion objective bar TOP-LEFT under resources;
                // WAVE x/y readout CENTERED-top; resource strip across the top-right.
                SetAnchors(_castleBanner,   new Vector2(0.005f, 0.875f), new Vector2(0.46f, 0.94f));
                // WO-563: _waveReadout / _skillBar / _vitalsCluster removed with the OLD battle HUD.
                SetAnchors(_resourceStrip,  new Vector2(0.48f, 0.955f), new Vector2(1f, 1f));
                // TOWN ACTIONS diamond (BUILD/TALK/BAG/QUESTS) — right edge, clear of the old
                // skill-bar band footprint.
                SetAnchors(_townActionPanel, new Vector2(0.66f, 0.30f), new Vector2(1.0f, 0.58f));
                // Build entry lifts to the upper-right, clear of the skill cluster.
                SetAnchors(_buildBtn,       new Vector2(0.84f, 0.255f), new Vector2(0.99f, 0.33f));
                // "Start Wave" CTA is now a CHILD of the wave plate (filling it) — no
                // root re-anchor here, or it would be yanked off the plate. It tracks
                // the plate automatically as the cluster reflows.

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
                // Owner 2026-06-16: "minimize the team to the left side in town." Compact LEFT
                // strip (248px) instead of the wide 510px landscape town frame.
                AnchorTopLeft(_partyStack, x: 10f, y: 160f, width: 248f,
                    height: PartyRowHeight * PartySlotCount + PartyRowGap * (PartySlotCount - 1));
                // MOCKUP ALIGN: Heart objective bar top-LEFT; WAVE readout centered-top;
                // resources top-right.
                SetAnchors(_castleBanner,   new Vector2(0.005f, 0.93f), new Vector2(0.30f, 0.99f));
                // WO-563: _waveReadout / _skillBar / _vitalsCluster removed with the OLD battle HUD.
                SetAnchors(_resourceStrip,  new Vector2(0.76f, 0.94f), new Vector2(0.995f, 0.99f));
                // TOWN ACTIONS diamond — right edge, clear of the old skill-bar band footprint.
                SetAnchors(_townActionPanel, new Vector2(0.74f, 0.36f), new Vector2(1.0f, 0.66f));
                SetAnchors(_buildBtn,       new Vector2(0.88f, 0.36f),  new Vector2(0.995f, 0.45f));
                // "Start Wave" CTA is now a CHILD of the wave plate (filling it) — no
                // root re-anchor here, or it would be yanked off the plate. It tracks
                // the plate automatically as the cluster reflows.

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
            // WO-457: track the active RAID scene so the combat cluster (party frames +
            // vitals) ungates in RaidBase_* the same way it does in the village. Cheap
            // string test; refreshed on the same context poll as InVillage.
            var activeForRaid = SceneManager.GetActiveScene();
            bool raidNow = activeForRaid.IsValid() && DeNelle.Core.HubScenes.IsRaid(activeForRaid.name);
            if (raidNow != _raidSceneActive)
            {
                _raidSceneActive = raidNow;
                DeNelle.Core.Diagnostics.FlowTrace.Step("Raid",
                    $"HUD combat-cluster gate: raidScene={raidNow} (scene '{activeForRaid.name}').");
            }

            bool inVillage = EvaluateInVillage();
            if (!force && inVillage == _inVillage) return;
            _inVillage = inVillage;

            // FIX (quest-board-in-Village2): the town action panel (Quest/Talk faces),
            // Build button, start-wave button, and castle banner must NOT appear in an
            // ENEMY-OWNED scene (Village2 enemy outpost) — even though Village2 counts as
            // a HUB (HubScenes.Names, needed by RaidEntryBridge). Gate the whole town
            // chrome off on the enemy-owned axis (covers the _villageOnlyForced path too).
            // Home hub (MainCastle_Hall) is not enemy-owned, so it is unaffected.
            bool enemyOwnedScene = DeNelle.Core.HubScenes.IsEnemyOwnedScene(
                SceneManager.GetActiveScene().name);
            bool showVillage = (inVillage || _villageOnlyForced) && !enemyOwnedScene;
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
            // WO-339: feed the TOWN wave-progress readout too.
            _townWaveCur = waveNumber;
            RefreshCombatWaveHeadline();
            RefreshTownWaveProgress();
        }

        /// <summary>
        /// MOCKUP ALIGN: compose the centered combat headline "WAVE N/M" (or "WAVE N"
        /// until a total is known) from the same wave-number/total the town readout uses,
        /// so the big top-centre label matches hud_mobile_combat's "WAVE 3/5".
        /// </summary>
        private void RefreshCombatWaveHeadline()
        {
            // WO-563: the OLD battle wave headline (_waveText) was removed; the town wave cluster
            // (RefreshTownWaveProgress) is the sole wave readout now. Kept as a no-op so the many
            // call sites (SetWave / SetWaveProgress / HideWaveClearBanner) don't need to change.
        }

        public void SetCountdown(float secondsRemaining)
        {
            if (secondsRemaining > 0.1f)
            {
                _lastWaveState = "Prepare — " + secondsRemaining.ToString("0.0") + "s";
                // WO-339: live town countdown timer + auto lookout escalation.
                _townTimerSeconds = secondsRemaining;
                _townWaveActive = false;
                if (_lookoutStatus != 3)
                    ApplyLookout(secondsRemaining < 30f ? 2 : 1);
            }
            else
            {
                _lastWaveState = "Defend";
                _townTimerSeconds = 0f;
                _townWaveActive = true;
                ApplyLookout(3); // combat
            }
        }

        public void SetHeartHp(float current, float maxHp)
        {
            if (maxHp <= 0f) return;
            float pct = Mathf.Clamp01(current / maxHp);
            // TGVRU: combat Heart bar + town Heart text are both optional targets; a null on the
            // PRIMARY (combat fill) means the objective bar silently never updates — self-report once.
            if (_castleFill == null && _townHeartText == null)
                ReportMissingTarget("SetHeartHp", "_castleFill/_townHeartText");
            if (_castleFill != null) _castleFill.fillAmount = pct;
            if (_castleText != null) _castleText.text = "Heart of Elarion — " + Mathf.RoundToInt(pct * 100f) + "%";
            // MOCKUP ALIGN: a healthy Heart reads GREEN (the objective-bar fill colour in
            // hud_mobile_combat), reddening as it takes damage. Lerp red→green.
            if (_castleFill != null) _castleFill.color = Color.Lerp(HudTheme.HpRed, ElarionUi.Affordable, Mathf.Clamp01(pct / 0.5f));
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

        /// <summary>Town GOLD/Coins badge (index 4) — mirrors SetCrystals; fed by HeartHudBridge.</summary>
        public void SetGold(int amount) { SetTownResource(4, amount); }

        // §12 log-on-change cache: SetResources is invoked every frame by HeartHudBridge.
        private int _lastLogWood = int.MinValue, _lastLogIron = int.MinValue,
                    _lastLogFood = int.MinValue, _lastLogGems = int.MinValue;

        public void SetResources(int wood, int iron, int food, int gems)
        {
            // Trace ONLY on change — an unconditional per-frame Step floods the capture (drowned the
            // seam-cross lines in the owner's F8) and allocs a string/frame. The HUD still updates below.
            if (wood != _lastLogWood || iron != _lastLogIron || food != _lastLogFood || gems != _lastLogGems)
            {
                _lastLogWood = wood; _lastLogIron = iron; _lastLogFood = food; _lastLogGems = gems;
                DeNelle.Core.Diagnostics.FlowTrace.Step("Eco",
                    $"HUD.SetResources W{wood} I{iron} F{food} C{gems} (battleTexts={(_resourceTexts != null)}, townTexts={(_townResText != null)})");
            }
            if (_resourceTexts != null && _resourceTexts.Length >= 4)
            {
                // Audit P1 fix (per-element null checks): guard each slot like
                // SetCrystals does — a single null entry would NRE the whole update
                // and drop the rest of the resource strip.
                if (_resourceTexts[0] != null) _resourceTexts[0].text = wood.ToString();
                if (_resourceTexts[1] != null) _resourceTexts[1].text = iron.ToString();
                if (_resourceTexts[2] != null) _resourceTexts[2].text = gems.ToString();
                if (_resourceTexts[3] != null) _resourceTexts[3].text = food.ToString();
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
                if (value < prev)
                {
                    // SPEND — flash red immediately (unchanged feel). A spend also cancels
                    // any pending coalesced gain (the net just went the other way).
                    _townResFlash[idx] = 1f;
                    _townResFlashUp[idx] = false;
                    _townResGainAccum[idx] = 0;
                    _townResGainWindow[idx] = 0f;
                }
                else
                {
                    int delta = value - prev;
                    if (delta >= ResGainBigDelta)
                    {
                        // DISCRETE gain (reward / sell / big extract) — flash green NOW,
                        // every time; fold any pending small drip into this flash.
                        _townResFlash[idx] = 1f;
                        _townResFlashUp[idx] = true;
                        _townResGainAccum[idx] = 0;
                        _townResGainWindow[idx] = 0f;
                        DeNelle.Core.Diagnostics.FlowTrace.Step("Eco",
                            $"ResFlash discrete gain idx{idx} +{delta} -> instant flash (>= {ResGainBigDelta})");
                    }
                    else
                    {
                        // DRIP gain (passive +1..+few tick) — do NOT flash now (that is the
                        // strobe). Accumulate + (re)arm the coalesce window; UpdateTownHud
                        // fires ONE flash once the trickle pauses.
                        _townResGainAccum[idx] += delta;
                        _townResGainWindow[idx] = ResGainCoalesceWindow;
                    }
                }
            }
            _townResLast[idx] = value;

            // red outline when a GATHERED resource runs low (< 50). Gold (currency) is
            // exempt — never paint the red low-warn box over the coin cell.
            if (_townResOutline != null && idx != TownResGoldIndex && _townResOutline[idx] != null)
            {
                var c = HudTheme.HpRed;
                c.a = value < TownResLowThreshold ? 0.9f : 0f;
                _townResOutline[idx].color = c;
            }
        }

        public void SetAttackDirections(bool north, bool east, bool south, bool west) { /* compass is the separate CompassHud component */ }

        public void SetWaveImminent(bool imminent)
        {
            // WO-563: OLD battle headline removed; only the town lookout pip + state string remain.
            _lastWaveState = imminent ? "Horde Approaching" : "Defend";
            // WO-339: escalate the town lookout pip (unless already in combat).
            if (_lookoutStatus != 3) ApplyLookout(imminent ? 2 : 0);
        }

        public void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine)
        {
            // WO-563: OLD battle wave banner removed; the town lookout drives the cleared state.
            // WO-339: wave cleared → exit combat lookout.
            _townWaveActive = false;
            ApplyLookout(0);
        }

        public void HideWaveClearBanner()
        {
            // WO-339: wave done → lookout returns to SAFE, timer awaits next countdown.
            _townWaveActive = false;
            _townTimerSeconds = -1f;
            ApplyLookout(0);
        }

        public void ShowRepairPrompt(string wallLabel, float damagePercent)
        {
            if (_repairPanel == null) { ReportMissingTarget("ShowRepairPrompt", "_repairPanel"); return; }
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
            // WO-421 RC2: do NOT dim the root immediately/unconditionally — that washed
            // out the combat hero plate in deep OuterWorld (no lit wards → whole HUD at
            // 55%). Store the target; Update() applies it lerped + only when combat is
            // NOT live (see ApplyForgettingDim).
            _forgettingLevel = Mathf.Clamp01(level01);
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
        /// WO-563: the OLD combat clusters (bottom-right ability cells + bottom-left vitals) were
        /// removed. This now just latches the combat-HUD flag and re-applies the combat gate (which
        /// drives the party-frame stack). Driven by the Village-side BuildModeHudBridge (hide on
        /// Build Enter) + the H hotkey. VISIBILITY ONLY — data bindings keep writing while hidden.
        /// </summary>
        public void SetCombatHudVisible(bool visible)
        {
            _combatHudVisible = visible;
            // WO-563: the OLD skill bar + vitals cluster this used to toggle are gone. The flag is
            // still honoured by ApplyCombatGate (party-frame stack), so re-apply it now (Build-mode
            // enter/exit + the H hotkey still hide/show the base combat chrome that remains).
            ApplyCombatGate();
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
            _hudHiddenByModal = !visible;     // WO-421 RC2: while a modal hides the HUD, the
                                              // forgetting dim must not re-show the root group.
            _rootGroup.alpha = visible ? 1f : 0f;
            _rootGroup.interactable = visible;
            _rootGroup.blocksRaycasts = visible;
            // On restore, resync the eased dim baseline so it doesn't snap from 0 → target.
            if (visible) _rootDimAlpha = 1f;
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
            _lastWaveNumber = current;
            RefreshCombatWaveHeadline();   // MOCKUP ALIGN: keep the "WAVE N/M" headline in sync
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

        // WO-563: the OLD battle HUD's combo/kill-streak momentum badge + enemy-count readout +
        // ability cells were REMOVED. These IVillageHud setters are kept as no-ops (the Village-side
        // ComboHudBridge / HudModelProducers / HeroAbilitiesHudBridge still push to them) — the
        // 9-zone battle HUD reads ability/target/HP state directly from the systems now.
        public void SetComboCount(int count) { /* WO-563: momentum badge removed */ }

        public void SetKillStreak(int streak) { /* WO-563: momentum badge removed */ }

        public void SetEnemyCount(int live, int total) { /* WO-563: battle enemy-count readout removed */ }

        /// <summary>WO-563: the OLD bottom-left MP box is gone (the 9-zone shows mana). No-op.</summary>
        public void SetMana(float current, float max) { /* WO-563: vitals MP box removed */ }

        /// <summary>
        /// Live hero HP — the OLD bottom-left HP bar/text were removed (WO-563), but this still
        /// latches _hpCurrent/_hpMax (read by ApplyCombatGate's heroHurt gate) and feeds the
        /// party-frame stack (slot 0 = hero), which survives. Pushed by HeroAbilitiesHudBridge.
        /// </summary>
        public void SetHeroHp(float current, float max)
        {
            _hpCurrent = current;
            _hpMax = max > 0f ? max : 1f;
            // Re-resolve the hero name/portrait once the class loads (in case it wasn't ready at build).
            if (_partyName != null && _partyName.Length > 0 && _partyName[0] != null && _partyName[0].text == "Hero")
                RefreshHeroPortrait();
            SetPartyMember(0, _partyName != null && _partyName[0] != null ? _partyName[0].text : "Hero", current, max);
        }

        /// <summary>WO-563: per-slot cooldown sweep — the OLD ability cells are gone; the 9-zone
        /// renders its own cooldown rings from HeroAbilities. Kept as a no-op for existing callers.</summary>
        public void SetAbilityCooldown(int slot, float remaining, float total) { /* WO-563: skill bar removed */ }

        /// <summary>Per-class ability cell content (key/glyph/name) — 5-arg path. No-op (WO-563).</summary>
        public void SetAbilitySlot(int slot, string key, string glyph, string name, string description)
        {
            SetAbilitySlot(slot, key, glyph, name, description, null);
        }

        /// <summary>Per-class ability cell content + accent — 6-arg path. No-op (WO-563: skill bar removed).</summary>
        public void SetAbilitySlot(int slot, string key, string glyph, string name, string description, string accentHex)
        {
            /* WO-563: the OLD bottom-right ability cells were removed; the 9-zone owns the ability
               bar (it reads the live loadout directly). Kept on IVillageHud so callers don't break. */
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
            if (_partyFrame == null || slot < 0 || slot >= _partyFrame.Length)
            { ReportMissingTarget("SetPartyMember", "_partyFrame[" + slot + "]"); return; }
            if (_partyFrame[slot] != null) _partyFrame[slot].SetActive(true);
            if (_partyName != null && _partyName[slot] != null && !string.IsNullOrEmpty(name)) _partyName[slot].text = name;
            // T-035: bind the companion portrait from the roster name (slot 0 = hero is
            // owned by RefreshHeroPortrait). Missing art → keep the current sprite.
            if (slot >= 1 && _partyPortrait != null && _partyPortrait[slot] != null)
            {
                // Companion portrait: prefer the canonical character art in Resources/HeroPortraits/<RosterName>
                // (Grom/Sylas/Thrain/Elara.jpg — where the portraits actually LIVE + what HeroSelect/Title use).
                // Fall back to the legacy class-key HudIcons portrait, then keep the current sprite if neither.
                var portSp = !string.IsNullOrEmpty(name) ? Resources.Load<Sprite>("HeroPortraits/" + name.Trim()) : null;
                var portKey = PortraitNameForRosterName(name);
                if (portSp == null && portKey != null) portSp = WidgetSprite(portKey);
                if (portSp != null) _partyPortrait[slot].sprite = portSp;
                // WO-446: a missing companion portrait used to fail silently (slot kept its
                // old/blank sprite). Surface it so a future typo'd key / un-imported icon is
                // visible in the flow trace instead of just a blank gilt ring.
                else if (portKey != null)
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("UI",
                        $"party portrait null for '{portKey}' (roster '{name}')");
            }
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
