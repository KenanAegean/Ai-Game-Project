using System.Collections.Generic;
using UnityEngine;

// Behavior Tree Base Classes
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

// Blackboard Class
public class Blackboard
{
    private Dictionary<string, object> data = new Dictionary<string, object>();

    public void Set(string key, object value)
    {
        data[key] = value;
    }

    public T Get<T>(string key)
    {
        if (data.ContainsKey(key))
        {
            return (T)data[key];
        }
        return default;
    }
}

// Behavior Nodes

// Node: Check if Kim is in a danger zone
public class IsInDangerZone : BehaviorNode
{
    private Blackboard blackboard;
    private Kim kim;

    public IsInDangerZone(Blackboard blackboard, Kim kim)
    {
        this.blackboard = blackboard;
        this.kim = kim;
    }

    public override bool Execute()
    {
        kim.MarkAndVisualizeZombieZones(); // Update danger zone status
        return kim.isInDangerZone;
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
        kim.TryRecalculatePathToTarget();
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
        // Logic to determine if the path is clear of zombies or obstacles
        return true; // For now, assume the path is clear. Modify as needed.
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
        // Check if Kim has collected a burger and needs to find a new target
        if (kim.AreBurgersLeft())
        {
            kim.SetPathToClosestBurger();
            return true;
        }
        else
        {
            // If all burgers are collected, set the target to the finish line
            kim.SetPathToFinishLine();
            return true;
        }
    }
}
