using System;

public class ActionNode : Node
{
    private Func<State> action;

    public ActionNode(string name, Func<State> action)
    {
        this.action = action;
    }

    public override State Evaluate()
    {
        state = action.Invoke();
        return state;
    }
}
