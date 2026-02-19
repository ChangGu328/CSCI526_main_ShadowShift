using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("Name of the Level Select scene")]
    public string levelSelectSceneName = "LevelSelect";

    // Start Button
    public void OnStartButton()
    {
        SceneManager.LoadScene(levelSelectSceneName);
    }

    // Option Button (Todo)
    public void OnOptionButton()
    {
        Debug.Log("Option button clicked (not implemented yet)");
    }

    // Quit Button
    public void OnQuitButton()
    {
#if UNITY_EDITOR
        Debug.Log("Quit Game (Editor mode)");
#else
        Application.Quit();
#endif
    }
}
