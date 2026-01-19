using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    private AudioSource audioSource; // For SFX
    private AudioSource ambianceSource; // For Background Music
    private AudioClip buttonClickSound;

    [Range(0f, 1f)] public float ambianceVolume = 0.5f;

    void Start()
    {
        // 1. Setup SFX Source (for buttons)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        buttonClickSound = Resources.Load<AudioClip>("button_click");

        // 2. Setup Ambiance (City Sounds)
        // Creating a child object to separate the loops
        GameObject ambiancePlayer = new GameObject("AmbiancePlayer");
        ambiancePlayer.transform.SetParent(this.transform);
        
        ambianceSource = ambiancePlayer.AddComponent<AudioSource>();
        ambianceSource.clip = Resources.Load<AudioClip>("city-sounds-296780");
        ambianceSource.volume = ambianceVolume;
        ambianceSource.loop = true;
        ambianceSource.playOnAwake = true;
        
        ambianceSource.Play();
    }

    // Called by the "PLAY" button
    public void PlayGame()
    {
        PlayClick();
        // Set to the first level (index 0)
        LevelData.SelectedLevel = 0; 
        SceneManager.LoadScene("SampleScene");
    }

    // Called by the "QUIT" button
    public void QuitGame()
    {
        PlayClick();
        Debug.Log("Quit Game Request");
        Application.Quit();
    }

    // Existing function (useful if you make a Level Select screen later)
    public void LoadLevel(int levelNumber)
    {
        PlayClick();
        LevelData.SelectedLevel = levelNumber;
        SceneManager.LoadScene("SampleScene");
    }

    private void PlayClick()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
    }
}