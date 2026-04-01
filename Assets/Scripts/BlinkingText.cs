using System.Collections;
using TMPro;
using UnityEngine;

public class BlinkingText : MonoBehaviour
{
    private TextMeshProUGUI tmp;
    public float fadeSpeed = 1.5f;
    public Transform box; 

    private Vector3 initialBoxPos;

    void Start()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        initialBoxPos = box.position;
        StartCoroutine(FadeLoop());
    }

    void Update()
    {
     
        if (Vector3.Distance(box.position, initialBoxPos) > 0.1f)
        {
            tmp.enabled = false;
            StopAllCoroutines();
        }
    }

    IEnumerator FadeLoop()
    {
        while (true)
        {
            yield return StartCoroutine(Fade(1f, 0f));
            yield return StartCoroutine(Fade(0f, 1f));
        }
    }

    IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsed = 0f;
        Color color = tmp.color;

        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime * fadeSpeed;
            color.a = Mathf.Lerp(startAlpha, endAlpha, elapsed);
            tmp.color = color;
            yield return null;
        }

        color.a = endAlpha;
        tmp.color = color;
    }
}