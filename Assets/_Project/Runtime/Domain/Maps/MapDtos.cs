using System.Collections.Generic;
using Unity.Plastic.Newtonsoft.Json;

namespace WhoWhoo3D.Domain.Maps;

internal sealed class MapDto
{
    [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonProperty("name")]          public string Name { get; set; } = "";
    [JsonProperty("createdAt")]     public string CreatedAt { get; set; } = "";
    [JsonProperty("cells")]         public List<CellDto> Cells { get; set; } = new();
}

internal sealed class CellDto
{
    [JsonProperty("id")]        public string Id { get; set; } = "";
    [JsonProperty("position")]  public Vec3Dto Position { get; set; } = new();
    [JsonProperty("sideCount")] public int SideCount { get; set; }
    [JsonProperty("sides")]     public List<SideDto> Sides { get; set; } = new();
}

internal sealed class SideDto
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("stack")] public TileStackDto Stack { get; set; }
    [JsonProperty("link")]  public SideLinkDto Link { get; set; }
}

internal sealed class TileStackDto
{
    [JsonProperty("owner")] public int Owner { get; set; }
    [JsonProperty("count")] public int Count { get; set; }
}

internal sealed class SideLinkDto
{
    [JsonProperty("cellId")]    public string CellId { get; set; } = "";
    [JsonProperty("sideIndex")] public int SideIndex { get; set; }
}

internal sealed class Vec3Dto
{
    [JsonProperty("x")] public float X { get; set; }
    [JsonProperty("y")] public float Y { get; set; }
    [JsonProperty("z")] public float Z { get; set; }
}
