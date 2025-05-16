using MultiplayerLib.Network.Server;

namespace MultiplayerLib.Network.Messages;

public class NetHandshakeResponse : IMessage<HandshakeResponse>
{
    public HandshakeResponse _data = new HandshakeResponse();
    public MessageType GetMessageType()
    {
        return MessageType.HandShakeResponse;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(_data.ClientId));
        outData.AddRange(BitConverter.GetBytes(_data.SecuritySeed));

        return outData.ToArray();
    }
    
    public byte[] Serialize(HandshakeResponse data)
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(data.ClientId));
        outData.AddRange(BitConverter.GetBytes(data.SecuritySeed));

        return outData.ToArray();
    }

    public HandshakeResponse Deserialize(byte[] message)
    {
        HandshakeResponse outData = new HandshakeResponse();

        int offset = 0;
        outData.ClientId = BitConverter.ToInt32(message, offset);
        offset += 4;
        outData.SecuritySeed = BitConverter.ToInt32(message, offset);

        return outData;
    }
}