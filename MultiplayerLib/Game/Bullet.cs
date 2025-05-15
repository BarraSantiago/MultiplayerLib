using System.Numerics;
using MultiplayerLib.Network.Factory;

namespace MultiplayerLib.Game;

public class Bullet : NetworkObject
{
    public Bullet(Vector3 position, NetObjectTypes prefabType)
    {
        PrefabType = prefabType;
        CurrentPos = position;
        LastUpdatedPos = position;
    }

    public int NetworkId { get; set; }
    public bool IsOwner { get; set; }
    public NetObjectTypes PrefabType { get; set; }
    public Vector3 LastUpdatedPos { get; set; }
    public Vector3 CurrentPos { get; set; }
}