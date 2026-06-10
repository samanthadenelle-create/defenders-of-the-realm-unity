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
    ///
    /// VISUAL STYLE: cohesive with the town/combat HUD + inventory rework — warm light parchment
    /// panels, thin gilt/rune rims, dark-ink text, ROUNDED panels/cards/bars (procedural sprites),
    /// circular rune-framed party portraits, a clean ATB gauge with a gold "ready" pop, and the
    /// ElarionUi crest glyph + typography ladder. Mirrors ElarionUi token VALUES locally (this
    /// assembly references Core data, not Core.UI styling) so the battle screen reads as ONE UI.
    ///
    /// Self-contained: creates its own ScreenSpaceOverlay Canvas + Scaler if none provided.
    /// No UXML/UIDocument — pure uGUI + procedural sprites so it works in player builds (WebGL-safe).
    /// </summary>
    public sealed class BattleHudUgui : MonoBehaviour
    {
        // ── Callbacks (same surface as old BattleHud for easy wiring in BattleController)
        public Action<BattleAction> OnAction;
        public Action<string, ControlMode> OnControlModeToggled;

        // ── LIGHT mystical-medieval palette (north-star: warm parchment + dark ink + gilt)
        // Self-contained; mirrors ElarionUi token VALUES (Parchment/Ink/Gold/Gilt) so the
        // battle screen matches the (now-light) town HUD + inventory. Do NOT flip the global kit.
        private static readonly Color Parchment = new Color(0.929f, 0.902f, 0.839f, 0.97f);   // EDE6D6 main panel fill
        private static readonly Color ParchmentLite = new Color(0.957f, 0.933f, 0.875f, 0.98f); // F4EEDF sub-panel / lighter card
        private static readonly Color Ink = new Color(0.137f, 0.098f, 0.055f, 1f);              // dark ink text (readable on light)
        private static readonly Color InkDim = new Color(0.34f, 0.28f, 0.20f, 1f);              // muted ink for secondary text
        private static readonly Color Gold = new Color(0.831f, 0.686f, 0.216f, 1f);             // ElarionUi.Gold accent
        private static readonly Color Gilt = new Color(0.933f, 0.784f, 0.282f, 1f);             // ElarionUi.Gilt bright rim/glow
        private static readonly Color GiltRim = new Color(0.933f, 0.784f, 0.282f, 0.85f);       // thin glowing gilt border

        // Panels / sub-panels now LIGHT parchment
        private static readonly Color PanelBg = Parchment;
        private static readonly Color SubPanelBg = ParchmentLite;

        // Text reads as dark ink (kept name `White` to avoid touching call-sites; now ink)
        private static readonly Color White = new Color(0.137f, 0.098f, 0.055f, 1f);

        // Gauges — deeper saturated fills so they pop on the light parchment track
        private static readonly Color HpFillColor = new Color(0.27f, 0.62f, 0.32f, 1f);
        private static readonly Color MpFillColor = new Color(0.22f, 0.46f, 0.90f, 1f);
        private static readonly Color AtbFillColor = new Color(0.55f, 0.38f, 0.82f, 1f);   // Aether violet (charging)
        private static readonly Color AtbReadyColor = new Color(0.93f, 0.78f, 0.28f, 1f);  // Gilt gold (ready)

        // Bar track on a light bg: warm tan recess (not black)
        private static readonly Color BarTrack = new Color(0.70f, 0.64f, 0.53f, 0.95f);

        // Buttons: soft parchment with gilt highlight; ink label
        private static readonly Color ButtonNormal = new Color(0.886f, 0.847f, 0.757f, 1f);
        private static readonly Color ButtonHighlight = new Color(0.957f, 0.886f, 0.690f, 1f);
        private static readonly Color ButtonPressed = new Color(0.80f, 0.72f, 0.55f, 1f);

        // Portrait placeholder ring + fill (rune-framed circle look)
        private static readonly Color PortraitFill = new Color(0.74f, 0.66f, 0.50f, 1f);

        // ── ElarionUi parity tokens (local mirror — keeps battle screen on the one ladder)
        private const string CrestGlyph = "*"; // decorative crest (font-safe ASCII)
        private const int FontTitle = 24;
        private const int FontHead = 18;
        private const int FontBody = 15;
        private const int FontLabel = 13;
        private const int FontMicro = 11;
        private const float RadiusPanel = 14f;
        private const float RadiusCard = 10f;
        private const float RadiusButton = 9f;

        // ── Procedural sprite cache (rounded panels/bars + circular portraits/rings) ─
        // Built once, reused across every Image so the look is cohesive and WebGL-safe
        // (no texture assets, no UXML — survives the player-build trap).
        private static Sprite _roundedSprite;     // 9-slice rounded rect (panels, cards, buttons, bars)
        private static Sprite _circleSprite;       // solid disc (portraits)
        private static Sprite _ringSprite;         // hollow gilt ring (portrait frame)

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
            public Image PortraitRing;
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

        // ── Procedural sprite builders ───────────────────────────────────────
        // Cheap runtime textures (built once) give the cohesive ROUNDED parchment
        // look without shipping art. Rounded panels/cards/bars + circular portraits.

        /// <summary>9-slice rounded-rect sprite shared by every panel/card/button/bar.</summary>
        private static Sprite RoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            const int size = 48;
            const int radius = 14;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = RoundedRectAlpha(x, y, size, size, radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            // Border = radius so the corners 9-slice cleanly and edges stay crisp.
            _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _roundedSprite;
        }

        /// <summary>Solid anti-aliased disc — party portraits.</summary>
        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int size = 64;
            float r = size * 0.5f - 1f;
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        /// <summary>Hollow ring — gilt rune frame around a portrait.</summary>
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 64;
            float ro = size * 0.5f - 1f;
            float ri = ro - 5f; // ring thickness
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float aOut = Mathf.Clamp01(ro - d + 0.5f);
                    float aIn = Mathf.Clamp01(d - ri + 0.5f);
                    float a = Mathf.Min(aOut, aIn);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }

        /// <summary>Coverage of a rounded-rect at pixel (x,y) — 1 inside, 0 outside, AA on the arc.</summary>
        private static float RoundedRectAlpha(int x, int y, int w, int h, int radius)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float dx = Mathf.Max(Mathf.Max(radius - px, px - (w - radius)), 0f);
            float dy = Mathf.Max(Mathf.Max(radius - py, py - (h - radius)), 0f);
            if (dx <= 0f || dy <= 0f) return 1f;                 // straight edges / interior
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius - d + 0.5f);             // AA corner arc
        }

        /// <summary>Make an Image render as a rounded parchment panel/card/button/bar.</summary>
        private static void MakeRounded(Image img, Color color)
        {
            img.sprite = RoundedSprite();
            img.type = Image.Type.Sliced;
            img.color = color;
        }

        private void CreateInfoPanel()
        {
            _infoPanel = new GameObject("BattleInfoPanel");
            var rt = _infoPanel.AddComponent<RectTransform>();
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -24);
            rt.sizeDelta = new Vector2(720, 76);

            var bg = _infoPanel.AddComponent<Image>();
            MakeRounded(bg, ParchmentLite);
            AddGiltRim(_infoPanel);

            // Title "✦ The Last Stand ✦" — gilt gold crest + letter-spaced ink-gold title
            var titleGO = CreateText($"{CrestGlyph}  The Last Stand  {CrestGlyph}", _infoPanel.transform, new Vector2(0, 18), FontTitle, Gold, TextAlignmentOptions.Center);
            _titleText = titleGO.GetComponent<TextMeshProUGUI>();
            _titleText.characterSpacing = 6f;
            _titleText.GetComponent<RectTransform>().sizeDelta = new Vector2(700, 34);

            // Thin gilt rule under the title (cohesive with ElarionUi.MakeRule)
            var rule = new GameObject("TitleRule");
            rule.transform.SetParent(_infoPanel.transform, false);
            var ruleRt = rule.AddComponent<RectTransform>();
            ruleRt.anchoredPosition = new Vector2(0, -2);
            ruleRt.sizeDelta = new Vector2(360, 2);
            var ruleImg = rule.AddComponent<Image>();
            MakeRounded(ruleImg, new Color(Gold.r, Gold.g, Gold.b, 0.55f));

            // WAVE — ink (readable on light)
            var waveGO = CreateText("WAVE 1", _infoPanel.transform, new Vector2(-228, -12), FontHead, Ink, TextAlignmentOptions.Left);
            _waveText = waveGO.GetComponent<TextMeshProUGUI>();
            _waveText.characterSpacing = 2f;

            // Active Turn — muted ink
            var turnGO = CreateText("", _infoPanel.transform, new Vector2(228, -12), FontHead, InkDim, TextAlignmentOptions.Right);
            _turnText = turnGO.GetComponent<TextMeshProUGUI>();
        }

        /// <summary>Thin glowing gilt rim + soft gold drop-glow on a panel (light north-star look).
        /// Uses uGUI Outline/Shadow on the panel Image — cheap, WebGL-safe, no sprites.</summary>
        private static void AddGiltRim(GameObject panel)
        {
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = GiltRim;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var glow = panel.AddComponent<Shadow>();
            glow.effectColor = new Color(Gilt.r, Gilt.g, Gilt.b, 0.28f); // soft gold glow
            glow.effectDistance = new Vector2(0f, -3f);
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
            rt.sizeDelta = new Vector2(272, 208);

            var bg = _commandPanel.AddComponent<Image>();
            MakeRounded(bg, PanelBg);
            AddGiltRim(_commandPanel);

            // Small rune crest header on the command box (cohesive accent)
            var header = CreateText($"{CrestGlyph} COMMAND", _commandPanel.transform, Vector2.zero, FontLabel, Gold, TextAlignmentOptions.Center);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 1);
            headerRt.anchorMax = new Vector2(1, 1);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0, -6);
            headerRt.sizeDelta = new Vector2(0, 20);
            header.GetComponent<TextMeshProUGUI>().characterSpacing = 3f;

            // Classic FF7 vertical command list
            var vlg = _commandPanel.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 4;
            vlg.padding = new RectOffset(10, 10, 30, 10); // top pad clears the header

            _attackBtn = CreateMenuButton("Attack", OnAttackClicked);
            _skillsBtn = CreateMenuButton("Skills", OnSkillsClicked);
            _itemBtn = CreateMenuButton("Item", OnItemClicked);
            _defendBtn = CreateMenuButton("Defend", OnDefendClicked);

            // Skills sub-window (shown on Skills click, positioned to the right of main command)
            _skillsSubPanel = new GameObject("SkillsSubPanel");
            var subRt = _skillsSubPanel.AddComponent<RectTransform>();
            subRt.SetParent(_commandPanel.transform, false);
            subRt.anchorMin = new Vector2(1, 0);
            subRt.anchorMax = new Vector2(1, 0);
            subRt.pivot = new Vector2(0, 0);
            subRt.anchoredPosition = new Vector2(12, 0);
            subRt.sizeDelta = new Vector2(228, 168);

            var subBg = _skillsSubPanel.AddComponent<Image>();
            MakeRounded(subBg, SubPanelBg);
            AddGiltRim(_skillsSubPanel);

            var subVlg = _skillsSubPanel.AddComponent<VerticalLayoutGroup>();
            subVlg.childControlHeight = false;
            subVlg.childForceExpandHeight = false;
            subVlg.childControlWidth = true;
            subVlg.childForceExpandWidth = true;
            subVlg.spacing = 3;
            subVlg.padding = new RectOffset(8, 8, 8, 8);

            _skillsSubPanel.SetActive(false);
        }

        private Button CreateMenuButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_commandPanel.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240, 40);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 40;
            le.preferredHeight = 40;

            var img = go.AddComponent<Image>();
            MakeRounded(img, ButtonNormal);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;

            var colors = btn.colors;
            colors.normalColor = Color.white;            // tint multiplies the parchment sprite
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.90f, 0.84f, 0.66f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            // Thin gilt rune rim around each command (circular-rune-frame feel, rounded)
            var rim = new GameObject("Rim");
            rim.transform.SetParent(go.transform, false);
            var rimRt = rim.AddComponent<RectTransform>();
            rimRt.anchorMin = Vector2.zero;
            rimRt.anchorMax = Vector2.one;
            rimRt.offsetMin = Vector2.zero;
            rimRt.offsetMax = Vector2.zero;
            var rimImg = rim.AddComponent<Image>();
            rimImg.sprite = RoundedSprite();
            rimImg.type = Image.Type.Sliced;
            rimImg.color = new Color(Gilt.r, Gilt.g, Gilt.b, 0.0f); // invisible fill; outline provides the rim
            rimImg.raycastTarget = false;
            var rimOutline = rim.AddComponent<Outline>();
            rimOutline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            rimOutline.effectDistance = new Vector2(1f, -1f);

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = FontBody;
            tmp.color = Ink;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 1f;
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
            rt.sizeDelta = new Vector2(432, 232);

            var bg = _partyPanel.AddComponent<Image>();
            MakeRounded(bg, PanelBg);
            AddGiltRim(_partyPanel);

            // Party header crest (cohesive with command box header)
            var header = CreateText($"{CrestGlyph} PARTY", _partyPanel.transform, Vector2.zero, FontLabel, Gold, TextAlignmentOptions.Center);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 1);
            headerRt.anchorMax = new Vector2(1, 1);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0, -6);
            headerRt.sizeDelta = new Vector2(0, 20);
            header.GetComponent<TextMeshProUGUI>().characterSpacing = 3f;

            var vlg = _partyPanel.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 5;
            vlg.padding = new RectOffset(10, 10, 30, 10); // top pad clears header

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
            rt.sizeDelta = new Vector2(412, 48);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 48;
            le.preferredHeight = 48;

            var bg = go.AddComponent<Image>();
            MakeRounded(bg, ParchmentLite);  // lighter rounded card on the parchment panel

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlHeight = false;
            hlg.childControlWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 8;
            hlg.padding = new RectOffset(6, 6, 4, 4);

            // Circular rune-framed portrait (disc fill + gilt ring overlay)
            var portWrap = new GameObject("Portrait");
            portWrap.transform.SetParent(go.transform, false);
            var portRt = portWrap.AddComponent<RectTransform>();
            portRt.sizeDelta = new Vector2(40, 40);
            var portLe = portWrap.AddComponent<LayoutElement>();
            portLe.minWidth = 40; portLe.preferredWidth = 40;
            var portImg = portWrap.AddComponent<Image>();
            portImg.sprite = CircleSprite();
            portImg.color = PortraitFill; // warm tan placeholder disc (real sprite assignable later)

            var ringGO = new GameObject("Ring");
            ringGO.transform.SetParent(portWrap.transform, false);
            var ringRt = ringGO.AddComponent<RectTransform>();
            ringRt.anchorMin = Vector2.zero;
            ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = new Vector2(-2, -2);
            ringRt.offsetMax = new Vector2(2, 2);
            var ringImg = ringGO.AddComponent<Image>();
            ringImg.sprite = RingSprite();
            ringImg.color = new Color(Gold.r, Gold.g, Gold.b, 0.8f); // gilt rune frame (re-tints gold when active)
            ringImg.raycastTarget = false;

            // Right column: name + bars
            var right = new GameObject("RightCol");
            right.transform.SetParent(go.transform, false);
            var rightRt = right.AddComponent<RectTransform>();
            rightRt.sizeDelta = new Vector2(346, 44);
            var rightLe = right.AddComponent<LayoutElement>();
            rightLe.minWidth = 346; rightLe.preferredWidth = 346;
            var v = right.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = false;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;
            v.childAlignment = TextAnchor.MiddleLeft;
            v.spacing = 1;

            // Name — ink on light (active state re-tinted in Render)
            var nameGO = CreateText("Hero", right.transform, Vector2.zero, FontLabel, Ink, TextAlignmentOptions.Left);
            var nameTmp = nameGO.GetComponent<TextMeshProUGUI>();
            nameTmp.characterSpacing = 1f;

            // HP row (bar + inline value)
            var hpRow = CreateBarRow(right.transform, "HP", HpFillColor, out var hpText);
            var hpBar = FindFill(hpRow);

            // MP row
            var mpRow = CreateBarRow(right.transform, "MP", MpFillColor, out var mpText);
            var mpBar = FindFill(mpRow);

            // ATB row (no inline value — the gauge speaks for itself)
            var atbRow = CreateBarRow(right.transform, "ATB", AtbFillColor, out _);
            var atbBar = FindFill(atbRow);

            var slot = new PartySlot
            {
                Root = go,
                Portrait = portImg,
                PortraitRing = ringImg,
                Name = nameTmp,
                HpBar = hpBar,
                HpText = hpText,
                MpBar = mpBar,
                MpText = mpText,
                AtbBar = atbBar
            };
            return slot;
        }

        /// <summary>Find the rounded fill Image inside a bar-row's BarBg.</summary>
        private static Image FindFill(GameObject barBg)
        {
            var fillT = barBg.transform.Find("Fill");
            return fillT != null ? fillT.GetComponent<Image>() : barBg.GetComponentInChildren<Image>(true);
        }

        /// <summary>
        /// One label + a clean rounded gauge + an optional inline value, laid out in a row.
        /// The gauge: warm tan rounded track with a rounded saturated fill that reads clean
        /// against the light parchment. Returns the BarBg (track); the fill is its "Fill" child.
        /// </summary>
        private GameObject CreateBarRow(Transform parent, string label, Color fillColor, out TextMeshProUGUI valueText)
        {
            valueText = null;
            var row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(340, 13);
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 13; rowLe.preferredHeight = 13;

            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.childControlHeight = false;
            h.childControlWidth = false;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.spacing = 5;

            // Label — muted ink, readable on light parchment
            var lbl = CreateText(label, row.transform, Vector2.zero, FontMicro, InkDim, TextAlignmentOptions.Left);
            var lblRt = lbl.GetComponent<RectTransform>();
            lblRt.sizeDelta = new Vector2(26, 12);
            var lblLe = lbl.AddComponent<LayoutElement>();
            lblLe.minWidth = 26; lblLe.preferredWidth = 26;

            // Bar background — warm tan ROUNDED recess on the light card
            var barBg = new GameObject("BarBg");
            barBg.transform.SetParent(row.transform, false);
            var bgRt = barBg.AddComponent<RectTransform>();
            bool hasValue = label == "HP" || label == "MP";
            float trackW = hasValue ? 196f : 250f;
            bgRt.sizeDelta = new Vector2(trackW, 9);
            var bgLe = barBg.AddComponent<LayoutElement>();
            bgLe.minWidth = trackW; bgLe.preferredWidth = trackW;
            bgLe.minHeight = 9; bgLe.preferredHeight = 9;
            var bgImg = barBg.AddComponent<Image>();
            MakeRounded(bgImg, BarTrack);

            // Fill — rounded, horizontally filled
            var fill = new GameObject("Fill");
            fill.transform.SetParent(barBg.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(1, 1);
            fillRt.offsetMax = new Vector2(-1, -1);
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = RoundedSprite();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.color = fillColor;

            // Inline numeric value for HP/MP (compact, right-aligned ink)
            if (hasValue)
            {
                var valGO = CreateText("", row.transform, Vector2.zero, FontMicro, Ink, TextAlignmentOptions.Right);
                var valRt = valGO.GetComponent<RectTransform>();
                valRt.sizeDelta = new Vector2(62, 12);
                var valLe = valGO.AddComponent<LayoutElement>();
                valLe.minWidth = 62; valLe.preferredWidth = 62;
                valueText = valGO.GetComponent<TextMeshProUGUI>();
            }

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

            // Highlight active member: name goes gold + portrait ring brightens to gilt.
            foreach (var s in _partySlots)
            {
                bool isActive = s.Name && s.Name.text == activeName && !string.IsNullOrEmpty(activeName);
                if (s.Name) s.Name.color = isActive ? AtbReadyColor : Ink;
                if (s.PortraitRing)
                    s.PortraitRing.color = isActive
                        ? new Color(Gilt.r, Gilt.g, Gilt.b, 1f)
                        : new Color(Gold.r, Gold.g, Gold.b, 0.8f);
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
