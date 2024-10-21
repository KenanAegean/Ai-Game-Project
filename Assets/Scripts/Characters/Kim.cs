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

    // Status flags
    public bool isInDangerZone = false;
    private bool wasInDangerZoneLastFrame = false;
    private bool isWaitingForPath = false;

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

        // Visualize occupied and danger zones
        VisualizeZombieZones();
    }

    private void SetPathToClosestBurger()
    {
        if (burgerTiles.Count == 0)
        {
            Grid.Tile finishTile = Grid.Instance.GetFinishTile();
            RecalculatePathToTarget(finishTile);
            return;
        }

        Grid.Tile closestBurger = GetClosestBurgerTile();
        if (closestBurger != null)
        {
            RecalculatePathToTarget(closestBurger);
        }
    }

    public void MoveAlongPath()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count)
            return;

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        Vector3 direction = (targetPosition - transform.position).normalized;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            direction = new Vector3(Mathf.Sign(direction.x), 0, 0);
        }
        else
        {
            direction = new Vector3(0, 0, Mathf.Sign(direction.z));
        }

        transform.position += direction * CharacterMoveSpeed * Time.deltaTime;

        if (Vector3.Distance(transform.position, targetPosition) < ReachDistThreshold)
        {
            transform.position = targetPosition;
            currentPathIndex++;
        }

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
            burgerTiles.Remove(currentTile);
            SetPathToClosestBurger();
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

    public List<Grid.Tile> MarkDynamicZombieZones()
    {
        List<Grid.Tile> zombieTiles = new List<Grid.Tile>();
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);

        isInDangerZone = false;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        Grid.Tile nearbyTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (nearbyTile != null && !nearbyTile.occupied)
                        {
                            nearbyTile.occupied = true;
                            dynamicOccupiedTiles.Add(nearbyTile);
                        }
                    }
                }

                for (int x = -3; x <= 3; x++)
                {
                    for (int y = -3; y <= 3; y++)
                    {
                        Grid.Tile dangerTile = Grid.Instance.TryGetTile(new Vector2Int(zombieTile.x + x, zombieTile.y + y));
                        if (dangerTile != null && Vector3.Distance(Grid.Instance.WorldPos(dangerTile), transform.position) <= 4)
                        {
                            isInDangerZone = true;
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
        }
        else
        {
            isWaitingForPath = false;
            currentPathIndex = 0;
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

    private void VisualizeZombieZones()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, ContextRadius);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Zombie"))
            {
                Grid.Tile zombieTile = Grid.Instance.GetClosest(hit.transform.position);

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
