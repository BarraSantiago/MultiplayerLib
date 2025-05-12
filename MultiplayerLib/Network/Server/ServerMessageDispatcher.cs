using System.Net;
using Network.ClientDir;
using Network.Factory;
using Network.interfaces;
using Network.Messages;

namespace Network.Server;

public class ServerMessageDispatcher : BaseMessageDispatcher
{
    public ServerMessageDispatcher(PlayerManager playerManager, UdpConnection connection, ClientManager clientManager)
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
        _messageHandlers[MessageType.PlayerInput] = HandlePlayerInput;
        _messageHandlers[MessageType.Acknowledgment] = HandleAcknowledgment;
        _messageHandlers[MessageType.Disconnect] = HandleDisconnect;
    }

    private void HandleDisconnect(byte[] arg1, IPEndPoint arg2)
    {
        if (!_clientManager.TryGetClientId(arg2, out var clientId))
        {
            Console.WriteLineWarning($"[ServerMessageDispatcher] Disconnect from unknown client {arg2}");
            return;
        }

        _clientManager.RemoveClient(arg2);
        NetworkObjectFactory.Instance.DestroyNetworkObject(clientId);
        ServerNetworkManager.OnSerializedBroadcast.Invoke(clientId, MessageType.ObjectDestroy, clientId);
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
            var networkObjects = NetworkObjectFactory.Instance.GetAllNetworkObjects();
            var clientId = _clientManager.AddClient(ip);
            var pData = _netHandShake.Deserialize(data);
            GameObject player = CreateNetworkObject(Vector3.zero, Vector3.zero, NetObjectTypes.Player, pData.Color)
                .gameObject;

            _playerManager.CreatePlayer(clientId, player);
            _clientManager.UpdateClientTimestamp(clientId);

            var newId = BitConverter.GetBytes((int)MessageType.Id).ToList();
            newId.AddRange(BitConverter.GetBytes(clientId));
            _connection.Send(newId.ToArray(), ip);

            ServerNetworkManager.OnSerializedBroadcast.Invoke(player.transform.position, MessageType.HandShake,
                clientId);

            _netPlayers.Data = _playerManager.GetAllPlayers();
            SendObjectsToClient(ip, networkObjects);
            var msg = ConvertToEnvelope(_netPlayers.Serialize(), MessageType.HandShake, ip, true);
            _connection.Send(msg, ip);


            Console.WriteLine($"[ServerMessageDispatcher] New client {clientId} connected from {ip}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error in HandleHandshake: {ex.Message}");
        }
    }

    private void SendObjectsToClient(IPEndPoint ip, Dictionary<int, NetworkObject> networkObjects)
    {
        foreach (var kvp in networkObjects)
        {
            var networkObject = kvp.Value;
            if (networkObject == null) continue;

            var createMsg = new NetworkObjectCreateMessage
            {
                NetworkId = networkObject.NetworkId,
                PrefabType = networkObject.PrefabType,
                Position = networkObject.transform.position,
                Rotation = networkObject.transform.rotation.eulerAngles
            };


            var msg = ConvertToEnvelope(_netCreateObject.Serialize(createMsg), MessageType.ObjectCreate, ip, true);
            _connection.Send(msg, ip);
        }
    }

    private void HandleConsoleMessage(byte[] data, IPEndPoint ip)
    {
        try
        {
            var message = _netString.Deserialize(data);
            OnConsoleMessageReceived?.Invoke(message);

            if (string.IsNullOrEmpty(message)) return;

            ServerNetworkManager.OnSerializedBroadcast.Invoke(message, MessageType.Console, -1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error in HandleConsoleMessage: {ex.Message}");
        }
    }

    private void HandlePositionUpdate(byte[] data, IPEndPoint ip)
    {
        try
        {
            if (data == null || data.Length < sizeof(float) * 3)
            {
                Console.WriteLine("[ServerMessageDispatcher] Invalid position data received");
                return;
            }

            Vector3 position = _netVector3.Deserialize(data);

            if (!_clientManager.TryGetClientId(ip, out var clientId))
            {
                Console.WriteLineWarning($"[ServerMessageDispatcher] Position update from unknown client {ip}");
                return;
            }

            _playerManager.UpdatePlayerPosition(clientId, position);

            ServerNetworkManager.OnSerializedBroadcast.Invoke(position, MessageType.Position, clientId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error in HandlePositionUpdate: {ex.Message}");
        }
    }

    private void HandlePing(byte[] data, IPEndPoint ip)
    {
        try
        {
            if (!_clientManager.TryGetClientId(ip, out var clientId))
            {
                Console.WriteLineWarning($"[ServerMessageDispatcher] Ping from unknown client {ip}");
                return;
            }

            _clientManager.UpdateClientTimestamp(clientId);
            ServerNetworkManager.OnSendToClient.Invoke(clientId, null, MessageType.Ping, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error in HandlePing: {ex.Message}");
        }
    }

    private void HandleIdMessage(byte[] data, IPEndPoint ip)
    {
        Console.WriteLine("[ServerMessageDispatcher] Received ID message from client (unexpected)");
    }


    private void HandleObjectDestroy(byte[] arg1, IPEndPoint arg2)
    {
        throw new NotImplementedException();
    }

    private void HandleObjectUpdate(byte[] arg1, IPEndPoint arg2)
    {
        throw new NotImplementedException();
    }

    private void HandleObjectCreate(byte[] arg1, IPEndPoint arg2)
    {
        throw new NotImplementedException();
    }

    private void HandlePlayerInput(byte[] arg1, IPEndPoint arg2)
    {
        try
        {
            if (arg1 == null || arg1.Length < sizeof(float) * 3)
            {
                Console.WriteLine("[ServerMessageDispatcher] Invalid player input data received");
                return;
            }

            PlayerInput input = _netPlayerInput.Deserialize(arg1);

            if (!_clientManager.TryGetClientId(arg2, out var clientId))
            {
                Console.WriteLineWarning($"[ServerMessageDispatcher] Player input from unknown client {arg2}");
                return;
            }

            _playerManager.UpdatePlayerInput(clientId, input);


            GameObject player = _playerManager.GetAllPlayers()[clientId];
            Vector3 pos = player.transform.position;
            NetworkObjectFactory.Instance.UpdateNetworkObjectPosition(clientId, pos);

            if (input.IsShooting)
            {
                var bullet = NetworkObjectFactory.Instance.CreateNetworkObject(player.transform.position,
                    Vector3.zero, NetObjectTypes.Projectile, _playerManager.GetPlayerColor(clientId));
                var createMsg = new NetworkObjectCreateMessage
                {
                    NetworkId = bullet.NetworkId,
                    PrefabType = NetObjectTypes.Projectile,
                    Position = bullet.transform.position,
                    Rotation = Vector3.zero
                };

                ServerNetworkManager.OnSerializedBroadcast.Invoke(createMsg, MessageType.ObjectCreate, -1);
            }

            ServerNetworkManager.OnSerializedBroadcast.Invoke(pos, MessageType.Position, clientId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error in HandlePlayerInput: {ex.Message}");
        }
    }

    private NetworkObject CreateNetworkObject(Vector3 position, Vector3 rotation, NetObjectTypes objectType, int color)
    {
        var networkObject = NetworkObjectFactory.Instance.CreateNetworkObject(position, rotation, objectType, color);
        var createMsg = new NetworkObjectCreateMessage
        {
            NetworkId = networkObject.NetworkId,
            PrefabType = objectType,
            Position = networkObject.transform.position,
            Rotation = rotation,
            Color = color
        };
        ServerNetworkManager.OnSerializedBroadcast.Invoke(createMsg, MessageType.ObjectCreate, -1);

        return networkObject;
    }
}