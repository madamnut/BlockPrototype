using UnityEngine;

public sealed class ChunkGenerator
{
    // Vanilla overworld/offset wraps the imported spline with this bias when blend_alpha=1
    // (which is the normal case outside old chunk blending).
    private const float VanillaOffsetBias = -0.5037500262260437f;
    private const int CellWidth = 4;
    private const int CellHeight = 8;
    private const int CellCountXZ = TerrainData.ChunkSize / CellWidth;
    private const int CellCountY = TerrainData.WorldHeight / CellHeight;
    private const int CornerCountXZ = CellCountXZ + 1;
    private const int CornerCountY = CellCountY + 1;

    private readonly int _seaLevel;
    private readonly ContinentalnessSampler _continentalnessSampler;
    private readonly ErosionSampler _erosionSampler;
    private readonly WeirdnessSampler _weirdnessSampler;
    private readonly VanillaJaggedNoise _jaggedNoise;
    private readonly VanillaBlendedTerrainNoise _terrainNoise;
    private readonly JsonSplineMapper _offsetMapper;
    private readonly JsonSplineMapper _factorMapper;
    private readonly JsonSplineMapper _jaggednessMapper;

    public ChunkGenerator(int seed, int seaLevel, ContinentalnessSampler continentalnessSampler, ErosionSampler erosionSampler, WeirdnessSampler weirdnessSampler, JsonSplineMapper offsetMapper, JsonSplineMapper factorMapper, JsonSplineMapper jaggednessMapper)
    {
        _seaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
        _continentalnessSampler = continentalnessSampler ?? throw new System.ArgumentNullException(nameof(continentalnessSampler));
        _erosionSampler = erosionSampler ?? throw new System.ArgumentNullException(nameof(erosionSampler));
        _weirdnessSampler = weirdnessSampler ?? throw new System.ArgumentNullException(nameof(weirdnessSampler));
        _jaggedNoise = new VanillaJaggedNoise(seed);
        _terrainNoise = new VanillaBlendedTerrainNoise(seed);
        _offsetMapper = offsetMapper ?? throw new System.ArgumentNullException(nameof(offsetMapper));
        _factorMapper = factorMapper ?? throw new System.ArgumentNullException(nameof(factorMapper));
        _jaggednessMapper = jaggednessMapper ?? throw new System.ArgumentNullException(nameof(jaggednessMapper));
    }

    public int GenerateChunkColumn(int chunkX, int chunkZ, BlockType[] blocks, VoxelFluid[] fluids, int[] columnHeights)
    {
        int maxHeight = -1;
        int chunkWorldX = chunkX * TerrainData.ChunkSize;
        int chunkWorldZ = chunkZ * TerrainData.ChunkSize;
        float[] bodyCornerSamples = BuildInterpolatedBodyCornerSamples(chunkWorldX, chunkWorldZ);

        for (int localZ = 0; localZ < TerrainData.ChunkSize; localZ++)
        {
            for (int localX = 0; localX < TerrainData.ChunkSize; localX++)
            {
                int worldX = chunkWorldX + localX;
                int worldZ = chunkWorldZ + localZ;
                int columnIndex = (localZ * TerrainData.ChunkSize) + localX;
                int highestSolid = FillSolidColumn(blocks, localX, localZ, bodyCornerSamples);
                columnHeights[columnIndex] = highestSolid;

                PaintSurfaceLayers(blocks, localX, localZ, highestSolid);
                FillWaterColumn(blocks, fluids, localX, localZ, highestSolid);

                maxHeight = Mathf.Max(maxHeight, Mathf.Max(highestSolid, _seaLevel));
            }
        }

        return maxHeight;
    }

    private float[] BuildInterpolatedBodyCornerSamples(int chunkWorldX, int chunkWorldZ)
    {
        float[] cornerSamples = new float[CornerCountXZ * CornerCountY * CornerCountXZ];
        for (int cornerZ = 0; cornerZ < CornerCountXZ; cornerZ++)
        {
            int worldZ = chunkWorldZ + (cornerZ * CellWidth);
            for (int cornerX = 0; cornerX < CornerCountXZ; cornerX++)
            {
                int worldX = chunkWorldX + (cornerX * CellWidth);

                float continentalness = _continentalnessSampler.Sample(worldX, worldZ);
                float erosion = _erosionSampler.Sample(worldX, worldZ);
                float ridges = _weirdnessSampler.Sample(worldX, worldZ);
                float peaksAndValleys = PeaksAndValleys.Fold(ridges);
                SplineContext splineContext = new(continentalness, erosion, peaksAndValleys, ridges);
                float offset = EvaluateOffset(splineContext);
                float factor = EvaluateFactor(splineContext);
                float jaggedness = EvaluateJaggedness(splineContext);
                float jaggedNoise = _jaggedNoise.Sample(worldX, worldZ);

                for (int cornerY = 0; cornerY < CornerCountY; cornerY++)
                {
                    int terraY = cornerY * CellHeight;
                    int minecraftY = TerrainData.ToMinecraftY(terraY);
                    float bodyDensity = EvaluateFinalDensityBody(worldX, minecraftY, worldZ, offset, factor, jaggedness, jaggedNoise);
                    cornerSamples[GetCornerIndex(cornerX, cornerY, cornerZ)] = bodyDensity;
                }
            }
        }

        return cornerSamples;
    }

    private int FillSolidColumn(BlockType[] blocks, int localX, int localZ, float[] bodyCornerSamples)
    {
        int highestSolid = -1;
        int cellX = localX / CellWidth;
        int cellZ = localZ / CellWidth;
        float tx = (localX % CellWidth) / (float)CellWidth;
        float tz = (localZ % CellWidth) / (float)CellWidth;

        for (int worldY = 0; worldY < TerrainData.WorldHeight; worldY++)
        {
            int cellY = worldY / CellHeight;
            float ty = (worldY % CellHeight) / (float)CellHeight;

            float bodyDensity = SampleInterpolatedBody(bodyCornerSamples, cellX, cellY, cellZ, tx, ty, tz);
            float density = Squeeze(0.64f * bodyDensity);
            if (density <= 0f)
            {
                continue;
            }

            int index = GetIndex(localX, worldY, localZ);
            blocks[index] = BlockType.Rock;
            highestSolid = worldY;
        }

        return highestSolid;
    }

    private void FillWaterColumn(BlockType[] blocks, VoxelFluid[] fluids, int localX, int localZ, int surfaceHeight)
    {
        int waterStart = Mathf.Max(surfaceHeight + 1, 0);
        for (int worldY = waterStart; worldY <= _seaLevel; worldY++)
        {
            int index = GetIndex(localX, worldY, localZ);
            if (blocks[index] == BlockType.Air)
            {
                fluids[index] = VoxelFluid.Water(100);
            }
        }
    }

    private static void PaintSurfaceLayers(BlockType[] blocks, int localX, int localZ, int highestSolid)
    {
        if (highestSolid < 0)
        {
            return;
        }

        int soilDepth = 0;
        for (int worldY = highestSolid; worldY >= 0; worldY--)
        {
            int index = GetIndex(localX, worldY, localZ);
            if (blocks[index] == BlockType.Air)
            {
                if (soilDepth > 0)
                {
                    break;
                }

                continue;
            }

            if (soilDepth == 0)
            {
                blocks[index] = BlockType.Grass;
            }
            else if (soilDepth <= 3)
            {
                blocks[index] = BlockType.Dirt;
            }
            else
            {
                break;
            }

            soilDepth++;
        }
    }

    private static int GetIndex(int localX, int worldY, int localZ)
    {
        return ((worldY * TerrainData.ChunkSize) + localZ) * TerrainData.ChunkSize + localX;
    }

    private float EvaluateFinalDensityBody(int worldX, int minecraftY, int worldZ, float offset, float factor, float jaggedness, float jaggedNoise)
    {
        float yGradient = GetVanillaDepthGradient(minecraftY);
        float depth = yGradient + offset;
        float jaggedContribution = jaggedness * HalfNegative(jaggedNoise);
        float slopedCheese = 4f * QuarterNegative((depth + jaggedContribution) * factor);
        float baseTerrainNoise = _terrainNoise.Sample(worldX, minecraftY, worldZ);
        float terrainShape = slopedCheese + baseTerrainNoise;
        return ApplyFinalDensitySlides(minecraftY, terrainShape);
    }

    private float EvaluateOffset(in SplineContext context)
    {
        return VanillaOffsetBias + _offsetMapper.Evaluate(context);
    }

    private float EvaluateFactor(in SplineContext context)
    {
        return _factorMapper.Evaluate(context);
    }

    private float EvaluateJaggedness(in SplineContext context)
    {
        return _jaggednessMapper.Evaluate(context);
    }

    private static float GetVanillaDepthGradient(int minecraftY)
    {
        float t = Mathf.InverseLerp(TerrainData.MinecraftMinY, TerrainData.MinecraftMaxYExclusive, minecraftY);
        return Mathf.Lerp(1.5f, -1.5f, t);
    }

    private static float HalfNegative(float value)
    {
        return value > 0f ? value : value * 0.5f;
    }

    private static float QuarterNegative(float value)
    {
        return value > 0f ? value : value * 0.25f;
    }

    private static float Squeeze(float value)
    {
        float clamped = Mathf.Clamp(value, -1f, 1f);
        return (clamped * 0.5f) - ((clamped * clamped * clamped) / 24f);
    }

    private static float ApplyFinalDensitySlides(int minecraftY, float terrainShape)
    {
        float upperGradient = GetClampedGradient(minecraftY, 240, 256, 1f, 0f);
        float upperAdjusted = -0.078125f + (upperGradient * (0.078125f + terrainShape));
        float lowerGradient = GetClampedGradient(minecraftY, -64, -40, 0f, 1f);
        return 0.1171875f + (lowerGradient * (-0.1171875f + upperAdjusted));
    }

    private static int GetCornerIndex(int cornerX, int cornerY, int cornerZ)
    {
        return ((cornerZ * CornerCountY) + cornerY) * CornerCountXZ + cornerX;
    }

    private static float SampleInterpolatedBody(float[] cornerSamples, int cellX, int cellY, int cellZ, float tx, float ty, float tz)
    {
        float v000 = cornerSamples[GetCornerIndex(cellX, cellY, cellZ)];
        float v100 = cornerSamples[GetCornerIndex(cellX + 1, cellY, cellZ)];
        float v010 = cornerSamples[GetCornerIndex(cellX, cellY + 1, cellZ)];
        float v110 = cornerSamples[GetCornerIndex(cellX + 1, cellY + 1, cellZ)];
        float v001 = cornerSamples[GetCornerIndex(cellX, cellY, cellZ + 1)];
        float v101 = cornerSamples[GetCornerIndex(cellX + 1, cellY, cellZ + 1)];
        float v011 = cornerSamples[GetCornerIndex(cellX, cellY + 1, cellZ + 1)];
        float v111 = cornerSamples[GetCornerIndex(cellX + 1, cellY + 1, cellZ + 1)];

        float x00 = Mathf.Lerp(v000, v100, tx);
        float x10 = Mathf.Lerp(v010, v110, tx);
        float x01 = Mathf.Lerp(v001, v101, tx);
        float x11 = Mathf.Lerp(v011, v111, tx);
        float y0 = Mathf.Lerp(x00, x10, ty);
        float y1 = Mathf.Lerp(x01, x11, ty);
        return Mathf.Lerp(y0, y1, tz);
    }

    private static float GetClampedGradient(int minecraftY, int fromY, int toY, float fromValue, float toValue)
    {
        if (minecraftY <= fromY)
        {
            return fromValue;
        }

        if (minecraftY >= toY)
        {
            return toValue;
        }

        float t = Mathf.InverseLerp(fromY, toY, minecraftY);
        return Mathf.Lerp(fromValue, toValue, t);
    }
}
