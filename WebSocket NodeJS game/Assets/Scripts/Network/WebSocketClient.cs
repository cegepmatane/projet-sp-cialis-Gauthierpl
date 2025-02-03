using UnityEngine;

public class WebSocketClient : MonoBehaviour
{
    // Exemple de traitement des messages du serveur
    public void ProcessMessage(string message)
    {
        // Traitement du message JSON re�u (par exemple, changement de carte)
        Debug.Log("Message trait� : " + message);

        // Impl�menter ici la logique de traitement des donn�es re�ues
        // Par exemple, si le message contient un changement de carte, appelle la fonction pour changer la carte
        if (message.Contains("mapChanged"))
        {
            ChangeMap();
        }
    }

    private void ChangeMap()
    {
        Debug.Log("La carte a chang� !");
        // Impl�menter la logique pour changer la carte dans le jeu ici
    }
}
