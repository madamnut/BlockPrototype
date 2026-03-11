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

    private struct TerrainLayerJobSettings
    {
        public float baseHeight;
        public float coarseScale;
        public float coarseAmplitude;
        public float detailScale;
        public float detailAmplitude;
        public float ridgeScale;
        public float ridgeAmplitude;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private struct GenerateChunkColumnJob : IJob
    {
        public int chunkX;
        public int chunkZ;
        public int seed;
        public TerrainLayerJobSettings dirt;
        public TerrainLayerJobSettings rock;

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
                    int dirtHeight = SampleLayerHeight(worldX, worldZ, seed, dirt, 0.173f, 0.127f, 100f, 300f);
                    int rockHeight = SampleLayerHeight(worldX, worldZ, seed, rock, 0.241f, 0.191f, 700f, 1100f);
                    rockHeight = math.clamp(rockHeight, 0, dirtHeight);

                    for (int worldY = 0; worldY <= dirtHeight; worldY++)
                    {
                        blocks[GetIndex(localX, worldY, localZ)] = worldY <= rockHeight ? BlockType.Rock : BlockType.Dirt;
                    }

                    columnHeights[(localZ * ChunkSize) + localX] = dirtHeight;
                }
            }
        }

        private static int SampleLayerHeight(
            int x,
            int z,
            int seed,
            TerrainLayerJobSettings layer,
            float offsetSeedX,
            float offsetSeedZ,
            float baseOffsetX,
            float baseOffsetZ)
        {
            float offsetX = (seed * offsetSeedX) + baseOffsetX;
            float offsetZ = (seed * offsetSeedZ) + baseOffsetZ;

            float coarse = SampleNoise(x, z, offsetX, offsetZ, layer.coarseScale);
            float detail = SampleNoise(x, z, -offsetZ, offsetX, layer.detailScale);
            float ridgeNoise = SampleNoise(x, z, offsetX, -offsetZ, layer.ridgeScale);
            float ridge = math.abs((ridgeNoise * 2f) - 1f);

            float elevation = layer.baseHeight;
            elevation += coarse * layer.coarseAmplitude;
            elevation += detail * layer.detailAmplitude;
            elevation -= ridge * layer.ridgeAmplitude;

            return math.clamp((int)math.round(elevation), 0, WorldHeight - 1);
        }

        private static float SampleNoise(int x, int z, float offsetX, float offsetZ, float scale)
        {
            if (scale <= 0f)
            {
                return 0f;
            }

            float2 point = new((x + offsetX) * scale, (z + offsetZ) * scale);
            return math.saturate((noise.snoise(point) * 0.5f) + 0.5f);
        }
    }

    public const int ChunkSize = 16;
    public const int SubChunkSize = 16;
    public const int WorldHeight = 256;
    public const int SubChunkCountY = WorldHeight / SubChunkSize;
    public const ushort PlantFoliageId = 100;
    private const float PlantSpawnChance = 0.7f;
    private const float TreeSpawnChance = 0.03f;
    private const int TreeEdgePadding = 2;

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
            dirt = ToJobSettings(_settings.dirt),
            rock = ToJobSettings(_settings.rock),
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

        for (int localZ = 0; localZ < ChunkSize; localZ++)
        {
            for (int localX = 0; localX < ChunkSize; localX++)
            {
                int surfaceHeight = pending.columnHeights[(localZ * ChunkSize) + localX];
                if (surfaceHeight < 0 || surfaceHeight >= WorldHeight)
                {
                    continue;
                }

                int surfaceIndex = GetIndex(localX, surfaceHeight, localZ);
                if (managedBlocks[surfaceIndex] != BlockType.Dirt)
                {
                    continue;
                }

                bool hasFluidAbove = surfaceHeight < WorldHeight - 1 &&
                                     managedFluids[GetIndex(localX, surfaceHeight + 1, localZ)].Exists;

                if (!hasFluidAbove)
                {
                    managedBlocks[surfaceIndex] = BlockType.Grass;

                    bool treeGenerated = TryGenerateTree(
                        key.x,
                        key.y,
                        localX,
                        surfaceHeight,
                        localZ,
                        managedBlocks,
                        managedFluids,
                        managedFoliageIds,
                        ref maxHeight);

                    int foliageY = surfaceHeight + 1;
                    if (!treeGenerated &&
                        foliageY < WorldHeight &&
                        managedBlocks[GetIndex(localX, foliageY, localZ)] == BlockType.Air &&
                        !managedFluids[GetIndex(localX, foliageY, localZ)].Exists &&
                        ShouldSpawnPlant(key.x, key.y, localX, foliageY, localZ))
                    {
                        managedFoliageIds[GetIndex(localX, foliageY, localZ)] = PlantFoliageId;
                        if (foliageY > maxHeight)
                        {
                            maxHeight = foliageY;
                        }
                    }
                }
            }
        }

        pending.blocks.Dispose();
        pending.columnHeights.Dispose();

        _chunkColumns[key] = new ChunkColumnData(managedBlocks, managedFluids, managedFoliageIds, maxHeight);
    }

    private bool TryGenerateTree(
        int chunkX,
        int chunkZ,
        int localX,
        int surfaceY,
        int localZ,
        BlockType[] blocks,
        VoxelFluid[] fluids,
        ushort[] foliageIds,
        ref int maxHeight)
    {
        if (localX < TreeEdgePadding || localX >= ChunkSize - TreeEdgePadding ||
            localZ < TreeEdgePadding || localZ >= ChunkSize - TreeEdgePadding)
        {
            return false;
        }

        if (!ShouldSpawnTree(chunkX, chunkZ, localX, surfaceY + 1, localZ))
        {
            return false;
        }

        int trunkBaseY = surfaceY + 1;
        int trunkHeight = 6 + GetDeterministicInt(chunkX, chunkZ, localX, trunkBaseY, localZ, 3, 17);
        int trunkTopY = trunkBaseY + trunkHeight - 1;
        if (trunkTopY + 2 >= WorldHeight)
        {
            return false;
        }

        for (int worldY = trunkBaseY; worldY <= trunkTopY; worldY++)
        {
            BlockType existing = blocks[GetIndex(localX, worldY, localZ)];
            if (existing != BlockType.Air && existing != BlockType.Leaves)
            {
                return false;
            }

            if (fluids[GetIndex(localX, worldY, localZ)].Exists)
            {
                return false;
            }
        }

        for (int worldY = trunkBaseY; worldY <= trunkTopY; worldY++)
        {
            int index = GetIndex(localX, worldY, localZ);
            blocks[index] = BlockType.Log;
            fluids[index] = VoxelFluid.None;
            foliageIds[index] = 0;
            if (worldY > maxHeight)
            {
                maxHeight = worldY;
            }
        }

        TryPlaceLeavesLayer(localX, trunkTopY - 1, localZ, 2, blocks, fluids, foliageIds, ref maxHeight);
        TryPlaceLeavesLayer(localX, trunkTopY, localZ, 2, blocks, fluids, foliageIds, ref maxHeight);
        TryPlaceLeavesLayer(localX, trunkTopY + 1, localZ, 1, blocks, fluids, foliageIds, ref maxHeight);
        TryPlaceLeavesLayer(localX, trunkTopY + 2, localZ, 0, blocks, fluids, foliageIds, ref maxHeight);
        return true;
    }

    private static void TryPlaceLeavesLayer(
        int centerX,
        int worldY,
        int centerZ,
        int radius,
        BlockType[] blocks,
        VoxelFluid[] fluids,
        ushort[] foliageIds,
        ref int maxHeight)
    {
        if (worldY < 0 || worldY >= WorldHeight)
        {
            return;
        }

        for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
        {
            int localZ = centerZ + offsetZ;
            if (localZ < 0 || localZ >= ChunkSize)
            {
                continue;
            }

            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                int localX = centerX + offsetX;
                if (localX < 0 || localX >= ChunkSize)
                {
                    continue;
                }

                if (radius > 1 && Mathf.Abs(offsetX) == radius && Mathf.Abs(offsetZ) == radius)
                {
                    continue;
                }

                int index = GetIndex(localX, worldY, localZ);
                if (blocks[index] != BlockType.Air || fluids[index].Exists)
                {
                    continue;
                }

                blocks[index] = BlockType.Leaves;
                foliageIds[index] = 0;
                if (worldY > maxHeight)
                {
                    maxHeight = worldY;
                }
            }
        }
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
        bool hasSupport = chunk.blocks[supportIndex] == BlockType.Grass && !chunk.fluids[aboveIndex].Exists;
        if (!hasSupport)
        {
            chunk.foliageIds[aboveIndex] = 0;
            return true;
        }

        return false;
    }

    private bool ShouldSpawnPlant(int chunkX, int chunkZ, int localX, int worldY, int localZ)
    {
        unchecked
        {
            float normalized = GetDeterministic01(chunkX, chunkZ, localX, worldY, localZ, 3);
            return normalized < PlantSpawnChance;
        }
    }

    private bool ShouldSpawnTree(int chunkX, int chunkZ, int localX, int worldY, int localZ)
    {
        float normalized = GetDeterministic01(chunkX, chunkZ, localX, worldY, localZ, 11);
        return normalized < TreeSpawnChance;
    }

    private float GetDeterministic01(int chunkX, int chunkZ, int localX, int worldY, int localZ, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)_seed) * 16777619u;
            hash = (hash ^ (uint)(chunkX * ChunkSize + localX)) * 16777619u;
            hash = (hash ^ (uint)worldY) * 16777619u;
            hash = (hash ^ (uint)(chunkZ * ChunkSize + localZ)) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    private int GetDeterministicInt(int chunkX, int chunkZ, int localX, int worldY, int localZ, int range, int salt)
    {
        if (range <= 1)
        {
            return 0;
        }

        return Mathf.FloorToInt(GetDeterministic01(chunkX, chunkZ, localX, worldY, localZ, salt) * range);
    }

    private static TerrainLayerJobSettings ToJobSettings(VoxelTerrainLayerSettings settings)
    {
        return new TerrainLayerJobSettings
        {
            baseHeight = settings.baseHeight,
            coarseScale = settings.coarseScale,
            coarseAmplitude = settings.coarseAmplitude,
            detailScale = settings.detailScale,
            detailAmplitude = settings.detailAmplitude,
            ridgeScale = settings.ridgeScale,
            ridgeAmplitude = settings.ridgeAmplitude,
        };
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
