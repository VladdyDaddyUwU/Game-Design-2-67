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
            img.type = Image.Type.Simple; 
            img.preserveAspect = true;
        }
        else
        {
            // If sprite is missing, it will be a white rectangle.
            // Let's set it to a semi-transparent black box so it looks intentional at least.
            img.color = new Color(0, 0, 0, 0.5f);
            Debug.LogError("LevelDialogueManager: Bubble Sprite is MISSING! Showing default rectangle.");
        }
        
        RectTransform rect = bubbleObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 200); // Slightly larger
        rect.pivot = new Vector2(1f, 0f); // Pivot on the side where the tail is
        
        // Flip the bubble horizontally so the tail (on the right) now points left
        // Because pivot is at 1.0 (right edge), flipping it makes the bubble 
        // expand to the right of the anchor point.
        bubbleObj.transform.localScale = new Vector3(-1, 1, 1);
        
        // 3. Text
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(bubbleObj.transform, false);
        
        // Flip the text back so it's not mirrored
        txtObj.transform.localScale = new Vector3(-1, 1, 1);
        
        bubbleText = txtObj.AddComponent<Text>();
        bubbleText.raycastTarget = false; // Allow clicks to pass through to the background
        bubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bubbleText.fontSize = 20;
        bubbleText.color = Color.black;
        bubbleText.alignment = TextAnchor.MiddleLeft;
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
        
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = new Vector2(0.1f, 0.1f);
        txtRect.anchorMax = new Vector2(0.9f, 0.9f);
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
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
        }
    }

    public bool IsDialogueActive()
    {
        return (dialogueCanvas != null && dialogueCanvas.activeSelf);
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
                // IsPointerOverGameObject() checks if the mouse is hovering over a UI element that catches events.
                // We assume if it's a keyboard press, we proceed. If mouse, we check UI.
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

    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        bubbleText.text = "";
        foreach(char c in line)
        {
            bubbleText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
        isTyping = false;
    }

    private void FinishTyping()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        bubbleText.text = currentLines[currentLineIndex];
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
