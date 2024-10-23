using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] private bool debugMode = false;
    [SerializeField] private float contextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;

    // Store burger and finish line positions
    public List<Grid.Tile> targets { get; private set; }
    public Grid.Tile finishTile { get; private set; }

    // Status flags
    public bool isWaitingForPath = false;
    public bool hasCollectedTargets = false;

    // Blackboard for the behavior tree
    private Blackboard blackboard = new Blackboard();

    // Behavior tree
    private BehaviorTree behaviorTree;
    private OccupiedZones occupiedZones;

    [SerializeField] private float pathRecalculationInterval = 2.0f; // Kim will recalculate her path every 2 seconds
    private float timeSinceLastRecalculation = 0f; // Time tracker for recalculating the path

    private Grid.Tile skippedTarget = null;


    public override void StartCharacter()
    {
        base.StartCharacter();

        // Initialize pathfinding and occupied zones
        pathfinding = new Pathfinding(Grid.Instance);
        occupiedZones = GameObject.FindObjectOfType<OccupiedZones>(); // Reference to the occupied zones

        // Store all burger positions at the start
        targets = GetAllBurgerTiles();
        finishTile = Grid.Instance.GetFinishTile();

        targets.Add(finishTile); // Always add the finish tile as the last target

        // Set up the blackboard with initial information
        blackboard.Set("burgers", targets);

        // Initialize the behavior tree
        behaviorTree = new BehaviorTree(
            new Selector(
                new Sequence(
                    new IsPathClear(),
                    new MoveToTarget(this),
                    new CheckAndSwitchTarget(this),
                    new IsPathClear(),
                    new MoveToTarget(this)
                ),
                new RetryMovementIfStuck(this),
                new IsPathClear(),
                new MoveToTarget(this)
            )
        );

        // Set the initial path to the nearest burger
        SetPathToTarget();
    }

    public override void UpdateCharacter()
    {
        // Update the path recalculation timer
        timeSinceLastRecalculation += Time.deltaTime;

        // Check if it's time to recalculate the path
        if (timeSinceLastRecalculation >= pathRecalculationInterval)
        {
            SetPathToTarget(); // Recalculate the path regularly
            timeSinceLastRecalculation = 0f; // Reset the timer
        }

        // Execute the behavior tree
        behaviorTree.Execute();

        base.UpdateCharacter();

        // Ensure Kim moves along the current path
        if (!isWaitingForPath && currentPath != null && currentPathIndex < currentPath.Count)
        {
            MoveAlongPath();
        }
    }

    // Helper function to compare two lists of danger zones
    private bool AreDangerZonesSame(List<Grid.Tile> lastDangerZones, List<Grid.Tile> currentDangerZones)
    {
        if (lastDangerZones.Count != currentDangerZones.Count)
        {
            return false;
        }

        for (int i = 0; i < lastDangerZones.Count; i++)
        {
            if (lastDangerZones[i] != currentDangerZones[i])
            {
                return false;
            }
        }

        return true;
    }

    public void SetPathToTarget()
    {
        if (targets.Count > 0)
        {
            // Find the closest target (could be a burger or finish tile)
            Grid.Tile closestTarget = GetClosestTarget();

            // If the closest target is the finish tile but there are burgers left, switch to the closest burger
            if (closestTarget == finishTile && AreBurgersLeft())
            {
                Debug.Log("Finish line is the closest, but burgers are left. Targeting the closest burger instead.");
                closestTarget = GetClosestBurgerTile(); // Get the closest burger
            }

            // Recalculate the path to the selected target
            RecalculatePath(closestTarget);
        }
        else
        {
            Debug.Log("All targets reached.");
        }
    }


    public void RecalculatePath(Grid.Tile targetTile = null)
    {
        if (targetTile == null)
        {
            Debug.LogError("No target specified for path recalculation.");
            return;
        }

        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);

        // Find the nearest tile on the current path to smooth the transition
        if (currentPath != null && currentPath.Count > 0)
        {
            startTile = FindClosestTileOnCurrentPath();
        }

        // Clear the current path before recalculating
        if (currentPath != null)
        {
            currentPath.Clear();  // Ensures no leftover path data is present
        }

        // Get the danger tiles from the occupied zones system
        List<Grid.Tile> dangerTiles = occupiedZones.GetOccupiedTiles();

        // Recalculate the path using dynamicOccupiedTiles to avoid the occupied zone
        currentPath = pathfinding.FindPath(startTile, targetTile);

        if (currentPath == null || currentPath.Count == 0)
        {
            isWaitingForPath = true;
            Debug.Log("No path available. Checking for next target...");

            // Switch to the next target
            SwitchToNextTarget();

        }
        else
        {
            isWaitingForPath = false;
            currentPathIndex = 0;

            // Log the recalculated path
            if (debugMode)
            {
                Debug.Log("Recalculated path:");
                foreach (var tile in currentPath)
                {
                    Debug.Log($"Tile at ({tile.x}, {tile.y})");
                }
            }

            // Set walk buffer using the newly calculated path
            SetWalkBuffer(currentPath);  // This will handle clearing the buffer
            Debug.Log("Kim's path recalculated. Starting movement.");
        }
    }

    private void SwitchToNextTarget()
    {
        // Remove the current target from the list (as Kim can't reach it)
        if (targets.Count > 0)
        {
            Grid.Tile currentTarget = targets[0]; // Get the current target
            targets.RemoveAt(0); // Remove the current target from the list

            // Keep track of the skipped burger if it's not the finish line
            if (currentTarget != finishTile)
            {
                skippedTarget = currentTarget;
            }
        }

        // Check if we have reached the finish line
        if (targets.Count > 0)
        {
            Grid.Tile nextTarget = targets[0];

            if (nextTarget == finishTile)
            {
                // If we have skipped a target, insert it before the finish line
                if (skippedTarget != null)
                {
                    Debug.Log("Adding skipped target back to the list before the finish line.");
                    targets.Insert(targets.Count - 1, skippedTarget); // Insert the skipped target before the finish line
                    skippedTarget = null; // Reset the skipped target
                }

                // Wait if the next target is the finish line and zombies are nearby
                Debug.Log("Next target is the finish line. Waiting for zombies to move away...");

                // Check if zombies are near the finish line
                List<Grid.Tile> dangerTiles = occupiedZones.GetOccupiedTiles();
                bool zombiesNearFinish = IsNearDangerZone(finishTile, dangerTiles);

                if (zombiesNearFinish)
                {
                    // Keep waiting and do not recalculate the path until zombies move away
                    isWaitingForPath = true;
                    return;
                }
                else
                {
                    // If no zombies are near the finish line, recalculate the path to the finish
                    Debug.Log("Zombies have moved away. Recalculating path to finish line.");
                    RecalculatePath(finishTile);
                }
            }
            else
            {
                // Switch to the next target (burger or finish line)
                Debug.Log("Switching to the next target.");
                RecalculatePath(nextTarget);
            }
        }
        else
        {
            Debug.Log("No more targets left.");
        }
    }

    private bool IsNearDangerZone(Grid.Tile targetTile, List<Grid.Tile> dangerZones)
    {
        foreach (Grid.Tile tile in dangerZones)
        {
            if (tile == targetTile)
            {
                return true; // The target tile is in a danger zone (zombies are nearby)
            }
        }
        return false;
    }



    // Helper function to find the closest tile on the current path to smooth transition
    private Grid.Tile FindClosestTileOnCurrentPath()
    {
        Grid.Tile closestTile = null;
        float shortestDistance = float.MaxValue;

        for (int i = currentPathIndex; i < currentPath.Count; i++)
        {
            Grid.Tile pathTile = currentPath[i];
            float distance = Vector3.Distance(transform.position, Grid.Instance.WorldPos(pathTile));

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestTile = pathTile;
            }
        }

        return closestTile != null ? closestTile : Grid.Instance.GetClosest(transform.position);
    }



    // Move along the current path
    public void MoveAlongPath()
    {
        //SetWalkBuffer(currentPath);
        //base.UpdateCharacter();
        if (currentPath == null)
        {
            Debug.LogWarning("No path available.");
            return;
        }

        if (currentPathIndex >= currentPath.Count)
        {
            Debug.LogWarning("Path completed.");
            return;
        }

        if (isWaitingForPath)
        {
            Debug.LogWarning("Kim is waiting for a new path.");
            return;
        }

        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        // Move towards the target
        Vector3 direction = (targetPosition - transform.position).normalized;
        //transform.position = Vector3.MoveTowards(transform.position, targetPosition, CharacterMoveSpeed * Time.deltaTime);
        //SetWalkBuffer(currentPath);

        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        if (distanceToTarget < ReachDistThreshold)
        {
            currentPathIndex++; // Move to next tile
            if (currentPathIndex >= currentPath.Count)
            {
                currentPath = null;
                Debug.Log("Path complete. No more tiles to follow.");
                return;
            }
        }

        // Visualize the path
        VisualizePath();
    }

    // Visualize the path
    private void VisualizePath()
    {
        if (currentPath != null)
        {
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 from = Grid.Instance.WorldPos(currentPath[i]);
                Vector3 to = Grid.Instance.WorldPos(currentPath[i + 1]);
                Debug.DrawLine(from, to, Color.red, 0.5f);
            }
        }
    }

    public bool AreBurgersLeft()
    {
        return targets != null && targets.Count > 1; // More than one means burgers are still left
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

    // Get the closest target (either a burger or the finish line)
    public Grid.Tile GetClosestTarget()
    {
        Grid.Tile closestTarget = null;
        float shortestDistance = float.MaxValue;

        foreach (Grid.Tile target in targets)
        {
            float distance = Vector3.Distance(transform.position, Grid.Instance.WorldPos(target));

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    // Get the closest burger tile (ignore the finish tile)
    public Grid.Tile GetClosestBurgerTile()
    {
        Grid.Tile closestBurgerTile = null;
        float shortestDistance = float.MaxValue;

        // Ignore the last item (which is the finish tile)
        for (int i = 0; i < targets.Count - 1; i++)
        {
            Grid.Tile burgerTile = targets[i];
            float distance = Vector3.Distance(transform.position, Grid.Instance.WorldPos(burgerTile));

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                closestBurgerTile = burgerTile;
            }
        }

        return closestBurgerTile;
    }

    public void CheckIfCollectedTarget()
    {
        Grid.Tile currentTile = Grid.Instance.GetClosest(transform.position);
        if (targets.Contains(currentTile))
        {
            targets.Remove(currentTile);
            hasCollectedTargets = true;
            Debug.Log("Kim collected a burger.");

            // Immediately recalculate path to next target
            SetPathToTarget();
        }
    }
}
