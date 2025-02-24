using UnityEngine;
using UnityEngine.SceneManagement;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    private SocketIO client;

    public static event Action<List<string>> OnPlayersListUpdated; // Pour l'UI (liste de pseudos)
    public static event Action<string, string> OnPlayerSpawn;        // (id, pseudo)
    public static event Action<string, float> OnPlayerUpdate;        // (id, x position)
    public static event Action<string> OnPlayerRemove;               // (id)

    public static List<string> lastPlayersList = new List<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        await ConnectToServer();
    }

    async Task ConnectToServer()
    {
        client = new SocketIO("http://localhost:3000");

        client.OnConnected += (sender, e) =>
        {
            Debug.Log("Connecté au serveur Socket.IO !");
        };

        client.On("gameJoined", response =>
        {
            Debug.Log("Réception de l'événement 'gameJoined'");
            var data = response.GetValue<Dictionary<string, string>>();
            if (data.ContainsKey("room"))
                Debug.Log($"Salon global rejoint: {data["room"]}");
            if (data.ContainsKey("id"))
                PlayerData.id = data["id"]; // Sauvegarde de l'id local

            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                Debug.Log("Chargement de la scène GameScene...");
                SceneManager.LoadScene("GameScene");
            });
        });

        client.On("playersList", response =>
        {
            Debug.Log("Réception de l'événement 'playersList'");
            // Traitement de la liste des joueurs pour l'UI (optionnel)
            // ...
        });

        client.On("spawnPlayer", response =>
        {
            Debug.Log("Réception de l'événement 'spawnPlayer'");
            var data = response.GetValue<Dictionary<string, object>>();
            string id = data["id"].ToString();
            string pseudo = data["pseudo"].ToString();
            OnPlayerSpawn?.Invoke(id, pseudo);
        });

        client.On("updatePlayer", response =>
        {
            Debug.Log("Réception de l'événement 'updatePlayer'");
            var data = response.GetValue<Dictionary<string, object>>();
            string id = data["id"].ToString();
            float x = float.Parse(data["x"].ToString());
            OnPlayerUpdate?.Invoke(id, x);
        });

        client.On("removePlayer", response =>
        {
            Debug.Log("Réception de l'événement 'removePlayer'");
            var data = response.GetValue<Dictionary<string, object>>();
            string id = data["id"].ToString();
            OnPlayerRemove?.Invoke(id);
        });

        await client.ConnectAsync();
    }

    public async void JoinGame(string pseudo)
    {
        Debug.Log("Demande de rejoindre le jeu avec pseudo: " + pseudo);
        var data = new Dictionary<string, string>
        {
            {"pseudo", pseudo }
        };
        await client.EmitAsync("joinGame", data);
    }

    public async void SendPlayerMove(float x)
    {
        var data = new Dictionary<string, float> { { "x", x } };
        await client.EmitAsync("playerMove", data);
    }

    private async void OnDestroy()
    {
        if (client != null)
        {
            await client.DisconnectAsync();
        }
    }
}
