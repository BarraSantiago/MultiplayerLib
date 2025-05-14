using System.Numerics;
using MultiplayerLib.Network.Messages;

namespace MultiplayerLib.Network.Factory;

public abstract class NetworkObject
{
    public int NetworkId { get; private set; } = -1;
    public bool IsOwner { get; private set; }
    public NetObjectTypes PrefabType { get; set; } = NetObjectTypes.None;
    public Vector3 LastUpdatedPos { get; set; } = Vector3.Zero;
    public Vector3 CurrentPos { get; set; } = Vector3.Zero;

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