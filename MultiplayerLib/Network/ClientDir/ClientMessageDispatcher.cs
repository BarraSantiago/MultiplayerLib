using System.Net;
using System.Numerics;
using Network.Factory;
using Network.interfaces;
using Network.Messages;

namespace Network.ClientDir;

public class ClientMessageDispatcher : BaseMessageDispatcher
{
    public static Action<object, MessageType, bool> OnSendToServer;

    public ClientMessageDispatcher(PlayerManager playerManager, UdpConnection connection,
        ClientManager clientManager)
        : base(playerManager, connection, clientManager)
    {
    }

    protected override void InitializeMessageHandlers()
    {
        _messageHandlers[MessageType.HandShake] = HandleHandshake;
        _messageHandlers[MessageType.Console] = HandleConsoleMessage;
        _messageHandlers[MessageType.Position] = HandlePositionUpdate;
        _messageHandlers[MessageType.Ping] = HandlePing;
        _messageHandlers[MessageType.Id] = HandleIdMessage;
        _messageHandlers[MessageType.ObjectCreate] = HandleObjectCreate;
        _messageHandlers[MessageType.ObjectDestroy] = HandleObjectDestroy;
        _messageHandlers[MessageType.ObjectUpdate] = HandleObjectUpdate;
        _messageHandlers[MessageType.Acknowledgment] = HandleAcknowledgment;
        _messageHandlers[MessageType.PingBroadcast] = HandlePingBroadcast;
    }

    private void HandlePingBroadcast(byte[] arg1, IPEndPoint arg2)
    {
        try
        {
            if (arg1 == null || arg1.Length < 4)
            {
                Console.WriteLine("[ClientMessageDispatcher] Invalid ping broadcast data");
                return;
            }

            var pingData = _netPingBroadcast.Deserialize(arg1);
            foreach (var data in pingData)
            {
                var clientId = data.Item1;
                var ping = data.Item2;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandlePingBroadcast: {ex.Message}");
        }
    }

    private void HandleAcknowledgment(byte[] arg1, IPEndPoint arg2)
    {
        var ackedType = (MessageType)BitConverter.ToInt32(arg1, 0);
        var ackedNumber = BitConverter.ToInt32(arg1, 4);

        MessageTracker.ConfirmMessage(arg2, ackedType, ackedNumber);
    }

    private void HandleHandshake(byte[] data, IPEndPoint ip)
    {
        try
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandleHandshake: {ex.Message}");
        }
    }

    private void HandleConsoleMessage(byte[] data, IPEndPoint ip)
    {
        try
        {
            var message = _netString.Deserialize(data);
            OnConsoleMessageReceived?.Invoke(message);
            Console.WriteLine($"[ClientMessageDispatcher] Console message received: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandleConsoleMessage: {ex.Message}");
        }
    }

    private void HandlePositionUpdate(byte[] data, IPEndPoint ip)
    {
        try
        {
            if (data == null || data.Length < sizeof(float) * 3)
            {
                Console.WriteLine("[ClientMessageDispatcher] Invalid position data received");
                return;
            }

            Vector3 position = _netVector3.Deserialize(data);
            var objectId = _netVector3.GetId(data);

            NetworkObjectFactory.Instance.GetAllNetworkObjects()[objectId].transform.position = position;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandlePositionUpdate: {ex.Message}");
        }
    }

    private void HandlePing(byte[] data, IPEndPoint ip)
    {
        try
        {
            _currentLatency = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - _lastPing) * 1000;
            _lastPing = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;;

            OnSendToServer?.Invoke(null, MessageType.Ping, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandlePing: {ex.Message}");
        }
    }

    private void HandleIdMessage(byte[] data, IPEndPoint ip)
    {
        try
        {
            if (data == null || data.Length < 4)
            {
                Console.WriteLine("[ClientMessageDispatcher] Invalid ID message data");
                return;
            }

            var clientId = BitConverter.ToInt32(data, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandleIdMessage: {ex.Message}");
        }
    }

    private void HandleObjectCreate(byte[] data, IPEndPoint ip)
    {
        try
        {
            var createMsg = _netCreateObject.Deserialize(data);

            NetworkObjectFactory.Instance.HandleCreateObjectMessage(createMsg);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandleObjectCreate: {ex.Message}");
        }
    }

    private void HandleObjectDestroy(byte[] data, IPEndPoint ip)
    {
        try
        {
            var networkId = BitConverter.ToInt32(data, 0);
            NetworkObjectFactory.Instance.DestroyNetworkObject(networkId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandleObjectDestroy: {ex.Message}");
        }
    }

    private void HandleObjectUpdate(byte[] data, IPEndPoint ip)
    {
        try
        {
            var networkId = BitConverter.ToInt32(data, 0);
            var objectMessageType = (MessageType)BitConverter.ToInt32(data, 4);

            // Get the payload (skip first 8 bytes)
            var payload = new byte[data.Length - 8];
            Array.Copy(data, 8, payload, 0, payload.Length);

            var obj = NetworkObjectFactory.Instance.GetNetworkObject(networkId);
            if (obj != null) obj.OnNetworkMessage(payload, objectMessageType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientMessageDispatcher] Error in HandleObjectUpdate: {ex.Message}");
        }
    }
}