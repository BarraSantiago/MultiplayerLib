using System.Text;
using MultiplayerLib.Network.ClientDir;

namespace MultiplayerLib.Network.Messages;

public class NetHandShake : IMessage<PlayerData>
{
    private PlayerData _data;

    public PlayerData Deserialize(byte[] message)
    {
        PlayerData outData;

        int offset = 0;
        outData.Color = BitConverter.ToInt32(message, offset);
        offset += 4;
        int stringLength = BitConverter.ToInt32(message, offset);
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
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(_data.Color));
        byte[] stringBytes = Encoding.UTF8.GetBytes(_data.Name);
        outData.AddRange(BitConverter.GetBytes(stringBytes.Length));
        outData.AddRange(stringBytes);

        return outData.ToArray();
    }

    public byte[] Serialize(PlayerData playerData)
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(playerData.Color));
        byte[] stringBytes = Encoding.UTF8.GetBytes(playerData.Name);
        outData.AddRange(BitConverter.GetBytes(stringBytes.Length));
        outData.AddRange(stringBytes);

        return outData.ToArray();
    }
}