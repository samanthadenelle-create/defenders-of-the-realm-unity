using System;
using System.Collections.Generic;

namespace DeNelle.Core.Combat
{
    public enum AffinityOutcome { Neutral = 0, Vulnerable = 1, Resisted = 2 }

    public readonly struct ElementalDamageResult
    {
        public readonly float BaseAmount;
        public readonly DamageElement SourceElement;
        public readonly DamageElement TargetAffinity;
        public readonly float Multiplier;
        public readonly float FinalAmount;
        public readonly AffinityOutcome Outcome;

        public ElementalDamageResult(float amount, DamageElement source, DamageElement target,
            float multiplier, AffinityOutcome outcome)
        {
            BaseAmount = amount;
            SourceElement = source;
            TargetAffinity = target;
            Multiplier = multiplier;
            FinalAmount = amount * multiplier;
            Outcome = outcome;
        }
    }

    /// <summary>WO-1065: the single, presentation-neutral elemental arithmetic authority.</summary>
    public static class ElementalDamageResolver
    {
        public const float VulnerableMultiplier = 1.25f;
        public const float ResistedMultiplier = 0.75f;
        public const float NeutralMultiplier = 1f;
        public static event Action<ElementalDamageResult> Resolved;

        public static ElementalDamageResult Resolve(float amount, DamageElement source,
            DamageElement affinity, IReadOnlyList<DamageElement> vulnerableTo = null)
        {
            amount = Math.Max(0f, amount);
            bool elemental = source != DamageElement.None;
            bool resisted = elemental && affinity != DamageElement.None && source == affinity;
            bool vulnerable = elemental && !resisted && Contains(vulnerableTo, source);
            AffinityOutcome outcome = resisted ? AffinityOutcome.Resisted
                : vulnerable ? AffinityOutcome.Vulnerable : AffinityOutcome.Neutral;
            float multiplier = outcome == AffinityOutcome.Resisted ? ResistedMultiplier
                : outcome == AffinityOutcome.Vulnerable ? VulnerableMultiplier : NeutralMultiplier;
            var result = new ElementalDamageResult(amount, source, affinity, multiplier, outcome);
            Resolved?.Invoke(result);
            return result;
        }

        public static DamageElement ParseElement(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DamageElement.None;
            switch (value.Trim().ToLowerInvariant())
            {
                case "aether": case "arcane": return DamageElement.Aether;
                case "flame": case "fire": return DamageElement.Flame;
                case "ice": case "frost": return DamageElement.Ice;
                default: return DamageElement.None;
            }
        }

        private static bool Contains(IReadOnlyList<DamageElement> values, DamageElement value)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++) if (values[i] == value) return true;
            return false;
        }
    }
}
