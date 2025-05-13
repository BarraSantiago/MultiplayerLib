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
}