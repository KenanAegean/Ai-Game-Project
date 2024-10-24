using System.Collections.Generic;
using UnityEngine;

public class OccupiedZones : MonoBehaviour
{
    private List<Grid.Tile> occupiedTiles = new List<Grid.Tile>();
    public float dangerRadius = 2.0f;
    public float lineHeightMultiplier = 2.0f;

    void Start()
    {
        InvokeRepeating(nameof(UpdateOccupiedZones), 0f, 0.1f);
    }

    void UpdateOccupiedZones()
    {
        occupiedTiles.Clear();

        GameObject[] zombies = GameObject.FindGameObjectsWithTag("Zombie");
        Debug.Log($"Number of zombies: {zombies.Length}");

        foreach (GameObject zombie in zombies)
        {
            Grid.Tile zombieTile = Grid.Instance.GetClosest(zombie.transform.position);

            // Mark the surrounding tiles as occupied
            List<Grid.Tile> dangerTiles = GetTilesWithinRadius(zombieTile, dangerRadius);
            Debug.Log($"Zombie at ({zombieTile.x}, {zombieTile.y}) occupies {dangerTiles.Count} tiles.");

            foreach (Grid.Tile tile in dangerTiles)
            {
                if (!tile.occupied && !occupiedTiles.Contains(tile))
                {
                    occupiedTiles.Add(tile);
                    MarkZombieZone(tile, zombie.transform.position);
                }
            }
        }

        Debug.Log($"Total occupied tiles: {occupiedTiles.Count}");
    }

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

    private void MarkZombieZone(Grid.Tile tile, Vector3 zombiePosition)
    {
        Vector3 tilePosition = Grid.Instance.WorldPos(tile);
        float distanceToZombie = Vector3.Distance(tilePosition, zombiePosition);
        float dynamicHeight = Mathf.Lerp(0.5f, lineHeightMultiplier, distanceToZombie / dangerRadius);

        Debug.DrawLine(tilePosition, tilePosition + Vector3.up * dynamicHeight, Color.Lerp(Color.yellow, Color.red, distanceToZombie / dangerRadius), 0.1f); 
    }

    public List<Grid.Tile> GetOccupiedTiles()
    {
        return occupiedTiles;
    }

}
