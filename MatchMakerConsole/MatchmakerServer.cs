using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Network.ClientDir;

namespace MatchMakerConsole;

public enum MessageType
{
    Heartbeat = 0,
    Registration = 1,
    Console = 2,
    ServerAssignment = 3,
    Disconnect = 4,
    ServerStatus = 5
}

public class MatchmakerServer
{
    private readonly Dictionary<int, GameServer> _activeServers = new();

    // State
    private readonly Dictionary<IPEndPoint, Client> _connectedClients = new();
    private readonly string _gameServerPath;
    private readonly bool _isRunning = true;
    private readonly int _minPlayersToStartMatches;

    private readonly int _playersPerServer;

    // Configuration
    private readonly int _port;
    private readonly int _startingPort;
    private readonly UdpClient _udpClient;
    private readonly HashSet<string> _usedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Client> _waitingPlayers = new();
    private int _nextClientId = 1;
    private int _nextServerId = 1;
    private int _nextServerPort;

    public MatchmakerServer(int port = 7777, int playersPerServer = 2, int minPlayersToStartMatches = 4,
        string gameServerPath = "GameServer.exe", int startingPort = 7778)
    {
        _port = port;
        _playersPerServer = playersPerServer;
        _minPlayersToStartMatches = minPlayersToStartMatches;
        _gameServerPath = gameServerPath;
        _startingPort = startingPort;
        _nextServerPort = startingPort;

        _udpClient = new UdpClient(_port);
        Console.WriteLine($"Matchmaker started on port {_port}");
    }

    public async Task RunAsync()
    {
        // Start listening for messages in a separate task
        Task receiveTask = ReceiveMessagesAsync();

        // Start periodic tasks in a separate task
        Task periodicTask = RunPeriodicTasksAsync();

        // Handle console commands
        Task consoleTask = HandleConsoleCommandsAsync();

        // Wait for any task to complete (they shouldn't unless there's an error)
        await Task.WhenAny(receiveTask, periodicTask, consoleTask);

        // Clean up
        CloseAllServers();
        _udpClient.Close();
    }

    private async Task ReceiveMessagesAsync()
    {
        while (_isRunning)
        {
            try
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                byte[] data = result.Buffer;
                IPEndPoint clientEndPoint = result.RemoteEndPoint;

                if (data.Length < 4) continue;

                MessageType messageType = (MessageType)BitConverter.ToInt32(data, 0);
                byte[] messageData = data.Skip(4).ToArray();

                HandleMessage(messageType, messageData, clientEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
        }
    }

    private void HandleMessage(MessageType messageType, byte[] messageData, IPEndPoint sender)
    {
        switch (messageType)
        {
            case MessageType.Registration:
                string name = Encoding.UTF8.GetString(messageData);
                HandleRegistration(sender, name);
                break;

            case MessageType.Heartbeat:
                UpdateClientHeartbeat(sender);
                break;

            case MessageType.ServerStatus:
                UpdateServerStatus(messageData);
                break;

            case MessageType.Disconnect:
                HandleDisconnect(sender);
                break;
        }
    }

    private void HandleRegistration(IPEndPoint clientEndPoint, string name)
    {
        if (_usedNames.Contains(name))
        {
            Console.WriteLine($"Rejecting client with duplicate name: {name}");
            SendConsoleMessage(clientEndPoint, "Name already in use. Please choose another name.");
            return;
        }

        // Create new client
        Client client = new Client(clientEndPoint, _nextClientId++, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond)
        {
            Name = name
        };

        _connectedClients[clientEndPoint] = client;
        _usedNames.Add(name);
        _waitingPlayers.Add(client);

        Console.WriteLine($"Client registered: {name} (ID: {client.id})");
        SendConsoleMessage(clientEndPoint,
            $"Welcome {name}! Waiting for {_minPlayersToStartMatches - _waitingPlayers.Count} more players.");
    }

    private void UpdateClientHeartbeat(IPEndPoint clientEndPoint)
    {
        if (_connectedClients.TryGetValue(clientEndPoint, out Client client))
        {
            client.LastHeartbeatTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }
    }

    private void HandleDisconnect(IPEndPoint clientEndPoint)
    {
        if (_connectedClients.TryGetValue(clientEndPoint, out Client client)) RemoveClient(client);
    }

    private void RemoveClient(Client client)
    {
        _waitingPlayers.Remove(client);
        _connectedClients.Remove(client.ipEndPoint);

        if (!string.IsNullOrEmpty(client.Name))
            _usedNames.Remove(client.Name);

        Console.WriteLine($"Client removed: {client.Name} (ID: {client.id})");
    }

    private void SendConsoleMessage(IPEndPoint endpoint, string message)
    {
        try
        {
            byte[] typeBytes = BitConverter.GetBytes((int)MessageType.Console
        }
    }

    private Process StartGameServerProcess(int serverId, int port)
    {
        try
        {
            // Setup process start info
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = gameServerExecutablePath,
                Arguments = $"-port {port} -serverId {serverId}",
                UseShellExecute = false,
                CreateNoWindow = false
            };

            Process serverProcess = new Process { StartInfo = startInfo };
            serverProcess.EnableRaisingEvents = true;
            serverProcess.Exited += (sender, args) => HandleServerTermination(serverId);
            serverProcess.Start();

            Console.WriteLine($"[Matchmaker] Started game server process #{serverId} on port {port}");
            return serverProcess;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Matchmaker] Failed to start game server: {ex.Message}");
            return null;
        }
    }

    private void HandleServerTermination(int serverId)
    {
        if (_activeServers.TryGetValue(serverId, out Process process))
        {
            Console.WriteLine($"[Matchmaker] Game server #{serverId} terminated");
            _activeServers.Remove(serverId);
        }
    }

    private static byte[] DeserializeServerInfo(byte[] data)
    {
        if (data.Length < 8)
            throw new ArgumentException("Invalid server info data");

        int ipLength = BitConverter.ToInt32(data, 0);
        string ip = Encoding.UTF8.GetString(data, 4, ipLength);
        int port = BitConverter.ToInt32(data, 4 + ipLength);

        return new ServerConnectionInfo
        {
            ServerIp = ip,
            ServerPort = port
        };
    }

    private void OnApplicationQuit()
    {
        // Terminate all active game servers when matchmaker shuts down
        foreach (Process serverProcess in _activeServers.Values)
            try
            {
                if (!serverProcess.HasExited)
                    serverProcess.Kill();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Matchmaker] Error shutting down server: {ex.Message}");
            }
    }
}