using UnityEngine;
using UnityEngine.InputSystem;

public sealed class VoxelFlyCamera : MonoBehaviour
{
    [SerializeField] private float baseMoveSpeed = 18f;
    [SerializeField] private float fastMoveMultiplier = 3.5f;
    [SerializeField] private float slowMoveMultiplier = 0.35f;
    [SerializeField] private float lookSensitivity = 0.14f;

    private float _pitch;
    private float _yaw;
    private bool _cursorLocked;

    private void Awake()
    {
        Vector3 angles = transform.eulerAngles;
        _pitch = NormalizeAngle(angles.x);
        _yaw = angles.y;

        SetCursorLock(true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }
        else if (mouse.leftButton.wasPressedThisFrame && !_cursorLocked)
        {
            SetCursorLock(true);
        }

        if (_cursorLocked)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue() * lookSensitivity;
            _yaw += mouseDelta.x;
            _pitch = Mathf.Clamp(_pitch - mouseDelta.y, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        Vector3 moveInput = ReadMoveInput(keyboard);
        float moveSpeed = baseMoveSpeed;

        if (keyboard.leftShiftKey.isPressed)
        {
            moveSpeed *= fastMoveMultiplier;
        }
        else if (keyboard.leftCtrlKey.isPressed)
        {
            moveSpeed *= slowMoveMultiplier;
        }

        Vector3 movement =
            (transform.right * moveInput.x) +
            (Vector3.up * moveInput.y) +
            (transform.forward * moveInput.z);

        transform.position += movement * moveSpeed * Time.unscaledDeltaTime;
    }

    private static Vector3 ReadMoveInput(Keyboard keyboard)
    {
        float x = 0f;
        float y = 0f;
        float z = 0f;

        if (keyboard.aKey.isPressed)
        {
            x -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            x += 1f;
        }

        if (keyboard.qKey.isPressed)
        {
            y -= 1f;
        }

        if (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed)
        {
            y += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            z -= 1f;
        }

        if (keyboard.wKey.isPressed)
        {
            z += 1f;
        }

        return new Vector3(x, y, z).normalized;
    }

    private void SetCursorLock(bool shouldLock)
    {
        _cursorLocked = shouldLock;
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}
