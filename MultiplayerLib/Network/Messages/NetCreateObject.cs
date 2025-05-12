using Network.Factory;

namespace Network.Messages;

public class NetCreateObject : IMessage<NetworkObjectCreateMessage>
{
    public NetworkObjectCreateMessage data;

    public MessageType GetMessageType()
    {
        throw new NotImplementedException();
    }

    public byte[] Serialize()
    {
        var serializedData = new List<byte>();
        serializedData.AddRange(BitConverter.GetBytes(data.NetworkId));
        serializedData.AddRange(BitConverter.GetBytes((int)data.PrefabType));
        serializedData.AddRange(BitConverter.GetBytes(data.Position.x));
        serializedData.AddRange(BitConverter.GetBytes(data.Position.y));
        serializedData.AddRange(BitConverter.GetBytes(data.Position.z));
        serializedData.AddRange(BitConverter.GetBytes(data.Rotation.x));
        serializedData.AddRange(BitConverter.GetBytes(data.Rotation.y));
        serializedData.AddRange(BitConverter.GetBytes(data.Rotation.z));
        serializedData.AddRange(BitConverter.GetBytes(data.Color));

        return serializedData.ToArray();
    }

    public NetworkObjectCreateMessage Deserialize(byte[] message)
    {
        var newData = new NetworkObjectCreateMessage
        {
            NetworkId = BitConverter.ToInt32(message, 0),
            PrefabType = (NetObjectTypes)BitConverter.ToInt32(message, 4),
            Position = new Vector3(
                BitConverter.ToSingle(message, 8),
                BitConverter.ToSingle(message, 12),
                BitConverter.ToSingle(message, 16)
            ),
            Rotation = new Vector3(
                BitConverter.ToSingle(message, 20),
                BitConverter.ToSingle(message, 24),
                BitConverter.ToSingle(message, 28)
            ),
            Color = BitConverter.ToInt32(message, 32)
        };

        return newData;
    }

    public byte[] Serialize(NetworkObjectCreateMessage newData)
    {
        var serializedData = new List<byte>();
        serializedData.AddRange(BitConverter.GetBytes(newData.NetworkId));
        serializedData.AddRange(BitConverter.GetBytes((int)newData.PrefabType));
        serializedData.AddRange(BitConverter.GetBytes(newData.Position.x));
        serializedData.AddRange(BitConverter.GetBytes(newData.Position.y));
        serializedData.AddRange(BitConverter.GetBytes(newData.Position.z));
        serializedData.AddRange(BitConverter.GetBytes(newData.Rotation.x));
        serializedData.AddRange(BitConverter.GetBytes(newData.Rotation.y));
        serializedData.AddRange(BitConverter.GetBytes(newData.Rotation.z));
        serializedData.AddRange(BitConverter.GetBytes(newData.Color));

        return serializedData.ToArray();
    }
}