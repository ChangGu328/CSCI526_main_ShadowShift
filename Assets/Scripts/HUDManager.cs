using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Star HUD (Top Right)")]
    public TextMeshProUGUI starCountText;

    [Header("Time HUD (Top Left)")]
    public TextMeshProUGUI currentTimeText;
    public TextMeshProUGUI targetTimeText;

    [Header("Form HUD")]
    public TextMeshProUGUI entityText;
    public TextMeshProUGUI shadowText;

    [Header("References")]
    public Terminal levelTerminal;

    private CollectibleManager collectibleManager;

    private void Start()
    {
        collectibleManager = CollectibleManager.Instance;
        if (collectibleManager != null)
        {
            collectibleManager.OnCollected.AddListener(UpdateStarUI);
            UpdateStarUI(null, null);
        }

        if (levelTerminal != null)
        {
            if (targetTimeText != null)
            {
                targetTimeText.text = $"Target Time: {FormatTime(levelTerminal.targetTime)}";
            }

            if (currentTimeText != null)
            {
                currentTimeText.text = $"Current Time: {FormatTime(levelTerminal.Timer)}";
            }
        }

        // this will be updated by the PlayerController when the player changes forms.
        UpdateFormUI(PLAYERSTATE.BODY);
    }

    private void Update()
    {
        if (levelTerminal != null && !levelTerminal.IsFinished && currentTimeText != null)
        {
            currentTimeText.text = $"Current Time: {FormatTime(levelTerminal.Timer)}";
        }
    }

    private void UpdateStarUI(Collectible c, GameObject g)
    {
        if (collectibleManager != null && starCountText != null)
        {
            starCountText.text = $"{collectibleManager.CollectedCount} / {collectibleManager.TotalCount}";
        }
    }

    private void OnDestroy()
    {
        if (collectibleManager != null)
        {
            collectibleManager.OnCollected.RemoveListener(UpdateStarUI);
        }
    }

    public void UpdateFormUI(PLAYERSTATE state)
    {
        if (entityText == null || shadowText == null) return;

        if (state == PLAYERSTATE.BODY)
        {
            entityText.color = Color.green;
            shadowText.color = Color.gray;
        }
        else
        {
            entityText.color = Color.gray;
            shadowText.color = Color.green;
        }
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
