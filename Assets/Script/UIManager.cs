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
            Debug.LogError("PlayButton non assigné dans l'inspecteur !");
        }
    }

    public void OnPlayButtonClick()
    {
        string pseudo = pseudoInput.text;
        if (string.IsNullOrEmpty(pseudo))
        {
            Debug.LogWarning("Veuillez saisir un pseudo.");
            return;
        }

        // Stocker le pseudo si besoin (ex. via PlayerData)
        PlayerData.pseudo = pseudo;
        Debug.Log("Pseudo enregistré : " + PlayerData.pseudo);

        // Appeler JoinGame en envoyant le pseudo
        networkManager.JoinGame(pseudo);
    }
}
