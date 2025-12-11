using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum GameMode { Build, Simulate }

public class GameManager : MonoBehaviour
{
    [Header("Game State")]
    public GameMode currentMode = GameMode.Build;

    [Header("Component References")]
    public GridController gridController;
    public StructureBuilder structureBuilder;
    public Transform structureHolder;

    [Header("Structural Properties")]
    public float youngsModulus = 210e9f;
    public float memberCrossSectionArea = 0.0025f; // 5cm x 5cm
    public float memberYieldStress = 250e6f;

    [Header("Simulation Setup")]
    public List<int> anchorNodeIds = new List<int> { 0, 1 }; // Default anchors
    public int loadNodeId = 6;
    public float loadMass = 40000f;
    public Vector2 gravity = new Vector2(0, -9.81f);

    private Dictionary<int, Node> nodeMap = new Dictionary<int, Node>();
    private List<Node> allNodes = new List<Node>();
    private List<Vector2> nodePositions;
    private List<int[]> structuralElements = new List<int[]>();
    private Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();

    void Start()
    {
        if (gridController == null)
        {
            GameObject gridManagerObj = GameObject.Find("GridManager");
            if (gridManagerObj != null)
            {
                gridController = gridManagerObj.GetComponent<GridController>();
            }
            
            if (gridController == null)
            {
                gridController = FindObjectOfType<GridController>();
            }
        }

        if (structureBuilder == null) structureBuilder = FindObjectOfType<StructureBuilder>();
        
        // InitializeStructure(); // DO NOT CALL THIS HERE - GridController will call it
    }

    void Update()
    {
        // Example: Press 'S' to start simulation
        if (Input.GetKeyDown(KeyCode.S))
        {
            StartSimulation();
        }
        // Example: Press 'B' to return to build mode
        if (Input.GetKeyDown(KeyCode.B))
        {
            currentMode = GameMode.Build;
            // Here you might want to reset the visuals to the pre-simulation state
        }
    }
    
    public void InitializeStructure(GridController sourceGridController = null)
    {
        // cleanup phantom NodeHolder if it exists under GameManager
        Transform phantomNodeHolder = transform.Find("NodeHolder");
        if (phantomNodeHolder != null)
        {
            Debug.LogWarning("Found phantom 'NodeHolder' under GameManager. Destroying it.");
            if (Application.isPlaying) Destroy(phantomNodeHolder.gameObject);
            else DestroyImmediate(phantomNodeHolder.gameObject);
        }

        // Resolve GridController
        if (sourceGridController != null)
        {
            this.gridController = sourceGridController;
        }
        else if (this.gridController == null)
        {
             // Try finding the correct one
             GameObject gridManagerObj = GameObject.Find("GridManager");
             if (gridManagerObj != null) this.gridController = gridManagerObj.GetComponent<GridController>();
        }

        if (this.gridController == null)
        {
             Debug.LogError("InitializeStructure: GridController is missing!");
             return;
        }

        RefreshNodes();
        
        // Clear old data before repopulating
        adjacencyList.Clear();
        structuralElements.Clear();

        // Manage Structure Holder (Visuals)
        if (structureHolder == null)
        {
            var existingHolder = transform.Find("StructureHolder");
            if (existingHolder != null)
            {
                structureHolder = existingHolder;
            }
            else
            {
                var holderObj = new GameObject("StructureHolder");
                holderObj.transform.SetParent(this.transform);
                holderObj.transform.localPosition = Vector3.zero;
                structureHolder = holderObj.transform;
            }
        }

        // Clear existing visuals
        for (int i = structureHolder.childCount - 1; i >= 0; i--)
        {
            var child = structureHolder.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        // ** CRITICAL: Repopulate the adjacency list and set anchors **
        foreach (var node in allNodes)
        {
            adjacencyList[node.id] = new List<int>();
            if (anchorNodeIds.Contains(node.id))
            {
                node.isAnchor = true;
            }
            else
            {
                node.isAnchor = false;
            }
        }
    }

    public void RefreshNodes()
    {
        var newNodes = gridController.GetNodes();
        
        if (newNodes == null || newNodes.Count == 0)
        {
            Debug.LogWarning("RefreshNodes: Node list is empty. Grid might be uninitialized or cleared.");
            // Do NOT force regeneration here to avoid loops and state corruption.
            return;
        }

        nodeMap.Clear();
        allNodes.Clear();
        
        foreach (var n in newNodes)
        {
            if (n != null)
            {
                if (!nodeMap.ContainsKey(n.id))
                {
                    nodeMap.Add(n.id, n);
                    allNodes.Add(n);
                }
            }
        }
        
        nodePositions = allNodes.Select(n => (Vector2)n.transform.position).ToList();
        Debug.Log($"Nodes refreshed. Count: {allNodes.Count}");
    }
    
    public void AddElement(int nodeId1, int nodeId2)
    {
        structuralElements.Add(new int[] { nodeId1, nodeId2 });
        if (!adjacencyList.ContainsKey(nodeId1)) adjacencyList[nodeId1] = new List<int>();
        if (!adjacencyList.ContainsKey(nodeId2)) adjacencyList[nodeId2] = new List<int>();
        adjacencyList[nodeId1].Add(nodeId2);
        adjacencyList[nodeId2].Add(nodeId1);
    }

    public void RemoveElement(int nodeId1, int nodeId2)
    {
        // Remove from structuralElements
        structuralElements.RemoveAll(e => (e[0] == nodeId1 && e[1] == nodeId2) || (e[0] == nodeId2 && e[1] == nodeId1));

        // Remove from adjacencyList
        if (adjacencyList.ContainsKey(nodeId1))
        {
            adjacencyList[nodeId1].Remove(nodeId2);
        }
        if (adjacencyList.ContainsKey(nodeId2))
        {
            adjacencyList[nodeId2].Remove(nodeId1);
        }
    }

    public bool DoesElementExist(int nodeId1, int nodeId2)
    {
        return structuralElements.Any(e => (e[0] == nodeId1 && e[1] == nodeId2) || (e[0] == nodeId2 && e[1] == nodeId1));
    }

    public bool IsNodeConnectedToAnchor(int startNodeId)
    {
        if (anchorNodeIds.Contains(startNodeId)) return true;

        var visited = new HashSet<int>();
        var queue = new Queue<int>();

        queue.Enqueue(startNodeId);
        visited.Add(startNodeId);

        while (queue.Count > 0)
        {
            int currentNodeId = queue.Dequeue();

            if (anchorNodeIds.Contains(currentNodeId))
            {
                return true;
            }

            if (adjacencyList.ContainsKey(currentNodeId))
            {
                foreach (int neighborId in adjacencyList[currentNodeId])
                {
                    if (!visited.Contains(neighborId))
                    {
                        visited.Add(neighborId);
                        queue.Enqueue(neighborId);
                    }
                }
            }
        }
        return false;
    }

    public bool IsNewElementOverlappingExisting(Node startNode, Node endNode)
    {
        if (structuralElements == null) 
        {
            Debug.LogError("IsNewElementOverlappingExisting: structuralElements is null!");
            return false;
        }

        // Ensure map is populated
        if (nodeMap == null || nodeMap.Count == 0)
        {
            RefreshNodes();
        }

        Vector2Int p1 = new Vector2Int(startNode.x_index, startNode.y_index);
        Vector2Int p2 = new Vector2Int(endNode.x_index, endNode.y_index);

        Debug.Log($"[OverlapCheck] START. New Beam: {startNode.id}-{endNode.id} ({p1}-{p2}). Total Elements: {structuralElements.Count}.");

        for (int i = 0; i < structuralElements.Count; i++)
        {
            var element = structuralElements[i];
            int id1 = element[0];
            int id2 = element[1];

            // Skip the element if it is the one we just added (the self-check)
            if ((id1 == startNode.id && id2 == endNode.id) || (id1 == endNode.id && id2 == startNode.id))
            {
                continue;
            }

            Node nodeA = null;
            Node nodeB = null;

            if (nodeMap.TryGetValue(id1, out nodeA) && nodeMap.TryGetValue(id2, out nodeB))
            {
                 // Check validity
                 if (nodeA == null || nodeB == null)
                 {
                     // This means the node object was destroyed but the ID is still in our map/list.
                     // This indicates a state desync, but we should not crash or recurse.
                     continue;
                 }

                Vector2Int pA = new Vector2Int(nodeA.x_index, nodeA.y_index);
                Vector2Int pB = new Vector2Int(nodeB.x_index, nodeB.y_index);

                if (DoSegmentsOverlap(p1, p2, pA, pB))
                {
                    Debug.Log($"[OverlapCheck] OVERLAP DETECTED at Index {i}!");
                    return true;
                }
            }
        }
        
        return false;
    }

    public bool IsElementContained(Node startNode, Node endNode)
    {
         // Same logic using map
         return IsNewElementOverlappingExisting(startNode, endNode);
    }

    private bool DoSegmentsOverlap(Vector2Int p1, Vector2Int p2, Vector2Int a, Vector2Int b)
    {
        // 1. Check if they are collinear lines.
        long ab_dx = b.x - a.x;
        long ab_dy = b.y - a.y;
        
        long p1p2_dx = p2.x - p1.x;
        long p1p2_dy = p2.y - p1.y;

        long ap1_dx = p1.x - a.x;
        long ap1_dy = p1.y - a.y;

        // Check 1: Are lines AB and P1P2 parallel? Cross product of direction vectors must be 0.
        long parallelCheck = (ab_dx * p1p2_dy) - (ab_dy * p1p2_dx);
        if (parallelCheck != 0) 
        {
            return false;
        }

        // Check 2: Are lines collinear? Cross product of AB and AP1 must be 0.
        long collinearCheck = (ab_dx * ap1_dy) - (ab_dy * ap1_dx);
        if (collinearCheck != 0) 
        {
            return false;
        }

        // 3. If collinear, check for 1D interval overlap.
        bool isVertical = (ab_dx == 0);

        int min1, max1, min2, max2;

        if (!isVertical)
        {
            min1 = Mathf.Min(p1.x, p2.x);
            max1 = Mathf.Max(p1.x, p2.x);
            min2 = Mathf.Min(a.x, b.x);
            max2 = Mathf.Max(a.x, b.x);
        }
        else
        {
            min1 = Mathf.Min(p1.y, p2.y);
            max1 = Mathf.Max(p1.y, p2.y);
            min2 = Mathf.Min(a.y, b.y);
            max2 = Mathf.Max(a.y, b.y);
        }

        bool overlaps = (max1 > min2) && (max2 > min1);
        return overlaps;
    }

    public void StartSimulation()
    {
        currentMode = GameMode.Simulate;
        Debug.Log("Starting simulation...");

        var fixedNodes = anchorNodeIds;
        var loads = new Dictionary<int, Vector2>
        {
            { loadNodeId, gravity * loadMass }
        };

        StructuralAnalysis.AnalysisResult result = StructuralAnalysis.RunAnalysis(
            nodePositions,
            structuralElements,
            fixedNodes,
            loads,
            youngsModulus,
            memberCrossSectionArea,
            memberYieldStress
        );

        if (result.IsStable)
        {
            Debug.Log("Structure is STABLE.");
            ApplyDeformation(result.Displacements);
        }
        else
        {
            Debug.LogError("Structure is UNSTABLE and failed!");
        }

        // Visualize results (e.g., color members by stress)
        // This part would require access to the beam game objects.
        Debug.Log("Member forces: " + string.Join(", ", result.MemberForces));
        Debug.Log("Stress percentages: " + string.Join(", ", result.MemberStressPercentages));
    }

    void ApplyDeformation(Vector2[] displacements, float scale = 1.0f)
    {
        if (displacements == null || displacements.Length != allNodes.Count) return;

        for (int i = 0; i < allNodes.Count; i++)
        {
            Vector3 newPos = nodePositions[i] + displacements[i] * scale;
            allNodes[i].transform.position = newPos;
        }

        // You would also need to update the LineRenderers for the beams here
        // This requires a more robust way to track beam game objects.
    }
}
