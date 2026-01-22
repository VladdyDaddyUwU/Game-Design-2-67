using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class LevelDialogueManager : MonoBehaviour
{
    private GameManager gameManager;
    private GameObject dialogueCanvas;
    private GameObject bubbleObj;
    private Text bubbleText;
    private List<string> currentLines;
    private int currentLineIndex;
    private bool isTyping = false;
    private Coroutine typingCoroutine;
    
    // Config
    private float typingSpeed = 0.03f;
    [Tooltip("Assign the bubble sprite here (e.g. from 'Assets/2D Atlas.../Sprites'). If null, it attempts to load '+Speechbubble_1' from Resources.")]
    public Sprite bubbleSprite; 
    
    [Header("Interaction Settings")]
    [Tooltip("Partial names of buttons to BLOCK during dialogue. All other buttons (Settings, Pause, etc.) will remain active.")]
    public List<string> buttonsToBlock = new List<string>() { 
        "Run", "Simulate", "Play", 
        "Reset", "Restart", "Clear", 
        "Undo", "Redo",
        "Help", "Guide", 
        "Compression", "Tension", 
        "Material", "Replay",
        "Exit", "Retry"
    };
    
    private List<Button> temporarilyDisabledButtons = new List<Button>();
    
    // Layout Variables
    private VerticalLayoutGroup bubbleLayout;
    private int basePadLeft = 50;
    private int basePadRight = 50;
    private int basePadTop = 40;
    private int basePadBottom = 50;
    private Vector3 initialScreenPos;
    
    void Awake()
    {
        gameManager = FindObjectOfType<GameManager>();
        
        // Fallback: If not assigned in Inspector, try loading from Resources
        if (bubbleSprite == null)
        {
            // 1. Try loading directly as Sprite (requires Texture Type: Sprite)
            bubbleSprite = Resources.Load<Sprite>("+Speechbubble_1"); 
            
            // 2. If that failed, try loading as Texture2D and creating the sprite programmatically
            // (This handles cases where it was imported as "Default" texture)
            if (bubbleSprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("+Speechbubble_1");
                if (tex != null)
                {
                    // Create a full-rect sprite with center pivot
                    bubbleSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            if (bubbleSprite == null)
            {
                 Debug.LogWarning("LevelDialogueManager: Bubble Sprite not assigned in Inspector and not found in Resources/+Speechbubble_1.");
            }
        }
    }

    public void ForceStopDialogue()
    {
        if (IsDialogueActive())
        {
            EndDialogue();
        }
    }

    public bool IsDialogueActive()
    {
        return (dialogueCanvas != null && dialogueCanvas.activeSelf);
    }

    public void StartLevelDialogue(List<string> lines, Vector3 targetWorldPos)
    {
        Debug.Log($"LevelDialogueManager: Starting dialogue with {lines.Count} lines.");
        if (lines == null || lines.Count == 0) return;
        
        // Restore first in case of restart/replay to ensure clean state
        RestoreGameplayButtons();
        
        currentLines = lines;
        currentLineIndex = 0;
        
        if (dialogueCanvas == null) CreateDialogueUI();
        
        // Ensure it is active
        dialogueCanvas.SetActive(true);
        PositionBubble(targetWorldPos);
        
        // Block Interaction
        if (gameManager != null) gameManager.currentMode = GameMode.Dialogue;
        DisableGameplayButtons();
        
        ShowLine(currentLines[0]);
    }

    private void DisableGameplayButtons()
    {
        temporarilyDisabledButtons.Clear();
        // Find all ACTIVE buttons in the scene
        var allButtons = FindObjectsOfType<Button>();
        
        foreach (var btn in allButtons)
        {
            // Check if this button should be blocked
            bool shouldBlock = false;
            foreach(string blockName in buttonsToBlock) 
            {
                if (btn.name.IndexOf(blockName, System.StringComparison.OrdinalIgnoreCase) >= 0) 
                {
                    shouldBlock = true;
                    break;
                }
            }
            
            // Do NOT block if it's part of the dialogue UI (like the click area if we had one, though we use global input now)
            // or if it's clearly a system button we missed (sanity check)
            
            if (shouldBlock && btn.enabled)
            {
                btn.enabled = false;
                temporarilyDisabledButtons.Add(btn);
            }
        }
    }

    private void RestoreGameplayButtons()
    {
        foreach (var btn in temporarilyDisabledButtons)
        {
            if (btn != null)
            {
                btn.enabled = true;
            }
        }
        temporarilyDisabledButtons.Clear();
    }

    private void CreateDialogueUI()
    {
        // 1. World Space Canvas for the bubble to sit near the player
        // OR Screen Space Overlay that tracks the player. 
        // Let's go with Screen Space Overlay + WorldToScreenPoint for crisp text.
        
        dialogueCanvas = new GameObject("LevelDialogueCanvas");
        Canvas c = dialogueCanvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 90; // High, but below Pause Menu (usually 100+)
        dialogueCanvas.AddComponent<CanvasScaler>();
        dialogueCanvas.AddComponent<GraphicRaycaster>();
        
        // 2. Bubble Container
        bubbleObj = new GameObject("Bubble");
        bubbleObj.transform.SetParent(dialogueCanvas.transform, false);
        
        Image img = bubbleObj.AddComponent<Image>();
        img.raycastTarget = false; // Allow clicks to pass through to the background
        if (bubbleSprite != null) 
        {
            img.sprite = bubbleSprite;
            // Use Sliced to allow resizing while keeping corners/tail fixed.
            // IMPORTANT: You MUST set up "Borders" in the Sprite Editor in Unity for this to work!
            img.type = Image.Type.Sliced; 
            img.pixelsPerUnitMultiplier = 1.0f;
        }
        else
        {
            img.color = new Color(0, 0, 0, 0.5f);
            Debug.LogError("LevelDialogueManager: Bubble Sprite is MISSING! Showing default rectangle.");
        }
        
        RectTransform rect = bubbleObj.GetComponent<RectTransform>();
        // Size will be controlled by ContentSizeFitter, but we set a pivot
        rect.pivot = new Vector2(1f, 0f); // Pivot on the side where the tail is
        
        // Flip the bubble horizontally so the tail (on the right) now points left
        bubbleObj.transform.localScale = new Vector3(-1, 1, 1);

        // Add Layout components for dynamic sizing
        VerticalLayoutGroup layout = bubbleObj.AddComponent<VerticalLayoutGroup>();
        // Add significant padding so text doesn't overlap the bubble edges/tail
        layout.padding = new RectOffset(50, 50, 40, 50); 
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        
        ContentSizeFitter fitter = bubbleObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // 3. Text
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(bubbleObj.transform, false);
        
        // Flip the text back so it's not mirrored
        txtObj.transform.localScale = new Vector3(-1, 1, 1);
        
        bubbleText = txtObj.AddComponent<Text>();
        bubbleText.raycastTarget = false; 
        bubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bubbleText.fontSize = 60; // Increased size as requested
        bubbleText.color = Color.black;
        bubbleText.alignment = TextAnchor.MiddleLeft;
        // Overflow settings to allow Fitter to measure text
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap; 
        bubbleText.verticalOverflow = VerticalWrapMode.Truncate; 
        
        // Add LayoutElement to define the wrapping point.
        LayoutElement layoutElem = txtObj.AddComponent<LayoutElement>();
        // Start with no preference, let the text dictate size until clamped
        layoutElem.preferredWidth = -1;  
    }

    private void PositionBubble(Vector3 targetWorldPos)
    {
        // Convert World to Screen
        if (Camera.main != null && bubbleObj != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(targetWorldPos);
            // Add offset to place it "To the right"
            screenPos.x += 50; 
            screenPos.y += 50;
            bubbleObj.transform.position = screenPos;
            
            // Store this as the anchor point for dynamic shifting
            initialScreenPos = screenPos;
        }
    }

    public void OnDialogueClick()
    {
        if (isTyping)
        {
            FinishTyping();
        }
        else
        {
            AdvanceDialogue();
        }
    }
    
    // Allow 'Space' to advance too
    void Update()
    {
        if (gameManager == null) return;
        
        // Handle Visibility based on Pause Menu
        bool isPaused = (gameManager.uiManager != null && gameManager.uiManager.IsPauseMenuOpen());
        if (dialogueCanvas != null)
        {
            // Hide dialogue if paused, Show if active and in Dialogue mode
            bool shouldBeVisible = !isPaused && (gameManager.currentMode == GameMode.Dialogue);
            if (dialogueCanvas.activeSelf != shouldBeVisible)
            {
                dialogueCanvas.SetActive(shouldBeVisible);
            }
        }

        // Handle Input
        if (gameManager.currentMode == GameMode.Dialogue && !isPaused)
        {
            if (Input.anyKeyDown)
            {
                // Check if clicking on UI (e.g. Pause Button)
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                {
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    {
                        return; // Clicked a button, don't advance dialogue
                    }
                }
                
                // Don't advance on the same frame as pausing (if X is pressed)
                if (Input.GetKeyDown(KeyCode.X)) return;

                OnDialogueClick();
            }
        }
    }

    private void ShowLine(string line)
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(line));
    }

    private void UpdateBubbleSize()
    {
        if (bubbleText == null || bubbleLayout == null) return;
        
        float w = bubbleText.preferredWidth;
        float h = bubbleText.preferredHeight;
        // Reduced horizontal limit by 20% (1125 * 0.8 = 900)
        float clampedW = Mathf.Min(w, 900f); 
        
        // 1. Enforce Wrapping Limit on Text
        LayoutElement le = bubbleText.GetComponent<LayoutElement>();
        if (le != null)
        {
             le.preferredWidth = clampedW;
        }
        
        // 2. Add dynamic padding to Parent Bubble (Horizontal)
        // Bubble grows linearly 1.2x relative to text
        int extraPadX = Mathf.RoundToInt(clampedW * 0.2f);
        int halfExtraX = extraPadX / 2;
        
        bubbleLayout.padding.left = basePadLeft + halfExtraX;
        bubbleLayout.padding.right = basePadRight + halfExtraX;
        
        // 3. Add dynamic padding to Parent Bubble (Vertical)
        // Vertical scaling increased by another 20% (0.5 + 0.2 = 0.7)
        int extraPadY = Mathf.RoundToInt(h * 0.7f);
        
        // Distribute unevenly to keep text closer to the top as it scales.
        // 30% of growth goes to Top padding, 70% goes to Bottom padding.
        int padTopAdd = Mathf.RoundToInt(extraPadY * 0.3f);
        int padBottomAdd = extraPadY - padTopAdd;
        
        bubbleLayout.padding.top = basePadTop + padTopAdd;
        bubbleLayout.padding.bottom = basePadBottom + padBottomAdd;
        
        // 4. Adjust Position (Shift Left as it grows)
        // Move left by 40% of the text width to counteract rightward expansion
        float xOffset = clampedW * 0.4f;
        if (bubbleObj != null)
        {
            bubbleObj.transform.position = initialScreenPos - new Vector3(xOffset, 0, 0);
        }
        
        // Force layout rebuild
        LayoutRebuilder.MarkLayoutForRebuild(bubbleLayout.transform as RectTransform);
    }

    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        bubbleText.text = "";
        
        // Cache Layout Group reference if missing
        if (bubbleLayout == null && bubbleObj != null) 
            bubbleLayout = bubbleObj.GetComponent<VerticalLayoutGroup>();

        foreach(char c in line)
        {
            bubbleText.text += c;
            UpdateBubbleSize();
            yield return new WaitForSeconds(typingSpeed);
        }
        isTyping = false;
    }

    private void FinishTyping()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        bubbleText.text = currentLines[currentLineIndex];
        
        if (bubbleLayout == null && bubbleObj != null) 
            bubbleLayout = bubbleObj.GetComponent<VerticalLayoutGroup>();
            
        UpdateBubbleSize();
        
        isTyping = false;
    }

    private void AdvanceDialogue()
    {
        currentLineIndex++;
        if (currentLineIndex < currentLines.Count)
        {
            ShowLine(currentLines[currentLineIndex]);
        }
        else
        {
            EndDialogue();
        }
    }

    private void EndDialogue()
    {
        RestoreGameplayButtons();
        if (dialogueCanvas != null) dialogueCanvas.SetActive(false);
        if (gameManager != null) gameManager.SwitchToBuildMode(); // Unlock
    }
}
