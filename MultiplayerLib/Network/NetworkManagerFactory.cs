using System.Net;
using Network.ClientDir;
using Network.Server;

namespace Network;

public class NetworkManagerFactory : MonoBehaviour
{
    [SerializeField] private ClientNetworkManager clientManagerPrefab;
    [SerializeField] private TMP_Dropdown ColorSelector;
    [SerializeField] private Button DisconnectButton;
    [SerializeField] private InputField PlayerNameInput;
    [SerializeField] private ServerNetworkManager serverManagerPrefab;

    public ServerNetworkManager CreateServerManager(int port)
    {
        ServerNetworkManager manager = Instantiate(serverManagerPrefab);
        manager.StartServer(port);
        DisconnectButton.onClick.AddListener(() =>
        {
            manager.Dispose();
            Application.Quit();
        });
        return manager;
    }

    public ClientNetworkManager CreateClientManager(IPAddress ip, int port)
    {
        ClientNetworkManager manager = Instantiate(clientManagerPrefab);

        manager.StartClient(ip, port, PlayerNameInput.text, ColorSelector.value);

        DisconnectButton.onClick.AddListener(() =>
        {
            manager.Dispose();
            Application.Quit();
        });
        return manager;
    }
}