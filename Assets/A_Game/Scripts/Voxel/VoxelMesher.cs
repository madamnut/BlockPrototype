using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class VoxelMesher
{
    private struct MaskCell
    {
        public ushort TextureLayer;
        public bool Exists;
        public bool BackFace;

        public readonly bool Matches(MaskCell other)
        {
            return Exists == other.Exists && TextureLayer == other.TextureLayer && BackFace == other.BackFace;
        }
    }

    public static Mesh BuildSubChunkMesh(VoxelTerrainData terrain, VoxelBlockDatabase blockDatabase, int chunkX, int subChunkY, int chunkZ)
    {
        List<Vector3> vertices = new(512);
        List<int> indices = new(768);
        List<Vector2> uvs = new(512);
        List<Vector3> normals = new(512);
        List<Color32> colors = new(512);

        int[] dims = { VoxelTerrainData.SubChunkSize, VoxelTerrainData.SubChunkSize, VoxelTerrainData.SubChunkSize };
        MaskCell[] mask = new MaskCell[dims[0] * dims[1]];
        int[] x = new int[3];
        int[] q = new int[3];

        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3;
            int v = (d + 2) % 3;

            q[0] = 0;
            q[1] = 0;
            q[2] = 0;
            q[d] = 1;

            for (x[d] = -1; x[d] < dims[d];)
            {
                int n = 0;

                for (x[v] = 0; x[v] < dims[v]; x[v]++)
                {
                    for (x[u] = 0; x[u] < dims[u]; x[u]++)
                    {
                        VoxelBlockType a = x[d] >= 0 ? SampleBlock(terrain, chunkX, subChunkY, chunkZ, x[0], x[1], x[2]) : VoxelBlockType.Air;
                        VoxelBlockType b = x[d] < dims[d]
                            ? SampleBlock(terrain, chunkX, subChunkY, chunkZ, x[0] + q[0], x[1] + q[1], x[2] + q[2])
                            : VoxelBlockType.Air;

                        bool aSolid = a != VoxelBlockType.Air;
                        bool bSolid = b != VoxelBlockType.Air;

                        if (aSolid == bSolid)
                        {
                            mask[n] = default;
                        }
                        else if (aSolid)
                        {
                            mask[n] = new MaskCell
                            {
                                Exists = true,
                                TextureLayer = blockDatabase.GetFaceTextureLayer((ushort)a, GetFace(d, false)),
                                BackFace = false,
                            };
                        }
                        else
                        {
                            mask[n] = new MaskCell
                            {
                                Exists = true,
                                TextureLayer = blockDatabase.GetFaceTextureLayer((ushort)b, GetFace(d, true)),
                                BackFace = true,
                            };
                        }

                        n++;
                    }
                }

                x[d]++;
                n = 0;

                for (int j = 0; j < dims[v]; j++)
                {
                    for (int i = 0; i < dims[u];)
                    {
                        MaskCell current = mask[n];
                        if (!current.Exists)
                        {
                            i++;
                            n++;
                            continue;
                        }

                        int width = 1;
                        while (i + width < dims[u] && mask[n + width].Matches(current))
                        {
                            width++;
                        }

                        int height = 1;
                        bool stop = false;
                        while (j + height < dims[v] && !stop)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                if (!mask[n + k + (height * dims[u])].Matches(current))
                                {
                                    stop = true;
                                    break;
                                }
                            }

                            if (!stop)
                            {
                                height++;
                            }
                        }

                        x[u] = i;
                        x[v] = j;

                        int[] du = { 0, 0, 0 };
                        int[] dv = { 0, 0, 0 };
                        du[u] = width;
                        dv[v] = height;

                        AddQuad(vertices, indices, uvs, normals, colors, x, du, dv, current.BackFace, current.TextureLayer);

                        for (int l = 0; l < height; l++)
                        {
                            int row = n + (l * dims[u]);
                            for (int k = 0; k < width; k++)
                            {
                                mask[row + k] = default;
                            }
                        }

                        i += width;
                        n += width;
                    }
                }
            }
        }

        if (vertices.Count == 0)
        {
            return null;
        }

        Mesh mesh = new()
        {
            name = $"SubChunk_{chunkX}_{subChunkY}_{chunkZ}",
            indexFormat = IndexFormat.UInt32,
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0, true);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);

        return mesh;
    }

    private static VoxelBlockType SampleBlock(VoxelTerrainData terrain, int chunkX, int subChunkY, int chunkZ, int localX, int localY, int localZ)
    {
        int worldX = (chunkX * VoxelTerrainData.ChunkSize) + localX;
        int worldY = (subChunkY * VoxelTerrainData.SubChunkSize) + localY;
        int worldZ = (chunkZ * VoxelTerrainData.ChunkSize) + localZ;

        return terrain.GetBlock(worldX, worldY, worldZ);
    }

    private static void AddQuad(
        List<Vector3> vertices,
        List<int> indices,
        List<Vector2> uvs,
        List<Vector3> normals,
        List<Color32> colors,
        int[] position,
        int[] du,
        int[] dv,
        bool backFace,
        ushort textureLayer)
    {
        Vector3 origin = new(position[0], position[1], position[2]);
        Vector3 uVector = new(du[0], du[1], du[2]);
        Vector3 vVector = new(dv[0], dv[1], dv[2]);
        int vertexStart = vertices.Count;
        Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(uVector, vVector));
        Color32 encodedLayer = EncodeTextureLayer(textureLayer);
        if (backFace)
        {
            faceNormal = -faceNormal;
        }

        float uvWidth = Mathf.Max(1f, Mathf.Abs(du[0]) + Mathf.Abs(du[1]) + Mathf.Abs(du[2]));
        float uvHeight = Mathf.Max(1f, Mathf.Abs(dv[0]) + Mathf.Abs(dv[1]) + Mathf.Abs(dv[2]));

        if (backFace)
        {
            vertices.Add(origin);
            vertices.Add(origin + vVector);
            vertices.Add(origin + uVector + vVector);
            vertices.Add(origin + uVector);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, uvHeight));
            uvs.Add(new Vector2(uvWidth, uvHeight));
            uvs.Add(new Vector2(uvWidth, 0f));
        }
        else
        {
            vertices.Add(origin);
            vertices.Add(origin + uVector);
            vertices.Add(origin + uVector + vVector);
            vertices.Add(origin + vVector);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(uvWidth, 0f));
            uvs.Add(new Vector2(uvWidth, uvHeight));
            uvs.Add(new Vector2(0f, uvHeight));
        }

        normals.Add(faceNormal);
        normals.Add(faceNormal);
        normals.Add(faceNormal);
        normals.Add(faceNormal);

        colors.Add(encodedLayer);
        colors.Add(encodedLayer);
        colors.Add(encodedLayer);
        colors.Add(encodedLayer);

        indices.Add(vertexStart + 0);
        indices.Add(vertexStart + 1);
        indices.Add(vertexStart + 2);
        indices.Add(vertexStart + 0);
        indices.Add(vertexStart + 2);
        indices.Add(vertexStart + 3);
    }

    private static VoxelBlockFace GetFace(int axis, bool backFace)
    {
        return axis switch
        {
            0 => backFace ? VoxelBlockFace.Left : VoxelBlockFace.Right,
            1 => backFace ? VoxelBlockFace.Bottom : VoxelBlockFace.Top,
            2 => backFace ? VoxelBlockFace.Back : VoxelBlockFace.Front,
            _ => VoxelBlockFace.Front,
        };
    }

    private static Color32 EncodeTextureLayer(ushort textureLayer)
    {
        byte low = (byte)(textureLayer & 0xFF);
        byte high = (byte)((textureLayer >> 8) & 0xFF);
        return new Color32(low, high, 0, 255);
    }
}
