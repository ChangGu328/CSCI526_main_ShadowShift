using UnityEngine;

public class Pressable : Switch
{
	private Vector2 originLocalScale; // Original local scale of the object
	private Vector3 originLocalPosition; // Original local position of the object

	private void Start()
	{
		originLocalScale = transform.localScale; // Store initial scale
		originLocalPosition = transform.localPosition; // Store initial LOCAL position
	}

	private void OnTriggerExit2D(Collider2D collision)
	{
		// When player body or box leaves the trigger, turn off the switch
		if (collision.gameObject.layer == LayerMask.NameToLayer("Player_Body")
			|| collision.gameObject.layer == LayerMask.NameToLayer("Box"))
		{
			isOn = false;
		}
	}

	private void OnTriggerStay2D(Collider2D collision)
	{
		// While player body or box is inside the trigger, turn on the switch
		if (collision.gameObject.layer == LayerMask.NameToLayer("Player_Body")
			|| collision.gameObject.layer == LayerMask.NameToLayer("Box"))
		{
			isOn = true;
		}
	}

	private void Update()
	{
		// Apply scale based on whether the switch is on
		if (isOn)
		{
			ApplyScale(0.3f); // Pressed state
		}
		else
		{
			ApplyScale(1f); // Original state
		}
	}

	void ApplyScale(float yScale)
	{
		// Scale the object on the Y axis
		transform.localScale = new Vector2(originLocalScale.x, originLocalScale.y * yScale);

		// Adjust local position so the object appears to be pressed from top
		float deltaY = (originLocalScale.y - transform.localScale.y) * 0.5f;
		transform.localPosition = originLocalPosition - new Vector3(0, deltaY, 0);
	}
}
