using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaterialLengthDisplay : MonoBehaviour
{
    public GameManager gameManager;
    public TMP_Text displayText;
    public Vector3 position = new Vector3(20, -20, 0); // Top-Left offset with Z support

    void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        
        if (displayText == null)
        {
            SetupText();
        }
    }

    void SetupText()
    {
        GameObject canvasObj = GameObject.Find("GameCanvas");
        if (canvasObj == null)
        {
            Debug.Log("MaterialLengthDisplay: Creating new Canvas 'GameCanvas'...");
            canvasObj = new GameObject("GameCanvas");
            Canvas c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; 
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Check if there's already a TMP object we can use
        GameObject existing = GameObject.Find("MaterialInfoText");
        if (existing != null)
        {
            displayText = existing.GetComponent<TMP_Text>();
        }

        if (displayText == null)
        {
            GameObject textObj = new GameObject("MaterialInfoText");
            textObj.transform.SetParent(canvasObj.transform, false);
            
            // Default to UI version if creating from scratch
            displayText = textObj.AddComponent<TextMeshProUGUI>();
            displayText.fontSize = 24;
            displayText.fontWeight = FontWeight.Bold;
            displayText.color = Color.red; 
            displayText.alignment = TextAlignmentOptions.TopLeft;

            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            
            rect.anchoredPosition3D = position;
            rect.sizeDelta = new Vector2(400, 100);
        }
        
        Debug.Log($"MaterialLengthDisplay: Setup TMP Text at {position}");
    }

    void Update()
    {
        if (gameManager == null || displayText == null) return;

        // Force position in real-time for easier adjustment
        RectTransform rect = displayText.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition3D = position;
        }

        float used = gameManager.GetTotalBeamLength();
        float max = gameManager.GetMaxMaterialLength();
        
        // Format: "Used Material: 12.5m"
        string txt = $"Used Material: {used:F1}m";
        
        if (used > max)
        {
            displayText.text = txt + " <color=red>(LIMIT EXCEEDED)</color>";
            displayText.color = Color.red;
        }
        else
        {
            displayText.text = txt;
            displayText.color = new Color(0.8f, 0.1f, 0.1f);
        }
    }
}
