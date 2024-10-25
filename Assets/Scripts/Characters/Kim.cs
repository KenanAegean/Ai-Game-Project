using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private bool debugMode = false;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;

    public List<Grid.Tile> allBurgers { get; private set; }
    public List<Grid.Tile> remainingBurgers { get; private set; }
    public Grid.Tile finishTile { get; private set; }

    private BehaviorTree behaviorTree;
    private OccupiedZones occupiedZones;

    private List<Grid.Tile> blockedBurgers = new List<Grid.Tile>();

    private float retryBlockedBurgersInterval = 2.0f;
    private float retryBlockedBurgersTimer = 0.0f;

    public override void StartCharacter()
    {
        base.StartCharacter();

        myReachedTile = true;

        pathfinding = new Pathfinding(Grid.Instance);
        occupiedZones = GameObject.FindObjectOfType<OccupiedZones>(); // Reference to the occupied zones

        allBurgers = GetAllBurgerTiles();
        remainingBurgers = new List<Grid.Tile>(allBurgers);
        finishTile = Grid.Instance.GetFinishTile();

        // Initialize the behavior tree
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

        behaviorTree.Execute();
    }

    public override void UpdateCharacter()
    {
        retryBlockedBurgersTimer += Time.deltaTime;

        behaviorTree.Execute();

        base.UpdateCharacter();

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
            blockedBurgers.Clear();

            // Attempt to set a path to any available burger again
            Grid.Tile nextBurger = GetNextAvailableBurgerTile();
            if (nextBurger != null)
            {
                RecalculatePath(nextBurger);
                return Node.State.Running;
            }
            else
            {
                // No available burgers, Kim waits
                return Node.State.Running;
            }
        }
        else
        {
            // Kim waiting for blocked burgers to become available
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

        VisualizePath();

        return Node.State.Running;
    }

    private Node.State Wait()
    {
        // Kim waits for zombies to move away
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
            if (Grid.Instance.IsSameTile(startTile, currentPath[0]))
            {
                currentPath.RemoveAt(0);
            }

            if (currentPath.Count == 0)
            {
                // No tiles to move 
                SetWalkBuffer(new List<Grid.Tile>());
                myReachedTile = true;
                return;
            }

            SetWalkBuffer(currentPath);
            Debug.Log($"Kim's path recalculated to tile ({targetTile.x}, {targetTile.y}). Starting movement.");
            myReachedTile = true;

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

            behaviorTree.Execute();
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

    public Grid.Tile GetNextAvailableBurgerTile()
    {
        foreach (var burgerTile in remainingBurgers)
        {
            // Skip blocked burgers
            if (blockedBurgers.Contains(burgerTile))
                continue;

            // Check if a path available
            Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
            List<Grid.Tile> path = pathfinding.FindPath(startTile, burgerTile);

            if (path != null && path.Count > 0)
            {
                return burgerTile;
            }
            else
            {
                if (!blockedBurgers.Contains(burgerTile))
                    blockedBurgers.Add(burgerTile);
            }
        }

        // No available burgers
        return null;
    }

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
