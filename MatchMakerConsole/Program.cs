namespace MatchMakerConsole
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.Title = "Matchmaker Server";
            Console.WriteLine("Starting Matchmaker Server...");

            // Default values
            int port = 12345;
            int playersPerServer = 2;
            int minPlayersToStartMatches = 4;
            string gameServerPath = "GameServer.exe";
            int startingPort = 12346;

            // Parse command-line arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-port":
                    case "--port":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedPort))
                            port = parsedPort;
                        break;

                    case "-players":
                    case "--players":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedPlayers))
                            playersPerServer = parsedPlayers;
                        break;

                    case "-minplayers":
                    case "--minplayers":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedMinPlayers))
                            minPlayersToStartMatches = parsedMinPlayers;
                        break;

                    case "-serverpath":
                    case "--serverpath":
                        if (i + 1 < args.Length)
                            gameServerPath = args[++i];
                        break;

                    case "-startingport":
                    case "--startingport":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int parsedStartingPort))
                            startingPort = parsedStartingPort;
                        break;
                }
            }

            // Display configuration
            Console.WriteLine($"Matchmaker Configuration:");
            Console.WriteLine($"- Listening on port: {port}");
            Console.WriteLine($"- Players per server: {playersPerServer}");
            Console.WriteLine($"- Min players to start: {minPlayersToStartMatches}");
            Console.WriteLine($"- Game server path: {gameServerPath}");
            Console.WriteLine($"- Starting port for servers: {startingPort}");
            
            try
            {
                // Create and run the matchmaker
                MatchmakerServer matchmaker = new MatchmakerServer(
                    port, 
                    playersPerServer, 
                    minPlayersToStartMatches, 
                    gameServerPath, 
                    startingPort);
                
                Console.WriteLine("Matchmaker server started. Press Ctrl+C to exit.");
                
                // Set up console cancel handler
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true;
                    Console.WriteLine("Shutting down matchmaker...");
                    matchmaker.Quit();
                };
                
                // Run the matchmaker until completion
                await matchmaker.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting matchmaker: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}