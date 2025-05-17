namespace MultiplayerLib.Network.Messages;


public struct AcknowledgeMessage
{
    public MessageType MessageType;
    public int MessageNumber;
}
public class NetAcknowledge : IMessage<AcknowledgeMessage>
{
    public AcknowledgeMessage Data { get; set; }

    public MessageType GetMessageType()
    {
        return MessageType.Acknowledgment;
    }

    public byte[] Serialize()
    {
        byte[] data = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes((int)Data.MessageType), 0, data, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(Data.MessageNumber), 0, data, 4, 4);
        return data;
    }
    
    public byte[] Serialize(AcknowledgeMessage message)
    {
        byte[] data = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes((int)message.MessageType), 0, data, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(message.MessageNumber), 0, data, 4, 4);
        return data;
    }

    public AcknowledgeMessage Deserialize(byte[] message)
    {
        AcknowledgeMessage result = new AcknowledgeMessage();

        result.MessageType = (MessageType)BitConverter.ToInt32(message, 0);
        result.MessageNumber = BitConverter.ToInt32(message, 4);
        return result;
    }
}