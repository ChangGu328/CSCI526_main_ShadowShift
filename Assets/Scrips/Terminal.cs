using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Terminal : MonoBehaviour
{
    public GameObject gameOverUI; // Reference to the Game Over UI panel

    private bool isGameOver; // if The Game is over;

    private void Update()
    {
        if (!isGameOver) return;

        // todo: 所有星星都被收集
        if (CollectibleManager.IsInitialized && CollectibleManager.Instance.IsAllCollected())
            Debug.Log("Game over with All Collectible Collected");
        
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Trigger Game Over when the player's body enters the terminal
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player_Body"))
        {
            Time.timeScale = 0; // Pause the game
            gameOverUI.SetActive(true); // Show the Game Over UI
            isGameOver = true; // Set the Game Over
        }
    }
}