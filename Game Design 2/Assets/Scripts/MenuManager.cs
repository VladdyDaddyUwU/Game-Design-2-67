using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void LoadLevel(int levelNumber)
    {
        // Store the selected level number in our static class
        LevelData.SelectedLevel = levelNumber;

        // Load the main game scene
        SceneManager.LoadScene("SampleScene");
    }
}
