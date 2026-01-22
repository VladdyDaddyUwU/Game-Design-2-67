using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StructureBuilder : MonoBehaviour
{
    public GameObject beamPrefab; // Prefab with a LineRenderer
    public LayerMask nodeLayer;
    public GameManager gameManager;
    public float beamWidth = 0.05f;

    private Node startNode;
    private LineRenderer tempLine;
    private bool isDrawing = false;
    private Camera mainCamera;
    private AudioSource audioSource;
    private AudioClip placingSound;
    private AudioClip incorrectPlacementSound;
    [Range(0f, 1f)] public float placingVolume = 1f;
    [Range(0f, 1f)] public float incorrectPlacementVolume = 1f;

    void Start()
    {
        mainCamera = Camera.main;
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        placingSound = Resources.Load<AudioClip>("placing");
        incorrectPlacementSound = Resources.Load<AudioClip>("incorect-placement");
    }

    void Update()
    {
        if (gameManager.currentMode == GameMode.Dialogue) return; // Block input during dialogue
        if (gameManager.currentMode != GameMode.Build) return;

        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Node clickedNode = GetNodeUnderMouse();
            if (clickedNode != null)
            {
                StartDrawing(clickedNode);
            }
        }
        else if (Input.GetMouseButton(0) && isDrawing)
        {
            UpdateDrawing();
        }
        else if (Input.GetMouseButtonUp(0) && isDrawing)
        {
            FinishDrawing();
        }
    }

    private void StartDrawing(Node node)
    {
        // Connectivity Check: Ensure the starting node is connected to an anchor
        if (!gameManager.IsNodeConnectedToAnchor(node.id))
        {
            Debug.LogWarning("Cannot start building from a disconnected node.");
            return;
        }

        startNode = node;
        isDrawing = true;

        // Create a temporary line renderer for visual feedback
        GameObject tempLineObj = new GameObject("TempLine");
        tempLine = tempLineObj.AddComponent<LineRenderer>();
        tempLine.startWidth = beamWidth;
        tempLine.endWidth = beamWidth;
        tempLine.positionCount = 2;
        // A simple material so the line is visible
        tempLine.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        tempLine.startColor = Color.white;
        tempLine.endColor = Color.white;
        tempLine.SetPosition(0, startNode.transform.position);
    }

    private bool CheckMaterialAvailability(Node n1, Node n2)
    {
        if (gameManager == null) return true;

        float currentArea = gameManager.currentCrossSectionArea;
        float limit = gameManager.GetMaterialLimit(currentArea);
        float used = gameManager.GetBeamLengthByArea(currentArea);

        // Calculate potential length using grid indices (Logical Distance)
        float dx = n1.x_index - n2.x_index;
        float dy = n1.y_index - n2.y_index;
        float newLength = Mathf.Sqrt(dx * dx + dy * dy);

        if (used + newLength > limit)
        {
            return false;
        }
        return true;
    }

    private void UpdateDrawing()
    {
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        tempLine.SetPosition(1, mousePos);

        Node endNode = GetNodeUnderMouse();

        if (endNode == null || endNode == startNode)
        {
            // Default: Drawing in empty space
            tempLine.startColor = Color.white;
            tempLine.endColor = Color.white;
            return;
        }

        // Validity Checks for Color Feedback
        bool isValid = true;

        // 1. Connection to anchor/structure check
        if (!gameManager.IsNodeConnectedToAnchor(endNode.id) && !gameManager.IsNodeConnectedToAnchor(startNode.id))
        {
            isValid = false;
        }
        // 2. Duplicate check
        else if (gameManager.DoesElementExist(startNode.id, endNode.id))
        {
            isValid = false;
        }
        // 3. Overlap check
        else if (gameManager.IsNewElementOverlappingExisting(startNode, endNode))
        {
            isValid = false;
        }
        // 4. Dead Zone Check
        else if (gameManager.gridController != null && gameManager.gridController.IsSegmentIntersectingDeadZone(startNode.transform.position, endNode.transform.position))
        {
            isValid = false;
        }
        // 5. Material Limit Check
        else if (!CheckMaterialAvailability(startNode, endNode))
        {
            isValid = false;
        }

        Color feedbackColor = isValid ? Color.green : Color.red;
        tempLine.startColor = feedbackColor;
        tempLine.endColor = feedbackColor;
    }

    private void FinishDrawing()
    {
        Node endNode = GetNodeUnderMouse();

        if (tempLine != null)
        {
            Destroy(tempLine.gameObject);
        }

        isDrawing = false;
        if (startNode == null || endNode == null || startNode == endNode)
        {
            if (incorrectPlacementSound != null) audioSource.PlayOneShot(incorrectPlacementSound, incorrectPlacementVolume);
            return;
        }

        // Final connectivity check for the end node
        if (!gameManager.IsNodeConnectedToAnchor(endNode.id) && !gameManager.IsNodeConnectedToAnchor(startNode.id))
        {
             Debug.LogWarning("One of the nodes must be connected to the main structure.");
             if (incorrectPlacementSound != null) audioSource.PlayOneShot(incorrectPlacementSound, incorrectPlacementVolume);
             return;
        }
        
        // Check if this element already exists
        if (gameManager.DoesElementExist(startNode.id, endNode.id))
        {
            Debug.LogWarning("This beam already exists.");
            if (incorrectPlacementSound != null) audioSource.PlayOneShot(incorrectPlacementSound, incorrectPlacementVolume);
            return;
        }

        // Dead Zone Check
        if (gameManager.gridController != null && gameManager.gridController.IsSegmentIntersectingDeadZone(startNode.transform.position, endNode.transform.position))
        {
            Debug.LogWarning("Cannot build through the restricted area (Dead Zone).");
            if (gameManager.uiManager != null) gameManager.uiManager.ShowWarning("CANNOT BUILD OVER DEADZONE");
            if (incorrectPlacementSound != null) audioSource.PlayOneShot(incorrectPlacementSound, incorrectPlacementVolume);
            return;
        }

        // Material Limit Check
        if (!CheckMaterialAvailability(startNode, endNode))
        {
            Debug.LogWarning("Not enough material!");
            if (gameManager.uiManager != null) gameManager.uiManager.ShowWarning("RAN OUT OF THIS MATERIAL");
            if (incorrectPlacementSound != null) audioSource.PlayOneShot(incorrectPlacementSound, incorrectPlacementVolume);
            return;
        }

        // Add the structural element first
        gameManager.AddElement(startNode.id, endNode.id);

        if (placingSound != null)
        {
            audioSource.PlayOneShot(placingSound, placingVolume);
        }

        // Create the permanent visual for the beam
        GameObject beamObj = null;
        if (beamPrefab != null)
        {
            beamObj = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity);
            
            // Parent to the manager's holder
            if (gameManager.structureHolder != null)
            {
                beamObj.transform.SetParent(gameManager.structureHolder);
            }

            LineRenderer beamLine = beamObj.GetComponent<LineRenderer>();
            beamLine.startWidth = beamWidth;
            beamLine.endWidth = beamWidth;
            beamLine.SetPosition(0, startNode.transform.position);
            beamLine.SetPosition(1, endNode.transform.position);
            
            // New Naming Convention: Beam(x1,y1)-(x2,y2)
            beamObj.name = $"Beam({startNode.x_index},{startNode.y_index})-({endNode.x_index},{endNode.y_index})";
        }

        // Post-placement validation: Check for overlaps.
        // We check if the NEWLY added beam overlaps with any OLDER beam.
        // IsElementContained iterates through ALL beams. We need to be careful not to match the beam against itself.
        // Actually, the previous logic compared the "new candidate" against "existing list". 
        // Now the "new candidate" is IN the list.
        // So we need to modify IsElementContained or call a specific check here.
        
        // Let's use a slightly modified approach: Check if the *just added* beam overlaps with any *other* beam.
        if (CheckAndRemoveIfInvalid(startNode, endNode, beamObj))
        {
             if (incorrectPlacementSound != null) audioSource.PlayOneShot(incorrectPlacementSound, incorrectPlacementVolume);
             return;
        }
        
        startNode = null;
    }

    private bool CheckAndRemoveIfInvalid(Node node1, Node node2, GameObject beamObj)
    {
        if (gameManager.IsNewElementOverlappingExisting(node1, node2)) 
        {
            Debug.LogWarning("Invalid beam: Overlaps with existing structure. Removing.");
            gameManager.RemoveElement(node1.id, node2.id);
            if (beamObj != null) Destroy(beamObj);
            startNode = null; // Reset state
            return true;
        }
        return false;
    }

    private Node GetNodeUnderMouse()
    {
        RaycastHit2D hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(Input.mousePosition), Vector2.zero, Mathf.Infinity, nodeLayer);
        if (hit.collider != null)
        {
            return hit.collider.GetComponent<Node>();
        }
        return null;
    }
}