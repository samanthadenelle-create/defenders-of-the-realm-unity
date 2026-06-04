// =============================================================================
// BTNode — DEF-43: Lightweight Behavior Tree base node.
// Assembly: DeNelle.AI   Namespace: DeNelle.AI
// =============================================================================

namespace DeNelle.AI
{
    /// <summary>
    /// Base class for all Behavior Tree nodes. Subclass to implement composite
    /// (Selector, Sequence) or leaf (Condition, ActionNode) behaviour.
    /// </summary>
    public abstract class BTNode
    {
        public enum NodeState { Running, Success, Failure }

        /// <summary>Result of the most recent <see cref="Evaluate"/> call.</summary>
        public NodeState State { get; protected set; }

        /// <summary>Evaluate this node and return the current state.</summary>
        public abstract NodeState Evaluate();

        /// <summary>Reset node state — called when the tree re-enters this node.</summary>
        public virtual void Reset() => State = NodeState.Running;
    }
}
