using UnityEngine;

public class DynamicCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float followDistance = 10f;

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PistaController pistaController;

    [Header("Framing")]
    [SerializeField] private float positionSmoothTime = 0.12f;
    [SerializeField] private Vector2 framingPadding = new Vector2(1.5f, 1.25f);
    [SerializeField] private float latchedPlayerWeight = 0.7f;
    [SerializeField] private float latchedPistaWeight = 0.3f;
    [SerializeField] private float movingPlayerWeight = 0.6f;
    [SerializeField] private float movingPistaWeight = 0.4f;
    [SerializeField] private float aimingPlayerWeight = 0.55f;
    [SerializeField] private float aimingPistaWeight = 0.2f;
    [SerializeField] private float aimingPreviewWeight = 0.25f;
    [SerializeField] private float extraPlayerWeightForNonNormalState = 0.15f;

    [Header("Zoom")]
    [SerializeField] private float baseOrthographicSize = 5f;
    [SerializeField] private float maxOrthographicSize = 7f;
    [SerializeField] private float zoomOutSmoothTime = 0.1f;
    [SerializeField] private float zoomInSmoothTime = 0.2f;

    private readonly FocusTarget[] focusTargets = new FocusTarget[3];
    private Vector3 positionVelocity;
    private float zoomVelocity;

    private struct FocusTarget
    {
        public Vector3 Position;
        public float Weight;

        public FocusTarget(Vector3 position, float weight)
        {
            Position = position;
            Weight = weight;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        SnapToFrame();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        UpdateFrame(snapInstantly: false);
    }

    private void OnValidate()
    {
        if (followDistance < 0f)
            followDistance = 0f;

        positionSmoothTime = Mathf.Max(0f, positionSmoothTime);
        framingPadding.x = Mathf.Max(0f, framingPadding.x);
        framingPadding.y = Mathf.Max(0f, framingPadding.y);

        latchedPlayerWeight = Mathf.Max(0f, latchedPlayerWeight);
        latchedPistaWeight = Mathf.Max(0f, latchedPistaWeight);
        movingPlayerWeight = Mathf.Max(0f, movingPlayerWeight);
        movingPistaWeight = Mathf.Max(0f, movingPistaWeight);
        aimingPlayerWeight = Mathf.Max(0f, aimingPlayerWeight);
        aimingPistaWeight = Mathf.Max(0f, aimingPistaWeight);
        aimingPreviewWeight = Mathf.Max(0f, aimingPreviewWeight);
        extraPlayerWeightForNonNormalState = Mathf.Max(0f, extraPlayerWeightForNonNormalState);

        baseOrthographicSize = Mathf.Max(0.01f, baseOrthographicSize);
        maxOrthographicSize = Mathf.Max(baseOrthographicSize, maxOrthographicSize);
        zoomOutSmoothTime = Mathf.Max(0f, zoomOutSmoothTime);
        zoomInSmoothTime = Mathf.Max(0f, zoomInSmoothTime);
    }

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
        playerController = FindFirstObjectByType<PlayerController>();
        pistaController = FindFirstObjectByType<PistaController>();
        target = playerController != null ? playerController.transform : null;
    }

    private void ResolveReferences()
    {
        if (target == null)
            target = FindFirstObjectByType<PlayerController>()?.transform;

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (playerController == null)
        {
            playerController = target != null
                ? target.GetComponent<PlayerController>()
                : FindFirstObjectByType<PlayerController>();
        }

        if (pistaController == null)
            pistaController = FindFirstObjectByType<PistaController>();

        if (targetCamera != null && targetCamera.orthographic && baseOrthographicSize <= 0f)
            baseOrthographicSize = targetCamera.orthographicSize;
    }

    private void SnapToFrame()
    {
        UpdateFrame(snapInstantly: true);
    }

    private void UpdateFrame(bool snapInstantly)
    {
        if (target == null)
            return;

        int focusTargetCount = BuildFocusTargets();
        Vector3 desiredCenter = CalculateWeightedCenter(focusTargetCount);
        Vector3 desiredPosition = new Vector3(desiredCenter.x, desiredCenter.y, target.position.z - followDistance);

        if (snapInstantly || positionSmoothTime <= 0f)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, positionSmoothTime);
        }

        UpdateOrthographicSize(desiredCenter, focusTargetCount, snapInstantly);
    }

    private int BuildFocusTargets()
    {
        int focusTargetCount = 0;
        AddFocusTarget(ref focusTargetCount, target.position, 1f);

        if (pistaController == null)
            return focusTargetCount;

        bool playerInPistaFocus = playerController != null && playerController.CurrentState == PlayerController.PlayerState.PistaFocus;
        bool playerInNonNormalState = playerController != null
            && playerController.CurrentState != PlayerController.PlayerState.Normal
            && playerController.CurrentState != PlayerController.PlayerState.PistaFocus;
        float playerWeightBonus = playerInNonNormalState ? extraPlayerWeightForNonNormalState : 0f;

        switch (pistaController.CurrentState)
        {
            case PistaController.PistaState.MovingToLantern:
                focusTargets[0] = new FocusTarget(target.position, movingPlayerWeight + playerWeightBonus);
                AddFocusTarget(ref focusTargetCount, pistaController.transform.position, movingPistaWeight);
                return focusTargetCount;

            case PistaController.PistaState.LatchedToLantern:
                focusTargets[0] = new FocusTarget(target.position, latchedPlayerWeight + playerWeightBonus);
                AddFocusTarget(ref focusTargetCount, pistaController.transform.position, latchedPistaWeight);
                return focusTargetCount;
        }

        if (!playerInPistaFocus)
            return focusTargetCount;

        focusTargets[0] = new FocusTarget(target.position, aimingPlayerWeight + playerWeightBonus);
        AddFocusTarget(ref focusTargetCount, pistaController.transform.position, aimingPistaWeight);

        if (pistaController.CurrentPreviewTarget != null)
            AddFocusTarget(ref focusTargetCount, pistaController.CurrentPreviewTarget.position, aimingPreviewWeight);

        return focusTargetCount;
    }

    private Vector3 CalculateWeightedCenter(int focusTargetCount)
    {
        if (focusTargetCount <= 0)
            return target != null ? target.position : transform.position;

        Vector3 weightedPosition = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < focusTargetCount; i++)
        {
            float weight = focusTargets[i].Weight;

            if (weight <= Mathf.Epsilon)
                continue;

            weightedPosition += focusTargets[i].Position * weight;
            totalWeight += weight;
        }

        if (totalWeight <= Mathf.Epsilon)
            return focusTargets[0].Position;

        return weightedPosition / totalWeight;
    }

    private void UpdateOrthographicSize(Vector3 desiredCenter, int focusTargetCount, bool snapInstantly)
    {
        if (targetCamera == null || !targetCamera.orthographic)
            return;

        float desiredSize = CalculateOrthographicSize(desiredCenter, focusTargetCount);

        if (snapInstantly)
        {
            targetCamera.orthographicSize = desiredSize;
            return;
        }

        float currentSize = targetCamera.orthographicSize;
        float smoothTime = desiredSize > currentSize ? zoomOutSmoothTime : zoomInSmoothTime;

        if (smoothTime <= 0f)
        {
            targetCamera.orthographicSize = desiredSize;
            return;
        }

        targetCamera.orthographicSize = Mathf.SmoothDamp(currentSize, desiredSize, ref zoomVelocity, smoothTime);
    }

    private float CalculateOrthographicSize(Vector3 desiredCenter, int focusTargetCount)
    {
        float requiredHalfHeight = 0f;
        float requiredHalfWidth = 0f;

        for (int i = 0; i < focusTargetCount; i++)
        {
            Vector3 focusPosition = focusTargets[i].Position;
            requiredHalfWidth = Mathf.Max(requiredHalfWidth, Mathf.Abs(focusPosition.x - desiredCenter.x));
            requiredHalfHeight = Mathf.Max(requiredHalfHeight, Mathf.Abs(focusPosition.y - desiredCenter.y));
        }

        requiredHalfWidth += framingPadding.x;
        requiredHalfHeight += framingPadding.y;

        float aspect = targetCamera.aspect > Mathf.Epsilon ? targetCamera.aspect : 1f;
        float sizeForWidth = requiredHalfWidth / aspect;
        float desiredSize = Mathf.Max(baseOrthographicSize, requiredHalfHeight, sizeForWidth);
        return Mathf.Clamp(desiredSize, baseOrthographicSize, maxOrthographicSize);
    }

    private void AddFocusTarget(ref int focusTargetCount, Vector3 position, float weight)
    {
        if (focusTargetCount >= focusTargets.Length || weight <= Mathf.Epsilon)
            return;

        focusTargets[focusTargetCount] = new FocusTarget(position, weight);
        focusTargetCount++;
    }
}
