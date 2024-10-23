using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private bool debugMode = false;
    [SerializeField] private float contextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;

    // Store burger and finish line positions
    public List<Grid.Tile> burgerTiles { get; private set; }
    public Grid.Tile finishTile { get; private set; }

    // Status flags
    public bool isWaitingForPath = false;
    public bool hasCollectedBurger = false;

    // Blackboard for the behavior tree
    private Blackboard blackboard = new Blackboard();

    // Behavior tree
    private BehaviorTree behaviorTree;

    public override void StartCharacter()
    {
        base.StartCharacter();

        // Initialize pathfinding
        pathfinding = new Pathfinding(Grid.Instance);

        // Store all burger positions at the start
        burgerTiles = GetAllBurgerTiles();
        finishTile = Grid.Instance.GetFinishTile();

        burgerTiles.Add(finishTile); // Always add the finish tile as the last target

        // Set up the blackboard with initial information
        blackboard.Set("burgers", burgerTiles);

        // Initialize the behavior tree
        behaviorTree = new BehaviorTree(
            new Selector(
                new Sequence(
                    new IsPathClear(),
                    new MoveToTarget(this),
                    new CheckAndSwitchTarget(this),
                    new IsPathClear(),
                    new MoveToTarget(this)
                ),
                new RetryMovementIfStuck(this),
                new IsPathClear(),
                new MoveToTarget(this)
            )
        );

        // Set the initial path to the nearest burger
        SetPathToTarget();
    }

    public override void UpdateCharacter()
    {
        // Execute the behavior tree
        behaviorTree.Execute();

        base.UpdateCharacter();

        // Ensure Kim moves along the current path
        if (!isWaitingForPath && currentPath != null && currentPathIndex < currentPath.Count)
        {
            MoveAlongPath();
        }
    }

    public void SetPathToTarget()
    {
        if (burgerTiles.Count > 0)
        {
            // Find the closest target (could be a burger or finish tile)
            Grid.Tile closestTarget = GetClosestTarget();

            // If the closest target is the finish tile but there are burgers left, switch to the closest burger
            if (closestTarget == finishTile && AreBurgersLeft())
            {
                Debug.Log("Finish line is the closest, but burgers are left. Targeting the closest burger instead.");
                closestTarget = GetClosestBurgerTile(); // Get the closest burger
            }

            // Recalculate the path to the selected target
            RecalculatePath(closestTarget);
        }
        else
        {
            Debug.Log("All targets reached.");
        }
    }


    // Recalculate the path to a target
    public void RecalculatePath(Grid.Tile targetTile = null)
    {
        if (targetTile == null)
        {
            Debug.LogError("No target specified for path recalculation.");
            return;
        }

        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);

        // Clear the current path before recalculating
        if (currentPath != null)
        {
            currentPath.Clear();  // Ensures no leftover path data is present
        }

        // Recalculate the path using dynamicOccupiedTiles when avoiding the occupied zone
        currentPath = pathfinding.FindPath(startTile, targetTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            isWaitingForPath = true;
            Debug.Log("No path found. Kim is waiting...");
        }
        else
        {
            isWaitingForPath = false;
            currentPathIndex = 0;

            // Log the recalculated path
            if (debugMode)
            {
                Debug.Log("Recalculated path:");
                foreach (var tile in currentPath)
                {
                    Debug.Log($"Tile at ({tile.x}, {tile.y})");
                }
            }

            // Set walk buffer using the newly calculated path
            SetWalkBuffer(currentPath);  // This will handle clearing the buffer
            Debug.Log("Kim's path recalculated. Starting movement.");
        }
    }


    // Move along the current path
    public void MoveAlongPath()
    {
        //SetWalkBuffer(currentPath);
        //base.UpdateCharacter();
        if (currentPath == null)
        {
            Debug.LogWarning("No path available.");
            return;
        }

        if (currentPathIndex >= currentPath.Count)
        {
            Debug.LogWarning("Path completed.");
            return;
        }

        if (isWaitingForPath)
        {
            Debug.LogWarning("Kim is waiting for a new path.");
            return;
        }

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        // Move towards the target
        Vector3 direction = (targetPosition - transform.position).normalized;
        //transform.position = Vector3.MoveTowards(transform.position, targetPosition, CharacterMoveSpeed * Time.deltaTime);
        //SetWalkBuffer(currentPath);

        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        if (distanceToTarget < ReachDistThreshold)
        {
            currentPathIndex++; // Move to next tile
            if (currentPathIndex >= currentPath.Count)
            {
                currentPath = null;
                Debug.Log("Path complete. No more tiles to follow.");
                return;
            }
        }

        // Visualize the path
        VisualizePath();
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
                Debug.DrawLine(from, to, Color.red, 0.5f);
            }
        }
    }

    public bool AreBurgersLeft()
    {
        return burgerTiles != null && burgerTiles.Count > 1; // More than one means burgers are still left
    }

    public List<Grid.Tile> GetAllBurgerTiles()
    {
        List<Grid.Tile> burgerTiles = new List<Grid.Tile>();
        GameObject[] allBurgers = GameObject.FindGameObjectsWithTag("Burger");

        foreach (GameObject burger in allBurgers)
        {
            Grid.Tile burgerTile = Grid.Instance.GetClosest(burger.transform.position);
            if (burgerTile != null)
            {
                burgerTiles.Add(burgerTile);
            }
        }

        return burgerTiles;
    }

    // Get the closest target (either a burger or the finish line)
    public Grid.Tile GetClosestTarget()
    {
        Grid.Tile closestTarget = null;
        float shortestDistance = float.MaxValue;

        foreach (Grid.Tile target in burgerTiles)
        {
            float distance = Vector3.Distance(transform.position, Grid.Instance.WorldPos(target));

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    // Get the closest burger tile (ignore the finish tile)
    public Grid.Tile GetClosestBurgerTile()
    {
        Grid.Tile closestBurgerTile = null;
        float shortestDistance = float.MaxValue;

        // Ignore the last item (which is the finish tile)
        for (int i = 0; i < burgerTiles.Count - 1; i++)
        {
            Grid.Tile burgerTile = burgerTiles[i];
            float distance = Vector3.Distance(transform.position, Grid.Instance.WorldPos(burgerTile));

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestBurgerTile = burgerTile;
            }
        }

        return closestBurgerTile;
    }

    public void CheckIfCollectedBurger()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);
        if (burgerTiles.Contains(currentTile))
        {
            burgerTiles.Remove(currentTile);
            hasCollectedBurger = true;
            Debug.Log("Kim collected a burger.");

            // Immediately recalculate path to next target
            SetPathToTarget();
        }
    }
}
