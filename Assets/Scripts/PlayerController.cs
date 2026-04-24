using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public PLAYERSTATE currentState;

    public GameObject body;
    public GameObject soul;
    [SerializeField] private float shadowEnterOffsetX = 0.3f;

    [Header("Return Animation")]
    [SerializeField] private float returnMoveDuration = 0.45f;

    [Header("Art")]
    [SerializeField] private PlayerAnimator bodyAnimator;

    public HUDManager hudManager;

    public bool wallBlocking = false;

    private bool isTransitioning = false;

    void Update()
    {
        if (isTransitioning) return;

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            HandleSwitch();
        }
    }

    void HandleSwitch()
    {
        if (currentState == PLAYERSTATE.BODY)
        {
            if (wallBlocking)
            {
                wallBlocking = false;
                EnterShadow();
            }
            else
            {
                EnterShadow();
            }
        }
        else
        {
            StartCoroutine(EnterBodyAnimated());
        }
    }

    void EnterShadow()
    {
        body.GetComponent<PlayerMove>().Stop();
        body.GetComponent<PlayerMove>().enabled = false;

        currentState = PLAYERSTATE.Soul;
        soul.transform.position = body.transform.position + new Vector3(shadowEnterOffsetX, 0f, 0f);
        soul.transform.rotation = Quaternion.identity;
        Rigidbody2D soulRb = soul.GetComponent<Rigidbody2D>();
        if (soulRb != null)
        {
            soulRb.angularVelocity = 0f;
            soulRb.linearVelocity = Vector2.zero;
        }
        soul.SetActive(true);
        soul.GetComponent<PlayerMove>().enabled = true;

        bodyAnimator?.PlayEnterShadow();
        hudManager?.UpdateFormUI(PLAYERSTATE.Soul);
    }

    IEnumerator EnterBodyAnimated()
    {
        isTransitioning = true;

        bodyAnimator?.PlayExitShadow();

        soul.GetComponent<PlayerMove>().Stop();
        soul.GetComponent<PlayerMove>().enabled = false;

        PlayerMove bodyMove = body.GetComponent<PlayerMove>();
        bodyMove.Stop();
        bodyMove.enabled = false;

        Rigidbody2D bodyRb = body.GetComponent<Rigidbody2D>();
        RigidbodyType2D prevBodyType = RigidbodyType2D.Dynamic;
        if (bodyRb != null)
        {
            prevBodyType = bodyRb.bodyType;
            bodyRb.linearVelocity = Vector2.zero;
            bodyRb.bodyType = RigidbodyType2D.Kinematic;
        }

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

        soul.SetActive(false);

        currentState = PLAYERSTATE.BODY;
        bodyMove.enabled = true;

        hudManager?.UpdateFormUI(PLAYERSTATE.BODY);

        isTransitioning = false;
    }

    public void NotifyForcedReturnToBody()
    {
        bodyAnimator?.PlayExitShadow();
        hudManager?.UpdateFormUI(PLAYERSTATE.BODY);
    }
}