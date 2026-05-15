using System;

namespace WhoWhoo3D.Domain.Maps;

public sealed record MapMetadata(string Name, string CreatedAt)
{
    public static MapMetadata New(string name) =>
        new(name, DateTimeOffset.UtcNow.ToString("O"));
}
