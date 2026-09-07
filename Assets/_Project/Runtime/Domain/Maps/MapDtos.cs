using System.Collections.Generic;
using Newtonsoft.Json;

namespace WhoWhoo3D.Domain.Maps;

public sealed class MapDto
{
    [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonProperty("name")]          public string Name { get; set; } = "";
    [JsonProperty("createdAt")]     public string CreatedAt { get; set; } = "";
    [JsonProperty("cells")]         public List<CellDto> Cells { get; set; } = new();
}

public sealed class CellDto
{
    [JsonProperty("id")]        public string Id { get; set; } = "";
    [JsonProperty("position")]  public Vec3Dto Position { get; set; } = new();
    [JsonProperty("sideCount")] public int SideCount { get; set; }

    /// <summary>Faces pressed against a neighbouring cell. Both cells list the join.</summary>
    [JsonProperty("joins")] public List<JoinDto> Joins { get; set; } = new();

    /// <summary>Tile slots on this cell's exposed faces. Both halves of a pair are listed.</summary>
    [JsonProperty("slots")] public List<SlotDto> Slots { get; set; } = new();
}

public sealed class JoinDto
{
    [JsonProperty("side")]      public int Side { get; set; }
    [JsonProperty("cellId")]    public string CellId { get; set; } = "";
    [JsonProperty("sideIndex")] public int SideIndex { get; set; }
}

public sealed class SlotDto
{
    [JsonProperty("side")]    public int Side { get; set; }
    [JsonProperty("edge")]    public int Edge { get; set; }

    /// <summary>0 (or omitted) means an unclaimed white tile.</summary>
    [JsonProperty("owner")]   public int Owner { get; set; }

    [JsonProperty("partner")] public SlotRefDto Partner { get; set; }
}

public sealed class SlotRefDto
{
    [JsonProperty("cellId")]    public string CellId { get; set; } = "";
    [JsonProperty("sideIndex")] public int SideIndex { get; set; }
    [JsonProperty("edge")]      public int Edge { get; set; }
}

public sealed class Vec3Dto
{
    [JsonProperty("x")] public float X { get; set; }
    [JsonProperty("y")] public float Y { get; set; }
    [JsonProperty("z")] public float Z { get; set; }
}
