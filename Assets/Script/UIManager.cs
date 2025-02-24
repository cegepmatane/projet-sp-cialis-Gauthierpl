using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public NetworkManager networkManager;
    public Button playButton;
    public TMP_InputField pseudoInput; // Champ pour saisir le pseudo

    private void Start()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClick);
        }
        else
        {
            Debug.LogError("PlayButton non assign� dans l'inspecteur !");
        }
    }

    public void OnPlayButtonClick()
    {
        // V�rifier que le pseudo n'est pas vide
        string pseudo = pseudoInput.text;
        if (string.IsNullOrEmpty(pseudo))
        {
            Debug.LogWarning("Veuillez saisir un pseudo.");
            return;
        }

        // Stocker le pseudo dans PlayerData
        PlayerData.pseudo = pseudo;
        Debug.Log("Pseudo enregistr� : " + PlayerData.pseudo);

        // Lancer la connexion et la redirection
        networkManager.JoinGame();
    }
}
