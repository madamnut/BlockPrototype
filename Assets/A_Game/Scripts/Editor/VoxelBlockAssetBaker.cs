#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VoxelBlockAssetBaker
{
    private static readonly VoxelBlockFace[] Faces =
    {
        VoxelBlockFace.Left,
        VoxelBlockFace.Right,
        VoxelBlockFace.Bottom,
        VoxelBlockFace.Top,
        VoxelBlockFace.Back,
        VoxelBlockFace.Front,
    };

    public static void Bake(VoxelBlockAuthoringDatabase authoringDatabase)
    {
        if (authoringDatabase == null)
        {
            Debug.LogError("Voxel block bake failed because the authoring database is null.");
            return;
        }

        string textureRootPath = AssetDatabase.GetAssetPath(authoringDatabase.TextureRootFolder);
        if (string.IsNullOrWhiteSpace(textureRootPath) || !AssetDatabase.IsValidFolder(textureRootPath))
        {
            Debug.LogError("Voxel block bake requires a valid texture root folder.", authoringDatabase);
            return;
        }

        VoxelBlockAuthoringEntry[] entries = authoringDatabase.Blocks;
        if (entries == null || entries.Length == 0)
        {
            Debug.LogError("Voxel block bake requires at least one block entry.", authoringDatabase);
            return;
        }

        if (!ValidateTargetMaterial(authoringDatabase.TargetMaterial, authoringDatabase))
        {
            return;
        }

        Texture2D[] allTextures = LoadTextures(textureRootPath);
        if (allTextures.Length == 0)
        {
            Debug.LogError($"Voxel block bake found no textures under '{textureRootPath}'.", authoringDatabase);
            return;
        }

        Dictionary<string, ushort> layerByAssetPath = new(StringComparer.OrdinalIgnoreCase);
        List<Texture2D> layeredTextures = new();
        List<VoxelBlockDefinition> bakedDefinitions = new(entries.Length);

        for (int i = 0; i < entries.Length; i++)
        {
            VoxelBlockAuthoringEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.BlockName))
            {
                Debug.LogError($"Voxel block bake found an empty block name for block id {entry.BlockId}.", authoringDatabase);
                return;
            }

            ushort[] layers = new ushort[6];
            for (int faceIndex = 0; faceIndex < Faces.Length; faceIndex++)
            {
                VoxelBlockFace face = Faces[faceIndex];
                Texture2D texture = FindFaceTexture(allTextures, entry.BlockName, face, authoringDatabase);
                if (texture == null)
                {
                    return;
                }

                string assetPath = AssetDatabase.GetAssetPath(texture);
                if (!layerByAssetPath.TryGetValue(assetPath, out ushort layer))
                {
                    layer = checked((ushort)layeredTextures.Count);
                    layerByAssetPath.Add(assetPath, layer);
                    layeredTextures.Add(texture);
                }

                layers[faceIndex] = layer;
            }

            bakedDefinitions.Add(new VoxelBlockDefinition(
                entry.BlockId,
                layers[0],
                layers[1],
                layers[2],
                layers[3],
                layers[4],
                layers[5]));
        }

        if (layeredTextures.Count == 0)
        {
            Debug.LogError("Voxel block bake produced no texture layers.", authoringDatabase);
            return;
        }

        string outputFolder = EnsureAssetFolder(authoringDatabase.OutputFolder);
        Texture2DArray textureArray = BuildTextureArray(layeredTextures, authoringDatabase);
        if (textureArray == null)
        {
            return;
        }

        string textureArrayPath = $"{outputFolder}/{authoringDatabase.name}_BlockTextures.asset";
        Texture2DArray textureArrayAsset = SaveTextureArray(textureArray, textureArrayPath);

        VoxelBlockDatabase runtimeDatabase = authoringDatabase.RuntimeDatabase;
        if (runtimeDatabase == null)
        {
            string databasePath = $"{outputFolder}/{authoringDatabase.name}_RuntimeDatabase.asset";
            runtimeDatabase = AssetDatabase.LoadAssetAtPath<VoxelBlockDatabase>(databasePath);
            if (runtimeDatabase == null)
            {
                runtimeDatabase = ScriptableObject.CreateInstance<VoxelBlockDatabase>();
                AssetDatabase.CreateAsset(runtimeDatabase, databasePath);
            }

            SerializedObject serializedAuthoring = new(authoringDatabase);
            serializedAuthoring.FindProperty("runtimeDatabase").objectReferenceValue = runtimeDatabase;
            serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(authoringDatabase);
        }

        runtimeDatabase.SetDefinitionsForBake(bakedDefinitions.ToArray());

        Material targetMaterial = authoringDatabase.TargetMaterial;
        if (targetMaterial != null)
        {
            targetMaterial.SetTexture("_BlockTextures", textureArrayAsset);
            EditorUtility.SetDirty(targetMaterial);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Voxel block bake completed. Blocks: {bakedDefinitions.Count}, Unique textures: {layeredTextures.Count}, TextureArray: {textureArrayPath}",
            authoringDatabase);
    }

    private static Texture2D[] LoadTextures(string textureRootPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureRootPath });
        Texture2D[] textures = new Texture2D[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        return textures;
    }

    private static Texture2D FindFaceTexture(Texture2D[] textures, string blockName, VoxelBlockFace face, Object context)
    {
        string blockToken = NormalizeToken(blockName);
        string faceToken = NormalizeToken(face.ToString());
        string expectedToken = blockToken + faceToken;

        List<Texture2D> matches = new();
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            if (texture == null)
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            string fileToken = NormalizeToken(Path.GetFileNameWithoutExtension(assetPath));
            if (fileToken == expectedToken)
            {
                matches.Add(texture);
            }
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        if (matches.Count == 0)
        {
            Debug.LogError(
                $"Voxel block bake could not find a texture for block '{blockName}' face '{face}'. Expected a file named '{blockName}_{face}'.",
                context);
            return null;
        }

        Debug.LogError(
            $"Voxel block bake found multiple textures for block '{blockName}' face '{face}'. Please keep only one matching texture.",
            context);
        return null;
    }

    private static string NormalizeToken(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            buffer[count++] = char.ToLowerInvariant(character);
        }

        return new string(buffer[..count]);
    }

    private static Texture2DArray BuildTextureArray(List<Texture2D> sourceTextures, Object context)
    {
        Texture2D firstTexture = sourceTextures[0];
        int width = firstTexture.width;
        int height = firstTexture.height;
        TextureFormat format = TextureFormat.RGBA32;

        for (int i = 0; i < sourceTextures.Count; i++)
        {
            if (sourceTextures[i].width != width || sourceTextures[i].height != height)
            {
                Debug.LogError(
                    $"Voxel block bake requires all textures to have the same size. '{sourceTextures[i].name}' is {sourceTextures[i].width}x{sourceTextures[i].height}, expected {width}x{height}.",
                    context);
                return null;
            }
        }

        Texture2DArray textureArray = new(width, height, sourceTextures.Count, format, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            anisoLevel = 0,
            name = "VoxelBlockTextures",
        };

        for (int layer = 0; layer < sourceTextures.Count; layer++)
        {
            Color32[] pixels = ReadPixels(sourceTextures[layer]);
            textureArray.SetPixels32(pixels, layer, 0);
        }

        textureArray.Apply(false, false);
        return textureArray;
    }

    private static Color32[] ReadPixels(Texture2D source)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);

        Graphics.Blit(source, renderTexture);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;

        Texture2D readable = new(source.width, source.height, TextureFormat.RGBA32, false, true);
        readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        readable.Apply(false, false);

        Color32[] pixels = readable.GetPixels32();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        UnityEngine.Object.DestroyImmediate(readable);

        return pixels;
    }

    private static Texture2DArray SaveTextureArray(Texture2DArray source, string assetPath)
    {
        Texture2DArray existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(source, assetPath);
            return source;
        }

        EditorUtility.CopySerialized(source, existing);
        EditorUtility.SetDirty(existing);
        UnityEngine.Object.DestroyImmediate(source);
        return existing;
    }

    private static string EnsureAssetFolder(string requestedPath)
    {
        string sanitizedPath = string.IsNullOrWhiteSpace(requestedPath) ? "Assets/A_Game/Generated/Voxel" : requestedPath.Trim();
        string[] parts = sanitizedPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }

        return currentPath;
    }

    private static bool ValidateTargetMaterial(Material targetMaterial, Object context)
    {
        if (targetMaterial == null)
        {
            return true;
        }

        if (targetMaterial.HasProperty("_BlockTextures"))
        {
            return true;
        }

        string shaderName = targetMaterial.shader != null ? targetMaterial.shader.name : "<none>";
        Debug.LogError(
            $"Assigned target material '{targetMaterial.name}' uses shader '{shaderName}', but voxel baking requires a material using 'YD/Voxel Texture Array Lit' or another shader with a _BlockTextures Texture2DArray property.",
            context);
        return false;
    }
}
#endif
