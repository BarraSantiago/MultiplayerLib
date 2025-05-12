using System.Net;

namespace Network.ClientDir;

public struct Client
{
    public float LastHeartbeatTime;
    public int id;
    public IPEndPoint ipEndPoint;
    public string Name;
    public float elo;

    public Client(IPEndPoint ipEndPoint, int id, float timeStamp)
    {
        LastHeartbeatTime = timeStamp;
        this.id = id;
        this.ipEndPoint = ipEndPoint;
        Name = null;
        elo = 0;
    }
}