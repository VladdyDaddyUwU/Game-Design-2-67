using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TheoryButtonConnector : MonoBehaviour
{
    void Start()
    {
        Button btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui != null)
        {
            ui.OpenTheoryMenu();
        }
        else
        {
            Debug.LogWarning("TheoryButtonConnector: Could not find UIManager in the scene.");
        }
    }
}
