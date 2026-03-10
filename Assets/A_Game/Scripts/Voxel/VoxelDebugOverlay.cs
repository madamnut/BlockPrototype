using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.UI;

public sealed class VoxelDebugOverlay : MonoBehaviour
{
    [SerializeField] private float refreshInterval = 0.25f;

    private readonly StringBuilder _leftBuilder = new(64);
    private readonly StringBuilder _rightBuilder = new(192);

    private GameObject _overlayRoot;
    private Text _leftText;
    private Text _rightText;
    private float _refreshTimer;
    private float _smoothedFps;
    private bool _visible = true;

    private void Awake()
    {
        CreateOverlay();
        RefreshTexts();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
        {
            _visible = !_visible;
            _overlayRoot.SetActive(_visible);
        }

        float unscaledDeltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float currentFps = 1f / unscaledDeltaTime;
        _smoothedFps = Mathf.Lerp(_smoothedFps <= 0f ? currentFps : _smoothedFps, currentFps, 0.12f);

        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f)
        {
            return;
        }

        _refreshTimer = refreshInterval;
        RefreshTexts();
    }

    private void CreateOverlay()
    {
        Font debugFont = LoadDebugFont();

        _overlayRoot = new GameObject("Debug Overlay");
        _overlayRoot.transform.SetParent(transform, false);

        Canvas canvas = _overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = _overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _overlayRoot.AddComponent<GraphicRaycaster>();

        _leftText = CreateText("Left Debug Text", debugFont, TextAnchor.UpperLeft);
        _leftText.rectTransform.anchorMin = new Vector2(0f, 1f);
        _leftText.rectTransform.anchorMax = new Vector2(0f, 1f);
        _leftText.rectTransform.pivot = new Vector2(0f, 1f);
        _leftText.rectTransform.anchoredPosition = new Vector2(16f, -16f);

        _rightText = CreateText("Right Debug Text", debugFont, TextAnchor.UpperRight);
        _rightText.rectTransform.anchorMin = new Vector2(1f, 1f);
        _rightText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _rightText.rectTransform.pivot = new Vector2(1f, 1f);
        _rightText.rectTransform.anchoredPosition = new Vector2(-16f, -16f);
    }

    private Text CreateText(string objectName, Font font, TextAnchor alignment)
    {
        GameObject textObject = new(objectName);
        textObject.transform.SetParent(_overlayRoot.transform, false);

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 22;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        RectTransform rectTransform = text.rectTransform;
        rectTransform.sizeDelta = new Vector2(700f, 240f);

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);

        return text;
    }

    private void RefreshTexts()
    {
        _leftBuilder.Clear();
        _leftBuilder.Append("FPS: ");
        _leftBuilder.Append(Mathf.RoundToInt(_smoothedFps));
        _leftText.text = _leftBuilder.ToString();

        long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
        long totalReserved = Profiler.GetTotalReservedMemoryLong();
        long monoUsed = Profiler.GetMonoUsedSizeLong();
        long monoHeap = Profiler.GetMonoHeapSizeLong();
        long gcMemory = System.GC.GetTotalMemory(false);

        _rightBuilder.Clear();
        _rightBuilder.Append("Allocated: ");
        _rightBuilder.Append(FormatMegabytes(totalAllocated));
        _rightBuilder.AppendLine();
        _rightBuilder.Append("Reserved: ");
        _rightBuilder.Append(FormatMegabytes(totalReserved));
        _rightBuilder.AppendLine();
        _rightBuilder.Append("Mono Used: ");
        _rightBuilder.Append(FormatMegabytes(monoUsed));
        _rightBuilder.AppendLine();
        _rightBuilder.Append("Mono Heap: ");
        _rightBuilder.Append(FormatMegabytes(monoHeap));
        _rightBuilder.AppendLine();
        _rightBuilder.Append("GC Memory: ");
        _rightBuilder.Append(FormatMegabytes(gcMemory));

        _rightText.text = _rightBuilder.ToString();
    }

    private static string FormatMegabytes(long bytes)
    {
        float megabytes = bytes / (1024f * 1024f);
        return megabytes.ToString("F1") + " MB";
    }

    private static Font LoadDebugFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }
}
