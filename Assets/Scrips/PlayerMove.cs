using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
	public float speed = 5f; // Movement speed
	public bool canJump = false; // Whether the player can jump
	public float jumpForce = 7f; // Jump strength

	private Rigidbody2D rb; // Reference to the Rigidbody2D component
	private bool isGrounded; // Flag to check if the player is on the ground

	void Awake()
	{
		rb = GetComponent<Rigidbody2D>(); // Get the Rigidbody2D component on Awake
	}

	void Update()
	{
		CheckGround(); // Check if the player is on the ground

		Vector2 moveInput = Vector2.zero; // Initialize movement input

		if (Keyboard.current != null)
		{
			// Support Arrow Keys and A/D for horizontal movement
			float horizontal = 0f;

			horizontal += Keyboard.current.leftArrowKey.isPressed ? -1f : 0f;
			horizontal += Keyboard.current.rightArrowKey.isPressed ? 1f : 0f;
			horizontal += Keyboard.current.aKey.isPressed ? -1f : 0f;
			horizontal += Keyboard.current.dKey.isPressed ? 1f : 0f;

			// Clamp the final value to avoid speed stacking
			horizontal = Mathf.Clamp(horizontal, -1f, 1f);

			moveInput.x = horizontal;

			// Jump when the Space key is pressed
			if (Keyboard.current.spaceKey.wasPressedThisFrame)
			{
				Jump();
			}
		}

		// Optional: support gamepad left stick movement
		if (Gamepad.current != null)
		{
			float stickX = Gamepad.current.leftStick.ReadValue().x;
			moveInput.x = Mathf.Clamp(moveInput.x + stickX, -1f, 1f);
		}

		// Apply horizontal velocity while keeping the current vertical velocity
		rb.linearVelocity = new Vector2(moveInput.x * speed, rb.linearVelocity.y);
	}

	void Jump()
	{
		if (!canJump) return; // Do nothing if jumping is disabled
		if (!isGrounded) return; // Do nothing if not on the ground

		// Set upward velocity to make the player jump
		rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
	}

	void CheckGround()
	{
		// Detect if the player is standing on "Ground" or "Box"
		int mask = LayerMask.GetMask("Ground", "Box");

		RaycastHit2D hit = Physics2D.Raycast(
			transform.position,
			Vector2.down,
			0.6f,
			mask
		);

		isGrounded = hit.collider != null; // True if ray hits something
	}

	public void Stop()
	{
		rb.linearVelocity = Vector2.zero; // Stop all player movement
	}
}
