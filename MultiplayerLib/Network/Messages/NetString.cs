using System.Text;

namespace Network.Messages;

public class NetString : IMessage<string>
{
    public string Data;

    public NetString()
    {
        Data = string.Empty;
    }

    public NetString(string data)
    {
        Data = data;
    }

    public MessageType GetMessageType()
    {
        return MessageType.Console;
    }

    public byte[] Serialize()
    {
        var outData = new List<byte>();

        var stringBytes = Encoding.UTF8.GetBytes(Data);
        outData.AddRange(BitConverter.GetBytes(stringBytes.Length));
        outData.AddRange(stringBytes);

        return outData.ToArray();
    }

    public string Deserialize(byte[] message)
    {
        var offset = 0;
        var stringLength = BitConverter.ToInt32(message, offset);
        offset += 4;

        Data = Encoding.UTF8.GetString(message, offset, stringLength);
        return Data;
    }

    public byte[] Serialize(string newData)
    {
        var outData = new List<byte>();

        var stringBytes = Encoding.UTF8.GetBytes(newData);
        outData.AddRange(BitConverter.GetBytes(stringBytes.Length));
        outData.AddRange(stringBytes);

        return outData.ToArray();
    }
}