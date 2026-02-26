using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectController : MonoBehaviour
{
    [Header("Scene names")]

    [Tooltip("Scene name for Tutorial")]
    [SerializeField] private string tutorialScene = "tutorial";

    [Tooltip("Scene name for Level 1")]
    [SerializeField] private string level1Scene = "level1";

    [Tooltip("Scene name for Level 2")]
    [SerializeField] private string level2Scene = "level2";

    [Tooltip("Scene name for Level 3")]
    [SerializeField] private string level3Scene = "level3";

    [Tooltip("Name of main menu scene to return to")]
    [SerializeField] private string mainMenuScene = "MainMenu";


    // Called by Tutorial button
    public void OnTTLButton()
    {
        if (string.IsNullOrEmpty(tutorialScene))
        {
            Debug.LogError("LevelSelectController: tutorialScene is empty.");
            return;
        }

        Debug.Log($"Loading Tutorial -> {tutorialScene}");
        SceneManager.LoadScene(tutorialScene);
    }

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

    public void OnLevel3Button()
    {
        if (string.IsNullOrEmpty(level3Scene))
        {
            Debug.LogError("LevelSelectController: level3Scene is empty.");
            return;
        }

        Debug.Log($"Loading Level 3 -> {level3Scene}");
        SceneManager.LoadScene(level3Scene);
    }

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