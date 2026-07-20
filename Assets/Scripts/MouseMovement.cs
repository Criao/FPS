using UnityEngine;

[DefaultExecutionOrder(-100)]
public class MouseMovement : MonoBehaviour
{
    private const float SensitivityScale = 0.02f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Look Settings")]
    [Min(0f)]
    [SerializeField] private float mouseSensitivity = 100f;
    [Range(0f, 0.1f)]
    [SerializeField] private float mouseSmoothTime = 0.015f;
    [SerializeField] private float minVerticalAngle = -90f;
    [SerializeField] private float maxVerticalAngle = 90f;
    [Min(0f)]
    [SerializeField] private float maxLookDegreesPerFrame = 35f;

    private Vector2 smoothedMouseDelta;
    private Vector2 mouseDeltaVelocity;
    private float xRotation;
    private float yRotation;

    private void Awake()
    {
        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
        }

        xRotation = cameraTransform != null ? NormalizeAngle(cameraTransform.localEulerAngles.x) : 0f;
        yRotation = transform.eulerAngles.y;
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0))
            {
                LockCursor();
            }

            return;
        }

        Vector2 mouseDelta = GetMouseDelta();

        yRotation += mouseDelta.x;
        xRotation = Mathf.Clamp(xRotation - mouseDelta.y, minVerticalAngle, maxVerticalAngle);

        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }

    private Vector2 GetMouseDelta()
    {
        Vector2 rawDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        Vector2 targetDelta = rawDelta * mouseSensitivity * SensitivityScale;

        if (maxLookDegreesPerFrame > 0f)
        {
            targetDelta = Vector2.ClampMagnitude(targetDelta, maxLookDegreesPerFrame);
        }

        if (mouseSmoothTime <= 0f)
        {
            smoothedMouseDelta = targetDelta;
            mouseDeltaVelocity = Vector2.zero;
            return targetDelta;
        }

        smoothedMouseDelta = Vector2.SmoothDamp(
            smoothedMouseDelta,
            targetDelta,
            ref mouseDeltaVelocity,
            mouseSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

        return smoothedMouseDelta;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
