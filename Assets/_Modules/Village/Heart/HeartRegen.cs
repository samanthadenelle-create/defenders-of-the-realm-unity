// =============================================================================
// HeartRegen -- passive out-of-combat health regeneration for the Heart of Elarion.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// GAMEPLAY (logic layer, not presentation): the Heart slowly restores HP while
// the village is NOT under attack, so a run that survives a wave heals back up
// during the calm build/prepare window instead of the damage carrying forever.
// This is the "restore the tree's health over time" half of the owner request.
//
// REGEN RULE (creative + design decision):
//   * Heal _regenPerSecond HP/sec, applied on a coarse _tickInterval accumulator
//     (not every frame) so the HeartController.OnHealthChanged event -- which the
//     HUD bar + the Heartwood ambient bed listen to -- is not spammed 60x/sec.
//   * Regen is PAUSED during combat. "Combat" = the WaveManager is in phase
//     Active or Breached, OR any live enemy is within _combatPauseRadius of the
//     Heart. So the tree only knits itself back together once the field is clear.
//   * Never heals a destroyed Heart (HP 0 is terminal -- the lose condition) and
//     never exceeds full (100). HeartController.Heal already clamps 0..100.
//   * With no WaveManager in the scene (e.g. a peaceful hub with no wave loop)
//     the field is treated as peaceful and the Heart regenerates freely.
//
// WIRES TO: HeartController.Heal(float) -- the canonical HP API (it delegates to
// SetHp, which clamps + fires OnHealthChanged + auto-derives the crystal state).
//
// ATTACH: self-bootstraps onto the HeartController GameObject at runtime (the
// canonical reactive-bridge pattern used by HeartwoodAmbientController) so no
// curated .unity scene is hand-edited. [RequireComponent(HeartController)] is
// honoured because we only AddComponent onto a GO that already has one.
//
// INSTRUMENTATION (CLAUDE.md section 12): FlowTrace.Step on every state
// transition (peaceful <-> combat, reached-full, dead) and FlowTrace.Throttle
// (~1/sec) on the hot per-tick heal so a headless run shows the regen flow
// without flooding the break-log.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Slowly restores <see cref="HeartController"/> HP while the village is out of
    /// combat (no active/breached wave and no enemy nearby). Pure gameplay logic --
    /// it only calls <see cref="HeartController.Heal"/>; it never touches presentation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeartController))]
    public sealed class HeartRegen : MonoBehaviour
    {
        // The regen flow's current mode -- Step-traced only on a change so the
        // break-log carries transitions, not a per-frame stream.
        private enum RegenMode { Unknown, Regenerating, PausedCombat, Full, Dead }

        [Header("Heart (auto-wired to the HeartController on this GameObject)")]
        [SerializeField] private HeartController _heart;

        [Header("Regen tuning")]
        [Tooltip("HP restored per second while out of combat. Heart HP is 0-100, so " +
                 "2/sec fully heals a badly-hurt Heart in under a minute of calm.")]
        [SerializeField, Min(0f)] private float _regenPerSecond = 2f;

        [Tooltip("Seconds between heal applications. The per-second rate is applied in " +
                 "these coarse steps so the OnHealthChanged event (HUD bar + Heartwood " +
                 "ambient) is not fired every frame.")]
        [SerializeField, Min(0.05f)] private float _tickInterval = 0.5f;

        [Tooltip("Regen pauses if any live enemy is within this many world units of the " +
                 "Heart, even between waves -- the tree cannot mend with a foe at its roots.")]
        [SerializeField, Min(0f)] private float _combatPauseRadius = 18f;

        private float _accum;
        private RegenMode _mode = RegenMode.Unknown;

        // Heart HP is authored on a fixed 0-100 scale (HeartController._hp Range(0,100)).
        private const float FullHp = 100f;

        // -- Self-bootstrap (attach onto the Heart at runtime; no scene edit) -----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedStatic;
            AttachToHearts();
        }

        private static void OnSceneLoadedStatic(
            UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
            => AttachToHearts();

        private static void AttachToHearts()
        {
            var hearts = Object.FindObjectsByType<HeartController>();
            foreach (var heart in hearts)
            {
                if (heart == null) continue;
                if (heart.GetComponent<HeartRegen>() == null)
                    heart.gameObject.AddComponent<HeartRegen>();
            }
        }

        private void Reset() => _heart = GetComponent<HeartController>();

        private void Awake()
        {
            if (_heart == null) _heart = GetComponent<HeartController>();
        }

        // -- Regen tick ----------------------------------------------------------

        private void Update()
        {
            if (_heart == null) return;

            float hp = _heart.Hp;

            // Terminal: a fallen Heart (HP 0) is the lose condition -- never revive it.
            if (hp <= 0f)
            {
                SetMode(RegenMode.Dead, hp);
                _accum = 0f;
                return;
            }

            // Already full -- nothing to do; hold the accumulator empty.
            if (hp >= FullHp)
            {
                SetMode(RegenMode.Full, hp);
                _accum = 0f;
                return;
            }

            // Combat gate -- the tree only mends once the field is clear.
            if (IsCombatActive())
            {
                SetMode(RegenMode.PausedCombat, hp);
                _accum = 0f;   // no post-combat heal spike -- start fresh when calm returns
                return;
            }

            SetMode(RegenMode.Regenerating, hp);

            _accum += Time.deltaTime;
            if (_accum < _tickInterval) return;

            float healed = _regenPerSecond * _accum;
            _accum = 0f;
            _heart.Heal(healed);   // clamps 0..100 + fires OnHealthChanged (HUD + ambient)

            FlowTrace.Throttle("Heart", "regen-tick", 1f,
                $"HeartRegen: +{healed:F2} HP (rate {_regenPerSecond:F1}/s) -> HP now {_heart.Hp:F1}/100 (out of combat).");
        }

        // -- Combat detection ----------------------------------------------------

        /// <summary>
        /// True when the village is under attack: the WaveManager is in an Active or
        /// Breached phase, OR any live enemy stands within <see cref="_combatPauseRadius"/>
        /// of the Heart. No WaveManager in the scene = peaceful (regen flows freely).
        /// </summary>
        private bool IsCombatActive()
        {
            var wave = WaveManager.Instance;
            if (wave == null) return false;

            WavePhase phase = wave.Phase;
            if (phase == WavePhase.Active || phase == WavePhase.Breached) return true;

            // Even between waves, a lingering enemy at the roots blocks regen.
            var enemies = wave.LiveEnemies;
            if (enemies != null && _combatPauseRadius > 0f)
            {
                float sqrR = _combatPauseRadius * _combatPauseRadius;
                Vector3 heartPos = transform.position;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e == null) continue;
                    if ((e.transform.position - heartPos).sqrMagnitude <= sqrR) return true;
                }
            }
            return false;
        }

        // -- Mode transitions (Step-traced once per change) ----------------------

        private void SetMode(RegenMode next, float hp)
        {
            if (next == _mode) return;
            _mode = next;
            FlowTrace.Step("Heart",
                $"HeartRegen mode -> {next} at HP {hp:F1}/100 " +
                (next == RegenMode.Regenerating ? "(field clear -- knitting back up)" :
                 next == RegenMode.PausedCombat  ? "(under attack -- regen held)" :
                 next == RegenMode.Full          ? "(fully restored)" :
                 next == RegenMode.Dead          ? "(Heart fell -- terminal)" : ""));
        }
    }
}
