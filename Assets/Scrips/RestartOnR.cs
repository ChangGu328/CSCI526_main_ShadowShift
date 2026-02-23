using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Reloads the active scene when the player presses the R key
public class RestartOnR : MonoBehaviour
{
    [Tooltip("Ignore restart input while a UI element (e.g. input field) is selected.")]
    public bool ignoreWhenUISelected = true;

    [Tooltip("If true, require the player to hold R for this many seconds before restarting. Set to 0 for instant restart.")]
    public float holdSeconds = 0f;

    // internal timer for hold behavior
    private float holdTimer = 0f;

    void Update()
    {
        // Validate that new Input System keyboard is available
        var kb = Keyboard.current;
        if (kb == null) return; // no keyboard (editor/remote?), ignore

        // Optionally ignore while UI element is selected (to prevent restarting while typing)
        if (ignoreWhenUISelected && EventSystem.current != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null)
            {
                // If some UI element has focus, do not restart
                ResetHold();
                return;
            }
        }

        // Check key press
        if (holdSeconds <= 0f)
        {
            // Instant restart on single key press
            if (kb.rKey.wasPressedThisFrame)
            {
                PerformRestart();
            }
        }
        else
        {
            // Hold-to-restart behavior: require holding R for holdSeconds
            if (kb.rKey.isPressed)
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= holdSeconds)
                {
                    PerformRestart();
                    ResetHold();
                }
            }
            else
            {
                ResetHold();
            }
        }
    }

    private void ResetHold()
    {
        holdTimer = 0f;
    }
    
    // Performs the actual restart: reset timeScale and reload the active scene by name.
    private void PerformRestart()
    {
        // Ensure time scale is restored (in case the game was paused)
        Time.timeScale = 1f;

        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
        CollectibleManager.Instance.ResetAll();
    }
}
