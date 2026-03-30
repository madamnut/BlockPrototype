using System;
using System.Diagnostics;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

public sealed class WorldDebugOverlay : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private WorldRuntime worldRuntime;
    [SerializeField] private FlyCamera playerController;
    [SerializeField] private TMP_Text upperLeftText;
    [SerializeField] private TMP_Text upperRightText;
    [SerializeField] private TMP_Text lowerLeftText;
    [SerializeField] private TMP_Text lowerRightText;

    [Header("Timing")]
    [SerializeField] private float memoryRefreshInterval = 0.25f;

    private readonly StringBuilder _upperLeftBuilder = new(128);
    private readonly StringBuilder _upperRightBuilder = new(192);
    private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

    private float _memoryRefreshTimer;
    private float _smoothedFps;
    private bool _topTextsVisible = true;
    private double _lastCpuSampleTime;
    private double _lastCpuTotalProcessorTimeMs;

    private void Awake()
    {
        InitializePerformanceSampling();
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

    public void BindWorldRuntime(WorldRuntime runtime)
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
            int wrappedX = TerrainData.WrapWorldCoord(position.x);
            int wrappedZ = TerrainData.WrapWorldCoord(position.z);
            ushort contentId = worldRuntime.SelectedContentId;

            _upperLeftBuilder.Append("Target: ");
            _upperLeftBuilder.Append(contentId);
            _upperLeftBuilder.Append('(');
            _upperLeftBuilder.Append(worldRuntime.SelectedContentName);
            _upperLeftBuilder.Append(") [");
            _upperLeftBuilder.Append(wrappedX);
            _upperLeftBuilder.Append(',');
            _upperLeftBuilder.Append(position.y);
            _upperLeftBuilder.Append(',');
            _upperLeftBuilder.Append(wrappedZ);
            _upperLeftBuilder.Append(']');
        }

        if (playerController != null)
        {
            Vector3 playerPosition = playerController.transform.position;
            float wrappedPlayerX = TerrainData.WrapWorldCoord(playerPosition.x);
            float wrappedPlayerZ = TerrainData.WrapWorldCoord(playerPosition.z);
            _upperLeftBuilder.AppendLine();
            _upperLeftBuilder.Append("Position: ");
            _upperLeftBuilder.Append(wrappedPlayerX.ToString("F1"));
            _upperLeftBuilder.Append(',');
            _upperLeftBuilder.Append(playerPosition.y.ToString("F1"));
            _upperLeftBuilder.Append(',');
            _upperLeftBuilder.Append(wrappedPlayerZ.ToString("F1"));
            _upperLeftBuilder.Append(" (facing: ");
            _upperLeftBuilder.Append(GetFacingLabel(playerController.GetInteractionRay().direction));
            _upperLeftBuilder.Append(')');

            if (worldRuntime != null)
            {
                int worldX = TerrainData.WrapWorldCoord(Mathf.FloorToInt(playerPosition.x));
                int worldZ = TerrainData.WrapWorldCoord(Mathf.FloorToInt(playerPosition.z));
                if (worldRuntime.TryGetContinentalnessAt(worldX, worldZ, out float continentalness))
                {
                    _upperLeftBuilder.AppendLine();
                    _upperLeftBuilder.Append("Cont: ");
                    _upperLeftBuilder.Append(continentalness.ToString("F3"));
                }

                if (worldRuntime.TryGetErosionAt(worldX, worldZ, out float erosion))
                {
                    _upperLeftBuilder.AppendLine();
                    _upperLeftBuilder.Append("Eros: ");
                    _upperLeftBuilder.Append(erosion.ToString("F3"));
                }

                if (worldRuntime.TryGetWeirdnessAt(worldX, worldZ, out float weirdness))
                {
                    _upperLeftBuilder.AppendLine();
                    _upperLeftBuilder.Append("Weird: ");
                    _upperLeftBuilder.Append(weirdness.ToString("F3"));
                }

                if (worldRuntime.TryGetPeaksAndValleysAt(worldX, worldZ, out float peaksAndValleys))
                {
                    _upperLeftBuilder.AppendLine();
                    _upperLeftBuilder.Append("PV: ");
                    _upperLeftBuilder.Append(peaksAndValleys.ToString("F3"));
                }
            }
        }

        upperLeftText.text = _upperLeftBuilder.ToString();
    }

    private void RefreshUpperRightText()
    {
        if (upperRightText == null)
        {
            return;
        }

        float cpuUsagePercent = SampleCpuUsagePercent();
        bool hasGpuFrameTime = TryGetGpuFrameTime(out float gpuFrameTimeMs);
        long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
        long totalReserved = Profiler.GetTotalReservedMemoryLong();
        long monoUsed = Profiler.GetMonoUsedSizeLong();
        long monoHeap = Profiler.GetMonoHeapSizeLong();
        long gcMemory = System.GC.GetTotalMemory(false);

        _upperRightBuilder.Clear();
        _upperRightBuilder.Append("CPU Usage: ");
        _upperRightBuilder.Append(cpuUsagePercent.ToString("F1"));
        _upperRightBuilder.Append('%');
        _upperRightBuilder.AppendLine();
        _upperRightBuilder.Append("GPU Frame: ");
        _upperRightBuilder.Append(hasGpuFrameTime ? gpuFrameTimeMs.ToString("F2") + " ms" : "N/A");
        _upperRightBuilder.AppendLine();
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

    private void InitializePerformanceSampling()
    {
        using Process process = Process.GetCurrentProcess();
        _lastCpuSampleTime = Time.realtimeSinceStartupAsDouble;
        _lastCpuTotalProcessorTimeMs = process.TotalProcessorTime.TotalMilliseconds;
    }

    private float SampleCpuUsagePercent()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        using Process process = Process.GetCurrentProcess();
        double currentCpuTotalProcessorTimeMs = process.TotalProcessorTime.TotalMilliseconds;
        double elapsedMs = Math.Max(1.0, (now - _lastCpuSampleTime) * 1000.0);
        double cpuTimeDeltaMs = Math.Max(0.0, currentCpuTotalProcessorTimeMs - _lastCpuTotalProcessorTimeMs);

        _lastCpuSampleTime = now;
        _lastCpuTotalProcessorTimeMs = currentCpuTotalProcessorTimeMs;

        return Mathf.Clamp((float)((cpuTimeDeltaMs / (elapsedMs * Environment.ProcessorCount)) * 100.0), 0f, 100f);
    }

    private bool TryGetGpuFrameTime(out float gpuFrameTimeMs)
    {
        gpuFrameTimeMs = 0f;
        FrameTimingManager.CaptureFrameTimings();
        uint timingCount = FrameTimingManager.GetLatestTimings(1, _frameTimings);
        if (timingCount == 0)
        {
            return false;
        }

        gpuFrameTimeMs = (float)_frameTimings[0].gpuFrameTime;
        return gpuFrameTimeMs > 0f;
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
            "F3 + B: Player bounds\n" +
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

        worldRuntime = GetComponent<WorldRuntime>();
        if (worldRuntime != null)
        {
            return;
        }

        worldRuntime = FindAnyObjectByType<WorldRuntime>();
    }

    private void ResolvePlayerController()
    {
        if (playerController != null)
        {
            return;
        }

        playerController = FindAnyObjectByType<FlyCamera>();
    }

    private static string FormatMegabytes(long bytes)
    {
        float megabytes = bytes / (1024f * 1024f);
        return megabytes.ToString("F1") + " MB";
    }

    private static string GetFacingLabel(Vector3 direction)
    {
        Vector3 flatDirection = new(direction.x, 0f, direction.z);
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return "North";
        }

        flatDirection.Normalize();
        if (Mathf.Abs(flatDirection.x) > Mathf.Abs(flatDirection.z))
        {
            return flatDirection.x >= 0f ? "East" : "West";
        }

        return flatDirection.z >= 0f ? "North" : "South";
    }
}
