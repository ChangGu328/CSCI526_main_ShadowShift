using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectController : MonoBehaviour
{
    [Header("Scene names")]
    [Tooltip("Scene name for Level 1")]
    string level1Scene = "level1";

    [Tooltip("Scene name for Level 2")]
    string level2Scene = "level2";

    [Tooltip("Scene name for Level 3 (not implemented)")]
    string level3Scene = "level3";

    [Tooltip("Name of main menu scene to return to")]
    string mainMenuScene = "MainMenu";

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
        if (string.IsNullOrEmpty(level3Scene))
        {
            Debug.LogError("LevelSelectController: level3Scene is empty.");
            return;
        }
        Debug.Log($"Loading Level 2 -> {level3Scene}");
        SceneManager.LoadScene(level3Scene);
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
