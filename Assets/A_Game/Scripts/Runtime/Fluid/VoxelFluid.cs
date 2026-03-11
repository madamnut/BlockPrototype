public struct VoxelFluid
{
    public byte fluidId;
    public byte amount;

    public readonly bool Exists => fluidId != 0 && amount > 0;

    public readonly VoxelFluidType FluidType => (VoxelFluidType)fluidId;

    public static VoxelFluid None => default;

    public static VoxelFluid Water(byte amount)
    {
        return new VoxelFluid
        {
            fluidId = (byte)VoxelFluidType.Water,
            amount = amount,
        };
    }
}
