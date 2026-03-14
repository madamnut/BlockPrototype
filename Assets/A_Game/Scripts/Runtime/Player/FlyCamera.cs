using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public sealed class FlyCamera : MonoBehaviour
{
    public enum CameraMode : byte
    {
        FirstPerson = 0,
        ThirdPersonBack = 1,
        ThirdPersonFront = 2,
    }

    public enum MovementMode : byte
    {
        Fly = 0,
        Ground = 1,
    }

    private const float CameraPitchMin = -89f;
    private const float CameraPitchMax = 89f;
    private const float HeadPitchMin = -60f;
    private const float HeadPitchMax = 80f;

    [Header("Scene References")]
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private CapsuleCollider playerCollider;

    [Header("Movement")]
    [FormerlySerializedAs("moveSpeed")]
    [SerializeField] private float groundMoveSpeed = 6f;
    [FormerlySerializedAs("flyVerticalSpeed")]
    [SerializeField] private float flyMoveSpeed = 6f;
    [SerializeField] private float fastMoveMultiplier = 2f;
    [SerializeField] private float slowMoveMultiplier = 0.35f;
    [SerializeField] private float gravity = 30f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float lookSensitivity = 0.14f;
    [SerializeField] private float spaceDoubleTapWindow = 0.3f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float collisionSkinWidth = 0.02f;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [Header("Third Person Camera")]
    [SerializeField, Min(0.5f)] private float thirdPersonBackDistance = 4f;
    [SerializeField, Min(0.5f)] private float thirdPersonFrontDistance = 3f;
    [SerializeField, Min(0.05f)] private float cameraCollisionRadius = 0.2f;

    private readonly RaycastHit[] _movementHits = new RaycastHit[8];
    private readonly RaycastHit[] _cameraHits = new RaycastHit[8];
    private readonly List<Renderer> _playerRenderers = new(8);
    private readonly List<ShadowCastingMode> _defaultShadowModes = new(8);

    private float _pitch;
    private float _yaw;
    private float _verticalVelocity;
    private float _lastSpacePressedTime = -10f;
    private bool _cursorLocked;
    private bool _isGrounded;
    private CameraMode _currentCameraMode = CameraMode.FirstPerson;
    private MovementMode _currentMovementMode = MovementMode.Fly;

    public CameraMode CurrentCameraMode => _currentCameraMode;

    public MovementMode CurrentMovementMode => _currentMovementMode;

    public string CurrentCameraModeLabel => _currentCameraMode switch
    {
        CameraMode.FirstPerson => "First Person",
        CameraMode.ThirdPersonBack => "Third Person Back",
        CameraMode.ThirdPersonFront => "Third Person Front",
        _ => _currentCameraMode.ToString(),
    };

    public string CurrentMovementModeLabel => _currentMovementMode switch
    {
        MovementMode.Fly => "Fly",
        MovementMode.Ground => "Ground",
        _ => _currentMovementMode.ToString(),
    };

    private void Awake()
    {
        ResolveReferences();

        Vector3 angles = playerRoot != null ? playerRoot.eulerAngles : transform.eulerAngles;
        _pitch = Mathf.Clamp(NormalizeAngle(angles.x), CameraPitchMin, CameraPitchMax);
        _yaw = angles.y;

        CachePlayerRenderers();
        ApplyPlayerVisualMode();
        SetCursorLock(true);
        SyncPlayerLook();
        UpdateCameraTransform();
    }

    private void LateUpdate()
    {
        if (!HasRequiredReferences())
        {
            return;
        }

        UpdateCameraTransform();
    }

    private void Update()
    {
        ResolveReferences();
        if (!HasRequiredReferences())
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null)
        {
            return;
        }

        HandleCursorLock(keyboard, mouse);
        if (_cursorLocked)
        {
            UpdateLook(mouse);
        }

        if (keyboard.f5Key.wasPressedThisFrame)
        {
            CycleCameraMode();
        }

        bool modeToggledThisFrame = HandleMovementModeToggle(keyboard);
        UpdateMovement(keyboard, modeToggledThisFrame);
        SyncPlayerLook();
    }

    public Ray GetInteractionRay()
    {
        Vector3 origin = headTransform != null ? headTransform.position : transform.position;
        Quaternion aimRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        return new Ray(origin, aimRotation * Vector3.forward);
    }

    private void HandleCursorLock(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }
        else if (mouse.leftButton.wasPressedThisFrame && !_cursorLocked)
        {
            SetCursorLock(true);
        }
    }

    private void UpdateLook(Mouse mouse)
    {
        Vector2 mouseDelta = mouse.delta.ReadValue() * lookSensitivity;
        _yaw += mouseDelta.x;
        _pitch = Mathf.Clamp(_pitch - mouseDelta.y, CameraPitchMin, CameraPitchMax);
    }

    private bool HandleMovementModeToggle(Keyboard keyboard)
    {
        if (!keyboard.spaceKey.wasPressedThisFrame)
        {
            return false;
        }

        float now = Time.unscaledTime;
        if (now - _lastSpacePressedTime <= spaceDoubleTapWindow)
        {
            _currentMovementMode = _currentMovementMode == MovementMode.Fly ? MovementMode.Ground : MovementMode.Fly;
            _lastSpacePressedTime = -10f;

            if (_currentMovementMode == MovementMode.Fly)
            {
                _verticalVelocity = 0f;
            }

            return true;
        }

        _lastSpacePressedTime = now;
        return false;
    }

    private void UpdateMovement(Keyboard keyboard, bool modeToggledThisFrame)
    {
        float speed = _currentMovementMode == MovementMode.Fly ? flyMoveSpeed : groundMoveSpeed;
        if (keyboard.leftShiftKey.isPressed)
        {
            speed *= fastMoveMultiplier;
        }
        else if (keyboard.leftCtrlKey.isPressed)
        {
            speed *= slowMoveMultiplier;
        }

        Vector3 moveInput = ReadMoveInput(keyboard);
        Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;

        if (_currentMovementMode == MovementMode.Fly)
        {
            float verticalInput = 0f;
            if (keyboard.qKey.isPressed)
            {
                verticalInput -= 1f;
            }

            if (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed)
            {
                verticalInput += 1f;
            }

            Vector3 displacement =
                (((right * moveInput.x) + (forward * moveInput.z)) * speed * Time.unscaledDeltaTime) +
                ((Vector3.up * verticalInput) * speed * Time.unscaledDeltaTime);

            MovePlayer(displacement);
            _isGrounded = false;
            _verticalVelocity = 0f;
            return;
        }

        _isGrounded = CheckGrounded();
        if (_isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
        }

        if (!modeToggledThisFrame && keyboard.spaceKey.wasPressedThisFrame && _isGrounded)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * gravity * 2f);
            _isGrounded = false;
        }

        _verticalVelocity -= gravity * Time.unscaledDeltaTime;

        Vector3 horizontalDisplacement =
            ((right * moveInput.x) + (forward * moveInput.z)) *
            speed *
            Time.unscaledDeltaTime;
        Vector3 verticalDisplacement = Vector3.up * (_verticalVelocity * Time.unscaledDeltaTime);

        MovePlayer(horizontalDisplacement + verticalDisplacement);

        _isGrounded = CheckGrounded();
        if (_isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
        }
    }

    private void MovePlayer(Vector3 displacement)
    {
        if (playerRoot == null)
        {
            return;
        }

        Vector3 currentPosition = playerRoot.position;
        Vector3 remaining = displacement;

        for (int iteration = 0; iteration < 3; iteration++)
        {
            if (remaining.sqrMagnitude <= 0.000001f)
            {
                break;
            }

            Vector3 direction = remaining.normalized;
            float castDistance = remaining.magnitude;

            GetCapsule(currentPosition, out Vector3 top, out Vector3 bottom, out float radius);
            int hitCount = Physics.CapsuleCastNonAlloc(
                top,
                bottom,
                radius,
                direction,
                _movementHits,
                castDistance + collisionSkinWidth,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            if (!TryGetClosestValidHit(_movementHits, hitCount, out RaycastHit hit))
            {
                currentPosition += remaining;
                break;
            }

            float travelDistance = Mathf.Max(hit.distance - collisionSkinWidth, 0f);
            currentPosition += direction * travelDistance;

            if (Vector3.Dot(hit.normal, Vector3.up) > 0.55f && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else if (Vector3.Dot(hit.normal, Vector3.down) > 0.55f && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }

            remaining -= direction * travelDistance;
            remaining = Vector3.ProjectOnPlane(remaining, hit.normal);
        }

        playerRoot.position = currentPosition;
    }

    private bool CheckGrounded()
    {
        if (playerRoot == null || playerCollider == null)
        {
            return false;
        }

        GetCapsule(playerRoot.position, out Vector3 top, out Vector3 bottom, out float radius);
        int hitCount = Physics.CapsuleCastNonAlloc(
            top,
            bottom,
            radius,
            Vector3.down,
            _movementHits,
            groundCheckDistance + collisionSkinWidth,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        if (!TryGetClosestValidHit(_movementHits, hitCount, out RaycastHit hit))
        {
            return false;
        }

        return Vector3.Dot(hit.normal, Vector3.up) > 0.55f;
    }

    private void UpdateCameraTransform()
    {
        if (controlledCamera == null || headTransform == null)
        {
            return;
        }

        Quaternion aimRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = headTransform.position;

        if (_currentCameraMode == CameraMode.FirstPerson)
        {
            controlledCamera.transform.SetPositionAndRotation(pivot, aimRotation);
            return;
        }

        float distance = _currentCameraMode == CameraMode.ThirdPersonBack ? thirdPersonBackDistance : thirdPersonFrontDistance;
        Vector3 direction = aimRotation * Vector3.forward;
        Vector3 desiredPosition = _currentCameraMode == CameraMode.ThirdPersonBack
            ? pivot - (direction * distance)
            : pivot + (direction * distance);

        Vector3 finalPosition = ResolveCameraCollision(pivot, desiredPosition);
        Quaternion finalRotation = _currentCameraMode == CameraMode.ThirdPersonBack
            ? aimRotation
            : Quaternion.LookRotation((pivot - finalPosition).normalized, Vector3.up);

        controlledCamera.transform.SetPositionAndRotation(finalPosition, finalRotation);
    }

    private Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - pivot;
        float desiredDistance = direction.magnitude;
        if (desiredDistance <= 0.0001f)
        {
            return pivot;
        }

        direction /= desiredDistance;

        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            cameraCollisionRadius,
            direction,
            _cameraHits,
            desiredDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        if (!TryGetClosestValidHit(_cameraHits, hitCount, out RaycastHit hit))
        {
            return desiredPosition;
        }

        float safeDistance = Mathf.Max(hit.distance - collisionSkinWidth, 0.05f);
        return pivot + (direction * safeDistance);
    }

    private bool TryGetClosestValidHit(RaycastHit[] hits, int hitCount, out RaycastHit closestHit)
    {
        closestHit = default;
        float closestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (playerRoot != null && hitCollider.transform.IsChildOf(playerRoot))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private void SyncPlayerLook()
    {
        if (playerRoot == null)
        {
            return;
        }

        playerRoot.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (headTransform != null)
        {
            float headPitch = Mathf.Clamp(_pitch, HeadPitchMin, HeadPitchMax);
            headTransform.localRotation = Quaternion.Euler(headPitch, 0f, 0f);
        }

        ApplyPlayerVisualMode();
    }

    private void ApplyPlayerVisualMode()
    {
        if (_playerRenderers.Count == 0)
        {
            return;
        }

        bool firstPerson = _currentCameraMode == CameraMode.FirstPerson;
        for (int i = 0; i < _playerRenderers.Count; i++)
        {
            Renderer renderer = _playerRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : _defaultShadowModes[i];
            renderer.enabled = true;
        }
    }

    private void CachePlayerRenderers()
    {
        _playerRenderers.Clear();
        _defaultShadowModes.Clear();

        AddRenderersFromRoot(headTransform);
        AddRenderersFromRoot(bodyTransform);
    }

    private void AddRenderersFromRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || _playerRenderers.Contains(renderer))
            {
                continue;
            }

            _playerRenderers.Add(renderer);
            _defaultShadowModes.Add(renderer.shadowCastingMode);
        }
    }

    private void CycleCameraMode()
    {
        _currentCameraMode = _currentCameraMode switch
        {
            CameraMode.FirstPerson => CameraMode.ThirdPersonBack,
            CameraMode.ThirdPersonBack => CameraMode.ThirdPersonFront,
            _ => CameraMode.FirstPerson,
        };

        ApplyPlayerVisualMode();
    }

    private void ResolveReferences()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        if (controlledCamera == null)
        {
            controlledCamera = Camera.main;
        }

        if (playerRoot == null)
        {
            if (GetComponent<CapsuleCollider>() != null)
            {
                playerRoot = transform;
            }
        }

        if (playerRoot == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
            {
                playerRoot = playerObject.transform;
            }
        }

        if (headTransform == null && playerRoot != null)
        {
            Transform head = playerRoot.Find("Head");
            if (head != null)
            {
                headTransform = head;
            }
        }

        if (bodyTransform == null && playerRoot != null)
        {
            Transform body = playerRoot.Find("Body");
            if (body != null)
            {
                bodyTransform = body;
            }
        }

        if (playerCollider == null && playerRoot != null)
        {
            playerCollider = playerRoot.GetComponent<CapsuleCollider>();
        }
    }

    private bool HasRequiredReferences()
    {
        return controlledCamera != null && playerRoot != null && headTransform != null && playerCollider != null;
    }

    private void GetCapsule(Vector3 playerPosition, out Vector3 top, out Vector3 bottom, out float radius)
    {
        Vector3 lossyScale = playerRoot.lossyScale;
        float radiusScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
        float heightScale = Mathf.Abs(lossyScale.y);

        radius = playerCollider.radius * radiusScale;
        float height = Mathf.Max(playerCollider.height * heightScale, radius * 2f);
        Vector3 center = playerPosition + playerCollider.center;
        float halfSegment = Mathf.Max((height * 0.5f) - radius, 0f);

        top = center + (Vector3.up * halfSegment);
        bottom = center - (Vector3.up * halfSegment);
    }

    private static Vector3 ReadMoveInput(Keyboard keyboard)
    {
        float x = 0f;
        float z = 0f;

        if (keyboard.aKey.isPressed)
        {
            x -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            x += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            z -= 1f;
        }

        if (keyboard.wKey.isPressed)
        {
            z += 1f;
        }

        return new Vector3(x, 0f, z).normalized;
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
