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

    public IPAddress ServerIPAddress { get; private set; }
    public static Action<object, MessageType, bool> OnSendToServer;

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
            Console.WriteLine($"[ClientNetworkManager] Client started, connected to {ip}:{port}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ClientNetworkManager] Failed to start client: {e.Message}");
            throw;
        }
    }
    
    public override void Tick()
    {
        base.Tick();
        
        if (_disposed) return;
        CheckServerTimeout();
    }
    
    public void SendToServer(object data, MessageType messageType, bool isImportant = false)
    {
        try
        {
            byte[] serializedData = SerializeMessage(data, messageType);

            if (_connection != null)
                _messageDispatcher.SendMessage(serializedData, messageType, _serverEndpoint, isImportant);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ClientNetworkManager] SendToServer failed: {e.Message}");
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
            Console.WriteLine($"[ClientNetworkManager] Send failed: {e.Message}");
        }
    }

    public override void SendMessage(byte[] data, IPEndPoint ipEndPoint)
    {
        _connection.Send(data);
    }
    
    public override void OnReceiveData(byte[] data, IPEndPoint ip)
    {
        try
        {
            MessageType messageType = _messageDispatcher.TryDispatchMessage(data, ip);
            
            if (messageType == MessageType.Ping)
            {
                UpdateServerPingTime();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientNetworkManager] Error processing data: {ex.Message}");
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
            Console.WriteLine("[ClientNetworkManager] Server timeout detected. No ping received in the last 3 seconds.");
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

            Console.WriteLine("[ClientNetworkManager] Client disconnect notification sent");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ClientNetworkManager] Disposal error: {e.Message}");
        }

        base.Dispose();
    }
}