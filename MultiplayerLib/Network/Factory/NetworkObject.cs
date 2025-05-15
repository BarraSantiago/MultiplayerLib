using System.Numerics;
using MultiplayerLib.Network.Messages;

namespace MultiplayerLib.Network.Factory;

public interface NetworkObject
{
    public int NetworkId { get; protected set; }
    public bool IsOwner { get; protected set; }
    public NetObjectTypes PrefabType { get; set; }
    public Vector3 LastUpdatedPos { get; set; }
    public Vector3 CurrentPos { get; set; }

    public virtual void Initialize(int networkId, bool isOwner, NetObjectTypes prefabType)
    {
        NetworkId = networkId;
        IsOwner = isOwner;
        PrefabType = prefabType;

        NetworkObjectFactory.Instance.RegisterObject(this);
    }

    public virtual void OnNetworkDestroy()
    {
        NetworkObjectFactory.Instance.UnregisterObject(NetworkId);
    }

    public virtual void SyncState()
    {
    }

    public virtual void OnNetworkMessage(object data, MessageType messageType)
    {
    }

    protected virtual void OnDestroy()
    {
        OnNetworkDestroy();
    }
}