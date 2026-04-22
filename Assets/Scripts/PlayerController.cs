using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PLAYERSTATE currentState; // Current state of the player (Body or Soul)

    public GameObject body; // Reference to the body GameObject
    public GameObject soul; // Reference to the soul GameObject
    [SerializeField] private float shadowEnterOffsetX = 0.3f; // Small right offset when entering shadow form

    [Header("Return Animation")]
    [SerializeField] private float returnMoveDuration = 0.45f;

    [Header("Art")]
    [SerializeField] private PlayerAnimator bodyAnimator; // attached to Player_Body

    public HUDManager hudManager; // NEW

    private bool isTransitioning = false;

    void Update()
    {
        if (isTransitioning) return;

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
            StartCoroutine(EnterBodyAnimated());
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

        bodyAnimator?.PlayEnterShadow();

        //Debug.Log("Calling HUD with state = " + currentState);
        hudManager?.UpdateFormUI(PLAYERSTATE.Soul);
    }


    IEnumerator EnterBodyAnimated()
    {
        isTransitioning = true;

        bodyAnimator?.PlayExitShadow();

        // Stop soul and body control during the slide
        soul.GetComponent<PlayerMove>().Stop();
        soul.GetComponent<PlayerMove>().enabled = false;

        PlayerMove bodyMove = body.GetComponent<PlayerMove>();
        bodyMove.Stop();
        bodyMove.enabled = false;

        // Temporarily freeze body physics so gravity/collisions don't fight the tween
        Rigidbody2D bodyRb = body.GetComponent<Rigidbody2D>();
        RigidbodyType2D prevBodyType = RigidbodyType2D.Dynamic;
        if (bodyRb != null)
        {
            prevBodyType = bodyRb.bodyType;
            bodyRb.linearVelocity = Vector2.zero;
            bodyRb.bodyType = RigidbodyType2D.Kinematic;
        }

        // Disable body's colliders so the slide (a) ignores hazards, (b) emits OnTriggerExit2D on any
        // pressure plates it is currently sitting on (while layer is still Player_Body, so their layer
        // check passes), and (c) doesn't trigger anything along the slide path.
        Collider2D[] bodyColliders = body.GetComponents<Collider2D>();
        bool[] bodyColliderPrevEnabled = new bool[bodyColliders.Length];
        for (int i = 0; i < bodyColliders.Length; i++)
        {
            bodyColliderPrevEnabled[i] = bodyColliders[i].enabled;
            bodyColliders[i].enabled = false;
        }

        Vector3 start = body.transform.position;
        Vector3 end = soul.transform.position;

        float t = 0f;
        while (t < returnMoveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / returnMoveDuration);
            // smootherstep easing (6x^5 - 15x^4 + 10x^3) for a softer start/stop than smoothstep
            float eased = k * k * k * (k * (k * 6f - 15f) + 10f);
            body.transform.position = Vector3.Lerp(start, end, eased);
            yield return null;
        }

        body.transform.position = end;

        if (bodyRb != null)
        {
            bodyRb.bodyType = prevBodyType;
            bodyRb.linearVelocity = Vector2.zero;
        }

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            bodyColliders[i].enabled = bodyColliderPrevEnabled[i];
        }

        // Shadow disappears now that entity has arrived
        soul.SetActive(false);

        currentState = PLAYERSTATE.BODY;
        bodyMove.enabled = true;

        //Debug.Log("Calling HUD with state = " + currentState);
        hudManager?.UpdateFormUI(PLAYERSTATE.BODY);

        isTransitioning = false;
    }
}
