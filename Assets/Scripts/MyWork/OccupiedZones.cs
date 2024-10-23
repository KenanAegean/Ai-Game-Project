using System.Collections.Generic;
using UnityEngine;

public class OccupiedZones : MonoBehaviour
{
    // Singleton pattern to access OccupiedZones globally
    public static OccupiedZones Instance { get; private set; }

    // List of occupied tiles
    private List<Grid.Tile> occupiedTiles = new List<Grid.Tile>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Add a tile to the occupied list
    public void MarkOccupied(Grid.Tile tile)
    {
        if (!occupiedTiles.Contains(tile))
        {
            occupiedTiles.Add(tile);
        }
    }

    // Clear all occupied tiles for the next frame
    public void ClearOccupiedZones()
    {
        occupiedTiles.Clear();
    }

    // Check if a tile is occupied
    public bool IsTileOccupied(Grid.Tile tile)
    {
        return occupiedTiles.Contains(tile);
    }

    // Get all occupied tiles (if needed)
    public List<Grid.Tile> GetOccupiedTiles()
    {
        return new List<Grid.Tile>(occupiedTiles);
    }
}
