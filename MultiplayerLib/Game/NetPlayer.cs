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
}