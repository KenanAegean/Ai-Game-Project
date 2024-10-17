using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] float ContextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;
    private List<Grid.Tile> previousZombieTiles = new List<Grid.Tile>(); // Track previously marked zombie tiles

    protected const float ReachDistThreshold = 1.0f;
    protected const float CharacterMoveSpeed = 3.0f;

    public override void StartCharacter()
    {
        base.StartCharacter();
        pathfinding = new Pathfinding(Grid.Instance);

        // Snap Kim to the nearest grid tile
        transform.position = Grid.Instance.WorldPos(Grid.Instance.GetClosest(transform.position));

        // Initialize the first path to the finish line or burger
        Grid.Tile burgerTile = GetClosestBurgerTile();
        Grid.Tile targetTile = burgerTile != null ? burgerTile : Grid.Instance.GetFinishTile();

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
        previousZombieTiles = zombieTiles; // Save the currently marked tiles for the next reset

        // Check for burgers nearby
        Grid.Tile burgerTile = GetClosestBurgerTile();
        Grid.Tile targetTile = burgerTile != null ? burgerTile : Grid.Instance.GetFinishTile();

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
    }

    private void MoveAlongPath()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count)
            return;

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        // Log Kim's movement details
        Debug.Log("Kim position: " + transform.position + " | Target tile: " + currentPathIndex + " at position: " + targetPosition);

        // Visualize the path by drawing lines between the tiles in the path
        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Vector3 from = Grid.Instance.WorldPos(currentPath[i]);
            Vector3 to = Grid.Instance.WorldPos(currentPath[i + 1]);
            Debug.DrawLine(from, to, Color.red, 5f); // Draw a red line for 5 seconds between each tile
        }

        // Move toward the next tile
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * CharacterMoveSpeed);

        // Check if Kim has reached the current target tile, then move to the next tile
        if (Vector3.Distance(transform.position, targetPosition) < ReachDistThreshold)
        {
            Debug.Log("Kim reached tile " + currentPathIndex);
            currentPathIndex++;
        }
    }


    // Detect nearby zombies and mark a 3-tile radius around them as occupied
    // Detect nearby zombies and mark a 3-tile radius around them as occupied
    private List<Grid.Tile> GetTilesNearZombies()
    {
        List<Grid.Tile> zombieTiles = new List<Grid.Tile>();
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

                // Mark a 3-tile radius around the zombie as occupied
                for (int x = -3; x <= 3; x++)
                {
                    for (int y = -3; y <= 3; y++)
                    {
                        Grid.Tile nearbyTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (nearbyTile != null && !nearbyTile.occupied)
                        {
                            nearbyTile.occupied = true;
                            zombieTiles.Add(nearbyTile); // Correct capitalization
                            Debug.Log("Marking tile (" + nearbyTile.x + ", " + nearbyTile.y + ") as occupied.");
                        }
                    }
                }
            }
        }

        return zombieTiles;
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

    // Detect nearby burgers and choose the closest one
    private Grid.Tile GetClosestBurgerTile()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);
        Grid.Tile closestBurgerTile = null;
        float shortestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Burger"))
            {
                Grid.Tile burgerTile = Grid.Instance.GetClosest(hit.transform.position);
                float distance = Vector3.Distance(transform.position, Grid.Instance.WorldPos(burgerTile));

                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closestBurgerTile = burgerTile;
                }
            }
        }

        return closestBurgerTile;
    }
}
