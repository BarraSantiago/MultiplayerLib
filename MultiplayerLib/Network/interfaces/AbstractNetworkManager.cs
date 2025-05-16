using System.Net;
using MultiplayerLib.Network.ClientDir;
using MultiplayerLib.Network.Messages;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.interfaces;

public abstract class AbstractNetworkManager : Singleton<AbstractNetworkManager>, IReceiveData, IDisposable
{
    public int SecuritySeed { get; protected set; }
    protected UdpConnection _connection;
    protected bool _disposed;
    public BaseMessageDispatcher _messageDispatcher;
    protected MessageSequenceTracker _messageSequenceTracker = new MessageSequenceTracker();

    public int Port { get; protected set; }
    
    public virtual void Dispose()
    {
        if (_disposed) return;

        try
        {
            _connection?.FlushReceiveData();
            Thread.Sleep(100);

            _connection?.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[NetworkManager] Disposal error: {e.Message}");
        }

        _disposed = true;
    }

    public virtual void OnReceiveData(byte[] data, IPEndPoint ip)
    {
        try
        {
            _messageDispatcher.TryDispatchMessage(data, 0, ip);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NetworkManager] Error processing data from {ip}: {ex.Message}");
        }
    }

    public virtual void SendMessage(byte[] data, IPEndPoint ipEndPoint)
    {
        try
        {
            _connection?.Send(data, ipEndPoint);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[NetworkManager] Send failed: {e.Message}");
        }
    }

    public virtual byte[] SerializeMessage(object data, MessageType messageType, int id = -1)
    {
        return _messageDispatcher.SerializeMessage(data, messageType, id);
    }

    public virtual void Tick()
    {
        if (_disposed) return;

        _connection?.FlushReceiveData();
        _messageDispatcher?.CheckAndResendMessages();
    }

    protected virtual void OnDestroy()
    {
        Dispose();
    }
}