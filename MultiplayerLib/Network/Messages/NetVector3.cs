using System.Numerics;

namespace MultiplayerLib.Network.Messages;

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
        Vector3 outData = new Vector3();

        outData.X = BitConverter.ToSingle(message, 4);
        outData.Y = BitConverter.ToSingle(message, 8);
        outData.Z = BitConverter.ToSingle(message, 12);

        return outData;
    }

    public MessageType GetMessageType()
    {
        return MessageType.Position;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(id));
        outData.AddRange(BitConverter.GetBytes(_data.X));
        outData.AddRange(BitConverter.GetBytes(_data.Y));
        outData.AddRange(BitConverter.GetBytes(_data.Z));

        return outData.ToArray();
    }

    public int GetId(byte[] message)
    {
        return BitConverter.ToInt32(message, 0);
    }

    public byte[] Serialize(Vector3 newData, int id = -1)
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(id));
        outData.AddRange(BitConverter.GetBytes(newData.X));
        outData.AddRange(BitConverter.GetBytes(newData.Y));
        outData.AddRange(BitConverter.GetBytes(newData.Z));

        return outData.ToArray();
    }
    //Dictionary<Client,Dictionary<msgType,int>>
}