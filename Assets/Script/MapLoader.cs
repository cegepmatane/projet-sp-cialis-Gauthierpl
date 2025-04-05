// Fichier: Script unity/Script/MapLoader.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MapLoader : MonoBehaviour
{
    [System.Serializable]
    public struct PrefabMapping { public string prefabId; public GameObject prefab; }

    [Header("Configuration")]
    public List<PrefabMapping> placeablePrefabs;
    public Transform mapContainer;

    private Dictionary<string, GameObject> prefabDictionary = new Dictionary<string, GameObject>();
    // <<< SUPPRESSION: currentMapSpawnPoint n'est plus utilisé ici pour le respawn >>>
    // private Vector3 currentMapSpawnPoint = Vector3.zero;
    // <<< SUPPRESSION: localPlayerController n'est plus géré ici >>>
    // private PlayerController localPlayerController = null;
    private bool mapIsLoading = false;

    void Awake()
    {
        if (mapContainer == null) { Debug.LogError("[MapLoader] Map Container non assigné !", this); enabled = false; return; }
        if (placeablePrefabs == null || placeablePrefabs.Count == 0) { Debug.LogError("[MapLoader] Liste PlaceablePrefabs vide !", this); enabled = false; return; }

        prefabDictionary.Clear();
        foreach (var mapping in placeablePrefabs)
        {
            if (!string.IsNullOrEmpty(mapping.prefabId) && mapping.prefab != null)
            {
                if (!prefabDictionary.ContainsKey(mapping.prefabId)) { prefabDictionary.Add(mapping.prefabId, mapping.prefab); }
                else { Debug.LogWarning($"[MapLoader] ID de prefab dupliqué: '{mapping.prefabId}'.", this); }
            }
            else { Debug.LogWarning("[MapLoader] Mapping de prefab invalide.", this); }
        }
        Debug.Log($"[MapLoader] Dictionnaire prefabs initialisé: {prefabDictionary.Count} entrées.");
    }

    void OnEnable()
    {
        Debug.Log("[MapLoader] OnEnable - Abonnement aux événements NetworkManager.");
        NetworkManager.OnMapLoadRequest += HandleMapLoadRequest;
        // <<< SUPPRESSION: Ne s'abonne plus à OnPlayerRespawnRequest >>>
        // NetworkManager.OnPlayerRespawnRequest += HandleRespawnRequest;
    }

    void OnDisable()
    {
        Debug.Log("[MapLoader] OnDisable - Désabonnement des événements NetworkManager.");
        NetworkManager.OnMapLoadRequest -= HandleMapLoadRequest;
        // <<< SUPPRESSION: Ne se désabonne plus de OnPlayerRespawnRequest >>>
        // NetworkManager.OnPlayerRespawnRequest -= HandleRespawnRequest;
    }

    // Gère la demande de chargement de carte reçue du serveur
    private void HandleMapLoadRequest(string mapJson)
    {
        if (mapIsLoading) { Debug.LogWarning("[MapLoader] Demande de chargement de carte ignorée (une autre est en cours)."); return; }
        mapIsLoading = true;
        Debug.Log("[MapLoader] Demande de chargement de carte reçue.");
        ClearCurrentMap();
        if (string.IsNullOrEmpty(mapJson)) { Debug.LogError("[MapLoader] JSON de carte reçu vide !"); mapIsLoading = false; return; }

        MapDefinition mapDefinition = null;
        try { mapDefinition = JsonUtility.FromJson<MapDefinition>(mapJson); }
        catch (System.Exception e) { Debug.LogError($"[MapLoader] Erreur parsing JSON carte: {e.Message}\nJSON: {mapJson}"); mapIsLoading = false; return; }

        if (mapDefinition == null || mapDefinition.objects == null) { Debug.LogError("[MapLoader] mapDefinition ou mapDefinition.objects est null après parsing JSON."); mapIsLoading = false; return; }

        Debug.Log($"[MapLoader] Chargement de {mapDefinition.objects.Count} objets...");
        // <<< SUPPRESSION: La recherche de spawn ici n'est plus nécessaire pour le respawn client >>>
        // bool spawnFound = false;
        // Vector3 foundSpawnPoint = Vector3.zero;

        foreach (var objData in mapDefinition.objects)
        {
            if (prefabDictionary.TryGetValue(objData.prefabId, out GameObject prefabToInstantiate))
            {
                Vector3 instantiationPos = objData.position;
                GameObject instantiatedObject = Instantiate(prefabToInstantiate, instantiationPos, Quaternion.Euler(0, 0, objData.rotationZ), mapContainer);
                instantiatedObject.transform.localScale = new Vector3(objData.scale.x, objData.scale.y, prefabToInstantiate.transform.localScale.z);

                // Désactiver le rendu du point de spawn (gardé pour info visuelle éditeur/debug)
                if (objData.prefabId == "cat_spawn")
                {
                    // <<< MODIFICATION: Juste désactiver le rendu, pas stocker la position ici >>>
                    Debug.Log($"[MapLoader] Point de spawn 'cat_spawn' trouvé dans la carte à {objData.position}. Rendu désactivé.");
                    SpriteRenderer sr = instantiatedObject.GetComponent<SpriteRenderer>();
                    if (sr != null) { sr.enabled = false; }
                    else { Debug.LogWarning("[MapLoader] Prefab 'cat_spawn' n'a pas de SpriteRenderer."); }
                    // Désactiver aussi le collider pour éviter interactions
                    Collider2D col = instantiatedObject.GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                }
            }
            else { Debug.LogWarning($"[MapLoader] Prefab non trouvé pour ID '{objData.prefabId}'."); }
        }

        // <<< SUPPRESSION: Mémorisation spawn local >>>
        // currentMapSpawnPoint = spawnFound ? foundSpawnPoint : Vector3.zero;
        // if (!spawnFound) { Debug.LogWarning("[MapLoader] Aucun 'cat_spawn' trouvé dans cette carte."); }

        Debug.Log("[MapLoader] Instanciation carte terminée.");
        mapIsLoading = false;

        // Le spawn/respawn est maintenant entièrement géré par NetworkManager/PlayerManager via l'event 'spawnPlayer'
    }

    // Nettoie les objets de la carte précédente (Inchangé)
    private void ClearCurrentMap()
    {
        Debug.Log("[MapLoader] Nettoyage carte précédente...");
        if (mapContainer == null) return;
        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(mapContainer.GetChild(i).gameObject);
        }
        Debug.Log("[MapLoader] Nettoyage terminé.");
    }

    // --- Méthodes de Respawn (SUPPRIMÉES) ---
    // private void HandleRespawnRequest(Vector3 serverSpawnPoint) { ... }
    // private void RespawnLocalPlayer(Vector3 targetSpawnPoint) { ... }
    // private void FindLocalPlayerController() { ... }

}