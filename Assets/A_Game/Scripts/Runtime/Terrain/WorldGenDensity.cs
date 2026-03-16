using Unity.Mathematics;

public readonly struct TerrainShapeSample
{
    public readonly float offset;
    public readonly float factor;
    public readonly float jaggedness;

    public TerrainShapeSample(float offset, float factor, float jaggedness)
    {
        this.offset = offset;
        this.factor = factor;
        this.jaggedness = jaggedness;
    }
}

public static class WorldGenDensity
{
    public static float EvaluateDensity(int worldY, int worldHeight, in TerrainShapeSample shape, float foldedWeirdness)
    {
        float depth = EvaluateDepth(worldY, worldHeight, shape.offset);
        float jaggedContribution = shape.jaggedness * HalfNegative(foldedWeirdness);
        float sloped = (depth + jaggedContribution) * shape.factor;
        return 4f * QuarterNegative(sloped);
    }

    public static int FindSurfaceHeight(int worldHeight, in TerrainShapeSample shape, float foldedWeirdness)
    {
        for (int worldY = worldHeight - 1; worldY >= 0; worldY--)
        {
            if (EvaluateDensity(worldY, worldHeight, shape, foldedWeirdness) > 0f)
            {
                return worldY;
            }
        }

        return -1;
    }

    private static float EvaluateDepth(int worldY, int worldHeight, float offset)
    {
        float t = worldHeight <= 1 ? 0f : worldY / (float)(worldHeight - 1);
        float gradient = math.lerp(1.5f, -1.5f, t);
        return gradient + offset;
    }

    private static float HalfNegative(float value)
    {
        return value < 0f ? value * 0.5f : value;
    }

    private static float QuarterNegative(float value)
    {
        return value < 0f ? value * 0.25f : value;
    }
}
