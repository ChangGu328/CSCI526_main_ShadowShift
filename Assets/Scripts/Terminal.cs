using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Terminal : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject winPanel;
    public GameObject[] starImages;

    [Header("Level Settings")]
    public float targetTime = 60f;
    public string nextLevelName;

    public float Timer { get; private set; }
    public bool IsFinished { get; private set; }

    private void Start()
    {
        Timer = 0f;
        IsFinished = false;
        winPanel.SetActive(false);
    }

    private void Update()
    {
        if (!IsFinished)
        {
            Timer += Time.deltaTime;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player_Body") && !IsFinished)
        {
            FinishLevel();
        }
    }

    public Sprite starOn;
    public Sprite starOff;

    private void FinishLevel()
    {
        IsFinished = true;
        Time.timeScale = 0;

        int starsEarned = CalculateStars();
        winPanel.SetActive(true);

        for (int i = 0; i < starImages.Length; i++)
        {
            Image img = starImages[i].GetComponent<Image>();
            img.sprite = (i < starsEarned) ? starOn : starOff;
        }
    }

    private int CalculateStars()
    {
        int stars = 0;
        stars++; 
        if (Timer <= targetTime) stars++; 
        if (CollectibleManager.Instance.IsAllCollected()) stars++;
        return stars;
    }

    public void OnNextLevel() { Time.timeScale = 1f; SceneManager.LoadScene(nextLevelName); }
    public void OnLevelSelect() { Time.timeScale = 1f; SceneManager.LoadScene("LevelSelect"); }
    public void OnMainMenu() { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
}
