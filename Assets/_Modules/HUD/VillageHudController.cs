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
        private const float PartyRowHeight = 56f;
        private const float PartyRowGap = 5f;

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

        // Skill bar cells
        private TextMeshProUGUI[] _slotKey;
        private TextMeshProUGUI[] _slotGlyph;
        private TextMeshProUGUI[] _slotName;
        private Image[] _slotAccent;
        private Image[] _slotCooldown;
        private float[] _slotCdFill;

        // Party frames (slot 0 = hero, 1..3 = companions)
        private GameObject[] _partyFrame;
        private Image[] _partyHpFill;
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
            UpdateTownHud();
        }

        // ── WO-339: per-frame TOWN-HUD animation (timer urgency, res flash, map). ─
        private void UpdateTownHud()
        {
            float dt = Time.unscaledDeltaTime;

            // Countdown timer text + urgency colour (only when a wave is pending).
            if (_townTimerText != null)
            {
                if (_townWaveActive)
                {
                    _townTimerText.text = "WAVE IN PROGRESS";
                    _townTimerText.color = HudTheme.LookoutCombat;
                }
                else if (_townTimerSeconds >= 0f)
                {
                    int total = Mathf.Max(0, Mathf.CeilToInt(_townTimerSeconds));
                    _townTimerText.text = string.Format("Next wave {0:00}:{1:00}", total / 60, total % 60);
                    // urgency: <10s red, <30s amber, else calm cream.
                    _townTimerText.color = _townTimerSeconds < 10f ? HudTheme.LookoutIncoming
                        : _townTimerSeconds < 30f ? HudTheme.LookoutAlert : HudTheme.Text;
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
                    _townResBadge[i].color = Color.Lerp(HudTheme.Glass, flash, _townResFlash[i] * 0.6f);
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
            BuildTownMiniMap(townRoot);
            BuildTownMetrics(townRoot);

            // IDLE / village UI — base canvas (NEVER hidden by the battle-HUD gate).
            BuildResourceStrip(_safeArea);
            BuildCastleBanner(_safeArea);
            BuildBuildButton(_safeArea);
            BuildStartWaveButton(_safeArea);
            BuildRepairPrompt(_safeArea);
            BuildPartyFrames(_safeArea);

            // BATTLE HUD — combat-only clusters (faded in/out by the visibility mgr).
            BuildWaveReadout(battleRoot);
            BuildMomentumBadge(battleRoot);
            BuildVitalsCluster(battleRoot);
            BuildSkillBar(battleRoot);
        }

        // ── Currency strip — thin glass bar, tiny colour dot + amount. ─────────
        private void BuildResourceStrip(Transform parent)
        {
            _resourceStrip = NewRect("ResourceStrip", parent, new Vector2(0.50f, 0.955f), new Vector2(1f, 1f));
            HudTheme.StylePanel(_resourceStrip.gameObject, HudTheme.Glass);
            HudTheme.AddRim(_resourceStrip.gameObject, HudTheme.AccentSoft);

            string[] names  = { "Wood", "Iron", "Crystal", "Gold" };
            string[] glyphs = { "▲", "◆", "❖", "●" };
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
                _resourceTexts[i] = AddText(amt, "0", 26, HudTheme.Text, TextAlignmentOptions.Left);
                _resourceTexts[i].fontStyle = FontStyles.Bold;
            }
        }

        // ── Castle (Heart) HP — top-centre slim bar. VILLAGE-ONLY. ─────────────
        private void BuildCastleBanner(Transform parent)
        {
            _castleBanner = NewRect("CastleBanner", parent, new Vector2(0.30f, 0.955f), new Vector2(0.70f, 1f));
            HudTheme.StylePanel(_castleBanner.gameObject, HudTheme.Glass);
            HudTheme.AddRim(_castleBanner.gameObject, HudTheme.AccentSoft);

            var track = NewRect("Track", _castleBanner, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.78f));
            HudTheme.StyleWell(track.gameObject);
            var fill = NewRect("Fill", track, Vector2.zero, Vector2.one);
            fill.offsetMin = new Vector2(1.5f, 1.5f); fill.offsetMax = new Vector2(-1.5f, -1.5f);
            _castleFill = fill.gameObject.AddComponent<Image>();
            _castleFill.color = HudTheme.CastleGold;
            _castleFill.sprite = HudTheme.RoundedFrame;
            _castleFill.type = HudTheme.RoundedFrame != null ? Image.Type.Filled : Image.Type.Filled;
            _castleFill.fillMethod = Image.FillMethod.Horizontal;
            _castleFill.fillOrigin = 0;
            _castleFill.fillAmount = 1f;
            _castleFill.raycastTarget = false;

            _castleText = AddText(track, "Heart of Elarion — 100%", 16, HudTheme.Text, TextAlignmentOptions.Center);
            _castleText.fontStyle = FontStyles.Bold;
            _castleText.outlineColor = new Color32(0, 0, 0, 180);
            _castleText.outlineWidth = 0.15f;
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
            _partyFrame   = new GameObject[PartySlotCount];
            _partyHpFill  = new Image[PartySlotCount];
            _partyName    = new TextMeshProUGUI[PartySlotCount];
            _partyHpText  = new TextMeshProUGUI[PartySlotCount];

            _partyStack = NewRect("PartyStack", parent, new Vector2(0f, 1f), new Vector2(0f, 1f));
            AnchorTopLeft(_partyStack, x: 10f, y: 10f, width: 240f,
                height: PartyRowHeight * PartySlotCount + PartyRowGap * (PartySlotCount - 1));

            for (int i = 0; i < PartySlotCount; i++)
            {
                var frame = NewRect("Party" + i, _partyStack, new Vector2(0f, 1f), new Vector2(1f, 1f));
                frame.pivot = new Vector2(0.5f, 1f);
                frame.anchoredPosition = new Vector2(0f, -i * (PartyRowHeight + PartyRowGap));
                frame.sizeDelta = new Vector2(0f, PartyRowHeight);
                HudTheme.StylePanel(frame.gameObject, HudTheme.Glass);
                if (i == 0) HudTheme.AddRim(frame.gameObject, HudTheme.AccentSoft);
                _partyFrame[i] = frame.gameObject;

                // Portrait swatch
                var port = NewRect("Portrait", frame, new Vector2(0.04f, 0.18f), new Vector2(0.26f, 0.90f));
                var pimg = port.gameObject.AddComponent<Image>();
                pimg.color = HudTheme.PortraitFill;
                pimg.sprite = HudTheme.RoundedFrame;
                pimg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                pimg.raycastTarget = false;
                AddText(port, "✦", 18, new Color(HudTheme.Gilt.r, HudTheme.Gilt.g, HudTheme.Gilt.b, 0.45f), TextAlignmentOptions.Center);

                // Name
                var nameRect = NewRect("Name", frame, new Vector2(0.30f, 0.50f), new Vector2(0.98f, 0.96f));
                _partyName[i] = AddText(nameRect, i == 0 ? "Hero" : "—", 16, HudTheme.Text, TextAlignmentOptions.Left);
                _partyName[i].fontStyle = FontStyles.Bold;
                _partyName[i].enableAutoSizing = true;
                _partyName[i].fontSizeMin = 9f;
                _partyName[i].fontSizeMax = 16f;

                // HP bar
                var track = NewRect("HPTrack", frame, new Vector2(0.30f, 0.14f), new Vector2(0.98f, 0.46f));
                HudTheme.StyleWell(track.gameObject);
                var fill = NewRect("HPFill", track, Vector2.zero, Vector2.one);
                fill.offsetMin = new Vector2(1f, 1f); fill.offsetMax = new Vector2(-1f, -1f);
                var fimg = fill.gameObject.AddComponent<Image>();
                fimg.color = HudTheme.HpRed;
                fimg.sprite = HudTheme.RoundedFrame;
                fimg.type = HudTheme.RoundedFrame != null ? Image.Type.Filled : Image.Type.Filled;
                fimg.fillMethod = Image.FillMethod.Horizontal;
                fimg.fillOrigin = 0;
                fimg.fillAmount = 1f;
                fimg.raycastTarget = false;
                _partyHpFill[i] = fimg;
                _partyHpText[i] = AddText(track, "", 12, HudTheme.Text, TextAlignmentOptions.Center);
                _partyHpText[i].outlineColor = new Color32(0, 0, 0, 160);
                _partyHpText[i].outlineWidth = 0.1f;

                _partyFrame[i].SetActive(i == 0);
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
            HudTheme.StylePanel(_vitalsCluster.gameObject, HudTheme.Glass);
            HudTheme.AddRim(_vitalsCluster.gameObject, HudTheme.AccentSoft);

            // HP bar (top half)
            var hpTrack = NewRect("HPTrack", _vitalsCluster, new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.92f));
            HudTheme.StyleWell(hpTrack.gameObject);
            var hpFill = NewRect("HPFill", hpTrack, Vector2.zero, Vector2.one);
            hpFill.offsetMin = new Vector2(1.5f, 1.5f); hpFill.offsetMax = new Vector2(-1.5f, -1.5f);
            _hpFill = hpFill.gameObject.AddComponent<Image>();
            _hpFill.color = HudTheme.HpRed;
            _hpFill.sprite = HudTheme.RoundedFrame;
            _hpFill.type = HudTheme.RoundedFrame != null ? Image.Type.Filled : Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = 0;
            _hpFill.fillAmount = 1f;
            _hpFill.raycastTarget = false;
            _hpText = AddText(hpTrack, "", 14, HudTheme.Text, TextAlignmentOptions.Center);
            _hpText.fontStyle = FontStyles.Bold;
            _hpText.outlineColor = new Color32(0, 0, 0, 170); _hpText.outlineWidth = 0.1f;

            // Mana bar (bottom half)
            var mTrack = NewRect("ManaTrack", _vitalsCluster, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.48f));
            HudTheme.StyleWell(mTrack.gameObject);
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
            _manaText = AddText(mTrack, "", 13, HudTheme.Text, TextAlignmentOptions.Center);
            _manaText.fontStyle = FontStyles.Bold;
            _manaText.outlineColor = new Color32(0, 0, 0, 170); _manaText.outlineWidth = 0.1f;
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
            HudTheme.StylePanel(_skillBar.gameObject, HudTheme.GlassDeep);
            HudTheme.AddRim(_skillBar.gameObject, HudTheme.AccentSoft);

            _slotKey      = new TextMeshProUGUI[AbilitySlotCount];
            _slotGlyph    = new TextMeshProUGUI[AbilitySlotCount];
            _slotName     = new TextMeshProUGUI[AbilitySlotCount];
            _slotAccent   = new Image[AbilitySlotCount];
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
                cellImg.color = HudTheme.Cell;
                cellImg.sprite = HudTheme.RoundedFrame;
                cellImg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;

                // Accent square (tinted per ability) — fills most of the cell.
                var disc = NewRect("Accent", cell, new Vector2(0.10f, 0.36f), new Vector2(0.90f, 0.96f));
                _slotAccent[i] = disc.gameObject.AddComponent<Image>();
                _slotAccent[i].color = HudTheme.SlotDisc;
                _slotAccent[i].sprite = HudTheme.RoundedFrame;
                _slotAccent[i].type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                _slotAccent[i].raycastTarget = false;

                _slotGlyph[i] = AddText(disc, "", 30, HudTheme.Text, TextAlignmentOptions.Center);
                _slotGlyph[i].outlineColor = new Color32(0, 0, 0, 150);
                _slotGlyph[i].outlineWidth = 0.1f;

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
                _slotName[i] = AddText(nameRect, "", 14, HudTheme.Text, TextAlignmentOptions.Center);
                _slotName[i].fontStyle = FontStyles.Bold;
                _slotName[i].enableAutoSizing = true;
                _slotName[i].fontSizeMin = 8f;
                _slotName[i].fontSizeMax = 14f;
                _slotName[i].raycastTarget = false;
                _slotName[i].outlineColor = new Color32(0, 0, 0, 140);
                _slotName[i].outlineWidth = 0.08f;

                // Hotkey badge (top-right)
                var keyBadge = NewRect("KeyBadge", cell, new Vector2(0.70f, 0.70f), new Vector2(1.0f, 1.0f));
                var keyImg = keyBadge.gameObject.AddComponent<Image>();
                keyImg.sprite = HudTheme.RoundedFrame;
                keyImg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                keyImg.color = new Color(0f, 0f, 0f, 0.6f);
                keyImg.raycastTarget = false;
                _slotKey[i] = AddText(keyBadge, defaultKeys[i], 14, HudTheme.Gold, TextAlignmentOptions.Center);
                _slotKey[i].fontStyle = FontStyles.Bold;

                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = cellImg;
                HudTheme.StyleButtonColors(btn, HudTheme.Cell);
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
            var t = AddText(_buildBtn, "⚒ BUILD", HudTheme.FontBody, HudTheme.Ink, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
        }

        // ── "Defend!" start-wave button. VILLAGE-ONLY + between-waves only. ───
        private void BuildStartWaveButton(Transform parent)
        {
            _startWaveBtn = NewRect("StartWaveBtn", parent, new Vector2(0.40f, 0.825f), new Vector2(0.60f, 0.875f));
            var bimg = _startWaveBtn.gameObject.AddComponent<Image>();
            bimg.color = HudTheme.GoldButton;
            bimg.sprite = HudTheme.RoundedFrame;
            bimg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            var btn = _startWaveBtn.gameObject.AddComponent<Button>();
            btn.targetGraphic = bimg;
            HudTheme.StyleButtonColors(btn, HudTheme.GoldButton);
            btn.onClick.AddListener(() => StartWaveRequested?.Invoke());
            var t = AddText(_startWaveBtn, "⚔ DEFEND!", HudTheme.FontBody, HudTheme.Ink, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 2f;
            _startWaveBtn.gameObject.SetActive(false);
        }

        private void BuildRepairPrompt(Transform parent)
        {
            var p = NewRect("RepairPrompt", parent, new Vector2(0.30f, 0.42f), new Vector2(0.70f, 0.58f));
            HudTheme.StylePanel(p.gameObject, HudTheme.GlassDeep);
            HudTheme.AddRim(p.gameObject, HudTheme.AccentSoft);
            _repairPanel = p.gameObject;

            var labelRect = NewRect("Label", p, new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.95f));
            _repairLabel = AddText(labelRect, "", 20, HudTheme.Text, TextAlignmentOptions.Center);
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
            nimg.color = HudTheme.Glass;
            nimg.sprite = HudTheme.RoundedFrame;
            nimg.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            var noBtn = no.gameObject.AddComponent<Button>();
            noBtn.targetGraphic = nimg;
            HudTheme.StyleButtonColors(noBtn, HudTheme.Glass);
            noBtn.onClick.AddListener(() => { RepairCancelRequested?.Invoke(); HideRepairPrompt(); });
            AddText(no, "Later", HudTheme.FontBody, HudTheme.Text, TextAlignmentOptions.Center);

            _repairPanel.SetActive(false);
        }

        // =====================================================================
        //  WO-339 · TOWN HUD builders (idle-village mode)
        // =====================================================================

        // ── Top-LEFT · WAVE MANAGEMENT — countdown + progress + lookout badge. ─
        private void BuildTownWaveCluster(Transform parent)
        {
            _townWaveCluster = NewRect("TownWave", parent, new Vector2(0f, 1f), new Vector2(0f, 1f));
            AnchorTopLeft(_townWaveCluster, x: 12f, y: 12f, width: 300f, height: 118f);
            HudTheme.StylePanel(_townWaveCluster.gameObject, HudTheme.Glass);
            HudTheme.AddRim(_townWaveCluster.gameObject, HudTheme.AccentSoft);

            // Lookout status badge (pip + label) — top row.
            var badgeRect = NewRect("Lookout", _townWaveCluster, new Vector2(0.04f, 0.66f), new Vector2(0.30f, 0.94f));
            _townLookoutBadge = badgeRect.gameObject.AddComponent<Image>();
            _townLookoutBadge.sprite = HudTheme.RoundedFrame;
            _townLookoutBadge.type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
            _townLookoutBadge.color = HudTheme.LookoutSafe;
            _townLookoutBadge.raycastTarget = false;
            _townLookoutText = AddText(badgeRect, "SAFE", 12, HudTheme.Ink, TextAlignmentOptions.Center);
            _townLookoutText.fontStyle = FontStyles.Bold;

            // Countdown timer (MM:SS) — top row, large.
            var timerRect = NewRect("Timer", _townWaveCluster, new Vector2(0.32f, 0.62f), new Vector2(0.98f, 0.97f));
            _townTimerText = AddText(timerRect, "Next wave —:—", HudTheme.FontHead, HudTheme.Text, TextAlignmentOptions.MidlineRight);
            _townTimerText.fontStyle = FontStyles.Bold;
            _townTimerText.outlineColor = new Color32(0, 0, 0, 160);
            _townTimerText.outlineWidth = 0.12f;

            // Wave progress label + bar — bottom rows.
            var progLabel = NewRect("ProgLabel", _townWaveCluster, new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.60f));
            _townWaveProgText = AddText(progLabel, "Wave 1", 14, HudTheme.TextDim, TextAlignmentOptions.Left);
            _townWaveProgText.fontStyle = FontStyles.Bold;

            var progTrack = NewRect("ProgTrack", _townWaveCluster, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.30f));
            HudTheme.StyleWell(progTrack.gameObject);
            var progFill = NewRect("ProgFill", progTrack, Vector2.zero, Vector2.one);
            progFill.offsetMin = new Vector2(1.5f, 1.5f); progFill.offsetMax = new Vector2(-1.5f, -1.5f);
            _townWaveProgFill = progFill.gameObject.AddComponent<Image>();
            _townWaveProgFill.color = HudTheme.Gold;
            _townWaveProgFill.sprite = HudTheme.RoundedFrame;
            _townWaveProgFill.type = HudTheme.RoundedFrame != null ? Image.Type.Filled : Image.Type.Filled;
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
            HudTheme.StylePanel(_townPassiveXp.gameObject, HudTheme.Glass);
            HudTheme.AddRim(_townPassiveXp.gameObject, HudTheme.AccentSoft);

            var label = NewRect("Label", _townPassiveXp, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            _townPassiveXpText = AddText(label, "⚡ Towers earning 0 XP/min", 13, HudTheme.Crystal, TextAlignmentOptions.MidlineLeft);
            _townPassiveXpText.fontStyle = FontStyles.Bold;
            _townPassiveXpText.raycastTarget = false;

            // Hidden until a non-zero rate is fed.
            _townPassiveXp.gameObject.SetActive(false);
        }

        // ── Top-CENTRE · RESOURCE badges (icon + number, distinct colours). ────
        private void BuildTownResourceBadges(Transform parent)
        {
            _townResStrip = NewRect("TownResources", parent, new Vector2(0.30f, 0.955f), new Vector2(0.70f, 1f));

            string[] names  = { "Gold", "Wood", "Crystal", "Iron" };
            string[] glyphs = { "●", "▲", "❖", "◆" };
            Color[] tints   = { HudTheme.GoldRes, HudTheme.Wood, HudTheme.Crystal, HudTheme.Iron };
            _townResText    = new TextMeshProUGUI[4];
            _townResBadge   = new Image[4];
            _townResOutline = new Image[4];

            float w = 1f / 4f;
            for (int i = 0; i < 4; i++)
            {
                var cell = NewRect("Res_" + names[i], _townResStrip, new Vector2(i * w + 0.01f, 0.08f), new Vector2((i + 1) * w - 0.01f, 0.92f));
                _townResBadge[i] = HudTheme.StylePanel(cell.gameObject, HudTheme.Glass);

                // red low-warn outline overlay (hidden until value < threshold).
                var outline = NewRect("Low", cell, Vector2.zero, Vector2.one);
                _townResOutline[i] = outline.gameObject.AddComponent<Image>();
                _townResOutline[i].sprite = HudTheme.RoundedFrame;
                _townResOutline[i].type = HudTheme.RoundedFrame != null ? Image.Type.Sliced : Image.Type.Simple;
                _townResOutline[i].color = new Color(HudTheme.HpRed.r, HudTheme.HpRed.g, HudTheme.HpRed.b, 0f);
                _townResOutline[i].raycastTarget = false;

                var dot = NewRect("Dot", cell, new Vector2(0.06f, 0.28f), new Vector2(0.30f, 0.72f));
                var dimg = dot.gameObject.AddComponent<Image>();
                dimg.color = tints[i];
                dimg.sprite = HudTheme.Disc;
                dimg.raycastTarget = false;
                AddText(dot, glyphs[i], 14, HudTheme.Ink, TextAlignmentOptions.Center);

                var amt = NewRect("Amt", cell, new Vector2(0.32f, 0f), new Vector2(0.97f, 1f));
                _townResText[i] = AddText(amt, "0", 20, HudTheme.Text, TextAlignmentOptions.Left);
                _townResText[i].fontStyle = FontStyles.Bold;
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
            HudTheme.StylePanel(_townMiniMap.gameObject, HudTheme.GlassDeep);
            HudTheme.AddRim(_townMiniMap.gameObject, HudTheme.AccentSoft);

            _townMiniMapInner = NewRect("Inner", _townMiniMap, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));

            // Hero dot — always centred (the map is hero-relative? No: world-anchored
            // at Heart origin; the hero marker tracks the hero's world position).
            AddMiniMapMarker("Hero", Vector3.zero, HudTheme.HeroDot, "✦", isHero: true);
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
            HudTheme.StylePanel(_townMetrics.gameObject, HudTheme.Glass);
            HudTheme.AddRim(_townMetrics.gameObject, HudTheme.AccentSoft);

            _townHeartText = BuildMetricCol(_townMetrics, 0f, 1f / 3f, "♥ Heart", "100%", HudTheme.HpRed);
            _townTowerText = BuildMetricCol(_townMetrics, 1f / 3f, 2f / 3f, "⛨ Towers", "0/0", HudTheme.Gold);
            _townPopText   = BuildMetricCol(_townMetrics, 2f / 3f, 1f, "☖ Pop", "0", HudTheme.Crystal);
        }

        private TextMeshProUGUI BuildMetricCol(Transform parent, float x0, float x1, string label, string value, Color tint)
        {
            var col = NewRect("Col_" + label, parent, new Vector2(x0, 0f), new Vector2(x1, 1f));
            var lab = NewRect("Lab", col, new Vector2(0.04f, 0.05f), new Vector2(0.50f, 0.95f));
            var lt = AddText(lab, label, 13, new Color(tint.r, tint.g, tint.b, 0.9f), TextAlignmentOptions.MidlineRight);
            lt.fontStyle = FontStyles.Bold;
            var valRect = NewRect("Val", col, new Vector2(0.52f, 0.05f), new Vector2(0.97f, 0.95f));
            var vt = AddText(valRect, value, 16, HudTheme.Text, TextAlignmentOptions.MidlineLeft);
            vt.fontStyle = FontStyles.Bold;
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
                AnchorTopLeft(_partyStack, x: 10f, y: 10f, width: 280f,
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
                SetAnchors(_startWaveBtn,   new Vector2(0.40f, 0.83f),  new Vector2(0.60f, 0.88f));

                // WO-339 TOWN HUD portrait reflow: wave/timer cluster top-LEFT
                // narrows; resources STACK on the left under it; mini-map shrinks
                // to ~100×100 top-right; metrics bottom span wider as a 2-col feel.
                AnchorTopLeft(_townWaveCluster, x: 10f, y: 10f, width: 260f, height: 110f);
                SetAnchors(_townResStrip, new Vector2(0.0f, 0.83f), new Vector2(0.46f, 0.875f));
                if (_townMiniMap != null) { _townMiniMap.anchoredPosition = new Vector2(-10f, -10f); _townMiniMap.sizeDelta = new Vector2(100f, 100f); }
                SetAnchors(_townMetrics, new Vector2(0.06f, 0f), new Vector2(0.94f, 0.05f));
            }
            else
            {
                AnchorTopLeft(_partyStack, x: 10f, y: 10f, width: 240f,
                    height: PartyRowHeight * PartySlotCount + PartyRowGap * (PartySlotCount - 1));
                SetAnchors(_castleBanner,   new Vector2(0.36f, 0.94f), new Vector2(0.64f, 0.99f));
                SetAnchors(_waveReadout,    new Vector2(0.36f, 0.86f), new Vector2(0.64f, 0.925f));
                SetAnchors(_resourceStrip,  new Vector2(0.76f, 0.94f), new Vector2(0.995f, 0.99f));
                // Landscape: more width — ability cluster sits tight in the corner.
                SetAnchors(_skillBar,       new Vector2(0.74f, 0.0f),   new Vector2(1.0f, 0.34f));
                // Vitals bottom-left above the (smaller) landscape joystick.
                SetAnchors(_vitalsCluster,  new Vector2(0.02f, 0.30f),  new Vector2(0.30f, 0.37f));
                SetAnchors(_buildBtn,       new Vector2(0.88f, 0.36f),  new Vector2(0.995f, 0.45f));
                SetAnchors(_startWaveBtn,   new Vector2(0.44f, 0.79f),  new Vector2(0.56f, 0.845f));

                // WO-339 TOWN HUD landscape: wide top spread — wave cluster top-left,
                // resource badges top-centre, full-size 140 mini-map top-right.
                AnchorTopLeft(_townWaveCluster, x: 12f, y: 12f, width: 300f, height: 118f);
                SetAnchors(_townResStrip, new Vector2(0.32f, 0.955f), new Vector2(0.68f, 1f));
                if (_townMiniMap != null) { _townMiniMap.anchoredPosition = new Vector2(-12f, -12f); _townMiniMap.sizeDelta = new Vector2(140f, 140f); }
                SetAnchors(_townMetrics, new Vector2(0.34f, 0f), new Vector2(0.66f, 0.05f));
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
            // Non-village active scene (dungeon, PatriciaLight, ATB) → not village.
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name != VillageSceneName) return false;

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
                _townHeartText.color = Color.Lerp(HudTheme.HpRed, HudTheme.Text, Mathf.Clamp01(pct / 0.5f));
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
            string label; Color tint;
            switch (status)
            {
                case 3: label = "COMBAT";   tint = HudTheme.LookoutCombat;   break;
                case 2: label = "INCOMING"; tint = HudTheme.LookoutIncoming; break;
                case 1: label = "ALERT";    tint = HudTheme.LookoutAlert;    break;
                default: label = "SAFE";    tint = HudTheme.LookoutSafe;     break;
            }
            _townLookoutBadge.color = tint;
            if (_townLookoutText != null) _townLookoutText.text = label;
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
                case "forge":     glyph = "⚒"; tint = HudTheme.Iron;    break;
                case "warehouse": glyph = "▦"; tint = HudTheme.Wood;    break;
                case "tree":
                case "resource":  glyph = "▲"; tint = HudTheme.LookoutSafe; break;
                case "gate":
                case "enemy":     glyph = "✖"; tint = HudTheme.LookoutIncoming; break;
                default:          glyph = "•"; tint = HudTheme.TextDim; break;
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
            if (_slotName != null && _slotName[slot] != null)
                _slotName[slot].color = ready ? HudTheme.Text : HudTheme.TextDim;
            if (_slotKey != null && _slotKey[slot] != null)
                _slotKey[slot].color = ready ? HudTheme.Gold : HudTheme.TextDim;
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
            if (_slotName != null && _slotName[slot] != null)
                _slotName[slot].text = string.IsNullOrEmpty(name) ? "" : name;
            if (_slotAccent != null && _slotAccent[slot] != null && !string.IsNullOrEmpty(accentHex)
                && ColorUtility.TryParseHtmlString(accentHex, out var c))
            {
                c.a = 0.85f;
                _slotAccent[slot].color = c;
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
