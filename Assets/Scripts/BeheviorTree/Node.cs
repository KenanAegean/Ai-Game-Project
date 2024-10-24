public abstract class Node
{
    public enum State
    {
        Running,
        Success,
        Failure
    }

    protected State state;

    public Node() { }

    public abstract State Evaluate();
}
