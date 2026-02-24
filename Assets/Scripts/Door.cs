using UnityEngine;

public class Door : MonoBehaviour
{
	public Switch sw; // Reference to the switch that controls the door
	public GameObject leftDoor; // Left part of the door
	public GameObject rightDoor; // Right part of the door

	// Update is called once per frame
	void Update()
	{
		if (sw.isOn)
		{
			leftDoor.SetActive(false); // Hide left door when switch is on
			rightDoor.SetActive(false); // Hide right door when switch is on
		}
		else
		{
			leftDoor.SetActive(true); // Show left door when switch is off
			rightDoor.SetActive(true); // Show right door when switch is off
		}
	}
}
