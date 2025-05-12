namespace Network.Messages;

public class NetPlayerInput : IMessage<PlayerInput>
{
    public PlayerInput PlayerInputData;

    public MessageType GetMessageType()
    {
        return MessageType.PlayerInput;
    }

    public byte[] Serialize()
    {
        var message = new List<byte>();
        message.AddRange(BitConverter.GetBytes(PlayerInputData.MoveDirection.x));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.MoveDirection.y));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsJumping ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsShooting ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsCrouching ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.Timestamp));

        return message.ToArray();
    }


    public PlayerInput Deserialize(byte[] message)
    {
        PlayerInput inputData = new PlayerInput();
        var offset = 0;

        inputData.MoveDirection.x = BitConverter.ToSingle(message, offset);
        offset += 4;
        inputData.MoveDirection.y = BitConverter.ToSingle(message, offset);
        offset += 4;
        inputData.IsJumping = BitConverter.ToBoolean(message, offset);
        offset += 1;
        inputData.IsShooting = BitConverter.ToBoolean(message, offset);
        offset += 1;
        inputData.IsCrouching = BitConverter.ToBoolean(message, offset);
        offset += 1;
        inputData.Timestamp = BitConverter.ToSingle(message, offset);

        return inputData;
    }

    public byte[] Serialize(PlayerInput inputData)
    {
        var message = new List<byte>();
        message.AddRange(BitConverter.GetBytes(inputData.MoveDirection.x));
        message.AddRange(BitConverter.GetBytes(inputData.MoveDirection.y));
        message.AddRange(BitConverter.GetBytes(inputData.IsJumping ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(inputData.IsShooting ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(inputData.IsCrouching ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(inputData.Timestamp));

        return message.ToArray();
    }
}