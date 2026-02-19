using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectController : MonoBehaviour
{
    [Header("Scene names")]
    [Tooltip("Scene name for Level 1")]
    public string level1Scene = "GameScene";

    [Tooltip("Scene name for Level 2")]
    public string level2Scene = "01-Pully";

    [Tooltip("Scene name for Level 3 (not implemented)")]
    public string level3Scene = "Portal";

    [Tooltip("Name of main menu scene to return to")]
    public string mainMenuScene = "MainMenu";

    // Called by Level 1 button
    public void OnLevel1Button()
    {
        if (string.IsNullOrEmpty(level1Scene))
        {
            Debug.LogError("LevelSelectController: level1Scene is empty.");
            return;
        }
        Debug.Log($"Loading Level 1 -> {level1Scene}");
        SceneManager.LoadScene(level1Scene);
    }

    // Called by Level 2 button
    public void OnLevel2Button()
    {
        if (string.IsNullOrEmpty(level2Scene))
        {
            Debug.LogError("LevelSelectController: level2Scene is empty.");
            return;
        }
        Debug.Log($"Loading Level 2 -> {level2Scene}");
        SceneManager.LoadScene(level2Scene);
    }

    // Called by Level 3 button (not implemented: only debug)
    public void OnLevel3Button()
    {
        Debug.LogWarning($"Level 3 ('{level3Scene}') not implemented yet. No action taken.");
        // If you want later to load the scene, replace the two lines above with:
        // SceneManager.LoadScene(level3Scene);
    }

    // Called by Back button
    public void OnBackButton()
    {
        if (string.IsNullOrEmpty(mainMenuScene))
        {
            Debug.LogError("LevelSelectController: mainMenuScene is empty.");
            return;
        }
        Debug.Log($"Returning to Main Menu -> {mainMenuScene}");
        SceneManager.LoadScene(mainMenuScene);
    }
}
