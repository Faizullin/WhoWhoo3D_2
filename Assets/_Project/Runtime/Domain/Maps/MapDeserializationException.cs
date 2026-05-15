using System;

namespace WhoWhoo3D.Domain.Maps;

public sealed class MapDeserializationException : Exception
{
    public MapDeserializationException(string message) : base(message) { }
}
