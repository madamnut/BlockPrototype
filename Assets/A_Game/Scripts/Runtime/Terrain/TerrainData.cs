using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public sealed class TerrainData : IDisposable
{
    public readonly struct ChunkColumnGenerationProfile
    {
        public readonly Vector2Int chunkCoords;
        public readonly double rawSampleMilliseconds;
        public readonly double remapFilterMilliseconds;
        public readonly double composeHeightMilliseconds;
        public readonly double blockFillMilliseconds;
        public readonly double totalMilliseconds;

        public ChunkColumnGenerationProfile(
            Vector2Int chunkCoords,
            double rawSampleMilliseconds,
            double remapFilterMilliseconds,
            double composeHeightMilliseconds,
            double blockFillMilliseconds,
            double totalMilliseconds)
        {
            this.chunkCoords = chunkCoords;
            this.rawSampleMilliseconds = rawSampleMilliseconds;
            this.remapFilterMilliseconds = remapFilterMilliseconds;
            this.composeHeightMilliseconds = composeHeightMilliseconds;
            this.blockFillMilliseconds = blockFillMilliseconds;
            this.totalMilliseconds = totalMilliseconds;
        }
    }

    public readonly struct CompletedChunkColumnInfo
    {
        public readonly Vector2Int chunkCoords;
        public readonly double readyTime;
        public readonly double heightJobMilliseconds;
        public readonly double blockFillJobMilliseconds;
        public readonly double finalizeMilliseconds;

        public CompletedChunkColumnInfo(
            Vector2Int chunkCoords,
            double readyTime,
            double heightJobMilliseconds,
            double blockFillJobMilliseconds,
            double finalizeMilliseconds)
        {
            this.chunkCoords = chunkCoords;
            this.readyTime = readyTime;
            this.heightJobMilliseconds = heightJobMilliseconds;
            this.blockFillJobMilliseconds = blockFillJobMilliseconds;
            this.finalizeMilliseconds = finalizeMilliseconds;
        }
    }

    public readonly struct WorldGenDebugSample
    {
        public readonly int height;
        public readonly float continentalness;
        public readonly float erosion;
        public readonly float weirdness;
        public readonly float foldedWeirdness;

        public WorldGenDebugSample(
            int height,
            float continentalness,
            float erosion,
            float weirdness,
            float foldedWeirdness)
        {
            this.height = height;
            this.continentalness = continentalness;
            this.erosion = erosion;
            this.weirdness = weirdness;
            this.foldedWeirdness = foldedWeirdness;
        }
    }

    private sealed class ChunkColumnData
    {
        public const byte SolidContentBit = 1 << 0;
        public const byte FluidContentBit = 1 << 1;
        public const byte FoliageContentBit = 1 << 2;

        public readonly BlockType[] blocks;
        public readonly VoxelFluid[] fluids;
        public readonly ushort[] foliageIds;
        public readonly int[] columnHeights;
        public readonly byte[] subChunkContents;
        public int maxHeight;

        public ChunkColumnData(BlockType[] blocks, VoxelFluid[] fluids, ushort[] foliageIds, int[] columnHeights, byte[] subChunkContents, int maxHeight)
        {
            this.blocks = blocks;
            this.fluids = fluids;
            this.foliageIds = foliageIds;
            this.columnHeights = columnHeights;
            this.subChunkContents = subChunkContents;
            this.maxHeight = maxHeight;
        }
    }

    private struct PendingChunkColumnData
    {
        public JobHandle handle;
        public JobHandle heightHandle;
        public JobHandle blockFillHandle;
        public NativeArray<BlockType> blocks;
        public NativeArray<int> columnHeights;
        public double requestTime;
        public double heightReadyTime;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct SampleChunkColumnHeightsJob : IJobParallelFor
    {
        public int chunkX;
        public int chunkZ;
        public int seed;
        public bool useContinentalnessRemap;
        public bool useErosionRemap;
        public bool useRidgesRemap;
        public ContinentalnessSettings continentalness;
        public ErosionSettings erosion;
        public RidgesSettings ridges;

        [ReadOnly] public NativeArray<float> continentalnessCdfLut;
        [ReadOnly] public NativeArray<float> erosionCdfLut;
        [ReadOnly] public NativeArray<float> ridgesCdfLut;
        [ReadOnly] public NativeArray<SplineTreeBakedNode> offsetSplineNodes;
        [ReadOnly] public NativeArray<SplineTreeBakedPoint> offsetSplinePoints;
        [ReadOnly] public NativeArray<SplineTreeBakedNode> factorSplineNodes;
        [ReadOnly] public NativeArray<SplineTreeBakedPoint> factorSplinePoints;
        [ReadOnly] public NativeArray<SplineTreeBakedNode> jaggednessSplineNodes;
        [ReadOnly] public NativeArray<SplineTreeBakedPoint> jaggednessSplinePoints;
        [ReadOnly] public NativeArray<SplineTreeBakedNode> legacyTerrainHeightSplineNodes;
        [ReadOnly] public NativeArray<SplineTreeBakedPoint> legacyTerrainHeightSplinePoints;
        [ReadOnly] public NativeArray<float> continentalnessHeightLut;
        public NativeArray<int> columnHeights;

        public void Execute(int index)
        {
            int localX = index % ChunkSize;
            int localZ = index / ChunkSize;
            int worldX = (chunkX * ChunkSize) + localX;
            int worldZ = (chunkZ * ChunkSize) + localZ;
            columnHeights[index] = SampleSurfaceHeight(worldX, worldZ);
        }

        private int SampleSurfaceHeight(int worldX, int worldZ)
        {
            float worldRegionX = worldX / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
            float worldRegionZ = worldZ / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
            float rawContinentalness = WorldGenPrototypeJobs.SampleRawContinentalness(
                seed,
                worldRegionX,
                worldRegionZ,
                continentalness);
            float continentalnessValue = RemapRawNoise(
                rawContinentalness,
                useContinentalnessRemap,
                continentalnessCdfLut);
            float rawErosion = WorldGenPrototypeJobs.SampleRawErosion(
                seed,
                worldRegionX,
                worldRegionZ,
                erosion);
            float erosionValue = RemapRawNoise(
                rawErosion,
                useErosionRemap,
                erosionCdfLut);
            float rawRidges = WorldGenPrototypeJobs.SampleRawRidges(
                seed,
                worldRegionX,
                worldRegionZ,
                ridges);
            float weirdnessValue = RemapRawNoise(
                rawRidges,
                useRidgesRemap,
                ridgesCdfLut);
            float foldedWeirdnessValue = WorldGenPrototypeJobs.CalculatePvFromWeirdness(weirdnessValue);
            return ComposeSurfaceHeight(
                continentalnessValue,
                erosionValue,
                weirdnessValue,
                foldedWeirdnessValue,
                offsetSplineNodes,
                offsetSplinePoints,
                factorSplineNodes,
                factorSplinePoints,
                jaggednessSplineNodes,
                jaggednessSplinePoints,
                legacyTerrainHeightSplineNodes,
                legacyTerrainHeightSplinePoints,
                continentalnessHeightLut,
                WorldHeight);
        }

        private static int ComposeSurfaceHeight(
            float continentalnessValue,
            float erosionValue,
            float weirdnessValue,
            float foldedWeirdnessValue,
            NativeArray<SplineTreeBakedNode> offsetSplineNodes,
            NativeArray<SplineTreeBakedPoint> offsetSplinePoints,
            NativeArray<SplineTreeBakedNode> factorSplineNodes,
            NativeArray<SplineTreeBakedPoint> factorSplinePoints,
            NativeArray<SplineTreeBakedNode> jaggednessSplineNodes,
            NativeArray<SplineTreeBakedPoint> jaggednessSplinePoints,
            NativeArray<SplineTreeBakedNode> legacyTerrainHeightSplineNodes,
            NativeArray<SplineTreeBakedPoint> legacyTerrainHeightSplinePoints,
            NativeArray<float> heightLut,
            int worldHeight)
        {
            if (offsetSplineNodes.IsCreated &&
                offsetSplinePoints.IsCreated &&
                factorSplineNodes.IsCreated &&
                factorSplinePoints.IsCreated &&
                jaggednessSplineNodes.IsCreated &&
                jaggednessSplinePoints.IsCreated &&
                offsetSplineNodes.Length > 0 &&
                offsetSplinePoints.Length > 0 &&
                factorSplineNodes.Length > 0 &&
                factorSplinePoints.Length > 0 &&
                jaggednessSplineNodes.Length > 0 &&
                jaggednessSplinePoints.Length > 0)
            {
                TerrainShapeSample shape = EvaluateTerrainShape(
                    continentalnessValue,
                    erosionValue,
                    weirdnessValue,
                    foldedWeirdnessValue,
                    offsetSplineNodes,
                    offsetSplinePoints,
                    factorSplineNodes,
                    factorSplinePoints,
                    jaggednessSplineNodes,
                    jaggednessSplinePoints);
                return WorldGenDensity.FindSurfaceHeight(worldHeight, shape, foldedWeirdnessValue);
            }

            if (legacyTerrainHeightSplineNodes.IsCreated &&
                legacyTerrainHeightSplinePoints.IsCreated &&
                legacyTerrainHeightSplineNodes.Length > 0 &&
                legacyTerrainHeightSplinePoints.Length > 0)
            {
                return SplineTreeEvaluator.EvaluateHeight(
                    continentalnessValue,
                    erosionValue,
                    weirdnessValue,
                    foldedWeirdnessValue,
                    legacyTerrainHeightSplineNodes,
                    legacyTerrainHeightSplinePoints,
                    worldHeight);
            }

            if (heightLut.IsCreated && heightLut.Length > 1)
            {
                float normalized = (math.clamp(continentalnessValue, -1f, 1f) + 1f) * 0.5f;
                float scaledIndex = normalized * (heightLut.Length - 1);
                int lowerIndex = (int)math.floor(scaledIndex);
                int upperIndex = math.min(lowerIndex + 1, heightLut.Length - 1);
                float t = scaledIndex - lowerIndex;
                return math.clamp((int)math.round(math.lerp(heightLut[lowerIndex], heightLut[upperIndex], t)), 0, worldHeight - 1);
            }

            float normalizedContinentalness = math.saturate((continentalnessValue + 1f) * 0.5f);
            return math.clamp((int)math.round(math.lerp(0f, 180f, normalizedContinentalness)), 0, worldHeight - 1);
        }

        private static TerrainShapeSample EvaluateTerrainShape(
            float continentalnessValue,
            float erosionValue,
            float weirdnessValue,
            float foldedWeirdnessValue,
            NativeArray<SplineTreeBakedNode> offsetSplineNodes,
            NativeArray<SplineTreeBakedPoint> offsetSplinePoints,
            NativeArray<SplineTreeBakedNode> factorSplineNodes,
            NativeArray<SplineTreeBakedPoint> factorSplinePoints,
            NativeArray<SplineTreeBakedNode> jaggednessSplineNodes,
            NativeArray<SplineTreeBakedPoint> jaggednessSplinePoints)
        {
            float offset = SplineTreeEvaluator.EvaluateValue(
                continentalnessValue,
                erosionValue,
                weirdnessValue,
                foldedWeirdnessValue,
                offsetSplineNodes,
                offsetSplinePoints);
            float factor = SplineTreeEvaluator.EvaluateValue(
                continentalnessValue,
                erosionValue,
                weirdnessValue,
                foldedWeirdnessValue,
                factorSplineNodes,
                factorSplinePoints);
            float jaggedness = SplineTreeEvaluator.EvaluateValue(
                continentalnessValue,
                erosionValue,
                weirdnessValue,
                foldedWeirdnessValue,
                jaggednessSplineNodes,
                jaggednessSplinePoints);
            return new TerrainShapeSample(offset, math.max(0.0001f, factor), jaggedness);
        }

        private static float RemapRawNoise(float rawValue, bool useCdfRemap, NativeArray<float> cdfLut)
        {
            float normalized = math.saturate(rawValue);
            if (useCdfRemap && cdfLut.IsCreated && cdfLut.Length > 1)
            {
                float scaledIndex = normalized * (cdfLut.Length - 1);
                int lowerIndex = (int)math.floor(scaledIndex);
                int upperIndex = math.min(lowerIndex + 1, cdfLut.Length - 1);
                float t = scaledIndex - lowerIndex;
                normalized = math.lerp(cdfLut[lowerIndex], cdfLut[upperIndex], t);
            }

            return (normalized * 2f) - 1f;
        }

    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct FillChunkColumnBlocksJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> columnHeights;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<BlockType> blocks;

        public void Execute(int index)
        {
            int localX = index % ChunkSize;
            int localZ = index / ChunkSize;
            int terrainHeight = columnHeights[index];

            for (int worldY = 0; worldY <= terrainHeight; worldY++)
            {
                blocks[GetIndex(localX, worldY, localZ)] = BlockType.Rock;
            }
        }
    }

    public const int ChunkSize = 16;
    public const int SubChunkSize = 16;
    public const int WorldHeight = 256;
    public const int SubChunkCountY = WorldHeight / SubChunkSize;
    private readonly Dictionary<Vector2Int, ChunkColumnData> _chunkColumns = new();
    private readonly Dictionary<Vector2Int, PendingChunkColumnData> _pendingChunkColumns = new();
    private readonly List<Vector2Int> _pendingChunkKeys = new();
    private readonly int _seed;
    private readonly TerrainGenerationSettings _settings;
    private readonly float[] _managedContinentalnessCdfLut;
    private readonly float[] _managedErosionCdfLut;
    private readonly float[] _managedRidgesCdfLut;
    private readonly SplineTreeBakedNode[] _managedOffsetSplineNodes;
    private readonly SplineTreeBakedPoint[] _managedOffsetSplinePoints;
    private readonly SplineTreeBakedNode[] _managedFactorSplineNodes;
    private readonly SplineTreeBakedPoint[] _managedFactorSplinePoints;
    private readonly SplineTreeBakedNode[] _managedJaggednessSplineNodes;
    private readonly SplineTreeBakedPoint[] _managedJaggednessSplinePoints;
    private readonly SplineTreeBakedNode[] _managedLegacyTerrainHeightSplineNodes;
    private readonly SplineTreeBakedPoint[] _managedLegacyTerrainHeightSplinePoints;
    private readonly float[] _managedContinentalnessHeightLut;
    private NativeArray<float> _continentalnessCdfLut;
    private NativeArray<float> _erosionCdfLut;
    private NativeArray<float> _ridgesCdfLut;
    private NativeArray<SplineTreeBakedNode> _offsetSplineNodes;
    private NativeArray<SplineTreeBakedPoint> _offsetSplinePoints;
    private NativeArray<SplineTreeBakedNode> _factorSplineNodes;
    private NativeArray<SplineTreeBakedPoint> _factorSplinePoints;
    private NativeArray<SplineTreeBakedNode> _jaggednessSplineNodes;
    private NativeArray<SplineTreeBakedPoint> _jaggednessSplinePoints;
    private NativeArray<SplineTreeBakedNode> _legacyTerrainHeightSplineNodes;
    private NativeArray<SplineTreeBakedPoint> _legacyTerrainHeightSplinePoints;
    private NativeArray<float> _continentalnessHeightLut;
    public TerrainData(
        int seed,
        TerrainGenerationSettings settings,
        float[] continentalnessCdfLut = null,
        float[] erosionCdfLut = null,
        float[] ridgesCdfLut = null,
        SplineTreeBakedNode[] offsetSplineNodes = null,
        SplineTreeBakedPoint[] offsetSplinePoints = null,
        SplineTreeBakedNode[] factorSplineNodes = null,
        SplineTreeBakedPoint[] factorSplinePoints = null,
        SplineTreeBakedNode[] jaggednessSplineNodes = null,
        SplineTreeBakedPoint[] jaggednessSplinePoints = null,
        SplineTreeBakedNode[] legacyTerrainHeightSplineNodes = null,
        SplineTreeBakedPoint[] legacyTerrainHeightSplinePoints = null,
        float[] continentalnessHeightLut = null)
    {
        _seed = seed;
        _settings = settings;
        _managedContinentalnessCdfLut = continentalnessCdfLut;
        _managedErosionCdfLut = erosionCdfLut;
        _managedRidgesCdfLut = ridgesCdfLut;
        _managedOffsetSplineNodes = offsetSplineNodes;
        _managedOffsetSplinePoints = offsetSplinePoints;
        _managedFactorSplineNodes = factorSplineNodes;
        _managedFactorSplinePoints = factorSplinePoints;
        _managedJaggednessSplineNodes = jaggednessSplineNodes;
        _managedJaggednessSplinePoints = jaggednessSplinePoints;
        _managedLegacyTerrainHeightSplineNodes = legacyTerrainHeightSplineNodes;
        _managedLegacyTerrainHeightSplinePoints = legacyTerrainHeightSplinePoints;
        _managedContinentalnessHeightLut = continentalnessHeightLut;
        _continentalnessCdfLut = CreateNativeLut(settings.useContinentalnessRemap, continentalnessCdfLut);
        _erosionCdfLut = CreateNativeLut(settings.useErosionRemap, erosionCdfLut);
        _ridgesCdfLut = CreateNativeLut(settings.useRidgesRemap, ridgesCdfLut);
        _offsetSplineNodes = CreateNativeSplineNodes(offsetSplineNodes);
        _offsetSplinePoints = CreateNativeSplinePoints(offsetSplinePoints);
        _factorSplineNodes = CreateNativeSplineNodes(factorSplineNodes);
        _factorSplinePoints = CreateNativeSplinePoints(factorSplinePoints);
        _jaggednessSplineNodes = CreateNativeSplineNodes(jaggednessSplineNodes);
        _jaggednessSplinePoints = CreateNativeSplinePoints(jaggednessSplinePoints);
        _legacyTerrainHeightSplineNodes = CreateNativeSplineNodes(legacyTerrainHeightSplineNodes);
        _legacyTerrainHeightSplinePoints = CreateNativeSplinePoints(legacyTerrainHeightSplinePoints);
        _continentalnessHeightLut = CreateNativeLut(continentalnessHeightLut != null && continentalnessHeightLut.Length > 1, continentalnessHeightLut);
    }

    public int PendingChunkColumnCount => _pendingChunkColumns.Count;

    public WorldGenDebugSample SampleWorldGen(int worldX, int worldZ)
    {
        int height = WorldGenSampler.SampleSurfaceHeight(
            worldX,
            worldZ,
            _seed,
            _settings,
            _managedContinentalnessCdfLut,
            _managedErosionCdfLut,
            _managedRidgesCdfLut,
            _managedOffsetSplineNodes,
            _managedOffsetSplinePoints,
            _managedFactorSplineNodes,
            _managedFactorSplinePoints,
            _managedJaggednessSplineNodes,
            _managedJaggednessSplinePoints,
            _managedLegacyTerrainHeightSplineNodes,
            _managedLegacyTerrainHeightSplinePoints,
            _managedContinentalnessHeightLut);
        float continentalness = WorldGenSampler.SampleContinentalness(worldX, worldZ, _seed, _settings, _managedContinentalnessCdfLut);
        float erosion = WorldGenSampler.SampleErosion(worldX, worldZ, _seed, _settings, _managedErosionCdfLut);
        float weirdness = WorldGenSampler.SampleWeirdness(worldX, worldZ, _seed, _settings, _managedRidgesCdfLut);
        float foldedWeirdness = WorldGenSampler.SampleFoldedWeirdness(worldX, worldZ, _seed, _settings, _managedRidgesCdfLut);

        return new WorldGenDebugSample(
            height,
            continentalness,
            erosion,
            weirdness,
            foldedWeirdness);
    }

    public ChunkColumnGenerationProfile ProfileChunkColumnGeneration(int chunkX, int chunkZ)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        long rawSampleTicks = 0;
        long remapFilterTicks = 0;
        long composeHeightTicks = 0;
        long blockFillTicks = 0;

        int[] columnHeights = new int[ChunkSize * ChunkSize];
        BlockType[] blocks = new BlockType[ChunkSize * WorldHeight * ChunkSize];

        for (int localZ = 0; localZ < ChunkSize; localZ++)
        {
            for (int localX = 0; localX < ChunkSize; localX++)
            {
                int columnIndex = (localZ * ChunkSize) + localX;
                int worldX = (chunkX * ChunkSize) + localX;
                int worldZ = (chunkZ * ChunkSize) + localZ;
                float worldRegionX = worldX / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;
                float worldRegionZ = worldZ / (float)WorldGenPrototypeJobs.RegionSizeInBlocks;

                long stageStart = stopwatch.ElapsedTicks;
                float rawContinentalness = WorldGenPrototypeJobs.SampleRawContinentalness(
                    _seed,
                    worldRegionX,
                    worldRegionZ,
                    _settings.continentalness);
                rawSampleTicks += stopwatch.ElapsedTicks - stageStart;

                stageStart = stopwatch.ElapsedTicks;
                float continentalness = WorldGenSampler.RemapRawContinentalness(
                    rawContinentalness,
                    _settings.useContinentalnessRemap,
                    _managedContinentalnessCdfLut);
                remapFilterTicks += stopwatch.ElapsedTicks - stageStart;

                stageStart = stopwatch.ElapsedTicks;
                float erosion = WorldGenSampler.SampleErosion(worldX, worldZ, _seed, _settings, _managedErosionCdfLut);
                float weirdness = WorldGenSampler.SampleWeirdness(worldX, worldZ, _seed, _settings, _managedRidgesCdfLut);
                float foldedWeirdness = WorldGenPrototypeJobs.CalculatePvFromWeirdness(weirdness);
                remapFilterTicks += stopwatch.ElapsedTicks - stageStart;

                stageStart = stopwatch.ElapsedTicks;
                int terrainHeight = WorldGenSampler.ComposeSurfaceHeight(
                    continentalness,
                    erosion,
                weirdness,
                foldedWeirdness,
                _managedOffsetSplineNodes,
                _managedOffsetSplinePoints,
                _managedFactorSplineNodes,
                _managedFactorSplinePoints,
                _managedJaggednessSplineNodes,
                _managedJaggednessSplinePoints,
                _managedLegacyTerrainHeightSplineNodes,
                _managedLegacyTerrainHeightSplinePoints,
                _managedContinentalnessHeightLut);
                composeHeightTicks += stopwatch.ElapsedTicks - stageStart;
                columnHeights[columnIndex] = terrainHeight;
            }
        }

        long fillStart = stopwatch.ElapsedTicks;
        for (int localZ = 0; localZ < ChunkSize; localZ++)
        {
            for (int localX = 0; localX < ChunkSize; localX++)
            {
                int columnIndex = (localZ * ChunkSize) + localX;
                int terrainHeight = columnHeights[columnIndex];
                for (int worldY = 0; worldY <= terrainHeight; worldY++)
                {
                    blocks[GetIndex(localX, worldY, localZ)] = BlockType.Rock;
                }
            }
        }

        blockFillTicks = stopwatch.ElapsedTicks - fillStart;
        double ticksToMilliseconds = 1000d / Stopwatch.Frequency;
        return new ChunkColumnGenerationProfile(
            new Vector2Int(chunkX, chunkZ),
            rawSampleTicks * ticksToMilliseconds,
            remapFilterTicks * ticksToMilliseconds,
            composeHeightTicks * ticksToMilliseconds,
            blockFillTicks * ticksToMilliseconds,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    public void Dispose()
    {
        for (int i = 0; i < _pendingChunkKeys.Count; i++)
        {
            Vector2Int key = _pendingChunkKeys[i];
            if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
            {
                continue;
            }

            pending.handle.Complete();
            if (pending.blocks.IsCreated)
            {
                pending.blocks.Dispose();
            }

            if (pending.columnHeights.IsCreated)
            {
                pending.columnHeights.Dispose();
            }
        }

        _pendingChunkColumns.Clear();
        _pendingChunkKeys.Clear();
        _chunkColumns.Clear();

        if (_continentalnessCdfLut.IsCreated)
        {
            _continentalnessCdfLut.Dispose();
        }

        if (_erosionCdfLut.IsCreated)
        {
            _erosionCdfLut.Dispose();
        }

        if (_ridgesCdfLut.IsCreated)
        {
            _ridgesCdfLut.Dispose();
        }

        if (_offsetSplineNodes.IsCreated)
        {
            _offsetSplineNodes.Dispose();
        }

        if (_offsetSplinePoints.IsCreated)
        {
            _offsetSplinePoints.Dispose();
        }

        if (_factorSplineNodes.IsCreated)
        {
            _factorSplineNodes.Dispose();
        }

        if (_factorSplinePoints.IsCreated)
        {
            _factorSplinePoints.Dispose();
        }

        if (_jaggednessSplineNodes.IsCreated)
        {
            _jaggednessSplineNodes.Dispose();
        }

        if (_jaggednessSplinePoints.IsCreated)
        {
            _jaggednessSplinePoints.Dispose();
        }

        if (_legacyTerrainHeightSplineNodes.IsCreated)
        {
            _legacyTerrainHeightSplineNodes.Dispose();
        }

        if (_legacyTerrainHeightSplinePoints.IsCreated)
        {
            _legacyTerrainHeightSplinePoints.Dispose();
        }

        if (_continentalnessHeightLut.IsCreated)
        {
            _continentalnessHeightLut.Dispose();
        }
    }

    public bool RequestChunkColumn(int chunkX, int chunkZ)
    {
        Vector2Int key = new(chunkX, chunkZ);
        if (_chunkColumns.ContainsKey(key) || _pendingChunkColumns.ContainsKey(key))
        {
            return false;
        }

        PendingChunkColumnData pending = new()
        {
            blocks = new NativeArray<BlockType>(ChunkSize * WorldHeight * ChunkSize, Allocator.Persistent, NativeArrayOptions.ClearMemory),
            columnHeights = new NativeArray<int>(ChunkSize * ChunkSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            requestTime = Time.realtimeSinceStartupAsDouble,
            heightReadyTime = -1d,
        };

        SampleChunkColumnHeightsJob heightJob = new()
        {
            chunkX = chunkX,
            chunkZ = chunkZ,
            seed = _seed,
            useContinentalnessRemap = _settings.useContinentalnessRemap,
            useErosionRemap = _settings.useErosionRemap,
            useRidgesRemap = _settings.useRidgesRemap,
            continentalness = _settings.continentalness,
            erosion = _settings.erosion,
            ridges = _settings.ridges,
            continentalnessCdfLut = _continentalnessCdfLut,
            erosionCdfLut = _erosionCdfLut,
            ridgesCdfLut = _ridgesCdfLut,
            offsetSplineNodes = _offsetSplineNodes,
            offsetSplinePoints = _offsetSplinePoints,
            factorSplineNodes = _factorSplineNodes,
            factorSplinePoints = _factorSplinePoints,
            jaggednessSplineNodes = _jaggednessSplineNodes,
            jaggednessSplinePoints = _jaggednessSplinePoints,
            legacyTerrainHeightSplineNodes = _legacyTerrainHeightSplineNodes,
            legacyTerrainHeightSplinePoints = _legacyTerrainHeightSplinePoints,
            continentalnessHeightLut = _continentalnessHeightLut,
            columnHeights = pending.columnHeights,
        };

        FillChunkColumnBlocksJob blockFillJob = new()
        {
            columnHeights = pending.columnHeights,
            blocks = pending.blocks,
        };

        pending.heightHandle = heightJob.Schedule(ChunkSize * ChunkSize, ChunkSize);
        pending.blockFillHandle = blockFillJob.Schedule(ChunkSize * ChunkSize, ChunkSize, pending.heightHandle);
        pending.handle = pending.blockFillHandle;
        _pendingChunkColumns.Add(key, pending);
        _pendingChunkKeys.Add(key);
        return true;
    }

    public int CompletePendingChunkColumns(List<CompletedChunkColumnInfo> completedChunkInfos, int maxCompletions)
    {
        int completedCount = 0;
        for (int i = 0; i < _pendingChunkKeys.Count && completedCount < maxCompletions; i++)
        {
            Vector2Int key = _pendingChunkKeys[i];
            if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
            {
                continue;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (pending.heightReadyTime < 0d && pending.heightHandle.IsCompleted)
            {
                pending.heightReadyTime = now;
                _pendingChunkColumns[key] = pending;
            }

            if (!pending.handle.IsCompleted)
            {
                continue;
            }

            double readyTime = Time.realtimeSinceStartupAsDouble;
            double heightReadyTime = pending.heightReadyTime >= 0d ? pending.heightReadyTime : readyTime;
            FinalizePendingChunkColumn(key, pending);
            double finalizeMilliseconds = (Time.realtimeSinceStartupAsDouble - readyTime) * 1000d;
            _pendingChunkColumns.Remove(key);
            _pendingChunkKeys.RemoveAt(i);
            i--;

            double heightJobMilliseconds = Math.Max(0d, (heightReadyTime - pending.requestTime) * 1000d);
            double blockFillJobMilliseconds = Math.Max(0d, (readyTime - heightReadyTime) * 1000d);
            completedChunkInfos?.Add(new CompletedChunkColumnInfo(
                key,
                readyTime,
                heightJobMilliseconds,
                blockFillJobMilliseconds,
                finalizeMilliseconds));
            completedCount++;
        }

        return completedCount;
    }

    public int CompletePendingChunkColumns(int maxCompletions)
    {
        return CompletePendingChunkColumns(null, maxCompletions);
    }

    public bool IsChunkColumnReady(int chunkX, int chunkZ)
    {
        return _chunkColumns.ContainsKey(new Vector2Int(chunkX, chunkZ));
    }

    public bool TryGetUsedSubChunkCount(int chunkX, int chunkZ, out int usedSubChunkCount)
    {
        if (!_chunkColumns.TryGetValue(new Vector2Int(chunkX, chunkZ), out ChunkColumnData chunk))
        {
            usedSubChunkCount = 0;
            return false;
        }

        usedSubChunkCount = GetUsedSubChunkCount(chunk);
        return true;
    }

    public int GetUsedSubChunkCount(int chunkX, int chunkZ)
    {
        return TryGetUsedSubChunkCount(chunkX, chunkZ, out int usedSubChunkCount) ? usedSubChunkCount : 0;
    }

    public bool HasSolidBlocksInSubChunk(int chunkX, int subChunkY, int chunkZ)
    {
        return TryGetSubChunkContents(chunkX, subChunkY, chunkZ, out byte contents) &&
               (contents & ChunkColumnData.SolidContentBit) != 0;
    }

    public bool HasFluidInSubChunk(int chunkX, int subChunkY, int chunkZ)
    {
        return TryGetSubChunkContents(chunkX, subChunkY, chunkZ, out byte contents) &&
               (contents & ChunkColumnData.FluidContentBit) != 0;
    }

    public bool HasFoliageInSubChunk(int chunkX, int subChunkY, int chunkZ)
    {
        return TryGetSubChunkContents(chunkX, subChunkY, chunkZ, out byte contents) &&
               (contents & ChunkColumnData.FoliageContentBit) != 0;
    }

    private static int GetUsedSubChunkCount(ChunkColumnData chunk)
    {
        if (chunk.subChunkContents == null)
        {
            return chunk.maxHeight < 0 ? 0 : Mathf.Clamp((chunk.maxHeight / SubChunkSize) + 1, 1, SubChunkCountY);
        }

        for (int subChunkY = SubChunkCountY - 1; subChunkY >= 0; subChunkY--)
        {
            if (chunk.subChunkContents[subChunkY] != 0)
            {
                return subChunkY + 1;
            }
        }

        return 0;
    }

    private bool TryGetSubChunkContents(int chunkX, int subChunkY, int chunkZ, out byte contents)
    {
        contents = 0;
        if (subChunkY < 0 || subChunkY >= SubChunkCountY)
        {
            return false;
        }

        Vector2Int key = new(chunkX, chunkZ);
        if (!_chunkColumns.TryGetValue(key, out ChunkColumnData chunk))
        {
            return false;
        }

        if (chunk.subChunkContents == null || chunk.subChunkContents.Length != SubChunkCountY)
        {
            return false;
        }

        contents = chunk.subChunkContents[subChunkY];
        return true;
    }

    public BlockType GetBlock(int worldX, int worldY, int worldZ)
    {
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return BlockType.Air;
        }

        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        Vector2Int key = new(chunkX, chunkZ);
        if (!_chunkColumns.TryGetValue(key, out ChunkColumnData chunk))
        {
            return BlockType.Air;
        }

        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
        return chunk.blocks[GetIndex(localX, worldY, localZ)];
    }

    public VoxelFluid GetFluid(int worldX, int worldY, int worldZ)
    {
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return VoxelFluid.None;
        }

        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        Vector2Int key = new(chunkX, chunkZ);
        if (!_chunkColumns.TryGetValue(key, out ChunkColumnData chunk))
        {
            return VoxelFluid.None;
        }

        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
        return chunk.fluids[GetIndex(localX, worldY, localZ)];
    }

    public ushort GetFoliageId(int worldX, int worldY, int worldZ)
    {
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return 0;
        }

        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        Vector2Int key = new(chunkX, chunkZ);
        if (!_chunkColumns.TryGetValue(key, out ChunkColumnData chunk))
        {
            return 0;
        }

        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
        return chunk.foliageIds[GetIndex(localX, worldY, localZ)];
    }

    public int GetColumnHeight(int worldX, int worldZ)
    {
        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        Vector2Int key = new(chunkX, chunkZ);
        if (!_chunkColumns.TryGetValue(key, out ChunkColumnData chunk) || chunk.columnHeights == null)
        {
            return -1;
        }

        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
        int index = (localZ * ChunkSize) + localX;
        return index >= 0 && index < chunk.columnHeights.Length ? chunk.columnHeights[index] : -1;
    }

    public bool SetBlock(int worldX, int worldY, int worldZ, BlockType blockType)
    {
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return false;
        }

        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        ChunkColumnData chunk = EnsureChunkColumnReady(chunkX, chunkZ);
        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
        int index = GetIndex(localX, worldY, localZ);
        int columnIndex = (localZ * ChunkSize) + localX;
        int currentColumnHeight = chunk.columnHeights != null && columnIndex < chunk.columnHeights.Length
            ? chunk.columnHeights[columnIndex]
            : -1;
        if (chunk.blocks[index] == blockType)
        {
            return false;
        }

        chunk.blocks[index] = blockType;
        if (blockType != BlockType.Air)
        {
            chunk.fluids[index] = VoxelFluid.None;
            chunk.foliageIds[index] = 0;
            bool removedFoliageAbove = ClearUnsupportedFoliageAbove(chunk, localX, worldY, localZ);
            RecalculateSubChunkContents(chunk, worldY / SubChunkSize);
            if (removedFoliageAbove)
            {
                RecalculateSubChunkContents(chunk, (worldY + 1) / SubChunkSize);
            }

            if (worldY > chunk.maxHeight)
            {
                chunk.maxHeight = worldY;
            }
            if (chunk.columnHeights != null && columnIndex < chunk.columnHeights.Length && worldY > currentColumnHeight)
            {
                chunk.columnHeights[columnIndex] = worldY;
            }
            else if (removedFoliageAbove && chunk.maxHeight <= worldY + 1)
            {
                RecalculateChunkMaxHeight(chunk);
            }

            return true;
        }

        bool removedUnsupportedFoliage = ClearUnsupportedFoliageAbove(chunk, localX, worldY, localZ);
        RecalculateSubChunkContents(chunk, worldY / SubChunkSize);
        if (removedUnsupportedFoliage)
        {
            RecalculateSubChunkContents(chunk, (worldY + 1) / SubChunkSize);
        }

        if (chunk.columnHeights != null && columnIndex < chunk.columnHeights.Length && worldY >= currentColumnHeight)
        {
            chunk.columnHeights[columnIndex] = CalculateColumnSurfaceHeight(chunk, localX, localZ);
        }

        if (worldY == chunk.maxHeight || (removedUnsupportedFoliage && chunk.maxHeight <= worldY + 1))
        {
            RecalculateChunkMaxHeight(chunk);
        }

        return true;
    }

    public bool SetFluid(int worldX, int worldY, int worldZ, VoxelFluid fluid)
    {
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return false;
        }

        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        ChunkColumnData chunk = EnsureChunkColumnReady(chunkX, chunkZ);
        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
        int index = GetIndex(localX, worldY, localZ);

        if (chunk.blocks[index] != BlockType.Air && fluid.Exists)
        {
            return false;
        }

        VoxelFluid current = chunk.fluids[index];
        if (current.fluidId == fluid.fluidId && current.amount == fluid.amount)
        {
            return false;
        }

        chunk.fluids[index] = fluid;
        if (fluid.Exists)
        {
            chunk.foliageIds[index] = 0;
        }
        RecalculateSubChunkContents(chunk, worldY / SubChunkSize);

        if (fluid.Exists)
        {
            if (worldY > chunk.maxHeight)
            {
                chunk.maxHeight = worldY;
            }

            return true;
        }

        if (worldY == chunk.maxHeight)
        {
            RecalculateChunkMaxHeight(chunk);
        }

        return true;
    }

    public bool SetFoliageId(int worldX, int worldY, int worldZ, ushort foliageId)
    {
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return false;
        }

        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        ChunkColumnData chunk = EnsureChunkColumnReady(chunkX, chunkZ);
        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
        int index = GetIndex(localX, worldY, localZ);

        if (chunk.foliageIds[index] == foliageId)
        {
            return false;
        }

        if (foliageId != 0)
        {
            if (chunk.blocks[index] != BlockType.Air || chunk.fluids[index].Exists)
            {
                return false;
            }
        }

        chunk.foliageIds[index] = foliageId;
        RecalculateSubChunkContents(chunk, worldY / SubChunkSize);
        if (foliageId != 0)
        {
            if (worldY > chunk.maxHeight)
            {
                chunk.maxHeight = worldY;
            }

            return true;
        }

        if (worldY == chunk.maxHeight)
        {
            RecalculateChunkMaxHeight(chunk);
        }

        return true;
    }

    public bool IsInBounds(int worldX, int worldY, int worldZ)
    {
        return worldY >= 0 && worldY < WorldHeight;
    }

    private ChunkColumnData EnsureChunkColumnReady(int chunkX, int chunkZ)
    {
        Vector2Int key = new(chunkX, chunkZ);
        if (_chunkColumns.TryGetValue(key, out ChunkColumnData chunk))
        {
            return chunk;
        }

        if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
        {
            RequestChunkColumn(chunkX, chunkZ);
            pending = _pendingChunkColumns[key];
        }

        FinalizePendingChunkColumn(key, pending);
        _pendingChunkColumns.Remove(key);
        _pendingChunkKeys.Remove(key);
        return _chunkColumns[key];
    }

    private void FinalizePendingChunkColumn(Vector2Int key, PendingChunkColumnData pending)
    {
        pending.handle.Complete();

        BlockType[] managedBlocks = new BlockType[pending.blocks.Length];
        pending.blocks.CopyTo(managedBlocks);

        int[] managedColumnHeights = new int[pending.columnHeights.Length];
        pending.columnHeights.CopyTo(managedColumnHeights);

        VoxelFluid[] managedFluids = new VoxelFluid[managedBlocks.Length];
        ushort[] managedFoliageIds = new ushort[managedBlocks.Length];
        byte[] subChunkContents = new byte[SubChunkCountY];
        int maxHeight = -1;

        for (int i = 0; i < pending.columnHeights.Length; i++)
        {
            int columnHeight = managedColumnHeights[i];
            if (columnHeight > maxHeight)
            {
                maxHeight = columnHeight;
            }

            if (columnHeight >= 0)
            {
                int highestSolidSubChunk = math.min(columnHeight / SubChunkSize, SubChunkCountY - 1);
                for (int subChunkY = 0; subChunkY <= highestSolidSubChunk; subChunkY++)
                {
                    subChunkContents[subChunkY] |= ChunkColumnData.SolidContentBit;
                }
            }
        }
        pending.blocks.Dispose();
        pending.columnHeights.Dispose();

        _chunkColumns[key] = new ChunkColumnData(
            managedBlocks,
            managedFluids,
            managedFoliageIds,
            managedColumnHeights,
            subChunkContents,
            maxHeight);
    }

    private static void RecalculateChunkMaxHeight(ChunkColumnData chunk)
    {
        for (int worldY = WorldHeight - 1; worldY >= 0; worldY--)
        {
            for (int localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (int localX = 0; localX < ChunkSize; localX++)
                {
                    int index = GetIndex(localX, worldY, localZ);
                    if (chunk.blocks[index] != BlockType.Air || chunk.fluids[index].Exists || chunk.foliageIds[index] != 0)
                    {
                        chunk.maxHeight = worldY;
                        return;
                    }
                }
            }
        }

        chunk.maxHeight = -1;
    }

    private static void RecalculateSubChunkContents(ChunkColumnData chunk, int subChunkY)
    {
        if (chunk.subChunkContents == null || subChunkY < 0 || subChunkY >= chunk.subChunkContents.Length)
        {
            return;
        }

        byte contents = 0;
        int startY = subChunkY * SubChunkSize;
        int endY = Mathf.Min(startY + SubChunkSize, WorldHeight);
        for (int worldY = startY; worldY < endY; worldY++)
        {
            for (int localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (int localX = 0; localX < ChunkSize; localX++)
                {
                    int index = GetIndex(localX, worldY, localZ);
                    if ((contents & ChunkColumnData.SolidContentBit) == 0 && chunk.blocks[index] != BlockType.Air)
                    {
                        contents |= ChunkColumnData.SolidContentBit;
                    }

                    if ((contents & ChunkColumnData.FluidContentBit) == 0 && chunk.fluids[index].Exists)
                    {
                        contents |= ChunkColumnData.FluidContentBit;
                    }

                    if ((contents & ChunkColumnData.FoliageContentBit) == 0 && chunk.foliageIds[index] != 0)
                    {
                        contents |= ChunkColumnData.FoliageContentBit;
                    }

                    if (contents == (ChunkColumnData.SolidContentBit | ChunkColumnData.FluidContentBit | ChunkColumnData.FoliageContentBit))
                    {
                        chunk.subChunkContents[subChunkY] = contents;
                        return;
                    }
                }
            }
        }

        chunk.subChunkContents[subChunkY] = contents;
    }

    private static int CalculateColumnSurfaceHeight(ChunkColumnData chunk, int localX, int localZ)
    {
        for (int worldY = WorldHeight - 1; worldY >= 0; worldY--)
        {
            if (chunk.blocks[GetIndex(localX, worldY, localZ)] != BlockType.Air)
            {
                return worldY;
            }
        }

        return -1;
    }

    private static bool ClearUnsupportedFoliageAbove(ChunkColumnData chunk, int localX, int worldY, int localZ)
    {
        if (worldY >= WorldHeight - 1)
        {
            return false;
        }

        int aboveIndex = GetIndex(localX, worldY + 1, localZ);
        if (chunk.foliageIds[aboveIndex] == 0)
        {
            return false;
        }

        int supportIndex = GetIndex(localX, worldY, localZ);
        bool hasSupport = chunk.blocks[supportIndex] != BlockType.Air && !chunk.fluids[aboveIndex].Exists;
        if (!hasSupport)
        {
            chunk.foliageIds[aboveIndex] = 0;
            return true;
        }

        return false;
    }

    private int SampleSurfaceHeight(int worldX, int worldZ)
    {
        return WorldGenSampler.SampleSurfaceHeight(
            worldX,
            worldZ,
            _seed,
            _settings,
            _managedContinentalnessCdfLut,
            _managedErosionCdfLut,
            _managedRidgesCdfLut,
            _managedOffsetSplineNodes,
            _managedOffsetSplinePoints,
            _managedFactorSplineNodes,
            _managedFactorSplinePoints,
            _managedJaggednessSplineNodes,
            _managedJaggednessSplinePoints,
            _managedLegacyTerrainHeightSplineNodes,
            _managedLegacyTerrainHeightSplinePoints,
            _managedContinentalnessHeightLut);
    }

    private static int GetIndex(int localX, int worldY, int localZ)
    {
        return ((worldY * ChunkSize) + localZ) * ChunkSize + localX;
    }

    private static int FloorDiv(int value, int divisor)
    {
        if (value >= 0)
        {
            return value / divisor;
        }

        return ((value + 1) / divisor) - 1;
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static NativeArray<float> CreateNativeLut(bool useLut, float[] managedLut)
    {
        if (!useLut || managedLut == null || managedLut.Length <= 1)
        {
            return new NativeArray<float>(0, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        NativeArray<float> nativeLut = new NativeArray<float>(managedLut.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < managedLut.Length; i++)
        {
            nativeLut[i] = managedLut[i];
        }

        return nativeLut;
    }

    private static NativeArray<SplineTreeBakedNode> CreateNativeSplineNodes(SplineTreeBakedNode[] managedNodes)
    {
        if (managedNodes == null || managedNodes.Length == 0)
        {
            return new NativeArray<SplineTreeBakedNode>(0, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        NativeArray<SplineTreeBakedNode> nativeNodes = new NativeArray<SplineTreeBakedNode>(managedNodes.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < managedNodes.Length; i++)
        {
            nativeNodes[i] = managedNodes[i];
        }

        return nativeNodes;
    }

    private static NativeArray<SplineTreeBakedPoint> CreateNativeSplinePoints(SplineTreeBakedPoint[] managedPoints)
    {
        if (managedPoints == null || managedPoints.Length == 0)
        {
            return new NativeArray<SplineTreeBakedPoint>(0, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        NativeArray<SplineTreeBakedPoint> nativePoints = new NativeArray<SplineTreeBakedPoint>(managedPoints.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < managedPoints.Length; i++)
        {
            nativePoints[i] = managedPoints[i];
        }

        return nativePoints;
    }
}
