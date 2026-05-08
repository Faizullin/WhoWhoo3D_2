using System.Text.Json;
using System.Text.Json.Serialization;
using WhoWhoo3D.Truth.Topology;

namespace WhoWhoo3D.Truth.Maps;

public static class MapSerializer
{
    private const int SupportedVersion = 1;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    public static string Serialize(BoardModel board, MapMetadata metadata)
    {
        var dto = new MapDto
        {
            SchemaVersion = SupportedVersion,
            Name = metadata.Name,
            CreatedAt = metadata.CreatedAt,
            Cells = board.Cells.Values
                .OrderBy(c => c.Id.Value, StringComparer.Ordinal)
                .Select(cell => new CellDto
                {
                    Id        = cell.Id.Value,
                    Position  = new Vec3Dto { X = cell.Position.X, Y = cell.Position.Y, Z = cell.Position.Z },
                    SideCount = cell.Sides.Count,
                    Sides     = cell.Sides
                        .Where(s => !s.Stack.IsEmpty || s.Link.HasValue)
                        .OrderBy(s => s.LocalIndex)
                        .Select(s => new SideDto
                        {
                            Index = s.LocalIndex,
                            Stack = s.Stack.IsEmpty ? null : new TileStackDto
                            {
                                Owner = s.Stack.Owner.Value,
                                Count = s.Stack.Count,
                            },
                            Link = s.Link.HasValue ? new SideLinkDto
                            {
                                CellId    = s.Link.Value.Other.Cell.Value,
                                SideIndex = s.Link.Value.Other.LocalIndex,
                            } : null,
                        })
                        .ToList(),
                })
                .ToList(),
        };

        return JsonSerializer.Serialize(dto, WriteOptions);
    }

    public static BoardModel Deserialize(string json)
    {
        MapDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<MapDto>(json, ReadOptions)
                  ?? throw new MapDeserializationException("JSON root was null.");
        }
        catch (JsonException ex)
        {
            throw new MapDeserializationException($"Invalid JSON: {ex.Message}");
        }

        if (dto.SchemaVersion == 0)
            throw new MapDeserializationException("'schemaVersion' field is missing or zero.");
        if (dto.SchemaVersion != SupportedVersion)
            throw new MapDeserializationException(
                $"Unsupported schemaVersion {dto.SchemaVersion}; expected {SupportedVersion}.");

        var seenIds = new HashSet<string>();
        foreach (var cell in dto.Cells)
        {
            if (!seenIds.Add(cell.Id))
                throw new MapDeserializationException($"Duplicate cell id '{cell.Id}'.");
        }

        ValidateStackDtos(dto);
        ValidateLinkReciprocity(dto);

        var board = new BoardModel();
        foreach (var cell in dto.Cells)
        {
            var id  = new CellId(cell.Id);
            var pos = new Vec3(cell.Position.X, cell.Position.Y, cell.Position.Z);
            board.AddCell(id, pos, cell.SideCount);
        }

        foreach (var cellDto in dto.Cells)
        {
            var cellId = new CellId(cellDto.Id);
            foreach (var sideDto in cellDto.Sides)
            {
                if (sideDto.Stack is { } stackDto)
                    board.SetStack(new SideId(cellId, sideDto.Index),
                        new TileStack(new PlayerId(stackDto.Owner), stackDto.Count));
            }
        }

        // Collect all links, process each unique pair once.
        var processed = new HashSet<(SideId, SideId)>();
        foreach (var cellDto in dto.Cells)
        {
            var cellId = new CellId(cellDto.Id);
            foreach (var sideDto in cellDto.Sides)
            {
                if (sideDto.Link is not { } linkDto) continue;

                var a = new SideId(cellId, sideDto.Index);
                var b = new SideId(new CellId(linkDto.CellId), linkDto.SideIndex);

                // Canonical ordering to avoid double-processing.
                var key = string.CompareOrdinal(a.Cell.Value, b.Cell.Value) <= 0
                    ? (a, b)
                    : (b, a);

                if (!processed.Add(key)) continue;

                try
                {
                    board.LinkSides(a, b);
                }
                catch (Exception ex)
                {
                    throw new MapDeserializationException(
                        $"Cannot link {a} ↔ {b}: {ex.Message}");
                }
            }
        }

        var validation = board.Validate();
        if (!validation.IsValid)
            throw new MapDeserializationException(
                $"Map failed validation: {validation}");

        return board;
    }

    private static void ValidateLinkReciprocity(MapDto dto)
    {
        // Build set of all declared (cellId, sideIndex) → (targetCellId, targetSideIndex) links.
        var declared = new HashSet<(string cellId, int sideIndex, string targetCellId, int targetSideIndex)>();
        foreach (var cell in dto.Cells)
            foreach (var side in cell.Sides)
                if (side.Link is { } link)
                    declared.Add((cell.Id, side.Index, link.CellId, link.SideIndex));

        // Every declared link must have its reverse also declared.
        foreach (var (cellId, sideIndex, targetCellId, targetSideIndex) in declared)
        {
            if (!declared.Contains((targetCellId, targetSideIndex, cellId, sideIndex)))
                throw new MapDeserializationException(
                    $"Link from '{cellId}'[{sideIndex}] → '{targetCellId}'[{targetSideIndex}] " +
                    $"is not reciprocal: the target side does not declare the reverse link.");
        }
    }

    private static void ValidateStackDtos(MapDto dto)
    {
        foreach (var cell in dto.Cells)
        {
            foreach (var side in cell.Sides)
            {
                if (side.Stack is not { } s) continue;
                if (s.Count <= 0)
                    throw new MapDeserializationException(
                        $"Cell '{cell.Id}' side {side.Index}: stack.count must be > 0 (got {s.Count}).");
                if (s.Owner <= 0)
                    throw new MapDeserializationException(
                        $"Cell '{cell.Id}' side {side.Index}: stack.owner must be > 0 (got {s.Owner}).");
            }
        }
    }
}
