public class PathNode
{
    public Grid.Tile tile;
    public int gCost; // Cost from the start tile to this tile
    public int hCost; // Heuristic cost from this tile to the target
    public PathNode parent; // Reference to the previous node in the path

    public int FCost
    {
        get { return gCost + hCost; }
    }

    public PathNode(Grid.Tile tile)
    {
        this.tile = tile;
        gCost = int.MaxValue;
        hCost = 0;
        parent = null;
    }
}
