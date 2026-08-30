using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>WO-586: directional AnyState transitions must precede generic Death.</summary>
    public static class KnightDirectionalDeathRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/Resources/Heroes/KnightMocap.controller");
            if (ctrl == null) failures.Add("KnightMocap.controller is missing");
            else
            {
                bool dead = ctrl.parameters.Any(p => p.name == "Dead" && p.type == AnimatorControllerParameterType.Bool);
                bool dir = ctrl.parameters.Any(p => p.name == "DeathDir" && p.type == AnimatorControllerParameterType.Int);
                if (!dead || !dir) failures.Add("controller lacks canonical Dead(bool) / DeathDir(int) parameters");

                var transitions = ctrl.layers[0].stateMachine.anyStateTransitions;
                int fallback = Array.FindIndex(transitions, t => t.destinationState != null && t.destinationState.name == "Death");
                foreach (var name in new[] { "DeathLeft", "DeathRight", "DeathFront", "DeathBack" })
                {
                    int index = Array.FindIndex(transitions, t => t.destinationState != null && t.destinationState.name == name);
                    if (index < 0) failures.Add(name + " transition/state is missing");
                    else
                    {
                        if (transitions[index].destinationState.motion == null) failures.Add(name + " has no clip");
                        else if (transitions[index].destinationState.motion.averageDuration < 0.5f)
                            failures.Add(name + " clip is shorter than 0.5s and reads as a shake, not a death");
                        if (fallback >= 0 && index > fallback)
                            failures.Add(name + " is ordered after unconditional Death and can never win");
                    }
                }
                if (fallback < 0) failures.Add("generic Death fallback is missing");
                else if (transitions[fallback].destinationState.motion == null)
                    failures.Add("generic Death fallback has no clip");
                else if (transitions[fallback].destinationState.motion.averageDuration < 0.5f)
                    failures.Add("generic Death fallback clip is shorter than 0.5s and reads as a shake, not a death");
            }

            // DEATH-SHAKE (owner felt-report 2026-08-30, build 2026.08.30.348154 "the knight dying is
            // still shaking"). AnyState transitions are re-evaluated every frame from EVERY state and
            // canTransitionToSelf only suppresses the destination that IS the current state. Dead and
            // DeathDir are LATCHED for the whole death window, so an UNCONDITIONED generic Death
            // fallback and a directional death transition were BOTH permanently satisfiable: the base
            // layer ping-ponged between them every 0.06s, each clip restarting at frame 0 — a shake,
            // and no death sequence. The invariant that fixes it: at most ONE death transition can be
            // satisfiable for any given DeathDir, i.e. the fallback carries a DeathDir != N guard for
            // every directional death state that exists. Checked on EVERY hero controller — all five
            // shipped the same ambiguity, the ordering only changed which clip you saw first.
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets/Resources/Heroes" }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var hero = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (hero == null || hero.layers == null || hero.layers.Length == 0) continue;
                var any = hero.layers[0].stateMachine.anyStateTransitions;
                if (any == null) continue;

                var directional = new List<int>();
                AnimatorStateTransition fallbackTransition = null;
                foreach (var t in any)
                {
                    if (t == null || t.destinationState == null) continue;
                    if (!t.destinationState.name.StartsWith("Death", StringComparison.Ordinal)) continue;
                    int dir = int.MinValue;
                    foreach (var c in t.conditions)
                        if (c.parameter == "DeathDir" && c.mode == AnimatorConditionMode.Equals)
                            dir = Mathf.RoundToInt(c.threshold);
                    if (dir != int.MinValue) directional.Add(dir);
                    else if (t.destinationState.name == "Death") fallbackTransition = t;
                }
                if (fallbackTransition == null || directional.Count == 0) continue;

                foreach (int dir in directional)
                {
                    bool guarded = false;
                    foreach (var c in fallbackTransition.conditions)
                        if (c.parameter == "DeathDir" && c.mode == AnimatorConditionMode.NotEqual &&
                            Mathf.RoundToInt(c.threshold) == dir)
                            guarded = true;
                    if (!guarded)
                        failures.Add(System.IO.Path.GetFileNameWithoutExtension(path) +
                            ": generic Death fallback lacks a 'DeathDir != " + dir + "' guard, so it and the " +
                            "directional death transition are BOTH satisfiable while Dead/DeathDir are latched " +
                            "— the base layer ping-pongs between the two death states every frame-window and the " +
                            "body shakes instead of dying");
                }
            }

            string heroHealthPath = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "Assets/_Modules/Village/Hero/HeroHealth.cs");
            string heroHealth = System.IO.File.Exists(heroHealthPath)
                ? System.IO.File.ReadAllText(heroHealthPath)
                : string.Empty;
            if (!heroHealth.Contains("anim.updateMode = AnimatorUpdateMode.UnscaledTime") ||
                !heroHealth.Contains("_deathAnimator.updateMode = _deathAnimatorPriorUpdateMode"))
                failures.Add("hero death animation is not scoped to unscaled time and restored on revive; lethal hit-stop can reduce a real death clip to a visible shake");
            if (!heroHealth.Contains("ForceKnightDeathState(anim, dir)") ||
                !heroHealth.Contains("anim.CrossFadeInFixedTime(hash, 0.06f, 0, 0f)") ||
                !heroHealth.Contains("anim.HasState(0, hash)"))
                failures.Add("Knight death only sets animator parameters and assumes a transition occurred; the Seeker failure was Dead=true followed by removal without observed death-state entry");

            // §12: the death-shake watch is PERMANENT instrumentation — a recurrence must NAME itself
            // in one capture instead of costing a fourth guess at the transform layer.
            if (!heroHealth.Contains("DeathStateWatch"))
                failures.Add("HeroHealth lost the DeathStateWatch instrumentation; a re-entering death state would again be invisible in the trace");

            reason = failures.Count == 0
                ? "KNIGHT_DIRECTIONAL_DEATH_OK -- four full directional clips exist, Knight explicitly enters the selected death state, and death advances through lethal hit-stop"
                : "KNIGHT_DIRECTIONAL_DEATH_FAIL: " + string.Join("; ", failures);
            if (failures.Count == 0) Debug.Log(reason); else Debug.LogError(reason);
            return failures.Count == 0;
        }
    }
}
