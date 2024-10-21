using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private float occupiedZoneRadius = 2.0f;
    [SerializeField] private float dangerZoneRadius = 3.0f;
    [SerializeField] float ContextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;

    // Store burger positions
    private List<Grid.Tile> burgerTiles;

    // Timer for recalculating path when Kim is in danger zone or waiting for path to clear
    private float pathCheckTimer = 1.0f;
    private float timeSinceLastCheck = 0f;

    // Status flags
    public bool isInDangerZone = false;
    private bool wasInDangerZoneLastFrame = false;
    public bool isWaitingForPath = false;

    // Dynamic occupied tiles
    private List<Grid.Tile> dynamicOccupiedTiles = new List<Grid.Tile>();

    // Blackboard to store data for the behavior tree
    private Blackboard blackboard = new Blackboard();

    // Define the behavior tree nodes
    private BehaviorTree behaviorTree;

    protected const float ReachDistThreshold = 0.3f;
    protected const float CharacterMoveSpeed = 2.0f;

    public override void StartCharacter()
    {
        base.StartCharacter();

        // Pathfinding setup
        pathfinding = new Pathfinding(Grid.Instance);

        // Snap Kim to the nearest grid tile
        transform.position = Grid.Instance.WorldPos(Grid.Instance.GetClosest(transform.position));

        // Get all burger positions at the start
        burgerTiles = GetAllBurgerTiles();

        // Set up the blackboard with initial information
        blackboard.Set("burgers", burgerTiles);

        // Initialize the behavior tree
        behaviorTree = new BehaviorTree(
            new Selector(
                new Sequence(
                    new IsInDangerZone(blackboard, this),
                    new RecalculatePathToAvoidDanger(this)
                ),
                new Sequence(
                    new IsPathClear(blackboard),
                    new MoveToBurgerAction(this, blackboard),
                    new CheckAndSwitchTarget(this)
                )
            )
        );

        // Set the initial path to the nearest burger
        SetPathToClosestBurger();
    }

    public override void UpdateCharacter()
    {
        base.UpdateCharacter();

        // Clear previous dynamic occupied zones
        ResetDynamicOccupiedTiles();

        // Execute the behavior tree every frame
        behaviorTree.Execute();

        // Ensure Kim moves along the current path
        if (!isWaitingForPath && currentPath != null && currentPathIndex < currentPath.Count)
        {
            Debug.Log($"Kim is moving along the path. Current path index: {currentPathIndex}, Path length: {currentPath.Count}");
            MoveAlongPath();
        }
        else
        {
            Debug.Log("Kim is not moving. Waiting for a path or has no valid path.");
        }
    }




    public void SetPathToClosestBurger()
    {
        if (burgerTiles.Count > 0)
        {
            // Set path to the closest burger
            Grid.Tile closestBurger = GetClosestBurgerTile();
            TryRecalculatePathToTarget();
        }
    }

    public void SetPathToFinishLine()
    {
        // Set path to the finish line
        Grid.Tile finishTile = Grid.Instance.GetFinishTile();
        TryRecalculatePathToTarget();
    }

    public void MoveAlongPath()
    {
        // Do not move if no valid path or Kim is waiting
        if (currentPath == null || currentPathIndex >= currentPath.Count || isWaitingForPath)
        {
            Debug.Log("No valid path to follow or Kim is waiting.");
            return;
        }

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        Debug.Log($"Kim's current position: {transform.position}, Target position: {targetPosition}");

        Vector3 direction = (targetPosition - transform.position).normalized;

        // Move Kim toward the target tile
        transform.position += direction * CharacterMoveSpeed * Time.deltaTime;

        // Log the distance to the target tile
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        Debug.Log($"Distance to target tile: {distanceToTarget}");

        // Check if Kim has reached the target tile (increase threshold if needed)
        if (distanceToTarget < ReachDistThreshold)
        {
            transform.position = targetPosition; // Snap to the target position
            currentPathIndex++; // Move to the next tile
            Debug.Log($"Reached tile {currentPathIndex}, moving to the next tile...");
        }

        // Visualize the path for debugging
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Vector3 from = Grid.Instance.WorldPos(currentPath[i]);
            Vector3 to = Grid.Instance.WorldPos(currentPath[i + 1]);
            Debug.DrawLine(from, to, Color.red, 0.5f);
        }
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

    private Grid.Tile GetClosestBurgerTile()
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

    public bool AreBurgersLeft()
    {
        return burgerTiles.Count > 0;
    }

    public void TryRecalculatePathToTarget()
    {
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        Grid.Tile targetTile = GetClosestBurgerTile() ?? Grid.Instance.GetFinishTile();

        // Debugging path calculation
        Debug.Log($"Recalculating path from {startTile} to {targetTile}");

        currentPath = pathfinding.FindPath(startTile, targetTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            isWaitingForPath = true;
            Debug.Log("No path found. Kim is waiting...");
        }
        else
        {
            isWaitingForPath = false;
            currentPathIndex = 0;  // Reset path index
            Debug.Log($"Path recalculated. Number of tiles in path: {currentPath.Count}");

            // Log each tile for debugging
            foreach (var tile in currentPath)
            {
                Debug.Log($"Path tile: {Grid.Instance.WorldPos(tile)}");
            }

            // Start moving along the path
            MoveAlongPath();
        }
    }



    private void ResetDynamicOccupiedTiles()
    {
        foreach (var tile in dynamicOccupiedTiles)
        {
            tile.occupied = false;
        }
        dynamicOccupiedTiles.Clear();
    }

    public List<Grid.Tile> MarkAndVisualizeZombieZones()
    {
        List<Grid.Tile> zombieTiles = new List<Grid.Tile>();
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);

        isInDangerZone = false;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

                // Mark and visualize the occupied zone
                for (int x = -(int)occupiedZoneRadius; x <= (int)occupiedZoneRadius; x++)
                {
                    for (int y = -(int)occupiedZoneRadius; y <= (int)occupiedZoneRadius; y++)
                    {
                        Grid.Tile nearbyTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (nearbyTile != null && !nearbyTile.occupied)
                        {
                            nearbyTile.occupied = true;
                            dynamicOccupiedTiles.Add(nearbyTile);
                            Debug.DrawLine(Grid.Instance.WorldPos(nearbyTile), Grid.Instance.WorldPos(nearbyTile) + Vector3.up * 2, Color.red);
                        }
                    }
                }

                // Mark and visualize the danger zone
                for (int x = -(int)dangerZoneRadius; x <= (int)dangerZoneRadius; x++)
                {
                    for (int y = -(int)dangerZoneRadius; y <= (int)dangerZoneRadius; y++)
                    {
                        Grid.Tile dangerTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (dangerTile != null && Vector3.Distance(Grid.Instance.WorldPos(dangerTile), transform.position) <= dangerZoneRadius)
                        {
                            isInDangerZone = true;
                            Debug.DrawLine(Grid.Instance.WorldPos(dangerTile), Grid.Instance.WorldPos(dangerTile) + Vector3.up * 2, Color.yellow);
                        }
                    }
                }
            }
        }

        return zombieTiles;
    }
}
