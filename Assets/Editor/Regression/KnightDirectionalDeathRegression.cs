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
                        if (fallback >= 0 && index > fallback)
                            failures.Add(name + " is ordered after unconditional Death and can never win");
                    }
                }
                if (fallback < 0) failures.Add("generic Death fallback is missing");
            }

            reason = failures.Count == 0
                ? "KNIGHT_DIRECTIONAL_DEATH_OK -- four directional transitions precede the generic fallback and own clips"
                : "KNIGHT_DIRECTIONAL_DEATH_FAIL: " + string.Join("; ", failures);
            if (failures.Count == 0) Debug.Log(reason); else Debug.LogError(reason);
            return failures.Count == 0;
        }
    }
}
