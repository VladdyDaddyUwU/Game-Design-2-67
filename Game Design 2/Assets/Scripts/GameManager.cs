using UnityEngine;
using System.Collections;
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
    public UIManager uiManager;
    public LevelManager levelManager;
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
    public float currentCrossSectionArea = 0.0025f; // Currently selected material
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

    [System.Serializable]
    public struct Beam
    {
        public int node1;
        public int node2;
        public float area;
    }

    private List<Beam> structuralElements = new List<Beam>();
    private Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();
    
    private bool isCollapsing = false;
    private StructuralAnalysis.AnalysisResult lastAnalysisResult;
    private int prebuiltElementCount = 0; // Track how many elements are permanent
    
    private Sprite connectedNodeSprite;

    // Tracking for broken parts
    private List<GameObject> brokenParts = new List<GameObject>();
    private class BrokenBeamVisual {
        public LineRenderer lr;
        public Transform t1;
        public Transform t2;
    }
    private List<BrokenBeamVisual> brokenBeamVisuals = new List<BrokenBeamVisual>();
    private float storedNodeRadius = -1f;

    private AudioSource audioSource;
    private AudioClip buttonClickSound;
    private AudioClip levelCompleteSound;
    private AudioClip failMusic;
    [Range(0f, 1f)] public float buttonVolume = 1f; // Public volume control, appears as slider
    [Range(0f, 1f)] public float levelCompleteVolume = 1f;
    [Range(0f, 1f)] public float failMusicVolume = 1f;

    [Header("Ambiance")]
    private AudioSource ambianceSource;
    private AudioSource simulationAmbianceSource;
    [Range(0f, 1f)] public float ambianceVolume = 0.5f;
    [Range(0f, 1f)] public float simulationAmbianceVolume = 0.5f;
    private bool isAmbiancePausedByMenu = false;

    void Awake()
    {
        // --- One-shot sound setup ---
        // Get or add AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.volume = buttonVolume; // Apply volume here

        // Load the shared button click sound
        buttonClickSound = Resources.Load<AudioClip>("button_click");
        levelCompleteSound = Resources.Load<AudioClip>("level-completion");
        failMusic = Resources.Load<AudioClip>("fail_music");
    }

    void Start()
    {
        #if UNITY_EDITOR
        string cbtPath = "Assets/Levels/CBT2.png";
        connectedNodeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(cbtPath);
        if (connectedNodeSprite == null)
        {
             Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(cbtPath);
             if (tex != null)
                 connectedNodeSprite = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        #endif
        
        // --- Ambiance sound setup ---
        GameObject ambiancePlayer = new GameObject("AmbiancePlayer");
        ambiancePlayer.transform.SetParent(this.transform);
        ambianceSource = ambiancePlayer.AddComponent<AudioSource>();
        ambianceSource.clip = Resources.Load<AudioClip>("city-sounds-296780");
        ambianceSource.volume = ambianceVolume;
        ambianceSource.loop = true;
        ambianceSource.playOnAwake = false;

        // --- Simulation Ambiance sound setup ---
        GameObject simulationAmbiancePlayer = new GameObject("SimulationAmbiancePlayer");
        simulationAmbiancePlayer.transform.SetParent(this.transform);
        simulationAmbianceSource = simulationAmbiancePlayer.AddComponent<AudioSource>();
        simulationAmbianceSource.clip = Resources.Load<AudioClip>("construction-site-49508");
        simulationAmbianceSource.volume = simulationAmbianceVolume;
        simulationAmbianceSource.loop = true;
        simulationAmbianceSource.playOnAwake = false;

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
        if (uiManager == null) 
        {
            uiManager = FindObjectOfType<UIManager>();
            // If still null, create it (it's a utility script)
            if (uiManager == null)
            {
                GameObject uiObj = new GameObject("UIManager");
                uiManager = uiObj.AddComponent<UIManager>();
            }
        }

        if (levelManager == null) levelManager = FindObjectOfType<LevelManager>();
    }

    public void OpenPauseMenu()
    {
        if (uiManager != null)
        {
            uiManager.OpenPauseMenu();
        }
    }

    [Header("Visualization Toggles")]
    public bool showCompressionOnly = false;
    public bool showTensionOnly = false;

    public void ToggleCompressionView()
    {
        showCompressionOnly = !showCompressionOnly;
        if (showCompressionOnly) showTensionOnly = false; // Exclusive
    }

    public void ToggleTensionView()
    {
        showTensionOnly = !showTensionOnly;
        if (showTensionOnly) showCompressionOnly = false; // Exclusive
    }

    void Update()
    {
        // Pause Menu Toggle (X)
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (uiManager != null) uiManager.TogglePauseMenu();
        }

        // If Menu is Open, Block all other inputs
        if (uiManager != null && uiManager.IsPauseMenuOpen()) return;

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

        // Undo Functionality (Z)
        if (currentMode == GameMode.Build && Input.GetKeyDown(KeyCode.Z))
        {
            UndoLastAction();
        }

        // Restart/Clear Level (R)
        if (currentMode == GameMode.Build && Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
        
        if (isCollapsing)
        {
            UpdateBeamVisuals();
            UpdateBrokenBeams(); // Update the visuals for snapped beams
            // Also update rope
            if (anvilRope != null && anvilInstance != null && nodeMap.ContainsKey(loadNodeId))
            {
                anvilRope.SetPosition(0, nodeMap[loadNodeId].transform.position);
                anvilRope.SetPosition(1, anvilInstance.transform.position);
            }
        }

        // Visualize Stress Percentages (O or Toggles)
        bool isOHeld = Input.GetKey(KeyCode.O);
        if (isOHeld || showCompressionOnly || showTensionOnly)
        {
            // If we are in build mode, we calculate on the fly (Predictive)
            if (currentMode == GameMode.Build)
            {
                 lastAnalysisResult = PerformAnalysis();
            }
            
            ShowStressLabels();
        }
        else
        {
            HideStressLabels();
        }

        // Handle Ambiance based on Game State
        if (ambianceSource != null)
        {
            // Music should play only in Build mode and when the menu is not open.
            bool shouldBePlaying = currentMode == GameMode.Build && (uiManager == null || !uiManager.IsPauseMenuOpen());

            if (shouldBePlaying && !ambianceSource.isPlaying)
            {
                ambianceSource.Play();
            }
            else if (!shouldBePlaying && ambianceSource.isPlaying)
            {
                ambianceSource.Pause();
            }
        }

        // Handle Simulation Ambiance based on Game State
        if (simulationAmbianceSource != null)
        {
            // Music should play only in Simulate mode and when the menu is not open.
            bool shouldBePlaying = currentMode == GameMode.Simulate && (uiManager == null || !uiManager.IsPauseMenuOpen());

            if (shouldBePlaying && !simulationAmbianceSource.isPlaying)
            {
                simulationAmbianceSource.Play();
            }
            else if (!shouldBePlaying && simulationAmbianceSource.isPlaying)
            {
                simulationAmbianceSource.Pause();
            }
        }
    }

    private void UpdateBrokenBeams()
    {
        foreach(var visual in brokenBeamVisuals)
        {
            if (visual.lr != null && visual.t1 != null && visual.t2 != null)
            {
                visual.lr.SetPosition(0, visual.t1.position);
                visual.lr.SetPosition(1, visual.t2.position);
            }
        }
    }

    private GameObject anvilLabelObj;

    public void ShowStressLabels()
    {
        // --- Anvil Mass Label ---
        if (anvilInstance != null)
        {
            if (anvilLabelObj == null)
            {
                anvilLabelObj = new GameObject("AnvilMassLabel");
                anvilLabelObj.transform.SetParent(this.transform); // Keep it clean
                TextMesh tm = anvilLabelObj.AddComponent<TextMesh>();
                tm.characterSize = 0.05f;
                tm.fontSize = 60;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.white;
            }

            anvilLabelObj.SetActive(true);
            // Position above the anvil
            float offset = 1.0f; 
            if (gridController != null && gridController.currentLevel != null) 
                offset = gridController.currentLevel.anvilScale + 0.5f;
                
            anvilLabelObj.transform.position = anvilInstance.transform.position + Vector3.up * offset;
            anvilLabelObj.GetComponent<TextMesh>().text = $"{loadMass:F0} kg";
        }

        if (structuralElements == null) return;
        
        // If unstable or data is missing, show "UNSTABLE"
        bool isDataValid = lastAnalysisResult.IsStable && 
                           lastAnalysisResult.MemberStressPercentages != null && 
                           lastAnalysisResult.MemberForces != null &&
                           structuralElements.Count == lastAnalysisResult.MemberStressPercentages.Length;

        for (int i = 0; i < structuralElements.Count; i++)
        {
            var el = structuralElements[i];
            int id1 = el.node1;
            int id2 = el.node2;

            if (!nodeMap.ContainsKey(id1) || !nodeMap.ContainsKey(id2)) continue;

            Node n1 = nodeMap[id1];
            Node n2 = nodeMap[id2];

            // Find Visual Beam
            string name1 = $"Beam({n1.x_index},{n1.y_index})-({n2.x_index},{n2.y_index})";
            string name2 = $"Beam({n2.x_index},{n2.y_index})-({n1.x_index},{n1.y_index})";
            
            Transform beamTransform = structureHolder.Find(name1);
            if (beamTransform == null) beamTransform = structureHolder.Find(name2);

            if (beamTransform != null)
            {
                Transform labelTr = beamTransform.Find("StressLabel");
                GameObject labelObj;
                TextMesh tm;

                if (labelTr == null)
                {
                    labelObj = new GameObject("StressLabel");
                    labelObj.transform.SetParent(beamTransform);
                    tm = labelObj.AddComponent<TextMesh>();
                    tm.characterSize = 0.05f;
                    tm.fontSize = 60;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                }
                else
                {
                    labelObj = labelTr.gameObject;
                    tm = labelObj.GetComponent<TextMesh>();
                }

                bool shouldShow = true;
                if (isDataValid)
                {
                    float force = lastAnalysisResult.MemberForces[i];
                    // Filtering Logic
                    if (showCompressionOnly && force >= 0) shouldShow = false; // Hide if Tension
                    if (showTensionOnly && force <= 0) shouldShow = false; // Hide if Compression
                }

                labelObj.SetActive(shouldShow);
                
                if (shouldShow)
                {
                    // Positioning logic (always needed)
                    Vector3 p1 = n1.transform.position;
                    Vector3 p2 = n2.transform.position;
                    if (p1.x > p2.x) { Vector3 temp = p1; p1 = p2; p2 = temp; }
                    Vector3 mid = (p1 + p2) / 2f;
                    Vector3 dir = (p2 - p1).normalized;
                    Vector3 normal = new Vector3(-dir.y, dir.x, 0);
                    float offsetDistance = 0.15f;
                    // Offset by normal (above beam) AND direction (along beam to avoid intersection overlap)
                    labelObj.transform.position = mid + (normal * offsetDistance) + (dir * 0.4f);
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    labelObj.transform.rotation = Quaternion.Euler(0, 0, angle);

                    if (isDataValid)
                    {
                        float percentage = lastAnalysisResult.MemberStressPercentages[i];
                        float force = lastAnalysisResult.MemberForces[i];

                        // Visual logic: minus sign for compression
                        string sign = (force < 0) ? "-" : "";
                        tm.text = $"{sign}{Mathf.RoundToInt(percentage)}%";
                        
                        // Color Code
                        if (force < 0)
                        {
                            tm.color = percentage >= 100f ? Color.red : new Color(1f, 0.4f, 0.4f); 
                        }
                        else
                        {
                            tm.color = percentage >= 100f ? Color.blue : new Color(0.4f, 0.4f, 1f); 
                        }
                    }
                    else
                    {
                        tm.text = "UNSTABLE";
                        tm.color = Color.red;
                    }
                }
            }
        }
    }

    public void HideStressLabels()
    {
        if (anvilLabelObj != null)
        {
            anvilLabelObj.SetActive(false);
        }

        if (structureHolder == null) return;
        foreach (Transform beam in structureHolder)
        {
            Transform label = beam.Find("StressLabel");
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }
        }
    }

    public void UndoLastAction()
    {
        audioSource.PlayOneShot(buttonClickSound);
        if (structuralElements.Count == 0) return;
        
        // Prevent undoing pre-built structures
        if (structuralElements.Count <= prebuiltElementCount)
        {
            Debug.Log("Cannot undo pre-built beams.");
            return;
        }

        // 1. Get the last element added
        Beam lastElement = structuralElements[structuralElements.Count - 1];
        int id1 = lastElement.node1;
        int id2 = lastElement.node2;

        // 2. Remove from Data (Logic similar to RemoveElement but specific index)
        structuralElements.RemoveAt(structuralElements.Count - 1);
        
        if (adjacencyList.ContainsKey(id1)) adjacencyList[id1].Remove(id2);
        if (adjacencyList.ContainsKey(id2)) adjacencyList[id2].Remove(id1);
        
        UpdateNodeColor(id1);
        UpdateNodeColor(id2);

        // 3. Remove Visuals
        if (structureHolder != null && nodeMap.ContainsKey(id1) && nodeMap.ContainsKey(id2))
        {
            Node n1 = nodeMap[id1];
            Node n2 = nodeMap[id2];
            
            string name1 = $"Beam({n1.x_index},{n1.y_index})-({n2.x_index},{n2.y_index})";
            string name2 = $"Beam({n2.x_index},{n2.y_index})-({n1.x_index},{n1.y_index})";
            
            Transform childToRemove = structureHolder.Find(name1);
            if (childToRemove == null) childToRemove = structureHolder.Find(name2);
            
            if (childToRemove != null)
            {
                Destroy(childToRemove.gameObject);
            }
        }
        
        Debug.Log("Undo: Removed last beam.");
    }

    public void SwitchToBuildMode()
    {
        currentMode = GameMode.Build;
        ResetSimulation();
    }

    public void RestartLevel()
    {
        audioSource.PlayOneShot(buttonClickSound);
        // 1. Clear Data down to prebuilt level
        if (structureHolder != null)
        {
            for (int i = structuralElements.Count - 1; i >= prebuiltElementCount; i--)
            {
                Beam el = structuralElements[i];
                int id1 = el.node1;
                int id2 = el.node2;
                
                // Remove from Adjacency
                if (adjacencyList.ContainsKey(id1)) adjacencyList[id1].Remove(id2);
                if (adjacencyList.ContainsKey(id2)) adjacencyList[id2].Remove(id1);

                // Remove Visuals
                if (nodeMap.ContainsKey(id1) && nodeMap.ContainsKey(id2))
                {
                    Node n1 = nodeMap[id1];
                    Node n2 = nodeMap[id2];
                    string name1 = $"Beam({n1.x_index},{n1.y_index})-({n2.x_index},{n2.y_index})";
                    string name2 = $"Beam({n2.x_index},{n2.y_index})-({n1.x_index},{n1.y_index})";
                    
                    Transform child = structureHolder.Find(name1);
                    if (child == null) child = structureHolder.Find(name2);
                    if (child != null) Destroy(child.gameObject);
                }
            }
        }
        
        // Truncate list
        if (structuralElements.Count > prebuiltElementCount)
        {
            structuralElements.RemoveRange(prebuiltElementCount, structuralElements.Count - prebuiltElementCount);
        }
        
        // Refresh all node colors
        foreach(var node in allNodes)
        {
            UpdateNodeColor(node.id);
        }

        Debug.Log("Level Restarted: Player beams cleared, pre-built preserved.");
    }
    
    public void SetStructuralMaterial(float area)
    {
        currentCrossSectionArea = area;
        float width = Mathf.Sqrt(area); 

        if (structureBuilder != null)
        {
            structureBuilder.beamWidth = width;
        }

        Debug.Log($"Material Changed: Area={area}, Width={width}");
    }

    public void InitializeStructure(GridController sourceGridController = null)
    {
        Transform phantomNodeHolder = transform.Find("NodeHolder");
        if (phantomNodeHolder != null)
        {
            if (Application.isPlaying) Destroy(phantomNodeHolder.gameObject);
            else DestroyImmediate(phantomNodeHolder.gameObject);
        }

        if (sourceGridController != null)
        {
            this.gridController = sourceGridController;
        }
        else if (this.gridController == null)
        {
             GameObject gridManagerObj = GameObject.Find("GridManager");
             if (gridManagerObj != null) this.gridController = gridManagerObj.GetComponent<GridController>();
        }

        if (this.gridController == null)
        {
             Debug.LogError("InitializeStructure: GridController is missing!");
             return;
        }

        RefreshNodes();
        
        adjacencyList.Clear();
        structuralElements.Clear();
        prebuiltElementCount = 0; 

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

        for (int i = structureHolder.childCount - 1; i >= 0; i--)
        {
            var child = structureHolder.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        if (gridController.currentLevel != null)
        {
            var level = gridController.currentLevel;
            loadMass = level.loadMass;
            
            anchorNodeIds.Clear();
            foreach(var coord in level.anchorCoords)
            {
                Node n = allNodes.FirstOrDefault(node => node.x_index == coord.x && node.y_index == coord.y);
                if (n != null) anchorNodeIds.Add(n.id);
            }
        }

        foreach (var node in allNodes)
        {
            adjacencyList[node.id] = new List<int>();
            if (anchorNodeIds.Contains(node.id))
            {
                node.isAnchor = true;
                node.GetComponent<Renderer>().material.color = Color.gray; 
            }
            else
            {
                node.isAnchor = false;
                node.GetComponent<Renderer>().material.color = Color.white; 
            }
        }

        // Spawn Pre-built beams
        if (gridController.currentLevel != null && gridController.currentLevel.prebuiltBeams != null)
        {
            foreach(var beam in gridController.currentLevel.prebuiltBeams)
            {
                Node startNode = allNodes.FirstOrDefault(n => n.x_index == beam.start.x && n.y_index == beam.start.y);
                Node endNode = allNodes.FirstOrDefault(n => n.x_index == beam.end.x && n.y_index == beam.end.y);
                
                if (startNode != null && endNode != null)
                {
                    // Pre-built beams use the DEFAULT medium area (0.0025f) unless specified otherwise.
                    // For now, we hardcode 0.0025f
                    AddElement(startNode.id, endNode.id, 0.0025f);
                    
                    // Force Create Visual (AddElement usually relies on Builder, but prebuilt needs manual)
                    if (structureBuilder != null && structureBuilder.beamPrefab != null)
                    {
                         GameObject beamObj = Instantiate(structureBuilder.beamPrefab, Vector3.zero, Quaternion.identity);
                         beamObj.transform.SetParent(structureHolder);
                         LineRenderer lr = beamObj.GetComponent<LineRenderer>();
                         float w = Mathf.Sqrt(0.0025f);
                         lr.startWidth = w; lr.endWidth = w;
                         lr.SetPosition(0, startNode.transform.position);
                         lr.SetPosition(1, endNode.transform.position);
                         
                         // Visual: Make pre-built beams darker
                         lr.startColor = Color.black;
                         lr.endColor = Color.black;
                         
                         beamObj.name = $"Beam({startNode.x_index},{startNode.y_index})-({endNode.x_index},{endNode.y_index})";
                    }
                }
            }
            prebuiltElementCount = structuralElements.Count;
        }

        SpawnGameElements();
    }

    public void SpawnGameElements()
    {
        if (elementsHolder != null)
        {
            for (int i = elementsHolder.childCount - 1; i >= 0; i--)
            {
                 var child = elementsHolder.GetChild(i);
                 DestroyImmediate(child.gameObject);
            }
        }

        int targetNodeID = gridController.GetTargetNodeID();
        Vector2 targetNodePos = gridController.GetTargetNodePosition();
        
        int targetX;
        if (gridController.currentLevel != null)
        {
            targetX = gridController.currentLevel.loadNodeCoords.x;
        }
        else
        {
            targetX = gridController.GetGridWidth() - (gridController.destroyWidth / 2);
        }

        float hScale = 0.5f;
        float aScale = 0.8f;
        
        if (gridController != null && gridController.currentLevel != null)
        {
            hScale = gridController.currentLevel.humanScale;
            aScale = gridController.currentLevel.anvilScale;
            this.anvilHangingDistance = gridController.currentLevel.anvilRopeLength;
        }

        Vector2 humanPos = gridController.GetNodePosition(targetX, 0); 
        humanPos += Vector2.up * hScale;

        Vector2 ropeStartPos = targetNodePos;
        Vector2 ropeEndPos = targetNodePos + (Vector2.down * anvilHangingDistance);
        
        Vector2 anvilPos = ropeEndPos - (Vector2.up * (aScale * 0.5f)); 

        if (humanPrefab != null)
        {
            humanInstance = Instantiate(humanPrefab, humanPos, Quaternion.identity);
            humanInstance.transform.localScale = Vector3.one * hScale;
        }
        else
        {
            humanInstance = new GameObject("Human (Placeholder)");
            humanInstance.transform.position = humanPos;
            humanInstance.transform.localScale = Vector3.one * hScale;
            
            SpriteRenderer sr = humanInstance.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCapsuleSprite();
            sr.sortingOrder = 5;
            humanInstance.GetComponent<Renderer>().material.color = Color.green;
        }
        if (elementsHolder != null) humanInstance.transform.SetParent(elementsHolder);

        if (anvilPrefab != null)
        {
            anvilInstance = Instantiate(anvilPrefab, anvilPos, Quaternion.identity);
            anvilInstance.transform.localScale = Vector3.one * aScale;
        }
        else
        {
            anvilInstance = new GameObject("Anvil (Placeholder)");
            anvilInstance.transform.position = anvilPos;
            anvilInstance.transform.localScale = Vector3.one * aScale;

            SpriteRenderer sr = anvilInstance.AddComponent<SpriteRenderer>();
            
            #if UNITY_EDITOR
            string path = "Assets/Levels/anvil.png";
            Sprite customAnvil = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (customAnvil == null)
            {
                Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    customAnvil = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            if (customAnvil != null)
            {
                sr.sprite = customAnvil;
                sr.color = Color.white;
            }
            else
            {
                sr.sprite = CreateSquareSprite();
                sr.color = Color.black;
            }
            #else
            sr.sprite = CreateSquareSprite();
            sr.color = Color.black;
            #endif
            
            sr.sortingOrder = 5;
        }
        if (elementsHolder != null) anvilInstance.transform.SetParent(elementsHolder);
        
        GameObject ropeObj = new GameObject("AnvilRope");
        if (elementsHolder != null) ropeObj.transform.SetParent(elementsHolder);
        
        anvilRope = ropeObj.AddComponent<LineRenderer>();
        anvilRope.startWidth = 0.05f;
        anvilRope.endWidth = 0.05f;
        anvilRope.positionCount = 2;
        anvilRope.material = new Material(Shader.Find("Sprites/Default"));
        anvilRope.startColor = Color.gray;
        anvilRope.endColor = Color.gray;
        anvilRope.sortingOrder = 0;
        
        anvilRope.SetPosition(0, ropeStartPos);
        anvilRope.SetPosition(1, ropeEndPos);
    }

    private Sprite CreateCapsuleSprite()
    {
        int w = 64;
        int h = 128;
        Texture2D tex = new Texture2D(w, h);
        Color[] colors = new Color[w * h];
        Color white = Color.white;
        Color clear = Color.clear;
        float radius = w / 2f;
        
        for(int y=0; y<h; y++)
        {
            for(int x=0; x<w; x++)
            {
                float cx = radius;
                float cy_bottom = radius;
                float cy_top = h - radius;
                
                float py = y;
                float dy = 0;
                if (py < cy_bottom) dy = cy_bottom - py;
                else if (py > cy_top) dy = py - cy_top;
                
                float dx = Mathf.Abs(x - cx);
                
                if (dx*dx + dy*dy <= radius*radius)
                    colors[y*w + x] = white;
                else
                    colors[y*w + x] = clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,w,h), new Vector2(0.5f, 0.5f), 64f);
    }

    private Sprite CreateSquareSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    public void RefreshNodes()
    {
        var newNodes = gridController.GetNodes();
        
        if (newNodes == null || newNodes.Count == 0)
        {
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
    }
    
    public void AddElement(int nodeId1, int nodeId2, float overrideArea = -1f)
    {
        float area = (overrideArea > 0f) ? overrideArea : currentCrossSectionArea;
        
        structuralElements.Add(new Beam { node1 = nodeId1, node2 = nodeId2, area = area });
        
        if (!adjacencyList.ContainsKey(nodeId1)) adjacencyList[nodeId1] = new List<int>();
        if (!adjacencyList.ContainsKey(nodeId2)) adjacencyList[nodeId2] = new List<int>();
        adjacencyList[nodeId1].Add(nodeId2);
        adjacencyList[nodeId2].Add(nodeId1);
        
        UpdateNodeColor(nodeId1);
        UpdateNodeColor(nodeId2);
    }

    public void RemoveElement(int nodeId1, int nodeId2)
    {
        structuralElements.RemoveAll(e => (e.node1 == nodeId1 && e.node2 == nodeId2) || (e.node1 == nodeId2 && e.node2 == nodeId1));
        if (adjacencyList.ContainsKey(nodeId1)) adjacencyList[nodeId1].Remove(nodeId2);
        if (adjacencyList.ContainsKey(nodeId2)) adjacencyList[nodeId2].Remove(nodeId1);
        
        UpdateNodeColor(nodeId1);
        UpdateNodeColor(nodeId2);
    }

    void UpdateNodeColor(int nodeId)
    {
        if (nodeMap.TryGetValue(nodeId, out Node n))
        {
            SpriteRenderer r = n.GetComponent<SpriteRenderer>();
            if (r == null) return;

            if (n.isAnchor)
            {
                if (n.defaultSprite != null) r.sprite = n.defaultSprite;
                r.color = Color.gray;
            }
            else
            {
                bool isConnected = adjacencyList.ContainsKey(nodeId) && adjacencyList[nodeId].Count > 0;
                
                if (isConnected)
                {
                    if (connectedNodeSprite != null)
                    {
                        r.sprite = connectedNodeSprite;
                        r.color = Color.white;
                    }
                    else
                    {
                        if (n.defaultSprite != null) r.sprite = n.defaultSprite;
                        r.color = Color.black;
                    }
                }
                else
                {
                    if (n.defaultSprite != null) r.sprite = n.defaultSprite;
                    r.color = Color.white;
                }
            }
        }
    }

    public bool DoesElementExist(int nodeId1, int nodeId2)
    {
        return structuralElements.Any(e => (e.node1 == nodeId1 && e.node2 == nodeId2) || (e.node1 == nodeId2 && e.node2 == nodeId1));
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
            int id1 = element.node1;
            int id2 = element.node2;

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
        audioSource.PlayOneShot(buttonClickSound);
        currentMode = GameMode.Simulate;
        Debug.Log("Starting simulation...");

        foreach (var node in allNodes)
        {
            bool isConnected = false;
            if (adjacencyList.ContainsKey(node.id) && adjacencyList[node.id].Count > 0)
            {
                isConnected = true;
            }
            
            if (!node.isAnchor && !isConnected)
            {
                node.gameObject.SetActive(false);
            }
        }

        StructuralAnalysis.AnalysisResult result = PerformAnalysis();
        lastAnalysisResult = result;

        if (result.IsStable)
        {
            Debug.Log("Structure is STABLE.");
            
            HashSet<int> activeNodeIDs = new HashSet<int>();
            foreach (var el in structuralElements) { activeNodeIDs.Add(el.node1); activeNodeIDs.Add(el.node2); }
            foreach (var anchorID in anchorNodeIds) { if (nodeMap.ContainsKey(anchorID)) activeNodeIDs.Add(anchorID); }
            if (nodeMap.ContainsKey(loadNodeId)) activeNodeIDs.Add(loadNodeId);

            var sortedIDs = activeNodeIDs.OrderBy(x => x).ToList(); 
            List<Node> activeNodesList = new List<Node>();
            List<Vector2> activePositions = new List<Vector2>();
            
            foreach (int id in sortedIDs)
            {
                if (nodeMap.TryGetValue(id, out Node node))
                {
                     activeNodesList.Add(node);
                     activePositions.Add(node.transform.position);
                }
            }
            
            foreach (int id in sortedIDs)
            {
                if (nodeMap.TryGetValue(id, out Node node))
                {
                     activeNodesList.Add(node);
                     activePositions.Add(node.transform.position);
                }
            }
            
            float normalizationFactor = gridController.lockedCellSize;
            ApplyDeformation(activeNodesList, activePositions, result.Displacements, normalizationFactor);
            
            bool failed = false;
            List<int> snappedIndices = new List<int>();
            List<int> buckledIndices = new List<int>();
            if (result.MemberStressPercentages != null)
            {
                for (int i = 0; i < result.MemberStressPercentages.Length; i++)
                {
                    if (result.MemberStressPercentages[i] >= 100f)
                    {
                        failed = true;
                        if (result.MemberForces[i] < 0)
                        {
                            buckledIndices.Add(i);
                        }
                        else
                        {
                            snappedIndices.Add(i);
                        }
                    }
                }
            }
            
            if (failed)
            {
                Debug.LogError("Structure FAILED: Materials yielded/buckled.");
                CollapseSequence(snappedIndices, buckledIndices);
            }
            else
            {
                Debug.Log("SUCCESS: Human Saved!");
                if (humanInstance != null) 
                    humanInstance.GetComponent<Renderer>().material.color = Color.blue; 
                
                StartCoroutine(LevelCompleteSequence());
            }
        }
        else
        {
            Debug.LogError("Structure is UNSTABLE (Mechanism detected)!");
            CollapseSequence(new List<int>(), new List<int>()); 
        }

        if (result.MemberForces != null)
            Debug.Log("Member forces: " + string.Join(", ", result.MemberForces));
            
        if (result.MemberStressPercentages != null)
            Debug.Log("Stress percentages: " + string.Join(", ", result.MemberStressPercentages));
    }

    IEnumerator LevelCompleteSequence()
    {
        if (uiManager != null) uiManager.ShowLevelComplete();
        if (levelCompleteSound != null)
        {
            audioSource.PlayOneShot(levelCompleteSound, levelCompleteVolume);
        }
        
        yield return new WaitForSeconds(3.5f);
        
        if (uiManager != null) uiManager.HideLevelComplete();
        
        currentMode = GameMode.Build;
        ResetSimulation(); 
        
        if (levelManager != null) 
             levelManager.NextLevel();
        else
             RestartLevel(); 
    }

    public StructuralAnalysis.AnalysisResult PerformAnalysis()
    {
        loadNodeId = gridController.GetTargetNodeID();

        HashSet<int> activeNodeIDs = new HashSet<int>();
        
        foreach (var el in structuralElements)
        {
            activeNodeIDs.Add(el.node1);
            activeNodeIDs.Add(el.node2);
        }
        
        foreach (var anchorID in anchorNodeIds)
        {
            if (nodeMap.ContainsKey(anchorID)) activeNodeIDs.Add(anchorID);
        }
        
        if (nodeMap.ContainsKey(loadNodeId)) activeNodeIDs.Add(loadNodeId);

        var sortedActiveIDs = activeNodeIDs.OrderBy(x => x).ToList();

        List<Vector2> activePositions = new List<Vector2>();
        Dictionary<int, int> idToIndex = new Dictionary<int, int>();
        
        int indexCounter = 0;
        foreach (int id in sortedActiveIDs)
        {
            if (nodeMap.TryGetValue(id, out Node node))
            {
                activePositions.Add(node.transform.position);
                idToIndex[id] = indexCounter;
                indexCounter++;
            }
        }
        
        List<int[]> analysisElements = new List<int[]>();
        List<float> elementAreas = new List<float>();

        foreach (var el in structuralElements)
        {
            if (idToIndex.ContainsKey(el.node1) && idToIndex.ContainsKey(el.node2))
            {
                analysisElements.Add(new int[] { idToIndex[el.node1], idToIndex[el.node2] });
                elementAreas.Add(el.area);
            }
        }

        List<int> analysisFixedNodes = new List<int>();
        foreach (var anchorID in anchorNodeIds)
        {
            if (idToIndex.ContainsKey(anchorID))
                analysisFixedNodes.Add(idToIndex[anchorID]);
        }

        var analysisLoads = new Dictionary<int, Vector2>();
        if (idToIndex.ContainsKey(loadNodeId))
        {
            analysisLoads.Add(idToIndex[loadNodeId], gravity * loadMass);
        }

        float normalizationFactor = gridController.lockedCellSize;
        List<Vector2> normalizedPositions = activePositions.Select(p => p / normalizationFactor).ToList();

        return StructuralAnalysis.RunAnalysis(
            normalizedPositions,
            analysisElements,
            analysisFixedNodes,
            analysisLoads,
            youngsModulus,
            elementAreas.ToArray(), 
            memberYieldStress,
            beamDensity
        );
    }

    void ApplyDeformation(List<Node> nodes, List<Vector2> originalPositions, Vector2[] displacements, float scale = 1.0f)
    {
        if (displacements == null || nodes.Count != displacements.Length) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 newPos = originalPositions[i] + displacements[i] * scale;
            nodes[i].transform.position = newPos;
        }
        
        if (anvilRope != null && nodeMap.ContainsKey(loadNodeId))
        {
            Vector3 newLoadPos = nodeMap[loadNodeId].transform.position;
            Vector3 ropeEndPos = newLoadPos + (Vector3.down * anvilHangingDistance);
            
            anvilRope.SetPosition(0, newLoadPos);
            anvilRope.SetPosition(1, ropeEndPos);
            
            if (anvilInstance != null)
            {
                float currentAScale = anvilInstance.transform.localScale.y; 
                anvilInstance.transform.position = ropeEndPos - (Vector3.up * (currentAScale * 0.5f));
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

    void CollapseSequence(List<int> snappedIndices = null, List<int> buckledIndices = null)
    {
        if (humanInstance != null) 
            humanInstance.GetComponent<Renderer>().material.color = Color.red; 
            
        Debug.Log("GAME OVER: Structure Collapsing!");
        isCollapsing = true;
        if (failMusic != null)
        {
            audioSource.PlayOneShot(failMusic, failMusicVolume);
        }
        EnablePhysicsCollapse(snappedIndices, buckledIndices);
    }

    private void CreateBrokenBeamVisual(Transform t1, Transform t2)
    {
        GameObject obj = new GameObject("BrokenBeamSegment");
        brokenParts.Add(obj);
        
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        
        float width = 0.05f;
        if (structureBuilder != null) width = structureBuilder.beamWidth;
        lr.startWidth = width; 
        lr.endWidth = width;
        
        lr.positionCount = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.grey;
        lr.endColor = Color.grey;

        brokenBeamVisuals.Add(new BrokenBeamVisual { lr = lr, t1 = t1, t2 = t2 });
    }

    void EnablePhysicsCollapse(List<int> snappedIndices, List<int> buckledIndices)
    {
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
                rb.mass = 10f; 
                rb.linearDamping = 1f; 
                rb.angularDamping = 1f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; 
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            
            CircleCollider2D col = node.gameObject.GetComponent<CircleCollider2D>();
            if (col == null)
            {
                col = node.gameObject.AddComponent<CircleCollider2D>();
                col.radius = 0.2f;
            }
            
            if (storedNodeRadius == -1f) storedNodeRadius = col.radius;
            col.radius = 0.1f;
        }

        if (snappedIndices == null) snappedIndices = new List<int>();
        if (buckledIndices == null) buckledIndices = new List<int>();

        for (int i = 0; i < structuralElements.Count; i++)
        {
            Beam element = structuralElements[i];
            int id1 = element.node1;
            int id2 = element.node2;

            if (nodeMap.TryGetValue(id1, out Node n1) && nodeMap.TryGetValue(id2, out Node n2))
            {
                if (snappedIndices.Contains(i))
                {
                    Debug.Log($"Snapping beam between {id1} and {id2}");
                    HideOriginalBeamVisual(n1, n2);

                    Vector3 midPoint = (n1.transform.position + n2.transform.position) / 2f;
                    
                    GameObject nodeM1 = Instantiate(gridController.nodePrefab, midPoint, Quaternion.identity);
                    GameObject nodeM2 = Instantiate(gridController.nodePrefab, midPoint, Quaternion.identity);
                    nodeM1.transform.localScale = Vector3.one * gridController.nodeScale;
                    nodeM2.transform.localScale = Vector3.one * gridController.nodeScale;
                    
                    if (nodeM1.GetComponent<CircleCollider2D>() != null) nodeM1.GetComponent<CircleCollider2D>().radius = 0.1f;
                    if (nodeM2.GetComponent<CircleCollider2D>() != null) nodeM2.GetComponent<CircleCollider2D>().radius = 0.1f;

                    brokenParts.Add(nodeM1); brokenParts.Add(nodeM2);

                    Rigidbody2D rbM1 = nodeM1.AddComponent<Rigidbody2D>();
                    Rigidbody2D rbM2 = nodeM2.AddComponent<Rigidbody2D>();
                    rbM1.mass = 5f; rbM2.mass = 5f;
                    
                    CreateDistanceJoint(n1.gameObject, rbM1, Vector2.Distance(n1.transform.position, midPoint));
                    CreateDistanceJoint(n2.gameObject, rbM2, Vector2.Distance(n2.transform.position, midPoint));
                    
                    CreateBrokenBeamVisual(n1.transform, nodeM1.transform);
                    CreateBrokenBeamVisual(n2.transform, nodeM2.transform);
                }
                else if (buckledIndices.Contains(i))
                {
                    Debug.Log($"Buckling beam between {id1} and {id2}");
                    HideOriginalBeamVisual(n1, n2);

                    Vector3 midPoint = (n1.transform.position + n2.transform.position) / 2f;
                    
                    GameObject nodeM = Instantiate(gridController.nodePrefab, midPoint, Quaternion.identity);
                    nodeM.transform.localScale = Vector3.one * gridController.nodeScale;
                    
                    if (nodeM.GetComponent<CircleCollider2D>() != null) nodeM.GetComponent<CircleCollider2D>().radius = 0.1f;

                    brokenParts.Add(nodeM);

                    Rigidbody2D rbM = nodeM.AddComponent<Rigidbody2D>();
                    rbM.mass = 5f;
                    rbM.linearDamping = 0.5f;

                    CreateDistanceJoint(n1.gameObject, rbM, Vector2.Distance(n1.transform.position, midPoint));
                    CreateDistanceJoint(n2.gameObject, rbM, Vector2.Distance(n2.transform.position, midPoint));

                    CreateBrokenBeamVisual(n1.transform, nodeM.transform);
                    CreateBrokenBeamVisual(n2.transform, nodeM.transform);
                }
                else
                {
                    DistanceJoint2D joint = n1.gameObject.AddComponent<DistanceJoint2D>();
                    joint.connectedBody = n2.GetComponent<Rigidbody2D>();
                    joint.autoConfigureDistance = true; 
                    joint.maxDistanceOnly = false; 
                    joint.enableCollision = false; 
                }
            }
        }

        if (anvilInstance != null)
        {
            var rb3d = anvilInstance.GetComponent<Rigidbody>();
            if (rb3d != null) DestroyImmediate(rb3d);
            var col3d = anvilInstance.GetComponent<Collider>();
            if (col3d != null) DestroyImmediate(col3d);

            Rigidbody2D rb2d = anvilInstance.GetComponent<Rigidbody2D>();
            if (rb2d == null) rb2d = anvilInstance.AddComponent<Rigidbody2D>();
            rb2d.mass = 500f; 
            rb2d.linearDamping = 0.5f;
            rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            
            if (anvilInstance.GetComponent<BoxCollider2D>() == null)
                 anvilInstance.AddComponent<BoxCollider2D>();
            
            if (nodeMap.TryGetValue(loadNodeId, out Node targetNode))
            {
                DistanceJoint2D rope = targetNode.gameObject.AddComponent<DistanceJoint2D>();
                rope.connectedBody = rb2d;
                rope.autoConfigureDistance = true; 
                rope.maxDistanceOnly = true; 
                rope.enableCollision = false;
            }
        }
    }

    private void HideOriginalBeamVisual(Node n1, Node n2)
    {
        string name1 = $"Beam({n1.x_index},{n1.y_index})-({n2.x_index},{n2.y_index})";
        string name2 = $"Beam({n2.x_index},{n2.y_index})-({n1.x_index},{n1.y_index})";
        Transform beamT = structureHolder.Find(name1);
        if (beamT == null) beamT = structureHolder.Find(name2);
        if (beamT != null) beamT.gameObject.SetActive(false); 
    }

    private void CreateDistanceJoint(GameObject obj, Rigidbody2D targetRb, float dist)
    {
        DistanceJoint2D j = obj.AddComponent<DistanceJoint2D>();
        j.connectedBody = targetRb;
        j.autoConfigureDistance = false;
        j.distance = dist;
        j.maxDistanceOnly = false;
    }
    
    void ResetSimulation()
    {
         currentMode = GameMode.Build; 
         isCollapsing = false;
         Debug.Log("Simulation Reset. Back to Build Mode. Undo history preserved.");

         foreach(var obj in brokenParts)
         {
             if (obj != null) Destroy(obj);
         }
         brokenParts.Clear();
         brokenBeamVisuals.Clear();
         
         if (structureHolder != null)
         {
             foreach(Transform child in structureHolder)
             {
                 child.gameObject.SetActive(true);
             }
         }

         if (nodePositions != null && allNodes != null)
         {
             for (int i = 0; i < allNodes.Count; i++)
             {
                 Node n = allNodes[i];
                 n.gameObject.SetActive(true); 
                 
                 if (storedNodeRadius != -1f)
                 {
                     var col = n.GetComponent<CircleCollider2D>();
                     if (col != null) col.radius = storedNodeRadius;
                 }

                 var joints = n.GetComponents<Joint2D>();
                 foreach(var j in joints) Destroy(j);
                 
                 var rb = n.GetComponent<Rigidbody2D>();
                 if (rb != null) Destroy(rb);
                 
                 if (i < nodePositions.Count)
                    n.transform.position = nodePositions[i];
             }
         }
         
         storedNodeRadius = -1f;
         
         if (anvilInstance != null)
         {
             var rb2d = anvilInstance.GetComponent<Rigidbody2D>();
             if (rb2d != null) Destroy(rb2d);
         }

         SpawnGameElements();
         UpdateBeamVisuals();
    }

    public float GetMaterialLimit(float area)
    {
        if (gridController == null || gridController.currentLevel == null) return 9999f;
        
        // Approximate float comparison
        if (Mathf.Abs(area - 0.0049f) < 0.0001f) return gridController.currentLevel.limit7x7;
        if (Mathf.Abs(area - 0.0025f) < 0.0001f) return gridController.currentLevel.limit5x5;
        if (Mathf.Abs(area - 0.0009f) < 0.0001f) return gridController.currentLevel.limit3x3;
        
        return 9999f;
    }

    public float GetBeamLengthByArea(float targetArea, float tolerance = 0.0001f)
    {
        float totalLen = 0f;
        for (int i = prebuiltElementCount; i < structuralElements.Count; i++)
        {
            Beam el = structuralElements[i];
            
            // Check if this beam matches the requested area
            if (Mathf.Abs(el.area - targetArea) < tolerance)
            {
                if (nodeMap.TryGetValue(el.node1, out Node n1) && nodeMap.TryGetValue(el.node2, out Node n2))
                {
                    // Logical Grid Distance
                    float dx = n1.x_index - n2.x_index;
                    float dy = n1.y_index - n2.y_index;
                    totalLen += Mathf.Sqrt(dx * dx + dy * dy);
                }
            }
        }
        return totalLen;
    }
}
