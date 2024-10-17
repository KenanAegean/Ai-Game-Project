using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] float ContextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;

    protected const float ReachDistThreshold = 1.0f; // Increased threshold for testing
    protected const float CharacterMoveSpeed = 3.0f; // Increased speed for testing

    public override void StartCharacter()
    {
        base.StartCharacter();
        pathfinding = new Pathfinding(Grid.Instance);

        // Snap Kim to the nearest grid tile to avoid starting misalignment
        transform.position = Grid.Instance.WorldPos(Grid.Instance.GetClosest(transform.position));

        // Find the initial path to the finish line
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        Grid.Tile finishTile = Grid.Instance.GetFinishTile();
        currentPath = pathfinding.FindPath(startTile, finishTile);

        // Log starting position and path details
        Debug.Log("Kim's starting position: " + transform.position);
        Debug.Log("Target position (finish line): " + Grid.Instance.WorldPos(finishTile));

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

        // Log the distance and the current/target positions
        Debug.Log("Kim position: " + transform.position + " | Target position: " + targetPosition);
        Debug.Log("Distance to tile " + currentPathIndex + ": " + Vector3.Distance(transform.position, targetPosition));

        // Move toward the next tile
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * CharacterMoveSpeed);

        // Check if Kim is close enough to the target tile to move to the next
        if (Vector3.Distance(transform.position, targetPosition) < ReachDistThreshold)
        {
            Debug.Log("Reached tile " + currentPathIndex);
            currentPathIndex++;
        }
    }
}
