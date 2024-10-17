using System.Collections.Generic;
using UnityEngine;

public class Pathfinding
{
    private Grid grid;

    public Pathfinding(Grid grid)
    {
        this.grid = grid;
    }

    public List<Grid.Tile> FindPath(Grid.Tile startTile, Grid.Tile targetTile)
    {
        // Create a dictionary to map Grid.Tile to PathNode
        Dictionary<Grid.Tile, PathNode> pathNodes = new Dictionary<Grid.Tile, PathNode>();

        // Initialize path nodes for every tile in the grid
        foreach (Grid.Tile tile in grid.GetTiles())
        {
            pathNodes[tile] = new PathNode(tile);
        }

        PathNode startNode = pathNodes[startTile];
        PathNode targetNode = pathNodes[targetTile];

        List<PathNode> openList = new List<PathNode> { startNode };
        HashSet<PathNode> closedList = new HashSet<PathNode>();

        startNode.gCost = 0;

        while (openList.Count > 0)
        {
            PathNode currentNode = GetNodeWithLowestFCost(openList);

            if (currentNode == targetNode)
            {
                Debug.Log("Path to target found.");
                return RetracePath(startNode, targetNode);
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            foreach (Grid.Tile neighborTile in GetNeighbors(currentNode.tile))
            {
                PathNode neighborNode = pathNodes[neighborTile];

                if (neighborTile.occupied || closedList.Contains(neighborNode))
                    continue;

                int newGCost = currentNode.gCost + GetDistance(currentNode.tile, neighborNode.tile);
                if (newGCost < neighborNode.gCost || !openList.Contains(neighborNode))
                {
                    neighborNode.gCost = newGCost;
                    neighborNode.hCost = GetDistance(neighborNode.tile, targetNode.tile);
                    neighborNode.parent = currentNode;

                    // Log the gCost, hCost, and fCost for debugging
                    Debug.Log("Tile: (" + neighborTile.x + "," + neighborTile.y + "), gCost: " + neighborNode.gCost + ", hCost: " + neighborNode.hCost + ", fCost: " + neighborNode.FCost);

                    if (!openList.Contains(neighborNode))
                        openList.Add(neighborNode);
                }
            }
        }

        Debug.LogError("No path found.");
        return null; // No path found
    }


    // Helper methods

    private PathNode GetNodeWithLowestFCost(List<PathNode> openList)
    {
        PathNode lowestFCostNode = openList[0];
        foreach (PathNode node in openList)
        {
            if (node.FCost < lowestFCostNode.FCost)
                lowestFCostNode = node;
        }
        return lowestFCostNode;
    }

    private List<Grid.Tile> GetNeighbors(Grid.Tile tile)
    {
        List<Grid.Tile> neighbors = new List<Grid.Tile>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int dir in directions)
        {
            Grid.Tile neighbor = grid.TryGetTile(new Vector2Int(tile.x + dir.x, tile.y + dir.y));
            if (neighbor != null)
                neighbors.Add(neighbor);
        }
        return neighbors;
    }

    private int GetDistance(Grid.Tile a, Grid.Tile b)
    {
        int distX = Mathf.Abs(a.x - b.x);
        int distY = Mathf.Abs(a.y - b.y);
        return distX + distY; // Manhattan distance
    }

    private List<Grid.Tile> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<Grid.Tile> path = new List<Grid.Tile>();
        PathNode currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode.tile);
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }
}
