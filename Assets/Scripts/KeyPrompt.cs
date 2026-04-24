using UnityEngine;
using UnityEngine.InputSystem;

public class KeyPrompt : MonoBehaviour
{
    public Key key;
    public Color normalColor = Color.white;
    public Color pressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[key].isPressed)
            sr.color = pressedColor;
        else
            sr.color = normalColor;
    }
}