// Fichier: Script unity/Script/PlayerManager.cs
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    public GameObject playerPrefab;
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    private Dictionary<string, Rigidbody2D> playerRigidbodies = new Dictionary<string, Rigidbody2D>();

    private Color[] playerColors = { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta, Color.white, Color.grey, Color.black };
    private int colorIndex = 0;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
        Debug.Log("[PlayerManager] Awake() appelé");
    }

    private void OnEnable()
    {
        Debug.Log("[PlayerManager] OnEnable - Abonnement aux événements NetworkManager.");
        NetworkManager.OnPlayerSpawn += HandlePlayerSpawn;
        NetworkManager.OnExistingPlayers += HandleExistingPlayersList;
        NetworkManager.OnPlayerUpdate += HandlePlayerUpdate;
        NetworkManager.OnPlayerRemove += HandlePlayerRemove;
    }

    private void OnDisable()
    {
        Debug.Log("[PlayerManager] OnDisable - Désabonnement des événements NetworkManager.");
        NetworkManager.OnPlayerSpawn -= HandlePlayerSpawn;
        NetworkManager.OnExistingPlayers -= HandleExistingPlayersList;
        NetworkManager.OnPlayerUpdate -= HandlePlayerUpdate;
        NetworkManager.OnPlayerRemove -= HandlePlayerRemove;
    }

    public void InstantiatePlayer(string id, string pseudo, Vector3 initialPosition, bool isLocal)
    {
        // (Code InstantiatePlayer inchangé par rapport à la version précédente)
        Debug.Log($"[PlayerManager] Tentative d'instanciation: ID={id}, Pseudo={pseudo}, Local={isLocal}, Pos={initialPosition}");
        if (playerPrefab == null) { Debug.LogError("[PlayerManager] Player Prefab non assigné !"); return; }
        if (players.ContainsKey(id))
        {
            Debug.LogWarning($"[PlayerManager] Joueur {id} déjà existant, skip instanciation.");
            GameObject existingPlayerGO = players[id];
            PlayerController existingPC = existingPlayerGO.GetComponent<PlayerController>();
            if (existingPC != null && existingPC.pseudo != pseudo) { existingPC.SetPseudo(pseudo); }
            return;
        }
        Debug.Log($"[PlayerManager] Instanciation réelle pour {id}...");
        GameObject newPlayer = Instantiate(playerPrefab, initialPosition, Quaternion.identity);
        if (newPlayer == null) { Debug.LogError($"[PlayerManager] Instanciation ÉCHOUÉE pour {id} !"); return; }
        PlayerController pc = newPlayer.GetComponent<PlayerController>();
        Rigidbody2D rb = newPlayer.GetComponent<Rigidbody2D>();
        if (pc != null && rb != null)
        {
            pc.SetNetworkId(id);
            pc.SetPseudo(pseudo);
            pc.SetColor(playerColors[colorIndex % playerColors.Length]);
            colorIndex++;
            players.Add(id, newPlayer);
            playerRigidbodies.Add(id, rb);
            newPlayer.name = $"Player_{(isLocal ? "LOCAL_" : "REMOTE_")}{pseudo}_{id.Substring(0, 4)}";
            Debug.Log($"[PlayerManager] Joueur {pseudo} ({id}) ajouté aux dictionnaires. Total: {players.Count}. GameObject: {newPlayer.name}");
            if (isLocal)
            {
                pc.SetAsLocalPlayer();
                pc.SetInitialInactiveState();
                Debug.Log($"[PlayerManager] Joueur local ({pseudo} / {id}) marqué et rendu INACTIF initialement.");
            }
            else
            {
                rb.simulated = true;
                SpriteRenderer sr = pc.spriteRenderer;
                Collider2D col = newPlayer.GetComponent<Collider2D>();
                if (sr) sr.enabled = true;
                if (col) col.enabled = true;
                Debug.Log($"[PlayerManager] Joueur distant ({pseudo} / {id}) configuré (actif).");
            }
        }
        else
        {
            if (pc == null) Debug.LogError($"[PlayerManager] Le prefab joueur ({newPlayer.name}) n'a pas de script PlayerController !");
            if (rb == null) Debug.LogError($"[PlayerManager] Le prefab joueur ({newPlayer.name}) n'a pas de Rigidbody2D !");
            Destroy(newPlayer);
        }
    }

    // HandlePlayerSpawn (Inchangé par rapport à la version précédente - gère spawn DISTANT)
    private void HandlePlayerSpawn(string id, string pseudo, Vector3 spawnPosition)
    {
        Debug.Log($"[PlayerManager] HandlePlayerSpawn (DISTANT) APPELÉ pour id={id}, pseudo={pseudo}, pos={spawnPosition}");
        if (players.ContainsKey(id))
        {
            Debug.LogWarning($"[PlayerManager] Joueur distant {id} existe déjà lors du spawn. Destruction de l'ancien...");
            HandlePlayerRemove(id); // Utilise la méthode de suppression existante (qui ne détruira pas le local)
        }
        InstantiatePlayer(id, pseudo, spawnPosition, false);
    }

    // HandleExistingPlayersList (Inchangé par rapport à la version précédente)
    private void HandleExistingPlayersList(List<NetworkManager.SpawnPlayerData> existingPlayers)
    {
        Debug.Log($"[PlayerManager] HandleExistingPlayersList APPELÉ avec {existingPlayers.Count} joueurs.");
        foreach (var playerInfo in existingPlayers)
        {
            if (playerInfo.id != PlayerData.id)
            {
                Debug.Log($"[PlayerManager] Traitement joueur existant distant: ID={playerInfo.id}, Pseudo={playerInfo.pseudo}");
                Vector3 initialPos = playerInfo.spawnPoint?.ToVector3() ?? Vector3.zero;
                InstantiatePlayer(playerInfo.id, playerInfo.pseudo, initialPos, false);
            }
            else { Debug.Log($"[PlayerManager] Ignoré joueur existant local: ID={playerInfo.id}"); }
        }
    }

    // HandlePlayerUpdate (Inchangé par rapport à la version précédente)
    void HandlePlayerUpdate(string id, float x, float y, bool isRunning, bool isIdle, bool flip, float velocityX, float velocityY)
    {
        if (id == PlayerData.id) return;
        if (players.TryGetValue(id, out GameObject playerObj) && playerRigidbodies.TryGetValue(id, out Rigidbody2D rb))
        {
            Vector3 targetPosition = new Vector3(x, y, playerObj.transform.position.z);
            playerObj.transform.position = Vector3.Lerp(playerObj.transform.position, targetPosition, Time.deltaTime * 15f);
            rb.linearVelocity = new Vector2(velocityX, velocityY);
            var playerController = playerObj.GetComponent<PlayerController>();
            if (playerController) { playerController.UpdateRemoteState(isRunning, isIdle, flip); }
        }
    }

    // HandlePlayerRemove (MODIFIÉ)
    void HandlePlayerRemove(string id)
    {
        Debug.Log($"[PlayerManager] Demande de suppression pour id={id}");

        // <<< AJOUT: Ne pas détruire le joueur local >>>
        if (id == PlayerData.id)
        {
            Debug.LogWarning($"[PlayerManager] Ignoré removePlayer pour le joueur local ({id}). Sa propre logique Die()/Revive() gère son état.");
            // On ne fait rien ici, car Die() a déjà désactivé les composants locaux.
            // L'objet doit persister pour que ReviveAndMoveToSpawn fonctionne.
            return;
        }
        // <<< FIN AJOUT >>>

        if (players.TryGetValue(id, out GameObject playerObj))
        {
            // On détruit uniquement les joueurs distants
            Destroy(playerObj);
            players.Remove(id);
            playerRigidbodies.Remove(id);
            Debug.Log($"[PlayerManager] Joueur DISTANT {id} supprimé. Total restant: {players.Count}");
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] Tentative de suppression pour un joueur inconnu ou déjà supprimé (id={id}).");
        }
    }

    // DisplayChatBubble (Inchangé)
    public void DisplayChatBubble(string playerId, string msg) { /* ... */ }

    // GetPlayerController (Inchangé)
    public PlayerController GetPlayerController(string id)
    {
        players.TryGetValue(id, out GameObject playerObj);
        return playerObj != null ? playerObj.GetComponent<PlayerController>() : null;
    }
}