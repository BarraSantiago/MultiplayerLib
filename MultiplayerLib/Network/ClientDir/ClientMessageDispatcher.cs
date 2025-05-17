using System.Net;
using System.Numerics;
using MultiplayerLib.Network.Factory;
using MultiplayerLib.Network.interfaces;
using MultiplayerLib.Network.Messages;
using MultiplayerLib.Network.Server;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.ClientDir;

public class ClientMessageDispatcher : BaseMessageDispatcher
{
    public static Action<object, MessageType, bool> OnSendToServer;
    public static Action OnServerDisconnect;
    public int ClientId { get; private set; } = -1;
    private Dictionary<MessageType, int> MessageSequenceTracker = new();

    protected override void InitializeMessageHandlers()
    {
        foreach (MessageType type in Enum.GetValues(typeof(MessageType)))
        {
            MessageSequenceTracker[type] = -1;
        }
        _messageHandlers[MessageType.HandShake] = HandleHandshake;
        _messageHandlers[MessageType.HandShakeResponse] = HandleHandshakeResponse;
        _messageHandlers[MessageType.Console] = HandleConsoleMessage;
        _messageHandlers[MessageType.Position] = HandlePositionUpdate;
        _messageHandlers[MessageType.Ping] = HandlePing;
        _messageHandlers[MessageType.ObjectCreate] = HandleObjectCreate;
        _messageHandlers[MessageType.ObjectDestroy] = HandleObjectDestroy;
        _messageHandlers[MessageType.ObjectUpdate] = HandleObjectUpdate;
        _messageHandlers[MessageType.Acknowledgment] = HandleAcknowledgment;
        _messageHandlers[MessageType.PingBroadcast] = HandlePingBroadcast;
        _messageHandlers[MessageType.Disconnect] = HandleDisconnect;
    }

    private void HandleHandshakeResponse(byte[] arg1, int arg2, IPEndPoint arg3)
    {
        try
        {
            HandshakeResponse response = _netHandshakeResponse.Deserialize(arg1);
            ClientId = response.ClientId;

            MessageEnvelope.SetSecuritySeed(response.SecuritySeed);

            ConsoleMessages.Log(
                $"[ClientNetworkManager] Connected to server. Client ID: {ClientId}, Security Seed: {response.SecuritySeed}");
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandleHandshakeResponse: {ex.Message}");
        }
    }

    public void HandleMessageData(MessageEnvelope envelope, IPEndPoint serverEndpoint)
    {
        try
        {
            if (envelope == null || serverEndpoint == null)
            {
                ConsoleMessages.Log("[ClientMessageDispatcher] Null envelope or server endpoint received");
                return;
            }

            MessageType messageType = envelope.MessageType;
            byte[] data = envelope.Data;
            
            if (_messageHandlers.TryGetValue(messageType, out var handler))
            {
                handler(data, envelope.MessageNumber, serverEndpoint);
            }
            else
            {
                ConsoleMessages.Log($"[ClientMessageDispatcher] No handler registered for message type {messageType}");
            }
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error handling message data: {ex.Message}");
        }
    }

    private void HandleDisconnect(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        MessageTracker.Clear();
        OnServerDisconnect?.Invoke();
    }

    private void HandlePingBroadcast(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        try
        {
            if (arg1 == null || arg1.Length < 4)
            {
                ConsoleMessages.Log("[ClientMessageDispatcher] Invalid ping broadcast data");
                return;
            }
            if(MessageSequenceTracker[MessageType.PingBroadcast] > messageNum)
            {
                return;
            }
            MessageSequenceTracker[MessageType.PingBroadcast] = messageNum;
            (int, float)[] pingData = _netPingBroadcast.Deserialize(arg1);
            foreach ((int, float) data in pingData)
            {
                int clientId = data.Item1;
                float ping = data.Item2;
            }
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandlePingBroadcast: {ex.Message}");
        }
    }

    private void HandleAcknowledgment(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        AcknowledgeMessage message = _netAcknowledge.Deserialize(arg1);


        MessageTracker.ConfirmMessage(arg2, message.MessageType, message.MessageNumber);
    }

    private void HandleHandshake(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            HandshakeResponse response = _netHandshakeResponse.Deserialize(data);
            ClientId = response.ClientId;

            MessageEnvelope.SetSecuritySeed(response.SecuritySeed);

            ConsoleMessages.Log(
                $"[ClientNetworkManager] Connected to server. Client ID: {ClientId}, Security Seed: {response.SecuritySeed}");
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandleHandshake: {ex.Message}");
        }
    }

    private void HandleConsoleMessage(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            string message = _netString.Deserialize(data);
            OnConsoleMessageReceived?.Invoke(message);
            ConsoleMessages.Log($"[ClientMessageDispatcher] Console message received: {message}");
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandleConsoleMessage: {ex.Message}");
        }
    }

    private void HandlePositionUpdate(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            if (data == null || data.Length < sizeof(float) * 3)
            {
                ConsoleMessages.Log("[ClientMessageDispatcher] Invalid position data received");
                return;
            }
            if(MessageSequenceTracker[MessageType.Position] > messageNum)
            {
                return;
            }
            MessageSequenceTracker[MessageType.Position] = messageNum;
            Vector3 position = _netVector3.Deserialize(data);
            int objectId = _netVector3.GetId(data);

            NetworkObjectFactory.Instance.UpdateObjectPosition(objectId, position);
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandlePositionUpdate: {ex.Message}");
        }
    }

    private void HandlePing(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            _currentLatency = (Time.CurrentTime - _lastPing) * 1000;
            _lastPing = Time.CurrentTime;

            OnSendToServer?.Invoke(null, MessageType.Ping, false);
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandlePing: {ex.Message}");
        }
    }

    private void HandleObjectCreate(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            NetworkObjectCreateMessage createMsg = _netCreateObject.Deserialize(data);

            NetworkObjectFactory.Instance.HandleCreateObjectMessage(createMsg);
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandleObjectCreate: {ex.Message}");
        }
    }

    private void HandleObjectDestroy(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            int networkId = BitConverter.ToInt32(data, 0);
            NetworkObjectFactory.Instance.DestroyNetworkObject(networkId);
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandleObjectDestroy: {ex.Message}");
        }
    }

    private void HandleObjectUpdate(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            int networkId = BitConverter.ToInt32(data, 0);
            MessageType objectMessageType = (MessageType)BitConverter.ToInt32(data, 4);

            byte[] payload = new byte[data.Length - 8];
            Array.Copy(data, 8, payload, 0, payload.Length);

            NetworkObject? obj = NetworkObjectFactory.Instance.GetNetworkObject(networkId);
            if (obj != null) obj.OnNetworkMessage(payload, objectMessageType);
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[ClientMessageDispatcher] Error in HandleObjectUpdate: {ex.Message}");
        }
    }
}