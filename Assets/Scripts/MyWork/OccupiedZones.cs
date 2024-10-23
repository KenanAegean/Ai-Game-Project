using System.Collections.Generic;
using UnityEngine;

public class OccupiedZones : MonoBehaviour
{
    private List<Grid.Tile> occupiedTiles = new List<Grid.Tile>();
    public float dangerRadius = 2.0f; // Define how large the occupied zone should be
    public float lineHeightMultiplier = 2.0f; // Multiplier to adjust line height dynamically

    void Start()
    {
        InvokeRepeating(nameof(UpdateOccupiedZones), 0f, 0.1f); // More frequent updates for dynamic response
    }

    // Call this method every few seconds to recalculate occupied zones
    void UpdateOccupiedZones()
    {
        occupiedTiles.Clear(); // Clear previous occupied zones

        // Find all zombies in the scene
        GameObject[] zombies = GameObject.FindGameObjectsWithTag("Zombie");

        foreach (GameObject zombie in zombies)
        {
            // Get the tile position of the zombie
            Grid.Tile zombieTile = Grid.Instance.GetClosest(zombie.transform.position);

            // Mark the surrounding tiles within the dangerRadius as occupied
            List<Grid.Tile> dangerTiles = GetTilesWithinRadius(zombieTile, dangerRadius);
            foreach (Grid.Tile tile in dangerTiles)
            {
                if (!tile.occupied)
                {
                    // Mark tile as occupied
                    occupiedTiles.Add(tile);
                    MarkZombieZone(tile, zombie.transform.position); // Visualize occupied tiles with dynamic lines
                }
            }
        }
    }

    // Utility method to get tiles within a radius
    List<Grid.Tile> GetTilesWithinRadius(Grid.Tile centerTile, float radius)
    {
        List<Grid.Tile> tilesInRange = new List<Grid.Tile>();
        int radiusInt = Mathf.CeilToInt(radius);

        for (int x = -radiusInt; x <= radiusInt; x++)
        {
            for (int y = -radiusInt; y <= radiusInt; y++)
            {
                Grid.Tile tile = Grid.Instance.TryGetTile(new Vector2Int(centerTile.x + x, centerTile.y + y));
                if (tile != null && !tile.occupied && Vector2.Distance(Grid.Instance.WorldPos(tile), Grid.Instance.WorldPos(centerTile)) <= radius)
                {
                    tilesInRange.Add(tile);
                }
            }
        }
        return tilesInRange;
    }

    // Visualize the occupied zones using Debug.DrawLine with dynamic height based on distance
    private void MarkZombieZone(Grid.Tile tile, Vector3 zombiePosition)
    {
        Vector3 tilePosition = Grid.Instance.WorldPos(tile);
        float distanceToZombie = Vector3.Distance(tilePosition, zombiePosition);

        // Dynamically adjust the height based on distance to the zombie
        float dynamicHeight = Mathf.Lerp(0.5f, lineHeightMultiplier, distanceToZombie / dangerRadius);

        // Draw a dynamic vertical red line based on the distance
        Debug.DrawLine(tilePosition, tilePosition + Vector3.up * dynamicHeight, Color.Lerp(Color.yellow, Color.red, distanceToZombie / dangerRadius), 0.1f); // 0.1f means this line updates every frame
    }

    public List<Grid.Tile> GetOccupiedTiles()
    {
        return occupiedTiles;
    }

}
