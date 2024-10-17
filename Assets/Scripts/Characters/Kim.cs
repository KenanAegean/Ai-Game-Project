using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] float ContextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;
    private List<Grid.Tile> previousZombieTiles = new List<Grid.Tile>();

    // New list to store burger positions at the start
    private List<Grid.Tile> burgerTiles;

    protected const float ReachDistThreshold = 0.1f;
    protected const float CharacterMoveSpeed = 2.0f;

    public override void StartCharacter()
    {
        base.StartCharacter();
        pathfinding = new Pathfinding(Grid.Instance);

        // Snap Kim to the nearest grid tile to avoid starting misalignment
        transform.position = Grid.Instance.WorldPos(Grid.Instance.GetClosest(transform.position));

        // Initialize burger tiles at the start of the game
        burgerTiles = GetAllBurgerTiles();

        // Get the path to the closest burger at the beginning
        Grid.Tile targetTile = GetClosestBurgerTile() ?? Grid.Instance.GetFinishTile();

        // Detect zombies and mark their tiles as occupied
        List<Grid.Tile> zombieTiles = GetTilesNearZombies();
        previousZombieTiles = zombieTiles;

        // Find the initial path
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        currentPath = pathfinding.FindPath(startTile, targetTile);

        // Log path details
        Debug.Log("Kim's starting position: " + transform.position);
        Debug.Log("Target position: " + Grid.Instance.WorldPos(targetTile));

        if (currentPath != null && currentPath.Count > 0)
        {
            Debug.Log("Path found. Number of tiles: " + currentPath.Count);
        }
        else
        {
            Debug.LogError("No path found!");
        }
    }

    public override void UpdateCharacter()
    {
        base.UpdateCharacter();

        // Reset previously marked zombie tiles
        ResetZombieTiles(previousZombieTiles);

        // Get the tiles near the current zombies and mark them
        List<Grid.Tile> zombieTiles = GetTilesNearZombies();
        previousZombieTiles = zombieTiles;

        // Always recalculate the path to the closest burger if there are still burgers left
        Grid.Tile targetTile = GetClosestBurgerTile() ?? Grid.Instance.GetFinishTile();

        // Recalculate path if needed
        Grid.Tile currentTargetTile = currentPath != null && currentPathIndex < currentPath.Count
            ? currentPath[currentPath.Count - 1]
            : null;

        if (currentTargetTile == null || targetTile != currentTargetTile)
        {
            Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
            currentPath = pathfinding.FindPath(startTile, targetTile);

            if (currentPath != null && currentPath.Count > 0)
            {
                Debug.Log("Recalculated path. Number of tiles: " + currentPath.Count);
            }
            else
            {
                Debug.LogError("No path found!");
            }
        }

        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            MoveAlongPath();
        }

        // Check if Kim reached a burger and update burger list
        CheckAndCollectBurger();
    }

    private void MoveAlongPath()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count)
            return;

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        // Calculate the direction to the target tile (restricted to grid axes)
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

        // Snap to the target tile once Kim is close enough
        if (Vector3.Distance(transform.position, targetPosition) < ReachDistThreshold)
        {
            transform.position = targetPosition;
            Debug.Log("Kim reached tile " + currentPathIndex);
            currentPathIndex++;
        }

        // Visualize the path
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Vector3 from = Grid.Instance.WorldPos(currentPath[i]);
            Vector3 to = Grid.Instance.WorldPos(currentPath[i + 1]);
            Debug.DrawLine(from, to, Color.red, 0.5f);
        }
    }

    // Store the positions of all burgers at the start of the game
    private List<Grid.Tile> GetAllBurgerTiles()
    {
        List<Grid.Tile> burgerTiles = new List<Grid.Tile>();
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Burger"))
            {
                Grid.Tile burgerTile = Grid.Instance.GetClosest(hit.transform.position);
                if (burgerTile != null)
                {
                    burgerTiles.Add(burgerTile);
                }
            }
        }

        return burgerTiles;
    }

    // Find the closest burger tile from the stored list of burger positions
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

    // Check if Kim has collected a burger, and if so, remove it from the list
    private void CheckAndCollectBurger()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);

        if (burgerTiles.Contains(currentTile))
        {
            burgerTiles.Remove(currentTile); // Remove the collected burger from the list
            Debug.Log("Burger collected at tile: " + currentTile.x + ", " + currentTile.y);
        }
    }

    // Reset previously marked zombie tiles
    private void ResetZombieTiles(List<Grid.Tile> zombieTiles)
    {
        foreach (var tile in zombieTiles)
        {
            tile.occupied = false;
            Debug.Log("Resetting tile (" + tile.x + ", " + tile.y + ") as unoccupied.");
        }
    }

    // Mark a 3-tile radius around zombies as occupied
    private List<Grid.Tile> GetTilesNearZombies()
    {
        List<Grid.Tile> zombieTiles = new List<Grid.Tile>();
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

                for (int x = -3; x <= 3; x++)
                {
                    for (int y = -3; y <= 3; y++)
                    {
                        Grid.Tile nearbyTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (nearbyTile != null && !nearbyTile.occupied)
                        {
                            nearbyTile.occupied = true;
                            zombieTiles.Add(nearbyTile);
                        }
                    }
                }
            }
        }

        return zombieTiles;
    }

    private void RecalculatePathToTarget(Grid.Tile targetTile)
    {
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        currentPath = pathfinding.FindPath(startTile, targetTile);

        if (currentPath != null && currentPath.Count > 0)
        {
            Debug.Log("Recalculated path. Number of tiles: " + currentPath.Count);
        }
        else
        {
            Debug.LogError("No path found!");
        }
    }
}
