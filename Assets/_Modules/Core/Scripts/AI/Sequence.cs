// =============================================================================
// Sequence — DEF-43: AND composite node. Succeeds only if ALL children succeed.
// Assembly: DeNelle.AI   Namespace: DeNelle.AI
// =============================================================================

namespace DeNelle.AI
{
    /// <summary>
    /// AND logic — runs children in order. Returns <see cref="BTNode.NodeState.Failure"/>
    /// on the first child that fails. Returns <see cref="BTNode.NodeState.Success"/>
    /// only when every child has succeeded. Resets its cursor on failure so the
    /// sequence restarts from the beginning next tick.
    /// </summary>
    public class Sequence : BTNode
    {
        private readonly BTNode[] _children;
        private int _currentChild;

        public Sequence(params BTNode[] children) => _children = children;

        public override NodeState Evaluate()
        {
            while (_currentChild < _children.Length)
            {
                State = _children[_currentChild].Evaluate();

                if (State == NodeState.Failure)
                {
                    _currentChild = 0;
                    return State;
                }

                if (State == NodeState.Running)
                    return State;

                _currentChild++;
            }

            _currentChild = 0;
            return State = NodeState.Success;
        }

        public override void Reset()
        {
            _currentChild = 0;
            base.Reset();
        }
    }
}
