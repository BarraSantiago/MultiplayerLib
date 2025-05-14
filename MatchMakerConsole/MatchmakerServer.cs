using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MultiplayerLib.Network.ClientDir;
using MultiplayerLib.Utils;

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
    private readonly Dictionary<int, Process> _activeServers = new();

    private readonly Dictionary<IPEndPoint, Client> _connectedClients = new();
    private readonly string _gameServerPath;
    private bool _isRunning = true;
    private readonly int _minPlayersToStartMatches;

    private readonly int _playersPerServer;

    private readonly int _port;
    private readonly int _startingPort;
    private readonly UdpClient _udpClient;
    private readonly HashSet<string> _usedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Client> _waitingPlayers = new();
    private int _nextClientId = 1;
    private int _nextServerId = 1;
    private int _nextServerPort;

    public MatchmakerServer(int port = 12345, int playersPerServer = 2, int minPlayersToStartMatches = 4,
        string gameServerPath = "GameServer.exe", int startingPort = 123456)
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

    private async Task RunPeriodicTasksAsync()
    {
        const int checkIntervalMs = 1000;
        const long clientTimeoutMs = 10000; // 10 seconds timeout

        while (_isRunning)
        {
            try
            {
                // Check for timed-out clients
                long currentTime = Time.CurrentTime;
                List<Client> timedOutClients = _connectedClients.Values
                    .Where(client => currentTime - client.LastHeartbeatTime > clientTimeoutMs)
                    .ToList();

                foreach (Client client in timedOutClients)
                {
                    Console.WriteLine($"Client timed out: {client.Name} (ID: {client.id})");
                    RemoveClient(client);
                }

                // Try to start matches if we have enough players
                if (_waitingPlayers.Count >= _minPlayersToStartMatches)
                {
                    List<Client> matchPlayers = _waitingPlayers.Take(_playersPerServer).ToList();

                    // Start a new server
                    int serverId = _nextServerId++;
                    int serverPort = _nextServerPort++;
                    Process serverProcess = StartGameServerProcess(serverId, serverPort);

                    if (serverProcess != null)
                    {
                        _activeServers[serverId] = serverProcess;

                        // Give the server a moment to initialize
                        await Task.Delay(1000);

                        // Assign players to the server
                        foreach (Client player in matchPlayers)
                        {
                            // Create server connection info
                            byte[] serverInfoBytes = BitConverter.GetBytes(serverId);
                            byte[] serverPortBytes = BitConverter.GetBytes(serverPort);
                            byte[] messageData = new byte[serverInfoBytes.Length + serverPortBytes.Length];

                            Buffer.BlockCopy(serverInfoBytes, 0, messageData, 0, serverInfoBytes.Length);
                            Buffer.BlockCopy(serverPortBytes, 0, messageData, serverInfoBytes.Length,
                                serverPortBytes.Length);

                            // Send server assignment message
                            byte[] message = new byte[4 + messageData.Length];
                            Buffer.BlockCopy(BitConverter.GetBytes((int)MessageType.ServerAssignment), 0, message, 0,
                                4);
                            Buffer.BlockCopy(messageData, 0, message, 4, messageData.Length);

                            await _udpClient.SendAsync(message, message.Length, player.ipEndPoint);

                            Console.WriteLine(
                                $"Assigned player {player.Name} to server #{serverId} on port {serverPort}");
                            _waitingPlayers.Remove(player);
                        }
                    }
                }

                await Task.Delay(checkIntervalMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in periodic tasks: {ex.Message}");
                await Task.Delay(checkIntervalMs);
            }
        }
    }

    private async Task HandleConsoleCommandsAsync()
    {
        while (_isRunning)
        {
            string command = await Task.Run(() => Console.ReadLine());

            if (string.IsNullOrEmpty(command))
                continue;

            string[] parts = command.Split(' ');

            switch (parts[0].ToLower())
            {
                case "list":
                case "clients":
                    Console.WriteLine($"Connected clients: {_connectedClients.Count}");
                    foreach (Client client in _connectedClients.Values)
                    {
                        Console.WriteLine($"  {client.Name} (ID: {client.id}, EP: {client.ipEndPoint})");
                    }

                    break;

                case "waiting":
                    Console.WriteLine($"Waiting players: {_waitingPlayers.Count}");
                    foreach (Client client in _waitingPlayers)
                    {
                        Console.WriteLine($"  {client.Name} (ID: {client.id})");
                    }

                    break;

                case "servers":
                    Console.WriteLine($"Active servers: {_activeServers.Count}");
                    foreach (var kvp in _activeServers)
                    {
                        Console.WriteLine($"  Server #{kvp.Key}, Process ID: {kvp.Value.Id}");
                    }

                    break;

                case "kick":
                    if (parts.Length > 1)
                    {
                        string playerName = parts[1];
                        Client playerToKick = _connectedClients.Values.FirstOrDefault(c =>
                            c.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                        if (playerToKick.Equals(default))
                        {
                            SendConsoleMessage(playerToKick.ipEndPoint, "You have been kicked from the server.");
                            RemoveClient(playerToKick);
                            Console.WriteLine($"Kicked player: {playerName}");
                        }
                        else
                        {
                            Console.WriteLine($"Player not found: {playerName}");
                        }
                    }

                    break;

                case "exit":
                case "quit":
                    Console.WriteLine("Shutting down matchmaker...");
                    Quit();
                    break;

                case "help":
                    Console.WriteLine("Available commands:");
                    Console.WriteLine("  clients/list - List all connected clients");
                    Console.WriteLine("  waiting - List players waiting for a match");
                    Console.WriteLine("  servers - List active game servers");
                    Console.WriteLine("  kick <name> - Kick a player");
                    Console.WriteLine("  exit/quit - Shut down the matchmaker");
                    break;

                default:
                    Console.WriteLine($"Unknown command: {parts[0]}. Type 'help' for available commands.");
                    break;
            }
        }
    }

    private void CloseAllServers()
    {
        Console.WriteLine("Closing all active game servers...");

        foreach (KeyValuePair<int, Process> server in _activeServers)
        {
            try
            {
                if (!server.Value.HasExited)
                {
                    Console.WriteLine($"Terminating server #{server.Key}");
                    server.Value.Kill();
                    server.Value.WaitForExit(1000); // Wait up to 1 second for clean exit
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error terminating server #{server.Key}: {ex.Message}");
            }
        }

        _activeServers.Clear();
        Console.WriteLine("All servers closed.");
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

    private void UpdateServerStatus(byte[] messageData)
    {
        try
        {
            // Parse server ID and player count from the first 8 bytes
            int serverId = BitConverter.ToInt32(messageData, 0);
            int playerCount = BitConverter.ToInt32(messageData, 4);

            // Validate server exists
            if (!_activeServers.ContainsKey(serverId))
            {
                Console.WriteLine($"[Matchmaker] Received status from unknown server #{serverId}");
                return;
            }

            Console.WriteLine($"[Matchmaker] Server #{serverId} status update: {playerCount} players");

            // Optional: Handle empty servers
            if (playerCount == 0)
            {
                // Could implement logic to shut down empty servers after a timeout period
                // For example, track when servers become empty and shut them down if empty for X minutes
                Console.WriteLine($"[Matchmaker] Server #{serverId} is currently empty");
            }

            // Additional status information could be parsed here if the protocol is expanded
            // For example: server load, match state, time remaining, etc.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Matchmaker] Error processing server status: {ex.Message}");
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
        Client client = new Client(clientEndPoint, _nextClientId++, Time.CurrentTime)
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
            client.LastHeartbeatTime = Time.CurrentTime;
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

    private async void SendConsoleMessage(IPEndPoint endpoint, string message)
    {
        try
        {
            // Convert the message to bytes
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            // Create the full message packet with MessageType.Console at the beginning
            byte[] fullMessage = new byte[4 + messageBytes.Length];

            // Copy the message type into the first 4 bytes
            Buffer.BlockCopy(BitConverter.GetBytes((int)MessageType.Console), 0, fullMessage, 0, 4);

            // Copy the message bytes after the type
            Buffer.BlockCopy(messageBytes, 0, fullMessage, 4, messageBytes.Length);

            // Send the message to the specified endpoint
            await _udpClient.SendAsync(fullMessage, fullMessage.Length, endpoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending console message: {ex.Message}");
        }
    }

    private Process StartGameServerProcess(int serverId, int port)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = _gameServerPath,
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
        if (!_activeServers.TryGetValue(serverId, out Process process)) return;
        Console.WriteLine($"[Matchmaker] Game server #{serverId} terminated");
        _activeServers.Remove(serverId);
    }

    private static ServerConnectionInfo DeserializeServerInfo(byte[] data)
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

    public void Quit()
    {
        // Terminate all active game servers when matchmaker shuts down
        _isRunning = false;

        CloseAllServers();
        _udpClient.Close();
        Console.WriteLine("[Matchmaker] UDP client closed");
        _udpClient.Dispose();
        Console.WriteLine("[Matchmaker] Matchmaker server closed");
        
    }
}

public class ServerConnectionInfo
{
    public string ServerIp { get; set; }
    public int ServerPort { get; set; }

    public override string ToString()
    {
        return $"{ServerIp}:{ServerPort}";
    }
}