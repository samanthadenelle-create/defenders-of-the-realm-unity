// =============================================================================
// BossHealthBar (DEF-60) — full-screen boss HP bar with phase indicators.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// WHAT IT DOES:
//   Shows a wide HP bar at the top of the screen when a boss is alive.
//   The bar reflects the boss's live HP fraction via IDamageable polling
//   (bosses don't broadcast an event) and transitions into a danger tint as
//   HP drops through the Phase 2 / Phase 3 thresholds.
//
//   Two modes:
//     Dragon mode — receives a DragonBoss reference, polls HpFraction + Phase.
//     Generic mode — receives any IDamageable; shows name + HP fraction only.
//
//   Phase pip indicators (Dragon only) mark the phase-change thresholds on the
//   bar so the player knows when the boss transitions.
//
// ARCHITECTURE:
//   * Code-UI-Toolkit (no UXML) — same pattern as LevelUpSkillPopup.
//   * [RequireComponent(typeof(UIDocument))] — attach to a UIDocument GO on the
//     Village HUD canvas.
//   * WaveManager calls Show(dragonBoss) when a boss wave starts and Hide() on
//     wave cleared. WaveManager already hooks OnWaveStarted.
//   * Polls HP each Update — bosses don't expose a Changed event.
//   * DragonBoss.HpFraction + Phase read from the public IDamageable interface
//     and an optional Cast check for the Dragon-specific Phase property.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.Combat;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// Displays a boss HP bar at the top of the screen when a boss is alive.
    /// Attach to a UIDocument GameObject. Wire via <see cref="Show"/> /
    /// <see cref="Hide"/> from <see cref="WaveManager"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BossHealthBar : MonoBehaviour
    {
        [Header("Colours")]
        [SerializeField] private Color _colFull    = new Color(0.82f, 0.12f, 0.12f); // deep red
        [SerializeField] private Color _colMid     = new Color(0.90f, 0.45f, 0.05f); // orange
        [SerializeField] private Color _colCrit    = new Color(1.00f, 0.80f, 0.00f); // gold warning
        [SerializeField] private Color _colEmpty   = new Color(0.22f, 0.22f, 0.22f); // empty track
        [SerializeField] private Color _colBg      = new Color(0.06f, 0.06f, 0.08f, 0.92f);
        [SerializeField] private Color _colText    = Color.white;

        // HP fraction thresholds at which bar colour shifts.
        private const float MidThreshold  = 0.60f;
        private const float CritThreshold = 0.25f;

        // ── Cached VisualElements ──────────────────────────────────────────────
        private VisualElement _root;
        private VisualElement _barFill;
        private VisualElement _barTrack;
        private Label         _nameLabel;
        private Label         _hpLabel;
        private VisualElement _phaseMarkers;
        private bool          _uiBuilt;

        // ── Target ────────────────────────────────────────────────────────────
        private IDamageable _target;
        private DragonBoss  _dragon;    // non-null when target is a DragonBoss
        private string      _bossName = "Boss";
        private DragonPhase _lastPhase = DragonPhase.Circling;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildUiIfNeeded();
            Hide();
        }

        private void Update()
        {
            if (_target == null || !_target.IsAlive) { Hide(); return; }

            float frac = _target.Hp / Mathf.Max(1f, GetMaxHp());
            frac = Mathf.Clamp01(frac);

            // Fill width
            _barFill.style.width = new StyleLength(new Length(frac * 100f, LengthUnit.Percent));

            // Colour transition based on HP fraction
            Color fillCol = frac > MidThreshold ? _colFull
                : frac > CritThreshold          ? Color.Lerp(_colMid, _colFull, (frac - CritThreshold) / (MidThreshold - CritThreshold))
                :                                  Color.Lerp(_colCrit, _colMid, frac / CritThreshold);
            _barFill.style.backgroundColor = new StyleColor(fillCol);

            // HP text
            if (_hpLabel != null)
                _hpLabel.text = $"{Mathf.CeilToInt(_target.Hp)} / {Mathf.CeilToInt(GetMaxHp())}";

            // Dragon-specific phase label
            if (_dragon != null)
            {
                var curPhase = _dragon.Phase;
                if (curPhase != _lastPhase)
                {
                    _lastPhase = curPhase;
                    if (_nameLabel != null)
                        _nameLabel.text = $"{_bossName}  {PhaseLabel(curPhase)}";
                }
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the bar for a DragonBoss. Called by <see cref="WaveManager"/>
        /// when a dragon-apex wave starts.
        /// </summary>
        public void Show(DragonBoss dragon, string displayName = "Syndrath the Devourer")
        {
            BuildUiIfNeeded();
            _dragon   = dragon;
            _target   = dragon;
            _bossName = displayName;
            if (_nameLabel != null) _nameLabel.text = $"{displayName}  {PhaseLabel(DragonPhase.Circling)}";
            _lastPhase = DragonPhase.Circling;
            BuildPhaseMarkers();
            SetVisible(true);
        }

        /// <summary>
        /// Shows the bar for any <see cref="IDamageable"/> boss (necromancer, etc.).
        /// </summary>
        public void Show(IDamageable target, string displayName = "Boss")
        {
            BuildUiIfNeeded();
            _dragon   = null;
            _target   = target;
            _bossName = displayName;
            if (_nameLabel != null) _nameLabel.text = displayName;
            ClearPhaseMarkers();
            SetVisible(true);
        }

        /// <summary>Hides the boss health bar.</summary>
        public void Hide()
        {
            SetVisible(false);
            _target = null;
            _dragon = null;
        }

        // ── UI build ───────────────────────────────────────────────────────────

        private void BuildUiIfNeeded()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;

            var doc = GetComponent<UIDocument>();
            if (doc == null) return;

            // Full-width wrapper docked to the top
            _root = new VisualElement
            {
                name = "BossHealthBarRoot",
                style =
                {
                    position       = Position.Absolute,
                    top            = 16, left = 0, right = 0,
                    alignItems     = Align.Center,
                }
            };

            // Card
            var card = new VisualElement
            {
                style =
                {
                    width              = new StyleLength(new Length(60f, LengthUnit.Percent)),
                    backgroundColor    = new StyleColor(_colBg),
                    borderTopLeftRadius     = 8, borderTopRightRadius    = 8,
                    borderBottomLeftRadius  = 8, borderBottomRightRadius = 8,
                    paddingTop    = 8, paddingBottom = 8,
                    paddingLeft   = 14, paddingRight  = 14,
                }
            };

            // Name + HP row
            var infoRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            _nameLabel = new Label("Boss")
            {
                style = { color = new StyleColor(_colText), fontSize = 14,
                           unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 }
            };
            _hpLabel = new Label("? / ?")
            {
                style = { color = new StyleColor(_colText), fontSize = 12,
                           unityTextAlign = TextAnchor.MiddleRight }
            };
            infoRow.Add(_nameLabel);
            infoRow.Add(_hpLabel);

            // Track + fill
            _barTrack = new VisualElement
            {
                style =
                {
                    height = 14,
                    backgroundColor = new StyleColor(_colEmpty),
                    borderTopLeftRadius     = 4, borderTopRightRadius    = 4,
                    borderBottomLeftRadius  = 4, borderBottomRightRadius = 4,
                    overflow = Overflow.Hidden,
                }
            };
            _barFill = new VisualElement
            {
                style =
                {
                    height = 14,
                    width  = new StyleLength(new Length(100f, LengthUnit.Percent)),
                    backgroundColor = new StyleColor(_colFull),
                    borderTopLeftRadius     = 4, borderTopRightRadius    = 4,
                    borderBottomLeftRadius  = 4, borderBottomRightRadius = 4,
                    transitionDuration      = new StyleList<TimeValue>(
                        new System.Collections.Generic.List<TimeValue>
                        { new TimeValue(0.15f, TimeUnit.Second) }),
                    transitionProperty = new StyleList<StylePropertyName>(
                        new System.Collections.Generic.List<StylePropertyName>
                        { new StylePropertyName("width") }),
                }
            };
            _barTrack.Add(_barFill);

            // Phase markers container (sits on top of the track)
            _phaseMarkers = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    top = 0, left = 0, right = 0, bottom = 0,
                    flexDirection = FlexDirection.Row,
                }
            };
            _barTrack.Add(_phaseMarkers);

            card.Add(infoRow);
            card.Add(_barTrack);
            _root.Add(card);
            doc.rootVisualElement.Add(_root);
        }

        // Place thin divider lines at the Phase2/Phase3 thresholds so the player
        // can see when phases will shift.
        private void BuildPhaseMarkers()
        {
            if (_phaseMarkers == null) return;
            _phaseMarkers.Clear();

            // Phase 2 marker at 60%
            AddMarker(MidThreshold,  new Color(1f, 0.6f, 0.1f, 0.85f));
            // Phase 3 / enrage marker at 25%
            AddMarker(CritThreshold, new Color(1f, 0.9f, 0.1f, 0.95f));
        }

        private void AddMarker(float fraction, Color color)
        {
            var spacer = new VisualElement
            {
                style = { width = new StyleLength(new Length(fraction * 100f, LengthUnit.Percent)) }
            };
            var line = new VisualElement
            {
                style =
                {
                    width = 2f,
                    height = new StyleLength(new Length(100f, LengthUnit.Percent)),
                    backgroundColor = new StyleColor(color),
                    position = Position.Relative,
                }
            };
            spacer.Add(line);
            _phaseMarkers.Add(spacer);
        }

        private void ClearPhaseMarkers()
        {
            _phaseMarkers?.Clear();
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private float GetMaxHp()
        {
            // DragonBoss exposes MaxHp through IDamageable (Hp field only).
            // Use reflection-free cast since we're in the same assembly.
            if (_dragon != null) return _dragon.MaxHp;
            return _target?.Hp ?? 1f;          // fallback — shows 100% until HP drops
        }

        private static string PhaseLabel(DragonPhase phase) => phase switch
        {
            DragonPhase.Circling => "⬧ Phase I",
            DragonPhase.Stooping => "⬧ Phase II",
            DragonPhase.LastWing => "⚡ ENRAGED",
            DragonPhase.Falling  => "✦ Falling",
            _                    => string.Empty,
        };
    }
}
