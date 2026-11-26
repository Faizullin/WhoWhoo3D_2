using System;
using System.Linq;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Networking;

public sealed class LanRoomListing
{
    public string Name { get; }
    public string Map { get; }
    public string Address { get; }
    public int Port { get; }
    public int OpenSeats { get; }
    public int Capacity { get; }
    internal string Id { get; }
    internal float LastSeen { get; }

    internal LanRoomListing(RoomAnnouncement message, string address, float now)
    {
        Name = message.name;
        Map = message.map;
        Port = message.port;
        Capacity = message.capacity;
        OpenSeats = message.open;
        Id = message.id;
        Address = address;
        LastSeen = now;
    }
}

[Serializable]
internal sealed class RoomAnnouncement
{
    internal const int DiscoveryPort = 47772;
    internal const string Signature = "WhoWhoo3D.room/1";
    internal const int MaxNameLength = 32;
    public string game, id, name, map;
    public int version, port, capacity, open;

    internal bool IsValid => game == Signature && version == LanMatchSession.ProtocolVersion &&
        Guid.TryParseExact(id, "N", out _) && ValidLabel(name, MaxNameLength) && ValidLabel(map, 255) &&
        port > 0 && port <= 65535 && capacity >= MatchSetup.MinPlayers && capacity <= MatchSetup.MaxPlayers &&
        open > 0 && open < capacity;

    internal static bool ValidLabel(string value, int limit) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl);
}
