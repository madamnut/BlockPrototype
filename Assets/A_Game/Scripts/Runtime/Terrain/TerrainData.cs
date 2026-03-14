using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public sealed class TerrainData : IDisposable
{
    public readonly struct WorldGenDebugSample
    {
        public readonly int height;
        public readonly float continentalness;
        public readonly float erosion;
        public readonly float weirdness;
        public readonly float pv;

        public WorldGenDebugSample(
            int height,
            float continentalness,
            float erosion,
            float weirdness,
            float pv)
        {
            this.height = height;
            this.continentalness = continentalness;
            this.erosion = erosion;
            this.weirdness = weirdness;
            this.pv = pv;
        }
    }

    private sealed class ChunkColumnData
    {
        public readonly BlockType[] blocks;
        public readonly VoxelFluid[] fluids;
        public readonly ushort[] foliageIds;
        public int maxHeight;

        public ChunkColumnData(BlockType[] blocks, VoxelFluid[] fluids, ushort[] foliageIds, int maxHeight)
        {
            this.blocks = blocks;
            this.fluids = fluids;
            this.foliageIds = foliageIds;
            this.maxHeight = maxHeight;
        }
    }

    private struct PendingChunkColumnData
    {
        public JobHandle handle;
        public NativeArray<BlockType> blocks;
        public NativeArray<int> columnHeights;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct GenerateChunkColumnJob : IJobParallelFor
    {
        public int chunkX;
        public int chunkZ;
        public int seed;
        public int minTerrainHeight;
        public int seaLevel;
        public int maxTerrainHeight;
        public bool useContinentalnessRemap;
        public bool useErosionRemap;
        public bool useRidgesRemap;
        public ContinentalnessSettings continentalness;
        public ErosionSettings erosion;
        public RidgesSettings ridges;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<BlockType> blocks;
        [ReadOnly] public NativeArray<float> continentalnessCdfLut;
        [ReadOnly] public NativeArray<float> erosionCdfLut;
        [ReadOnly] public NativeArray<float> ridgesCdfLut;
        [ReadOnly] public NativeArray<float> continentalnessFilterLut;
        public NativeArray<int> columnHeights;

        public void Execute(int index)
        {
            int localX = index % ChunkSize;
            int localZ = index / ChunkSize;
            int worldX = (chunkX * ChunkSize) + localX;
            int worldZ = (chunkZ * ChunkSize) + localZ;
            int terrainHeight = SampleSurfaceHeight(worldX, worldZ);

            for (int worldY = 0; worldY <= terrainHeight; worldY++)
            {
                blocks[GetIndex(localX, worldY, localZ)] = worldY == terrainHeight
                    ? BlockType.Grass
                    : BlockType.Rock;
            }

            columnHeights[index] = terrainHeight;
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
            continentalnessValue = ApplyBakedFilter(continentalnessValue, continentalnessFilterLut);
            return ComposeSurfaceHeight(
                continentalnessValue,
                minTerrainHeight,
                seaLevel,
                maxTerrainHeight,
                WorldHeight);
        }

        private static int ComposeSurfaceHeight(
            float continentalnessValue,
            int minHeight,
            int seaLevel,
            int maxHeight,
            int worldHeight)
        {
            int clampedMin = math.clamp(minHeight, 0, worldHeight - 1);
            int clampedSeaLevel = math.clamp(seaLevel, 0, worldHeight - 1);
            int clampedMax = math.clamp(math.max(seaLevel, maxHeight), 0, worldHeight - 1);

            int baseHeight;
            if (continentalnessValue < 0f)
            {
                float oceanT = math.saturate(continentalnessValue + 1f);
                baseHeight = math.clamp((int)math.round(math.lerp(clampedMin, clampedSeaLevel, oceanT)), 0, worldHeight - 1);
            }
            else
            {
                float landT = math.saturate(continentalnessValue);
                baseHeight = math.clamp((int)math.round(math.lerp(clampedSeaLevel, clampedMax, landT)), 0, worldHeight - 1);
            }

            return baseHeight;
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

        private static float ApplyBakedFilter(float value, NativeArray<float> bakedLut)
        {
            if (!bakedLut.IsCreated || bakedLut.Length <= 1)
            {
                return value;
            }

            float normalized = (math.clamp(value, -1f, 1f) + 1f) * 0.5f;
            float scaledIndex = normalized * (bakedLut.Length - 1);
            int lowerIndex = (int)math.floor(scaledIndex);
            int upperIndex = math.min(lowerIndex + 1, bakedLut.Length - 1);
            float t = scaledIndex - lowerIndex;
            return math.clamp(math.lerp(bakedLut[lowerIndex], bakedLut[upperIndex], t), -1f, 1f);
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
    private readonly float[] _managedContinentalnessFilterLut;
    private NativeArray<float> _continentalnessCdfLut;
    private NativeArray<float> _erosionCdfLut;
    private NativeArray<float> _ridgesCdfLut;
    private NativeArray<float> _continentalnessFilterLut;

    public TerrainData(
        int seed,
        TerrainGenerationSettings settings,
        float[] continentalnessCdfLut = null,
        float[] erosionCdfLut = null,
        float[] ridgesCdfLut = null,
        float[] continentalnessFilterLut = null)
    {
        _seed = seed;
        _settings = settings;
        _managedContinentalnessCdfLut = continentalnessCdfLut;
        _managedErosionCdfLut = erosionCdfLut;
        _managedRidgesCdfLut = ridgesCdfLut;
        _managedContinentalnessFilterLut = continentalnessFilterLut;
        _continentalnessCdfLut = CreateNativeLut(settings.useContinentalnessRemap, continentalnessCdfLut);
        _erosionCdfLut = CreateNativeLut(settings.useErosionRemap, erosionCdfLut);
        _ridgesCdfLut = CreateNativeLut(settings.useRidgesRemap, ridgesCdfLut);
        _continentalnessFilterLut = CreateNativeLut(continentalnessFilterLut != null && continentalnessFilterLut.Length > 1, continentalnessFilterLut);
    }

    public int SeaLevel => _settings.seaLevel;

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
            _managedContinentalnessFilterLut);
        float continentalness = WorldGenSampler.SampleContinentalness(worldX, worldZ, _seed, _settings, _managedContinentalnessCdfLut);
        float erosion = WorldGenSampler.SampleErosion(worldX, worldZ, _seed, _settings, _managedErosionCdfLut);
        float weirdness = WorldGenSampler.SampleWeirdness(worldX, worldZ, _seed, _settings, _managedRidgesCdfLut);
        float pv = WorldGenSampler.SamplePv(worldX, worldZ, _seed, _settings, _managedRidgesCdfLut);

        return new WorldGenDebugSample(
            height,
            continentalness,
            erosion,
            weirdness,
            pv);
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

        if (_continentalnessFilterLut.IsCreated)
        {
            _continentalnessFilterLut.Dispose();
        }
    }

    public void RequestChunkColumn(int chunkX, int chunkZ)
    {
        Vector2Int key = new(chunkX, chunkZ);
        if (_chunkColumns.ContainsKey(key) || _pendingChunkColumns.ContainsKey(key))
        {
            return;
        }

        PendingChunkColumnData pending = new()
        {
            blocks = new NativeArray<BlockType>(ChunkSize * WorldHeight * ChunkSize, Allocator.Persistent, NativeArrayOptions.ClearMemory),
            columnHeights = new NativeArray<int>(ChunkSize * ChunkSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };

        GenerateChunkColumnJob job = new()
        {
            chunkX = chunkX,
            chunkZ = chunkZ,
            seed = _seed,
            minTerrainHeight = _settings.minTerrainHeight,
            seaLevel = _settings.seaLevel,
            maxTerrainHeight = _settings.maxTerrainHeight,
            useContinentalnessRemap = _settings.useContinentalnessRemap,
            useErosionRemap = _settings.useErosionRemap,
            useRidgesRemap = _settings.useRidgesRemap,
            continentalness = _settings.continentalness,
            erosion = _settings.erosion,
            ridges = _settings.ridges,
            blocks = pending.blocks,
            continentalnessCdfLut = _continentalnessCdfLut,
            erosionCdfLut = _erosionCdfLut,
            ridgesCdfLut = _ridgesCdfLut,
            continentalnessFilterLut = _continentalnessFilterLut,
            columnHeights = pending.columnHeights,
        };

        pending.handle = job.Schedule(ChunkSize * ChunkSize, ChunkSize);
        _pendingChunkColumns.Add(key, pending);
        _pendingChunkKeys.Add(key);
    }

    public int CompletePendingChunkColumns(List<Vector2Int> completedChunkCoords, int maxCompletions)
    {
        int completedCount = 0;
        for (int i = 0; i < _pendingChunkKeys.Count && completedCount < maxCompletions; i++)
        {
            Vector2Int key = _pendingChunkKeys[i];
            if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
            {
                continue;
            }

            if (!pending.handle.IsCompleted)
            {
                continue;
            }

            FinalizePendingChunkColumn(key, pending);
            _pendingChunkColumns.Remove(key);
            _pendingChunkKeys.RemoveAt(i);
            i--;

            completedChunkCoords?.Add(key);
            completedCount++;
        }

        return completedCount;
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

        if (chunk.maxHeight < 0)
        {
            usedSubChunkCount = 0;
            return true;
        }

        usedSubChunkCount = Mathf.Clamp((chunk.maxHeight / SubChunkSize) + 1, 1, SubChunkCountY);
        return true;
    }

    public int GetUsedSubChunkCount(int chunkX, int chunkZ)
    {
        return TryGetUsedSubChunkCount(chunkX, chunkZ, out int usedSubChunkCount) ? usedSubChunkCount : 0;
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
            if (worldY > chunk.maxHeight)
            {
                chunk.maxHeight = worldY;
            }
            else if (removedFoliageAbove && chunk.maxHeight <= worldY + 1)
            {
                RecalculateChunkMaxHeight(chunk);
            }

            return true;
        }

        bool removedUnsupportedFoliage = ClearUnsupportedFoliageAbove(chunk, localX, worldY, localZ);

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

    public bool HasSolidBlocksInSubChunk(int chunkX, int subChunkY, int chunkZ)
    {
        if (subChunkY < 0 || subChunkY >= SubChunkCountY)
        {
            return false;
        }

        if (!_chunkColumns.TryGetValue(new Vector2Int(chunkX, chunkZ), out ChunkColumnData chunk))
        {
            return false;
        }

        if (chunk.maxHeight < 0)
        {
            return false;
        }

        int startY = subChunkY * SubChunkSize;
        if (chunk.maxHeight < startY)
        {
            return false;
        }

        int endY = Mathf.Min(startY + SubChunkSize, WorldHeight);
        for (int worldY = startY; worldY < endY; worldY++)
        {
            for (int localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (int localX = 0; localX < ChunkSize; localX++)
                {
                    if (chunk.blocks[GetIndex(localX, worldY, localZ)] != BlockType.Air)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public bool HasFluidInSubChunk(int chunkX, int subChunkY, int chunkZ)
    {
        if (subChunkY < 0 || subChunkY >= SubChunkCountY)
        {
            return false;
        }

        if (!_chunkColumns.TryGetValue(new Vector2Int(chunkX, chunkZ), out ChunkColumnData chunk))
        {
            return false;
        }

        if (chunk.maxHeight < 0)
        {
            return false;
        }

        int startY = subChunkY * SubChunkSize;
        if (chunk.maxHeight < startY)
        {
            return false;
        }

        int endY = Mathf.Min(startY + SubChunkSize, WorldHeight);
        for (int worldY = startY; worldY < endY; worldY++)
        {
            for (int localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (int localX = 0; localX < ChunkSize; localX++)
                {
                    if (chunk.fluids[GetIndex(localX, worldY, localZ)].Exists)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public bool HasFoliageInSubChunk(int chunkX, int subChunkY, int chunkZ)
    {
        if (subChunkY < 0 || subChunkY >= SubChunkCountY)
        {
            return false;
        }

        if (!_chunkColumns.TryGetValue(new Vector2Int(chunkX, chunkZ), out ChunkColumnData chunk))
        {
            return false;
        }

        if (chunk.maxHeight < 0)
        {
            return false;
        }

        int startY = subChunkY * SubChunkSize;
        if (chunk.maxHeight < startY)
        {
            return false;
        }

        int endY = Mathf.Min(startY + SubChunkSize, WorldHeight);
        for (int worldY = startY; worldY < endY; worldY++)
        {
            for (int localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (int localX = 0; localX < ChunkSize; localX++)
                {
                    if (chunk.foliageIds[GetIndex(localX, worldY, localZ)] != 0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
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

        BlockType[] managedBlocks = pending.blocks.ToArray();
        VoxelFluid[] managedFluids = new VoxelFluid[managedBlocks.Length];
        ushort[] managedFoliageIds = new ushort[managedBlocks.Length];
        int maxHeight = -1;
        for (int i = 0; i < pending.columnHeights.Length; i++)
        {
            if (pending.columnHeights[i] > maxHeight)
            {
                maxHeight = pending.columnHeights[i];
            }
        }

        if (_settings.seaLevel > 0)
        {
            for (int localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (int localX = 0; localX < ChunkSize; localX++)
                {
                    int columnIndex = (localZ * ChunkSize) + localX;
                    int surfaceHeight = pending.columnHeights[columnIndex];
                    int waterStartY = surfaceHeight + 1;
                    int waterEndY = Mathf.Min(_settings.seaLevel, WorldHeight);

                    for (int worldY = waterStartY; worldY < waterEndY; worldY++)
                    {
                        int index = GetIndex(localX, worldY, localZ);
                        if (managedBlocks[index] != BlockType.Air)
                        {
                            continue;
                        }

                        managedFluids[index] = VoxelFluid.Water(100);
                        if (worldY > maxHeight)
                        {
                            maxHeight = worldY;
                        }
                    }
                }
            }
        }

        pending.blocks.Dispose();
        pending.columnHeights.Dispose();

        _chunkColumns[key] = new ChunkColumnData(managedBlocks, managedFluids, managedFoliageIds, maxHeight);
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
            _managedContinentalnessFilterLut);
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
}
