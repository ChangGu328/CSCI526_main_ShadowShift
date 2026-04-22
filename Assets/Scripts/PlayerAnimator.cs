using UnityEngine;

// Drives the Player_Body Animator based on movement and shadow-switch events.
// Animator parameters it expects:
//   Bool    IsRunning        - true while horizontal velocity exceeds moveThreshold
//   Bool    IsGrounded       - mirrors PlayerMove.IsGrounded
//   Float   VerticalVelocity - rigidbody vy, for distinguishing Jump (>0) vs Fall (<0)
//   Trigger Land             - fires the frame the player touches down
//   Trigger EnterShadow      - swap to shadow form (plays SitDown -> Resting)
//   Trigger ExitShadow       - swap back to body form (returns to Idle)
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerMove playerMove;

    [Tooltip("Velocity below this magnitude is treated as idle.")]
    [SerializeField] private float moveThreshold = 0.1f;

    [Tooltip("Set true if the source sprites face left by default.")]
    [SerializeField] private bool spriteFacesLeftByDefault = false;

    private static readonly int ParamIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int ParamIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int ParamVerticalVel = Animator.StringToHash("VerticalVelocity");
    private static readonly int ParamLand = Animator.StringToHash("Land");
    private static readonly int ParamEnterShadow = Animator.StringToHash("EnterShadow");
    private static readonly int ParamExitShadow = Animator.StringToHash("ExitShadow");

    private bool shadowAnimActive = false;
    private bool prevGrounded = true;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
    }

    private void Update()
    {
        if (animator == null) return;

        float vx = rb != null ? rb.linearVelocity.x : 0f;
        float vy = rb != null ? rb.linearVelocity.y : 0f;
        bool moving = Mathf.Abs(vx) > moveThreshold;
        bool grounded = playerMove != null ? playerMove.IsGrounded : true;

        // Don't flip into Run while we're mid-shadow animation (body should sit/rest even if nudged)
        animator.SetBool(ParamIsRunning, !shadowAnimActive && moving);
        animator.SetBool(ParamIsGrounded, grounded);
        animator.SetFloat(ParamVerticalVel, vy);

        // Fire Land trigger on the frame we transition from air -> ground
        if (!prevGrounded && grounded && !shadowAnimActive)
        {
            animator.SetTrigger(ParamLand);
        }
        prevGrounded = grounded;

        if (spriteRenderer != null && moving)
        {
            bool movingLeft = vx < 0f;
            spriteRenderer.flipX = spriteFacesLeftByDefault ? !movingLeft : movingLeft;
        }
    }

    public void PlayEnterShadow()
    {
        if (animator == null) return;
        shadowAnimActive = true;
        animator.ResetTrigger(ParamExitShadow);
        animator.SetBool(ParamIsRunning, false);
        animator.SetTrigger(ParamEnterShadow);
    }

    public void PlayExitShadow()
    {
        if (animator == null) return;
        shadowAnimActive = false;
        animator.ResetTrigger(ParamEnterShadow);
        animator.SetTrigger(ParamExitShadow);
    }
}
