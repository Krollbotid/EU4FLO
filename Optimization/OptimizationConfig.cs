using System.Text.Json.Serialization;

namespace EU4FLO.Optimization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AdjacencyMode
{
    NoAdjacent,   // запретить крепости в соседних провинциях
    Homebrew,     // разрешить только если хотя бы одна из пары — «исключительная»
    AnyAdjacent   // без ограничений
}

public sealed class OptimizationConfig
{
    public string CoverageText  { get; set; } = "";
    public string RequiredText  { get; set; } = "";
    public string PreferredText { get; set; } = "";
    public string ExcludedText  { get; set; } = "";
    public string GroupsText    { get; set; } = "";
    public AdjacencyMode Mode   { get; set; } = AdjacencyMode.NoAdjacent;
}