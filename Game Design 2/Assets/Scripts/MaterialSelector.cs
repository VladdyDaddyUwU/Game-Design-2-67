using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaterialSelector : MonoBehaviour
{
    public GameManager gameManager;
    public GameObject materialDisplay;

    [Header("Material Buttons")]
    public GameObject material1_7x7; // Material 1
    public GameObject material2_5x5; // Material 2
    public GameObject material3_3x3; // Material 3

    [Header("Meters Left Labels (TMP)")]
    public TMP_Text label7x7;
    public TMP_Text label5x5;
    public TMP_Text label3x3;

    [Header("Meters Left Labels (Legacy)")]
    public Text legacyLabel7x7;
    public Text legacyLabel5x5;
    public Text legacyLabel3x3;

    // Standard areas: 7x7=0.0049, 5x5=0.0025, 3x3=0.0009
    private readonly float areaLarge = 0.0049f;
    private readonly float areaMedium = 0.0025f;
    private readonly float areaSmall = 0.0009f;

    private Color originalColor7x7 = Color.black;
    private Color originalColor5x5 = Color.black;
    private Color originalColor3x3 = Color.black;

    private Image frame1, frame2, frame3;
    private Color originalFrameColor = Color.white;
    private Color highlightColor = new Color(249f/255f, 233f/255f, 155f/255f); // Gold-ish

    void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();

        // Use this object as container if nothing assigned
        if (materialDisplay == null) materialDisplay = this.gameObject;

        // 1. Find Buttons if not manually assigned
        if (material1_7x7 == null) material1_7x7 = FindChild(materialDisplay, "Material 1");
        if (material2_5x5 == null) material2_5x5 = FindChild(materialDisplay, "Material 2");
        if (material3_3x3 == null) material3_3x3 = FindChild(materialDisplay, "Material 3");
        
        // 2. Find Labels specifically named "Text (Metres Left)"
        FindLabels(material1_7x7, ref label7x7, ref legacyLabel7x7, ref originalColor7x7);
        FindLabels(material2_5x5, ref label5x5, ref legacyLabel5x5, ref originalColor5x5);
        FindLabels(material3_3x3, ref label3x3, ref legacyLabel3x3, ref originalColor3x3);

        // 3. Find Metal Frames for highlighting
        frame1 = FindFrame(material1_7x7);
        frame2 = FindFrame(material2_5x5);
        frame3 = FindFrame(material3_3x3);

        if (frame1 != null) originalFrameColor = frame1.color; // Assume all start same

        // Debug: Report status
        Debug.Log($"[MaterialSelector Setup] Found Material 1: {(material1_7x7 != null ? material1_7x7.name : "NULL")}");
        Debug.Log($"[MaterialSelector Setup] Found Material 2: {(material2_5x5 != null ? material2_5x5.name : "NULL")}");
        Debug.Log($"[MaterialSelector Setup] Found Material 3: {(material3_3x3 != null ? material3_3x3.name : "NULL")}");

        // Optional: Initial Highlight
        HighlightButton(areaMedium);
    }

    Image FindFrame(GameObject parent)
    {
        if (parent == null) return null;
        // Look for Canvas/Metal Frame pattern
        Transform canvas = parent.transform.Find("Canvas");
        if (canvas != null)
        {
            Transform frame = canvas.Find("Metal Frame");
            if (frame != null) return frame.GetComponent<Image>();
        }
        return null;
    }

    void FindLabels(GameObject parent, ref TMP_Text tmpLabel, ref Text legacyLabel, ref Color originalColor)
    {
        if (parent == null) return;
        Transform t = parent.transform.Find("Text (Metres Left)");
        
        // Recursive search fallback
        if (t == null)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>())
            {
                if (child.name == "Text (Metres Left)") 
                {
                    t = child;
                    break;
                }
            }
        }

        if (t != null)
        {
            tmpLabel = t.GetComponent<TMP_Text>();
            legacyLabel = t.GetComponent<Text>();
            
            // Capture original color
            if (tmpLabel != null) originalColor = tmpLabel.color;
            else if (legacyLabel != null) originalColor = legacyLabel.color;

            // Ensure active
            t.gameObject.SetActive(true);

            Debug.Log($"[MaterialSelector] Found Label '{t.name}' inside '{parent.name}'. Original Color: {originalColor}");

            if (tmpLabel == null && legacyLabel == null)
            {
                Debug.LogWarning($"Found 'Text (Metres Left)' under {parent.name} but it has NO Text component!");
            }
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left Click
        {
            CheckClick();
        }

        UpdateMetersLeft();
    }

    void UpdateMetersLeft()
    {
        if (gameManager == null || gameManager.gridController == null || gameManager.gridController.currentLevel == null) return;

        var level = gameManager.gridController.currentLevel;

        UpdateSingleLabel(label7x7, legacyLabel7x7, level.limit7x7, 0.0049f, originalColor7x7);
        UpdateSingleLabel(label5x5, legacyLabel5x5, level.limit5x5, 0.0025f, originalColor5x5);
        UpdateSingleLabel(label3x3, legacyLabel3x3, level.limit3x3, 0.0009f, originalColor3x3);
    }

    void UpdateSingleLabel(TMP_Text tmp, Text legacy, float limit, float area, Color baseColor)
    {
        float left = limit - gameManager.GetBeamLengthByArea(area);
        string text = $"{left:F1} m left";
        Color color = (left < 0) ? Color.red : baseColor;

        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;
        }
        if (legacy != null)
        {
            legacy.text = text;
            legacy.color = color;
        }
    }

    void CheckClick()
    {
        bool buttonClicked = false;
        
        // 1. Try 2D Physics
        Vector2 mousePos2D = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D[] hits2D = Physics2D.RaycastAll(mousePos2D, Vector2.zero);
        
        foreach (RaycastHit2D hit in hits2D)
        {
            if (hit.collider != null)
            {
                if (HandleHit(hit.collider.gameObject))
                {
                    buttonClicked = true;
                    return; // Stop processing if we clicked a button
                }
            }
        }

        // 2. Try 3D Physics (if 2D didn't catch it)
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits3D;
        hits3D = Physics.RaycastAll(ray);

        foreach (RaycastHit hit in hits3D)
        {
             if (HandleHit(hit.collider.gameObject))
             {
                 buttonClicked = true;
                 return;
             }
        }

        if (!buttonClicked)
        {
            Debug.Log("Clicked, but no Material Button matched.");
        }
    }

    bool HandleHit(GameObject hitObj)
    {
        if (hitObj == material1_7x7) 
        {
            OnMaterialClicked(areaLarge);
            return true;
        }
        else if (hitObj == material2_5x5) 
        {
            OnMaterialClicked(areaMedium);
            return true;
        }
        else if (hitObj == material3_3x3) 
        {
            OnMaterialClicked(areaSmall);
            return true;
        }
        return false;
    }

    GameObject FindChild(GameObject parent, string name)
    {
        if (parent == null) return null;
        Transform t = parent.transform.Find(name);
        return (t != null) ? t.gameObject : null;
    }

    void SetupButton(GameObject obj)
    {
        if (obj == null) return;

        // Ensure it has a collider for mouse detection
        BoxCollider2D col = obj.GetComponent<BoxCollider2D>();
        if (col == null)
        {
            col = obj.AddComponent<BoxCollider2D>();
            // Give it a default size in case there is no sprite to auto-size to
            col.size = new Vector2(2f, 1f); 
        }
    }

    void OnMaterialClicked(float area)
    {
        Debug.Log($"Material Selector: Button clicked! Setting Member Cross Section Area to: {area}");
        if (gameManager != null)
        {
            gameManager.SetStructuralMaterial(area);
        }
        HighlightButton(area);
    }

    void HighlightButton(float activeArea)
    {
        SetFrameColor(frame1, activeArea == areaLarge);
        SetFrameColor(frame2, activeArea == areaMedium);
        SetFrameColor(frame3, activeArea == areaSmall);
    }

    void SetFrameColor(Image frame, bool isActive)
    {
        if (frame != null)
        {
            frame.color = isActive ? highlightColor : originalFrameColor;
        }
    }
}