using System.Net;
using MultiplayerLib.Network.interfaces;
using MultiplayerLib.Network.Messages;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.ClientDir;

public struct PlayerData
{
    public string Name;
    public int Color;
}

public class ClientNetworkManager : AbstractNetworkManager
{
    private IPEndPoint _serverEndpoint;
    public float ServerTimeout = 3;

    private float _lastServerPingTime;

    //TODO update ms text
    //[SerializeField] private TMP_Text heartbeatText;
    private Dictionary<MessageType, Dictionary<int, byte[]>> _sentMessages = new();
    public IPAddress ServerIPAddress { get; private set; }
    public static Action<object, MessageType, bool> OnSendToServer;
    public static int ClientId { get; private set; } = -1;

    public void Init()
    {
        _lastServerPingTime = Time.CurrentTime;
        _messageSequenceTracker.InitializeClient(ClientId);

        foreach (MessageType type in Enum.GetValues(typeof(MessageType)))
        {
            _sentMessages[type] = new Dictionary<int, byte[]>();
        }
    }


    public void StartClient(IPAddress ip, int port, string pName, int color)
    {
        ServerIPAddress = ip;
        Port = port;
        OnSendToServer += SendToServer;
        ClientMessageDispatcher.OnServerDisconnect += Dispose;
        _lastServerPingTime = Time.CurrentTime;
        try
        {
            _connection = new UdpConnection(ip, port, this);
            _messageDispatcher = new ClientMessageDispatcher();
            ClientMessageDispatcher.OnSendToServer += SendToServer;
            _serverEndpoint = new IPEndPoint(ip, port);

            PlayerData playerData = new PlayerData
            {
                Name = pName,
                Color = color
            };
            SendToServer(playerData, MessageType.HandShake);
            ConsoleMessages.Log($"[ClientNetworkManager] Client started, connected to {ip}:{port}");
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ClientNetworkManager] Failed to start client: {e.Message}");
            throw;
        }
    }

    public void SendToServer(object data, MessageType messageType, bool isImportant = false)
    {
        try
        {
            byte[] serializedData = SerializeMessage(data, messageType);

            if (_connection == null) return;
            
            byte[] message = _messageDispatcher.ConvertToEnvelope(serializedData, messageType, _serverEndpoint, isImportant);
            _connection.Send(message);
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ClientNetworkManager] SendToServer failed: {e.Message}");
        }
    }

    public void SendToServer(byte[] data)
    {
        try
        {
            _connection?.Send(data);
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ClientNetworkManager] Send failed: {e.Message}");
        }
    }

    public override void OnReceiveData(byte[] data, IPEndPoint serverEndpoint)
    {
        try
        {
            UpdateServerPingTime();
            MessageEnvelope envelope = MessageEnvelope.Deserialize(data);

            if (!envelope.IsSortable && envelope.MessageType != MessageType.RequestResend)
            {
                UpdateServerPingTime();
                _messageDispatcher.TryDispatchMessage(data,envelope.MessageNumber, serverEndpoint);
                return;
            }

            if (envelope.MessageType == MessageType.RequestResend)
            {
                HandleResendRequest(envelope.Data, serverEndpoint);
                return;
            }
            if (envelope.IsImportant)
            {
                SendAcknowledgment(envelope.MessageType, envelope.MessageNumber, serverEndpoint);
            }

            bool inSequence = _messageSequenceTracker.CheckMessageSequence(ClientId, envelope.MessageType,
                envelope.MessageNumber, envelope.Data, out List<byte[]> messagesToProcess);

            if (!inSequence && envelope.IsImportant)
            {
                List<int> missingNumbers = _messageSequenceTracker.GetMissingMessageNumbers(
                    ClientId, envelope.MessageType);

                if (missingNumbers.Count > 0)
                {
                    ConsoleMessages.Log(
                        $"[ClientNetworkManager] Detected gap in message sequence for type {envelope.MessageType}. Missing: {string.Join(", ", missingNumbers)}");
                }
            }

            foreach (byte[] messageData in messagesToProcess)
            {
                MessageEnvelope dataEnvelope = new MessageEnvelope
                {
                    MessageType = envelope.MessageType,
                    Data = messageData
                };

                if (_messageDispatcher is ClientMessageDispatcher clientDispatcher)
                {
                    clientDispatcher.HandleMessageData(dataEnvelope, serverEndpoint);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientNetworkManager] Error processing data: {ex.Message}");
        }
    }

    private void HandleResendRequest(byte[] data, IPEndPoint serverEndpoint)
    {
        try
        {
            int offset = 0;
            MessageType requestedType = (MessageType)BitConverter.ToInt32(data, offset);
            offset += 4;

            int count = BitConverter.ToInt32(data, offset);
            offset += 4;

            ConsoleMessages.Log(
                $"[ClientNetworkManager] Server requested resend of {count} messages of type {requestedType}");

            for (int i = 0; i < count; i++)
            {
                int messageNumber = BitConverter.ToInt32(data, offset);
                offset += 4;

                if (_sentMessages.TryGetValue(requestedType, out var messageDict) &&
                    messageDict.TryGetValue(messageNumber, out byte[] storedMessage))
                {
                    SendMessage(storedMessage, serverEndpoint);
                    ConsoleMessages.Log(
                        $"[ClientNetworkManager] Resending message {messageNumber} of type {requestedType}");
                }
                else
                {
                    ConsoleMessages.Log(
                        $"[ClientNetworkManager] Requested message {messageNumber} of type {requestedType} not found in cache");
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientNetworkManager] Error handling resend request: {ex.Message}");
        }
    }

    public override void SendMessage(byte[] data, IPEndPoint target)
    {
        try
        {
            MessageEnvelope envelope = MessageEnvelope.Deserialize(data);
            if (envelope.IsImportant)
            {
                _sentMessages[envelope.MessageType][envelope.MessageNumber] = data;
            }
            _connection.Send(data);
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientNetworkManager] Error storing sent message: {ex.Message}");
            _connection.Send(data);
        }
    }

    public override void Tick()
    {
        base.Tick();

        if (_disposed) return;

        CheckServerTimeout();
        CleanupOldMessages();
    }

    private void SendAcknowledgment(MessageType ackedType, int ackedNumber, IPEndPoint target)
    {
        AcknowledgeMessage ackMessage = new AcknowledgeMessage
        {
            MessageType = ackedType,
            MessageNumber = ackedNumber
        };

        SendToServer(ackMessage, MessageType.Acknowledgment, false);
    }
    
    private void CleanupOldMessages()
    {
        const int MAX_MESSAGES_TO_KEEP = 100;

        foreach (var messageType in _sentMessages.Keys)
        {
            var messages = _sentMessages[messageType];
            if (messages.Count > MAX_MESSAGES_TO_KEEP)
            {
                var keysToRemove = messages.Keys.OrderBy(k => k).Take(messages.Count - MAX_MESSAGES_TO_KEEP);
                foreach (var key in keysToRemove.ToList())
                {
                    messages.Remove(key);
                }
            }
        }
    }

    public void UpdateServerPingTime()
    {
        _lastServerPingTime = Time.CurrentTime;
    }

    private void CheckServerTimeout()
    {
        float currentTime = Time.CurrentTime;
        if (currentTime - _lastServerPingTime > ServerTimeout)
        {
            ConsoleMessages.Log(
                "[ClientNetworkManager] Server timeout detected. No ping received in the last 3 seconds.");
            Dispose();
        }
    }

    public override void Dispose()
    {
        if (_disposed) return;

        try
        {
            SendToServer("Client disconnecting", MessageType.Console);
            SendToServer(null, MessageType.Disconnect);
            ClientMessageDispatcher.OnSendToServer -= SendToServer;

            ConsoleMessages.Log("[ClientNetworkManager] Client disconnect notification sent");
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ClientNetworkManager] Disposal error: {e.Message}");
        }

        base.Dispose();
    }
}