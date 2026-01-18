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

        float limit7x7 = 20f, limit5x5 = 20f, limit3x3 = 20f;
        
        if (gameManager.gridController != null && gameManager.gridController.currentLevel != null)
        {
            limit7x7 = gameManager.gridController.currentLevel.limit7x7;
            limit5x5 = gameManager.gridController.currentLevel.limit5x5;
            limit3x3 = gameManager.gridController.currentLevel.limit3x3;
        }

        float used7x7 = gameManager.GetBeamLengthByArea(0.0049f);
        float used5x5 = gameManager.GetBeamLengthByArea(0.0025f);
        float used3x3 = gameManager.GetBeamLengthByArea(0.0009f);
        
        // Format string
        // Using rich text for colors per section would be nice, but simple first.
        string txt = $"7x7: {used7x7:F1}/{limit7x7:F0}m\n" +
                     $"5x5: {used5x5:F1}/{limit5x5:F0}m\n" +
                     $"3x3: {used3x3:F1}/{limit3x3:F0}m";

        bool exceeded = (used7x7 > limit7x7) || (used5x5 > limit5x5) || (used3x3 > limit3x3);

        displayText.text = txt;
        
        if (exceeded)
        {
            displayText.color = Color.red;
        }
        else
        {
            displayText.color = new Color(0.8f, 0.1f, 0.1f);
        }
    }
}
