using UnityEngine;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    public List<LevelData> levels;
    public int currentLevelIndex = 0;
    
    public GridController gridController;
    public GameManager gameManager;

    void Start()
    {
        if (gridController == null) gridController = FindObjectOfType<GridController>();
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();

        LoadLevel(currentLevelIndex);
    }

    void Update()
    {
        // Debug controls to switch levels
        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            NextLevel();
        }
        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            PreviousLevel();
        }
    }

    public void LoadLevel(int index)
    {
        if (levels == null || levels.Count == 0) return;
        
        // Wrap index
        if (index >= levels.Count) index = 0;
        if (index < 0) index = levels.Count - 1;
        
        currentLevelIndex = index;
        LevelData data = levels[currentLevelIndex];
        
        Debug.Log($"Loading Level {index + 1}: {data.name}");
        
        // 1. Tell Grid to load specific level config
        gridController.LoadLevel(data);
        
        // 2. Regenerate Grid (This triggers GameManager.InitializeStructure inside GridController)
        gridController.GenerateGrid();
        
        // 3. Reset Game State
        gameManager.currentMode = GameMode.Build;
        gameManager.RestartLevel();
    }

    public void NextLevel()
    {
        LoadLevel(currentLevelIndex + 1);
    }

    public void PreviousLevel()
    {
        LoadLevel(currentLevelIndex - 1);
    }
}
