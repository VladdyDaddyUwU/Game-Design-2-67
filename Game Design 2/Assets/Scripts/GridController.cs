using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridController : MonoBehaviour
{
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
    [SerializeField, HideInInspector] private float lockedCellSize = 1f;
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
        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
        {
            return false;
        }
        return x >= gridWidth - destroyWidth && y < destroyHeight;
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

            if (fitToScreen && Camera.main != null)
            {
                float totalScreenHeight_World = Camera.main.orthographicSize * 2;
                float totalScreenWidth_World = totalScreenHeight_World * Camera.main.aspect;
                float screenWidth_Pixels = Screen.width;
                float screenHeight_Pixels = Screen.height;
                float smallerDimension_Pixels = Mathf.Min(screenWidth_Pixels, screenHeight_Pixels);
                float cutAmount_Pixels = smallerDimension_Pixels / 4f;
                float pixelsPerWorldUnit = screenHeight_Pixels / totalScreenHeight_World;
                float cutAmount_World = cutAmount_Pixels / pixelsPerWorldUnit;
                float availableWidth_World = totalScreenWidth_World - cutAmount_World;
                float availableHeight_World = totalScreenHeight_World - cutAmount_World;
                
                if (availableWidth_World >= availableHeight_World)
                {
                    gridHeight = minSquares;
                    lockedCellSize = availableHeight_World / gridHeight;
                    gridWidth = Mathf.FloorToInt(availableWidth_World / lockedCellSize);
                }
                else
                {
                    gridWidth = minSquares;
                    lockedCellSize = availableWidth_World / gridWidth;
                    gridHeight = Mathf.FloorToInt(availableHeight_World / lockedCellSize);
                }
                lockedXOffset = -totalScreenWidth_World / 2f + cutAmount_World;
                lockedYOffset = -totalScreenHeight_World / 2f + (cutAmount_World * gridUpShiftFactor);
            }
            else
            {
                gridWidth = minSquares;
                gridHeight = minSquares;
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

                bool cell_bottom_left  = IsCellDestroyed(x - 1, y - 1);
                bool cell_bottom_right = IsCellDestroyed(x,     y - 1);
                bool cell_top_left     = IsCellDestroyed(x - 1, y);
                bool cell_top_right    = IsCellDestroyed(x,     y);

                if (cell_bottom_left && cell_bottom_right && cell_top_left && cell_top_right)
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
}
