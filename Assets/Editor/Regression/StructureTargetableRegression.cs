// =============================================================================
// StructureTargetableRegression [structure-targetable]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
// Markers: STRUCTURE_TARGETABLE_OK / STRUCTURE_TARGETABLE_FAIL.
//
// THE ORACLE FOR WO-853 "structures are targetable" (acceptance 10.7).
//
// WO-853 opened the seam that made a raid a DEMOLITION instead of only a fight:
// WallSegment, Gate and DefenseTower now implement BOTH combat contracts -
// IDamageableStructure (the seam ENEMIES acquire through) and IDamageable (the
// seam the PLAYER, the hero's abilities, troops and pets search through). Four
// invariants make that safe rather than catastrophic, and each of them is one
// careless "simplification" away from silently reverting. This suite pins them.
//
//   CASE 1 [faction-derived]   Every production IDamageable implementor answers
//       Faction from a DERIVED, read-only expression - never a serialized field.
//       Faction is the ONLY thing standing between a troop and the player's own
//       perimeter: WO-853 widened TroopController / PlayerAttackController /
//       HeroAbilities / HeroTargetIndicator target masks to include layer
//       Structure, and the ONLY reason that is safe is that a player-owned wall
//       reports Friendly and is rejected at the sweep. A [SerializeField]
//       CombatFaction would let a prefab or a stale baked scene LIE about
//       allegiance - and a wall lying "Hostile" means the player's own troops
//       demolish their own town. Verified by REFLECTION over every loaded
//       DeNelle.* runtime type implementing IDamageable (so a NEW implementor is
//       caught the day it is added, not the day someone updates a list), plus a
//       source-lint of each implementor's own file - the house style used by
//       UiMvvmConformanceRegression.
//
//   CASE 2 [faction-flips]     The derivation is not just read-only, it actually
//       RESPONDS: with SceneOwnership.IsEnemyOwned flipped, a WallSegment and a
//       Gate really do report Hostile, and Friendly again when it is flipped
//       back. A hardcoded `=> Friendly` would satisfy Case 1 and make every raid
//       wall permanently un-attackable; a hardcoded `=> Hostile` would satisfy
//       Case 1 and hand the player's own troops their own town. The flag is
//       saved and RESTORED in a finally - this suite must never leak global
//       ownership state into the suites that run after it.
//
//   CASE 3 [wall-layer]        A wall still lands on layer "Structure" (WO-853
//       section 4, the constraint that shaped the whole ticket). RaidSpire and
//       BreakableContainer make themselves findable by rewriting their layer to
//       "Enemy"; a wall MUST NOT copy that trick, because layer Structure IS the
//       line-of-sight blocker mask that DefenseTower.BlockedByWall, TowerCombat,
//       ArcaneTower, PlayerAttackController and HeroTargetIndicator all linecast
//       against. Relayering walls to Enemy would make towers shoot straight
//       through the perimeter again - regressing the fix shipped in 2cb3c40d.
//       Asserted BEHAVIOURALLY: a WallSegment driven through its real Configure()
//       is READ BACK off gameObject.layer, plus a lint that WallSegment.cs never
//       moves itself to the Enemy layer.
//
//   CASE 4 [wall-collapse]     A WallSegment driven to 100 damage has its SOLID
//       colliders disabled - the observable effect, driven through BOTH damage
//       seams (the enemy ApplyContactDamage entry and the player IDamageable
//       TakeDamage entry), because a collapse that only fires on one seam is a
//       wall the hero cannot actually raze. Until the collider drops, a razed
//       section still stands: it still blocks tower line-of-sight and still
//       blocks pathing, so steps 1-4 of the ticket buy nothing the player can
//       see. (This criterion originally read "Collapsed has at least one
//       subscriber" and was CORRECTED in WO-853 section 10.7 on 2026-08-03: the
//       collapse is the component's OWN lifecycle - like Gate.ApplyForceFieldState
//       - so the event correctly has zero external subscribers and always will.
//       Asserting a subscriber count would have forced a fake listener into
//       existence purely to satisfy a gate. We assert the effect instead.)
//
//   CASE 5 [tower-seam-split]  THE HIGHEST-VALUE ASSERTION HERE. DefenseTower
//       answers IsAlive TWO DIFFERENT WAYS on purpose: the public / IDamageable
//       member is LIVENESS ONLY (this is what lets the hero and troops find and
//       kill an EnemyOwned garrison turret), while the EXPLICIT
//       IDamageableStructure.IsAlive additionally requires PlayerOwned. Both are
//       pinned, from behaviour AND from the interface map. If someone "cleans
//       this up" into one property, one of two things breaks:
//         * collapse to liveness-only  -> hostile mobs ACQUIRE their own garrison
//           turret through the enemy seam, which ApplyContactDamage then refuses
//           to damage, so they path to it and flail at an invulnerable target
//           forever;
//         * collapse to liveness+ownership -> an EnemyOwned turret reads as
//           permanently dead and the player can never attack it again (the
//           original WO-853 defect).
//       The paired ApplyContactDamage ownership gate is pinned in the same case,
//       because keeping only ONE half of the pair strands mobs either way.
//
// WHAT THIS SUITE DELIBERATELY DOES NOT ASSERT (not decidable in EditMode):
//   * The collapse PRESENTATION (the accelerating sink + the _Collapse shader
//     ramp) is a coroutine, and WallSegment.Collapse early-outs on
//     !Application.isPlaying because an EditMode-driven wall on a bare
//     GameObject cannot run one. The collider drop and the Collapsed event DO
//     fire in edit mode, so those - and only those - are asserted here. The
//     visible tell stays a fleet / felt check.
//   * Whether a razed raid wall opens a WALKABLE lane. Per WO-853 section 11 that
//     is deferred: raid wall panels carry no carving NavMeshObstacle, so pathing
//     through the gap needs a re-bake. This suite asserts only that any obstacle
//     the segment DOES own is dropped.
//   * Whether an enemy turret is reachable in a BAKED raid arena. Also section
//     11: RaidBaseGenerator.ArmTower leaves turrets on layer Default, so the
//     widened masks still will not return one until that is fixed. That is a
//     scene-content fact, not a contract fact.
//
// OVERLAP WITH EXISTING SUITES - checked before writing, no duplication:
//   * TowerWallLosRegression [tower-wall-los] source-lints that WallSegment.cs
//     CONTAINS the token NameToLayer("Structure") and CONTAINS some `.layer =`
//     assignment. It does not prove the two are connected: a future edit that
//     added `gameObject.layer = NameToLayer("Enemy")` while leaving the
//     Structure token behind for a mask would still pass it. Case 3 reads the
//     layer back off a real configured WallSegment and lints the Enemy-relayer
//     specifically, which is the failure mode section 4 actually fears.
//   * DefenseTargetableRegression [def-target] proves the catalog's defense
//     entries implement IDamageableStructure (the ENEMY seam). It says nothing
//     about IDamageable, Faction, the IsAlive split, or the collapse.
//
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.StructureTargetableRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Combat;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class StructureTargetableRegression
    {
        // ---------------------------------------------------------------------
        //  The production IDamageable census. Reflection DISCOVERS implementors;
        //  this list exists so a DISAPPEARANCE is a failure too. WallSegment /
        //  Gate / DefenseTower dropping IDamageable is precisely the WO-853
        //  regression (structures become un-findable scenery again), and a
        //  discovery-only oracle would report "0 problems" for it.
        // ---------------------------------------------------------------------
        private static readonly string[] ExpectedImplementors =
        {
            "EnemyDamageable",     // the Enemy adapter - the original implementor
            "DragonBoss",          // apex boss, flying layer
            "BreakableContainer",  // world props
            "RaidSpire",           // the dual-contract precedent WO-853 extended
            "WallSegment",         // NEW in WO-853
            "Gate",                // NEW in WO-853
            "DefenseTower",        // NEW in WO-853
        };

        private const string ModulesRoot = "_Modules";

        // A serialized-looking CombatFaction member in source. Two shapes: an
        // attribute-decorated field, and a bare public field (Unity serializes
        // public fields with no attribute at all - the easiest one to slip in).
        private static readonly Regex SerializedFactionAttr = new Regex(
            @"\[[^\]\r\n]*SerializeField[^\]\r\n]*\][^;\r\n]*\bCombatFaction\b", RegexOptions.Compiled);

        private static readonly Regex FieldSerializedFactionProp = new Regex(
            @"\[\s*field\s*:[^\]\r\n]*SerializeField[^\]\r\n]*\][^;\r\n]*\bCombatFaction\b", RegexOptions.Compiled);

        // `public CombatFaction Foo;` or `public CombatFaction Foo = ...;` - a FIELD.
        // A property is excluded by requiring the terminator to be ; or = (a property
        // continues with an arrow or an opening brace, and the arrow is excluded by
        // requiring the character after '=' not to be '>').
        private static readonly Regex BarePublicFactionField = new Regex(
            @"public\s+CombatFaction\s+\w+\s*(;|=[^>])", RegexOptions.Compiled);

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("STRUCTURE_TARGETABLE_OK - " + reason);
            else Debug.LogError("STRUCTURE_TARGETABLE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var created = new List<GameObject>();

            try
            {
                Case(failures, "faction-derived", () => Case1_FactionIsDerived(failures, notes));
                Case(failures, "faction-flips", () => Case2_FactionFollowsSceneOwnership(failures, notes, created));
                Case(failures, "wall-layer", () => Case3_WallStaysOnStructureLayer(failures, notes, created));
                Case(failures, "wall-collapse", () => Case4_WallCollapseDropsColliders(failures, notes, created));
                Case(failures, "tower-seam-split", () => Case5_TowerSeamSplit(failures, notes, created));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                for (int i = 0; i < created.Count; i++)
                    if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "STRUCTURE TARGETABLE OK - every production IDamageable derives Faction (no serialized " +
                         "field anywhere in the census), wall/gate allegiance really follows SceneOwnership, walls " +
                         "still land on layer Structure so towers cannot shoot through them, a wall driven to 100 " +
                         "damage drops its solid colliders through BOTH damage seams, and DefenseTower still answers " +
                         "IsAlive two different ways (player seam = liveness, enemy seam = liveness AND PlayerOwned) " +
                         "with the paired ApplyContactDamage ownership gate intact" + noteStr;
                return true;
            }

            reason = "structure-targetable FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - Faction is DERIVED, never serialized.
        // =====================================================================
        private static void Case1_FactionIsDerived(List<string> failures, List<string> notes)
        {
            var implementors = DiscoverImplementors();
            if (implementors.Count == 0)
            {
                failures.Add("[faction-derived] reflection found ZERO production types implementing IDamageable. " +
                             "Either the DeNelle.Village assembly is not loaded or the combat contract was renamed - " +
                             "either way nothing in the game can be targeted and this oracle cannot decide anything.");
                return;
            }

            var sourceByType = IndexModuleSources();
            var found = new HashSet<string>(StringComparer.Ordinal);

            foreach (var t in implementors)
            {
                found.Add(t.Name);

                // -- the derived-expression contract, from metadata ------------
                PropertyInfo prop = FindFactionProperty(t);
                if (prop == null)
                {
                    failures.Add("[faction-derived] " + t.Name + " implements IDamageable but exposes no Faction " +
                                 "PROPERTY - the contract must be answered by a derived expression (ownership -> " +
                                 "faction), never by state something else can write.");
                }
                else if (prop.CanWrite)
                {
                    failures.Add("[faction-derived] " + t.Name + ".Faction has a SETTER. Faction must be DERIVED " +
                                 "from ownership (DefenseTower: Allegiance; WallSegment/Gate: SceneOwnership.IsEnemyOwned), " +
                                 "so nothing can ever assign an allegiance that disagrees with who owns the thing. " +
                                 "Remove the setter and compute the value.");
                }

                // -- no serialized CombatFaction backing, from metadata --------
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.FieldType != typeof(CombatFaction)) continue;
                    if (Attribute.IsDefined(f, typeof(NonSerializedAttribute))) continue;
                    bool serialized = f.IsPublic || Attribute.IsDefined(f, typeof(SerializeField));
                    if (!serialized) continue;

                    failures.Add("[faction-derived] " + t.Name + " carries a SERIALIZED CombatFaction field '" +
                                 f.Name + "'. Faction must never be authored data: WO-853 widened the hero/troop " +
                                 "target masks to include layer Structure, and the ONLY thing that then keeps a " +
                                 "deployed troop off the player's own perimeter is that a player-owned structure " +
                                 "DERIVES Friendly. A serialized field lets a prefab or a stale baked scene claim " +
                                 "Hostile - and the player's own troops demolish their own town. Derive it from " +
                                 "ownership instead.");
                }

                // -- the source-lint half (house style: UiMvvmConformanceRegression) --
                string path;
                if (!sourceByType.TryGetValue(t.Name, out path))
                {
                    notes.Add(t.Name + ": no <TypeName>.cs found under Assets/" + ModulesRoot +
                              " (declared in a differently-named file?) - metadata check stands, source-lint skipped");
                    continue;
                }

                string src = ReadOrEmpty(path);
                if (src.Length == 0)
                {
                    notes.Add(t.Name + ": source at " + path + " read empty - source-lint skipped");
                    continue;
                }

                if (SerializedFactionAttr.IsMatch(src) || FieldSerializedFactionProp.IsMatch(src))
                    failures.Add("[faction-derived] " + Path.GetFileName(path) + " declares a [SerializeField]-adjacent " +
                                 "CombatFaction member. Faction is DERIVED from ownership by contract - authoring it " +
                                 "lets a scene lie about allegiance and turns the player's own walls into troop targets.");

                if (BarePublicFactionField.IsMatch(src))
                    failures.Add("[faction-derived] " + Path.GetFileName(path) + " declares a bare PUBLIC CombatFaction " +
                                 "FIELD. Unity serializes public fields with no attribute at all, so this is a serialized " +
                                 "faction by another name - derive it from ownership instead.");
            }

            // -- the census ratchet: a DISAPPEARANCE is the WO-853 regression --
            foreach (var expected in ExpectedImplementors)
            {
                if (found.Contains(expected)) continue;
                failures.Add("[faction-derived] '" + expected + "' no longer implements IDamageable. That interface is " +
                             "the seam the PLAYER, the hero's abilities, troops and pets SEARCH through " +
                             "(GetComponentInParent<IDamageable>() + a Faction != Hostile reject). A structure carrying " +
                             "only IDamageableStructure can be hit by an enemy but can never be FOUND by anything that " +
                             "targets - which is the exact WO-853 defect: nothing in the game could damage a wall, gate " +
                             "or enemy tower, so a raid was a fight and never a demolition. Restore the dual contract.");
            }

            foreach (var t in implementors)
            {
                if (Array.IndexOf(ExpectedImplementors, t.Name) >= 0) continue;
                notes.Add("new IDamageable implementor discovered and CHECKED: " + t.Name +
                          " (add it to ExpectedImplementors so its removal is caught too)");
            }
        }

        // =====================================================================
        //  CASE 2 - the derivation actually responds to ownership.
        // =====================================================================
        private static void Case2_FactionFollowsSceneOwnership(List<string> failures, List<string> notes, List<GameObject> created)
        {
            var setter = typeof(SceneOwnership).GetMethod("SetEnemyOwned",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (setter == null)
            {
                notes.Add("SceneOwnership.SetEnemyOwned not found (renamed?) - the wall/gate faction FLIP could not be " +
                          "driven; Case 1 still pins that Faction is derived and read-only");
                return;
            }

            bool original = SceneOwnership.IsEnemyOwned;

            var wallGo = NewGo("StructTargetable_FactionWall", created);
            var wall = wallGo.AddComponent<WallSegment>();
            var gateGo = NewGo("StructTargetable_FactionGate", created);
            var gate = gateGo.AddComponent<Gate>();

            try
            {
                setter.Invoke(null, new object[] { true });
                if (!SceneOwnership.IsEnemyOwned)
                {
                    notes.Add(DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                        "flip check", "SceneOwnership.SetEnemyOwned(true) did not take effect"));
                    return;
                }

                if (((IDamageable)wall).Faction != CombatFaction.Hostile)
                    failures.Add("[faction-flips] in an ENEMY-owned scene a WallSegment reported " +
                                 ((IDamageable)wall).Faction + ", not Hostile. Every player-side sweep rejects " +
                                 "Faction != Hostile, so a raid wall that does not report Hostile can never be " +
                                 "acquired by the hero or a deployed troop - the raid is a fight again, not a demolition.");

                if (((IDamageable)gate).Faction != CombatFaction.Hostile)
                    failures.Add("[faction-flips] in an ENEMY-owned scene a Gate reported " +
                                 ((IDamageable)gate).Faction + ", not Hostile - it would be unattackable by the player seam.");

                setter.Invoke(null, new object[] { false });

                if (((IDamageable)wall).Faction != CombatFaction.Friendly)
                    failures.Add("[faction-flips] in a PLAYER-owned scene a WallSegment reported " +
                                 ((IDamageable)wall).Faction + ", not Friendly. This is the guard rail on the WO-853 " +
                                 "mask widening: hero/troop target masks now include layer Structure, so the ONLY thing " +
                                 "keeping a deployed troop from chewing through the player's own perimeter is that the " +
                                 "player's own wall reads Friendly and is rejected at the sweep.");

                if (((IDamageable)gate).Faction != CombatFaction.Friendly)
                    failures.Add("[faction-flips] in a PLAYER-owned scene a Gate reported " +
                                 ((IDamageable)gate).Faction + ", not Friendly - the player's own troops would attack " +
                                 "their own gate.");
            }
            finally
            {
                // NEVER leak global ownership state into the suites that run after this one.
                try { setter.Invoke(null, new object[] { original }); }
                catch (Exception ex) { notes.Add("SceneOwnership restore threw " + ex.GetType().Name + " - later suites may see a flipped flag"); }
            }
        }

        // =====================================================================
        //  CASE 3 - walls stay on layer "Structure" (WO-853 section 4).
        // =====================================================================
        private static void Case3_WallStaysOnStructureLayer(List<string> failures, List<string> notes, List<GameObject> created)
        {
            int structureLayer = LayerMask.NameToLayer("Structure");
            if (structureLayer < 0)
            {
                failures.Add("[wall-layer] the project declares no layer named \"Structure\". That layer IS the " +
                             "line-of-sight blocker mask every tower linecasts against (DefenseTower.BlockedByWall, " +
                             "TowerCombat, ArcaneTower, PlayerAttackController, HeroTargetIndicator) - without it every " +
                             "tower shoots straight through the perimeter. Restore it in ProjectSettings/TagManager.asset.");
                return;
            }

            var go = NewGo("StructTargetable_LayerWall", created);
            var wall = go.AddComponent<WallSegment>();

            // The REAL wiring path VillageController uses right after instantiation.
            var data = new WallSegmentData { Id = "wall-oracle", Index = 0, X = 0f, Z = 0f, Rot = 0f, Length = 4f, Corner = false };
            wall.Configure(data, 3f);

            if (go.layer != structureLayer)
                failures.Add("[wall-layer] a Configure()'d WallSegment landed on layer " + go.layer + " ('" +
                             LayerMask.LayerToName(go.layer) + "'), not " + structureLayer + " ('Structure'). WO-853 " +
                             "section 4: RaidSpire and BreakableContainer make themselves findable by rewriting their " +
                             "layer to \"Enemy\", and a wall MUST NOT copy that trick - the target masks were widened to " +
                             "include Structure instead, exactly so walls could stay on the line-of-sight blocker layer. " +
                             "Moving a wall off Structure makes towers shoot through walls again (regressing 2cb3c40d).");

            if (go.GetComponent<Collider>() == null)
                failures.Add("[wall-layer] Configure() built no collider on the wall. The layer only matters on a " +
                             "collider: a layer-tagged object with nothing for a linecast to hit blocks no shot and " +
                             "stops no enemy.");

            // The relayer lint - the specific edit section 4 fears.
            string wallSrc = ReadOrEmpty(ModulePath("Village/Walls/WallSegment.cs"));
            if (wallSrc.Length == 0)
            {
                notes.Add("WallSegment.cs not readable - the Enemy-relayer lint was skipped (the behavioural layer read above stands)");
            }
            else if (Regex.IsMatch(wallSrc, @"\blayer\s*=\s*[^;\r\n]*NameToLayer\(\s*""Enemy""") ||
                     Regex.IsMatch(wallSrc, @"NameToLayer\(\s*""Enemy""\s*\)[^;\r\n]*;\s*$", RegexOptions.Multiline))
            {
                failures.Add("[wall-layer] WallSegment.cs resolves the \"Enemy\" layer. That is the RaidSpire/" +
                             "BreakableContainer findability trick, and it is BANNED for walls (WO-853 section 4): a wall " +
                             "on layer Enemy is invisible to the Structure-mask line-of-sight linecasts, so every tower " +
                             "fires through the perimeter. Walls are found through the WIDENED target masks instead.");
            }
        }

        // =====================================================================
        //  CASE 4 - a wall driven to 100 damage drops its solid colliders.
        // =====================================================================
        private static void Case4_WallCollapseDropsColliders(List<string> failures, List<string> notes, List<GameObject> created)
        {
            // Both seams, because a collapse that only fires on one of them is a wall
            // the hero (or the enemy) can damage forever without ever razing it.
            DriveWallToRubble(failures, notes, created, "enemy contact seam (IDamageableStructure.ApplyContactDamage)",
                w => w.ApplyContactDamage(1000f));
            DriveWallToRubble(failures, notes, created, "player/troop seam (IDamageable.TakeDamage)",
                w => ((IDamageable)w).TakeDamage(1000f, DamageElement.None));
        }

        private static void DriveWallToRubble(List<string> failures, List<string> notes, List<GameObject> created,
                                              string seam, Action<WallSegment> deliver)
        {
            var go = NewGo("StructTargetable_CollapseWall", created);
            var wall = go.AddComponent<WallSegment>();

            // The blocker Configure() authors, plus a child solid collider (raid panels
            // carry their art colliders on children) and a trigger the collapse must LEAVE
            // ALONE - a trigger is a sensor volume, not a blocker.
            var data = new WallSegmentData { Id = "wall-oracle-collapse", Index = 1, X = 0f, Z = 0f, Rot = 0f, Length = 4f, Corner = false };
            wall.Configure(data, 3f);

            var childGo = new GameObject("StructTargetable_CollapseWall_Art");
            childGo.transform.SetParent(go.transform, false);
            var childSolid = childGo.AddComponent<SphereCollider>();
            var childTrigger = childGo.AddComponent<BoxCollider>();
            childTrigger.isTrigger = true;

            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;

            bool collapsedFired = false;
            wall.Collapsed += _ => collapsedFired = true;

            deliver(wall);

            if (!wall.IsDestroyed)
            {
                failures.Add("[wall-collapse] 1000 damage through the " + seam + " left the wall at damage " +
                             wall.Damage.ToString("0.#") + "/100 - it never reached the rubble threshold, so the " +
                             "collapse can never run. Both damage entry points must funnel into the one ApplyDamage.");
                return;
            }

            var stillBlocking = new List<string>();
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c.isTrigger) continue;
                if (c.enabled) stillBlocking.Add(c.GetType().Name + " on '" + c.gameObject.name + "'");
            }

            if (stillBlocking.Count > 0)
                failures.Add("[wall-collapse] a WallSegment at 100 damage via the " + seam + " STILL has " +
                             stillBlocking.Count + " solid collider(s) enabled (" + string.Join(", ", stillBlocking) +
                             "). A razed section that keeps its collider still stands: it still blocks the towers' " +
                             "Structure-mask line-of-sight and still blocks NavMeshAgent pathing, so the player " +
                             "destroys a wall and nothing whatsoever changes on screen or in the fight. Collapse must " +
                             "disable every solid collider in the hierarchy (triggers excluded - they are sensors, " +
                             "not blockers).");

            if (!collapsedFired)
                failures.Add("[wall-collapse] WallSegment.Collapsed did not fire on the " + seam + " at 100 damage. " +
                             "The event is the component's own lifecycle signal (it correctly has no external " +
                             "subscribers), but it must still be raised - it is the one hook a future consumer " +
                             "(scoring census, rubble swap, VFX) can attach to, and a collapse that never announces " +
                             "itself is indistinguishable from one that never happened.");

            if (wall.IsAlive)
                failures.Add("[wall-collapse] a razed WallSegment still reports IsAlive == true via the " + seam +
                             ". Both contracts read this one answer, so a rubble pile that claims to be alive stays " +
                             "on every attacker's target list forever.");

            if (wall.Hp > 0f)
                failures.Add("[wall-collapse] a razed WallSegment reports Hp " + wall.Hp.ToString("0.#") +
                             " (expected 0) - the IDamageable Hp reading must be the same single 0-100 track as " +
                             "Damage, read from the other end (MaxHp - Damage), never a second HP bucket.");

            if (obstacle != null && obstacle.enabled)
                failures.Add("[wall-collapse] the carving NavMeshObstacle on a razed WallSegment is still enabled via " +
                             "the " + seam + ". A collider alone never carved anything - dropping the obstacle is what " +
                             "hands the carved navmesh back, and without it agents still refuse to walk through the gap.");

            if (childTrigger != null && !childTrigger.enabled)
                notes.Add("collapse also disabled a TRIGGER collider (" + seam + ") - triggers are sensor volumes, not " +
                          "blockers; disabling them is not required by the contract and may silence a detection volume");

            if (childSolid == null)
                notes.Add("the child solid collider vanished during the " + seam + " collapse (destroyed rather than disabled)");

            notes.Add("collapse PRESENTATION (the sink + the _Collapse shader ramp) is a coroutine and is skipped " +
                      "outside play mode by design - only the collider/obstacle drop and the Collapsed event are " +
                      "EditMode-decidable; the visible tell stays a felt check (" + seam + ")");
        }

        // =====================================================================
        //  CASE 5 - the two IsAlive answers are deliberately different.
        // =====================================================================
        private static void Case5_TowerSeamSplit(List<string> failures, List<string> notes, List<GameObject> created)
        {
            // ---- ENEMY-OWNED: reachable by the player seam, invisible to the enemy seam ----
            var enemyGo = NewGo("StructTargetable_EnemyTower", created);
            var enemyTower = enemyGo.AddComponent<DefenseTower>();
            enemyTower.Allegiance = TowerAllegiance.EnemyOwned;

            IDamageable enemyPlayerSeam = enemyTower;
            IDamageableStructure enemyStructureSeam = enemyTower;

            if (enemyPlayerSeam.Faction != CombatFaction.Hostile)
                failures.Add("[tower-seam-split] an EnemyOwned DefenseTower reports Faction " + enemyPlayerSeam.Faction +
                             ", not Hostile. Every player-side sweep rejects Faction != Hostile, so a garrison turret " +
                             "that does not report Hostile is scenery the hero and troops can never acquire.");

            if (!enemyPlayerSeam.IsAlive)
                failures.Add("[tower-seam-split] an EnemyOwned DefenseTower reports IsAlive == FALSE on the PLAYER seam " +
                             "(IDamageable). That is the original WO-853 defect: the single IsAlive also required " +
                             "Allegiance == PlayerOwned, so an enemy garrison turret read as permanently dead and no " +
                             "hero or troop attack could ever acquire it. The public/IDamageable member must be " +
                             "LIVENESS ONLY: Hp > 0 && !broken, whatever the allegiance.");

            if (enemyStructureSeam.IsAlive)
                failures.Add("[tower-seam-split] an EnemyOwned DefenseTower reports IsAlive == TRUE on the ENEMY seam " +
                             "(the explicit IDamageableStructure.IsAlive). That seam must ALSO require PlayerOwned. " +
                             "Enemy.SweepForNearestStructure, Enemy.ProbeForStructureForward, DragonBoss.NearestAliveTower " +
                             "and StructureBurn all acquire through it, while ApplyContactDamage still REFUSES damage to a " +
                             "non-PlayerOwned tower - so a hostile mob would path to its own garrison turret and flail at " +
                             "an invulnerable target forever. These two IsAlive answers are different ON PURPOSE " +
                             "(WO-853): do not simplify them into one property.");

            // The other half of the pair. Keeping only one of the two strands mobs either way.
            float hpBefore = enemyTower.Hp;
            enemyTower.ApplyContactDamage(5f);
            if (enemyTower.Hp < hpBefore)
                failures.Add("[tower-seam-split] ApplyContactDamage damaged an EnemyOwned DefenseTower (Hp " +
                             hpBefore.ToString("0.#") + " -> " + enemyTower.Hp.ToString("0.#") + "). That seam is driven " +
                             "by the ENEMY side and a garrison turret is their own asset - the ownership gate there is " +
                             "PAIRED with the explicit IDamageableStructure.IsAlive above. Dropping this gate makes the " +
                             "garrison demolish itself; dropping the other strands mobs on a target they cannot hurt.");

            // The player seam must really deal damage - being findable is worthless if it is invulnerable.
            hpBefore = enemyTower.Hp;
            enemyPlayerSeam.TakeDamage(5f, DamageElement.None);
            if (enemyTower.Hp >= hpBefore)
                failures.Add("[tower-seam-split] IDamageable.TakeDamage did NOT damage an EnemyOwned DefenseTower (Hp " +
                             "stayed " + hpBefore.ToString("0.#") + "). The player path must carry NO allegiance gate - " +
                             "the Faction filter at the caller already decided a PlayerOwned tower is not a target. " +
                             "Without this, an enemy turret is findable but invulnerable, which is worse than invisible.");

            // ---- PLAYER-OWNED: alive on BOTH seams, and rejected by the faction filter ----
            var friendlyGo = NewGo("StructTargetable_PlayerTower", created);
            var friendlyTower = friendlyGo.AddComponent<DefenseTower>();
            friendlyTower.Allegiance = TowerAllegiance.PlayerOwned;

            IDamageable friendlyPlayerSeam = friendlyTower;
            IDamageableStructure friendlyStructureSeam = friendlyTower;

            if (friendlyPlayerSeam.Faction != CombatFaction.Friendly)
                failures.Add("[tower-seam-split] a PlayerOwned DefenseTower reports Faction " + friendlyPlayerSeam.Faction +
                             ", not Friendly. Friendly is what makes the widened Structure-layer target masks safe: it is " +
                             "the reject that stops the hero and the player's own troops from attacking their own defences.");

            if (!friendlyPlayerSeam.IsAlive || !friendlyStructureSeam.IsAlive)
                failures.Add("[tower-seam-split] a healthy PlayerOwned DefenseTower is not alive on both seams (player=" +
                             friendlyPlayerSeam.IsAlive + ", enemy=" + friendlyStructureSeam.IsAlive + "). A PlayerOwned " +
                             "tower must read alive on BOTH - the enemy seam is how marching Hollow Ones acquire and siege " +
                             "the player's defences at all (F8-41).");

            // ---- and the two members are genuinely DISTINCT, not one property ----
            var publicIsAlive = typeof(DefenseTower).GetProperty("IsAlive",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo publicGetter = publicIsAlive != null ? publicIsAlive.GetGetMethod(true) : null;
            MethodInfo structureGetter = null;

            try
            {
                var map = typeof(DefenseTower).GetInterfaceMap(typeof(IDamageableStructure));
                for (int i = 0; i < map.InterfaceMethods.Length; i++)
                {
                    if (map.InterfaceMethods[i].Name != "get_IsAlive") continue;
                    structureGetter = map.TargetMethods[i];
                    break;
                }
            }
            catch (Exception ex)
            {
                notes.Add("DefenseTower interface map for IDamageableStructure could not be read (" + ex.GetType().Name +
                          ") - the two IsAlive answers are still pinned behaviourally above");
            }

            if (publicGetter == null)
                failures.Add("[tower-seam-split] DefenseTower exposes no PUBLIC IsAlive property. The public member is " +
                             "the IDamageable (player/troop) answer and must stay liveness-only and publicly readable.");

            if (structureGetter != null && publicGetter != null && structureGetter == publicGetter)
                failures.Add("[tower-seam-split] DefenseTower's IDamageableStructure.IsAlive and its public IsAlive now " +
                             "resolve to the SAME member - the explicit implementation was folded away. They answer " +
                             "differently ON PURPOSE (player seam = liveness; enemy seam = liveness AND PlayerOwned), and " +
                             "collapsing them breaks one of the two sides no matter which expression survives.");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static GameObject NewGo(string name, List<GameObject> created)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        /// <summary>
        /// Every loaded PRODUCTION type implementing IDamageable. Editor and test
        /// assemblies are excluded on purpose - TowerLosLogicTests and
        /// HeroAbilityEffectTests both declare fake implementors, and a fake is
        /// allowed to be as crude as its test needs.
        /// </summary>
        private static List<Type> DiscoverImplementors()
        {
            var result = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = asm.GetName().Name;
                if (string.IsNullOrEmpty(n) || !n.StartsWith("DeNelle.", StringComparison.Ordinal)) continue;
                if (n.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;

                foreach (var t in types)
                {
                    if (t == null || !t.IsClass || t.IsAbstract) continue;
                    if (!typeof(IDamageable).IsAssignableFrom(t)) continue;
                    if (!result.Contains(t)) result.Add(t);
                }
            }
            return result;
        }

        private static PropertyInfo FindFactionProperty(Type t)
        {
            var direct = t.GetProperty("Faction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (direct != null) return direct;

            // Explicitly implemented: the property name carries the interface prefix.
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (p.Name.EndsWith(".Faction", StringComparison.Ordinal)) return p;

            return null;
        }

        /// <summary>filename-without-extension -> full path, for every .cs under Assets/_Modules.</summary>
        private static Dictionary<string, string> IndexModuleSources()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            string root = Path.Combine(Application.dataPath, ModulesRoot);
            if (!Directory.Exists(root)) return map;

            foreach (var p in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string key = Path.GetFileNameWithoutExtension(p);
                if (!map.ContainsKey(key)) map[key] = p;
            }
            return map;
        }

        private static string ModulePath(string relative)
        {
            return Path.Combine(Application.dataPath, ModulesRoot + "/" + relative);
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
        }
    }
}
