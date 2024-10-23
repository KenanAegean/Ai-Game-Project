using System.Collections.Generic;
using UnityEngine;

public class OccupiedZones : MonoBehaviour
{
    private List<Grid.Tile> occupiedTiles = new List<Grid.Tile>();
    public float dangerRadius = 2.0f; // Define how large the occupied zone should be

    void Start()
    {
        InvokeRepeating(nameof(UpdateOccupiedZones), 0f, 2f); // Update occupied zones every 2 seconds
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
                    occupiedTiles.Add(tile);
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

    public List<Grid.Tile> GetOccupiedTiles()
    {
        return occupiedTiles;
    }

    // Visualize the occupied zones using Gizmos
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red; // Set the color of the gizmos

        // Find all zombies in the scene
        GameObject[] zombies = GameObject.FindGameObjectsWithTag("Zombie");

        foreach (GameObject zombie in zombies)
        {
            // Draw a sphere around the zombie to visualize its occupied zone
            Gizmos.DrawWireSphere(zombie.transform.position, dangerRadius);
        }
    }
}
