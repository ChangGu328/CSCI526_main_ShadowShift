using UnityEngine;
using UnityEngine.SceneManagement;

public class Hazard : MonoBehaviour
{
    private const string DefaultDeathHintObjectName = "Text (Died)";

    public GameObject hintUI; // "Press R to Restart" UI

    private bool playerDead = false;

    private void Awake()
    {
        hintUI = ResolveHintUI();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the object entering the trigger is the player's body
        if (playerDead) return;

        if (collision.gameObject.layer == LayerMask.NameToLayer("Player_Body"))
        {
            playerDead = true;
            hintUI = ResolveHintUI();

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

    private GameObject ResolveHintUI()
    {
        if (hintUI != null)
        {
            return hintUI;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            GameObject match = FindChildByName(rootObjects[i].transform, DefaultDeathHintObjectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static GameObject FindChildByName(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject match = FindChildByName(root.GetChild(i), targetName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
