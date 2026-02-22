using UnityEngine;

public class TriggerDoorOpen : MonoBehaviour
{
    [Header("Switch Link")]
    public Switch currentDoorSwitch;
    public Switch targetDoorSwitch;
    [Min(0f)] public float targetOpenSeconds = 0f;

    private bool previousCurrentIsOn;
    private float targetOpenUntil = -1f;

    private void Awake()
    {
        if (currentDoorSwitch == null)
        {
            Door door = GetComponent<Door>();
            if (door != null)
            {
                currentDoorSwitch = door.sw;
            }
        }

        if (currentDoorSwitch != null)
        {
            previousCurrentIsOn = currentDoorSwitch.isOn;
        }
    }

    private void Update()
    {
        if (currentDoorSwitch == null || targetDoorSwitch == null)
        {
            return;
        }

        bool currentIsOn = currentDoorSwitch.isOn;

        // Backward compatibility: <= 0 means keep the original always-open behavior.
        if (targetOpenSeconds <= 0f)
        {
            if (currentIsOn)
            {
                targetDoorSwitch.isOn = true;
            }

            previousCurrentIsOn = currentIsOn;
            return;
        }

        // Trigger once when current switch changes from off -> on.
        if (currentIsOn && !previousCurrentIsOn)
        {
            targetDoorSwitch.isOn = true;
            targetOpenUntil = Time.time + targetOpenSeconds;
        }

        if (targetOpenUntil > 0f && Time.time >= targetOpenUntil)
        {
            targetDoorSwitch.isOn = false;
            targetOpenUntil = -1f;
        }

        previousCurrentIsOn = currentIsOn;
    }
}
