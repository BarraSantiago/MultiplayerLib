namespace Utils;

public class MonoBehaviourSingleton<T> where T : MonoBehaviourSingleton<T>
{
    private static MonoBehaviourSingleton<T> _instance;

    public static T Instance => (T)_instance;

    protected virtual void Initialize()
    {
    }

    private void Awake()
    {
        _instance = this;

        Initialize();
    }
}