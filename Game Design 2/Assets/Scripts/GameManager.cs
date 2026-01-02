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
    public Transform elementsHolder;

    [Header("Game Elements")]
    public GameObject humanPrefab;
    public GameObject anvilPrefab;
    public float anvilHangingDistance = 1.5f; // How far below the node the anvil hangs
    private GameObject humanInstance;
    private GameObject anvilInstance;
    private LineRenderer anvilRope;

    [Header("Structural Properties")]
    public float youngsModulus = 210e9f;
    public float memberCrossSectionArea = 0.0025f; // 5cm x 5cm
    public float memberYieldStress = 250e6f;
    public float beamDensity = 7850f; // Density of Steel (kg/m3)

    [Header("Simulation Setup")]
    public List<int> anchorNodeIds = new List<int> { 0, 1 }; // Default anchors
    public int loadNodeId; // Calculated dynamically
    public float loadMass = 40000f;
    public Vector2 gravity = new Vector2(0, -9.81f);

    private Dictionary<int, Node> nodeMap = new Dictionary<int, Node>();
    private List<Node> allNodes = new List<Node>();
    private List<Vector2> nodePositions;
    private List<int[]> structuralElements = new List<int[]>();
    private Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();
    
    private bool isCollapsing = false;

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
            ResetSimulation();
        }
        
        if (isCollapsing)
        {
            UpdateBeamVisuals();
            // Also update rope
            if (anvilRope != null && anvilInstance != null && nodeMap.ContainsKey(loadNodeId))
            {
                anvilRope.SetPosition(0, nodeMap[loadNodeId].transform.position);
                anvilRope.SetPosition(1, anvilInstance.transform.position);
            }
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
        
        // Manage Elements Holder (Game Elements)
        if (elementsHolder == null)
        {
            var existingHolder = transform.Find("ElementsHolder");
            if (existingHolder != null)
            {
                elementsHolder = existingHolder;
            }
            else
            {
                var holderObj = new GameObject("ElementsHolder");
                holderObj.transform.SetParent(this.transform);
                holderObj.transform.localPosition = Vector3.zero;
                elementsHolder = holderObj.transform;
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

        SpawnGameElements();
    }

    public void SpawnGameElements()
    {
        // 1. Cleanup old elements - Destroy all children of elementsHolder
        if (elementsHolder != null)
        {
            for (int i = elementsHolder.childCount - 1; i >= 0; i--)
            {
                 var child = elementsHolder.GetChild(i);
                 DestroyImmediate(child.gameObject);
            }
        }

        // 2. Calculate Positions
        int targetNodeID = gridController.GetTargetNodeID();
        Vector2 targetNodePos = gridController.GetTargetNodePosition();
        
        // Human is at the bottom of the same column
        int targetX = gridController.GetGridWidth() - (gridController.destroyWidth / 2);
        Vector2 humanPos = gridController.GetNodePosition(targetX, 0); 
        humanPos += Vector2.up * 0.5f;

        Vector2 anvilPos = targetNodePos + (Vector2.down * anvilHangingDistance);

        // 3. Spawn Human
        if (humanPrefab != null)
        {
            humanInstance = Instantiate(humanPrefab, humanPos, Quaternion.identity);
        }
        else
        {
            humanInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            humanInstance.transform.position = humanPos;
            humanInstance.transform.localScale = Vector3.one * 0.5f;
            humanInstance.GetComponent<Renderer>().material.color = Color.green;
            humanInstance.name = "Human (Placeholder)";
        }
        if (elementsHolder != null) humanInstance.transform.SetParent(elementsHolder);

        // 4. Spawn Anvil
        if (anvilPrefab != null)
        {
            anvilInstance = Instantiate(anvilPrefab, anvilPos, Quaternion.identity);
        }
        else
        {
            anvilInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anvilInstance.transform.position = anvilPos;
            anvilInstance.transform.localScale = Vector3.one * 0.8f;
            anvilInstance.GetComponent<Renderer>().material.color = Color.black;
            anvilInstance.name = "Anvil (Placeholder)";
        }
        if (elementsHolder != null) anvilInstance.transform.SetParent(elementsHolder);
        
        // 5. Draw Rope
        GameObject ropeObj = new GameObject("AnvilRope");
        if (elementsHolder != null) ropeObj.transform.SetParent(elementsHolder);
        
        anvilRope = ropeObj.AddComponent<LineRenderer>();
        anvilRope.startWidth = 0.05f;
        anvilRope.endWidth = 0.05f;
        anvilRope.positionCount = 2;
        anvilRope.material = new Material(Shader.Find("Sprites/Default"));
        anvilRope.startColor = Color.gray;
        anvilRope.endColor = Color.gray;
        
        anvilRope.SetPosition(0, targetNodePos);
        anvilRope.SetPosition(1, anvilPos);
    }

    public void RefreshNodes()
    {
        var newNodes = gridController.GetNodes();
        
        if (newNodes == null || newNodes.Count == 0)
        {
            Debug.LogWarning("RefreshNodes: Node list is empty. Grid might be uninitialized or cleared.");
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
        structuralElements.RemoveAll(e => (e[0] == nodeId1 && e[1] == nodeId2) || (e[0] == nodeId2 && e[1] == nodeId1));
        if (adjacencyList.ContainsKey(nodeId1)) adjacencyList[nodeId1].Remove(nodeId2);
        if (adjacencyList.ContainsKey(nodeId2)) adjacencyList[nodeId2].Remove(nodeId1);
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

            if (anchorNodeIds.Contains(currentNodeId)) return true;

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
        if (structuralElements == null) return false;
        if (nodeMap == null || nodeMap.Count == 0) RefreshNodes();

        Vector2Int p1 = new Vector2Int(startNode.x_index, startNode.y_index);
        Vector2Int p2 = new Vector2Int(endNode.x_index, endNode.y_index);

        for (int i = 0; i < structuralElements.Count; i++)
        {
            var element = structuralElements[i];
            int id1 = element[0];
            int id2 = element[1];

            if ((id1 == startNode.id && id2 == endNode.id) || (id1 == endNode.id && id2 == startNode.id)) continue;

            Node nodeA = null;
            Node nodeB = null;

            if (nodeMap.TryGetValue(id1, out nodeA) && nodeMap.TryGetValue(id2, out nodeB))
            {
                 if (nodeA == null || nodeB == null) continue;

                Vector2Int pA = new Vector2Int(nodeA.x_index, nodeA.y_index);
                Vector2Int pB = new Vector2Int(nodeB.x_index, nodeB.y_index);

                if (DoSegmentsOverlap(p1, p2, pA, pB)) return true;
            }
        }
        return false;
    }

    public bool IsElementContained(Node startNode, Node endNode)
    {
         return IsNewElementOverlappingExisting(startNode, endNode);
    }

    private bool DoSegmentsOverlap(Vector2Int p1, Vector2Int p2, Vector2Int a, Vector2Int b)
    {
        long ab_dx = b.x - a.x;
        long ab_dy = b.y - a.y;
        long p1p2_dx = p2.x - p1.x;
        long p1p2_dy = p2.y - p1.y;
        long ap1_dx = p1.x - a.x;
        long ap1_dy = p1.y - a.y;

        long parallelCheck = (ab_dx * p1p2_dy) - (ab_dy * p1p2_dx);
        if (parallelCheck != 0) return false;

        long collinearCheck = (ab_dx * ap1_dy) - (ab_dy * ap1_dx);
        if (collinearCheck != 0) return false;

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

        return (max1 > min2) && (max2 > min1);
    }

    public void StartSimulation()
    {
        currentMode = GameMode.Simulate;
        Debug.Log("Starting simulation...");

        // Hide unconnected nodes
        foreach (var node in allNodes)
        {
            bool isConnected = false;
            if (adjacencyList.ContainsKey(node.id) && adjacencyList[node.id].Count > 0)
            {
                isConnected = true;
            }
            
            // Keep anchors visible, keep connected nodes visible. Hide the rest.
            if (!node.isAnchor && !isConnected)
            {
                node.gameObject.SetActive(false);
            }
        }

        loadNodeId = gridController.GetTargetNodeID();

        // --- PREPARE ANALYSIS DATA ---
        // We must map Node IDs to a compact index range (0..N) containing ONLY active nodes.
        // Unconnected nodes in the grid cause Singularity (Matrix Error) if included.
        
        HashSet<int> activeNodeIDs = new HashSet<int>();
        
        // 1. Include nodes connected by beams
        foreach (var el in structuralElements)
        {
            activeNodeIDs.Add(el[0]);
            activeNodeIDs.Add(el[1]);
        }
        
        // 2. Include Anchors (must be part of system)
        foreach (var anchorID in anchorNodeIds)
        {
            // Only add if it exists
            if (nodeMap.ContainsKey(anchorID)) activeNodeIDs.Add(anchorID);
        }
        
        // 3. Include Load Node
        if (nodeMap.ContainsKey(loadNodeId)) activeNodeIDs.Add(loadNodeId);

        // Build Mapping
        List<Vector2> activePositions = new List<Vector2>();
        Dictionary<int, int> idToIndex = new Dictionary<int, int>();
        List<Node> activeNodesList = new List<Node>(); 
        
        int indexCounter = 0;
        foreach (int id in activeNodeIDs)
        {
            if (nodeMap.TryGetValue(id, out Node node))
            {
                activePositions.Add(node.transform.position);
                idToIndex[id] = indexCounter;
                activeNodesList.Add(node);
                indexCounter++;
            }
        }
        
        // Remap Elements
        List<int[]> analysisElements = new List<int[]>();
        foreach (var el in structuralElements)
        {
            if (idToIndex.ContainsKey(el[0]) && idToIndex.ContainsKey(el[1]))
            {
                analysisElements.Add(new int[] { idToIndex[el[0]], idToIndex[el[1]] });
            }
        }

        // Remap Anchors
        List<int> analysisFixedNodes = new List<int>();
        foreach (var anchorID in anchorNodeIds)
        {
            if (idToIndex.ContainsKey(anchorID))
                analysisFixedNodes.Add(idToIndex[anchorID]);
        }

        // Remap Loads
        var analysisLoads = new Dictionary<int, Vector2>();
        if (idToIndex.ContainsKey(loadNodeId))
        {
            analysisLoads.Add(idToIndex[loadNodeId], gravity * loadMass);
        }

        // --- RUN ANALYSIS ---
        StructuralAnalysis.AnalysisResult result = StructuralAnalysis.RunAnalysis(
            activePositions,
            analysisElements,
            analysisFixedNodes,
            analysisLoads,
            youngsModulus,
            memberCrossSectionArea,
            memberYieldStress,
            beamDensity
        );

        if (result.IsStable)
        {
            Debug.Log("Structure is STABLE.");
            // Apply deformation ONLY to the nodes involved in analysis
            ApplyDeformation(activeNodesList, activePositions, result.Displacements);
            
            bool failed = false;
            foreach (var stress in result.MemberStressPercentages)
            {
                if (stress >= 100f)
                {
                    failed = true;
                    break;
                }
            }
            
            if (failed)
            {
                Debug.LogError("Structure FAILED: Materials yielded/buckled.");
                CollapseSequence();
            }
            else
            {
                Debug.Log("SUCCESS: Human Saved!");
                if (humanInstance != null) 
                    humanInstance.GetComponent<Renderer>().material.color = Color.blue; 
            }
        }
        else
        {
            Debug.LogError("Structure is UNSTABLE (Mechanism detected)!");
            CollapseSequence();
        }

        if (result.MemberForces != null)
            Debug.Log("Member forces: " + string.Join(", ", result.MemberForces));
            
        if (result.MemberStressPercentages != null)
            Debug.Log("Stress percentages: " + string.Join(", ", result.MemberStressPercentages));
    }

    void ApplyDeformation(List<Node> nodes, List<Vector2> originalPositions, Vector2[] displacements, float scale = 1.0f)
    {
        if (displacements == null || nodes.Count != displacements.Length) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 newPos = originalPositions[i] + displacements[i] * scale;
            nodes[i].transform.position = newPos;
        }
        
        if (anvilRope != null)
        {
            if (nodeMap.ContainsKey(loadNodeId))
            {
                anvilRope.SetPosition(0, nodeMap[loadNodeId].transform.position);
            }
        }
        UpdateBeamVisuals();
    }
    
    void UpdateBeamVisuals()
    {
        if (structureHolder == null) return;
        
        foreach (Transform child in structureHolder)
        {
            LineRenderer lr = child.GetComponent<LineRenderer>();
            if (lr != null)
            {
                try 
                {
                     string name = child.name;
                     string[] parts = name.Split('-'); 
                     if (parts.Length == 2)
                     {
                         var p1 = ParseNodeCoords(parts[0].Replace("Beam", ""));
                         var p2 = ParseNodeCoords(parts[1]);
                         
                         Node n1 = allNodes.FirstOrDefault(n => n.x_index == p1.x && n.y_index == p1.y);
                         Node n2 = allNodes.FirstOrDefault(n => n.x_index == p2.x && n.y_index == p2.y);
                         
                         if (n1 != null && n2 != null)
                         {
                             lr.SetPosition(0, n1.transform.position);
                             lr.SetPosition(1, n2.transform.position);
                         }
                     }
                }
                catch {}
            }
        }
    }
    
    Vector2Int ParseNodeCoords(string s)
    {
        s = s.Trim('(', ')');
        var nums = s.Split(',');
        return new Vector2Int(int.Parse(nums[0]), int.Parse(nums[1]));
    }

    void CollapseSequence()
    {
        if (humanInstance != null) 
            humanInstance.GetComponent<Renderer>().material.color = Color.red; 
            
        Debug.Log("GAME OVER: Structure Collapsing!");
        isCollapsing = true;
        EnablePhysicsCollapse();
    }

    void EnablePhysicsCollapse()
    {
        // 1. Convert Nodes to Rigidbodies
        foreach (var node in allNodes)
        {
            Rigidbody2D rb = node.gameObject.GetComponent<Rigidbody2D>();
            if (rb == null) rb = node.gameObject.AddComponent<Rigidbody2D>();
            
            if (node.isAnchor)
            {
                rb.bodyType = RigidbodyType2D.Static;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.mass = 10f; // Give nodes some weight
                rb.linearDamping = 0.5f;
            }
            
            // Add collider if missing
            if (node.gameObject.GetComponent<CircleCollider2D>() == null)
            {
                node.gameObject.AddComponent<CircleCollider2D>().radius = 0.2f;
            }
        }

        // 2. Convert Beams to Joints
        foreach (var element in structuralElements)
        {
            int id1 = element[0];
            int id2 = element[1];

            if (nodeMap.TryGetValue(id1, out Node n1) && nodeMap.TryGetValue(id2, out Node n2))
            {
                DistanceJoint2D joint = n1.gameObject.AddComponent<DistanceJoint2D>();
                joint.connectedBody = n2.GetComponent<Rigidbody2D>();
                joint.autoConfigureDistance = false;
                joint.distance = Vector2.Distance(n1.transform.position, n2.transform.position);
                // Make it slightly elastic but mostly rigid
                joint.maxDistanceOnly = false; 
                joint.enableCollision = true;
            }
        }

        // 3. Connect Anvil
        if (anvilInstance != null)
        {
            // Remove 3D components first to avoid conflicts
            // MUST use DestroyImmediate because Destroy() is delayed to end of frame,
            // causing the AddComponent<Rigidbody2D> below to fail due to conflict.
            var rb3d = anvilInstance.GetComponent<Rigidbody>();
            if (rb3d != null) DestroyImmediate(rb3d);
            var col3d = anvilInstance.GetComponent<Collider>();
            if (col3d != null) DestroyImmediate(col3d);

            Rigidbody2D rb2d = anvilInstance.GetComponent<Rigidbody2D>();
            if (rb2d == null) rb2d = anvilInstance.AddComponent<Rigidbody2D>();
            rb2d.mass = 500f; // Heavy!
            
            // Add 2D collider so it hits the human
            if (anvilInstance.GetComponent<BoxCollider2D>() == null)
                 anvilInstance.AddComponent<BoxCollider2D>();
            
            if (nodeMap.TryGetValue(loadNodeId, out Node targetNode))
            {
                DistanceJoint2D rope = targetNode.gameObject.AddComponent<DistanceJoint2D>();
                rope.connectedBody = rb2d;
                rope.autoConfigureDistance = false;
                rope.distance = anvilHangingDistance;
                rope.maxDistanceOnly = true; // Rope behavior (can fold, can't stretch)
            }
        }
    }
    
    void ResetSimulation()
    {
         isCollapsing = false;

         if (nodePositions != null && allNodes != null)
         {
             for (int i = 0; i < allNodes.Count; i++)
             {
                 Node n = allNodes[i];
                 n.gameObject.SetActive(true); // Restore visibility
                 
                 // Remove physics
                 var joints = n.GetComponents<DistanceJoint2D>();
                 foreach(var j in joints) Destroy(j);
                 
                 var rb = n.GetComponent<Rigidbody2D>();
                 if (rb != null) Destroy(rb);
                 
                 var col = n.GetComponent<CircleCollider2D>();
                 if (col != null) Destroy(col);

                 // Restore Position
                 if (i < nodePositions.Count)
                    n.transform.position = nodePositions[i];
             }
         }
         
         // Fix Anvil
         if (anvilInstance != null)
         {
             var rb2d = anvilInstance.GetComponent<Rigidbody2D>();
             if (rb2d != null) Destroy(rb2d);
             // We used 3D primitive which has 3D collider. It's fine for visuals but physics mixed is bad.
             // For reset, we just respawn.
         }

         // Force respawn to clean slate
         SpawnGameElements();
         
         UpdateBeamVisuals();
    }
}