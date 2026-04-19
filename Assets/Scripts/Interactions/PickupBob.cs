using UnityEngine;

[DisallowMultipleComponent]
public class PickupBob : MonoBehaviour
{
    [SerializeField, Min(0f)] private float amplitude = 0.06f;
    [SerializeField, Min(0f)] private float frequency = 2.25f;
    [SerializeField] private bool randomizeStartOffset = true;

    private Vector3 baseLocalPosition;
    private float timeOffset;
    private bool hasInitialized;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        timeOffset = randomizeStartOffset ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        hasInitialized = true;
    }

    private void OnEnable()
    {
        if (!hasInitialized)
        {
            baseLocalPosition = transform.localPosition;
            timeOffset = randomizeStartOffset ? Random.Range(0f, Mathf.PI * 2f) : 0f;
            hasInitialized = true;
        }

        transform.localPosition = baseLocalPosition;
    }

    private void LateUpdate()
    {
        float bobOffset = Mathf.Sin((Time.time * frequency) + timeOffset) * amplitude;
        transform.localPosition = baseLocalPosition + (Vector3.up * bobOffset);
    }
}
