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
    protected readonly Dictionary<int, NetworkObject> _networkObjects = new();
    protected int _networkIdCounter;
    public abstract void CreateGameObject(NetworkObject createMsg);
    public abstract void UpdateObjectPosition(int id, Vector3 position);

    public NetworkObject CreateNetworkObject(Vector3 position, NetObjectTypes netObj, int color, bool isOwner = false)
    {
        int netId = GetNextNetworkId();
        
        NetworkObject networkObject = netObj switch
        {
            NetObjectTypes.Player => new NetPlayer(position, netObj, color),
            NetObjectTypes.Projectile => new Bullet(position, netObj, color),
            _ => throw new ArgumentOutOfRangeException(nameof(netObj), netObj, null)
        };
        CreateGameObject(networkObject);
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
        RemoveNetworkObject(networkId);
    }

    protected abstract void RemoveNetworkObject(int networkId);

    private int GetNextNetworkId()
    {
        return _networkIdCounter++;
    }

    public void HandleCreateObjectMessage(NetworkObjectCreateMessage createMsg)
    {
        NetObjectTypes netObjectType = createMsg.PrefabType;

        if (_networkObjects.ContainsKey(createMsg.NetworkId))
        {
            ConsoleMessages.Log($"[NetworkObjectFactory] Object with ID {createMsg.NetworkId} already exists.");
            return;
        }

        NetworkObject networkObject = createMsg switch
        {
            { PrefabType: NetObjectTypes.Player } => new NetPlayer(createMsg.Position, createMsg.PrefabType,createMsg.Color),
            { PrefabType: NetObjectTypes.Projectile } => new Bullet(createMsg.Position, netObjectType,createMsg.Color),
            _ => throw new ArgumentOutOfRangeException(nameof(createMsg.PrefabType), createMsg.PrefabType, null)
        };

        networkObject.Initialize(createMsg.NetworkId, false, netObjectType);
        CreateGameObject(networkObject);
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
            ConsoleMessages.Log($"[NetworkObjectFactory] Network object with ID {clientId} not found.");
        }
    }

    public void SyncPositions()
    {
        float threshold = 0.0001f;
        foreach (KeyValuePair<int, NetworkObject> kvp in _networkObjects)
        {
            NetworkObject networkObject = kvp.Value;
            float deltaSquared = Vector3.DistanceSquared(networkObject.CurrentPos, networkObject.LastUpdatedPos);
            
            if (deltaSquared <= threshold) continue;
            ServerNetworkManager.OnSerializedBroadcast?.Invoke(networkObject.CurrentPos, MessageType.Position,
                networkObject.NetworkId);

            networkObject.LastUpdatedPos = networkObject.CurrentPos;
        }
    }
}