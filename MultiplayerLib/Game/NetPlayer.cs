using System.Numerics;
using MultiplayerLib.Network.Factory;

namespace MultiplayerLib.Game;

public class NetPlayer : NetworkObject
{
    public NetPlayer(Vector3 position, NetObjectTypes prefabType, int color)
    {
        PrefabType = prefabType;
        CurrentPos = position;
        LastUpdatedPos = position;
        Color = color;
    }

    public int NetworkId { get; set; }
    public bool IsOwner { get; set; }
    public NetObjectTypes PrefabType { get; set; }
    public Vector3 LastUpdatedPos { get; set; }
    public virtual Vector3 CurrentPos { get; set; }
    public int Color { get; set; }
}