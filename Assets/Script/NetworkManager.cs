// Fichier: Script unity/Script/NetworkManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using SocketIOClient.Newtonsoft.Json; // Assurez-vous que ce using est correct pour votre version

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    private SocketIO client;

    // --- Événements ---
    public static event Action<List<string>> OnPlayersListUpdated;
    // Gardons simple pour l'instant, sans position initiale dans l'event
    public static event Action<string, string> OnPlayerSpawn;
    public static event Action<List<PlayerInfo>> OnExistingPlayers;
    // <<< MODIFICATION: Ajout de velocityX, velocityY >>>
    public static event Action<string, float, float, bool, bool, bool, float, float> OnPlayerUpdate;
    public static event Action<string> OnPlayerRemove;
    public static event Action<string, string, string> OnChatMessage;
    public static event Action<string> OnMapLoadRequest;
    public static event Action<Vector3> OnPlayerRespawnRequest;
    public static event Action<string> OnJoinError;

    public static List<string> lastPlayersList = new List<string>();

    // Variables pour stocker temporairement les données initiales
    private string initialMapJson = null;
    private bool receivedInitialData = false;
    private string localPlayerPseudo = ""; // Pour stocker le pseudo local

    private void Awake()
    {
        Debug.Log($"[NetworkManager] Awake() appelé sur GameObject: {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})"); // Log Awake
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            Debug.Log($"[NetworkManager] >>> Instance DÉFINIE sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}). DontDestroyOnLoad appliqué."); // Log Instance Set
        }
        else if (Instance != this) // Vérifier si l'instance existante est différente de soi-même
        {
            Debug.LogWarning($"[NetworkManager] Instance DÉJÀ EXISTANTE ({Instance.gameObject.name} - InstanceID: {Instance.gameObject.GetInstanceID()}). Destruction de ce GameObject ({gameObject.name} - InstanceID: {gameObject.GetInstanceID()})..."); // Log Duplicate
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        Debug.Log($"[NetworkManager] Start() appelé sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})");
        // Vérifier si la connexion est déjà en cours ou établie pour éviter double appel
        // --- CORRECTION ICI: Suppression de !client.Connecting ---
        if (client == null || !client.Connected)
        // --- FIN CORRECTION ---
        {
            Debug.Log("[NetworkManager] Start: Appel de ConnectToServer...");
            await ConnectToServer();
        }
        else
        {
            // --- CORRECTION ICI: Log simplifié ---
            Debug.Log($"[NetworkManager] Start: Connexion déjà établie (Connected: {client.Connected}). Skip ConnectToServer.");
            // --- FIN CORRECTION ---
        }
    }

    async Task ConnectToServer()
    {
        // --- AJOUT LOG ---
        Debug.Log("[NetworkManager] ConnectToServer: Début de la configuration SocketIO.");
        // --- FIN AJOUT ---

        var uri = new Uri("http://localhost:3000"); // Ou l'URL de ton serveur distant
        client = new SocketIO(uri, new SocketIOOptions
        {
            Query = new Dictionary<string, string> { { "token", "UNITY" } },
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        client.JsonSerializer = new NewtonsoftJsonSerializer();

        client.OnConnected += (sender, e) => {
            // --- AJOUT LOG ---
            Debug.Log("[NetworkManager] >>> Événement OnConnected REÇU <<<");
            // --- FIN AJOUT ---
        };

        client.OnError += (sender, e) => {
            Debug.LogError($"[NetworkManager] >>> Événement OnError REÇU: {e} <<<"); // Log Erreur
        };

        client.OnDisconnected += (sender, e) => {
            Debug.LogWarning($"[NetworkManager] >>> Événement OnDisconnected REÇU: {e} <<<"); // Log Déco
        };

        // --- Handler 'gameJoined' AVEC CORRECTION ---
        client.On("gameJoined", response =>
        {
            // Log simple sur le thread Socket.IO
            Debug.Log("[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : Événement reçu du serveur.");

            UnityMainThreadDispatcher dispatcher = null;
            bool dispatcherFound = false; // Utiliser un drapeau pour vérifier

            try
            {
                dispatcher = UnityMainThreadDispatcher.Instance();
                dispatcherFound = (dispatcher != null); // Vérifier si on a bien récupéré l'instance

                // --- CORRECTION : Le log qui accédait à .gameObject.name a été supprimé d'ici ---

                if (!dispatcherFound)
                {
                    // Loguer l'erreur si le dispatcher est null, mais sans accéder à l'API Unity ici
                    Debug.LogError("[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : ERREUR CRITIQUE - UnityMainThreadDispatcher.Instance() est NULL !");
                    return; // Quitter si pas de dispatcher
                }
                // Log de succès simple sans appel API Unity depuis ce thread
                Debug.Log("[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : Instance Dispatcher trouvée. Tentative d'Enqueue.");

            }
            catch (Exception ex) // Capturer les erreurs potentielles venant de Instance() lui-même
            {
                Debug.LogError($"[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : ERREUR CRITIQUE lors de l'obtention/vérification de l'instance Dispatcher: {ex}");
                return; // Quitter en cas d'erreur
            }

            // Continuer avec Enqueue seulement si le dispatcher a été trouvé
            if (dispatcherFound) // Utiliser le drapeau
            {
                dispatcher.Enqueue(() => {
                    // --- Log déplacé et sécurisé À L'INTÉRIEUR d'Enqueue (s'exécute sur le Thread Unity) ---
                    // Maintenant, il est sûr d'accéder à dispatcher.gameObject.name ici si besoin pour le debug
                    // Debug.Log($"[NetworkManager] >>> Code Enqueued 'gameJoined' (Thread Unity) : Exécution commence. Dispatcher: {dispatcher.gameObject.name}");
                    Debug.Log("[NetworkManager] >>> Code Enqueued 'gameJoined' (Thread Unity) : Exécution commence."); // Version sans .gameObject.name
                                                                                                                       // --- Fin du log déplacé ---
                    try
                    {
                        // ... (le reste du code pour traiter 'data' et charger la scène reste identique) ...

                        var data = response.GetValue<GameJoinedData>();
                        if (data != null)
                        {
                            PlayerData.id = data.id;
                            localPlayerPseudo = PlayerData.pseudo;
                            if (string.IsNullOrEmpty(localPlayerPseudo))
                            {
                                Debug.LogWarning("[NetworkManager] (Thread Unity) PlayerData.pseudo est vide lors de gameJoined ! Utilisation d'un pseudo par défaut.");
                                localPlayerPseudo = $"Player_{PlayerData.id.Substring(0, 4)}"; // Fallback
                                PlayerData.pseudo = localPlayerPseudo; // Met à jour le statique aussi
                            }

                            initialMapJson = data.currentMap;
                            receivedInitialData = true;

                            Debug.Log($"[NetworkManager] (Thread Unity) ID reçu: {PlayerData.id}, Pseudo utilisé: {localPlayerPseudo}. Carte stockée.");
                            Debug.Log("[NetworkManager] (Thread Unity) >>> Tentative de chargement de GameScene via SceneManager.LoadSceneAsync...");

                            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("GameScene");

                            if (asyncLoad == null)
                            {
                                Debug.LogError("[NetworkManager] (Thread Unity) ERREUR CRITIQUE : SceneManager.LoadSceneAsync(\"GameScene\") a retourné NULL ! La scène est-elle dans Build Settings et correctement nommée ?");
                                return;
                            }
                            asyncLoad.completed += OnGameSceneLoaded;
                            Debug.Log("[NetworkManager] (Thread Unity) >>> SceneManager.LoadSceneAsync(\"GameScene\") appelé. Attente du callback 'completed'.");
                        }
                        else { Debug.LogError("[NetworkManager] (Thread Unity) Données 'gameJoined' invalides ou nulles."); }

                    }
                    catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'gameJoined': {e}"); }
                }); // Fin de la lambda Enqueue
            } // Fin du if(dispatcherFound)
        }); // Fin de client.On("gameJoined", ...)

        // --- Handler 'joinError' ---
        client.On("joinError", response => {
            Debug.Log("[NetworkManager] Callback 'joinError' (Thread Socket.IO) : Événement reçu.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => { // Sécurité avec '?'
                Debug.Log("[NetworkManager] >>> Code Enqueued 'joinError' (Thread Unity) : Exécution commence.");
                try
                {
                    var data = response.GetValue<ErrorData>();
                    string errorMsg = data?.message ?? "Erreur inconnue lors de la connexion.";
                    Debug.LogError($"[NetworkManager] (Thread Unity) Erreur de Join reçue du serveur: {errorMsg}");
                    OnJoinError?.Invoke(errorMsg);
                    // Gérer l'erreur (ex: message UI, retour menu)
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'joinError': {e}"); }
            });
        });

        // --- Handler 'existingPlayers' ---
        client.On("existingPlayers", response => {
            // <<< AJOUT DEBUG >>>
            Debug.Log("[NetworkManager] Callback 'existingPlayers' (Thread Socket.IO) : Événement reçu.");
            // <<< FIN AJOUT DEBUG >>>
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                // <<< AJOUT DEBUG >>>
                Debug.Log("[NetworkManager] >>> Code Enqueued 'existingPlayers' (Thread Unity) : Exécution commence.");
                // <<< FIN AJOUT DEBUG >>>
                try
                {
                    var data = response.GetValue<PlayerListResponse>();
                    if (data?.players != null)
                    {
                        // <<< AJOUT DEBUG >>>
                        Debug.Log($"[NetworkManager] (Thread Unity) DONNÉES EXISTINGPLAYERS REÇUES: {data.players.Count} joueurs");
                        // <<< FIN AJOUT DEBUG >>>
                        Debug.Log($"[NetworkManager] (Thread Unity) Invocation de OnExistingPlayers avec {data.players.Count} joueur(s).");
                        OnExistingPlayers?.Invoke(data.players);
                    }
                    else { Debug.LogWarning("[NetworkManager] (Thread Unity) Données 'existingPlayers' invalides ou liste vide."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'existingPlayers': {e}"); }
            });
        });

        // --- Handler 'playersList' ---
        client.On("playersList", response => {
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                try
                {
                    var data = response.GetValue<PlayerListResponse>();
                    if (data?.players != null)
                    {
                        lastPlayersList = data.players.Select(p => p.pseudo).ToList();
                        OnPlayersListUpdated?.Invoke(lastPlayersList);
                    }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'playersList': {e}"); }
            });
        });

        // --- Handler 'spawnPlayer' ---
        client.On("spawnPlayer", response => {
            // <<< AJOUT DEBUG >>>
            Debug.Log("[NetworkManager] Callback 'spawnPlayer' (Thread Socket.IO) : Événement reçu.");
            // <<< FIN AJOUT DEBUG >>>
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                // <<< AJOUT DEBUG >>>
                Debug.Log("[NetworkManager] >>> Code Enqueued 'spawnPlayer' (Thread Unity) : Exécution commence.");
                // <<< FIN AJOUT DEBUG >>>
                try
                {
                    var data = response.GetValue<PlayerInfo>(); // Ou PlayerInfoWithSpawn si modifié
                    if (data != null && data.id != PlayerData.id)
                    {
                        // <<< AJOUT DEBUG >>>
                        Debug.Log($"[NetworkManager] (Thread Unity) DONNÉES SPAWNPLAYER REÇUES: ID={data.id}, Pseudo={data.pseudo}");
                        // <<< FIN AJOUT DEBUG >>>
                        Debug.Log($"[NetworkManager] (Thread Unity) Invocation de OnPlayerSpawn pour {data.pseudo} ({data.id}).");
                        OnPlayerSpawn?.Invoke(data.id, data.pseudo); // Ou avec position si modifié
                    }
                    else if (data != null && data.id == PlayerData.id) { Debug.Log($"[NetworkManager] (Thread Unity) Ignoré spawnPlayer pour soi-même ({data.id})."); }
                    else { Debug.LogError("[NetworkManager] (Thread Unity) Données 'spawnPlayer' invalides ou ID manquant."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'spawnPlayer': {e}"); }
            });
        });

        // --- Handler 'updatePlayer' ---
        client.On("updatePlayer", response => {
            // Pas besoin de log ici si tu ne veux pas spammer la console à chaque mouvement
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                try
                {
                    // Désérialise directement dans la classe avec les float et la vélocité
                    var data = response.GetValue<PlayerUpdateData>();

                    // Vérifie si les données sont valides et si ce n'est pas le joueur local
                    if (data != null && data.id != PlayerData.id)
                    {
                        // <<< MODIFICATION: Ajout de data.velocityX, data.velocityY >>>
                        // Utilise directement data.x, data.y, data.velocityX, data.velocityY
                        OnPlayerUpdate?.Invoke(data.id, data.x, data.y, data.isRunning, data.isIdle, data.flip, data.velocityX, data.velocityY);
                    }
                    // Optionnel: Log si on ignore l'update pour soi-même
                    // else if (data != null && data.id == PlayerData.id) {
                    //     Debug.Log($"[NetworkManager] Ignoré updatePlayer pour soi-même ({data.id})");
                    // }
                }
                catch (Exception e)
                {
                    // Log en cas d'erreur pendant la désérialisation ou l'invocation
                    Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'updatePlayer': {e}");
                }
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
                        Debug.Log($"[NetworkManager] (Thread Unity) Invocation de OnPlayerRemove pour {data.id}.");
                        OnPlayerRemove?.Invoke(data.id);
                    }
                    else { Debug.LogError("[NetworkManager] (Thread Unity) Données 'removePlayer' invalides ou ID manquant."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'removePlayer': {e}"); }
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
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'chatMessage': {e}"); }
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
                        Debug.Log("[NetworkManager] (Thread Unity) Invocation de OnMapLoadRequest (rotation)...");
                        OnMapLoadRequest?.Invoke(mapJson);
                    }
                    else { Debug.LogError("[NetworkManager] (Thread Unity) JSON de carte vide reçu pour 'loadMap'."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'loadMap': {e}"); }
            });
        });

        // --- Handler 'respawnPlayer' ---
        client.On("respawnPlayer", response => {
            Debug.Log("[NetworkManager] Callback 'respawnPlayer' (Thread Socket.IO) : Événement reçu.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => { // Sécurité avec '?'
                Debug.Log("[NetworkManager] >>> Code Enqueued 'respawnPlayer' (Thread Unity) : Exécution commence.");
                try
                {
                    var data = response.GetValue<RespawnData>();
                    if (data?.spawnPoint != null)
                    {
                        Vector3 spawnPos = data.spawnPoint.ToVector3();
                        Debug.Log($"[NetworkManager] (Thread Unity) Invocation de OnPlayerRespawnRequest au point {spawnPos}.");
                        OnPlayerRespawnRequest?.Invoke(spawnPos);
                    }
                    else { Debug.LogError("[NetworkManager] (Thread Unity) Données 'respawnPlayer' invalides ou spawnPoint manquant."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'respawnPlayer': {e}"); }
            });
        });

        // --- Connexion ---
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

    // --- Appelée après le chargement de GameScene ---
    private void OnGameSceneLoaded(AsyncOperation op)
    {
        // --- AJOUT LOG ---
        Debug.Log($"[NetworkManager] >>> CALLBACK OnGameSceneLoaded (Thread Unity) : Exécution commence pour l'opération: {op}.");
        // --- FIN AJOUT ---

        // --- Étape 1: Vérifier PlayerManager ---
        if (PlayerManager.Instance == null)
        {
            Debug.LogError("[NetworkManager] (OnGameSceneLoaded) PlayerManager.Instance est null ! Assurez-vous qu'un GameObject avec PlayerManager existe dans GameScene.");
            return; // Ne pas continuer si PlayerManager est manquant
        }
        else
        {
            Debug.Log("[NetworkManager] (OnGameSceneLoaded) PlayerManager.Instance trouvé.");
        }

        // --- Étape 2: Instancier le Joueur Local ---
        if (!string.IsNullOrEmpty(PlayerData.id) && !string.IsNullOrEmpty(localPlayerPseudo))
        {
            Debug.Log($"[NetworkManager] (OnGameSceneLoaded) Demande explicite d'instanciation pour le joueur local ID: {PlayerData.id}, Pseudo: {localPlayerPseudo}");
            try
            {
                // Assigner une position initiale temporaire (ex: 0,0,0). Le respawn initial via serveur corrigera.
                PlayerManager.Instance.InstantiatePlayer(PlayerData.id, localPlayerPseudo, Vector3.zero, true); // true = isLocal
                Debug.Log("[NetworkManager] (OnGameSceneLoaded) Appel à PlayerManager.Instance.InstantiatePlayer terminé.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) ERREUR lors de l'appel à PlayerManager.Instance.InstantiatePlayer: {e}");
            }
        }
        else { Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) Impossible d'instancier le joueur local : ID ({PlayerData.id}) ou Pseudo ({localPlayerPseudo}) manquant."); }

        // --- Étape 3: Charger la Carte Initiale ---
        if (receivedInitialData)
        {
            Debug.Log("[NetworkManager] (OnGameSceneLoaded) Traitement des données initiales (carte).");
            if (!string.IsNullOrEmpty(initialMapJson))
            {
                Debug.Log("[NetworkManager] (OnGameSceneLoaded) Invocation de OnMapLoadRequest (initial)...");
                try
                {
                    OnMapLoadRequest?.Invoke(initialMapJson);
                    Debug.Log("[NetworkManager] (OnGameSceneLoaded) OnMapLoadRequest?.Invoke terminé.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) ERREUR lors de l'invocation de OnMapLoadRequest: {e}");
                }
            }
            else { Debug.LogWarning("[NetworkManager] (OnGameSceneLoaded) Pas de carte initiale à charger (JSON vide)."); }
            receivedInitialData = false;
            initialMapJson = null;
        }
        else { Debug.LogWarning("[NetworkManager] (OnGameSceneLoaded) Pas de données initiales reçues à traiter."); }

        // --- Étape 4: Demander la liste et Signaler Prêt ---
        // Les délais peuvent aider à s'assurer que tout est prêt après le chargement de scène
        Debug.Log("[NetworkManager] (OnGameSceneLoaded) Planification de RequestPlayersList (0.2s) et SendPlayerReady (0.5s)...");
        // Utiliser Instance est généralement sûr ici car OnGameSceneLoaded est un callback Unity sur le thread principal
        Invoke(nameof(RequestPlayersList), 0.2f);
        Invoke(nameof(SendPlayerReady), 0.5f); // Envoyer Ready après un court délai

        Debug.Log("[NetworkManager] (OnGameSceneLoaded) Fin de l'exécution.");
    }

    // --- Méthodes d'émission ---
    // Dans NetworkManager.cs

    public async void JoinGame(string pseudo)
    {
        Debug.Log($"[NetworkManager] JoinGame({pseudo}) appelé.");

        // --- CORRECTION : Vérifier si client est null ET s'il est connecté ---
        if (client == null)
        {
            Debug.LogError("[NetworkManager] Tentative de JoinGame mais client est null.");
            // Optionnel : Informer l'utilisateur via un événement ou UI
            OnJoinError?.Invoke("Erreur interne: Client réseau non initialisé.");
            return;
        }
        // Ajout de la vérification cruciale !
        if (!client.Connected)
        {
            Debug.LogError("[NetworkManager] Tentative de JoinGame mais client n'est PAS connecté !");
            // Optionnel : Informer l'utilisateur via un événement ou UI
            OnJoinError?.Invoke("Non connecté au serveur. Veuillez patienter ou réessayer.");
            return; // Ne pas continuer si non connecté
        }
        // --- FIN CORRECTION ---

        PlayerData.pseudo = pseudo;
        Debug.Log("[NetworkManager] Demande joinGame avec pseudo: " + pseudo);
        try
        {
            // Maintenant on sait que client n'est pas null et Connected est true
            await client.EmitAsync("joinGame", new { pseudo });
            Debug.Log("[NetworkManager] EmitAsync 'joinGame' terminé.");
        }
        catch (NullReferenceException nre) // Capturer spécifiquement NRE
        {
            // Log plus détaillé si cela se produit malgré la vérification .Connected
            Debug.LogError($"[NetworkManager] ERREUR (NullReferenceException) lors de EmitAsync 'joinGame'. Suggère un problème interne avec l'état WebSocket même si .Connected=true. Détails: {nre}");
            OnJoinError?.Invoke($"Erreur réseau (NRE) : {nre.Message}");
        }
        catch (Exception e) // Capturer les autres erreurs possibles
        {
            Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'joinGame': {e}");
            OnJoinError?.Invoke($"Erreur réseau : {e.Message}");
        }
    }

    // <<< MODIFICATION: Ajout de velocityX, velocityY >>>
    public async void SendPlayerMove(float x, float y, bool isRunning, bool isIdle, bool flip, float velocityX, float velocityY)
    {
        if (client == null || !client.Connected) return;
        try
        {
            // <<< MODIFICATION: Ajout de velocityX, velocityY dans l'objet envoyé >>>
            await client.EmitAsync("playerMove", new { x, y, isRunning, isIdle, flip, velocityX, velocityY });
        }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'playerMove': {e}"); }
    }

    private async void RequestPlayersList()
    {
        // --- AJOUT LOG ---
        Debug.Log("[NetworkManager] RequestPlayersList() appelé via Invoke.");
        // --- FIN AJOUT ---
        if (client == null || !client.Connected) { Debug.LogWarning("[NetworkManager] RequestPlayersList: Non connecté."); return; }
        try
        {
            await client.EmitAsync("getPlayersList");
        }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'getPlayersList': {e}"); }
    }

    private async void SendPlayerReady()
    {
        // --- AJOUT LOG ---
        Debug.Log("[NetworkManager] SendPlayerReady() appelé via Invoke.");
        // --- FIN AJOUT ---
        if (client == null || !client.Connected)
        {
            Debug.LogWarning("[NetworkManager] SendPlayerReady: Non connecté.");
            return;
        }
        Debug.Log("[NetworkManager] Envoi de l'événement playerReady au serveur...");
        try
        {
            await client.EmitAsync("playerReady");
            Debug.Log("[NetworkManager] EmitAsync 'playerReady' terminé.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'playerReady': {e}");
        }
    }

    public async void SendChatMessage(string message)
    {
        if (client == null || !client.Connected) return;
        if (string.IsNullOrWhiteSpace(message)) return;
        try
        {
            await client.EmitAsync("sendChatMessage", new { message });
        }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'sendChatMessage': {e}"); }
    }

    public async void SendPlayerDied()
    {
        if (client == null || !client.Connected) return;
        Debug.Log("[NetworkManager] Envoi playerDied...");
        try
        {
            await client.EmitAsync("playerDied");
        }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'playerDied': {e}"); }
    }

    // --- AJOUT : Log à la destruction ---
    private void OnDestroy()
    {
        Debug.LogWarning($"[NetworkManager] OnDestroy() appelé sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}).");
        // Si c'était l'instance principale, la remettre à null pour la prochaine fois?
        // Cela peut aider à éviter les problèmes avec les Singletons qui persistent mal dans l'éditeur.
        if (Instance == this)
        {
            Instance = null;
            Debug.LogWarning("[NetworkManager] Instance statique remise à NULL.");
        }
        // Se déconnecter proprement si ce n'est pas déjà fait et si client existe
        // Utiliser Task.Run pour ne pas bloquer OnDestroy
        // Attention : La déconnexion ici peut être problématique si OnDestroy est appelé lors d'un rechargement de scène normal
        /*
        Task.Run(async () => {
            if (client != null && client.Connected) {
                Debug.LogWarning($"[NetworkManager - OnDestroy Task] Tentative de déconnexion...");
                // await client.DisconnectAsync(); // Ne pas attendre ici car OnDestroy doit être rapide
                client.DisconnectAsync().ConfigureAwait(false); // Appel sans attendre
                Debug.LogWarning($"[NetworkManager - OnDestroy Task] Appel DisconnectAsync effectué.");
            }
            // Mettre client à null pour que Start() puisse recréer une connexion si nécessaire ? Dangereux si pas ApplicationQuit
            // client = null;
        });
        */
    }

    private async void OnApplicationQuit()
    {
        Debug.Log("[NetworkManager] OnApplicationQuit() appelé.");
        if (client != null && client.Connected)
        {
            Debug.Log("[NetworkManager] Déconnexion propre du serveur lors de la fermeture de l'application...");
            // Donner un petit délai pour que l'envoi se fasse
            await client.DisconnectAsync(); //.ConfigureAwait(false); // Peut utiliser ConfigureAwait(false)
            Debug.Log("[NetworkManager] Déconnexion OnApplicationQuit terminée.");
        }
        client = null; // Assurer le nettoyage
    }


    // --- Classes pour la désérialisation ---
    // Note : Utiliser [JsonProperty("nom_json")] si les noms C# diffèrent des clés JSON
    [System.Serializable] public class PlayerInfo { public string id; public string pseudo; /* Potentiellement: public PointData spawnPoint; */ }
    [System.Serializable] public class PlayerIdData { public string id; }
    [System.Serializable] public class PlayerListResponse { public List<PlayerInfo> players; }
    // <<< MODIFICATION: Ajout de velocityX, velocityY >>>
    [System.Serializable] public class PlayerUpdateData { public string id; public float x; public float y; public bool isRunning; public bool isIdle; public bool flip; public float velocityX; public float velocityY; }
    [System.Serializable] public class ChatMessageData { public string id; public string pseudo; public string message; }
    [System.Serializable] public class ErrorData { public string message; }
    [System.Serializable] public class PointData { public float x; public float y; public float z; public Vector3 ToVector3() => new Vector3(x, y, z); }
    [System.Serializable] public class GameJoinedData { public string room; public string id; public string currentMap; public PointData spawnPoint; /* Potentiellement: public string pseudo; */ }
    [System.Serializable] public class RespawnData { public PointData spawnPoint; }
    // Potentiellement :
    // [System.Serializable] public class PlayerInfoWithSpawn { public string id; public string pseudo; public PointData spawnPoint; }
}