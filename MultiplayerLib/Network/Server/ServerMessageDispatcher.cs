using System.Net;
using System.Numerics;
using MultiplayerLib.Game;
using MultiplayerLib.Network.ClientDir;
using MultiplayerLib.Network.Factory;
using MultiplayerLib.Network.interfaces;
using MultiplayerLib.Network.Messages;

namespace MultiplayerLib.Network.Server;

public class HandshakeResponse
{
    public int ClientId { get; set; }
    public int SecuritySeed { get; set; }
}
public abstract class ServerMessageDispatcher : BaseMessageDispatcher
{
    private ClientManager _clientManager;
    private readonly Dictionary<int, int> _clientColor = new Dictionary<int, int>();
    public Action<int> OnNewClient;
    public ServerMessageDispatcher(ClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    protected override void InitializeMessageHandlers()
    {
        _messageHandlers[MessageType.HandShake] = HandleHandshake;
        _messageHandlers[MessageType.Console] = HandleConsoleMessage;
        _messageHandlers[MessageType.Position] = HandlePositionUpdate;
        _messageHandlers[MessageType.Ping] = HandlePing;
        _messageHandlers[MessageType.ObjectCreate] = HandleObjectCreate;
        _messageHandlers[MessageType.ObjectDestroy] = HandleObjectDestroy;
        _messageHandlers[MessageType.ObjectUpdate] = HandleObjectUpdate;
        _messageHandlers[MessageType.PlayerInput] = HandlePlayerInput;
        _messageHandlers[MessageType.Acknowledgment] = HandleAcknowledgment;
        _messageHandlers[MessageType.Disconnect] = HandleDisconnect;
    }

    public void HandleDisconnect(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        if (!_clientManager.TryGetClientId(arg2, out int clientId))
        {
            Console.WriteLine($"[ServerMessageDispatcher] Disconnect from unknown client {arg2}");
            return;
        }

        MessageTracker.RemoveMessages(arg2);
        _clientManager.RemoveClient(arg2);
        NetworkObjectFactory.Instance.DestroyNetworkObject(clientId);
        ServerNetworkManager.OnSerializedBroadcast.Invoke(clientId, MessageType.ObjectDestroy, clientId);
    }

    private void HandleAcknowledgment(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        MessageType ackedType = (MessageType)BitConverter.ToInt32(arg1, 0);
        int ackedNumber = BitConverter.ToInt32(arg1, 4);

        MessageTracker.ConfirmMessage(arg2, ackedType, ackedNumber);
    }


    private void HandleHandshake(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            PlayerData pData = _netHandShake.Deserialize(data);
            NetworkObject pObject = CreateNetworkObject(Vector3.Zero, NetObjectTypes.Player, pData.Color);
            int clientId = _clientManager.AddClient(ip, pObject.NetworkId);
            OnNewClient?.Invoke(clientId);
            
            _clientColor[clientId] = pData.Color;
            _clientManager.UpdateClientTimestamp(clientId);
            Dictionary<int, NetworkObject> networkObjects = NetworkObjectFactory.Instance.GetAllNetworkObjects();
            
            HandshakeResponse response = new HandshakeResponse
            {
                ClientId = clientId,
                SecuritySeed = ServerNetworkManager.Instance.SecuritySeed
            };
            ServerNetworkManager.OnSendToClient?.Invoke(clientId, response, MessageType.HandShakeResponse, true);
            
            SendObjectsToClient(ip, networkObjects);

            Console.WriteLine($"[ServerMessageDispatcher] New client {clientId} connected from {ip}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error in HandleHandshake: {ex.Message}");
        }
    }
    
    private void SendObjectsToClient(IPEndPoint ip, Dictionary<int, NetworkObject> networkObjects)
    {
        foreach (KeyValuePair<int, NetworkObject> kvp in networkObjects)
        {
            NetworkObject? networkObject = kvp.Value;
            if (networkObject == null) continue;

            NetworkObjectCreateMessage createMsg = new NetworkObjectCreateMessage
            {
                NetworkId = networkObject.NetworkId,
                PrefabType = networkObject.PrefabType,
                Position = networkObject.CurrentPos,
                Color = networkObject.Color
            };


            _clientManager.TryGetClientId(ip, out int clientId);
            ServerNetworkManager.OnSendToClient?.Invoke(clientId, createMsg, MessageType.ObjectCreate, false);
        }
    }

    private void HandleConsoleMessage(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            string message = _netString.Deserialize(data);
            OnConsoleMessageReceived?.Invoke(message);

            if (string.IsNullOrEmpty(message)) return;

            ServerNetworkManager.OnSerializedBroadcast.Invoke(message, MessageType.Console, -1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error in HandleConsoleMessage: {ex.Message}");
        }
    }

    private void HandlePositionUpdate(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            if (data == null)
            {
                Console.WriteLine("[ServerMessageDispatcher] Invalid position data received");
                return;
            }

            Vector3 position = _netVector3.Deserialize(data);

            if (!_clientManager.TryGetClientId(ip, out int clientId))
            {
                Console.WriteLine($"[ServerMessageDispatcher] Position update from unknown client {ip}");
                return;
            }

            UpdatePlayerPosition(clientId, position);

            ServerNetworkManager.OnSerializedBroadcast.Invoke(position, MessageType.Position, clientId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error in HandlePositionUpdate: {ex.Message}");
        }
    }

    protected abstract void UpdatePlayerPosition(int clientId, Vector3 position);

    private void HandlePing(byte[] data, int messageNum, IPEndPoint ip)
    {
        try
        {
            if (!_clientManager.TryGetClientId(ip, out int clientId))
            {
                Console.WriteLine($"[ServerMessageDispatcher] Ping from unknown client {ip}");
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

    private void HandleObjectDestroy(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        throw new NotImplementedException();
    }

    private void HandleObjectUpdate(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        throw new NotImplementedException();
    }

    private void HandleObjectCreate(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        throw new NotImplementedException();
    }

    private Dictionary<IPEndPoint, int> SequenceTracker = new();
    private void HandlePlayerInput(byte[] arg1, int messageNum, IPEndPoint arg2)
    {
        try
        {
            if (arg1 == null)
            {
                Console.WriteLine("[ServerMessageDispatcher] Invalid player input data received");
                return;
            }
            if(SequenceTracker[arg2] > messageNum)
            {
                return;
            }
            SequenceTracker[arg2] = messageNum;
            PlayerInput input = _netPlayerInput.Deserialize(arg1);

            if (!_clientManager.TryGetClientId(arg2, out int clientId))
            {
                Console.WriteLine($"[ServerMessageDispatcher] Player input from unknown client {arg2}");
                return;
            }


            UpdatePlayerInput(clientId, input);

            Vector3 pos = NetworkObjectFactory.Instance.GetNetworkObject(clientId).CurrentPos;
            NetworkObjectFactory.Instance.UpdateNetworkObjectPosition(clientId, pos);

            if (input.IsShooting)
            {
                NetworkObject bullet =
                    NetworkObjectFactory.Instance.CreateNetworkObject(pos, NetObjectTypes.Projectile,
                        _clientColor[clientId]);
                NetworkObjectCreateMessage createMsg = new NetworkObjectCreateMessage
                {
                    NetworkId = bullet.NetworkId,
                    PrefabType = NetObjectTypes.Projectile,
                    Position = bullet.CurrentPos,
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
    
    private NetworkObject CreateNetworkObject(Vector3 position, NetObjectTypes objectType, int color)
    {
        NetworkObject networkObject = NetworkObjectFactory.Instance.CreateNetworkObject(position, objectType, color);
        NetworkObjectCreateMessage createMsg = new NetworkObjectCreateMessage
        {
            NetworkId = networkObject.NetworkId,
            PrefabType = objectType,
            Position = networkObject.CurrentPos,
            Color = color
        };
        ServerNetworkManager.OnSerializedBroadcast.Invoke(createMsg, MessageType.ObjectCreate, networkObject.NetworkId);

        return networkObject;
    }

    protected abstract void UpdatePlayerInput(int clientId, PlayerInput input);

    public void HandleMessageData(MessageEnvelope envelope, IPEndPoint ip)
    {
        try
        {
            if (envelope == null || ip == null)
            {
                Console.WriteLine("[ServerMessageDispatcher] Null envelope or IP received");
                return;
            }

            MessageType messageType = envelope.MessageType;
            byte[] data = envelope.Data;

            if (_clientManager.TryGetClientId(ip, out int clientId))
            {
                Console.WriteLine($"[ServerMessageDispatcher] Processing in-sequence message type {messageType} from client {clientId}");
            }

            if (_messageHandlers.TryGetValue(messageType, out var handler))
            {
                if (clientId != -1)
                {
                    _clientManager.UpdateClientTimestamp(clientId);
                }

                handler(data, envelope.MessageNumber, ip);
            }
            else
            {
                Console.WriteLine($"[ServerMessageDispatcher] No handler registered for message type {messageType}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServerMessageDispatcher] Error handling message data from {ip}: {ex.Message}");
        }
    }
}