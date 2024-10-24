public class BehaviorTree
{
    private Node rootNode;

    public BehaviorTree(Node rootNode)
    {
        this.rootNode = rootNode;
    }

    public void Execute()
    {
        if (rootNode != null)
        {
            rootNode.Evaluate();
        }
    }
}
