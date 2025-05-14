using System.Collections.Concurrent;
using System.Net;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.ClientDir;

public class ClientManager
{
    private readonly ConcurrentDictionary<int, Client> _clients = new();
    private readonly ConcurrentDictionary<IPEndPoint, int> _ipToId = new();
    private int _clientIdCounter;

    public Action<int> OnClientConnected;
    public Action<int> OnClientDisconnected;

    public bool HasClient(IPEndPoint endpoint)
    {
        return _ipToId.ContainsKey(endpoint);
    }

    public bool TryGetClientId(IPEndPoint endpoint, out int clientId)
    {
        return _ipToId.TryGetValue(endpoint, out clientId);
    }

    public bool TryGetClient(int clientId, out Client client)
    {
        return _clients.TryGetValue(clientId, out client);
    }

    public IReadOnlyDictionary<int, Client> GetAllClients()
    {
        return _clients;
    }

    public int AddClient(IPEndPoint endpoint)
    {
        if (_ipToId.TryGetValue(endpoint, out int client)) return client;

        int id = _clientIdCounter++;
        Client newClient = new Client(endpoint, id, Time.CurrentTime);

        _ipToId[endpoint] = id;
        _clients[id] = newClient;

        OnClientConnected?.Invoke(id);
        Console.WriteLine($"[ClientManager] Client added: {endpoint.Address}, ID: {id}");

        return id;
    }

    public bool RemoveClient(IPEndPoint endpoint)
    {
        if (!_ipToId.TryGetValue(endpoint, out int clientId))
            return false;

        _ipToId.TryRemove(endpoint, out _);
        _clients.TryRemove(clientId, out _);

        OnClientDisconnected?.Invoke(clientId);
        return true;
    }

    public void UpdateClientTimestamp(int clientId)
    {
        if (!_clients.TryGetValue(clientId, out Client client)) return;
        client.LastHeartbeatTime = Time.CurrentTime;
        _clients[clientId] = client;
    }

    public List<IPEndPoint> GetTimedOutClients(float timeout)
    {
        float currentTime = Time.CurrentTime;
        List<IPEndPoint> timedOut = new List<IPEndPoint>();

        foreach (KeyValuePair<int, Client> client in _clients)
        {
            if (currentTime - client.Value.LastHeartbeatTime <= timeout) continue;

            timedOut.Add(client.Value.ipEndPoint);
            Console.WriteLine($"[ClientManager] Client {client.Key} timed out");
        }

        return timedOut;
    }

    public void Clear()
    {
        _clients.Clear();
        _ipToId.Clear();
    }
}