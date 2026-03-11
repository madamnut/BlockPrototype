using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

public sealed class VoxelDebugOverlay : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private VoxelWorldRuntime worldRuntime;
    [SerializeField] private VoxelFlyCamera playerController;
    [SerializeField] private TMP_Text upperLeftText;
    [SerializeField] private TMP_Text upperRightText;
    [SerializeField] private TMP_Text lowerLeftText;
    [SerializeField] private TMP_Text lowerRightText;

    [Header("Timing")]
    [SerializeField] private float memoryRefreshInterval = 0.25f;

    private readonly StringBuilder _upperLeftBuilder = new(128);
    private readonly StringBuilder _upperRightBuilder = new(192);

    private float _memoryRefreshTimer;
    private float _smoothedFps;
    private bool _topTextsVisible = true;

    private void Awake()
    {
        ResolveWorldRuntime();
        ResolvePlayerController();
        RefreshKeyHelpText();
        RefreshUpperRightText();
        ApplyTopTextVisibility();
    }

    private void Update()
    {
        HandleVisibilityToggle();

        float unscaledDeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float currentFps = 1f / unscaledDeltaTime;
        _smoothedFps = Mathf.Lerp(_smoothedFps <= 0f ? currentFps : _smoothedFps, currentFps, 0.12f);

        ResolveWorldRuntime();
        ResolvePlayerController();
        RefreshUpperLeftText();

        _memoryRefreshTimer -= Time.unscaledDeltaTime;
        if (_memoryRefreshTimer <= 0f)
        {
            _memoryRefreshTimer = memoryRefreshInterval;
            RefreshUpperRightText();
        }
    }

    public void BindWorldRuntime(VoxelWorldRuntime runtime)
    {
        worldRuntime = runtime;
    }

    private void HandleVisibilityToggle()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f3Key.wasPressedThisFrame)
        {
            return;
        }

        _topTextsVisible = !_topTextsVisible;
        ApplyTopTextVisibility();
    }

    private void ApplyTopTextVisibility()
    {
        if (upperLeftText != null)
        {
            upperLeftText.gameObject.SetActive(_topTextsVisible);
        }

        if (upperRightText != null)
        {
            upperRightText.gameObject.SetActive(_topTextsVisible);
        }
    }

    private void RefreshUpperLeftText()
    {
        if (upperLeftText == null)
        {
            return;
        }

        _upperLeftBuilder.Clear();
        if (playerController != null)
        {
            _upperLeftBuilder.Append("Camera: ");
            _upperLeftBuilder.Append(playerController.CurrentCameraModeLabel);
            _upperLeftBuilder.AppendLine();
            _upperLeftBuilder.Append("Move: ");
            _upperLeftBuilder.Append(playerController.CurrentMovementModeLabel);
            _upperLeftBuilder.AppendLine();
        }

        _upperLeftBuilder.Append("FPS: ");
        _upperLeftBuilder.Append(Mathf.RoundToInt(_smoothedFps));
        _upperLeftBuilder.AppendLine();

        if (worldRuntime == null || !worldRuntime.HasSelection)
        {
            _upperLeftBuilder.Append("Target: None");
        }
        else
        {
            Vector3Int position = worldRuntime.SelectedBlockPosition;
            ushort contentId = worldRuntime.SelectedContentId;

            _upperLeftBuilder.Append("Target Kind: ");
            _upperLeftBuilder.Append(worldRuntime.SelectedContentKindLabel);
            _upperLeftBuilder.AppendLine();
            _upperLeftBuilder.Append("Target ID: ");
            _upperLeftBuilder.Append(contentId);
            _upperLeftBuilder.AppendLine();
            _upperLeftBuilder.Append("Target Name: ");
            _upperLeftBuilder.Append(worldRuntime.SelectedContentName);
            _upperLeftBuilder.AppendLine();
            _upperLeftBuilder.Append("Target Pos: ");
            _upperLeftBuilder.Append(position.x);
            _upperLeftBuilder.Append(", ");
            _upperLeftBuilder.Append(position.y);
            _upperLeftBuilder.Append(", ");
            _upperLeftBuilder.Append(position.z);
        }

        upperLeftText.text = _upperLeftBuilder.ToString();
    }

    private void RefreshUpperRightText()
    {
        if (upperRightText == null)
        {
            return;
        }

        long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
        long totalReserved = Profiler.GetTotalReservedMemoryLong();
        long monoUsed = Profiler.GetMonoUsedSizeLong();
        long monoHeap = Profiler.GetMonoHeapSizeLong();
        long gcMemory = System.GC.GetTotalMemory(false);

        _upperRightBuilder.Clear();
        _upperRightBuilder.Append("Allocated: ");
        _upperRightBuilder.Append(FormatMegabytes(totalAllocated));
        _upperRightBuilder.AppendLine();
        _upperRightBuilder.Append("Reserved: ");
        _upperRightBuilder.Append(FormatMegabytes(totalReserved));
        _upperRightBuilder.AppendLine();
        _upperRightBuilder.Append("Mono Used: ");
        _upperRightBuilder.Append(FormatMegabytes(monoUsed));
        _upperRightBuilder.AppendLine();
        _upperRightBuilder.Append("Mono Heap: ");
        _upperRightBuilder.Append(FormatMegabytes(monoHeap));
        _upperRightBuilder.AppendLine();
        _upperRightBuilder.Append("GC Memory: ");
        _upperRightBuilder.Append(FormatMegabytes(gcMemory));

        upperRightText.text = _upperRightBuilder.ToString();
    }

    private void RefreshKeyHelpText()
    {
        if (lowerLeftText == null)
        {
            return;
        }

        lowerLeftText.text =
            "F3: Toggle top debug\n" +
            "F3 + G: Chunk bounds\n" +
            "F5: Cycle camera mode\n" +
            "Space x2: Toggle fly/ground\n" +
            "LMB: Break block\n" +
            "RMB: Place block\n" +
            "Q / E: Fly down / up\n" +
            "1: Select Dirt\n" +
            "2: Select Rock";
    }

    private void ResolveWorldRuntime()
    {
        if (worldRuntime != null)
        {
            return;
        }

        worldRuntime = GetComponent<VoxelWorldRuntime>();
        if (worldRuntime != null)
        {
            return;
        }

        worldRuntime = FindAnyObjectByType<VoxelWorldRuntime>();
    }

    private void ResolvePlayerController()
    {
        if (playerController != null)
        {
            return;
        }

        playerController = FindAnyObjectByType<VoxelFlyCamera>();
    }

    private static string FormatMegabytes(long bytes)
    {
        float megabytes = bytes / (1024f * 1024f);
        return megabytes.ToString("F1") + " MB";
    }
}
