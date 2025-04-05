// Fichier: Script unity/Script/PlayerManager.cs
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    public GameObject playerPrefab; // Assigner le prefab du joueur dans l'inspecteur
    // Dictionnaire pour stocker les joueurs par ID r�seau
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

    // Couleurs pour diff�rencier les joueurs (optionnel)
    private Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta, Color.white, Color.grey, Color.black };
    private int colorIndex = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Ne pas utiliser DontDestroyOnLoad si ce manager est sp�cifique � GameScene
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        Debug.Log("[PlayerManager] Awake() appel�");
    }

    private void OnEnable()
    {
        Debug.Log("[PlayerManager] OnEnable - Abonnement aux �v�nements NetworkManager.");
        // S'abonner aux �v�nements du NetworkManager
        NetworkManager.OnPlayerSpawn += HandleRemotePlayerSpawn; // Pour les autres joueurs qui spawnent
        NetworkManager.OnExistingPlayers += HandleExistingPlayersList; // Pour les joueurs d�j� l� quand on arrive
        NetworkManager.OnPlayerUpdate += HandlePlayerUpdate;
        NetworkManager.OnPlayerRemove += HandlePlayerRemove;
    }

    private void OnDisable()
    {
        Debug.Log("[PlayerManager] OnDisable - D�sabonnement des �v�nements NetworkManager.");
        // Se d�sabonner pour �viter les erreurs
        NetworkManager.OnPlayerSpawn -= HandleRemotePlayerSpawn;
        NetworkManager.OnExistingPlayers -= HandleExistingPlayersList;
        NetworkManager.OnPlayerUpdate -= HandlePlayerUpdate;
        NetworkManager.OnPlayerRemove -= HandlePlayerRemove;
    }

    /// <summary>
    /// M�thode centrale pour instancier un joueur (local ou distant).
    /// </summary>
    /// <param name="id">ID r�seau du joueur.</param>
    /// <param name="pseudo">Pseudo du joueur.</param>
    /// <param name="initialPosition">Position de d�part (peut �tre Vector3.zero si g�r� par respawn).</param>
    /// <param name="isLocal">True si c'est le joueur contr�l� localement.</param>
    public void InstantiatePlayer(string id, string pseudo, Vector3 initialPosition, bool isLocal)
    {
        // <<< AJOUT DEBUG >>>
        Debug.Log($"[PlayerManager] Tentative d'instanciation: ID={id}, Pseudo={pseudo}, Local={isLocal}");
        // <<< FIN AJOUT DEBUG >>>

        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerManager] Player Prefab non assign� ! Impossible d'instancier.");
            return;
        }

        // V�rifier si le joueur existe d�j� pour �viter les doublons
        if (!players.ContainsKey(id))
        {
            // <<< AJOUT DEBUG >>>
            Debug.Log($"[PlayerManager] Instanciation r�elle pour {id}...");
            // <<< FIN AJOUT DEBUG >>>
            Debug.Log($"[PlayerManager] Instanciation du joueur ID: {id}, Pseudo: {pseudo}, Local: {isLocal}, Position: {initialPosition}");
            GameObject newPlayer = Instantiate(playerPrefab, initialPosition, Quaternion.identity);

            // <<< AJOUT DEBUG >>>
            if (newPlayer == null)
            {
                Debug.LogError($"[PlayerManager] Instanciation �CHOU�E pour {id} ! Instantiate a retourn� null.");
                return; // Sortir si l'instanciation �choue
            }
            else
            {
                Debug.Log($"[PlayerManager] Instanciation BRUTE r�ussie pour {id}. GameObject: {newPlayer.name}");
            }
            // <<< FIN AJOUT DEBUG >>>


            PlayerController pc = newPlayer.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.SetNetworkId(id); // Donner l'ID r�seau
                pc.SetPseudo(pseudo);

                // Attribuer une couleur diff�rente (optionnel)
                pc.SetColor(playerColors[colorIndex % playerColors.Length]);
                colorIndex++;

                if (isLocal) // Si c'est le joueur local
                {
                    pc.SetAsLocalPlayer();
                    Debug.Log($"[PlayerManager] Joueur local ({pseudo} / {id}) marqu�.");
                    // La logique de suivi cam�ra devrait �tre g�r�e ailleurs (ex: script CameraFollow)
                }
                // <<< AJOUT DEBUG >>>
                else
                {
                    Debug.Log($"[PlayerManager] Joueur distant ({pseudo} / {id}) configur� (non-local).");
                }
                // <<< FIN AJOUT DEBUG >>>
            }
            else
            {
                Debug.LogError("[PlayerManager] Le prefab joueur n'a pas de script PlayerController ! Destruction de l'objet instanci�.");
                Destroy(newPlayer); // Nettoyer l'objet invalide
                return; // Sortir pour ne pas l'ajouter au dictionnaire
            }

            players.Add(id, newPlayer); // Ajouter au dictionnaire
            newPlayer.name = $"Player_{(isLocal ? "LOCAL_" : "REMOTE_")}{pseudo}_{id.Substring(0, 4)}"; // Nommer l'objet pour le d�bogage
                                                                                                        // <<< AJOUT DEBUG >>>
            Debug.Log($"[PlayerManager] Joueur {pseudo} ({id}) ajout� au dictionnaire. Total: {players.Count}. GameObject: {newPlayer.name}");
            // <<< FIN AJOUT DEBUG >>>
        }
        else
        {
            // <<< AJOUT DEBUG >>>
            Debug.LogWarning($"[PlayerManager] Joueur {id} d�j� existant, skip instanciation. Mise � jour �ventuelle.");
            // <<< FIN AJOUT DEBUG >>>
            Debug.LogWarning($"[PlayerManager] Tentative d'instanciation pour un joueur d�j� existant (id={id}). Mise � jour du pseudo et v�rification �tat local.");
            GameObject existingPlayer = players[id];
            PlayerController existingPc = existingPlayer.GetComponent<PlayerController>();
            if (existingPc)
            {
                existingPc.SetPseudo(pseudo); // Met � jour le pseudo au cas o�
                                              // Si on re�oit une instruction pour instancier localement un objet qui existait d�j� comme distant (cas rare), on le met � jour.
                if (isLocal && !existingPc.IsLocalPlayer) existingPc.SetAsLocalPlayer();
            }
        }
    }

    // G�re l'arriv�e d'un joueur distant signal� par le serveur
    // private void HandleRemotePlayerSpawn(string id, string pseudo, Vector3 initialPosition) // Signature si position envoy�e
    private void HandleRemotePlayerSpawn(string id, string pseudo)
    {
        // <<< AJOUT DEBUG >>>
        Debug.Log($"[PlayerManager] HandleRemotePlayerSpawn APPEL� pour id={id}, pseudo={pseudo}");
        // <<< FIN AJOUT DEBUG >>>
        // Utilise InstantiatePlayer avec isLocal = false.
        // Position initiale � 0,0,0 car le serveur ne l'envoie pas encore dans cet exemple.
        // Si le serveur envoyait la position: InstantiatePlayer(id, pseudo, initialPosition, false);
        InstantiatePlayer(id, pseudo, Vector3.zero, false);
    }

    // G�re la liste des joueurs d�j� pr�sents re�ue lors de notre connexion
    private void HandleExistingPlayersList(List<NetworkManager.PlayerInfo> existingPlayers)
    {
        // <<< AJOUT DEBUG >>>
        Debug.Log($"[PlayerManager] HandleExistingPlayersList APPEL� avec {existingPlayers.Count} joueurs.");
        // <<< FIN AJOUT DEBUG >>>
        foreach (var playerInfo in existingPlayers)
        {
            // Important: Ne pas s'instancier soi-m�me � partir de cette liste
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
                Debug.Log($"[PlayerManager] Ignor� joueur existant local: ID={playerInfo.id}");
            }
            // <<< FIN AJOUT DEBUG >>>
        }
    }

    // G�re la mise � jour de position/�tat d'un joueur distant
    void HandlePlayerUpdate(string id, float x, float y, bool isRunning, bool isIdle, bool flip)
    {
        // On ignore les updates pour le joueur local car il est contr�l� par l'input direct
        if (id == PlayerData.id) return;

        if (players.TryGetValue(id, out GameObject playerObj))
        {
            // Lissage de mouvement (interpolation)
            Vector3 targetPosition = new Vector3(x, y, playerObj.transform.position.z); // Garder le Z actuel
            // Ajuster le facteur de lissage (ex: 10f � 20f) selon la r�activit� souhait�e
            playerObj.transform.position = Vector3.Lerp(playerObj.transform.position, targetPosition, Time.deltaTime * 15f);

            // Mise � jour de l'�tat (animation, flip) via le PlayerController distant
            var playerController = playerObj.GetComponent<PlayerController>();
            if (playerController)
            {
                playerController.UpdateRemoteState(isRunning, isIdle, flip);
            }
        }
        // else { Debug.LogWarning($"[PlayerManager] HandlePlayerUpdate: Joueur ID {id} non trouv� pour mise � jour."); } // Peut �tre verbeux
    }

    // G�re la suppression d'un joueur qui s'est d�connect�
    void HandlePlayerRemove(string id)
    {
        Debug.Log($"[PlayerManager] Demande de suppression pour id={id}");
        if (players.TryGetValue(id, out GameObject playerObj))
        {
            Destroy(playerObj); // D�truire le GameObject
            players.Remove(id); // Retirer du dictionnaire
            Debug.Log($"[PlayerManager] Joueur {id} supprim�. Total restant: {players.Count}");
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] Tentative de suppression pour un joueur inconnu (id={id}).");
        }
    }

    // --- M�thodes Publiques Utilitaires ---

    // Affiche une bulle de chat au-dessus du joueur sp�cifi�
    public void DisplayChatBubble(string playerId, string msg)
    {
        if (players.TryGetValue(playerId, out GameObject playerObj))
        {
            PlayerController pc = playerObj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.DisplayChatMessage(msg); // Utilise la dur�e par d�faut d�finie dans PlayerController
            }
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] DisplayChatBubble: Joueur introuvable (id={playerId})");
        }
    }

    // R�cup�re le script PlayerController d'un joueur par son ID
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