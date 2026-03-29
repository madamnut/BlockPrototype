using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public sealed class TileableNoiseChunkGenerator : IDisposable
{
    public sealed class PendingChunkColumnGeneration : IDisposable
    {
        public int chunkX;
        public int chunkZ;
        public readonly NativeArray<float> sampledSurfaceHeights;
        public readonly NativeArray<BlockType> blocks;
        public readonly NativeArray<VoxelFluid> fluids;
        public readonly NativeArray<int> columnHeights;
        public readonly NativeArray<byte> subChunkContents;
        public readonly NativeArray<int> maxHeight;

        public JobHandle handle;

        public PendingChunkColumnGeneration(int chunkX, int chunkZ, int sampleCountPerAxis)
        {
            this.chunkX = chunkX;
            this.chunkZ = chunkZ;
            sampledSurfaceHeights = new NativeArray<float>(sampleCountPerAxis * sampleCountPerAxis, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            blocks = new NativeArray<BlockType>(TerrainData.ChunkSize * TerrainData.WorldHeight * TerrainData.ChunkSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            fluids = new NativeArray<VoxelFluid>(TerrainData.ChunkSize * TerrainData.WorldHeight * TerrainData.ChunkSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            columnHeights = new NativeArray<int>(TerrainData.ChunkSize * TerrainData.ChunkSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            subChunkContents = new NativeArray<byte>(TerrainData.SubChunkCountY, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            maxHeight = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            handle = default;
        }

        public bool IsCompleted => handle.IsCompleted;

        public void Complete()
        {
            handle.Complete();
        }

        public void PrepareForSchedule(int nextChunkX, int nextChunkZ)
        {
            Complete();
            chunkX = nextChunkX;
            chunkZ = nextChunkZ;
            handle = default;
            ClearNativeArray(blocks);
            ClearNativeArray(fluids);
            ClearNativeArray(columnHeights);
            ClearNativeArray(subChunkContents);
            ClearNativeArray(maxHeight);
        }

        public void Dispose()
        {
            Complete();
            if (sampledSurfaceHeights.IsCreated)
            {
                sampledSurfaceHeights.Dispose();
            }

            if (blocks.IsCreated)
            {
                blocks.Dispose();
            }

            if (fluids.IsCreated)
            {
                fluids.Dispose();
            }

            if (columnHeights.IsCreated)
            {
                columnHeights.Dispose();
            }

            if (subChunkContents.IsCreated)
            {
                subChunkContents.Dispose();
            }

            if (maxHeight.IsCreated)
            {
                maxHeight.Dispose();
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct FillChunkColumnJob : IJob
    {
        public GndJobData gndSampler;
        public ReliefJobData reliefSampler;
        public VeinJobData veinSampler;
        public int chunkWorldX;
        public int chunkWorldZ;
        public int sampleSpacing;
        public int sampleCountPerAxis;
        public int seaLevel;
        [ReadOnly] public NativeArray<float> nestedBaseHeightLut;
        public int nestedBaseHeightResolution;

        public NativeArray<float> sampledSurfaceHeights;
        public NativeArray<BlockType> blocks;
        public NativeArray<VoxelFluid> fluids;
        public NativeArray<int> columnHeights;
        public NativeArray<byte> subChunkContents;
        public NativeArray<int> maxHeightOut;

        public void Execute()
        {
            BuildGlobalSurfaceSamples();

            int maxHeight = -1;
            for (int localZ = 0; localZ < TerrainData.ChunkSize; localZ++)
            {
                for (int localX = 0; localX < TerrainData.ChunkSize; localX++)
                {
                    int surfaceHeight = EvaluateInterpolatedSurfaceHeight(sampledSurfaceHeights, localX, localZ, sampleSpacing, sampleCountPerAxis);
                    int columnIndex = (localZ * TerrainData.ChunkSize) + localX;
                    columnHeights[columnIndex] = surfaceHeight;
                    maxHeight = math.max(maxHeight, surfaceHeight);

                    for (int worldY = 0; worldY <= surfaceHeight; worldY++)
                    {
                        blocks[GetIndex(localX, worldY, localZ)] = BlockType.Rock;
                    }

                    ApplySurfaceLayers(localX, localZ, surfaceHeight, blocks);
                    MarkSubChunkRange(subChunkContents, 0, surfaceHeight, ChunkColumnContentBits.Solid);

                    if (surfaceHeight < seaLevel)
                    {
                        for (int worldY = surfaceHeight + 1; worldY <= seaLevel; worldY++)
                        {
                            fluids[GetIndex(localX, worldY, localZ)] = new VoxelFluid
                            {
                                fluidId = (byte)VoxelFluidType.Water,
                                amount = byte.MaxValue,
                            };
                        }

                        MarkSubChunkRange(subChunkContents, surfaceHeight + 1, seaLevel, ChunkColumnContentBits.Fluid);
                        maxHeight = math.max(maxHeight, seaLevel);
                    }
                }
            }

            maxHeightOut[0] = maxHeight;
        }

        private void BuildGlobalSurfaceSamples()
        {
            for (int sampleZ = 0; sampleZ < sampleCountPerAxis; sampleZ++)
            {
                int sampleWorldZ = chunkWorldZ + (sampleZ * sampleSpacing);
                for (int sampleX = 0; sampleX < sampleCountPerAxis; sampleX++)
                {
                    int sampleWorldX = chunkWorldX + (sampleX * sampleSpacing);
                    float gnd = GndBurstUtility.SampleGnd(gndSampler, sampleWorldX, sampleWorldZ);
                    float relief = ClimateBurstUtility.SampleRelief(reliefSampler, sampleWorldX, sampleWorldZ);
                    float veinFold = ClimateBurstUtility.FoldVein(ClimateBurstUtility.SampleVein(veinSampler, sampleWorldX, sampleWorldZ));
                    sampledSurfaceHeights[GetSampleIndex(sampleX, sampleZ, sampleCountPerAxis)] =
                        EvaluateLiftHeight(
                            gnd,
                            relief,
                            veinFold,
                            nestedBaseHeightLut,
                            nestedBaseHeightResolution);
                }
            }
        }

        private static void ApplySurfaceLayers(int localX, int localZ, int surfaceHeight, NativeArray<BlockType> blocks)
        {
            if (surfaceHeight < 0)
            {
                return;
            }

            blocks[GetIndex(localX, surfaceHeight, localZ)] = BlockType.Grass;

            int dirtStartY = math.max(0, surfaceHeight - 3);
            for (int worldY = dirtStartY; worldY < surfaceHeight; worldY++)
            {
                blocks[GetIndex(localX, worldY, localZ)] = BlockType.Dirt;
            }
        }

        private static float EvaluateLiftHeight(
            float gnd,
            float relief,
            float veinFold,
            NativeArray<float> nestedBaseLut,
            int nestedBaseResolution)
        {
            if (nestedBaseResolution >= 2 && nestedBaseLut.IsCreated)
            {
                return NestedSplineGraphUtility.EvaluateLut3D(nestedBaseLut, nestedBaseResolution, gnd, relief, veinFold);
            }

            return math.lerp(40f, 196f, math.saturate((gnd + 1f) * 0.5f));
        }
    }

    private readonly GndSampler _gndSampler;
    private readonly ReliefSampler _reliefSampler;
    private readonly VeinSampler _veinSampler;
    private readonly NativeArray<float> _nestedBaseHeightLut;
    private readonly int _nestedBaseHeightResolution;
    private readonly Stack<PendingChunkColumnGeneration> _pendingGenerationPool = new();

    public TileableNoiseChunkGenerator(int seed)
        : this(seed, GndRuntimeSettings.CreateDefault(), ReliefRuntimeSettings.CreateDefault(), VeinRuntimeSettings.CreateDefault(), LiftRuntimeSettings.CreateDefault())
    {
    }

    public TileableNoiseChunkGenerator(int seed, GndRuntimeSettings settings)
        : this(seed, settings, ReliefRuntimeSettings.CreateDefault(), VeinRuntimeSettings.CreateDefault(), LiftRuntimeSettings.CreateDefault())
    {
    }

    public TileableNoiseChunkGenerator(
        int seed,
        GndRuntimeSettings gndSettings,
        ReliefRuntimeSettings reliefSettings,
        VeinRuntimeSettings veinSettings,
        LiftRuntimeSettings liftSettings)
    {
        _gndSampler = new GndSampler(gndSettings, seed);
        _reliefSampler = new ReliefSampler(reliefSettings, seed);
        _veinSampler = new VeinSampler(veinSettings, seed);
        _nestedBaseHeightResolution = liftSettings.nestedBaseHeightResolution;
        if (liftSettings.nestedBaseHeightLut != null && liftSettings.nestedBaseHeightResolution >= 2)
        {
            _nestedBaseHeightLut = new NativeArray<float>(liftSettings.nestedBaseHeightLut, Allocator.Persistent);
        }
    }

    public int GenerateChunkColumn(int chunkX, int chunkZ, BlockType[] blocks, VoxelFluid[] fluids, int[] columnHeights, byte[] subChunkContents)
    {
        PendingChunkColumnGeneration pending = ScheduleChunkColumn(chunkX, chunkZ);
        try
        {
            return CompleteChunkColumn(pending, blocks, fluids, columnHeights, subChunkContents);
        }
        finally
        {
            ReturnPendingChunkColumnGeneration(pending);
        }
    }

    public PendingChunkColumnGeneration ScheduleChunkColumn(int chunkX, int chunkZ)
    {
        int sampleSpacing = _gndSampler.SampleSpacing;
        int seaLevel = _gndSampler.SeaLevel;
        int sampleCountPerAxis = Mathf.CeilToInt(TerrainData.ChunkSize / (float)sampleSpacing) + 1;
        int chunkWorldX = chunkX * TerrainData.ChunkSize;
        int chunkWorldZ = chunkZ * TerrainData.ChunkSize;
        PendingChunkColumnGeneration pending = RentPendingChunkColumnGeneration(chunkX, chunkZ, sampleCountPerAxis);
        FillChunkColumnJob fillJob = new()
        {
            gndSampler = _gndSampler.JobData,
            reliefSampler = _reliefSampler.JobData,
            veinSampler = _veinSampler.JobData,
            chunkWorldX = chunkWorldX,
            chunkWorldZ = chunkWorldZ,
            sampleSpacing = sampleSpacing,
            sampleCountPerAxis = sampleCountPerAxis,
            seaLevel = seaLevel,
            nestedBaseHeightLut = _nestedBaseHeightLut,
            nestedBaseHeightResolution = _nestedBaseHeightResolution,
            sampledSurfaceHeights = pending.sampledSurfaceHeights,
            blocks = pending.blocks,
            fluids = pending.fluids,
            columnHeights = pending.columnHeights,
            subChunkContents = pending.subChunkContents,
            maxHeightOut = pending.maxHeight,
        };
        pending.handle = fillJob.Schedule();
        return pending;
    }

    public int CompleteChunkColumn(
        PendingChunkColumnGeneration pending,
        BlockType[] blocks,
        VoxelFluid[] fluids,
        int[] columnHeights,
        byte[] subChunkContents)
    {
        pending.Complete();
        pending.blocks.CopyTo(blocks);
        pending.fluids.CopyTo(fluids);
        pending.columnHeights.CopyTo(columnHeights);
        pending.subChunkContents.CopyTo(subChunkContents);
        return pending.maxHeight[0];
    }

    public void ReturnPendingChunkColumnGeneration(PendingChunkColumnGeneration pending)
    {
        if (pending == null)
        {
            return;
        }

        pending.Complete();
        pending.handle = default;
        _pendingGenerationPool.Push(pending);
    }

    public float SampleGnd(int worldX, int worldZ)
    {
        return _gndSampler.SampleGnd(worldX, worldZ);
    }

    private static int EvaluateInterpolatedSurfaceHeight(float[] sampledSurfaceHeights, int localX, int localZ, int sampleSpacing, int sampleCountPerAxis)
    {
        int cellX = localX / sampleSpacing;
        int cellZ = localZ / sampleSpacing;
        float tx = (localX - (cellX * sampleSpacing)) / (float)sampleSpacing;
        float tz = (localZ - (cellZ * sampleSpacing)) / (float)sampleSpacing;

        float h00 = sampledSurfaceHeights[GetSampleIndex(cellX, cellZ, sampleCountPerAxis)];
        float h10 = sampledSurfaceHeights[GetSampleIndex(cellX + 1, cellZ, sampleCountPerAxis)];
        float h01 = sampledSurfaceHeights[GetSampleIndex(cellX, cellZ + 1, sampleCountPerAxis)];
        float h11 = sampledSurfaceHeights[GetSampleIndex(cellX + 1, cellZ + 1, sampleCountPerAxis)];
        float x0 = Mathf.Lerp(h00, h10, tx);
        float x1 = Mathf.Lerp(h01, h11, tx);
        float surfaceHeight = Mathf.Lerp(x0, x1, tz);
        return Mathf.Clamp(Mathf.FloorToInt(surfaceHeight), 0, TerrainData.WorldHeight - 1);
    }

    private static int EvaluateInterpolatedSurfaceHeight(NativeArray<float> sampledSurfaceHeights, int localX, int localZ, int sampleSpacing, int sampleCountPerAxis)
    {
        int cellX = localX / sampleSpacing;
        int cellZ = localZ / sampleSpacing;
        float tx = (localX - (cellX * sampleSpacing)) / (float)sampleSpacing;
        float tz = (localZ - (cellZ * sampleSpacing)) / (float)sampleSpacing;

        float h00 = sampledSurfaceHeights[GetSampleIndex(cellX, cellZ, sampleCountPerAxis)];
        float h10 = sampledSurfaceHeights[GetSampleIndex(cellX + 1, cellZ, sampleCountPerAxis)];
        float h01 = sampledSurfaceHeights[GetSampleIndex(cellX, cellZ + 1, sampleCountPerAxis)];
        float h11 = sampledSurfaceHeights[GetSampleIndex(cellX + 1, cellZ + 1, sampleCountPerAxis)];
        float x0 = math.lerp(h00, h10, tx);
        float x1 = math.lerp(h01, h11, tx);
        float surfaceHeight = math.lerp(x0, x1, tz);
        return math.clamp((int)math.floor(surfaceHeight), 0, TerrainData.WorldHeight - 1);
    }

    private static int GetSampleIndex(int sampleX, int sampleZ, int sampleCountPerAxis)
    {
        return (sampleZ * sampleCountPerAxis) + sampleX;
    }

    private static int GetIndex(int localX, int worldY, int localZ)
    {
        return ((worldY * TerrainData.ChunkSize) + localZ) * TerrainData.ChunkSize + localX;
    }

    private static void MarkSubChunkRange(byte[] subChunkContents, int startY, int endY, byte contentBit)
    {
        if (subChunkContents == null || contentBit == 0 || startY > endY)
        {
            return;
        }

        int clampedStartY = Mathf.Clamp(startY, 0, TerrainData.WorldHeight - 1);
        int clampedEndY = Mathf.Clamp(endY, 0, TerrainData.WorldHeight - 1);
        if (clampedStartY > clampedEndY)
        {
            return;
        }

        int startSubChunkY = clampedStartY / TerrainData.SubChunkSize;
        int endSubChunkY = clampedEndY / TerrainData.SubChunkSize;
        for (int subChunkY = startSubChunkY; subChunkY <= endSubChunkY; subChunkY++)
        {
            subChunkContents[subChunkY] |= contentBit;
        }
    }

    private static void MarkSubChunkRange(NativeArray<byte> subChunkContents, int startY, int endY, byte contentBit)
    {
        if (!subChunkContents.IsCreated || contentBit == 0 || startY > endY)
        {
            return;
        }

        int clampedStartY = math.clamp(startY, 0, TerrainData.WorldHeight - 1);
        int clampedEndY = math.clamp(endY, 0, TerrainData.WorldHeight - 1);
        if (clampedStartY > clampedEndY)
        {
            return;
        }

        int startSubChunkY = clampedStartY / TerrainData.SubChunkSize;
        int endSubChunkY = clampedEndY / TerrainData.SubChunkSize;
        for (int subChunkY = startSubChunkY; subChunkY <= endSubChunkY; subChunkY++)
        {
            subChunkContents[subChunkY] = (byte)(subChunkContents[subChunkY] | contentBit);
        }
    }

    private static class ChunkColumnContentBits
    {
        public const byte Solid = 1 << 0;
        public const byte Fluid = 1 << 1;
    }

    public void Dispose()
    {
        while (_pendingGenerationPool.Count > 0)
        {
            _pendingGenerationPool.Pop().Dispose();
        }

        _gndSampler.Dispose();
        _reliefSampler.Dispose();
        _veinSampler.Dispose();

        if (_nestedBaseHeightLut.IsCreated)
        {
            _nestedBaseHeightLut.Dispose();
        }
    }

    private PendingChunkColumnGeneration RentPendingChunkColumnGeneration(int chunkX, int chunkZ, int sampleCountPerAxis)
    {
        int sampleBufferLength = sampleCountPerAxis * sampleCountPerAxis;
        while (_pendingGenerationPool.Count > 0)
        {
            PendingChunkColumnGeneration pooled = _pendingGenerationPool.Pop();
            if (!pooled.sampledSurfaceHeights.IsCreated || pooled.sampledSurfaceHeights.Length != sampleBufferLength)
            {
                pooled.Dispose();
                continue;
            }

            pooled.PrepareForSchedule(chunkX, chunkZ);
            return pooled;
        }

        return new PendingChunkColumnGeneration(chunkX, chunkZ, sampleCountPerAxis);
    }

    private static void ClearNativeArray<T>(NativeArray<T> array) where T : struct
    {
        if (!array.IsCreated || array.Length == 0)
        {
            return;
        }

        T defaultValue = default;
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = defaultValue;
        }
    }
}
