// =============================================================================
// SkillSystem — DEF-73 (Linear). Singleton tracking the player's craft-skill
// levels (Blacksmith / Woodworking / Arcane) and the gate that tower placement
// queries via HasRequiredSkill(SkillRequirement).
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core.Progression (the namespace inside the existing
// DeNelle.Core asmdef — no new asmdef). It is ADDITIVE and does not conflict with
// the existing IXpEarner / XpEarnerRegistry / hero TalentTree in this namespace:
// those track hero XP/talents, this tracks separate craft-skill levels used to
// gate buildables. Per DEF-73 Correction Pass 1:
//   • namespace DeNelle.Core.Progression + `using UnityEngine;` (Issues 10, 11).
//   • Full singleton guard + OnDestroy clear (mirrors EconomyService).
//   • Skill levels are [SerializeField] private with public getters; only
//     GrantPoints/SetLevel mutate the backing fields (Issue 12).
//
// Adaptation: a RuntimeInitializeOnLoadMethod self-bootstrap (like EconomyService)
// so SkillSystem.Instance is non-null at runtime without scene authoring.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Data;

namespace DeNelle.Core.Progression
{
    /// <summary>
    /// Runtime store of the player's craft-skill levels. Gates tower placement via
    /// <see cref="HasRequiredSkill(SkillRequirement)"/>. Levels start at 1.
    /// </summary>
    public class SkillSystem : MonoBehaviour
    {
        public static SkillSystem Instance;

        // Owner Decision 2 (2026-05-27): craft skills START AT 0 so required-skill
        // gates are meaningful and leveling unlocks better towers. The new-player
        // gift (_availablePoints = 2, below) keeps the early game from a dead start.
        // Per-tower gate LEVELS live in the TowerData assets (TowerDataSeeder),
        // tunable independently of these starting values.
        [SerializeField] private int _blacksmithLevel  = 0;
        [SerializeField] private int _woodworkingLevel = 0;
        [SerializeField] private int _arcaneLevel      = 0;

        public int BlacksmithLevel  => _blacksmithLevel;
        public int WoodworkingLevel => _woodworkingLevel;
        public int ArcaneLevel      => _arcaneLevel;

        // DEF-77 — gathering skill + skill-point economy (additive delta; nothing
        // above is removed). A point is granted on hero level-up and spent via the
        // LevelUpSkillPopup. All backing fields are [SerializeField] private with
        // public getters (DEF-77 CP1 Issue 4).
        [SerializeField] private int _gatheringSpeed  = 0;
        [SerializeField] private int _availablePoints = NewPlayerPointGift;   // Decision 2: new-player gift (tunable)

        public int GatheringSpeed  => _gatheringSpeed;
        public int AvailablePoints => _availablePoints;

        /// <summary>Fired whenever a skill level or the available-point count changes.</summary>
        public event Action OnSkillsChanged;

        // Self-install so Instance is never null at runtime (no scene authoring
        // required). No-op if a scene-placed SkillSystem already exists. Mirrors
        // EconomyService's bootstrap (DEF-78).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("SkillSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<SkillSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // WO-1220 — this singleton is DontDestroyOnLoad and persists NOTHING, so its
            // levels and banked points survive a New Game for as long as the process lives.
            // That is also what makes it the cleanest proof of the defect: skill points that
            // reappear on a "new game" can only have come from memory.
            DeNelle.Core.State.GameStateService.NewGameStarted += ResetForNewGame;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DeNelle.Core.State.GameStateService.NewGameStarted -= ResetForNewGame;   // WO-1220 — static event: never leak.
        }

        /// <summary>
        /// WO-1220 — restores the authored FIRST-LAUNCH values: every craft skill back to 0
        /// (Owner Decision 2, 2026-05-27) and the banked points back to the new-player gift of
        /// 2. This is the state a freshly-constructed SkillSystem holds, not an arbitrary zero
        /// — a New Game that handed the player 0 points would be a harsher start than a cold
        /// launch, and one that kept the old total is the bug this closes.
        /// </summary>
        public void ResetForNewGame()
        {
            DeNelle.Core.Diagnostics.FlowTrace.Step("HeroXp",
                $"SkillSystem.ResetForNewGame: dropping banked points {_availablePoints}->{NewPlayerPointGift} " +
                $"and skills (bs={_blacksmithLevel} wood={_woodworkingLevel} arcane={_arcaneLevel} " +
                $"gather={_gatheringSpeed}) to the first-launch baseline — this singleton is " +
                "DontDestroyOnLoad and persists nothing, so nothing else would have cleared it.");
            _blacksmithLevel = 0;
            _woodworkingLevel = 0;
            _arcaneLevel = 0;
            _gatheringSpeed = 0;
            _availablePoints = NewPlayerPointGift;
            OnSkillsChanged?.Invoke();
        }

        /// <summary>
        /// The new-player skill-point gift (Decision 2). Named so the field initializer and
        /// <see cref="ResetForNewGame"/> can never drift onto two different numbers.
        /// </summary>
        public const int NewPlayerPointGift = 2;

        /// <summary>
        /// True when the requirement is satisfied: no requirement (type == None) or
        /// the matching skill level is at least <c>req.minLevel</c>.
        /// </summary>
        public bool HasRequiredSkill(SkillRequirement req)
        {
            if (req == null || req.type == SkillType.None) return true;
            switch (req.type)
            {
                case SkillType.Blacksmith:  return _blacksmithLevel  >= req.minLevel;
                case SkillType.Woodworking: return _woodworkingLevel >= req.minLevel;
                case SkillType.Arcane:      return _arcaneLevel      >= req.minLevel;
                // GatheringSpeed is not a tower gate (reserved for DEF-77); treat as
                // "no tower requirement" so it never blocks placement.
                case SkillType.GatheringSpeed: return true;
                default: return true;
            }
        }

        /// <summary>Adds <paramref name="amount"/> levels to a craft skill (clamped &gt;= 0).</summary>
        public void GrantPoints(SkillType type, int amount)
        {
            switch (type)
            {
                case SkillType.Blacksmith:  _blacksmithLevel  = Mathf.Max(0, _blacksmithLevel  + amount); break;
                case SkillType.Woodworking: _woodworkingLevel = Mathf.Max(0, _woodworkingLevel + amount); break;
                case SkillType.Arcane:      _arcaneLevel      = Mathf.Max(0, _arcaneLevel      + amount); break;
            }
        }

        /// <summary>Sets a craft skill to an absolute level (clamped &gt;= 0).</summary>
        public void SetLevel(SkillType type, int level)
        {
            level = Mathf.Max(0, level);
            switch (type)
            {
                case SkillType.Blacksmith:     _blacksmithLevel  = level; break;
                case SkillType.Woodworking:    _woodworkingLevel = level; break;
                case SkillType.Arcane:         _arcaneLevel      = level; break;
                case SkillType.GatheringSpeed: _gatheringSpeed   = level; break;
            }
            OnSkillsChanged?.Invoke();
        }

        // ── DEF-77 — skill-point economy ────────────────────────────────────────

        /// <summary>Award one spendable skill point (fired on hero level-up — DEF-77).</summary>
        public void GrantSkillPoint()
        {
            _availablePoints++;
            OnSkillsChanged?.Invoke();
        }

        /// <summary>
        /// Spend one available point to raise <paramref name="type"/> by a level.
        /// Returns false (no-op) with no points banked or for <see cref="SkillType.None"/>.
        /// </summary>
        public bool SpendPoint(SkillType type)
        {
            if (_availablePoints <= 0 || type == SkillType.None) return false;
            switch (type)
            {
                case SkillType.Blacksmith:     _blacksmithLevel++;  break;
                case SkillType.Woodworking:    _woodworkingLevel++; break;
                case SkillType.Arcane:         _arcaneLevel++;      break;
                case SkillType.GatheringSpeed: _gatheringSpeed++;   break;
                default: return false;
            }
            _availablePoints--;
            OnSkillsChanged?.Invoke();
            return true;
        }

        /// <summary>Current level of a craft skill (<see cref="SkillType.None"/> → 0).</summary>
        public int GetSkillLevel(SkillType type)
        {
            switch (type)
            {
                case SkillType.Blacksmith:     return _blacksmithLevel;
                case SkillType.Woodworking:    return _woodworkingLevel;
                case SkillType.Arcane:         return _arcaneLevel;
                case SkillType.GatheringSpeed: return _gatheringSpeed;
                default:                       return 0;
            }
        }
    }
}