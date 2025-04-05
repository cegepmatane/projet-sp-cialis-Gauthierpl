// Fichier: Script unity/Script/MapLoader.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Pour FirstOrDefault

public class MapLoader : MonoBehaviour
{
    [System.Serializable]
    public struct PrefabMapping { public string prefabId; public GameObject prefab; }

    [Header("Configuration")]
    public List<PrefabMapping> placeablePrefabs; // Assigner les prefabs de la carte ici
    public Transform mapContainer; // Assigner un objet vide pour contenir la carte

    private Dictionary<string, GameObject> prefabDictionary = new Dictionary<string, GameObject>();
    // Le point de spawn de la carte ACTUELLEMENT chargée visuellement
    private Vector3 currentMapSpawnPoint = Vector3.zero;
    private PlayerController localPlayerController = null; // Cache la référence au joueur local
    private bool mapIsLoading = false; // Pour éviter chargements concurrents

    void Awake()
    {
        // Initialisation et vérifications robustes
        if (mapContainer == null) { Debug.LogError("[MapLoader] Map Container non assigné !", this); enabled = false; return; }
        if (placeablePrefabs == null || placeablePrefabs.Count == 0) { Debug.LogError("[MapLoader] Liste PlaceablePrefabs vide !", this); enabled = false; return; }

        // Remplir le dictionnaire de prefabs
        prefabDictionary.Clear();
        foreach (var mapping in placeablePrefabs)
        {
            if (!string.IsNullOrEmpty(mapping.prefabId) && mapping.prefab != null)
            {
                if (!prefabDictionary.ContainsKey(mapping.prefabId))
                {
                    prefabDictionary.Add(mapping.prefabId, mapping.prefab);
                }
                else { Debug.LogWarning($"[MapLoader] ID de prefab dupliqué: '{mapping.prefabId}'. Utilisation du premier trouvé.", this); }
            }
            else { Debug.LogWarning("[MapLoader] Mapping de prefab invalide (ID ou prefab manquant).", this); }
        }
        Debug.Log($"[MapLoader] Dictionnaire prefabs initialisé: {prefabDictionary.Count} entrées.");
    }

    void OnEnable()
    {
        Debug.Log("[MapLoader] OnEnable - Abonnement aux événements NetworkManager.");
        NetworkManager.OnMapLoadRequest += HandleMapLoadRequest;
        // On n'a plus besoin de s'abonner à OnPlayerSpawn ou OnExistingPlayers ici, PlayerManager s'en charge.
        // NetworkManager.OnPlayerSpawn += HandleOtherPlayerSpawn; // Peut être gardé pour log
        // NetworkManager.OnExistingPlayers += HandleExistingPlayers; // Peut être gardé pour log
        NetworkManager.OnPlayerRespawnRequest += HandleRespawnRequest;
    }

    void OnDisable()
    {
        Debug.Log("[MapLoader] OnDisable - Désabonnement des événements NetworkManager.");
        NetworkManager.OnMapLoadRequest -= HandleMapLoadRequest;
        // NetworkManager.OnPlayerSpawn -= HandleOtherPlayerSpawn;
        // NetworkManager.OnExistingPlayers -= HandleExistingPlayers;
        NetworkManager.OnPlayerRespawnRequest -= HandleRespawnRequest;
    }

    // Gère la demande de chargement de carte reçue du serveur
    private void HandleMapLoadRequest(string mapJson)
    {
        if (mapIsLoading)
        {
            Debug.LogWarning("[MapLoader] Demande de chargement de carte ignorée (une autre est en cours).");
            return;
        }
        mapIsLoading = true;
        Debug.Log("[MapLoader] Demande de chargement de carte reçue.");
        ClearCurrentMap(); // Nettoie l'ancienne carte
        if (string.IsNullOrEmpty(mapJson)) { Debug.LogError("[MapLoader] JSON de carte reçu vide !"); mapIsLoading = false; return; }

        MapDefinition mapDefinition = null;
        try
        {
            mapDefinition = JsonUtility.FromJson<MapDefinition>(mapJson);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MapLoader] Erreur parsing JSON carte: {e.Message}\nJSON: {mapJson}");
            mapIsLoading = false;
            return;
        }

        if (mapDefinition == null || mapDefinition.objects == null)
        {
            Debug.LogError("[MapLoader] mapDefinition ou mapDefinition.objects est null après parsing JSON.");
            mapIsLoading = false;
            return;
        }

        Debug.Log($"[MapLoader] Chargement de {mapDefinition.objects.Count} objets...");
        bool spawnFound = false;
        Vector3 foundSpawnPoint = Vector3.zero; // Pour stocker le spawn de cette carte

        // Instancier les objets de la carte
        foreach (var objData in mapDefinition.objects)
        {
            if (prefabDictionary.TryGetValue(objData.prefabId, out GameObject prefabToInstantiate))
            {
                // Force Z=0 pour être sûr en 2D ? Optionnel. Gardons la valeur Z du JSON.
                Vector3 instantiationPos = objData.position;
                // instantiationPos.z = 0;

                GameObject instantiatedObject = Instantiate(prefabToInstantiate, instantiationPos, Quaternion.Euler(0, 0, objData.rotationZ), mapContainer);
                // Appliquer l'échelle du JSON
                instantiatedObject.transform.localScale = new Vector3(objData.scale.x, objData.scale.y, prefabToInstantiate.transform.localScale.z);

                // Cas spécial pour le point de spawn
                if (objData.prefabId == "cat_spawn")
                {
                    foundSpawnPoint = objData.position; // Utilise la position exacte du prefab 'cat_spawn'
                    spawnFound = true;
                    Debug.Log($"[MapLoader] Point de spawn 'cat_spawn' trouvé dans la carte à {foundSpawnPoint}");
                    // Désactiver le rendu visuel du point de spawn
                    SpriteRenderer sr = instantiatedObject.GetComponent<SpriteRenderer>();
                    if (sr != null) { sr.enabled = false; }
                    else { Debug.LogWarning("[MapLoader] Prefab 'cat_spawn' n'a pas de SpriteRenderer à désactiver."); }
                    // Peut-être aussi désactiver son collider ?
                    // Collider2D col = instantiatedObject.GetComponent<Collider2D>();
                    // if (col != null) col.enabled = false;
                }
            }
            else { Debug.LogWarning($"[MapLoader] Prefab non trouvé pour ID '{objData.prefabId}'."); }
        }

        // Mémoriser le point de spawn trouvé DANS CETTE CARTE
        currentMapSpawnPoint = spawnFound ? foundSpawnPoint : Vector3.zero;
        if (!spawnFound) { Debug.LogWarning("[MapLoader] Aucun 'cat_spawn' trouvé dans cette carte. Le spawn par défaut sera (0,0,0) si le serveur ne spécifie rien."); }

        Debug.Log("[MapLoader] Instanciation carte terminée.");
        mapIsLoading = false;

        // NOTE IMPORTANTE: On ne déplace PAS le joueur ici.
        // Le déplacement initial ou après changement de carte est géré par le serveur
        // qui envoie un événement 'respawnPlayer' après que le client ait signalé 'playerReady'.
    }

    // Nettoie les objets de la carte précédente
    private void ClearCurrentMap()
    {
        Debug.Log("[MapLoader] Nettoyage carte précédente...");
        if (mapContainer == null) return;
        // Détruit tous les enfants du conteneur de carte
        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(mapContainer.GetChild(i).gameObject);
        }
        Debug.Log("[MapLoader] Nettoyage terminé.");
    }

    // Gère la demande de respawn reçue du serveur pour NOTRE joueur local
    private void HandleRespawnRequest(Vector3 serverSpawnPoint)
    {
        Debug.Log($"[MapLoader] HandleRespawnRequest appelé avec serverSpawnPoint: {serverSpawnPoint}");
        // Utilise directement le point de spawn fourni par le serveur
        RespawnLocalPlayer(serverSpawnPoint);
    }

    // Tente de trouver et de déplacer le joueur local
    private void RespawnLocalPlayer(Vector3 targetSpawnPoint)
    {
        Debug.Log($"[MapLoader] RespawnLocalPlayer appelé. Tentative de recherche du joueur local ID: {PlayerData.id}");

        // Essayer de trouver le PlayerController local (la référence peut être nulle au début)
        if (localPlayerController == null)
        {
            FindLocalPlayerController();
        }

        // Vérifier si on l'a trouvé
        if (localPlayerController != null)
        {
            Debug.Log($"[MapLoader] Joueur local trouvé ({localPlayerController.playerId}). Déplacement vers le spawn: {targetSpawnPoint}");
            // Appeler la méthode de respawn sur le PlayerController trouvé
            localPlayerController.ReviveAndMoveToSpawn(targetSpawnPoint);
        }
        else
        {
            // Si le joueur local n'est toujours pas trouvé, c'est probablement que PlayerManager ne l'a pas encore instancié.
            // Loguer une erreur claire. La correction principale est dans l'ordre d'instanciation (NetworkManager).
            Debug.LogError($"[MapLoader] ERREUR CRITIQUE: Tentative de respawn mais PlayerController local (ID: {PlayerData.id}) INTROUVABLE. Le joueur local n'a pas été correctement instancié par PlayerManager AVANT la demande de respawn.");
            // On pourrait tenter une nouvelle recherche après un délai, mais c'est un contournement, pas une solution.
            // Invoke(nameof(RetryRespawn), 0.5f, targetSpawnPoint); // A éviter si possible
        }
    }

    // Trouve la référence au PlayerController local
    private void FindLocalPlayerController()
    {
        localPlayerController = null; // Réinitialiser

        if (string.IsNullOrEmpty(PlayerData.id))
        {
            Debug.LogWarning("[MapLoader] PlayerData.id est vide, impossible de trouver le joueur local.");
            return;
        }

        // Méthode préférée : via PlayerManager
        if (PlayerManager.Instance != null)
        {
            localPlayerController = PlayerManager.Instance.GetPlayerController(PlayerData.id);
            if (localPlayerController != null)
            {
                Debug.Log("[MapLoader] Réf PlayerController local trouvée via PlayerManager.");
                return; // Trouvé !
            }
            else
            {
                // Ce n'est pas forcément une erreur ici, PlayerManager n'a peut-être pas encore traité l'instanciation
                Debug.LogWarning($"[MapLoader] PlayerManager ne trouve pas (encore?) de joueur avec ID {PlayerData.id}.");
            }
        }
        else
        {
            Debug.LogWarning("[MapLoader] PlayerManager.Instance est null lors de FindLocalPlayerController.");
        }

        // Méthode Fallback (moins fiable) : chercher dans toute la scène
        Debug.LogWarning("[MapLoader] Fallback: Recherche du PlayerController local via FindObjectsByType.");
        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        // Cherche le bon ID ET qui est marqué comme local
        localPlayerController = allPlayers.FirstOrDefault(p => p.playerId == PlayerData.id && p.IsLocalPlayer);

        if (localPlayerController != null)
        {
            Debug.Log("[MapLoader] Réf PlayerController local trouvée via FindObjectsByType (Fallback).");
        }
        else
        {
            Debug.LogWarning($"[MapLoader] Fallback FindObjectsByType n'a pas trouvé de PlayerController local actif avec ID {PlayerData.id}.");
        }
    }

    // --- Logs pour les autres joueurs (Optionnel) ---
    /*
    private void HandleOtherPlayerSpawn(string id, string pseudo) // Ou signature avec Vector3 pos
    {
        Debug.Log($"[MapLoader] Notification: Autre joueur ({pseudo}, {id}) devrait être spawn par PlayerManager.");
    }

    private void HandleExistingPlayers(List<NetworkManager.PlayerInfo> existingPlayers) // Ou List<PlayerInfoWithSpawn>
    {
        Debug.Log($"[MapLoader] Notification: Traitement de {existingPlayers.Count} joueurs existants par PlayerManager.");
    }
    */
}