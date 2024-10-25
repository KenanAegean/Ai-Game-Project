using System.Collections.Generic;

public class Selector : Node
{
    private List<Node> children = new List<Node>();

    public Selector(params Node[] nodes)
    {
        children.AddRange(nodes);
    }

    public override State Evaluate()
    {
        foreach (Node node in children)
        {
            State childState = node.Evaluate();
            if (childState == State.Success)
            {
                state = State.Success;
                return state;
            }
            if (childState == State.Running)
            {
                state = State.Running;
                return state;
            }
        }

        state = State.Failure;
        return state;
    }
}
