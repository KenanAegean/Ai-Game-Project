using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
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

        // Initialize the behavior tree
        behaviorTree = new BehaviorTree(
            new Selector(
                new Sequence(
                    new IsPathClear(),
                    new MoveToBurgerAction(this),
                    new CheckAndSwitchTarget(this)
                ),
                new RetryMovementIfStuck(this)
            )
        );

        // Set the initial path to the nearest burger and start moving
        SetPathToClosestBurger();
        MoveAlongPath();  // Start moving immediately after setting the path
    }

    public override void UpdateCharacter()
    {
        base.UpdateCharacter();
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
    public void RecalculatePath(Grid.Tile targetTile = null)
    {
        if (targetTile == null)
        {
            targetTile = GetClosestBurgerTile() ?? finishTile;
        }

        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
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

            Debug.Log("Recalculated path:");
            foreach (var tile in currentPath)
            {
                Debug.Log($"Tile at ({tile.x}, {tile.y})");
            }

            Debug.Log("Kim's path recalculated. Starting movement.");
        }
    }

    // Move along the current path
    public void MoveAlongPath()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count || isWaitingForPath)
            return;

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        Debug.Log($"Moving towards target position: {targetPosition}");

        Vector3 direction = (targetPosition - transform.position).normalized;

        if (direction.magnitude > 0.001f)
        {
            transform.position += direction * CharacterMoveSpeed * Time.deltaTime;
            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            if (distanceToTarget < 0.1f)
            {
                transform.position = targetPosition;
                currentPathIndex++;
                Debug.Log($"Reached tile {currentPathIndex}, moving to the next tile...");
            }
        }
        else
        {
            Debug.LogWarning("Movement direction is zero. No movement.");
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
            burgerTiles.Remove(currentTile);
            hasCollectedBurger = true;
            Debug.Log("Kim collected a burger.");
        }
    }
}
