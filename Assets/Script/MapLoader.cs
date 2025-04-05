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
    // Le point de spawn de la carte ACTUELLEMENT charg�e visuellement
    private Vector3 currentMapSpawnPoint = Vector3.zero;
    private PlayerController localPlayerController = null; // Cache la r�f�rence au joueur local
    private bool mapIsLoading = false; // Pour �viter chargements concurrents

    void Awake()
    {
        // Initialisation et v�rifications robustes
        if (mapContainer == null) { Debug.LogError("[MapLoader] Map Container non assign� !", this); enabled = false; return; }
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
                else { Debug.LogWarning($"[MapLoader] ID de prefab dupliqu�: '{mapping.prefabId}'. Utilisation du premier trouv�.", this); }
            }
            else { Debug.LogWarning("[MapLoader] Mapping de prefab invalide (ID ou prefab manquant).", this); }
        }
        Debug.Log($"[MapLoader] Dictionnaire prefabs initialis�: {prefabDictionary.Count} entr�es.");
    }

    void OnEnable()
    {
        Debug.Log("[MapLoader] OnEnable - Abonnement aux �v�nements NetworkManager.");
        NetworkManager.OnMapLoadRequest += HandleMapLoadRequest;
        // On n'a plus besoin de s'abonner � OnPlayerSpawn ou OnExistingPlayers ici, PlayerManager s'en charge.
        // NetworkManager.OnPlayerSpawn += HandleOtherPlayerSpawn; // Peut �tre gard� pour log
        // NetworkManager.OnExistingPlayers += HandleExistingPlayers; // Peut �tre gard� pour log
        NetworkManager.OnPlayerRespawnRequest += HandleRespawnRequest;
    }

    void OnDisable()
    {
        Debug.Log("[MapLoader] OnDisable - D�sabonnement des �v�nements NetworkManager.");
        NetworkManager.OnMapLoadRequest -= HandleMapLoadRequest;
        // NetworkManager.OnPlayerSpawn -= HandleOtherPlayerSpawn;
        // NetworkManager.OnExistingPlayers -= HandleExistingPlayers;
        NetworkManager.OnPlayerRespawnRequest -= HandleRespawnRequest;
    }

    // G�re la demande de chargement de carte re�ue du serveur
    private void HandleMapLoadRequest(string mapJson)
    {
        if (mapIsLoading)
        {
            Debug.LogWarning("[MapLoader] Demande de chargement de carte ignor�e (une autre est en cours).");
            return;
        }
        mapIsLoading = true;
        Debug.Log("[MapLoader] Demande de chargement de carte re�ue.");
        ClearCurrentMap(); // Nettoie l'ancienne carte
        if (string.IsNullOrEmpty(mapJson)) { Debug.LogError("[MapLoader] JSON de carte re�u vide !"); mapIsLoading = false; return; }

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
            Debug.LogError("[MapLoader] mapDefinition ou mapDefinition.objects est null apr�s parsing JSON.");
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
                // Force Z=0 pour �tre s�r en 2D ? Optionnel. Gardons la valeur Z du JSON.
                Vector3 instantiationPos = objData.position;
                // instantiationPos.z = 0;

                GameObject instantiatedObject = Instantiate(prefabToInstantiate, instantiationPos, Quaternion.Euler(0, 0, objData.rotationZ), mapContainer);
                // Appliquer l'�chelle du JSON
                instantiatedObject.transform.localScale = new Vector3(objData.scale.x, objData.scale.y, prefabToInstantiate.transform.localScale.z);

                // Cas sp�cial pour le point de spawn
                if (objData.prefabId == "cat_spawn")
                {
                    foundSpawnPoint = objData.position; // Utilise la position exacte du prefab 'cat_spawn'
                    spawnFound = true;
                    Debug.Log($"[MapLoader] Point de spawn 'cat_spawn' trouv� dans la carte � {foundSpawnPoint}");
                    // D�sactiver le rendu visuel du point de spawn
                    SpriteRenderer sr = instantiatedObject.GetComponent<SpriteRenderer>();
                    if (sr != null) { sr.enabled = false; }
                    else { Debug.LogWarning("[MapLoader] Prefab 'cat_spawn' n'a pas de SpriteRenderer � d�sactiver."); }
                    // Peut-�tre aussi d�sactiver son collider ?
                    // Collider2D col = instantiatedObject.GetComponent<Collider2D>();
                    // if (col != null) col.enabled = false;
                }
            }
            else { Debug.LogWarning($"[MapLoader] Prefab non trouv� pour ID '{objData.prefabId}'."); }
        }

        // M�moriser le point de spawn trouv� DANS CETTE CARTE
        currentMapSpawnPoint = spawnFound ? foundSpawnPoint : Vector3.zero;
        if (!spawnFound) { Debug.LogWarning("[MapLoader] Aucun 'cat_spawn' trouv� dans cette carte. Le spawn par d�faut sera (0,0,0) si le serveur ne sp�cifie rien."); }

        Debug.Log("[MapLoader] Instanciation carte termin�e.");
        mapIsLoading = false;

        // NOTE IMPORTANTE: On ne d�place PAS le joueur ici.
        // Le d�placement initial ou apr�s changement de carte est g�r� par le serveur
        // qui envoie un �v�nement 'respawnPlayer' apr�s que le client ait signal� 'playerReady'.
    }

    // Nettoie les objets de la carte pr�c�dente
    private void ClearCurrentMap()
    {
        Debug.Log("[MapLoader] Nettoyage carte pr�c�dente...");
        if (mapContainer == null) return;
        // D�truit tous les enfants du conteneur de carte
        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(mapContainer.GetChild(i).gameObject);
        }
        Debug.Log("[MapLoader] Nettoyage termin�.");
    }

    // G�re la demande de respawn re�ue du serveur pour NOTRE joueur local
    private void HandleRespawnRequest(Vector3 serverSpawnPoint)
    {
        Debug.Log($"[MapLoader] HandleRespawnRequest appel� avec serverSpawnPoint: {serverSpawnPoint}");
        // Utilise directement le point de spawn fourni par le serveur
        RespawnLocalPlayer(serverSpawnPoint);
    }

    // Tente de trouver et de d�placer le joueur local
    private void RespawnLocalPlayer(Vector3 targetSpawnPoint)
    {
        Debug.Log($"[MapLoader] RespawnLocalPlayer appel�. Tentative de recherche du joueur local ID: {PlayerData.id}");

        // Essayer de trouver le PlayerController local (la r�f�rence peut �tre nulle au d�but)
        if (localPlayerController == null)
        {
            FindLocalPlayerController();
        }

        // V�rifier si on l'a trouv�
        if (localPlayerController != null)
        {
            Debug.Log($"[MapLoader] Joueur local trouv� ({localPlayerController.playerId}). D�placement vers le spawn: {targetSpawnPoint}");
            // Appeler la m�thode de respawn sur le PlayerController trouv�
            localPlayerController.ReviveAndMoveToSpawn(targetSpawnPoint);
        }
        else
        {
            // Si le joueur local n'est toujours pas trouv�, c'est probablement que PlayerManager ne l'a pas encore instanci�.
            // Loguer une erreur claire. La correction principale est dans l'ordre d'instanciation (NetworkManager).
            Debug.LogError($"[MapLoader] ERREUR CRITIQUE: Tentative de respawn mais PlayerController local (ID: {PlayerData.id}) INTROUVABLE. Le joueur local n'a pas �t� correctement instanci� par PlayerManager AVANT la demande de respawn.");
            // On pourrait tenter une nouvelle recherche apr�s un d�lai, mais c'est un contournement, pas une solution.
            // Invoke(nameof(RetryRespawn), 0.5f, targetSpawnPoint); // A �viter si possible
        }
    }

    // Trouve la r�f�rence au PlayerController local
    private void FindLocalPlayerController()
    {
        localPlayerController = null; // R�initialiser

        if (string.IsNullOrEmpty(PlayerData.id))
        {
            Debug.LogWarning("[MapLoader] PlayerData.id est vide, impossible de trouver le joueur local.");
            return;
        }

        // M�thode pr�f�r�e : via PlayerManager
        if (PlayerManager.Instance != null)
        {
            localPlayerController = PlayerManager.Instance.GetPlayerController(PlayerData.id);
            if (localPlayerController != null)
            {
                Debug.Log("[MapLoader] R�f PlayerController local trouv�e via PlayerManager.");
                return; // Trouv� !
            }
            else
            {
                // Ce n'est pas forc�ment une erreur ici, PlayerManager n'a peut-�tre pas encore trait� l'instanciation
                Debug.LogWarning($"[MapLoader] PlayerManager ne trouve pas (encore?) de joueur avec ID {PlayerData.id}.");
            }
        }
        else
        {
            Debug.LogWarning("[MapLoader] PlayerManager.Instance est null lors de FindLocalPlayerController.");
        }

        // M�thode Fallback (moins fiable) : chercher dans toute la sc�ne
        Debug.LogWarning("[MapLoader] Fallback: Recherche du PlayerController local via FindObjectsByType.");
        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        // Cherche le bon ID ET qui est marqu� comme local
        localPlayerController = allPlayers.FirstOrDefault(p => p.playerId == PlayerData.id && p.IsLocalPlayer);

        if (localPlayerController != null)
        {
            Debug.Log("[MapLoader] R�f PlayerController local trouv�e via FindObjectsByType (Fallback).");
        }
        else
        {
            Debug.LogWarning($"[MapLoader] Fallback FindObjectsByType n'a pas trouv� de PlayerController local actif avec ID {PlayerData.id}.");
        }
    }

    // --- Logs pour les autres joueurs (Optionnel) ---
    /*
    private void HandleOtherPlayerSpawn(string id, string pseudo) // Ou signature avec Vector3 pos
    {
        Debug.Log($"[MapLoader] Notification: Autre joueur ({pseudo}, {id}) devrait �tre spawn par PlayerManager.");
    }

    private void HandleExistingPlayers(List<NetworkManager.PlayerInfo> existingPlayers) // Ou List<PlayerInfoWithSpawn>
    {
        Debug.Log($"[MapLoader] Notification: Traitement de {existingPlayers.Count} joueurs existants par PlayerManager.");
    }
    */
}