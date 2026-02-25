using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
	[HideInInspector] public float lastPortalTime = -999f;
	[Header("Movement")]
	public float maxMoveSpeed = 6f;
	public float groundAcceleration = 55f;
	public float groundDeceleration = 70f;
	public float airAcceleration = 35f;
	public float airDeceleration = 30f;

	[Header("Jump")]
	public bool canJump = false;
	public float jumpForce = 12f;
	public float coyoteTime = 0.12f;
	public float jumpBufferTime = 0.12f;
	[Range(0f, 1f)]
	public float jumpCutMultiplier = 0.5f;

	[Header("Gravity")]
	public float fallGravityMultiplier = 2f;
	public float lowJumpGravityMultiplier = 1.5f;
	public float maxFallSpeed = 18f;

	[Header("Visual Smooth")]
	public bool useRigidbodyInterpolation = true;

	[Header("Ground Check")]
	public LayerMask groundMask;
	public Vector2 groundCheckBoxSize = new Vector2(0.7f, 0.1f);
	public float groundCheckOffsetY = 0.06f;

	private Rigidbody2D rb;
	private Collider2D col;

	private float moveInputX;
	private bool jumpPressedThisFrame;
	private bool jumpHeld;

	private float coyoteTimer;
	private float jumpBufferTimer;
	private bool isGrounded;

	private void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		col = GetComponent<Collider2D>();

		if (useRigidbodyInterpolation && rb != null && rb.interpolation == RigidbodyInterpolation2D.None)
		{
			rb.interpolation = RigidbodyInterpolation2D.Interpolate;
		}

		if (groundMask.value == 0)
		{
			groundMask = LayerMask.GetMask("Ground", "Box");
		}
	}

	private void OnEnable()
	{
		moveInputX = 0f;
		jumpPressedThisFrame = false;
		jumpHeld = false;
		coyoteTimer = 0f;
		jumpBufferTimer = 0f;
	}

	private void Update()
	{
		ReadInput();
		UpdateTimers();
	}

	private void FixedUpdate()
	{
		isGrounded = CheckGrounded();

		if (isGrounded)
		{
			coyoteTimer = coyoteTime;
		}

		ApplyHorizontalMovement();
		ApplyJump();
		ApplyBetterGravity();

		jumpPressedThisFrame = false;
	}

	private void ReadInput()
	{
		float horizontal = 0f;
		bool keyboardJumpPressed = false;
		bool keyboardJumpHeld = false;

		if (Keyboard.current != null)
		{
			if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
			{
				horizontal -= 1f;
			}

			if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
			{
				horizontal += 1f;
			}

			keyboardJumpPressed = Keyboard.current.wKey.wasPressedThisFrame;
			keyboardJumpHeld = Keyboard.current.wKey.isPressed;
		}

		if (Gamepad.current != null)
		{
			float stickX = Gamepad.current.leftStick.ReadValue().x;
			horizontal = Mathf.Clamp(horizontal + stickX, -1f, 1f);

			bool gamepadJumpPressed = Gamepad.current.buttonSouth.wasPressedThisFrame;
			bool gamepadJumpHeld = Gamepad.current.buttonSouth.isPressed;
			keyboardJumpPressed |= gamepadJumpPressed;
			keyboardJumpHeld |= gamepadJumpHeld;
		}

		moveInputX = Mathf.Clamp(horizontal, -1f, 1f);
		jumpPressedThisFrame = keyboardJumpPressed;
		jumpHeld = keyboardJumpHeld;
	}

	private void UpdateTimers()
	{
		if (coyoteTimer > 0f)
		{
			coyoteTimer -= Time.deltaTime;
		}

		if (jumpPressedThisFrame)
		{
			jumpBufferTimer = jumpBufferTime;
		}
		else if (jumpBufferTimer > 0f)
		{
			jumpBufferTimer -= Time.deltaTime;
		}
	}

	private void ApplyHorizontalMovement()
	{
		float targetSpeed = moveInputX * maxMoveSpeed;
		float accelRate = Mathf.Abs(targetSpeed) > 0.01f
			? (isGrounded ? groundAcceleration : airAcceleration)
			: (isGrounded ? groundDeceleration : airDeceleration);

		float nextX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
		rb.linearVelocity = new Vector2(nextX, rb.linearVelocity.y);
	}

	private void ApplyJump()
	{
		if (!canJump)
		{
			return;
		}

		if (jumpBufferTimer > 0f && coyoteTimer > 0f)
		{
			rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
			jumpBufferTimer = 0f;
			coyoteTimer = 0f;
		}
	}

	private void ApplyBetterGravity()
	{
		Vector2 velocity = rb.linearVelocity;

		if (velocity.y < 0f)
		{
			velocity.y += Physics2D.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
		}
		else if (velocity.y > 0f && !jumpHeld)
		{
			velocity.y += Physics2D.gravity.y * (lowJumpGravityMultiplier - 1f) * Time.fixedDeltaTime;
			velocity.y = Mathf.Min(velocity.y, jumpForce * jumpCutMultiplier);
		}

		if (velocity.y < -maxFallSpeed)
		{
			velocity.y = -maxFallSpeed;
		}

		rb.linearVelocity = velocity;
	}

	private bool CheckGrounded()
	{
		if (col != null)
		{
			Bounds b = col.bounds;
			Vector2 origin = new Vector2(b.center.x, b.min.y - groundCheckOffsetY);
			Collider2D hit = Physics2D.OverlapBox(origin, groundCheckBoxSize, 0f, groundMask);
			return hit != null;
		}

		RaycastHit2D hitRay = Physics2D.Raycast(
			transform.position,
			Vector2.down,
			0.8f,
			groundMask
		);
		return hitRay.collider != null;
	}

	public void Stop()
	{
		moveInputX = 0f;
		jumpPressedThisFrame = false;
		jumpHeld = false;
		jumpBufferTimer = 0f;
		coyoteTimer = 0f;
		rb.linearVelocity = Vector2.zero;
	}
}
