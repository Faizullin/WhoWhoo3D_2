using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace WhoWhoo3D.Networking;

internal sealed class LanRoomBrowser : IDisposable
{
    private const int MaxPacket = 2048;
    private const float Lifetime = 3.5f;
    private readonly byte[] _buffer = new byte[MaxPacket + 1];
    private readonly List<LanRoomListing> _rooms = new();
    private Socket _socket;
    private float _nextRetry;

    public IReadOnlyList<LanRoomListing> Rooms => _rooms;
    public bool Available => _socket != null;

    public void Tick(float now)
    {
        for (int i = _rooms.Count - 1; i >= 0; i--)
            if (now - _rooms[i].LastSeen >= Lifetime) _rooms.RemoveAt(i);
        if (_socket == null && now < _nextRetry) return;
        try
        {
            if (_socket == null)
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _socket.Bind(new IPEndPoint(IPAddress.Any, RoomAnnouncement.DiscoveryPort));
                _socket.Blocking = false;
            }
            EndPoint source = new IPEndPoint(IPAddress.Any, 0);
            for (int i = 0; i < 32 && _socket.Poll(0, SelectMode.SelectRead); i++)
            {
                int count;
                try { count = _socket.ReceiveFrom(_buffer, ref source); }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.MessageSize) { continue; }
                if (count > 0 && count <= MaxPacket)
                    ReadAnnouncement(count, ((IPEndPoint)source).Address, now);
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock) { }
        catch (SocketException)
        {
            Dispose();
            _nextRetry = now + 3;
        }
    }

    private void ReadAnnouncement(int count, IPAddress address, float now)
    {
        if (_buffer[0] != '{') return;
        RoomAnnouncement message;
        try { message = JsonUtility.FromJson<RoomAnnouncement>(Encoding.UTF8.GetString(_buffer, 0, count)); }
        catch (ArgumentException) { return; }
        if (message == null || !message.IsValid) return;

        int index = _rooms.FindIndex(room => room.Id == message.id);
        if (index < 0)
        {
            if (_rooms.Count < 64) _rooms.Add(new LanRoomListing(message, address.ToString(), now));
            return;
        }
        // A host can be heard on several interfaces. Prefer a LAN address for display and joining.
        var previous = _rooms[index];
        string endpoint = IPAddress.IsLoopback(address) ? previous.Address : address.ToString();
        _rooms[index] = new LanRoomListing(message, endpoint, now);
    }

    public void Dispose()
    {
        _socket?.Dispose();
        _socket = null;
        _rooms.Clear();
    }
}
