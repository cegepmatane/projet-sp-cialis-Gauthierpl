// Fichier: Script unity/Script/Editeur/PlacableObject.cs
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))] // Assure qu'il y a toujours un BoxCollider2D
public class PlaceableObject : MonoBehaviour
{
    [Tooltip("Identifiant unique pour ce type de prefab (doit correspondre dans l'EditorManager)")]
    public string prefabId; // Ex: "wood_floor", "ice_floor", "trampoline", "cat_tree", "cat_spawn", "mouse_goal"

    [HideInInspector] public bool isSelected = false;

    // Références aux composants principaux pour la taille
    private BoxCollider2D objectCollider;
    private SpriteRenderer objectSpriteRenderer;

    // Référence au highlight si cet objet est sélectionné
    private GameObject currentHighlight = null;
    private Transform highlightTransform = null; // Cache le transform pour l'optimisation

    void Awake()
    {
        // Récupérer les composants une seule fois
        objectCollider = GetComponent<BoxCollider2D>();
        objectSpriteRenderer = GetComponent<SpriteRenderer>(); // Peut être null
    }

    public MapObjectData GetData()
    {
        return new MapObjectData
        {
            prefabId = this.prefabId,
            position = transform.position,
            rotationZ = transform.eulerAngles.z,
            // On enregistre le localScale. La taille réelle dépendra de ce scale ET de la taille de base du prefab lors du chargement.
            scale = new Vector2(transform.localScale.x, transform.localScale.y)
        };
    }

    // Appelé par EditorManager lors de la sélection/désélection
    public void SetSelected(bool selected, GameObject highlightPrefab)
    {
        isSelected = selected;
        if (selected)
        {
            if (currentHighlight == null && highlightPrefab != null)
            {
                // Instancier comme enfant de cet objet
                currentHighlight = Instantiate(highlightPrefab, transform); // Position/Rotation locales à zero par défaut
                highlightTransform = currentHighlight.transform; // Cache le transform

                // Ajuster la position locale Z du highlight pour qu'il soit légèrement devant l'objet (facultatif)
                highlightTransform.localPosition = new Vector3(0, 0, -0.1f);
            }

            if (currentHighlight != null)
            {
                currentHighlight.SetActive(true);
                UpdateHighlightSizeAndPosition(); // Mettre à jour taille et position
            }
        }
        else
        {
            if (currentHighlight != null)
            {
                currentHighlight.SetActive(false); // Désactiver au lieu de détruire
            }
        }
    }

    // Met à jour la taille ET la position/rotation locale du highlight
    public void UpdateHighlightSizeAndPosition()
    {
        if (currentHighlight == null || highlightTransform == null) return;

        // --- Déterminer la taille LOCALE de base de l'objet ---
        Vector2 objectLocalSize = Vector2.one; // Taille par défaut

        if (objectCollider != null)
        {
            // La taille du BoxCollider2D est déjà en espace local et non affectée par le scale du transform parent
            objectLocalSize = objectCollider.size;

            // Le centre du BoxCollider peut avoir un offset, on l'utilise pour la position locale du highlight
            highlightTransform.localPosition = new Vector3(objectCollider.offset.x, objectCollider.offset.y, highlightTransform.localPosition.z); // Garde le Z
        }
        else if (objectSpriteRenderer != null && objectSpriteRenderer.sprite != null)
        {
            // Utiliser la taille du sprite lui-même (non affectée par le scale du transform)
            // Attention: sprite.bounds.size est en unités locales basées sur Pixels Per Unit
            objectLocalSize = objectSpriteRenderer.sprite.bounds.size;
            // Le sprite est généralement centré, donc offset local à (0,0)
            highlightTransform.localPosition = new Vector3(0, 0, highlightTransform.localPosition.z); // Garde le Z
        }
        // else { // Si ni collider ni sprite, on garde objectLocalSize = Vector2.one }


        // --- Appliquer la taille au highlight ---
        // Le highlight (enfant) aura son localScale multiplié par le localScale du parent (cet objet).
        // Donc, le localScale du highlight doit être la taille locale de base de l'objet.
        highlightTransform.localScale = new Vector3(objectLocalSize.x, objectLocalSize.y, 1f);

        // --- Rotation ---
        // La rotation locale du highlight doit être nulle, car il hérite déjà de la rotation du parent.
        highlightTransform.localRotation = Quaternion.identity;

    }

    // Appelé par PropertiesPanel ou EditorManager lors de la mise à jour via UI ou Drag
    public void UpdateTransform(Vector2 pos, float rotZ, Vector2 scale)
    {
        transform.position = new Vector3(pos.x, pos.y, transform.position.z); // Conserve le Z original
        transform.eulerAngles = new Vector3(0, 0, rotZ);
        transform.localScale = new Vector3(scale.x, scale.y, transform.localScale.z); // Conserve le Z original

        // Mettre à jour le highlight si sélectionné
        if (isSelected)
        {
            UpdateHighlightSizeAndPosition();
        }
    }

    // Appelé quand l'échelle est modifiée dans l'inspecteur Unity pendant l'exécution (utile pour le débogage)
    void OnValidate()
    {
        // Utiliser UnityEditor.EditorApplication.isPlaying pour éviter les erreurs hors mode Play
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying && isSelected)
        {
            // Léger délai pour s'assurer que les changements sont pris en compte
            UnityEditor.EditorApplication.delayCall += UpdateHighlightSizeAndPosition;
        }
#endif
    }

    // S'assurer que le highlight est détruit si l'objet l'est
    void OnDestroy()
    {
        if (currentHighlight != null)
        {
            Destroy(currentHighlight);
        }
    }
}