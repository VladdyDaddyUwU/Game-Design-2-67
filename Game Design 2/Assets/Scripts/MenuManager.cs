using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    // Called by the "PLAY" button
    public void PlayGame()
    {
        // Set to the first level (index 0)
        LevelData.SelectedLevel = 0; 
        SceneManager.LoadScene("SampleScene");
    }

    // Called by the "QUIT" button
    public void QuitGame()
    {
        Debug.Log("Quit Game Request");
        Application.Quit();
    }

    // Existing function (useful if you make a Level Select screen later)
    public void LoadLevel(int levelNumber)
    {
        LevelData.SelectedLevel = levelNumber;
        SceneManager.LoadScene("SampleScene");
    }
}