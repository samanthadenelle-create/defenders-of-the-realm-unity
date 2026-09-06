// =============================================================================
// CombatCastCaravanMarkRegression — WO-935 / WO-991 / WO-910 / WO-994 pins
// -----------------------------------------------------------------------------
// Source + pure-logic gates (headless). Markers:
//   COMBAT_CAST_CARAVAN_MARK_OK / COMBAT_CAST_CARAVAN_MARK_FAIL
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class CombatCastCaravanMarkRegression
    {
        private const string Tag = "[combat-cast-caravan-mark] ";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                GateCombatCast(failures);
                GateCaravan(failures);
                GateMark(failures);
                GateShieldPort(failures);
            }
            catch (Exception ex)
            {
                failures.Add("threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = Tag + "FAIL " + failures.Count + ": " + string.Join("; ", failures);
                Debug.LogError("COMBAT_CAST_CARAVAN_MARK_FAIL " + reason);
                return false;
            }
            reason = Tag + "OK cast+caravan+mark+shield-port seams present";
            Debug.Log("COMBAT_CAST_CARAVAN_MARK_OK " + reason);
            return true;
        }

        private static void GateCombatCast(List<string> failures)
        {
            string path = "Assets/_Modules/Village/Combat/CombatCast.cs";
            if (!File.Exists(path))
            {
                failures.Add("CombatCast.cs missing (WO-935)");
                return;
            }
            string src = File.ReadAllText(path);
            if (!src.Contains("class CombatCast"))
                failures.Add("CombatCast type missing");
            if (!src.Contains("Fireball") || !src.Contains("Heal"))
                failures.Add("CombatCast missing fireball/heal spell ids");
            if (!src.Contains("SpellVfxFactory"))
                failures.Add("CombatCast must route VFX through SpellVfxFactory");
            if (!src.Contains("PlayCast"))
                failures.Add("CombatCast must drive cast anim");

            string troop = "Assets/_Modules/Village/Troops/TroopController.cs";
            if (File.Exists(troop))
            {
                string t = File.ReadAllText(troop);
                if (!t.Contains("CombatCast.Play"))
                    failures.Add("TroopController does not call CombatCast.Play for mage strikes");
            }
        }

        private static void GateCaravan(List<string> failures)
        {
            string path = "Assets/_Modules/Village/Buildings/HealingCaravanMobility.cs";
            if (!File.Exists(path))
            {
                failures.Add("HealingCaravanMobility.cs missing (WO-991)");
                return;
            }
            string src = File.ReadAllText(path);
            if (!src.Contains("FollowSpeed") && !src.Contains("1.05f"))
                failures.Add("caravan follow speed not pinned as crawl");
            if (!src.Contains("DamageTakenMult") && !src.Contains("1.75f"))
                failures.Add("caravan glass damage mult missing");
            if (!src.Contains("IDamageableStructure"))
                failures.Add("caravan must be damageable (IDamageableStructure)");

            // ── WO-1424 — THE DEATH PATH MUST ROUTE THROUGH Destructible ────────────────────
            // The three checks above pin the caravan as KILLABLE (48 glass HP x1.75 damage) and
            // nothing asserted that killing it cleaned up after itself. It did not: Die() called a
            // raw Destroy(gameObject, 0.4f) and NOTHING else, bypassing Destructible.NotifyBroken
            // — the one owner of structure death (Destructible.cs:150-193) that frees the grid
            // cell, Forgets the loader entry, DROPS THE PERSISTED BaseLayout RECORD, burns the
            // free-build and notifies the singleton bootstrap. The observed consequences: the F8
            // census line "REPLAYABLE record(s) have NO live body = [healing_caravan@(18,18)]", a
            // grid cell Occupied for the rest of the session, a free resurrection on reload
            // against the WO-753 ruling, and — because healing_caravan is singleton:true and
            // StructureSingleton.HasPlacedInstance answers from the RECORD ALONE — a build card
            // stuck on "Built" with the heal field gone and no way to get it back.
            // The caravan was the ONLY structure-death bypass in Assets/_Modules/Village; towers,
            // buildings and walls already route correctly (Building.cs:230, DefenseTower.cs:356).
            //
            // ⛔ EVERY PIN BELOW MATCHES THE STATEMENT FORM, INCLUDING ITS TRAILING PUNCTUATION —
            // NEVER THE BARE TOKEN. This file greps the WHOLE source with Contains, and
            // HealingCaravanMobility.cs now carries long comments that name "NotifyBroken",
            // "SceneOwnership.IsEnemyOwned" and "FollowsHero" in prose. A bare-token pin would be
            // satisfied by those comments and stay GREEN through a full revert of the code — which
            // is exactly the failure this suite already recorded once for the mark ("the old
            // source-grep passed while the mark was dead code", see GateMark below). If you add a
            // pin here, pin a line of code, then re-read the file's comments to confirm the string
            // appears nowhere else.
            //
            // ⚠ REVERT RECIPE (prove these two RED in ~10 seconds): in
            //   Assets/_Modules/Village/Buildings/HealingCaravanMobility.cs, inside Die(), replace
            //   `destructible.NotifyBroken("HealingCaravan hp0");` with `Destroy(gameObject, 0.4f);`
            //   and delete the `Destructible.Ensure(gameObject);` statement from Awake(). Both
            //   failures below must fire. Restore both statements to go green.
            if (!src.Contains("Destructible.Ensure(gameObject);"))
                failures.Add("caravan does not call Destructible.Ensure(gameObject) — its death would bypass " +
                             "the one-owner structure-death path and strand its BaseLayout record (WO-1424)");
            if (!src.Contains("NotifyBroken(\"HealingCaravan hp0\")"))
                failures.Add("caravan Die() does not call Destructible.NotifyBroken(\"HealingCaravan hp0\") — a " +
                             "killed caravan leaves its persisted record, keeps its grid cell Occupied, resurrects " +
                             "free on reload and locks the singleton build card on 'Built' (WO-1424)");

            // ── WO-1424 — THE OFFENSIVE / DEFENSIVE SPLIT ──────────────────────────────────
            // Owner ruling 2026-09-06, verbatim: "it slow follows as a combat attack item as
            // defensive item it stationary". The WO-991 follow is KEPT for the offensive case and
            // gated on SceneOwnership.IsEnemyOwned — this repo's one town-vs-enemy-scene signal.
            // STATIONARY IS THE FAIL-SAFE DEFAULT (an unresolved scene reads Player-owned).
            //
            // ⚠ REVERT RECIPE: delete the `_followsHero = SceneOwnership.IsEnemyOwned;` STATEMENT
            //   from HealingCaravanMobility.Awake() — the first failure fires (the header comment
            //   still names the type, which is why the pin includes the assignment). Delete the
            //   `public bool FollowsHero` declaration — the second fires, and BaseLayoutLoader stops
            //   compiling, which is the point: the carve decision must never drift away from the
            //   latched mode. Restore both to go green.
            if (!src.Contains("_followsHero = SceneOwnership.IsEnemyOwned;"))
                failures.Add("caravan no longer latches its follow mode from SceneOwnership.IsEnemyOwned — the " +
                             "defensive town caravan must be STATIONARY where the player placed it (WO-1424)");
            if (!src.Contains("public bool FollowsHero"))
                failures.Add("caravan no longer exposes public bool FollowsHero — BaseLayoutLoader keys the " +
                             "NavMesh carve on it, and a stationary caravan that does not carve wedges pets/NPCs " +
                             "on its collider forever (WO-1424)");

            string loader = "Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs";
            if (File.Exists(loader))
            {
                string l = File.ReadAllText(loader);
                // The carve test must be "will it MOVE?", not "does the component EXIST?" — the
                // component is now present on the stationary caravan too, so a presence test would
                // skip the carve on exactly the structure that needs it.
                if (!l.Contains("caravan.FollowsHero"))
                    failures.Add("BaseLayoutLoader keys the NavMesh carve on HealingCaravanMobility PRESENCE " +
                                 "rather than caravan.FollowsHero — the stationary caravan would not carve (WO-1424)");
            }
            else
            {
                failures.Add("BaseLayoutLoader.cs missing — cannot verify the caravan NavMesh carve split (WO-1424)");
            }

            string factory = "Assets/_Modules/Village/Catalog/StructureFactory.cs";
            string f = File.ReadAllText(factory);
            if (!f.Contains("healing_caravan") || !f.Contains("HealingCaravanMobility"))
                failures.Add("StructureFactory must attach HealingCaravanMobility for healing_caravan");
        }

        private static void GateMark(List<string> failures)
        {
            string path = "Assets/_Modules/Village/Combat/CombatMark.cs";
            if (!File.Exists(path))
            {
                failures.Add("CombatMark.cs missing (WO-910)");
                return;
            }
            string src = File.ReadAllText(path);
            if (!src.Contains("ScaleDamage") || !src.Contains("Apply"))
                failures.Add("CombatMark must expose Apply + ScaleDamage");

            // BEHAVIORAL round-trip (2026-08-15 review finding #6 — the old source-grep
            // passed while the mark was dead code): Apply on ONE component of a GameObject
            // must be readable through a DIFFERENT component of the same GameObject. That is
            // exactly the Apply(EnemyDamageable) → ScaleDamage(Enemy) seam that was broken by
            // per-component instance-id keying (finding #3).
            GameObject probe = null;
            try
            {
                probe = new GameObject("CombatMarkProbe");
                var compA = probe.AddComponent<BoxCollider>();     // stand-in for EnemyDamageable
                var compB = probe.AddComponent<SphereCollider>();  // stand-in for Enemy
                CombatMark.Apply(compA, 5f, 1.3f);
                float viaOther = CombatMark.DamageTakenMultiplier((UnityEngine.Object)compB);
                if (Mathf.Abs(viaOther - 1.3f) > 0.001f)
                    failures.Add($"mark applied via one component must read 1.3x via a sibling component of the same GameObject (got {viaOther:F3}) — per-GameObject keying broken");
                float scaled = CombatMark.ScaleDamage((UnityEngine.Object)compB, 100f);
                if (Mathf.Abs(scaled - 130f) > 0.1f)
                    failures.Add($"ScaleDamage(100) on a 1.3x-marked foe must be 130 (got {scaled:F1})");
                var unmarked = new GameObject("CombatMarkUnmarked");
                try
                {
                    float baseline = CombatMark.DamageTakenMultiplier((UnityEngine.Object)unmarked.transform);
                    if (Mathf.Abs(baseline - 1f) > 0.001f)
                        failures.Add($"unmarked foe must read 1.0x (got {baseline:F3})");
                }
                finally { UnityEngine.Object.DestroyImmediate(unmarked); }
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }

            string enemy = File.ReadAllText("Assets/_Modules/Village/Enemies/Enemy.cs");
            if (!enemy.Contains("CombatMark.ScaleDamage"))
                failures.Add("Enemy.TakeDamageFrom must scale by CombatMark");

            string ab = File.ReadAllText("Assets/_Modules/Village/Hero/HeroAbilities.cs");
            if (!ab.Contains("CombatMark.Apply") || !ab.Contains("IsHuntersMark"))
                failures.Add("HeroAbilities must apply Hunter's Mark via CombatMark");

            // SINGLE-APPLICATION LAW (2026-08-15 review): Enemy.TakeDamageFrom is the ONE
            // place mark scaling happens. A caller-side CombatMark.ScaleDamage on a path that
            // funnels into it double-applies (1.2 × 1.2) now that keys resolve per-GameObject.
            foreach (var callerPath in new[]
            {
                "Assets/_Modules/Village/Hero/HeroAbilities.cs",
                "Assets/_Modules/Village/Enemies/PlayerAttackController.cs",
                "Assets/_Modules/Village/Troops/TroopController.cs",
            })
            {
                string caller = File.ReadAllText(callerPath);
                // Comments explaining the law are fine; a CALL is the violation.
                if (Regex.IsMatch(caller, @"^(?!\s*//).*CombatMark\.ScaleDamage\s*\(", RegexOptions.Multiline))
                    failures.Add(Path.GetFileName(callerPath) + " re-applies CombatMark.ScaleDamage caller-side — double-apply with Enemy.TakeDamageFrom (single-application law)");
            }
        }

        private static void GateShieldPort(List<string> failures)
        {
            string path = "Assets/_Modules/Village/Hero/EquipmentController.cs";
            string src = File.ReadAllText(path);
            if (!src.Contains("OnSceneLoadedReapplyGear") && !src.Contains("CoReapplyGearAfterSceneLoad"))
                failures.Add("EquipmentController missing WO-994 scene-load gear reapply");
            if (!src.Contains("InvalidateHeroHeightCache"))
                failures.Add("EquipmentController missing height cache invalidate (WO-994)");
            if (!src.Contains("sceneLoaded"))
                failures.Add("EquipmentController must subscribe SceneManager.sceneLoaded");
        }
    }
}
