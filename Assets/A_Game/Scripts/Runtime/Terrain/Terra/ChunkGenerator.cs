using System.Diagnostics;
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
    private const float UpperSlideOffset = -0.078125f;
    private const float LowerSlideOffset = 0.1171875f;
    private const float LowerSlideUpperBias = -0.1171875f;
    private const float UpperSlideTerrainBias = 0.078125f;

    private readonly struct CellCornerSamples
    {
        public CellCornerSamples(float v000, float v100, float v010, float v110, float v001, float v101, float v011, float v111)
        {
            this.v000 = v000;
            this.v100 = v100;
            this.v010 = v010;
            this.v110 = v110;
            this.v001 = v001;
            this.v101 = v101;
            this.v011 = v011;
            this.v111 = v111;
        }

        public readonly float v000;
        public readonly float v100;
        public readonly float v010;
        public readonly float v110;
        public readonly float v001;
        public readonly float v101;
        public readonly float v011;
        public readonly float v111;
    }

    private readonly int _seaLevel;
    private readonly ContinentalnessSampler _continentalnessSampler;
    private readonly ErosionSampler _erosionSampler;
    private readonly WeirdnessSampler _weirdnessSampler;
    private readonly SimplexNoiseSampler _jaggedNoise;
    private readonly SimplexNoise3DSampler _terrainNoise;
    private readonly JsonSplineMapper _offsetMapper;
    private readonly JsonSplineMapper _factorMapper;
    private readonly JsonSplineMapper _jaggednessMapper;
    [System.ThreadStatic] private static float[] s_bodyCornerSampleBuffer;
    [System.ThreadStatic] private static float[] s_terrainNoiseColumnBuffer;
    private static readonly int[] s_cornerMinecraftY = CreateCornerMinecraftYTable();
    private static readonly float[] s_cornerDepthGradient = CreateCornerDepthGradientTable();
    private static readonly float[] s_cornerSlideScale = CreateCornerSlideScaleTable();
    private static readonly float[] s_cornerSlideBias = CreateCornerSlideBiasTable();

    public ChunkGenerator(
        int seaLevel,
        ContinentalnessSampler continentalnessSampler,
        ErosionSampler erosionSampler,
        WeirdnessSampler weirdnessSampler,
        SimplexNoiseSampler jaggedNoiseSampler,
        SimplexNoise3DSampler terrainNoiseSampler,
        JsonSplineMapper offsetMapper,
        JsonSplineMapper factorMapper,
        JsonSplineMapper jaggednessMapper)
    {
        _seaLevel = Mathf.Clamp(seaLevel, 0, TerrainData.WorldHeight - 1);
        _continentalnessSampler = continentalnessSampler ?? throw new System.ArgumentNullException(nameof(continentalnessSampler));
        _erosionSampler = erosionSampler ?? throw new System.ArgumentNullException(nameof(erosionSampler));
        _weirdnessSampler = weirdnessSampler ?? throw new System.ArgumentNullException(nameof(weirdnessSampler));
        _jaggedNoise = jaggedNoiseSampler ?? throw new System.ArgumentNullException(nameof(jaggedNoiseSampler));
        _terrainNoise = terrainNoiseSampler ?? throw new System.ArgumentNullException(nameof(terrainNoiseSampler));
        _offsetMapper = offsetMapper ?? throw new System.ArgumentNullException(nameof(offsetMapper));
        _factorMapper = factorMapper ?? throw new System.ArgumentNullException(nameof(factorMapper));
        _jaggednessMapper = jaggednessMapper ?? throw new System.ArgumentNullException(nameof(jaggednessMapper));
    }

    public int GenerateChunkColumn(int chunkX, int chunkZ, BlockType[] blocks, VoxelFluid[] fluids, int[] columnHeights, out TerrainData.ChunkColumnGenerationProfile generationProfile)
    {
        long totalStart = Stopwatch.GetTimestamp();
        int maxHeight = -1;
        int chunkWorldX = chunkX * TerrainData.ChunkSize;
        int chunkWorldZ = chunkZ * TerrainData.ChunkSize;

        long cornerStart = Stopwatch.GetTimestamp();
        float[] bodyCornerSamples = BuildInterpolatedBodyCornerSamples(chunkWorldX, chunkWorldZ);
        double cornerSampleMilliseconds = ElapsedMilliseconds(cornerStart);
        System.Array.Fill(columnHeights, -1);
        double cellTraversalMilliseconds = 0d;
        double solidCellFillMilliseconds = 0d;
        double mixedCellFillMilliseconds = 0d;
        double surfacePaintMilliseconds = 0d;
        double waterFillMilliseconds = 0d;
        int skippedCellCount = 0;
        int solidCellCount = 0;
        int mixedCellCount = 0;

        for (int cellZ = 0; cellZ < CellCountXZ; cellZ++)
        {
            for (int cellX = 0; cellX < CellCountXZ; cellX++)
            {
                for (int cellY = 0; cellY < CellCountY; cellY++)
                {
                    long cellStart = Stopwatch.GetTimestamp();
                    CellCornerSamples corners = GetCellCornerSamples(bodyCornerSamples, cellX, cellY, cellZ);
                    GetCellCornerExtrema(in corners, out float minDensity, out float maxDensity);
                    cellTraversalMilliseconds += ElapsedMilliseconds(cellStart);
                    if (maxDensity <= 0f)
                    {
                        skippedCellCount++;
                        continue;
                    }

                    if (minDensity > 0f)
                    {
                        long solidFillStart = Stopwatch.GetTimestamp();
                        FillSolidCell(blocks, columnHeights, cellX, cellY, cellZ);
                        solidCellFillMilliseconds += ElapsedMilliseconds(solidFillStart);
                        solidCellCount++;
                        continue;
                    }

                    long mixedFillStart = Stopwatch.GetTimestamp();
                    FillMixedCell(blocks, columnHeights, cellX, cellY, cellZ, in corners);
                    mixedCellFillMilliseconds += ElapsedMilliseconds(mixedFillStart);
                    mixedCellCount++;
                }
            }
        }

        for (int localZ = 0; localZ < TerrainData.ChunkSize; localZ++)
        {
            for (int localX = 0; localX < TerrainData.ChunkSize; localX++)
            {
                int columnIndex = GetColumnIndex(localX, localZ);
                int highestSolid = columnHeights[columnIndex];
                columnHeights[columnIndex] = highestSolid;

                long surfaceStart = Stopwatch.GetTimestamp();
                PaintSurfaceLayers(blocks, localX, localZ, highestSolid);
                surfacePaintMilliseconds += ElapsedMilliseconds(surfaceStart);
                long waterStart = Stopwatch.GetTimestamp();
                FillWaterColumn(blocks, fluids, localX, localZ, highestSolid);
                waterFillMilliseconds += ElapsedMilliseconds(waterStart);

                maxHeight = Mathf.Max(maxHeight, Mathf.Max(highestSolid, _seaLevel));
            }
        }

        generationProfile = new TerrainData.ChunkColumnGenerationProfile(
            new Vector2Int(chunkX, chunkZ),
            cornerSampleMilliseconds,
            cellTraversalMilliseconds,
            solidCellFillMilliseconds,
            mixedCellFillMilliseconds,
            surfacePaintMilliseconds,
            waterFillMilliseconds,
            skippedCellCount,
            solidCellCount,
            mixedCellCount,
            ElapsedMilliseconds(totalStart));
        return maxHeight;
    }

    private float[] BuildInterpolatedBodyCornerSamples(int chunkWorldX, int chunkWorldZ)
    {
        int sampleCount = CornerCountXZ * CornerCountY * CornerCountXZ;
        float[] cornerSamples = GetBodyCornerSampleBuffer(sampleCount);
        float[] terrainNoiseColumn = GetTerrainNoiseColumnBuffer();
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
                float jaggedContribution = jaggedness * HalfNegative(jaggedNoise);
                _terrainNoise.SampleColumn(worldX, s_cornerMinecraftY[0], worldZ, CellHeight, terrainNoiseColumn, CornerCountY);

                for (int cornerY = 0; cornerY < CornerCountY; cornerY++)
                {
                    float slopedCheese = EvaluateSlopedCheese(s_cornerDepthGradient[cornerY], offset, factor, jaggedContribution);
                    float terrainShape = slopedCheese + terrainNoiseColumn[cornerY];
                    float bodyDensity = ApplyFinalDensitySlides(s_cornerSlideScale[cornerY], s_cornerSlideBias[cornerY], terrainShape);
                    cornerSamples[GetCornerIndex(cornerX, cornerY, cornerZ)] = bodyDensity;
                }
            }
        }

        return cornerSamples;
    }

    private static float[] GetBodyCornerSampleBuffer(int sampleCount)
    {
        if (s_bodyCornerSampleBuffer == null || s_bodyCornerSampleBuffer.Length != sampleCount)
        {
            s_bodyCornerSampleBuffer = new float[sampleCount];
        }

        return s_bodyCornerSampleBuffer;
    }

    private static float[] GetTerrainNoiseColumnBuffer()
    {
        if (s_terrainNoiseColumnBuffer == null || s_terrainNoiseColumnBuffer.Length != CornerCountY)
        {
            s_terrainNoiseColumnBuffer = new float[CornerCountY];
        }

        return s_terrainNoiseColumnBuffer;
    }

    private static CellCornerSamples GetCellCornerSamples(float[] cornerSamples, int cellX, int cellY, int cellZ)
    {
        return new CellCornerSamples(
            cornerSamples[GetCornerIndex(cellX, cellY, cellZ)],
            cornerSamples[GetCornerIndex(cellX + 1, cellY, cellZ)],
            cornerSamples[GetCornerIndex(cellX, cellY + 1, cellZ)],
            cornerSamples[GetCornerIndex(cellX + 1, cellY + 1, cellZ)],
            cornerSamples[GetCornerIndex(cellX, cellY, cellZ + 1)],
            cornerSamples[GetCornerIndex(cellX + 1, cellY, cellZ + 1)],
            cornerSamples[GetCornerIndex(cellX, cellY + 1, cellZ + 1)],
            cornerSamples[GetCornerIndex(cellX + 1, cellY + 1, cellZ + 1)]);
    }

    private static void GetCellCornerExtrema(in CellCornerSamples corners, out float minDensity, out float maxDensity)
    {
        minDensity = corners.v000;
        maxDensity = corners.v000;
        IncludeCornerDensity(corners.v100, ref minDensity, ref maxDensity);
        IncludeCornerDensity(corners.v010, ref minDensity, ref maxDensity);
        IncludeCornerDensity(corners.v110, ref minDensity, ref maxDensity);
        IncludeCornerDensity(corners.v001, ref minDensity, ref maxDensity);
        IncludeCornerDensity(corners.v101, ref minDensity, ref maxDensity);
        IncludeCornerDensity(corners.v011, ref minDensity, ref maxDensity);
        IncludeCornerDensity(corners.v111, ref minDensity, ref maxDensity);
    }

    private static void IncludeCornerDensity(float density, ref float minDensity, ref float maxDensity)
    {
        if (density < minDensity)
        {
            minDensity = density;
        }

        if (density > maxDensity)
        {
            maxDensity = density;
        }
    }

    private static void FillSolidCell(BlockType[] blocks, int[] columnHeights, int cellX, int cellY, int cellZ)
    {
        int startX = cellX * CellWidth;
        int startY = cellY * CellHeight;
        int startZ = cellZ * CellWidth;
        int cellTopY = startY + CellHeight - 1;

        for (int localZ = 0; localZ < CellWidth; localZ++)
        {
            int blockZ = startZ + localZ;
            for (int localX = 0; localX < CellWidth; localX++)
            {
                int blockX = startX + localX;
                int columnIndex = GetColumnIndex(blockX, blockZ);
                if (cellTopY > columnHeights[columnIndex])
                {
                    columnHeights[columnIndex] = cellTopY;
                }

                for (int localY = 0; localY < CellHeight; localY++)
                {
                    blocks[GetIndex(blockX, startY + localY, blockZ)] = BlockType.Rock;
                }
            }
        }
    }

    private static void FillMixedCell(BlockType[] blocks, int[] columnHeights, int cellX, int cellY, int cellZ, in CellCornerSamples corners)
    {
        int startX = cellX * CellWidth;
        int startY = cellY * CellHeight;
        int startZ = cellZ * CellWidth;

        for (int localZ = 0; localZ < CellWidth; localZ++)
        {
            int blockZ = startZ + localZ;
            float tz = localZ / (float)CellWidth;
            for (int localX = 0; localX < CellWidth; localX++)
            {
                int blockX = startX + localX;
                float tx = localX / (float)CellWidth;
                int columnIndex = GetColumnIndex(blockX, blockZ);
                int highestSolid = columnHeights[columnIndex];

                for (int localY = 0; localY < CellHeight; localY++)
                {
                    float ty = localY / (float)CellHeight;
                    float bodyDensity = SampleInterpolatedBody(in corners, tx, ty, tz);
                    float density = Squeeze(0.64f * bodyDensity);
                    if (density <= 0f)
                    {
                        continue;
                    }

                    int worldY = startY + localY;
                    blocks[GetIndex(blockX, worldY, blockZ)] = BlockType.Rock;
                    highestSolid = worldY;
                }

                columnHeights[columnIndex] = highestSolid;
            }
        }
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

    private static int GetColumnIndex(int localX, int localZ)
    {
        return (localZ * TerrainData.ChunkSize) + localX;
    }

    private static float EvaluateSlopedCheese(float yGradient, float offset, float factor, float jaggedContribution)
    {
        float depth = yGradient + offset;
        return 4f * QuarterNegative((depth + jaggedContribution) * factor);
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

    private static float ApplyFinalDensitySlides(float slideScale, float slideBias, float terrainShape)
    {
        return slideBias + (slideScale * terrainShape);
    }

    private static int GetCornerIndex(int cornerX, int cornerY, int cornerZ)
    {
        return ((cornerZ * CornerCountY) + cornerY) * CornerCountXZ + cornerX;
    }

    private static float SampleInterpolatedBody(in CellCornerSamples corners, float tx, float ty, float tz)
    {
        float x00 = Mathf.Lerp(corners.v000, corners.v100, tx);
        float x10 = Mathf.Lerp(corners.v010, corners.v110, tx);
        float x01 = Mathf.Lerp(corners.v001, corners.v101, tx);
        float x11 = Mathf.Lerp(corners.v011, corners.v111, tx);
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

    private static double ElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
    }

    private static int[] CreateCornerMinecraftYTable()
    {
        int[] values = new int[CornerCountY];
        for (int cornerY = 0; cornerY < CornerCountY; cornerY++)
        {
            values[cornerY] = TerrainData.ToMinecraftY(cornerY * CellHeight);
        }

        return values;
    }

    private static float[] CreateCornerDepthGradientTable()
    {
        float[] values = new float[CornerCountY];
        for (int cornerY = 0; cornerY < CornerCountY; cornerY++)
        {
            values[cornerY] = GetVanillaDepthGradient(s_cornerMinecraftY[cornerY]);
        }

        return values;
    }

    private static float[] CreateCornerSlideScaleTable()
    {
        float[] values = new float[CornerCountY];
        for (int cornerY = 0; cornerY < CornerCountY; cornerY++)
        {
            int minecraftY = s_cornerMinecraftY[cornerY];
            float upperGradient = GetClampedGradient(minecraftY, 240, 256, 1f, 0f);
            float lowerGradient = GetClampedGradient(minecraftY, -64, -40, 0f, 1f);
            values[cornerY] = lowerGradient * upperGradient;
        }

        return values;
    }

    private static float[] CreateCornerSlideBiasTable()
    {
        float[] values = new float[CornerCountY];
        for (int cornerY = 0; cornerY < CornerCountY; cornerY++)
        {
            int minecraftY = s_cornerMinecraftY[cornerY];
            float upperGradient = GetClampedGradient(minecraftY, 240, 256, 1f, 0f);
            float lowerGradient = GetClampedGradient(minecraftY, -64, -40, 0f, 1f);
            values[cornerY] = LowerSlideOffset + (lowerGradient * (LowerSlideUpperBias + UpperSlideOffset + (upperGradient * UpperSlideTerrainBias)));
        }

        return values;
    }
}
