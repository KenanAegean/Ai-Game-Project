using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private float occupiedZoneRadius = 2.0f;  // Radius for the occupied zone around zombies
    [SerializeField] private float dangerZoneRadius = 3.0f;    // Radius for the danger zone around zombies
    [SerializeField] float ContextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;

    // Store burger positions
    private List<Grid.Tile> burgerTiles;

    // Timer for recalculating path when Kim is in danger zone or waiting for path to clear
    private float pathCheckTimer = 1.0f; // Check every second
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

    protected const float ReachDistThreshold = 0.1f;
    protected const float CharacterMoveSpeed = 2.0f;

    public override void StartCharacter()
    {
        base.StartCharacter();

        // Pathfinding setup
        pathfinding = new Pathfinding(Grid.Instance);

        // Snap Kim to the nearest grid tile only once during start to avoid misalignment
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
                    new RecalculatePathAction(this)
                ),
                new Sequence(
                    new IsPathClear(blackboard),
                    new MoveToBurgerAction(this, blackboard)
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

        // Update the behavior tree every frame
        behaviorTree.Execute();

        // Mark and visualize zombie zones
        MarkAndVisualizeZombieZones();

        // Ensure Kim moves along the current path
        MoveAlongPath();  // This ensures Kim moves every frame if a path is valid
    }

    private void SetPathToClosestBurger()
    {
        // If there are no burgers left, set the path to the finish line
        if (burgerTiles.Count == 0)
        {
            Grid.Tile finishTile = Grid.Instance.GetFinishTile();
            RecalculatePathToTarget(finishTile);
            Debug.Log("All burgers collected. Heading to the finish line...");
            return;
        }

        // Set path to the closest burger
        Grid.Tile closestBurger = GetClosestBurgerTile();
        if (closestBurger != null)
        {
            RecalculatePathToTarget(closestBurger);
            Debug.Log("Path set to the closest burger.");
        }
    }


    public void MoveAlongPath()
    {
        // Do not move if no valid path or Kim is waiting
        if (currentPath == null || currentPathIndex >= currentPath.Count || isWaitingForPath)
        {
            return;
        }

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        // Log current and target positions for debugging
        Debug.Log($"Kim's current position: {transform.position}");
        Debug.Log($"Moving towards target position: {targetPosition}");

        Vector3 direction = (targetPosition - transform.position).normalized;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            direction = new Vector3(Mathf.Sign(direction.x), 0, 0);
        }
        else
        {
            direction = new Vector3(0, 0, Mathf.Sign(direction.z));
        }

        // Move Kim toward the target tile
        transform.position += direction * CharacterMoveSpeed * Time.deltaTime;

        // Log the distance to the target tile
        Debug.Log($"Distance to target tile: {Vector3.Distance(transform.position, targetPosition)}");

        if (Vector3.Distance(transform.position, targetPosition) < ReachDistThreshold)
        {
            transform.position = targetPosition;
            currentPathIndex++;
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



    private List<Grid.Tile> GetAllBurgerTiles()
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

    private void CheckAndCollectBurger()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);

        if (burgerTiles.Contains(currentTile))
        {
            // Remove the collected burger from the list
            burgerTiles.Remove(currentTile);

            // Recalculate path to the next burger or the finish line
            SetPathToClosestBurger();
            Debug.Log("Burger collected. Recalculating path to the next target...");
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

                // Mark and visualize the occupied zone (adjustable via Unity Inspector)
                for (int x = -(int)occupiedZoneRadius; x <= (int)occupiedZoneRadius; x++)
                {
                    for (int y = -(int)occupiedZoneRadius; y <= (int)occupiedZoneRadius; y++)
                    {
                        Grid.Tile nearbyTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (nearbyTile != null && !nearbyTile.occupied)
                        {
                            // Mark tile as occupied
                            nearbyTile.occupied = true;
                            dynamicOccupiedTiles.Add(nearbyTile); // Track dynamic occupied tiles

                            // Visualize the occupied zone (red)
                            Vector3 tilePosition = Grid.Instance.WorldPos(nearbyTile);
                            Debug.DrawLine(tilePosition, tilePosition + Vector3.up * 2, Color.red);
                        }
                    }
                }

                // Mark and visualize the danger zone (adjustable via Unity Inspector)
                for (int x = -(int)dangerZoneRadius; x <= (int)dangerZoneRadius; x++)
                {
                    for (int y = -(int)dangerZoneRadius; y <= (int)dangerZoneRadius; y++)
                    {
                        Grid.Tile dangerTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (dangerTile != null && Vector3.Distance(Grid.Instance.WorldPos(dangerTile), transform.position) <= dangerZoneRadius)
                        {
                            // Mark Kim as being in the danger zone
                            isInDangerZone = true;

                            // Visualize the danger zone (yellow)
                            Vector3 tilePosition = Grid.Instance.WorldPos(dangerTile);
                            Debug.DrawLine(tilePosition, tilePosition + Vector3.up * 2, Color.yellow);
                        }
                    }
                }
            }
        }

        return zombieTiles;
    }



    public void TryRecalculatePathToTarget()
    {
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        Grid.Tile targetTile = GetClosestBurgerTile() ?? Grid.Instance.GetFinishTile();

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

            // Log the path for debugging
            Debug.Log($"Path recalculated. Number of tiles in path: {currentPath.Count}");
            foreach (var tile in currentPath)
            {
                Debug.Log($"Tile: {Grid.Instance.WorldPos(tile)}");
            }
        }
    }





    private void ReattemptPathToTarget()
    {
        TryRecalculatePathToTarget();
    }

    private void RecalculatePathToTarget(Grid.Tile targetTile)
    {
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        currentPath = pathfinding.FindPath(startTile, targetTile);
        currentPathIndex = 0;

        if (currentPath != null && currentPath.Count > 0)
        {
            isWaitingForPath = false;
        }
        else
        {
            isWaitingForPath = true;
        }
    }

}
