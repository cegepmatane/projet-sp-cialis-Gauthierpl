using UnityEngine;
using TMPro;

public class GameSceneManager : MonoBehaviour
{
    public TextMeshProUGUI pseudoDisplay; // Assignez ce champ via l'inspecteur

    void Start()
    {
        // Afficher le pseudo stocké
        pseudoDisplay.text = "Bienvenue " + PlayerData.pseudo;
        Debug.Log("Affichage du pseudo dans la scène de jeu : " + PlayerData.pseudo);
    }
}
