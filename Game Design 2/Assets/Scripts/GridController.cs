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

    [Header("Visuals")]
    public GameObject nodePrefab; // Assign a circle sprite prefab with a Collider2D and the Node.cs script
    public float nodeScale = 0.1f;

    private Vector2[,] intersectionPoints;
    private Mesh gridMesh;
    private int gridWidth;
    private int gridHeight;
    private List<GameObject> nodeObjects = new List<GameObject>();
    private List<Node> nodeComponents = new List<Node>();
    private Transform nodeHolder;
    private bool isDirty = true; // Flag to trigger regeneration

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
        GetComponent<MeshFilter>().mesh = gridMesh = new Mesh();
        // Don't call GenerateGrid directly, let Update handle it on the first frame
        isDirty = true;
    }

    void Update()
    {
        // This now runs in the editor and in play mode thanks to [ExecuteAlways]
        if (isDirty)
        {
            GenerateGrid();
            isDirty = false;
        }
    }

    void OnValidate()
    {
        // Don't generate grid directly. Just flag that it needs to be regenerated.
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
        // Find or create the node holder object
        if (nodeHolder == null)
        {
            GameObject holder = GameObject.Find("NodeHolder");
            if (holder == null)
            {
                holder = new GameObject("NodeHolder");
                holder.transform.SetParent(this.transform);
            }
            nodeHolder = holder.transform;
        }

        // Use a robust backward loop for cleanup. This is much safer than the previous method.
        for (int i = nodeHolder.childCount - 1; i >= 0; i--)
        {
            Transform child = nodeHolder.GetChild(i);
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

        float cellSize = 1f;
        float xOffset = 0;
        float yOffset = 0;
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
                cellSize = availableHeight_World / gridHeight;
                gridWidth = Mathf.FloorToInt(availableWidth_World / cellSize);
            }
            else
            {
                gridWidth = minSquares;
                cellSize = availableWidth_World / gridWidth;
                gridHeight = Mathf.FloorToInt(availableHeight_World / cellSize);
            }
            xOffset = -totalScreenWidth_World / 2f + cutAmount_World;
            yOffset = -totalScreenHeight_World / 2f + cutAmount_World;
        }
        else
        {
            gridWidth = minSquares;
            gridHeight = minSquares;
        }

        intersectionPoints = new Vector2[gridWidth + 1, gridHeight + 1];
        List<Vector3> vertices = new List<Vector3>();
        int idCounter = 0;
        for (int y = 0; y <= gridHeight; y++)
        {
            for (int x = 0; x <= gridWidth; x++)
            {
                // Always calculate position and add a vertex to the mesh data.
                float xPos = x * cellSize + xOffset;
                float yPos = y * cellSize + yOffset;
                intersectionPoints[x, y] = new Vector2(xPos, yPos);
                vertices.Add(new Vector3(xPos, yPos, 0));

                // Now, decide if a visible/interactable node GameObject should be created at this vertex.
                bool cell_bottom_left  = IsCellDestroyed(x - 1, y - 1);
                bool cell_bottom_right = IsCellDestroyed(x,     y - 1);
                bool cell_top_left     = IsCellDestroyed(x - 1, y);
                bool cell_top_right    = IsCellDestroyed(x,     y);

                if (cell_bottom_left && cell_bottom_right && cell_top_left && cell_top_right)
                {
                    // This vertex is fully surrounded by destroyed cells, so skip creating a visible node.
                    continue;
                }

                // If we are not skipping, create the visible node object.
                if (nodePrefab != null)
                {
                    GameObject nodeObj = Instantiate(nodePrefab, new Vector3(xPos, yPos, 0), Quaternion.identity, nodeHolder);
                    nodeObj.transform.localScale = Vector3.one * nodeScale;
                    nodeObj.name = $"Node_{idCounter}({x},{y})";
                    
                    Node nodeComp = nodeObj.GetComponent<Node>();
                    if (nodeComp == null) nodeComp = nodeObj.AddComponent<Node>();
                    
                    nodeComp.x_index = x;
                    nodeComp.y_index = y;
                    nodeComp.id = idCounter++; // Assign sequential ID only when a node is created
                    
                    nodeObjects.Add(nodeObj);
                    nodeComponents.Add(nodeComp);
                }
            }
        }

        List<int> indices = new List<int>();
        gridEdges.Clear();
        for (int y = 0; y <= gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (!(IsCellDestroyed(x, y - 1) && IsCellDestroyed(x, y)))
                {
                    int startVertex = y * (gridWidth + 1) + x;
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
                    int startVertex = y * (gridWidth + 1) + x;
                    indices.Add(startVertex);
                    indices.Add(startVertex + (gridWidth + 1));
                    gridEdges.Add(new GridEdge(intersectionPoints[x, y], intersectionPoints[x, y + 1]));
                }
            }
        }
        gridMesh.Clear();
        gridMesh.vertices = vertices.ToArray();
        gridMesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);

        // Tell the GameManager to initialize itself now that the grid is ready
        if (gameManager != null)
        {
            gameManager.InitializeStructure();
        }
    }

    public Vector2[,] GetIntersectionPoints() => intersectionPoints;
    public List<GridEdge> GetGridEdges() => gridEdges;
    public List<Node> GetNodes() => nodeComponents;
    public int GetGridWidth() => gridWidth;
    public int GetGridHeight() => gridHeight;
}

