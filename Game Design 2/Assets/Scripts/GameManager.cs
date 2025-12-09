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

    [Header("Structural Properties")]
    public float youngsModulus = 210e9f;
    public float memberCrossSectionArea = 0.0025f; // 5cm x 5cm
    public float memberYieldStress = 250e6f;

    [Header("Simulation Setup")]
    public List<int> anchorNodeIds = new List<int> { 0, 1 }; // Default anchors
    public int loadNodeId = 6;
    public float loadMass = 40000f;
    public Vector2 gravity = new Vector2(0, -9.81f);

    private List<Node> allNodes;
    private List<Vector2> nodePositions;
    private List<int[]> structuralElements = new List<int[]>();
    private Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();

    void Start()
    {
        if (gridController == null) gridController = FindObjectOfType<GridController>();
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
    
    public void InitializeStructure()
    {
        var newNodes = gridController.GetNodes();
        if (newNodes == null || newNodes.Count == 0)
        {
            Debug.LogWarning("Initialization skipped: Node list is empty. Grid may not have generated yet.");
            return;
        }

        // Defensively filter out any "ghost" objects that may exist for a frame in the editor
        allNodes = newNodes.Where(n => n != null).ToList();

        nodePositions = allNodes.Select(n => (Vector2)n.transform.position).ToList();
        
        // Clear old data before repopulating
        adjacencyList.Clear();
        structuralElements.Clear();

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
    
    public void AddElement(int nodeId1, int nodeId2)
    {
        structuralElements.Add(new int[] { nodeId1, nodeId2 });
        adjacencyList[nodeId1].Add(nodeId2);
        adjacencyList[nodeId2].Add(nodeId1);
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
