using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Structural/Level Data")]
public class LevelData : ScriptableObject
{
    // Static variable to pass data between scenes (Menu -> Game)
    // -1 indicates no specific level was selected (default start)
    public static int SelectedLevel = -1;
    
    // Store stars earned in the current session (LevelIndex -> StarCount)
    public static Dictionary<int, int> SessionStars = new Dictionary<int, int>();

    [Header("Grid Dimensions")]
    public int gridWidth = 10;
    public int gridHeight = 10;

    [Header("Level Config")]
    [Tooltip("Amount of material available for 7x7 beams (0.0049 area)")]
    public float limit7x7 = 20.0f;
    [Tooltip("Amount of material available for 5x5 beams (0.0025 area)")]
    public float limit5x5 = 20.0f;
    [Tooltip("Amount of material available for 3x3 beams (0.0009 area)")]
    public float limit3x3 = 20.0f;

    [Tooltip("Scale of the human character")]
    public float humanScale = 0.5f;

    [Tooltip("Scale of the anvil weight")]
    public float anvilScale = 0.8f;

    [Tooltip("Length of the rope holding the anvil")]
    public float anvilRopeLength = 1.5f;

    [Tooltip("Coordinates of the Anchor Nodes (Triangle supports)")]
    public List<Vector2Int> anchorCoords = new List<Vector2Int>();

    [Tooltip("Coordinate of the node that holds the weight")]
    public Vector2Int loadNodeCoords;

    [Tooltip("Mass of the weight")]
    public float loadMass = 10000f;

    [Header("Restricted Areas")]
    [Tooltip("Define rectangular areas where beams/nodes cannot exist. x,y is bottom-left, w,h is size.")]
    public List<RectInt> deadZones = new List<RectInt>();
    
    [Header("Pre-built Structure")]
    [Tooltip("List of beams that exist at start. defined by start and end node coords")]
    public List<BeamDef> prebuiltBeams = new List<BeamDef>();

    [Header("Star Rating Thresholds")]
    [Tooltip("Maximum total length of meters allowed to earn 3 Stars")]
    public float threeStarsLengthLimit = 30f;
    [Tooltip("Maximum total length of meters allowed to earn 2 Stars")]
    public float twoStarsLengthLimit = 50f;
    [Tooltip("Maximum total length of meters allowed to earn 1 Star")]
    public float oneStarLengthLimit = 100f;
}

[System.Serializable]
public struct BeamDef
{
    public Vector2Int start;
    public Vector2Int end;
}
