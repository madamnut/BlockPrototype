using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

public sealed class TerrainData : IDisposable
{
    public readonly struct ChunkColumnGenerationProfile
    {
        public readonly Vector2Int chunkCoords;
        public readonly double cornerSampleMilliseconds;
        public readonly double cellTraversalMilliseconds;
        public readonly double solidCellFillMilliseconds;
        public readonly double mixedCellFillMilliseconds;
        public readonly double surfacePaintMilliseconds;
        public readonly double waterFillMilliseconds;
        public readonly int skippedCellCount;
        public readonly int solidCellCount;
        public readonly int mixedCellCount;
        public readonly double totalMilliseconds;

        public ChunkColumnGenerationProfile(
            Vector2Int chunkCoords,
            double cornerSampleMilliseconds,
            double cellTraversalMilliseconds,
            double solidCellFillMilliseconds,
            double mixedCellFillMilliseconds,
            double surfacePaintMilliseconds,
            double waterFillMilliseconds,
            int skippedCellCount,
            int solidCellCount,
            int mixedCellCount,
            double totalMilliseconds)
        {
            this.chunkCoords = chunkCoords;
            this.cornerSampleMilliseconds = cornerSampleMilliseconds;
            this.cellTraversalMilliseconds = cellTraversalMilliseconds;
            this.solidCellFillMilliseconds = solidCellFillMilliseconds;
            this.mixedCellFillMilliseconds = mixedCellFillMilliseconds;
            this.surfacePaintMilliseconds = surfacePaintMilliseconds;
            this.waterFillMilliseconds = waterFillMilliseconds;
            this.skippedCellCount = skippedCellCount;
            this.solidCellCount = solidCellCount;
            this.mixedCellCount = mixedCellCount;
            this.totalMilliseconds = totalMilliseconds;
        }
    }

    public readonly struct CompletedChunkColumnInfo
    {
        public readonly Vector2Int chunkCoords;
        public readonly double readyTime;
        public readonly double generationMilliseconds;
        public readonly double finalizeMilliseconds;
        public readonly ChunkColumnGenerationProfile generationProfile;

        public CompletedChunkColumnInfo(
            Vector2Int chunkCoords,
            double readyTime,
            double generationMilliseconds,
            double finalizeMilliseconds,
            ChunkColumnGenerationProfile generationProfile)
        {
            this.chunkCoords = chunkCoords;
            this.readyTime = readyTime;
            this.generationMilliseconds = generationMilliseconds;
            this.finalizeMilliseconds = finalizeMilliseconds;
            this.generationProfile = generationProfile;
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
        public PendingChunkColumnData(double requestTime)
        {
            RequestTime = requestTime;
            State = PendingChunkColumnState.Queued;
        }

        public double RequestTime { get; }
        public PendingChunkColumnState State { get; private set; }
        public GeneratedChunkColumnResult Result { get; private set; }
        public Exception Fault { get; private set; }

        public bool TryMarkGenerating()
        {
            if (State != PendingChunkColumnState.Queued)
            {
                return false;
            }

            State = PendingChunkColumnState.Generating;
            return true;
        }

        public void MarkCompleted(GeneratedChunkColumnResult result)
        {
            Result = result;
            Fault = null;
            State = PendingChunkColumnState.Completed;
        }

        public void MarkFaulted(Exception fault)
        {
            Result = null;
            Fault = fault;
            State = PendingChunkColumnState.Faulted;
        }
    }

    private sealed class GeneratedChunkColumnResult
    {
        public GeneratedChunkColumnResult(ChunkColumnData chunk, double generationMilliseconds, ChunkColumnGenerationProfile generationProfile)
        {
            Chunk = chunk;
            GenerationMilliseconds = generationMilliseconds;
            GenerationProfile = generationProfile;
        }

        public ChunkColumnData Chunk { get; }
        public double GenerationMilliseconds { get; }
        public ChunkColumnGenerationProfile GenerationProfile { get; }
    }

    private enum PendingChunkColumnState : byte
    {
        Queued = 0,
        Generating = 1,
        Completed = 2,
        Faulted = 3,
    }

    public const int ChunkSize = 16;
    public const int SubChunkSize = 16;
    public const int MinecraftMinY = -64;
    public const int WorldHeight = 384;
    public const int MinecraftHeight = WorldHeight;
    public const int MinecraftMaxYExclusive = MinecraftMinY + MinecraftHeight;
    public const int SubChunkCountY = WorldHeight / SubChunkSize;
    public const int DefaultSeaLevel = 63 - MinecraftMinY;
    public const int WorldSizeXZ = 65536;
    public const int WorldChunkCountXZ = WorldSizeXZ / ChunkSize;
    public const int HalfWorldChunkCountXZ = WorldChunkCountXZ / 2;

    public static int ToMinecraftY(int terraY)
    {
        return terraY + MinecraftMinY;
    }

    public static int WrapWorldCoord(int worldCoord)
    {
        return Mod(worldCoord, WorldSizeXZ);
    }

    public static float WrapWorldCoord(float worldCoord)
    {
        float result = worldCoord % WorldSizeXZ;
        return result < 0f ? result + WorldSizeXZ : result;
    }

    public static int WrapChunkCoord(int chunkCoord)
    {
        return Mod(chunkCoord, WorldChunkCountXZ);
    }

    public static Vector2Int WrapChunkCoords(int chunkX, int chunkZ)
    {
        return new Vector2Int(WrapChunkCoord(chunkX), WrapChunkCoord(chunkZ));
    }

    public static int GetDisplayChunkCoord(int wrappedChunkCoord, int referenceChunkCoord)
    {
        int referenceWrapped = WrapChunkCoord(referenceChunkCoord);
        int delta = wrappedChunkCoord - referenceWrapped;
        if (delta > HalfWorldChunkCountXZ)
        {
            delta -= WorldChunkCountXZ;
        }
        else if (delta < -HalfWorldChunkCountXZ)
        {
            delta += WorldChunkCountXZ;
        }

        return referenceChunkCoord + delta;
    }

    private readonly int _seed;
    private readonly ContinentalnessSampler _continentalnessSampler;
    private readonly ErosionSampler _erosionSampler;
    private readonly WeirdnessSampler _weirdnessSampler;
    private readonly TemperatureSampler _temperatureSampler;
    private readonly SimplexNoiseSampler _precipitationSampler;
    private readonly ChunkGenerator _chunkGenerator;
    private readonly object _generationStateLock = new();
    private readonly ArrayPool<BlockType> _blockArrayPool = ArrayPool<BlockType>.Shared;
    private readonly ArrayPool<VoxelFluid> _fluidArrayPool = ArrayPool<VoxelFluid>.Shared;
    private readonly ArrayPool<ushort> _foliageArrayPool = ArrayPool<ushort>.Shared;
    private readonly ArrayPool<int> _intArrayPool = ArrayPool<int>.Shared;
    private readonly ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;
    private readonly Dictionary<Vector2Int, ChunkColumnData> _chunkColumns = new();
    private readonly Dictionary<Vector2Int, PendingChunkColumnData> _pendingChunkColumns = new();
    private readonly List<Vector2Int> _queuedChunkGenerations = new();
    private readonly Queue<Vector2Int> _completedChunkGenerations = new();
    private readonly Thread[] _generationWorkers;
    private Vector2Int _generationPriorityCenterChunk;
    private bool _generationWorkersStopping;

    public TerrainData(int seed, TerrainGenerationSettings settings, WorldGenPackAsset worldGenPack)
    {
        _seed = seed;
        if (worldGenPack == null)
        {
            throw new ArgumentNullException(nameof(worldGenPack), "TerrainData requires a WorldGenPackAsset.");
        }

        _continentalnessSampler = new ContinentalnessSampler(_seed, worldGenPack.ContinentalnessSimplexSettings);
        _erosionSampler = new ErosionSampler(_seed, worldGenPack.ErosionSimplexSettings);
        _weirdnessSampler = new WeirdnessSampler(_seed, worldGenPack.WeirdnessSimplexSettings);
        _temperatureSampler = new TemperatureSampler(_seed, worldGenPack.TemperatureSettings);
        _precipitationSampler = new SimplexNoiseSampler(_seed, worldGenPack.PrecipitationSimplexSettings);
        SimplexNoiseSampler jaggedNoiseSampler = new(_seed, worldGenPack.JaggedSimplexSettings);
        SimplexNoise3DSampler terrainNoiseSampler = new(_seed, worldGenPack.Terrain3DSimplexSettings);
        JsonSplineMapper offsetMapper = new(worldGenPack.OffsetSplineGraph != null ? worldGenPack.OffsetSplineGraph.RuntimeJson : null);
        JsonSplineMapper factorMapper = new(worldGenPack.FactorSplineGraph != null ? worldGenPack.FactorSplineGraph.RuntimeJson : null);
        JsonSplineMapper jaggednessMapper = new(worldGenPack.JaggednessSplineGraph != null ? worldGenPack.JaggednessSplineGraph.RuntimeJson : null);
        _chunkGenerator = new ChunkGenerator(worldGenPack.SeaLevel, _continentalnessSampler, _erosionSampler, _weirdnessSampler, jaggedNoiseSampler, terrainNoiseSampler, offsetMapper, factorMapper, jaggednessMapper);
        _generationWorkers = CreateGenerationWorkers();
    }

    public int PendingChunkColumnCount
    {
        get
        {
            lock (_generationStateLock)
            {
                return _pendingChunkColumns.Count;
            }
        }
    }

    public float SampleContinentalness(int worldX, int worldZ)
    {
        return _continentalnessSampler.Sample(WrapWorldCoord(worldX), WrapWorldCoord(worldZ));
    }

    public float SampleWeirdness(int worldX, int worldZ)
    {
        return _weirdnessSampler.Sample(WrapWorldCoord(worldX), WrapWorldCoord(worldZ));
    }

    public float SampleErosion(int worldX, int worldZ)
    {
        return _erosionSampler.Sample(WrapWorldCoord(worldX), WrapWorldCoord(worldZ));
    }

    public float SamplePeaksAndValleys(int worldX, int worldZ)
    {
        return PeaksAndValleys.Fold(_weirdnessSampler.Sample(WrapWorldCoord(worldX), WrapWorldCoord(worldZ)));
    }

    public float SampleTemperature(int worldX, int worldZ)
    {
        return _temperatureSampler.Sample(WrapWorldCoord(worldX), WrapWorldCoord(worldZ));
    }

    public float SamplePrecipitation(int worldX, int worldZ)
    {
        return _precipitationSampler.Sample(WrapWorldCoord(worldX), WrapWorldCoord(worldZ));
    }

    public void SetGenerationPriorityCenter(Vector2Int centerChunk)
    {
        lock (_generationStateLock)
        {
            _generationPriorityCenterChunk = TerrainData.WrapChunkCoords(centerChunk.x, centerChunk.y);
        }
    }

    public BiomeGroupKind SampleBiomeGroup(int worldX, int worldZ)
    {
        int wrappedWorldX = WrapWorldCoord(worldX);
        int wrappedWorldZ = WrapWorldCoord(worldZ);
        float temperature = _temperatureSampler.Sample(wrappedWorldX, wrappedWorldZ);
        float humidity = _precipitationSampler.Sample(wrappedWorldX, wrappedWorldZ);
        float continentalness = _continentalnessSampler.Sample(wrappedWorldX, wrappedWorldZ);
        float erosion = _erosionSampler.Sample(wrappedWorldX, wrappedWorldZ);
        float weirdness = _weirdnessSampler.Sample(wrappedWorldX, wrappedWorldZ);
        return OverworldBiomeGroupClassifier.Classify(temperature, humidity, continentalness, erosion, weirdness);
    }

    public ChunkColumnGenerationProfile ProfileChunkColumnGeneration(int chunkX, int chunkZ)
    {
        Vector2Int wrappedChunkCoords = WrapChunkCoords(chunkX, chunkZ);
        double startTime = GetCurrentTimeSeconds();
        ChunkColumnData chunk = GenerateChunkColumn(wrappedChunkCoords.x, wrappedChunkCoords.y, out _, out ChunkColumnGenerationProfile profile);
        double totalMilliseconds = (GetCurrentTimeSeconds() - startTime) * 1000d;
        ReturnChunkColumnData(chunk);
        return new ChunkColumnGenerationProfile(
            wrappedChunkCoords,
            profile.cornerSampleMilliseconds,
            profile.cellTraversalMilliseconds,
            profile.solidCellFillMilliseconds,
            profile.mixedCellFillMilliseconds,
            profile.surfacePaintMilliseconds,
            profile.waterFillMilliseconds,
            profile.skippedCellCount,
            profile.solidCellCount,
            profile.mixedCellCount,
            totalMilliseconds);
    }

    public void Dispose()
    {
        lock (_generationStateLock)
        {
            _generationWorkersStopping = true;
            Monitor.PulseAll(_generationStateLock);
        }

        for (int i = 0; i < _generationWorkers.Length; i++)
        {
            Thread worker = _generationWorkers[i];
            if (worker == null)
            {
                continue;
            }

            worker.Join();
        }

        foreach (ChunkColumnData chunk in _chunkColumns.Values)
        {
            ReturnChunkColumnData(chunk);
        }

        _chunkColumns.Clear();
        lock (_generationStateLock)
        {
            foreach (PendingChunkColumnData pending in _pendingChunkColumns.Values)
            {
                if (pending.Result?.Chunk != null)
                {
                    ReturnChunkColumnData(pending.Result.Chunk);
                }
            }

            _pendingChunkColumns.Clear();
            _queuedChunkGenerations.Clear();
            _completedChunkGenerations.Clear();
        }
    }

    public bool RequestChunkColumn(int chunkX, int chunkZ)
    {
        Vector2Int key = WrapChunkCoords(chunkX, chunkZ);
        lock (_generationStateLock)
        {
            if (_chunkColumns.ContainsKey(key) || _pendingChunkColumns.ContainsKey(key) || _generationWorkersStopping)
            {
                return false;
            }

            _pendingChunkColumns.Add(key, new PendingChunkColumnData(GetCurrentTimeSeconds()));
            _queuedChunkGenerations.Add(key);
            Monitor.Pulse(_generationStateLock);
            return true;
        }
    }

    public int CompletePendingChunkColumns(List<CompletedChunkColumnInfo> completedChunkInfos, int maxCompletions)
    {
        if (maxCompletions <= 0)
        {
            return 0;
        }

        List<(Vector2Int key, PendingChunkColumnData pending)> completedEntries = new(maxCompletions);
        lock (_generationStateLock)
        {
            while (_completedChunkGenerations.Count > 0 && completedEntries.Count < maxCompletions)
            {
                Vector2Int key = _completedChunkGenerations.Dequeue();
                if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
                {
                    continue;
                }

                if (pending.State != PendingChunkColumnState.Completed && pending.State != PendingChunkColumnState.Faulted)
                {
                    continue;
                }

                _pendingChunkColumns.Remove(key);
                completedEntries.Add((key, pending));
            }
        }

        int completions = 0;
        for (int i = 0; i < completedEntries.Count; i++)
        {
            (Vector2Int key, PendingChunkColumnData pending) = completedEntries[i];
            if (pending.State == PendingChunkColumnState.Faulted)
            {
                if (pending.Fault != null)
                {
                    UnityEngine.Debug.LogException(pending.Fault);
                }

                continue;
            }

            if (pending.State != PendingChunkColumnState.Completed || pending.Result == null)
            {
                continue;
            }

            double completeTime = GetCurrentTimeSeconds();
            GeneratedChunkColumnResult result = pending.Result;
            if (_chunkColumns.TryGetValue(key, out ChunkColumnData previousChunk))
            {
                ReturnChunkColumnData(previousChunk);
            }

            _chunkColumns[key] = result.Chunk;
            completedChunkInfos?.Add(new CompletedChunkColumnInfo(
                key,
                completeTime - pending.RequestTime,
                result.GenerationMilliseconds,
                0d,
                result.GenerationProfile));
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

    public bool ReleaseChunkColumn(int chunkX, int chunkZ)
    {
        Vector2Int key = WrapChunkCoords(chunkX, chunkZ);
        if (!_chunkColumns.TryGetValue(key, out ChunkColumnData chunk))
        {
            return false;
        }

        _chunkColumns.Remove(key);
        ReturnChunkColumnData(chunk);
        return true;
    }

    public int EvictChunkColumns(Func<Vector2Int, bool> shouldKeepChunk, int maxEvictions)
    {
        if (shouldKeepChunk == null || maxEvictions <= 0 || _chunkColumns.Count == 0)
        {
            return 0;
        }

        List<Vector2Int> evictionKeys = new(Math.Min(maxEvictions, _chunkColumns.Count));
        foreach (Vector2Int key in _chunkColumns.Keys)
        {
            if (shouldKeepChunk(key))
            {
                continue;
            }

            evictionKeys.Add(key);
            if (evictionKeys.Count >= maxEvictions)
            {
                break;
            }
        }

        for (int i = 0; i < evictionKeys.Count; i++)
        {
            Vector2Int key = evictionKeys[i];
            if (_chunkColumns.TryGetValue(key, out ChunkColumnData chunk))
            {
                _chunkColumns.Remove(key);
                ReturnChunkColumnData(chunk);
            }
        }

        return evictionKeys.Count;
    }

    public int PrunePendingChunkColumns(Func<Vector2Int, bool> shouldKeepChunk)
    {
        if (shouldKeepChunk == null)
        {
            return 0;
        }

        int removedCount = 0;
        lock (_generationStateLock)
        {
            List<Vector2Int> keysToRemove = null;
            foreach (KeyValuePair<Vector2Int, PendingChunkColumnData> pair in _pendingChunkColumns)
            {
                if (shouldKeepChunk(pair.Key) || pair.Value.State == PendingChunkColumnState.Generating)
                {
                    continue;
                }

                keysToRemove ??= new List<Vector2Int>();
                keysToRemove.Add(pair.Key);
            }

            if (keysToRemove == null || keysToRemove.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                Vector2Int key = keysToRemove[i];
                if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
                {
                    continue;
                }

                if (pending.Result?.Chunk != null)
                {
                    ReturnChunkColumnData(pending.Result.Chunk);
                }

                _pendingChunkColumns.Remove(key);
                removedCount++;
            }

            RebuildQueuedPendingCollections();
        }

        return removedCount;
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
        int wrappedWorldX = WrapWorldCoord(worldX);
        int wrappedWorldZ = WrapWorldCoord(worldZ);
        int chunkX = FloorDiv(wrappedWorldX, ChunkSize);
        int chunkZ = FloorDiv(wrappedWorldZ, ChunkSize);
        if (!_chunkColumns.TryGetValue(WrapChunkCoords(chunkX, chunkZ), out ChunkColumnData chunk))
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

        int wrappedWorldX = WrapWorldCoord(worldX);
        int wrappedWorldZ = WrapWorldCoord(worldZ);
        if (!TryGetEditableChunkColumn(FloorDiv(wrappedWorldX, ChunkSize), FloorDiv(wrappedWorldZ, ChunkSize), out ChunkColumnData chunk))
        {
            return false;
        }

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

        int wrappedWorldX = WrapWorldCoord(worldX);
        int wrappedWorldZ = WrapWorldCoord(worldZ);
        if (!TryGetEditableChunkColumn(FloorDiv(wrappedWorldX, ChunkSize), FloorDiv(wrappedWorldZ, ChunkSize), out ChunkColumnData chunk))
        {
            return false;
        }

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

        int wrappedWorldX = WrapWorldCoord(worldX);
        int wrappedWorldZ = WrapWorldCoord(worldZ);
        if (!TryGetEditableChunkColumn(FloorDiv(wrappedWorldX, ChunkSize), FloorDiv(wrappedWorldZ, ChunkSize), out ChunkColumnData chunk))
        {
            return false;
        }

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

    private bool TryGetEditableChunkColumn(int chunkX, int chunkZ, out ChunkColumnData chunk)
    {
        Vector2Int key = WrapChunkCoords(chunkX, chunkZ);
        if (_chunkColumns.TryGetValue(key, out chunk))
        {
            return true;
        }

        RequestChunkColumn(key.x, key.y);
        chunk = null;
        return false;
    }

    private Thread[] CreateGenerationWorkers()
    {
        int workerCount = Math.Max(1, Environment.ProcessorCount - 1);
        Thread[] workers = new Thread[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            Thread worker = new(GenerationWorkerLoop)
            {
                IsBackground = true,
                Name = $"Terrain Chunk Worker {i + 1}",
            };
            worker.Start();
            workers[i] = worker;
        }

        return workers;
    }

    private void GenerationWorkerLoop()
    {
        while (true)
        {
            Vector2Int key = default;
            PendingChunkColumnData pending = null;

            lock (_generationStateLock)
            {
                while (!_generationWorkersStopping && !TryDequeueQueuedChunk(out key, out pending))
                {
                    Monitor.Wait(_generationStateLock);
                }

                if (_generationWorkersStopping)
                {
                    return;
                }
            }

            try
            {
                double generateStart = GetCurrentTimeSeconds();
                ChunkColumnData chunk = GenerateChunkColumn(key.x, key.y, out _, out ChunkColumnGenerationProfile generationProfile);
                double generateEnd = GetCurrentTimeSeconds();
                CompleteBackgroundChunkGeneration(
                    key,
                    pending,
                    new GeneratedChunkColumnResult(chunk, (generateEnd - generateStart) * 1000d, generationProfile),
                    null);
            }
            catch (Exception ex)
            {
                CompleteBackgroundChunkGeneration(key, pending, null, ex);
            }
        }
    }

    private bool TryDequeueQueuedChunk(out Vector2Int key, out PendingChunkColumnData pending)
    {
        while (_queuedChunkGenerations.Count > 0)
        {
            int bestIndex = GetHighestPriorityQueuedChunkIndex();
            key = _queuedChunkGenerations[bestIndex];
            _queuedChunkGenerations.RemoveAt(bestIndex);
            if (!_pendingChunkColumns.TryGetValue(key, out pending))
            {
                continue;
            }

            if (!pending.TryMarkGenerating())
            {
                continue;
            }

            return true;
        }

        key = default;
        pending = null;
        return false;
    }

    private int GetHighestPriorityQueuedChunkIndex()
    {
        int bestIndex = 0;
        Vector2Int centerChunk = _generationPriorityCenterChunk;
        int bestDistance = GetChunkPriorityDistance(_queuedChunkGenerations[0], centerChunk);

        for (int i = 1; i < _queuedChunkGenerations.Count; i++)
        {
            Vector2Int candidate = _queuedChunkGenerations[i];
            int candidateDistance = GetChunkPriorityDistance(candidate, centerChunk);
            if (candidateDistance < bestDistance)
            {
                bestIndex = i;
                bestDistance = candidateDistance;
                continue;
            }

            if (candidateDistance == bestDistance)
            {
                Vector2Int currentBest = _queuedChunkGenerations[bestIndex];
                int zComparison = candidate.y.CompareTo(currentBest.y);
                if (zComparison < 0 || (zComparison == 0 && candidate.x < currentBest.x))
                {
                    bestIndex = i;
                }
            }
        }

        return bestIndex;
    }

    private static int GetChunkPriorityDistance(Vector2Int chunkCoords, Vector2Int centerChunk)
    {
        int dx = TerrainData.GetDisplayChunkCoord(chunkCoords.x, centerChunk.x) - centerChunk.x;
        int dz = TerrainData.GetDisplayChunkCoord(chunkCoords.y, centerChunk.y) - centerChunk.y;
        return (dx * dx) + (dz * dz);
    }

    private void CompleteBackgroundChunkGeneration(Vector2Int key, PendingChunkColumnData pending, GeneratedChunkColumnResult result, Exception fault)
    {
        lock (_generationStateLock)
        {
            if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData currentPending) || !ReferenceEquals(currentPending, pending))
            {
                return;
            }

            if (fault != null)
            {
                pending.MarkFaulted(fault);
            }
            else
            {
                pending.MarkCompleted(result);
            }

            _completedChunkGenerations.Enqueue(key);
        }
    }

    private ChunkColumnData GenerateChunkColumn(int chunkX, int chunkZ, out int[] columnHeights, out ChunkColumnGenerationProfile generationProfile)
    {
        chunkX = WrapChunkCoord(chunkX);
        chunkZ = WrapChunkCoord(chunkZ);
        int voxelCount = ChunkSize * WorldHeight * ChunkSize;
        BlockType[] blocks = RentArray(_blockArrayPool, voxelCount);
        VoxelFluid[] fluids = RentArray(_fluidArrayPool, voxelCount);
        ushort[] foliageIds = RentArray(_foliageArrayPool, voxelCount);
        columnHeights = RentArray(_intArrayPool, ChunkSize * ChunkSize);
        byte[] subChunkContents = RentArray(_byteArrayPool, SubChunkCountY);

        int maxHeight = _chunkGenerator.GenerateChunkColumn(chunkX, chunkZ, blocks, fluids, columnHeights, out generationProfile);

        ChunkColumnData chunk = new(blocks, fluids, foliageIds, columnHeights, subChunkContents, maxHeight);
        for (int subChunkY = 0; subChunkY < SubChunkCountY; subChunkY++)
        {
            RecalculateSubChunkContents(chunk, subChunkY);
        }

        RecalculateChunkMaxHeight(chunk);
        return chunk;
    }

    private void RebuildQueuedPendingCollections()
    {
        for (int i = _queuedChunkGenerations.Count - 1; i >= 0; i--)
        {
            Vector2Int key = _queuedChunkGenerations[i];
            if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending) || pending.State != PendingChunkColumnState.Queued)
            {
                _queuedChunkGenerations.RemoveAt(i);
            }
        }

        if (_completedChunkGenerations.Count == 0)
        {
            return;
        }

        Queue<Vector2Int> filteredCompleted = new(_completedChunkGenerations.Count);
        while (_completedChunkGenerations.Count > 0)
        {
            Vector2Int key = _completedChunkGenerations.Dequeue();
            if (_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending) &&
                (pending.State == PendingChunkColumnState.Completed || pending.State == PendingChunkColumnState.Faulted))
            {
                filteredCompleted.Enqueue(key);
            }
        }

        while (filteredCompleted.Count > 0)
        {
            _completedChunkGenerations.Enqueue(filteredCompleted.Dequeue());
        }
    }

    private T[] RentArray<T>(ArrayPool<T> pool, int minimumLength)
    {
        T[] array = pool.Rent(minimumLength);
        Array.Clear(array, 0, minimumLength);
        return array;
    }

    private void ReturnChunkColumnData(ChunkColumnData chunk)
    {
        if (chunk == null)
        {
            return;
        }

        _blockArrayPool.Return(chunk.blocks, clearArray: false);
        _fluidArrayPool.Return(chunk.fluids, clearArray: false);
        _foliageArrayPool.Return(chunk.foliageIds, clearArray: false);
        _intArrayPool.Return(chunk.columnHeights, clearArray: false);
        _byteArrayPool.Return(chunk.subChunkContents, clearArray: false);
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

        int wrappedWorldX = WrapWorldCoord(worldX);
        int wrappedWorldZ = WrapWorldCoord(worldZ);
        int chunkX = FloorDiv(wrappedWorldX, ChunkSize);
        int chunkZ = FloorDiv(wrappedWorldZ, ChunkSize);
        if (!_chunkColumns.TryGetValue(WrapChunkCoords(chunkX, chunkZ), out chunk))
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
