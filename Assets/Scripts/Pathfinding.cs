using System.Collections.Generic;
using UnityEngine;

public class Pathfinding
{
    private Grid grid;

    public Pathfinding(Grid grid)
    {
        this.grid = grid;
    }

    public List<Grid.Tile> FindPath(Grid.Tile startTile, Grid.Tile targetTile, bool avoidDanger = false, List<Grid.Tile> dangerTiles = null)
    {
        List<Grid.Tile> openSet = new List<Grid.Tile> { startTile };
        HashSet<Grid.Tile> closedSet = new HashSet<Grid.Tile>();

        Dictionary<Grid.Tile, Grid.Tile> cameFrom = new Dictionary<Grid.Tile, Grid.Tile>();
        Dictionary<Grid.Tile, int> gScore = new Dictionary<Grid.Tile, int>();
        Dictionary<Grid.Tile, int> fScore = new Dictionary<Grid.Tile, int>();

        gScore[startTile] = 0;
        fScore[startTile] = GetHeuristic(startTile, targetTile);

        while (openSet.Count > 0)
        {
            Grid.Tile currentTile = GetLowestFScoreTile(openSet, fScore);

            if (currentTile == targetTile)
            {
                return ReconstructPath(cameFrom, currentTile); // Found the path
            }

            openSet.Remove(currentTile);
            closedSet.Add(currentTile);

            foreach (Grid.Tile neighbor in GetNeighbors(currentTile))
            {
                if (closedSet.Contains(neighbor) || neighbor.occupied) // Skip occupied tiles or already processed ones
                {
                    continue;
                }

                // Avoid danger zones if enabled
                if (avoidDanger && IsTileInDangerZone(neighbor, dangerTiles))
                {
                    continue;  // Skip tiles that are in danger zones
                }

                int tentativeGScore = gScore[currentTile] + 1; // Distance between adjacent tiles is always 1

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = currentTile;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + GetHeuristic(neighbor, targetTile);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        Debug.LogError("No path found!");
        return null; // No path found
    }

    // Helper function to check if a tile is in danger
    private bool IsTileInDangerZone(Grid.Tile tile, List<Grid.Tile> dangerTiles)
    {
        if (dangerTiles == null)
        {
            return false;
        }

        return dangerTiles.Contains(tile);
    }






    // Get the neighbors of the current tile, restricted to up, down, left, and right
    private List<Grid.Tile> GetNeighbors(Grid.Tile tile)
    {
        List<Grid.Tile> neighbors = new List<Grid.Tile>();

        // Only allow movement in the four cardinal directions (up, down, left, right)
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int dir in directions)
        {
            Grid.Tile neighbor = Grid.Instance.TryGetTile(new Vector2Int(tile.x + dir.x, tile.y + dir.y));
            if (neighbor != null && !neighbor.occupied)
            {
                neighbors.Add(neighbor); // Add only unoccupied neighboring tiles
            }
        }

        return neighbors;
    }

    // Heuristic function (Manhattan distance for grid movement)
    private int GetHeuristic(Grid.Tile a, Grid.Tile b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan distance (no diagonals)
    }

    // Get the tile with the lowest F score
    private Grid.Tile GetLowestFScoreTile(List<Grid.Tile> openSet, Dictionary<Grid.Tile, int> fScore)
    {
        Grid.Tile lowestFScoreTile = openSet[0];
        int lowestFScore = fScore[lowestFScoreTile];

        foreach (Grid.Tile tile in openSet)
        {
            if (fScore.ContainsKey(tile) && fScore[tile] < lowestFScore)
            {
                lowestFScore = fScore[tile];
                lowestFScoreTile = tile;
            }
        }

        return lowestFScoreTile;
    }

    // Reconstruct the path by backtracking through the "cameFrom" map
    private List<Grid.Tile> ReconstructPath(Dictionary<Grid.Tile, Grid.Tile> cameFrom, Grid.Tile currentTile)
    {
        List<Grid.Tile> path = new List<Grid.Tile> { currentTile };

        while (cameFrom.ContainsKey(currentTile))
        {
            currentTile = cameFrom[currentTile];
            path.Insert(0, currentTile); // Insert the tile at the start of the list to reverse the path
        }

        return path;
    }
}
