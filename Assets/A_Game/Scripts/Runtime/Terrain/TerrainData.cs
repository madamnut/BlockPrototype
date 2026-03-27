using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

        public ChunkColumnData(
            BlockType[] blocks,
            VoxelFluid[] fluids,
            ushort[] foliageIds,
            int[] columnHeights,
            byte[] subChunkContents,
            int maxHeight)
        {
            this.blocks = blocks;
            this.fluids = fluids;
            this.foliageIds = foliageIds;
            this.columnHeights = columnHeights;
            this.subChunkContents = subChunkContents;
            this.maxHeight = maxHeight;
        }
    }

    private sealed class PendingChunkColumnData
    {
        private readonly List<Vector2Int> _requestedChunkCoords = new();

        public PendingChunkColumnData(double requestTime, Task<GeneratedChunkColumnResult> generationTask, Vector2Int requestedChunkCoords)
        {
            RequestTime = requestTime;
            GenerationTask = generationTask;
            _requestedChunkCoords.Add(requestedChunkCoords);
        }

        public double RequestTime { get; }
        public Task<GeneratedChunkColumnResult> GenerationTask { get; }
        public IReadOnlyList<Vector2Int> RequestedChunkCoords => _requestedChunkCoords;

        public void AddRequestedChunkCoords(Vector2Int requestedChunkCoords)
        {
            for (int i = 0; i < _requestedChunkCoords.Count; i++)
            {
                if (_requestedChunkCoords[i] == requestedChunkCoords)
                {
                    return;
                }
            }

            _requestedChunkCoords.Add(requestedChunkCoords);
        }
    }

    private sealed class GeneratedChunkColumnResult
    {
        public GeneratedChunkColumnResult(ChunkColumnData chunk, double generationMilliseconds)
        {
            Chunk = chunk;
            GenerationMilliseconds = generationMilliseconds;
        }

        public ChunkColumnData Chunk { get; }
        public double GenerationMilliseconds { get; }
    }

    public const int ChunkSize = 16;
    public const int SubChunkSize = 16;
    public const int MinecraftMinY = -64;
    public const int WorldHeight = 384;
    public const int MinecraftHeight = WorldHeight;
    public const int MinecraftMaxYExclusive = MinecraftMinY + MinecraftHeight;
    public const int SubChunkCountY = WorldHeight / SubChunkSize;
    public const int DefaultSeaLevel = 63 - MinecraftMinY;
    public const int WorldSizeXZ = TileableNoiseChunkGenerator.WorldSizeXZ;
    public const int WorldSizeInChunks = WorldSizeXZ / ChunkSize;

    public static int ToMinecraftY(int terraY)
    {
        return terraY + MinecraftMinY;
    }

    private const string EmptyBiomeName = "empty";

    private readonly TileableNoiseChunkGenerator _chunkGenerator;
    private readonly Dictionary<Vector2Int, ChunkColumnData> _chunkColumns = new();
    private readonly Dictionary<Vector2Int, PendingChunkColumnData> _pendingChunkColumns = new();
    private readonly List<Vector2Int> _pendingChunkKeys = new();
    private readonly CancellationTokenSource _generationCancellation = new();

    public TerrainData(int seed, TerrainGenerationSettings settings)
    {
        _ = seed;
        _ = settings;
        _chunkGenerator = new TileableNoiseChunkGenerator(seed);
    }

    public int PendingChunkColumnCount => _pendingChunkColumns.Count;

    public float SampleContinentalness(int worldX, int worldZ)
    {
        _ = worldX;
        _ = worldZ;
        return 0f;
    }

    public float SampleWeirdness(int worldX, int worldZ)
    {
        _ = worldX;
        _ = worldZ;
        return 0f;
    }

    public float SampleErosion(int worldX, int worldZ)
    {
        _ = worldX;
        _ = worldZ;
        return 0f;
    }

    public float SamplePeaksAndValleys(int worldX, int worldZ)
    {
        _ = worldX;
        _ = worldZ;
        return 0f;
    }

    public float SampleTemperature(int worldX, int worldZ)
    {
        _ = worldX;
        _ = worldZ;
        return 0f;
    }

    public float SampleHumidity(int worldX, int worldZ)
    {
        _ = worldX;
        _ = worldZ;
        return 0f;
    }

    public BiomeKind SampleBiome(int worldX, int worldZ)
    {
        _ = worldX;
        _ = worldZ;
        return BiomeKind.Land;
    }

    public string SampleBiomeName(int worldX, int worldZ)
    {
        _ = worldX;
        _ = worldZ;
        return EmptyBiomeName;
    }

    public ChunkColumnGenerationProfile ProfileChunkColumnGeneration(int chunkX, int chunkZ)
    {
        double startTime = GetCurrentTimeSeconds();
        GenerateChunkColumn(chunkX, chunkZ, out _);
        double totalMilliseconds = (GetCurrentTimeSeconds() - startTime) * 1000d;
        return new ChunkColumnGenerationProfile(
            new Vector2Int(chunkX, chunkZ),
            0d,
            0d,
            0d,
            totalMilliseconds,
            totalMilliseconds);
    }

    public void Dispose()
    {
        _generationCancellation.Cancel();
        _chunkColumns.Clear();
        _pendingChunkColumns.Clear();
        _pendingChunkKeys.Clear();
        _generationCancellation.Dispose();
    }

    public bool RequestChunkColumn(int chunkX, int chunkZ)
    {
        Vector2Int requestedCoords = new(chunkX, chunkZ);
        Vector2Int key = WrapChunkCoords(chunkX, chunkZ);
        if (_chunkColumns.ContainsKey(key) || _pendingChunkColumns.ContainsKey(key))
        {
            if (_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
            {
                pending.AddRequestedChunkCoords(requestedCoords);
            }

            return false;
        }

        CancellationToken cancellationToken = _generationCancellation.Token;
        Task<GeneratedChunkColumnResult> generationTask = Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            double generateStart = GetCurrentTimeSeconds();
            ChunkColumnData chunk = GenerateChunkColumn(key.x, key.y, out _);
            double generateEnd = GetCurrentTimeSeconds();
            return new GeneratedChunkColumnResult(chunk, (generateEnd - generateStart) * 1000d);
        }, cancellationToken);

        _pendingChunkColumns.Add(key, new PendingChunkColumnData(GetCurrentTimeSeconds(), generationTask, requestedCoords));
        _pendingChunkKeys.Add(key);
        return true;
    }

    public int CompletePendingChunkColumns(List<CompletedChunkColumnInfo> completedChunkInfos, int maxCompletions)
    {
        if (maxCompletions <= 0 || _pendingChunkKeys.Count == 0)
        {
            return 0;
        }

        int completions = 0;
        for (int pendingIndex = 0; pendingIndex < _pendingChunkKeys.Count && completions < maxCompletions;)
        {
            Vector2Int key = _pendingChunkKeys[pendingIndex];
            if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
            {
                _pendingChunkKeys.RemoveAt(pendingIndex);
                continue;
            }

            if (!pending.GenerationTask.IsCompleted)
            {
                pendingIndex++;
                continue;
            }

            _pendingChunkKeys.RemoveAt(pendingIndex);
            _pendingChunkColumns.Remove(key);

            if (pending.GenerationTask.IsCanceled)
            {
                continue;
            }

            if (pending.GenerationTask.IsFaulted)
            {
                UnityEngine.Debug.LogException(pending.GenerationTask.Exception);
                continue;
            }

            double completeTime = GetCurrentTimeSeconds();
            GeneratedChunkColumnResult result = pending.GenerationTask.Result;
            _chunkColumns[key] = result.Chunk;
            if (completedChunkInfos != null)
            {
                for (int requestedIndex = 0; requestedIndex < pending.RequestedChunkCoords.Count; requestedIndex++)
                {
                    completedChunkInfos.Add(new CompletedChunkColumnInfo(
                        pending.RequestedChunkCoords[requestedIndex],
                        completeTime - pending.RequestTime,
                        0d,
                        result.GenerationMilliseconds,
                        0d));
                }
            }

            completions++;
        }

        return completions;
    }

    public int CompletePendingChunkColumns(int maxCompletions)
    {
        return CompletePendingChunkColumns(null, maxCompletions);
    }

    public bool IsChunkColumnReady(int chunkX, int chunkZ)
    {
        return _chunkColumns.ContainsKey(WrapChunkCoords(chunkX, chunkZ));
    }

    public bool TryGetUsedSubChunkCount(int chunkX, int chunkZ, out int usedSubChunkCount)
    {
        if (!_chunkColumns.TryGetValue(WrapChunkCoords(chunkX, chunkZ), out ChunkColumnData chunk))
        {
            usedSubChunkCount = 0;
            return false;
        }

        usedSubChunkCount = GetUsedSubChunkCount(chunk);
        return true;
    }

    public int GetUsedSubChunkCount(int chunkX, int chunkZ)
    {
        return _chunkColumns.TryGetValue(WrapChunkCoords(chunkX, chunkZ), out ChunkColumnData chunk)
            ? GetUsedSubChunkCount(chunk)
            : 0;
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

    public BlockType GetBlock(int worldX, int worldY, int worldZ)
    {
        if (!TryGetChunkAndIndex(worldX, worldY, worldZ, out ChunkColumnData chunk, out int index))
        {
            return BlockType.Air;
        }

        return chunk.blocks[index];
    }

    public VoxelFluid GetFluid(int worldX, int worldY, int worldZ)
    {
        if (!TryGetChunkAndIndex(worldX, worldY, worldZ, out ChunkColumnData chunk, out int index))
        {
            return VoxelFluid.None;
        }

        return chunk.fluids[index];
    }

    public ushort GetFoliageId(int worldX, int worldY, int worldZ)
    {
        if (!TryGetChunkAndIndex(worldX, worldY, worldZ, out ChunkColumnData chunk, out int index))
        {
            return 0;
        }

        return chunk.foliageIds[index];
    }

    public int GetColumnHeight(int worldX, int worldZ)
    {
        int wrappedWorldX = WrapWorldCoordinate(worldX);
        int wrappedWorldZ = WrapWorldCoordinate(worldZ);
        Vector2Int wrappedChunkCoords = GetWrappedChunkCoordsFromWorld(wrappedWorldX, wrappedWorldZ);
        if (!_chunkColumns.TryGetValue(wrappedChunkCoords, out ChunkColumnData chunk))
        {
            return -1;
        }

        int localX = Mod(wrappedWorldX, ChunkSize);
        int localZ = Mod(wrappedWorldZ, ChunkSize);
        return chunk.columnHeights[(localZ * ChunkSize) + localX];
    }

    public bool SetBlock(int worldX, int worldY, int worldZ, BlockType blockType)
    {
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return false;
        }

        int wrappedWorldX = WrapWorldCoordinate(worldX);
        int wrappedWorldZ = WrapWorldCoordinate(worldZ);
        ChunkColumnData chunk = EnsureChunkColumnReady(
            FloorDiv(wrappedWorldX, ChunkSize),
            FloorDiv(wrappedWorldZ, ChunkSize));
        int localX = Mod(wrappedWorldX, ChunkSize);
        int localZ = Mod(wrappedWorldZ, ChunkSize);
        int index = GetIndex(localX, worldY, localZ);
        int columnIndex = (localZ * ChunkSize) + localX;
        int currentColumnHeight = chunk.columnHeights[columnIndex];
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

            if (worldY > currentColumnHeight)
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

        if (worldY >= currentColumnHeight)
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

        int wrappedWorldX = WrapWorldCoordinate(worldX);
        int wrappedWorldZ = WrapWorldCoordinate(worldZ);
        ChunkColumnData chunk = EnsureChunkColumnReady(
            FloorDiv(wrappedWorldX, ChunkSize),
            FloorDiv(wrappedWorldZ, ChunkSize));
        int localX = Mod(wrappedWorldX, ChunkSize);
        int localZ = Mod(wrappedWorldZ, ChunkSize);
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

        int wrappedWorldX = WrapWorldCoordinate(worldX);
        int wrappedWorldZ = WrapWorldCoordinate(worldZ);
        ChunkColumnData chunk = EnsureChunkColumnReady(
            FloorDiv(wrappedWorldX, ChunkSize),
            FloorDiv(wrappedWorldZ, ChunkSize));
        int localX = Mod(wrappedWorldX, ChunkSize);
        int localZ = Mod(wrappedWorldZ, ChunkSize);
        int index = GetIndex(localX, worldY, localZ);
        if (chunk.foliageIds[index] == foliageId)
        {
            return false;
        }

        if (foliageId != 0 && (chunk.blocks[index] != BlockType.Air || chunk.fluids[index].Exists))
        {
            return false;
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
        Vector2Int key = WrapChunkCoords(chunkX, chunkZ);
        if (_chunkColumns.TryGetValue(key, out ChunkColumnData chunk))
        {
            return chunk;
        }

        if (_pendingChunkColumns.ContainsKey(key))
        {
            _pendingChunkColumns.Remove(key);
            _pendingChunkKeys.Remove(key);
        }

        chunk = GenerateChunkColumn(chunkX, chunkZ, out _);
        _chunkColumns[key] = chunk;
        return chunk;
    }

    private ChunkColumnData GenerateChunkColumn(int chunkX, int chunkZ, out int[] columnHeights)
    {
        Vector2Int wrappedChunkCoords = WrapChunkCoords(chunkX, chunkZ);
        int voxelCount = ChunkSize * WorldHeight * ChunkSize;
        BlockType[] blocks = new BlockType[voxelCount];
        VoxelFluid[] fluids = new VoxelFluid[voxelCount];
        ushort[] foliageIds = new ushort[voxelCount];
        columnHeights = new int[ChunkSize * ChunkSize];
        byte[] subChunkContents = new byte[SubChunkCountY];

        int maxHeight = _chunkGenerator.GenerateChunkColumn(wrappedChunkCoords.x, wrappedChunkCoords.y, blocks, fluids, columnHeights);

        ChunkColumnData chunk = new(blocks, fluids, foliageIds, columnHeights, subChunkContents, maxHeight);
        for (int subChunkY = 0; subChunkY < SubChunkCountY; subChunkY++)
        {
            RecalculateSubChunkContents(chunk, subChunkY);
        }

        RecalculateChunkMaxHeight(chunk);
        return chunk;
    }

    private static double GetCurrentTimeSeconds()
    {
        return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }

    private bool TryGetSubChunkContents(int chunkX, int subChunkY, int chunkZ, out byte contents)
    {
        contents = 0;
        if (subChunkY < 0 || subChunkY >= SubChunkCountY)
        {
            return false;
        }

        if (!_chunkColumns.TryGetValue(WrapChunkCoords(chunkX, chunkZ), out ChunkColumnData chunk))
        {
            return false;
        }

        contents = chunk.subChunkContents[subChunkY];
        return true;
    }

    private bool TryGetChunkAndIndex(int worldX, int worldY, int worldZ, out ChunkColumnData chunk, out int index)
    {
        chunk = null;
        index = -1;
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return false;
        }

        int wrappedWorldX = WrapWorldCoordinate(worldX);
        int wrappedWorldZ = WrapWorldCoordinate(worldZ);
        Vector2Int wrappedChunkCoords = GetWrappedChunkCoordsFromWorld(wrappedWorldX, wrappedWorldZ);
        if (!_chunkColumns.TryGetValue(wrappedChunkCoords, out chunk))
        {
            return false;
        }

        int localX = Mod(wrappedWorldX, ChunkSize);
        int localZ = Mod(wrappedWorldZ, ChunkSize);
        index = GetIndex(localX, worldY, localZ);
        return true;
    }

    private static int GetUsedSubChunkCount(ChunkColumnData chunk)
    {
        for (int subChunkY = SubChunkCountY - 1; subChunkY >= 0; subChunkY--)
        {
            if (chunk.subChunkContents[subChunkY] != 0)
            {
                return subChunkY + 1;
            }
        }

        return 0;
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

    private static int GetIndex(int localX, int worldY, int localZ)
    {
        return ((worldY * ChunkSize) + localZ) * ChunkSize + localX;
    }

    private static int WrapWorldCoordinate(int worldCoordinate)
    {
        return Mod(worldCoordinate, WorldSizeXZ);
    }

    private static Vector2Int GetWrappedChunkCoordsFromWorld(int worldX, int worldZ)
    {
        return new Vector2Int(
            FloorDiv(worldX, ChunkSize),
            FloorDiv(worldZ, ChunkSize));
    }

    private static Vector2Int WrapChunkCoords(int chunkX, int chunkZ)
    {
        return new Vector2Int(
            Mod(chunkX, WorldSizeInChunks),
            Mod(chunkZ, WorldSizeInChunks));
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
