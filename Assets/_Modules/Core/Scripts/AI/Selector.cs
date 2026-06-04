// =============================================================================
// Selector — DEF-43: OR composite node. Succeeds if ANY child succeeds.
// Assembly: DeNelle.AI   Namespace: DeNelle.AI
// =============================================================================

namespace DeNelle.AI
{
    /// <summary>
    /// OR logic — tries each child in order and returns <see cref="BTNode.NodeState.Success"/>
    /// on the first child that succeeds or is still running. Returns
    /// <see cref="BTNode.NodeState.Failure"/> only when all children fail.
    /// </summary>
    public class Selector : BTNode
    {
        private readonly BTNode[] _children;

        public Selector(params BTNode[] children) => _children = children;

        public override NodeState Evaluate()
        {
            foreach (var child in _children)
            {
                State = child.Evaluate();
                if (State == NodeState.Success || State == NodeState.Running)
                    return State;
            }
            return State = NodeState.Failure;
        }
    }
}
