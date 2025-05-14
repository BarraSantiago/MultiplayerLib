namespace MultiplayerLib.Network.Messages;

public enum MessageType
{
    None = -2,
    HandShake,
    Console,
    Position,
    Ping,
    Id,
    Acknowledgment,
    PlayerInput,
    ObjectCreate,
    ObjectDestroy,
    ObjectUpdate,
    Disconnect,
    PingBroadcast
}

public interface IMessage<T>
{
    public MessageType GetMessageType();
    public byte[] Serialize();
    public T Deserialize(byte[] message);
}