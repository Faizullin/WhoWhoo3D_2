using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Domain.Maps;

public static class MapSerializer
{
    public const int SchemaVersion = 2;

    public static string Serialize(BoardModel board, MapMetadata metadata)
    {
        var dto = new MapDto
        {
            SchemaVersion = SchemaVersion,
            Name = metadata.Name,
            CreatedAt = metadata.CreatedAt,
            Cells = board.Cells
                .OrderBy(kv => kv.Key.Value, StringComparer.Ordinal)
                .Select(kv => ToDto(kv.Value))
                .ToList()
        };

        return JsonConvert.SerializeObject(dto, Formatting.Indented);
    }

    private static CellDto ToDto(Cell cell)
    {
        var dto = new CellDto
        {
            Id = cell.Id.Value,
            Position = new Vec3Dto { X = cell.Position.X, Y = cell.Position.Y, Z = cell.Position.Z },
            SideCount = cell.Sides.Count
        };

        foreach (var side in cell.Sides)
        {
            if (side.Link.HasValue)
            {
                var other = side.Link.Value.Other;
                dto.Joins.Add(new JoinDto
                {
                    Side = side.LocalIndex,
                    CellId = other.Cell.Value,
                    SideIndex = other.LocalIndex
                });
            }

            foreach (var slot in side.Slots)
            {
                dto.Slots.Add(new SlotDto
                {
                    Side = side.LocalIndex,
                    Edge = slot.Id.Edge,
                    Owner = slot.Owner.Value,
                    Partner = new SlotRefDto
                    {
                        CellId = slot.Partner.Side.Cell.Value,
                        SideIndex = slot.Partner.Side.LocalIndex,
                        Edge = slot.Partner.Edge
                    }
                });
            }
        }

        return dto;
    }

    public static BoardModel Deserialize(string json)
    {
        MapDto dto;
        try
        {
            dto = JsonConvert.DeserializeObject<MapDto>(json);
        }
        catch (JsonException ex)
        {
            throw new MapDeserializationException($"Map JSON is malformed: {ex.Message}");
        }

        if (dto == null)
            throw new MapDeserializationException("Map JSON is empty.");
        if (dto.SchemaVersion != SchemaVersion)
            throw new MapDeserializationException(
                $"Unsupported schemaVersion {dto.SchemaVersion}; expected {SchemaVersion}.");

        var board = new BoardModel();

        foreach (var cellDto in dto.Cells)
        {
            if (string.IsNullOrEmpty(cellDto.Id))
                throw new MapDeserializationException("Cell is missing an id.");
            if (board.Cells.ContainsKey(new CellId(cellDto.Id)))
                throw new MapDeserializationException($"Duplicate cell id '{cellDto.Id}'.");

            var pos = cellDto.Position ?? new Vec3Dto();
            board.AddCell(new CellId(cellDto.Id), new Vec3(pos.X, pos.Y, pos.Z), cellDto.SideCount);
        }

        // Joins before slots: a join marks both faces interior, and slots are rejected there.
        ApplyJoins(board, dto);
        ApplySlots(board, dto);

        var validation = board.Validate();
        if (!validation.IsValid)
            throw new MapDeserializationException(validation.ToString());

        return board;
    }

    private static void ApplyJoins(BoardModel board, MapDto dto)
    {
        var done = new HashSet<(string, int)>();

        foreach (var cellDto in dto.Cells)
        {
            foreach (var join in cellDto.Joins ?? new List<JoinDto>())
            {
                var a = new SideId(new CellId(cellDto.Id), join.Side);
                var b = new SideId(new CellId(join.CellId), join.SideIndex);

                // Both cells list the join; apply it once.
                if (!done.Add((cellDto.Id, join.Side))) continue;
                if (done.Contains((join.CellId, join.SideIndex))) continue;
                done.Add((join.CellId, join.SideIndex));

                try
                {
                    board.JoinSides(a, b);
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
                {
                    throw new MapDeserializationException($"Invalid join {a} → {b}: {ex.Message}");
                }
            }
        }
    }

    private static void ApplySlots(BoardModel board, MapDto dto)
    {
        var created = new HashSet<(string, int, int)>();
        var owners = new List<(SlotId Slot, int Owner)>();

        foreach (var cellDto in dto.Cells)
        {
            foreach (var slotDto in cellDto.Slots ?? new List<SlotDto>())
            {
                if (slotDto.Partner == null)
                    throw new MapDeserializationException(
                        $"Slot {cellDto.Id}[{slotDto.Side}]@{slotDto.Edge} has no partner; tiles must exist in pairs.");

                var a = new SlotId(new SideId(new CellId(cellDto.Id), slotDto.Side), slotDto.Edge);
                var b = new SlotId(
                    new SideId(new CellId(slotDto.Partner.CellId), slotDto.Partner.SideIndex),
                    slotDto.Partner.Edge);

                owners.Add((a, slotDto.Owner));

                var keyA = (cellDto.Id, slotDto.Side, slotDto.Edge);
                var keyB = (slotDto.Partner.CellId, slotDto.Partner.SideIndex, slotDto.Partner.Edge);
                if (created.Contains(keyA) || created.Contains(keyB)) continue;

                created.Add(keyA);
                created.Add(keyB);

                try
                {
                    board.AddSlotPair(a, b);
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
                {
                    throw new MapDeserializationException($"Invalid slot pair {a} ↔ {b}: {ex.Message}");
                }
            }
        }

        foreach (var (slotId, owner) in owners)
        {
            if (owner < 0)
                throw new MapDeserializationException($"Slot {slotId} has negative owner {owner}.");
            if (owner == 0) continue;

            var slot = board.GetSlot(slotId);
            if (slot == null)
                throw new MapDeserializationException($"Slot {slotId} was never created.");

            slot.Owner = new PlayerId(owner);
        }
    }
}
