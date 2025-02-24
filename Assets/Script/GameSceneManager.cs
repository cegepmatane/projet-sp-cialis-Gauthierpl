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
        // Si la liste a d�j� �t� re�ue avant que cette sc�ne ne soit charg�e, l'afficher
        if (NetworkManager.lastPlayersList != null && NetworkManager.lastPlayersList.Count > 0)
        {
            UpdatePlayersList(NetworkManager.lastPlayersList);
        }
    }

    public void UpdatePlayersList(List<string> players)
    {
        // Affiche chaque pseudo sur une nouvelle ligne
        playersListText.text = "Joueurs connect�s :\n" + string.Join("\n", players);
        Debug.Log("Mise � jour de la liste des joueurs : " + playersListText.text);
    }
}
