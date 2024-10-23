using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private float occupiedZoneRadius = 2.0f;
    //[SerializeField] private float dangerZoneRadius = 3.0f;
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
                    new IsCloseToOccupiedZone(this),   // Check if Kim is near an occupied zone
                    new RecalculatePathToAvoidOccupiedZones(this)  // Recalculate path avoiding occupied zones
                ),
                new Sequence(
                    new IsPathClear(), // Assuming path is always clear for now
                    new MoveToBurgerAction(this),  // Move Kim towards the closest burger
                    new CheckAndSwitchTarget(this)  // Check if the target needs switching (burgers -> finish line)
                ),
                new RetryMovementIfStuck(this)  // Retry movement if Kim gets stuck
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
    public void RecalculatePath(Grid.Tile targetTile = null, bool avoidOccupied = false)
    {
        // If no target is provided, use the closest burger or finish line
        if (targetTile == null)
        {
            targetTile = GetClosestBurgerTile() ?? finishTile;
        }

        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);

        // Pass the occupied tiles list if avoiding occupied zones
        List<Grid.Tile> tilesToAvoid = avoidOccupied ? dynamicOccupiedTiles : null;

        // Recalculate the path using dynamicOccupiedTiles when avoiding the occupied zone
        currentPath = pathfinding.FindPath(startTile, targetTile, avoidOccupied, tilesToAvoid);

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
        if (currentPath == null || currentPathIndex >= currentPath.Count || isWaitingForPath)
        {
            Debug.LogWarning("No valid path to follow or Kim is waiting.");
            return;
        }

        // Log how many tiles are left to the target
        int tilesLeft = currentPath.Count - currentPathIndex;
        Debug.Log($"Tiles left to target: {tilesLeft}");

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        // Debug target position to ensure we're moving towards a valid point
        Debug.Log($"Moving towards target position: {targetPosition}");
        Debug.Log($"Kim's current position: {transform.position}");

        // If Kim's current position is the same as the target position, move to the next tile
        if (Vector3.Distance(transform.position, targetPosition) < ReachDistThreshold)
        {
            Debug.Log($"Reached tile {currentPathIndex}, moving to the next tile...");
            currentPathIndex++; // Move to the next tile

            // Log how many tiles are left after moving to the next tile
            tilesLeft = currentPath.Count - currentPathIndex;
            Debug.Log($"After moving, tiles left to target: {tilesLeft}");

            // If we've reached the end of the path, reset the path
            if (currentPathIndex >= currentPath.Count)
            {
                Debug.Log("Path complete. No more tiles to follow.");
                currentPath = null; // Reset the path once all tiles are reached
                return;
            }

            // Get the new target position after incrementing the path index
            targetTile = currentPath[currentPathIndex];
            targetPosition = Grid.Instance.WorldPos(targetTile);
            Debug.Log($"New target position: {targetPosition}");
        }

        // Calculate the direction and magnitude
        Vector3 direction = (targetPosition - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // Debug direction vector and distance
        Debug.Log($"Direction vector: {direction}");
        Debug.Log($"Distance to target: {distanceToTarget}");

        // If the distance is very small, snap to the target position
        if (distanceToTarget < ReachDistThreshold)
        {
            transform.position = targetPosition; // Snap Kim to the target position
            Debug.Log("Kim snapped to the target position due to small distance.");
        }
        else if (direction.magnitude > 0.001f)  // Ensure direction magnitude is non-zero before moving
        {
            // Apply movement to Kim based on the direction and speed
            transform.position += direction * CharacterMoveSpeed * Time.deltaTime;
            Debug.Log($"Kim moved towards the target. New position: {transform.position}");
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
    public void MarkAndVisualizeZombieZones()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, contextRadius);

        bool isKimCloseToOccupiedZone = false;  // Reset the close-to-occupied-zone flag

        // Clear previous dynamic occupied tiles before marking again
        ResetDynamicOccupiedTiles();

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

                // Mark and visualize occupied zones (red)
                MarkZombieZones(zombieTile);  // Handles marking the occupied zones

                // Check if Kim is near an occupied zone
                isKimCloseToOccupiedZone = IsKimNearOccupiedZone();
            }
        }

        if (isKimCloseToOccupiedZone)
        {
            Debug.Log("Kim is close to an occupied zone! Recalculating path to avoid occupied zones.");
            RecalculatePath(null, true); // Recalculate path avoiding occupied zones
        }
        else
        {
            Debug.Log("Kim is safe and not near any occupied zone.");
        }
    }

    // Mark and visualize the zones around zombies (only occupied zones)
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
    }

    // Check if Kim is near any occupied zone
    private bool IsKimNearOccupiedZone()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);

        foreach (Grid.Tile occupiedTile in dynamicOccupiedTiles)
        {
            // If Kim is within a tile or two from any occupied zone, she should avoid it
            if (Vector3.Distance(Grid.Instance.WorldPos(currentTile), Grid.Instance.WorldPos(occupiedTile)) <= occupiedZoneRadius)
            {
                Debug.Log("Kim is close to an occupied zone!");
                return true;
            }
        }

        return false;  // Kim is not near any occupied zones
    }

    // Clear dynamic occupied tiles after recalculating zones
    private void ResetDynamicOccupiedTiles()
    {
        foreach (var tile in dynamicOccupiedTiles)
        {
            tile.occupied = false;
        }
        dynamicOccupiedTiles.Clear();
    }




}



