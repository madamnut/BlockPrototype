using UnityEngine;

[CreateAssetMenu(menuName = "A_Game/Terrain/World Gen Settings", fileName = "WorldGenSettings")]
public sealed class VoxelWorldGenSettingsAsset : ScriptableObject
{
    public VoxelTerrainGenerationSettings settings = VoxelTerrainGenerationSettings.Default;
}
