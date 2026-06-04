// =============================================================================
// TierSystem (DEF-50) — milestone rewards + prestige unlocks.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Sits above HeroProgression's per-level trickle and fires bigger, more
//   satisfying rewards every 5 hero levels. The first six milestones are
//   hand-authored (level 5 → 30, escalating); beyond level 30 a repeating
//   pattern fires every 10 levels so the system never dead-ends.
//
//   Each milestone grants:
//     • Bonus Wisdom  — talent-tree currency (WisdomCurrencyService.Grant)
//     • Bonus Glimmer — cosmetic-shop currency (GlimmerCurrencyService.TryAddGlimmer)
//     • Achievement cosmetic unlock (optional) — free achievement-gated items
//       that the player can't buy; granted via GlimmerCurrencyService.GrantAchievement
//
//   A "TIER UP!" label pops at the hero's world position (DamageNumberSpawner),
//   and OnTierReached fires for any HUD banner / fanfare that wants to react.
//
// MILESTONES:
//   Lv 5   Initiate  — +5 Wisdom, +10 Glimmer
//   Lv 10  Defender  — +8 Wisdom, +20 Glimmer + "village-bloomtide-banners"
//   Lv 15  Guardian  — +12 Wisdom, +30 Glimmer
//   Lv 20  Warden    — +15 Wisdom, +50 Glimmer + "pet-ice-firstfriend"
//   Lv 25  Champion  — +20 Wisdom, +75 Glimmer
//   Lv 30  Legend    — +25 Wisdom, +100 Glimmer + "hero-mage-wanderer"
//   Lv 40+ Mythic    — +30 Wisdom, +150 Glimmer (repeats every 10 levels)
//
// ARCHITECTURE:
//   * DontDestroyOnLoad singleton, BeforeSceneLoad bootstrap.
//   * Subscribes to HeroProgression.OnLevelUp lazily in Update() so the two
//     BeforeSceneLoad singletons don't race each other.
//   * _lastGrantedLevel guards against double-granting if HandleLevelUp is
//     invoked with the same level more than once (e.g. a re-enable path).
//   * All currency grants are null-guarded — missing a service is a silent no-op,
//     not a crash; the label still fires.
// =============================================================================

using DeNelle.Cosmetics;
using DeNelle.Village.Talents;
using UnityEngine;

namespace DeNelle.Village
{
    // ── Milestone data ────────────────────────────────────────────────────────

    /// <summary>One tier milestone — the data package handed to <see cref="TierSystem.OnTierReached"/>.</summary>
    public sealed class TierMilestone
    {
        /// <summary>Hero level at which this milestone fires.</summary>
        public readonly int Level;

        /// <summary>Display title shown in the "TIER UP!" banner.</summary>
        public readonly string Title;

        /// <summary>Bonus Wisdom points granted on reaching this tier.</summary>
        public readonly int BonusWisdom;

        /// <summary>Bonus Glimmer granted on reaching this tier.</summary>
        public readonly int BonusGlimmer;

        /// <summary>
        /// Achievement-gated cosmetic id unlocked for free on reaching this tier.
        /// Null when the tier carries no cosmetic reward.
        /// </summary>
        public readonly string AchievementCosmeticId;

        public TierMilestone(int level, string title, int wisdom, int glimmer,
                             string achievementCosmeticId = null)
        {
            Level = level;
            Title = title;
            BonusWisdom = wisdom;
            BonusGlimmer = glimmer;
            AchievementCosmeticId = achievementCosmeticId;
        }
    }

    // ── TierSystem ────────────────────────────────────────────────────────────

    /// <summary>
    /// Milestone progression layer over <see cref="HeroProgression"/>. Subscribes
    /// to <see cref="HeroProgression.OnLevelUp"/> and fires bigger rewards at
    /// every 5-level threshold. Cosmetic achievement unlocks are granted at levels
    /// 10, 20, and 30 using the three achievement-gated items in cosmetics.json.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TierSystem : MonoBehaviour
    {
        public static TierSystem Instance { get; private set; }

        /// <summary>Fires when a tier milestone is crossed. Arg = the milestone reached.</summary>
        public event System.Action<TierMilestone> OnTierReached;

        // ── Milestone table ───────────────────────────────────────────────────

        // cosmetic ids match cosmetics.json "achievement" entries verbatim.
        private static readonly TierMilestone[] HandCraftedTiers =
        {
            new TierMilestone( 5, "Initiate",  5,  10),
            new TierMilestone(10, "Defender",  8,  20, "village-bloomtide-banners"),
            new TierMilestone(15, "Guardian", 12,  30),
            new TierMilestone(20, "Warden",   15,  50, "pet-ice-firstfriend"),
            new TierMilestone(25, "Champion", 20,  75),
            new TierMilestone(30, "Legend",   25, 100, "hero-mage-wanderer"),
        };

        // Repeating tier for every 10 levels beyond the last hand-crafted one.
        private const int RepeatEvery    = 10;
        private const int RepeatStart    = 40;  // first repeating tier
        private const int RepeatWisdom   = 30;
        private const int RepeatGlimmer  = 150;

        // ── Runtime state ─────────────────────────────────────────────────────

        private bool _subscribed;
        private int  _lastGrantedLevel;   // guard: never grant the same level twice

        private static readonly Color TierLabelColor = new Color(1f, 0.84f, 0.2f, 1f); // gold

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[TierSystem]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TierSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        // ── Lazy subscription (avoids BeforeSceneLoad ordering race) ──────────

        private void Update()
        {
            if (!_subscribed) TrySubscribe();
        }

        private void TrySubscribe()
        {
            var hp = HeroProgression.Instance;
            if (hp == null) return;
            hp.OnLevelUp += HandleLevelUp;
            _subscribed = true;
            // Check the current level immediately so a tier earned before we
            // subscribed (extremely unlikely at BeforeSceneLoad, but defensive)
            // is not silently skipped.
            if (hp.Level > 1) HandleLevelUp(hp.Level);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var hp = HeroProgression.Instance;
            if (hp != null) hp.OnLevelUp -= HandleLevelUp;
            _subscribed = false;
        }

        // ── Core handler ──────────────────────────────────────────────────────

        private void HandleLevelUp(int level)
        {
            if (level <= _lastGrantedLevel) return;   // already processed this level

            var milestone = ResolveMilestone(level);
            if (milestone == null) return;

            _lastGrantedLevel = level;

            // ── Grant rewards ─────────────────────────────────────────────────

            if (milestone.BonusWisdom > 0)
                WisdomCurrencyService.Instance?.Grant(milestone.BonusWisdom);

            if (milestone.BonusGlimmer > 0)
                GlimmerCurrencyService.Instance?.TryAddGlimmer(milestone.BonusGlimmer);

            if (!string.IsNullOrEmpty(milestone.AchievementCosmeticId))
                GlimmerCurrencyService.Instance?.GrantAchievement(milestone.AchievementCosmeticId);

            // ── Feedback ──────────────────────────────────────────────────────

            var hp = HeroProgression.Instance;
            if (hp != null)
            {
                // Gold label floats above the hero — bigger scale (1.6f) so it's
                // clearly a bigger moment than the regular "LEVEL UP!" pop.
                DamageNumberSpawner.SpawnLabel(
                    $"TIER UP!  {milestone.Title}",
                    hp.WorldPosition + Vector3.up * 0.5f,
                    TierLabelColor,
                    1.6f);
            }

            OnTierReached?.Invoke(milestone);
        }

        // ── Milestone lookup ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the <see cref="TierMilestone"/> for <paramref name="level"/>,
        /// or null if this level has no milestone.
        /// </summary>
        private static TierMilestone ResolveMilestone(int level)
        {
            // Hand-crafted tiers (levels 5, 10, 15, 20, 25, 30).
            foreach (var t in HandCraftedTiers)
                if (t.Level == level) return t;

            // Repeating milestone every RepeatEvery levels beyond RepeatStart.
            if (level >= RepeatStart && (level - RepeatStart) % RepeatEvery == 0)
                return new TierMilestone(level, "Mythic", RepeatWisdom, RepeatGlimmer);

            return null;
        }

        // ── Static query helpers (for HUD / other systems) ───────────────────

        /// <summary>
        /// The <see cref="TierMilestone"/> the hero is working toward, i.e.
        /// the first milestone whose level exceeds <paramref name="currentLevel"/>.
        /// Returns null beyond the last defined tier (no ceiling for repeating tiers
        /// — there is always a next one).
        /// </summary>
        public static TierMilestone NextMilestone(int currentLevel)
        {
            foreach (var t in HandCraftedTiers)
                if (t.Level > currentLevel) return t;

            // Compute the next repeating tier.
            int next = currentLevel < RepeatStart
                ? RepeatStart
                : RepeatStart + ((currentLevel - RepeatStart) / RepeatEvery + 1) * RepeatEvery;
            return new TierMilestone(next, "Mythic", RepeatWisdom, RepeatGlimmer);
        }
    }
}
