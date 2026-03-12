using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BiomeGraph", menuName = "C_WorldGenTest/Biome Graph")]
public sealed class BiomeGraph : ScriptableObject
{
    [SerializeField] private string xAxisLabel = "Temperature";
    [SerializeField] private string yAxisLabel = "Humidity";
    [SerializeField] private Color graphBackgroundColor = new(0.96f, 0.96f, 0.96f, 1f);
    [SerializeField] private BiomeGraphEntry[] entries = Array.Empty<BiomeGraphEntry>();

    public string XAxisLabel => xAxisLabel;
    public string YAxisLabel => yAxisLabel;
    public Color GraphBackgroundColor => graphBackgroundColor;
    public BiomeGraphEntry[] Entries => entries;

    public bool TryGetBiome(float temperature, float humidity, out BiomeGraphEntry entry)
    {
        if (entries == null || entries.Length == 0)
        {
            entry = default;
            return false;
        }

        float clampedTemperature = Mathf.Clamp01(temperature);
        float clampedHumidity = Mathf.Clamp01(humidity);
        Vector2 sample = new(clampedTemperature, clampedHumidity);
        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        for (int index = 0; index < entries.Length; index++)
        {
            Vector2 point = ClampPoint(entries[index].ClimatePoint);
            float distance = (sample - point).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        if (bestIndex >= 0)
        {
            entry = entries[bestIndex];
            return true;
        }

        entry = default;
        return false;
    }

    public static Vector2 ClampPoint(Vector2 point)
    {
        return new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
    }
}
