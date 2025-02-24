using UnityEngine;
using TMPro;

public class GameSceneManager : MonoBehaviour
{
    public TextMeshProUGUI pseudoDisplay; // Assignez ce champ via l'inspecteur

    void Start()
    {
        // Afficher le pseudo stock�
        pseudoDisplay.text = "Bienvenue " + PlayerData.pseudo;
        Debug.Log("Affichage du pseudo dans la sc�ne de jeu : " + PlayerData.pseudo);
    }
}
