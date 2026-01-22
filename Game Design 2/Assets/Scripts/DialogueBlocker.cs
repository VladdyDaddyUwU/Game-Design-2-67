using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DialogueBlocker : MonoBehaviour
{
    private Button button;
    private GameManager gameManager;

    void Start()
    {
        button = GetComponent<Button>();
        gameManager = FindObjectOfType<GameManager>();
    }

    void Update()
    {
        if (gameManager != null && button != null)
        {
            // If in Dialogue mode, disable the button
            // UNLESS it is the Settings button (we'll check via name or tag if needed, 
            // but for now let's assume this script is attached to Play/Simulate buttons)
            bool isDialogue = (gameManager.currentMode == GameMode.Dialogue);
            
            // If button is interactable and we are in dialogue, disable it
            if (isDialogue && button.interactable)
            {
                button.interactable = false;
            }
            // If button is disabled and we are NOT in dialogue, enable it
            else if (!isDialogue && !button.interactable)
            {
                button.interactable = true;
            }
        }
    }
}
