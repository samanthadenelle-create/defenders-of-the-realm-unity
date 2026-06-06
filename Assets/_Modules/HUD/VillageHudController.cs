// =============================================================================
// VillageHudController — sleek classic-RPG combat HUD (pure code-built uGUI).
// -----------------------------------------------------------------------------
// Styled "along the lines of" the classic fantasy-RPG reference: dark, minimal,
// the SKILL BAR is the centrepiece (easy-access abilities), with hero HP/Mana
// bars over it, party frames top-left, and banked resources as a thin top strip.
// The MMO-only bits from the reference (chat tabs, friends/yell, compass, D-pad,
// "unleash the horde") are intentionally dropped — not applicable to a single-
// player TD-RPG.
//
// WIRING (all via the existing reflection bridges — DeNelle.Village → DeNelle.HUD
// is blocked by asmdef isolation, so the Village side calls these by name):
//   • HeroAbilitiesHudBridge  → SetMana / SetAbilityCooldown / SetAbilitySlot /
//                               SetHeroHp + reads the AbilityRequested event.
//   • HeartHudBridge          → SetHeartHp / SetResources / SetCrystals.
//   • WaveHudBridge           → SetWave / SetCountdown / SetWaveImminent.
//   • WaveFeedbackDirector    → ShowWaveClearBanner / HideWaveClearBanner.
//   • WallRepairHudBridge     → ShowRepairPrompt / HideRepairPrompt (+ the
//                               RepairConfirmRequested / RepairCancelRequested events).
//   • BuildButtonBridge / BuildMenuHudBridge → BuildRequested event.
//   • WardTetherService       → SetForgettingLevel / SetWardsReadout.
//
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

        [System.Serializable] public sealed class AbilitySlotEvent : UnityEvent<int> { }

        // ── Theme (dark, sleek classic-RPG) ──────────────────────────────────
        private static readonly Color Panel      = new Color(0.07f, 0.07f, 0.09f, 0.90f);
        private static readonly Color PanelSolid  = new Color(0.10f, 0.10f, 0.13f, 0.98f);
        private static readonly Color Metal       = new Color(0.42f, 0.40f, 0.38f, 1f);
        private static readonly Color MetalDim     = new Color(0.26f, 0.25f, 0.24f, 1f);
        private static readonly Color Gold        = new Color(0.85f, 0.72f, 0.30f, 1f);
        private static readonly Color Parchment   = new Color(0.93f, 0.90f, 0.82f, 1f);
        private static readonly Color HpRed       = new Color(0.78f, 0.13f, 0.13f, 1f);
        private static readonly Color HpTrack     = new Color(0.18f, 0.05f, 0.05f, 1f);
        private static readonly Color ManaBlue    = new Color(0.22f, 0.46f, 0.90f, 1f);
        private static readonly Color ManaTrack   = new Color(0.05f, 0.10f, 0.25f, 1f);
        private static readonly Color CdShade     = new Color(0.03f, 0.03f, 0.05f, 0.78f);

        // ── Canvas + cached widgets ──────────────────────────────────────────
        private Canvas _hudCanvas;

        private TextMeshProUGUI[] _resourceTexts; // 0 Wood, 1 Iron, 2 Food, 3 Crystals

        private TextMeshProUGUI _waveText;
        private int _lastWaveNumber = 1;

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
            Debug.Log("[VillageHudController] Sleek classic-RPG HUD active (skill bar + vitals + party + resources).");
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
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _rootGroup = go.AddComponent<CanvasGroup>();

            BuildResourceStrip(go.transform);
            BuildWaveReadout(go.transform);
            BuildPartyFrames(go.transform);
            BuildSkillBar(go.transform);
            BuildBuildButton(go.transform);
            BuildRepairPrompt(go.transform);
        }

        // Thin banked-resource strip across the very top.
        private void BuildResourceStrip(Transform parent)
        {
            var strip = NewRect("ResourceStrip", parent, new Vector2(0f, 0.955f), new Vector2(1f, 1f));
            strip.gameObject.AddComponent<Image>().color = Panel;

            string[] names = { "Wood", "Iron", "Food", "Gems" };
            _resourceTexts = new TextMeshProUGUI[4];
            float w = 0.25f;
            for (int i = 0; i < 4; i++)
            {
                var cell = NewRect("Res_" + names[i], strip, new Vector2(i * w, 0f), new Vector2((i + 1) * w, 1f));
                var tmp = AddText(cell, names[i] + " 0", 26, Gold, TextAlignmentOptions.Center);
                _resourceTexts[i] = tmp;
            }
        }

        private void BuildWaveReadout(Transform parent)
        {
            var waveRect = NewRect("WaveReadout", parent, new Vector2(0.34f, 0.905f), new Vector2(0.66f, 0.95f));
            _waveText = AddText(waveRect, "WAVE 1", 30, Parchment, TextAlignmentOptions.Center);
            _waveText.fontStyle = FontStyles.Bold;
        }

        // Top-left vertical party stack: hero (slot 0) + up to 3 companions.
        private void BuildPartyFrames(Transform parent)
        {
            _partyFrame   = new GameObject[PartySlotCount];
            _partyHpFill  = new Image[PartySlotCount];
            _partyName    = new TextMeshProUGUI[PartySlotCount];
            _partyHpText  = new TextMeshProUGUI[PartySlotCount];

            var stack = NewRect("PartyStack", parent, new Vector2(0.01f, 0.66f), new Vector2(0.34f, 0.90f));
            float rowH = 1f / PartySlotCount;

            for (int i = 0; i < PartySlotCount; i++)
            {
                float yMax = 1f - i * rowH;
                float yMin = 1f - (i + 1) * rowH + 0.012f; // small gap
                var frame = NewRect("Party" + i, stack, new Vector2(0f, yMin), new Vector2(1f, yMax));
                frame.gameObject.AddComponent<Image>().color = Panel;
                _partyFrame[i] = frame.gameObject;

                // Portrait swatch (left)
                var port = NewRect("Portrait", frame, new Vector2(0.03f, 0.18f), new Vector2(0.26f, 0.94f));
                port.gameObject.AddComponent<Image>().color = MetalDim;

                // Name
                var nameRect = NewRect("Name", frame, new Vector2(0.30f, 0.52f), new Vector2(0.98f, 0.96f));
                _partyName[i] = AddText(nameRect, i == 0 ? "Hero" : "—", 20, Gold, TextAlignmentOptions.Left);

                // HP bar track + fill
                var track = NewRect("HPTrack", frame, new Vector2(0.30f, 0.14f), new Vector2(0.98f, 0.46f));
                track.gameObject.AddComponent<Image>().color = HpTrack;
                var fill = NewRect("HPFill", track, new Vector2(0f, 0f), new Vector2(1f, 1f));
                var fimg = fill.gameObject.AddComponent<Image>();
                fimg.color = HpRed;
                fimg.type = Image.Type.Filled;
                fimg.fillMethod = Image.FillMethod.Horizontal;
                fimg.fillOrigin = 0;
                fimg.fillAmount = 1f;
                _partyHpFill[i] = fimg;
                _partyHpText[i] = AddText(track, "", 14, Parchment, TextAlignmentOptions.Center);

                // Hidden until populated (hero shown immediately once HP pushed).
                _partyFrame[i].SetActive(i == 0);
            }
        }

        // Bottom-centre centrepiece: HP + mana bars, then 4 ability cells.
        private void BuildSkillBar(Transform parent)
        {
            var bar = NewRect("SkillBar", parent, new Vector2(0.14f, 0.0f), new Vector2(0.86f, 0.135f));
            bar.gameObject.AddComponent<Image>().color = PanelSolid;

            // HP bar (top edge of the bar)
            var hpTrack = NewRect("HPTrack", bar, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.95f));
            hpTrack.gameObject.AddComponent<Image>().color = HpTrack;
            var hpFill = NewRect("HPFill", hpTrack, Vector2.zero, Vector2.one);
            _hpFill = hpFill.gameObject.AddComponent<Image>();
            _hpFill.color = HpRed;
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = 0;
            _hpFill.fillAmount = 1f;
            _hpText = AddText(hpTrack, "", 18, Parchment, TextAlignmentOptions.Center);

            // Mana bar (just below HP)
            var mTrack = NewRect("ManaTrack", bar, new Vector2(0.04f, 0.63f), new Vector2(0.96f, 0.78f));
            mTrack.gameObject.AddComponent<Image>().color = ManaTrack;
            var mFill = NewRect("ManaFill", mTrack, Vector2.zero, Vector2.one);
            _manaFill = mFill.gameObject.AddComponent<Image>();
            _manaFill.color = ManaBlue;
            _manaFill.type = Image.Type.Filled;
            _manaFill.fillMethod = Image.FillMethod.Horizontal;
            _manaFill.fillOrigin = 0;
            _manaFill.fillAmount = 1f;
            _manaText = AddText(mTrack, "", 16, Parchment, TextAlignmentOptions.Center);

            // 4 ability cells across the lower portion.
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
                var cell = NewRect("Slot" + i, bar, new Vector2(x, 0.06f), new Vector2(x + cellW, 0.58f));
                cell.gameObject.AddComponent<Image>().color = Metal;

                // Accent rune disc (tinted per ability)
                var disc = NewRect("Accent", cell, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
                _slotAccent[i] = disc.gameObject.AddComponent<Image>();
                _slotAccent[i].color = MetalDim;

                // Glyph
                _slotGlyph[i] = AddText(disc, "", 34, Parchment, TextAlignmentOptions.Center);

                // Cooldown radial overlay (drains as the ability comes off cooldown)
                var cd = NewRect("CD", cell, Vector2.zero, Vector2.one);
                _slotCooldown[i] = cd.gameObject.AddComponent<Image>();
                _slotCooldown[i].color = CdShade;
                _slotCooldown[i].type = Image.Type.Filled;
                _slotCooldown[i].fillMethod = Image.FillMethod.Radial360;
                _slotCooldown[i].fillOrigin = (int)Image.Origin360.Top;
                _slotCooldown[i].fillClockwise = false;
                _slotCooldown[i].fillAmount = 0f;
                _slotCooldown[i].raycastTarget = false;

                // Hotkey badge (bottom-right of the cell)
                var keyRect = NewRect("Key", cell, new Vector2(0.62f, 0.0f), new Vector2(1f, 0.30f));
                _slotKey[i] = AddText(keyRect, defaultKeys[i], 18, Gold, TextAlignmentOptions.Center);

                // Click → AbilityRequested(slot)
                var btn = cell.gameObject.AddComponent<Button>();
                int slot = i;
                btn.onClick.AddListener(() => AbilityRequested?.Invoke(slot));
            }
        }

        // Single small build entry (build is a core pillar; the busy MMO chrome is dropped).
        private void BuildBuildButton(Transform parent)
        {
            var b = NewRect("BuildBtn", parent, new Vector2(0.88f, 0.145f), new Vector2(0.99f, 0.225f));
            b.gameObject.AddComponent<Image>().color = Panel;
            var btn = b.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => BuildRequested?.Invoke());
            AddText(b, "BUILD", 20, Gold, TextAlignmentOptions.Center);
        }

        private void BuildRepairPrompt(Transform parent)
        {
            var p = NewRect("RepairPrompt", parent, new Vector2(0.30f, 0.42f), new Vector2(0.70f, 0.58f));
            p.gameObject.AddComponent<Image>().color = PanelSolid;
            _repairPanel = p.gameObject;

            var labelRect = NewRect("Label", p, new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.95f));
            _repairLabel = AddText(labelRect, "", 22, Parchment, TextAlignmentOptions.Center);

            var yes = NewRect("Yes", p, new Vector2(0.10f, 0.10f), new Vector2(0.46f, 0.42f));
            yes.gameObject.AddComponent<Image>().color = Metal;
            yes.gameObject.AddComponent<Button>().onClick.AddListener(() => { RepairConfirmRequested?.Invoke(); HideRepairPrompt(); });
            AddText(yes, "Repair", 20, Gold, TextAlignmentOptions.Center);

            var no = NewRect("No", p, new Vector2(0.54f, 0.10f), new Vector2(0.90f, 0.42f));
            no.gameObject.AddComponent<Image>().color = MetalDim;
            no.gameObject.AddComponent<Button>().onClick.AddListener(() => { RepairCancelRequested?.Invoke(); HideRepairPrompt(); });
            AddText(no, "Later", 20, Parchment, TextAlignmentOptions.Center);

            _repairPanel.SetActive(false);
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
            if (_waveText == null) return;
            _waveText.text = secondsRemaining > 0.1f
                ? "WAVE " + _lastWaveNumber + "  " + secondsRemaining.ToString("0.0") + "s"
                : "WAVE " + _lastWaveNumber;
        }

        public void SetHeartHp(float current, float maxHp)
        {
            // The Heart is the slot-0 party frame's vital companion to the hero bar;
            // surfaced on the wave readout when low so the player feels the stakes.
            if (maxHp <= 0f) return;
            float pct = Mathf.Clamp01(current / maxHp);
            if (_waveText != null && pct <= 0.35f)
                _waveText.color = Color.Lerp(HpRed, Parchment, pct / 0.35f);
        }

        public void SetCrystals(int amount)
        {
            if (_resourceTexts != null && _resourceTexts.Length >= 4 && _resourceTexts[3] != null)
                _resourceTexts[3].text = "Gems " + amount;
        }

        public void SetResources(int wood, int iron, int food, int gems)
        {
            if (_resourceTexts == null || _resourceTexts.Length < 4) return;
            _resourceTexts[0].text = "Wood " + wood;
            _resourceTexts[1].text = "Iron " + iron;
            _resourceTexts[2].text = "Food " + food;
            _resourceTexts[3].text = "Gems " + gems;
        }

        public void SetAttackDirections(bool north, bool east, bool south, bool west) { /* compass dropped — not applicable */ }

        public void SetWaveImminent(bool imminent)
        {
            if (_waveText != null) _waveText.color = imminent ? HpRed : Parchment;
        }

        public void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine)
        {
            if (_waveText != null) _waveText.text = "WAVE " + waveNumber + " CLEAR";
        }

        public void HideWaveClearBanner()
        {
            if (_waveText != null) _waveText.text = "WAVE " + _lastWaveNumber;
        }

        public void ShowRepairPrompt(string wallLabel, float damagePercent)
        {
            if (_repairPanel == null) return;
            if (_repairLabel != null)
                _repairLabel.text = string.Format("Repair {0}? ({0:0}% damaged)", wallLabel, Mathf.RoundToInt(damagePercent * 100f));
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
