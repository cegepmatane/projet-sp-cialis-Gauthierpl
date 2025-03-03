using UnityEngine;
using UnityEngine.SceneManagement;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    private SocketIO client;

    public static event Action<List<string>> OnPlayersListUpdated; // Pour l'UI (liste de pseudos)
    public static event Action<string, string> OnPlayerSpawn;      // (id, pseudo)
    public static event Action<string, float, float> OnPlayerUpdate; // (id, x position, y position)
    public static event Action<string> OnPlayerRemove;             // (id)

    public static List<string> lastPlayersList = new List<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            Debug.Log("NetworkManager prêt et persistant !");
        }
        else
        {
            Debug.Log("Instance NetworkManager déjà existante, suppression...");
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

        // Quand le serveur confirme qu'on a rejoint la partie
        client.On("gameJoined", response =>
        {
            // Important : exécuter les actions Unity sur le thread principal
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("Réception de l'événement 'gameJoined'");
                var data = response.GetValue<Dictionary<string, string>>();
                if (data.ContainsKey("room"))
                    Debug.Log($"Salon global rejoint: {data["room"]}");
                if (data.ContainsKey("id"))
                    PlayerData.id = data["id"]; // Sauvegarde de l'id local

                Debug.Log("Chargement asynchrone de la scène GameScene...");
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("GameScene");

                asyncLoad.completed += (op) =>
                {
                    Debug.Log("Scène GameScene chargée !");

                    // Attendre un court délai pour que tout soit bien en place
                    Instance.Invoke(nameof(RequestPlayersList), 1.0f);
                };
            });
        });

        // Réception de la liste complète des joueurs
        client.On("playersList", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("Réception de l'événement 'playersList'");
                var data = response.GetValue<Dictionary<string, List<Dictionary<string, string>>>>();
                if (data.ContainsKey("players"))
                {
                    List<string> playersPseudoList = new List<string>();
                    foreach (var playerInfo in data["players"])
                    {
                        string id = playerInfo["id"];
                        string pseudo = playerInfo["pseudo"];
                        playersPseudoList.Add(pseudo);

                        // Forcer un spawn local pour chaque joueur listé
                        Debug.Log($"[playersList] Force spawn for id={id}, pseudo={pseudo}");
                        OnPlayerSpawn?.Invoke(id, pseudo);
                    }
                    lastPlayersList = playersPseudoList;
                    Debug.Log("Liste des joueurs connectés : " + string.Join(", ", playersPseudoList));
                    OnPlayersListUpdated?.Invoke(playersPseudoList);
                }
                else
                {
                    Debug.LogWarning("Erreur : pas de clé 'players' dans playersList");
                }
            });
        });

        // Réception de l'event "spawnPlayer" (serveur)
        client.On("spawnPlayer", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("Réception de l'événement 'spawnPlayer'");
                var data = response.GetValue<Dictionary<string, object>>();
                string id = data["id"].ToString();
                string pseudo = data["pseudo"].ToString();
                Debug.Log($"spawnPlayer -> id:{id}, pseudo:{pseudo}");
                OnPlayerSpawn?.Invoke(id, pseudo);
            });
        });

        // Réception de l'event "updatePlayer" (mouvement d'un joueur)
        client.On("updatePlayer", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("Réception de l'événement 'updatePlayer'");
                var data = response.GetValue<Dictionary<string, object>>();
                string id = data["id"].ToString();
                string xStr = data["x"].ToString();
                string yStr = data["y"].ToString();

                float x, y;
                if (float.TryParse(xStr, NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                    float.TryParse(yStr, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                {
                    OnPlayerUpdate?.Invoke(id, x, y);
                }
                else
                {
                    Debug.LogError($"FormatException sur xStr={xStr} ou yStr={yStr}");
                }
            });
        });



        // Réception de l'event "removePlayer" (joueur déconnecté)
        client.On("removePlayer", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("Réception de l'événement 'removePlayer'");
                var data = response.GetValue<Dictionary<string, object>>();
                string id = data["id"].ToString();
                OnPlayerRemove?.Invoke(id);
            });
        });

        // Connexion Socket.IO
        await client.ConnectAsync();
    }

    public async void JoinGame(string pseudo)
    {
        Debug.Log("Demande de rejoindre le jeu avec pseudo: " + pseudo);
        var data = new Dictionary<string, string>
        {
            { "pseudo", pseudo }
        };
        await client.EmitAsync("joinGame", data);
    }

    public async void SendPlayerMove(float x, float y)
    {
        var data = new Dictionary<string, float> { { "x", x }, { "y", y } };
        await client.EmitAsync("playerMove", data);
    }


    // Déconnexion propre quand on quitte le jeu
    private async void OnDestroy()
    {
        if (client != null)
        {
            await client.DisconnectAsync();
        }
    }

    private async void RequestPlayersList()
    {
        Debug.Log("Demande manuelle de mise à jour des joueurs après chargement... (Emit getPlayersList)");
        await client.EmitAsync("getPlayersList");
        Debug.Log("Après EmitAsync getPlayersList, requête envoyée.");
    }

}
