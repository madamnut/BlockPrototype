using UnityEngine;

public sealed class TileableNoiseChunkGenerator
{
    public const int WorldSizeXZ = 640;

    private const float MacroFrequency = 1f / 320f;
    private const float DetailFrequency = 1f / 96f;
    private const float BaseSurfaceHeight = 72f;
    private const float SurfaceHeightRange = 72f;
    private const int MacroOctaves = 3;
    private const int DetailOctaves = 4;
    private const float Persistence = 0.5f;
    private const float Lacunarity = 2f;

    private readonly Vector2 _macroOffset;
    private readonly Vector2 _detailOffset;

    public TileableNoiseChunkGenerator(int seed)
    {
        Random.State previousState = Random.state;
        Random.InitState(seed);
        _macroOffset = new Vector2(Random.Range(-10000f, 10000f), Random.Range(-10000f, 10000f));
        _detailOffset = new Vector2(Random.Range(-10000f, 10000f), Random.Range(-10000f, 10000f));
        Random.state = previousState;
    }

    public int GenerateChunkColumn(int chunkX, int chunkZ, BlockType[] blocks, VoxelFluid[] fluids, int[] columnHeights)
    {
        _ = fluids;

        int maxHeight = -1;
        int chunkWorldX = chunkX * TerrainData.ChunkSize;
        int chunkWorldZ = chunkZ * TerrainData.ChunkSize;
        for (int localZ = 0; localZ < TerrainData.ChunkSize; localZ++)
        {
            int worldZ = chunkWorldZ + localZ;
            for (int localX = 0; localX < TerrainData.ChunkSize; localX++)
            {
                int worldX = chunkWorldX + localX;
                int surfaceHeight = SampleSurfaceHeight(worldX, worldZ);
                columnHeights[(localZ * TerrainData.ChunkSize) + localX] = surfaceHeight;
                maxHeight = Mathf.Max(maxHeight, surfaceHeight);

                for (int worldY = 0; worldY <= surfaceHeight; worldY++)
                {
                    blocks[GetIndex(localX, worldY, localZ)] = BlockType.Rock;
                }
            }
        }

        return maxHeight;
    }

    private int SampleSurfaceHeight(int worldX, int worldZ)
    {
        float wrappedX = PositiveModulo(worldX, WorldSizeXZ);
        float wrappedZ = PositiveModulo(worldZ, WorldSizeXZ);

        float macroNoise = SampleFractalNoise(wrappedX, wrappedZ, MacroFrequency, MacroOctaves, _macroOffset);
        float detailNoise = SampleFractalNoise(wrappedX, wrappedZ, DetailFrequency, DetailOctaves, _detailOffset);
        float combinedNoise = Mathf.Clamp01((macroNoise * 0.7f) + (detailNoise * 0.3f));
        return Mathf.RoundToInt(BaseSurfaceHeight + (combinedNoise * SurfaceHeightRange));
    }

    private float SampleFractalNoise(float worldX, float worldZ, float baseFrequency, int octaves, Vector2 offset)
    {
        float amplitude = 1f;
        float frequency = baseFrequency;
        float total = 0f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            float sampleX = worldX * frequency;
            float sampleZ = worldZ * frequency;
            float period = WorldSizeXZ * frequency;
            float octaveSample = SampleTileablePerlin(sampleX, sampleZ, period, period, offset + new Vector2(octave * 37.17f, octave * 53.41f));
            total += octaveSample * amplitude;
            amplitudeSum += amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return amplitudeSum > 0f ? total / amplitudeSum : 0f;
    }

    private static float SampleTileablePerlin(float x, float z, float periodX, float periodZ, Vector2 offset)
    {
        float u = x / periodX;
        float v = z / periodZ;

        float a = Mathf.PerlinNoise(x + offset.x, z + offset.y);
        float b = Mathf.PerlinNoise((x - periodX) + offset.x, z + offset.y);
        float c = Mathf.PerlinNoise(x + offset.x, (z - periodZ) + offset.y);
        float d = Mathf.PerlinNoise((x - periodX) + offset.x, (z - periodZ) + offset.y);

        float x0 = Mathf.Lerp(a, b, u);
        float x1 = Mathf.Lerp(c, d, u);
        return Mathf.Lerp(x0, x1, v);
    }

    private static float PositiveModulo(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static int GetIndex(int localX, int worldY, int localZ)
    {
        return ((worldY * TerrainData.ChunkSize) + localZ) * TerrainData.ChunkSize + localX;
    }
}
