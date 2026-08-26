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
using System.Collections.Generic;
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
        //  WO-1229 - the AMBIENT ENVIRONMENT ring, and the accessibility reserve
        // =====================================================================
        //
        // ## THE CAPTURED DEFECT (device log, dg_starter_loop, 08-25 19:29-19:31)
        //
        //   [Flow:DungeonVFX] bound 44 CandleAnchor marker(s) to proximity-pooled
        //                     Env_Candle flames in 'dg_starter_loop'.
        //   ... 57 s later, continuously ...
        //   [Flow:VFXManager] PlayLoop('Env_Candle')     SKIPPED - active loops 24/24
        //   [Flow:VFXManager] PlayLoop('Aura_NearDeath') SKIPPED - active loops 24/24
        //   [Flow:HeroHpAura] 'NearDeath' aura was REFUSED by VFXManager ... the hero
        //                     has no non-colour danger signal.
        //
        // 44 ambient candles, each an INDEPENDENT first-come claimant on the GLOBAL
        // loop pool, with no ring and no restraint. NearestAuraRing bounds enemy and
        // pet auras precisely so they "can never monopolise the pool no matter how
        // many bodies a wave spawns" - and ambient dressing, which a dressed room
        // authors by the dozen, had no equivalent. The colourblind low-HP tell lost
        // the race to a candle.
        //
        // ## WHY A RESERVE AND NOT A BIGGER CEILING
        //
        // The ceiling has already moved 20 -> 40 -> 24 across this repo's history
        // while the symptom kept coming back. Ambient dress is the one loop class
        // that is BY DEFINITION unbounded (a room author adds candles until the room
        // looks right) and BY DEFINITION the least load-bearing (a flame you cannot
        // resolve is decoration). So it is the class that yields, and it yields by a
        // rule rather than by luck: the ambient ring is the SMALLER of a fixed
        // nearest-N and whatever headroom is left ABOVE a reserve that ambient may
        // never touch. The reserve is what the accessibility loops start into.

        /// <summary>
        /// How many AMBIENT ENVIRONMENT loops (dungeon candles, braziers, steam
        /// vents - the authored room dress) may hold a loop slot at once, nearest
        /// first. Deliberately smaller than <see cref="NearestAuraRing"/>: room dress
        /// is the most numerous and least load-bearing loop class in the game.
        /// </summary>
        public const int AmbientEnvRing = 8;

        /// <summary>
        /// Loop slots that AMBIENT dress may never occupy, held open for the loops a
        /// player reads state from - above all the colourblind low-HP tell
        /// (Aura_LowHealth / Aura_NearDeath), which is a LOOP and is therefore
        /// refusable. Two, because the tell swaps recipe below quarter health: the
        /// outgoing loop's graceful pool-return overlaps the incoming one, so a
        /// reserve of one could still be a race.
        /// </summary>
        public const int AccessibilityReserve = 2;

        /// <summary>
        /// How many ambient environment loops may be granted right now: the fixed
        /// <see cref="AmbientEnvRing"/>, further clamped so that the ambient class
        /// can never push the live loop count above <c>cap - AccessibilityReserve</c>.
        /// Pure arithmetic on numbers the caller reads - it owns no state, starts no
        /// effect and is therefore directly testable headless (VfxAmbientLoopBudgetRegression).
        /// </summary>
        /// <param name="liveLoops">VFXManager's current live loop count (ALL classes).</param>
        /// <param name="ambientHeld">How many of <paramref name="liveLoops"/> this ambient class holds.</param>
        /// <param name="cap">The live ceiling; pass <see cref="CurrentCap"/> unless testing.</param>
        public static int AmbientEnvBudget(int liveLoops, int ambientHeld, int cap)
        {
            if (cap <= 0) return 0;
            // What everyone ELSE is holding. Ambient's own hold is excluded so the
            // budget is a stable target rather than a feedback loop that ratchets
            // down every tick it is already at its own limit.
            int others = Mathf.Max(0, liveLoops - Mathf.Max(0, ambientHeld));
            int room   = cap - others - AccessibilityReserve;
            return Mathf.Clamp(room, 0, AmbientEnvRing);
        }

        /// <summary>Convenience overload against the live tier ceiling.</summary>
        public static int AmbientEnvBudget(int liveLoops, int ambientHeld)
            => AmbientEnvBudget(liveLoops, ambientHeld, CurrentCap);

        // =====================================================================
        //  WO-1229 ruling 2 - THE ACCESSIBILITY ALLOWLIST (owner, 2026-08-26)
        // =====================================================================
        //
        // The reserve above keeps AMBIENT dress honest. It does not make the low-HP
        // tell UNREFUSABLE: enemy auras, POI markers, tower projectiles and portal
        // loops can still fill the pool between them, and the captured line the owner
        // actually lived through was a refusal:
        //
        //   [Flow:HeroHpAura] 'NearDeath' aura ('Aura_NearDeath') was REFUSED by
        //                     VFXManager (loop cap or quality gate). This is the
        //                     PRIMARY colourblind low-HP read - if it is being
        //                     dropped, the hero has no non-colour danger signal.
        //
        // So these two types BYPASS THE CAP ENTIRELY. The owner chose this over a
        // priority field on PlayLoop, and the reasoning is worth keeping: a priority
        // parameter puts a CLASSIFICATION BURDEN on ~30 call sites forever, and the
        // day one of them classifies wrong the effect goes missing SILENTLY - which is
        // the exact bug class this whole ticket is. Two hardcoded ids cannot be
        // mis-classified by a caller who never sees them.
        //
        // ## IT IS A NAMED CONSTANT, NOT TWO LITERALS AT THE CHECK SITE
        //
        // Owner ruling 2026-08-26: this repo is "moving to consistency" on exactly
        // this. An id written inline at the place it is tested is the duplicated-state
        // drift CLAUDE.md keeps recording (the stale WO number block, the retired
        // dependency table, the hardcoded repo root). One array, one predicate, and
        // every reader - VFXManager, the regression, the next person - asks the same
        // question of the same list.
        //
        // ## THE BOUND, STATED HONESTLY
        //
        // HeroHpStateAura holds exactly ONE handle ("THE one held loop. There is
        // deliberately no second field") and is the only owner of either type. So the
        // overrun this allowlist can cause is at most TWO loops above the ceiling, and
        // only during the recipe swap below quarter health, while the outgoing loop's
        // pool return overlaps the incoming one. It cannot grow with the enemy count,
        // the room dress or the session length. If a second owner of either type ever
        // appears, that bound is gone - which is why the regression asserts the list
        // has exactly these two members.

        /// <summary>
        /// The loop types that may ALWAYS start, cap or no cap: the colourblind low-HP
        /// tell. Not a priority, not a policy a caller opts into - a closed list this
        /// file owns. See the block comment above for why it is two ids and not a
        /// parameter.
        /// </summary>
        public static readonly VFXType[] AccessibilityLoops =
        {
            VFXType.Aura_LowHealth,
            VFXType.Aura_NearDeath,
        };

        /// <summary>True when <paramref name="type"/> is on <see cref="AccessibilityLoops"/>.</summary>
        public static bool IsAccessibilityLoop(VFXType type)
        {
            for (int i = 0; i < AccessibilityLoops.Length; i++)
                if (AccessibilityLoops[i] == type) return true;
            return false;
        }

        /// <summary>
        /// THE cap decision, in one place. <see cref="VFXManager"/> calls this rather
        /// than comparing the two numbers itself, so the allowlist cannot be true here
        /// and false at the check site. Pure - directly testable headless.
        /// </summary>
        public static bool WouldRefuseLoop(VFXType type, int liveLoops, int cap)
        {
            if (IsAccessibilityLoop(type)) return false;   // unrefusable, by ruling
            return liveLoops >= cap;
        }

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
        /// Declare that the dungeon tier is (or is no longer) in force. Idempotent.
        ///
        /// WO-1229: this is now a MANUAL OVERRIDE, not the binding path. It is still
        /// called by <see cref="VFXManager.ApplyDungeonMode"/>, but that seam has never
        /// fired in a shipped build (its only caller is a component placed in zero
        /// scenes), which is why <see cref="RebindSceneTier"/> exists and is
        /// authoritative on every scene event. Anything written here is overwritten by
        /// the next scene load or unload - deliberately: the loaded scene set is the
        /// fact, and a flag that can disagree with it is the drift this ticket ended.
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
        /// Drop back to the village tier.
        ///
        /// WO-1229: this is NO LONGER THE SCENE-UNLOAD HOOK - that line was true when
        /// written and is not any more, so it is corrected rather than left to mislead.
        /// <see cref="OnSceneUnloaded"/> now clears the encounter-scoped boss flag and
        /// RE-RESOLVES the dungeon flag from what is still loaded, because blanket-
        /// clearing it meant unloading any additive scene off a dungeon silently
        /// dropped the dungeon ceiling. Kept as an explicit escape hatch; the invariant
        /// it protected (no stale tier into the next scene) is now structural.
        /// </summary>
        public static void ResetToVillage(string reason)
        {
            if (!_dungeon && !_boss) return;
            _dungeon = false;
            _boss    = false;
            Recompute("reset (" + reason + ")");
        }

        // =====================================================================
        //  WO-1229 ruling 1 - THE TIER BINDS ITSELF (owner, 2026-08-26)
        // =====================================================================
        //
        // ## THE DEAD SEAM THIS REPLACES
        //
        // The header above says tier state is "PUSHED BY THE THINGS THAT KNOW, NOT
        // SNIFFED FROM SCENE NAMES", and names VFXManager.ApplyDungeonMode as the
        // thing that knows. That principle was right and it still produced a seam that
        // has NEVER FIRED IN A SHIPPED BUILD, because the only caller of
        // ApplyDungeonMode is DungeonSceneBootstrap - a MonoBehaviour that must be
        // hand-placed in a dungeon scene, and which
        //
        //     grep -rl DungeonSceneBootstrap Assets --include=*.unity --include=*.prefab
        //
        // finds in ZERO scenes and ZERO prefabs (verified independently by the lead,
        // 2026-08-26). The evidence-by-absence is total: there is not one
        // [Flow:VfxBudget] line in any device log in this repo. Every dungeon the owner
        // has ever played ran on the VILLAGE ceiling of 24 - which is precisely the
        // 24/24 saturation captured in dg_starter_loop with 44 candles bound.
        //
        // ## WHY THIS ONE CANNOT GO DEAD THE SAME WAY
        //
        // It depends on NO AUTHORING STEP. A new dungeon scene gets the right tier by
        // existing, the way CastleDefensePlansService and RaidScoring already bind
        // themselves: RuntimeInitializeOnLoadMethod + a scene test, registered once,
        // for the lifetime of the process. There is nothing for a future scene author
        // to remember, and therefore nothing for them to forget. The failure mode that
        // killed the old seam - "someone must add a component" - has no analogue here.
        //
        // The remaining risk is a NAMING one, and it is deliberately concentrated in a
        // single public predicate (IsDungeonSceneName) that the candle installer shares
        // rather than re-implementing, so the project has ONE answer to the question
        // "is this a dungeon scene" instead of two that can drift. VfxSceneTierRegression
        // pins both halves of that convention.
        //
        // ## IT SCANS EVERY LOADED SCENE, NOT THE ONE THAT JUST LOADED
        //
        // Dungeons load additively alongside a persistent hub. Reading only the newest
        // scene would drop the tier the moment any unrelated scene loaded on top. So
        // both hooks RE-RESOLVE from the full set of loaded scenes, which also makes
        // the unload path fall out for free.

        /// <summary>
        /// Is <paramref name="sceneName"/> a dungeon scene? THE single answer for the
        /// whole project - DungeonCandleVfxInstaller binds its flames off this same
        /// predicate rather than repeating the test. Convention: a name starting
        /// "dg_" (the baked RoomForge dungeons) or containing "Dungeon".
        /// </summary>
        public static bool IsDungeonSceneName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName.StartsWith("dg_", StringComparison.OrdinalIgnoreCase)
                || sceneName.IndexOf("Dungeon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Does this set of loaded scene names put the dungeon tier in force? True if
        /// ANY of them is a dungeon scene (additive loads - see the block comment).
        /// Pure: no Unity state, no side effects, directly testable headless.
        /// </summary>
        public static bool ResolveDungeonTier(IReadOnlyList<string> loadedSceneNames)
        {
            if (loadedSceneNames == null) return false;
            for (int i = 0; i < loadedSceneNames.Count; i++)
                if (IsDungeonSceneName(loadedSceneNames[i])) return true;
            return false;
        }

        /// <summary>
        /// The ceiling for a given tier combination - the MAX rule, in one place, so
        /// <see cref="Recompute"/> and any test ask the same function. Pure.
        /// </summary>
        public static int TierCapFor(bool dungeon, bool boss)
        {
            int next = VillageLoops;
            if (boss)    next = Mathf.Max(next, BossLoops);
            if (dungeon) next = Mathf.Max(next, DungeonLoops);
            return next;
        }

        // Registered once, at load, so the tier is structural rather than something
        // each scene has to remember to declare.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneLoaded   += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RebindSceneTier("runtime init");
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode)
            => RebindSceneTier("sceneLoaded:" + s.name);

        private static void OnSceneUnloaded(Scene s)
        {
            // The BOSS flag is encounter-scoped and cannot survive a scene teardown -
            // that half of the old ResetToVillage hook is preserved verbatim in intent.
            // The DUNGEON flag is no longer blanket-cleared here: it is re-resolved from
            // what is still loaded, so unloading a UI overlay off a dungeon no longer
            // silently drops the tier.
            _boss = false;
            RebindSceneTier("sceneUnloaded:" + s.name);
        }

        // Reusable scratch - scene hooks are rare, but this never runs in a hot path
        // and allocating a list per scene load is still pointless.
        private static readonly List<string> _loadedSceneNames = new List<string>(8);

        /// <summary>
        /// Re-resolve the dungeon tier from every loaded scene and apply it. Public so
        /// a headless harness can drive it; idempotent, and silent when nothing changes.
        /// </summary>
        public static void RebindSceneTier(string why)
        {
            _loadedSceneNames.Clear();
            int n = SceneManager.sceneCount;
            for (int i = 0; i < n; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.IsValid() && sc.isLoaded) _loadedSceneNames.Add(sc.name);
            }

            bool dungeon = ResolveDungeonTier(_loadedSceneNames);

            // NO SILENT BIND. The whole reason this ticket needed a device session to
            // diagnose is that a tier which never engaged produced no line at all. An
            // ENGAGE is always announced, whether or not the resolved cap happens to
            // change (a dungeon entered during a boss fight would not move the number,
            // and that is exactly the case a reader would otherwise mis-read as
            // "never engaged").
            if (dungeon && !_dungeon)
                FlowTrace.Step("VfxBudget",
                    "DUNGEON TIER ENGAGED (" + why + "; loaded scenes: " +
                    string.Join(", ", _loadedSceneNames) + "). Ceiling " + VillageLoops + " -> " +
                    TierCapFor(true, _boss) + ". Bound by the VfxLoopBudget runtime hook, NOT by a " +
                    "component in the scene - the old DungeonSceneBootstrap seam was in zero scenes " +
                    "and never once fired (WO-1229). The ambient nearest-" + AmbientEnvRing +
                    " ring and the " + AccessibilityReserve + "-slot reserve still apply: " +
                    DungeonLoops + " is headroom, not a licence to unbind room dress.");
            else if (!dungeon && _dungeon)
                FlowTrace.Step("VfxBudget",
                    "dungeon tier RELEASED (" + why + "; loaded scenes: " +
                    string.Join(", ", _loadedSceneNames) + "). Back to the village ceiling.");

            _dungeon = dungeon;
            Recompute(why);
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        // MAX of the applicable tiers - see the header for why this is not a
        // precedence chain.
        private static void Recompute(string why)
        {
            int next = TierCapFor(_dungeon, _boss);

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
