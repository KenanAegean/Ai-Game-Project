using System.Collections.Generic;
using UnityEngine;

public class Kim : CharacterController
{
    [SerializeField] float ContextRadius;
    private Pathfinding pathfinding;
    private List<Grid.Tile> currentPath;
    private int currentPathIndex = 0;

    public override void StartCharacter()
    {
        base.StartCharacter();
        pathfinding = new Pathfinding(Grid.Instance);

        // Find the initial path to the finish line
        Grid.Tile startTile = Grid.Instance.GetClosest(transform.position);
        Grid.Tile finishTile = Grid.Instance.GetFinishTile();
        currentPath = pathfinding.FindPath(startTile, finishTile);
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
        Grid.Tile targetTile = currentPath[currentPathIndex];
        Vector3 targetPosition = Grid.Instance.WorldPos(targetTile);

        // Move toward the next tile
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * CharacterMoveSpeed);

        if (Vector3.Distance(transform.position, targetPosition) < ReachDistThreshold)
        {
            currentPathIndex++;
        }
    }
}
