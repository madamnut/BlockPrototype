using Unity.Collections;
using UnityEngine;

public sealed class TerrainGpuHeightGenerator : System.IDisposable
{
    private const string KernelName = "GenerateChunkHeights";

    private readonly ComputeShader _computeShader;
    private readonly int _kernelIndex = -1;
    private readonly ComputeBuffer _columnHeightsBuffer;
    private readonly ComputeBuffer _continentalnessRemapBuffer;
    private readonly ComputeBuffer _continentalnessFilterBuffer;
    private readonly int _continentalnessRemapLength;
    private readonly int _continentalnessFilterLength;
    private readonly int[] _heightReadback = new int[TerrainData.ChunkSize * TerrainData.ChunkSize];

    public TerrainGpuHeightGenerator(
        ComputeShader computeShader,
        float[] continentalnessRemapLut,
        float[] continentalnessFilterLut)
    {
        if (computeShader == null || !SystemInfo.supportsComputeShaders || !computeShader.HasKernel(KernelName))
        {
            return;
        }

        _computeShader = computeShader;
        _kernelIndex = _computeShader.FindKernel(KernelName);
        _columnHeightsBuffer = new ComputeBuffer(TerrainData.ChunkSize * TerrainData.ChunkSize, sizeof(int));
        _continentalnessRemapBuffer = CreateLutBuffer(continentalnessRemapLut, out _continentalnessRemapLength);
        _continentalnessFilterBuffer = CreateLutBuffer(continentalnessFilterLut, out _continentalnessFilterLength);
    }

    public bool TryGenerateChunkHeights(
        int chunkX,
        int chunkZ,
        int seed,
        in TerrainGenerationSettings settings,
        NativeArray<int> outputHeights)
    {
        if (_computeShader == null || _kernelIndex < 0 || _columnHeightsBuffer == null)
        {
            return false;
        }

        ContinentalnessSettings continentalness = settings.continentalness;

        _computeShader.SetInt("_ChunkX", chunkX);
        _computeShader.SetInt("_ChunkZ", chunkZ);
        _computeShader.SetInt("_Seed", seed);
        _computeShader.SetInt("_ChunkSize", TerrainData.ChunkSize);
        _computeShader.SetInt("_RegionSizeInBlocks", WorldGenPrototypeJobs.RegionSizeInBlocks);
        _computeShader.SetInt("_WorldHeight", TerrainData.WorldHeight);
        _computeShader.SetInt("_MinTerrainHeight", settings.minTerrainHeight);
        _computeShader.SetInt("_SeaLevel", settings.seaLevel);
        _computeShader.SetInt("_MaxTerrainHeight", settings.maxTerrainHeight);
        _computeShader.SetInt("_UseContinentalnessRemap", settings.useContinentalnessRemap ? 1 : 0);
        _computeShader.SetInt("_ContinentalnessRemapLutLength", _continentalnessRemapLength);
        _computeShader.SetInt("_ContinentalnessFilterLutLength", _continentalnessFilterLength);

        _computeShader.SetInt("_UseWarp", continentalness.useWarp ? 1 : 0);
        _computeShader.SetInt("_WarpOctaves", continentalness.warpOctaves);
        _computeShader.SetFloat("_WarpFrequency", continentalness.warpFrequency);
        _computeShader.SetFloat("_WarpAmplitude", continentalness.warpAmplitude);
        _computeShader.SetFloat("_WarpLacunarity", continentalness.warpLacunarity);
        _computeShader.SetFloat("_WarpGain", continentalness.warpGain);

        _computeShader.SetInt("_UseMacro", continentalness.useMacro ? 1 : 0);
        _computeShader.SetInt("_MacroOctaves", continentalness.macroOctaves);
        _computeShader.SetFloat("_MacroFrequency", continentalness.macroFrequency);
        _computeShader.SetFloat("_MacroLacunarity", continentalness.macroLacunarity);
        _computeShader.SetFloat("_MacroGain", continentalness.macroGain);
        _computeShader.SetFloat("_MacroWeight", continentalness.macroWeight);

        _computeShader.SetInt("_UseBroad", continentalness.useBroad ? 1 : 0);
        _computeShader.SetInt("_BroadOctaves", continentalness.broadOctaves);
        _computeShader.SetFloat("_BroadFrequency", continentalness.broadFrequency);
        _computeShader.SetFloat("_BroadLacunarity", continentalness.broadLacunarity);
        _computeShader.SetFloat("_BroadGain", continentalness.broadGain);
        _computeShader.SetFloat("_BroadWeight", continentalness.broadWeight);

        _computeShader.SetInt("_UseDetail", continentalness.useDetail ? 1 : 0);
        _computeShader.SetInt("_DetailOctaves", continentalness.detailOctaves);
        _computeShader.SetFloat("_DetailFrequency", continentalness.detailFrequency);
        _computeShader.SetFloat("_DetailLacunarity", continentalness.detailLacunarity);
        _computeShader.SetFloat("_DetailGain", continentalness.detailGain);
        _computeShader.SetFloat("_DetailWeight", continentalness.detailWeight);

        _computeShader.SetBuffer(_kernelIndex, "_ColumnHeights", _columnHeightsBuffer);
        _computeShader.SetBuffer(_kernelIndex, "_ContinentalnessRemapLut", _continentalnessRemapBuffer);
        _computeShader.SetBuffer(_kernelIndex, "_ContinentalnessFilterLut", _continentalnessFilterBuffer);
        _computeShader.Dispatch(_kernelIndex, TerrainData.ChunkSize / 8, TerrainData.ChunkSize / 8, 1);
        _columnHeightsBuffer.GetData(_heightReadback);

        for (int i = 0; i < _heightReadback.Length; i++)
        {
            outputHeights[i] = _heightReadback[i];
        }

        return true;
    }

    public void Dispose()
    {
        _columnHeightsBuffer?.Release();
        _continentalnessRemapBuffer?.Release();
        _continentalnessFilterBuffer?.Release();
    }

    private static ComputeBuffer CreateLutBuffer(float[] source, out int length)
    {
        if (source == null || source.Length <= 1)
        {
            length = 0;
            ComputeBuffer dummyBuffer = new ComputeBuffer(1, sizeof(float));
            dummyBuffer.SetData(new[] { 0f });
            return dummyBuffer;
        }

        length = source.Length;
        ComputeBuffer buffer = new ComputeBuffer(source.Length, sizeof(float));
        buffer.SetData(source);
        return buffer;
    }
}
