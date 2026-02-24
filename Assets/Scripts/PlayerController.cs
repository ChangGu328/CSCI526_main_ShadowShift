using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
	public PLAYERSTATE currentState; // Current state of the player (Body or Soul)

	public GameObject body; // Reference to the body GameObject
	public GameObject soul; // Reference to the soul GameObject

	void Update()
	{
		if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
		{
			// Switch player state when Q is pressed
			HandleSwitch();
		}
	}

	void HandleSwitch()
	{
		// Determine which state to switch to
		if (currentState == PLAYERSTATE.BODY)
		{
			EnterShadow(); // Switch to Soul form
		}
		else
		{
			EnterBody(); // Switch to Body form
		}
	}

	void EnterShadow()
	{
		// Stop body movement and disable its PlayerMove script
		body.GetComponent<PlayerMove>().Stop();
		body.GetComponent<PlayerMove>().enabled = false;

		// Set state to Soul and position soul at body's location
		currentState = PLAYERSTATE.Soul;
		soul.transform.position = body.transform.position;
		soul.SetActive(true);

		// Enable movement for the soul
		soul.GetComponent<PlayerMove>().enabled = true;
	}

	void EnterBody()
	{
		// Stop soul movement and disable its PlayerMove script
		soul.GetComponent<PlayerMove>().Stop();
		soul.GetComponent<PlayerMove>().enabled = false;

		// Set state to Body and swap positions of body and soul
		currentState = PLAYERSTATE.BODY;
		Vector2 pos = body.transform.position;
		body.transform.position = soul.transform.position;
		soul.transform.position = pos;

		// Enable movement for the body
		body.GetComponent<PlayerMove>().enabled = true;
	}
}
