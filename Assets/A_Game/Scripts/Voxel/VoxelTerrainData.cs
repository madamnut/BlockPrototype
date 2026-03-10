using UnityEngine;

public sealed class VoxelTerrainData
{
    public const int ChunkSize = 16;
    public const int SubChunkSize = 16;
    public const int WorldHeight = 256;
    public const int SubChunkCountY = WorldHeight / SubChunkSize;

    private readonly int _worldSizeInChunks;
    private readonly int _worldSizeInBlocks;
    private readonly int[] _heights;
    private readonly int[] _chunkMaxHeights;

    public VoxelTerrainData(int worldSizeInChunks, int seed)
    {
        _worldSizeInChunks = worldSizeInChunks;
        _worldSizeInBlocks = _worldSizeInChunks * ChunkSize;
        _heights = new int[_worldSizeInBlocks * _worldSizeInBlocks];
        _chunkMaxHeights = new int[_worldSizeInChunks * _worldSizeInChunks];

        BuildHeightMap(seed);
    }

    public int WorldSizeInChunks => _worldSizeInChunks;

    public int WorldSizeInBlocks => _worldSizeInBlocks;

    public int GetUsedSubChunkCount(int chunkX, int chunkZ)
    {
        int maxHeight = _chunkMaxHeights[(chunkZ * _worldSizeInChunks) + chunkX];
        return Mathf.Clamp((maxHeight / SubChunkSize) + 1, 1, SubChunkCountY);
    }

    public VoxelBlockType GetBlock(int worldX, int worldY, int worldZ)
    {
        if (worldX < 0 || worldZ < 0 || worldX >= _worldSizeInBlocks || worldZ >= _worldSizeInBlocks)
        {
            return VoxelBlockType.Air;
        }

        if (worldY < 0 || worldY >= WorldHeight)
        {
            return VoxelBlockType.Air;
        }

        int surfaceHeight = _heights[(worldZ * _worldSizeInBlocks) + worldX];
        if (worldY > surfaceHeight)
        {
            return VoxelBlockType.Air;
        }

        if (worldY == 0)
        {
            return VoxelBlockType.Stone;
        }

        if (worldY == surfaceHeight)
        {
            return VoxelBlockType.Grass;
        }

        if (worldY >= surfaceHeight - 3)
        {
            return VoxelBlockType.Dirt;
        }

        return VoxelBlockType.Stone;
    }

    private void BuildHeightMap(int seed)
    {
        float coarseScale = 0.028f;
        float detailScale = 0.085f;
        float ridgeScale = 0.041f;
        float offsetX = (seed * 0.173f) + 100f;
        float offsetZ = (seed * 0.127f) + 300f;

        for (int z = 0; z < _worldSizeInBlocks; z++)
        {
            for (int x = 0; x < _worldSizeInBlocks; x++)
            {
                float coarse = Mathf.PerlinNoise((x + offsetX) * coarseScale, (z + offsetZ) * coarseScale);
                float detail = Mathf.PerlinNoise((x - offsetZ) * detailScale, (z + offsetX) * detailScale);
                float ridge = Mathf.Abs((Mathf.PerlinNoise((x + offsetX) * ridgeScale, (z - offsetZ) * ridgeScale) * 2f) - 1f);

                float elevation = 26f;
                elevation += coarse * 34f;
                elevation += detail * 10f;
                elevation -= ridge * 8f;

                int surfaceHeight = Mathf.Clamp(Mathf.RoundToInt(elevation), 12, 84);
                _heights[(z * _worldSizeInBlocks) + x] = surfaceHeight;

                int chunkX = x / ChunkSize;
                int chunkZ = z / ChunkSize;
                int chunkIndex = (chunkZ * _worldSizeInChunks) + chunkX;
                if (surfaceHeight > _chunkMaxHeights[chunkIndex])
                {
                    _chunkMaxHeights[chunkIndex] = surfaceHeight;
                }
            }
        }
    }
}
