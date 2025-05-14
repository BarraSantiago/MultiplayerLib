using System.Net;
using System.Numerics;
using MultiplayerLib.Game;
using MultiplayerLib.Network.ClientDir;
using MultiplayerLib.Network.Factory;
using MultiplayerLib.Network.Messages;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.interfaces;

public abstract class BaseMessageDispatcher
{
    protected const float ResendInterval = .1f;
    public static Action<string> OnConsoleMessageReceived;
    protected readonly Dictionary<MessageType, Action<byte[], IPEndPoint>> _messageHandlers;
    protected readonly NetCreateObject _netCreateObject = new();
    protected readonly NetHandShake _netHandShake = new();
    protected readonly NetPingBroadcast _netPingBroadcast = new();
    protected readonly NetPlayerInput _netPlayerInput = new();
    protected readonly NetPlayers _netPlayers = new();
    protected readonly NetString _netString = new();
    protected readonly NetVector3 _netVector3 = new();

    public readonly MessageTracker MessageTracker = new();
    protected ClientManager _clientManager;

    protected UdpConnection _connection;
    protected float _currentLatency = 0;
    protected float _lastPing;
    protected float _lastResendCheckTime;

    protected BaseMessageDispatcher(UdpConnection connection, ClientManager clientManager)
    {
        _connection = connection;
        _clientManager = clientManager;
        _messageHandlers = new Dictionary<MessageType, Action<byte[], IPEndPoint>>();
        InitializeMessageHandlers();
        InitializeAcknowledgmentHandler();
    }

    public float CurrentLatency => _currentLatency;

    protected abstract void InitializeMessageHandlers();

    protected void InitializeAcknowledgmentHandler()
    {
        _messageHandlers[MessageType.Acknowledgment] = (data, ip) =>
        {
            int offset = 0;
            MessageType ackedType = (MessageType)BitConverter.ToInt32(data, offset);
            offset += 4;
            int ackedNumber = BitConverter.ToInt32(data, offset);

            MessageTracker.ConfirmMessage(ip, ackedType, ackedNumber);
        };
    }

    public bool TryDispatchMessage(byte[] data, IPEndPoint ip)
    {
        try
        {
            if (data == null)
            {
                Console.WriteLine(
                    $"[MessageDispatcher] Dropped malformed packet from {ip}: insufficient data length ({data?.Length ?? 0} bytes)");
                return false;
            }

            MessageEnvelope envelope = MessageEnvelope.Deserialize(data);

            if (envelope.IsImportant) SendAcknowledgment(envelope.MessageType, envelope.MessageNumber, ip);

            if (_messageHandlers.TryGetValue(envelope.MessageType, out Action<byte[], IPEndPoint>? handler))
            {
                handler(envelope.Data, ip);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessageDispatcher] Error dispatching message: {ex.Message}");
            return false;
        }
    }

    protected void SendAcknowledgment(MessageType ackedType, int ackedNumber, IPEndPoint target)
    {
        List<byte> ackData = new List<byte>();
        ackData.AddRange(BitConverter.GetBytes((int)ackedType));
        ackData.AddRange(BitConverter.GetBytes(ackedNumber));

        SendMessage(ackData.ToArray(), MessageType.Acknowledgment, target, false);
    }

    public byte[] ConvertToEnvelope(byte[] data, MessageType messageType, IPEndPoint target, bool isImportant,
        bool isCritical = false)
    {
        int messageNumber = MessageTracker.GetNextMessageNumber(messageType);

        MessageEnvelope envelope = new MessageEnvelope
        {
            IsCritical = isCritical,
            MessageType = messageType,
            MessageNumber = messageNumber,
            IsImportant = isImportant,
            Data = data
        };

        byte[] serializedEnvelope = envelope.Serialize();

        if (isImportant && target != null)
            MessageTracker.AddPendingMessage(serializedEnvelope, target, messageType, messageNumber);

        return serializedEnvelope;
    }

    public virtual void SendMessage(byte[] data, MessageType messageType, IPEndPoint target, bool isImportant,
        bool isCritical = false)
    {
        int messageNumber = MessageTracker.GetNextMessageNumber(messageType);

        MessageEnvelope envelope = new MessageEnvelope
        {
            IsCritical = isCritical,
            MessageType = messageType,
            MessageNumber = messageNumber,
            IsImportant = isImportant,
            Data = data
        };

        byte[] serializedEnvelope = envelope.Serialize();

        if (isImportant) MessageTracker.AddPendingMessage(serializedEnvelope, target, messageType, messageNumber);

        if (target == null)
        {
            Console.WriteLine("[MessageDispatcher] Target endpoint is null");
            return;
        }

        AbstractNetworkManager.Instance.SendMessage(serializedEnvelope, target);
    }

    public byte[] SerializeMessage(object data, MessageType messageType, int id = -1)
    {
        switch (messageType)
        {
            case MessageType.HandShake:
                if (data is PlayerData playerData) return _netHandShake.Serialize(playerData);

                return null;

            case MessageType.Console:
                if (data is string str) return _netString.Serialize(str);
                throw new ArgumentException("Data must be string for Console messages");

            case MessageType.Position:
                if (data is Vector3 vec3) return _netVector3.Serialize(vec3, id);
                throw new ArgumentException("Data must be Vector3 for Position messages");

            case MessageType.Ping:
                return null;
            case MessageType.PingBroadcast:
                if (data is (int, float)[] pings) return _netPingBroadcast.Serialize(pings);
                throw new ArgumentException("Data must be (int, float)[] for PingBroadcast messages");

            case MessageType.Id:
                if (data is int idValue) return BitConverter.GetBytes(idValue);
                throw new ArgumentException("Data must be int for Id messages");

            case MessageType.Disconnect:
                return null;

            // TODO serialization
            case MessageType.ObjectCreate:
                if (data is NetworkObjectCreateMessage createMessage) return _netCreateObject.Serialize(createMessage);

                throw new ArgumentException("Data must be NetworkObjectCreateMessage");

            case MessageType.ObjectDestroy:
                if (data is int intData) return BitConverter.GetBytes(intData);
                return null;

            case MessageType.ObjectUpdate:
                return null;

            case MessageType.Acknowledgment:
                if (data is int ackedNumber)
                {
                    byte[] ackData = new byte[4];
                    Buffer.BlockCopy(BitConverter.GetBytes(ackedNumber), 0, ackData, 0, 4);
                    return ackData;
                }

                throw new ArgumentException("Data must be int for Acknowledgment messages");

            case MessageType.PlayerInput:
                if (data is PlayerInput input) return _netPlayerInput.Serialize(input);

                throw new ArgumentException("Data must be PlayerInput for PlayerInput messages");
            default:
                throw new ArgumentOutOfRangeException(nameof(messageType));
        }
    }

    public void CheckAndResendMessages()
    {
        float currentTime = Time.CurrentTime;
        if (currentTime - _lastResendCheckTime < ResendInterval)
            return;

        _lastResendCheckTime = currentTime;

        Dictionary<IPEndPoint, List<MessageTracker.PendingMessage>> pendingMessages =
            MessageTracker.GetPendingMessages();
        foreach (KeyValuePair<IPEndPoint, List<MessageTracker.PendingMessage>> endpointMessages in pendingMessages)
        {
            IPEndPoint target = endpointMessages.Key;
            foreach (MessageTracker.PendingMessage message in endpointMessages.Value)
                if (currentTime - message.LastSentTime >= ResendInterval)
                {
                    AbstractNetworkManager.Instance.SendMessage(message.Data, target);
                    MessageTracker.UpdateMessageSentTime(target, message.MessageType, message.MessageNumber);
                    Console.WriteLine(
                        $"[MessageDispatcher] Resending message: Type={message.MessageType}, Number={message.MessageNumber} to {target}");
                }
        }
    }

    public MessageType DeserializeMessageType(byte[] data)
    {
        if (data == null || data.Length < 4)
            throw new ArgumentException("[MessageDispatcher] Invalid byte array for deserialization");

        int messageTypeInt = BitConverter.ToInt32(data, 0);
        return (MessageType)messageTypeInt;
    }
}