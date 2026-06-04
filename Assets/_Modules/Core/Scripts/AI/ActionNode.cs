// =============================================================================
// ActionNode — DEF-43: Leaf node wrapping a System.Func<NodeState> action.
// Assembly: DeNelle.AI   Namespace: DeNelle.AI
// =============================================================================

using System;

namespace DeNelle.AI
{
    /// <summary>
    /// Leaf node that wraps a <see cref="Func{NodeState}"/> delegate. Keeps leaf
    /// actions inline at the call site without requiring separate subclasses.
    /// </summary>
    public class ActionNode : BTNode
    {
        private readonly Func<NodeState> _action;

        public ActionNode(Func<NodeState> action) => _action = action;

        public override NodeState Evaluate() => State = _action();
    }
}
