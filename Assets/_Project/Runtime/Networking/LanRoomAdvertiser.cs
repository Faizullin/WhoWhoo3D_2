using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace WhoWhoo3D.Networking;

internal sealed class LanRoomAdvertiser : IDisposable
{
    private readonly RoomAnnouncement _room;
    private UdpClient _sender;
    private IPEndPoint[] _targets;
    private float _nextSend, _refreshTargets;

    public string Id => _room.id;

    public LanRoomAdvertiser(string name, string map, int capacity, int port)
    {
        _room = new RoomAnnouncement { game = RoomAnnouncement.Signature, id = Guid.NewGuid().ToString("N"),
            name = name, map = map, capacity = capacity, port = port, version = LanMatchSession.ProtocolVersion };
    }

    public void Tick(float now, int openSeats)
    {
        if (openSeats <= 0 || now < _nextSend) return;
        _nextSend = now + 1;
        try
        {
            if (_sender == null)
            {
                _sender = new UdpClient(AddressFamily.InterNetwork);
                _sender.EnableBroadcast = true;
                _sender.Client.Blocking = false;
                _sender.Ttl = 1;
            }
            if (_targets == null || now >= _refreshTargets)
            {
                _targets = Targets();
                _refreshTargets = now + 10;
            }
            _room.open = openSeats;
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(_room));
            foreach (var target in _targets)
            {
                try { _sender.Send(bytes, bytes.Length, target); }
                catch (SocketException) { /* Another interface may still be usable. */ }
            }
        }
        catch (SocketException) { Dispose(); }
    }

    private static IPEndPoint[] Targets()
    {
        var addresses = new HashSet<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
        // The limited broadcast and loopback also work when an adapter cannot report its mask.
        try { AddSubnetBroadcasts(addresses); }
        catch (NetworkInformationException) { }
        catch (NotSupportedException) { }
        catch (NotImplementedException) { }
        var endpoints = new List<IPEndPoint>(addresses.Count);
        foreach (var address in addresses) endpoints.Add(new IPEndPoint(address, RoomAnnouncement.DiscoveryPort));
        return endpoints.ToArray();
    }

    private static void AddSubnetBroadcasts(HashSet<IPAddress> addresses)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Ppp || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
            foreach (var entry in nic.GetIPProperties().UnicastAddresses)
            {
                if (entry.Address.AddressFamily != AddressFamily.InterNetwork || entry.IPv4Mask == null) continue;
                var mask = entry.IPv4Mask.GetAddressBytes();
                if (mask[3] >= 254) continue; // /31 and /32 links have no broadcast address.
                var address = entry.Address.GetAddressBytes();
                for (int i = 0; i < address.Length; i++) address[i] |= (byte)~mask[i];
                addresses.Add(new IPAddress(address));
            }
        }
    }

    public void Dispose()
    {
        _sender?.Close();
        _sender = null;
    }
}
