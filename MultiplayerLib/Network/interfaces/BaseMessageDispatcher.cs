using System.Net;
using System.Numerics;
using MultiplayerLib.Game;
using MultiplayerLib.Network.ClientDir;
using MultiplayerLib.Network.Factory;
using MultiplayerLib.Network.Messages;
using MultiplayerLib.Network.Server;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.interfaces;

public abstract class BaseMessageDispatcher
{
    protected const float ResendInterval = .1f;
    public static Action<string> OnConsoleMessageReceived;
    protected readonly Dictionary<MessageType, Action<byte[], int, IPEndPoint>> _messageHandlers;
    protected readonly NetCreateObject _netCreateObject = new();
    protected readonly NetHandShake _netHandShake = new();
    protected readonly NetPingBroadcast _netPingBroadcast = new();
    protected readonly NetPlayerInput _netPlayerInput = new();
    protected readonly NetString _netString = new();
    protected readonly NetVector3 _netVector3 = new();
    protected readonly NetHandshakeResponse _netHandshakeResponse = new();
    
    public readonly MessageTracker MessageTracker = new();

    public Action OnUpdatePing;
    protected float _currentLatency = 0;
    protected float _lastPing;
    protected float _lastResendCheckTime;

    protected BaseMessageDispatcher()
    {
        _messageHandlers = new Dictionary<MessageType, Action<byte[], int, IPEndPoint>>();
        InitializeMessageHandlers();
        InitializeAcknowledgmentHandler();
    }

    public float CurrentLatency => _currentLatency;

    protected abstract void InitializeMessageHandlers();

    protected void InitializeAcknowledgmentHandler()
    {
        _messageHandlers[MessageType.Acknowledgment] = (data,num, ip) =>
        {
            int offset = 0;
            MessageType ackedType = (MessageType)BitConverter.ToInt32(data, offset);
            offset += 4;
            int ackedNumber = BitConverter.ToInt32(data, offset);

            MessageTracker.ConfirmMessage(ip, ackedType, ackedNumber);
        };
    }

    public virtual MessageType TryDispatchMessage(byte[] data, int envelopeMessageNumber, IPEndPoint ip)
    {
        try
        {
            if (data == null)
            {
                Console.WriteLine(
                    $"[MessageDispatcher] Dropped malformed packet from {ip}: insufficient data length ({data?.Length ?? 0} bytes)");
                return MessageType.None;
            }

            MessageEnvelope envelope = MessageEnvelope.Deserialize(data);

            if (envelope.IsImportant) SendAcknowledgment(envelope.MessageType, envelope.MessageNumber, ip);

            if (_messageHandlers.TryGetValue(envelope.MessageType, out Action<byte[], int, IPEndPoint>? handler))
            {
                handler(envelope.Data, envelopeMessageNumber, ip);
                return envelope.MessageType;
            }


            return MessageType.None;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessageDispatcher] Error dispatching message: {ex.Message}");
            return MessageType.None;
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

            case MessageType.Disconnect:
                return null;

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
            
            case MessageType.HandShakeResponse:
                if (data is HandshakeResponse handshakeResponse)
                {
                    return _netHandshakeResponse.Serialize(handshakeResponse);
                }
                throw new ArgumentException("Data must be HandshakeResponse for HandShakeResponse messages");
                
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

        Dictionary<IPEndPoint, List<PendingMessage>> pendingMessages = MessageTracker.GetPendingMessages();
        foreach (KeyValuePair<IPEndPoint, List<PendingMessage>> endpointMessages in pendingMessages)
        {
            IPEndPoint target = endpointMessages.Key;
            foreach (PendingMessage message in endpointMessages.Value)
            {
                if (currentTime - message.LastSentTime >= ResendInterval)
                {
                    AbstractNetworkManager.Instance.SendMessage(message.Data, target);
                    MessageTracker.UpdateMessageSentTime(target, message.MessageType, message.MessageNumber);
                    Console.WriteLine($"[MessageDispatcher] Resending message: Type={message.MessageType}, Number={message.MessageNumber} to {target}");
                }
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