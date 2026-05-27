// =============================================================================
// Condition — DEF-43: Leaf node wrapping a System.Func<bool> predicate.
// Assembly: DeNelle.AI   Namespace: DeNelle.AI
// =============================================================================

using System;

namespace DeNelle.AI
{
    /// <summary>
    /// Leaf node that wraps a <see cref="Func{bool}"/> lambda. Keeps condition
    /// checks inline at the call site without requiring separate files.
    /// </summary>
    public class Condition : BTNode
    {
        private readonly Func<bool> _predicate;

        public Condition(Func<bool> predicate) => _predicate = predicate;

        public override NodeState Evaluate() =>
            State = _predicate() ? NodeState.Success : NodeState.Failure;
    }
}
