using UnityEngine;

[DisallowMultipleComponent]
public class CameraFocusTrigger : MonoBehaviour
{
    [SerializeField] private DynamicCameraFollow dynamicCameraFollow;
    [SerializeField] private Transform focusPoint;
    [SerializeField, Min(0.01f)] private float focusOrthographicSize = 3f;
    [SerializeField, Min(0f)] private float focusDuration = 2f;

    private void OnValidate()
    {
        if (dynamicCameraFollow == null)
            dynamicCameraFollow = FindAnyObjectByType<DynamicCameraFollow>();
    }

    public void TriggerFocus()
    {
        if (dynamicCameraFollow == null)
            dynamicCameraFollow = FindAnyObjectByType<DynamicCameraFollow>();

        if (dynamicCameraFollow == null || focusPoint == null)
            return;

        dynamicCameraFollow.FocusOnPoint(focusPoint, focusOrthographicSize, focusDuration);
    }

    public void TriggerFocusAt(Transform overridePoint)
    {
        if (overridePoint == null)
            return;

        if (dynamicCameraFollow == null)
            dynamicCameraFollow = FindAnyObjectByType<DynamicCameraFollow>();

        if (dynamicCameraFollow == null)
            return;

        dynamicCameraFollow.FocusOnPoint(overridePoint, focusOrthographicSize, focusDuration);
    }

    public void ClearFocus()
    {
        if (dynamicCameraFollow == null)
            dynamicCameraFollow = FindAnyObjectByType<DynamicCameraFollow>();

        dynamicCameraFollow?.ClearFocusOverride();
    }
}
