
using UnityEngine;



[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]



public class GridController : MonoBehaviour



{



    [Header("Grid Settings")]



    public int minSquares = 10;



    public bool fitToScreen = true;



    public int destroyWidth = 0;



    public int destroyHeight = 0;







    private Vector2[,] intersectionPoints;



    private Mesh gridMesh;



    



    // These are now calculated by the formula



    private int gridWidth;



    private int gridHeight;







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



        if (Application.isPlaying) return;



        if (gridMesh == null)



        {



            gridMesh = new Mesh();



            GetComponent<MeshFilter>().mesh = gridMesh;



        }



        GenerateGrid();



    }







    bool IsCellDestroyed(int x, int y)



    {



        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)



        {



            return false; // Out of bounds cells are not "destroyed"



        }



        return x >= gridWidth - destroyWidth && y < destroyHeight;



    }







    public void GenerateGrid()



    {



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



                



                System.Collections.Generic.List<int> indices = new System.Collections.Generic.List<int>();



                gridEdges.Clear();



        



                // Horizontal lines



                for (int y = 0; y <= gridHeight; y++)



                {



                    for (int x = 0; x < gridWidth; x++)



                    {



                        // This line separates cell (x, y-1) and (x, y)



                        // Draw it unless both are destroyed



                        if (!(IsCellDestroyed(x, y - 1) && IsCellDestroyed(x, y)))



                        {



                            int startVertex = y * (gridWidth + 1) + x;



                            indices.Add(startVertex);



                            indices.Add(startVertex + 1);



                            gridEdges.Add(new GridEdge(intersectionPoints[x, y], intersectionPoints[x + 1, y]));



                        }



                    }



                }



        



                // Vertical lines



                for (int x = 0; x <= gridWidth; x++)



                {



                    for (int y = 0; y < gridHeight; y++)



                    {



                        // This line separates cell (x-1, y) and (x, y)



                        // Draw it unless both are destroyed



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



            }







    public Vector2[,] GetIntersectionPoints()



    {



        return intersectionPoints;



    }



    



    public System.Collections.Generic.List<GridEdge> GetGridEdges()



    {



        return gridEdges;



    }



    



    public int GetGridWidth() { return gridWidth; }



    public int GetGridHeight() { return gridHeight; }



}
