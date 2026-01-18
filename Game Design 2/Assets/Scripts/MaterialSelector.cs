using UnityEngine;

public class MaterialSelector : MonoBehaviour
{
    public GameManager gameManager;
    public GameObject materialDisplay;

    [Header("Material Buttons")]
    public GameObject material1_7x7; // Material 1
    public GameObject material2_5x5; // Material 2
    public GameObject material3_3x3; // Material 3

    // Standard areas: 7x7=0.0049, 5x5=0.0025, 3x3=0.0009
    private readonly float areaLarge = 0.0049f;
    private readonly float areaMedium = 0.0025f;
    private readonly float areaSmall = 0.0009f;

    void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();

        // Use this object as container if nothing assigned
        if (materialDisplay == null) materialDisplay = this.gameObject;

        // 1. Find Buttons if not manually assigned
        if (material1_7x7 == null) material1_7x7 = FindChild(materialDisplay, "Material 1");
        if (material2_5x5 == null) material2_5x5 = FindChild(materialDisplay, "Material 2");
        if (material3_3x3 == null) material3_3x3 = FindChild(materialDisplay, "Material 3");
        
        // Debug: Report status
        Debug.Log($"[MaterialSelector Setup] Found Material 1: {(material1_7x7 != null ? material1_7x7.name : "NULL")}");
        Debug.Log($"[MaterialSelector Setup] Found Material 2: {(material2_5x5 != null ? material2_5x5.name : "NULL")}");
        Debug.Log($"[MaterialSelector Setup] Found Material 3: {(material3_3x3 != null ? material3_3x3.name : "NULL")}");

        // Optional: Initial Highlight
        HighlightButton(areaMedium);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left Click
        {
            CheckClick();
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
        SetButtonColor(material1_7x7, activeArea == areaLarge);
        SetButtonColor(material2_5x5, activeArea == areaMedium);
        SetButtonColor(material3_3x3, activeArea == areaSmall);
    }

    void SetButtonColor(GameObject obj, bool isActive)
    {
        if (obj == null) return;
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Green tint if active, White (default) if inactive
            sr.color = isActive ? new Color(0.2f, 1f, 0.2f, 1f) : Color.white;
        }
    }
}
