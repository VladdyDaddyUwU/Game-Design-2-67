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

    [Header("Dialogue Content")]
    [TextArea(3, 10)]
    public List<string> lines = new List<string>();

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
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(lines[index]));
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
        dialogueText.text = lines[currentLineIndex];
        isTyping = false;
    }

    void NextLine()
    {
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
        currentLineIndex = 0;
        if (lines.Count > 0)
        {
            bubbleObject.SetActive(true);
            StartLine(0);
        }
    }
}