using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] float ContextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;

    // Store burger positions
    private List<Grid.Tile> burgerTiles;

    // Timer for recalculating path when Kim is in danger zone or waiting for path to clear
    private float pathCheckTimer = 1.0f; // Check every second
    private float timeSinceLastCheck = 0f;

    // Status flag for being in the danger zone
    private bool isInDangerZone = false;
    private bool wasInDangerZoneLastFrame = false; // Track previous state
    private bool isWaitingForPath = false; // Track waiting for path state

    // Store dynamically marked tiles (for resetting)
    private List<Grid.Tile> dynamicOccupiedTiles = new List<Grid.Tile>();

    protected const float ReachDistThreshold = 0.1f;
    protected const float CharacterMoveSpeed = 2.0f;

    public override void StartCharacter()
    {
        base.StartCharacter();
        pathfinding = new Pathfinding(Grid.Instance);

        // Snap Kim to the nearest grid tile to avoid starting misalignment
        transform.position = Grid.Instance.WorldPos(Grid.Instance.GetClosest(transform.position));

        // Get all burger positions at the start
        burgerTiles = GetAllBurgerTiles();

        if (burgerTiles.Count == 0)
        {
            Debug.LogError("No burgers found!");
            return;
        }

        // Set the initial path to the nearest burger
        SetPathToClosestBurger();

        Debug.Log("Kim's starting position: " + transform.position);
    }

    public override void UpdateCharacter()
    {
        base.UpdateCharacter();

        // Clear previous dynamic occupied zones
        ResetDynamicOccupiedTiles();

        // Check for danger zone recalculation or path recheck every second
        timeSinceLastCheck += Time.deltaTime;
        if (timeSinceLastCheck >= pathCheckTimer)
        {
            timeSinceLastCheck = 0f; // Reset the timer

            // Check if Kim is waiting for a path to clear
            if (isWaitingForPath)
            {
                Debug.Log("Kim is waiting for a path to clear...");
                ReattemptPathToTarget(); // Retry pathfinding
            }
            else
            {
                // Check if Kim is inside a danger zone
                List<Grid.Tile> zombieTiles = MarkDynamicZombieZones();

                if (isInDangerZone && !wasInDangerZoneLastFrame)
                {
                    Debug.Log("Kim entered the danger zone. Recalculating path.");
                    TryRecalculatePathToTarget(); // Recalculate path immediately on entering
                    wasInDangerZoneLastFrame = true; // Set state as just entered the danger zone
                }
                else if (isInDangerZone && wasInDangerZoneLastFrame)
                {
                    Debug.Log("Kim is still in the danger zone. Recalculating path again.");
                    TryRecalculatePathToTarget(); // Recalculate path every second while in danger zone
                }
                else if (!isInDangerZone && wasInDangerZoneLastFrame)
                {
                    Debug.Log("Kim exited the danger zone.");
                    wasInDangerZoneLastFrame = false; // Reset the danger zone flag
                }
            }
        }

        // Move Kim along the current path if a valid path is found
        if (currentPath != null && currentPathIndex < currentPath.Count && !isWaitingForPath)
        {
            MoveAlongPath();
        }

        // Check if Kim has reached a burger
        CheckAndCollectBurger();

        // Visualize occupied and danger zones
        VisualizeZombieZones();
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

    // Set path to the closest burger or finish line if no burgers are left
    private void SetPathToClosestBurger()
    {
        // If there are no burgers left, set the path to the finish line
        if (burgerTiles.Count == 0)
        {
            Grid.Tile finishTile = Grid.Instance.GetFinishTile();
            RecalculatePathToTarget(finishTile);
            return;
        }

        // Set path to the closest burger
        Grid.Tile closestBurger = GetClosestBurgerTile();
        if (closestBurger != null)
        {
            RecalculatePathToTarget(closestBurger);
        }
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

    // Check if Kim has collected a burger, and if so, remove it from the list and set path to the next burger or finish line
    private void CheckAndCollectBurger()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);

        if (burgerTiles.Contains(currentTile))
        {
            // Remove the collected burger from the list
            burgerTiles.Remove(currentTile);
            SetPathToClosestBurger();
        }
    }

    // Reset previously marked dynamic occupied tiles
    private void ResetDynamicOccupiedTiles()
    {
        foreach (var tile in dynamicOccupiedTiles)
        {
            tile.occupied = false; // Clear dynamic occupied status
        }
        dynamicOccupiedTiles.Clear(); // Clear the list for next update
    }

    // Mark a 2-tile radius around zombies as occupied and a 4-tile radius as the danger zone dynamically
    private List<Grid.Tile> MarkDynamicZombieZones()
    {
        List<Grid.Tile> zombieTiles = new List<Grid.Tile>();
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);

        isInDangerZone = false; // Reset danger zone flag

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

                // Mark occupied zone (2 tiles around zombie)
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        Grid.Tile nearbyTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (nearbyTile != null && !nearbyTile.occupied)
                        {
                            nearbyTile.occupied = true;
                            dynamicOccupiedTiles.Add(nearbyTile); // Track dynamic occupied tiles
                        }
                    }
                }

                // Mark danger zone (4 tiles around zombie)
                for (int x = -4; x <= 4; x++)
                {
                    for (int y = -4; y <= 4; y++)
                    {
                        Grid.Tile dangerTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (dangerTile != null && Vector3.Distance(Grid.Instance.WorldPos(dangerTile), transform.position) <= 4)
                        {
                            isInDangerZone = true; // Kim is in the danger zone
                        }
                    }
                }
            }
        }

        return zombieTiles;
    }

    // Try to recalculate the path to the target
    private void TryRecalculatePathToTarget()
    {
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        Grid.Tile targetTile = GetClosestBurgerTile() ?? Grid.Instance.GetFinishTile();

        currentPath = pathfinding.FindPath(startTile, targetTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogError("No path found! Waiting for path to clear...");
            isWaitingForPath = true; // Set waiting flag if no path is found
        }
        else
        {
            isWaitingForPath = false; // Clear waiting flag if path is found
            currentPathIndex = 0; // Reset the path index
        }
    }

    // Retry pathfinding if Kim is waiting for a clear path
    private void ReattemptPathToTarget()
    {
        Debug.Log("Reattempting path to target...");
        TryRecalculatePathToTarget();
    }

    // Recalculate the path to the given target
    private void RecalculatePathToTarget(Grid.Tile targetTile)
    {
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        currentPath = pathfinding.FindPath(startTile, targetTile);
        currentPathIndex = 0; // Reset the path index

        if (currentPath != null && currentPath.Count > 0)
        {
            Debug.Log("Recalculated path to target. Number of tiles: " + currentPath.Count);
            isWaitingForPath = false; // Clear waiting flag
        }
        else
        {
            Debug.LogError("No path found!");
            isWaitingForPath = true; // Set waiting flag if no path is found
        }
    }

    // Visualize the zones (Occupied: Red, Danger: Yellow)
    private void VisualizeZombieZones()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

                // Visualize the occupied zone (Red, 2 tiles)
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        Grid.Tile occupiedTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (occupiedTile != null)
                        {
                            Vector3 tilePosition = Grid.Instance.WorldPos(occupiedTile);
                            Debug.DrawLine(tilePosition, tilePosition + Vector3.up * 2, Color.red);
                        }
                    }
                }

                // Visualize the danger zone (Yellow, 4 tiles)
                for (int x = -4; x <= 4; x++)
                {
                    for (int y = -4; y <= 4; y++)
                    {
                        Grid.Tile dangerTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (dangerTile != null)
                        {
                            Vector3 tilePosition = Grid.Instance.WorldPos(dangerTile);
                            Debug.DrawLine(tilePosition, tilePosition + Vector3.up * 2, Color.yellow);
                        }
                    }
                }
            }
        }
    }
}
