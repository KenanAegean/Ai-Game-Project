using System.Collections.Generic;

public class Sequence : Node
{
    private List<Node> children = new List<Node>();

    public Sequence(params Node[] nodes)
    {
        children.AddRange(nodes);
    }

    public override State Evaluate()
    {
        bool anyChildRunning = false;

        foreach (Node node in children)
        {
            State childState = node.Evaluate();
            if (childState == State.Failure)
            {
                state = State.Failure;
                return state;
            }
            if (childState == State.Running)
            {
                anyChildRunning = true;
            }
        }

        state = anyChildRunning ? State.Running : State.Success;
        return state;
    }
}
