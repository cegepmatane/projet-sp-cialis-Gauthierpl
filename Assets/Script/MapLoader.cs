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
    // <<< SUPPRESSION: currentMapSpawnPoint n'est plus utilis� ici pour le respawn >>>
    // private Vector3 currentMapSpawnPoint = Vector3.zero;
    // <<< SUPPRESSION: localPlayerController n'est plus g�r� ici >>>
    // private PlayerController localPlayerController = null;
    private bool mapIsLoading = false;

    void Awake()
    {
        if (mapContainer == null) { Debug.LogError("[MapLoader] Map Container non assign� !", this); enabled = false; return; }
        if (placeablePrefabs == null || placeablePrefabs.Count == 0) { Debug.LogError("[MapLoader] Liste PlaceablePrefabs vide !", this); enabled = false; return; }

        prefabDictionary.Clear();
        foreach (var mapping in placeablePrefabs)
        {
            if (!string.IsNullOrEmpty(mapping.prefabId) && mapping.prefab != null)
            {
                if (!prefabDictionary.ContainsKey(mapping.prefabId)) { prefabDictionary.Add(mapping.prefabId, mapping.prefab); }
                else { Debug.LogWarning($"[MapLoader] ID de prefab dupliqu�: '{mapping.prefabId}'.", this); }
            }
            else { Debug.LogWarning("[MapLoader] Mapping de prefab invalide.", this); }
        }
        Debug.Log($"[MapLoader] Dictionnaire prefabs initialis�: {prefabDictionary.Count} entr�es.");
    }

    void OnEnable()
    {
        Debug.Log("[MapLoader] OnEnable - Abonnement aux �v�nements NetworkManager.");
        NetworkManager.OnMapLoadRequest += HandleMapLoadRequest;
        // <<< SUPPRESSION: Ne s'abonne plus � OnPlayerRespawnRequest >>>
        // NetworkManager.OnPlayerRespawnRequest += HandleRespawnRequest;
    }

    void OnDisable()
    {
        Debug.Log("[MapLoader] OnDisable - D�sabonnement des �v�nements NetworkManager.");
        NetworkManager.OnMapLoadRequest -= HandleMapLoadRequest;
        // <<< SUPPRESSION: Ne se d�sabonne plus de OnPlayerRespawnRequest >>>
        // NetworkManager.OnPlayerRespawnRequest -= HandleRespawnRequest;
    }

    // G�re la demande de chargement de carte re�ue du serveur
    private void HandleMapLoadRequest(string mapJson)
    {
        if (mapIsLoading) { Debug.LogWarning("[MapLoader] Demande de chargement de carte ignor�e (une autre est en cours)."); return; }
        mapIsLoading = true;
        Debug.Log("[MapLoader] Demande de chargement de carte re�ue.");
        ClearCurrentMap();
        if (string.IsNullOrEmpty(mapJson)) { Debug.LogError("[MapLoader] JSON de carte re�u vide !"); mapIsLoading = false; return; }

        MapDefinition mapDefinition = null;
        try { mapDefinition = JsonUtility.FromJson<MapDefinition>(mapJson); }
        catch (System.Exception e) { Debug.LogError($"[MapLoader] Erreur parsing JSON carte: {e.Message}\nJSON: {mapJson}"); mapIsLoading = false; return; }

        if (mapDefinition == null || mapDefinition.objects == null) { Debug.LogError("[MapLoader] mapDefinition ou mapDefinition.objects est null apr�s parsing JSON."); mapIsLoading = false; return; }

        Debug.Log($"[MapLoader] Chargement de {mapDefinition.objects.Count} objets...");
        // <<< SUPPRESSION: La recherche de spawn ici n'est plus n�cessaire pour le respawn client >>>
        // bool spawnFound = false;
        // Vector3 foundSpawnPoint = Vector3.zero;

        foreach (var objData in mapDefinition.objects)
        {
            if (prefabDictionary.TryGetValue(objData.prefabId, out GameObject prefabToInstantiate))
            {
                Vector3 instantiationPos = objData.position;
                GameObject instantiatedObject = Instantiate(prefabToInstantiate, instantiationPos, Quaternion.Euler(0, 0, objData.rotationZ), mapContainer);
                instantiatedObject.transform.localScale = new Vector3(objData.scale.x, objData.scale.y, prefabToInstantiate.transform.localScale.z);

                // D�sactiver le rendu du point de spawn (gard� pour info visuelle �diteur/debug)
                if (objData.prefabId == "cat_spawn")
                {
                    // <<< MODIFICATION: Juste d�sactiver le rendu, pas stocker la position ici >>>
                    Debug.Log($"[MapLoader] Point de spawn 'cat_spawn' trouv� dans la carte � {objData.position}. Rendu d�sactiv�.");
                    SpriteRenderer sr = instantiatedObject.GetComponent<SpriteRenderer>();
                    if (sr != null) { sr.enabled = false; }
                    else { Debug.LogWarning("[MapLoader] Prefab 'cat_spawn' n'a pas de SpriteRenderer."); }
                    // D�sactiver aussi le collider pour �viter interactions
                    Collider2D col = instantiatedObject.GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                }
            }
            else { Debug.LogWarning($"[MapLoader] Prefab non trouv� pour ID '{objData.prefabId}'."); }
        }

        // <<< SUPPRESSION: M�morisation spawn local >>>
        // currentMapSpawnPoint = spawnFound ? foundSpawnPoint : Vector3.zero;
        // if (!spawnFound) { Debug.LogWarning("[MapLoader] Aucun 'cat_spawn' trouv� dans cette carte."); }

        Debug.Log("[MapLoader] Instanciation carte termin�e.");
        mapIsLoading = false;

        // Le spawn/respawn est maintenant enti�rement g�r� par NetworkManager/PlayerManager via l'event 'spawnPlayer'
    }

    // Nettoie les objets de la carte pr�c�dente (Inchang�)
    private void ClearCurrentMap()
    {
        Debug.Log("[MapLoader] Nettoyage carte pr�c�dente...");
        if (mapContainer == null) return;
        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(mapContainer.GetChild(i).gameObject);
        }
        Debug.Log("[MapLoader] Nettoyage termin�.");
    }

    // --- M�thodes de Respawn (SUPPRIM�ES) ---
    // private void HandleRespawnRequest(Vector3 serverSpawnPoint) { ... }
    // private void RespawnLocalPlayer(Vector3 targetSpawnPoint) { ... }
    // private void FindLocalPlayerController() { ... }

}