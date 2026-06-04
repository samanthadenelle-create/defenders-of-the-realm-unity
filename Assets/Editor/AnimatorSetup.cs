// =============================================================================
// AnimatorSetup — builds the character AnimatorController assets (Editor-only).
// -----------------------------------------------------------------------------
// One static entry point the main Unity session runs (Defenders menu, or via the
// Unity -executeMethod flag):
//
//     -executeMethod DeNelle.Editor.AnimatorSetup.BuildAnimators
//
// WHAT IT DOES — implements docs/enemy-codex.md §5 ("Animation strategy"):
// the codex's key fact is that EVERY KayKit humanoid (Skeletons, Adventurers,
// every Mystery Monthly character) shares ONE skeleton — Rig_Medium — and the
// bulky bodies (Skeleton Golem, Frost Golem) share Rig_Large. So the whole
// 19-entity roster animates from a tiny set of controllers built ONCE:
//
//   • HumanoidEnemy.controller  — Rig_Medium fodder/melee/skirmisher/caster.
//   • LargeEnemy.controller     — Rig_Large heavy bodies (Brute, Frost Golem).
//   • Boss.controller           — Rig_Medium bosses (adds an extra Special state).
//   • Hero.controller           — the Keeper / village hero (Rig_Medium).
//   • Pet.controller            — the starter pets (Rig_Medium humanoid clips;
//                                 the quadruped Ice-Wolf rig is GAP-PRIMARY and
//                                 is flagged, not solved here — see the port note).
//   • Npc.controller            — idle-only village folk.
//
// Each controller carries the parameters the gameplay scripts drive —
//   Speed   (float)   — locomotion blend / idle<->move
//   Attack  (trigger) — melee / cast strike
//   Hit     (trigger) — flinch
//   Dead    (bool)    — collapse + hold on the death pose
//   Cast    (trigger, Boss + Hero only) — the Special clip
// — wired with idle/move/attack/hit/death states and transitions, and saved as
// .controller assets under Assets/Generated/Animators/.
//
// ── CLIP SOURCING — the awkward truth ────────────────────────────────────────
// The KayKit Character Animations 1.1 FBXs (Rig_Medium_*.fbx / Rig_Large_*.fbx)
// import with `clipAnimations: []` — Unity has NOT split them into named
// sub-clips, so each FBX exposes whatever AnimationClip sub-assets its take(s)
// produce. This script does NOT assume clip names: it scans every category FBX
// with AssetDatabase.LoadAllAssetRepresentationsAtPath, collects ALL
// AnimationClip sub-assets, and matches each animator state to a clip by a
// keyword search (case-insensitive "contains"). If a state finds no clip the
// state is still created (empty Motion) and the miss is logged — the controller
// is always valid; the integrator can drop a clip in later. This makes the
// script correct whether KayKit ships one-clip-per-FBX or many.
//
// ── ASSEMBLY NOTE ────────────────────────────────────────────────────────────
// Lives in the fixed DeNelle.Editor.asmdef (Editor-only; editing asmdefs is
// forbidden). DeNelle.Editor does NOT reference the gameplay asmdefs, so this
// script takes NO compile-time dependency on Enemy / DungeonHero / Pet — it only
// builds .controller assets (no gameplay type is touched here at all). Assigning
// the controllers to character prefabs is the integrator's step — see the
// summary log + docs/port-notes/animation-setup.md.
//
// IDEMPOTENT — re-running BuildAnimators() overwrites the .controller assets in
// place. Safe to repeat.
//
// This script does NOT run itself; the main Unity session triggers it.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that builds the shared character <see cref="AnimatorController"/>
    /// assets from the KayKit Character Animations 1.1 clip library, per
    /// docs/enemy-codex.md §5. Entry point: <see cref="BuildAnimators"/>.
    /// </summary>
    public static class AnimatorSetup
    {
        // ── Output ───────────────────────────────────────────────────────────
        private const string OutputDir = "Assets/Generated/Animators";

        // ── KayKit Character Animations 1.1 FBX category paths ───────────────
        // The folder name literally contains spaces + "1.1" — valid in Unity
        // asset paths. LoadAllAssetRepresentationsAtPath handles them directly.
        private const string AnimRoot =
            "Assets/Models/KayKit/KayKit Character Animations 1.1/Animations/fbx/";

        private const string RigMediumDir = AnimRoot + "Rig_Medium/";
        private const string RigLargeDir  = AnimRoot + "Rig_Large/";

        // The Rig_Medium category FBXs (the humanoid clip library).
        private static readonly string[] RigMediumFbx =
        {
            RigMediumDir + "Rig_Medium_General.fbx",
            RigMediumDir + "Rig_Medium_MovementBasic.fbx",
            RigMediumDir + "Rig_Medium_MovementAdvanced.fbx",
            RigMediumDir + "Rig_Medium_CombatMelee.fbx",
            RigMediumDir + "Rig_Medium_CombatRanged.fbx",
            RigMediumDir + "Rig_Medium_Special.fbx",
            RigMediumDir + "Rig_Medium_Simulation.fbx",
            RigMediumDir + "Rig_Medium_Tools.fbx",
        };

        // The Rig_Large category FBXs (the bulky-body clip library).
        private static readonly string[] RigLargeFbx =
        {
            RigLargeDir + "Rig_Large_General.fbx",
            RigLargeDir + "Rig_Large_MovementBasic.fbx",
            RigLargeDir + "Rig_Large_MovementAdvanced.fbx",
            RigLargeDir + "Rig_Large_CombatMelee.fbx",
            RigLargeDir + "Rig_Large_Special.fbx",
            RigLargeDir + "Rig_Large_Simulation.fbx",
        };

        // ── Animator parameter names — MUST match the gameplay-script strings ─
        // Enemy.cs / DungeonHero.cs / Pet.cs drive the Animator by these exact
        // names. Changing one here means changing it there too.
        private const string PSpeed  = "Speed";   // float  — locomotion
        private const string PAttack = "Attack";  // trigger — melee / cast strike
        private const string PHit    = "Hit";     // trigger — flinch
        private const string PDead   = "Dead";    // bool   — death (collapse + hold)
        private const string PCast   = "Cast";    // trigger — Special clip (Boss/Hero)

        // Locomotion threshold — Speed above this counts as "moving".
        private const float MoveThreshold = 0.1f;

        // WO-217: enemy attack snappiness — modest (Generic rigs, keep conservative).
        // Speed multiplier on the Attack/Cast state + an earlier return-to-idle so
        // the recovery window is shorter. Kept gentle vs the hero (1.3) on purpose.
        private const float EnemyAttackSpeed = 1.15f;   // 1.1–1.2 band
        private const float EnemyAttackExitTime = 0.75f; // return earlier than 0.85

        // Running tallies for the summary log.
        private static int _controllerCount;
        private static int _missingClipCount;
        private static readonly List<string> _missingClips = new List<string>();
        private static readonly List<string> _builtControllers = new List<string>();

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds every character <see cref="AnimatorController"/> from the KayKit
        /// Character Animations 1.1 clip library. Runnable via
        /// <c>-executeMethod DeNelle.Editor.AnimatorSetup.BuildAnimators</c>.
        /// Idempotent — overwrites the .controller assets in place.
        /// </summary>
        [MenuItem("Defenders/Animation/Build Animator Controllers")]
        public static void BuildAnimators()
        {
            _controllerCount = 0;
            _missingClipCount = 0;
            _missingClips.Clear();
            _builtControllers.Clear();

            EnsureFolder(OutputDir);

            // ── Gather the shared clip libraries (one scan per rig) ──────────
            var mediumLib = LoadClipLibrary(RigMediumFbx, "Rig_Medium");
            var largeLib  = LoadClipLibrary(RigLargeFbx,  "Rig_Large");

            if (mediumLib.Count == 0)
            {
                Debug.LogError(
                    "[AnimatorSetup] No Rig_Medium animation clips found under " +
                    AnimRoot + " — the KayKit Character Animations 1.1 pack is " +
                    "missing or unimported. Controllers will be built with EMPTY " +
                    "states (still valid assets); re-run after the pack imports.");
            }

            // ── Build one controller per archetype (codex §5.1 / §5.2) ───────
            // Walker / Warrior / Rogue / Caster — the Rig_Medium humanoid roster.
            BuildHumanoidController(
                "HumanoidEnemy", mediumLib, withCast: false,
                "Standard Rig_Medium enemy — Hollow Walker / Warrior / Rogue / " +
                "Caster, the Wildlands Caveman / Tiefling. Idle/move/attack/hit/death.");

            // Skeleton Golem / Frost Golem — the Rig_Large heavy bodies.
            BuildHumanoidController(
                "LargeEnemy", largeLib.Count > 0 ? largeLib : mediumLib,
                withCast: false,
                "Heavy Rig_Large body — Hollow Brute / Bone-Golem, Frost Golem. " +
                "Falls back to the Rig_Medium library if Rig_Large clips are absent.");

            // Bosses — Rig_Medium body + an extra Cast/Special state (codex §4).
            BuildHumanoidController(
                "Boss", mediumLib, withCast: true,
                "Humanoid boss — Necromancer / Apprentice / Vault Keeper / " +
                "Inn-Keeper / Watcher / Wolfwarden Ph.1. Adds a Cast Special state.");

            // The Keeper / village hero — Rig_Medium + a Cast state for abilities.
            BuildHumanoidController(
                "Hero", mediumLib, withCast: true,
                "The Keeper — village hero + dungeon hero. Cast state fires the " +
                "Q/W/E/R ability animation.");

            // Starter pets — Rig_Medium humanoid clips. The quadruped Ice-Wolf rig
            // is codex GAP-PRIMARY and is NOT solved here (see the port note).
            BuildHumanoidController(
                "Pet", mediumLib, withCast: false,
                "Starter pet (Aether Sprite / Flame Pup). NOTE: the Ice Wolf is a " +
                "quadruped — codex GAP-PRIMARY — and needs its own rig + clips.");

            // Village NPC folk — an idle-only controller (no combat states).
            BuildNpcController("Npc", mediumLib,
                "Village NPC / townsfolk — idle loop only, no combat states.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Summary ──────────────────────────────────────────────────────
            string summary =
                $"[AnimatorSetup] Built {_controllerCount} controller(s) under " +
                $"{OutputDir}:\n  - " + string.Join("\n  - ", _builtControllers);

            if (_missingClipCount > 0)
            {
                summary +=
                    $"\n[AnimatorSetup] {_missingClipCount} state(s) found NO matching " +
                    "clip and were left empty (still valid):\n  - " +
                    string.Join("\n  - ", _missingClips) +
                    "\nThis is expected if the KayKit clip take-names differ from " +
                    "the keyword search; the integrator can assign clips by hand.";
            }

            summary +=
                "\n[AnimatorSetup] INTEGRATOR STEP: assign these controllers to the " +
                "character prefabs' Animator components (see " +
                "docs/port-notes/animation-setup.md). The gameplay scripts " +
                "(Enemy / DungeonHero / Pet) auto-find the Animator at runtime.";

            Debug.Log(summary);
        }

        // =====================================================================
        //  Clip library — scan the Character Animations FBXs
        // =====================================================================

        /// <summary>
        /// Loads every <see cref="AnimationClip"/> sub-asset out of the given
        /// category FBXs into one keyword-searchable library. The FBXs import
        /// with <c>clipAnimations: []</c> (no manual clip split), so this picks
        /// up whatever clips Unity auto-generated, by name.
        /// </summary>
        private static List<AnimationClip> LoadClipLibrary(string[] fbxPaths, string rigLabel)
        {
            var clips = new List<AnimationClip>();
            foreach (string path in fbxPaths)
            {
                // LoadAllAssetRepresentationsAtPath returns the sub-assets (the
                // clips + meshes) WITHOUT the main model GameObject.
                var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                if (reps == null || reps.Length == 0)
                {
                    // Not fatal — the FBX may be missing or still importing.
                    Debug.LogWarning(
                        $"[AnimatorSetup] No sub-assets at '{path}' — skipped " +
                        $"(its clips will be absent from the {rigLabel} library).");
                    continue;
                }

                foreach (var rep in reps)
                {
                    if (rep is AnimationClip clip && !clip.name.StartsWith("__preview"))
                        clips.Add(clip);
                }
            }

            Debug.Log(
                $"[AnimatorSetup] {rigLabel} clip library: {clips.Count} clip(s) — " +
                (clips.Count > 0
                    ? string.Join(", ", clips.Select(c => c.name).Distinct().Take(40))
                    : "(none)"));
            return clips;
        }

        /// <summary>
        /// Returns the first clip in <paramref name="library"/> whose name
        /// contains ANY of <paramref name="keywords"/> (case-insensitive). Earlier
        /// keywords win, so callers list the best match first. Returns null when
        /// nothing matches — the caller then builds an empty state and logs it.
        /// </summary>
        private static AnimationClip FindClip(
            List<AnimationClip> library, params string[] keywords)
        {
            if (library == null || library.Count == 0) return null;

            foreach (string kw in keywords)
            {
                string needle = kw.ToLowerInvariant();
                foreach (var clip in library)
                {
                    if (clip != null &&
                        clip.name.ToLowerInvariant().Contains(needle))
                        return clip;
                }
            }
            return null;
        }

        // =====================================================================
        //  Controller builders
        // =====================================================================

        /// <summary>
        /// Builds a full combat controller — Idle, Move, Attack, Hit, Death (plus
        /// an optional Cast/Special) — wired to the clip library by keyword.
        /// </summary>
        private static void BuildHumanoidController(
            string assetName, List<AnimationClip> library, bool withCast, string note)
        {
            string path = $"{OutputDir}/{assetName}.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // ── Parameters — the exact names the gameplay scripts drive ──────
            controller.AddParameter(PSpeed,  AnimatorControllerParameterType.Float);
            controller.AddParameter(PAttack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PHit,    AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PDead,   AnimatorControllerParameterType.Bool);
            if (withCast)
                controller.AddParameter(PCast, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            // ── Clip resolution (keyword search over the library) ────────────
            AnimationClip idleClip   = FindClip(library, "idle", "rest", "breathing");
            AnimationClip moveClip   = FindClip(library, "walk", "run", "move", "jog");
            AnimationClip attackClip = FindClip(library,
                "1h_melee_attack", "melee_attack", "attack", "slash", "swing", "shoot");
            AnimationClip hitClip    = FindClip(library,
                "hit", "hitrecieve", "recieve_hit", "flinch", "damage", "impact");
            AnimationClip deathClip  = FindClip(library, "death", "die", "dead");
            AnimationClip castClip   = withCast
                ? FindClip(library, "cast", "channel", "spellcast", "special", "interact")
                : null;

            // ── States ───────────────────────────────────────────────────────
            // Idle is the default — a static or freshly-spawned character stands.
            var idle = sm.AddState("Idle");
            idle.motion = idleClip;
            ReportClip(assetName, "Idle", idleClip);
            sm.defaultState = idle;

            var move = sm.AddState("Move");
            move.motion = moveClip;
            ReportClip(assetName, "Move", moveClip);

            var attack = sm.AddState("Attack");
            attack.motion = attackClip;
            attack.speed  = EnemyAttackSpeed;   // WO-217: snappier enemy swing
            ReportClip(assetName, "Attack", attackClip);

            var hit = sm.AddState("Hit");
            hit.motion = hitClip;
            ReportClip(assetName, "Hit", hitClip);

            var death = sm.AddState("Death");
            death.motion = deathClip;
            ReportClip(assetName, "Death", deathClip);

            AnimatorState cast = null;
            if (withCast)
            {
                cast = sm.AddState("Cast");
                cast.motion = castClip;
                cast.speed  = EnemyAttackSpeed;   // WO-217: snappier enemy cast
                ReportClip(assetName, "Cast", castClip);
            }

            // ── Transitions ──────────────────────────────────────────────────
            // Idle <-> Move on the Speed float (the codex's locomotion blend).
            var idleToMove = idle.AddTransition(move);
            idleToMove.AddCondition(AnimatorConditionMode.Greater, MoveThreshold, PSpeed);
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0.12f;

            var moveToIdle = move.AddTransition(idle);
            moveToIdle.AddCondition(AnimatorConditionMode.Less, MoveThreshold, PSpeed);
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0.12f;

            // Attack — fired by the Attack trigger from Idle OR Move; plays once
            // (hasExitTime) then returns to Idle (locomotion re-resolves itself).
            AddTriggerTransition(idle, attack, PAttack);
            AddTriggerTransition(move, attack, PAttack);
            // WO-217: snappier attack recovery — return to idle earlier than the
            // shared 0.85 exit (Hit/Cast keep the default feel via AddReturnToIdle).
            AddReturnToIdle(attack, idle, EnemyAttackExitTime);

            // Hit — a flinch interrupt from Idle / Move / Attack.
            AddTriggerTransition(idle, hit, PHit);
            AddTriggerTransition(move, hit, PHit);
            AddTriggerTransition(attack, hit, PHit);
            AddReturnToIdle(hit, idle);

            if (withCast)
            {
                AddTriggerTransition(idle, cast, PCast);
                AddTriggerTransition(move, cast, PCast);
                AddReturnToIdle(cast, idle);
                AddDeathTransition(cast, death);
            }

            // Death — the Dead bool latches the collapse from ANY state; it never
            // transitions out (the character is destroyed / pooled afterward).
            AddDeathTransition(idle, death);
            AddDeathTransition(move, death);
            AddDeathTransition(attack, death);
            AddDeathTransition(hit, death);

            FinishController(assetName, path, note);
        }

        /// <summary>
        /// Builds an idle-only controller — a village NPC with no combat states.
        /// Still carries the Speed parameter so wandering NPCs can blend to a
        /// walk if the integrator wires one.
        /// </summary>
        private static void BuildNpcController(
            string assetName, List<AnimationClip> library, string note)
        {
            string path = $"{OutputDir}/{assetName}.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.AddParameter(PSpeed, AnimatorControllerParameterType.Float);

            var sm = controller.layers[0].stateMachine;

            AnimationClip idleClip = FindClip(library, "idle", "rest", "breathing");
            AnimationClip moveClip = FindClip(library, "walk", "move", "jog");

            var idle = sm.AddState("Idle");
            idle.motion = idleClip;
            ReportClip(assetName, "Idle", idleClip);
            sm.defaultState = idle;

            var move = sm.AddState("Move");
            move.motion = moveClip;
            ReportClip(assetName, "Move", moveClip);

            var idleToMove = idle.AddTransition(move);
            idleToMove.AddCondition(AnimatorConditionMode.Greater, MoveThreshold, PSpeed);
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0.15f;

            var moveToIdle = move.AddTransition(idle);
            moveToIdle.AddCondition(AnimatorConditionMode.Less, MoveThreshold, PSpeed);
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0.15f;

            FinishController(assetName, path, note);
        }

        // =====================================================================
        //  Transition helpers
        // =====================================================================

        /// <summary>
        /// Adds an interrupt transition fired by a trigger parameter — no exit
        /// time, so it cuts in the moment the trigger is set.
        /// </summary>
        private static void AddTriggerTransition(
            AnimatorState from, AnimatorState to, string trigger)
        {
            var t = from.AddTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.hasExitTime = false;
            t.duration = 0.08f;
        }

        /// <summary>
        /// Adds a transition back to Idle that fires on clip completion
        /// (hasExitTime) — used by one-shot states (Attack / Hit / Cast).
        /// </summary>
        private static void AddReturnToIdle(
            AnimatorState from, AnimatorState idle, float exitTime = 0.85f)
        {
            var t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = exitTime;    // most of the clip plays before returning
            t.duration = 0.12f;
        }

        /// <summary>
        /// Adds a transition into Death gated on the <c>Dead</c> bool — fires
        /// from any state, never returns.
        /// </summary>
        private static void AddDeathTransition(AnimatorState from, AnimatorState death)
        {
            var t = from.AddTransition(death);
            t.AddCondition(AnimatorConditionMode.If, 0f, PDead);
            t.hasExitTime = false;
            t.duration = 0.1f;
        }

        // =====================================================================
        //  Logging helpers
        // =====================================================================

        /// <summary>Logs and tallies a state whose clip search came up empty.</summary>
        private static void ReportClip(string controller, string state, AnimationClip clip)
        {
            if (clip != null) return;
            _missingClipCount++;
            _missingClips.Add($"{controller}.{state}");
        }

        /// <summary>Tallies a finished controller for the summary log.</summary>
        private static void FinishController(string assetName, string path, string note)
        {
            _controllerCount++;
            _builtControllers.Add($"{assetName}.controller — {note}");
            EditorUtility.SetDirty(
                AssetDatabase.LoadAssetAtPath<AnimatorController>(path));
        }

        // =====================================================================
        //  Folder helper
        // =====================================================================

        /// <summary>Creates <paramref name="dir"/> (and parents) if it does not exist.</summary>
        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;

            string parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
            string leaf = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
