// Fichier: Script unity/Script/UIManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    // Garde une r�f�rence directe si possible, sinon utilise FindObjectOfType
    public NetworkManager networkManager;
    public Button playButton;
    public TMP_InputField pseudoInput;

    private void Start()
    {
        // Essayer de trouver NetworkManager s'il n'est pas assign�
        if (networkManager == null)
        {
            // --- CORRECTION ICI ---
            networkManager = FindFirstObjectByType<NetworkManager>();
            // --- FIN CORRECTION ---
            if (networkManager == null)
            {
                Debug.LogError("[UIManager] NetworkManager non trouv� dans la sc�ne !");
                if (playButton) playButton.interactable = false; // D�sactiver le bouton si pas de NetworkManager
                return;
            }
        }

        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClick);
            Debug.Log("[UIManager] Listener ajout� au PlayButton.");
        }
        else
        {
            Debug.LogError("[UIManager] PlayButton non assign� dans l'inspecteur !");
        }

        // Log initial
        Debug.Log("[UIManager] Start() termin�.");
    }

    public void OnPlayButtonClick()
    {
        Debug.Log("[UIManager] OnPlayButtonClick CALLED."); // Log 1

        string pseudo = pseudoInput.text;
        if (string.IsNullOrWhiteSpace(pseudo)) // Utiliser IsNullOrWhiteSpace
        {
            Debug.LogWarning("[UIManager] Pseudo vide ou contient seulement des espaces.");
            // Afficher un message � l'utilisateur ici ?
            return;
        }

        // V�rifier si NetworkManager est pr�t
        if (networkManager == null)
        {
            Debug.LogError("[UIManager] NetworkManager est null lors du clic !");
            return;
        }

        // Stocker le pseudo (PlayerData est statique, OK)
        PlayerData.pseudo = pseudo;
        Debug.Log($"[UIManager] Pseudo '{PlayerData.pseudo}' enregistr� dans PlayerData."); // Log 2

        // Appeler JoinGame
        Debug.Log("[UIManager] Appel de networkManager.JoinGame..."); // Log 3
        networkManager.JoinGame(pseudo);
        Debug.Log("[UIManager] networkManager.JoinGame appel�."); // Log 4

        // Optionnel: D�sactiver le bouton pour �viter double clic
        // if (playButton) playButton.interactable = false;
    }

    // Assure-toi de te d�sabonner si UIManager est d�truit avant le bouton
    private void OnDestroy()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlayButtonClick);
        }
    }
}