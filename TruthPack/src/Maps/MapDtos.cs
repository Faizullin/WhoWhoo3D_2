using System.Text.Json.Serialization;

namespace WhoWhoo3D.Truth.Maps;

internal sealed class MapDto
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("name")]          public string Name { get; set; } = "";
    [JsonPropertyName("createdAt")]     public string CreatedAt { get; set; } = "";
    [JsonPropertyName("cells")]         public List<CellDto> Cells { get; set; } = new();
}

internal sealed class CellDto
{
    [JsonPropertyName("id")]        public string Id { get; set; } = "";
    [JsonPropertyName("position")]  public Vec3Dto Position { get; set; } = new();
    [JsonPropertyName("sideCount")] public int SideCount { get; set; }
    [JsonPropertyName("sides")]     public List<SideDto> Sides { get; set; } = new();
}

internal sealed class SideDto
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("stack")] public TileStackDto? Stack { get; set; }
    [JsonPropertyName("link")]  public SideLinkDto? Link { get; set; }
}

internal sealed class TileStackDto
{
    [JsonPropertyName("owner")] public int Owner { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
}

internal sealed class SideLinkDto
{
    [JsonPropertyName("cellId")]    public string CellId { get; set; } = "";
    [JsonPropertyName("sideIndex")] public int SideIndex { get; set; }
}

internal sealed class Vec3Dto
{
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
}
