using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class VoxelFoliageMesher
{
    private const float PositionJitterRange = 0.2f;
    private const float MinX = 0.15f;
    private const float MaxX = 0.85f;
    private const float MinZ = 0.15f;
    private const float MaxZ = 0.85f;
    private const float Height = 0.9f;

    private sealed class MeshBuildBuffers
    {
        public readonly List<Vector3> vertices = new(256);
        public readonly List<int> indices = new(384);
        public readonly List<Vector2> uvs = new(256);
        public readonly List<Vector3> normals = new(256);
        public readonly List<Color32> colors = new(256);

        public void Clear()
        {
            vertices.Clear();
            indices.Clear();
            uvs.Clear();
            normals.Clear();
            colors.Clear();
        }
    }

    [System.ThreadStatic] private static MeshBuildBuffers s_buffers;

    public static bool AppendSubChunkGeometry(
        List<Vector3> vertices,
        List<int> indices,
        List<Vector2> uvs,
        List<Vector3> normals,
        List<Color32> colors,
        TerrainData terrain,
        BlockDatabase blockDatabase,
        int chunkX,
        int subChunkY,
        int chunkZ)
    {
        if (terrain == null || blockDatabase == null)
        {
            return false;
        }

        MeshBuildBuffers buffers = GetBuffers();
        buffers.Clear();
        BuildSubChunkGeometry(buffers, terrain, blockDatabase, chunkX, subChunkY, chunkZ);
        return AppendBuiltGeometry(buffers, vertices, indices, uvs, normals, colors);
    }

    public static bool RebuildSubChunkMesh(Mesh mesh, TerrainData terrain, BlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        if (mesh == null || terrain == null || blockDatabase == null)
        {
            return false;
        }

        MeshBuildBuffers buffers = GetBuffers();
        buffers.Clear();
        BuildSubChunkGeometry(buffers, terrain, blockDatabase, chunkX, subChunkY, chunkZ);

        mesh.Clear();
        if (buffers.vertices.Count == 0)
        {
            return false;
        }

        mesh.name = $"FoliageSubChunk_{chunkX}_{subChunkY}_{chunkZ}";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(buffers.vertices);
        mesh.SetTriangles(buffers.indices, 0, true);
        mesh.SetUVs(0, buffers.uvs);
        mesh.SetNormals(buffers.normals);
        mesh.SetColors(buffers.colors);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return true;
    }

    private static void BuildSubChunkGeometry(MeshBuildBuffers buffers, TerrainData terrain, BlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        int startX = chunkX * TerrainData.ChunkSize;
        int startY = subChunkY * TerrainData.SubChunkSize;
        int startZ = chunkZ * TerrainData.ChunkSize;

        for (int localY = 0; localY < TerrainData.SubChunkSize; localY++)
        {
            int worldY = startY + localY;
            for (int localZ = 0; localZ < TerrainData.ChunkSize; localZ++)
            {
                int worldZ = startZ + localZ;
                for (int localX = 0; localX < TerrainData.ChunkSize; localX++)
                {
                    int worldX = startX + localX;
                    ushort foliageId = terrain.GetFoliageId(worldX, worldY, worldZ);
                    if (foliageId == 0 || !blockDatabase.HasDefinition(foliageId))
                    {
                        continue;
                    }

                    AddCrossQuad(
                        buffers,
                        localX,
                        localY,
                        localZ,
                        worldX,
                        worldY,
                        worldZ,
                        foliageId,
                        blockDatabase.GetFaceTextureLayer(foliageId, BlockFace.Front));
                }
            }
        }
    }

    private static bool AppendBuiltGeometry(
        MeshBuildBuffers buffers,
        List<Vector3> vertices,
        List<int> indices,
        List<Vector2> uvs,
        List<Vector3> normals,
        List<Color32> colors)
    {
        if (buffers.vertices.Count == 0)
        {
            return false;
        }

        int vertexOffset = vertices.Count;
        vertices.AddRange(buffers.vertices);
        uvs.AddRange(buffers.uvs);
        normals.AddRange(buffers.normals);
        colors.AddRange(buffers.colors);

        for (int i = 0; i < buffers.indices.Count; i++)
        {
            indices.Add(buffers.indices[i] + vertexOffset);
        }

        return true;
    }

    private static void AddCrossQuad(
        MeshBuildBuffers buffers,
        int localX,
        int localY,
        int localZ,
        int worldX,
        int worldY,
        int worldZ,
        ushort foliageId,
        ushort textureLayer)
    {
        Vector2 jitter = GetPositionJitter(worldX, worldY, worldZ, foliageId);
        Vector3 basePosition = new(localX + jitter.x, localY, localZ + jitter.y);
        Color32 encodedLayer = EncodeTextureLayer(textureLayer);

        AddQuad(
            buffers,
            basePosition + new Vector3(MinX, 0f, MinZ),
            basePosition + new Vector3(MaxX, 0f, MaxZ),
            basePosition + new Vector3(MaxX, Height, MaxZ),
            basePosition + new Vector3(MinX, Height, MinZ),
            new Vector3(1f, 0f, -1f).normalized,
            encodedLayer);

        AddQuad(
            buffers,
            basePosition + new Vector3(MaxX, 0f, MinZ),
            basePosition + new Vector3(MinX, 0f, MaxZ),
            basePosition + new Vector3(MinX, Height, MaxZ),
            basePosition + new Vector3(MaxX, Height, MinZ),
            new Vector3(-1f, 0f, -1f).normalized,
            encodedLayer);
    }

    private static Vector2 GetPositionJitter(int worldX, int worldY, int worldZ, ushort foliageId)
    {
        float jitterX = (HashToUnitFloat(worldX, worldY, worldZ, foliageId, 0) * 2f - 1f) * PositionJitterRange;
        float jitterZ = (HashToUnitFloat(worldX, worldY, worldZ, foliageId, 1) * 2f - 1f) * PositionJitterRange;
        return new Vector2(jitterX, jitterZ);
    }

    private static float HashToUnitFloat(int worldX, int worldY, int worldZ, ushort foliageId, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)worldX) * 16777619u;
            hash = (hash ^ (uint)worldY) * 16777619u;
            hash = (hash ^ (uint)worldZ) * 16777619u;
            hash = (hash ^ foliageId) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static void AddQuad(
        MeshBuildBuffers buffers,
        Vector3 bottomLeft,
        Vector3 bottomRight,
        Vector3 topRight,
        Vector3 topLeft,
        Vector3 normal,
        Color32 encodedLayer)
    {
        int start = buffers.vertices.Count;

        buffers.vertices.Add(bottomLeft);
        buffers.vertices.Add(bottomRight);
        buffers.vertices.Add(topRight);
        buffers.vertices.Add(topLeft);

        buffers.uvs.Add(new Vector2(0f, 0f));
        buffers.uvs.Add(new Vector2(1f, 0f));
        buffers.uvs.Add(new Vector2(1f, 1f));
        buffers.uvs.Add(new Vector2(0f, 1f));

        buffers.normals.Add(normal);
        buffers.normals.Add(normal);
        buffers.normals.Add(normal);
        buffers.normals.Add(normal);

        buffers.colors.Add(encodedLayer);
        buffers.colors.Add(encodedLayer);
        buffers.colors.Add(encodedLayer);
        buffers.colors.Add(encodedLayer);

        buffers.indices.Add(start + 0);
        buffers.indices.Add(start + 1);
        buffers.indices.Add(start + 2);
        buffers.indices.Add(start + 0);
        buffers.indices.Add(start + 2);
        buffers.indices.Add(start + 3);
    }

    private static Color32 EncodeTextureLayer(ushort textureLayer)
    {
        byte low = (byte)(textureLayer & 0xFF);
        byte high = (byte)((textureLayer >> 8) & 0xFF);
        return new Color32(low, high, 0, 255);
    }

    private static MeshBuildBuffers GetBuffers()
    {
        s_buffers ??= new MeshBuildBuffers();
        return s_buffers;
    }
}
