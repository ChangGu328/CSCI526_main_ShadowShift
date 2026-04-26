using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TutorialPanel : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI messageText;
    public Button confirmButton;

    private System.Action onConfirm;

    void Awake()
    {
        confirmButton.onClick.AddListener(OnConfirmClicked);
        gameObject.SetActive(false);
    }

    public void Show(string message = null, System.Action callback = null)
    {
        if (!string.IsNullOrEmpty(message))
            messageText.text = message; 
        onConfirm = callback;
        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    void OnConfirmClicked()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f;
        onConfirm?.Invoke();
    }
}