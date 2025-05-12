using System.Net;
using Network.Factory;
using Network.interfaces;
using Network.Messages;

namespace Network.Server;

public class ServerNetworkManager : AbstractNetworkManager
{
    public static Action<object, MessageType, int> OnSerializedBroadcast;
    public static Action<int, object, MessageType, bool> OnSendToClient;

    private float _lastHeartbeatTime;
    private float _lastPingBroadcastTime;
    private float _lastTimeoutCheck;
    public float HeartbeatInterval = 0.15f;
    public float PingBroadcastInterval = 0.50f;
    public int TimeOut = 30;

    protected override void Awake()
    {
        base.Awake();
        _clientManager.OnClientConnected += OnClientConnected;
        _clientManager.OnClientDisconnected += OnClientDisconnected;
        OnSerializedBroadcast += SerializedBroadcast;
        OnSendToClient += SendToClient;
    }

    public void StartServer(int port)
    {
        Port = port;

        try
        {
            _connection = new UdpConnection(port, this);
            _messageDispatcher = new ServerMessageDispatcher(_playerManager, _connection, _clientManager);

            Console.WriteLine($"[ServerNetworkManager] Server started on port {port}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] Failed to start server: {e.Message}");
            throw;
        }
    }

    public int GetClientId(IPEndPoint ip)
    {
        if (_clientManager.TryGetClientId(ip, out var clientId)) return clientId;

        return -1;
    }

    private void OnClientConnected(int clientId)
    {
        if (!_playerManager.HasPlayer(clientId))
        {
        }
    }

    private void OnClientDisconnected(int clientId)
    {
        _playerManager.RemovePlayer(clientId);
    }

    public void SendToClient(int clientId, object data, MessageType messageType, bool isImportant = false)
    {
        try
        {
            if (_clientManager.TryGetClient(clientId, out var client))
            {
                var serializedData = SerializeMessage(data, messageType);
                _messageDispatcher.SendMessage(serializedData, messageType, client.ipEndPoint, isImportant);
            }
            else
            {
                Console.WriteLineWarning($"[ServerNetworkManager] Cannot send to client {clientId}: client not found");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] SendToClient failed: {e.Message}");
        }
    }

    public void Broadcast(byte[] data, bool isImportant = false, MessageType messageType = MessageType.None,
        int messageNumber = -1)
    {
        try
        {
            foreach (var client in _clientManager.GetAllClients())
            {
                _connection.Send(data, client.Value.ipEndPoint);
                if (isImportant)
                    _messageDispatcher.MessageTracker.AddPendingMessage(data, client.Value.ipEndPoint, messageType,
                        messageNumber);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] Broadcast failed: {e.Message}");
        }
    }

    public void SerializedBroadcast(object data, MessageType messageType, int id = -1)
    {
        try
        {
            var serializedData = SerializeMessage(data, messageType, id);
            serializedData = _messageDispatcher.ConvertToEnvelope(serializedData, messageType, null, false);
            Broadcast(serializedData);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] Serialized broadcast failed: {e.Message}");
        }
    }

    private void SendHeartbeat()
    {
        foreach (var client in _clientManager.GetAllClients())
            _messageDispatcher.SendMessage(null, MessageType.Ping, client.Value.ipEndPoint, false);
    }

    private void CheckForTimeouts()
    {
        var clientsToRemove = _clientManager.GetTimedOutClients(TimeOut);

        foreach (var ip in clientsToRemove) _clientManager.RemoveClient(ip);
    }

    protected override void Update()
    {
        base.Update();

        if (_disposed) return;

        float currentTime = Time.realtimeSinceStartup;

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

        foreach (var valuePair in NetworkObjectFactory.Instance.GetAllNetworkObjects())
        {
            if (Mathf.Approximately(valuePair.Value.LastUpdatedPos.sqrMagnitude,
                    valuePair.Value.transform.position.sqrMagnitude)) return;
            valuePair.Value.LastUpdatedPos = valuePair.Value.transform.position;
            SerializedBroadcast(valuePair.Value.LastUpdatedPos, MessageType.Position, valuePair.Key);
        }

        if (currentTime - _lastPingBroadcastTime > PingBroadcastInterval)
        {
            BroadcastPlayerPings();
            _lastPingBroadcastTime = currentTime;
        }
    }

    private void BroadcastPlayerPings()
    {
        try
        {
            var playerPings = new Dictionary<int, float>();

            foreach (var clientPair in _clientManager.GetAllClients())
            {
                var clientId = clientPair.Key;
                var clientPing = clientPair.Value.LastHeartbeatTime;
                playerPings.Add(clientId, clientPing);
            }

            if (playerPings.Count <= 0) return;
            SerializedBroadcast(playerPings.ToArray(), MessageType.PingBroadcast);

            Console.WriteLine($"[ServerNetworkManager] Broadcasting ping data for {playerPings.Count} players");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] Ping broadcast failed: {e.Message}");
        }
    }

    public override void Dispose()
    {
        if (_disposed) return;

        try
        {
            SerializedBroadcast(null, MessageType.Disconnect);
            Console.WriteLine("[ServerNetworkManager] Server shutdown notification sent");

            _clientManager.OnClientConnected -= OnClientConnected;
            _clientManager.OnClientDisconnected -= OnClientDisconnected;
            OnSerializedBroadcast -= SerializedBroadcast;
            OnSendToClient -= SendToClient;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] Disposal error: {e.Message}");
        }

        base.Dispose();
    }
}