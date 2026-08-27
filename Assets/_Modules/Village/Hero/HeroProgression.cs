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
// wiring).
//
// PERSISTED via GameState (HeroLevel / HeroXp / HeroLifetimeXp, schema v29 —
// F8-47): the component is still the live per-run authority, but it RESTORES
// from GameState on attach (never downgrading a live higher level) and writes
// back on every XP change — so a Single scene load (e.g. porting home from the
// challenge outpost) attaching a fresh component no longer resets the hero to
// level 1. Village → Core write is the sanctioned pattern (CLAUDE.md §5).
// =============================================================================

using System;
using DeNelle.Core.Progression;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Village.Talents;
using DeNelle.Village.Monetization;
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

        /// <summary>WO-977/981: AvailablePoints at the FIRST starter-grant attempt. -1 = not yet
        /// attempted. Held across a failed attempt so a retry grants only the remainder — see the
        /// partial-throw case in ApplyLevelRewards.</summary>
        private int _starterBaseline = -1;

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

        /// <summary>F8-47 — true once this component has adopted persisted level/XP from GameState.</summary>
        public bool HasRestoredFromSave { get; private set; }

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
        /// NOTE (WO-763, owner 2026-07-25): Wisdom is now a LEVEL-UP reward. The old
        /// per-wave (+2/wave), arena-win, and daily-quest Wisdom grants were REMOVED /
        /// redirected so skills feel EARNED, not sprayed by combat. Direct Wisdom sources
        /// today = THIS level-up grant + level-gated tier milestones (TierSystem). Combat
        /// (kills / waves / arena wins) still earns Wisdom INDIRECTLY via XP -> level-up.
        /// </summary>
        private static int WisdomForLevel(int level)
        {
            return level <= 8 ? 2 : 3;
        }

        private void OnEnable()
        {
            Instance = this;
            XpEarnerRegistry.Register(this);
            // WO-1220 — a New Game must reset the LIVE component, not just the save. This
            // component is the per-run authority and it writes itself BACK over GameState
            // (WriteBackToState) on the next XP grant, so a survivor from the previous run
            // re-stamps its level onto a freshly-reset save. Village -> Core subscription is
            // the sanctioned direction (CLAUDE.md §5); unsubscribed in OnDisable because the
            // event is static and would otherwise pin every carrier that ever existed.
            GameStateService.NewGameStarted += ResetForNewGame;
            // F8-47 — adopt the persisted level/XP (never downgrades a live higher
            // level). Runs after Awake's bootstrap-migration, so a fresh attach on a
            // scene load lands on the saved level instead of a default level 1.
            RestoreFromSave();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            XpEarnerRegistry.Unregister(this);
            GameStateService.NewGameStarted -= ResetForNewGame;   // WO-1220 — static event: never leak a carrier.
        }

        /// <summary>
        /// WO-1220 — drops this LIVE component back to the state of a component that was
        /// just added, because a New Game was started while it was alive.
        ///
        /// WHY THE SAVE ZERO IS NOT ENOUGH: <c>GameStateService.ResetToNewGame</c> sets GameState's
        /// heroLevel/heroXp/heroLifetimeXp to 1/0/0, but THIS component is the run's
        /// authority and pushes its own values back out through
        /// <see cref="WriteBackToState"/> on the very next XP grant. A carrier that survived
        /// the New Game (a hero carried DontDestroyOnLoad across a Single load, or a reset
        /// taken in-place from the dev overlay) therefore re-introduces the old level into the
        /// freshly-reset save, and the next fresh attach restores it as if it had been earned.
        ///
        /// ⛔ THIS IS NOT A BLIND ZERO, and specifically NOT a change to the WO-981 starter
        /// latch. The latch is not persisted — it is INFERRED at
        /// <see cref="RestoreFromSave"/> from <c>_level &gt; 1</c>, and that inference is
        /// untouched here. What this method restores is exactly the field state of a
        /// newly-constructed HeroProgression (level 1, no XP, latch OPEN, baseline unset), so
        /// a genuinely new hero receives its DEF-82 starter gift on its first level-up just as
        /// it would on a cold launch. Leaving the latch CLOSED would have silently deleted
        /// that gift from every new game — the WO-981 §A loss, made permanent.
        ///
        /// Deliberately does NOT call <see cref="WriteBackToState"/>: the reset already wrote
        /// 1/0/0 into GameState and is about to persist it, and writing back mid-reset would
        /// re-enter the state the reset is still assembling.
        /// </summary>
        public void ResetForNewGame()
        {
            FlowTrace.Step("HeroXp",
                $"ResetForNewGame: LIVE carrier on '{gameObject.name}' dropped from level={_level} " +
                $"xp={_xp:0.#} lifetime={_lifetimeXp:0.#} (starterLatch={_hasGrantedStarterPoints}) " +
                "to a fresh level-1 hero — a New Game was started while this component was alive.");
            _level = 1;
            _xp = 0f;
            _lifetimeXp = 0f;
            // WO-981: the latch re-OPENS so the new hero still earns its DEF-82 starter points
            // on its first level-up. The RestoreFromSave:level>1 inference is not touched.
            _hasGrantedStarterPoints = false;
            _starterBaseline = -1;
            HasRestoredFromSave = false;
            OnXpChanged?.Invoke(_xp, XpToNextFor(_level));
        }

        private void OnDestroy()
        {
            // F8-47 — a destroyed carrier is where a level reset would originate;
            // name it so any future loss pinpoints itself in the break-log.
            FlowTrace.Step("HeroXp", $"carrier destroyed scene '{gameObject.scene.name}' level={_level} xp={_xp:0.#}");
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
                    HasRestoredFromSave = Instance.HasRestoredFromSave;   // F8-47 — carry the restore mark across the takeover
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

        /// <summary>
        /// F8-47 — adopts the persisted level/XP from GameState. Returns true when the
        /// saved values were applied. NEVER downgrades: a live component already at a
        /// higher level (or equal level with more banked XP) keeps its values — a stale
        /// default-1 save read must not undo a run in progress. Null-safe when the save
        /// service isn't up yet (the BeforeSceneLoad bootstrap); the ProgressionManager
        /// attach re-runs it once the scene is ready. Idempotent.
        /// </summary>
        public bool RestoreFromSave()
        {
            var s = GameStateService.Instance?.State;
            if (s == null) return false;
            bool savedAhead = s.HeroLevel > _level || (s.HeroLevel == _level && s.HeroXp > _xp);
            if (!savedAhead)
            {
                // WO-1220 §12 — the DECLINED branch used to return in silence, which is the
                // half of the ordering the owner's capture could not show: a trace that only
                // fires when a restore SUCCEEDS cannot distinguish "the save was level 1" from
                // "this call never looked". Both outcomes are now on the record.
                FlowTrace.Step("HeroXp",
                    $"RestoreFromSave DECLINED — live level={_level} xp={_xp:0.#} is at or ahead of " +
                    $"the save (level={s.HeroLevel} xp={s.HeroXp:0.#}); the live values stand.");
                return false;
            }

            FlowTrace.Step("HeroXp", $"RestoreFromSave: level {_level}->{s.HeroLevel} xp {_xp:0.#}->{s.HeroXp:0.#} (lifetime={s.HeroLifetimeXp:0.#})");
            _level = Mathf.Max(1, s.HeroLevel);
            _xp = Mathf.Max(0f, s.HeroXp);
            _lifetimeXp = Mathf.Max(_lifetimeXp, s.HeroLifetimeXp);
            // A restored hero past level 1 already received its first-level-up starter
            // gift in the run that earned the level — never double-grant it.
            if (_level > 1) _hasGrantedStarterPoints = true;
            HasRestoredFromSave = true;
            OnXpChanged?.Invoke(_xp, XpToNextFor(_level));
            return true;
        }

        /// <summary>
        /// F8-47 — mirrors the live level/XP into GameState so any save write (wave
        /// clear, quit, scene transition) persists it. Deliberately does NOT force a
        /// Save() — AddXp is hot (every kill); the spine's existing save moments flush it.
        /// </summary>
        private void WriteBackToState()
        {
            var s = GameStateService.Instance?.State;
            if (s == null) return;
            // WO-1220 §12 — THE CLOBBER DETECTOR. GameState.HeroLevel has exactly two runtime
            // writers: the Load path (which traces what it installs) and this line. If the save
            // is sitting at a pristine new-game 1/0/0 and this component is about to stamp a
            // level above 1 over it, the previous run's carrier outlived the New Game — the
            // exact shape of the owner's 2026-08-26 capture (a fresh Ranger restoring the
            // Mage's level=4 xp=3531.9 "fromSave=True"). Name it at the moment it happens
            // rather than five minutes later at the attach that reads the result.
            if (_level > 1 && s.HeroLevel <= 1 && s.HeroXp <= 0f && s.HeroLifetimeXp <= 0f)
            {
                FlowTrace.Warn("HeroXp",
                    $"WriteBackToState is OVERWRITING a pristine new-game save (level={s.HeroLevel} " +
                    $"xp={s.HeroXp:0.#}) with this carrier's level={_level} xp={_xp:0.#} on " +
                    $"'{gameObject.name}' scene '{gameObject.scene.name}' — a HeroProgression from " +
                    "the PREVIOUS run outlived ResetToNewGame and was never sent ResetForNewGame " +
                    "(WO-1220).");
            }
            s.HeroLevel = _level;
            s.HeroXp = _xp;
            s.HeroLifetimeXp = _lifetimeXp;
        }

        public int AddXp(float amount)
        {
            if (amount <= 0f) return 0;
            // WO-1246: an xp-weekend charge doubles hero XP for 24h. The multiplier is
            // TIME, never a combat stat — it does not change damage, only the rate this
            // method banks. Capped at 2x; a second token extends the window.
            float xpMult = ConvenienceRedeemer.XpMultiplier();
            if (xpMult > 1f) amount *= xpMult;
            FlowTrace.Step("HeroXp", $"AddXp amount={amount:0.#} (level={_level} xp={_xp:0.#}/{XpToNextFor(_level):0.#}" +
                                     (xpMult > 1f ? $", xp-weekend {xpMult:0.##}x" : "") + ")");
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
            WriteBackToState();   // F8-47 — persist level/xp on every change
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
            // WO-977: the latch used to flip BEFORE the grants and the two `?.` calls were
            // UNWRAPPED — a null SkillSystem.Instance (or a throw) silently granted ZERO
            // points while the already-true latch made the loss PERMANENT (fires once per
            // player, so a save replay can never reproduce it). Now: grant FIRST, latch
            // ONLY on a CONFIRMED AvailablePoints delta, and trace the MEASURED before/after
            // instead of asserting the intent (INSTRUMENTATION_STANDARD §1.4b).
            if (!_hasGrantedStarterPoints)
            {
                const int StarterPoints = 2;
                var skills = SkillSystem.Instance;
                if (skills == null)
                {
                    FlowTrace.Fail("HeroXp", $"DEF-82 starter grant SKIPPED at level {newLevel} — SkillSystem.Instance is NULL; player received 0 of {StarterPoints} starter skill points. NOT latched — retries on the next level-up IN THIS SESSION ONLY: RestoreFromSave latches on level>1, so a reload before then loses them permanently (WO-981 §A).");
                }
                else
                {
                    // The baseline is captured ONCE, on the first attempt, and survives a failed
                    // one. GrantSkillPoint increments BEFORE firing OnSkillsChanged, so a throwing
                    // subscriber leaves the increment landed; re-reading AvailablePoints as the
                    // baseline on a retry would treat that survivor as pre-existing and grant the
                    // full 2 again — 3 points instead of 2. Measuring every attempt against the
                    // ORIGINAL baseline makes the whole sequence idempotent, however many attempts
                    // it takes.
                    if (_starterBaseline < 0) _starterBaseline = skills.AvailablePoints;
                    int before = _starterBaseline;
                    int calls = 0;
                    try
                    {
                        while (skills.AvailablePoints - _starterBaseline < StarterPoints)
                        {
                            skills.GrantSkillPoint();
                            calls++;
                            if (calls > StarterPoints) break;   // belt: never spin on a no-op granter
                        }
                    }
                    catch (System.Exception e)
                    {
                        FlowTrace.Fail("HeroXp", $"DEF-82 starter grant THREW at level {newLevel} after {calls}/{StarterPoints} calls — {StarterPoints - calls} starter skill points NOT granted: {e.GetType().Name}: {e.Message}");
                    }

                    int after = skills.AvailablePoints;
                    int delta = after - before;
                    if (delta >= StarterPoints)
                    {
                        _hasGrantedStarterPoints = true;   // latch ONLY on confirmed success
                        FlowTrace.Step("HeroXp", $"DEF-82 starter skill points GRANTED at level {newLevel}: availablePoints {before}->{after} (delta={delta}, calls={calls}/{StarterPoints}) — latched.");
                    }
                    else
                    {
                        FlowTrace.Fail("HeroXp", $"DEF-82 starter grant INCOMPLETE at level {newLevel}: availablePoints {before}->{after} (delta={delta}, expected {StarterPoints}, calls={calls}) — points LOST this level. NOT latched, retries on the next level-up IN THIS SESSION ONLY (RestoreFromSave latches on level>1 — WO-981 §A).");
                    }
                }
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
