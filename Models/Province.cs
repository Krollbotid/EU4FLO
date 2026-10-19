public sealed class Province
{
    public int Id { get; init; }
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }
    public string Name { get; init; } = "";

    public uint Packed => (uint)((R << 16) | (G << 8) | B);
}