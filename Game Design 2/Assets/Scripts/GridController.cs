
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridController : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public bool fitToScreen = true;
    public float cellSize = 1f;

    private Vector2[,] intersectionPoints;
    private Mesh gridMesh;

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

    public System.Collections.Generic.List<GridEdge> gridEdges = new System.Collections.Generic.List<GridEdge>();


    void Awake()
    {
        GetComponent<MeshFilter>().mesh = gridMesh = new Mesh();
        GenerateGrid();
    }

    void OnValidate()
    {
        if (gridMesh == null)
        {
            gridMesh = new Mesh();
            GetComponent<MeshFilter>().mesh = gridMesh;
        }
        GenerateGrid();
    }


    public void GenerateGrid()
    {
        float totalScreenWidth = 0;
        float totalScreenHeight = 0;

        if (Camera.main != null)
        {
            totalScreenHeight = Camera.main.orthographicSize * 2;
            totalScreenWidth = totalScreenHeight * Camera.main.aspect;
        }

        if (fitToScreen && Camera.main != null)
        {
            // Size the grid to fit in a 3/4 by 3/4 area
            float availableWidth = totalScreenWidth * 0.75f;
            float availableHeight = totalScreenHeight * 0.75f;
            cellSize = Mathf.Min(availableWidth / gridWidth, availableHeight / gridHeight);
        }

        // Position the grid to leave the bottom-left 1/4 empty
        float xOffset = -totalScreenWidth / 4f;
        float yOffset = -totalScreenHeight / 4f;

        intersectionPoints = new Vector2[gridWidth + 1, gridHeight + 1];
        System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
        
        for (int y = 0; y <= gridHeight; y++)
        {
            for (int x = 0; x <= gridWidth; x++)
            {
                float xPos = x * cellSize + xOffset;
                float yPos = y * cellSize + yOffset;
                intersectionPoints[x, y] = new Vector2(xPos, yPos);
                vertices.Add(new Vector3(xPos, yPos, 0));
            }
        }

        gridMesh.vertices = vertices.ToArray();
        
        int[] indices = new int[gridWidth * (gridHeight + 1) * 2 + gridHeight * (gridWidth + 1) * 2];
        int index = 0;
        gridEdges.Clear();

        // Horizontal lines
        for (int y = 0; y <= gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int startVertex = y * (gridWidth + 1) + x;
                indices[index++] = startVertex;
                indices[index++] = startVertex + 1;
                gridEdges.Add(new GridEdge(intersectionPoints[x,y], intersectionPoints[x+1,y]));
            }
        }

        // Vertical lines
        for (int x = 0; x <= gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                int startVertex = y * (gridWidth + 1) + x;
                indices[index++] = startVertex;
                indices[index++] = startVertex + (gridWidth + 1);
                gridEdges.Add(new GridEdge(intersectionPoints[x,y], intersectionPoints[x,y+1]));
            }
        }

        gridMesh.Clear();
        gridMesh.vertices = vertices.ToArray();
        gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
    }

    public Vector2[,] GetIntersectionPoints()
    {
        return intersectionPoints;
    }
    
    public System.Collections.Generic.List<GridEdge> GetGridEdges()
    {
        return gridEdges;
    }
}
