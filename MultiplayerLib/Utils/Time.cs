namespace MultiplayerLib.Utils;

public static class Time
{
    public static long CurrentTime => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
}