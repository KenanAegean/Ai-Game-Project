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
        // Logic for checking if Kim is in a danger zone
        kim.MarkAndVisualizeZombieZones(); // Update danger zone status
        return kim.isInDangerZone;
    }
}

// Node: Recalculate the path when Kim is in danger
public class RecalculatePathAction : BehaviorNode
{
    private Kim kim;

    public RecalculatePathAction(Kim kim)
    {
        this.kim = kim;
    }

    public override bool Execute()
    {
        // Recalculate path if in danger zone
        kim.TryRecalculatePathToTarget();

        // If Kim is waiting for the path to clear, the action should indicate failure
        if (kim.isWaitingForPath)
        {
            return false;  // Kim should wait for a clear path
        }

        // Otherwise, Kim can proceed with the new path
        return true;
    }
}


// Node: Check if path is clear of obstacles
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

// Node: Move Kim to the closest burger
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
        // Logic for moving towards the closest burger
        var burgerTiles = blackboard.Get<List<Grid.Tile>>("burgers");

        if (burgerTiles == null || burgerTiles.Count == 0)
        {
            return false; // No burgers left to collect
        }

        kim.MoveAlongPath(); // Continue moving along the current path
        return true;
    }
}
