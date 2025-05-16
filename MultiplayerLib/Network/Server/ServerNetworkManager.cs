using System.Net;
using System.Security.Cryptography;
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
    public float InactivityTimeout = 15f;
    public int TimeOut = 30;
    public ClientManager ClientManager;
    private Dictionary<int, float> _lastClientActivityTime = new Dictionary<int, float>();

    public void Init()
    {
        using (RandomNumberGenerator? rng = RandomNumberGenerator.Create())
        {
            byte[] seedBytes = new byte[4];
            rng.GetBytes(seedBytes);
            SecuritySeed = BitConverter.ToInt32(seedBytes, 0);
        }
        ClientManager = new ClientManager();
        OnSerializedBroadcast += SerializedBroadcast;
        OnSendToClient += SendToClient;
        MessageEnvelope.SetSecuritySeed(SecuritySeed);
    }

    public void StartServer(int port)
    {
        Port = port;

        try
        {
            _connection = new UdpConnection(port, this);

            Console.WriteLine($"[ServerNetworkManager] Server started on port {port}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] Failed to start server: {e.Message}");
            throw;
        }
    }

    public override void OnReceiveData(byte[] data, IPEndPoint ip)
    {
        try
        {
            MessageType messageType = _messageDispatcher.TryDispatchMessage(data, ip);

            if (messageType == MessageType.Ping || messageType == MessageType.None) return;
            UpdateClientActivity(ip);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkManager] Error processing data from {ip}: {ex.Message}");
        }
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

            Console.WriteLine($"[ServerNetworkManager] Broadcasting ping data for {playerPings.Count} players");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ServerNetworkManager] Ping broadcast failed: {e.Message}");
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
            Console.WriteLine($"[ServerNetworkManager] Client {clientId} disconnected due to inactivity");
        }

        foreach (IPEndPoint ip in clientsToRemove)
        {
            if (!ClientManager.TryGetClientId(ip, out int clientId)) continue;
            _lastClientActivityTime.Remove(clientId);

            ServerMessageDispatcher? serverDispatcher = _messageDispatcher as ServerMessageDispatcher;
            serverDispatcher?.HandleDisconnect(Array.Empty<byte>(), ip);
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
}