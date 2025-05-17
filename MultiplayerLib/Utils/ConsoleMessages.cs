namespace MultiplayerLib.Utils;

public class ConsoleMessages
{
    public delegate void LogDelegate(string message);
    
    private static LogDelegate _logAction = DefaultLog;
    
    public static LogDelegate LogAction
    {
        get => _logAction;
        set => _logAction = value ?? DefaultLog; // Prevent null logger
    }
    
    private static void DefaultLog(string message)
    {
        ConsoleMessages.Log($"[ConsoleMessages] {message}");
    }
    
    public static void Log(string message)
    {
        _logAction(message);
    }
}