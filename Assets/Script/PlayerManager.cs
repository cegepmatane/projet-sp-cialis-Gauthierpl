// Fichier: Script unity/Script/PlayerManager.cs
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    public GameObject playerPrefab; // Assigner le prefab du joueur dans l'inspecteur
    // Dictionnaire pour stocker les joueurs par ID réseau
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    // Dictionnaire pour stocker les Rigidbodies associés pour accès rapide
    private Dictionary<string, Rigidbody2D> playerRigidbodies = new Dictionary<string, Rigidbody2D>();


    // Couleurs pour différencier les joueurs (optionnel)
    private Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta, Color.white, Color.grey, Color.black };
    private int colorIndex = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Ne pas utiliser DontDestroyOnLoad si ce manager est spécifique à GameScene
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        Debug.Log("[PlayerManager] Awake() appelé");
    }

    private void OnEnable()
    {
        Debug.Log("[PlayerManager] OnEnable - Abonnement aux événements NetworkManager.");
        // S'abonner aux événements du NetworkManager
        NetworkManager.OnPlayerSpawn += HandleRemotePlayerSpawn; // Pour les autres joueurs qui spawnent
        NetworkManager.OnExistingPlayers += HandleExistingPlayersList; // Pour les joueurs déjà là quand on arrive
        NetworkManager.OnPlayerUpdate += HandlePlayerUpdate;
        NetworkManager.OnPlayerRemove += HandlePlayerRemove;
    }

    private void OnDisable()
    {
        Debug.Log("[PlayerManager] OnDisable - Désabonnement des événements NetworkManager.");
        // Se désabonner pour éviter les erreurs
        NetworkManager.OnPlayerSpawn -= HandleRemotePlayerSpawn;
        NetworkManager.OnExistingPlayers -= HandleExistingPlayersList;
        NetworkManager.OnPlayerUpdate -= HandlePlayerUpdate;
        NetworkManager.OnPlayerRemove -= HandlePlayerRemove;
    }

    /// <summary>
    /// Méthode centrale pour instancier un joueur (local ou distant).
    /// </summary>
    /// <param name="id">ID réseau du joueur.</param>
    /// <param name="pseudo">Pseudo du joueur.</param>
    /// <param name="initialPosition">Position de départ (peut être Vector3.zero si géré par respawn).</param>
    /// <param name="isLocal">True si c'est le joueur contrôlé localement.</param>
    public void InstantiatePlayer(string id, string pseudo, Vector3 initialPosition, bool isLocal)
    {
        // <<< AJOUT DEBUG >>>
        Debug.Log($"[PlayerManager] Tentative d'instanciation: ID={id}, Pseudo={pseudo}, Local={isLocal}");
        // <<< FIN AJOUT DEBUG >>>

        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerManager] Player Prefab non assigné ! Impossible d'instancier.");
            return;
        }

        // Vérifier si le joueur existe déjà pour éviter les doublons
        if (!players.ContainsKey(id))
        {
            // <<< AJOUT DEBUG >>>
            Debug.Log($"[PlayerManager] Instanciation réelle pour {id}...");
            // <<< FIN AJOUT DEBUG >>>
            Debug.Log($"[PlayerManager] Instanciation du joueur ID: {id}, Pseudo: {pseudo}, Local: {isLocal}, Position: {initialPosition}");
            GameObject newPlayer = Instantiate(playerPrefab, initialPosition, Quaternion.identity);

            // <<< AJOUT DEBUG >>>
            if (newPlayer == null)
            {
                Debug.LogError($"[PlayerManager] Instanciation ÉCHOUÉE pour {id} ! Instantiate a retourné null.");
                return; // Sortir si l'instanciation échoue
            }
            else
            {
                Debug.Log($"[PlayerManager] Instanciation BRUTE réussie pour {id}. GameObject: {newPlayer.name}");
            }
            // <<< FIN AJOUT DEBUG >>>


            PlayerController pc = newPlayer.GetComponent<PlayerController>();
            Rigidbody2D rb = newPlayer.GetComponent<Rigidbody2D>(); // Récupérer le Rigidbody

            if (pc != null && rb != null) // Vérifier que les deux composants existent
            {
                pc.SetNetworkId(id); // Donner l'ID réseau
                pc.SetPseudo(pseudo);

                // Attribuer une couleur différente (optionnel)
                pc.SetColor(playerColors[colorIndex % playerColors.Length]);
                colorIndex++;

                if (isLocal) // Si c'est le joueur local
                {
                    pc.SetAsLocalPlayer();
                    Debug.Log($"[PlayerManager] Joueur local ({pseudo} / {id}) marqué.");
                    // La logique de suivi caméra devrait être gérée ailleurs (ex: script CameraFollow)
                }
                // <<< AJOUT DEBUG >>>
                else
                {
                    // Pour les joueurs distants, s'assurer que le Rigidbody n'est PAS Kinematic
                    // et que la gravité est activée (si nécessaire, dépend de ton prefab)
                    // rb.isKinematic = false; // Décommenter si nécessaire
                    // rb.simulated = true;    // Décommenter si nécessaire
                    Debug.Log($"[PlayerManager] Joueur distant ({pseudo} / {id}) configuré (non-local).");
                }
                // <<< FIN AJOUT DEBUG >>>

                players.Add(id, newPlayer); // Ajouter au dictionnaire des GameObjects
                playerRigidbodies.Add(id, rb); // Ajouter au dictionnaire des Rigidbodies
                newPlayer.name = $"Player_{(isLocal ? "LOCAL_" : "REMOTE_")}{pseudo}_{id.Substring(0, 4)}"; // Nommer l'objet pour le débogage
                                                                                                            // <<< AJOUT DEBUG >>>
                Debug.Log($"[PlayerManager] Joueur {pseudo} ({id}) ajouté aux dictionnaires. Total: {players.Count}. GameObject: {newPlayer.name}");
                // <<< FIN AJOUT DEBUG >>>
            }
            else
            {
                if (pc == null) Debug.LogError($"[PlayerManager] Le prefab joueur ({newPlayer.name}) n'a pas de script PlayerController !");
                if (rb == null) Debug.LogError($"[PlayerManager] Le prefab joueur ({newPlayer.name}) n'a pas de Rigidbody2D !");
                Destroy(newPlayer); // Nettoyer l'objet invalide
                return; // Sortir pour ne pas l'ajouter aux dictionnaires
            }


        }
        else
        {
            // <<< AJOUT DEBUG >>>
            Debug.LogWarning($"[PlayerManager] Joueur {id} déjà existant, skip instanciation. Mise à jour éventuelle.");
            // <<< FIN AJOUT DEBUG >>>
            Debug.LogWarning($"[PlayerManager] Tentative d'instanciation pour un joueur déjà existant (id={id}). Mise à jour du pseudo et vérification état local.");
            GameObject existingPlayer = players[id];
            PlayerController existingPc = existingPlayer.GetComponent<PlayerController>();
            if (existingPc)
            {
                existingPc.SetPseudo(pseudo); // Met à jour le pseudo au cas où
                                              // Si on reçoit une instruction pour instancier localement un objet qui existait déjà comme distant (cas rare), on le met à jour.
                if (isLocal && !existingPc.IsLocalPlayer) existingPc.SetAsLocalPlayer();
            }
        }
    }

    // Gère l'arrivée d'un joueur distant signalé par le serveur
    // private void HandleRemotePlayerSpawn(string id, string pseudo, Vector3 initialPosition) // Signature si position envoyée
    private void HandleRemotePlayerSpawn(string id, string pseudo)
    {
        // <<< AJOUT DEBUG >>>
        Debug.Log($"[PlayerManager] HandleRemotePlayerSpawn APPELÉ pour id={id}, pseudo={pseudo}");
        // <<< FIN AJOUT DEBUG >>>
        // Utilise InstantiatePlayer avec isLocal = false.
        // Position initiale à 0,0,0 car le serveur ne l'envoie pas encore dans cet exemple.
        // Si le serveur envoyait la position: InstantiatePlayer(id, pseudo, initialPosition, false);
        InstantiatePlayer(id, pseudo, Vector3.zero, false);
    }

    // Gère la liste des joueurs déjà présents reçue lors de notre connexion
    private void HandleExistingPlayersList(List<NetworkManager.PlayerInfo> existingPlayers)
    {
        // <<< AJOUT DEBUG >>>
        Debug.Log($"[PlayerManager] HandleExistingPlayersList APPELÉ avec {existingPlayers.Count} joueurs.");
        // <<< FIN AJOUT DEBUG >>>
        foreach (var playerInfo in existingPlayers)
        {
            // Important: Ne pas s'instancier soi-même à partir de cette liste
            if (playerInfo.id != PlayerData.id)
            {
                // <<< AJOUT DEBUG >>>
                Debug.Log($"[PlayerManager] Traitement joueur existant distant: ID={playerInfo.id}, Pseudo={playerInfo.pseudo}");
                // <<< FIN AJOUT DEBUG >>>
                // Si le serveur envoyait la position:
                // Vector3 initialPos = playerInfo.spawnPoint?.ToVector3() ?? Vector3.zero;
                // InstantiatePlayer(playerInfo.id, playerInfo.pseudo, initialPos, false);

                // Version actuelle:
                InstantiatePlayer(playerInfo.id, playerInfo.pseudo, Vector3.zero, false);
            }
            // <<< AJOUT DEBUG >>>
            else
            {
                Debug.Log($"[PlayerManager] Ignoré joueur existant local: ID={playerInfo.id}");
            }
            // <<< FIN AJOUT DEBUG >>>
        }
    }

    // Gère la mise à jour de position/état/vélocité d'un joueur distant
    // <<< MODIFICATION: Ajout de velocityX, velocityY >>>
    void HandlePlayerUpdate(string id, float x, float y, bool isRunning, bool isIdle, bool flip, float velocityX, float velocityY)
    {
        // On ignore les updates pour le joueur local car il est contrôlé par l'input direct
        if (id == PlayerData.id) return;

        if (players.TryGetValue(id, out GameObject playerObj) && playerRigidbodies.TryGetValue(id, out Rigidbody2D rb))
        {
            // Lissage de mouvement (interpolation) pour la POSITION
            Vector3 targetPosition = new Vector3(x, y, playerObj.transform.position.z); // Garder le Z actuel
            // Ajuster le facteur de lissage (ex: 10f à 20f) selon la réactivité souhaitée
            playerObj.transform.position = Vector3.Lerp(playerObj.transform.position, targetPosition, Time.deltaTime * 15f);

            // <<< AJOUT: Appliquer la VÉLOCITÉ directement >>>
            // Ceci écrase l'effet de la gravité locale et force le Rigidbody à avoir la vitesse correcte
            rb.linearVelocity = new Vector2(velocityX, velocityY);

            // Mise à jour de l'état VISUEL (animation, flip) via le PlayerController distant
            var playerController = playerObj.GetComponent<PlayerController>();
            if (playerController)
            {
                // UpdateRemoteState ne gère plus la vélocité, juste l'animation et le flip
                playerController.UpdateRemoteState(isRunning, isIdle, flip);
            }
        }
        // else { Debug.LogWarning($"[PlayerManager] HandlePlayerUpdate: Joueur ID {id} non trouvé pour mise à jour."); } // Peut être verbeux
    }


    // Gère la suppression d'un joueur qui s'est déconnecté
    void HandlePlayerRemove(string id)
    {
        Debug.Log($"[PlayerManager] Demande de suppression pour id={id}");
        if (players.TryGetValue(id, out GameObject playerObj))
        {
            Destroy(playerObj); // Détruire le GameObject
            players.Remove(id); // Retirer du dictionnaire
            playerRigidbodies.Remove(id); // <<< AJOUT: Retirer aussi du dictionnaire des Rigidbodies >>>
            Debug.Log($"[PlayerManager] Joueur {id} supprimé. Total restant: {players.Count}");
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] Tentative de suppression pour un joueur inconnu (id={id}).");
        }
    }

    // --- Méthodes Publiques Utilitaires ---

    // Affiche une bulle de chat au-dessus du joueur spécifié
    public void DisplayChatBubble(string playerId, string msg)
    {
        if (players.TryGetValue(playerId, out GameObject playerObj))
        {
            PlayerController pc = playerObj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.DisplayChatMessage(msg); // Utilise la durée par défaut définie dans PlayerController
            }
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] DisplayChatBubble: Joueur introuvable (id={playerId})");
        }
    }

    // Récupère le script PlayerController d'un joueur par son ID
    public PlayerController GetPlayerController(string id)
    {
        if (players.TryGetValue(id, out GameObject playerObj))
        {
            return playerObj.GetComponent<PlayerController>();
        }
        // Debug.LogWarning($"[PlayerManager] GetPlayerController: Joueur introuvable (id={id})"); // Optionnel
        return null;
    }
}