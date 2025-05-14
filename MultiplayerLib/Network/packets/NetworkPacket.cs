using System.Net;

namespace MultiplayerLib.Network.packets;

public enum PacketType
{
    HandShake,
    HandShake_OK,
    Error,
    Ping,
    Pong,
    Message
}

public class NetworkPacket
{
    public int ClientId;
    public IPEndPoint IPEndPoint;
    public byte[] Payload;
    public float TimeStamp;
    public PacketType Type;

    public NetworkPacket(PacketType type, byte[] data, float timeStamp, int clientId = -1, IPEndPoint ipEndPoint = null)
    {
        Type = type;
        TimeStamp = timeStamp;
        ClientId = clientId;
        IPEndPoint = ipEndPoint;
        Payload = data;
    }
}