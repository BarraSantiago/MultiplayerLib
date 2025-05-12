namespace Network.Messages;

public class NetVector3 : IMessage<Vector3>
{
    private static ulong _lastMsgID = 0;
    private readonly Vector3 _data;
    public int id = 0;

    public NetVector3()
    {
        _data = new Vector3();
    }

    public NetVector3(Vector3 data)
    {
        _data = data;
    }

    public Vector3 Deserialize(byte[] message)
    {
        Vector3 outData;

        outData.x = BitConverter.ToSingle(message, 4);
        outData.y = BitConverter.ToSingle(message, 8);
        outData.z = BitConverter.ToSingle(message, 12);

        return outData;
    }

    public MessageType GetMessageType()
    {
        return MessageType.Position;
    }

    public byte[] Serialize()
    {
        var outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(id));
        outData.AddRange(BitConverter.GetBytes(_data.x));
        outData.AddRange(BitConverter.GetBytes(_data.y));
        outData.AddRange(BitConverter.GetBytes(_data.z));

        return outData.ToArray();
    }

    public int GetId(byte[] message)
    {
        return BitConverter.ToInt32(message, 0);
    }

    public byte[] Serialize(Vector3 newData, int id = -1)
    {
        var outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(id));
        outData.AddRange(BitConverter.GetBytes(newData.x));
        outData.AddRange(BitConverter.GetBytes(newData.y));
        outData.AddRange(BitConverter.GetBytes(newData.z));

        return outData.ToArray();
    }
    //Dictionary<Client,Dictionary<msgType,int>>
}