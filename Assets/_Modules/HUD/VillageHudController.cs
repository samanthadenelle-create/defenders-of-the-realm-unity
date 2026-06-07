// =============================================================================
// VillageHudController — themed fantasy combat HUD (pure code-built uGUI).
// -----------------------------------------------------------------------------
// WO-307 visual overhaul. Matches the owner concepts (docs/design/
// hud-vertical-mobile-concept.jpg + hud-landscape-concept.jpg): grouped clusters,
// stone/gold/parchment fantasy theme (HudTheme), rounded panel frames, large
// touch targets, and a RESPONSIVE layout that re-anchors for portrait (mobile)
// AND landscape (web/tablet). One HUD only — there is no second overlapping
// Canvas (the old HUDManager demo path was retired).
//
// Clusters (concept-aligned):
//   • Top-left   — hero/party frames (portrait + level + HP/mana/XP).
//   • Top-centre — Castle (Heart) HP + "WAVE N — <state>" banner.
//   • Top-right  — currency strip (Wood / Iron / Crystal / Gold), icon discs.
//   • Bottom     — ability/skill bar (4 cells, cooldown sweep) + build entry.
//   The COMPASS is a separate component (CompassHud); owner chose compass over
//   minimap (WO-307 roundtable). This controller leaves room for it top-centre.
//
// WIRING (unchanged — all via the existing reflection bridges; DeNelle.Village →
// DeNelle.HUD is asmdef-isolated so the Village side calls these by name):
//   • HeroAbilitiesHudBridge  → SetMana / SetAbilityCooldown / SetAbilitySlot /
//                               SetHeroHp + reads the AbilityRequested event.
//   • HeartHudBridge          → SetHeartHp / SetResources / SetCrystals.
//   • WaveHudBridge           → SetWave / SetCountdown / SetWaveImminent.
//   • WaveFeedbackDirector    → ShowWaveClearBanner / HideWaveClearBanner.
//   • WallRepairHudBridge     → ShowRepairPrompt / HideRepairPrompt (+ events).
//   • BuildButtonBridge       → BuildRequested event.
//   • PartyHudBridge          → SetPartyMember / SetPartyMemberVisible.
//   • WardTetherService       → SetForgettingLevel / SetWardsReadout.
//
// Every public method signature is preserved — this is restyle + re-layout only.
// Pure UnityEngine.UI + TMPro. No UXML/Toolkit (does not render in builds — see
// memory "uxml-uidocuments-dont-render-in-builds"). Passive IVillageHud.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.HUD;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace DeNelle.HUD
{
    public sealed class VillageHudController : MonoBehaviour, IVillageHud
    {
        public const int AbilitySlotCount = 4;
        private const int PartySlotCount = 4; // hero + up to 3 companions

        [Header("Events (read by the Village-side bridges via reflection)")]
        public UnityEvent BuildRequested = new UnityEvent();
        public UnityEvent SkillsRequested = new UnityEvent();
        public UnityEvent ShopRequested = new UnityEvent();
        public AbilitySlotEvent AbilityRequested = new AbilitySlotEvent();
        public UnityEvent RepairConfirmRequested = new UnityEvent();
        public UnityEvent RepairCancelRequested = new UnityEvent();
        // Player presses the "Defend!" button to begin the next wave immediately
        // (skip the Prepare-Phase countdown). Read by the Village-side
        // StartWaveHudBridge, which calls WaveManager.ForceBeginNextWave().
        public UnityEvent StartWaveRequested = new UnityEvent();

        [System.Serializable] public sealed class AbilitySlotEvent : UnityEvent<int> { }

        // ── Canvas + cached widgets ──────────────────────────────────────────
        private Canvas _hudCanvas;
        private CanvasScaler _scaler;

        private TextMeshProUGUI[] _resourceTexts; // 0 Wood, 1 Iron, 2 Crystal, 3 Gold

        private TextMeshProUGUI _waveText;
        private TextMeshProUGUI _waveStateText;
        private int _lastWaveNumber = 1;
        private string _lastWaveState = "Defend";

        // Castle (Heart) HP banner — top-centre.
        private Image _castleFill;
        private TextMeshProUGUI _castleText;

        // Hero vitals (over the skill bar)
        private Image _hpFill;
        private TextMeshProUGUI _hpText;
        private Image _manaFill;
        private TextMeshProUGUI _manaText;
        private float _hpCurrent, _hpMax = 1f;

        // Skill bar cells
        private TextMeshProUGUI[] _slotKey;
        private TextMeshProUGUI[] _slotGlyph;
        private Image[] _slotAccent;     // tinted rune disc behind the glyph
        private Image[] _slotCooldown;   // radial dark overlay (remaining/total)

        // Party frames (slot 0 = hero, 1..3 = companions)
        private GameObject[] _partyFrame;
        private Image[] _partyHpFill;
        private TextMeshProUGUI[] _partyName;
        private TextMeshProUGUI[] _partyHpText;

        // Repair prompt (transient)
        private GameObject _repairPanel;
        private TextMeshProUGUI _repairLabel;

        private CanvasGroup _rootGroup; // for SetForgettingLevel fade

        // ── Responsive layout ────────────────────────────────────────────────
        // Cluster roots whose anchors flip between portrait & landscape.
        private RectTransform _resourceStrip;
        private RectTransform _waveReadout;
        private RectTransform _castleBanner;
        private RectTransform _partyStack;
        private RectTransform _skillBar;
        private RectTransform _buildBtn;
        private RectTransform _startWaveBtn;     // "Defend!" — begin the next wave
        private bool _startWaveAvailable;         // last pushed availability (between waves)
        private bool _isPortrait = true;
        private int _lastScreenW, _lastScreenH;

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
            Build();
            ApplyResponsiveLayout(force: true);
            Debug.Log("[VillageHudController] WO-307 themed responsive HUD active (party + castle + currency + skill bar).");
        }

        private void Update()
        {
            // Cheap watcher — re-anchor only when the screen size actually changes
            // (orientation flip / window resize). No per-frame layout cost.
            if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
                ApplyResponsiveLayout(force: false);
        }

        // =====================================================================
        //  BUILD
        // =====================================================================
        private void Build()
        {
            if (_hudCanvas != null) return;

            var go = new GameObject("VillageHUD");
            go.transform.SetParent(transform, false);
            _hudCanvas = go.AddComponent<Canvas>();
            _hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hudCanvas.sortingOrder = 100;
            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1080, 1920);
            _scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _rootGroup = go.AddComponent<CanvasGroup>();

            BuildResourceStrip(go.transform);
            BuildCastleBanner(go.transform);
            BuildWaveReadout(go.transform);
            BuildPartyFrames(go.transform);
            BuildSkillBar(go.transform);
            BuildBuildButton(go.transform);
            BuildStartWaveButton(go.transform);
            BuildRepairPrompt(go.transform);
        }

        // ── Currency strip: Wood / Iron / Crystal / Gold, icon disc per cell. ──
        private void BuildResourceStrip(Transform parent)
        {
            _resourceStrip = NewRect("ResourceStrip", parent, new Vector2(0.50f, 0.955f), new Vector2(1f, 1f));
            HudTheme.StylePanel(_resourceStrip.gameObject, HudTheme.PanelStone);

            // Concept order: Wood, Iron, Crystal (gem), Gold. Glyph + warm tint per.
            string[] names  = { "Wood", "Iron", "Crystal", "Gold" };
            string[] glyphs = { "▲", "◆", "❖", "●" }; // tri / diamond / facet / coin
            Color[] tints   = { HudTheme.Wood, HudTheme.Iron, HudTheme.Crystal, HudTheme.Gold };
            _resourceTexts = new TextMeshProUGUI[4];

            float w = 1f / 4f;
            for (int i = 0; i < 4; i++)
            {
                var cell = NewRect("Res_" + names[i], _resourceStrip, new Vector2(i * w, 0f), new Vector2((i + 1) * w, 1f));

                // icon disc (left)
                var disc = NewRect("Icon", cell, new Vector2(0.06f, 0.18f), new Vector2(0.42f, 0.82f));
                var dimg = disc.gameObject.AddComponent<Image>();
                dimg.color = tints[i];
                dimg.sprite = HudTheme.Disc;
                dimg.type = Image.Type.Simple;
                AddText(disc, glyphs[i], 26, HudTheme.Ink, TextAlignmentOptions.Center);

                // amount text (right)
                var amt = NewRect("Amt", cell, new Vector2(0.42f, 0f), new Vector2(1f, 1f));
                _resourceTexts[i] = AddText(amt, "0", 28, HudTheme.Parchment, TextAlignmentOptions.Left);
                _resourceTexts[i].fontStyle = FontStyles.Bold;
            }
        }

        // ── Castle (Heart) HP banner — top-centre, parchment trim. ─────────────
        private void BuildCastleBanner(Transform parent)
        {
            _castleBanner = NewRect("CastleBanner", parent, new Vector2(0.26f, 0.94f), new Vector2(0.74f, 0.99f));
            HudTheme.StylePanel(_castleBanner.gameObject, HudTheme.PanelStone);

            var track = NewRect("Track", _castleBanner, new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.82f));
            track.gameObject.AddComponent<Image>().color = HudTheme.HpTrack;
            var fill = NewRect("Fill", track, Vector2.zero, Vector2.one);
            _castleFill = fill.gameObject.AddComponent<Image>();
            _castleFill.color = HudTheme.CastleGold;
            _castleFill.type = Image.Type.Filled;
            _castleFill.fillMethod = Image.FillMethod.Horizontal;
            _castleFill.fillOrigin = 0;
            _castleFill.fillAmount = 1f;
            _castleText = AddText(track, "Castle 100%", 22, HudTheme.Ink, TextAlignmentOptions.Center);
            _castleText.fontStyle = FontStyles.Bold;
        }

        private void BuildWaveReadout(Transform parent)
        {
            _waveReadout = NewRect("WaveReadout", parent, new Vector2(0.26f, 0.885f), new Vector2(0.74f, 0.935f));
            // No solid panel — floating banner text per the concept.
            _waveText = AddText(_waveReadout, "WAVE 1", 32, HudTheme.Parchment, TextAlignmentOptions.Center);
            _waveText.fontStyle = FontStyles.Bold;
            var stateRect = NewRect("State", _waveReadout, new Vector2(0f, 0f), new Vector2(1f, 0.48f));
            _waveStateText = AddText(stateRect, _lastWaveState, 20, HudTheme.Gold, TextAlignmentOptions.Center);
        }

        // ── Top-left party stack: hero (slot 0) + up to 3 companions. ──────────
        private void BuildPartyFrames(Transform parent)
        {
            _partyFrame   = new GameObject[PartySlotCount];
            _partyHpFill  = new Image[PartySlotCount];
            _partyName    = new TextMeshProUGUI[PartySlotCount];
            _partyHpText  = new TextMeshProUGUI[PartySlotCount];

            _partyStack = NewRect("PartyStack", parent, new Vector2(0.01f, 0.66f), new Vector2(0.30f, 0.93f));
            float rowH = 1f / PartySlotCount;

            for (int i = 0; i < PartySlotCount; i++)
            {
                float yMax = 1f - i * rowH;
                float yMin = 1f - (i + 1) * rowH + 0.014f; // small gap
                var frame = NewRect("Party" + i, _partyStack, new Vector2(0f, yMin), new Vector2(1f, yMax));
                HudTheme.StylePanel(frame.gameObject, HudTheme.PanelStone);
                _partyFrame[i] = frame.gameObject;

                // Portrait swatch (left) — rounded inset, gold-rimmed.
                var port = NewRect("Portrait", frame, new Vector2(0.04f, 0.16f), new Vector2(0.30f, 0.92f));
                var pimg = port.gameObject.AddComponent<Image>();
                pimg.color = HudTheme.PortraitFill;
                pimg.sprite = HudTheme.RoundedFrame;
                pimg.type = Image.Type.Sliced;

                // Name
                var nameRect = NewRect("Name", frame, new Vector2(0.34f, 0.50f), new Vector2(0.98f, 0.95f));
                _partyName[i] = AddText(nameRect, i == 0 ? "Hero" : "—", 20, HudTheme.Gold, TextAlignmentOptions.Left);
                _partyName[i].fontStyle = FontStyles.Bold;

                // HP bar track + fill
                var track = NewRect("HPTrack", frame, new Vector2(0.34f, 0.14f), new Vector2(0.98f, 0.44f));
                track.gameObject.AddComponent<Image>().color = HudTheme.HpTrack;
                var fill = NewRect("HPFill", track, new Vector2(0f, 0f), new Vector2(1f, 1f));
                var fimg = fill.gameObject.AddComponent<Image>();
                fimg.color = HudTheme.HpRed;
                fimg.type = Image.Type.Filled;
                fimg.fillMethod = Image.FillMethod.Horizontal;
                fimg.fillOrigin = 0;
                fimg.fillAmount = 1f;
                _partyHpFill[i] = fimg;
                _partyHpText[i] = AddText(track, "", 14, HudTheme.Parchment, TextAlignmentOptions.Center);

                // Hidden until populated (hero shown immediately once HP pushed).
                _partyFrame[i].SetActive(i == 0);
            }
        }

        // ── Bottom skill bar: HP + mana bars, then 4 ability cells. ────────────
        private void BuildSkillBar(Transform parent)
        {
            _skillBar = NewRect("SkillBar", parent, new Vector2(0.10f, 0.0f), new Vector2(0.90f, 0.135f));
            HudTheme.StylePanel(_skillBar.gameObject, HudTheme.PanelStoneDark);

            // HP bar (top edge of the bar)
            var hpTrack = NewRect("HPTrack", _skillBar, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.95f));
            hpTrack.gameObject.AddComponent<Image>().color = HudTheme.HpTrack;
            var hpFill = NewRect("HPFill", hpTrack, Vector2.zero, Vector2.one);
            _hpFill = hpFill.gameObject.AddComponent<Image>();
            _hpFill.color = HudTheme.HpRed;
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = 0;
            _hpFill.fillAmount = 1f;
            _hpText = AddText(hpTrack, "", 18, HudTheme.Parchment, TextAlignmentOptions.Center);

            // Mana bar (just below HP)
            var mTrack = NewRect("ManaTrack", _skillBar, new Vector2(0.04f, 0.63f), new Vector2(0.96f, 0.78f));
            mTrack.gameObject.AddComponent<Image>().color = HudTheme.ManaTrack;
            var mFill = NewRect("ManaFill", mTrack, Vector2.zero, Vector2.one);
            _manaFill = mFill.gameObject.AddComponent<Image>();
            _manaFill.color = HudTheme.ManaBlue;
            _manaFill.type = Image.Type.Filled;
            _manaFill.fillMethod = Image.FillMethod.Horizontal;
            _manaFill.fillOrigin = 0;
            _manaFill.fillAmount = 1f;
            _manaText = AddText(mTrack, "", 16, HudTheme.Parchment, TextAlignmentOptions.Center);

            // 4 ability cells across the lower portion (large touch targets).
            _slotKey      = new TextMeshProUGUI[AbilitySlotCount];
            _slotGlyph    = new TextMeshProUGUI[AbilitySlotCount];
            _slotAccent   = new Image[AbilitySlotCount];
            _slotCooldown = new Image[AbilitySlotCount];

            string[] defaultKeys = { "Q", "W", "E", "R" };
            float cellW = 0.22f;
            float gap = 0.013f;
            float totalW = AbilitySlotCount * cellW + (AbilitySlotCount - 1) * gap;
            float x0 = 0.5f - totalW / 2f;

            for (int i = 0; i < AbilitySlotCount; i++)
            {
                float x = x0 + i * (cellW + gap);
                var cell = NewRect("Slot" + i, _skillBar, new Vector2(x, 0.06f), new Vector2(x + cellW, 0.58f));
                var cellImg = cell.gameObject.AddComponent<Image>();
                cellImg.color = HudTheme.SlotBack;
                cellImg.sprite = HudTheme.RoundedFrame;
                cellImg.type = Image.Type.Sliced;

                // Accent rune disc (tinted per ability)
                var disc = NewRect("Accent", cell, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f));
                _slotAccent[i] = disc.gameObject.AddComponent<Image>();
                _slotAccent[i].color = HudTheme.SlotDisc;
                _slotAccent[i].sprite = HudTheme.Disc;
                _slotAccent[i].type = Image.Type.Simple;

                // Glyph
                _slotGlyph[i] = AddText(disc, "", 34, HudTheme.Parchment, TextAlignmentOptions.Center);

                // Cooldown radial overlay (drains as the ability comes off cooldown)
                var cd = NewRect("CD", cell, Vector2.zero, Vector2.one);
                _slotCooldown[i] = cd.gameObject.AddComponent<Image>();
                _slotCooldown[i].color = HudTheme.CdShade;
                _slotCooldown[i].sprite = HudTheme.Disc;
                _slotCooldown[i].type = Image.Type.Filled;
                _slotCooldown[i].fillMethod = Image.FillMethod.Radial360;
                _slotCooldown[i].fillOrigin = (int)Image.Origin360.Top;
                _slotCooldown[i].fillClockwise = false;
                _slotCooldown[i].fillAmount = 0f;
                _slotCooldown[i].raycastTarget = false;

                // Hotkey badge (bottom-right of the cell)
                var keyRect = NewRect("Key", cell, new Vector2(0.62f, 0.0f), new Vector2(1f, 0.30f));
                _slotKey[i] = AddText(keyRect, defaultKeys[i], 18, HudTheme.Gold, TextAlignmentOptions.Center);

                // Click → AbilityRequested(slot)
                var btn = cell.gameObject.AddComponent<Button>();
                int slot = i;
                btn.onClick.AddListener(() => AbilityRequested?.Invoke(slot));
            }
        }

        // ── Build entry — bottom-right, gold pill (≥80px touch target). ────────
        private void BuildBuildButton(Transform parent)
        {
            _buildBtn = NewRect("BuildBtn", parent, new Vector2(0.86f, 0.145f), new Vector2(0.99f, 0.225f));
            var bimg = _buildBtn.gameObject.AddComponent<Image>();
            bimg.color = HudTheme.GoldButton;
            bimg.sprite = HudTheme.RoundedFrame;
            bimg.type = Image.Type.Sliced;
            var btn = _buildBtn.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => BuildRequested?.Invoke());
            var t = AddText(_buildBtn, "BUILD", 22, HudTheme.Ink, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
        }

        // ── "Defend!" / Start-Wave button — centred above the skill bar. ───────
        // Minimal compact button anchored near the wave timer at the TOP. Shown
        // ONLY between waves (countdown / idle / cleared); hidden while a wave is
        // active. The click raises StartWaveRequested, which the Village-side
        // StartWaveHudBridge forwards to WaveManager.ForceBeginNextWave().
        // Initial anchors are placeholders; ApplyResponsiveLayout positions it.
        private void BuildStartWaveButton(Transform parent)
        {
            _startWaveBtn = NewRect("StartWaveBtn", parent, new Vector2(0.40f, 0.815f), new Vector2(0.60f, 0.86f));
            var bimg = _startWaveBtn.gameObject.AddComponent<Image>();
            bimg.color = HudTheme.GoldButton;
            bimg.sprite = HudTheme.RoundedFrame;
            bimg.type = Image.Type.Sliced;
            var btn = _startWaveBtn.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => StartWaveRequested?.Invoke());
            var t = AddText(_startWaveBtn, "DEFEND!", 16, HudTheme.Ink, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            // Hidden until the bridge reports a wave is ready to start.
            _startWaveBtn.gameObject.SetActive(false);
        }

        private void BuildRepairPrompt(Transform parent)
        {
            var p = NewRect("RepairPrompt", parent, new Vector2(0.28f, 0.42f), new Vector2(0.72f, 0.58f));
            HudTheme.StylePanel(p.gameObject, HudTheme.PanelStoneDark);
            _repairPanel = p.gameObject;

            var labelRect = NewRect("Label", p, new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.95f));
            _repairLabel = AddText(labelRect, "", 22, HudTheme.Parchment, TextAlignmentOptions.Center);

            var yes = NewRect("Yes", p, new Vector2(0.10f, 0.10f), new Vector2(0.46f, 0.42f));
            var yimg = yes.gameObject.AddComponent<Image>();
            yimg.color = HudTheme.GoldButton;
            yimg.sprite = HudTheme.RoundedFrame;
            yimg.type = Image.Type.Sliced;
            yes.gameObject.AddComponent<Button>().onClick.AddListener(() => { RepairConfirmRequested?.Invoke(); HideRepairPrompt(); });
            AddText(yes, "Repair", 20, HudTheme.Ink, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

            var no = NewRect("No", p, new Vector2(0.54f, 0.10f), new Vector2(0.90f, 0.42f));
            var nimg = no.gameObject.AddComponent<Image>();
            nimg.color = HudTheme.PanelStone;
            nimg.sprite = HudTheme.RoundedFrame;
            nimg.type = Image.Type.Sliced;
            no.gameObject.AddComponent<Button>().onClick.AddListener(() => { RepairCancelRequested?.Invoke(); HideRepairPrompt(); });
            AddText(no, "Later", 20, HudTheme.Parchment, TextAlignmentOptions.Center);

            _repairPanel.SetActive(false);
        }

        // =====================================================================
        //  RESPONSIVE LAYOUT — re-anchor clusters for portrait vs landscape.
        // =====================================================================
        // Both concepts share the same clusters; only the proportions change.
        // Portrait (concept: hud-vertical-mobile): tall, wider top strips, taller
        // bottom tray. Landscape (concept: hud-landscape): side rails, slimmer
        // top bar, currency tucked top-right, skill bar centred along the bottom.
        private void ApplyResponsiveLayout(bool force)
        {
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
            float aspect = _lastScreenH > 0 ? (float)_lastScreenW / _lastScreenH : 0.5f;
            bool portrait = aspect < 1f;
            if (!force && portrait == _isPortrait) return;
            _isPortrait = portrait;

            // CanvasScaler match: bias to height in portrait, width in landscape so
            // the reference layout doesn't stretch awkwardly across orientations.
            if (_scaler != null)
                _scaler.matchWidthOrHeight = portrait ? 0.5f : 0.35f;

            if (portrait)
            {
                // ── PORTRAIT (mobile-first) — vertical concept ──────────────
                SetAnchors(_partyStack,     new Vector2(0.01f, 0.66f), new Vector2(0.46f, 0.93f));
                SetAnchors(_castleBanner,   new Vector2(0.18f, 0.935f), new Vector2(0.74f, 0.99f));
                SetAnchors(_waveReadout,    new Vector2(0.18f, 0.875f), new Vector2(0.82f, 0.93f));
                SetAnchors(_resourceStrip,  new Vector2(0.46f, 0.935f), new Vector2(1f, 0.99f));
                // Bottom tray a little taller for big thumb targets.
                SetAnchors(_skillBar,       new Vector2(0.06f, 0.0f), new Vector2(0.94f, 0.145f));
                SetAnchors(_buildBtn,       new Vector2(0.84f, 0.155f), new Vector2(0.99f, 0.235f));
                // Minimal — small tap target just under the top wave timer (top-center).
                SetAnchors(_startWaveBtn,   new Vector2(0.40f, 0.815f), new Vector2(0.60f, 0.865f));
            }
            else
            {
                // ── LANDSCAPE (web/tablet) — horizontal concept ─────────────
                // Party rail hugs the left; currency strip tucked top-right;
                // castle banner + wave centred along a slim top bar.
                SetAnchors(_partyStack,     new Vector2(0.005f, 0.42f), new Vector2(0.20f, 0.98f));
                SetAnchors(_castleBanner,   new Vector2(0.34f, 0.92f), new Vector2(0.66f, 0.99f));
                SetAnchors(_waveReadout,    new Vector2(0.34f, 0.85f), new Vector2(0.66f, 0.915f));
                SetAnchors(_resourceStrip,  new Vector2(0.74f, 0.92f), new Vector2(0.995f, 0.99f));
                // Skill bar narrower & centred; build button bottom-right.
                SetAnchors(_skillBar,       new Vector2(0.22f, 0.0f), new Vector2(0.78f, 0.16f));
                SetAnchors(_buildBtn,       new Vector2(0.90f, 0.18f), new Vector2(0.995f, 0.28f));
                // Minimal — small tap target just under the top wave timer (top-center).
                SetAnchors(_startWaveBtn,   new Vector2(0.44f, 0.785f), new Vector2(0.56f, 0.84f));
            }
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // =====================================================================
        //  IVillageHud — passive setters pushed by the Village-side bridges
        // =====================================================================
        public void SetWave(int waveNumber)
        {
            _lastWaveNumber = waveNumber;
            if (_waveText != null) _waveText.text = "WAVE " + waveNumber;
        }

        public void SetCountdown(float secondsRemaining)
        {
            if (secondsRemaining > 0.1f)
            {
                _lastWaveState = "Prepare — " + secondsRemaining.ToString("0.0") + "s";
                if (_waveStateText != null) _waveStateText.text = _lastWaveState;
            }
            else
            {
                _lastWaveState = "Defend";
                if (_waveStateText != null) _waveStateText.text = _lastWaveState;
            }
        }

        public void SetHeartHp(float current, float maxHp)
        {
            if (maxHp <= 0f) return;
            float pct = Mathf.Clamp01(current / maxHp);
            if (_castleFill != null) _castleFill.fillAmount = pct;
            if (_castleText != null) _castleText.text = "Castle " + Mathf.RoundToInt(pct * 100f) + "%";
            // Tint the banner toward red as the castle nears death.
            if (_castleFill != null) _castleFill.color = Color.Lerp(HudTheme.HpRed, HudTheme.CastleGold, Mathf.Clamp01(pct / 0.5f));
        }

        public void SetCrystals(int amount)
        {
            // Crystal is currency cell index 2 in the WO-307 strip.
            if (_resourceTexts != null && _resourceTexts.Length >= 4 && _resourceTexts[2] != null)
                _resourceTexts[2].text = amount.ToString();
        }

        public void SetResources(int wood, int iron, int food, int gems)
        {
            // Concept strip = Wood / Iron / Crystal / Gold. "food" is mapped to the
            // Gold cell (closest currency-of-record); crystals own cell 2. Data
            // bindings are unchanged — only the visual labels shifted (icons/rename
            // detail lands in WO-309).
            if (_resourceTexts == null || _resourceTexts.Length < 4) return;
            _resourceTexts[0].text = wood.ToString();
            _resourceTexts[1].text = iron.ToString();
            _resourceTexts[2].text = gems.ToString();
            _resourceTexts[3].text = food.ToString();
        }

        public void SetAttackDirections(bool north, bool east, bool south, bool west) { /* compass is the separate CompassHud component (owner: compass over minimap) */ }

        public void SetWaveImminent(bool imminent)
        {
            if (_waveText != null) _waveText.color = imminent ? HudTheme.HpRed : HudTheme.Parchment;
            if (_waveStateText != null)
            {
                _lastWaveState = imminent ? "Horde Approaching" : "Defend";
                _waveStateText.text = _lastWaveState;
                _waveStateText.color = imminent ? HudTheme.HpRed : HudTheme.Gold;
            }
        }

        public void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine)
        {
            if (_waveText != null) _waveText.text = "WAVE " + waveNumber + " CLEAR";
            if (_waveStateText != null)
            {
                _waveStateText.text = enemiesDefeated > 0 ? enemiesDefeated + " slain" : "Cleared";
                _waveStateText.color = HudTheme.Gold;
            }
        }

        public void HideWaveClearBanner()
        {
            if (_waveText != null) _waveText.text = "WAVE " + _lastWaveNumber;
            if (_waveStateText != null) { _waveStateText.text = _lastWaveState; _waveStateText.color = HudTheme.Gold; }
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

        public void SetWardsReadout(int wardsLit, int wardsTotal, string summary) { /* surfaced in the Arcane Tower panel, not the persistent HUD */ }

        // =====================================================================
        //  Bridge-reflected extras (not on IVillageHud — resolved by name)
        // =====================================================================

        /// <summary>
        /// Shows / hides the "Defend!" start-wave button. Pushed by the Village-side
        /// StartWaveHudBridge: true between waves (Countdown / Idle / cleared) so the
        /// player can begin the next wave on demand; false while a wave is Active (or
        /// the run is over). Not on IVillageHud — resolved by name like SetMana.
        /// </summary>
        public void SetStartWaveAvailable(bool available)
        {
            _startWaveAvailable = available;
            if (_startWaveBtn != null && _startWaveBtn.gameObject.activeSelf != available)
                _startWaveBtn.gameObject.SetActive(available);
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
            // Hero is always party slot 0.
            SetPartyMember(0, _partyName != null && _partyName[0] != null ? _partyName[0].text : "Hero", current, max);
        }

        /// <summary>Per-slot cooldown sweep (radial drains as the ability returns).</summary>
        public void SetAbilityCooldown(int slot, float remaining, float total)
        {
            if (_slotCooldown == null || slot < 0 || slot >= _slotCooldown.Length || _slotCooldown[slot] == null) return;
            _slotCooldown[slot].fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
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
            if (_slotAccent != null && _slotAccent[slot] != null && !string.IsNullOrEmpty(accentHex)
                && ColorUtility.TryParseHtmlString(accentHex, out var c))
            {
                c.a = 0.92f;
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
