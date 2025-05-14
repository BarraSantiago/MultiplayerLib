using System.Net;
using System.Numerics;
using MultiplayerLib.Network.ClientDir;
using MultiplayerLib.Network.Factory;
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
    public int TimeOut = 30;

    protected override void Awake()
    {
        base.Awake();
        OnSerializedBroadcast += SerializedBroadcast;
        OnSendToClient += SendToClient;
    }

    public void StartServer(int port)
    {
        Port = port;

        try
        {
            _connection = new UdpConnection(port, this);
            // TODO init message dispatcher
            //_messageDispatcher = new ServerMessageDispatcher(_connection, _clientManager);

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
        if (_clientManager.TryGetClientId(ip, out int clientId)) return clientId;

        return -1;
    }

    public void SendToClient(int clientId, object data, MessageType messageType, bool isImportant = false)
    {
        try
        {
            if (_clientManager.TryGetClient(clientId, out Client client))
            {
                byte[] serializedData = SerializeMessage(data, messageType);
                _messageDispatcher.SendMessage(serializedData, messageType, client.ipEndPoint, isImportant);
            }
            else
            {
                Console.WriteLine($"[ServerNetworkManager] Cannot send to client {clientId}: client not found");
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
            foreach (KeyValuePair<int, Client> client in _clientManager.GetAllClients())
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
            byte[] serializedData = SerializeMessage(data, messageType, id);
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
        foreach (KeyValuePair<int, Client> client in _clientManager.GetAllClients())
            _messageDispatcher.SendMessage(null, MessageType.Ping, client.Value.ipEndPoint, false);
    }

    private void CheckForTimeouts()
    {
        List<IPEndPoint> clientsToRemove = _clientManager.GetTimedOutClients(TimeOut);

        foreach (IPEndPoint ip in clientsToRemove) _clientManager.RemoveClient(ip);
    }

    protected override void Update()
    {
        base.Update();

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

        foreach (KeyValuePair<int, NetworkObject> valuePair in NetworkObjectFactory.Instance.GetAllNetworkObjects())
        {
            if (Approximately(valuePair.Value.LastUpdatedPos, valuePair.Value.CurrentPos)) return;
            valuePair.Value.LastUpdatedPos = valuePair.Value.CurrentPos;
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
            Dictionary<int, float> playerPings = new Dictionary<int, float>();

            foreach (KeyValuePair<int, Client> clientPair in _clientManager.GetAllClients())
            {
                int clientId = clientPair.Key;
                float clientPing = clientPair.Value.LastHeartbeatTime;
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

            OnSerializedBroadcast -= SerializedBroadcast;
            OnSendToClient -= SendToClient;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] Disposal error: {e.Message}");
        }

        base.Dispose();
    }

    private bool Approximately(Vector3 a, Vector3 b)
    {
        return Math.Abs(a.X - b.X) < 0.01f && Math.Abs(a.Y - b.Y) < 0.01f && Math.Abs(a.Z - b.Z) < 0.01f;
    }
}