using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    private GameObject canvasObj;
    private GameObject panelObj;
    private GameObject pauseMenuObj; 
    private GameObject levelSelectMenuObj; // New Level Select Panel
    private LevelManager levelManager; // Reference to access levels
    private Text levelCompleteText; // Missing field declaration
    private AudioSource audioSource;
    private AudioClip buttonClickSound;

    void Start()
    {
        levelManager = FindObjectOfType<LevelManager>();
        SetupUI();
        SetupPauseMenu(); 
        SetupLevelSelectMenu(); // Build the secondary menu

        // Add AudioSource component
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Load sound
        buttonClickSound = Resources.Load<AudioClip>("button_click");
        if (buttonClickSound == null) {
            Debug.LogError("Failed to load button_click sound from Resources folder. Make sure the file is there and named correctly.");
        }
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

    private void SetupPauseMenu()
    {
        // Check if exists
        Transform existingPause = canvasObj.transform.Find("PauseMenu");
        if (existingPause != null)
        {
            pauseMenuObj = existingPause.gameObject;
            pauseMenuObj.SetActive(false);
            return;
        }

        Font workingFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (workingFont == null) workingFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        // Create Panel
        pauseMenuObj = new GameObject("PauseMenu");
        pauseMenuObj.transform.SetParent(canvasObj.transform, false);

        Image bg = pauseMenuObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f); 
        RectTransform rect = pauseMenuObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(pauseMenuObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "PAUSED";
        titleText.font = workingFont;
        titleText.fontSize = 80;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.85f);
        titleRect.anchorMax = new Vector2(0.5f, 0.85f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(600, 150);
        titleRect.anchoredPosition = Vector2.zero;

        // --- CONTROLS HEADER ---
        GameObject controlsHeader = new GameObject("ControlsHeader");
        controlsHeader.transform.SetParent(pauseMenuObj.transform, false);
        Text controlsText = controlsHeader.AddComponent<Text>();
        controlsText.text = "<b>Controls</b>";
        controlsText.font = workingFont;
        controlsText.fontSize = 30;
        controlsText.alignment = TextAnchor.MiddleCenter;
        controlsText.color = Color.white;
        controlsText.supportRichText = true;

        RectTransform headerRect = controlsHeader.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 0.7f); // Back to original
        headerRect.anchorMax = new Vector2(0.5f, 0.7f);
        headerRect.sizeDelta = new Vector2(400, 50);
        headerRect.anchoredPosition = Vector2.zero;

        // --- LEFT INSTRUCTION ---
        GameObject leftInst = new GameObject("InstructionLeft");
        leftInst.transform.SetParent(pauseMenuObj.transform, false);
        Text leftText = leftInst.AddComponent<Text>();
        leftText.text = "Left Drag from joints: Build";
        leftText.font = workingFont;
        leftText.fontSize = 22;
        leftText.alignment = TextAnchor.MiddleCenter; 
        leftText.color = new Color(0.9f, 0.9f, 0.9f);
        leftText.horizontalOverflow = HorizontalWrapMode.Overflow;
        leftText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform leftRect = leftInst.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0.1f, 0.62f); // Shrunk height to remove gap
        leftRect.anchorMax = new Vector2(0.45f, 0.66f); 
        leftRect.offsetMin = Vector2.zero; leftRect.offsetMax = Vector2.zero;

        // --- RIGHT INSTRUCTION ---
        GameObject rightInst = new GameObject("InstructionRight");
        rightInst.transform.SetParent(pauseMenuObj.transform, false);
        Text rightText = rightInst.AddComponent<Text>();
        rightText.text = "Hold O: See Tensions";
        rightText.font = workingFont;
        rightText.fontSize = 22;
        rightText.alignment = TextAnchor.MiddleCenter;
        rightText.color = new Color(0.9f, 0.9f, 0.9f);
        rightText.horizontalOverflow = HorizontalWrapMode.Overflow;
        rightText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rightRect = rightInst.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.55f, 0.62f); // Shrunk height to remove gap
        rightRect.anchorMax = new Vector2(0.9f, 0.66f); 
        rightRect.offsetMin = Vector2.zero; rightRect.offsetMax = Vector2.zero;

        // --- RESUME BUTTON ---
        GameObject resumeBtnObj = new GameObject("BtnResume");
        resumeBtnObj.transform.SetParent(pauseMenuObj.transform, false);
        Image resumeImg = resumeBtnObj.AddComponent<Image>();
        resumeImg.color = new Color(0.2f, 0.8f, 0.2f, 0.3f); 
        Button resumeBtn = resumeBtnObj.AddComponent<Button>();
        resumeBtn.targetGraphic = resumeImg;
        resumeBtn.onClick.AddListener(TogglePauseMenu); 

        RectTransform resumeRect = resumeBtnObj.GetComponent<RectTransform>();
        resumeRect.anchorMin = new Vector2(0.5f, 0.52f); // Sitting close to instructions
        resumeRect.anchorMax = new Vector2(0.5f, 0.52f);
        resumeRect.sizeDelta = new Vector2(300, 60);

        GameObject resumeTxtObj = new GameObject("Text");
        resumeTxtObj.transform.SetParent(resumeBtnObj.transform, false);
        Text resumeTxt = resumeTxtObj.AddComponent<Text>();
        resumeTxt.text = "RESUME";
        resumeTxt.font = workingFont;
        resumeTxt.fontSize = 24;
        resumeTxt.alignment = TextAnchor.MiddleCenter;
        resumeTxt.color = Color.white;
        resumeTxtObj.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        resumeTxtObj.GetComponent<RectTransform>().anchorMax = Vector2.one;
        resumeTxtObj.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        resumeTxtObj.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // --- CHANGE LEVEL BUTTON ---
        GameObject btnObj = new GameObject("BtnChangeLevel");
        btnObj.transform.SetParent(pauseMenuObj.transform, false);
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.1f);
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(ShowLevelSelect); 

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.37f); 
        btnRect.anchorMax = new Vector2(0.5f, 0.37f);
        btnRect.sizeDelta = new Vector2(300, 60);

        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.text = "CHANGE LEVEL";
        btnText.font = workingFont;
        btnText.fontSize = 24;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.yellow;
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero; btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero; btnTextRect.offsetMax = Vector2.zero;

        // --- QUIT GAME BUTTON ---
        GameObject quitBtnObj = new GameObject("BtnQuit");
        quitBtnObj.transform.SetParent(pauseMenuObj.transform, false);
        Image quitImg = quitBtnObj.AddComponent<Image>();
        quitImg.color = new Color(1f, 0.2f, 0.2f, 0.1f); 
        Button quitBtn = quitBtnObj.AddComponent<Button>();
        quitBtn.targetGraphic = quitImg;
        quitBtn.onClick.AddListener(() => {
            Debug.Log("Quit Game pressed.");
            Application.Quit();
        });

        RectTransform quitRect = quitBtnObj.GetComponent<RectTransform>();
        quitRect.anchorMin = new Vector2(0.5f, 0.22f); 
        quitRect.anchorMax = new Vector2(0.5f, 0.22f);
        quitRect.sizeDelta = new Vector2(300, 60);

        GameObject quitTextObj = new GameObject("Text");
        quitTextObj.transform.SetParent(quitBtnObj.transform, false);
        Text quitText = quitTextObj.AddComponent<Text>();
        quitText.text = "QUIT GAME";
        quitText.font = workingFont;
        quitText.fontSize = 24;
        quitText.alignment = TextAnchor.MiddleCenter;
        quitText.color = new Color(1f, 0.4f, 0.4f); // Light Red
        
        RectTransform quitTextRect = quitTextObj.GetComponent<RectTransform>();
        quitTextRect.anchorMin = Vector2.zero; 
        quitTextRect.anchorMax = Vector2.one;
        quitTextRect.offsetMin = Vector2.zero; 
        quitTextRect.offsetMax = Vector2.zero;

        pauseMenuObj.SetActive(false);
    }

    // Public method for external buttons (Settings/Menu button in Hierarchy)
    public void OpenPauseMenu()
    {
        if (pauseMenuObj != null && !pauseMenuObj.activeSelf)
        {
            if (buttonClickSound != null)
            {
                audioSource.PlayOneShot(buttonClickSound);
            }
            TogglePauseMenu();
        }
    }

    private void SetupLevelSelectMenu()
    {
        // Container
        levelSelectMenuObj = new GameObject("LevelSelectMenu");
        levelSelectMenuObj.transform.SetParent(canvasObj.transform, false);
        
        Image bg = levelSelectMenuObj.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.98f); 
        RectTransform rect = levelSelectMenuObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(levelSelectMenuObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "SELECT LEVEL";
        Font workingFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (workingFont == null) workingFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.font = workingFont;
        titleText.fontSize = 50;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleObj.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.9f);
        titleObj.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.9f);
        titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 100);

        // --- SCROLL VIEW SETUP ---
        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(levelSelectMenuObj.transform, false);
        RectTransform scrollRectTransform = scrollObj.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0.1f, 0.15f); // Leave room for Title and Back button
        scrollRectTransform.anchorMax = new Vector2(0.9f, 0.8f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 20f;

        // Viewport (Mask)
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        RectTransform viewRect = viewportObj.AddComponent<RectTransform>();
        viewRect.anchorMin = Vector2.zero; viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = Vector2.zero; viewRect.offsetMax = Vector2.zero;
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;
        Image maskImg = viewportObj.AddComponent<Image>(); // Mask needs an image to work
        maskImg.color = Color.white; 

        // Content (Grid Holder)
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1); 
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 300); // Initial height

        scrollRect.content = contentRect;
        scrollRect.viewport = viewRect;

        // Grid Layout Group
        GridLayoutGroup grid = contentObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100, 100);
        grid.spacing = new Vector2(20, 20);
        grid.padding = new RectOffset(20, 20, 20, 20);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        
        // Content Size Fitter (Auto-expand height)
        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Generate Buttons
        if (levelManager != null && levelManager.levels != null)
        {
            for (int i = 0; i < levelManager.levels.Count; i++)
            {
                int levelIndex = i; // Capture for lambda
                GameObject lvlBtn = new GameObject($"Level_{i+1}");
                lvlBtn.transform.SetParent(contentObj.transform, false);
                
                Image btnImg = lvlBtn.AddComponent<Image>();
                btnImg.color = new Color(1f, 1f, 1f, 0.2f);
                
                Button btn = lvlBtn.AddComponent<Button>();
                btn.onClick.AddListener(() => {
                    SelectLevel(levelIndex);
                });

                GameObject txtObj = new GameObject("Text");
                txtObj.transform.SetParent(lvlBtn.transform, false);
                Text t = txtObj.AddComponent<Text>();
                t.text = (i + 1).ToString();
                t.font = workingFont;
                t.fontSize = 40;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = Color.white;
                txtObj.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                txtObj.GetComponent<RectTransform>().anchorMax = Vector2.one;
            }
        }

        // Back Button (Outside ScrollView)
        GameObject backBtnObj = new GameObject("BtnBack");
        backBtnObj.transform.SetParent(levelSelectMenuObj.transform, false);
        Image backImg = backBtnObj.AddComponent<Image>();
        backImg.color = new Color(1f, 0.5f, 0.5f, 0.2f);
        Button backBtn = backBtnObj.AddComponent<Button>();
        backBtn.onClick.AddListener(HideLevelSelect);
        
        RectTransform backRect = backBtnObj.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.05f);
        backRect.anchorMax = new Vector2(0.5f, 0.05f);
        backRect.sizeDelta = new Vector2(200, 50);
        backRect.pivot = new Vector2(0.5f, 0);

        GameObject backTxtObj = new GameObject("Text");
        backTxtObj.transform.SetParent(backBtnObj.transform, false);
        Text backTxt = backTxtObj.AddComponent<Text>();
        backTxt.text = "BACK";
        backTxt.font = workingFont;
        backTxt.fontSize = 20;
        backTxt.alignment = TextAnchor.MiddleCenter;
        backTxt.color = Color.white;
        backTxtObj.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        backTxtObj.GetComponent<RectTransform>().anchorMax = Vector2.one;


        levelSelectMenuObj.SetActive(false);
    }

    public void ShowLevelSelect()
    {
        if (pauseMenuObj != null) pauseMenuObj.SetActive(false);
        if (levelSelectMenuObj != null) levelSelectMenuObj.SetActive(true);
    }

    public void HideLevelSelect()
    {
        if (levelSelectMenuObj != null) levelSelectMenuObj.SetActive(false);
        if (pauseMenuObj != null) pauseMenuObj.SetActive(true);
    }

    public void SelectLevel(int index)
    {
        if (levelManager != null)
        {
            levelManager.LoadLevel(index);
            TogglePauseMenu(); // Close all menus
        }
    }

    public void TogglePauseMenu()
    {
        // If Level Select is open, close it and strictly close the Pause Menu (Resume Game)
        if (levelSelectMenuObj != null && levelSelectMenuObj.activeSelf)
        {
            levelSelectMenuObj.SetActive(false);
            pauseMenuObj.SetActive(false);
            return;
        }

        if (pauseMenuObj != null)
        {
            bool isActive = !pauseMenuObj.activeSelf;
            pauseMenuObj.SetActive(isActive);
        }
    }

    public bool IsPauseMenuOpen()
    {
        bool isPaused = (pauseMenuObj != null && pauseMenuObj.activeSelf) || (levelSelectMenuObj != null && levelSelectMenuObj.activeSelf);
        // Debug.Log($"UIManager.IsPauseMenuOpen: pauseMenu active = {pauseMenuObj.activeSelf}, levelSelect active = {levelSelectMenuObj.activeSelf}, returning {isPaused}");
        return isPaused;
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