// =============================================================================
// HeroProgression — the hero's XP / level, and the rewards each level grants.
// -----------------------------------------------------------------------------
// An IXpEarner on the hero: ProgressionManager feeds it shared kill-XP; it
// levels up when XP crosses a GROWING threshold (level*120 + 80 — owner's hero
// curve; the hero levels slower than a pet) and on each level grants BOTH:
//   • a stat bonus  — a live ability-damage multiplier HeroAbilities reads, and
//   • Wisdom points — the talent-tree currency (owner's decision: leveling feeds
//     the skill tree). The per-level grant scales by level band (2 / 3 / 4).
//
// Level-ups are INSTANT and non-disruptive (no pause): XP applies in one call,
// fires OnLevelUp/OnXPChanged (for VFX / a future XP bar / the talent glow), and
// ProgressionManager floats a "+Level" popup. Spending Wisdom stays a calm
// wave-end / manual-button action via the talent tree — never forced mid-combat.
//
// Registers in the Core XpEarnerRegistry under the id "hero" — the same id
// HeroAbilities attributes its damage to — so the cross-asmdef XP grant resolves
// back here. Lives in namespace DeNelle.Village to match its hero siblings
// (HeroAbilities et al.). Attached at runtime by ProgressionManager (no scene
// wiring). In-memory for a run; cross-run save is a later pass.
// =============================================================================

using System;
using DeNelle.Core.Progression;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Talents;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Tracks the hero's XP/level and applies level-up rewards (stats + Wisdom).</summary>
    [DisallowMultipleComponent]
    public sealed class HeroProgression : MonoBehaviour, IXpEarner
    {
        /// <summary>Attribution / registry id for the hero.</summary>
        public const string Id = "hero";

        /// <summary>
        /// The active hero's progression. Set in OnEnable / cleared in OnDisable so
        /// the DEF-77 LevelUpSkillPopup can subscribe to <see cref="OnLevelUp"/>
        /// without FindAnyObjectByType (DEF-77 CP1 Issue 6). Single hero per run.
        /// </summary>
        public static HeroProgression Instance { get; private set; }

        // ── Tuning ────────────────────────────────────────────────────────────
        private const float DamagePerLevel = 0.06f;      // +6% ability damage per level
        private const float MaxDamageMultiplier = 3f;     // cap the damage scaling

        [SerializeField] private int _level = 1;
        [SerializeField] private float _xp;               // XP banked toward the next level
        [SerializeField] private float _lifetimeXp;       // total XP ever earned (telemetry/UI)

        private bool _hasGrantedStarterPoints;

        /// <summary>Fired on each level gained — arg = new level (for VFX / sound).</summary>
        public event Action<int> OnLevelUp;
        /// <summary>Fired whenever XP changes — args = (current XP, XP needed for next level).</summary>
        public event Action<float, float> OnXpChanged;

        /// <summary>
        /// STATIC level-up relay (arg = new level). Fired in lock-step with the
        /// instance <see cref="OnLevelUp"/> event. Subscribers that outlive a single
        /// HeroProgression instance MUST use this: ProgressionManager destroys the
        /// BeforeSceneLoad bootstrap instance and migrates XP onto the hero's own
        /// HeroProgression (see Awake). A listener that subscribed to the instance
        /// event of the standalone bootstrap is left dangling on a destroyed object
        /// and never hears the hero's real level-ups (DEF-261 root cause). This
        /// static relay is immune to that instance swap.
        /// </summary>
        public static event Action<int> OnAnyLevelUp;

        public string EarnerId => Id;
        public int Level => _level;
        public float Xp => _xp;
        public float XpToNext => XpToNextFor(_level);
        public float LifetimeXp => _lifetimeXp;
        public Vector3 WorldPosition => transform.position + Vector3.up * 2.2f;

        /// <summary>
        /// Ability-damage multiplier from levels — HeroAbilities multiplies this
        /// into outgoing damage on top of the talent <c>DamageMultiplier</c>.
        /// </summary>
        public float DamageMultiplier =>
            Mathf.Min(MaxDamageMultiplier, 1f + (_level - 1) * DamagePerLevel);

        /// <summary>Growing XP cost to reach the level AFTER <paramref name="level"/> (owner's hero curve).</summary>
        // DEF playtest 2026-05-28: the old curve (level*120+80 → 200 XP for L1→L2) was
        // far too shallow against kill-XP rewards (~1800 XP/wave), so a wave jumped the
        // hero ~5 levels and spammed the popup. Owner direction: the FIRST level-up
        // should be cheap (a quick early reward), then ramp steeply so later waves grant
        // ~1 level, not 5. Front-loaded quadratic:
        //   L1→L2: 150   L2→L3: 1000   L3→L4: 2850   L4→L5: 5700   (first-pass — tune)
        private static float XpToNextFor(int level) => 150f + (level - 1) * 350f + (level - 1) * (level - 1) * 500f;

        /// <summary>
        /// Wisdom granted for reaching <paramref name="level"/> — the v2 "specialize" curve
        /// (owner 2026-06-27). A maxed hero (~L20) earns ~50 Wisdom from levels: +2/level
        /// through L8, then +3/level. That is ~70% of the 71 needed for a WHOLE hero tree
        /// (hero 55 + 8 shared @2 = 16), so the player MUST pick a focus rather than buy
        /// everything. (Prior curve 2/3/4 paid ~63 by L20 ≈ 89% — too generous.)
        /// NOTE: levels are NOT the only Wisdom source — see file-level report; waves /
        /// arena wins / daily quests / tier milestones also grant Wisdom.
        /// </summary>
        private static int WisdomForLevel(int level)
        {
            return level <= 8 ? 2 : 3;
        }

        private void OnEnable()
        {
            Instance = this;
            XpEarnerRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            XpEarnerRegistry.Unregister(this);
        }

        private void Awake()
        {
            // ProgressionManager attaches a HeroProgression to the HERO while the
            // BeforeSceneLoad Bootstrap's throwaway standalone "HeroProgression" GO
            // already holds Instance. The OLD code ran Destroy(gameObject) -> deleted
            // the hero in frame 1 (the frozen-village bug). Now: if the existing
            // Instance is that standalone, the hero's copy TAKES OVER (migrate XP,
            // destroy only the standalone) so level/XP + the level-up popup track the
            // hero instead of world origin. Any other duplicate removes only this
            // component (never the host GameObject).
            if (Instance != null && Instance != this)
            {
                if (Instance.gameObject != gameObject && Instance.gameObject.name == "HeroProgression")
                {
                    FlowTrace.Step("HeroXp", $"Awake: hero takes over standalone bootstrap — migrating level={Instance._level} xp={Instance._xp:0.#}");
                    _level = Instance._level;
                    _xp = Instance._xp;
                    _lifetimeXp = Instance._lifetimeXp;
                    _hasGrantedStarterPoints = Instance._hasGrantedStarterPoints;
                    Destroy(Instance.gameObject);   // the standalone, NOT the hero
                }
                else { FlowTrace.Warn("HeroXp", $"Awake: duplicate HeroProgression on '{gameObject.name}' — removing this component."); Destroy(this); return; }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("HeroProgression");
            DontDestroyOnLoad(go);
            go.AddComponent<HeroProgression>();
        }

        public int AddXp(float amount)
        {
            if (amount <= 0f) return 0;
            FlowTrace.Step("HeroXp", $"AddXp amount={amount:0.#} (level={_level} xp={_xp:0.#}/{XpToNextFor(_level):0.#})");
            _xp += amount;
            _lifetimeXp += amount;

            int gained = 0;
            while (_xp >= XpToNextFor(_level))
            {
                _xp -= XpToNextFor(_level);
                _level++;
                gained++;
                ApplyLevelRewards(_level);
            }

            if (gained > 0)
                FlowTrace.Step("HeroXp", $"leveled +{gained} -> level={_level} (xp={_xp:0.#}/{XpToNextFor(_level):0.#})");
            OnXpChanged?.Invoke(_xp, XpToNextFor(_level));
            return gained;
        }

        /// <summary>Applies one level's rewards (Wisdom; the damage bonus is read live).</summary>
        private void ApplyLevelRewards(int newLevel)
        {
            FlowTrace.Step("HeroXp", $"ApplyLevelRewards level={newLevel} wisdomGrant={WisdomForLevel(newLevel)} firstLevel={( !_hasGrantedStarterPoints )}");

            // Owner's decision: a hero level grants Wisdom talent points so leveling
            // feeds the talent tree (the DamageMultiplier is read live, no push needed).
            // §12: the previously-SILENT catch is now surfaced — a throw here means the
            // level's Wisdom is LOST (debit-of-progression without grant), so it is a Fail.
            try { WisdomCurrencyService.Instance?.Grant(WisdomForLevel(newLevel)); }
            catch (System.Exception e) { FlowTrace.Fail("HeroXp", $"Wisdom grant THREW at level {newLevel} — {WisdomForLevel(newLevel)} Wisdom NOT granted: {e.GetType().Name}: {e.Message}"); }

            // DEF-77 — each hero level also banks a spendable craft-skill point; the
            // LevelUpSkillPopup reacts via SkillSystem.OnSkillsChanged + OnLevelUp.
            try { SkillSystem.Instance?.GrantSkillPoint(); }
            catch (System.Exception e) { FlowTrace.Fail("HeroXp", $"SkillPoint grant THREW at level {newLevel}: {e.GetType().Name}: {e.Message}"); }

            // DEF-82 — on the very first level-up, gift two bonus skill points so
            // new players can immediately engage the skill tree.
            if (!_hasGrantedStarterPoints)
            {
                _hasGrantedStarterPoints = true;
                SkillSystem.Instance?.GrantSkillPoint();
                SkillSystem.Instance?.GrantSkillPoint();
                FlowTrace.Step("HeroXp", "granted 2 starter skill points (first level-up).");
            }

            try { OnLevelUp?.Invoke(newLevel); }
            catch (System.Exception e) { FlowTrace.Fail("HeroXp", $"OnLevelUp subscriber THREW at level {newLevel}: {e.GetType().Name}: {e.Message}"); }
            // DEF-261 — also fire the instance-swap-proof static relay so listeners
            // that outlive a HeroProgression instance (LevelUpSkillPopup) still hear it.
            try { OnAnyLevelUp?.Invoke(newLevel); }
            catch (System.Exception e) { FlowTrace.Fail("HeroXp", $"OnAnyLevelUp subscriber THREW at level {newLevel}: {e.GetType().Name}: {e.Message}"); }
        }
    }
}
