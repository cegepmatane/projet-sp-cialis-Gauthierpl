// Fichier: Script unity/Script/NetworkManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using SocketIOClient.Newtonsoft.Json; // Assure-toi que ce using est correct pour ta version

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    private SocketIO client;

    // --- Événements (Option 2) ---
    public static event Action<List<string>> OnPlayersListUpdated;
    public static event Action<string, string, Vector3> OnPlayerSpawn; // id, pseudo, pos
    public static event Action<List<SpawnPlayerData>> OnExistingPlayers;
    public static event Action<string, float, float, bool, bool, bool, float, float> OnPlayerUpdate; // id, x, y, isRunning, isIdle, flip, velX, velY
    public static event Action<string> OnPlayerRemove;
    public static event Action<string, string, string> OnChatMessage; // id, pseudo, message
    public static event Action<string> OnMapLoadRequest; // mapJson
    public static event Action<string> OnJoinError; // message

    public static List<string> lastPlayersList = new List<string>();

    // --- Variables Internes ---
    private string initialMapJson = null;
    private Vector3 initialSpawnPoint = Vector3.zero;
    private bool receivedInitialData = false;
    private string localPlayerPseudo = "";

    // Référence au PlayerController local (sera cherchée au besoin)
    private PlayerController localPlayerController = null;

    // --- Méthodes Unity Lifecycle ---
    private void Awake()
    {
        Debug.Log($"[NetworkManager] Awake() appelé sur GameObject: {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            Debug.Log($"[NetworkManager] >>> Instance DÉFINIE sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}). DontDestroyOnLoad appliqué.");
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[NetworkManager] Instance DÉJÀ EXISTANTE ({Instance.gameObject.name} - InstanceID: {Instance.gameObject.GetInstanceID()}). Destruction de ce GameObject ({gameObject.name} - InstanceID: {gameObject.GetInstanceID()})...");
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        Debug.Log($"[NetworkManager] Start() appelé sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})");
        if (client == null || !client.Connected)
        {
            Debug.Log("[NetworkManager] Start: Appel de ConnectToServer...");
            await ConnectToServer();
        }
        else
        {
            Debug.Log($"[NetworkManager] Start: Connexion déjà établie (Connected: {client.Connected}). Skip ConnectToServer.");
        }
    }

    private void OnDestroy()
    {
        Debug.LogWarning($"[NetworkManager] OnDestroy() appelé sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}).");
        if (Instance == this)
        {
            Instance = null;
            Debug.LogWarning("[NetworkManager] Instance statique remise à NULL.");
        }
        // Optionnel: Déconnexion propre si nécessaire, mais peut causer pbs lors des rechargements de scène normaux
        // Task.Run(async () => { if (client != null && client.Connected) await client.DisconnectAsync(); });
    }

    private async void OnApplicationQuit()
    {
        Debug.Log("[NetworkManager] OnApplicationQuit() appelé.");
        if (client != null && client.Connected)
        {
            Debug.Log("[NetworkManager] Déconnexion propre du serveur lors de la fermeture...");
            await client.DisconnectAsync();
            Debug.Log("[NetworkManager] Déconnexion OnApplicationQuit terminée.");
        }
        client = null;
    }

    // --- Connexion et Handlers Socket.IO ---
    async Task ConnectToServer()
    {
        Debug.Log("[NetworkManager] ConnectToServer: Début de la configuration SocketIO.");
        var uri = new Uri("http://localhost:3000"); // Ou l'URL de ton serveur distant
        client = new SocketIO(uri, new SocketIOOptions
        {
            Query = new Dictionary<string, string> { { "token", "UNITY" } },
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        client.JsonSerializer = new NewtonsoftJsonSerializer();

        // --- Abonnements aux événements système Socket.IO ---
        client.OnConnected += (sender, e) => { Debug.Log("[NetworkManager] >>> Événement OnConnected REÇU <<<"); };
        client.OnError += (sender, e) => { Debug.LogError($"[NetworkManager] >>> Événement OnError REÇU: {e} <<<"); };
        client.OnDisconnected += (sender, e) => { Debug.LogWarning($"[NetworkManager] >>> Événement OnDisconnected REÇU: {e} <<<"); };

        // --- Handler 'gameJoined' ---
        client.On("gameJoined", response =>
        {
            Debug.Log("[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : Événement reçu du serveur.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                Debug.Log("[NetworkManager] >>> Code Enqueued 'gameJoined' (Thread Unity) : Exécution commence.");
                try
                {
                    var data = response.GetValue<GameJoinedData>();
                    if (data != null && data.spawnPoint != null)
                    {
                        PlayerData.id = data.id;
                        localPlayerPseudo = PlayerData.pseudo; // Assumer que PlayerData.pseudo a été mis avant JoinGame
                        if (string.IsNullOrEmpty(localPlayerPseudo)) { localPlayerPseudo = $"Player_{PlayerData.id.Substring(0, 4)}"; PlayerData.pseudo = localPlayerPseudo; Debug.LogWarning("[NetworkManager] Pseudo vide, pseudo par défaut généré."); }
                        initialMapJson = data.currentMap;
                        initialSpawnPoint = data.spawnPoint.ToVector3();
                        receivedInitialData = true;
                        Debug.Log($"[NetworkManager] (Thread Unity) ID reçu: {PlayerData.id}, Pseudo: {localPlayerPseudo}. Carte et Spawn initial stockés.");
                        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("GameScene");
                        if (asyncLoad == null) { Debug.LogError("[NetworkManager] ERREUR CRITIQUE : SceneManager.LoadSceneAsync(\"GameScene\") a retourné NULL !"); return; }
                        asyncLoad.completed += OnGameSceneLoaded;
                    }
                    else { Debug.LogError("[NetworkManager] Données 'gameJoined' invalides ou spawnPoint manquant."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'gameJoined': {e}"); }
            });
        });

        // --- Handler 'joinError' ---
        client.On("joinError", response => {
            Debug.Log("[NetworkManager] Callback 'joinError' (Thread Socket.IO) : Événement reçu.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                Debug.Log("[NetworkManager] >>> Code Enqueued 'joinError' (Thread Unity) : Exécution commence.");
                try
                {
                    var data = response.GetValue<ErrorData>();
                    string errorMsg = data?.message ?? "Erreur inconnue lors de la connexion.";
                    Debug.LogError($"[NetworkManager] Erreur de Join reçue du serveur: {errorMsg}");
                    OnJoinError?.Invoke(errorMsg);
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'joinError': {e}"); }
            });
        });

        // --- Handler 'existingPlayers' ---
        client.On("existingPlayers", response => {
            Debug.Log("[NetworkManager] Callback 'existingPlayers' (Thread Socket.IO) : Événement reçu.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                Debug.Log("[NetworkManager] >>> Code Enqueued 'existingPlayers' (Thread Unity) : Exécution commence.");
                try
                {
                    var data = response.GetValue<ExistingPlayersResponse>(); // Utilise la classe avec SpawnPlayerData
                    if (data?.players != null)
                    {
                        Debug.Log($"[NetworkManager] DONNÉES EXISTINGPLAYERS REÇUES: {data.players.Count} joueurs");
                        OnExistingPlayers?.Invoke(data.players);
                    }
                    else { Debug.LogWarning("[NetworkManager] Données 'existingPlayers' invalides ou liste vide."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'existingPlayers': {e}"); }
            });
        });

        // --- Handler 'playersList' ---
        client.On("playersList", response => {
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                try
                {
                    var data = response.GetValue<PlayerListResponse>(); // Utilise PlayerInfo standard
                    if (data?.players != null)
                    {
                        lastPlayersList = data.players.Select(p => p.pseudo).ToList();
                        OnPlayersListUpdated?.Invoke(lastPlayersList);
                    }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'playersList': {e}"); }
            });
        });

        // --- Handler 'spawnPlayer' (CORRIGÉ pour chercher la réf locale au besoin) ---
        client.On("spawnPlayer", response => {
            Debug.Log("[NetworkManager] Callback 'spawnPlayer' (Thread Socket.IO) : Événement reçu.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                Debug.Log("[NetworkManager] >>> Code Enqueued 'spawnPlayer' (Thread Unity) : Exécution commence.");
                try
                {
                    var data = response.GetValue<SpawnPlayerData>();
                    if (data != null && data.spawnPoint != null)
                    {
                        Vector3 spawnPos = data.spawnPoint.ToVector3();
                        Debug.Log($"[NetworkManager] DONNÉES SPAWNPLAYER REÇUES: ID={data.id}, Pseudo={data.pseudo}, Pos={spawnPos}");

                        if (data.id == PlayerData.id)
                        {
                            // Joueur LOCAL qui (re)spawn
                            Debug.Log($"[NetworkManager] C'est le joueur LOCAL ({data.id}) qui spawn/respawn.");
                            FindLocalPlayerController(); // <<< Cherche la référence MAINTENANT
                            if (localPlayerController != null)
                            {
                                localPlayerController.ReviveAndMoveToSpawn(spawnPos);
                                Debug.Log($"[NetworkManager] Appel de ReviveAndMoveToSpawn pour le joueur local à {spawnPos}.");
                            }
                            else { Debug.LogError($"[NetworkManager] ERREUR: Impossible de trouver le PlayerController local ({data.id}) pour le faire respawn !"); }
                        }
                        else
                        {
                            // Joueur DISTANT qui (re)spawn
                            Debug.Log($"[NetworkManager] C'est un joueur DISTANT ({data.pseudo} / {data.id}) qui spawn/respawn.");
                            OnPlayerSpawn?.Invoke(data.id, data.pseudo, spawnPos); // Déclenche création/recréation via PlayerManager
                        }
                    }
                    else { Debug.LogError("[NetworkManager] Données 'spawnPlayer' invalides, ID ou spawnPoint manquant."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'spawnPlayer': {e}"); }
            });
        });

        // --- Handler 'updatePlayer' ---
        client.On("updatePlayer", response => {
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                try
                {
                    var data = response.GetValue<PlayerUpdateData>();
                    if (data != null && data.id != PlayerData.id)
                    {
                        OnPlayerUpdate?.Invoke(data.id, data.x, data.y, data.isRunning, data.isIdle, data.flip, data.velocityX, data.velocityY);
                    }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'updatePlayer': {e}"); }
            });
        });

        // --- Handler 'removePlayer' ---
        client.On("removePlayer", response => {
            Debug.Log("[NetworkManager] Callback 'removePlayer' (Thread Socket.IO) : Événement reçu.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                Debug.Log("[NetworkManager] >>> Code Enqueued 'removePlayer' (Thread Unity) : Exécution commence.");
                try
                {
                    var data = response.GetValue<PlayerIdData>();
                    if (data != null && !string.IsNullOrEmpty(data.id))
                    {
                        Debug.Log($"[NetworkManager] Invocation de OnPlayerRemove pour {data.id}.");
                        OnPlayerRemove?.Invoke(data.id); // PlayerManager gérera la destruction (sauf pour le local)
                    }
                    else { Debug.LogError("[NetworkManager] Données 'removePlayer' invalides ou ID manquant."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'removePlayer': {e}"); }
            });
        });

        // --- Handler 'chatMessage' ---
        client.On("chatMessage", response => {
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                try
                {
                    var data = response.GetValue<ChatMessageData>();
                    if (data != null) { OnChatMessage?.Invoke(data.id, data.pseudo, data.message); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'chatMessage': {e}"); }
            });
        });

        // --- Handler 'loadMap' ---
        client.On("loadMap", response => {
            Debug.Log("[NetworkManager] Callback 'loadMap' (Thread Socket.IO) : Événement reçu.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                Debug.Log("[NetworkManager] >>> Code Enqueued 'loadMap' (Thread Unity) : Exécution commence.");
                try
                {
                    string mapJson = response.GetValue<string>();
                    if (!string.IsNullOrEmpty(mapJson))
                    {
                        Debug.Log("[NetworkManager] Invocation de OnMapLoadRequest (rotation)...");
                        OnMapLoadRequest?.Invoke(mapJson);
                        localPlayerController = null; // <<< Vider la réf locale avant changement de carte
                        Debug.Log("[NetworkManager] Référence localPlayerController vidée avant chargement nouvelle carte.");
                    }
                    else { Debug.LogError("[NetworkManager] JSON de carte vide reçu pour 'loadMap'."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR dans le code Enqueued 'loadMap': {e}"); }
            });
        });

        // --- Connexion Finale ---
        Debug.Log($"[NetworkManager] Tentative de connexion à {uri}...");
        try
        {
            await client.ConnectAsync();
            Debug.Log("[NetworkManager] ConnectAsync terminé (attente de l'événement OnConnected).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkManager] Échec CRITIQUE de client.ConnectAsync(): {e.Message}");
            OnJoinError?.Invoke($"Échec connexion: {e.Message}");
        }
    }

    // --- Gestion Chargement Scène ---
    private void OnGameSceneLoaded(AsyncOperation op)
    {
        Debug.Log($"[NetworkManager] >>> CALLBACK OnGameSceneLoaded (Thread Unity) : Exécution commence pour l'opération: {op}.");

        if (PlayerManager.Instance == null) { Debug.LogError("[NetworkManager] (OnGameSceneLoaded) PlayerManager.Instance est null !"); return; }
        Debug.Log("[NetworkManager] (OnGameSceneLoaded) PlayerManager.Instance trouvé.");

        // Étape 1: Charger la Carte
        if (receivedInitialData)
        {
            Debug.Log("[NetworkManager] (OnGameSceneLoaded) Traitement des données initiales (carte).");
            if (!string.IsNullOrEmpty(initialMapJson))
            {
                try { OnMapLoadRequest?.Invoke(initialMapJson); Debug.Log("[NetworkManager] (OnGameSceneLoaded) OnMapLoadRequest?.Invoke terminé."); }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) ERREUR lors de l'invocation de OnMapLoadRequest: {e}"); }
            }
            else { Debug.LogWarning("[NetworkManager] (OnGameSceneLoaded) Pas de carte initiale à charger (JSON vide)."); }
        }
        else { Debug.Log("[NetworkManager] (OnGameSceneLoaded) Pas de données initiales reçues à traiter (probablement changement de carte)."); }

        // Étape 2: Instancier le Joueur Local INACTIF (seulement si données initiales)
        if (receivedInitialData && !string.IsNullOrEmpty(PlayerData.id) && !string.IsNullOrEmpty(localPlayerPseudo))
        {
            Debug.Log($"[NetworkManager] (OnGameSceneLoaded) Demande d'instanciation INITIALE INACTIVE pour le joueur local ID: {PlayerData.id}");
            try
            {
                PlayerManager.Instance.InstantiatePlayer(PlayerData.id, localPlayerPseudo, initialSpawnPoint, true);
                Debug.Log("[NetworkManager] (OnGameSceneLoaded) Appel à PlayerManager.Instance.InstantiatePlayer terminé (devrait être inactif).");
                // On ne cherche PAS la référence ici, on attend le spawnPlayer event
            }
            catch (Exception e) { Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) ERREUR lors de l'appel à PlayerManager.Instance.InstantiatePlayer: {e}"); }
            receivedInitialData = false; // Marquer comme traitées
            initialMapJson = null;
        }
        else if (string.IsNullOrEmpty(PlayerData.id) || string.IsNullOrEmpty(localPlayerPseudo)) { Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) Impossible d'instancier le joueur local : ID ou Pseudo manquant."); }
        else { Debug.Log("[NetworkManager] (OnGameSceneLoaded) Données initiales déjà traitées ou absentes, pas d'instanciation locale ici."); }

        // Étape 3: Demander la liste et Signaler Prêt
        Debug.Log("[NetworkManager] (OnGameSceneLoaded) Planification de RequestPlayersList (0.2s) et SendPlayerReady (0.5s)...");
        Invoke(nameof(RequestPlayersList), 0.2f);
        Invoke(nameof(SendPlayerReady), 0.5f);

        Debug.Log("[NetworkManager] (OnGameSceneLoaded) Fin de l'exécution.");
    }

    // --- Méthode Utilitaire pour Trouver le Joueur Local ---
    private void FindLocalPlayerController()
    {
        if (!string.IsNullOrEmpty(PlayerData.id) && PlayerManager.Instance != null)
        {
            localPlayerController = PlayerManager.Instance.GetPlayerController(PlayerData.id);
            if (localPlayerController != null) { Debug.Log($"[NetworkManager] Référence au PlayerController local ({PlayerData.id}) trouvée/mise à jour."); }
            else { Debug.LogWarning($"[NetworkManager] FindLocalPlayerController n'a pas trouvé l'ID {PlayerData.id} dans PlayerManager."); }
        }
        else
        {
            localPlayerController = null;
            if (PlayerManager.Instance == null) Debug.LogError("[NetworkManager] FindLocalPlayerController: PlayerManager.Instance est null !");
            else if (string.IsNullOrEmpty(PlayerData.id)) Debug.LogError("[NetworkManager] FindLocalPlayerController: PlayerData.id est vide !");
        }
    }

    // --- Méthodes d'Émission vers le Serveur ---
    public async void JoinGame(string pseudo)
    {
        Debug.Log($"[NetworkManager] JoinGame({pseudo}) appelé.");
        if (client == null) { Debug.LogError("[NetworkManager] Client null."); OnJoinError?.Invoke("Erreur: Client réseau non initialisé."); return; }
        if (!client.Connected) { Debug.LogError("[NetworkManager] Non connecté !"); OnJoinError?.Invoke("Non connecté au serveur."); return; }

        PlayerData.pseudo = pseudo; // Stocker avant l'envoi
        Debug.Log("[NetworkManager] Demande joinGame avec pseudo: " + pseudo);
        try { await client.EmitAsync("joinGame", new { pseudo }); Debug.Log("[NetworkManager] EmitAsync 'joinGame' terminé."); }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'joinGame': {e}"); OnJoinError?.Invoke($"Erreur réseau : {e.Message}"); }
    }

    public async void SendPlayerMove(float x, float y, bool isRunning, bool isIdle, bool flip, float velocityX, float velocityY)
    {
        if (client == null || !client.Connected) return;
        try { await client.EmitAsync("playerMove", new { x, y, isRunning, isIdle, flip, velocityX, velocityY }); }
        catch (Exception e) { /* Optionnel: Log d'erreur moins fréquent */ } // Évite spam console
    }

    private async void RequestPlayersList()
    {
        Debug.Log("[NetworkManager] RequestPlayersList() appelé via Invoke.");
        if (client == null || !client.Connected) { Debug.LogWarning("[NetworkManager] RequestPlayersList: Non connecté."); return; }
        try { await client.EmitAsync("getPlayersList"); }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'getPlayersList': {e}"); }
    }

    private async void SendPlayerReady()
    {
        Debug.Log("[NetworkManager] SendPlayerReady() appelé via Invoke.");
        if (client == null || !client.Connected) { Debug.LogWarning("[NetworkManager] SendPlayerReady: Non connecté."); return; }
        Debug.Log("[NetworkManager] Envoi de l'événement playerReady au serveur...");
        try { await client.EmitAsync("playerReady"); Debug.Log("[NetworkManager] EmitAsync 'playerReady' terminé."); }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'playerReady': {e}"); }
    }

    public async void SendChatMessage(string message)
    {
        if (client == null || !client.Connected) return;
        if (string.IsNullOrWhiteSpace(message)) return;
        try { await client.EmitAsync("sendChatMessage", new { message }); }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'sendChatMessage': {e}"); }
    }

    public async void SendPlayerDied()
    {
        if (client == null || !client.Connected) return;
        Debug.Log("[NetworkManager] Envoi playerDied...");
        try { await client.EmitAsync("playerDied"); }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'playerDied': {e}"); }
    }

    // --- Classes pour la désérialisation ---
    [System.Serializable] public class PlayerInfo { public string id; public string pseudo; }
    [System.Serializable] public class PlayerListResponse { public List<PlayerInfo> players; }
    [System.Serializable] public class SpawnPlayerData { public string id; public string pseudo; public PointData spawnPoint; }
    [System.Serializable] public class ExistingPlayersResponse { public List<SpawnPlayerData> players; }
    [System.Serializable] public class PlayerIdData { public string id; }
    [System.Serializable] public class PlayerUpdateData { public string id; public float x; public float y; public bool isRunning; public bool isIdle; public bool flip; public float velocityX; public float velocityY; }
    [System.Serializable] public class ChatMessageData { public string id; public string pseudo; public string message; }
    [System.Serializable] public class ErrorData { public string message; }
    [System.Serializable] public class PointData { public float x; public float y; public float z; public Vector3 ToVector3() => new Vector3(x, y, z); }
    [System.Serializable] public class GameJoinedData { public string room; public string id; public string currentMap; public PointData spawnPoint; }

} // Fin de la classe NetworkManager