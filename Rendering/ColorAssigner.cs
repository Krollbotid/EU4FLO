// Rendering/ColorAssigner.cs
public static class ColorAssigner
{
    // стабильный псевдослучайный цвет по строке/числу
    public static Color FromHash(int seed)
    {
        var rnd = new Random(seed);
        return Color.FromArgb(
            rnd.Next(60, 220),
            rnd.Next(60, 220),
            rnd.Next(60, 220));
    }

    public static Color FromString(string s)
        => FromHash(s.GetHashCode(StringComparison.Ordinal));
}