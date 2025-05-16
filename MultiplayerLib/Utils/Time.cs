namespace MultiplayerLib.Utils;

public static class Time
{
    private static readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

    public static float CurrentTime => (float)_stopwatch.Elapsed.TotalSeconds;
}