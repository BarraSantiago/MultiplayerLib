using System.Text;
using Network.ClientDir;

namespace Network.Messages;

public class NetHandShake : IMessage<PlayerData>
{
    private PlayerData _data;

    public PlayerData Deserialize(byte[] message)
    {
        PlayerData outData;

        var offset = 0;
        outData.Color = BitConverter.ToInt32(message, offset);
        offset += 4;
        var stringLength = BitConverter.ToInt32(message, offset);
        offset += 4;

        outData.Name = Encoding.UTF8.GetString(message, offset, stringLength);

        return outData;
    }

    public MessageType GetMessageType()
    {
        return MessageType.HandShake;
    }

    // TODO Cliente: color, nombre Servidor: Seed, Players
    public byte[] Serialize()
    {
        var outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(_data.Color));
        var stringBytes = Encoding.UTF8.GetBytes(_data.Name);
        outData.AddRange(BitConverter.GetBytes(stringBytes.Length));
        outData.AddRange(stringBytes);

        return outData.ToArray();
    }

    public byte[] Serialize(PlayerData playerData)
    {
        var outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(playerData.Color));
        var stringBytes = Encoding.UTF8.GetBytes(playerData.Name);
        outData.AddRange(BitConverter.GetBytes(stringBytes.Length));
        outData.AddRange(stringBytes);

        return outData.ToArray();
    }
}