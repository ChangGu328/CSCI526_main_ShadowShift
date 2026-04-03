using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PLAYERSTATE currentState; // Current state of the player (Body or Soul)

    public GameObject body; // Reference to the body GameObject
    public GameObject soul; // Reference to the soul GameObject
    [SerializeField] private float shadowEnterOffsetX = 0.3f; // Small right offset when entering shadow form

    public HUDManager hudManager; // NEW

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            //Debug.Log("Q pressed, currentState = " + currentState);
            HandleSwitch();
        }
    }

    void HandleSwitch()
    {
        if (currentState == PLAYERSTATE.BODY)
        {
            EnterShadow();
        }
        else
        {
            EnterBody();
        }
    }

    void EnterShadow()
    {
        //Debug.Log("EnterShadow called");

        body.GetComponent<PlayerMove>().Stop();
        body.GetComponent<PlayerMove>().enabled = false;

        currentState = PLAYERSTATE.Soul;
        soul.transform.position = body.transform.position + new Vector3(shadowEnterOffsetX, 0f, 0f);
        soul.SetActive(true);

        soul.GetComponent<PlayerMove>().enabled = true;

        //Debug.Log("Calling HUD with state = " + currentState);
        hudManager?.UpdateFormUI(PLAYERSTATE.Soul);
    }


    void EnterBody()
    {
        //Debug.Log("EnterBody called");

        soul.GetComponent<PlayerMove>().Stop();
        soul.GetComponent<PlayerMove>().enabled = false;

        currentState = PLAYERSTATE.BODY;
        Vector2 pos = body.transform.position;
        body.transform.position = soul.transform.position;
        soul.transform.position = pos;

        body.GetComponent<PlayerMove>().enabled = true;

        //Debug.Log("Calling HUD with state = " + currentState);
        hudManager?.UpdateFormUI(PLAYERSTATE.BODY);
    }
}
