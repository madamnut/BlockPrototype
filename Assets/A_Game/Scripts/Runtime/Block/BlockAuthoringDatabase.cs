using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BlockAuthoringDatabase", menuName = "World/Block/Texture Authoring")]
public sealed class BlockAuthoringDatabase : ScriptableObject
{
    [SerializeField] private UnityEngine.Object textureRootFolder;
    [SerializeField] private string outputFolder = "Assets/A_Game/Generated/Block";
    [SerializeField] private BlockDatabase runtimeDatabase;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private BlockAuthoringEntry[] blocks = Array.Empty<BlockAuthoringEntry>();

    public UnityEngine.Object TextureRootFolder => textureRootFolder;
    public string OutputFolder => outputFolder;
    public BlockDatabase RuntimeDatabase => runtimeDatabase;
    public Material TargetMaterial => targetMaterial;
    public BlockAuthoringEntry[] Blocks => blocks;
}
