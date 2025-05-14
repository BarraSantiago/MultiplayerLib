using System.Numerics;
using MultiplayerLib.Network.Factory;

namespace MultiplayerLib.Network.Messages;

public class NetCreateObject : IMessage<NetworkObjectCreateMessage>
{
    public NetworkObjectCreateMessage data;

    public MessageType GetMessageType()
    {
        throw new NotImplementedException();
    }

    public byte[] Serialize()
    {
        List<byte> serializedData = new List<byte>();
        serializedData.AddRange(BitConverter.GetBytes(data.NetworkId));
        serializedData.AddRange(BitConverter.GetBytes((int)data.PrefabType));
        serializedData.AddRange(BitConverter.GetBytes(data.Position.X));
        serializedData.AddRange(BitConverter.GetBytes(data.Position.Y));
        serializedData.AddRange(BitConverter.GetBytes(data.Position.Z));
        serializedData.AddRange(BitConverter.GetBytes(data.Color));

        return serializedData.ToArray();
    }

    public NetworkObjectCreateMessage Deserialize(byte[] message)
    {
        NetworkObjectCreateMessage newData = new NetworkObjectCreateMessage
        {
            NetworkId = BitConverter.ToInt32(message, 0),
            PrefabType = (NetObjectTypes)BitConverter.ToInt32(message, 4),
            Position = new Vector3(
                BitConverter.ToSingle(message, 8),
                BitConverter.ToSingle(message, 12),
                BitConverter.ToSingle(message, 16)
            ),
            Color = BitConverter.ToInt32(message, 32)
        };

        return newData;
    }

    public byte[] Serialize(NetworkObjectCreateMessage newData)
    {
        List<byte> serializedData = new List<byte>();
        serializedData.AddRange(BitConverter.GetBytes(newData.NetworkId));
        serializedData.AddRange(BitConverter.GetBytes((int)newData.PrefabType));
        serializedData.AddRange(BitConverter.GetBytes(newData.Position.X));
        serializedData.AddRange(BitConverter.GetBytes(newData.Position.Y));
        serializedData.AddRange(BitConverter.GetBytes(newData.Position.Z));
        serializedData.AddRange(BitConverter.GetBytes(newData.Color));

        return serializedData.ToArray();
    }
}