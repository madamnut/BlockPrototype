using System;
using UnityEngine;

[CreateAssetMenu(fileName = "VoxelBlockAuthoringDatabase", menuName = "Voxel/Block Authoring Database")]
public sealed class VoxelBlockAuthoringDatabase : ScriptableObject
{
    [SerializeField] private UnityEngine.Object textureRootFolder;
    [SerializeField] private string outputFolder = "Assets/A_Game/Generated/Voxel";
    [SerializeField] private VoxelBlockDatabase runtimeDatabase;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private VoxelBlockAuthoringEntry[] blocks = Array.Empty<VoxelBlockAuthoringEntry>();

    public UnityEngine.Object TextureRootFolder => textureRootFolder;
    public string OutputFolder => outputFolder;
    public VoxelBlockDatabase RuntimeDatabase => runtimeDatabase;
    public Material TargetMaterial => targetMaterial;
    public VoxelBlockAuthoringEntry[] Blocks => blocks;
}
