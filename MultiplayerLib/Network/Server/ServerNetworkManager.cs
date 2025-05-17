using System.Net;
using System.Security.Cryptography;
using MultiplayerLib.Network.ClientDir;
using MultiplayerLib.Network.interfaces;
using MultiplayerLib.Network.Messages;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.Server;

public class ServerNetworkManager : AbstractNetworkManager
{
    public static Action<object, MessageType, int> OnSerializedBroadcast;
    public static Action<int, object, MessageType, bool> OnSendToClient;

    private float _lastHeartbeatTime;
    private float _lastPingBroadcastTime;
    private float _lastTimeoutCheck;
    public float HeartbeatInterval = 0.15f;
    public float PingBroadcastInterval = 0.50f;
    public float InactivityTimeout = 15f;
    public int TimeOut = 30;
    public ClientManager ClientManager = new ClientManager();
    private Dictionary<int, float> _lastClientActivityTime = new Dictionary<int, float>();
    private Action<int> _onNewClient;

    public void Init(ref Action<int> OnNewClient)
    {
        using (RandomNumberGenerator? rng = RandomNumberGenerator.Create())
        {
            byte[] seedBytes = new byte[4];
            rng.GetBytes(seedBytes);
            SecuritySeed = BitConverter.ToInt32(seedBytes, 0);
            SecuritySeed = Math.Abs(SecuritySeed);
        }

        OnSerializedBroadcast += SerializedBroadcast;
        OnSendToClient += SendToClient;
        MessageEnvelope.SetSecuritySeed(SecuritySeed);
        _onNewClient = OnNewClient;
        _onNewClient += RegisterNewClient;
    }

    public void StartServer(int port)
    {
        Port = port;

        try
        {
            _connection = new UdpConnection(port, this);

            ConsoleMessages.Log($"[ServerNetworkManager] Server started on port {port}");
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ServerNetworkManager] Failed to start server: {e.Message}");
            throw;
        }
    }

    public override void OnReceiveData(byte[] data, IPEndPoint ip)
    {
        try
        {
            MessageEnvelope envelope = MessageEnvelope.Deserialize(data);
            int clientId = GetClientId(ip);
            
            if (envelope.IsImportant && envelope.MessageType != MessageType.Acknowledgment)
            {
                SendAcknowledgment(clientId, envelope.MessageType, envelope.MessageNumber);
            }

            if (!envelope.IsSortable)
            {
                _messageDispatcher.TryDispatchMessage(data, envelope.MessageNumber, ip);
                return;
            }

            bool inSequence = _messageSequenceTracker.CheckMessageSequence(
                clientId,
                envelope.MessageType,
                envelope.MessageNumber,
                envelope.Data,
                out List<byte[]> messagesToProcess);

            if (!inSequence)
            {
                if (envelope.IsImportant)
                {
                    List<int> missingNumbers = _messageSequenceTracker.GetMissingMessageNumbers(
                        clientId, envelope.MessageType);

                    if (missingNumbers.Count > 0)
                        RequestResend(clientId, envelope.MessageType, missingNumbers);
                }

                return;
            }

            foreach (byte[] messageData in messagesToProcess)
            {
                MessageEnvelope dataEnvelope = new MessageEnvelope
                {
                    MessageType = envelope.MessageType,
                    Data = messageData
                };

                if (_messageDispatcher is ServerMessageDispatcher serverDispatcher)
                {
                    serverDispatcher.HandleMessageData(dataEnvelope, ip);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleMessages.Log($"[NetworkManager] Error processing data from {ip}: {ex.Message}");
        }
    }

    private void SendAcknowledgment(int clientId, MessageType ackedType, int ackedNumber)
    {
        AcknowledgeMessage ackMessage = new AcknowledgeMessage
        {
            MessageType = ackedType,
            MessageNumber = ackedNumber
        };
        
        SendToClient(clientId, ackMessage, MessageType.Acknowledgment);
    }
    
    private void RequestResend(int clientId, MessageType messageType, List<int> missingNumbers)
    {
        if (!ClientManager.TryGetClient(clientId, out Client client)) return;

        List<byte> requestData = new List<byte>();
        requestData.AddRange(BitConverter.GetBytes((int)messageType));
        requestData.AddRange(BitConverter.GetBytes(missingNumbers.Count));

        foreach (int number in missingNumbers)
        {
            requestData.AddRange(BitConverter.GetBytes(number));
        }

        byte[] message = _messageDispatcher.ConvertToEnvelope(requestData.ToArray(), MessageType.RequestResend, client.ipEndPoint, true);
        _connection.Send(message, client.ipEndPoint);
        
        ConsoleMessages.Log(
            $"[ServerNetworkManager] Requesting resend of {missingNumbers.Count} messages from client {clientId}");
    }

    public void RegisterNewClient(int clientId)
    {
        _messageSequenceTracker.InitializeClient(clientId);
        _lastClientActivityTime[clientId] = Time.CurrentTime;
    }

    public int GetClientId(IPEndPoint ip)
    {
        if (ClientManager.TryGetClientId(ip, out int clientId)) return clientId;

        return -1;
    }

    public void SendToClient(int clientId, object data, MessageType messageType, bool isImportant = false)
    {
        try
        {
            if (ClientManager.TryGetClient(clientId, out Client client))
            {
                byte[] serializedData = SerializeMessage(data, messageType);
                _messageDispatcher.SendMessage(serializedData, messageType, client.ipEndPoint, isImportant);
            }
            else
            {
                ConsoleMessages.Log($"[ServerNetworkManager] Cannot send to client {clientId}: client not found");
            }
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ServerNetworkManager] SendToClient failed: {e.Message}");
        }
    }

    public void Broadcast(byte[] data, bool isImportant = false, MessageType messageType = MessageType.None,
        int messageNumber = -1)
    {
        try
        {
            foreach (KeyValuePair<int, Client> client in ClientManager.GetAllClients())
            {
                _connection.Send(data, client.Value.ipEndPoint);
                if (isImportant)
                {
                    _messageDispatcher.MessageTracker.AddPendingMessage(data, client.Value.ipEndPoint, messageType,
                        messageNumber);
                }
            }
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ServerNetworkManager] Broadcast failed: {e.Message}");
        }
    }

    public void SerializedBroadcast(object data, MessageType messageType, int id = -1)
    {
        try
        {
            byte[] serializedData = SerializeMessage(data, messageType, id);
            serializedData = _messageDispatcher.ConvertToEnvelope(serializedData, messageType, null, false);
            Broadcast(serializedData);
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ServerNetworkManager] Serialized broadcast failed: {e.Message}");
        }
    }

    private void SendHeartbeat()
    {
        foreach (KeyValuePair<int, Client> client in ClientManager.GetAllClients())
        {
            _messageDispatcher.SendMessage(null, MessageType.Ping, client.Value.ipEndPoint, false);
        }
    }

    public override void Tick()
    {
        base.Tick();

        if (_disposed) return;

        float currentTime = Time.CurrentTime;

        if (currentTime - _lastHeartbeatTime > HeartbeatInterval)
        {
            SendHeartbeat();
            _lastHeartbeatTime = currentTime;
        }

        if (currentTime - _lastTimeoutCheck > 1f)
        {
            CheckForTimeouts();
            _lastTimeoutCheck = currentTime;
        }

        if (!(currentTime - _lastPingBroadcastTime > PingBroadcastInterval)) return;
        BroadcastPlayerPings();
        _lastPingBroadcastTime = currentTime;
    }

    private void BroadcastPlayerPings()
    {
        try
        {
            Dictionary<int, float> playerPings = new Dictionary<int, float>();

            foreach (KeyValuePair<int, Client> clientPair in ClientManager.GetAllClients())
            {
                int clientId = clientPair.Key;
                float clientPing = clientPair.Value.LastHeartbeatTime;
                playerPings.Add(clientId, clientPing);
            }

            if (playerPings.Count <= 0) return;
            SerializedBroadcast(playerPings.ToArray(), MessageType.PingBroadcast);
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ServerNetworkManager] Ping broadcast failed: {e.Message}");
        }
    }

    public void UpdateClientActivity(IPEndPoint clientEndpoint)
    {
        if (ClientManager.TryGetClientId(clientEndpoint, out int clientId))
        {
            _lastClientActivityTime[clientId] = Time.CurrentTime;
        }
    }

    private void CheckForTimeouts()
    {
        List<IPEndPoint> clientsToRemove = ClientManager.GetTimedOutClients(TimeOut);

        float currentTime = Time.CurrentTime;
        foreach (KeyValuePair<int, Client> kvp in ClientManager.GetAllClients())
        {
            int clientId = kvp.Key;
            Client client = kvp.Value;

            if (!_lastClientActivityTime.ContainsKey(clientId))
            {
                _lastClientActivityTime[clientId] = currentTime;
                continue;
            }

            if (!(currentTime - _lastClientActivityTime[clientId] > InactivityTimeout)) continue;
            clientsToRemove.Add(client.ipEndPoint);
            ConsoleMessages.Log($"[ServerNetworkManager] Client {clientId} disconnected due to inactivity");
        }

        foreach (IPEndPoint ip in clientsToRemove)
        {
            if (!ClientManager.TryGetClientId(ip, out int clientId)) continue;
            _lastClientActivityTime.Remove(clientId);
            _messageSequenceTracker.RemoveClient(clientId);

            ServerMessageDispatcher? serverDispatcher = _messageDispatcher as ServerMessageDispatcher;
            serverDispatcher?.HandleDisconnect(Array.Empty<byte>(), 0, ip);
        }
    }

    public override void Dispose()
    {
        if (_disposed) return;

        try
        {
            SerializedBroadcast(null, MessageType.Disconnect);
            ConsoleMessages.Log("[ServerNetworkManager] Server shutdown notification sent");

            OnSerializedBroadcast -= SerializedBroadcast;
            OnSendToClient -= SendToClient;
            _onNewClient -= RegisterNewClient;
        }
        catch (Exception e)
        {
            ConsoleMessages.Log($"[ServerNetworkManager] Disposal error: {e.Message}");
        }

        base.Dispose();
    }
}