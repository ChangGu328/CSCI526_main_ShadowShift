using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Hazard : MonoBehaviour
{
    public GameObject hintUI; // "Press R to Restart" UI

    private bool playerDead = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the object entering the trigger is the player's body
        if (playerDead) return;

        if (collision.gameObject.layer == LayerMask.NameToLayer("Player_Body"))
        {
            playerDead = true;

            // Show restart hint
            if (hintUI != null)
            {
                hintUI.SetActive(true);
            }

            // Pause the game
            Time.timeScale = 0f;
        }
    }

    private void Update()
    {

    }
}