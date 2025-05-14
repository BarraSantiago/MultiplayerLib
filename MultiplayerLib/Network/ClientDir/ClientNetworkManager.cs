using System.Net;
using MultiplayerLib.Network.interfaces;
using MultiplayerLib.Network.Messages;

namespace MultiplayerLib.Network.ClientDir;

public struct PlayerData
{
    public string Name;
    public int Color;
}

public class ClientNetworkManager : AbstractNetworkManager
{
    private IPEndPoint _serverEndpoint;
    //TODO update ms text
    //[SerializeField] private TMP_Text heartbeatText;

    public IPAddress ServerIPAddress { get; private set; }


    public void StartClient(IPAddress ip, int port, string pName, int color)
    {
        ServerIPAddress = ip;
        Port = port;

        try
        {
            _connection = new UdpConnection(ip, port, this);
            _messageDispatcher = new ClientMessageDispatcher(_connection, _clientManager);
            ClientMessageDispatcher.OnSendToServer += SendToServer;
            _serverEndpoint = new IPEndPoint(ip, port);

            // TODO create players game object
            //GameObject player = new GameObject();
            //player.AddComponent<Player>();

            _clientManager.AddClient(_serverEndpoint);
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

    protected override void Update()
    {
        base.Update();
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