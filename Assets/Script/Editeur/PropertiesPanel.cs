using UnityEngine;
using TMPro;

public class PropertiesPanel : MonoBehaviour
{
    public TMP_InputField posXInput;
    public TMP_InputField posYInput;
    public TMP_InputField rotationZInput;
    public TMP_InputField widthInput;
    public TMP_InputField heightInput;

    private PlaceableObject currentTarget;
    private EditorManager editorManager;
    private bool isUpdatingFromCode = false;

    void Start()
    {
        editorManager = FindObjectOfType<EditorManager>();

        // --- Changement ici: onValueChanged ---
        if (posXInput) posXInput.onValueChanged.AddListener(OnPosXChanged);
        if (posYInput) posYInput.onValueChanged.AddListener(OnPosYChanged);
        if (rotationZInput) rotationZInput.onValueChanged.AddListener(OnRotationZChanged);
        if (widthInput) widthInput.onValueChanged.AddListener(OnWidthChanged);
        if (heightInput) heightInput.onValueChanged.AddListener(OnHeightChanged);
        // --------------------------------------
    }

    public void DisplayObjectProperties(PlaceableObject target)
    {
        currentTarget = target;
        if (currentTarget == null)
        {
            ClearProperties();
            gameObject.SetActive(false);
            return;
        }

        isUpdatingFromCode = true;
        UpdateFieldsFromTarget(); // Extrait dans une m�thode pour r�utilisation
        isUpdatingFromCode = false;
        gameObject.SetActive(true);
    }

    // Met � jour seulement l'affichage de la position (appel� pendant le drag)
    public void UpdatePositionDisplay(Vector3 pos)
    {
        isUpdatingFromCode = true;
        if (posXInput) posXInput.text = pos.x.ToString("F2");
        if (posYInput) posYInput.text = pos.y.ToString("F2");
        isUpdatingFromCode = false;
    }

    // Met � jour seulement l'affichage de la rotation (appel� pendant le scroll)
    public void UpdateRotationDisplay(float rotZ)
    {
        isUpdatingFromCode = true;
        if (rotationZInput) rotationZInput.text = rotZ.ToString("F1");
        isUpdatingFromCode = false;
    }

    // Met � jour tous les champs depuis la cible
    private void UpdateFieldsFromTarget()
    {
        if (currentTarget == null) return;
        Vector3 pos = currentTarget.transform.position;
        float rotZ = currentTarget.transform.eulerAngles.z;
        Vector3 scale = currentTarget.transform.localScale;

        if (posXInput) posXInput.text = pos.x.ToString("F2");
        if (posYInput) posYInput.text = pos.y.ToString("F2");
        if (rotationZInput) rotationZInput.text = rotZ.ToString("F1");
        if (widthInput) widthInput.text = scale.x.ToString("F2");
        if (heightInput) heightInput.text = scale.y.ToString("F2");
    }

    public void ClearProperties()
    {
        currentTarget = null;
        isUpdatingFromCode = true;
        if (posXInput) posXInput.text = "";
        if (posYInput) posYInput.text = "";
        if (rotationZInput) rotationZInput.text = "";
        if (widthInput) widthInput.text = "";
        if (heightInput) heightInput.text = "";
        isUpdatingFromCode = false;
    }

    private void OnPosXChanged(string value) => ApplyChanges();
    private void OnPosYChanged(string value) => ApplyChanges();
    private void OnRotationZChanged(string value) => ApplyChanges();
    private void OnWidthChanged(string value) => ApplyChanges();
    private void OnHeightChanged(string value) => ApplyChanges();

    private void ApplyChanges()
    {
        if (isUpdatingFromCode || currentTarget == null || editorManager == null) return;

        // M�mes logiques de parsing et validation qu'avant
        float posX = TryParseFloat(posXInput.text, currentTarget.transform.position.x);
        float posY = TryParseFloat(posYInput.text, currentTarget.transform.position.y);
        float rotZ = TryParseFloat(rotationZInput.text, currentTarget.transform.eulerAngles.z);
        float width = Mathf.Max(0.1f, TryParseFloat(widthInput.text, currentTarget.transform.localScale.x));
        float height = Mathf.Max(0.1f, TryParseFloat(heightInput.text, currentTarget.transform.localScale.y));

        // Notifier l'EditorManager
        editorManager.UpdateSelectedObjectTransformFromUI(new Vector2(posX, posY), rotZ, new Vector2(width, height));

        // On ne remet pas � jour l'UI ici pour �viter les conflits avec la frappe de l'utilisateur
    }

    private float TryParseFloat(string input, float defaultValue)
    {
        // Important : Utiliser CultureInfo.InvariantCulture pour que le "." soit toujours le s�parateur d�cimal
        if (float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }
        // Si l'utilisateur efface le champ, on retourne la valeur par d�faut (ou 0?)
        // Peut-�tre retourner defaultValue si l'input est vide?
        if (string.IsNullOrEmpty(input)) return defaultValue; // Conserve l'ancienne valeur si champ vid�
        return defaultValue; // Conserve l'ancienne valeur si parsing �choue
    }

    void OnDestroy()
    {
        // --- Changement ici: onValueChanged ---
        if (posXInput) posXInput.onValueChanged.RemoveListener(OnPosXChanged);
        if (posYInput) posYInput.onValueChanged.RemoveListener(OnPosYChanged);
        if (rotationZInput) rotationZInput.onValueChanged.RemoveListener(OnRotationZChanged);
        if (widthInput) widthInput.onValueChanged.RemoveListener(OnWidthChanged);
        if (heightInput) heightInput.onValueChanged.RemoveListener(OnHeightChanged);
        // --------------------------------------
    }
}