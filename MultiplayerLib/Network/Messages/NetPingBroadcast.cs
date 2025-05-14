namespace MultiplayerLib.Network.Messages;

public class NetPingBroadcast : IMessage<(int, float)[]>
{
    public (int, float)[] Data { get; set; }

    public MessageType GetMessageType()
    {
        return MessageType.PingBroadcast;
    }

    public byte[] Serialize()
    {
        if (Data == null || Data.Length == 0) throw new ArgumentException("Data is null or empty");

        int size = Data.Length * sizeof(float);
        byte[] message = new byte[size];
        Buffer.BlockCopy(Data, 0, message, 0, size);
        return message;
    }

    public (int, float)[] Deserialize(byte[] message)
    {
        if (message == null || message.Length == 0) throw new ArgumentException("Message is null or empty");

        int size = message.Length / (sizeof(int) + sizeof(float));
        (int, float)[] data = new (int, float)[size];
        Buffer.BlockCopy(message, 0, data, 0, message.Length);
        return data;
    }

    public byte[] Serialize((int, float)[] newData)
    {
        if (newData == null || newData.Length == 0) throw new ArgumentException("Data is null or empty");

        int size = newData.Length * sizeof(float);
        byte[] message = new byte[size];
        Buffer.BlockCopy(newData, 0, message, 0, size);
        return message;
    }
}