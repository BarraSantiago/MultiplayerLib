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
        message.AddRange(BitConverter.GetBytes(PlayerInputData.xMovement));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsJumping));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsShooting));
        message.AddRange(BitConverter.GetBytes(PlayerInputData.IsCrouching));

        return message.ToArray();
    }


    public PlayerInput Deserialize(byte[] message)
    {
        PlayerInput inputData = new PlayerInput();
        int offset = 0;

        inputData.xMovement = BitConverter.ToSingle(message, offset);
        offset += 4;
        inputData.IsJumping = BitConverter.ToBoolean(message, offset);
        offset += 1;
        inputData.IsShooting = BitConverter.ToBoolean(message, offset);
        offset += 1;
        inputData.IsCrouching = BitConverter.ToBoolean(message, offset);

        return inputData;
    }

    public byte[] Serialize(PlayerInput inputData)
    {
        List<byte> message = new List<byte>();
        message.AddRange(BitConverter.GetBytes(inputData.xMovement));
        message.AddRange(BitConverter.GetBytes(inputData.IsJumping));
        message.AddRange(BitConverter.GetBytes(inputData.IsShooting));
        message.AddRange(BitConverter.GetBytes(inputData.IsCrouching));

        return message.ToArray();
    }
}