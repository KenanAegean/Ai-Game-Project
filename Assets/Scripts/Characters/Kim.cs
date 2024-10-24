using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private bool debugMode = false;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;

    // Store burger and finish line positions
    public List<Grid.Tile> allBurgers { get; private set; }
    public List<Grid.Tile> remainingBurgers { get; private set; }
    public Grid.Tile finishTile { get; private set; }

    // Behavior tree
    private BehaviorTree behaviorTree;
    private OccupiedZones occupiedZones;

    // Keep track of blocked burgers
    private List<Grid.Tile> blockedBurgers = new List<Grid.Tile>();

    // Timer to periodically retry blocked burgers
    private float retryBlockedBurgersInterval = 2.0f; // in seconds
    private float retryBlockedBurgersTimer = 0.0f;

    public override void StartCharacter()
    {
        base.StartCharacter();

        // Ensure that myReachedTile is true at the start
        myReachedTile = true;

        // Initialize pathfinding and occupied zones
        pathfinding = new Pathfinding(Grid.Instance);
        occupiedZones = GameObject.FindObjectOfType<OccupiedZones>(); // Reference to the occupied zones

        // Store all burger positions at the start
        allBurgers = GetAllBurgerTiles();
        remainingBurgers = new List<Grid.Tile>(allBurgers);
        finishTile = Grid.Instance.GetFinishTile();

        // Initialize the behavior tree with adjusted structure
        behaviorTree = new BehaviorTree(
            new Selector(
                // Collect Burgers Sequence
                new Sequence(
                    new ConditionNode("AreBurgersLeft", AreBurgersLeft),
                    new Selector(
                        new Sequence(
                            new ActionNode("SetPathToAvailableBurger", SetPathToAvailableBurger),
                            new ConditionNode("IsPathAvailable", IsPathAvailable),
                            new ActionNode("MoveAlongPath", MoveAlongPath)
                        ),
                        new ActionNode("WaitOrRetryBlockedBurgers", WaitOrRetryBlockedBurgers)
                    )
                ),
                // Go to Finish Line Sequence
                new Sequence(
                    new ConditionNode("AllBurgersCollected", AllBurgersCollected),
                    new Selector(
                        new Sequence(
                            new ActionNode("SetPathToFinishLine", SetPathToFinishLine),
                            new ConditionNode("IsPathAvailable", IsPathAvailable),
                            new ActionNode("MoveAlongPath", MoveAlongPath)
                        ),
                        new ActionNode("Wait", Wait)
                    )
                )
            )
        );

        // Start moving by executing the behavior tree
        behaviorTree.Execute();
    }

    public override void UpdateCharacter()
    {
        // Update retry timer
        retryBlockedBurgersTimer += Time.deltaTime;

        // Execute the behavior tree
        behaviorTree.Execute();

        base.UpdateCharacter();

        // Check if Kim has collected the target (burger)
        CheckIfCollectedTarget();
    }

    // Behavior Tree Methods
    private bool AreBurgersLeft()
    {
        return remainingBurgers != null && remainingBurgers.Count > 0;
    }

    private bool AllBurgersCollected()
    {
        return remainingBurgers != null && remainingBurgers.Count == 0;
    }

    private Node.State SetPathToAvailableBurger()
    {
        Grid.Tile nextBurger = GetNextAvailableBurgerTile();
        if (nextBurger != null)
        {
            RecalculatePath(nextBurger);
            return Node.State.Success;
        }
        else
        {
            return Node.State.Failure;
        }
    }

    private Node.State WaitOrRetryBlockedBurgers()
    {
        // If the retry timer exceeds the interval, attempt to retry blocked burgers
        if (retryBlockedBurgersTimer >= retryBlockedBurgersInterval)
        {
            retryBlockedBurgersTimer = 0.0f;
            blockedBurgers.Clear(); // Clear blocked burgers to retry them

            Debug.Log("Retrying blocked burgers.");

            // Attempt to set a path to any available burger again
            Grid.Tile nextBurger = GetNextAvailableBurgerTile();
            if (nextBurger != null)
            {
                RecalculatePath(nextBurger);
                return Node.State.Running;
            }
            else
            {
                // Still no available burgers, Kim waits
                Debug.Log("No available burgers after retrying. Kim continues waiting.");
                return Node.State.Running;
            }
        }
        else
        {
            // Kim waits for the retry interval
            Debug.Log("Kim is waiting for blocked burgers to become available.");
            // Ensure Kim doesn't move
            myReachedTile = true;
            SetWalkBuffer(new List<Grid.Tile>());
            return Node.State.Running;
        }
    }

    private Node.State SetPathToFinishLine()
    {
        RecalculatePath(finishTile);
        return Node.State.Success;
    }

    private bool IsPathAvailable()
    {
        return currentPath != null && currentPath.Count > 0;
    }

    private Node.State MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            return Node.State.Failure;
        }

        // Movement is handled in base.UpdateCharacter(), which uses myWalkBuffer

        // Visualize the path
        VisualizePath();

        return Node.State.Running;
    }

    private Node.State Wait()
    {
        // Kim waits for zombies to move away
        Debug.Log("Kim is waiting for zombies to move away.");
        // Ensure Kim doesn't move
        myReachedTile = true;
        SetWalkBuffer(new List<Grid.Tile>());
        return Node.State.Running;
    }

    private Grid.Tile currentTarget = null;

    public void RecalculatePath(Grid.Tile targetTile = null)
    {
        if (targetTile == null)
        {
            Debug.LogError("No target specified for path recalculation.");
            return;
        }

        currentTarget = targetTile;

        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);

        // Recalculate the path to avoid the occupied zones
        currentPath = pathfinding.FindPath(startTile, targetTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.Log($"No path available to tile ({targetTile.x}, {targetTile.y}).");
            // Clear the walk buffer to stop movement
            SetWalkBuffer(new List<Grid.Tile>());
            myReachedTile = true;

            // Mark this burger as blocked if it's a burger
            if (remainingBurgers.Contains(targetTile) && !blockedBurgers.Contains(targetTile))
            {
                blockedBurgers.Add(targetTile);
            }
        }
        else
        {
            // Remove the starting tile if it's the same as the current tile
            if (Grid.Instance.IsSameTile(startTile, currentPath[0]))
            {
                currentPath.RemoveAt(0);
            }

            if (currentPath.Count == 0)
            {
                // No tiles to move to
                Debug.Log("No tiles to move to after removing the starting tile.");
                SetWalkBuffer(new List<Grid.Tile>());
                myReachedTile = true;
                return;
            }

            // Set walk buffer using the newly calculated path
            SetWalkBuffer(currentPath);  // This will handle clearing the buffer
            Debug.Log($"Kim's path recalculated to tile ({targetTile.x}, {targetTile.y}). Starting movement.");
            myReachedTile = true;

            // Log the recalculated path
            if (debugMode)
            {
                Debug.Log("Recalculated path:");
                foreach (var tile in currentPath)
                {
                    Debug.Log($"Tile at ({tile.x}, {tile.y})");
                }
            }
        }
    }

    public void CheckIfCollectedTarget()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);
        if (remainingBurgers.Contains(currentTile))
        {
            remainingBurgers.Remove(currentTile);
            blockedBurgers.Remove(currentTile);
            Debug.Log("Kim collected a burger.");

            // Immediately recalculate path to next target
            behaviorTree.Execute(); // Re-run the behavior tree to update the path
        }
    }

    public List<Grid.Tile> GetAllBurgerTiles()
    {
        List<Grid.Tile> targets = new List<Grid.Tile>();
        GameObject[] allBurgers = GameObject.FindGameObjectsWithTag("Burger");

        foreach (GameObject burger in allBurgers)
        {
            Grid.Tile burgerTile = Grid.Instance.GetClosest(burger.transform.position);
            if (burgerTile != null)
            {
                targets.Add(burgerTile);
            }
        }

        return targets;
    }

    // Get the next available burger tile
    public Grid.Tile GetNextAvailableBurgerTile()
    {
        foreach (var burgerTile in remainingBurgers)
        {
            // Skip blocked burgers
            if (blockedBurgers.Contains(burgerTile))
                continue;

            // Check if a path is available
            Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
            List<Grid.Tile> path = pathfinding.FindPath(startTile, burgerTile);

            if (path != null && path.Count > 0)
            {
                return burgerTile;
            }
            else
            {
                // Mark this burger as blocked
                if (!blockedBurgers.Contains(burgerTile))
                    blockedBurgers.Add(burgerTile);
            }
        }

        // No available burgers found
        return null;
    }

    // Visualize the path
    private void VisualizePath()
    {
        if (currentPath != null && currentPath.Count > 0)
        {
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 from = Grid.Instance.WorldPos(currentPath[i]);
                Vector3 to = Grid.Instance.WorldPos(currentPath[i + 1]);
                Debug.DrawLine(from, to, Color.red, 0.1f);
            }
        }
    }
}
