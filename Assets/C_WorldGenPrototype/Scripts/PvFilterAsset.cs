using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PvFilter",
    menuName = "World/WorldGen/Pv Filter")]
public sealed class PvFilterAsset : ScriptableObject
{
    [System.Serializable]
    public struct FixedPoint
    {
        [Range(-1f, 1f)] public float input;
        [Range(-1f, 1f)] public float output;

        public FixedPoint(float input, float output)
        {
            this.input = input;
            this.output = output;
        }
    }

    [SerializeField] private AnimationCurve curve = new(
        new Keyframe(-1f, -1f),
        new Keyframe(0f, 0f),
        new Keyframe(1f, 1f));
    [SerializeField] private List<FixedPoint> fixedPoints = new()
    {
        new FixedPoint(-1f, -1f),
        new FixedPoint(0f, 0f),
        new FixedPoint(1f, 1f),
    };
    [SerializeField, Min(16)] private int bakeResolution = 256;
    [SerializeField, HideInInspector] private float[] bakedLut = new float[0];
    [SerializeField, HideInInspector] private int bakeVersion;
    [SerializeField, HideInInspector] private string bakedSummary = "Not baked";

    [System.NonSerialized] private bool isValidating;

    public AnimationCurve Curve => curve;
    public IReadOnlyList<FixedPoint> FixedPoints => fixedPoints;
    public int BakeResolution => Mathf.Max(16, bakeResolution);
    public float[] BakedLut => bakedLut;
    public int BakeVersion => bakeVersion;
    public string BakedSummary => bakedSummary;
    public bool HasBakedLut => bakedLut != null && bakedLut.Length > 1;

    public float Evaluate(float pv)
    {
        return Mathf.Clamp(curve.Evaluate(pv), -1f, 1f);
    }

    public void Bake()
    {
        SyncFixedPointsToCurve();

        int resolution = BakeResolution;
        float[] lut = new float[resolution];
        float minValue = float.MaxValue;
        float maxValue = float.MinValue;
        float sum = 0f;

        for (int i = 0; i < resolution; i++)
        {
            float input = Mathf.Lerp(-1f, 1f, i / (float)(resolution - 1));
            float value = Evaluate(input);
            lut[i] = value;
            minValue = Mathf.Min(minValue, value);
            maxValue = Mathf.Max(maxValue, value);
            sum += value;
        }

        bakedLut = lut;
        bakeVersion++;
        bakedSummary = $"Samples {resolution}, Output Min/Avg/Max {minValue:F4} / {sum / resolution:F4} / {maxValue:F4}";
    }

    public void SyncFixedPointsToCurve()
    {
        EnsureCurve();
        NormalizeFixedPoints();

        List<Keyframe> keys = new(curve.keys);
        for (int i = 0; i < fixedPoints.Count; i++)
        {
            FixedPoint fixedPoint = fixedPoints[i];
            Keyframe lockedKey = new(fixedPoint.input, fixedPoint.output);
            int existingIndex = FindKeyIndex(keys, fixedPoint.input);
            if (existingIndex >= 0)
            {
                keys[existingIndex] = lockedKey;
            }
            else
            {
                keys.Add(lockedKey);
            }
        }

        keys.Sort((a, b) => a.time.CompareTo(b.time));
        curve.keys = keys.ToArray();
    }

    private void OnValidate()
    {
        if (isValidating)
        {
            return;
        }

        isValidating = true;
        try
        {
            SyncFixedPointsToCurve();
        }
        finally
        {
            isValidating = false;
        }
    }

    private void EnsureCurve()
    {
        curve ??= new AnimationCurve(
            new Keyframe(-1f, -1f),
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));
    }

    private void NormalizeFixedPoints()
    {
        fixedPoints ??= new List<FixedPoint>();
        for (int i = 0; i < fixedPoints.Count; i++)
        {
            FixedPoint fixedPoint = fixedPoints[i];
            fixedPoint.input = Mathf.Clamp(fixedPoint.input, -1f, 1f);
            fixedPoint.output = Mathf.Clamp(fixedPoint.output, -1f, 1f);
            fixedPoints[i] = fixedPoint;
        }

        fixedPoints.Sort((a, b) => a.input.CompareTo(b.input));
    }

    private static int FindKeyIndex(List<Keyframe> keys, float input)
    {
        const float epsilon = 0.0001f;
        const float nearestMatchRange = 0.25f;
        int nearestIndex = -1;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < keys.Count; i++)
        {
            float distance = Mathf.Abs(keys[i].time - input);
            if (distance <= epsilon)
            {
                return i;
            }

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        if (nearestDistance <= nearestMatchRange)
        {
            return nearestIndex;
        }

        return -1;
    }
}
