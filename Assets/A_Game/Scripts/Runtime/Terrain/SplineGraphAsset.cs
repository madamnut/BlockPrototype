using UnityEngine;

[CreateAssetMenu(fileName = "SplineGraph", menuName = "World/Gen/Spline Graph")]
public sealed class SplineGraphAsset : ScriptableObject
{
    public GndSplinePoint[] points = LiftRuntimeSettings.CreateDefaultLowSpline();
    [Min(2)] public int lutResolution = 256;

    [SerializeField] private int bakedPointsHash;
    [SerializeField] private float[] bakedHeights;

    public GndSplinePoint[] GetPointsOrDefault()
    {
        return points != null && points.Length >= 2
            ? points
            : LiftRuntimeSettings.CreateDefaultLowSpline();
    }

    public bool HasBakedHeightLut => bakedHeights != null && bakedHeights.Length >= 2;

    public bool HasFreshBakedHeightLut
    {
        get
        {
            return HasBakedHeightLut &&
                   bakedHeights.Length == Mathf.Max(2, lutResolution) &&
                   bakedPointsHash == GndSplineUtility.ComputeHash(GetPointsOrDefault());
        }
    }

    public float[] GetBakedHeightLutOrNull()
    {
        return HasFreshBakedHeightLut ? bakedHeights : null;
    }

    public float MinHeight
    {
        get { return GndSplineUtility.GetMinHeight(GetPointsOrDefault()); }
    }

    public float MaxHeight
    {
        get { return GndSplineUtility.GetMaxHeight(GetPointsOrDefault()); }
    }
    public void BakeHeightLut()
    {
        int resolution = Mathf.Max(2, lutResolution);
        bakedHeights = GndSplineUtility.BakeLut(GetPointsOrDefault(), resolution);
        bakedPointsHash = GndSplineUtility.ComputeHash(GetPointsOrDefault());
    }

    public void ClearBakedHeightLut()
    {
        bakedHeights = null;
        bakedPointsHash = 0;
    }

    private void OnValidate()
    {
        if (points == null || points.Length < 2)
        {
            points = LiftRuntimeSettings.CreateDefaultLowSpline();
        }

        lutResolution = Mathf.Max(2, lutResolution);
    }
}
