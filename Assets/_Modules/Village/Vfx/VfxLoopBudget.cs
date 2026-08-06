// =============================================================================
// VfxLoopBudget - the scene-tiered ceiling on simultaneous Family-A loops.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## WHY THIS EXISTS (WO-889 part 1 - and it lands BEFORE the auras it guards)
//
// VFXManager._maxActiveLoops shipped at a flat 20 (VFXManager.cs, the serialized
// "Performance Limits" field). That number was never a budget anybody computed; it
// was a default. SIX separate F8 captures show it saturated and starving live
// effects - the evidence is cited at source in VfxLoopFlagRegression's header:
//
//   capture-20260730-175552.md:55  PlayKey('ArcherTower_Projectile') SKIPPED -
//                                  active loops 20/20 (cap hit)
//   capture-20260730-175447.md:21  ARcaneTower_Projectile
//   capture-20260730-175729.md:54  ArcaneTower-Baselevel_Projectile
//   capture-20260716-205819.md:99  Poi_NodeAura
//   capture-20260716-210343.md:97  Poi_Landmark
//
// Tower projectiles, the Tree-of-Life aura and every POI marker went silently
// missing because unrelated loops had eaten the pool. WO-889 then wants to add
// PERSISTENT auras to enemies, pets, towers, Hearts and boss phases - i.e. to
// multiply the population of exactly the thing that saturated it. Mass-wiring
// those first is how tonight's P0 (bd532d5b) happened. So the ceiling is raised
// and made SCENE-AWARE here, and the nearest-N ring (VfxAuraProximityCuller)
// bounds the population, BEFORE any of that wiring exists.
//
// ## THE TIERS (WO-889: village 24 / dungeon 48 / boss 32)
//
// They differ because the scenes differ in what is legitimately on screen:
//   * VILLAGE 24 - a static town. Its loops are ambient dressing plus a handful of
//     combat auras. It is the tier a phone spends the most wall-clock time in.
//   * DUNGEON 48 - the dressed-room tier. Candles, braziers, fog and steam vents
//     are AMBIENT loops authored per room (handbook section 10 calls out "30 candle
//     IsLoop" as the motivating risk), and they coexist with a pack's worth of
//     enemy auras. This is the only tier where the ambient dress alone can
//     outnumber the old flat cap.
//   * BOSS 32 - fewer bodies than a dungeon, but each is expensive: the dragon
//     alone holds a phase aura AND a breath stream (DragonBoss._auraHandle +
//     _breathHandle), and the arena keeps its own dressing.
//
// A cap is NOT a licence to leak. It is headroom so that a CORRECTLY stopped
// population fits. Every loop still needs an owner that stops it on every exit
// path; see HeroHpStateAura for the worked example.
//
// ## PRECEDENCE IS "MAX OF WHAT APPLIES", DELIBERATELY
//
// A boss fought INSIDE a dungeon is both tiers at once. Taking the max (48) rather
// than the last-set value means a tier combination can never be tighter than
// either tier alone - the failure mode of a precedence chain is that some
// unanticipated overlap silently picks the SMALLER number and starves a scene
// nobody tested. Over-provisioning by a few slots costs pooled GameObjects;
// under-provisioning costs invisible effects, which is the bug this file exists
// to end.
//
// ## STATE IS PUSHED BY THE THINGS THAT KNOW, NOT SNIFFED FROM SCENE NAMES
//
// Scene-name string matching would be a second, silently-diverging source of truth
// the day a scene is renamed (the repo already carries the Village.unity /
// MainCastle_Hall.unity naming hangover). Instead the two systems that ALREADY own
// the fact declare it:
//   * VFXManager.ApplyDungeonMode(bool) - the existing dungeon entry/exit seam.
//   * DragonBoss OnEnable/OnDisable      - the existing boss lifecycle.
// Everything else derives. SetDungeon/SetBossActive are idempotent and safe to
// call from either seam in any order.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The scene-tiered ceiling on simultaneous VFX loops, plus the size of the
    /// nearest-N aura ring. Pure state + arithmetic: it owns no pool and starts no
    /// effect. <see cref="VFXManager"/> subscribes to <see cref="CapChanged"/> and
    /// applies the value to its own limit.
    /// </summary>
    public static class VfxLoopBudget
    {
        // =====================================================================
        //  The tiers (WO-889). Felt-tunable bones; every one is a ceiling, never
        //  a target, and none of them excuses an unstopped loop.
        // =====================================================================

        /// <summary>Static town: ambient dressing plus a few combat auras.</summary>
        public const int VillageLoops = 24;

        /// <summary>Dressed dungeon: per-room candles/fog/steam PLUS a pack of enemy auras.</summary>
        public const int DungeonLoops = 48;

        /// <summary>Boss arena: fewer bodies, but the boss alone holds an aura AND a breath stream.</summary>
        public const int BossLoops = 32;

        /// <summary>
        /// How many enemy/pet auras may be live at once, nearest-first
        /// (<see cref="VfxAuraProximityCuller"/>). WO-889 specifies 6-8; the top of
        /// that band is used because the ring is re-evaluated on a timer and a
        /// tighter ring makes auras visibly pop in and out as the hero walks. This
        /// is a population bound on ONE class of loop and is deliberately far below
        /// every tier above, so enemy/pet auras can never monopolise the pool no
        /// matter how many bodies a wave spawns.
        /// </summary>
        public const int NearestAuraRing = 8;

        /// <summary>
        /// The flat value VFXManager shipped with, kept only so the change is
        /// legible in a log line ("20 -> 48") rather than appearing as a bare number.
        /// </summary>
        public const int LegacyFlatCap = 20;

        // =====================================================================
        //  State
        // =====================================================================

        private static bool _dungeon;
        private static bool _boss;
        private static int  _cap = VillageLoops;

        /// <summary>The live ceiling for the current scene tier.</summary>
        public static int CurrentCap => _cap;

        /// <summary>True while the dungeon tier applies.</summary>
        public static bool DungeonTier => _dungeon;

        /// <summary>True while the boss tier applies.</summary>
        public static bool BossTier => _boss;

        /// <summary>
        /// Raised whenever the resolved cap CHANGES. <see cref="VFXManager"/> subscribes
        /// and writes the value onto its own limit; nothing else should need it.
        /// </summary>
        public static event Action<int> CapChanged;

        /// <summary>A human-readable name for the tier currently in force.</summary>
        public static string TierName =>
            _dungeon && _boss ? "dungeon+boss"
            : _dungeon        ? "dungeon"
            : _boss           ? "boss"
                              : "village";

        // =====================================================================
        //  Seams
        // =====================================================================

        /// <summary>
        /// Declare that the dungeon tier is (or is no longer) in force. Called from
        /// <see cref="VFXManager.ApplyDungeonMode"/>, which is the existing seam the
        /// dungeon load/unload path already drives. Idempotent.
        /// </summary>
        public static void SetDungeon(bool active)
        {
            if (_dungeon == active) return;
            _dungeon = active;
            Recompute("dungeon=" + active);
        }

        /// <summary>
        /// Declare that a boss encounter is (or is no longer) live. Called from the
        /// boss's own OnEnable/OnDisable, so the flag cannot outlive the encounter
        /// even if the fight ends by the boss being destroyed rather than resolved.
        /// Idempotent.
        /// </summary>
        public static void SetBossActive(bool active)
        {
            if (_boss == active) return;
            _boss = active;
            Recompute("boss=" + active);
        }

        /// <summary>
        /// Drop back to the village tier. Hooked to scene unload so a dungeon or boss
        /// flag can NEVER survive into the next scene - a stale dungeon flag would
        /// silently hand the static town twice the ceiling it was measured for, and
        /// nothing would ever say so.
        /// </summary>
        public static void ResetToVillage(string reason)
        {
            if (!_dungeon && !_boss) return;
            _dungeon = false;
            _boss    = false;
            Recompute("reset (" + reason + ")");
        }

        // Registered once, at load, so the reset is structural rather than something
        // each scene has to remember to call.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene s) => ResetToVillage("sceneUnloaded:" + s.name);

        // =====================================================================
        //  Internals
        // =====================================================================

        // MAX of the applicable tiers - see the header for why this is not a
        // precedence chain.
        private static void Recompute(string why)
        {
            int next = VillageLoops;
            if (_boss)    next = Mathf.Max(next, BossLoops);
            if (_dungeon) next = Mathf.Max(next, DungeonLoops);

            if (next == _cap) return;

            int prev = _cap;
            _cap = next;

            FlowTrace.Step("VfxBudget",
                "loop cap " + prev + " -> " + _cap + " (tier=" + TierName + ", " + why +
                "; legacy flat cap was " + LegacyFlatCap + "). Nearest-N enemy/pet aura ring = " +
                NearestAuraRing + ".");

            CapChanged?.Invoke(_cap);
        }
    }
}
