using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace WhoWhoo3D.Networking;

// Nonblocking length-prefixed frames. Partial reads and writes resume on the next Unity update.
internal sealed class LanPeer : IDisposable
{
    private const int MaxFrame = 256 * 1024;
    private readonly TcpClient _client;
    private readonly Queue<byte[]> _outgoing = new();
    private readonly byte[] _header = new byte[4];
    private byte[] _payload;
    private int _received, _sent, _queued;
    public int Seat;
    public bool Ready;
    public float LastReceived = Time.realtimeSinceStartup;

    public LanPeer(TcpClient client)
    {
        _client = client;
        client.NoDelay = true;
        client.Client.Blocking = false;
    }

    public void Send(LanMessage message)
    {
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
        if (body.Length > MaxFrame || _queued + body.Length + 4 > MaxFrame * 2)
            throw new IOException("LAN message queue is full.");
        byte[] frame = new byte[body.Length + 4];
        for (int i = 0; i < 4; i++) frame[i] = (byte)(body.Length >> (8 * (3 - i)));
        Buffer.BlockCopy(body, 0, frame, 4, body.Length);
        _outgoing.Enqueue(frame);
        _queued += frame.Length;
    }

    public void Flush()
    {
        var socket = _client.Client;
        while (_outgoing.Count > 0 && socket.Poll(0, SelectMode.SelectWrite))
        {
            var frame = _outgoing.Peek();
            int n = socket.Send(frame, _sent, frame.Length - _sent, SocketFlags.None, out var error);
            if (error == SocketError.WouldBlock) return;
            if (error != SocketError.Success || n == 0) throw new IOException("Connection closed while sending.");
            _sent += n;
            _queued -= n;
            if (_sent < frame.Length) return;
            _sent = 0;
            _outgoing.Dequeue();
        }
    }

    public LanMessage Read()
    {
        var socket = _client.Client;
        while (socket.Poll(0, SelectMode.SelectRead))
        {
            var buffer = _payload ?? _header;
            int n = socket.Receive(buffer, _received, buffer.Length - _received, SocketFlags.None, out var error);
            if (error == SocketError.WouldBlock) return null;
            if (error != SocketError.Success || n == 0) throw new IOException("Connection closed.");
            _received += n;
            if (_received < buffer.Length) return null;
            _received = 0;
            if (_payload == null)
            {
                int length = (_header[0] << 24) | (_header[1] << 16) | (_header[2] << 8) | _header[3];
                if (length < 2 || length > MaxFrame) throw new IOException("Invalid LAN frame length.");
                _payload = new byte[length];
            }
            else
            {
                _payload = null;
                LastReceived = Time.realtimeSinceStartup;
                return JsonUtility.FromJson<LanMessage>(Encoding.UTF8.GetString(buffer))
                    ?? throw new IOException("Invalid LAN message.");
            }
        }
        return null;
    }

    public void Dispose() => _client.Close();
}
