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
    public const int VanillaMinY = -64;
    public const int VanillaMaxYExclusive = 320;
    public const int VanillaSeaLevel = 63;
    public const int InternalSeaLevel = VanillaSeaLevel - VanillaMinY;
    private const float TerrainBias = -0.703125f;
    private const float TopSlideStartY = 240f;
    private const float TopSlideEndY = 256f;
    private const float TopSlideBase = -0.078125f;
    private const float BottomSlideStartY = -64f;
    private const float BottomSlideEndY = -40f;
    private const float BottomSlideBase = 0.1171875f;
    private const float DensityScale = 0.64f;

    public static float EvaluateDensity(
        int worldY,
        int worldHeight,
        in TerrainShapeSample shape,
        float jaggedNoise,
        float base3DNoise)
    {
        float slopedCheese = EvaluateSlopedCheese(worldY, worldHeight, shape, jaggedNoise, base3DNoise);
        return ApplyCavelessOverworldShaping(worldY, worldHeight, slopedCheese);
    }

    public static int FindSurfaceHeight(
        int worldX,
        int worldZ,
        int worldHeight,
        in TerrainShapeSample shape,
        float jaggedNoise,
        in VanillaBlendedNoiseManaged blendedNoise)
    {
        for (int worldY = worldHeight - 1; worldY >= 0; worldY--)
        {
            float base3DNoise = VanillaNoise.SampleOverworldBlendedNoise(
                worldX,
                ToVanillaY(worldY, worldHeight),
                worldZ,
                blendedNoise);
            if (EvaluateDensity(worldY, worldHeight, shape, jaggedNoise, base3DNoise) > 0f)
            {
                return worldY;
            }
        }

        return -1;
    }

    private static float EvaluateDepth(int worldY, int worldHeight, float offset)
    {
        float vanillaY = ToVanillaY(worldY, worldHeight);
        float gradient = (vanillaY - VanillaMinY) / (VanillaMaxYExclusive - VanillaMinY);
        gradient = math.lerp(1.5f, -1.5f, gradient);
        return gradient + offset;
    }

    private static float EvaluateSlopedCheese(
        int worldY,
        int worldHeight,
        in TerrainShapeSample shape,
        float jaggedNoise,
        float base3DNoise)
    {
        float depth = EvaluateDepth(worldY, worldHeight, shape.offset);
        float jaggedContribution = shape.jaggedness * HalfNegative(jaggedNoise);
        float sloped = (depth + jaggedContribution) * shape.factor;
        return (4f * QuarterNegative(sloped)) + base3DNoise;
    }

    private static float ApplyCavelessOverworldShaping(int worldY, int worldHeight, float slopedCheese)
    {
        float vanillaY = ToVanillaY(worldY, worldHeight);
        float terrain = math.clamp(TerrainBias + slopedCheese, -64f, 64f);

        float topSlide = ClampedMap(vanillaY, TopSlideStartY, TopSlideEndY, 1f, 0f);
        terrain = TopSlideBase + topSlide * (-TopSlideBase + terrain);

        float bottomSlide = ClampedMap(vanillaY, BottomSlideStartY, BottomSlideEndY, 0f, 1f);
        terrain = BottomSlideBase + bottomSlide * (-BottomSlideBase + terrain);

        return Squeeze(DensityScale * terrain);
    }

    public static float ToVanillaY(int worldY, int worldHeight)
    {
        if (worldHeight <= 1)
        {
            return VanillaMinY;
        }

        float t = worldY / (float)(worldHeight - 1);
        return math.lerp(VanillaMinY, VanillaMaxYExclusive, t);
    }

    public static int ToInternalY(int vanillaY)
    {
        return vanillaY - VanillaMinY;
    }

    private static float HalfNegative(float value)
    {
        return value < 0f ? value * 0.5f : value;
    }

    private static float QuarterNegative(float value)
    {
        return value < 0f ? value * 0.25f : value;
    }

    private static float ClampedMap(float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        if (math.abs(fromMax - fromMin) < 0.000001f)
        {
            return toMin;
        }

        float t = math.saturate((value - fromMin) / (fromMax - fromMin));
        return math.lerp(toMin, toMax, t);
    }

    private static float Squeeze(float value)
    {
        float clamped = math.clamp(value, -1f, 1f);
        return (clamped * 0.5f) - ((clamped * clamped * clamped) / 24f);
    }
}
