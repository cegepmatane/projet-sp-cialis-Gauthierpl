// Fichier: Script unity/Script/Editeur/DraggablePanel.cs
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePanel : MonoBehaviour, IPointerDownHandler, IDragHandler, IBeginDragHandler
{
    private RectTransform panelRectTransform;
    private Canvas canvas;
    private Vector3 startPositionOffset = Vector3.zero; // Pour stocker la différence entre le pivot et le clic initial

    // Garder une référence au RectTransform du Canvas pour l'optimisation
    private RectTransform canvasRectTransform;


    void Awake()
    {
        panelRectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("DraggablePanel doit être un enfant d'un Canvas.", this);
            enabled = false;
            return;
        }
        canvasRectTransform = canvas.transform as RectTransform;
        if (canvasRectTransform == null)
        {
            Debug.LogError("Le transform du Canvas parent n'est pas un RectTransform.", this);
            enabled = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (canvas == null) return;

        // Calcul de l'offset au début du drag, une seule fois
        Vector3 globalMousePos;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectTransform, eventData.position, eventData.pressEventCamera, out globalMousePos))
        {
            startPositionOffset = panelRectTransform.position - globalMousePos;
        }
        else
        {
            // Fallback si la conversion échoue (peu probable en Screen Space Camera)
            startPositionOffset = Vector3.zero;
        }

        // Mettre le panel au premier plan (facultatif mais recommandé)
        panelRectTransform.SetAsLastSibling();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // On ne fait plus le calcul d'offset ici, seulement SetAsLastSibling si on veut
        // Mettre le panel au premier plan dès le clic
        panelRectTransform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (panelRectTransform == null || canvas == null) return;

        // Utiliser ScreenPointToWorldPointInRectangle pour obtenir la position dans le monde (plus fiable pour Screen Space Camera)
        Vector3 globalMousePos;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectTransform, eventData.position, eventData.pressEventCamera, out globalMousePos))
        {
            // La nouvelle position cible est la position actuelle de la souris + l'offset initial
            panelRectTransform.position = globalMousePos + startPositionOffset;
        }


        // --- Clamp / Contrainte dans l'écran (version simplifiée pour Screen Space) ---
        // Convertir les coins du panel en coordonnées d'écran
        Vector3[] panelCorners = new Vector3[4];
        panelRectTransform.GetWorldCorners(panelCorners); // Coins en coordonnées mondiales

        // Convertir les coins mondiaux en Screen points
        Vector2 bottomLeftScreen = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, panelCorners[0]);
        Vector2 topRightScreen = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, panelCorners[2]);


        Vector3 currentPanelPos = panelRectTransform.position;
        Vector3 adjustment = Vector3.zero;

        // Vérifier les bords gauche/droite (en coordonnées d'écran)
        if (bottomLeftScreen.x < 0)
            adjustment.x = -bottomLeftScreen.x;
        else if (topRightScreen.x > Screen.width)
            adjustment.x = Screen.width - topRightScreen.x;

        // Vérifier les bords haut/bas (en coordonnées d'écran)
        if (bottomLeftScreen.y < 0)
            adjustment.y = -bottomLeftScreen.y;
        else if (topRightScreen.y > Screen.height) // Screen coords Y monte en bas
            adjustment.y = Screen.height - topRightScreen.y;


        // L'ajustement doit être converti de l'espace écran à l'espace mondial relatif au canvas
        // C'est plus simple de directement ajouter l'ajustement en screen space si le canvas est Screen Space Overlay
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // En Overlay, l'ajustement en pixels écran correspond directement
            // Il faut juste considérer le scale factor du canvas s'il y en a un (Scale with Screen Size)
            panelRectTransform.position += (adjustment / canvas.scaleFactor); // Ajustement direct en Screen Space
        }
        else // Pour Screen Space Camera ou World Space
        {
            // Convertir l'ajustement écran en déplacement mondial
            Vector3 worldAdjustment = Vector3.zero;
            if (eventData.pressEventCamera != null)
            {
                // On prend un point à l'écran, on le déplace de l'ajustement, et on voit la différence dans le monde
                Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, panelRectTransform.position);
                Vector3 adjustedScreenPoint = screenPoint + adjustment;

                Vector3 worldPoint;
                Vector3 adjustedWorldPoint;

                bool converted1 = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectTransform, screenPoint, eventData.pressEventCamera, out worldPoint);
                bool converted2 = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectTransform, adjustedScreenPoint, eventData.pressEventCamera, out adjustedWorldPoint);

                if (converted1 && converted2)
                {
                    worldAdjustment = adjustedWorldPoint - worldPoint;
                    panelRectTransform.position += worldAdjustment;
                }
                else
                {
                    // Fallback moins précis:
                    // Approximer en utilisant la taille de l'écran et du canvas
                    //  Rect canvasRect = canvasRectTransform.rect;
                    //  worldAdjustment.x = adjustment.x * (canvasRect.width / Screen.width);
                    //  worldAdjustment.y = adjustment.y * (canvasRect.height / Screen.height);
                    //  panelRectTransform.position += worldAdjustment;

                    // Ou simplement appliquer l'ajustement direct (peut être incorrect si caméra perspective)
                    panelRectTransform.position += adjustment; // Peut être imprécis
                }
            }
            else
            {
                panelRectTransform.position += adjustment; // Fallback si pas de caméra (cas étrange)
            }

        }

    }

}