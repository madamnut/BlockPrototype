using UnityEngine;

public static class PeaksAndValleys
{
    public static float Fold(float weirdness)
    {
        return 1f - Mathf.Abs((3f * Mathf.Abs(weirdness)) - 2f);
    }
}
