using UnityEngine;
using TMPro; 

public class HUDManager : MonoBehaviour
{
    [Header("Star HUD (Top Right)")]
    public TextMeshProUGUI starCountText; 

    [Header("Time HUD (Top Left)")]
    public TextMeshProUGUI currentTimeText; 
    public TextMeshProUGUI targetTimeText; 

    [Header("References")]
    public Terminal levelTerminal;

    private void Start()
    {
        
        if (CollectibleManager.IsInitialized)
        {
            CollectibleManager.Instance.OnCollected.AddListener(UpdateStarUI);
            UpdateStarUI(null, null); 
        }

        
        if (levelTerminal != null)
        {
            targetTimeText.text = $"Target Time: {FormatTime(levelTerminal.targetTime)}";
        }
    }

    private void Update()
    {
        
        if (levelTerminal != null && !levelTerminal.IsFinished)
        {
            currentTimeText.text = $"Current Time: {FormatTime(levelTerminal.Timer)}";
        }
    }


    private void UpdateStarUI(Collectible c, GameObject g)
    {
        if (CollectibleManager.IsInitialized)
        {
            starCountText.text = $"{CollectibleManager.Instance.CollectedCount} / {CollectibleManager.Instance.TotalCount}";
        }
    }


    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}