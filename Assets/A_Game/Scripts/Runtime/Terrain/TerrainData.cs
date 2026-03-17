using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        }

        public double RequestTime { get; }
    }

    public const int ChunkSize = 16;
    public const int SubChunkSize = 16;
    public const int WorldHeight = 384;
    public const int SubChunkCountY = WorldHeight / SubChunkSize;
    public const int DefaultSeaLevel = 127;

    private readonly int _seed;
    private readonly TerrainGenerationSettings _settings;
    private readonly Dictionary<Vector2Int, ChunkColumnData> _chunkColumns = new();
    private readonly Dictionary<Vector2Int, PendingChunkColumnData> _pendingChunkColumns = new();
    private readonly List<Vector2Int> _pendingChunkKeys = new();

    public TerrainData(int seed, TerrainGenerationSettings settings)
    {
        _seed = seed;
        _settings = settings.IsInitialized ? settings : TerrainGenerationSettings.Default;
    }

    public int PendingChunkColumnCount => _pendingChunkColumns.Count;

    public WorldGenDebugSample SampleWorldGen(int worldX, int worldZ)
    {
        float continentalness = SampleContinentalness(worldX, worldZ);
        float erosion = SampleErosion(worldX, worldZ);
        float weirdness = SampleWeirdness(worldX, worldZ);
        return new WorldGenDebugSample(
            SampleSurfaceHeight(worldX, worldZ),
            continentalness,
            erosion,
            weirdness,
            FoldWeirdness(weirdness));
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
        _chunkColumns.Clear();
        _pendingChunkColumns.Clear();
        _pendingChunkKeys.Clear();
    }

    public bool RequestChunkColumn(int chunkX, int chunkZ)
    {
        Vector2Int key = new(chunkX, chunkZ);
        if (_chunkColumns.ContainsKey(key) || _pendingChunkColumns.ContainsKey(key))
        {
            return false;
        }

        _pendingChunkColumns.Add(key, new PendingChunkColumnData(GetCurrentTimeSeconds()));
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
        while (completions < maxCompletions && _pendingChunkKeys.Count > 0)
        {
            Vector2Int key = _pendingChunkKeys[0];
            _pendingChunkKeys.RemoveAt(0);
            if (!_pendingChunkColumns.TryGetValue(key, out PendingChunkColumnData pending))
            {
                continue;
            }

            double generateStart = GetCurrentTimeSeconds();
            ChunkColumnData chunk = GenerateChunkColumn(key.x, key.y, out int[] columnHeights);
            double generateEnd = GetCurrentTimeSeconds();

            _pendingChunkColumns.Remove(key);
            _chunkColumns[key] = chunk;
            completedChunkInfos?.Add(new CompletedChunkColumnInfo(
                key,
                generateEnd - pending.RequestTime,
                0d,
                (generateEnd - generateStart) * 1000d,
                0d));
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
        return _chunkColumns.TryGetValue(new Vector2Int(chunkX, chunkZ), out ChunkColumnData chunk)
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
        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        if (!_chunkColumns.TryGetValue(new Vector2Int(chunkX, chunkZ), out ChunkColumnData chunk))
        {
            return -1;
        }

        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
        return chunk.columnHeights[(localZ * ChunkSize) + localX];
    }

    public bool SetBlock(int worldX, int worldY, int worldZ, BlockType blockType)
    {
        if (!IsInBounds(worldX, worldY, worldZ))
        {
            return false;
        }

        ChunkColumnData chunk = EnsureChunkColumnReady(FloorDiv(worldX, ChunkSize), FloorDiv(worldZ, ChunkSize));
        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
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

        ChunkColumnData chunk = EnsureChunkColumnReady(FloorDiv(worldX, ChunkSize), FloorDiv(worldZ, ChunkSize));
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

        ChunkColumnData chunk = EnsureChunkColumnReady(FloorDiv(worldX, ChunkSize), FloorDiv(worldZ, ChunkSize));
        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
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
        Vector2Int key = new(chunkX, chunkZ);
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
        int voxelCount = ChunkSize * WorldHeight * ChunkSize;
        BlockType[] blocks = new BlockType[voxelCount];
        VoxelFluid[] fluids = new VoxelFluid[voxelCount];
        ushort[] foliageIds = new ushort[voxelCount];
        columnHeights = new int[ChunkSize * ChunkSize];
        byte[] subChunkContents = new byte[SubChunkCountY];

        int maxHeight = -1;
        for (int localZ = 0; localZ < ChunkSize; localZ++)
        {
            for (int localX = 0; localX < ChunkSize; localX++)
            {
                int worldX = (chunkX * ChunkSize) + localX;
                int worldZ = (chunkZ * ChunkSize) + localZ;
                int surfaceHeight = SampleSurfaceHeight(worldX, worldZ);
                int columnIndex = (localZ * ChunkSize) + localX;
                columnHeights[columnIndex] = surfaceHeight;

                for (int worldY = 0; worldY <= surfaceHeight; worldY++)
                {
                    int index = GetIndex(localX, worldY, localZ);
                    blocks[index] = ResolveSolidBlock(surfaceHeight, worldY);
                }

                int seaLevel = Mathf.Clamp(_settings.seaLevel, 0, WorldHeight - 1);
                int waterStart = Mathf.Max(surfaceHeight + 1, 0);
                for (int worldY = waterStart; worldY <= seaLevel; worldY++)
                {
                    int index = GetIndex(localX, worldY, localZ);
                    if (blocks[index] == BlockType.Air)
                    {
                        fluids[index] = VoxelFluid.Water(100);
                    }
                }

                maxHeight = Mathf.Max(maxHeight, Mathf.Max(surfaceHeight, seaLevel));
            }
        }

        ChunkColumnData chunk = new(blocks, fluids, foliageIds, columnHeights, subChunkContents, maxHeight);
        for (int subChunkY = 0; subChunkY < SubChunkCountY; subChunkY++)
        {
            RecalculateSubChunkContents(chunk, subChunkY);
        }

        RecalculateChunkMaxHeight(chunk);
        return chunk;
    }

    private BlockType ResolveSolidBlock(int surfaceHeight, int worldY)
    {
        if (worldY == surfaceHeight)
        {
            return BlockType.Grass;
        }

        if (worldY >= surfaceHeight - 3)
        {
            return BlockType.Dirt;
        }

        return BlockType.Rock;
    }

    private int SampleSurfaceHeight(int worldX, int worldZ)
    {
        float continentalness = SampleContinentalness(worldX, worldZ);
        float erosion = SampleErosion(worldX, worldZ);
        float weirdness = SampleWeirdness(worldX, worldZ);

        float baseHeight = _settings.seaLevel + 8f;
        float continentalHeight = continentalness * 28f;
        float erosionHeight = erosion * 10f;
        float ridgeHeight = FoldWeirdness(weirdness) * 18f;
        int surfaceHeight = Mathf.RoundToInt(baseHeight + continentalHeight + erosionHeight + ridgeHeight);
        return Mathf.Clamp(surfaceHeight, 1, WorldHeight - 8);
    }

    private float SampleContinentalness(int worldX, int worldZ)
    {
        return SampleSignedNoise(worldX, worldZ, 0.0025f, 0.35f, 0.0055f, 0.15f, 17.17f);
    }

    private float SampleErosion(int worldX, int worldZ)
    {
        return SampleSignedNoise(worldX, worldZ, 0.004f, 0.4f, 0.013f, 0.12f, 91.73f);
    }

    private float SampleWeirdness(int worldX, int worldZ)
    {
        return SampleSignedNoise(worldX, worldZ, 0.0035f, 0.45f, 0.009f, 0.2f, 173.31f);
    }

    private float SampleSignedNoise(int worldX, int worldZ, float primaryScale, float primaryWeight, float detailScale, float detailWeight, float seedOffset)
    {
        float x = worldX + (_seed * 0.137f) + seedOffset;
        float z = worldZ - (_seed * 0.091f) + seedOffset * 1.7f;
        float primary = Mathf.PerlinNoise((x * primaryScale) + 10000f, (z * primaryScale) + 10000f);
        float detail = Mathf.PerlinNoise((x * detailScale) + 20000f, (z * detailScale) + 20000f);
        float combined = (primary * primaryWeight) + (detail * detailWeight);
        return Mathf.Clamp((combined * 2f) - 1f, -1f, 1f);
    }

    private static float FoldWeirdness(float weirdness)
    {
        float folded = 1f - Mathf.Abs(weirdness);
        return Mathf.Clamp((folded * 2f) - 1f, -1f, 1f);
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

        if (!_chunkColumns.TryGetValue(new Vector2Int(chunkX, chunkZ), out ChunkColumnData chunk))
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

        int chunkX = FloorDiv(worldX, ChunkSize);
        int chunkZ = FloorDiv(worldZ, ChunkSize);
        if (!_chunkColumns.TryGetValue(new Vector2Int(chunkX, chunkZ), out chunk))
        {
            return false;
        }

        int localX = Mod(worldX, ChunkSize);
        int localZ = Mod(worldZ, ChunkSize);
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
