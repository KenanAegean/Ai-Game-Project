using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private float occupiedZoneRadius = 2.0f;
    [SerializeField] private float dangerZoneRadius = 3.0f;
    [SerializeField] float contextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;

    // Store burger and finish line positions
    public List<Grid.Tile> burgerTiles { get; private set; }
    public Grid.Tile finishTile { get; private set; }

    // Status flags
    public bool isInDangerZone = false;
    public bool isWaitingForPath = false;
    public bool hasCollectedBurger = false;

    // Zombie-related zones
    private List<Grid.Tile> dynamicOccupiedTiles = new List<Grid.Tile>();

    // Blackboard for the behavior tree
    private Blackboard blackboard = new Blackboard();

    // Behavior tree
    private BehaviorTree behaviorTree;

    // Movement speed
    protected const float CharacterMoveSpeed = 1.0f;

    public override void StartCharacter()
    {
        base.StartCharacter();

        // Initialize pathfinding
        pathfinding = new Pathfinding(Grid.Instance);

        // Ensure Kim is aligned to the grid tile (snaps to the nearest tile)
        AlignToGrid();

        // Store all burger positions at the start
        burgerTiles = GetAllBurgerTiles();
        finishTile = Grid.Instance.GetFinishTile();

        // Set up the blackboard with initial information
        blackboard.Set("burgers", burgerTiles);

        // Initialize the behavior tree
        behaviorTree = new BehaviorTree(
            new Selector(
                new Sequence(
                    new IsInDangerZone(this),
                    new RecalculatePathToAvoidDanger(this)
                ),
                new Sequence(
                    new IsPathClear(blackboard),
                    new MoveToBurgerAction(this, blackboard),
                    new CheckAndSwitchTarget(this)
                ),
                new RetryMovementIfStuck(this) // New logic to keep retrying movement
            )
        );



        // Set the initial path to the nearest burger and start moving
        SetPathToClosestBurger();
        MoveAlongPath();  // Start moving immediately after setting the path
    }

    public override void UpdateCharacter()
    {
        base.UpdateCharacter();

        // Clear any previous dynamic occupied zones
        ResetDynamicOccupiedTiles();

        // Call this function every frame to update zombie zones
        MarkAndVisualizeZombieZones();

        // Execute the behavior tree
        behaviorTree.Execute();

        // Ensure Kim moves along the current path
        if (!isWaitingForPath && currentPath != null && currentPathIndex < currentPath.Count)
        {
            MoveAlongPath();
        }
    }

    public bool AreBurgersLeft()
    {
        return burgerTiles != null && burgerTiles.Count > 0;
    }

    // Align Kim to the nearest grid tile (Snap to grid)
    private void AlignToGrid()
    {
        Grid.Tile closestTile = Grid.Instance.GetClosest(transform.position);
        transform.position = Grid.Instance.WorldPos(closestTile); // Snap Kim to the grid tile position
        myCurrentTile = closestTile;  // Ensure Kim's current tile is set correctly
        Debug.Log($"Kim aligned to grid tile at position {transform.position}");
    }

    // Set the path to the closest burger
    public void SetPathToClosestBurger()
    {
        if (burgerTiles.Count > 0)
        {
            Grid.Tile closestBurger = GetClosestBurgerTile();
            RecalculatePath(closestBurger);
        }
        else
        {
            SetPathToFinishLine();
        }
    }

    // Set the path to the finish line
    public void SetPathToFinishLine()
    {
        RecalculatePath(finishTile);
    }

    // Recalculate the path to a target
    public void RecalculatePath(Grid.Tile targetTile = null, bool avoidDanger = false)
    {
        // If no target is provided, use the closest burger or finish line
        if (targetTile == null)
        {
            targetTile = GetClosestBurgerTile() ?? finishTile;
        }

        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);

        // Pass the danger zone radius if avoidDanger is true
        currentPath = pathfinding.FindPath(startTile, targetTile, avoidDanger, dangerZoneRadius);

        if (currentPath == null || currentPath.Count == 0)
        {
            // If no valid path is found, Kim will wait and try again
            isWaitingForPath = true;
            Debug.Log("No path found due to obstacles. Waiting for path to clear...");

            // Retry after a delay to see if zombies moved
            Invoke("RetryRecalculatePath", 1.0f); // Retry after 1 second
        }
        else
        {
            isWaitingForPath = false;
            currentPathIndex = 0;

            // Log the recalculated path
            Debug.Log("Recalculated path:");
            foreach (var tile in currentPath)
            {
                Debug.Log($"Tile at ({tile.x}, {tile.y})");
            }

            // Set walk buffer using the newly calculated path
            SetWalkBuffer(currentPath);
            Debug.Log("Kim's path recalculated. Starting movement.");
        }
    }

    private void RetryRecalculatePath()
    {
        RecalculatePath(null, true); // Retry by avoiding danger zones again
    }



    // Move along the current path
    public void MoveAlongPath()
    {
        // Ensure there's a valid path and that Kim is not waiting
        if (currentPath == null || currentPathIndex >= currentPath.Count || isWaitingForPath)
        {
            Debug.LogWarning("No valid path or Kim is waiting.");
            return;
        }

        // Get the current target tile
        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        // Check if Kim is already at the target position
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        if (distanceToTarget < ReachDistThreshold)
        {
            // Kim is close enough to the target, move to the next tile
            currentPathIndex++;
            Debug.Log($"Reached tile {currentPathIndex}, moving to the next tile...");
            return;  // Skip further movement calculations and go to the next tile
        }

        // Calculate the direction vector
        Vector3 direction = (targetPosition - transform.position).normalized;

        // Debug target position and direction vector
        Debug.Log($"Moving towards target position: {targetPosition}");
        Debug.Log($"Direction vector: {direction}");

        // Check if the direction vector has a valid magnitude before moving
        if (direction.magnitude > 0.01f)  // Slightly higher threshold to ensure movement happens
        {
            // Move Kim towards the target tile
            transform.position += direction * CharacterMoveSpeed * Time.deltaTime;

            // Debug distance to target
            Debug.Log($"Distance to target: {distanceToTarget}");
        }
        else
        {
            Debug.LogWarning("Movement direction is zero. No movement.");
        }

        // Visualize the path
        VisualizePath();
    }




    // Visualize the path
    private void VisualizePath()
    {
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Vector3 from = Grid.Instance.WorldPos(currentPath[i]);
            Vector3 to = Grid.Instance.WorldPos(currentPath[i + 1]);
            Debug.DrawLine(from, to, Color.red, 0.5f);
        }
    }

    // Get all burger tiles at the start of the game
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

    // Get the closest burger tile
    public Grid.Tile GetClosestBurgerTile()
    {
        Grid.Tile closestBurgerTile = null;
        float shortestDistance = float.MaxValue;

        foreach (Grid.Tile burgerTile in burgerTiles)
        {
            float distance = Vector3.Distance(transform.position, Grid.Instance.WorldPos(burgerTile));

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestBurgerTile = burgerTile;
            }
        }

        return closestBurgerTile;
    }

    // Check if Kim collected a burger
    public void CheckIfCollectedBurger()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);
        if (burgerTiles.Contains(currentTile))
        {
            // Remove the collected burger
            burgerTiles.Remove(currentTile);
            hasCollectedBurger = true;
            Debug.Log("Kim collected a burger.");

            // Immediately recalculate path to next target
            SetPathToClosestBurger();
        }
    }


    // Reset dynamic occupied tiles after each frame
    private void ResetDynamicOccupiedTiles()
    {
        foreach (var tile in dynamicOccupiedTiles)
        {
            tile.occupied = false;
        }
        dynamicOccupiedTiles.Clear();
    }


    // Mark and visualize the zombie zones
    public void MarkAndVisualizeZombieZones()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, contextRadius);

        isInDangerZone = false;  // Reset the danger zone flag

        Debug.Log("Checking for zombies...");

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Debug.Log("Zombie detected!");

                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

                // Mark and visualize occupied and danger zones
                MarkZombieZones(zombieTile);
            }
        }

        if (isInDangerZone)
        {
            Debug.Log("Kim is in the danger zone!");
        }
        else
        {
            Debug.Log("Kim is safe.");
        }
    }

    private void MarkZombieZones(Grid.Tile zombieTile)
    {
        // Occupied zone (red)
        for (int x = -(int)occupiedZoneRadius; x <= (int)occupiedZoneRadius; x++)
        {
            for (int y = -(int)occupiedZoneRadius; y <= (int)occupiedZoneRadius; y++)
            {
                Grid.Tile nearbyTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                if (nearbyTile != null && !nearbyTile.occupied)
                {
                    // Mark tile as occupied
                    nearbyTile.occupied = true;
                    dynamicOccupiedTiles.Add(nearbyTile);
                    Debug.DrawLine(Grid.Instance.WorldPos(nearbyTile), Grid.Instance.WorldPos(nearbyTile) + Vector3.up * 2, Color.red);
                }
            }
        }

        // Danger zone (yellow)
        for (int x = -(int)dangerZoneRadius; x <= (int)dangerZoneRadius; x++)
        {
            for (int y = -(int)dangerZoneRadius; y <= (int)dangerZoneRadius; y++)
            {
                Grid.Tile dangerTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                if (dangerTile != null && Vector3.Distance(Grid.Instance.WorldPos(dangerTile), transform.position) <= dangerZoneRadius)
                {
                    // Instead of blocking the entire zone, check if there's still a possible path
                    if (!dangerTile.occupied)
                    {
                        isInDangerZone = true; // Mark Kim as in danger
                        Debug.DrawLine(Grid.Instance.WorldPos(dangerTile), Grid.Instance.WorldPos(dangerTile) + Vector3.up * 2, Color.yellow);
                    }
                }
            }
        }
    }


}
