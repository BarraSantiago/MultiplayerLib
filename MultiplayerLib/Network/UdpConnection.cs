using System.Net;
using System.Net.Sockets;
using MultiplayerLib.Network.interfaces;

namespace MultiplayerLib.Network;

public class UdpConnection
{
    private readonly UdpClient _connection;
    private readonly Queue<DataReceived> _dataReceivedQueue = new();

    private readonly object _handler = new();
    private readonly IReceiveData _receiver;

    public UdpConnection(int port, IReceiveData receiver = null)
    {
        _connection = new UdpClient(port);

        _receiver = receiver;

        _connection.BeginReceive(OnReceive, null);
    }

    public UdpConnection(IPAddress ip, int port, IReceiveData receiver = null)
    {
        _connection = new UdpClient();
        _connection.Connect(ip, port);

        _receiver = receiver;

        _connection.BeginReceive(OnReceive, null);
    }

    public void Close()
    {
        _connection.Close();
    }

    public void FlushReceiveData()
    {
        lock (_handler)
        {
            while (_dataReceivedQueue.Count > 0)
            {
                DataReceived dataReceived = _dataReceivedQueue.Dequeue();
                _receiver?.OnReceiveData(dataReceived.Data, dataReceived.ipEndPoint);
            }
        }
    }

    private void OnReceive(IAsyncResult ar)
    {
        try
        {
            DataReceived dataReceived = new DataReceived();
            dataReceived.Data = _connection.EndReceive(ar, ref dataReceived.ipEndPoint);

            lock (_handler)
            {
                _dataReceivedQueue.Enqueue(dataReceived);
            }
        }
        catch (SocketException e)
        {
            // Client disconnection.
            Console.WriteLine("[UdpConnection] " + e.Message);
        }
        catch (Exception e)
        {
            // Handle any other unexpected exceptions
            Console.WriteLine("[UdpConnection] Unexpected error: " + e.Message);
        }
        finally
        {
            try
            {
                _connection.BeginReceive(OnReceive, null);
            }
            catch (Exception e)
            {
                Console.WriteLine("[UdpConnection] Failed to continue receive loop: " + e.Message);
            }
        }
    }

    public void Send(byte[] data)
    {
        _connection.Send(data, data.Length);
    }

    public void Send(byte[] data, IPEndPoint ipEndpoint)
    {
        _connection.Send(data, data.Length, ipEndpoint);
    }

    private struct DataReceived
    {
        public byte[] Data;
        public IPEndPoint ipEndPoint;
    }
}