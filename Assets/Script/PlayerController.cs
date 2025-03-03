using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public string playerId;
    public string pseudo;
    public float speed = 5f;
    private bool isLocalPlayer = false;

    // Optionnel : référence à un TextMeshPro pour afficher le pseudo au-dessus du joueur
    public TextMeshProUGUI pseudoLabel;

    // Retire le mouvement depuis Update() et place-le dans FixedUpdate()
    void FixedUpdate()
    {
        // Si c'est le joueur local, on gère son déplacement et on envoie sa position au serveur
        if (isLocalPlayer)
        {
            // Récupère l'input horizontal
            float move = Input.GetAxis("Horizontal") * speed * Time.fixedDeltaTime;
            // Déplace le joueur localement
            transform.Translate(move, 0, 0);

            // Envoie la nouvelle position à chaque FixedUpdate (50 fois/s par défaut)
            NetworkManager.Instance.SendPlayerMove(transform.position.x);
        }
    }

    public void SetPseudo(string pseudo)
    {
        this.pseudo = pseudo;
        if (pseudoLabel != null)
        {
            pseudoLabel.text = pseudo;
        }
    }

    public void SetAsLocalPlayer()
    {
        isLocalPlayer = true;
    }
}
