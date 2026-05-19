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

        [Header("Events")]
        [Tooltip("Raised when the player taps the Build button. The integrator hooks this " +
                 "to the village BuildMenu.Open() — the HUD cannot reference BuildMenu itself.")]
        public UnityEvent BuildRequested = new UnityEvent();

        // ── UXML element names — the binding contract with VillageHud.uxml ───
        private const string RootName = "village-hud-root";
        private const string HeartHpFillName = "heart-hp-fill";
        private const string HeartHpLabelName = "heart-hp-label";
        private const string CrystalCountName = "crystal-count";
        private const string WaveNumberName = "wave-number";
        private const string WaveCountdownName = "wave-countdown";
        private const string AbilityBarName = "ability-bar";
        private const string ManaFillName = "mana-fill";
        private const string ManaLabelName = "mana-label";
        private const string BuildButtonName = "build-button";

        // ── USS class names — styled by VillageHud.uss ───────────────────────
        private const string HeartWarningClass = "bar-fill--heart-warning";
        private const string HeartCriticalClass = "bar-fill--heart-critical";
        private const string CountdownUrgentClass = "wave-countdown--urgent";
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
        private VisualElement _abilityBar;
        private VisualElement _manaFill;
        private Label _manaLabel;
        private Button _buildButton;

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
            _bound = false;
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
            _abilityBar = _root.Q<VisualElement>(AbilityBarName);
            _manaFill = _root.Q<VisualElement>(ManaFillName);
            _manaLabel = _root.Q<Label>(ManaLabelName);
            _buildButton = _root.Q<Button>(BuildButtonName);

            if (_buildButton != null)
            {
                _buildButton.clicked -= OnBuildClicked; // guard against a double OnEnable
                _buildButton.clicked += OnBuildClicked;
            }

            BuildAbilityCells();
            _bound = true;
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

            if (countdown > 0f)
            {
                int seconds = Mathf.CeilToInt(countdown);
                _waveCountdown.text = $"Next wave in {seconds}s";
                _waveCountdown.EnableInClassList(
                    CountdownUrgentClass, countdown <= _countdownUrgentSeconds);
            }
            else
            {
                _waveCountdown.text = string.Empty;
                _waveCountdown.EnableInClassList(CountdownUrgentClass, false);
            }
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
        //  Internals
        // =====================================================================

        /// <summary>Forwards the Build button click to the BuildRequested event.</summary>
        private void OnBuildClicked()
        {
            BuildRequested?.Invoke();
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
// The HUD holds no timers — the integrator drives it. See docs/port-notes/
// hud-module.md.
// =============================================================================
