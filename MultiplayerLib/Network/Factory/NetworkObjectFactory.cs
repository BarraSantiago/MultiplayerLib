using Network.interfaces;
using Network.Messages;
using Network.Server;
using Utils;

namespace Network.Factory;

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
    public Vector3 Rotation;
}

public class NetworkObjectFactory : MonoBehaviourSingleton<NetworkObjectFactory>
{
    private readonly Dictionary<int, NetworkObject> _networkObjects = new();
    private readonly Dictionary<NetObjectTypes, GameObject> _prefabs = new();
    [SerializeField] private readonly List<GameObject> registeredPrefabs = new();
    private int _networkIdCounter;

    private void Awake()
    {
        var netObjTypes = (NetObjectTypes[])Enum.GetValues(typeof(NetObjectTypes));
        for (var i = 0; i < registeredPrefabs.Count; i++)
        {
            GameObject prefab = registeredPrefabs[i];
            NetworkObject netObj = prefab.GetComponent<NetworkObject>();
            if (netObj) RegisterPrefab(prefab, netObjTypes[i + 1]);
        }
    }

    public void RegisterPrefab(GameObject prefab, NetObjectTypes netObjType)
    {
        if (_prefabs.ContainsKey(netObjType)) return;

        NetworkObject netObj = prefab.GetComponent<NetworkObject>();
        if (!netObj) return;

        _prefabs[netObjType] = prefab;
    }

    public NetworkObject CreateNetworkObject(Vector3 position, Vector3 rotation, NetObjectTypes netObj, int color,
        bool isOwner = false)
    {
        if (!_prefabs.TryGetValue(netObj, out GameObject prefab)) return null;

        var netId = GetNextNetworkId();
        Quaternion rot = Quaternion.Euler(rotation);
        GameObject instance = Instantiate(prefab, position, rot);

        instance.GetComponent<MeshRenderer>().material.color = color switch
        {
            0 => Color.red,
            1 => Color.blue,
            2 => Color.green,
            _ => Color.red
        };

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
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
        if (!_networkObjects.TryGetValue(networkId, out var obj)) return;
        Destroy(obj.gameObject);
        _networkObjects.Remove(networkId);
    }

    private int GetNextNetworkId()
    {
        return _networkIdCounter++;
    }

    public void HandleCreateObjectMessage(NetworkObjectCreateMessage createMsg)
    {
        var netObjectType = createMsg.PrefabType;

        if (!_prefabs.TryGetValue(netObjectType, out GameObject prefab)) return;

        if (_networkObjects.ContainsKey(createMsg.NetworkId))
        {
            Debug.LogWarning($"[NetworkObjectFactory] Object with ID {createMsg.NetworkId} already exists.");
            return;
        }

        GameObject instance = Instantiate(prefab, createMsg.Position, Quaternion.Euler(createMsg.Rotation));
        instance.GetComponent<MeshRenderer>().material.color = createMsg.Color switch
        {
            0 => Color.red,
            1 => Color.blue,
            2 => Color.green,
            _ => Color.red
        };
        NetworkObject networkObject = instance.GetComponent<NetworkObject>();

        networkObject.Initialize(createMsg.NetworkId, false, netObjectType);
    }

    public void UpdateNetworkObjectPosition(int clientId, Vector3 pos)
    {
        if (_networkObjects.TryGetValue(clientId, out var networkObject))
        {
            networkObject.LastUpdatedPos = pos;
            networkObject.transform.position = pos;
        }
        else
        {
            Debug.LogWarning($"[NetworkObjectFactory] Network object with ID {clientId} not found.");
        }
    }
}