using MultiplayerLib.Game;

namespace MultiplayerLib.Network.Messages;

public class NetPlayerInput : IMessage<PlayerInput>
{
    public PlayerInput PlayerInputData;

    public MessageType GetMessageType()
    {
        return MessageType.PlayerInput;
    }

    public byte[] Serialize()
    {
        List<byte> message = new List<byte>();
        message.AddRange(BitConverter.GetBytes(PlayerInputData.MoveDirection.X));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.MoveDirection.Y));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsJumping ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsShooting ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsCrouching ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.Timestamp));

        return message.ToArray();
    }


    public PlayerInput Deserialize(byte[] message)
    {
        PlayerInput inputData = new PlayerInput();
        int offset = 0;

        inputData.MoveDirection.X = BitConverter.ToSingle(message, offset);
        offset += 4;
        inputData.MoveDirection.Y = BitConverter.ToSingle(message, offset);
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
        List<byte> message = new List<byte>();
        message.AddRange(BitConverter.GetBytes(inputData.MoveDirection.X));
        message.AddRange(BitConverter.GetBytes(inputData.MoveDirection.Y));
        message.AddRange(BitConverter.GetBytes(inputData.IsJumping ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(inputData.IsShooting ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(inputData.IsCrouching ? 1 : 0));
        message.AddRange(BitConverter.GetBytes(inputData.Timestamp));

        return message.ToArray();
    }
}