// Fichier: Script unity/Script/Editeur/EditorManager.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro; // Ajout pour le message d'erreur
using System.Linq; // Ajout pour utiliser Contains sur la liste

public class EditorManager : MonoBehaviour
{
    [Header("R�f�rences UI")]
    public GameObject propertiesPanelUI;
    public PropertiesPanel propertiesPanelScript;
    public List<GameObject> uiPanelsToIgnoreHover;
    public TextMeshProUGUI statusText; // R�utiliser ou ajouter un TMP pour les erreurs de validation

    [Header("Prefabs")]
    public GameObject woodFloorPrefab;
    public GameObject iceFloorPrefab;
    public GameObject trampolinePrefab;
    public GameObject catTreePrefab;
    public GameObject catSpawnPrefab;
    public GameObject mouseGoalPrefab;
    public GameObject highlightPrefab;

    [Header("Param�tres")]
    public float rotationSpeed = 5f;
    public string mainMenuSceneName = "MainMenu";
    [Tooltip("PrefabId requis pour la validation (au moins 1 de chaque)")]
    public string requiredCatSpawnId = "cat_spawn";
    public string requiredMouseGoalId = "mouse_goal";
    public string requiredCatTreeId = "cat_tree";
    [Tooltip("Liste des prefab IDs qui ne doivent PAS afficher le panneau de propri�t�s")]
    public List<string> idsToHidePropertiesPanel = new List<string> { "cat_spawn", "mouse_goal", "cat_tree" };


    private GameObject currentPlacingPrefab = null;
    private GameObject ghostPreviewInstance = null;
    private PlaceableObject selectedObject = null;
    private Camera mainCamera;
    private bool isMouseOverUI = false;

    // --- Variables pour le Drag & Drop ---
    private bool isDraggingObject = false;
    private Vector3 dragOffset = Vector3.zero;
    // ------------------------------------

    void Start()
    {
        mainCamera = Camera.main;
        if (propertiesPanelScript == null && propertiesPanelUI != null)
        {
            propertiesPanelScript = propertiesPanelUI.GetComponent<PropertiesPanel>();
        }
        if (propertiesPanelUI != null) propertiesPanelUI.SetActive(false);
        if (statusText) statusText.text = ""; // Vider le statut au d�marrage

        // Assure-toi que les IDs par d�faut sont bien dans la liste si tu veux les utiliser directement
        // (Tu peux aussi remplir cette liste directement dans l'inspecteur Unity)
        // if (idsToHidePropertiesPanel.Count == 0) {
        //     idsToHidePropertiesPanel.Add(requiredCatSpawnId);
        //     idsToHidePropertiesPanel.Add(requiredMouseGoalId);
        //     idsToHidePropertiesPanel.Add(requiredCatTreeId);
        // }
    }

    void Update()
    {
        // V�rifier si la souris est sur l'UI *avant* de g�rer les clics
        isMouseOverUI = EventSystem.current.IsPointerOverGameObject();

        // --- Gestion du Rel�chement Souris (Prioritaire pour arr�ter le drag) ---
        if (Input.GetMouseButtonUp(0))
        {
            if (isDraggingObject)
            {
                StopDragging(); // Arr�ter le drag si en cours, PEU IMPORTE si on est sur l'UI ou non
            }
        }

        // Priorit� au Drag & Drop s'il est en cours
        if (isDraggingObject)
        {
            HandleObjectDragging();
        }
        else // Si on ne drague PAS
        {
            HandleGhostPreview();
            HandleMouseInput(); // G�re s�lection et d�but du drag (ne g�re plus le rel�chement)
            HandleMouseWheelRotation();
        }

        // --- AJOUT : Gestion de la touche Supprimer ---
        // V�rifie si la touche Delete est press�e ET qu'un objet est s�lectionn�
        // ET qu'on n'est pas en train d'�crire dans un champ de texte (InputField)
        if (Input.GetKeyDown(KeyCode.Delete) && selectedObject != null)
        {
            // V�rification suppl�mentaire : ne pas supprimer si un InputField est focus
            // (pour �viter de supprimer en voulant effacer du texte dans le panneau de propri�t�s)
            GameObject currentSelectedUI = EventSystem.current.currentSelectedGameObject;
            if (currentSelectedUI == null || currentSelectedUI.GetComponent<TMP_InputField>() == null)
            {
                Debug.Log($"Touche Suppr press�e - Suppression de {selectedObject.name}");
                DeleteSelectedObject();
            }
        }
        // ---------------------------------------------
    }

    // --- Gestion du Placement ---
    public void SelectPrefabToPlace(GameObject prefab)
    {
        if (selectedObject != null) DeselectCurrentObject(); // D�s�lectionne avant de choisir un nouveau prefab

        currentPlacingPrefab = prefab;

        // D�truit l'ancien fant�me s'il existe
        if (ghostPreviewInstance != null) Destroy(ghostPreviewInstance);

        // Cr�e le nouveau fant�me
        if (currentPlacingPrefab != null)
        {
            ghostPreviewInstance = Instantiate(currentPlacingPrefab);
            // Appliquer l'opacit�
            SpriteRenderer sr = ghostPreviewInstance.GetComponentInChildren<SpriteRenderer>(); // Cherche dans les enfants aussi
            if (sr != null)
            {
                Color color = sr.color;
                color.a = 0.5f; // 50% d'opacit�
                sr.color = color;
            }
            // D�sactiver les colliders sur le fant�me pour �viter les interactions
            Collider2D[] colliders = ghostPreviewInstance.GetComponentsInChildren<Collider2D>();
            foreach (var col in colliders) col.enabled = false;

            // Utilise la valeur de isMouseOverUI calcul�e au d�but de Update
            ghostPreviewInstance.SetActive(!isMouseOverUI);
        }
    }

    void HandleGhostPreview()
    {
        if (ghostPreviewInstance != null)
        {
            // Utilise la valeur de isMouseOverUI calcul�e au d�but de Update
            ghostPreviewInstance.SetActive(!isMouseOverUI);

            if (!isMouseOverUI)
            {
                // Faire suivre la souris
                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = 0; // Assurer la position Z en 2D
                ghostPreviewInstance.transform.position = mouseWorldPos;
            }
        }
    }

    void PlaceObject()
    {
        // Utilise la valeur de isMouseOverUI calcul�e au d�but de Update
        if (currentPlacingPrefab != null && !isMouseOverUI)
        {
            Vector3 placePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            placePos.z = 0; // Position Z pour la 2D

            GameObject newObj = Instantiate(currentPlacingPrefab, placePos, ghostPreviewInstance.transform.rotation); // Utilise la rotation du fant�me

            // Ajouter ou r�cup�rer le PlaceableObject (il devrait �tre sur le prefab)
            PlaceableObject po = newObj.GetComponent<PlaceableObject>();
            if (po == null)
            {
                Debug.LogError($"Le prefab {currentPlacingPrefab.name} n'a pas de script PlaceableObject!", newObj);
                Destroy(newObj); // Ne pas placer un objet invalide
                return;
            }

            // Optionnel: Rendre persistant ou annuler le mode placement apr�s avoir cliqu�
            // CancelPlacementMode(); // Si tu veux quitter le mode apr�s un clic
        }
    }

    public void CancelPlacementMode()
    {
        if (ghostPreviewInstance != null)
        {
            Destroy(ghostPreviewInstance);
        }
        currentPlacingPrefab = null;
        ghostPreviewInstance = null;
    }


    // --- Gestion de la S�lection/�dition/Drag ---

    void HandleMouseInput() // Ne g�re plus GetMouseButtonUp
    {
        // --- Clic Gauche ---
        // Utilise la valeur de isMouseOverUI calcul�e au d�but de Update
        if (Input.GetMouseButtonDown(0) && !isMouseOverUI)
        {
            if (currentPlacingPrefab != null)
            {
                PlaceObject(); // Placer si en mode placement
            }
            else
            {
                // Essayer de s�lectionner OU de commencer un drag si on clique sur l'objet d�j� s�lectionn�
                Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

                if (hit.collider != null)
                {
                    PlaceableObject hitObject = hit.collider.GetComponentInParent<PlaceableObject>(); // GetComponentInParent pour les objets complexes
                    if (hitObject != null)
                    {
                        if (hitObject == selectedObject)
                        {
                            // Clic sur l'objet d�j� s�lectionn� -> Commence le Drag & Drop
                            StartDragging(hitObject);
                        }
                        else
                        {
                            // Clic sur un nouvel objet -> S�lectionner
                            SelectObject(hitObject);
                        }
                    }
                    else
                    {
                        // Clic sur autre chose (pas un PlaceableObject) -> D�s�lectionner
                        if (selectedObject != null) DeselectCurrentObject();
                    }
                }
                else
                {
                    // Clic dans le vide -> D�s�lectionner
                    if (selectedObject != null) DeselectCurrentObject();
                }
            }
        }

        // --- Clic Droit ---
        if (Input.GetMouseButtonDown(1))
        {
            if (currentPlacingPrefab != null)
            {
                CancelPlacementMode(); // Annule le placement
            }
            else if (selectedObject != null && !isMouseOverUI) // Ne pas d�s�lectionner si on clique droit sur l'UI
            {
                DeselectCurrentObject(); // D�s�lectionne l'objet
            }
        }
    }

    void StartDragging(PlaceableObject obj)
    {
        if (obj == null) return;
        isDraggingObject = true;
        selectedObject = obj; // Assure qu'il est bien s�lectionn�

        // S'assurer que le panneau de propri�t�s est � jour avant de drag (si visible)
        if (ShouldShowPropertiesPanel(obj) && propertiesPanelScript != null)
        {
            propertiesPanelScript.DisplayObjectProperties(selectedObject);
        }

        // Calculer l'offset entre le centre de l'objet et la position de la souris
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = obj.transform.position.z; // Garder le Z de l'objet
        dragOffset = obj.transform.position - mouseWorldPos;

        // Optionnel: cacher le panneau des propri�t�s pendant le drag?
        // if (propertiesPanelUI != null && propertiesPanelUI.activeSelf) propertiesPanelUI.SetActive(false);
    }

    void HandleObjectDragging()
    {
        if (selectedObject == null)
        {
            isDraggingObject = false; // S�curit�
            return;
        }

        // Mettre � jour la position de l'objet en fonction de la souris + offset
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector3 targetPos = mouseWorldPos + dragOffset;
        targetPos.z = selectedObject.transform.position.z; // Conserver le Z original

        selectedObject.transform.position = targetPos;

        // Mettre � jour l'affichage des propri�t�s en temps r�el pendant le drag (si le panneau est visible pour cet objet)
        if (ShouldShowPropertiesPanel(selectedObject) && propertiesPanelScript != null && propertiesPanelUI.activeSelf)
        {
            propertiesPanelScript.UpdatePositionDisplay(targetPos);
        }
        // Mettre � jour la position/taille du highlight
        if (selectedObject.isSelected) selectedObject.UpdateHighlightSizeAndPosition();
    }

    void StopDragging()
    {
        isDraggingObject = false;
        // L'offset n'est plus n�cessaire
        dragOffset = Vector3.zero;

        // R�afficher et mettre � jour le panneau des propri�t�s si n�cessaire et si autoris� pour cet objet
        if (selectedObject != null)
        {
            if (ShouldShowPropertiesPanel(selectedObject))
            {
                // if (propertiesPanelUI != null) propertiesPanelUI.SetActive(true); // D�j� g�r� dans SelectObject
                if (propertiesPanelScript != null) propertiesPanelScript.DisplayObjectProperties(selectedObject); // MAJ finale
            }
            else
            {
                if (propertiesPanelUI != null) propertiesPanelUI.SetActive(false); // Assure qu'il est cach�
            }
        }
    }

    void SelectObject(PlaceableObject obj)
    {
        if (isDraggingObject) StopDragging(); // Arr�ter le drag si on s�lectionne autre chose
        if (selectedObject == obj) return; // Ne rien faire si on reclique sur le m�me objet (le drag est g�r� ailleurs)

        // D�s�lectionner l'ancien
        if (selectedObject != null)
        {
            selectedObject.SetSelected(false, null);
        }

        // S�lectionner le nouveau
        selectedObject = obj;
        selectedObject.SetSelected(true, highlightPrefab);

        // Afficher le panneau SEULEMENT si l'ID n'est pas dans la liste d'exclusion
        bool showPanel = ShouldShowPropertiesPanel(selectedObject);

        if (showPanel)
        {
            if (propertiesPanelUI != null) propertiesPanelUI.SetActive(true);
            if (propertiesPanelScript != null) propertiesPanelScript.DisplayObjectProperties(selectedObject);
        }
        else
        {
            if (propertiesPanelUI != null) propertiesPanelUI.SetActive(false); // Cacher si on s�lectionne un objet interdit
        }


        CancelPlacementMode(); // Quitte le mode placement si on s�lectionne un objet
    }

    // Helper pour v�rifier si on doit afficher le panneau
    bool ShouldShowPropertiesPanel(PlaceableObject obj)
    {
        if (obj == null || string.IsNullOrEmpty(obj.prefabId)) return false;
        // V�rifie si l'ID de l'objet N'EST PAS dans la liste des IDs � cacher
        return !idsToHidePropertiesPanel.Contains(obj.prefabId);
    }

    void DeselectCurrentObject()
    {
        if (isDraggingObject) StopDragging(); // Arr�ter le drag lors de la d�s�lection

        if (selectedObject != null)
        {
            selectedObject.SetSelected(false, null);
            selectedObject = null;

            // Toujours cacher le panneau lors de la d�s�lection
            if (propertiesPanelUI != null) propertiesPanelUI.SetActive(false);
            if (propertiesPanelScript != null) propertiesPanelScript.ClearProperties();
        }
    }

    void HandleMouseWheelRotation()
    {
        // Ne pas tourner si on drague l'objet ou si la souris est sur l'UI
        if (selectedObject != null && !isMouseOverUI && !isDraggingObject)
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.1f)
            {
                float rotationAmount = -scrollDelta * rotationSpeed; // Inverser si le sens ne convient pas
                selectedObject.transform.Rotate(0, 0, rotationAmount);

                // Mettre � jour l'UI si le panneau est visible pour cet objet
                if (ShouldShowPropertiesPanel(selectedObject) && propertiesPanelScript != null && propertiesPanelUI.activeSelf)
                {
                    propertiesPanelScript.UpdateRotationDisplay(selectedObject.transform.eulerAngles.z);
                }
                // Mettre � jour la rotation du highlight (qui est enfant, donc suit d�j� mais on pourrait avoir besoin de UpdateHighlightSize si la rotation affecte les bounds)
                if (selectedObject.isSelected) selectedObject.UpdateHighlightSizeAndPosition();
            }
        }
        // Rotation du fant�me avant placement
        else if (ghostPreviewInstance != null && !isMouseOverUI)
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.1f)
            {
                float rotationAmount = -scrollDelta * rotationSpeed;
                ghostPreviewInstance.transform.Rotate(0, 0, rotationAmount);
            }
        }
    }


    // --- Boutons UI ---
    public void OnClick_SelectWoodFloor() => SelectPrefabToPlace(woodFloorPrefab);
    public void OnClick_SelectIceFloor() => SelectPrefabToPlace(iceFloorPrefab);
    public void OnClick_SelectTrampoline() => SelectPrefabToPlace(trampolinePrefab);
    public void OnClick_SelectCatTree() => SelectPrefabToPlace(catTreePrefab);
    public void OnClick_SelectCatSpawn() => SelectPrefabToPlace(catSpawnPrefab);
    public void OnClick_SelectMouseGoal() => SelectPrefabToPlace(mouseGoalPrefab);

    public void OnClick_ReturnToMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }


    // --- SAUVEGARDE AVEC VALIDATION ---
    public void OnClick_SaveMap()
    {
        if (!ValidateMap()) // Appel de la validation avant tout
        {
            return; // Arr�ter si la validation �choue
        }

        // Si validation OK, continuer comme avant
        PlaceableObject[] allObjects = FindObjectsOfType<PlaceableObject>();
        MapDefinition mapDef = new MapDefinition();

        foreach (var obj in allObjects)
        {
            mapDef.objects.Add(obj.GetData());
        }

        string json = JsonUtility.ToJson(mapDef, true);
        Debug.Log("Map JSON:\n" + json);

        MapSaver saver = FindObjectOfType<MapSaver>();
        if (saver != null)
        {
            // Effacer le message d'erreur pr�c�dent avant de sauvegarder
            if (statusText) statusText.text = "";
            saver.SaveMapData(json);
        }
        else
        {
            Debug.LogError("MapSaver non trouv� dans la sc�ne ! Impossible d'enregistrer.");
            if (statusText) statusText.text = "Erreur: MapSaver manquant.";
        }
    }

    private bool ValidateMap()
    {
        int catSpawnCount = 0;
        int mouseGoalCount = 0;
        int catTreeCount = 0;

        PlaceableObject[] allObjects = FindObjectsOfType<PlaceableObject>();

        foreach (var obj in allObjects)
        {
            if (obj == null || string.IsNullOrEmpty(obj.prefabId)) continue; // S�curit�

            if (obj.prefabId == requiredCatSpawnId) catSpawnCount++;
            if (obj.prefabId == requiredMouseGoalId) mouseGoalCount++;
            if (obj.prefabId == requiredCatTreeId) catTreeCount++;
        }

        string errorMessage = "";
        bool isValid = true;

        if (catSpawnCount == 0)
        {
            errorMessage += $"- Il manque un point de spawn pour chat ('{requiredCatSpawnId}').\n";
            isValid = false;
        }
        else if (catSpawnCount > 1)
        {
            errorMessage += "- Il ne peut y avoir qu'un seul point de spawn pour chat.\n";
            isValid = false;
        }

        if (mouseGoalCount == 0)
        {
            errorMessage += $"- Il manque au moins un objectif Souris ('{requiredMouseGoalId}').\n";
            isValid = false;
        }

        if (catTreeCount == 0)
        {
            errorMessage += $"- Il manque au moins un objectif Arbre � Chat ('{requiredCatTreeId}').\n";
            isValid = false;
        }

        if (!isValid)
        {
            Debug.LogError("Validation de la carte �chou�e:\n" + errorMessage);
            if (statusText) statusText.text = "Erreur Validation:\n" + errorMessage;
        }
        else
        {
            if (statusText) statusText.text = "Validation OK"; // Message de succ�s optionnel
        }

        return isValid;
    }

    // --- Mise � jour depuis l'UI ---
    public void UpdateSelectedObjectTransformFromUI(Vector2 pos, float rotZ, Vector2 size)
    {
        // Ne pas mettre � jour depuis l'UI si on drague (pour �viter conflit)
        // Ou si l'objet n'est pas cens� afficher le panneau
        if (selectedObject != null && !isDraggingObject && ShouldShowPropertiesPanel(selectedObject))
        {
            selectedObject.UpdateTransform(pos, rotZ, size);
        }
    }

    // --- Suppression d'objet ---
    public void DeleteSelectedObject()
    {
        if (selectedObject != null)
        {
            GameObject objectToDelete = selectedObject.gameObject;
            DeselectCurrentObject(); // D�s�lectionne d'abord (cache panel, enl�ve highlight)
            Destroy(objectToDelete); // Puis d�truit l'objet
        }
    }

    // Peut �tre appel� par un bouton "Supprimer" dans l'UI ou associ� � la touche Suppr dans Update
    // Exemple pour la touche Suppr:
    // void Update() {
    //     ...
    //     if (Input.GetKeyDown(KeyCode.Delete) && selectedObject != null) {
    //         DeleteSelectedObject();
    //     }
    //     ...
    // }

}