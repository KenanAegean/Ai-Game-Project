using System.Collections.Generic;
using UnityEngine;


public abstract class BehaviorNode
{
    public abstract bool Execute();
}

public class Selector : BehaviorNode
{
    private List<BehaviorNode> children;

    public Selector(params BehaviorNode[] nodes)
    {
        children = new List<BehaviorNode>(nodes);
    }

    public override bool Execute()
    {
        foreach (var node in children)
        {
            if (node.Execute()) return true;
        }
        return false;
    }
}

public class Sequence : BehaviorNode
{
    private List<BehaviorNode> children;

    public Sequence(params BehaviorNode[] nodes)
    {
        children = new List<BehaviorNode>(nodes);
    }

    public override bool Execute()
    {
        foreach (var node in children)
        {
            if (!node.Execute()) return false;
        }
        return true;
    }
}

public class BehaviorTree
{
    private BehaviorNode rootNode;

    public BehaviorTree(BehaviorNode rootNode)
    {
        this.rootNode = rootNode;
    }

    public void Execute()
    {
        rootNode.Execute();
    }
}

// Node: Check if the path is clear of obstacles
public class IsPathClear : BehaviorNode
{
    public override bool Execute()
    {
        return true;
    }
}

// Node: Move Kim to the nearest burger or target
public class MoveToBurgerAction : BehaviorNode
{
    private Kim kim;

    public MoveToBurgerAction(Kim kim)
    {
        this.kim = kim;
    }

    public override bool Execute()
    {
        kim.MoveAlongPath();
        return true;
    }
}

// Node: Check if Kim needs to switch targets (from burgers to the finish line)
public class CheckAndSwitchTarget : BehaviorNode
{
    private Kim kim;

    public CheckAndSwitchTarget(Kim kim)
    {
        this.kim = kim;
    }

    public override bool Execute()
    {
        kim.CheckIfCollectedBurger();
        if (kim.hasCollectedBurger)
        {
            kim.SetPathToClosestBurger();
            kim.hasCollectedBurger = false;
            return true;
        }
        else if (!kim.AreBurgersLeft())
        {
            kim.SetPathToFinishLine();
            return true;
        }
        return false;
    }
}

// Node: Retry movement if Kim is stuck
public class RetryMovementIfStuck : BehaviorNode
{
    private Kim kim;
    private float stuckCheckInterval = 2.0f;
    private Vector3 lastPosition;
    private float timeSinceLastMove;

    public RetryMovementIfStuck(Kim kim)
    {
        this.kim = kim;
        this.lastPosition = kim.transform.position;
        this.timeSinceLastMove = 0f;
    }

    public override bool Execute()
    {
        if (Vector3.Distance(kim.transform.position, lastPosition) < 0.1f)
        {
            timeSinceLastMove += Time.deltaTime;
            if (timeSinceLastMove >= stuckCheckInterval)
            {
                kim.RecalculatePath();
                timeSinceLastMove = 0f;
                return true;
            }
        }
        else
        {
            timeSinceLastMove = 0f;
            lastPosition = kim.transform.position;
        }
        return false;
    }
}
