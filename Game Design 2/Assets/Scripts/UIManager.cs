using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    private GameObject canvasObj;
    private GameObject panelObj;
    private Text levelCompleteText;

    void Start()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        // 1. Create or Find Canvas
        canvasObj = GameObject.Find("GameCanvas");
        Canvas canvas;
        if (canvasObj == null)
        {
            canvasObj = new GameObject("GameCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas = canvasObj.GetComponent<Canvas>();
        }

        // 2. Create "Level Completed" Panel
        Transform existingPanel = canvasObj.transform.Find("LevelCompletePanel");
        if (existingPanel != null)
        {
            panelObj = existingPanel.gameObject;
            levelCompleteText = panelObj.GetComponentInChildren<Text>();
        }
        else
        {
            panelObj = new GameObject("LevelCompletePanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            
            // Background Image
            Image bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f); // Dark semi-transparent
            
            // RectTransform positioning (Center, fairly large)
            RectTransform rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600, 200);

            // 3. Create Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(panelObj.transform, false);
            
            levelCompleteText = textObj.AddComponent<Text>();
            levelCompleteText.text = "LEVEL COMPLETED!";
            levelCompleteText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Standard Unity font
            levelCompleteText.fontSize = 50;
            levelCompleteText.fontStyle = FontStyle.Bold;
            levelCompleteText.alignment = TextAnchor.MiddleCenter;
            levelCompleteText.color = new Color(1f, 0.84f, 0f); // Gold Color
            
            // Add Shadow for style
            Shadow shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(3, -3);

            // Text Rect
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one; // Stretch to fill panel
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        // Start hidden
        if (panelObj != null) panelObj.SetActive(false);
    }

    public void ShowLevelComplete()
    {
        if (panelObj != null)
        {
            panelObj.SetActive(true);
            // Optional: Simple pop-in animation effect
            panelObj.transform.localScale = Vector3.zero;
            StartCoroutine(AnimatePopIn());
        }
    }

    public void HideLevelComplete()
    {
        if (panelObj != null) panelObj.SetActive(false);
    }

    private IEnumerator AnimatePopIn()
    {
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Elastic ease-out effect
            float scale = Mathf.Sin(-13f * (t + 1f) * Mathf.PI * 0.5f) * Mathf.Pow(2f, -10f * t) + 1f;
            panelObj.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        panelObj.transform.localScale = Vector3.one;
    }
}