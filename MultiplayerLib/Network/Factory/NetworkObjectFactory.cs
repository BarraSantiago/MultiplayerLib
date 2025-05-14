using System.Numerics;
using MultiplayerLib.Game;
using MultiplayerLib.Network.interfaces;
using MultiplayerLib.Network.Messages;
using MultiplayerLib.Network.Server;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.Factory;

public enum NetObjectTypes
{
    None,
    Player,
    Projectile
}

[Serializable]
public class NetworkObjectCreateMessage
{
    public int Color;
    public int NetworkId;
    public Vector3 Position;
    public NetObjectTypes PrefabType;
}

public abstract class NetworkObjectFactory : Singleton<NetworkObjectFactory>
{
    private readonly Dictionary<int, NetworkObject> _networkObjects = new();
    private int _networkIdCounter;
    public static Action<int, Vector3> OnPositionUpdate;
    public static Action<int, Vector3> OnObjectDestroy;
    public abstract void CreateGameObject(NetworkObjectCreateMessage createMsg);
    public abstract void UpdateObjectPosition(int id, Vector3 position);
    public NetworkObject CreateNetworkObject(Vector3 position, NetObjectTypes netObj, int color, bool isOwner = false)
    {
        
        int netId = GetNextNetworkId();
        CreateGameObject(new NetworkObjectCreateMessage
        {
            NetworkId = netId,
            Position = position,
            PrefabType = netObj,
            Color = color
        });
        NetworkObject networkObject = netObj switch 
        {
            NetObjectTypes.Player => new NetPlayer(position, netObj),
            NetObjectTypes.Projectile => CreateNetworkObject(position, netObj, color),
            _ => throw new ArgumentOutOfRangeException(nameof(netObj), netObj, null)
        };
        
        networkObject.Initialize(netId, isOwner, netObj);

        return networkObject;
    }


    public void RegisterObject(NetworkObject obj)
    {
        if (obj.NetworkId == -1) obj.Initialize(GetNextNetworkId(), true, obj.PrefabType);

        _networkObjects[obj.NetworkId] = obj;
    }

    public void UnregisterObject(int networkId)
    {
        if (!_networkObjects.Remove(networkId)) return;

        if (AbstractNetworkManager.Instance is ServerNetworkManager serverManager)
            serverManager.SerializedBroadcast(networkId, MessageType.ObjectDestroy);
    }

    public NetworkObject GetNetworkObject(int networkId)
    {
        return _networkObjects.GetValueOrDefault(networkId);
    }

    public Dictionary<int, NetworkObject> GetAllNetworkObjects()
    {
        return _networkObjects;
    }

    public void DestroyNetworkObject(int networkId)
    {
        if (!_networkObjects.TryGetValue(networkId, out NetworkObject? obj)) return;
        _networkObjects.Remove(networkId);
    }

    private int GetNextNetworkId()
    {
        return _networkIdCounter++;
    }

    public void HandleCreateObjectMessage(NetworkObjectCreateMessage createMsg)
    {
        CreateGameObject( new NetworkObjectCreateMessage
        {
            NetworkId = createMsg.NetworkId,
            Position = createMsg.Position,
            PrefabType = createMsg.PrefabType,
            Color = createMsg.Color
        });
        NetObjectTypes netObjectType = createMsg.PrefabType;
        
        if (_networkObjects.ContainsKey(createMsg.NetworkId))
        {
            Console.WriteLine($"[NetworkObjectFactory] Object with ID {createMsg.NetworkId} already exists.");
            return;
        }
        
        NetworkObject networkObject = createMsg switch 
        {
            { PrefabType: NetObjectTypes.Player } => new NetPlayer(createMsg.Position, createMsg.PrefabType),
            { PrefabType: NetObjectTypes.Projectile } => new Bullet(createMsg.Position, netObjectType),
            _ => throw new ArgumentOutOfRangeException(nameof(createMsg.PrefabType), createMsg.PrefabType, null)
        };

        networkObject.Initialize(createMsg.NetworkId, false, netObjectType);
    }

    public void UpdateNetworkObjectPosition(int clientId, Vector3 pos)
    {
        if (_networkObjects.TryGetValue(clientId, out NetworkObject? networkObject))
        {
            networkObject.LastUpdatedPos = pos;
            networkObject.CurrentPos = pos;
            UpdateObjectPosition(clientId, pos);
        }
        else
        {
            Console.WriteLine($"[NetworkObjectFactory] Network object with ID {clientId} not found.");
        }
    }
}