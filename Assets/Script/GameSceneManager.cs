using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class GameSceneManager : MonoBehaviour
{
    public TextMeshProUGUI playersListText; // Assignez ce champ dans l'inspecteur

    private void OnEnable()
    {
        NetworkManager.OnPlayersListUpdated += UpdatePlayersList;
    }

    private void OnDisable()
    {
        NetworkManager.OnPlayersListUpdated -= UpdatePlayersList;
    }

    void Start()
    {
        // Si la liste a déjà été reçue avant que cette scène ne soit chargée, l'afficher
        if (NetworkManager.lastPlayersList != null && NetworkManager.lastPlayersList.Count > 0)
        {
            UpdatePlayersList(NetworkManager.lastPlayersList);
        }
    }

    public void UpdatePlayersList(List<string> players)
    {
        // Affiche chaque pseudo sur une nouvelle ligne
        playersListText.text = "Joueurs connectés :\n" + string.Join("\n", players);
        Debug.Log("Mise à jour de la liste des joueurs : " + playersListText.text);
    }
}
