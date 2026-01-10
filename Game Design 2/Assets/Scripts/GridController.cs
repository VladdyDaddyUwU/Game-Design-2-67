using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridController : MonoBehaviour
{
    [Header("Level Configuration")]
    public LevelData currentLevel;

    [Header("Component References")]
    public GameManager gameManager;

    [Header("Grid Settings")]
    public int minSquares = 10;
    public bool fitToScreen = true;
    public int destroyWidth = 0;
    public int destroyHeight = 0;
    public float gridUpShiftFactor = 1.0f;

    [Header("Visuals")]
    public GameObject nodePrefab; // Assign a circle sprite prefab with a Collider2D and the Node.cs script
    public float nodeScale = 0.1f;
    public bool autoInitializeGameManager = true;

    private Vector2[,] intersectionPoints;
    private Mesh gridMesh;
    
    // Runtime grid state
    private int gridWidth;
    private int gridHeight;
    private List<GameObject> nodeObjects = new List<GameObject>();
    private List<Node> nodeComponents = new List<Node>();
    private Transform nodeHolder;
    private bool isDirty = true;

    // Locked grid parameters to ensure stability across repairs/reloads
    public float lockedCellSize = 1f;
    [SerializeField, HideInInspector] private float lockedXOffset = 0f;
    [SerializeField, HideInInspector] private float lockedYOffset = 0f;
    [SerializeField, HideInInspector] private int lockedGridWidth = 0;
    [SerializeField, HideInInspector] private int lockedGridHeight = 0;
    [SerializeField, HideInInspector] private bool initializedInPlayMode = false;

    [System.Serializable]
    public struct GridEdge
    {
        public Vector2 start;
        public Vector2 end;
        public GridEdge(Vector2 start, Vector2 end)
        {
            this.start = start;
            this.end = end;
        }
    }
    public List<GridEdge> gridEdges = new List<GridEdge>();

    void Awake()
    {
        // Safety Check: Detect duplicate instance on GameManager
        if (gameObject.name == "GameManager")
        {
            Debug.LogError("GridController is incorrectly attached to 'GameManager'. It should only be on 'GridManager'. Destroying this duplicate component.");
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
            return;
        }

        GetComponent<MeshFilter>().mesh = gridMesh = new Mesh();
        // Force a regeneration on startup to sync state, but respect Play Mode lock if already set
        isDirty = true;
        
        // If we are entering play mode or starting up, check if we need to reset the lock
        if (!Application.isPlaying)
        {
            initializedInPlayMode = false;
        }
    }

    public void LoadLevel(LevelData level)
    {
        currentLevel = level;
        // Reset play mode initialization so we recalculate for the new level dimensions
        initializedInPlayMode = false;
        isDirty = true;
    }

    void Update()
    {
        if (isDirty)
        {
            GenerateGrid();
            isDirty = false;
        }
    }

    void OnValidate()
    {
        isDirty = true;
    }

    bool IsCellDestroyed(int x, int y)
    {
        // 1. Level Data Priority
        if (currentLevel != null)
        {
            if (currentLevel.deadZones != null)
            {
                foreach (var zone in currentLevel.deadZones)
                {
                    // Check if x,y is inside the rectangle
                    if (x >= zone.x && x < zone.x + zone.width &&
                        y >= zone.y && y < zone.y + zone.height)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // 2. Legacy Fallback
        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
        {
            return false;
        }
        return x >= gridWidth - destroyWidth && y < destroyHeight;
    }

    bool IsNodeInsideDeadZone(int x, int y)
    {
        if (currentLevel != null && currentLevel.deadZones != null)
        {
            foreach (var zone in currentLevel.deadZones)
            {
                if (x > zone.x && x < zone.x + zone.width &&
                    y > zone.y && y < zone.y + zone.height)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void GenerateGrid()
    {
        if (nodeHolder == null)
        {
            // Robustly find or create NodeHolder as a child
            Transform existingHolder = transform.Find("NodeHolder");
            if (existingHolder != null)
            {
                nodeHolder = existingHolder;
            }
            else
            {
                GameObject holder = new GameObject("NodeHolder");
                holder.transform.SetParent(this.transform);
                holder.transform.localPosition = Vector3.zero; // Ensure local position is zero
                holder.transform.localScale = Vector3.one;
                nodeHolder = holder.transform;
            }
        }

        // Cleanup existing nodes
        // IMPORTANT: Disable them first to immediately remove from Physics/Raycasts
        for (int i = nodeHolder.childCount - 1; i >= 0; i--)
        {
            Transform child = nodeHolder.GetChild(i);
            child.gameObject.SetActive(false); 
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
        nodeObjects.Clear();
        nodeComponents.Clear();

        // Calculate or Restore Grid Dimensions
        bool shouldRecalculate = true;
        if (Application.isPlaying && initializedInPlayMode && lockedGridWidth > 0)
        {
            shouldRecalculate = false;
        }

        if (shouldRecalculate)
        {
            lockedCellSize = 1f;
            lockedXOffset = 0;
            lockedYOffset = 0;
            
            // Determine Target Dimensions
            int targetW = minSquares;
            int targetH = minSquares;
            
            if (currentLevel != null)
            {
                targetW = currentLevel.gridWidth;
                targetH = currentLevel.gridHeight;
            }

            if (fitToScreen && Camera.main != null)
            {
                float totalScreenHeight_World = Camera.main.orthographicSize * 2;
                float totalScreenWidth_World = totalScreenHeight_World * Camera.main.aspect;
                float screenWidth_Pixels = Screen.width;
                float screenHeight_Pixels = Screen.height;
                float smallerDimension_Pixels = Mathf.Min(screenWidth_Pixels, screenHeight_Pixels);
                
                // Restore the specific spacing formula
                float cutAmount_Pixels = smallerDimension_Pixels / 4f; 
                float pixelsPerWorldUnit = screenHeight_Pixels / totalScreenHeight_World;
                float cutAmount_World = cutAmount_Pixels / pixelsPerWorldUnit;
                
                // Calculate available space respecting the start offset (cutAmount)
                float availableWidth_World = totalScreenWidth_World - cutAmount_World;
                // For height, we subtract the bottom offset. We also might want a bit of top margin, 
                // but the original formula effectively used (Total - Cut) as the max dimension.
                // We use the shift factor for the offset, but the available space is usually Total - Offset.
                // Let's stick to the previous available space logic: Total - Cut.
                float availableHeight_World = totalScreenHeight_World - (cutAmount_World * gridUpShiftFactor); 
                // Note: Original code used 'totalScreenHeight_World - cutAmount_World' for calculation, 
                // but offset by 'cutAmount_World * gridUpShiftFactor'. 
                // To ensure it stays on screen: Available = Total - BottomOffset. 
                
                // Let's stick EXACTLY to the previous logic for available space to be safe, 
                // assuming the user wants to fit it into that "Total - Cut" box.
                float availableHeight_ForCalc = totalScreenHeight_World - cutAmount_World;

                // Calculate Cell Size to preserve Square Aspect Ratio
                float sizeX = availableWidth_World / targetW;
                float sizeY = availableHeight_ForCalc / targetH;
                
                // Use the smaller size to ensure it fits both dimensions
                lockedCellSize = Mathf.Min(sizeX, sizeY);
                
                gridWidth = targetW;
                gridHeight = targetH;
                
                // Position logic using the restored spacing formulas
                lockedXOffset = -totalScreenWidth_World / 2f + cutAmount_World;
                lockedYOffset = -totalScreenHeight_World / 2f + (cutAmount_World * gridUpShiftFactor);
            }
            else
            {
                gridWidth = targetW;
                gridHeight = targetH;
            }

            lockedGridWidth = gridWidth;
            lockedGridHeight = gridHeight;

            if (Application.isPlaying)
            {
                initializedInPlayMode = true;
            }
        }
        else
        {
            // Restore from locked state
            gridWidth = lockedGridWidth;
            gridHeight = lockedGridHeight;
        }


        intersectionPoints = new Vector2[gridWidth + 1, gridHeight + 1];
        List<Vector3> vertices = new List<Vector3>();

        // Generate Mesh and Nodes
        for (int y = 0; y <= gridHeight; y++)
        {
            for (int x = 0; x <= gridWidth; x++)
            {
                float xPos = x * lockedCellSize + lockedXOffset;
                float yPos = y * lockedCellSize + lockedYOffset;
                intersectionPoints[x, y] = new Vector2(xPos, yPos);
                vertices.Add(new Vector3(xPos, yPos, 0));

                // Check if node falls within the "Destroy Zone" (Legacy Fallback)
                if (currentLevel == null)
                {
                    // We strictly keep the "Left Wall" (x = gridWidth - destroyWidth)
                    // We strictly keep the "Ceiling" (y = destroyHeight)
                    // We remove everything else inside (x > start) and below (y < height)
                    int startDestroyX = gridWidth - destroyWidth;
                    bool inDestroyX = x > startDestroyX;
                    bool inDestroyY = y < destroyHeight;

                    if (inDestroyX && inDestroyY)
                    {
                        continue;
                    }
                }
                
                // Check new Dead Zone Logic (Nodes strictly inside should not be created)
                if (IsNodeInsideDeadZone(x, y))
                {
                    continue;
                }

                if (nodePrefab != null)
                {
                    GameObject nodeObj = Instantiate(nodePrefab, new Vector3(xPos, yPos, 0), Quaternion.identity, nodeHolder);
                    nodeObj.transform.localScale = Vector3.one * nodeScale;
                    
                    // Spatial ID Assignment: Robust against holes/skips
                    int spatialID = y * (gridWidth + 1) + x;
                    nodeObj.name = $"Node_{spatialID}({x},{y})";
                    
                    Node nodeComp = nodeObj.GetComponent<Node>();
                    if (nodeComp == null) nodeComp = nodeObj.AddComponent<Node>();
                    
                    nodeComp.x_index = x;
                    nodeComp.y_index = y;
                    nodeComp.id = spatialID;
                    
                    nodeObjects.Add(nodeObj);
                    nodeComponents.Add(nodeComp);
                }
            }
        }

        // Generate Edges
        List<int> indices = new List<int>();
        gridEdges.Clear();
        int rowStride = gridWidth + 1; // Used for vertex index calculation

        for (int y = 0; y <= gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (!(IsCellDestroyed(x, y - 1) && IsCellDestroyed(x, y)))
                {
                    int startVertex = y * rowStride + x;
                    indices.Add(startVertex);
                    indices.Add(startVertex + 1);
                    gridEdges.Add(new GridEdge(intersectionPoints[x, y], intersectionPoints[x + 1, y]));
                }
            }
        }
        for (int x = 0; x <= gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (!(IsCellDestroyed(x - 1, y) && IsCellDestroyed(x, y)))
                {
                    int startVertex = y * rowStride + x;
                    indices.Add(startVertex);
                    indices.Add(startVertex + rowStride);
                    gridEdges.Add(new GridEdge(intersectionPoints[x, y], intersectionPoints[x, y + 1]));
                }
            }
        }
        gridMesh.Clear();
        gridMesh.vertices = vertices.ToArray();
        gridMesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);

        if (gameManager != null && autoInitializeGameManager)
        {
            gameManager.InitializeStructure(this);
        }
    }

    public Vector2[,] GetIntersectionPoints() => intersectionPoints;
    public List<GridEdge> GetGridEdges() => gridEdges;
    public List<Node> GetNodes()
    {
        if (nodeComponents == null || nodeComponents.Count == 0 || nodeComponents.Exists(n => n == null))
        {
             // Fallback: Fetch from children if internal list is stale or empty
             if (nodeHolder != null)
             {
                 nodeComponents = new List<Node>(nodeHolder.GetComponentsInChildren<Node>());
             }
        }
        else 
        {
            nodeComponents.RemoveAll(n => n == null);
        }
        return nodeComponents;
    }
    public int GetGridWidth() => gridWidth;
    public int GetGridHeight() => gridHeight;

    public int GetTargetNodeID()
    {
        // 1. Level Data Priority
                    if (currentLevel != null)
                    {
                        int tX = currentLevel.loadNodeCoords.x;
                        int tY = currentLevel.loadNodeCoords.y;
                        // Map coords to ID
                        int levelRowStride = gridWidth + 1;
                        return tY * levelRowStride + tX;
                    }
        // 2. Fallback Logic
        // Logic: Middle of destroy zone width, immediately above destroy zone height
        // Since destroy zone is at the far right:
        // x start = gridWidth - destroyWidth
        // x center = x start + (destroyWidth / 2)
        
        int targetX = gridWidth - (destroyWidth / 2);
        // Ensure it stays within bounds
        if (targetX > gridWidth) targetX = gridWidth;
        
        int targetY = destroyHeight; 
        
        // Calculate ID
        int rowStride = gridWidth + 1;
        return targetY * rowStride + targetX;
    }

    public Vector2 GetTargetNodePosition()
    {
        if (currentLevel != null)
        {
            return GetNodePosition(currentLevel.loadNodeCoords.x, currentLevel.loadNodeCoords.y);
        }

        int targetX = gridWidth - (destroyWidth / 2);
        int targetY = destroyHeight;
        return GetNodePosition(targetX, targetY);
    }

    public Vector2 GetNodePosition(int x, int y)
    {
        float xPos = x * lockedCellSize + lockedXOffset;
        float yPos = y * lockedCellSize + lockedYOffset;
        return new Vector2(xPos, yPos);
    }

    public bool IsSegmentIntersectingDeadZone(Vector2 startWorld, Vector2 endWorld)
    {
        if (currentLevel != null)
        {
            if (currentLevel.deadZones == null || currentLevel.deadZones.Count == 0) return false;
            
            float startX = (startWorld.x - lockedXOffset) / lockedCellSize;
            float startY = (startWorld.y - lockedYOffset) / lockedCellSize;
            float endX = (endWorld.x - lockedXOffset) / lockedCellSize;
            float endY = (endWorld.y - lockedYOffset) / lockedCellSize;

            int steps = 20;
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                float px = Mathf.Lerp(startX, endX, t);
                float py = Mathf.Lerp(startY, endY, t);

                foreach (var zone in currentLevel.deadZones)
                {
                    // Check intersection with epsilon for grazing
                     if (px > zone.x + 0.001f && px < zone.x + zone.width - 0.001f &&
                         py > zone.y + 0.001f && py < zone.y + zone.height - 0.001f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Fallback
        if (destroyWidth <= 0 || destroyHeight <= 0) return false;

        float startX_f = (startWorld.x - lockedXOffset) / lockedCellSize;
        float startY_f = (startWorld.y - lockedYOffset) / lockedCellSize;
        float endX_f = (endWorld.x - lockedXOffset) / lockedCellSize;
        float endY_f = (endWorld.y - lockedYOffset) / lockedCellSize;

        float wallX = gridWidth - destroyWidth;
        float ceilingY = destroyHeight;

        // Sample points along the line (excluding exact endpoints to allow connecting TO the wall)
        int steps_f = 20;
        for (int i = 1; i < steps_f; i++)
        {
            float t = i / (float)steps_f;
            float px = Mathf.Lerp(startX_f, endX_f, t);
            float py = Mathf.Lerp(startY_f, endY_f, t);

            // Check if strictly inside the danger zone
            // Use epsilon to allow grazing the edge
            if (px > wallX + 0.001f && py < ceilingY - 0.001f)
            {
                return true;
            }
        }
        return false;
    }
}
