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
    public static event Action<string, float, float, bool, bool> OnPlayerUpdate; // (id, x, y, la valeur de IsIdle, la valeur de IsRunning)
    public static event Action<string> OnPlayerRemove;             // (id)

    public static List<string> lastPlayersList = new List<string>();


    public static event Action<string, string, string> OnChatMessage;
    // param1: l'id du joueur qui envoie
    // param2: le pseudo du joueur
    // param3: le texte du message


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            Debug.Log("NetworkManager pr�t et persistant !");
        }
        else
        {
            Debug.Log("Instance NetworkManager d�j� existante, suppression...");
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        await ConnectToServer();
    }

    async Task ConnectToServer()
    {
        client = new SocketIO("http://192.46.222.42:3000");

        client.OnConnected += (sender, e) =>
        {
            Debug.Log("Connect� au serveur Socket.IO !");
        };

        // Quand le serveur confirme qu'on a rejoint la partie
        client.On("gameJoined", response =>
        {
            // Important : ex�cuter les actions Unity sur le thread principal
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("R�ception de l'�v�nement 'gameJoined'");
                var data = response.GetValue<Dictionary<string, string>>();
                if (data.ContainsKey("room"))
                    Debug.Log($"Salon global rejoint: {data["room"]}");
                if (data.ContainsKey("id"))
                    PlayerData.id = data["id"]; // Sauvegarde de l'id local

                Debug.Log("Chargement asynchrone de la sc�ne GameScene...");
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("GameScene");

                // Une fois la sc�ne charg�e
                asyncLoad.completed += (op) =>
                {
                    Debug.Log("Sc�ne GameScene charg�e !");

                    // 1) On r�cup�re la liste compl�te des joueurs existants (pour local spawn)
                    Instance.Invoke(nameof(RequestPlayersList), 0.5f);

                    // 2) Ensuite, on signale au serveur qu'on est pr�t
                    Instance.Invoke(nameof(SendPlayerReady), 1.0f);
                };
            });
        });

        // R�ception de la liste compl�te des joueurs
        client.On("playersList", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("R�ception de l'�v�nement 'playersList'");
                var data = response.GetValue<Dictionary<string, List<Dictionary<string, string>>>>();
                if (data.ContainsKey("players"))
                {
                    List<string> playersPseudoList = new List<string>();
                    foreach (var playerInfo in data["players"])
                    {
                        string id = playerInfo["id"];
                        string pseudo = playerInfo["pseudo"];

                        // Ne pas forcer un spawn local du joueur s'il est lui-m�me en train de se connecter
                        if (id != PlayerData.id)
                        {
                            Debug.Log($"[playersList] Force spawn pour id={id}, pseudo={pseudo}");
                            OnPlayerSpawn?.Invoke(id, pseudo);
                        }

                        playersPseudoList.Add(pseudo);
                    }
                    lastPlayersList = playersPseudoList;
                    Debug.Log("Liste des joueurs connect�s : " + string.Join(", ", playersPseudoList));
                    OnPlayersListUpdated?.Invoke(playersPseudoList);
                }
                else
                {
                    Debug.LogWarning("Erreur : pas de cl� 'players' dans playersList");
                }
            });
        });


        // R�ception de l'event "spawnPlayer" (envoy� par playerReady c�t� serveur)
        client.On("spawnPlayer", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("R�ception de l'�v�nement 'spawnPlayer'");
                var data = response.GetValue<Dictionary<string, object>>();
                string id = data["id"].ToString();
                string pseudo = data["pseudo"].ToString();
                Debug.Log($"spawnPlayer -> id:{id}, pseudo:{pseudo}");
                OnPlayerSpawn?.Invoke(id, pseudo);
            });
        });

        // R�ception de l'event "updatePlayer" (mouvement)
        client.On("updatePlayer", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("R�ception de l'�v�nement 'updatePlayer'");
                var data = response.GetValue<Dictionary<string, object>>();
                string id = data["id"].ToString();
                string xStr = data["x"].ToString();
                string yStr = data["y"].ToString();
                // Parse des bool�ens
                bool isRunning = bool.Parse(data["isRunning"].ToString());
        
                bool isIdle = bool.Parse(data["isIdle"].ToString());
        


                        float x, y;
                if (float.TryParse(xStr, NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                    float.TryParse(yStr, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                {
                    OnPlayerUpdate?.Invoke(id, x, y, isRunning, isIdle);
                }
                else
                {
                    Debug.LogError($"FormatException sur xStr={xStr} ou yStr={yStr}");
                }
            });
        });

        // R�ception de l'event "removePlayer"
        client.On("removePlayer", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                Debug.Log("R�ception de l'�v�nement 'removePlayer'");
                var data = response.GetValue<Dictionary<string, object>>();
                string id = data["id"].ToString();
                OnPlayerRemove?.Invoke(id);
            });
        });



        client.On("chatMessage", response =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                var data = response.GetValue<Dictionary<string, object>>();
                string id = data["id"].ToString();
                string pseudo = data["pseudo"].ToString();
                string message = data["message"].ToString();
                // time = data["time"] si tu veux l�utiliser

                OnChatMessage?.Invoke(id, pseudo, message);
            });
        });




        // Connexion
        await client.ConnectAsync();
    }

    public async void JoinGame(string pseudo)
    {
        Debug.Log("Demande de rejoindre le jeu avec pseudo: " + pseudo);
        var data = new Dictionary<string, string> { { "pseudo", pseudo } };
        await client.EmitAsync("joinGame", data);
    }

    public async void SendPlayerMove(float x, float y, bool isRunning, bool isIdle)
    {
        var data = new Dictionary<string, object>
        {
            { "x", x },
            { "y", y },
            { "isRunning", isRunning },
            { "isIdle", isIdle }
        };
            await client.EmitAsync("playerMove", data);
    }

    // � appeler juste apr�s le chargement de la sc�ne pour recevoir la liste des joueurs
    private async void RequestPlayersList()
    {
        Debug.Log("Demande de mise � jour des joueurs (Emit getPlayersList)...");
        await client.EmitAsync("getPlayersList");
    }

    // Signale au serveur qu�on est pr�t � �tre spawn� c�t� autres joueurs
    private async void SendPlayerReady()
    {
        Debug.Log("Envoi de 'playerReady' au serveur...");
        await client.EmitAsync("playerReady");
    }

    // D�connexion propre
    private async void OnDestroy()
    {
        if (client != null)
        {
            await client.DisconnectAsync();
        }
    }

    public async void SendChatMessage(string message)
    {
        var data = new Dictionary<string, string>
    {
        { "message", message }
    };
        await client.EmitAsync("sendChatMessage", data);
    }

}
