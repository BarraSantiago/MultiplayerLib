namespace MultiplayerLib.Utils;

public class Singleton<T> where T : Singleton<T>
{
    private static readonly object _lock = new object();
    private static T _instance;

    /// <summary>
    /// Gets the singleton instance, creating it if necessary.
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (_lock)
            {
                if (_instance != null) return _instance;
                _instance.Initialize();
            }
            return _instance;
        }
    }

    protected virtual void Initialize()
    {
        
    }

    private void Awake()
    {
        Initialize();
    }
}