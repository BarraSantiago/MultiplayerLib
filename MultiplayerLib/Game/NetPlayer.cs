using System.Numerics;
using MultiplayerLib.Network.Factory;

namespace MultiplayerLib.Game;

public class NetPlayer : NetworkObject
{
    public NetPlayer(Vector3 position, NetObjectTypes prefabType)
    {
        PrefabType = prefabType;
        CurrentPos = position;
        LastUpdatedPos = position;
    }

    public int NetworkId { get; set; }
    public bool IsOwner { get; set; }
    public NetObjectTypes PrefabType { get; set; }
    public Vector3 LastUpdatedPos { get; set; }
    public virtual Vector3 CurrentPos { get; set; }
}