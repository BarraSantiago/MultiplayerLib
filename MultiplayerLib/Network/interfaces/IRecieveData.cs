using System.Net;

namespace Network.interfaces;

public interface IReceiveData
{
    public void OnReceiveData(byte[] data, IPEndPoint ipEndpoint);
}