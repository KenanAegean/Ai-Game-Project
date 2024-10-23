using UnityEngine;
using System.Collections.Generic;

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

// Node: Retry movement if Kim is stuck
public class RetryMovementIfStuck : BehaviorNode
{
    private Kim kim;
    private float stuckCheckInterval = 2.0f; // Check every 2 seconds
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
        // Check if Kim's position has changed since the last check
        if (Vector3.Distance(kim.transform.position, lastPosition) < 0.1f)
        {
            timeSinceLastMove += Time.deltaTime;

            // If Kim has been stuck for too long, force a path recalculation
            if (timeSinceLastMove >= stuckCheckInterval)
            {
                Debug.Log("Kim is stuck, retrying path calculation...");
                kim.RecalculatePath(null, true); // Recalculate path avoiding danger zones
                timeSinceLastMove = 0f; // Reset the stuck timer
                return true; // Kim was stuck, path recalculation triggered
            }
        }
        else
        {
            // Kim is moving, reset the stuck timer and position check
            timeSinceLastMove = 0f;
            lastPosition = kim.transform.position;
        }

        return false; // No need to retry, Kim is not stuck
    }
}

// Node: Check if the path is clear (or if Kim is not stuck)
public class IsPathClear : BehaviorNode
{
    public override bool Execute()
    {
        // For now, we assume the path is always clear
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
        // Logic for moving towards the closest burger or target
        kim.MoveAlongPath(); // Continue moving along the current path
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
        // Check if Kim has collected a burger
        kim.CheckIfCollectedBurger();

        if (kim.hasCollectedBurger)
        {
            kim.SetPathToClosestBurger(); // Set the next target
            kim.hasCollectedBurger = false; // Reset the flag
            return true;
        }
        else if (!kim.AreBurgersLeft())
        {
            // If all burgers are collected, set the target to the finish line
            kim.SetPathToFinishLine();
            return true;
        }

        return false;
    }
}
