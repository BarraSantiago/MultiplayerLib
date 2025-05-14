using System.Collections.Concurrent;
using System.Numerics;

namespace MultiplayerLib.Network.Messages;

public class NetPlayers : IMessage<Dictionary<int, Vector3>>
{
    public IReadOnlyDictionary<int, Vector3> Data;

    public NetPlayers()
    {
        Data = new ConcurrentDictionary<int, Vector3>();
    }

    public MessageType GetMessageType()
    {
        return MessageType.HandShake;
    }

    public byte[] Serialize()
    {
        var outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(Data.Count));

        foreach (KeyValuePair<int, Vector3> kvp in Data)
        {
            outData.AddRange(BitConverter.GetBytes(kvp.Key));
            Vector3 position = kvp.Value;
            outData.AddRange(BitConverter.GetBytes(position.X));
            outData.AddRange(BitConverter.GetBytes(position.Y));
            outData.AddRange(BitConverter.GetBytes(position.Z));
        }

        return outData.ToArray();
    }

    public Dictionary<int, Vector3> Deserialize(byte[] message)
    {
        Dictionary<int, Vector3> outData = new();

        var offset = 0;
        var count = BitConverter.ToInt32(message, offset);
        offset += 4;

        for (var i = 0; i < count; i++)
        {
            var key = BitConverter.ToInt32(message, offset);
            offset += 4;

            var x = BitConverter.ToSingle(message, offset);
            offset += 4;
            var y = BitConverter.ToSingle(message, offset);
            offset += 4;
            var z = BitConverter.ToSingle(message, offset);
            offset += 4;

            Vector3 position = new Vector3(x, y, z);

            outData[key] = position;
        }

        return outData;
    }

    public byte[] Serialize(Vector3 pos, int id)
    {
        var outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes(1));
        outData.AddRange(BitConverter.GetBytes(id));
        outData.AddRange(BitConverter.GetBytes(pos.X));
        outData.AddRange(BitConverter.GetBytes(pos.Y));
        outData.AddRange(BitConverter.GetBytes(pos.Z));

        return outData.ToArray();
    }
}