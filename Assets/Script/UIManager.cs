// Fichier: Script unity/Script/UIManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    // Garde une référence directe si possible, sinon utilise FindObjectOfType
    public NetworkManager networkManager;
    public Button playButton;
    public TMP_InputField pseudoInput;

    private void Start()
    {
        // Essayer de trouver NetworkManager s'il n'est pas assigné
        if (networkManager == null)
        {
            // --- CORRECTION ICI ---
            networkManager = FindFirstObjectByType<NetworkManager>();
            // --- FIN CORRECTION ---
            if (networkManager == null)
            {
                Debug.LogError("[UIManager] NetworkManager non trouvé dans la scène !");
                if (playButton) playButton.interactable = false; // Désactiver le bouton si pas de NetworkManager
                return;
            }
        }

        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClick);
            Debug.Log("[UIManager] Listener ajouté au PlayButton.");
        }
        else
        {
            Debug.LogError("[UIManager] PlayButton non assigné dans l'inspecteur !");
        }

        // Log initial
        Debug.Log("[UIManager] Start() terminé.");
    }

    public void OnPlayButtonClick()
    {
        Debug.Log("[UIManager] OnPlayButtonClick CALLED."); // Log 1

        string pseudo = pseudoInput.text;
        if (string.IsNullOrWhiteSpace(pseudo)) // Utiliser IsNullOrWhiteSpace
        {
            Debug.LogWarning("[UIManager] Pseudo vide ou contient seulement des espaces.");
            // Afficher un message à l'utilisateur ici ?
            return;
        }

        // Vérifier si NetworkManager est prêt
        if (networkManager == null)
        {
            Debug.LogError("[UIManager] NetworkManager est null lors du clic !");
            return;
        }

        // Stocker le pseudo (PlayerData est statique, OK)
        PlayerData.pseudo = pseudo;
        Debug.Log($"[UIManager] Pseudo '{PlayerData.pseudo}' enregistré dans PlayerData."); // Log 2

        // Appeler JoinGame
        Debug.Log("[UIManager] Appel de networkManager.JoinGame..."); // Log 3
        networkManager.JoinGame(pseudo);
        Debug.Log("[UIManager] networkManager.JoinGame appelé."); // Log 4

        // Optionnel: Désactiver le bouton pour éviter double clic
        // if (playButton) playButton.interactable = false;
    }

    // Assure-toi de te désabonner si UIManager est détruit avant le bouton
    private void OnDestroy()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlayButtonClick);
        }
    }
}