using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
    private bool isEnding = false;
    private Coroutine typingCoroutine;
    private Vector3 originalScale;

    void Start()
    {
        if (bubbleObject == null) bubbleObject = this.gameObject;
        originalScale = bubbleObject.transform.localScale;
        
        if (dialogueText == null) 
            dialogueText = GetComponentInChildren<TMP_Text>();

        if (dialogueText == null)
        {
            Debug.LogError("DialogueManager: No TMP_Text found on this object or its children!");
            return;
        }

        dialogueText.alignment = TextAlignmentOptions.Center;
        dialogueText.enableWordWrapping = true;
        dialogueText.overflowMode = TextOverflowModes.Overflow;

        // Hide bubble initially
        SetBubbleVisible(false);

        if (lines.Count > 0)
        {
            StartCoroutine(DelayedStart(3.0f));
        }
    }

    IEnumerator DelayedStart(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetBubbleVisible(true);
        StartLine(0);
    }

    void SetBubbleVisible(bool visible)
    {
        if (bubbleObject == this.gameObject)
        {
            // If script is on the object, hide via scale to keep coroutines running
            bubbleObject.transform.localScale = visible ? originalScale : Vector3.zero;
        }
        else
        {
            // If separate, we can safely deactivate
            bubbleObject.SetActive(visible);
        }
    }

    void Update()
    {
        if (isEnding) return; 

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
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
        isEnding = true;
        // Immediate Transition
        LevelData.SelectedLevel = 0;
        SceneManager.LoadScene("SampleScene");
    }

    public void ResetAndStart()
    {
        isEnding = false;
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