using System;
using System.Collections.Generic;
using System.Linq;
using DeNelle.BattleATB.Engine;
using DeNelle.BattleATB.State;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// FF7-style Active Battle HUD implemented as pure code-built uGUI (Canvas + Image + TMP + Layouts).
    /// Matches the requested layout: Command window bottom-left (Attack/Skills/Item/Defend + dynamic skills sub),
    /// Party status bottom-right (4 slots: portrait placeholder, name, HP/MP/ATB bars+text),
    /// Top info ("The Last Stand", WAVE, active turn).
    /// 
    /// Designed to replace the overly complex previous VisualElement-based BattleHud.
    /// Keeps the same callback surface (OnAction, OnControlModeToggled) for drop-in with BattleController / ATBRuntimeState.
    /// Uses existing BattleState for data (units, HP, ATB visual fill, active unit, wave, etc.).
    /// Blue/grey FF7 aesthetic via colors (easy to skin with 9-slice sprites later).
    /// 
    /// Self-contained: creates its own ScreenSpaceOverlay Canvas + Scaler if none provided.
    /// No UXML/UIDocument — pure uGUI so it works in builds.
    /// </summary>
    public sealed class BattleHudUgui : MonoBehaviour
    {
        // ── Callbacks (same surface as old BattleHud for easy wiring in BattleController)
        public Action<BattleAction> OnAction;
        public Action<string, ControlMode> OnControlModeToggled;

        // ── FF7-ish palette (classic blue/grey with gold accents)
        private static readonly Color PanelBg = new Color(0.12f, 0.18f, 0.35f, 0.92f);
        private static readonly Color SubPanelBg = new Color(0.10f, 0.15f, 0.30f, 0.95f);
        private static readonly Color Gold = new Color(0.95f, 0.85f, 0.45f, 1f);
        private static readonly Color White = Color.white;
        private static readonly Color HpFillColor = new Color(0.25f, 0.75f, 0.35f, 1f);
        private static readonly Color MpFillColor = new Color(0.35f, 0.55f, 0.95f, 1f);
        private static readonly Color AtbFillColor = new Color(0.65f, 0.55f, 0.95f, 1f);
        private static readonly Color AtbReadyColor = new Color(0.98f, 0.82f, 0.25f, 1f);
        private static readonly Color ButtonNormal = new Color(0.15f, 0.22f, 0.38f, 1f);
        private static readonly Color ButtonHighlight = new Color(0.25f, 0.35f, 0.55f, 1f);

        // ── Roots
        private Canvas _canvas;
        private GameObject _commandPanel;
        private GameObject _partyPanel;
        private GameObject _infoPanel;
        private GameObject _skillsSubPanel;

        // Buttons
        private Button _attackBtn;
        private Button _skillsBtn;
        private Button _itemBtn;
        private Button _defendBtn;

        // Info
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _waveText;
        private TextMeshProUGUI _turnText;

        // Party slots (4 as per spec)
        private readonly List<PartySlot> _partySlots = new List<PartySlot>();

        // Skills list (dynamic per active hero)
        private readonly List<Button> _skillButtons = new List<Button>();

        // State
        private BattleState _lastState;
        private string _activeMemberId;
        private PickKind _pendingKind = PickKind.None;
        private AbilitySlot _pendingAbility;
        private ItemKind _pendingItem;

        // Visual ATB simulation (engine is discrete; this gives the "filling" feel without touching engine)
        private readonly Dictionary<string, float> _visualAtb = new Dictionary<string, float>();
        private const float VisualAtbChargeSeconds = 3.0f;

        private enum PickKind { None, Ability, Item }

        private class PartySlot
        {
            public GameObject Root;
            public Image Portrait;
            public TextMeshProUGUI Name;
            public Image HpBar;
            public TextMeshProUGUI HpText;
            public Image MpBar;
            public TextMeshProUGUI MpText;
            public Image AtbBar;
        }

        /// <summary>Build the full FF7-style HUD. Creates its own Canvas if none supplied.</summary>
        public void Build(Canvas existingCanvas = null)
        {
            if (existingCanvas != null)
            {
                _canvas = existingCanvas;
            }
            else
            {
                var canvasGO = new GameObject("BattleHUD_Canvas");
                _canvas = canvasGO.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100;

                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasGO.AddComponent<GraphicRaycaster>();
            }

            // Top info
            CreateInfoPanel();

            // Bottom-left command window (classic FF7 box + 4 commands + skills sub)
            CreateCommandPanel();

            // Bottom-right party status (exactly 4 slots as specified)
            CreatePartyPanel();

            // Initial state
            _skillsSubPanel.SetActive(false);
        }

        private void CreateInfoPanel()
        {
            _infoPanel = new GameObject("BattleInfoPanel");
            var rt = _infoPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -30);
            rt.sizeDelta = new Vector2(700, 70);

            var bg = _infoPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.85f);

            // Title "The Last Stand"
            var titleGO = CreateText("The Last Stand", _infoPanel.transform, new Vector2(0, 18), 26, Gold, TextAlignmentOptions.Center);
            _titleText = titleGO.GetComponent<TextMeshProUGUI>();

            // WAVE
            var waveGO = CreateText("WAVE 1", _infoPanel.transform, new Vector2(-220, -8), 18, Color.yellow, TextAlignmentOptions.Left);
            _waveText = waveGO.GetComponent<TextMeshProUGUI>();

            // Active Turn
            var turnGO = CreateText("", _infoPanel.transform, new Vector2(220, -8), 18, Color.cyan, TextAlignmentOptions.Right);
            _turnText = turnGO.GetComponent<TextMeshProUGUI>();
        }

        private GameObject CreateText(string txt, Transform parent, Vector2 anchoredPos, int fontSize, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(280, 32);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = txt;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private void CreateCommandPanel()
        {
            _commandPanel = new GameObject("CommandPanel");
            var rt = _commandPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(30, 30);
            rt.sizeDelta = new Vector2(260, 180);

            var bg = _commandPanel.AddComponent<Image>();
            bg.color = PanelBg;

            // Classic FF7 vertical command list
            var vlg = _commandPanel.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 3;
            vlg.padding = new RectOffset(6, 6, 6, 6);

            _attackBtn = CreateMenuButton("Attack", OnAttackClicked);
            _skillsBtn = CreateMenuButton("Skills", OnSkillsClicked);
            _itemBtn = CreateMenuButton("Item", OnItemClicked);
            _defendBtn = CreateMenuButton("Defend", OnDefendClicked);

            // Skills sub-window (shown on Skills click, positioned to the right of main command)
            _skillsSubPanel = new GameObject("SkillsSubPanel");
            var subRt = _skillsSubPanel.AddComponent<RectTransform>();
            subRt.SetParent(_commandPanel.transform, false);
            subRt.anchoredPosition = new Vector2(280, 0);
            subRt.sizeDelta = new Vector2(220, 160);

            var subBg = _skillsSubPanel.AddComponent<Image>();
            subBg.color = SubPanelBg;

            var subVlg = _skillsSubPanel.AddComponent<VerticalLayoutGroup>();
            subVlg.spacing = 2;
            subVlg.padding = new RectOffset(4, 4, 4, 4);

            _skillsSubPanel.SetActive(false);
        }

        private Button CreateMenuButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_commandPanel.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240, 36);

            var img = go.AddComponent<Image>();
            img.color = ButtonNormal;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.highlightedColor = ButtonHighlight;
            btn.colors = colors;

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.color = Gold;
            tmp.alignment = TextAlignmentOptions.Center;
            var txtRt = txtGO.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            return btn;
        }

        private void OnAttackClicked() => SubmitAction(new BattleAction { Kind = ActionKind.Attack });
        private void OnSkillsClicked() => ShowSkillsSubmenu();
        private void OnItemClicked() 
        { 
            /* populate item submenu similarly */ 
        }
        private void OnDefendClicked() => SubmitAction(new BattleAction { Kind = ActionKind.Defend });

        private void ShowSkillsSubmenu()
        {
            _skillsSubPanel.SetActive(true);
            foreach (Transform t in _skillsSubPanel.transform) Destroy(t.gameObject);
            _skillButtons.Clear();

            // Dynamic skills from current active member (use engine defs / catalog for the hero class)
            var abilities = GetAbilitiesForActiveHero();
            foreach (var ab in abilities)
            {
                var b = CreateMenuButton($"{ab.Name} ({ab.Cost} MP)", () => SubmitAbility(ab.Slot));
                b.transform.SetParent(_skillsSubPanel.transform, false);
                _skillButtons.Add(b);
            }
        }

        private List<AbilityDef> GetAbilitiesForActiveHero()
        {
            if (_lastState == null) return new List<AbilityDef>();
            var state = _lastState;  // BattleState
            string activeId = state.ActiveUnitId;
            if (string.IsNullOrEmpty(activeId)) return new List<AbilityDef>();

            var unit = state.Units.FirstOrDefault(u => u.Id == activeId);
            if (unit?.HeroClass == null) return new List<AbilityDef>();

            if (DeNelle.BattleATB.Engine.Defs.HERO_ABILITIES.TryGetValue(unit.HeroClass.Value, out var defs))
            {
                return defs.ToList();
            }
            return new List<AbilityDef>();
        }

        private void SubmitAction(BattleAction action)
        {
            OnAction?.Invoke(action);
            _skillsSubPanel.SetActive(false);
        }

        private void SubmitAbility(AbilitySlot slot)
        {
            var action = BattleAction.MakeAbility(slot);
            OnAction?.Invoke(action);
            _skillsSubPanel.SetActive(false);
        }

        private void CreatePartyPanel()
        {
            _partyPanel = new GameObject("PartyStatusPanel");
            var rt = _partyPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-30, 30);
            rt.sizeDelta = new Vector2(420, 200);

            var bg = _partyPanel.AddComponent<Image>();
            bg.color = PanelBg;

            var vlg = _partyPanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(6, 6, 6, 6);

            _partySlots.Clear();
            for (int i = 0; i < 4; i++) // exactly as specified
            {
                var slot = CreatePartySlot();
                slot.Root.transform.SetParent(_partyPanel.transform, false);
                _partySlots.Add(slot);
            }
        }

        private PartySlot CreatePartySlot()
        {
            var go = new GameObject("PartySlot");
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 46);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.padding = new RectOffset(4, 4, 2, 2);

            // Portrait (placeholder square — user can assign real sprite from Resources/HeroPortraits later)
            var portGO = new GameObject("Portrait");
            portGO.transform.SetParent(go.transform, false);
            var portRt = portGO.AddComponent<RectTransform>();
            portRt.sizeDelta = new Vector2(38, 38);
            var portImg = portGO.AddComponent<Image>();
            portImg.color = new Color(0.4f, 0.4f, 0.5f); // grey placeholder

            // Right column: name + bars
            var right = new GameObject("RightCol");
            right.transform.SetParent(go.transform, false);
            var rightRt = right.AddComponent<RectTransform>();
            rightRt.sizeDelta = new Vector2(340, 42);
            var v = right.AddComponent<VerticalLayoutGroup>();
            v.spacing = 1;

            // Name
            var nameGO = CreateText("Hero", right.transform, Vector2.zero, 13, Gold, TextAlignmentOptions.Left);
            var nameTmp = nameGO.GetComponent<TextMeshProUGUI>();

            // HP row
            var hpRow = CreateBarRow(right.transform, "HP", HpFillColor);
            var hpBar = hpRow.GetComponentInChildren<Image>(true); // the fill
            var hpText = CreateText("120/120", right.transform, Vector2.zero, 11, White, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();

            // MP row
            var mpRow = CreateBarRow(right.transform, "MP", MpFillColor);
            var mpBar = mpRow.GetComponentInChildren<Image>(true);
            var mpText = CreateText("40/40", right.transform, Vector2.zero, 11, White, TextAlignmentOptions.Left).GetComponent<TextMeshProUGUI>();

            // ATB row
            var atbRow = CreateBarRow(right.transform, "ATB", AtbFillColor);
            var atbBar = atbRow.GetComponentInChildren<Image>(true);

            var slot = new PartySlot
            {
                Root = go,
                Portrait = portImg,
                Name = nameTmp,
                HpBar = hpBar,
                HpText = hpText,
                MpBar = mpBar,
                MpText = mpText,
                AtbBar = atbBar
            };
            return slot;
        }

        private GameObject CreateBarRow(Transform parent, string label, Color fillColor)
        {
            var row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320, 14);

            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4;

            // Label
            var lbl = CreateText(label, row.transform, Vector2.zero, 10, Gold, TextAlignmentOptions.Left);
            lbl.GetComponent<RectTransform>().sizeDelta = new Vector2(28, 12);

            // Bar background
            var barBg = new GameObject("BarBg");
            barBg.transform.SetParent(row.transform, false);
            var bgRt = barBg.AddComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(220, 10);
            var bgImg = barBg.AddComponent<Image>();
            bgImg.color = Color.black;

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(barBg.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0, 1); // will be scaled by fillAmount
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;

            return barBg; // return the bg so caller can get the fill child
        }

        // ── Public API for BattleController to drive ─────────────────────────

        public void Render(ATBRuntimeState runtime)
        {
            var state = runtime?.Battle;
            if (state == null) return;
            _lastState = state;

            // Top info
            string activeName = "";
            if (!string.IsNullOrEmpty(state.ActiveUnitId))
            {
                var active = state.Units.FirstOrDefault(u => u.Id == state.ActiveUnitId);
                activeName = active?.Name ?? "";
            }
            if (_turnText) _turnText.text = string.IsNullOrEmpty(activeName) ? "" : activeName + "'s Turn";
            if (_waveText) _waveText.text = "WAVE 1";  // wave info can come from runtime or setup in full integration

            // Party slots (first 4 party members)
            int idx = 0;
            foreach (var u in state.Units.Where(u => u.Side == Side.Party))
            {
                if (idx >= _partySlots.Count) break;
                var s = _partySlots[idx++];
                UpdateSlot(s, u);
            }

            // Highlight active (simple tint on name or bar)
            foreach (var s in _partySlots)
            {
                if (s.Name) s.Name.color = (s.Name.text == activeName) ? AtbReadyColor : Gold;
            }
        }

        private void UpdateSlot(PartySlot slot, BattleUnit u)
        {
            if (slot.Name) slot.Name.text = u.Name ?? "???";
            if (slot.HpText) slot.HpText.text = $"{u.Hp}/{u.MaxHp}";
            if (slot.HpBar) slot.HpBar.fillAmount = u.MaxHp > 0 ? Mathf.Clamp01((float)u.Hp / u.MaxHp) : 0;

            // Resource acts as MP
            int curRes = u.Resource;
            int maxRes = u.MaxResource;
            if (slot.MpText) slot.MpText.text = $"{curRes}/{maxRes}";
            if (slot.MpBar) slot.MpBar.fillAmount = maxRes > 0 ? Mathf.Clamp01((float)curRes / maxRes) : 0;

            // ATB — use visual simulation or unit Atb (double)
            float atb = 0f;
            if (_visualAtb.TryGetValue(u.Id, out float vis)) atb = vis;
            else if (u.Atb > 0) atb = Mathf.Clamp01((float)u.Atb);
            if (slot.AtbBar)
            {
                slot.AtbBar.fillAmount = atb;
                slot.AtbBar.color = atb >= 0.99f ? AtbReadyColor : AtbFillColor;
            }
        }

        public void TickVisualAtb(ATBRuntimeState runtime, float dt)
        {
            if (runtime?.Battle == null) return;
            var state = runtime.Battle;
            foreach (var u in state.Units)
            {
                if (!u.Alive) continue;
                if (!_visualAtb.ContainsKey(u.Id)) _visualAtb[u.Id] = 0f;

                float current = _visualAtb[u.Id];
                if (u.Id == state.ActiveUnitId)
                {
                    _visualAtb[u.Id] = 1f; // pinned when acting
                }
                else
                {
                    float speed = Mathf.Max(0.5f, (float)u.Speed);
                    _visualAtb[u.Id] = Mathf.MoveTowards(current, 1f, dt / (VisualAtbChargeSeconds / speed));
                }
            }
        }

        // Helper for controller to know current active for command enabling
        public string ActiveUnitId => _lastState?.ActiveUnitId;

        // Call from controller when battle ends or to reset
        public void Reset()
        {
            _skillsSubPanel?.SetActive(false);
            _pendingKind = PickKind.None;
            _visualAtb.Clear();
        }
    }
}