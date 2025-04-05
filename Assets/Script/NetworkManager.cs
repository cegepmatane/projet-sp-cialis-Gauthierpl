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

    // --- �v�nements ---
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

    // Variables pour stocker temporairement les donn�es initiales
    private string initialMapJson = null;
    private bool receivedInitialData = false;
    private string localPlayerPseudo = ""; // Pour stocker le pseudo local

    private void Awake()
    {
        Debug.Log($"[NetworkManager] Awake() appel� sur GameObject: {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})"); // Log Awake
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            Debug.Log($"[NetworkManager] >>> Instance D�FINIE sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}). DontDestroyOnLoad appliqu�."); // Log Instance Set
        }
        else if (Instance != this) // V�rifier si l'instance existante est diff�rente de soi-m�me
        {
            Debug.LogWarning($"[NetworkManager] Instance D�J� EXISTANTE ({Instance.gameObject.name} - InstanceID: {Instance.gameObject.GetInstanceID()}). Destruction de ce GameObject ({gameObject.name} - InstanceID: {gameObject.GetInstanceID()})..."); // Log Duplicate
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        Debug.Log($"[NetworkManager] Start() appel� sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})");
        // V�rifier si la connexion est d�j� en cours ou �tablie pour �viter double appel
        // --- CORRECTION ICI: Suppression de !client.Connecting ---
        if (client == null || !client.Connected)
        // --- FIN CORRECTION ---
        {
            Debug.Log("[NetworkManager] Start: Appel de ConnectToServer...");
            await ConnectToServer();
        }
        else
        {
            // --- CORRECTION ICI: Log simplifi� ---
            Debug.Log($"[NetworkManager] Start: Connexion d�j� �tablie (Connected: {client.Connected}). Skip ConnectToServer.");
            // --- FIN CORRECTION ---
        }
    }

    async Task ConnectToServer()
    {
        // --- AJOUT LOG ---
        Debug.Log("[NetworkManager] ConnectToServer: D�but de la configuration SocketIO.");
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
            Debug.Log("[NetworkManager] >>> �v�nement OnConnected RE�U <<<");
            // --- FIN AJOUT ---
        };

        client.OnError += (sender, e) => {
            Debug.LogError($"[NetworkManager] >>> �v�nement OnError RE�U: {e} <<<"); // Log Erreur
        };

        client.OnDisconnected += (sender, e) => {
            Debug.LogWarning($"[NetworkManager] >>> �v�nement OnDisconnected RE�U: {e} <<<"); // Log D�co
        };

        // --- Handler 'gameJoined' AVEC CORRECTION ---
        client.On("gameJoined", response =>
        {
            // Log simple sur le thread Socket.IO
            Debug.Log("[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : �v�nement re�u du serveur.");

            UnityMainThreadDispatcher dispatcher = null;
            bool dispatcherFound = false; // Utiliser un drapeau pour v�rifier

            try
            {
                dispatcher = UnityMainThreadDispatcher.Instance();
                dispatcherFound = (dispatcher != null); // V�rifier si on a bien r�cup�r� l'instance

                // --- CORRECTION : Le log qui acc�dait � .gameObject.name a �t� supprim� d'ici ---

                if (!dispatcherFound)
                {
                    // Loguer l'erreur si le dispatcher est null, mais sans acc�der � l'API Unity ici
                    Debug.LogError("[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : ERREUR CRITIQUE - UnityMainThreadDispatcher.Instance() est NULL !");
                    return; // Quitter si pas de dispatcher
                }
                // Log de succ�s simple sans appel API Unity depuis ce thread
                Debug.Log("[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : Instance Dispatcher trouv�e. Tentative d'Enqueue.");

            }
            catch (Exception ex) // Capturer les erreurs potentielles venant de Instance() lui-m�me
            {
                Debug.LogError($"[NetworkManager] Callback 'gameJoined' (Thread Socket.IO) : ERREUR CRITIQUE lors de l'obtention/v�rification de l'instance Dispatcher: {ex}");
                return; // Quitter en cas d'erreur
            }

            // Continuer avec Enqueue seulement si le dispatcher a �t� trouv�
            if (dispatcherFound) // Utiliser le drapeau
            {
                dispatcher.Enqueue(() => {
                    // --- Log d�plac� et s�curis� � L'INT�RIEUR d'Enqueue (s'ex�cute sur le Thread Unity) ---
                    // Maintenant, il est s�r d'acc�der � dispatcher.gameObject.name ici si besoin pour le debug
                    // Debug.Log($"[NetworkManager] >>> Code Enqueued 'gameJoined' (Thread Unity) : Ex�cution commence. Dispatcher: {dispatcher.gameObject.name}");
                    Debug.Log("[NetworkManager] >>> Code Enqueued 'gameJoined' (Thread Unity) : Ex�cution commence."); // Version sans .gameObject.name
                                                                                                                       // --- Fin du log d�plac� ---
                    try
                    {
                        // ... (le reste du code pour traiter 'data' et charger la sc�ne reste identique) ...

                        var data = response.GetValue<GameJoinedData>();
                        if (data != null)
                        {
                            PlayerData.id = data.id;
                            localPlayerPseudo = PlayerData.pseudo;
                            if (string.IsNullOrEmpty(localPlayerPseudo))
                            {
                                Debug.LogWarning("[NetworkManager] (Thread Unity) PlayerData.pseudo est vide lors de gameJoined ! Utilisation d'un pseudo par d�faut.");
                                localPlayerPseudo = $"Player_{PlayerData.id.Substring(0, 4)}"; // Fallback
                                PlayerData.pseudo = localPlayerPseudo; // Met � jour le statique aussi
                            }

                            initialMapJson = data.currentMap;
                            receivedInitialData = true;

                            Debug.Log($"[NetworkManager] (Thread Unity) ID re�u: {PlayerData.id}, Pseudo utilis�: {localPlayerPseudo}. Carte stock�e.");
                            Debug.Log("[NetworkManager] (Thread Unity) >>> Tentative de chargement de GameScene via SceneManager.LoadSceneAsync...");

                            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("GameScene");

                            if (asyncLoad == null)
                            {
                                Debug.LogError("[NetworkManager] (Thread Unity) ERREUR CRITIQUE : SceneManager.LoadSceneAsync(\"GameScene\") a retourn� NULL ! La sc�ne est-elle dans Build Settings et correctement nomm�e ?");
                                return;
                            }
                            asyncLoad.completed += OnGameSceneLoaded;
                            Debug.Log("[NetworkManager] (Thread Unity) >>> SceneManager.LoadSceneAsync(\"GameScene\") appel�. Attente du callback 'completed'.");
                        }
                        else { Debug.LogError("[NetworkManager] (Thread Unity) Donn�es 'gameJoined' invalides ou nulles."); }

                    }
                    catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'gameJoined': {e}"); }
                }); // Fin de la lambda Enqueue
            } // Fin du if(dispatcherFound)
        }); // Fin de client.On("gameJoined", ...)

        // --- Handler 'joinError' ---
        client.On("joinError", response => {
            Debug.Log("[NetworkManager] Callback 'joinError' (Thread Socket.IO) : �v�nement re�u.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => { // S�curit� avec '?'
                Debug.Log("[NetworkManager] >>> Code Enqueued 'joinError' (Thread Unity) : Ex�cution commence.");
                try
                {
                    var data = response.GetValue<ErrorData>();
                    string errorMsg = data?.message ?? "Erreur inconnue lors de la connexion.";
                    Debug.LogError($"[NetworkManager] (Thread Unity) Erreur de Join re�ue du serveur: {errorMsg}");
                    OnJoinError?.Invoke(errorMsg);
                    // G�rer l'erreur (ex: message UI, retour menu)
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'joinError': {e}"); }
            });
        });

        // --- Handler 'existingPlayers' ---
        client.On("existingPlayers", response => {
            // <<< AJOUT DEBUG >>>
            Debug.Log("[NetworkManager] Callback 'existingPlayers' (Thread Socket.IO) : �v�nement re�u.");
            // <<< FIN AJOUT DEBUG >>>
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                // <<< AJOUT DEBUG >>>
                Debug.Log("[NetworkManager] >>> Code Enqueued 'existingPlayers' (Thread Unity) : Ex�cution commence.");
                // <<< FIN AJOUT DEBUG >>>
                try
                {
                    var data = response.GetValue<PlayerListResponse>();
                    if (data?.players != null)
                    {
                        // <<< AJOUT DEBUG >>>
                        Debug.Log($"[NetworkManager] (Thread Unity) DONN�ES EXISTINGPLAYERS RE�UES: {data.players.Count} joueurs");
                        // <<< FIN AJOUT DEBUG >>>
                        Debug.Log($"[NetworkManager] (Thread Unity) Invocation de OnExistingPlayers avec {data.players.Count} joueur(s).");
                        OnExistingPlayers?.Invoke(data.players);
                    }
                    else { Debug.LogWarning("[NetworkManager] (Thread Unity) Donn�es 'existingPlayers' invalides ou liste vide."); }
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
            Debug.Log("[NetworkManager] Callback 'spawnPlayer' (Thread Socket.IO) : �v�nement re�u.");
            // <<< FIN AJOUT DEBUG >>>
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                // <<< AJOUT DEBUG >>>
                Debug.Log("[NetworkManager] >>> Code Enqueued 'spawnPlayer' (Thread Unity) : Ex�cution commence.");
                // <<< FIN AJOUT DEBUG >>>
                try
                {
                    var data = response.GetValue<PlayerInfo>(); // Ou PlayerInfoWithSpawn si modifi�
                    if (data != null && data.id != PlayerData.id)
                    {
                        // <<< AJOUT DEBUG >>>
                        Debug.Log($"[NetworkManager] (Thread Unity) DONN�ES SPAWNPLAYER RE�UES: ID={data.id}, Pseudo={data.pseudo}");
                        // <<< FIN AJOUT DEBUG >>>
                        Debug.Log($"[NetworkManager] (Thread Unity) Invocation de OnPlayerSpawn pour {data.pseudo} ({data.id}).");
                        OnPlayerSpawn?.Invoke(data.id, data.pseudo); // Ou avec position si modifi�
                    }
                    else if (data != null && data.id == PlayerData.id) { Debug.Log($"[NetworkManager] (Thread Unity) Ignor� spawnPlayer pour soi-m�me ({data.id})."); }
                    else { Debug.LogError("[NetworkManager] (Thread Unity) Donn�es 'spawnPlayer' invalides ou ID manquant."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'spawnPlayer': {e}"); }
            });
        });

        // --- Handler 'updatePlayer' ---
        client.On("updatePlayer", response => {
            // Pas besoin de log ici si tu ne veux pas spammer la console � chaque mouvement
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                try
                {
                    // D�s�rialise directement dans la classe avec les float et la v�locit�
                    var data = response.GetValue<PlayerUpdateData>();

                    // V�rifie si les donn�es sont valides et si ce n'est pas le joueur local
                    if (data != null && data.id != PlayerData.id)
                    {
                        // <<< MODIFICATION: Ajout de data.velocityX, data.velocityY >>>
                        // Utilise directement data.x, data.y, data.velocityX, data.velocityY
                        OnPlayerUpdate?.Invoke(data.id, data.x, data.y, data.isRunning, data.isIdle, data.flip, data.velocityX, data.velocityY);
                    }
                    // Optionnel: Log si on ignore l'update pour soi-m�me
                    // else if (data != null && data.id == PlayerData.id) {
                    //     Debug.Log($"[NetworkManager] Ignor� updatePlayer pour soi-m�me ({data.id})");
                    // }
                }
                catch (Exception e)
                {
                    // Log en cas d'erreur pendant la d�s�rialisation ou l'invocation
                    Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'updatePlayer': {e}");
                }
            });
        });


        // --- Handler 'removePlayer' ---
        client.On("removePlayer", response => {
            Debug.Log("[NetworkManager] Callback 'removePlayer' (Thread Socket.IO) : �v�nement re�u.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                Debug.Log("[NetworkManager] >>> Code Enqueued 'removePlayer' (Thread Unity) : Ex�cution commence.");
                try
                {
                    var data = response.GetValue<PlayerIdData>();
                    if (data != null && !string.IsNullOrEmpty(data.id))
                    {
                        Debug.Log($"[NetworkManager] (Thread Unity) Invocation de OnPlayerRemove pour {data.id}.");
                        OnPlayerRemove?.Invoke(data.id);
                    }
                    else { Debug.LogError("[NetworkManager] (Thread Unity) Donn�es 'removePlayer' invalides ou ID manquant."); }
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
            Debug.Log("[NetworkManager] Callback 'loadMap' (Thread Socket.IO) : �v�nement re�u.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => {
                Debug.Log("[NetworkManager] >>> Code Enqueued 'loadMap' (Thread Unity) : Ex�cution commence.");
                try
                {
                    string mapJson = response.GetValue<string>();
                    if (!string.IsNullOrEmpty(mapJson))
                    {
                        Debug.Log("[NetworkManager] (Thread Unity) Invocation de OnMapLoadRequest (rotation)...");
                        OnMapLoadRequest?.Invoke(mapJson);
                    }
                    else { Debug.LogError("[NetworkManager] (Thread Unity) JSON de carte vide re�u pour 'loadMap'."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'loadMap': {e}"); }
            });
        });

        // --- Handler 'respawnPlayer' ---
        client.On("respawnPlayer", response => {
            Debug.Log("[NetworkManager] Callback 'respawnPlayer' (Thread Socket.IO) : �v�nement re�u.");
            UnityMainThreadDispatcher.Instance()?.Enqueue(() => { // S�curit� avec '?'
                Debug.Log("[NetworkManager] >>> Code Enqueued 'respawnPlayer' (Thread Unity) : Ex�cution commence.");
                try
                {
                    var data = response.GetValue<RespawnData>();
                    if (data?.spawnPoint != null)
                    {
                        Vector3 spawnPos = data.spawnPoint.ToVector3();
                        Debug.Log($"[NetworkManager] (Thread Unity) Invocation de OnPlayerRespawnRequest au point {spawnPos}.");
                        OnPlayerRespawnRequest?.Invoke(spawnPos);
                    }
                    else { Debug.LogError("[NetworkManager] (Thread Unity) Donn�es 'respawnPlayer' invalides ou spawnPoint manquant."); }
                }
                catch (Exception e) { Debug.LogError($"[NetworkManager] (Thread Unity) ERREUR dans le code Enqueued 'respawnPlayer': {e}"); }
            });
        });

        // --- Connexion ---
        Debug.Log($"[NetworkManager] Tentative de connexion � {uri}...");
        try
        {
            await client.ConnectAsync();
            Debug.Log("[NetworkManager] ConnectAsync termin� (attente de l'�v�nement OnConnected).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkManager] �chec CRITIQUE de client.ConnectAsync(): {e.Message}");
            OnJoinError?.Invoke($"�chec connexion: {e.Message}");
        }
    }

    // --- Appel�e apr�s le chargement de GameScene ---
    private void OnGameSceneLoaded(AsyncOperation op)
    {
        // --- AJOUT LOG ---
        Debug.Log($"[NetworkManager] >>> CALLBACK OnGameSceneLoaded (Thread Unity) : Ex�cution commence pour l'op�ration: {op}.");
        // --- FIN AJOUT ---

        // --- �tape 1: V�rifier PlayerManager ---
        if (PlayerManager.Instance == null)
        {
            Debug.LogError("[NetworkManager] (OnGameSceneLoaded) PlayerManager.Instance est null ! Assurez-vous qu'un GameObject avec PlayerManager existe dans GameScene.");
            return; // Ne pas continuer si PlayerManager est manquant
        }
        else
        {
            Debug.Log("[NetworkManager] (OnGameSceneLoaded) PlayerManager.Instance trouv�.");
        }

        // --- �tape 2: Instancier le Joueur Local ---
        if (!string.IsNullOrEmpty(PlayerData.id) && !string.IsNullOrEmpty(localPlayerPseudo))
        {
            Debug.Log($"[NetworkManager] (OnGameSceneLoaded) Demande explicite d'instanciation pour le joueur local ID: {PlayerData.id}, Pseudo: {localPlayerPseudo}");
            try
            {
                // Assigner une position initiale temporaire (ex: 0,0,0). Le respawn initial via serveur corrigera.
                PlayerManager.Instance.InstantiatePlayer(PlayerData.id, localPlayerPseudo, Vector3.zero, true); // true = isLocal
                Debug.Log("[NetworkManager] (OnGameSceneLoaded) Appel � PlayerManager.Instance.InstantiatePlayer termin�.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) ERREUR lors de l'appel � PlayerManager.Instance.InstantiatePlayer: {e}");
            }
        }
        else { Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) Impossible d'instancier le joueur local : ID ({PlayerData.id}) ou Pseudo ({localPlayerPseudo}) manquant."); }

        // --- �tape 3: Charger la Carte Initiale ---
        if (receivedInitialData)
        {
            Debug.Log("[NetworkManager] (OnGameSceneLoaded) Traitement des donn�es initiales (carte).");
            if (!string.IsNullOrEmpty(initialMapJson))
            {
                Debug.Log("[NetworkManager] (OnGameSceneLoaded) Invocation de OnMapLoadRequest (initial)...");
                try
                {
                    OnMapLoadRequest?.Invoke(initialMapJson);
                    Debug.Log("[NetworkManager] (OnGameSceneLoaded) OnMapLoadRequest?.Invoke termin�.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkManager] (OnGameSceneLoaded) ERREUR lors de l'invocation de OnMapLoadRequest: {e}");
                }
            }
            else { Debug.LogWarning("[NetworkManager] (OnGameSceneLoaded) Pas de carte initiale � charger (JSON vide)."); }
            receivedInitialData = false;
            initialMapJson = null;
        }
        else { Debug.LogWarning("[NetworkManager] (OnGameSceneLoaded) Pas de donn�es initiales re�ues � traiter."); }

        // --- �tape 4: Demander la liste et Signaler Pr�t ---
        // Les d�lais peuvent aider � s'assurer que tout est pr�t apr�s le chargement de sc�ne
        Debug.Log("[NetworkManager] (OnGameSceneLoaded) Planification de RequestPlayersList (0.2s) et SendPlayerReady (0.5s)...");
        // Utiliser Instance est g�n�ralement s�r ici car OnGameSceneLoaded est un callback Unity sur le thread principal
        Invoke(nameof(RequestPlayersList), 0.2f);
        Invoke(nameof(SendPlayerReady), 0.5f); // Envoyer Ready apr�s un court d�lai

        Debug.Log("[NetworkManager] (OnGameSceneLoaded) Fin de l'ex�cution.");
    }

    // --- M�thodes d'�mission ---
    // Dans NetworkManager.cs

    public async void JoinGame(string pseudo)
    {
        Debug.Log($"[NetworkManager] JoinGame({pseudo}) appel�.");

        // --- CORRECTION : V�rifier si client est null ET s'il est connect� ---
        if (client == null)
        {
            Debug.LogError("[NetworkManager] Tentative de JoinGame mais client est null.");
            // Optionnel : Informer l'utilisateur via un �v�nement ou UI
            OnJoinError?.Invoke("Erreur interne: Client r�seau non initialis�.");
            return;
        }
        // Ajout de la v�rification cruciale !
        if (!client.Connected)
        {
            Debug.LogError("[NetworkManager] Tentative de JoinGame mais client n'est PAS connect� !");
            // Optionnel : Informer l'utilisateur via un �v�nement ou UI
            OnJoinError?.Invoke("Non connect� au serveur. Veuillez patienter ou r�essayer.");
            return; // Ne pas continuer si non connect�
        }
        // --- FIN CORRECTION ---

        PlayerData.pseudo = pseudo;
        Debug.Log("[NetworkManager] Demande joinGame avec pseudo: " + pseudo);
        try
        {
            // Maintenant on sait que client n'est pas null et Connected est true
            await client.EmitAsync("joinGame", new { pseudo });
            Debug.Log("[NetworkManager] EmitAsync 'joinGame' termin�.");
        }
        catch (NullReferenceException nre) // Capturer sp�cifiquement NRE
        {
            // Log plus d�taill� si cela se produit malgr� la v�rification .Connected
            Debug.LogError($"[NetworkManager] ERREUR (NullReferenceException) lors de EmitAsync 'joinGame'. Sugg�re un probl�me interne avec l'�tat WebSocket m�me si .Connected=true. D�tails: {nre}");
            OnJoinError?.Invoke($"Erreur r�seau (NRE) : {nre.Message}");
        }
        catch (Exception e) // Capturer les autres erreurs possibles
        {
            Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'joinGame': {e}");
            OnJoinError?.Invoke($"Erreur r�seau : {e.Message}");
        }
    }

    // <<< MODIFICATION: Ajout de velocityX, velocityY >>>
    public async void SendPlayerMove(float x, float y, bool isRunning, bool isIdle, bool flip, float velocityX, float velocityY)
    {
        if (client == null || !client.Connected) return;
        try
        {
            // <<< MODIFICATION: Ajout de velocityX, velocityY dans l'objet envoy� >>>
            await client.EmitAsync("playerMove", new { x, y, isRunning, isIdle, flip, velocityX, velocityY });
        }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'playerMove': {e}"); }
    }

    private async void RequestPlayersList()
    {
        // --- AJOUT LOG ---
        Debug.Log("[NetworkManager] RequestPlayersList() appel� via Invoke.");
        // --- FIN AJOUT ---
        if (client == null || !client.Connected) { Debug.LogWarning("[NetworkManager] RequestPlayersList: Non connect�."); return; }
        try
        {
            await client.EmitAsync("getPlayersList");
        }
        catch (Exception e) { Debug.LogError($"[NetworkManager] ERREUR lors de EmitAsync 'getPlayersList': {e}"); }
    }

    private async void SendPlayerReady()
    {
        // --- AJOUT LOG ---
        Debug.Log("[NetworkManager] SendPlayerReady() appel� via Invoke.");
        // --- FIN AJOUT ---
        if (client == null || !client.Connected)
        {
            Debug.LogWarning("[NetworkManager] SendPlayerReady: Non connect�.");
            return;
        }
        Debug.Log("[NetworkManager] Envoi de l'�v�nement playerReady au serveur...");
        try
        {
            await client.EmitAsync("playerReady");
            Debug.Log("[NetworkManager] EmitAsync 'playerReady' termin�.");
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

    // --- AJOUT : Log � la destruction ---
    private void OnDestroy()
    {
        Debug.LogWarning($"[NetworkManager] OnDestroy() appel� sur {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}).");
        // Si c'�tait l'instance principale, la remettre � null pour la prochaine fois?
        // Cela peut aider � �viter les probl�mes avec les Singletons qui persistent mal dans l'�diteur.
        if (Instance == this)
        {
            Instance = null;
            Debug.LogWarning("[NetworkManager] Instance statique remise � NULL.");
        }
        // Se d�connecter proprement si ce n'est pas d�j� fait et si client existe
        // Utiliser Task.Run pour ne pas bloquer OnDestroy
        // Attention : La d�connexion ici peut �tre probl�matique si OnDestroy est appel� lors d'un rechargement de sc�ne normal
        /*
        Task.Run(async () => {
            if (client != null && client.Connected) {
                Debug.LogWarning($"[NetworkManager - OnDestroy Task] Tentative de d�connexion...");
                // await client.DisconnectAsync(); // Ne pas attendre ici car OnDestroy doit �tre rapide
                client.DisconnectAsync().ConfigureAwait(false); // Appel sans attendre
                Debug.LogWarning($"[NetworkManager - OnDestroy Task] Appel DisconnectAsync effectu�.");
            }
            // Mettre client � null pour que Start() puisse recr�er une connexion si n�cessaire ? Dangereux si pas ApplicationQuit
            // client = null;
        });
        */
    }

    private async void OnApplicationQuit()
    {
        Debug.Log("[NetworkManager] OnApplicationQuit() appel�.");
        if (client != null && client.Connected)
        {
            Debug.Log("[NetworkManager] D�connexion propre du serveur lors de la fermeture de l'application...");
            // Donner un petit d�lai pour que l'envoi se fasse
            await client.DisconnectAsync(); //.ConfigureAwait(false); // Peut utiliser ConfigureAwait(false)
            Debug.Log("[NetworkManager] D�connexion OnApplicationQuit termin�e.");
        }
        client = null; // Assurer le nettoyage
    }


    // --- Classes pour la d�s�rialisation ---
    // Note : Utiliser [JsonProperty("nom_json")] si les noms C# diff�rent des cl�s JSON
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