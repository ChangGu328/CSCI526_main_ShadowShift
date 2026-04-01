using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Terminal : MonoBehaviour
{
    public GameObject gameOverUI; // Reference to the Game Over UI panel
    public bool showCollectibleSummary;
    public bool collectiblesAreOptional = true;

    private bool isGameOver; // if The Game is over;
    private Text gameOverLabel;

    private void Awake()
    {
        ResolveGameOverLabel();
    }
    public bool IsGameOver => isGameOver;

    private void Update()
    {
        if (!isGameOver) return;

        // todo: All stars have been collected.
        if (CollectibleManager.IsInitialized && CollectibleManager.Instance.IsAllCollected())
            Debug.Log("Game over with All Collectible Collected");
        
        // press L to enter level choice menu
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f; // recover time
            SceneManager.LoadScene("LevelSelect");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Trigger Game Over when the player's body enters the terminal
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player_Body"))
        {
            UpdateVictoryText();
            Time.timeScale = 0; // Pause the game
            gameOverUI.SetActive(true); // Show the Game Over UI
            isGameOver = true; // Set the Game Over
        }
    }

    public string BuildVictoryMessage(int collectedCount, int totalCount)
    {
        return TerminalVictoryMessageFormatter.Build(
            showCollectibleSummary,
            collectiblesAreOptional,
            collectedCount,
            totalCount);
    }

    private void UpdateVictoryText()
    {
        Text label = ResolveGameOverLabel();
        if (label == null) return;

        int collectedCount = 0;
        int totalCount = 0;
        if (CollectibleManager.IsInitialized)
        {
            collectedCount = CollectibleManager.Instance.CollectedCount;
            totalCount = CollectibleManager.Instance.TotalCount;
        }

        label.text = BuildVictoryMessage(collectedCount, totalCount);
    }

    private Text ResolveGameOverLabel()
    {
        if (gameOverLabel == null && gameOverUI != null)
        {
            gameOverLabel = gameOverUI.GetComponent<Text>();
        }

        return gameOverLabel;
    }
}
