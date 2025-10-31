using UnityEngine;

public class VrDebug : MonoBehaviour
{
    [Header("UI References")]
    public GameObject UI;
    public GameObject UIAnchor;

    [Header("UI Settings")]
    public Vector3 UIAnchorOffset = Vector3.zero;
    public bool UIActive = false;

    [Header("Adjustment Settings")]
    public float moveSpeed = 0.5f;      // Base speed for moving the UI offset
    public float verticalSpeed = 0.3f;  // Vertical movement speed
    public bool adjustMode = false;     // Whether offset adjustment is enabled

    void Update()
    {
        // Toggle UI visibility (Y button)
        if (OVRInput.GetDown(OVRInput.Button.Four))
        {
            SetUIActive(!UIActive);
        }

        // Clear console (X button)
        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            GetComponent<ConsoleToText>().ClearLog();
        }

        // Toggle offset adjustment (Left Thumbstick Click)
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
        {
            adjustMode = !adjustMode;
        }

        // Handle offset movement when in adjustment mode
        if (adjustMode)
        {
            Vector2 thumbInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

            // Move horizontally and forward/backward relative to anchor
            UIAnchorOffset += (UIAnchor.transform.right * thumbInput.x + UIAnchor.transform.forward * thumbInput.y)
                              * moveSpeed * Time.deltaTime;

            // Vertical adjustment using grip buttons
            float vertical = 0f;
            if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger)) vertical += 1f;
            if (OVRInput.Get(OVRInput.Button.SecondaryHandTrigger)) vertical -= 1f;
            UIAnchorOffset += Vector3.up * vertical * verticalSpeed * Time.deltaTime;
        }

        // Apply position and rotation
        if (UIActive)
        {
            UI.transform.position = UIAnchor.transform.position + UIAnchorOffset;
            UI.transform.rotation = Quaternion.Euler(
                new Vector3(UIAnchor.transform.eulerAngles.x, UIAnchor.transform.eulerAngles.y, 0)
            );
        }
    }

    /// <summary>
    /// Enables or disables the VR Debug UI.
    /// </summary>
    /// <param name="active">True to enable, false to disable.</param>
    public void SetUIActive(bool active)
    {
        UIActive = active;
        if (UI != null)
        {
            UI.SetActive(active);
        }
    }
}
