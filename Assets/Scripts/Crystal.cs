using UnityEngine;

public class Crystal : Switch
{
	public Transform cap; // Reference to the cap object that will move when activated

	private void OnTriggerEnter2D(Collider2D collision)
	{
		if (isOn) return; // If the crystal is already activated, do nothing

		if (collision.gameObject.layer == LayerMask.NameToLayer("Player_Soul"))
		{
			cap.localPosition = Vector3.zero; // Move the cap to the default local position
			isOn = true; // Mark the crystal as activated
		}
	}
}
