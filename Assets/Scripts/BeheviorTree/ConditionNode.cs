using System;

public class ConditionNode : Node
{
    private Func<bool> condition;

    public ConditionNode(string name, Func<bool> condition)
    {
        this.condition = condition;
    }

    public override State Evaluate()
    {
        state = condition() ? State.Success : State.Failure;
        return state;
    }
}
