using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("If left empty, it will use this GameObject")]
    public GameObject bubbleObject; 
    
    [Tooltip("If left empty, it will search children for a TMP component")]
    public TMP_Text dialogueText;

    [System.Serializable]
    public class DialogueLine
    {
        [TextArea(3, 10)]
        public string text;
        [Tooltip("Optional: The visual object (Photo/Anim) to show during this line")]
        public GameObject visualToShow;
    }

    [Header("Dialogue Content")]
    public List<DialogueLine> lines = new List<DialogueLine>();

    [Header("Settings")]
    public float typingSpeed = 0.03f;

    private int currentLineIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    void Start()
    {
        // 1. Auto-assign references if they are missing
        if (bubbleObject == null) bubbleObject = this.gameObject;
        
        if (dialogueText == null) 
            dialogueText = GetComponentInChildren<TMP_Text>();

        if (dialogueText == null)
        {
            Debug.LogError("DialogueManager: No TMP_Text found on this object or its children!");
            return;
        }

        // 1.5 Setup "Centering and stuff" programmatically
        dialogueText.alignment = TextAlignmentOptions.Center;
        dialogueText.enableWordWrapping = true;
        dialogueText.overflowMode = TextOverflowModes.Overflow;

        // 2. Start the sequence if we have lines
        if (lines.Count > 0)
        {
            bubbleObject.SetActive(true);
            StartLine(0);
        }
        else
        {
            bubbleObject.SetActive(false);
        }
    }

    void Update()
    {
        // 3. Advance dialogue on click or key press
        if (Input.GetMouseButtonDown(0) || Input.anyKeyDown)
        {
            if (isTyping)
            {
                FinishLineInstantly();
            }
            else
            {
                NextLine();
            }
        }
    }

    void StartLine(int index)
    {
        currentLineIndex = index;
        dialogueText.text = ""; 
        
        // Show Visual for this line
        if (lines[index].visualToShow != null)
        {
            lines[index].visualToShow.SetActive(true);
        }

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(lines[index].text));
    }

    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = "";
        
        foreach (char c in line.ToCharArray())
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
        
        isTyping = false;
    }

    void FinishLineInstantly()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        dialogueText.text = lines[currentLineIndex].text;
        isTyping = false;
    }

    void NextLine()
    {
        // Hide Visual from the PREVIOUS line before moving on
        if (lines[currentLineIndex].visualToShow != null)
        {
            lines[currentLineIndex].visualToShow.SetActive(false);
        }

        currentLineIndex++;
        if (currentLineIndex < lines.Count)
        {
            StartLine(currentLineIndex);
        }
        else
        {
            EndDialogue();
        }
    }

    void EndDialogue()
    {
        bubbleObject.SetActive(false);
    }

    // Public method if you want to trigger dialogue from other scripts
    public void ResetAndStart()
    {
        // Ensure any active visual is hidden before restarting
        if (currentLineIndex < lines.Count && lines[currentLineIndex].visualToShow != null)
        {
            lines[currentLineIndex].visualToShow.SetActive(false);
        }

        currentLineIndex = 0;
        if (lines.Count > 0)
        {
            bubbleObject.SetActive(true);
            StartLine(0);
        }
    }
}