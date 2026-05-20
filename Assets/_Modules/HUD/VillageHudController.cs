// =============================================================================
// VillageHudController — drives the in-game village HUD overlay (Week 4).
// -----------------------------------------------------------------------------
// Architecture review item ARC-003: "DeNelle.HUD is an empty asmdef." This is
// the module's first real component — the controller behind VillageHud.uxml,
// the glanceable readout for the Week-4 "playable Wave 1" loop.
//
// MODULE ISOLATION (port spec Part 2) — IMPORTANT:
//   The HUD is a PASSIVE display. It owns NO gameplay state and never reaches
//   into DeNelle.Village (the Heart, the wave manager, the hero ability block).
//   The DeNelle.HUD asmdef references DeNelle.Core + UI Toolkit ONLY. All HUD
//   data is PUSHED IN by the integrator through the public setters below:
//
//     SetHeartHp(current, max)            — Heart HP bar
//     SetCrystals(amount)                 — crystal counter
//     SetWave(number, countdown)          — wave indicator + between-wave timer
//     SetAbilityCooldown(slot, rem, tot)  — one Q/W/E/R cooldown sweep
//     SetMana(current, max)               — hero mana pool
//
//   The "Build" button raises the BuildRequested UnityEvent — the integrator
//   hooks that to the village's BuildMenu.Open() (BuildMenu lives in
//   DeNelle.Village, so the wiring CANNOT happen here). See the integrator
//   notes at the foot of this file and docs/port-notes/hud-module.md.
//
// The four ability cells are built ONCE at runtime into the "ability-bar"
// container — VillageHud.uxml only supplies the bar shell + corner panels.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    /// <summary>
    /// Drives the village in-game HUD: Heart HP bar, crystal counter, wave
    /// indicator, the Q/W/E/R hero ability bar and the Build button. A passive
    /// display — gameplay code pushes data in through the public setters; the
    /// HUD never reads gameplay modules. Lives on the village HUD
    /// <see cref="UIDocument"/>; wired by the village scene builder / integrator.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class VillageHudController : MonoBehaviour
    {
        /// <summary>Number of hero ability slots — the Q/W/E/R kit.</summary>
        public const int AbilitySlotCount = 4;

        [Header("UI")]
        [Tooltip("UIDocument hosting VillageHud.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Heart HP thresholds (fraction of max)")]
        [Tooltip("At or below this HP fraction the Heart bar turns amber (warning).")]
        [SerializeField, Range(0f, 1f)] private float _heartWarningFraction = 0.5f;

        [Tooltip("At or below this HP fraction the Heart bar turns red (critical).")]
        [SerializeField, Range(0f, 1f)] private float _heartCriticalFraction = 0.25f;

        [Header("Wave countdown")]
        [Tooltip("Seconds remaining at or below which the countdown text turns amber + bold.")]
        [SerializeField, Min(0f)] private float _countdownUrgentSeconds = 3f;

        [Header("Repair prompt")]
        [Tooltip("Seconds the repair-feedback toast stays on screen before it auto-hides.")]
        [SerializeField, Min(0.5f)] private float _repairToastSeconds = 2.5f;

        [Header("Events")]
        [Tooltip("Raised when the player taps the Build button. The integrator hooks this " +
                 "to the village BuildMenu.Open() — the HUD cannot reference BuildMenu itself.")]
        public UnityEvent BuildRequested = new UnityEvent();

        [Tooltip("Raised when the player taps the repair prompt's Repair button. The integrator " +
                 "hooks this to WallRepairController.ConfirmRepair() (cross-module — see " +
                 "WallRepairSceneSetup.cs). The HUD cannot reference WallRepairController itself.")]
        public UnityEvent RepairConfirmRequested = new UnityEvent();

        [Tooltip("Raised when the player taps the repair prompt's Cancel button (or the prompt " +
                 "is dismissed). The integrator hooks this to WallRepairController.CancelRepair().")]
        public UnityEvent RepairCancelRequested = new UnityEvent();

        /// <summary>
        /// Raised when the player taps one of the Q/W/E/R ability slots in the
        /// HUD. Arg = slot index (0=Q, 1=W, 2=E, 3=R). The integrator hooks
        /// this to <c>HeroAbilities.TryCast</c> via a cross-asmdef bridge —
        /// the HUD cannot reference DeNelle.Village itself.
        /// </summary>
        [System.Serializable] public sealed class AbilitySlotEvent : UnityEvent<int> { }
        public AbilitySlotEvent AbilityRequested = new AbilitySlotEvent();

        // ── UXML element names — the binding contract with VillageHud.uxml ───
        private const string RootName = "village-hud-root";
        private const string HeartHpFillName = "heart-hp-fill";
        private const string HeartHpLabelName = "heart-hp-label";
        private const string CrystalCountName = "crystal-count";
        private const string WaveNumberName = "wave-number";
        private const string WaveCountdownName = "wave-countdown";
        private const string WaveCountdownTimerName = "wave-countdown-timer";
        private const string WaveCountdownIconName = "wave-countdown-icon";
        private const string AbilityBarName = "ability-bar";
        private const string ManaFillName = "mana-fill";
        private const string ManaLabelName = "mana-label";
        private const string BuildButtonName = "build-button";

        // Repair-prompt elements (Workstream B — player wall-repair mechanic).
        private const string RepairPromptName = "repair-prompt";
        private const string RepairPromptSubtitleName = "repair-prompt-subtitle";
        private const string RepairPromptCostName = "repair-prompt-cost";
        private const string RepairPromptConfirmName = "repair-prompt-confirm";
        private const string RepairPromptCancelName = "repair-prompt-cancel";
        private const string RepairToastName = "repair-toast";

        // ── USS class names — styled by VillageHud.uss ───────────────────────
        private const string HeartWarningClass = "bar-fill--heart-warning";
        private const string HeartCriticalClass = "bar-fill--heart-critical";
        private const string CountdownUrgentClass = "wave-countdown--urgent";
        private const string CountdownTimerUrgentClass = "wave-countdown-timer--urgent";
        private const string CountdownIconUrgentClass = "wave-countdown-icon--urgent";
        private const string RepairCostUnaffordableClass = "repair-prompt-cost--unaffordable";
        private const string RepairToastErrorClass = "repair-toast--error";
        private const string AbilitySlotClass = "ability-slot";
        private const string AbilitySlotReadyClass = "ability-slot--ready";
        private const string AbilitySlotCoolingClass = "ability-slot--cooling";
        private const string AbilityKeyClass = "ability-key";
        private const string AbilityIconClass = "ability-icon";
        private const string AbilityCooldownFillClass = "ability-cooldown-fill";
        private const string AbilityCooldownLabelClass = "ability-cooldown-label";

        // The Q/W/E/R hotkey labels + placeholder glyphs for the four slots.
        // Glyphs are visual stand-ins until ability icon art lands (Week 4+).
        private static readonly string[] SlotKeys = { "Q", "W", "E", "R" };
        private static readonly string[] SlotGlyphs = { "✦", "❄", "✚", "☄" };

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private VisualElement _heartHpFill;
        private Label _heartHpLabel;
        private Label _crystalCount;
        private Label _waveNumber;
        private Label _waveCountdown;
        private VisualElement _waveCountdownTimer;
        private Label _waveCountdownIcon;
        private VisualElement _abilityBar;
        private VisualElement _manaFill;
        private Label _manaLabel;
        private Button _buildButton;

        // Repair-prompt elements (Workstream B — player wall-repair mechanic).
        private VisualElement _repairPrompt;
        private Label _repairPromptSubtitle;
        private Label _repairPromptCost;
        private Button _repairPromptConfirm;
        private Button _repairPromptCancel;
        private Label _repairToast;

        // Auto-hide bookkeeping for the repair-feedback toast.
        private float _repairToastHideAt = -1f;

        /// <summary>One built ability cell — the cooldown sweep + numeral handles.</summary>
        private struct AbilityCell
        {
            public VisualElement Slot;
            public VisualElement CooldownFill;
            public Label CooldownLabel;
        }

        private readonly AbilityCell[] _abilityCells = new AbilityCell[AbilitySlotCount];
        private bool _bound;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BindElements();
        }

        private void OnDisable()
        {
            if (_buildButton != null) _buildButton.clicked -= OnBuildClicked;
            if (_repairPromptConfirm != null) _repairPromptConfirm.clicked -= OnRepairConfirmClicked;
            if (_repairPromptCancel != null) _repairPromptCancel.clicked -= OnRepairCancelClicked;
            _bound = false;
        }

        private void Update()
        {
            // Auto-hide the repair-feedback toast once its dwell time elapses.
            if (_repairToastHideAt >= 0f && Time.unscaledTime >= _repairToastHideAt)
            {
                _repairToastHideAt = -1f;
                if (_repairToast != null)
                {
                    _repairToast.text = string.Empty;
                    _repairToast.style.display = DisplayStyle.None;
                }
            }
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning("[VillageHudController] No UIDocument root — HUD will not display.");
                return;
            }

            _heartHpFill = _root.Q<VisualElement>(HeartHpFillName);
            _heartHpLabel = _root.Q<Label>(HeartHpLabelName);
            _crystalCount = _root.Q<Label>(CrystalCountName);
            _waveNumber = _root.Q<Label>(WaveNumberName);
            _waveCountdown = _root.Q<Label>(WaveCountdownName);
            _waveCountdownTimer = _root.Q<VisualElement>(WaveCountdownTimerName);
            _waveCountdownIcon = _root.Q<Label>(WaveCountdownIconName);
            _abilityBar = _root.Q<VisualElement>(AbilityBarName);
            _manaFill = _root.Q<VisualElement>(ManaFillName);
            _manaLabel = _root.Q<Label>(ManaLabelName);
            _buildButton = _root.Q<Button>(BuildButtonName);

            // Repair-prompt elements (Workstream B).
            _repairPrompt = _root.Q<VisualElement>(RepairPromptName);
            _repairPromptSubtitle = _root.Q<Label>(RepairPromptSubtitleName);
            _repairPromptCost = _root.Q<Label>(RepairPromptCostName);
            _repairPromptConfirm = _root.Q<Button>(RepairPromptConfirmName);
            _repairPromptCancel = _root.Q<Button>(RepairPromptCancelName);
            _repairToast = _root.Q<Label>(RepairToastName);

            if (_buildButton != null)
            {
                _buildButton.clicked -= OnBuildClicked; // guard against a double OnEnable
                _buildButton.clicked += OnBuildClicked;
            }

            if (_repairPromptConfirm != null)
            {
                _repairPromptConfirm.clicked -= OnRepairConfirmClicked;
                _repairPromptConfirm.clicked += OnRepairConfirmClicked;
            }
            if (_repairPromptCancel != null)
            {
                _repairPromptCancel.clicked -= OnRepairCancelClicked;
                _repairPromptCancel.clicked += OnRepairCancelClicked;
            }

            // The repair prompt + toast start hidden until the repair flow runs.
            HideRepairPrompt();
            if (_repairToast != null)
            {
                _repairToast.text = string.Empty;
                _repairToast.style.display = DisplayStyle.None;
            }

            BuildAbilityCells();
            BuildTriggerWaveButton();
            _bound = true;
        }

        /// <summary>
        /// Owner 2026-05-20: "cannot manually start waves". The dedicated
        /// AdminOverlay shortcut still exists (Ctrl+Shift+A → Trigger next
        /// wave) but it's hidden behind a chord. Surface a visible "Start
        /// Wave" button on the HUD so the wave loop can be kicked from the
        /// normal play flow. Reflection-bridge into the WaveManager so the
        /// HUD asmdef doesn't need to reference DeNelle.Village.
        /// </summary>
        private void BuildTriggerWaveButton()
        {
            if (_root == null) return;
            // Avoid duplicate insertion on a second OnEnable.
            var existing = _root.Q<Button>("trigger-wave-button");
            if (existing != null) return;

            var btn = new Button { name = "trigger-wave-button", text = "▶ Start Wave" };
            btn.style.position = Position.Absolute;
            btn.style.top = 60;
            btn.style.right = 16;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.paddingTop = 5;
            btn.style.paddingBottom = 5;
            btn.style.fontSize = 12;
            btn.style.color = new StyleColor(new Color(1f, 0.94f, 0.78f, 1f));
            btn.style.backgroundColor = new StyleColor(new Color(0.30f, 0.08f, 0.10f, 0.92f));
            btn.style.borderTopLeftRadius = 8;
            btn.style.borderTopRightRadius = 8;
            btn.style.borderBottomLeftRadius = 8;
            btn.style.borderBottomRightRadius = 8;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderTopColor = new StyleColor(new Color(0.92f, 0.45f, 0.28f, 1f));
            btn.style.borderBottomColor = new StyleColor(new Color(0.92f, 0.45f, 0.28f, 1f));
            btn.clicked += OnTriggerWaveClicked;
            _root.Add(btn);
        }

        private static System.Type s_waveManagerType;
        private void OnTriggerWaveClicked()
        {
            try
            {
                if (s_waveManagerType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("DeNelle.Village.WaveManager", false);
                        if (t != null) { s_waveManagerType = t; break; }
                    }
                }
                if (s_waveManagerType == null) { Debug.LogWarning("[VillageHudController] WaveManager type not found."); return; }
                var inst = UnityEngine.Object.FindObjectOfType(s_waveManagerType) as Component;
                if (inst == null) { Debug.LogWarning("[VillageHudController] No WaveManager in scene."); return; }
                var m = s_waveManagerType.GetMethod("ForceBeginNextWave");
                m?.Invoke(inst, null);
                Debug.Log("[VillageHudController] Trigger Wave button → ForceBeginNextWave.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[VillageHudController] Trigger Wave failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Builds the four Q/W/E/R cells into the "ability-bar" container once.
        /// Each cell carries a hotkey badge, a placeholder glyph, a bottom-anchored
        /// cooldown sweep and a seconds-remaining numeral.
        /// </summary>
        private void BuildAbilityCells()
        {
            if (_abilityBar == null) return;
            _abilityBar.Clear();

            for (int i = 0; i < AbilitySlotCount; i++)
            {
                var slot = new VisualElement { name = $"ability-slot-{i}" };
                slot.AddToClassList(AbilitySlotClass);
                slot.AddToClassList(AbilitySlotReadyClass);

                var icon = new Label(SlotGlyphs[i]) { name = "ability-icon" };
                icon.AddToClassList(AbilityIconClass);
                slot.Add(icon);

                // Cooldown sweep — drawn over the icon, height driven 0..100%.
                var cooldownFill = new VisualElement { name = "ability-cooldown-fill" };
                cooldownFill.AddToClassList(AbilityCooldownFillClass);
                cooldownFill.pickingMode = PickingMode.Ignore;
                slot.Add(cooldownFill);

                var cooldownLabel = new Label(string.Empty) { name = "ability-cooldown-label" };
                cooldownLabel.AddToClassList(AbilityCooldownLabelClass);
                cooldownLabel.pickingMode = PickingMode.Ignore;
                slot.Add(cooldownLabel);

                // Hotkey badge last so it paints above the sweep.
                var key = new Label(SlotKeys[i]) { name = "ability-key" };
                key.AddToClassList(AbilityKeyClass);
                key.pickingMode = PickingMode.Ignore;
                slot.Add(key);

                // Tap-to-cast — slot raises AbilityRequested(index). The
                // bridge in DeNelle.Village forwards to HeroAbilities.TryCast.
                int slotIndex = i;
                slot.pickingMode = PickingMode.Position;
                slot.RegisterCallback<PointerDownEvent>(_ => AbilityRequested?.Invoke(slotIndex));

                _abilityBar.Add(slot);
                _abilityCells[i] = new AbilityCell
                {
                    Slot = slot,
                    CooldownFill = cooldownFill,
                    CooldownLabel = cooldownLabel,
                };
            }
        }

        // =====================================================================
        //  Public API — the integrator pushes HUD data through these setters.
        // =====================================================================

        /// <summary>
        /// Sets the Heart HP bar. <paramref name="current"/> and
        /// <paramref name="max"/> are in whatever units the caller tracks
        /// (e.g. the Heart's 0-100 HP) — the bar fills by their ratio and the
        /// label shows the rounded values. The bar tints amber then red as HP
        /// crosses the warning / critical fractions.
        /// </summary>
        public void SetHeartHp(float current, float max)
        {
            float safeMax = Mathf.Max(1f, max);
            float clamped = Mathf.Clamp(current, 0f, safeMax);
            float fraction = clamped / safeMax;

            SetBarWidth(_heartHpFill, fraction);

            if (_heartHpFill != null)
            {
                bool critical = fraction <= _heartCriticalFraction;
                bool warning = !critical && fraction <= _heartWarningFraction;
                _heartHpFill.EnableInClassList(HeartCriticalClass, critical);
                _heartHpFill.EnableInClassList(HeartWarningClass, warning);
            }

            if (_heartHpLabel != null)
                _heartHpLabel.text = $"{Mathf.RoundToInt(clamped)} / {Mathf.RoundToInt(safeMax)}";
        }

        /// <summary>Sets the crystal counter. Negative values are clamped to zero.</summary>
        public void SetCrystals(int amount)
        {
            if (_crystalCount != null)
                _crystalCount.text = Mathf.Max(0, amount).ToString();
        }

        /// <summary>
        /// Sets the wave indicator. <paramref name="number"/> is the 1-based wave
        /// ordinal; <paramref name="countdown"/> is the seconds remaining in the
        /// between-wave Prepare Phase — pass 0 (or less) while a wave is active to
        /// clear the countdown line.
        /// </summary>
        public void SetWave(int number, float countdown)
        {
            if (_waveNumber != null)
                _waveNumber.text = $"Wave {Mathf.Max(1, number)}";

            if (_waveCountdown == null) return;

            bool counting = countdown > 0f;
            bool urgent = counting && countdown <= _countdownUrgentSeconds;

            if (counting)
            {
                // Compact "M:SS" / "Ns" timer text — small enough for the
                // top-centre pill (owner ask: a small timer at the top).
                int seconds = Mathf.CeilToInt(countdown);
                _waveCountdown.text = FormatCountdown(seconds);
            }
            else
            {
                _waveCountdown.text = string.Empty;
            }

            // Toggle the whole top-centre pill — it only shows during the
            // between-wave Prepare Phase, then collapses while a wave is active.
            if (_waveCountdownTimer != null)
            {
                _waveCountdownTimer.style.display =
                    counting ? DisplayStyle.Flex : DisplayStyle.None;
                _waveCountdownTimer.EnableInClassList(CountdownTimerUrgentClass, urgent);
            }
            _waveCountdown.EnableInClassList(CountdownUrgentClass, urgent);
            if (_waveCountdownIcon != null)
                _waveCountdownIcon.EnableInClassList(CountdownIconUrgentClass, urgent);
        }

        /// <summary>
        /// Formats a between-wave countdown into a compact timer string —
        /// <c>M:SS</c> when a minute or more remains, plain <c>Ns</c> below a
        /// minute. Kept short so the top-centre pill stays small.
        /// </summary>
        private static string FormatCountdown(int totalSeconds)
        {
            totalSeconds = Mathf.Max(0, totalSeconds);
            if (totalSeconds >= 60)
            {
                int m = totalSeconds / 60;
                int s = totalSeconds % 60;
                return $"{m}:{s:00}";
            }
            return $"{totalSeconds}s";
        }

        /// <summary>
        /// Sets the cooldown state of one ability slot (0 = Q, 1 = W, 2 = E,
        /// 3 = R). <paramref name="remaining"/> is the seconds left on cooldown
        /// and <paramref name="total"/> the ability's full cooldown duration; the
        /// cell shows a sweep + a seconds numeral while cooling and clears to a
        /// ready cell once <paramref name="remaining"/> reaches 0.
        /// </summary>
        public void SetAbilityCooldown(int slot, float remaining, float total)
        {
            if (slot < 0 || slot >= AbilitySlotCount) return;
            AbilityCell cell = _abilityCells[slot];
            if (cell.Slot == null) return;

            bool cooling = remaining > 0f && total > 0f;

            // Sweep height: 100% just cast → 0% ready. A vertical wipe stands in
            // for a radial sweep until ability icon art lands.
            float fraction = cooling ? Mathf.Clamp01(remaining / total) : 0f;
            if (cell.CooldownFill != null)
                cell.CooldownFill.style.height = Length.Percent(fraction * 100f);

            if (cell.CooldownLabel != null)
                cell.CooldownLabel.text = cooling ? Mathf.CeilToInt(remaining).ToString() : string.Empty;

            cell.Slot.EnableInClassList(AbilitySlotCoolingClass, cooling);
            cell.Slot.EnableInClassList(AbilitySlotReadyClass, !cooling);
        }

        /// <summary>
        /// Sets the hero mana bar. The bar fills by the current/max ratio and the
        /// label shows the rounded values.
        /// </summary>
        public void SetMana(float current, float max)
        {
            float safeMax = Mathf.Max(1f, max);
            float clamped = Mathf.Clamp(current, 0f, safeMax);

            SetBarWidth(_manaFill, clamped / safeMax);

            if (_manaLabel != null)
                _manaLabel.text = $"{Mathf.RoundToInt(clamped)} / {Mathf.RoundToInt(safeMax)}";
        }

        /// <summary>
        /// Enables / disables the Build button — e.g. the integrator may grey it
        /// out while a wave is active. Purely cosmetic; the button still raises
        /// <see cref="BuildRequested"/> only while enabled.
        /// </summary>
        public void SetBuildButtonEnabled(bool enabled)
        {
            if (_buildButton != null) _buildButton.SetEnabled(enabled);
        }

        /// <summary>True once VillageHud.uxml has been bound and the cells built.</summary>
        public bool IsBound => _bound;

        // =====================================================================
        //  Repair prompt — Workstream B (player wall-repair mechanic).
        //  A passive display, exactly like the rest of this HUD: the village
        //  WallRepairController pushes the prompt data in through these setters
        //  and listens to RepairConfirmRequested / RepairCancelRequested. The
        //  cross-module wiring is done by WallRepairSceneSetup.cs (the HUD asmdef
        //  cannot reference DeNelle.Village).
        // =====================================================================

        /// <summary>
        /// Shows the repair-confirm prompt for a selected structure.
        /// <paramref name="subtitle"/> is the prompt's sub-line (already composed
        /// + localized by the caller — the HUD displays it verbatim),
        /// <paramref name="crystalCost"/> is the crystal price, and
        /// <paramref name="affordable"/> greys the Repair button + reddens the
        /// cost when the player cannot pay.
        /// </summary>
        public void ShowRepairPrompt(string subtitle, int crystalCost, bool affordable)
        {
            if (_repairPrompt == null) return;

            if (_repairPromptSubtitle != null)
                _repairPromptSubtitle.text = subtitle ?? string.Empty;

            if (_repairPromptCost != null)
            {
                _repairPromptCost.text = Mathf.Max(0, crystalCost).ToString();
                _repairPromptCost.EnableInClassList(RepairCostUnaffordableClass, !affordable);
            }

            if (_repairPromptConfirm != null)
                _repairPromptConfirm.SetEnabled(affordable);

            _repairPrompt.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the repair-confirm prompt.</summary>
        public void HideRepairPrompt()
        {
            if (_repairPrompt != null)
                _repairPrompt.style.display = DisplayStyle.None;
        }

        /// <summary>True while the repair-confirm prompt is on screen.</summary>
        public bool IsRepairPromptVisible =>
            _repairPrompt != null && _repairPrompt.style.display == DisplayStyle.Flex;

        /// <summary>
        /// Shows a brief repair-feedback toast (success or insufficient-funds).
        /// <paramref name="isError"/> flips the toast to the red error look. The
        /// toast auto-hides after <c>_repairToastSeconds</c>. An empty / null
        /// message hides the toast immediately.
        /// </summary>
        public void ShowRepairFeedback(string message, bool isError)
        {
            if (_repairToast == null) return;

            if (string.IsNullOrEmpty(message))
            {
                _repairToast.text = string.Empty;
                _repairToast.style.display = DisplayStyle.None;
                _repairToastHideAt = -1f;
                return;
            }

            _repairToast.text = message;
            _repairToast.EnableInClassList(RepairToastErrorClass, isError);
            _repairToast.style.display = DisplayStyle.Flex;
            _repairToastHideAt = Time.unscaledTime + _repairToastSeconds;
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        /// <summary>Forwards the Build button click to the BuildRequested event.</summary>
        private void OnBuildClicked()
        {
            BuildRequested?.Invoke();
        }

        /// <summary>Forwards the repair-prompt Repair button to RepairConfirmRequested.</summary>
        private void OnRepairConfirmClicked()
        {
            RepairConfirmRequested?.Invoke();
        }

        /// <summary>Forwards the repair-prompt Cancel button to RepairCancelRequested.</summary>
        private void OnRepairCancelClicked()
        {
            HideRepairPrompt();
            RepairCancelRequested?.Invoke();
        }

        /// <summary>Sets a bar fill's width as a percentage (0..1) of its track.</summary>
        private static void SetBarWidth(VisualElement fill, float fraction)
        {
            if (fill == null) return;
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        }
    }
}

// =============================================================================
// INTEGRATOR NOTES — wiring the village HUD into the village scene.
// -----------------------------------------------------------------------------
// This file (and the DeNelle.HUD asmdef) deliberately cannot see DeNelle.Village,
// so the scene builder / VillageController owns every connection below:
//
//   1. Add a UIDocument to the village HUD GameObject; assign VillageHud.uxml as
//      its Source Asset. Add this VillageHudController component beside it.
//
//   2. Build button → BuildMenu:
//        hud.BuildRequested.AddListener(buildMenu.Open);
//      (BuildMenu.Open() is parameterless — a direct UnityEvent listener.)
//
//   3. Push data each frame / on change from the village sub-systems:
//        - HeartController : hud.SetHeartHp(heart.Hp, 100f);
//        - crystal balance : hud.SetCrystals(GameStateService.Instance.State.Resources.Crystals);
//                            (or subscribe to GameStateService.ResourcesChanged)
//        - WaveManager     : hud.SetWave(wave.CurrentWaveId, wave.CountdownRemaining);
//                            WaveManager also exposes OnCountdownTick / OnWaveStarted
//                            UnityEvents — bind those to refresh on change.
//        - HeroAbilities   : for each slot Q/W/E/R, hud.SetAbilityCooldown(
//                                (int)slot, hero.CooldownRemaining(slot), <ability cooldown>);
//                            and hud.SetMana(hero.Mana, hero.MaxMana);
//
//   4. Wall-repair prompt (Workstream B):
//        - WallRepairController raises PromptShown / PromptHidden / FeedbackShown;
//          wire those to hud.ShowRepairPrompt / hud.HideRepairPrompt /
//          hud.ShowRepairFeedback.
//        - hud.RepairConfirmRequested -> WallRepairController.ConfirmRepair();
//          hud.RepairCancelRequested  -> WallRepairController.CancelRepair().
//        The cross-module wiring is done by Assets/Editor/WallRepairSceneSetup.cs
//        (the HUD asmdef cannot reference DeNelle.Village).
//
// The HUD holds one short timer for the repair-feedback toast auto-hide; every
// other readout is integrator-driven. See docs/port-notes/hud-module.md.
// =============================================================================
