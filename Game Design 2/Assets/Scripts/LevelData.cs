using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Structural/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Grid Dimensions")]
    public int gridWidth = 10;
    public int gridHeight = 10;

    [Header("Level Config")]
    [Tooltip("Amount of material available for this level (in meters)")]
    public float maxMaterialLength = 20.0f;

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
}

[System.Serializable]
public struct BeamDef
{
    public Vector2Int start;
    public Vector2Int end;
}
