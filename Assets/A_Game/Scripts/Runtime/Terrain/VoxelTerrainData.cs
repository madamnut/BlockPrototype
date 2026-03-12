using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public sealed class VoxelTerrainData : IDisposable
{
    public readonly struct WorldGenDebugSample
    {
        public readonly int height;
        public readonly float continentalness;

        public WorldGenDebugSample(
            int height,
            float continentalness)
        {
            this.height = height;
            this.continentalness = continentalness;
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

    private struct FractalNoiseJobSettings
    {
        public float scale;
        public int octaves;
        public float persistence;
        public float lacunarity;
        public float offsetX;
        public float offsetY;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct GenerateChunkColumnJob : IJob
    {
        public int chunkX;
        public int chunkZ;
        public int seed;
        public int minTerrainHeight;
        public int maxTerrainHeight;
        public float continentalnessSeaLevel;
        public float continentalnessWeight;
        public float continentalnessWarpScaleMultiplier;
        public float continentalnessWarpStrength;
        public float continentalnessDetailScaleMultiplier;
        public float continentalnessDetailWeight;
        public float continentalnessRemapMin;
        public float continentalnessRemapMax;
        public FractalNoiseJobSettings continentalness;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<BlockType> blocks;
        public NativeArray<int> columnHeights;

        public void Execute()
        {
            for (int localZ = 0; localZ < ChunkSize; localZ++)
            {
                for (int localX = 0; localX < ChunkSize; localX++)
                {
                    int worldX = (chunkX * ChunkSize) + localX;
                    int worldZ = (chunkZ * ChunkSize) + localZ;
                    int terrainHeight = SampleSurfaceHeight(worldX, worldZ);

                    for (int worldY = 0; worldY <= terrainHeight; worldY++)
                    {
                        blocks[GetIndex(localX, worldY, localZ)] = BlockType.Rock;
                    }

                    columnHeights[(localZ * ChunkSize) + localX] = terrainHeight;
                }
            }
        }

        private int SampleSurfaceHeight(int worldX, int worldZ)
        {
            float seedOffset = seed * 0.000031f;
            ApplyContinentalnessWarp(ref worldX, ref worldZ, seedOffset);
            float continentalnessValue = SampleFractalPerlin(worldX, worldZ, continentalness, seedOffset);
            FractalNoiseJobSettings detailSettings = continentalness;
            detailSettings.scale *= math.max(1f, continentalnessDetailScaleMultiplier);
            detailSettings.offsetX += 173.31f;
            detailSettings.offsetY += 91.77f;
            float continentalnessDetailValue = SampleFractalPerlin(worldX, worldZ, detailSettings, seedOffset);
            float shapedContinentalness = ShapeContinentalness(
                continentalnessValue,
                continentalnessDetailValue,
                continentalnessDetailWeight,
                continentalnessRemapMin,
                continentalnessRemapMax);
            return ComposeSurfaceHeight(
                shapedContinentalness,
                minTerrainHeight,
                maxTerrainHeight,
                continentalnessSeaLevel,
                continentalnessWeight);
        }

        private static int ComposeSurfaceHeight(
            float continentalnessValue,
            int minHeight,
            int maxHeight,
            float seaLevelThreshold,
            float continentalnessMultiplier)
        {
            float landness = math.saturate(math.unlerp(seaLevelThreshold, 1f, continentalnessValue));
            float height01 = math.saturate(landness * continentalnessMultiplier);
            int clampedMin = math.clamp(minHeight, 0, WorldHeight - 1);
            int clampedMax = math.clamp(math.max(minHeight, maxHeight), 0, WorldHeight - 1);
            return math.clamp((int)math.round(math.lerp(clampedMin, clampedMax, height01)), 0, WorldHeight - 1);
        }

        private void ApplyContinentalnessWarp(ref int worldX, ref int worldZ, float seedOffset)
        {
            float warpScale = continentalness.scale * math.clamp(continentalnessWarpScaleMultiplier, 0.1f, 8f);
            float warpStrength = math.max(0f, continentalnessWarpStrength);
            if (warpStrength <= 0.0001f)
            {
                return;
            }

            float warpX = SampleFractalPerlin(worldX, worldZ, new FractalNoiseJobSettings
            {
                scale = warpScale,
                octaves = continentalness.octaves,
                persistence = continentalness.persistence,
                lacunarity = continentalness.lacunarity,
                offsetX = continentalness.offsetX + 401.17f,
                offsetY = continentalness.offsetY + 233.91f,
            }, seedOffset);
            float warpZ = SampleFractalPerlin(worldX, worldZ, new FractalNoiseJobSettings
            {
                scale = warpScale,
                octaves = continentalness.octaves,
                persistence = continentalness.persistence,
                lacunarity = continentalness.lacunarity,
                offsetX = continentalness.offsetX + 719.43f,
                offsetY = continentalness.offsetY + 587.29f,
            }, seedOffset);

            worldX = (int)math.round(worldX + (((warpX - 0.5f) * 2f) * warpStrength));
            worldZ = (int)math.round(worldZ + (((warpZ - 0.5f) * 2f) * warpStrength));
        }

        private static float ShapeContinentalness(
            float baseContinentalness,
            float detailContinentalness,
            float detailWeight,
            float remapMin,
            float remapMax)
        {
            float baseRemapped = math.saturate(math.unlerp(remapMin, remapMax, baseContinentalness));
            float baseSmoothed = baseRemapped * baseRemapped * (3f - (2f * baseRemapped));

            float coastMask = 1f - math.abs((baseSmoothed * 2f) - 1f);
            float detailSigned = (detailContinentalness - 0.5f) * 2f;
            float coastAdjusted = math.saturate(baseSmoothed + (detailSigned * math.saturate(detailWeight) * coastMask));

            float centered = (coastAdjusted * 2f) - 1f;
            float contrasted = math.sign(centered) * math.pow(math.abs(centered), 0.72f);
            return math.saturate((contrasted * 0.5f) + 0.5f);
        }

        private static float SampleFractalPerlin(int worldX, int worldZ, FractalNoiseJobSettings settings, float seedOffset)
        {
            int octaveCount = math.max(1, settings.octaves);
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float amplitudeSum = 0f;
            float safeScale = math.max(0.00001f, settings.scale);
            float persistence = math.saturate(settings.persistence);
            float lacunarity = math.max(1f, settings.lacunarity);

            for (int octave = 0; octave < octaveCount; octave++)
            {
                float sampleX = (worldX * safeScale * frequency) + settings.offsetX + seedOffset;
                float sampleZ = (worldZ * safeScale * frequency) + settings.offsetY + seedOffset;
                total += SamplePerlin2D(sampleX, sampleZ) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return amplitudeSum <= 0f ? 0.5f : total / amplitudeSum;
        }

        private static float SamplePerlin2D(float x, float z)
        {
            float cellX = math.floor(x);
            float cellZ = math.floor(z);
            float localX = math.frac(x);
            float localZ = math.frac(z);

            Gradient(cellX, cellZ, out float g00x, out float g00z);
            Gradient(cellX + 1f, cellZ, out float g10x, out float g10z);
            Gradient(cellX, cellZ + 1f, out float g01x, out float g01z);
            Gradient(cellX + 1f, cellZ + 1f, out float g11x, out float g11z);

            float n00 = (g00x * localX) + (g00z * localZ);
            float n10 = (g10x * (localX - 1f)) + (g10z * localZ);
            float n01 = (g01x * localX) + (g01z * (localZ - 1f));
            float n11 = (g11x * (localX - 1f)) + (g11z * (localZ - 1f));

            float fadeX = localX * localX * localX * (localX * (localX * 6f - 15f) + 10f);
            float fadeZ = localZ * localZ * localZ * (localZ * (localZ * 6f - 15f) + 10f);
            float nx0 = math.lerp(n00, n10, fadeX);
            float nx1 = math.lerp(n01, n11, fadeX);
            float nxy = math.lerp(nx0, nx1, fadeZ);
            return math.saturate((nxy * 0.70710677f) + 0.5f);
        }

        private static void Gradient(float cellX, float cellZ, out float gx, out float gz)
        {
            uint hash = Hash((int)cellX, (int)cellZ);
            float angle = (hash / 4294967295f) * 6.28318530718f;
            gx = math.cos(angle);
            gz = math.sin(angle);
        }

        private static uint Hash(int xValue, int zValue)
        {
            uint x = (uint)xValue;
            uint z = (uint)zValue;
            uint h = (x * 374761393u) + (z * 668265263u);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
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
    private readonly VoxelTerrainGenerationSettings _settings;

    public VoxelTerrainData(int seed, VoxelTerrainGenerationSettings settings)
    {
        _seed = seed;
        _settings = settings;
    }

    public int SeaLevel => _settings.seaLevel;

    public WorldGenDebugSample SampleWorldGen(int worldX, int worldZ)
    {
        int height = VoxelWorldGenSampler.SampleSurfaceHeight(worldX, worldZ, _seed, _settings);
        float continentalness = VoxelWorldGenSampler.SampleContinentalness(worldX, worldZ, _seed, _settings);

        return new WorldGenDebugSample(
            height,
            continentalness);
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
            maxTerrainHeight = _settings.maxTerrainHeight,
            continentalnessSeaLevel = _settings.continentalnessSeaLevel,
            continentalnessWeight = _settings.continentalnessWeight,
            continentalnessWarpScaleMultiplier = _settings.continentalnessWarpScaleMultiplier,
            continentalnessWarpStrength = _settings.continentalnessWarpStrength,
            continentalnessDetailScaleMultiplier = _settings.continentalnessDetailScaleMultiplier,
            continentalnessDetailWeight = _settings.continentalnessDetailWeight,
            continentalnessRemapMin = _settings.continentalnessRemapMin,
            continentalnessRemapMax = _settings.continentalnessRemapMax,
            continentalness = ToJobSettings(_settings.continentalness),
            blocks = pending.blocks,
            columnHeights = pending.columnHeights,
        };

        pending.handle = job.Schedule();
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

    private static FractalNoiseJobSettings ToJobSettings(VoxelTerrainNoiseSettings settings)
    {
        return new FractalNoiseJobSettings
        {
            scale = settings.scale,
            octaves = settings.octaves,
            persistence = settings.persistence,
            lacunarity = settings.lacunarity,
            offsetX = settings.offset.x,
            offsetY = settings.offset.y,
        };
    }

    private int SampleSurfaceHeight(int worldX, int worldZ)
    {
        return VoxelWorldGenSampler.SampleSurfaceHeight(worldX, worldZ, _seed, _settings);
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
}
