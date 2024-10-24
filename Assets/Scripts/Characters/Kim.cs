using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private bool debugMode = false;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;

    // Store burger and finish line positions
    public List<Grid.Tile> targets { get; private set; }
    public Grid.Tile finishTile { get; private set; }

    // Behavior tree
    private BehaviorTree behaviorTree;
    private OccupiedZones occupiedZones;

    public override void StartCharacter()
    {
        base.StartCharacter();

        // Ensure that myReachedTile is true at the start
        myReachedTile = true;

        // Initialize pathfinding and occupied zones
        pathfinding = new Pathfinding(Grid.Instance);
        occupiedZones = GameObject.FindObjectOfType<OccupiedZones>(); // Reference to the occupied zones

        // Store all burger positions at the start
        targets = GetAllBurgerTiles();
        finishTile = Grid.Instance.GetFinishTile();

        targets.Add(finishTile); // Always add the finish tile as the last target

        // Initialize the behavior tree
        behaviorTree = new BehaviorTree(
            new Selector(
                // Collect Burgers Sequence
                new Sequence(
                    new ConditionNode("AreBurgersLeft", AreBurgersLeft),
                    new ActionNode("SetPathToClosestBurger", SetPathToClosestBurger),
                    new Selector(
                        new Sequence(
                            new ConditionNode("IsPathAvailable", IsPathAvailable),
                            new ActionNode("MoveAlongPath", MoveAlongPath)
                        ),
                        new ActionNode("Wait", Wait)
                    )
                ),
                // Go to Finish Line Sequence
                new Sequence(
                    new ActionNode("SetPathToFinishLine", SetPathToFinishLine),
                    new Selector(
                        new Sequence(
                            new ConditionNode("IsPathAvailable", IsPathAvailable),
                            new ActionNode("MoveAlongPath", MoveAlongPath)
                        ),
                        new ActionNode("Wait", Wait)
                    )
                )
            )
        );

        // Start moving by executing the behavior tree
        behaviorTree.Execute();
    }

    public override void UpdateCharacter()
    {
        // Execute the behavior tree
        behaviorTree.Execute();

        base.UpdateCharacter();

        // Check if Kim has collected the target (burger)
        CheckIfCollectedTarget();
    }

    // Behavior Tree Methods
    private bool AreBurgersLeft()
    {
        return targets != null && targets.Count > 1; // More than one means burgers are still left
    }

    private Node.State SetPathToClosestBurger()
    {
        Grid.Tile closestBurger = GetClosestBurgerTile();
        if (closestBurger != null)
        {
            RecalculatePath(closestBurger);
            return Node.State.Success;
        }
        else
        {
            return Node.State.Failure;
        }
    }

    private Node.State SetPathToFinishLine()
    {
        RecalculatePath(finishTile);
        return Node.State.Success;
    }

    private bool IsPathAvailable()
    {
        return currentPath != null && currentPath.Count > 0;
    }

    private Node.State MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            return Node.State.Success;
        }

        // Movement is handled in base.UpdateCharacter(), which uses myWalkBuffer

        // Visualize the path
        VisualizePath();

        return Node.State.Running;
    }

    private Node.State Wait()
    {
        // Kim waits for zombies to move away
        Debug.Log("Kim is waiting for zombies to move away.");
        return Node.State.Running;
    }

    public void RecalculatePath(Grid.Tile targetTile = null)
    {
        if (targetTile == null)
        {
            Debug.LogError("No target specified for path recalculation.");
            return;
        }

        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);

        // Recalculate the path to avoid the occupied zones
        currentPath = pathfinding.FindPath(startTile, targetTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.Log("No path available. Kim will wait for zombies to move away.");
            // Clear the walk buffer to stop movement
            SetWalkBuffer(new List<Grid.Tile>());
        }
        else
        {
            // **Remove the starting tile if it's the same as the current tile**
            if (Grid.Instance.IsSameTile(startTile, currentPath[0]))
            {
                currentPath.RemoveAt(0);
            }

            if (currentPath.Count == 0)
            {
                // No tiles to move to
                Debug.Log("No tiles to move to after removing the starting tile.");
                SetWalkBuffer(new List<Grid.Tile>());
                return;
            }

            // Set walk buffer using the newly calculated path
            SetWalkBuffer(currentPath);  // This will handle clearing the buffer
            Debug.Log("Kim's path recalculated. Starting movement.");
            myReachedTile = true;

            // Log the recalculated path
            if (debugMode)
            {
                Debug.Log("Recalculated path:");
                foreach (var tile in currentPath)
                {
                    Debug.Log($"Tile at ({tile.x}, {tile.y})");
                }
            }
        }
    }

    public void CheckIfCollectedTarget()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);
        if (targets.Contains(currentTile))
        {
            targets.Remove(currentTile);
            Debug.Log("Kim collected a burger.");

            // Immediately recalculate path to next target
            behaviorTree.Execute(); // Re-run the behavior tree to update the path
        }
    }

    public List<Grid.Tile> GetAllBurgerTiles()
    {
        List<Grid.Tile> targets = new List<Grid.Tile>();
        GameObject[] allBurgers = GameObject.FindGameObjectsWithTag("Burger");

        foreach (GameObject burger in allBurgers)
        {
            Grid.Tile burgerTile = Grid.Instance.GetClosest(burger.transform.position);
            if (burgerTile != null)
            {
                targets.Add(burgerTile);
            }
        }

        return targets;
    }

    // Get the closest burger tile (ignore the finish tile)
    public Grid.Tile GetClosestBurgerTile()
    {
        Grid.Tile closestBurgerTile = null;
        float shortestDistance = float.MaxValue;

        // Ignore the last item (which is the finish tile)
        for (int i = 0; i < targets.Count - 1; i++)
        {
            Grid.Tile burgerTile = targets[i];
            float distance = Vector3.Distance(transform.position, Grid.Instance.WorldPos(burgerTile));

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestBurgerTile = burgerTile;
            }
        }

        Debug.Log($"Closest Burger Tile: {closestBurgerTile?.x}, {closestBurgerTile?.y}");
        return closestBurgerTile;
    }

    // Visualize the path
    private void VisualizePath()
    {
        if (currentPath != null)
        {
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 from = Grid.Instance.WorldPos(currentPath[i]);
                Vector3 to = Grid.Instance.WorldPos(currentPath[i + 1]);
                Debug.DrawLine(from, to, Color.red, 0.1f);
            }
        }
    }
}
