// Fichier: Script unity/Script/Editeur/PlacableObject.cs
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))] // Assure qu'il y a toujours un BoxCollider2D
public class PlaceableObject : MonoBehaviour
{
    [Tooltip("Identifiant unique pour ce type de prefab (doit correspondre dans l'EditorManager)")]
    public string prefabId; // Ex: "wood_floor", "ice_floor", "trampoline", "cat_tree", "cat_spawn", "mouse_goal"

    [HideInInspector] public bool isSelected = false;

    // R�f�rences aux composants principaux pour la taille
    private BoxCollider2D objectCollider;
    private SpriteRenderer objectSpriteRenderer;

    // R�f�rence au highlight si cet objet est s�lectionn�
    private GameObject currentHighlight = null;
    private Transform highlightTransform = null; // Cache le transform pour l'optimisation

    void Awake()
    {
        // R�cup�rer les composants une seule fois
        objectCollider = GetComponent<BoxCollider2D>();
        objectSpriteRenderer = GetComponent<SpriteRenderer>(); // Peut �tre null
    }

    public MapObjectData GetData()
    {
        return new MapObjectData
        {
            prefabId = this.prefabId,
            position = transform.position,
            rotationZ = transform.eulerAngles.z,
            // On enregistre le localScale. La taille r�elle d�pendra de ce scale ET de la taille de base du prefab lors du chargement.
            scale = new Vector2(transform.localScale.x, transform.localScale.y)
        };
    }

    // Appel� par EditorManager lors de la s�lection/d�s�lection
    public void SetSelected(bool selected, GameObject highlightPrefab)
    {
        isSelected = selected;
        if (selected)
        {
            if (currentHighlight == null && highlightPrefab != null)
            {
                // Instancier comme enfant de cet objet
                currentHighlight = Instantiate(highlightPrefab, transform); // Position/Rotation locales � zero par d�faut
                highlightTransform = currentHighlight.transform; // Cache le transform

                // Ajuster la position locale Z du highlight pour qu'il soit l�g�rement devant l'objet (facultatif)
                highlightTransform.localPosition = new Vector3(0, 0, -0.1f);
            }

            if (currentHighlight != null)
            {
                currentHighlight.SetActive(true);
                UpdateHighlightSizeAndPosition(); // Mettre � jour taille et position
            }
        }
        else
        {
            if (currentHighlight != null)
            {
                currentHighlight.SetActive(false); // D�sactiver au lieu de d�truire
            }
        }
    }

    // Met � jour la taille ET la position/rotation locale du highlight
    public void UpdateHighlightSizeAndPosition()
    {
        if (currentHighlight == null || highlightTransform == null) return;

        // --- D�terminer la taille LOCALE de base de l'objet ---
        Vector2 objectLocalSize = Vector2.one; // Taille par d�faut

        if (objectCollider != null)
        {
            // La taille du BoxCollider2D est d�j� en espace local et non affect�e par le scale du transform parent
            objectLocalSize = objectCollider.size;

            // Le centre du BoxCollider peut avoir un offset, on l'utilise pour la position locale du highlight
            highlightTransform.localPosition = new Vector3(objectCollider.offset.x, objectCollider.offset.y, highlightTransform.localPosition.z); // Garde le Z
        }
        else if (objectSpriteRenderer != null && objectSpriteRenderer.sprite != null)
        {
            // Utiliser la taille du sprite lui-m�me (non affect�e par le scale du transform)
            // Attention: sprite.bounds.size est en unit�s locales bas�es sur Pixels Per Unit
            objectLocalSize = objectSpriteRenderer.sprite.bounds.size;
            // Le sprite est g�n�ralement centr�, donc offset local � (0,0)
            highlightTransform.localPosition = new Vector3(0, 0, highlightTransform.localPosition.z); // Garde le Z
        }
        // else { // Si ni collider ni sprite, on garde objectLocalSize = Vector2.one }


        // --- Appliquer la taille au highlight ---
        // Le highlight (enfant) aura son localScale multipli� par le localScale du parent (cet objet).
        // Donc, le localScale du highlight doit �tre la taille locale de base de l'objet.
        highlightTransform.localScale = new Vector3(objectLocalSize.x, objectLocalSize.y, 1f);

        // --- Rotation ---
        // La rotation locale du highlight doit �tre nulle, car il h�rite d�j� de la rotation du parent.
        highlightTransform.localRotation = Quaternion.identity;

    }

    // Appel� par PropertiesPanel ou EditorManager lors de la mise � jour via UI ou Drag
    public void UpdateTransform(Vector2 pos, float rotZ, Vector2 scale)
    {
        transform.position = new Vector3(pos.x, pos.y, transform.position.z); // Conserve le Z original
        transform.eulerAngles = new Vector3(0, 0, rotZ);
        transform.localScale = new Vector3(scale.x, scale.y, transform.localScale.z); // Conserve le Z original

        // Mettre � jour le highlight si s�lectionn�
        if (isSelected)
        {
            UpdateHighlightSizeAndPosition();
        }
    }

    // Appel� quand l'�chelle est modifi�e dans l'inspecteur Unity pendant l'ex�cution (utile pour le d�bogage)
    void OnValidate()
    {
        // Utiliser UnityEditor.EditorApplication.isPlaying pour �viter les erreurs hors mode Play
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying && isSelected)
        {
            // L�ger d�lai pour s'assurer que les changements sont pris en compte
            UnityEditor.EditorApplication.delayCall += UpdateHighlightSizeAndPosition;
        }
#endif
    }

    // S'assurer que le highlight est d�truit si l'objet l'est
    void OnDestroy()
    {
        if (currentHighlight != null)
        {
            Destroy(currentHighlight);
        }
    }
}