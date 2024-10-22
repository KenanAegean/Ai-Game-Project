using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

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

// Node: Check if Kim is in a danger zone
public class IsInDangerZone : BehaviorNode
{
    private Kim kim;

    public IsInDangerZone(Kim kim)
    {
        this.kim = kim;
    }

    public override bool Execute()
    {
        Debug.Log("Checking if Kim is in a danger zone...");

        kim.MarkAndVisualizeZombieZones(); // Update danger zone status

        if (kim.isInDangerZone)
        {
            Debug.Log("Kim is in a danger zone. Recalculating path...");
            return true;
        }
        else
        {
            Debug.Log("Kim is not in a danger zone.");
            return false;
        }
    }
}


// Node: Recalculate the path to avoid danger zones
public class RecalculatePathToAvoidDanger : BehaviorNode
{
    private Kim kim;

    public RecalculatePathToAvoidDanger(Kim kim)
    {
        this.kim = kim;
    }

    public override bool Execute()
    {
        // Recalculate the path while avoiding the danger zone
        kim.RecalculatePath(kim.GetClosestBurgerTile() ?? kim.finishTile); // Recalculate path to avoid danger
        return !kim.isWaitingForPath; // Return true if Kim can follow the new path
    }
}

// Node: Check if the path is clear of obstacles
public class IsPathClear : BehaviorNode
{
    private Blackboard blackboard;

    public IsPathClear(Blackboard blackboard)
    {
        this.blackboard = blackboard;
    }

    public override bool Execute()
    {
        // Assume the path is clear. You can add additional logic here later if needed.
        return true;
    }
}

// Node: Move Kim to the nearest burger or target
public class MoveToBurgerAction : BehaviorNode
{
    private Kim kim;
    private Blackboard blackboard;

    public MoveToBurgerAction(Kim kim, Blackboard blackboard)
    {
        this.kim = kim;
        this.blackboard = blackboard;
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
