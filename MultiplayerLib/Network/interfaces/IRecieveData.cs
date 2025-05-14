using System.Net;

namespace MultiplayerLib.Network.interfaces;

public interface IReceiveData
{
    public void OnReceiveData(byte[] data, IPEndPoint ipEndpoint);
}